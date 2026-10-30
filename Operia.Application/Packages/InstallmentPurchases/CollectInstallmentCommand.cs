using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Packages.InstallmentPurchases;

public sealed record CollectInstallmentRequest(decimal Amount, string? PaymentMethod);

public sealed record CollectInstallmentCommand(
    string PurchaseId, int Number, decimal Amount, string? PaymentMethod)
    : IRequest<InstallmentPurchaseDto>;

public sealed class CollectInstallmentHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IAuditWriter auditWriter,
    IDateTimeProvider clock)
    : IRequestHandler<CollectInstallmentCommand, InstallmentPurchaseDto>
{
    public async Task<InstallmentPurchaseDto> Handle(
        CollectInstallmentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = PackageMapper.RequireTenant(currentUser);
        var purchase = await db.CustomerPackages
            .Include(x => x.Installments)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.PurchaseId,
                cancellationToken);
        if (purchase is null || purchase.Installments.Count == 0)
        {
            throw ApiNotFoundException.FromCode(ApiErrorCodes.Packages.PackageNotFound);
        }

        var rows = purchase.Installments.OrderBy(x => x.Number).ToList();
        var row = rows.SingleOrDefault(x => x.Number == request.Number);
        if (row is null || row.Amount != request.Amount)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.InstallmentCollectionInvalid, "amount");
        }
        if (row.CollectedAtUtc is not null)
        {
            return InstallmentPurchaseMapper.ToDto(purchase);
        }
        if (rows.FirstOrDefault(x => x.CollectedAtUtc is null) != row ||
            purchase.AmountDue is null || purchase.AmountDue < request.Amount)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.InstallmentCollectionInvalid, "amount");
        }
        if (request.Amount > 0)
        {
            var enabledMethods = await BookingPaymentMethods.GetEnabledAsync(db, tenantId, cancellationToken);
            if (string.IsNullOrWhiteSpace(request.PaymentMethod) ||
                !enabledMethods.Contains(request.PaymentMethod, StringComparer.Ordinal))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Bookings.PaymentMethodUnavailable, "paymentMethod");
            }
        }

        row.CollectedAtUtc = clock.UtcNow;
        row.PaymentMethod = request.PaymentMethod;
        purchase.AmountDue -= row.Amount;
        var fullyPaid = purchase.AmountDue == 0;
        purchase.Total = InstallmentSessionRelease.AvailableAfterPayment(
            rows.Last().CumulativeAvailableSessions,
            purchase.Total,
            row.CumulativeAvailableSessions,
            paymentConfirmed: true,
            fullyPaid);

        if (purchase.OriginBookingId is not null && row.Amount > 0)
        {
            var booking = await db.Bookings.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == purchase.OriginBookingId,
                cancellationToken);
            if (booking is not null)
            {
                booking.PaidAmount += row.Amount;
            }
        }
        auditWriter.Write(tenantId, AuditActions.PackageInstallmentCollected,
            nameof(CustomerPackage), purchase.Id, purchase.Id,
            new { row.Number, row.Amount, row.PaymentMethod, purchase.AmountDue, purchase.Total });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.Changed);
        }
        return InstallmentPurchaseMapper.ToDto(purchase);
    }
}
