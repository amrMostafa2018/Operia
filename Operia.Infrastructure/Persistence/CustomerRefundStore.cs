using System.Data;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Customers.Commands.CancelAndRefundPurchase;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using Operia.Domain.Enums;

namespace Operia.Infrastructure.Persistence;

public sealed class CustomerRefundStore(ApplicationDbContext db, IDateTimeProvider clock) : ICustomerRefundStore
{
    public async Task<CancelAndRefundPurchaseResult> ExecuteAsync(
        string tenantId, string purchaseId,
        Func<CancellationToken, Task<CancelAndRefundPurchaseResult>> post,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            return await post(cancellationToken);
        }

        var today = DateOnly.FromDateTime(clock.UtcNow);
        var affected = await (from reservation in db.BookingPackageReservations.AsNoTracking()
                              join booking in db.Bookings.AsNoTracking()
                                  on reservation.BookingId equals booking.Id
                              where reservation.TenantId == tenantId &&
                                    reservation.CustomerPackageId == purchaseId &&
                                    reservation.CancellationAtUtc == null &&
                                    booking.TenantId == tenantId &&
                                    booking.Status == BookingStatus.Booked &&
                                    booking.ScheduledDate >= today
                              select new { booking.Id, booking.EmployeeId, booking.ScheduledDate })
            .Distinct().ToListAsync(cancellationToken);
        var bookingIds = affected.Select(x => x.Id).ToArray();
        var packageIds = await db.BookingPackageReservations.AsNoTracking()
            .Where(x => x.TenantId == tenantId && bookingIds.Contains(x.BookingId) &&
                        x.CancellationAtUtc == null)
            .Select(x => x.CustomerPackageId).Distinct().ToListAsync(cancellationToken);
        packageIds.Add(purchaseId);

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        foreach (var slot in affected.Select(x => $"booking-slot:{tenantId}:{x.EmployeeId}:{x.ScheduledDate:yyyyMMdd}")
                     .Distinct().OrderBy(x => x, StringComparer.Ordinal))
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.AcquireBookingMutationLock @Resource={slot}", cancellationToken);
        }
        foreach (var id in packageIds.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.AcquireBookingMutationLock @Resource={"booking-package:" + tenantId + ":" + id}",
                cancellationToken);
        }
        try
        {
            var result = await post(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Customers.RefundUnavailable);
        }
    }
}
