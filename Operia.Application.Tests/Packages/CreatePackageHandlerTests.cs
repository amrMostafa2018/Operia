using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.Commands.CreatePackage;
using Operia.Application.Packages.DTOs;
using Operia.Application.Packages.Queries.GetAvailablePackageStaff;
using Operia.Domain.Entities;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Interfaces;
using Operia.SharedKernel.Errors;
using Xunit;
using ValidationException = Operia.Application.Common.Exceptions.ValidationException;

namespace Operia.Application.Tests.Packages;

public sealed class CreatePackageHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private readonly IAuditWriter _auditWriter;
    private string? _tenantId = "tenant-1";

    public CreatePackageHandlerTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("CreatePackageTests_" + Guid.NewGuid())
            .Options;
        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _auditWriter = new AuditWriter(_db, _currentUserServiceMock.Object);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(() => _tenantId);
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-1");
        _db.Businesses.Add(BranchTestData.Business("tenant-1"));
        _db.SaveChanges();
    }

    [Fact]
    public async Task CreatePackage_SingleSession_SavesCorrectly()
    {
        await SeedCategoryAsync("cat-1");
        var handler = new CreatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter, new Mock<IBranchScope>().Object, new Mock<IIdentityService>().Object);

        var result = await handler.Handle(ValidCommand("cat-1", "singleSession"), default);

        result.OfferType.Should().Be("singleSession");
        result.SessionCount.Should().BeNull();
        result.PulseCount.Should().BeNull();
        result.PackageExpiryMonths.Should().BeNull();

        var saved = await _db.Packages.SingleAsync();
        saved.OfferType.Should().Be(OfferType.SingleSession);
        saved.SessionCount.Should().BeNull();
        saved.PulseCount.Should().BeNull();
        saved.PackageExpiryMonths.Should().BeNull();
    }

    [Fact]
    public async Task CreatePackage_PackageType_SavesSessionCountAndPulse()
    {
        await SeedCategoryAsync("cat-1");
        var handler = new CreatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter, new Mock<IBranchScope>().Object, new Mock<IIdentityService>().Object);
        var command = ValidCommand("cat-1", "package") with
        {
            SessionCount = 8,
            PulseCount = 120,
            PackageExpiryMonths = 6
        };

        var result = await handler.Handle(command, default);

        result.OfferType.Should().Be("package");
        result.SessionCount.Should().Be(8);
        result.PulseCount.Should().Be(120);
        result.PackageExpiryMonths.Should().Be(6);
    }

    [Fact]
    public async Task CreatePackage_UnknownServiceCategoryId_ThrowsValidationException()
    {
        var handler = new CreatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter, new Mock<IBranchScope>().Object, new Mock<IIdentityService>().Object);
        var act = () => handler.Handle(ValidCommand("missing-cat"), default);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.ErrorCodes.Keys.Should().Contain(k => k.Equals("serviceCategoryId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreatePackage_WithDiscount_PersistsDiscountFields()
    {
        await SeedCategoryAsync("cat-1");
        var handler = new CreatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter, new Mock<IBranchScope>().Object, new Mock<IIdentityService>().Object);
        var command = ValidCommand("cat-1") with
        {
            DiscountCode = "WELCOME15",
            DiscountPercent = 15m
        };

        var result = await handler.Handle(command, default);

        result.DiscountCode.Should().Be("WELCOME15");
        result.DiscountPercent.Should().Be(15m);
    }

    [Fact]
    public async Task CreatePackage_WritesAuditLog_WithActionPackageCreated()
    {
        await SeedCategoryAsync("cat-1");
        var handler = new CreatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter, new Mock<IBranchScope>().Object, new Mock<IIdentityService>().Object);
        await handler.Handle(ValidCommand("cat-1"), default);

        var audit = await _db.AuditLogs.SingleAsync(x => x.Action == AuditActions.PackageCreated);
        audit.Should().NotBeNull();
        audit.DetailsJson.Should().NotBeNullOrWhiteSpace();
        audit.DetailsJson.Should().Contain("\"offerType\":\"singleSession\"");
    }

    [Fact]
    public async Task CreatePackage_StoresNumberedInstallmentsAndCumulativeRelease()
    {
        await SeedCategoryAsync("cat-1");
        var handler = new CreatePackageHandler(_db, _currentUserServiceMock.Object, _auditWriter, new Mock<IBranchScope>().Object, new Mock<IIdentityService>().Object);
        var command = ValidCommand("cat-1", "package") with
        {
            InstallmentEnabled = true,
            InstallmentPrice = 20000m,
            FirstPayment = 5000m,
            InstallmentCount = 3,
            SessionCount = 10,
            Installments = [
                new PackageInstallmentInput(5000m, 3),
                new PackageInstallmentInput(5000m, 4),
                new PackageInstallmentInput(5000m, 3)
            ]
        };

        var result = await handler.Handle(command, default);

        result.Installments.Select(row => row.CumulativeAvailableSessions)
            .Should().Equal(3, 7, 10);
        (await _db.PackageInstallments.CountAsync()).Should().Be(3);
        var audit = await _db.AuditLogs.SingleAsync(x => x.Action == AuditActions.PackageCreated);
        audit.DetailsJson.Should().Contain("\"firstPayment\":5000");
    }

    [Fact]
    public async Task CreatePackage_SelectedStaff_PersistsScopedAssignmentAndAudit()
    {
        await SeedCategoryAsync("cat-1");
        _db.Branches.Add(BranchTestData.Branch("branch-1", "tenant-1", "business-tenant-1", "Main"));
        _db.Employees.Add(BranchTestData.Employee(
            "staff-1", "tenant-1", "business-tenant-1", "staff-user", "E1",
            "Mona", "mona@example.com", "01000000000"));
        _db.UserBranches.Add(new UserBranch
        {
            TenantId = "tenant-1",
            EmployeeId = "staff-1",
            BranchId = "branch-1"
        });
        await _db.SaveChangesAsync();
        var branches = new Mock<IBranchScope>();
        branches.Setup(x => x.GetAllowedBranchIdsAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-1"]);
        var identity = new Mock<IIdentityService>();
        identity.Setup(x => x.GetRolesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["staff-user"] = "Staff" });
        var command = ValidCommand("cat-1") with
        {
            AllStaff = false,
            StaffIds = ["staff-1"]
        };

        var result = await new CreatePackageHandler(
            _db, _currentUserServiceMock.Object, _auditWriter,
            branches.Object, identity.Object).Handle(command, default);

        result.AllStaff.Should().BeFalse();
        result.StaffIds.Should().Equal("staff-1");
        (await _db.PackageStaff.SingleAsync()).TenantId.Should().Be("tenant-1");
        var audit = await _db.AuditLogs.SingleAsync(x => x.Action == AuditActions.PackageCreated);
        audit.DetailsJson.Should().Contain("staff-1");
    }

    [Fact]
    public async Task CreatePackage_RejectsStaffOutsideAccessibleBranches()
    {
        await SeedCategoryAsync("cat-1");
        _tenantId = "tenant-2";
        _db.Businesses.Add(BranchTestData.Business("tenant-2"));
        _db.Branches.Add(BranchTestData.Branch("branch-2", "tenant-2", "business-tenant-2", "Other"));
        _db.Employees.Add(BranchTestData.Employee(
            "other-tenant-staff", "tenant-2", "business-tenant-2", "other-user", "E2",
            "Other Staff", "other@example.com", "01000000002"));
        _db.UserBranches.Add(new UserBranch
        {
            TenantId = "tenant-2",
            EmployeeId = "other-tenant-staff",
            BranchId = "branch-2"
        });
        await _db.SaveChangesAsync();
        _tenantId = "tenant-1";
        var command = ValidCommand("cat-1") with
        {
            AllStaff = false,
            StaffIds = ["other-tenant-staff"]
        };
        var branches = new Mock<IBranchScope>();
        branches.Setup(x => x.GetAllowedBranchIdsAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-2"]);
        var identity = new Mock<IIdentityService>();
        identity.Setup(x => x.GetRolesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["other-user"] = "Staff" });

        var act = () => new CreatePackageHandler(
            _db, _currentUserServiceMock.Object, _auditWriter,
            branches.Object, identity.Object).Handle(command, default);

        var failure = await act.Should().ThrowAsync<ValidationException>();
        failure.Which.ErrorCodes.Values.SelectMany(codes => codes)
            .Should().Contain(ApiErrorCodes.Packages.PackageStaffInvalid);
        (await _db.Packages.CountAsync()).Should().Be(0);
    }

    private async Task SeedCategoryAsync(string categoryId)
    {
        _db.ServiceCategories.Add(PackageTestData.ServiceCategory(categoryId, "tenant-1", "Body Laser"));
        await _db.SaveChangesAsync();
    }

    private static CreatePackageCommand ValidCommand(
        string serviceCategoryId,
        string offerType = "singleSession",
        string? subServiceCategoryId = null) =>
        new(
            "Summer Offer",
            true,
            offerType,
            "Full body session",
            serviceCategoryId,
            subServiceCategoryId,
            45,
            offerType == "package" ? 6 : 0,
            null,
            offerType == "package" ? 12 : null,
            1500m,
            string.Empty,
            null);
}
