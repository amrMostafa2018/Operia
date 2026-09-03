using MediatR;

namespace Operia.Application.Packages.Commands.DeletePackage;

public sealed record DeletePackageCommand(string Id) : IRequest;
