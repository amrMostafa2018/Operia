using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.DTOs;
using Operia.Application.Settings.Common;
using Operia.Domain.Entities;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Packages.Commands.CreateServiceCategory;

public sealed class CreateServiceCategoryHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IAuditWriter auditWriter) : IRequestHandler<CreateServiceCategoryCommand, ServiceCategoryDto>
{
    public async Task<ServiceCategoryDto> Handle(
        CreateServiceCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = PackageMapper.RequireTenant(currentUser);
        var business = await SettingsHandlerHelpers.GetBusinessAsync(db, tenantId, cancellationToken);
        var name = request.Name.Trim();
        var normalizedName = name.ToLowerInvariant();

        var nameExists = await db.ServiceCategories.AnyAsync(
            category => category.BusinessId == business.Id
                        && category.Name.ToLower() == normalizedName,
            cancellationToken);

        if (nameExists)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Packages.CategoryNameTaken, "name");
        }

        var category = new ServiceCategory
        {
            TenantId = tenantId,
            BusinessId = business.Id,
            Name = name,
            Icon = request.Icon.Trim(),
            IsActive = true
        };

        db.ServiceCategories.Add(category);
        auditWriter.Write(
            tenantId,
            AuditActions.ServiceCategoryCreated,
            nameof(ServiceCategory),
            category.Id,
            category.Name,
            PackageAuditDetails.ForServiceCategory(category));
        await db.SaveChangesAsync(cancellationToken);

        return PackageMapper.ToDto(category);
    }
}
