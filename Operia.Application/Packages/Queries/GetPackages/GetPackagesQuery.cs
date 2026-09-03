using MediatR;
using Operia.Application.Packages.DTOs;

namespace Operia.Application.Packages.Queries.GetPackages;

public sealed record GetPackagesQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Search = null,
    string? OfferType = null,
    string? ServiceCategoryId = null,
    string? Status = null) : IRequest<PackageListResultDto>;
