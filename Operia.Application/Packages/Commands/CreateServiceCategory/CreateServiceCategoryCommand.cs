using MediatR;
using Operia.Application.Packages.DTOs;

namespace Operia.Application.Packages.Commands.CreateServiceCategory;

public sealed record CreateServiceCategoryCommand(string Name, string Icon) : IRequest<ServiceCategoryDto>;
