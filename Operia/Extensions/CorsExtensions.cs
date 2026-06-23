using Operia.Infrastructure.Options;

namespace Operia.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        if (!ShouldEnableCors(configuration, environment))
            return services;

        var corsSettings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()
            ?? new CorsSettings();

        services.AddCors(options =>
        {
            options.AddPolicy(CorsSettings.PolicyName, policy =>
            {
                if (environment.IsDevelopment())
                {
                    policy.SetIsOriginAllowed(origin =>
                    {
                        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                            return false;

                        return uri.Host is "localhost" or "127.0.0.1";
                    });
                }
                else
                {
                    policy.WithOrigins(corsSettings.AllowedOrigins);
                }

                policy.AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }

    public static WebApplication UseCorsPolicy(
        this WebApplication app,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        if (ShouldEnableCors(configuration, environment))
            app.UseCors(CorsSettings.PolicyName);

        return app;
    }

    private static bool ShouldEnableCors(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
            return true;

        var corsSettings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()
            ?? new CorsSettings();

        return corsSettings.AllowedOrigins.Length > 0;
    }
}
