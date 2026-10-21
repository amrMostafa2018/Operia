using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Bookings.Commands.CloseBooking;
using Operia.Application.Bookings.Commands.CreateBooking;
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

/// <summary>Verifies booking close finalises balances, history, and audit behavior.</summary>
public sealed class CloseBookingHandlerTests
{
    private readonly Mock<ICurrentUserService> _user = new();
    private readonly Mock<IBranchScope> _branchScope = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly TestApplicationDbContext _db;
    private readonly DateOnly _scheduledDate = new(2026, 9, 18);

    public CloseBookingHandlerTests()
    {
        _user.SetupGet(x => x.UserId).Returns("operator-1");
        _user.SetupGet(x => x.TenantId).Returns("tenant-1");
        _user.SetupGet(x => x.DisplayName).Returns("Mona");
        _branchScope.Setup(x => x.GetAllowedBranchIdsAsync("operator-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-1"]);
        _clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc));
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"CloseBooking_{Guid.NewGuid()}")
            .Options;
        _db = new TestApplicationDbContext(options, _clock.Object, _user.Object);
    }

    [Fact]
    public async Task Handle_PackageComplete_IncrementsUsedAndMarksBookingCompleted()
    {
        await SeedAsync();
        var created = await CreateHandler().Handle(
            new CreateBookingCommand(
                "close-package",
                "customer-1",
                "branch-1",
                "employee-1",
                _scheduledDate,
                600,
                660,
                [new CreateBookingItemInput("package-1", "owned-1", 1)]),
            CancellationToken.None);

        var booking = await _db.Bookings.Include(x => x.Items).SingleAsync(x => x.Id == created.Id);
        var item = booking.Items.Single();
        var ownedBefore = await _db.CustomerPackages.SingleAsync(x => x.Id == "owned-1");
        ownedBefore.ReservedSessions.Should().Be(1);

        var result = await CloseHandler().Handle(
            new CloseBookingCommand(
                booking.Id,
                Convert.ToBase64String(booking.Version),
                [new CloseBookingItemInput(item.Id, "owned-1", "complete", null, "Done")]),
            CancellationToken.None);

        result.Status.Should().Be(nameof(BookingStatus.Completed));
        var ownedAfter = await _db.CustomerPackages.SingleAsync(x => x.Id == "owned-1");
        ownedAfter.Used.Should().Be(3);
        ownedAfter.ReservedSessions.Should().Be(0);
        (await _db.BookingHistory.CountAsync(x => x.BookingId == booking.Id)).Should().Be(2);
        (await _db.AuditLogs.CountAsync(x => x.EntityId == booking.Id && x.Action == AuditActions.BookingClosed))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task Handle_StandaloneCancel_ReleasesReservationWithoutConsumingBalance()
    {
        await SeedAsync();
        var created = await CreateHandler().Handle(
            new CreateBookingCommand(
                "close-standalone",
                "customer-1",
                "branch-1",
                "employee-1",
                _scheduledDate,
                720,
                750,
                [new CreateBookingItemInput("service-1", null, 1)]),
            CancellationToken.None);

        var booking = await _db.Bookings.Include(x => x.Items).SingleAsync(x => x.Id == created.Id);
        var item = booking.Items.Single();
        var purchase = await _db.CustomerPackages.SingleAsync(x => x.PackageId == "service-1");

        var result = await CloseHandler().Handle(
            new CloseBookingCommand(
                booking.Id,
                Convert.ToBase64String(booking.Version),
                [new CloseBookingItemInput(item.Id, purchase.Id, "cancel", null, null)]),
            CancellationToken.None);

        result.Status.Should().Be(nameof(BookingStatus.Completed));
        var purchaseAfter = await _db.CustomerPackages.SingleAsync(x => x.PackageId == "service-1");
        purchaseAfter.Used.Should().Be(0);
        purchaseAfter.ReservedSessions.Should().Be(0);
        (await _db.BookingPackageReservations.SingleAsync()).CancellationAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_MissingLineItem_ThrowsCloseItemsIncomplete()
    {
        await SeedAsync();
        var created = await CreateHandler().Handle(
            new CreateBookingCommand(
                "close-incomplete",
                "customer-1",
                "branch-1",
                "employee-1",
                _scheduledDate,
                600,
                660,
                [new CreateBookingItemInput("package-1", "owned-1", 1)]),
            CancellationToken.None);

        var booking = await _db.Bookings.SingleAsync(x => x.Id == created.Id);
        var action = () => CloseHandler().Handle(
            new CloseBookingCommand(booking.Id, Convert.ToBase64String(booking.Version), []),
            CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes.Values.SelectMany(x => x)
            .Should()
            .Contain(ApiErrorCodes.Bookings.CloseItemsIncomplete);
    }

    [Fact]
    public async Task Handle_PulseCompleteBeyondRemaining_ThrowsPulsesExceedRemaining()
    {
        await SeedAsync();
        _db.Packages.Add(new Package
        {
            Id = "pulse-1",
            TenantId = "tenant-1",
            BusinessId = "business-1",
            Name = "Pulse Package",
            Description = "Pulse",
            OfferType = OfferType.Package,
            ServiceCategoryId = "category-1",
            SessionDurationMinutes = 60,
            PulseCount = 100,
            Price = 500,
            Status = PackageStatus.Active
        });
        _db.CustomerPackages.Add(new CustomerPackage
        {
            Id = "owned-pulse",
            TenantId = "tenant-1",
            CustomerId = "customer-1",
            PackageId = "pulse-1",
            Total = 100,
            Used = 95,
            ReservedSessions = 0,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var created = await CreateHandler().Handle(
            new CreateBookingCommand(
                "close-pulse",
                "customer-1",
                "branch-1",
                "employee-1",
                _scheduledDate,
                600,
                660,
                [new CreateBookingItemInput("pulse-1", "owned-pulse", 1)]),
            CancellationToken.None);

        var booking = await _db.Bookings.Include(x => x.Items).SingleAsync(x => x.Id == created.Id);
        var item = booking.Items.Single();
        var action = () => CloseHandler().Handle(
            new CloseBookingCommand(
                booking.Id,
                Convert.ToBase64String(booking.Version),
                [new CloseBookingItemInput(item.Id, "owned-pulse", "complete", 10, null)]),
            CancellationToken.None);

        var exception = await action.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes.Values.SelectMany(x => x)
            .Should()
            .Contain(ApiErrorCodes.Bookings.PulsesExceedRemaining);
    }

    private CloseBookingHandler CloseHandler() =>
        new(
            _db,
            _user.Object,
            _branchScope.Object,
            new AuditWriter(_db, _user.Object),
            new BookingConcurrencyStore(_db),
            _clock.Object);

    private CreateBookingHandler CreateHandler() =>
        new(
            _db,
            _user.Object,
            _branchScope.Object,
            new AuditWriter(_db, _user.Object),
            new BookingConcurrencyStore(_db),
            _clock.Object);

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
