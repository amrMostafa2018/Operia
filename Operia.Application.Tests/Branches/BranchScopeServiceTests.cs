using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Interfaces;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Services;
using Operia.Infrastructure.Persistence;
using Xunit;

namespace Operia.Application.Tests.Branches;

public class BranchScopeServiceTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private readonly BranchScopeService _sut;

    public BranchScopeServiceTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("BranchScopeTests_" + Guid.NewGuid())
            .Options;

        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _sut = new BranchScopeService(_db, _currentUserServiceMock.Object);
    }

    [Fact]
    public async Task SuperAdmin_ReturnsAllTenantBranches_ExcludesOtherTenants()
    {
        // Arrange
        _db.Businesses.AddRange(
            BranchTestData.Business("tenant-1"),
            BranchTestData.Business("tenant-2"));
        _db.Branches.AddRange(
            BranchTestData.Branch("branch-a", "tenant-1", "business-tenant-1", "Branch A", "Addr A"),
            BranchTestData.Branch("branch-b", "tenant-1", "business-tenant-1", "Branch B", "Addr B"),
            BranchTestData.Branch("branch-c", "tenant-2", "business-tenant-2", "Branch C", "Addr C"));
        await _db.SaveChangesAsync();

        _currentUserServiceMock.Setup(x => x.UserId).Returns("super-admin");
        _currentUserServiceMock.Setup(x => x.TenantId).Returns("tenant-1");
        _currentUserServiceMock.Setup(x => x.IsInRole(Roles.SuperAdmin)).Returns(true);

        // Act
        var result = await _sut.GetAllowedBranchIdsAsync("super-admin");

        // Assert
        result.Should().BeEquivalentTo("branch-a", "branch-b");
        result.Should().NotContain("branch-c");
    }

    [Fact]
    public async Task Admin_MappedOnlyToBranchA_ReturnsOnlyBranchA()
    {
        // Arrange
        _db.Businesses.Add(BranchTestData.Business("tenant-1"));
        _db.Branches.AddRange(
            BranchTestData.Branch("branch-a", "tenant-1", "business-tenant-1", "Branch A", "Addr A"),
            BranchTestData.Branch("branch-b", "tenant-1", "business-tenant-1", "Branch B", "Addr B"));
        var emp = BranchTestData.Employee("emp-1", "tenant-1", "business-tenant-1", "user-admin", "EMP-0001", "Admin User", "a@test.com", "123");
        var mapA = new UserBranch { TenantId = "tenant-1", EmployeeId = "emp-1", BranchId = "branch-a" };

        _db.Employees.Add(emp);
        _db.UserBranches.Add(mapA);
        await _db.SaveChangesAsync();

        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-admin");
        _currentUserServiceMock.Setup(x => x.TenantId).Returns("tenant-1");
        _currentUserServiceMock.Setup(x => x.IsInRole(Roles.SuperAdmin)).Returns(false);

        // Act
        var result = await _sut.GetAllowedBranchIdsAsync("user-admin");

        // Assert
        result.Should().BeEquivalentTo("branch-a");
        result.Should().NotContain("branch-b");
    }

    [Fact]
    public async Task ReceptionAndStaff_FollowSameMappedSetRule()
    {
        // Arrange
        _db.Businesses.Add(BranchTestData.Business("tenant-1"));
        _db.Branches.AddRange(
            BranchTestData.Branch("branch-a", "tenant-1", "business-tenant-1", "Branch A", "Addr A"),
            BranchTestData.Branch("branch-b", "tenant-1", "business-tenant-1", "Branch B", "Addr B"));

        var recEmp = BranchTestData.Employee("emp-rec", "tenant-1", "business-tenant-1", "user-rec", "EMP-0001", "Rec User", "rec@test.com", "111");
        var staffEmp = BranchTestData.Employee("emp-staff", "tenant-1", "business-tenant-1", "user-staff", "EMP-0002", "Staff User", "staff@test.com", "222");

        _db.Employees.AddRange(recEmp, staffEmp);
        _db.UserBranches.AddRange(
            new UserBranch { TenantId = "tenant-1", EmployeeId = "emp-rec", BranchId = "branch-a" },
            new UserBranch { TenantId = "tenant-1", EmployeeId = "emp-rec", BranchId = "branch-b" },
            new UserBranch { TenantId = "tenant-1", EmployeeId = "emp-staff", BranchId = "branch-b" }
        );
        await _db.SaveChangesAsync();

        // Test Reception mapped to A and B
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-rec");
        _currentUserServiceMock.Setup(x => x.TenantId).Returns("tenant-1");
        _currentUserServiceMock.Setup(x => x.IsInRole(Roles.SuperAdmin)).Returns(false);
        var recResult = await _sut.GetAllowedBranchIdsAsync("user-rec");
        recResult.Should().BeEquivalentTo("branch-a", "branch-b");

        // Test Staff mapped only to B
        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-staff");
        var staffResult = await _sut.GetAllowedBranchIdsAsync("user-staff");
        staffResult.Should().BeEquivalentTo("branch-b");
    }

    [Fact]
    public async Task NonSuperAdmin_WithNoMapping_ReturnsEmptySet()
    {
        // Arrange
        _db.Businesses.Add(BranchTestData.Business("tenant-1"));
        _db.Branches.Add(BranchTestData.Branch("branch-a", "tenant-1", "business-tenant-1", "Branch A", "Addr A"));
        var emp = BranchTestData.Employee("emp-nomap", "tenant-1", "business-tenant-1", "user-nomap", "EMP-0003", "NoMap User", "nomap@test.com", "333");
        _db.Employees.Add(emp);
        await _db.SaveChangesAsync();

        _currentUserServiceMock.Setup(x => x.UserId).Returns("user-nomap");
        _currentUserServiceMock.Setup(x => x.TenantId).Returns("tenant-1");
        _currentUserServiceMock.Setup(x => x.IsInRole(Roles.SuperAdmin)).Returns(false);

        // Act
        var result = await _sut.GetAllowedBranchIdsAsync("user-nomap");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MismatchedUserId_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _currentUserServiceMock.Setup(x => x.UserId).Returns("actual-user");

        // Act
        var act = () => _sut.GetAllowedBranchIdsAsync("different-user");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Requested user ID does not match the authenticated user.");
    }

    [Fact]
    public void ScopedToBranches_FiltersRecords_And_EmptyScopeReturnsZero()
    {
        // Arrange
        var records = new[]
        {
            new DummyBranchScoped { BranchId = "branch-a", Name = "Record A" },
            new DummyBranchScoped { BranchId = "branch-b", Name = "Record B" },
            new DummyBranchScoped { BranchId = "branch-c", Name = "Record C" }
        }.AsQueryable();

        // Act & Assert 1: Scope [branch-a] excludes B and C
        var scopedA = records.ScopedToBranches(new[] { "branch-a" }).ToList();
        scopedA.Should().HaveCount(1);
        scopedA.Single().Name.Should().Be("Record A");

        // Act & Assert 2: Empty scope returns zero records
        var scopedEmpty = records.ScopedToBranches(new string[0]).ToList();
        scopedEmpty.Should().BeEmpty();
    }

    private class DummyBranchScoped : IBranchScoped
    {
        public string BranchId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
