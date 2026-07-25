using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Operia.Application.Common.Interfaces;
using Operia.Infrastructure.Services;

namespace Operia.Infrastructure.Persistence;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=OperiaDesignTime;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new ApplicationDbContext(options, new DateTimeProvider(), new DesignTimeCurrentUserService());
    }

    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        public string? UserId => null;
        public string? TenantId => null;
        public string? DisplayName => null;
        public bool IsInRole(string role) => false;
    }
}
