using System.Text.Json;
using FluentValidation;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Interfaces;
using Operia.Infrastructure.Persistence;
using Operia.Application.Employees.Commands.CreateEmployee;
using Operia.Application.Employees.Commands.UpdateEmployee;
using Operia.Application.Employees.Queries.GetEmployee;
using Operia.Application.Employees.Queries.ListEmployees;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Xunit;
using ValidationException = Operia.Application.Common.Exceptions.ValidationException;

namespace Operia.Application.Tests.Employees;

public class EmployeeBranchMappingTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly Mock<IIdentityService> _identityServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IFileStorageService> _fileStorageServiceMock = new();
    private readonly Mock<IEmployeeCodeGenerator> _employeeCodeGeneratorMock = new();
    private readonly TestApplicationDbContext _db;

    public EmployeeBranchMappingTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("EmpBranchMappingTests_" + Guid.NewGuid())
            .Options;

        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);

        _unitOfWorkMock.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _currentUserServiceMock.Setup(x => x.UserId).Returns("current-admin");
        _currentUserServiceMock.Setup(x => x.TenantId).Returns("tenant-1");
        _currentUserServiceMock.Setup(x => x.DisplayName).Returns("Admin Tester");
        _employeeCodeGeneratorMock.Setup(x => x.ReserveNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("EMP-0001");
    }

    [Fact]
    public async Task CreateEmployee_DeduplicatesMappings_And_RejectsEmptyOrCrossTenantBranchIds()
    {
        // Arrange
        var branch1 = new Branch { Id = "b-1", TenantId = "tenant-1", Name = "Branch 1", Address = "Addr", PhoneNumber = "123", GoogleMapsUrl = "url" };
        var branch2 = new Branch { Id = "b-2", TenantId = "tenant-1", Name = "Branch 2", Address = "Addr", PhoneNumber = "123", GoogleMapsUrl = "url" };
        var crossBranch = new Branch { Id = "b-cross", TenantId = "tenant-2", Name = "Cross Branch", Address = "Addr", PhoneNumber = "123", GoogleMapsUrl = "url" };
        _db.Branches.AddRange(branch1, branch2, crossBranch);
        await _db.SaveChangesAsync();

        var handler = new CreateEmployeeHandler(
            _db,
            _employeeCodeGeneratorMock.Object,
            _identityServiceMock.Object,
            _unitOfWorkMock.Object,
            _currentUserServiceMock.Object,
            _fileStorageServiceMock.Object);

        _identityServiceMock.Setup(x => x.EnsureIdentityUniqueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _identityServiceMock.Setup(x => x.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("new-id-user");
        _identityServiceMock.Setup(x => x.GetUsersSummaryAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, UserSummaryDto> { ["new-id-user"] = new UserSummaryDto("new-id-user", "testuser", "test@test.com", "123", false) });

        // Act & Assert 1: Empty branch IDs
        var emptyCmd = new CreateEmployeeCommand("Name", "test@test.com", "1234567890", "testuser", null, null, DateOnly.FromDateTime(DateTime.Today), true, Roles.Staff, new string[0], "Password123!", null);
        var exEmpty = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(emptyCmd, default));
        exEmpty.Errors.Keys.Should().Contain(k => k.Equals("branchIds", StringComparison.OrdinalIgnoreCase));
        exEmpty.Errors.Values.SelectMany(v => v).Should().Contain("At least one branch is required.");

        // Act & Assert 2: Cross-tenant branch ID
        var crossCmd = new CreateEmployeeCommand("Name", "test@test.com", "1234567890", "testuser", null, null, DateOnly.FromDateTime(DateTime.Today), true, Roles.Staff, new[] { "b-cross" }, "Password123!", null);
        var exCross = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(crossCmd, default));
        exCross.Errors.Keys.Should().Contain(k => k.Equals("branchIds", StringComparison.OrdinalIgnoreCase));
        exCross.Errors.Values.SelectMany(v => v).Should().Contain("One or more branches are invalid for this tenant.");

        // Act & Assert 3: Deduplication with duplicates [b-1, b-1, b-2]
        var validCmd = new CreateEmployeeCommand("Name", "test@test.com", "+1234567890", "testuser", null, null, DateOnly.FromDateTime(DateTime.Today), true, Roles.Staff, new[] { "b-1", "b-1", "b-2" }, "Password123!", null);
        var res = await handler.Handle(validCmd, default);
        await _db.SaveChangesAsync();

        var empBranches = await _db.UserBranches.Where(x => x.EmployeeId == res.Id).ToListAsync();
        empBranches.Should().HaveCount(2);
        empBranches.Select(x => x.BranchId).Should().BeEquivalentTo("b-1", "b-2");

        var auditLog = await _db.AuditLogs.SingleOrDefaultAsync(x => x.EntityId == res.Id && x.Action == "EmployeeCreated");
        auditLog.Should().NotBeNull();
        auditLog!.DetailsJson.Should().Contain("\"branchIds\":[\"b-1\",\"b-2\"]");
        _employeeCodeGeneratorMock.Verify(
            x => x.ReserveNextAsync("tenant-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateEmployee_ReplacesMappingSet_And_CapturesBeforeAfterAudit()
    {
        // Arrange
        var branchA = new Branch { Id = "b-a", TenantId = "tenant-1", Name = "Branch A", Address = "Addr", PhoneNumber = "123", GoogleMapsUrl = "url" };
        var branchB = new Branch { Id = "b-b", TenantId = "tenant-1", Name = "Branch B", Address = "Addr", PhoneNumber = "123", GoogleMapsUrl = "url" };
        var branchC = new Branch { Id = "b-c", TenantId = "tenant-1", Name = "Branch C", Address = "Addr", PhoneNumber = "123", GoogleMapsUrl = "url" };
        var emp = new Employee { Id = "emp-upd", TenantId = "tenant-1", IdentityUserId = "user-upd", FullName = "Update User", Email = "upd@test.com", MobileNumber = "+1111111111", Code = "EMP-0010", IsActive = true };
        var mapA = new UserBranch { TenantId = "tenant-1", EmployeeId = "emp-upd", BranchId = "b-a" };
        var mapB = new UserBranch { TenantId = "tenant-1", EmployeeId = "emp-upd", BranchId = "b-b" };

        _db.Branches.AddRange(branchA, branchB, branchC);
        _db.Employees.Add(emp);
        _db.UserBranches.AddRange(mapA, mapB);
        await _db.SaveChangesAsync();

        _identityServiceMock.Setup(x => x.EnsureIdentityUniqueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _identityServiceMock.Setup(x => x.UpdateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _identityServiceMock.Setup(x => x.GetRolesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, string> { ["user-upd"] = Roles.Staff });
        _identityServiceMock.Setup(x => x.GetUsersSummaryAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, UserSummaryDto> { ["user-upd"] = new UserSummaryDto("user-upd", "upduser", "upd@test.com", "+1111111111", false) });

        var handler = new UpdateEmployeeHandler(_db, _identityServiceMock.Object, _unitOfWorkMock.Object, _currentUserServiceMock.Object, _fileStorageServiceMock.Object);
        var cmd = new UpdateEmployeeCommand("emp-upd", "Update User New", "upd@test.com", "+1111111111", "upduser", null, null, DateOnly.FromDateTime(DateTime.Today), true, Roles.Staff, new[] { "b-a", "b-c" }, null, false);

        // Act
        var res = await handler.Handle(cmd, default);
        await _db.SaveChangesAsync();

        // Assert
        var currentBranches = await _db.UserBranches.Where(x => x.EmployeeId == "emp-upd").Select(x => x.BranchId).ToListAsync();
        currentBranches.Should().BeEquivalentTo("b-a", "b-c");
        currentBranches.Should().NotContain("b-b");

        var auditLog = await _db.AuditLogs.SingleOrDefaultAsync(x => x.EntityId == "emp-upd" && x.Action == "EmployeeUpdated");
        auditLog.Should().NotBeNull();
        auditLog!.DetailsJson.Should().Contain("\"oldBranchIds\":[\"b-a\",\"b-b\"]");
        auditLog.DetailsJson.Should().Contain("\"newBranchIds\":[\"b-a\",\"b-c\"]");
    }

    [Fact]
    public async Task GetEmployee_And_ListEmployees_ReturnCompleteAssignedSet()
    {
        // Arrange
        var branch1 = new Branch { Id = "br-1", TenantId = "tenant-1", Name = "North Branch", Address = "Addr 1", PhoneNumber = "111", GoogleMapsUrl = "url" };
        var branch2 = new Branch { Id = "br-2", TenantId = "tenant-1", Name = "South Branch", Address = "Addr 2", PhoneNumber = "222", GoogleMapsUrl = "url" };
        var emp = new Employee { Id = "emp-read", TenantId = "tenant-1", IdentityUserId = "user-read", FullName = "Read Tester", Email = "read@test.com", MobileNumber = "+9999999999", Code = "EMP-0099", IsActive = true };
        var map1 = new UserBranch { TenantId = "tenant-1", EmployeeId = "emp-read", BranchId = "br-1" };
        var map2 = new UserBranch { TenantId = "tenant-1", EmployeeId = "emp-read", BranchId = "br-2" };

        _db.Branches.AddRange(branch1, branch2);
        _db.Employees.Add(emp);
        _db.UserBranches.AddRange(map1, map2);
        await _db.SaveChangesAsync();

        _identityServiceMock.Setup(x => x.GetRolesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, string> { ["user-read"] = Roles.Admin });
        _identityServiceMock.Setup(x => x.GetUsersSummaryAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, UserSummaryDto> { ["user-read"] = new UserSummaryDto("user-read", "readuser", "read@test.com", "+9999999999", false) });

        var getHandler = new GetEmployeeHandler(_db, _identityServiceMock.Object, _currentUserServiceMock.Object);
        var listHandler = new ListEmployeesHandler(_db, _identityServiceMock.Object, _currentUserServiceMock.Object);

        // Act
        var getRes = await getHandler.Handle(new GetEmployeeQuery("emp-read"), default);
        var listRes = await listHandler.Handle(new ListEmployeesQuery(1, 10, null, null, null, null, null, null), default);

        // Assert
        getRes.Branches.Should().HaveCount(2);
        getRes.Branches.Select(b => b.Id).Should().BeEquivalentTo("br-1", "br-2");
        getRes.Branches.Select(b => b.Name).Should().BeEquivalentTo("North Branch", "South Branch");

        listRes.Items.Should().HaveCount(1);
        listRes.Items.Single().Branches.Should().HaveCount(2);
        listRes.Items.Single().Branches.Select(b => b.Id).Should().BeEquivalentTo("br-1", "br-2");
    }
}
