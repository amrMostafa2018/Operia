using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Commands.CreateBooking;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Bookings.Commands.UpdateBooking;

/// <summary>
/// Applies permitted edits to a Booked appointment while preserving its Package session and audit history.
/// </summary>
public sealed class UpdateBookingHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope,
    IAuditWriter auditWriter,
    IDateTimeProvider clock) : IRequestHandler<UpdateBookingCommand, UpdateBookingResult>
{
    /// <summary>Checks the version, reconciles edited services, and saves the booking with its history.</summary>
    public async Task<UpdateBookingResult> Handle(UpdateBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var booking = await LoadEditableBookingAsync(tenantId, request, cancellationToken);
        var catalog = await LoadCatalogAsync(tenantId, request.Items, cancellationToken);
        ValidatePackageSelection(booking.Items, request.Items, catalog);

        var itemsChanged = HasItemsChanged(booking.Items, request.Items);
        var paymentMethodChanged = !string.Equals(booking.PaymentMethod, request.PaymentMethod, StringComparison.Ordinal);
        if (!itemsChanged && !paymentMethodChanged)
        {
            return ToResult(booking);
        }

        await ValidatePaymentMethodAsync(tenantId, request.PaymentMethod, paymentMethodChanged, cancellationToken);

        var before = SnapshotItems(booking.Items);
        var beforePaymentMethod = booking.PaymentMethod;
        if (itemsChanged)
        {
            await ReplaceItemsAsync(booking, request.Items, catalog, tenantId, cancellationToken);
        }

        booking.PaymentMethod = request.PaymentMethod;
        // Touch the aggregate so SQL Server checks and advances its rowversion for every edit.
        booking.LastModifiedAt = booking.LastModifiedAt is { } previous && clock.UtcNow <= previous
            ? previous.AddTicks(1)
            : clock.UtcNow;

        StageEditHistory(tenantId, booking, before, beforePaymentMethod);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.Changed);
        }

        return ToResult(booking);
    }

    /// <summary>Loads a tenant and branch accessible booking that is still Booked at the requested version.</summary>
    private async Task<Booking> LoadEditableBookingAsync(
        string tenantId,
        UpdateBookingCommand request,
        CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.BookingId, cancellationToken)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Bookings.NotFound);
        var allowedBranches = await BookingAccess.GetAllowedBranchesAsync(currentUser, branchScope, cancellationToken);
        BookingAccess.RequireBranch(allowedBranches, booking.BranchId);
        if (booking.Status != BookingStatus.Booked)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.TerminalReadOnly);
        }

        byte[] version;
        try
        {
            version = Convert.FromBase64String(request.Version);
        }
        catch (FormatException)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.Changed);
        }
        if (!booking.Version.SequenceEqual(version))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.Changed);
        }
        return booking;
    }

    /// <summary>Loads all requested active catalog records from the current tenant.</summary>
    private async Task<IReadOnlyDictionary<string, Package>> LoadCatalogAsync(
        string tenantId,
        IReadOnlyList<CreateBookingItemInput> items,
        CancellationToken cancellationToken)
    {
        var ids = items.Select(x => x.PackageId).Distinct().ToList();
        var catalog = await db.Packages.AsNoTracking()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.Id) && x.Status == PackageStatus.Active)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (catalog.Count != ids.Count)
        {
            throw ApiNotFoundException.FromCode(ApiErrorCodes.Packages.PackageNotFound, "items");
        }
        return catalog;
    }

    /// <summary>Rejects edits that add, remove, or change the booking's Package session.</summary>
    private static void ValidatePackageSelection(
        IEnumerable<BookingItem> existingItems,
        IReadOnlyList<CreateBookingItemInput> requestedItems,
        IReadOnlyDictionary<string, Package> catalog)
    {
        var existingPackageId = existingItems
            .SingleOrDefault(x => x.Type == BookingItemType.PackageSession)?.PackageId;
        var requestedPackageItems = requestedItems
            .Where(x => catalog[x.PackageId].OfferType == OfferType.Package)
            .ToList();
        if (requestedPackageItems.Count > 1 || requestedPackageItems.Any(x => x.Quantity != 1)
            || !string.Equals(existingPackageId, requestedPackageItems.SingleOrDefault()?.PackageId, StringComparison.Ordinal))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.PackageEditNotAllowed, "items");
        }
    }

    /// <summary>Compares the product and quantity selection without depending on item order.</summary>
    private static bool HasItemsChanged(
        IEnumerable<BookingItem> existingItems,
        IReadOnlyList<CreateBookingItemInput> requestedItems)
    {
        var existingSelection = existingItems
            .Select(x => (PackageId: x.PackageId, x.Quantity))
            .OrderBy(x => x.PackageId, StringComparer.Ordinal)
            .ThenBy(x => x.Quantity)
            .ToList();
        var requestedSelection = requestedItems
            .Select(x => (PackageId: (string?)x.PackageId, x.Quantity))
            .OrderBy(x => x.PackageId, StringComparer.Ordinal)
            .ThenBy(x => x.Quantity)
            .ToList();
        return !existingSelection.SequenceEqual(requestedSelection);
    }

    /// <summary>Checks a changed payment method against the tenant's currently enabled methods.</summary>
    private async Task ValidatePaymentMethodAsync(
        string tenantId,
        string? paymentMethod,
        bool changed,
        CancellationToken cancellationToken)
    {
        if (changed && paymentMethod is not null)
        {
            var enabledMethods = await BookingPaymentMethods.GetEnabledAsync(db, tenantId, cancellationToken);
            if (!enabledMethods.Contains(paymentMethod, StringComparer.Ordinal))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Bookings.PaymentMethodUnavailable, "paymentMethod");
            }
        }
    }

    /// <summary>Reconciles standalone reservations and replaces item snapshots and the booking total.</summary>
    private async Task ReplaceItemsAsync(
        Booking booking,
        IReadOnlyList<CreateBookingItemInput> requestedItems,
        IReadOnlyDictionary<string, Package> catalog,
        string tenantId,
        CancellationToken cancellationToken)
    {
        await ReconcileStandaloneReservationsAsync(booking, requestedItems, catalog, tenantId, cancellationToken);
        var existingItems = booking.Items
            .Where(x => x.PackageId is not null)
            .GroupBy(x => x.PackageId!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => new Queue<BookingItem>(x), StringComparer.Ordinal);
        db.BookingItems.RemoveRange(booking.Items);
        booking.Items = requestedItems.Select(input =>
        {
            var product = catalog[input.PackageId];
            existingItems.TryGetValue(input.PackageId, out var matchingItems);
            var existingItem = matchingItems?.Count > 0 ? matchingItems.Dequeue() : null;
            return new BookingItem
            {
                TenantId = tenantId,
                BookingId = booking.Id,
                Type = existingItem?.Type ?? (string.Equals(input.Type, "unlisted", StringComparison.OrdinalIgnoreCase)
                    ? BookingItemType.UnlistedService
                    : product.OfferType == OfferType.Package
                        ? BookingItemType.PackageSession
                        : BookingItemType.Service),
                PackageId = product.Id,
                Name = existingItem?.Name ?? product.Name,
                Quantity = input.Quantity,
                DurationMinutes = existingItem?.DurationMinutes ?? product.SessionDurationMinutes,
                UnitPrice = CalculateUnitPrice(existingItem, input.Quantity, product)
            };
        }).ToList();
        db.BookingItems.AddRange(booking.Items);
        booking.TotalAmount = booking.Items.Sum(x => x.UnitPrice * x.Quantity);
    }

    /// <summary>Stages before and after details in booking history and the audit trail.</summary>
    private void StageEditHistory(
        string tenantId,
        Booking booking,
        IReadOnlyList<BookingItemSnapshot> before,
        string? beforePaymentMethod)
    {
        var after = SnapshotItems(booking.Items);
        db.BookingHistory.Add(new BookingHistory
        {
            TenantId = tenantId,
            BookingId = booking.Id,
            Action = "Updated",
            ChangedByUserId = currentUser.UserId,
            ChangedByDisplayName = currentUser.DisplayName,
            ChangesJson = JsonSerializer.Serialize(new
            {
                Before = new { Items = before, PaymentMethod = beforePaymentMethod },
                After = new { Items = after, booking.PaymentMethod }
            })
        });
        auditWriter.Write(
            tenantId,
            AuditActions.BookingUpdated,
            nameof(Booking),
            booking.Id,
            booking.BookingNumber,
            new
            {
                Before = new { Items = before, PaymentMethod = beforePaymentMethod },
                After = new { Items = after, booking.PaymentMethod }
            });
    }

    /// <summary>Captures stable item details before tracked items are replaced.</summary>
    private static List<BookingItemSnapshot> SnapshotItems(IEnumerable<BookingItem> items) =>
        items.Select(x => new BookingItemSnapshot(x.PackageId, x.Name, x.Quantity)).ToList();

    /// <summary>Item details retained in edit history.</summary>
    private sealed record BookingItemSnapshot(string? PackageId, string Name, int Quantity);

    /// <summary>Returns the booking identity, status, and current concurrency version.</summary>
    private static UpdateBookingResult ToResult(Booking booking) =>
        new(booking.Id, booking.Status.ToString(), Convert.ToBase64String(booking.Version));

    /// <summary>Releases removed service units and reserves newly added one-session purchases.</summary>
    private async Task ReconcileStandaloneReservationsAsync(
        Booking booking,
        IReadOnlyList<CreateBookingItemInput> requestedItems,
        IReadOnlyDictionary<string, Package> catalog,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var active = await db.BookingPackageReservations
            .Include(x => x.CustomerPackage)
            .ThenInclude(x => x!.Package)
            .Where(x => x.TenantId == tenantId && x.BookingId == booking.Id && x.CancellationAtUtc == null)
            .ToListAsync(cancellationToken);
        var serviceReservations = active
            .Where(x => x.CustomerPackage?.Package?.OfferType == OfferType.SingleSession)
            .GroupBy(x => x.CustomerPackage!.PackageId)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
        var requestedServices = requestedItems
            .Where(x => catalog[x.PackageId].OfferType == OfferType.SingleSession)
            .GroupBy(x => x.PackageId)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Quantity), StringComparer.Ordinal);

        foreach (var packageId in serviceReservations.Keys.Union(requestedServices.Keys, StringComparer.Ordinal))
        {
            serviceReservations.TryGetValue(packageId, out var current);
            requestedServices.TryGetValue(packageId, out var target);
            current ??= [];

            foreach (var reservation in current.Skip(target))
            {
                var owned = reservation.CustomerPackage!;
                if (owned.ReservedSessionCount < 1)
                {
                    throw ConflictException.FromCode(ApiErrorCodes.Bookings.Changed);
                }
                reservation.CancellationAtUtc = clock.UtcNow;
                owned.ReservedSessions = owned.ReservedSessionCount - 1;
            }

            for (var unit = current.Count; unit < target; unit++)
            {
                var purchase = new CustomerPackage
                {
                    TenantId = tenantId,
                    CustomerId = booking.CustomerId,
                    PackageId = packageId,
                    Total = 1,
                    ReservedSessions = 1,
                    IsActive = true
                };
                db.CustomerPackages.Add(purchase);
                db.BookingPackageReservations.Add(new BookingPackageReservation
                {
                    TenantId = tenantId,
                    BookingId = booking.Id,
                    CustomerPackageId = purchase.Id,
                    SessionNumber = 1
                });
            }
        }
    }

    /// <summary>Preserves previously paid or reused units when a service quantity changes.</summary>
    private static decimal CalculateUnitPrice(BookingItem? existingItem, int quantity, Package product)
    {
        if (product.OfferType == OfferType.Package)
        {
            return 0;
        }
        if (existingItem is null)
        {
            return product.Price;
        }
        if (existingItem.UnitPrice > 0)
        {
            return existingItem.UnitPrice;
        }

        var addedUnits = Math.Max(0, quantity - existingItem.Quantity);
        return addedUnits * product.Price / quantity;
    }
}
