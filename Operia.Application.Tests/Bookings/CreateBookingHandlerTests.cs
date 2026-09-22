using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Bookings.Commands.CreateBooking;
using Operia.Application.Bookings.Commands.CancelBooking;
using Operia.Application.Bookings.Commands.UpdateBooking;
using Operia.Application.Bookings.Queries.GetCalendarBookings;
using Operia.Application.Bookings.Queries.FindBookingCustomer;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Bookings;

/// <summary>Verifies booking creation, edits, cancellation, reservations, and audit behavior.</summary>
public sealed class CreateBookingHandlerTests
{
    private readonly Mock<ICurrentUserService> _user = new();
    private readonly Mock<IBranchScope> _branchScope = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly TestApplicationDbContext _db;
    private readonly DateOnly _scheduledDate = new(2026, 9, 18);

    public CreateBookingHandlerTests()
    {
        _user.SetupGet(x => x.UserId).Returns("operator-1");
        _user.SetupGet(x => x.TenantId).Returns("tenant-1");
        _user.SetupGet(x => x.DisplayName).Returns("Mona");
        _branchScope.Setup(x => x.GetAllowedBranchIdsAsync("operator-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-1"]);
        _clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc));
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"CreateBooking_{Guid.NewGuid()}")
            .Options;
        _db = new TestApplicationDbContext(options, _clock.Object, _user.Object);
    }

    [Fact]
    public async Task Handle_PersistsBookingHistoryAuditAndOnePackageReservation()
    {
        await SeedAsync();
        var command = Command("request-1", 600, 660);

        var first = await CreateHandler().Handle(command, CancellationToken.None);
        var repeated = await CreateHandler().Handle(command, CancellationToken.None);

        first.Id.Should().Be(repeated.Id);
        first.Status.Should().Be(nameof(BookingStatus.Booked));
        (await _db.Bookings.CountAsync()).Should().Be(1);
        (await _db.BookingHistory.CountAsync()).Should().Be(1);
        (await _db.AuditLogs.CountAsync()).Should().Be(1);
        (await _db.BookingPackageReservations.CountAsync()).Should().Be(1);
        (await _db.CustomerPackages.SingleAsync()).ReservedSessions.Should().Be(1);
    }

    [Fact]
    public async Task Create_PackageAndExtraService_AppearInCalendarDetails()
    {
        await SeedAsync();
        await CreateHandler().Handle(
            new CreateBookingCommand(
                "request-with-extra",
                "customer-1",
                "branch-1",
                "employee-1",
                _scheduledDate,
                600,
                690,
                [
                    new CreateBookingItemInput("package-1", "owned-1", 1),
                    new CreateBookingItemInput("service-1", null, 1)
                ]),
            CancellationToken.None);

        var calendar = await new GetCalendarBookingsHandler(
                _db,
                _user.Object,
                _branchScope.Object,
                _clock.Object)
            .Handle(
                new GetCalendarBookingsQuery("branch-1", _scheduledDate, _scheduledDate, "employee-1"),
                CancellationToken.None);

        calendar.Bookings.Single().Items.Select(item => item.Name)
            .Should().BeEquivalentTo("Laser Package", "Standalone Service");
    }

    [Fact]
    public async Task Handle_RejectsPartialOverlapWithoutSavingAnything()
    {
        await SeedAsync();
        await CreateHandler().Handle(Command("request-1", 600, 660), CancellationToken.None);

        var action = () => CreateHandler().Handle(Command("request-2", 630, 690), CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ApiErrorCodes.Bookings.SlotUnavailable);
        (await _db.Bookings.CountAsync()).Should().Be(1);
        (await _db.CustomerPackages.SingleAsync()).ReservedSessions.Should().Be(1);
    }

    [Fact]
    public async Task Cancel_ServiceOnlyBooking_PersistsStatusHistoryAndAudit()
    {
        await SeedAsync();
        var booking = new Booking
        {
            Id = "service-booking",
            TenantId = "tenant-1",
            BookingNumber = "OP-260918-SERVICE",
            IdempotencyKey = "service-request",
            BranchId = "branch-1",
            EmployeeId = "employee-1",
            CustomerId = "customer-1",
            CustomerName = "Ahmed Ali",
            CustomerMobile = "01012345678",
            ScheduledDate = _scheduledDate,
            StartMinutes = 720,
            EndMinutes = 750
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        var result = await new CancelBookingHandler(
                _db,
                _user.Object,
                _branchScope.Object,
                new AuditWriter(_db, _user.Object),
                new BookingConcurrencyStore(_db),
                _clock.Object)
            .Handle(new CancelBookingCommand(booking.Id, Convert.ToBase64String(booking.Version)), CancellationToken.None);

        result.Status.Should().Be(nameof(BookingStatus.Cancelled));
        (await _db.BookingHistory.CountAsync(x => x.BookingId == booking.Id)).Should().Be(1);
        (await _db.AuditLogs.CountAsync(x => x.EntityId == booking.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Cancel_LegacyStandaloneBooking_CreatesReturnedOneSessionBalance()
    {
        await SeedAsync();
        var booking = new Booking
        {
            TenantId = "tenant-1",
            BookingNumber = "OP-260918-LEGACY",
            IdempotencyKey = "legacy-service",
            BranchId = "branch-1",
            EmployeeId = "employee-1",
            CustomerId = "customer-1",
            CustomerName = "Ahmed Ali",
            CustomerMobile = "01012345678",
            ScheduledDate = _scheduledDate,
            StartMinutes = 720,
            EndMinutes = 750
        };
        _db.Bookings.Add(booking);
        _db.BookingItems.Add(new BookingItem
        {
            TenantId = "tenant-1",
            BookingId = booking.Id,
            PackageId = "service-1",
            Type = BookingItemType.Service,
            Name = "Standalone Service",
            Quantity = 1,
            DurationMinutes = 30,
            UnitPrice = 250
        });
        await _db.SaveChangesAsync();

        await new CancelBookingHandler(
                _db, _user.Object, _branchScope.Object, new AuditWriter(_db, _user.Object),
                new BookingConcurrencyStore(_db), _clock.Object)
            .Handle(new CancelBookingCommand(booking.Id, Convert.ToBase64String(booking.Version)), CancellationToken.None);

        var returned = await _db.CustomerPackages.SingleAsync(x => x.PackageId == "service-1");
        returned.Total.Should().Be(1);
        returned.ReservedSessions.Should().Be(0);
        (await _db.BookingPackageReservations.SingleAsync()).CancellationAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Cancel_PackageBooking_ReturnsItsSessionToTheBalance()
    {
        await SeedAsync();
        var created = await CreateHandler().Handle(Command("request-1", 600, 660), CancellationToken.None);
        var booking = await _db.Bookings.SingleAsync(x => x.Id == created.Id);

        var result = await new CancelBookingHandler(
                _db,
                _user.Object,
                _branchScope.Object,
                new AuditWriter(_db, _user.Object),
                new BookingConcurrencyStore(_db),
                _clock.Object)
            .Handle(new CancelBookingCommand(booking.Id, Convert.ToBase64String(booking.Version)), CancellationToken.None);

        result.Status.Should().Be(nameof(BookingStatus.Cancelled));
        (await _db.CustomerPackages.SingleAsync()).ReservedSessions.Should().Be(0);
        (await _db.BookingPackageReservations.SingleAsync()).CancellationAtUtc.Should().Be(_clock.Object.UtcNow);
    }

    [Fact]
    public async Task Cancel_StandaloneService_ReturnsOneSessionThatCanBeRebookedWithoutAnotherCharge()
    {
        await SeedAsync();
        var created = await CreateHandler().Handle(
            new CreateBookingCommand(
                "standalone-1", "customer-1", "branch-1", "employee-1", _scheduledDate,
                720, 750, [new CreateBookingItemInput("service-1", null, 1)]),
            CancellationToken.None);
        var purchase = await _db.CustomerPackages.SingleAsync(x => x.PackageId == "service-1");
        purchase.Total.Should().Be(1);
        purchase.ReservedSessions.Should().Be(1);
        (await _db.Bookings.SingleAsync(x => x.Id == created.Id)).TotalAmount.Should().Be(250);

        var cancelled = await new CancelBookingHandler(
                _db, _user.Object, _branchScope.Object, new AuditWriter(_db, _user.Object),
                new BookingConcurrencyStore(_db), _clock.Object)
            .Handle(new CancelBookingCommand(created.Id, created.Version), CancellationToken.None);

        cancelled.Status.Should().Be(nameof(BookingStatus.Cancelled));
        purchase.ReservedSessions.Should().Be(0);
        (await _db.BookingPackageReservations.SingleAsync()).CancellationAtUtc.Should().NotBeNull();
        var found = await new FindBookingCustomerHandler(_db, _user.Object, _clock.Object)
            .Handle(new FindBookingCustomerQuery("01012345678"), CancellationToken.None);
        found!.Packages.Single(x => x.CustomerPackageId == purchase.Id).OfferType.Should().Be("session");
        found.Packages.Single(x => x.CustomerPackageId == purchase.Id).AvailableSessions.Should().Be(1);
        found.Packages.Single(x => x.CustomerPackageId == purchase.Id).PulseCount.Should().BeNull();
        found.Packages.Single(x => x.CustomerPackageId == purchase.Id).SessionCount.Should().BeNull();

        var rebooked = await CreateHandler().Handle(
            new CreateBookingCommand(
                "standalone-2", "customer-1", "branch-1", "employee-1", _scheduledDate,
                720, 750, [new CreateBookingItemInput("service-1", purchase.Id, 1)]),
            CancellationToken.None);

        (await _db.Bookings.SingleAsync(x => x.Id == rebooked.Id)).TotalAmount.Should().Be(0);
        purchase.ReservedSessions.Should().Be(1);
        (await _db.BookingPackageReservations.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Cancel_MultipleStandaloneUnits_ReturnsEachOneSessionPurchase()
    {
        await SeedAsync();
        var created = await CreateHandler().Handle(
            new CreateBookingCommand(
                "two-services", "customer-1", "branch-1", "employee-1", _scheduledDate,
                720, 780, [new CreateBookingItemInput("service-1", null, 2)]),
            CancellationToken.None);

        var purchases = await _db.CustomerPackages.Where(x => x.PackageId == "service-1").ToListAsync();
        purchases.Should().HaveCount(2);
        purchases.Should().OnlyContain(x => x.Total == 1 && x.ReservedSessions == 1);

        await new CancelBookingHandler(
                _db, _user.Object, _branchScope.Object, new AuditWriter(_db, _user.Object),
                new BookingConcurrencyStore(_db), _clock.Object)
            .Handle(new CancelBookingCommand(created.Id, created.Version), CancellationToken.None);

        purchases.Should().OnlyContain(x => x.ReservedSessions == 0);
        (await _db.BookingPackageReservations.CountAsync(x => x.CancellationAtUtc != null)).Should().Be(2);
    }

    [Fact]
    public async Task Cancel_MixedBooking_ReleasesPackageAndStandaloneBalancesOnce()
    {
        await SeedAsync();
        var created = await CreateHandler().Handle(
            new CreateBookingCommand(
                "mixed-1", "customer-1", "branch-1", "employee-1", _scheduledDate,
                600, 690,
                [
                    new CreateBookingItemInput("package-1", "owned-1", 1),
                    new CreateBookingItemInput("service-1", null, 1)
                ]),
            CancellationToken.None);

        var handler = new CancelBookingHandler(
            _db, _user.Object, _branchScope.Object, new AuditWriter(_db, _user.Object),
            new BookingConcurrencyStore(_db), _clock.Object);
        var first = await handler.Handle(new CancelBookingCommand(created.Id, created.Version), CancellationToken.None);
        var second = await handler.Handle(new CancelBookingCommand(created.Id, created.Version), CancellationToken.None);

        first.Status.Should().Be(nameof(BookingStatus.Cancelled));
        second.Status.Should().Be(nameof(BookingStatus.Cancelled));
        (await _db.CustomerPackages.SingleAsync(x => x.Id == "owned-1")).ReservedSessions.Should().Be(0);
        (await _db.CustomerPackages.SingleAsync(x => x.PackageId == "service-1")).ReservedSessions.Should().Be(0);
        (await _db.BookingPackageReservations.CountAsync(x => x.CancellationAtUtc != null)).Should().Be(2);
        (await _db.BookingHistory.CountAsync(x => x.BookingId == created.Id)).Should().Be(2);
    }

    [Fact]
    public async Task Cancel_EarlierPackageSession_AllowsItsNumberToBeReservedAgain()
    {
        await SeedAsync();
        var first = await CreateHandler().Handle(Command("package-first", 600, 660), CancellationToken.None);
        var second = await CreateHandler().Handle(Command("package-second", 720, 780), CancellationToken.None);
        var firstReservation = await _db.BookingPackageReservations.SingleAsync(x => x.BookingId == first.Id);

        await new CancelBookingHandler(
                _db, _user.Object, _branchScope.Object, new AuditWriter(_db, _user.Object),
                new BookingConcurrencyStore(_db), _clock.Object)
            .Handle(new CancelBookingCommand(first.Id, first.Version), CancellationToken.None);
        var replacement = await CreateHandler().Handle(Command("package-replacement", 600, 660), CancellationToken.None);

        var secondReservation = await _db.BookingPackageReservations.SingleAsync(x => x.BookingId == second.Id);
        var replacementReservation = await _db.BookingPackageReservations.SingleAsync(x => x.BookingId == replacement.Id);
        firstReservation.CancellationAtUtc.Should().NotBeNull();
        replacementReservation.SessionNumber.Should().Be(firstReservation.SessionNumber);
        secondReservation.SessionNumber.Should().NotBe(replacementReservation.SessionNumber);
        (await _db.CustomerPackages.SingleAsync(x => x.Id == "owned-1")).ReservedSessions.Should().Be(2);
    }

    [Fact]
    public async Task Cancel_WithStaleVersion_DoesNotReleaseAnyBalance()
    {
        await SeedAsync();
        var created = await CreateHandler().Handle(Command("stale-package", 600, 660), CancellationToken.None);

        var action = () => new CancelBookingHandler(
                _db, _user.Object, _branchScope.Object, new AuditWriter(_db, _user.Object),
                new BookingConcurrencyStore(_db), _clock.Object)
            .Handle(new CancelBookingCommand(created.Id, Convert.ToBase64String([1, 2, 3])), CancellationToken.None);

        var error = await action.Should().ThrowAsync<ConflictException>();
        error.Which.ErrorCode.Should().Be(ApiErrorCodes.Bookings.Changed);
        (await _db.CustomerPackages.SingleAsync(x => x.Id == "owned-1")).ReservedSessions.Should().Be(1);
        (await _db.BookingPackageReservations.SingleAsync()).CancellationAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Rebook_AnotherCustomersStandaloneBalance_IsRejected()
    {
        await SeedAsync();
        _db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "someone-elses-service",
            TenantId = "tenant-1",
            CustomerId = "other-customer",
            PackageId = "service-1",
            Total = 1,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var action = () => CreateHandler().Handle(
            new CreateBookingCommand(
                "other-balance", "customer-1", "branch-1", "employee-1", _scheduledDate,
                720, 750, [new CreateBookingItemInput("service-1", "someone-elses-service", 1)]),
            CancellationToken.None);

        var error = await action.Should().ThrowAsync<ConflictException>();
        error.Which.ErrorCode.Should().Be(ApiErrorCodes.Bookings.PackageSessionUnavailable);
        (await _db.Bookings.CountAsync()).Should().Be(0);
        (await _db.BookingPackageReservations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Update_AddingAnotherStandaloneUnit_ToAReusedBooking_ChargesOnlyTheNewUnit()
    {
        await SeedAsync();
        var original = await CreateHandler().Handle(
            new CreateBookingCommand(
                "original-service", "customer-1", "branch-1", "employee-1", _scheduledDate,
                720, 750, [new CreateBookingItemInput("service-1", null, 1)]),
            CancellationToken.None);
        var purchase = await _db.CustomerPackages.SingleAsync(x => x.PackageId == "service-1");
        await new CancelBookingHandler(
                _db, _user.Object, _branchScope.Object, new AuditWriter(_db, _user.Object),
                new BookingConcurrencyStore(_db), _clock.Object)
            .Handle(new CancelBookingCommand(original.Id, original.Version), CancellationToken.None);
        var reused = await CreateHandler().Handle(
            new CreateBookingCommand(
                "reused-service", "customer-1", "branch-1", "employee-1", _scheduledDate,
                720, 780, [new CreateBookingItemInput("service-1", purchase.Id, 1)]),
            CancellationToken.None);

        await new UpdateBookingHandler(
                _db, _user.Object, _branchScope.Object, new AuditWriter(_db, _user.Object), _clock.Object)
            .Handle(
                new UpdateBookingCommand(
                    reused.Id,
                    reused.Version,
                    [
                        new CreateBookingItemInput("service-1", null, 1),
                        new CreateBookingItemInput("service-1", null, 1)
                    ]),
                CancellationToken.None);

        var updated = await _db.Bookings.Include(x => x.Items).SingleAsync(x => x.Id == reused.Id);
        updated.TotalAmount.Should().Be(250);
        updated.Items.Select(x => x.UnitPrice).Should().BeEquivalentTo(new[] { 0m, 250m });
        (await _db.BookingPackageReservations.CountAsync(x => x.BookingId == reused.Id && x.CancellationAtUtc == null))
            .Should().Be(2);
    }

    [Fact]
    public async Task Update_AddsStandaloneServiceWithoutChangingPackageReservation()
    {
        await SeedAsync();
        var created = await CreateHandler().Handle(Command("request-1", 600, 660), CancellationToken.None);
        var booking = await _db.Bookings.SingleAsync(x => x.Id == created.Id);

        await new UpdateBookingHandler(
                _db,
                _user.Object,
                _branchScope.Object,
                new AuditWriter(_db, _user.Object),
                _clock.Object)
            .Handle(
                new UpdateBookingCommand(
                    booking.Id,
                    Convert.ToBase64String(booking.Version),
                    [
                        new CreateBookingItemInput("package-1", "owned-1", 1),
                        new CreateBookingItemInput("service-1", null, 1)
                    ]),
                CancellationToken.None);

        (await _db.CustomerPackages.SingleAsync(x => x.Id == "owned-1")).ReservedSessions.Should().Be(1);
        (await _db.CustomerPackages.SingleAsync(x => x.PackageId == "service-1")).ReservedSessions.Should().Be(1);
        (await _db.BookingItems.CountAsync(x => x.BookingId == booking.Id)).Should().Be(2);
        (await _db.BookingHistory.CountAsync(x => x.BookingId == booking.Id)).Should().Be(2);
        (await _db.Bookings.SingleAsync(x => x.Id == booking.Id)).TotalAmount.Should().Be(250);

        var calendar = await new GetCalendarBookingsHandler(
                _db,
                _user.Object,
                _branchScope.Object,
                _clock.Object)
            .Handle(
                new GetCalendarBookingsQuery("branch-1", _scheduledDate, _scheduledDate, "employee-1"),
                CancellationToken.None);
        calendar.Bookings.Single().Items.Select(item => item.Name)
            .Should().BeEquivalentTo("Laser Package", "Standalone Service");
    }

    [Fact]
    public async Task Update_WithUnchangedItems_DoesNotCreateHistoryOrAuditNoise()
    {
        await SeedAsync();
        var created = await CreateHandler().Handle(Command("request-1", 600, 660), CancellationToken.None);
        var booking = await _db.Bookings.SingleAsync(x => x.Id == created.Id);

        await new UpdateBookingHandler(
                _db,
                _user.Object,
                _branchScope.Object,
                new AuditWriter(_db, _user.Object),
                _clock.Object)
            .Handle(
                new UpdateBookingCommand(
                    booking.Id,
                    Convert.ToBase64String(booking.Version),
                    [new CreateBookingItemInput("package-1", "owned-1", 1)]),
                CancellationToken.None);

        (await _db.BookingHistory.CountAsync(x => x.BookingId == booking.Id)).Should().Be(1);
        (await _db.AuditLogs.CountAsync(x => x.EntityId == booking.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Update_PaymentMethodOnly_PersistsChoiceWithoutReplacingItems()
    {
        await SeedAsync();
        var created = await CreateHandler().Handle(Command("request-payment", 600, 660), CancellationToken.None);
        var booking = await _db.Bookings.Include(x => x.Items).SingleAsync(x => x.Id == created.Id);
        var originalItemId = booking.Items.Single().Id;
        var originalTotal = booking.TotalAmount;

        await new UpdateBookingHandler(
                _db,
                _user.Object,
                _branchScope.Object,
                new AuditWriter(_db, _user.Object),
                _clock.Object)
            .Handle(
                new UpdateBookingCommand(
                    booking.Id,
                    Convert.ToBase64String(booking.Version),
                    [new CreateBookingItemInput("package-1", "owned-1", 1)],
                    "cash"),
                CancellationToken.None);

        var updated = await _db.Bookings.Include(x => x.Items).SingleAsync(x => x.Id == booking.Id);
        updated.PaymentMethod.Should().Be("cash");
        updated.Items.Should().ContainSingle().Which.Id.Should().Be(originalItemId);
        updated.TotalAmount.Should().Be(originalTotal);
        (await _db.BookingHistory.CountAsync(x => x.BookingId == booking.Id)).Should().Be(2);
        (await _db.AuditLogs.CountAsync(x => x.EntityId == booking.Id)).Should().Be(2);
    }

    [Fact]
    public async Task Update_DisabledPaymentMethod_RejectsWithoutSaving()
    {
        await SeedAsync();
        var created = await CreateHandler().Handle(Command("request-disabled-payment", 600, 660), CancellationToken.None);
        var booking = await _db.Bookings.SingleAsync(x => x.Id == created.Id);

        var action = () => new UpdateBookingHandler(
                _db,
                _user.Object,
                _branchScope.Object,
                new AuditWriter(_db, _user.Object),
                _clock.Object)
            .Handle(
                new UpdateBookingCommand(
                    booking.Id,
                    Convert.ToBase64String(booking.Version),
                    [new CreateBookingItemInput("package-1", "owned-1", 1)],
                    "bank_transfer"),
                CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ApiErrorCodes.Bookings.PaymentMethodUnavailable);
        (await _db.Bookings.SingleAsync(x => x.Id == booking.Id)).PaymentMethod.Should().BeNull();
        (await _db.BookingHistory.CountAsync(x => x.BookingId == booking.Id)).Should().Be(1);
        (await _db.AuditLogs.CountAsync(x => x.EntityId == booking.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Update_PreservesBookedPriceForRetainedServices()
    {
        await SeedAsync();
        var first = await CreateHandler().Handle(
            new CreateBookingCommand(
                "service-request-1",
                "customer-1",
                "branch-1",
                "employee-1",
                _scheduledDate,
                720,
                750,
                [new CreateBookingItemInput("service-1", null, 1)]),
            CancellationToken.None);
        var booking = await _db.Bookings.Include(x => x.Items).SingleAsync(x => x.Id == first.Id);
        booking.Items.Single().UnitPrice.Should().Be(250);

        (await _db.Packages.SingleAsync(x => x.Id == "service-1")).Price = 400;
        await _db.SaveChangesAsync();

        await new UpdateBookingHandler(
                _db,
                _user.Object,
                _branchScope.Object,
                new AuditWriter(_db, _user.Object),
                _clock.Object)
            .Handle(
                new UpdateBookingCommand(
                    booking.Id,
                    Convert.ToBase64String(booking.Version),
                    [new CreateBookingItemInput("service-1", null, 2)]),
                CancellationToken.None);

        var updated = await _db.Bookings.Include(x => x.Items).SingleAsync(x => x.Id == booking.Id);
        updated.Items.Single(x => x.PackageId == "service-1").UnitPrice.Should().Be(250);
        updated.TotalAmount.Should().Be(500);
        updated.LastModifiedAt.Should().Be(_clock.Object.UtcNow);
    }

    [Fact]
    public async Task Update_TouchesAggregateWhenReplacementHasSameTotal()
    {
        await SeedAsync();
        var first = await CreateHandler().Handle(
            new CreateBookingCommand(
                "service-request-2",
                "customer-1",
                "branch-1",
                "employee-1",
                _scheduledDate,
                720,
                750,
                [new CreateBookingItemInput("service-1", null, 1)]),
            CancellationToken.None);
        _db.Packages.Add(new Package
        {
            Id = "service-2",
            TenantId = "tenant-1",
            BusinessId = "business-1",
            Name = "Same-price replacement",
            Description = "Service",
            OfferType = OfferType.SingleSession,
            ServiceCategoryId = "category-1",
            SessionDurationMinutes = 30,
            Price = 250,
            Status = PackageStatus.Active
        });
        await _db.SaveChangesAsync();
        var booking = await _db.Bookings.SingleAsync(x => x.Id == first.Id);

        await new UpdateBookingHandler(
                _db,
                _user.Object,
                _branchScope.Object,
                new AuditWriter(_db, _user.Object),
                _clock.Object)
            .Handle(
                new UpdateBookingCommand(
                    booking.Id,
                    Convert.ToBase64String(booking.Version),
                    [new CreateBookingItemInput("service-2", null, 1)]),
                CancellationToken.None);

        var updated = await _db.Bookings.SingleAsync(x => x.Id == booking.Id);
        updated.TotalAmount.Should().Be(250);
        updated.LastModifiedAt.Should().Be(_clock.Object.UtcNow);
    }

    private CreateBookingHandler CreateHandler() =>
        new(
            _db,
            _user.Object,
            _branchScope.Object,
            new AuditWriter(_db, _user.Object),
            new BookingConcurrencyStore(_db),
            _clock.Object);

    private CreateBookingCommand Command(string key, int start, int end) =>
        new(
            key,
            "customer-1",
            "branch-1",
            "employee-1",
            _scheduledDate,
            start,
            end,
            [new CreateBookingItemInput("package-1", "owned-1", 1)]);

    [Fact]
    public async Task FindCustomer_PulsePackage_ReturnsItsPulseCount()
    {
        await SeedAsync();
        _db.Packages.Add(new Package
        {
            Id = "pulse-1",
            TenantId = "tenant-1",
            BusinessId = "business-1",
            Name = "5000 Plus",
            Description = "Pulses",
            OfferType = OfferType.Package,
            ServiceCategoryId = "category-1",
            SessionDurationMinutes = 30,
            SessionCount = null,
            PulseCount = 5000,
            Price = 5000,
            Status = PackageStatus.Active
        });
        _db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "owned-pulse",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "pulse-1",
            Total = 5000,
            Used = 0,
            ReservedSessions = null,
            IsActive = true,
            ExpiresOn = new DateOnly(2026, 12, 31)
        });
        await _db.SaveChangesAsync();

        var found = await new FindBookingCustomerHandler(_db, _user.Object, _clock.Object)
            .Handle(new FindBookingCustomerQuery("01012345678"), CancellationToken.None);

        var pulse = found!.Packages.Single(x => x.PackageId == "pulse-1");
        pulse.PulseCount.Should().Be(5000);
        pulse.SessionCount.Should().BeNull();
        pulse.TotalSessions.Should().Be(5000);
        found.Packages.Single(x => x.PackageId == "package-1").PulseCount.Should().BeNull();
        found.Packages.Single(x => x.PackageId == "package-1").SessionCount.Should().Be(6);
    }

    [Fact]
    public async Task Create_PulsePackageBooking_IncrementsReservedSessions()
    {
        await SeedAsync();
        _db.Packages.Add(new Package
        {
            Id = "pulse-1",
            TenantId = "tenant-1",
            BusinessId = "business-1",
            Name = "5000 Plus",
            Description = "Pulses",
            OfferType = OfferType.Package,
            ServiceCategoryId = "category-1",
            SessionDurationMinutes = 30,
            SessionCount = null,
            PulseCount = 5000,
            Price = 5000,
            Status = PackageStatus.Active
        });
        _db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "owned-pulse",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "pulse-1",
            Total = 5000,
            Used = 0,
            ReservedSessions = null,
            IsActive = true,
            ExpiresOn = new DateOnly(2026, 12, 31)
        });
        await _db.SaveChangesAsync();

        await CreateHandler().Handle(
            new CreateBookingCommand(
                "pulse-booking",
                "customer-1",
                "branch-1",
                "employee-1",
                _scheduledDate,
                600,
                630,
                [new CreateBookingItemInput("pulse-1", "owned-pulse", 1)]),
            CancellationToken.None);

        var owned = await _db.CustomerPackages.SingleAsync(x => x.Id == "owned-pulse");
        owned.ReservedSessions.Should().Be(1);
        (await _db.BookingPackageReservations.CountAsync(x => x.CustomerPackageId == "owned-pulse")).Should().Be(1);
    }

    [Fact]
    public async Task FindCustomer_PulsePackage_IgnoresReservedSessionsInAvailableBalance()
    {
        await SeedAsync();
        _db.Packages.Add(new Package
        {
            Id = "pulse-1",
            TenantId = "tenant-1",
            BusinessId = "business-1",
            Name = "5000 Plus",
            Description = "Pulses",
            OfferType = OfferType.Package,
            ServiceCategoryId = "category-1",
            SessionDurationMinutes = 30,
            SessionCount = null,
            PulseCount = 5000,
            Price = 5000,
            Status = PackageStatus.Active
        });
        _db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "owned-pulse",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "pulse-1",
            Total = 5000,
            Used = 0,
            ReservedSessions = 3,
            IsActive = true,
            ExpiresOn = new DateOnly(2026, 12, 31)
        });
        await _db.SaveChangesAsync();

        var found = await new FindBookingCustomerHandler(_db, _user.Object, _clock.Object)
            .Handle(new FindBookingCustomerQuery("01012345678"), CancellationToken.None);

        var pulse = found!.Packages.Single(x => x.PackageId == "pulse-1");
        pulse.ReservedSessions.Should().Be(3);
        pulse.AvailableSessions.Should().Be(5000);
    }

    [Fact]
    public async Task FindCustomer_SingleSession_SubtractsReservedSessionsInAvailableBalance()
    {
        await SeedAsync();
        _db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "owned-service",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "service-1",
            Total = 2,
            Used = 0,
            ReservedSessions = 1,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var found = await new FindBookingCustomerHandler(_db, _user.Object, _clock.Object)
            .Handle(new FindBookingCustomerQuery("01012345678"), CancellationToken.None);

        var service = found!.Packages.Single(x => x.PackageId == "service-1");
        service.OfferType.Should().Be("session");
        service.ReservedSessions.Should().Be(1);
        service.AvailableSessions.Should().Be(1);
    }

    [Fact]
    public async Task Update_WithBookingSessionAndPurchasePackage_AllowsUnchangedSave()
    {
        await SeedAsync();
        _db.Packages.Add(new Package
        {
            Id = "pulse-1",
            TenantId = "tenant-1",
            BusinessId = "business-1",
            Name = "5000 Plus",
            Description = "Pulses",
            OfferType = OfferType.Package,
            ServiceCategoryId = "category-1",
            SessionDurationMinutes = 30,
            SessionCount = null,
            PulseCount = 5000,
            Price = 5000,
            Status = PackageStatus.Active
        });
        _db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "owned-pulse",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "pulse-1",
            Total = 5000,
            Used = 0,
            ReservedSessions = 0,
            IsActive = true,
            ExpiresOn = new DateOnly(2026, 12, 31)
        });
        await _db.SaveChangesAsync();

        var created = await CreateHandler().Handle(
            new CreateBookingCommand(
                "multi-package-booking",
                "customer-1",
                "branch-1",
                "employee-1",
                _scheduledDate,
                600,
                630,
                [
                    new CreateBookingItemInput("package-1", "owned-1", 1),
                    new CreateBookingItemInput("pulse-1", null, 1, "packagePurchase")
                ]),
            CancellationToken.None);

        var booking = await _db.Bookings.SingleAsync(x => x.Id == created.Id);

        var action = async () => await new UpdateBookingHandler(
                _db,
                _user.Object,
                _branchScope.Object,
                new AuditWriter(_db, _user.Object),
                _clock.Object)
            .Handle(
                new UpdateBookingCommand(
                    booking.Id,
                    Convert.ToBase64String(booking.Version),
                    [
                        new CreateBookingItemInput("package-1", "owned-1", 1),
                        new CreateBookingItemInput("pulse-1", null, 1, "packagePurchase")
                    ],
                    "cash"),
                CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    private async Task SeedAsync()
    {
        var business = new Business
        {
            Id = "business-1",
            TenantId = "tenant-1",
            ActivityName = "Business"
        };
        _db.Businesses.Add(business);
        _db.Branches.Add(new Branch
        {
            Id = "branch-1",
            TenantId = "tenant-1",
            BusinessId = business.Id,
            Name = "Main",
            Address = "Address",
            PhoneNumber = "01000000000",
            GoogleMapsUrl = "https://maps.example"
        });
        _db.Employees.Add(new Employee
        {
            Id = "employee-1",
            TenantId = "tenant-1",
            BusinessId = business.Id,
            IdentityUserId = "employee-user",
            Code = "EMP-1",
            FullName = "Dr Sara",
            Email = "sara@example.com",
            MobileNumber = "01000000001",
            JoiningDate = _scheduledDate
        });
        _db.UserBranches.Add(new UserBranch
        {
            TenantId = "tenant-1",
            EmployeeId = "employee-1",
            BranchId = "branch-1"
        });
        _db.EmployeeWorkingDays.Add(new EmployeeWorkingDay
        {
            TenantId = "tenant-1",
            EmployeeId = "employee-1",
            BranchId = "branch-1",
            Day = _scheduledDate.DayOfWeek.ToString()[..3].ToLowerInvariant(),
            Enabled = true,
            FromTime = new TimeOnly(8, 0),
            ToTime = new TimeOnly(18, 0)
        });
        _db.Customers.Add(new Customer
        {
            Id = "customer-1",
            TenantId = "tenant-1",
            FullName = "Ahmed Ali",
            MobileNumber = "01012345678",
            NormalizedMobileNumber = "01012345678"
        });
        _db.Packages.Add(new Package
        {
            Id = "package-1",
            TenantId = "tenant-1",
            BusinessId = business.Id,
            Name = "Laser Package",
            Description = "Package",
            OfferType = OfferType.Package,
            ServiceCategoryId = "category-1",
            SessionDurationMinutes = 60,
            SessionCount = 6,
            Price = 1200,
            Status = PackageStatus.Active
        });
        _db.Packages.Add(new Package
        {
            Id = "service-1",
            TenantId = "tenant-1",
            BusinessId = business.Id,
            Name = "Standalone Service",
            Description = "Service",
            OfferType = OfferType.SingleSession,
            ServiceCategoryId = "category-1",
            SessionDurationMinutes = 30,
            Price = 250,
            Status = PackageStatus.Active
        });
        _db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "owned-1",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "package-1",
            Total = 6,
            Used = 2,
            ReservedSessions = 0,
            IsActive = true,
            ExpiresOn = new DateOnly(2026, 12, 31)
        });
        await _db.SaveChangesAsync();
    }
}
