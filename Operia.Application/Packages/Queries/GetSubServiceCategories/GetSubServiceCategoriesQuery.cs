using MediatR;
using Operia.Application.Packages.DTOs;

namespace Operia.Application.Packages.Queries.GetSubServiceCategories;

public sealed record GetSubServiceCategoriesQuery(string? ServiceCategoryId = null)
    : IRequest<IReadOnlyList<SubServiceCategoryDto>>;
