using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Interfaces;
using Operia.Infrastructure.Persistence;
using Operia.Infrastructure.Repositories;
using Operia.Infrastructure.Services;
using Operia.SharedKernel.Interfaces;

namespace Operia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IWeatherForecastRepository, WeatherForecastRepository>();

        return services;
    }
}
