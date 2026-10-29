using System.Text.Json;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Revenue.Queries.GetActivityRevenueReport;

public sealed class GetActivityRevenueReportHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IDateTimeProvider clock)
    : IRequestHandler<GetActivityRevenueReportQuery, ActivityRevenueReport>
{
    public async Task<ActivityRevenueReport> Handle(
        GetActivityRevenueReportQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var ledger = await new RevenueLedger(db, currentUser, branchScope)
            .ScopedAsync(request.BranchId, cancellationToken);
        var periods = new RevenuePeriods(db, currentUser, clock);
        var range = await periods.ReportAsync(request.FromDate, request.ToDate, cancellationToken);
        var current = await periods.CurrentAsync(cancellationToken);
        if (request.ServiceId is not null && !await db.Packages.AsNoTracking().AnyAsync(
                x => x.TenantId == tenantId && x.Id == request.ServiceId,
                cancellationToken))
        {
            throw new ValidationException([
                new ValidationFailure("serviceId", ApiErrorCodes.Revenue.ServiceInvalid)
                { ErrorCode = ApiErrorCodes.Revenue.ServiceInvalid }
            ]);
        }

        var saleRows = await (
            from collection in ledger.SaleAttempts
            join sale in db.Sales.AsNoTracking() on collection.SaleId equals sale.Id
            join customer in db.Customers.AsNoTracking() on sale.CustomerId equals customer.Id
            join branch in db.Branches.AsNoTracking() on sale.BranchId equals branch.Id
            where sale.TenantId == tenantId && customer.TenantId == tenantId &&
                  branch.TenantId == tenantId &&
                  (collection.ConfirmedAtUtc ?? collection.AttemptedAtUtc) >= range.FromUtc &&
                  (collection.ConfirmedAtUtc ?? collection.AttemptedAtUtc) < range.ToUtc
            select new
            {
                collection.Id,
                collection.Amount,
                collection.PaymentMethod,
                collection.Confirmed,
                collection.ReviewStatus,
                HasProof = collection.ProofContent != null,
                collection.AttemptedAtUtc,
                collection.ConfirmedAtUtc,
                sale.IdempotencyKey,
                SaleId = sale.Id,
                sale.ItemsSnapshotJson,
                CustomerName = customer.FullName,
                Mobile = customer.MobileNumber,
                Branch = branch.Name
            }).ToListAsync(cancellationToken);

        var installmentRows = await (
            from collection in ledger.Installments
            join purchase in db.CustomerPackages.AsNoTracking()
                on collection.CustomerPackageId equals purchase.Id
            join sale in db.Sales.AsNoTracking() on purchase.SaleId equals sale.Id
            join customer in db.Customers.AsNoTracking() on purchase.CustomerId equals customer.Id
            join branch in db.Branches.AsNoTracking() on sale.BranchId equals branch.Id
            where purchase.TenantId == tenantId && sale.TenantId == tenantId &&
                  customer.TenantId == tenantId && branch.TenantId == tenantId &&
                  collection.ConfirmedAtUtc >= range.FromUtc &&
                  collection.ConfirmedAtUtc < range.ToUtc
            select new
            {
                collection.Id,
                collection.Amount,
                collection.PaymentMethod,
                collection.ConfirmedAtUtc,
                collection.CustomerPackageId,
                purchase.PackageId,
                purchase.PurchaseSnapshotJson,
                SaleId = sale.Id,
                CustomerName = customer.FullName,
                Mobile = customer.MobileNumber,
                Branch = branch.Name
            }).ToListAsync(cancellationToken);

        var refundRows = await (
            from refund in ledger.Refunds
            join purchase in db.CustomerPackages.AsNoTracking()
                on refund.CustomerPackageId equals purchase.Id
            join sale in db.Sales.AsNoTracking() on refund.SaleId equals sale.Id
            join customer in db.Customers.AsNoTracking() on refund.CustomerId equals customer.Id
            join branch in db.Branches.AsNoTracking() on sale.BranchId equals branch.Id
            where purchase.TenantId == tenantId && sale.TenantId == tenantId &&
                  customer.TenantId == tenantId && branch.TenantId == tenantId &&
                  refund.RecordedAtUtc >= range.FromUtc &&
                  refund.RecordedAtUtc < range.ToUtc
            select new
            {
                refund.Id,
                refund.Amount,
                refund.RecordedAtUtc,
                refund.CustomerPackageId,
                purchase.PackageId,
                purchase.PurchaseSnapshotJson,
                SaleId = sale.Id,
                CustomerName = customer.FullName,
                Mobile = customer.MobileNumber,
                Branch = branch.Name
            }).ToListAsync(cancellationToken);

        var rows = new List<RevenueTransactionRow>(
            saleRows.Count + installmentRows.Count + refundRows.Count);
        foreach (var row in saleRows)
        {
            var items = JsonSerializer.Deserialize<ConfirmedSaleItemSnapshot[]>(
                row.ItemsSnapshotJson ?? "[]") ?? [];
            if (request.ServiceId is not null && !items.Any(x => x.OfferId == request.ServiceId))
            {
                continue;
            }
            rows.Add(new RevenueTransactionRow(
                $"PAY-{row.Id}", "SaleCollection", row.ConfirmedAtUtc ?? row.AttemptedAtUtc,
                row.CustomerName, row.Mobile,
                string.Join(", ", items.Select(x => x.Name)), row.Branch,
                row.Amount, row.PaymentMethod,
                row.ReviewStatus ?? (row.Confirmed ? "Paid" : "Failed"), row.SaleId,
                null, row.Id, null, row.HasProof));
        }

        foreach (var row in installmentRows)
        {
            if (request.ServiceId is not null && row.PackageId != request.ServiceId)
            {
                continue;
            }
            rows.Add(new RevenueTransactionRow(
                $"INS-{row.Id}", "Installment", row.ConfirmedAtUtc,
                row.CustomerName, row.Mobile,
                PackagePurchaseSnapshot.FromJson(row.PurchaseSnapshotJson)?.OfferName ?? string.Empty,
                row.Branch, row.Amount, row.PaymentMethod, "Paid", row.SaleId,
                row.CustomerPackageId, row.Id, null, false));
        }

        foreach (var row in refundRows)
        {
            if (request.ServiceId is not null && row.PackageId != request.ServiceId)
            {
                continue;
            }
            rows.Add(new RevenueTransactionRow(
                $"REF-{row.Id}", "Refund", row.RecordedAtUtc,
                row.CustomerName, row.Mobile,
                PackagePurchaseSnapshot.FromJson(row.PurchaseSnapshotJson)?.OfferName ?? string.Empty,
                row.Branch, -row.Amount, string.Empty, "Refunded", row.SaleId,
                row.CustomerPackageId, null, row.Id, false));
        }

        var ordered = rows.OrderByDescending(x => x.TransactionAtUtc)
            .ThenByDescending(x => x.TransactionId, StringComparer.Ordinal).ToArray();
        var paid = ordered.Where(x => x.Status == "Paid").ToArray();
        var gross = paid.Sum(x => x.Amount);
        var refunds = -ordered.Where(x => x.Kind == "Refund").Sum(x => x.Amount);
        var page = Math.Clamp(request.Page, 1, 100_000);
        var pageSize = request.AllRows ? ordered.Length : Math.Clamp(request.PageSize, 1, 100);
        return new ActivityRevenueReport(range.FromDate, range.ToDate, range.TimezoneId,
            ordered.Length,
            page, pageSize, new RevenueReportTotals(gross, refunds, gross - refunds),
            Card(paid, current.TodayFromUtc, current.TodayToUtc),
            Card(paid, current.MonthFromUtc, current.MonthToUtc),
            request.AllRows ? ordered : ordered.Skip((page - 1) * pageSize)
                .Take(pageSize).ToArray());
    }

    private static RevenueReportCard Card(
        IEnumerable<RevenueTransactionRow> rows, DateTime from, DateTime to)
    {
        var matching = rows.Where(x => x.TransactionAtUtc >= from && x.TransactionAtUtc < to)
            .ToArray();
        return new RevenueReportCard(matching.Length, matching.Sum(x => x.Amount));
    }
}
