using Microsoft.OpenApi;

namespace Operia.Extensions;

public static class SwaggerExtensions
{
    private const string DocumentName = "v1";
    private const string DocumentTitle = "Operia API v1";

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = "Operia API",
                Version = "v1",
                Description = "Operia backend REST API"
            });
        });

        return services;
    }

    public static WebApplication UseSwaggerDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint($"/swagger/{DocumentName}/swagger.json", DocumentTitle);
            options.RoutePrefix = "swagger";
        });

        return app;
    }
}
