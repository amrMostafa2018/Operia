namespace Operia.Application.Branches.Commands.DeleteBranch;

public sealed record DeleteBranchCommand(string Id) : MediatR.IRequest;
