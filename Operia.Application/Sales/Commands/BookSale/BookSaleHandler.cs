using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Commands.CreateBooking;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Sales.Commands.ConfirmSale;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Sales.Commands.BookSale;

/// <summary>Books already collected sale items without charging them again.</summary>
public sealed class BookSaleHandler(
    IApplicationDbContext db,
    IBookingConcurrencyStore bookingStore,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IIdentityService identity,
    IAuditWriter auditWriter,
    IDateTimeProvider clock) : IRequestHandler<BookSaleCommand, BookSaleResult>
{
    public async Task<BookSaleResult> Handle(
        BookSaleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var sale = await db.Sales.SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.Id == request.SaleId, cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Sales.NotFound);
        var allowed = await branchScope.GetAllowedBranchIdsAsync(
            currentUser.UserId ?? string.Empty, cancellationToken);
        if (!allowed.Contains(sale.BranchId) || !await db.Branches.AsNoTracking().AnyAsync(x =>
                x.TenantId == tenantId && x.Id == sale.BranchId, cancellationToken))
        {
            throw ForbiddenException.FromCode(ApiErrorCodes.Sales.BranchUnavailable, "branchId");
        }
        var prior = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.SaleId == sale.Id, cancellationToken);
        if (prior is not null)
        {
            return new BookSaleResult(sale.Id, prior.Id, "Booked", null);
        }
        if (sale.Status != "ReadyToBook" || sale.Disposition == "Close")
        {
            throw ConflictException.FromCode(ApiErrorCodes.Sales.BookingUnavailable);
        }
        if (SaleCollectionRequirement.RequiresCollection(sale) &&
            !await db.SaleCollections.AsNoTracking().AnyAsync(x =>
                x.TenantId == tenantId && x.SaleId == sale.Id && x.Confirmed,
                cancellationToken))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Sales.BookingUnavailable);
        }
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.Id == sale.CustomerId && x.IsActive,
            cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Sales.CustomerUnavailable);
        if (customer.IsBlocked)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Customers.CustomerBlocked, "customerId");
        }
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.Id == request.EmployeeId && x.IsActive,
            cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Bookings.EmployeeNotFound,
                "employeeId");
        if (!await db.UserBranches.AsNoTracking().AnyAsync(x =>
                x.TenantId == tenantId && x.EmployeeId == employee.Id &&
                x.BranchId == sale.BranchId, cancellationToken))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.EmployeeNotInBranch,
                "employeeId");
        }
        var roles = await identity.GetRolesAsync([employee.IdentityUserId], cancellationToken);
        if (!string.Equals(roles.GetValueOrDefault(employee.IdentityUserId), Roles.Staff,
                StringComparison.OrdinalIgnoreCase))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Packages.PackageStaffUnavailable,
                "employeeId");
        }

        var day = request.ScheduledDate.DayOfWeek.ToString()[..3].ToLowerInvariant();
        var schedule = await db.EmployeeWorkingDays.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.EmployeeId == employee.Id &&
            x.BranchId == sale.BranchId && x.Day == day && x.Enabled,
            cancellationToken);
        var start = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(request.StartMinutes));
        var end = request.EndMinutes == 1440
            ? TimeOnly.MaxValue
            : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(request.EndMinutes));
        if (schedule?.FromTime is null || schedule.ToTime is null ||
            start < schedule.FromTime || end > schedule.ToTime)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.OutsideWorkingHours,
                "scheduledDate");
        }

        var lines = JsonSerializer.Deserialize<ConfirmedSaleItemSnapshot[]>(sale.ItemsSnapshotJson)
            ?? [];
        if (customer.IsDirect && lines.Any(line => line.OfferType == "package"))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Sales.DirectPackageIncomeOnly);
        }
        var offerIds = lines.Where(x => x.OfferId is not null)
            .Select(x => x.OfferId!).Distinct().ToArray();
        var offers = await db.Packages.AsNoTracking().Include(x => x.AvailableStaff)
            .Where(x => x.TenantId == tenantId && offerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (offers.Count != offerIds.Length || offers.Values.Any(x =>
                !x.AllStaff && x.AvailableStaff.Count > 0 &&
                x.AvailableStaff.All(staff => staff.EmployeeId != employee.Id)))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Packages.PackageStaffUnavailable,
                "employeeId");
        }
        var duration = lines.Sum(x => x.DurationMinutes *
            (x.OfferType == "package" ? 1 : x.Quantity));
        if (request.EndMinutes - request.StartMinutes < duration)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Sales.DurationMismatch,
                "endMinutes");
        }
        var purchases = await db.CustomerPackages.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SaleId == sale.Id)
            .ToListAsync(cancellationToken);
        var reservations = new List<BookingPackageReservation>();
        var booking = new Booking
        {
            TenantId = tenantId,
            SaleId = sale.Id,
            BookingNumber = $"OP-{request.ScheduledDate:yyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            IdempotencyKey = "sale:" + sale.Id,
            BranchId = sale.BranchId,
            EmployeeId = employee.Id,
            CustomerId = customer.Id,
            CustomerName = customer.FullName,
            CustomerMobile = customer.MobileNumber,
            ScheduledDate = request.ScheduledDate,
            StartMinutes = request.StartMinutes,
            EndMinutes = request.EndMinutes,
            Source = "Sale",
            PaymentMethod = sale.PaymentMethod,
            TotalAmount = sale.GrandTotal,
            PaidAmount = sale.DueNow,
            DiscountAmount = lines.Sum(x => x.DiscountAmount)
        };
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            booking.Items.Add(new BookingItem
            {
                TenantId = tenantId,
                BookingId = booking.Id,
                Type = line.OfferType == "package"
                    ? BookingItemType.PackageSession
                    : line.OfferType == "unlisted"
                        ? BookingItemType.UnlistedService : BookingItemType.Service,
                PackageId = line.OfferId,
                Name = line.Name,
                Quantity = line.Quantity,
                DurationMinutes = line.DurationMinutes,
                UnitPrice = line.FinalAmount / line.Quantity
            });
            if (line.OwnedCustomerPackageId is not null)
            {
                reservations.Add(NewReservation(tenantId, booking.Id,
                    line.OwnedCustomerPackageId));
            }
            else if (!customer.IsDirect && line.OfferId is not null)
            {
                var linePurchases = purchases.Where(x => x.SaleItemIndex == index)
                    .OrderBy(x => x.SaleUnitIndex).ToArray();
                if (linePurchases.Length != line.Quantity)
                {
                    throw ConflictException.FromCode(ApiErrorCodes.Sales.BookingUnavailable);
                }
                reservations.AddRange((line.OfferType == "package" ? linePurchases.Take(1) : linePurchases).Select(x =>
                    NewReservation(tenantId, booking.Id, x.Id)));
            }
        }
        db.Bookings.Add(booking);
        db.BookingItems.AddRange(booking.Items);
        db.BookingPackageReservations.AddRange(reservations);
        db.BookingHistory.Add(new BookingHistory
        {
            TenantId = tenantId,
            BookingId = booking.Id,
            Action = "Created",
            ChangedByUserId = currentUser.UserId,
            ChangedByDisplayName = currentUser.DisplayName,
            ChangesJson = JsonSerializer.Serialize(new
            {
                sale.Id,
                booking.BranchId,
                booking.EmployeeId,
                booking.ScheduledDate,
                booking.StartMinutes,
                booking.EndMinutes
            })
        });
        sale.Status = "Booked";
        auditWriter.Write(tenantId, AuditActions.BookingCreated, nameof(Booking),
            booking.Id, booking.BookingNumber,
            new
            {
                sale.Id,
                booking.BranchId,
                booking.EmployeeId,
                booking.ScheduledDate,
                booking.StartMinutes,
                booking.EndMinutes
            });
        auditWriter.Write(tenantId, AuditActions.SaleBooked, nameof(Sale),
            sale.Id, sale.Id, new { booking.Id });
        try
        {
            var saved = await bookingStore.CommitCreateAsync(
                booking, reservations, [], clock.UtcNow, cancellationToken);
            return new BookSaleResult(sale.Id, saved.Id, "Booked", null);
        }
        catch (ConflictException ex) when (ex.ErrorCode is
            ApiErrorCodes.Bookings.SlotUnavailable or
            ApiErrorCodes.Bookings.PackageSessionUnavailable)
        {
            return new BookSaleResult(sale.Id, null, "BookingPending", ex.ErrorCode);
        }
    }

    private static BookingPackageReservation NewReservation(
        string tenantId, string bookingId, string purchaseId) => new()
        {
            TenantId = tenantId,
            BookingId = bookingId,
            CustomerPackageId = purchaseId
        };
}
