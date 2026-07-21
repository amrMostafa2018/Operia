namespace Operia.Application.Branches.Commands.CreateBranch;

public sealed record CreateBranchCommand(
    string Name,
    string Address,
    string PhoneNumber,
    decimal Latitude,
    decimal Longitude) : MediatR.IRequest<BranchDto>;
