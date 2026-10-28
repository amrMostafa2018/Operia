namespace Operia.Application.Customers.Commands.SetCustomerBlock;

public sealed record SetCustomerBlockRequest(bool IsBlocked, string? Reason, string Version);
