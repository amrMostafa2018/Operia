using System.Text.Json;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Bookings.Commands.CreateBooking;

/// <summary>
/// Validates a booking request, prepares its purchases and reservations, and commits it through the concurrency store.
/// </summary>
public sealed class CreateBookingHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IAuditWriter auditWriter,
    IBookingConcurrencyStore concurrencyStore,
    IDateTimeProvider clock) : IRequestHandler<CreateBookingCommand, CreateBookingResult>
{
    /// <summary>
    /// Returns an existing booking for the same idempotency key or creates a new Booked appointment.
    /// </summary>
    public async Task<CreateBookingResult> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var allowedBranches = await BookingAccess.GetAllowedBranchesAsync(currentUser, branchScope, cancellationToken);
        BookingAccess.RequireBranch(allowedBranches, request.BranchId);

        var existing = await db.Bookings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return ToResult(existing);
        }

        var customer = await db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.CustomerId && x.IsActive, cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Bookings.CustomerNotFound, "customerId");

        await ValidateEmployeeAsync(tenantId, request, cancellationToken);
        await ValidateWorkingHoursAsync(tenantId, request, cancellationToken);

        var catalog = await LoadCatalogAsync(tenantId, request.Items, cancellationToken);
        ValidatePackageSelection(request.Items, catalog);
        await ValidatePaymentMethodAsync(tenantId, request.PaymentMethod, cancellationToken);

        var booking = BuildBooking(tenantId, customer, request, catalog);
        var (reservations, newPurchases) = await BuildReservationsAsync(
            tenantId, customer.Id, booking.Id, request.Items, catalog, cancellationToken);

        StageCreation(tenantId, booking, reservations, newPurchases);

        var saved = await concurrencyStore.CommitCreateAsync(
            booking,
            reservations,
            newPurchases,
            clock.UtcNow,
            cancellationToken);
        return ToResult(saved);
    }

    /// <summary>Requires an active employee assigned to the requested tenant branch.</summary>
    private async Task ValidateEmployeeAsync(
        string tenantId,
        CreateBookingCommand request,
        CancellationToken cancellationToken)
    {
        var employee = await db.Employees.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.EmployeeId && x.IsActive, cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Bookings.EmployeeNotFound, "employeeId");

        var assigned = await db.UserBranches.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.EmployeeId == employee.Id && x.BranchId == request.BranchId, cancellationToken);
        if (!assigned)
        {
            throw Invalid(ApiErrorCodes.Bookings.EmployeeNotInBranch, "employeeId");
        }
    }

    /// <summary>Loads every requested active catalog item within the current tenant.</summary>
    private async Task<IReadOnlyDictionary<string, Package>> LoadCatalogAsync(
        string tenantId,
        IReadOnlyList<CreateBookingItemInput> items,
        CancellationToken cancellationToken)
    {
        var requestedPackageIds = items.Select(x => x.PackageId).Distinct().ToList();
        var catalog = await db.Packages.AsNoTracking()
            .Where(x => x.TenantId == tenantId && requestedPackageIds.Contains(x.Id) && x.Status == PackageStatus.Active)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (catalog.Count != requestedPackageIds.Count)
        {
            throw ApiNotFoundException.FromCode(ApiErrorCodes.Packages.PackageNotFound, "items");
        }
        return catalog;
    }

    /// <summary>Allows at most one Package item, representing exactly one session.</summary>
    private static void ValidatePackageSelection(
        IReadOnlyList<CreateBookingItemInput> items,
        IReadOnlyDictionary<string, Package> catalog)
    {
        var packageInputs = items.Where(item => catalog[item.PackageId].OfferType == OfferType.Package).ToList();
        if (packageInputs.Count > 1 || packageInputs.Any(item => item.Quantity != 1))
        {
            throw Invalid(ApiErrorCodes.Bookings.OnePackageSessionRequired, "items");
        }
    }

    /// <summary>Accepts a selected payment method only while it is enabled for this tenant.</summary>
    private async Task ValidatePaymentMethodAsync(
        string tenantId,
        string? paymentMethod,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(paymentMethod))
        {
            var enabledMethods = await BookingPaymentMethods.GetEnabledAsync(db, tenantId, cancellationToken);
            if (!enabledMethods.Contains(paymentMethod, StringComparer.Ordinal))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Bookings.PaymentMethodUnavailable, "paymentMethod");
            }
        }
    }

    /// <summary>Stages the booking, related records, history, and audit entry for one commit.</summary>
    private void StageCreation(
        string tenantId,
        Booking booking,
        IReadOnlyList<BookingPackageReservation> reservations,
        IReadOnlyList<CustomerPackage> newPurchases)
    {
        db.Bookings.Add(booking);
        db.BookingItems.AddRange(booking.Items);
        db.CustomerPackages.AddRange(newPurchases);
        db.BookingHistory.Add(new BookingHistory
        {
            TenantId = tenantId,
            BookingId = booking.Id,
            Action = "Created",
            ChangedByUserId = currentUser.UserId,
            ChangedByDisplayName = currentUser.DisplayName,
            ChangesJson = JsonSerializer.Serialize(new
            {
                booking.BranchId,
                booking.EmployeeId,
                booking.ScheduledDate,
                booking.StartMinutes,
                booking.EndMinutes,
                booking.Status
            })
        });
        db.BookingPackageReservations.AddRange(reservations);

        auditWriter.Write(
            tenantId,
            AuditActions.BookingCreated,
            nameof(Booking),
            booking.Id,
            booking.BookingNumber,
            new { booking.BranchId, booking.EmployeeId, booking.ScheduledDate, booking.StartMinutes, booking.EndMinutes });
    }

    /// <summary>
    /// Reuses eligible owned balances or creates one-session purchases for each new standalone service unit.
    /// The concurrency store assigns session numbers and updates reserved balances at commit time.
    /// </summary>
    private async Task<(List<BookingPackageReservation> Reservations, List<CustomerPackage> NewPurchases)> BuildReservationsAsync(
        string tenantId,
        string customerId,
        string bookingId,
        IReadOnlyList<CreateBookingItemInput> items,
        IReadOnlyDictionary<string, Package> catalog,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var reservations = new List<BookingPackageReservation>();
        var newPurchases = new List<CustomerPackage>();
        foreach (var item in items)
        {
            var product = catalog[item.PackageId];
            if (!string.IsNullOrWhiteSpace(item.CustomerPackageId))
            {
                if (item.Quantity != 1)
                {
                    throw Invalid(
                        product.OfferType == OfferType.Package
                            ? ApiErrorCodes.Bookings.OnePackageSessionRequired
                            : ApiErrorCodes.Bookings.ReusedServiceQuantityOne,
                        "items");
                }

                var owned = await db.CustomerPackages.AsNoTracking().SingleOrDefaultAsync(
                    x => x.TenantId == tenantId &&
                         x.Id == item.CustomerPackageId &&
                         x.CustomerId == customerId &&
                         x.PackageId == item.PackageId &&
                         x.IsActive &&
                         (x.ExpiresOn == null || x.ExpiresOn >= today),
                    cancellationToken);
                if (owned is null || owned.TotalSessions <= owned.UsedSessions + owned.ReservedSessions)
                {
                    throw ConflictException.FromCode(ApiErrorCodes.Bookings.PackageSessionUnavailable, "items");
                }

                reservations.Add(NewReservation(tenantId, bookingId, owned.Id));
                continue;
            }

            if (product.OfferType == OfferType.Package)
            {
                throw Invalid(ApiErrorCodes.Bookings.OwnedPackageRequired, "items");
            }

            // Each standalone service unit is a one-session customer purchase.
            for (var unit = 0; unit < item.Quantity; unit++)
            {
                var purchase = new CustomerPackage
                {
                    TenantId = tenantId,
                    CustomerId = customerId,
                    PackageId = product.Id,
                    TotalSessions = 1,
                    IsActive = true
                };
                newPurchases.Add(purchase);
                reservations.Add(NewReservation(tenantId, bookingId, purchase.Id));
            }
        }

        return (reservations, newPurchases);
    }

    /// <summary>Creates an unsaved reservation for a customer purchase.</summary>
    private static BookingPackageReservation NewReservation(string tenantId, string bookingId, string customerPackageId) =>
        new()
        {
            TenantId = tenantId,
            BookingId = bookingId,
            CustomerPackageId = customerPackageId
        };

    /// <summary>Checks the requested branch-local interval against the employee's enabled working day.</summary>
    private async Task ValidateWorkingHoursAsync(string tenantId, CreateBookingCommand request, CancellationToken cancellationToken)
    {
        var day = request.ScheduledDate.DayOfWeek.ToString()[..3].ToLowerInvariant();
        var schedule = await db.EmployeeWorkingDays.AsNoTracking().SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.EmployeeId == request.EmployeeId &&
                 x.BranchId == request.BranchId && x.Day == day && x.Enabled,
            cancellationToken);
        var start = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(request.StartMinutes));
        var end = request.EndMinutes == 1440
            ? TimeOnly.MaxValue
            : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(request.EndMinutes));
        if (schedule?.FromTime is null || schedule.ToTime is null | start < schedule.FromTime || end > schedule.ToTime)
        {
            throw Invalid(ApiErrorCodes.Bookings.OutsideWorkingHours, "scheduledDate");
        }
    }

    /// <summary>Snapshots catalog details and prices so later catalog changes do not alter this booking.</summary>
    private static Booking BuildBooking(string tenantId, Customer customer, CreateBookingCommand request, IReadOnlyDictionary<string, Package> catalog)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var booking = new Booking
        {
            TenantId = tenantId,
            BookingNumber = $"OP-{request.ScheduledDate:yyMMdd}-{suffix}",
            IdempotencyKey = request.IdempotencyKey.Trim(),
            BranchId = request.BranchId,
            EmployeeId = request.EmployeeId,
            CustomerId = customer.Id,
            CustomerName = customer.FullName,
            CustomerMobile = customer.MobileNumber,
            ScheduledDate = request.ScheduledDate,
            StartMinutes = request.StartMinutes,
            EndMinutes = request.EndMinutes,
            TotalAmount = request.Items.Sum(item =>
                catalog[item.PackageId].OfferType == OfferType.Package || item.CustomerPackageId is not null
                    ? 0
                    : catalog[item.PackageId].Price * item.Quantity),
            PaymentMethod = request.PaymentMethod
        };
        booking.Items = request.Items.Select(input =>
        {
            var product = catalog[input.PackageId];
            return new BookingItem
            {
                TenantId = tenantId,
                BookingId = booking.Id,
                Type = string.Equals(input.Type, "unlisted", StringComparison.OrdinalIgnoreCase)
                    ? BookingItemType.UnlistedService
                    : product.OfferType == OfferType.Package
                        ? BookingItemType.PackageSession
                        : BookingItemType.Service,
                PackageId = product.Id,
                Name = product.Name,
                Quantity = input.Quantity,
                DurationMinutes = product.SessionDurationMinutes,
                UnitPrice = product.OfferType == OfferType.Package || input.CustomerPackageId is not null
                    ? 0
                    : product.Price
            };
        }).ToList();
        return booking;
    }

    /// <summary>Builds a coded field validation failure for booking input.</summary>
    private static ValidationException Invalid(string code, string field) =>
        new([new ValidationFailure(field, code) { ErrorCode = code }]);

    /// <summary>Returns the persisted identity, status, and concurrency version.</summary>
    private static CreateBookingResult ToResult(Booking booking) =>
        new(booking.Id, booking.BookingNumber, booking.Status.ToString(), Convert.ToBase64String(booking.Version));
}
