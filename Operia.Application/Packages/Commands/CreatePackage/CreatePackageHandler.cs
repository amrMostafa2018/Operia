using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Packages.DTOs;
using Operia.Application.Settings.Common;
using Operia.Domain.Entities;
using Operia.Domain.Enums;

namespace Operia.Application.Packages.Commands.CreatePackage;

public sealed class CreatePackageHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IAuditWriter auditWriter,
    IBranchScope branchScope,
    IIdentityService identity) : IRequestHandler<CreatePackageCommand, PackageDetailDto>
{
    public async Task<PackageDetailDto> Handle(
        CreatePackageCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = PackageMapper.RequireTenant(currentUser);
        var business = await SettingsHandlerHelpers.GetBusinessAsync(db, tenantId, cancellationToken);
        var offerType = PackageMapper.ParseOfferType(request.OfferType);

        await PackageCategoryValidator.ValidateAsync(
            db,
            business.Id,
            request.ServiceCategoryId,
            request.SubServiceCategoryId,
            cancellationToken);
        await PackageStaffEligibility.ValidateSelectionAsync(
            db, currentUser, branchScope, identity, tenantId,
            request.AllStaff, request.StaffIds, cancellationToken);

        var package = new Package
        {
            TenantId = tenantId,
            BusinessId = business.Id,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            OfferType = offerType,
            ServiceCategoryId = request.ServiceCategoryId,
            SubServiceCategoryId = request.SubServiceCategoryId,
            SessionDurationMinutes = request.SessionDurationMinutes,
            SessionCount = offerType == OfferType.Package ? request.SessionCount : null,
            PulseCount = offerType == OfferType.Package ? request.PulseCount : null,
            PackageExpiryMonths = offerType == OfferType.Package ? request.PackageExpiryMonths : null,
            Price = request.Price,
            CashEnabled = request.CashEnabled,
            InstallmentEnabled = request.InstallmentEnabled,
            InstallmentPrice = request.InstallmentEnabled ? request.InstallmentPrice : null,
            FirstPayment = request.InstallmentEnabled ? request.FirstPayment : null,
            InstallmentCount = request.InstallmentEnabled ? request.InstallmentCount : null,
            AllStaff = request.AllStaff,
            DiscountCode = request.DiscountCode.Trim(),
            DiscountPercent = request.DiscountPercent,
            Status = PackageMapper.FromIsActive(request.IsActive)
        };

        if (request.InstallmentEnabled)
        {
            InstallmentPlanRules.ReplaceRows(package, tenantId, request.Installments);
        }
        package.AvailableStaff = (request.StaffIds ?? [])
            .Select(id => new PackageStaff { TenantId = tenantId, EmployeeId = id, PackageId = package.Id })
            .ToList();

        db.Packages.Add(package);
        auditWriter.Write(
            tenantId,
            AuditActions.PackageCreated,
            nameof(Package),
            package.Id,
            package.Name,
            PackageAuditDetails.ForPackage(package));
        await db.SaveChangesAsync(cancellationToken);

        var created = await db.Packages
            .AsNoTracking()
            .Include(item => item.ServiceCategory)
            .Include(item => item.Installments)
            .Include(item => item.AvailableStaff)
            .SingleAsync(item => item.Id == package.Id, cancellationToken);

        return PackageMapper.ToDetailDto(created);
    }
}
