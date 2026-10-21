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
/// Applies permitted edits to a Booked appointment while preserving audit history and purchase-only pricing.
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
        BookingItemPricing.ValidatePackageSelection(request.Items, catalog);

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

    /// <summary>Reconciles reservations, package purchases, and replaces item snapshots and the booking total.</summary>
    private async Task ReplaceItemsAsync(
        Booking booking,
        IReadOnlyList<CreateBookingItemInput> requestedItems,
        IReadOnlyDictionary<string, Package> catalog,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var ownedCatalogPackageIds = await LoadOwnedCatalogPackageIdsAsync(tenantId, booking.CustomerId, today, cancellationToken);
        await ReconcileStandaloneReservationsAsync(booking, requestedItems, catalog, tenantId, cancellationToken);
        await ReconcilePackagePurchasesAsync(
            booking,
            requestedItems,
            catalog,
            ownedCatalogPackageIds,
            tenantId,
            today,
            cancellationToken);
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
                UnitPrice = ResolveUnitPrice(input, product, existingItem, ownedCatalogPackageIds)
            };
        }).ToList();
        db.BookingItems.AddRange(booking.Items);
        booking.TotalAmount = booking.Items.Sum(x => x.UnitPrice * x.Quantity);
    }

    /// <summary>Preserves booked session prices while applying purchase-only package pricing.</summary>
    private static decimal ResolveUnitPrice(
        CreateBookingItemInput input,
        Package product,
        BookingItem? existingItem,
        IReadOnlySet<string> ownedCatalogPackageIds)
    {
        if (product.OfferType == OfferType.Package)
        {
            return BookingItemPricing.CalculateUnitPrice(input, product, ownedCatalogPackageIds);
        }

        if (existingItem is null)
        {
            return BookingItemPricing.CalculateUnitPrice(input, product, ownedCatalogPackageIds);
        }

        if (existingItem.UnitPrice > 0)
        {
            return existingItem.UnitPrice;
        }

        var addedUnits = Math.Max(0, input.Quantity - existingItem.Quantity);
        return input.Quantity == 0 ? 0 : addedUnits * product.Price / input.Quantity;
    }

    /// <summary>Loads active owned catalog package ids for purchase-only pricing.</summary>
    private async Task<HashSet<string>> LoadOwnedCatalogPackageIdsAsync(
        string tenantId,
        string customerId,
        DateOnly today,
        CancellationToken cancellationToken) =>
        (await db.CustomerPackages.AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        x.CustomerId == customerId &&
                        x.IsActive &&
                        (x.ExpiresOn == null || x.ExpiresOn >= today))
            .Select(x => x.PackageId)
            .ToListAsync(cancellationToken))
        .ToHashSet(StringComparer.Ordinal);

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

    /// <summary>Releases removed service units, reuses owned balances, and reserves new one-session purchases.</summary>
    private async Task ReconcileStandaloneReservationsAsync(
        Booking booking,
        IReadOnlyList<CreateBookingItemInput> requestedItems,
        IReadOnlyDictionary<string, Package> catalog,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var active = await db.BookingPackageReservations
            .Include(x => x.CustomerPackage)
            .ThenInclude(x => x!.Package)
            .Where(x => x.TenantId == tenantId && x.BookingId == booking.Id && x.CancellationAtUtc == null)
            .ToListAsync(cancellationToken);
        var singleSessionReservations = active
            .Where(x => x.CustomerPackage?.Package?.OfferType == OfferType.SingleSession)
            .ToList();

        var requestedOwned = requestedItems
            .Where(x =>
                catalog[x.PackageId].OfferType == OfferType.SingleSession &&
                !string.IsNullOrWhiteSpace(x.CustomerPackageId))
            .GroupBy(x => x.CustomerPackageId!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Quantity), StringComparer.Ordinal);
        var requestedNewPurchases = requestedItems
            .Where(x =>
                catalog[x.PackageId].OfferType == OfferType.SingleSession &&
                string.IsNullOrWhiteSpace(x.CustomerPackageId))
            .GroupBy(x => x.PackageId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Quantity), StringComparer.Ordinal);

        var unmatchedReservations = new List<BookingPackageReservation>(singleSessionReservations);
        var remainingOwned = new Dictionary<string, int>(requestedOwned, StringComparer.Ordinal);
        var remainingNewPurchases = new Dictionary<string, int>(requestedNewPurchases, StringComparer.Ordinal);

        foreach (var reservation in singleSessionReservations)
        {
            if (remainingOwned.TryGetValue(reservation.CustomerPackageId, out var ownedCount) && ownedCount > 0)
            {
                remainingOwned[reservation.CustomerPackageId] = ownedCount - 1;
                unmatchedReservations.Remove(reservation);
                continue;
            }

            var packageId = reservation.CustomerPackage!.PackageId;
            if (remainingNewPurchases.TryGetValue(packageId, out var newCount) && newCount > 0)
            {
                remainingNewPurchases[packageId] = newCount - 1;
                unmatchedReservations.Remove(reservation);
            }
        }

        foreach (var reservation in unmatchedReservations)
        {
            CancelStandaloneReservation(reservation);
        }

        foreach (var (customerPackageId, count) in remainingOwned.Where(x => x.Value > 0))
        {
            var packageId = requestedItems
                .First(x => string.Equals(x.CustomerPackageId, customerPackageId, StringComparison.Ordinal))
                .PackageId;
            for (var unit = 0; unit < count; unit++)
            {
                await AddOwnedStandaloneReservationAsync(
                    booking,
                    tenantId,
                    customerPackageId,
                    packageId,
                    today,
                    cancellationToken);
            }
        }

        foreach (var (packageId, count) in remainingNewPurchases.Where(x => x.Value > 0))
        {
            for (var unit = 0; unit < count; unit++)
            {
                AddNewStandalonePurchaseReservation(booking, tenantId, packageId);
            }
        }
    }

    /// <summary>Cancels a standalone reservation and releases its reserved slot.</summary>
    private void CancelStandaloneReservation(BookingPackageReservation reservation)
    {
        var owned = reservation.CustomerPackage!;
        if (owned.ReservedSessionCount < 1)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.Changed);
        }

        reservation.CancellationAtUtc = clock.UtcNow;
        owned.ReservedSessions = owned.ReservedSessionCount - 1;
    }

    /// <summary>Reserves one session on an existing customer-owned single-session purchase.</summary>
    private async Task AddOwnedStandaloneReservationAsync(
        Booking booking,
        string tenantId,
        string customerPackageId,
        string packageId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var owned = await db.CustomerPackages
            .Include(x => x.Package)
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId &&
                     x.Id == customerPackageId &&
                     x.CustomerId == booking.CustomerId &&
                     x.PackageId == packageId &&
                     x.IsActive &&
                     (x.ExpiresOn == null || x.ExpiresOn >= today),
                cancellationToken);
        if (owned is null ||
            !CustomerPackageBalance.HasAvailable(
                owned.Total,
                owned.Used,
                owned.ReservedSessions,
                owned.Package?.OfferType ?? OfferType.SingleSession,
                owned.Package?.PulseCount))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.PackageSessionUnavailable, "items");
        }

        var occupied = await db.BookingPackageReservations.AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        x.CustomerPackageId == owned.Id &&
                        x.CancellationAtUtc == null)
            .Select(x => x.SessionNumber)
            .ToListAsync(cancellationToken);
        var next = Enumerable.Range(owned.Used + 1, owned.Total - owned.Used)
            .FirstOrDefault(sessionNumber => !occupied.Contains(sessionNumber));
        if (next == 0)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.PackageSessionUnavailable, "items");
        }

        owned.ReservedSessions = owned.ReservedSessionCount + 1;
        db.BookingPackageReservations.Add(new BookingPackageReservation
        {
            TenantId = tenantId,
            BookingId = booking.Id,
            CustomerPackageId = owned.Id,
            SessionNumber = next
        });
    }

    /// <summary>Creates a one-session customer purchase and reserves it on the booking.</summary>
    private void AddNewStandalonePurchaseReservation(Booking booking, string tenantId, string packageId)
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

    /// <summary>Creates customer-package rows for newly billable purchase-only package units.</summary>
    private Task ReconcilePackagePurchasesAsync(
        Booking booking,
        IReadOnlyList<CreateBookingItemInput> requestedItems,
        IReadOnlyDictionary<string, Package> catalog,
        IReadOnlySet<string> ownedCatalogPackageIds,
        string tenantId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var previousBillableUnits = booking.Items
            .Where(x =>
                x.PackageId is not null &&
                catalog.ContainsKey(x.PackageId) &&
                catalog[x.PackageId].OfferType == OfferType.Package)
            .GroupBy(x => x.PackageId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var quantity = group.Sum(x => x.Quantity);
                    return ownedCatalogPackageIds.Contains(group.Key)
                        ? Math.Max(0, quantity - 1)
                        : quantity;
                },
                StringComparer.Ordinal);

        var requestedBillableUnits = requestedItems
            .Where(item =>
                BookingItemPricing.IsPackagePurchaseOnly(item) &&
                catalog[item.PackageId].OfferType == OfferType.Package)
            .GroupBy(item => item.PackageId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => BookingItemPricing.PackageNewPurchaseUnits(item, ownedCatalogPackageIds)),
                StringComparer.Ordinal);

        foreach (var packageId in requestedBillableUnits.Keys.Union(previousBillableUnits.Keys, StringComparer.Ordinal))
        {
            previousBillableUnits.TryGetValue(packageId, out var previousUnits);
            requestedBillableUnits.TryGetValue(packageId, out var requestedUnits);
            var delta = Math.Max(0, requestedUnits - previousUnits);
            if (delta == 0)
            {
                continue;
            }

            var product = catalog[packageId];
            for (var unit = 0; unit < delta; unit++)
            {
                var purchase = BookingItemPricing.CreatePackagePurchase(product, tenantId, booking.CustomerId, today);
                purchase.ReservedSessions = 1;
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

        return Task.CompletedTask;
    }
}
