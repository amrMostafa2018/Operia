using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Bookings.Queries.GetBookingHistory;
using Operia.Application.Bookings.Queries.GetBookingPaymentMethods;
using Operia.Application.Bookings.Queries.GetCalendarBookings;
using Operia.Application.Bookings.Queries.ListBookings;
using Operia.Application.Bookings.Queries.ExportBookings;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Bookings;

/// <summary>Verifies tenant-scoped booking reads, filters, and projections.</summary>
public sealed class BookingReadHandlerTests
{
    [Fact]
    public async Task PaymentMethods_ReturnsOnlyMethodsEnabledInBusinessSettings()
    {
        var fixture = await CreateFixtureAsync();
        fixture.Db.BusinessSettings.Add(new BusinessSettings
        {
            BusinessId = "business-1",
            PaymentMethodsJson = """
                {"cashEnabled":false,"bankTransferEnabled":true,"instapayEnabled":true,"eWalletEnabled":false,"fawryEnabled":false}
                """
        });
        await fixture.Db.SaveChangesAsync();

        var methods = await new GetBookingPaymentMethodsHandler(fixture.Db, fixture.User.Object)
            .Handle(new GetBookingPaymentMethodsQuery(), CancellationToken.None);

        methods.Should().Equal("bank_transfer", "instapay");
    }

    [Fact]
    public async Task List_UsesAllowedTenantBranchesForRowsAndSummary()
    {
        var fixture = await CreateFixtureAsync();
        var result = await new ListBookingsHandler(fixture.Db, fixture.User.Object, fixture.Scope.Object)
            .Handle(
                new ListBookingsQuery(
                    new DateOnly(2026, 9, 1),
                    new DateOnly(2026, 9, 30),
                    1,
                    10,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Id.Should().Be("booking-allowed");
        result.Summary.Should().Be(new BookingSummaryDto(1, 1, 0, 0));
    }

    [Fact]
    public async Task History_ReturnsOnlyTheSelectedBookingTimeline()
    {
        var fixture = await CreateFixtureAsync();
        var result = await new GetBookingHistoryHandler(fixture.Db, fixture.User.Object, fixture.Scope.Object)
            .Handle(new GetBookingHistoryQuery("booking-allowed"), CancellationToken.None);

        result.Should().ContainSingle().Which.Action.Should().Be("Created");
    }

    [Fact]
    public async Task History_StaffCannotReadAColleaguesBooking()
    {
        var fixture = await CreateFixtureAsync();
        fixture.User.Setup(x => x.IsInRole(Roles.Staff)).Returns(true);

        var action = () => new GetBookingHistoryHandler(
                fixture.Db,
                fixture.User.Object,
                fixture.Scope.Object)
            .Handle(new GetBookingHistoryQuery("booking-allowed"), CancellationToken.None);

        await action.Should().ThrowAsync<ApiNotFoundException>();
    }

    [Fact]
    public async Task Export_ContainsOnlyAllowedBranchBookings()
    {
        var fixture = await CreateFixtureAsync();
        var csv = await new ExportBookingsHandler(fixture.Db, fixture.User.Object, fixture.Scope.Object)
            .Handle(
                new ExportBookingsQuery(
                    new DateOnly(2026, 9, 1),
                    new DateOnly(2026, 9, 30),
                    null,
                    null,
                    null,
                    null),
                CancellationToken.None);

        csv.Should().Contain("OP-booking-allowed");
        csv.Should().NotContain("OP-booking-denied");
    }

    [Fact]
    public async Task Export_NeutralizesSpreadsheetFormulaValues()
    {
        var fixture = await CreateFixtureAsync();
        var booking = await fixture.Db.Bookings.SingleAsync(x => x.Id == "booking-allowed");
        booking.CustomerName = "=HYPERLINK(\"https://example.test\")";
        await fixture.Db.SaveChangesAsync();

        var csv = await new ExportBookingsHandler(fixture.Db, fixture.User.Object, fixture.Scope.Object)
            .Handle(
                new ExportBookingsQuery(
                    new DateOnly(2026, 9, 1),
                    new DateOnly(2026, 9, 30),
                    null,
                    null,
                    null,
                    null),
                CancellationToken.None);

        csv.Should().Contain("\"'=HYPERLINK(\"\"https://example.test\"\")\"");
    }

    [Fact]
    public async Task Calendar_ReturnsOnlyActiveUnconvertedHoldsInTheAllowedBranch()
    {
        var fixture = await CreateFixtureAsync();
        fixture.Db.BookingHolds.AddRange(
            Hold("active", "branch-allowed", "employee-allowed", new DateTime(2026, 9, 17, 8, 8, 0, DateTimeKind.Utc)),
            Hold("expired", "branch-allowed", "employee-allowed", new DateTime(2026, 9, 17, 7, 59, 0, DateTimeKind.Utc)),
            Hold("denied", "branch-denied", "employee-denied", new DateTime(2026, 9, 17, 8, 8, 0, DateTimeKind.Utc)));
        await fixture.Db.SaveChangesAsync();

        var result = await new GetCalendarBookingsHandler(
                fixture.Db,
                fixture.User.Object,
                fixture.Scope.Object,
                fixture.Clock.Object)
            .Handle(
                new GetCalendarBookingsQuery(
                    "branch-allowed",
                    new DateOnly(2026, 9, 18),
                    new DateOnly(2026, 9, 18),
                    null),
                CancellationToken.None);

        result.Bookings.Should().ContainSingle().Which.Id.Should().Be("booking-allowed");
        result.Holds.Should().ContainSingle().Which.Id.Should().Be("active");
        result.ServerNowUtc.Should().Be(fixture.Clock.Object.UtcNow);
        var item = result.Bookings.Single().Items.Should().ContainSingle().Which;
        item.CustomerPackageId.Should().Be("customer-package-1");
        item.PackageRemainingSessions.Should().Be(6);
    }

    [Fact]
    public async Task Calendar_EnrichesEachRepeatedPackageLineWithItsOwnReservation()
    {
        var fixture = await CreateFixtureAsync();
        fixture.Db.BookingItems.Add(new BookingItem
        {
            Id = "item-allowed-2",
            TenantId = "tenant-1",
            BookingId = "booking-allowed",
            Type = BookingItemType.PackageSession,
            PackageId = "package-1",
            Name = "Owned package",
            Quantity = 1,
            DurationMinutes = 60,
            CreatedAt = new DateTime(2026, 9, 18, 8, 1, 0, DateTimeKind.Utc)
        });
        fixture.Db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "customer-package-2",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "package-1",
            Total = 11,
            Used = 0,
            ReservedSessions = 1
        });
        fixture.Db.BookingPackageReservations.Add(new BookingPackageReservation
        {
            Id = "reservation-2",
            TenantId = "tenant-1",
            BookingId = "booking-allowed",
            CustomerPackageId = "customer-package-2",
            SessionNumber = 1,
            CreatedAt = new DateTime(2026, 9, 18, 8, 1, 0, DateTimeKind.Utc)
        });
        await fixture.Db.SaveChangesAsync();

        var result = await new GetCalendarBookingsHandler(
                fixture.Db,
                fixture.User.Object,
                fixture.Scope.Object,
                fixture.Clock.Object)
            .Handle(
                new GetCalendarBookingsQuery(
                    "branch-allowed",
                    new DateOnly(2026, 9, 18),
                    new DateOnly(2026, 9, 18),
                    null),
                CancellationToken.None);

        var items = result.Bookings.Single().Items;
        items.Should().HaveCount(2);
        items[0].CustomerPackageId.Should().Be("customer-package-1");
        items[0].PackageRemainingSessions.Should().Be(6);
        items[1].CustomerPackageId.Should().Be("customer-package-2");
        items[1].PackageRemainingSessions.Should().Be(10);
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.UserId).Returns("operator");
        string? tenantId = null;
        user.SetupGet(x => x.TenantId).Returns(() => tenantId);
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc));
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"BookingReads_{Guid.NewGuid()}")
            .Options;
        var db = new TestApplicationDbContext(options, clock.Object, user.Object);
        db.Businesses.AddRange(
            new Business { Id = "business-1", TenantId = "tenant-1", ActivityName = "Business" },
            new Business { Id = "other-business", TenantId = "tenant-2", ActivityName = "Other business" });
        db.BusinessSettings.Add(new BusinessSettings
        {
            BusinessId = "other-business",
            PaymentMethodsJson = """
                {"cashEnabled":true,"bankTransferEnabled":false,"instapayEnabled":false,"eWalletEnabled":true,"fawryEnabled":true}
                """
        });
        db.Branches.AddRange(
            Branch("branch-allowed", "tenant-1"),
            Branch("branch-denied", "tenant-1"));
        db.Employees.AddRange(
            Employee("employee-allowed", "tenant-1"),
            Employee("employee-denied", "tenant-1"));
        db.Packages.Add(new Package
        {
            Id = "package-1",
            TenantId = "tenant-1",
            BusinessId = "business-1",
            Name = "Owned package",
            Description = "Package",
            OfferType = OfferType.Package,
            ServiceCategoryId = "category-1",
            SessionDurationMinutes = 60,
            SessionCount = 11,
            PulseCount = null,
            Price = 1000,
            Status = PackageStatus.Active
        });
        db.Bookings.AddRange(
            Booking("booking-allowed", "tenant-1", "branch-allowed", "employee-allowed"),
            Booking("booking-denied", "tenant-1", "branch-denied", "employee-denied"));
        db.BookingHistory.AddRange(
            History("history-allowed", "tenant-1", "booking-allowed"),
            History("history-denied", "tenant-1", "booking-denied"));
        db.BookingItems.Add(new BookingItem
        {
            Id = "item-allowed",
            TenantId = "tenant-1",
            BookingId = "booking-allowed",
            Type = BookingItemType.PackageSession,
            PackageId = "package-1",
            Name = "Owned package",
            Quantity = 1,
            DurationMinutes = 60,
            CreatedAt = new DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc)
        });
        db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "customer-package-1",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "package-1",
            Total = 11,
            Used = 2,
            ReservedSessions = 3
        });
        db.BookingPackageReservations.Add(new BookingPackageReservation
        {
            Id = "reservation-1",
            TenantId = "tenant-1",
            BookingId = "booking-allowed",
            CustomerPackageId = "customer-package-1",
            SessionNumber = 3,
            CreatedAt = new DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();
        tenantId = "tenant-1";
        var scope = new Mock<IBranchScope>();
        scope.Setup(x => x.GetAllowedBranchIdsAsync("operator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-allowed"]);
        return new Fixture(db, user, scope, clock);
    }

    private static Branch Branch(string id, string tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        BusinessId = "business-1",
        Name = id,
        Address = "Address",
        PhoneNumber = "01000000000",
        GoogleMapsUrl = "https://maps.example"
    };

    private static Employee Employee(string id, string tenantId) => new()
    {
        Id = id,
        TenantId = tenantId,
        BusinessId = "business-1",
        IdentityUserId = $"user-{id}",
        Code = id,
        FullName = id,
        Email = $"{id}@example.com",
        MobileNumber = "01000000000",
        JoiningDate = new DateOnly(2026, 1, 1)
    };

    private static Booking Booking(string id, string tenantId, string branchId, string employeeId) => new()
    {
        Id = id,
        TenantId = tenantId,
        BookingNumber = $"OP-{id}",
        IdempotencyKey = $"request-{id}",
        BranchId = branchId,
        EmployeeId = employeeId,
        CustomerId = "customer-1",
        CustomerName = "Ahmed",
        CustomerMobile = "01012345678",
        ScheduledDate = new DateOnly(2026, 9, 18),
        StartMinutes = 600,
        EndMinutes = 660,
        Status = BookingStatus.Booked
    };

    private static BookingHistory History(string id, string tenantId, string bookingId) => new()
    {
        Id = id,
        TenantId = tenantId,
        BookingId = bookingId,
        Action = "Created"
    };

    private static BookingHold Hold(string id, string branchId, string employeeId, DateTime expiresAtUtc) => new()
    {
        Id = id,
        TenantId = "tenant-1",
        BranchId = branchId,
        EmployeeId = employeeId,
        CustomerId = "customer-1",
        ScheduledDate = new DateOnly(2026, 9, 18),
        StartMinutes = 660,
        EndMinutes = 720,
        ExpiresAtUtc = expiresAtUtc,
        IdempotencyKey = $"request-{id}"
    };

    /// <summary>Represents fixture in the booking workflow.</summary>
    private sealed record Fixture(
        TestApplicationDbContext Db,
        Mock<ICurrentUserService> User,
        Mock<IBranchScope> Scope,
        Mock<IDateTimeProvider> Clock);
}
