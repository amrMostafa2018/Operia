using System.Globalization;
using System.Text.Json;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Customers.Queries.GetCustomerActionHistory;

public sealed class GetCustomerActionHistoryHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IBranchScope branchScope)
    : IRequestHandler<GetCustomerActionHistoryQuery, CustomerActionHistoryPage>
{
    private const int PageSize = 10;

    public async Task<CustomerActionHistoryPage> Handle(
        GetCustomerActionHistoryQuery request, CancellationToken token)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var allowed = await branchScope.GetAllowedBranchIdsAsync(
            currentUser.UserId ?? string.Empty, token);
        var purchaseId = request.SaleOnly ? null : request.OperationId;
        var saleId = request.SaleOnly
            ? await db.Sales.AsNoTracking().Where(x => x.TenantId == tenantId &&
                x.Id == request.OperationId && x.CustomerId == request.CustomerId &&
                allowed.Contains(x.BranchId)).Select(x => x.Id).SingleOrDefaultAsync(token)
            : await (from purchase in db.CustomerPackages.AsNoTracking()
                     join sale in db.Sales.AsNoTracking() on purchase.SaleId equals sale.Id
                     where purchase.TenantId == tenantId && sale.TenantId == tenantId &&
                           purchase.Id == purchaseId &&
                           purchase.CustomerId == request.CustomerId &&
                           sale.CustomerId == request.CustomerId &&
                           allowed.Contains(sale.BranchId)
                     select sale.Id).SingleOrDefaultAsync(token);
        if (saleId is null)
        {
            throw ApiNotFoundException.FromCode(request.SaleOnly
                ? ApiErrorCodes.Sales.NotFound : ApiErrorCodes.Customers.PurchaseNotFound);
        }

        var refundIds = purchaseId is null ? Array.Empty<string>() : await db.RefundTransactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerId == request.CustomerId &&
                        x.CustomerPackageId == purchaseId)
            .Select(x => x.Id).ToArrayAsync(token);
        var saleCollectionIds = await db.SaleCollections.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SaleId == saleId)
            .Select(x => x.Id).ToArrayAsync(token);

        IQueryable<AuditLog> events = db.AuditLogs.AsNoTracking().Where(x =>
            x.TenantId == tenantId &&
            (x.EntityType == nameof(Customer) && x.EntityId == request.CustomerId &&
                (x.Action == AuditActions.CustomerCreated ||
                 x.Action == AuditActions.CustomerBlocked ||
                 x.Action == AuditActions.CustomerUnblocked ||
                 x.Action == AuditActions.CustomerBlockReasonChanged) ||
             x.EntityType == nameof(CustomerPackage) && x.EntityId == purchaseId &&
                (x.Action == AuditActions.PurchaseCancelled ||
                 x.Action == AuditActions.InstallmentCollected) ||
             x.EntityType == nameof(RefundTransaction) && refundIds.Contains(x.EntityId) &&
                x.Action == AuditActions.RefundPosted ||
             x.EntityType == nameof(SaleCollection) && saleCollectionIds.Contains(x.EntityId) &&
                (x.Action == AuditActions.RevenuePaymentSubmitted ||
                 x.Action == AuditActions.RevenuePaymentConfirmed ||
                 x.Action == AuditActions.RevenuePaymentCancelled) ||
             x.EntityType == nameof(Sale) && x.EntityId == saleId &&
                (x.Action == AuditActions.SaleConfirmed ||
                 x.Action == AuditActions.SalePaymentConfirmed ||
                 x.Action == AuditActions.SaleCollectionAttempted)));

        var total = await events.CountAsync(token);
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var previous = await events.Where(x => x.Id == request.Cursor)
                .Select(x => new { x.Id, x.CreatedAt }).SingleOrDefaultAsync(token);
            if (previous is null)
            {
                throw new ValidationException([
                    new ValidationFailure("cursor", ApiErrorCodes.Customers.HistoryCursorInvalid)
                    {
                        ErrorCode = ApiErrorCodes.Customers.HistoryCursorInvalid
                    }
                ]);
            }
            events = events.Where(x => x.CreatedAt < previous.CreatedAt ||
                x.CreatedAt == previous.CreatedAt && string.Compare(x.Id, previous.Id) < 0);
        }

        var page = await events.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(PageSize + 1)
            .Select(x => new
            {
                x.Id,
                x.CreatedAt,
                x.Action,
                x.UserDisplayName,
                x.UserId,
                x.EntityId,
                x.DetailsJson
            }).ToListAsync(token);
        var hasMore = page.Count > PageSize;
        var visible = page.Take(PageSize).ToArray();
        return new CustomerActionHistoryPage(total,
            hasMore ? visible[^1].Id : null,
            visible.Select(x => Map(x.Id, x.CreatedAt, x.Action,
                x.UserDisplayName ?? x.UserId, x.EntityId,
                x.DetailsJson, purchaseId ?? saleId)).ToArray());
    }

    private static CustomerActionHistoryRow Map(string id, DateTime atUtc, string action,
        string? actor, string entityId, string? detailsJson,
        string purchaseId)
    {
        using var details = string.IsNullOrWhiteSpace(detailsJson)
            ? JsonDocument.Parse("{}") : JsonDocument.Parse(detailsJson);
        var root = details.RootElement;
        string? oldValue = null;
        string? newValue = null;
        string? reason = null;
        string? oldReason = null;
        string? newReason = null;
        decimal? amount = null;
        var referenceType = "purchase";
        var referenceId = purchaseId;

        switch (action)
        {
            case AuditActions.CustomerCreated:
                referenceType = "customer";
                referenceId = entityId;
                newValue = "created";
                break;
            case AuditActions.CustomerBlocked:
            case AuditActions.CustomerUnblocked:
            case AuditActions.CustomerBlockReasonChanged:
                referenceType = "customer";
                referenceId = entityId;
                oldValue = BooleanState(root, "oldState");
                newValue = BooleanState(root, "newState");
                oldReason = String(root, "oldReason");
                newReason = String(root, "newReason");
                break;
            case AuditActions.PurchaseCancelled:
                oldValue = String(root, "oldStatus");
                newValue = String(root, "newStatus");
                break;
            case AuditActions.RefundPosted:
                referenceType = "refund";
                referenceId = entityId;
                amount = Decimal(root, "amount");
                reason = String(root, "note");
                var previousRefunds = Decimal(root, "previousRefunds");
                if (previousRefunds is not null && amount is not null)
                {
                    oldValue = previousRefunds.Value.ToString(CultureInfo.InvariantCulture);
                    newValue = (previousRefunds.Value + amount.Value)
                        .ToString(CultureInfo.InvariantCulture);
                }
                break;
            case AuditActions.InstallmentCollected:
                referenceType = "payment";
                referenceId = String(root, "id") ?? entityId;
                amount = Decimal(root, "amount");
                oldValue = Decimal(root, "oldRemaining")?.ToString(CultureInfo.InvariantCulture);
                newValue = Decimal(root, "remaining")?.ToString(CultureInfo.InvariantCulture);
                break;
            case AuditActions.SaleConfirmed:
                referenceType = "sale";
                referenceId = entityId;
                newValue = String(root, "newStatus");
                break;
            case AuditActions.SalePaymentConfirmed:
            case AuditActions.SaleCollectionAttempted:
                referenceType = "payment";
                referenceId = String(root, "id") ?? entityId;
                var confirmed = Boolean(root, "confirmed");
                action = confirmed ? AuditActions.SalePaymentConfirmed
                    : AuditActions.SaleCollectionAttempted;
                amount = confirmed ? Decimal(root, "amount") : null;
                oldValue = String(root, "oldStatus");
                newValue = String(root, "newStatus");
                break;
            case AuditActions.RevenuePaymentSubmitted:
            case AuditActions.RevenuePaymentConfirmed:
            case AuditActions.RevenuePaymentCancelled:
                referenceType = "payment";
                referenceId = entityId;
                amount = action == AuditActions.RevenuePaymentConfirmed
                    ? Decimal(root, "amount") : null;
                oldValue = String(root, "oldStatus");
                newValue = String(root, "newStatus");
                break;
        }

        return new CustomerActionHistoryRow(id, atUtc, action, actor, oldValue,
            newValue, amount, reason, oldReason, newReason, referenceType, referenceId);
    }

    private static string? String(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static decimal? Decimal(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDecimal() : null;

    private static bool Boolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? BooleanState(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) &&
        (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean() ? "blocked" : "unblocked" : null;
}
