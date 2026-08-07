using Microsoft.EntityFrameworkCore;
using Operia.Application.Branches;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;

namespace Operia.Application.Branches.Commands.CreateBranch;

public sealed class CreateBranchHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IAuditWriter auditWriter) : MediatR.IRequestHandler<CreateBranchCommand, BranchDto>
{
    public async Task<BranchDto> Handle(
        CreateBranchCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = BranchMapper.RequireTenant(currentUser);
        var name = request.Name.Trim();

        var nameExists = await db.Branches.AnyAsync(
            branch => branch.TenantId == tenantId && branch.Name == name,
            cancellationToken);

        if (nameExists)
        {
            throw new ConflictException("A branch with this name already exists.");
        }

        var branch = BranchMapper.CreateEntity(
            tenantId,
            name,
            request.Address,
            request.PhoneNumber,
            request.Latitude,
            request.Longitude);

        db.Branches.Add(branch);
        auditWriter.Write(
            tenantId,
            AuditActions.BranchCreated,
            nameof(Branch),
            branch.Id,
            branch.Name);
        await db.SaveChangesAsync(cancellationToken);

        return BranchMapper.ToDto(branch);
    }
}
