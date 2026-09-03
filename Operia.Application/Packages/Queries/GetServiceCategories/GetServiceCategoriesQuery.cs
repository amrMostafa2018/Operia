using MediatR;
using Operia.Application.Packages.DTOs;

namespace Operia.Application.Packages.Queries.GetServiceCategories;

public sealed record GetServiceCategoriesQuery : IRequest<IReadOnlyList<ServiceCategoryDto>>;
