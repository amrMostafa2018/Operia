using MediatR;

namespace Operia.Application.Customers.Commands.RegisterBookingCustomer;

public sealed record RegisterBookingCustomerCommand(string FullName, string MobileNumber)
    : IRequest<RegisteredBookingCustomerResult>;

public sealed record RegisteredBookingCustomerResult(string Id, string FullName, string MobileNumber);
