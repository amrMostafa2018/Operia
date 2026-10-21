using Operia.Infrastructure.Options;

namespace Operia.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection AddCorsPolicy(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(CorsSettings.PolicyName, policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }

    public static WebApplication UseCorsPolicy(this WebApplication app)
    {
        app.UseCors(CorsSettings.PolicyName);

        return app;
    }
}
