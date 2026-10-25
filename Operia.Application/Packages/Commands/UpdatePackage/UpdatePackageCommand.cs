using MediatR;
using Operia.Application.Packages.DTOs;

namespace Operia.Application.Packages.Commands.UpdatePackage;

public sealed record UpdatePackageCommand(
    string Id,
    string Name,
    bool IsActive,
    string OfferType,
    string Description,
    string ServiceCategoryId,
    string? SubServiceCategoryId,
    int SessionDurationMinutes,
    int? SessionCount,
    int? PulseCount,
    int? PackageExpiryMonths,
    decimal Price,
    string DiscountCode,
    decimal? DiscountPercent,
    bool CashEnabled = true,
    bool InstallmentEnabled = false,
    decimal? InstallmentPrice = null) : IRequest<PackageDetailDto>;
