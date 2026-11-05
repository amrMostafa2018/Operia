using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Customers.Queries.ListCustomerOperations;

/// <summary>The shared, branch-scoped ledger projection used by the grid and its export.</summary>
public sealed class CustomerOperationsReader(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IDateTimeProvider clock)
{
    public async Task<IReadOnlyList<CustomerOperationDto>> ReadAsync(
        CustomerOperationsFilter filter,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var branches = await branchScope.GetAllowedBranchIdsAsync(
            currentUser.UserId ?? string.Empty, cancellationToken);
        var sales = from sale in db.Sales.AsNoTracking()
                    join customer in db.Customers.AsNoTracking() on sale.CustomerId equals customer.Id
                    join branch in db.Branches.AsNoTracking() on sale.BranchId equals branch.Id
                    where sale.TenantId == tenantId && customer.TenantId == tenantId &&
                          branch.TenantId == tenantId && branches.Contains(sale.BranchId)
                    select new
                    {
                        SaleId = sale.Id,
                        sale.CreatedAt,
                        ItemsJson = sale.ItemsSnapshotJson,
                        sale.Status,
                        CustomerId = customer.Id,
                        CustomerName = customer.FullName,
                        Mobile = customer.MobileNumber,
                        CustomerActive = customer.IsActive,
                        BranchName = branch.Name
                    };

        if (filter.FromDate is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            sales = sales.Where(x => x.CreatedAt >= start);
        }
        if (filter.ToDate is { } to && to != DateOnly.MaxValue)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            sales = sales.Where(x => x.CreatedAt < end);
        }
        if (!string.IsNullOrWhiteSpace(filter.Customer))
        {
            var name = filter.Customer.Trim();
            sales = sales.Where(x => x.CustomerName.Contains(name));
        }
        if (!string.IsNullOrWhiteSpace(filter.Mobile))
        {
            var mobile = filter.Mobile.Trim();
            sales = sales.Where(x => x.Mobile.Contains(mobile));
        }

        var scopedSaleData = await sales.OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.SaleId).ToListAsync(cancellationToken);
        var scopedSales = scopedSaleData.Select(x => new SaleSource(
            x.SaleId, x.CreatedAt, x.ItemsJson, x.Status, x.CustomerId,
            x.CustomerName, x.Mobile, x.CustomerActive, x.BranchName)).ToList();
        if (scopedSales.Count == 0)
        {
            return [];
        }

        var saleIds = scopedSales.Select(x => x.Id).ToArray();
        var purchases = await db.CustomerPackages.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SaleId != null && saleIds.Contains(x.SaleId))
            .Select(x => new PurchaseSource(
                x.Id, x.SaleId!, x.SaleItemIndex, x.PurchasePrice, x.AmountDue,
                x.PurchaseSnapshotJson, x.IsActive, x.ExpiresOn, x.Total, x.Used))
            .ToListAsync(cancellationToken);
        var purchaseLookup = purchases
            .Where(x => x.ItemIndex.HasValue)
            .ToLookup(x => (x.SaleId, x.ItemIndex!.Value));
        var confirmedIds = await db.SaleCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && saleIds.Contains(x.SaleId) && x.Confirmed)
            .Select(x => x.SaleId).Distinct().ToArrayAsync(cancellationToken);
        var confirmed = confirmedIds.ToHashSet();
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var rows = new List<CustomerOperationDto>();

        foreach (var sale in scopedSales)
        {
            var lines = JsonSerializer.Deserialize<ConfirmedSaleItemSnapshot[]>(sale.ItemsJson) ?? [];
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (line.PaymentOption == "owned")
                {
                    continue;
                }
                var linked = purchaseLookup[(sale.Id, index)].ToArray();
                if (linked.Length == 0)
                {
                    var collected = confirmed.Contains(sale.Id) ? line.DueNow : 0m;
                    rows.Add(CreateRow(sale, line, $"{sale.Id}:{index}", line.FinalAmount,
                        collected, null, today));
                }
                else
                {
                    foreach (var purchase in linked)
                    {
                        var total = purchase.PurchasePrice ?? line.FinalAmount / linked.Length;
                        var collected = Math.Clamp(total - (purchase.AmountDue ?? total), 0m, total);
                        rows.Add(CreateRow(sale, line, purchase.Id, total, collected,
                            purchase, today));
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Service))
        {
            var service = filter.Service.Trim();
            rows = rows.Where(x => x.Service.Contains(service, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (filter.ServiceType is not null)
        {
            rows = rows.Where(x => x.ServiceType == filter.ServiceType).ToList();
        }
        if (filter.Status is not null)
        {
            rows = rows.Where(x => x.PurchaseStatus == filter.Status).ToList();
        }
        if (filter.PaymentType is not null)
        {
            rows = rows.Where(x => x.PaymentType == filter.PaymentType).ToList();
        }
        return rows;
    }

    private static CustomerOperationDto CreateRow(
        SaleSource sale,
        ConfirmedSaleItemSnapshot line,
        string id,
        decimal total,
        decimal collected,
        PurchaseSource? purchase,
        DateOnly today)
    {
        var remaining = Math.Max(0m, total - collected);
        var status = purchase is null ? sale.Status == "PendingCollection" ? "pending" : "completed"
            : !purchase.IsActive ? "cancelled"
            : purchase.ExpiresOn is { } expiry && expiry < today ? "expired"
            : purchase.Total > 0 && purchase.Used >= purchase.Total ? "completed" : "active";
        var paymentStatus = total == 0m || remaining == 0m ? "fullyPaid"
            : collected > 0m ? "partial" : "unpaid";
        var snapshot = PackagePurchaseSnapshot.FromJson(purchase?.SnapshotJson);
        var installmentStatus = InstallmentStatus(snapshot, remaining, collected, purchase?.Used ?? 0);
        return new CustomerOperationDto(
            id, sale.Id, sale.CustomerId, sale.CustomerName, sale.Mobile,
            sale.BranchName, line.Name,
            line.OfferType == "package" ? "package" : "session", sale.CreatedAt,
            line.PaymentOption, total, collected, remaining,
            purchase?.Total, purchase is null ? null : Math.Max(0, purchase.Total - purchase.Used),
            sale.CustomerActive ? "active" : "blocked", status, paymentStatus,
            installmentStatus);
    }

    private static string InstallmentStatus(
        PackagePurchaseSnapshot? snapshot,
        decimal remaining,
        decimal collected,
        int used)
    {
        if (snapshot?.PaymentOption != "installment")
        {
            return "none";
        }
        if (remaining == 0m)
        {
            return "paid";
        }

        var ordered = snapshot.Installments.OrderBy(x => x.Number).ToArray();
        var cumulative = snapshot.FirstPayment ?? 0m;
        for (var index = 0; index < ordered.Length; index++)
        {
            cumulative += ordered[index].Amount;
            if (collected < cumulative)
            {
                return index == 0 || used >= ordered[index - 1].CumulativeAvailableSessions
                    ? "due" : "scheduled";
            }
        }
        return "due";
    }

    private sealed record SaleSource(
        string Id, DateTime CreatedAt, string ItemsJson, string Status,
        string CustomerId, string CustomerName, string Mobile,
        bool CustomerActive, string BranchName);

    private sealed record PurchaseSource(
        string Id, string SaleId, int? ItemIndex, decimal? PurchasePrice,
        decimal? AmountDue, string? SnapshotJson, bool IsActive, DateOnly? ExpiresOn,
        int Total, int Used);
}
