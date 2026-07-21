using Microsoft.EntityFrameworkCore;
using Operia.Application.Branches;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Application.Common.PhoneNumbers;
using Operia.Domain.Entities;
using Operia.Domain.Exceptions;

namespace Operia.Application.Branches.Commands.UpdateBranch;

public sealed class UpdateBranchHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser) : MediatR.IRequestHandler<UpdateBranchCommand, BranchDto>
{
    public async Task<BranchDto> Handle(
        UpdateBranchCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = BranchMapper.RequireTenant(currentUser);
        var branch = await db.Branches.SingleOrDefaultAsync(
            item => item.Id == request.Id && item.TenantId == tenantId,
            cancellationToken) ?? throw new NotFoundException(nameof(Branch), request.Id);
        var name = request.Name.Trim();

        var nameExists = await db.Branches.AnyAsync(
            item => item.Id != branch.Id && item.TenantId == tenantId && item.Name == name,
            cancellationToken);

        if (nameExists)
        {
            throw new ConflictException("A branch with this name already exists.");
        }

        branch.Name = name;
        branch.Address = request.Address.Trim();
        branch.PhoneNumber = PhoneNumberHelper.ToE164(request.PhoneNumber);
        branch.Latitude = request.Latitude;
        branch.Longitude = request.Longitude;
        branch.GoogleMapsUrl = BranchMapper.MapsUrl(request.Latitude, request.Longitude);

        BranchMapper.AddAudit(
            db,
            currentUser,
            tenantId,
            "BranchUpdated",
            branch,
            new { branch.Address, branch.PhoneNumber, branch.Latitude, branch.Longitude });
        await db.SaveChangesAsync(cancellationToken);

        return BranchMapper.ToDto(branch);
    }
}
