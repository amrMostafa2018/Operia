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
        var branchA = new Branch { Id = "branch-a", TenantId = "tenant-1", Name = "Branch A", Address = "Addr A", PhoneNumber = "123", GoogleMapsUrl = "url" };
        var branchB = new Branch { Id = "branch-b", TenantId = "tenant-1", Name = "Branch B", Address = "Addr B", PhoneNumber = "123", GoogleMapsUrl = "url" };
        var branchC = new Branch { Id = "branch-c", TenantId = "tenant-2", Name = "Branch C", Address = "Addr C", PhoneNumber = "123", GoogleMapsUrl = "url" };
        _db.Branches.AddRange(branchA, branchB, branchC);
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
        var branchA = new Branch { Id = "branch-a", TenantId = "tenant-1", Name = "Branch A", Address = "Addr A", PhoneNumber = "123", GoogleMapsUrl = "url" };
        var branchB = new Branch { Id = "branch-b", TenantId = "tenant-1", Name = "Branch B", Address = "Addr B", PhoneNumber = "123", GoogleMapsUrl = "url" };
        var emp = new Employee { Id = "emp-1", TenantId = "tenant-1", IdentityUserId = "user-admin", FullName = "Admin User", Email = "a@test.com", MobileNumber = "123", Code = "EMP-0001", IsActive = true };
        var mapA = new UserBranch { TenantId = "tenant-1", EmployeeId = "emp-1", BranchId = "branch-a" };

        _db.Branches.AddRange(branchA, branchB);
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
        var branchA = new Branch { Id = "branch-a", TenantId = "tenant-1", Name = "Branch A", Address = "Addr A", PhoneNumber = "123", GoogleMapsUrl = "url" };
        var branchB = new Branch { Id = "branch-b", TenantId = "tenant-1", Name = "Branch B", Address = "Addr B", PhoneNumber = "123", GoogleMapsUrl = "url" };

        var recEmp = new Employee { Id = "emp-rec", TenantId = "tenant-1", IdentityUserId = "user-rec", FullName = "Rec User", Email = "rec@test.com", MobileNumber = "111", Code = "EMP-0001", IsActive = true };
        var staffEmp = new Employee { Id = "emp-staff", TenantId = "tenant-1", IdentityUserId = "user-staff", FullName = "Staff User", Email = "staff@test.com", MobileNumber = "222", Code = "EMP-0002", IsActive = true };

        _db.Branches.AddRange(branchA, branchB);
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
        var branchA = new Branch { Id = "branch-a", TenantId = "tenant-1", Name = "Branch A", Address = "Addr A", PhoneNumber = "123", GoogleMapsUrl = "url" };
        var emp = new Employee { Id = "emp-nomap", TenantId = "tenant-1", IdentityUserId = "user-nomap", FullName = "NoMap User", Email = "nomap@test.com", MobileNumber = "333", Code = "EMP-0003", IsActive = true };
        _db.Branches.Add(branchA);
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
