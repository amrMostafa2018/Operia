namespace Operia.Application.Branches.Commands.UpdateBranch;

public sealed record UpdateBranchRequest(
    string Name,
    string Address,
    string PhoneNumber,
    decimal Latitude,
    decimal Longitude);
