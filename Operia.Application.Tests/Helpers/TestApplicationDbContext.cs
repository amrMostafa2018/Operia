using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Interfaces;
using Operia.Infrastructure.Persistence;

namespace Operia.Application.Tests.Helpers;

public class TestApplicationDbContext : ApplicationDbContext
{
    public TestApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IDateTimeProvider dateTimeProvider,
        ICurrentUserService currentUserService)
        : base(options, dateTimeProvider, currentUserService)
    {
    }

}
