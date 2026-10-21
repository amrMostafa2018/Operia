using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Enums;
using Operia.Domain.Exceptions;

namespace Operia.Application.Packages.Commands.DeletePackage;

public sealed class DeletePackageHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IAuditWriter auditWriter) : IRequestHandler<DeletePackageCommand>
{
    public async Task Handle(DeletePackageCommand request, CancellationToken cancellationToken)
    {
        var tenantId = PackageMapper.RequireTenant(currentUser);
        var package = await db.Packages.SingleOrDefaultAsync(
            item => item.Id == request.Id && item.TenantId == tenantId,
            cancellationToken) ?? throw new NotFoundException(nameof(Package), request.Id);

        if (package.Status == PackageStatus.Cancelled)
        {
            return;
        }

        package.Status = PackageStatus.Cancelled;
        auditWriter.Write(
            tenantId,
            AuditActions.PackageDeleted,
            nameof(Package),
            package.Id,
            package.Name,
            PackageAuditDetails.ForPackageDeleted());
        await db.SaveChangesAsync(cancellationToken);
    }
}
