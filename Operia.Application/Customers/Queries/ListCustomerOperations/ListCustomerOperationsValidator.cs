using FluentValidation;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Customers.Queries.ListCustomerOperations;

public sealed class ListCustomerOperationsValidator : AbstractValidator<ListCustomerOperationsQuery>
{
    public ListCustomerOperationsValidator()
    {
        RuleFor(x => x.Filter).SetValidator(new CustomerOperationsFilterValidator());
    }
}

public sealed class CustomerOperationsFilterValidator : AbstractValidator<CustomerOperationsFilter>
{
    public CustomerOperationsFilterValidator()
    {
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithErrorCode(ApiErrorCodes.Customers.InvalidOperationDateRange);
        RuleFor(x => x.ServiceType)
            .Must(x => x is null or "package" or "session")
            .WithErrorCode(ApiErrorCodes.Customers.InvalidOperationFilter);
        RuleFor(x => x.Status)
            .Must(x => x is null or "active" or "completed" or "expired" or "cancelled" or "pending")
            .WithErrorCode(ApiErrorCodes.Customers.InvalidOperationFilter);
        RuleFor(x => x.PaymentType)
            .Must(x => x is null or "cash" or "installment" or "owned")
            .WithErrorCode(ApiErrorCodes.Customers.InvalidOperationFilter);
    }
}
