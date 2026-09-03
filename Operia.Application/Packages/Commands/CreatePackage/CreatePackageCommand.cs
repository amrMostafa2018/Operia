using MediatR;
using Operia.Application.Packages.DTOs;

namespace Operia.Application.Packages.Commands.CreatePackage;

public sealed record CreatePackageCommand(
    string Name,
    bool IsActive,
    string OfferType,
    string Description,
    string ServiceCategoryId,
    string? SubServiceCategoryId,
    int SessionDurationMinutes,
    int SessionCount,
    int? PulseCount,
    int? PackageExpiryMonths,
    decimal Price,
    string DiscountCode,
    decimal? DiscountPercent) : IRequest<PackageDetailDto>;
