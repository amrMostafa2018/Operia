using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.DTOs;
using Operia.Application.Settings.Common;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Packages.Commands.CreateSubServiceCategory;

public sealed class CreateSubServiceCategoryHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IAuditWriter auditWriter) : IRequestHandler<CreateSubServiceCategoryCommand, SubServiceCategoryDto>
{
    public async Task<SubServiceCategoryDto> Handle(
        CreateSubServiceCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = PackageMapper.RequireTenant(currentUser);
        var business = await SettingsHandlerHelpers.GetBusinessAsync(db, tenantId, cancellationToken);
        var name = request.Name.Trim();
        var normalizedName = name.ToLowerInvariant();

        var parentExists = await db.ServiceCategories.AnyAsync(
            category => category.Id == request.ServiceCategoryId
                        && category.TenantId == tenantId
                        && category.BusinessId == business.Id,
            cancellationToken);

        if (!parentExists)
        {
            throw new NotFoundException(nameof(ServiceCategory), request.ServiceCategoryId);
        }

        var nameExists = await db.SubServiceCategories.AnyAsync(
            category => category.BusinessId == business.Id
                        && category.ServiceCategoryId == request.ServiceCategoryId
                        && category.Name.ToLower() == normalizedName,
            cancellationToken);

        if (nameExists)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Packages.SubCategoryNameTaken, "name");
        }

        var subCategory = new SubServiceCategory
        {
            TenantId = tenantId,
            BusinessId = business.Id,
            ServiceCategoryId = request.ServiceCategoryId,
            Name = name,
            IsActive = true
        };

        db.SubServiceCategories.Add(subCategory);
        auditWriter.Write(
            tenantId,
            AuditActions.SubServiceCategoryCreated,
            nameof(SubServiceCategory),
            subCategory.Id,
            subCategory.Name,
            PackageAuditDetails.ForSubServiceCategory(subCategory));
        await db.SaveChangesAsync(cancellationToken);

        return PackageMapper.ToDto(subCategory);
    }
}
