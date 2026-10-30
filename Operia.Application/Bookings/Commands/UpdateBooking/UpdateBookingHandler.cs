using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Commands.CreateBooking;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages;
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
    IIdentityService identity,
    IDateTimeProvider clock) : IRequestHandler<UpdateBookingCommand, UpdateBookingResult>
{
    /// <summary>Checks the version, reconciles edited services, and saves the booking with its history.</summary>
    public async Task<UpdateBookingResult> Handle(UpdateBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var booking = await LoadEditableBookingAsync(tenantId, request, cancellationToken);
        var catalog = await LoadCatalogAsync(tenantId, request.Items, cancellationToken);
        BookingItemPricing.ValidatePackageSelection(request.Items, catalog);

        var itemsChanged = HasItemsChanged(booking.Items, request.Items) ||
            await HasExplicitReservationChangedAsync(booking, request.Items, tenantId, cancellationToken);
        var containsInstallment = booking.Items.Any(x =>
            string.Equals(x.PaymentOption, "installment", StringComparison.OrdinalIgnoreCase));
        if ((containsInstallment && itemsChanged) ||
            (!containsInstallment && request.Items.Any(BookingItemPricing.IsInstallmentPurchase)))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Bookings.InstallmentEditNotAllowed, "items");
        }

        var addedProductIds = request.Items
            .GroupBy(item => item.PackageId)
            .Where(group => group.Sum(item => item.Quantity) >
                            booking.Items.Where(line => line.PackageId == group.Key).Sum(line => line.Quantity))
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (addedProductIds.Count > 0)
        {
            var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == booking.EmployeeId && item.IsActive,
                cancellationToken);
            var assigned = employee is not null && await db.UserBranches.AsNoTracking().AnyAsync(
                item => item.TenantId == tenantId && item.EmployeeId == employee.Id &&
                        item.BranchId == booking.BranchId,
                cancellationToken);
            if (!assigned)
            {
                throw ConflictException.FromCode(ApiErrorCodes.Packages.PackageStaffUnavailable, "items");
            }
            var roles = await identity.GetRolesAsync([employee!.IdentityUserId], cancellationToken);
            if (!string.Equals(roles.GetValueOrDefault(employee.IdentityUserId), Roles.Staff,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw ConflictException.FromCode(ApiErrorCodes.Packages.PackageStaffUnavailable, "items");
            }
        }
        if (addedProductIds.Any(id =>
                catalog.TryGetValue(id, out var product) &&
                !product.AllStaff && product.AvailableStaff.All(staff => staff.EmployeeId != booking.EmployeeId)))
        {
            throw ConflictException.FromCode(ApiErrorCodes.Packages.PackageStaffUnavailable, "items");
        }
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
            .Include(x => x.AvailableStaff)
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

    /// <summary>Detects a replacement of the selected owned purchase for an otherwise unchanged line.</summary>
    private async Task<bool> HasExplicitReservationChangedAsync(
        Booking booking,
        IReadOnlyList<CreateBookingItemInput> requestedItems,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var selectedIds = requestedItems
            .Where(x => !string.IsNullOrWhiteSpace(x.CustomerPackageId))
            .Select(x => x.CustomerPackageId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (requestedItems.Any(item =>
                BookingItemPricing.IsExplicitNewPurchase(item) &&
                booking.Items.Any(existing => existing.PackageId == item.PackageId && existing.UnitPrice == 0)))
        {
            return true;
        }

        if (selectedIds.Count == 0)
        {
            return false;
        }

        var activeIds = await db.BookingPackageReservations.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BookingId == booking.Id &&
                        x.CancellationAtUtc == null && selectedIds.Contains(x.CustomerPackageId))
            .Select(x => x.CustomerPackageId)
            .ToListAsync(cancellationToken);
        return selectedIds.Any(x => !activeIds.Contains(x, StringComparer.Ordinal));
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
        var ownedCatalogPackageIds = await LoadOwnedCatalogPackageIdsAsync(
            tenantId, booking.CustomerId, booking.Id, catalog, today, cancellationToken);
        await ReconcileStandaloneReservationsAsync(booking, requestedItems, catalog, tenantId, cancellationToken);
        var billableUnitsByItem = await ReconcilePackageReservationsAsync(
            booking,
            requestedItems,
            catalog,
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
            int? billableUnits = billableUnitsByItem.TryGetValue(input, out var count) ? count : null;
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
                UnitPrice = ResolveUnitPrice(input, product, existingItem, ownedCatalogPackageIds, billableUnits)
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
        IReadOnlySet<string> ownedCatalogPackageIds,
        int? billableUnits = null)
    {
        if (product.OfferType == OfferType.Package)
        {
            if (BookingItemPricing.IsExplicitNewPurchase(input))
            {
                return BookingItemPricing.CashPrice(product);
            }

            if (existingItem is not null)
            {
                if (existingItem.Quantity == input.Quantity)
                {
                    return existingItem.UnitPrice;
                }

                if (billableUnits.HasValue)
                {
                    return input.Quantity == 0 ? 0 : billableUnits.Value * BookingItemPricing.CashPrice(product) / input.Quantity;
                }
                var addedPackageUnits = Math.Max(0, input.Quantity - existingItem.Quantity);
                return addedPackageUnits * BookingItemPricing.CashPrice(product) / input.Quantity;
            }

            return billableUnits.HasValue
                ? (input.Quantity == 0 ? 0 : billableUnits.Value * BookingItemPricing.CashPrice(product) / input.Quantity)
                : BookingItemPricing.CalculateItemTotal(input, product, ownedCatalogPackageIds) / input.Quantity;
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
        return input.Quantity == 0 ? 0 : addedUnits * BookingItemPricing.CashPrice(product) / input.Quantity;
    }

    /// <summary>Loads active owned catalog package ids for purchase-only pricing.</summary>
    private async Task<HashSet<string>> LoadOwnedCatalogPackageIdsAsync(
        string tenantId,
        string customerId,
        string bookingId,
        IReadOnlyDictionary<string, Package> catalog,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var reservedIds = await db.BookingPackageReservations.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BookingId == bookingId && x.CancellationAtUtc == null)
            .Select(x => x.CustomerPackageId)
            .ToHashSetAsync(StringComparer.Ordinal, cancellationToken);
        var balances = await db.CustomerPackages.AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        x.CustomerId == customerId &&
                        x.IsActive &&
                        (x.ExpiresOn == null || x.ExpiresOn >= today))
            .ToListAsync(cancellationToken);
        return balances
            .Where(x => catalog.TryGetValue(x.PackageId, out var product) &&
                        ((x.OriginBookingId != bookingId && reservedIds.Contains(x.Id)) || CustomerPackageBalance.HasAvailable(
                            x.Total, x.Used, x.ReservedSessions, product.OfferType, product.PulseCount)))
            .Select(x => x.PackageId)
            .ToHashSet(StringComparer.Ordinal);
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
                AddNewStandalonePurchaseReservation(booking, tenantId, catalog[packageId]);
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
        if (owned.OriginBookingId == reservation.BookingId && owned.ReservedSessions == 0)
        {
            owned.IsActive = false;
        }
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

        var persisted = await db.BookingPackageReservations
            .Where(x => x.TenantId == tenantId && x.CustomerPackageId == owned.Id)
            .ToListAsync(cancellationToken);
        var occupied = persisted.Concat(db.BookingPackageReservations.Local)
            .Where(x => x.CustomerPackageId == owned.Id && x.CancellationAtUtc == null)
            .Select(x => x.SessionNumber)
            .ToHashSet();
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
    private void AddNewStandalonePurchaseReservation(Booking booking, string tenantId, Package product)
    {
        var currentCashPrice = BookingItemPricing.CashPrice(product);
        var existingPrice = booking.Items.FirstOrDefault(item => item.PackageId == product.Id)?.UnitPrice;
        var purchasePrice = existingPrice is > 0 ? existingPrice.Value : currentCashPrice;
        var priorLine = booking.Items.FirstOrDefault(item => item.PackageId == product.Id);
        var snapshot = PackagePurchaseSnapshot.Capture(product, "cash", purchasePrice) with
        {
            OfferName = priorLine?.Name ?? product.Name,
            SessionDurationMinutes = priorLine?.DurationMinutes ?? product.SessionDurationMinutes
        };
        var purchase = new CustomerPackage
        {
            TenantId = tenantId,
            CustomerId = booking.CustomerId,
            PackageId = product.Id,
            OriginBookingId = booking.Id,
            PurchasePrice = purchasePrice,
            AmountDue = purchasePrice,
            PurchaseSnapshotJson = snapshot.ToJson(),
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

    /// <summary>Reconciles package reservations when package lines are added, removed, or replaced.</summary>
    private async Task<Dictionary<CreateBookingItemInput, int>> ReconcilePackageReservationsAsync(
        Booking booking,
        IReadOnlyList<CreateBookingItemInput> requestedItems,
        IReadOnlyDictionary<string, Package> catalog,
        string tenantId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var active = await db.BookingPackageReservations
            .Include(x => x.CustomerPackage)
            .ThenInclude(x => x!.Package)
            .Where(x => x.TenantId == tenantId && x.BookingId == booking.Id && x.CancellationAtUtc == null)
            .ToListAsync(cancellationToken);
        var unmatched = active
            .Where(x => x.CustomerPackage?.Package?.OfferType == OfferType.Package)
            .ToList();
        var pending = new List<(CreateBookingItemInput Item, int Needed, List<BookingPackageReservation> Retained)>();

        foreach (var item in requestedItems.Where(x => catalog[x.PackageId].OfferType == OfferType.Package))
        {
            var desired = BookingItemPricing.IsPackagePurchaseOnly(item) ? item.Quantity : 1;
            var replacingOwnedWithPurchase = BookingItemPricing.IsExplicitNewPurchase(item) &&
                booking.Items.Any(existing => existing.PackageId == item.PackageId && existing.UnitPrice == 0);
            var matching = unmatched
                .Where(x => x.CustomerPackage?.PackageId == item.PackageId &&
                    !replacingOwnedWithPurchase &&
                    (string.IsNullOrWhiteSpace(item.CustomerPackageId) || x.CustomerPackageId == item.CustomerPackageId))
                .OrderByDescending(x => x.CustomerPackage?.OriginBookingId != booking.Id)
                .Take(desired)
                .ToList();
            foreach (var reservation in matching)
            {
                unmatched.Remove(reservation);
            }
            pending.Add((item, desired - matching.Count, matching));
        }

        foreach (var reservation in unmatched)
        {
            CancelStandaloneReservation(reservation);
        }

        var billableUnitsByItem = new Dictionary<CreateBookingItemInput, int>(ReferenceEqualityComparer.Instance);

        foreach (var (item, needed, retained) in pending)
        {
            var product = catalog[item.PackageId];
            var retainedPurchases = retained.Count(x => x.CustomerPackage?.OriginBookingId == booking.Id);
            var retainedOwned = retained.Count(x => x.CustomerPackage?.OriginBookingId != booking.Id);
            var newlyPurchased = 0;
            for (var unit = 0; unit < needed; unit++)
            {
                var ownedId = unit == 0 && retainedOwned == 0 ? item.CustomerPackageId : null;
                if (string.IsNullOrWhiteSpace(ownedId) &&
                    BookingItemPricing.IsPackagePurchaseOnly(item) &&
                    !BookingItemPricing.IsExplicitNewPurchase(item) && unit == 0 && retainedOwned == 0)
                {
                    ownedId = await FindAvailableOwnedPackageAsync(
                        booking, tenantId, product, today, cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(ownedId))
                {
                    await AddOwnedStandaloneReservationAsync(
                        booking, tenantId, ownedId, item.PackageId, today, cancellationToken);
                    retainedOwned++;
                    continue;
                }

                var purchase = BookingItemPricing.CreatePackagePurchase(
                    product, tenantId, booking.CustomerId, today, originBookingId: booking.Id);
                purchase.ReservedSessions = 1;
                db.CustomerPackages.Add(purchase);
                db.BookingPackageReservations.Add(new BookingPackageReservation
                {
                    TenantId = tenantId,
                    BookingId = booking.Id,
                    CustomerPackageId = purchase.Id,
                    SessionNumber = 1
                });
                newlyPurchased++;
            }

            if (!BookingItemPricing.IsPackagePurchaseOnly(item))
            {
                var oldQuantity = booking.Items
                    .Where(x => x.PackageId == item.PackageId)
                    .Select(x => x.Quantity)
                    .DefaultIfEmpty(0)
                    .Max();
                var extraPurchases = Math.Max(0, item.Quantity - Math.Max(1, oldQuantity));
                for (var unit = 0; unit < extraPurchases; unit++)
                {
                    db.CustomerPackages.Add(BookingItemPricing.CreatePackagePurchase(
                        product, tenantId, booking.CustomerId, today, originBookingId: booking.Id));
                    newlyPurchased++;
                }
            }

            billableUnitsByItem[item] = retainedPurchases + newlyPurchased;
        }

        return billableUnitsByItem;
    }

    /// <summary>Finds an available owned package that this booking has not already reserved.</summary>
    private async Task<string?> FindAvailableOwnedPackageAsync(
        Booking booking,
        string tenantId,
        Package product,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var owned = await db.CustomerPackages
            .Where(x => x.TenantId == tenantId && x.CustomerId == booking.CustomerId &&
                        x.PackageId == product.Id && x.IsActive &&
                        x.OriginBookingId != booking.Id &&
                        (x.ExpiresOn == null || x.ExpiresOn >= today))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var alreadyReserved = db.BookingPackageReservations.Local
            .Where(x => x.BookingId == booking.Id && x.CancellationAtUtc == null)
            .Select(x => x.CustomerPackageId)
            .ToHashSet(StringComparer.Ordinal);
        return owned.FirstOrDefault(x =>
            !alreadyReserved.Contains(x.Id) &&
            CustomerPackageBalance.HasAvailable(
                x.Total, x.Used, x.ReservedSessions, product.OfferType, product.PulseCount))?.Id;
    }
}
