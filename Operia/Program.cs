using QuestPDF.Infrastructure;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Operia.Application;
using Operia.Extensions;
using Operia.Infrastructure;
using Operia.Infrastructure.Middleware;
using Operia.Infrastructure.Options;
using Operia.Infrastructure.Persistence;
using Operia.Middleware;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Operia API");

    QuestPDF.Settings.License = LicenseType.Community;

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });
    builder.Services.AddSwaggerDocumentation();

    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddCorsPolicy(builder.Configuration, builder.Environment);

    const long multipartRequestBodyOverheadBytes = 1024 * 1024;

    var fileStorageSettings = builder.Configuration
        .GetSection(FileStorageSettings.SectionName)
        .Get<FileStorageSettings>() ?? new FileStorageSettings();

    builder.Services.Configure<FormOptions>(options =>
        options.MultipartBodyLengthLimit = fileStorageSettings.MaxFileSizeBytes);
    builder.WebHost.ConfigureKestrel(options =>
        options.Limits.MaxRequestBodySize = fileStorageSettings.MaxFileSizeBytes + multipartRequestBodyOverheadBytes);

    var app = builder.Build();

    await DatabaseInitializer.InitializeAsync(app.Services);

    app.UseSerilogRequestLogging();

    app.UseSwaggerDocumentation();

    app.UseCorsPolicy(builder.Configuration, app.Environment);

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (!app.Environment.IsDevelopment())
        app.UseHttpsRedirection();

    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseMiddleware<SecurityStampValidationMiddleware>();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
