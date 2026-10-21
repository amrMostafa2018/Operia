using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Employees;
using Operia.Application.Employees.Commands.UpdateEmployeeSchedule;
using Operia.Application.Tests.Helpers;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;
using Operia.Infrastructure.Persistence;
using Xunit;

namespace Operia.Application.Tests.Employees;

public class EmployeeScheduleValidationTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock = new();
    private readonly TestApplicationDbContext _db;
    private readonly IAuditWriter _auditWriter;
    private readonly UpdateEmployeeScheduleHandler _handler;

    public EmployeeScheduleValidationTests()
    {
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("EmployeeScheduleValidationTests_" + Guid.NewGuid())
            .Options;

        _db = new TestApplicationDbContext(options, _dateTimeProviderMock.Object, _currentUserServiceMock.Object);
        _auditWriter = new AuditWriter(_db, _currentUserServiceMock.Object);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns("tenant-1");
        _currentUserServiceMock.Setup(x => x.UserId).Returns("admin-user");
        _currentUserServiceMock.Setup(x => x.DisplayName).Returns("Admin Tester");

        _handler = new UpdateEmployeeScheduleHandler(_db, _currentUserServiceMock.Object, _auditWriter);
    }

    [Fact]
    public async Task UpdateEmployeeSchedule_RejectsOverlappingHoursAcrossBranchesOnSameDay()
    {
        await SeedEmployeeWithBranchesAsync();

        var command = new UpdateEmployeeScheduleCommand(
            "emp-sched",
            [
                new EmployeeBranchScheduleInput("b-a", CreateWeek("mon", new(9, 0), new(13, 0))),
                new EmployeeBranchScheduleInput("b-b", CreateWeek("mon", new(12, 0), new(17, 0)))
            ]);

        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(() => _handler.Handle(command, default));

        ex.Errors.Values.SelectMany(x => x).Should().Contain(
            "An employee cannot work in multiple branches at the same time on the same day.");
        ex.Errors.Keys.Should().Contain("branches[1].days[3]");
    }

    [Fact]
    public async Task UpdateEmployeeSchedule_AllowsAdjacentNonOverlappingHoursAcrossBranches()
    {
        await SeedEmployeeWithBranchesAsync();

        var command = new UpdateEmployeeScheduleCommand(
            "emp-sched",
            [
                new EmployeeBranchScheduleInput("b-a", CreateWeek("mon", new(9, 0), new(12, 0))),
                new EmployeeBranchScheduleInput("b-b", CreateWeek("mon", new(12, 0), new(17, 0)))
            ]);

        var result = await _handler.Handle(command, default);
        await _db.SaveChangesAsync();

        result.Branches.Should().HaveCount(2);
        var mondayRows = await _db.EmployeeWorkingDays
            .Where(x => x.EmployeeId == "emp-sched" && x.Day == "mon" && x.Enabled)
            .ToListAsync();
        mondayRows.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateEmployeeSchedule_AllowsSameDayAcrossBranchesWhenOnlyOneBranchIsEnabled()
    {
        await SeedEmployeeWithBranchesAsync();

        var command = new UpdateEmployeeScheduleCommand(
            "emp-sched",
            [
                new EmployeeBranchScheduleInput("b-a", CreateWeek("mon", new(9, 0), new(17, 0))),
                new EmployeeBranchScheduleInput("b-b", DisabledWeek())
            ]);

        var result = await _handler.Handle(command, default);
        await _db.SaveChangesAsync();

        result.Branches.Should().HaveCount(2);
        var enabledRows = await _db.EmployeeWorkingDays
            .Where(x => x.EmployeeId == "emp-sched" && x.Enabled)
            .ToListAsync();
        enabledRows.Should().HaveCount(1);
    }

    private async Task SeedEmployeeWithBranchesAsync()
    {
        var branchA = BranchTestData.Branch("b-a", "tenant-1", "business-tenant-1", "Branch A");
        var branchB = BranchTestData.Branch("b-b", "tenant-1", "business-tenant-1", "Branch B");
        var employee = BranchTestData.Employee(
            "emp-sched",
            "tenant-1",
            "business-tenant-1",
            "user-sched",
            "EMP-0200",
            "Schedule Tester",
            "sched@test.com",
            "+1000000001");

        _db.Businesses.Add(BranchTestData.Business("tenant-1"));
        _db.Branches.AddRange(branchA, branchB);
        _db.Employees.Add(employee);
        _db.UserBranches.AddRange(
            new UserBranch { TenantId = "tenant-1", EmployeeId = employee.Id, BranchId = branchA.Id },
            new UserBranch { TenantId = "tenant-1", EmployeeId = employee.Id, BranchId = branchB.Id });
        await _db.SaveChangesAsync();
    }

    private static IReadOnlyList<EmployeeWorkingDayDto> CreateWeek(string enabledDay, TimeOnly from, TimeOnly to)
    {
        var weekDays = new[] { "fri", "sat", "sun", "mon", "tue", "wed", "thu" };
        return weekDays
            .Select(day =>
            {
                var enabled = day.Equals(enabledDay, StringComparison.OrdinalIgnoreCase);
                return new EmployeeWorkingDayDto(day, enabled, enabled ? from : null, enabled ? to : null);
            })
            .ToList();
    }

    private static IReadOnlyList<EmployeeWorkingDayDto> DisabledWeek()
    {
        var weekDays = new[] { "fri", "sat", "sun", "mon", "tue", "wed", "thu" };
        return weekDays.Select(day => new EmployeeWorkingDayDto(day, false, null, null)).ToList();
    }
}
