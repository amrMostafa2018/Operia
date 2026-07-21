namespace Operia.Application.Branches.Commands.UpdateBranch;

public sealed record UpdateBranchCommand(
    string Id,
    string Name,
    string Address,
    string PhoneNumber,
    decimal Latitude,
    decimal Longitude) : MediatR.IRequest<BranchDto>;
