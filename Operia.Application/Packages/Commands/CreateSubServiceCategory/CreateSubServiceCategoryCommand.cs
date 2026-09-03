using MediatR;
using Operia.Application.Packages.DTOs;

namespace Operia.Application.Packages.Commands.CreateSubServiceCategory;

public sealed record CreateSubServiceCategoryCommand(string Name, string ServiceCategoryId)
    : IRequest<SubServiceCategoryDto>;
