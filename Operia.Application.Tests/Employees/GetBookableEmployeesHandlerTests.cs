using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Employees.Queries.GetBookableEmployees;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Infrastructure.Persistence;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using Xunit;

namespace Operia.Application.Tests.Employees;

/// <summary>Verifies employee lookup under booking branch and tenant scope.</summary>
public sealed class GetBookableEmployeesHandlerTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IIdentityService> _identity = new();
    private readonly Mock<IBranchScope> _branchScope = new();
    private readonly TestApplicationDbContext _db;

    /// <summary>Returns bookable employees handler tests for the current view.</summary>
    public GetBookableEmployeesHandlerTests()
    {
        _currentUser.SetupGet(service => service.UserId).Returns("staff-user");
        _currentUser.SetupGet(service => service.TenantId).Returns("tenant-1");
        _clock.SetupGet(provider => provider.UtcNow).Returns(DateTime.UtcNow);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"GetBookableEmployees_{Guid.NewGuid()}")
            .Options;

        _db = new TestApplicationDbContext(options, _clock.Object, _currentUser.Object);
    }

    [Fact]
    public async Task Handle_RejectsBranchOutsideCurrentUsersScope()
    {
        _branchScope
            .Setup(scope => scope.GetAllowedBranchIdsAsync("staff-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = CreateHandler();

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetBookableEmployeesQuery("branch-2"), CancellationToken.None));

        exception.ErrorCode.Should().Be(ApiErrorCodes.Access.TenantAccessDenied);
        exception.Field.Should().Be("branchId");
        _identity.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForStaff_ReturnsOnlyTheAuthenticatedEmployeesOwnRecord()
    {
        _currentUser
            .Setup(service => service.IsInRole(Roles.Staff))
            .Returns(true);
        _branchScope
            .Setup(scope => scope.GetAllowedBranchIdsAsync("staff-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-1"]);

        _db.Businesses.Add(BranchTestData.Business("tenant-1"));
        _db.Branches.Add(BranchTestData.Branch(
            "branch-1",
            "tenant-1",
            "business-tenant-1",
            "Main"));

        var ownEmployee = BranchTestData.Employee(
            "employee-own",
            "tenant-1",
            "business-tenant-1",
            "staff-user",
            "EMP-0001",
            "Own Staff",
            "own@example.com",
            "+201000000001");
        var colleague = BranchTestData.Employee(
            "employee-other",
            "tenant-1",
            "business-tenant-1",
            "other-user",
            "EMP-0002",
            "Other Staff",
            "other@example.com",
            "+201000000002");

        _db.Employees.AddRange(ownEmployee, colleague);
        _db.UserBranches.AddRange(
            new UserBranch
            {
                TenantId = "tenant-1",
                EmployeeId = ownEmployee.Id,
                BranchId = "branch-1"
            },
            new UserBranch
            {
                TenantId = "tenant-1",
                EmployeeId = colleague.Id,
                BranchId = "branch-1"
            });
        await _db.SaveChangesAsync();

        _identity
            .Setup(service => service.GetRolesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["staff-user"] = Roles.Staff
            });

        var result = await CreateHandler().Handle(
            new GetBookableEmployeesQuery("branch-1"),
            CancellationToken.None);

        result.Should().ContainSingle();
        result.Single().Id.Should().Be("employee-own");
    }

    [Fact]
    public async Task Handle_ForPackage_ReturnsOnlyAssignedStaffInBranch()
    {
        _branchScope
            .Setup(scope => scope.GetAllowedBranchIdsAsync("staff-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["branch-1"]);
        _db.Businesses.Add(BranchTestData.Business("tenant-1"));
        _db.Branches.Add(BranchTestData.Branch(
            "branch-1", "tenant-1", "business-tenant-1", "Main"));
        foreach (var id in new[] { "employee-1", "employee-2" })
        {
            _db.Employees.Add(BranchTestData.Employee(
                id, "tenant-1", "business-tenant-1", id + "-user", id,
                id, id + "@example.com", "01000000000"));
            _db.UserBranches.Add(new UserBranch
            {
                TenantId = "tenant-1",
                EmployeeId = id,
                BranchId = "branch-1"
            });
        }
        _db.Packages.Add(new Package
        {
            Id = "package-1",
            TenantId = "tenant-1",
            BusinessId = "business-tenant-1",
            Name = "Offer",
            ServiceCategoryId = "category-1",
            OfferType = OfferType.Package,
            Status = PackageStatus.Active,
            AllStaff = false,
            AvailableStaff =
            [
                new PackageStaff
                {
                    TenantId = "tenant-1", PackageId = "package-1", EmployeeId = "employee-1"
                }
            ]
        });
        await _db.SaveChangesAsync();
        _identity.Setup(service => service.GetRolesAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["employee-1-user"] = Roles.Staff,
                ["employee-2-user"] = Roles.Staff
            });

        var result = await CreateHandler().Handle(
            new GetBookableEmployeesQuery("branch-1", "package-1"), CancellationToken.None);

        result.Select(employee => employee.Id).Should().Equal("employee-1");
    }

    private GetBookableEmployeesHandler CreateHandler() =>
        new(_db, _currentUser.Object, _identity.Object, _branchScope.Object);
}
