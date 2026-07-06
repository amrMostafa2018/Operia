using MediatR;

namespace Operia.Application.Admin.Commands.AddTenantBalance;

public sealed record AddTenantBalanceCommand(
    string TenantId,
    decimal Amount) : IRequest;
