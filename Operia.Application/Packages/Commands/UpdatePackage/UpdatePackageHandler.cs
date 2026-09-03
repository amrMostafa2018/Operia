using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.DTOs;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Packages.Commands.UpdatePackage;

public sealed class UpdatePackageHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IAuditWriter auditWriter) : IRequestHandler<UpdatePackageCommand, PackageDetailDto>
{
    public async Task<PackageDetailDto> Handle(
        UpdatePackageCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = PackageMapper.RequireTenant(currentUser);
        var package = await db.Packages.SingleOrDefaultAsync(
            item => item.Id == request.Id && item.TenantId == tenantId,
            cancellationToken) ?? throw new NotFoundException(nameof(Package), request.Id);

        if (package.Status == PackageStatus.Cancelled)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Packages.PackageAlreadyCancelled);
        }

        var offerType = PackageMapper.ParseOfferType(request.OfferType);
        await PackageCategoryValidator.ValidateAsync(
            db,
            package.BusinessId,
            request.ServiceCategoryId,
            request.SubServiceCategoryId,
            cancellationToken);

        var previous = PackageAuditDetails.ForPackage(package);

        package.Name = request.Name.Trim();
        package.Description = request.Description.Trim();
        package.OfferType = offerType;
        package.ServiceCategoryId = request.ServiceCategoryId;
        package.SubServiceCategoryId = request.SubServiceCategoryId;
        package.SessionDurationMinutes = request.SessionDurationMinutes;
        package.SessionCount = offerType == OfferType.Package ? request.SessionCount : 0;
        package.PulseCount = offerType == OfferType.Package ? request.PulseCount : null;
        package.PackageExpiryMonths = offerType == OfferType.Package ? request.PackageExpiryMonths : null;
        package.Price = request.Price;
        package.DiscountCode = request.DiscountCode.Trim();
        package.DiscountPercent = request.DiscountPercent;
        package.Status = PackageMapper.FromIsActive(request.IsActive);

        auditWriter.Write(
            tenantId,
            AuditActions.PackageUpdated,
            nameof(Package),
            package.Id,
            package.Name,
            PackageAuditDetails.ForPackageUpdated(previous, package));
        await db.SaveChangesAsync(cancellationToken);

        var updated = await db.Packages
            .AsNoTracking()
            .Include(item => item.ServiceCategory)
            .SingleAsync(item => item.Id == package.Id, cancellationToken);

        return PackageMapper.ToDetailDto(updated);
    }
}
