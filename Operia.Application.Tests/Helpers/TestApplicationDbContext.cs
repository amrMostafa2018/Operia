using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Interfaces;
using Operia.Infrastructure.Persistence;

namespace Operia.Application.Tests.Helpers;

public class TestApplicationDbContext : ApplicationDbContext
{
    private int _counter = 1;

    public TestApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IDateTimeProvider dateTimeProvider,
        ICurrentUserService currentUserService)
        : base(options, dateTimeProvider, currentUserService)
    {
    }

    public override Task<string> GetNextEmployeeCodeAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var code = $"EMP-{_counter++:0000}";
        return Task.FromResult(code);
    }
}
