using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Operia.Controllers;
using Operia.Extensions;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Operia.Application.Tests.Swagger;

public sealed class SwaggerDocumentTests
{
    [Fact]
    public void ApiDocumentCanBeGenerated()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(AdminController).Assembly.GetName().Name,
            EnvironmentName = "Development"
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminController).Assembly);
        builder.Services.AddSwaggerDocumentation();

        using var app = builder.Build();
        var swagger = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        Assert.NotEmpty(swagger.Paths);
        var schemaIds = swagger.Components!.Schemas!.Keys;
        Assert.Contains(
            "Operia.Application.Customers.Commands.CollectInstallment.CollectInstallmentRequest",
            schemaIds);
        Assert.Contains(
            "Operia.Application.Packages.InstallmentPurchases.CollectInstallmentRequest",
            schemaIds);
    }
}
