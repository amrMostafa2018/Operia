using MediatR;

namespace Operia.Application.Customers.Commands.SetCustomerBlock;

public sealed record SetCustomerBlockCommand(
    string CustomerId, bool IsBlocked, string? Reason, string Version)
    : IRequest<SetCustomerBlockResult>;

public sealed record SetCustomerBlockResult(
    string CustomerId, bool IsBlocked, string? Reason, string Version);
