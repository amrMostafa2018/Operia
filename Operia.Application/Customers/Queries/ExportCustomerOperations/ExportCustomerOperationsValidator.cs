using FluentValidation;
using Operia.Application.Customers.Queries.ListCustomerOperations;

namespace Operia.Application.Customers.Queries.ExportCustomerOperations;

public sealed class ExportCustomerOperationsValidator : AbstractValidator<ExportCustomerOperationsQuery>
{
    public ExportCustomerOperationsValidator()
    {
        RuleFor(x => x.Filter).SetValidator(new CustomerOperationsFilterValidator());
    }
}
