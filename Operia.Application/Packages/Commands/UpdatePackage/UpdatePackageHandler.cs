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
    IAuditWriter auditWriter,
    IBranchScope branchScope,
    IIdentityService identity) : IRequestHandler<UpdatePackageCommand, PackageDetailDto>
{
    public async Task<PackageDetailDto> Handle(
        UpdatePackageCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = PackageMapper.RequireTenant(currentUser);
        var package = await db.Packages.Include(item => item.Installments)
            .Include(item => item.AvailableStaff).SingleOrDefaultAsync(
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
        await PackageStaffEligibility.ValidateSelectionAsync(
            db, currentUser, branchScope, identity, tenantId,
            request.AllStaff, request.StaffIds, cancellationToken);

        var previous = PackageAuditDetails.ForPackage(package);

        package.Name = request.Name.Trim();
        package.Description = request.Description.Trim();
        package.OfferType = offerType;
        package.ServiceCategoryId = request.ServiceCategoryId;
        package.SubServiceCategoryId = request.SubServiceCategoryId;
        package.SessionDurationMinutes = request.SessionDurationMinutes;
        package.SessionCount = offerType == OfferType.Package ? request.SessionCount : null;
        package.PulseCount = offerType == OfferType.Package ? request.PulseCount : null;
        package.PackageExpiryMonths = offerType == OfferType.Package ? request.PackageExpiryMonths : null;
        package.Price = request.Price;
        package.CashEnabled = request.CashEnabled;
        package.InstallmentEnabled = request.InstallmentEnabled;
        package.InstallmentPrice = request.InstallmentEnabled ? request.InstallmentPrice : null;
        package.FirstPayment = request.InstallmentEnabled ? request.FirstPayment : null;
        package.InstallmentCount = request.InstallmentEnabled ? request.InstallmentCount : null;
        package.AllStaff = request.AllStaff;
        InstallmentPlanRules.ReplaceRows(
            package,
            tenantId,
            request.InstallmentEnabled ? request.Installments : null);
        var selectedStaffIds = (request.StaffIds ?? []).ToHashSet(StringComparer.Ordinal);
        foreach (var existing in package.AvailableStaff.Where(staff => !selectedStaffIds.Contains(staff.EmployeeId)).ToList())
        {
            db.PackageStaff.Remove(existing);
            package.AvailableStaff.Remove(existing);
        }
        var existingStaffIds = package.AvailableStaff.Select(staff => staff.EmployeeId).ToHashSet(StringComparer.Ordinal);
        foreach (var id in selectedStaffIds.Where(id => !existingStaffIds.Contains(id)))
        {
            package.AvailableStaff.Add(new PackageStaff
            {
                TenantId = tenantId,
                PackageId = package.Id,
                EmployeeId = id
            });
        }
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
            .Include(item => item.Installments)
            .Include(item => item.AvailableStaff)
            .SingleAsync(item => item.Id == package.Id, cancellationToken);

        return PackageMapper.ToDetailDto(updated);
    }
}
