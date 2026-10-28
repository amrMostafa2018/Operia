using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Behaviors;
using Operia.Application.Customers.Queries.ListCustomerOperations;

namespace Operia.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<CustomerOperationsReader>();

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
