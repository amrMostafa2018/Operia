using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages;
using Operia.Application.Sales.Queries.QuoteSale;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Sales.Commands.ConfirmSale;

/// <summary>Confirms immutable sale terms without asserting a payment or booking succeeded.</summary>
public sealed class ConfirmSaleHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IMediator mediator,
    IAuditWriter auditWriter) : IRequestHandler<ConfirmSaleCommand, ConfirmSaleResult>
{
    public async Task<ConfirmSaleResult> Handle(ConfirmSaleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        if (request.DirectCustomer && !string.IsNullOrWhiteSpace(request.CustomerId))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Sales.DirectCustomerSelectionInvalid,
                "customerId");
        }
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new
            {
                request.CustomerId,
                request.DirectCustomer,
                request.BranchId,
                request.Items,
                request.PaymentMethod,
                request.ExpectedGrandTotal,
                request.ExpectedDueNow
            }))));

        var prior = await db.Sales.AsNoTracking().SingleOrDefaultAsync(sale =>
            sale.TenantId == tenantId && sale.IdempotencyKey == request.IdempotencyKey,
            cancellationToken);
        if (prior is not null)
        {
            if (prior.RequestHash != requestHash)
            {
                throw ConflictException.FromCode(ApiErrorCodes.Sales.IdempotencyConflict);
            }
            return new ConfirmSaleResult(prior.Id, prior.Status, prior.GrandTotal, prior.DueNow);
        }

        var quote = await mediator.Send(
            new QuoteSaleQuery(request.CustomerId, request.BranchId, request.Items,
                request.DirectCustomer),
            cancellationToken);
        if (quote.GrandTotal != request.ExpectedGrandTotal || quote.DueNow != request.ExpectedDueNow)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Sales.QuoteChanged, "items");
        }

        var method = request.PaymentMethod?.Trim().ToLowerInvariant();
        if (quote.DueNow > 0)
        {
            var enabled = await BookingPaymentMethods.GetEnabledAsync(db, tenantId, cancellationToken);
            if (method is null || !enabled.Contains(method))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Sales.PaymentMethodUnavailable, "paymentMethod");
            }
        }
        else if (!string.IsNullOrEmpty(method))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Sales.PaymentMethodUnavailable, "paymentMethod");
        }

        var snapshots = quote.Items.Select(line => new ConfirmedSaleItemSnapshot(
            line.OfferId,
            line.OwnedCustomerPackageId,
            line.Name,
            line.OfferType,
            line.DurationMinutes,
            line.SessionCount,
            line.Quantity,
            line.PaymentOption,
            line.ListPrice,
            line.DiscountAmount,
            line.FinalAmount,
            line.DueNow,
            line.PurchaseTerms)).ToArray();

        Customer? directCustomer = null;
        var directCustomerCreated = false;
        var customerId = request.CustomerId;
        if (request.DirectCustomer)
        {
            directCustomer = await db.Customers.SingleOrDefaultAsync(customer =>
                customer.TenantId == tenantId && customer.IsDirect, cancellationToken);
            if (directCustomer is null)
            {
                directCustomer = new Customer
                {
                    TenantId = tenantId,
                    FullName = "عميل مباشر",
                    MobileNumber = string.Empty,
                    NormalizedMobileNumber = string.Empty,
                    IsDirect = true
                };
                db.Customers.Add(directCustomer);
                directCustomerCreated = true;
            }
            customerId = directCustomer.Id;
        }

        var sale = new Sale
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            CustomerId = customerId!,
            IdempotencyKey = request.IdempotencyKey,
            RequestHash = requestHash,
            PaymentMethod = method ?? "none",
            GrandTotal = quote.GrandTotal,
            DueNow = quote.DueNow,
            ItemsSnapshotJson = JsonSerializer.Serialize(snapshots),
            Status = quote.DueNow > 0 ? "PendingCollection" : "ReadyToBook"
        };
        db.Sales.Add(sale);
        auditWriter.Write(tenantId, AuditActions.SaleConfirmed, nameof(Sale), sale.Id,
            sale.Id, new
            {
                sale.CustomerId,
                sale.BranchId,
                sale.GrandTotal,
                sale.DueNow,
                sale.PaymentMethod,
                ItemCount = snapshots.Length
            });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (directCustomerCreated && directCustomer is not null)
        {
            // A concurrent walk-in sale may have created the shared ledger first.
            var priorSale = await db.Sales.AsNoTracking().SingleOrDefaultAsync(existing =>
                existing.TenantId == tenantId && existing.IdempotencyKey == request.IdempotencyKey,
                cancellationToken);
            if (priorSale is not null)
            {
                if (priorSale.RequestHash != requestHash)
                {
                    throw ConflictException.FromCode(ApiErrorCodes.Sales.IdempotencyConflict);
                }
                return new ConfirmSaleResult(priorSale.Id, priorSale.Status,
                    priorSale.GrandTotal, priorSale.DueNow);
            }
            var existing = await db.Customers.AsNoTracking().SingleOrDefaultAsync(customer =>
                customer.TenantId == tenantId && customer.IsDirect, cancellationToken);
            if (existing is null)
            {
                throw;
            }
            sale.CustomerId = existing.Id;
            db.Customers.Remove(directCustomer);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                var concurrent = await FindConcurrentSale(tenantId, request.IdempotencyKey,
                    requestHash, cancellationToken);
                if (concurrent is null)
                {
                    throw;
                }
                return concurrent;
            }
        }
        catch (DbUpdateException)
        {
            // The unique tenant/key index handles concurrent retries. EF's failed save is rolled
            // back; a matching committed sale is the only expected recovery outcome.
            var concurrent = await FindConcurrentSale(tenantId, request.IdempotencyKey,
                requestHash, cancellationToken);
            if (concurrent is null)
            {
                throw;
            }
            return concurrent;
        }
        return new ConfirmSaleResult(sale.Id, sale.Status, sale.GrandTotal, sale.DueNow);
    }

    private async Task<ConfirmSaleResult?> FindConcurrentSale(string tenantId,
        string idempotencyKey, string requestHash, CancellationToken cancellationToken)
    {
        var concurrent = await db.Sales.AsNoTracking().SingleOrDefaultAsync(existing =>
            existing.TenantId == tenantId && existing.IdempotencyKey == idempotencyKey,
            cancellationToken);
        if (concurrent is null)
        {
            return null;
        }
        if (concurrent.RequestHash != requestHash)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Sales.IdempotencyConflict);
        }
        return new ConfirmSaleResult(concurrent.Id, concurrent.Status,
            concurrent.GrandTotal, concurrent.DueNow);
    }
}

public sealed record ConfirmedSaleItemSnapshot(
    string? OfferId,
    string? OwnedCustomerPackageId,
    string Name,
    string OfferType,
    int DurationMinutes,
    int? SessionCount,
    int Quantity,
    string PaymentOption,
    decimal ListPrice,
    decimal DiscountAmount,
    decimal FinalAmount,
    decimal DueNow,
    PackagePurchaseSnapshot? PurchaseTerms);
