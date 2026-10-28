using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Auditing;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Customers.Commands.SetCustomerBlock;

public sealed class SetCustomerBlockHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IAuditWriter auditWriter)
    : IRequestHandler<SetCustomerBlockCommand, SetCustomerBlockResult>
{
    public async Task<SetCustomerBlockResult> Handle(SetCustomerBlockCommand request,
        CancellationToken token)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var customer = await db.Customers.SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.Id == request.CustomerId && !x.IsDirect, token)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Customers.CustomerNotFound);
        if (Convert.ToBase64String(customer.Version) != request.Version)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Customers.CustomerChanged);
        }
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (customer.IsBlocked == request.IsBlocked && customer.BlockReason == reason)
        {
            return new(customer.Id, customer.IsBlocked, customer.BlockReason,
                Convert.ToBase64String(customer.Version));
        }
        var oldState = customer.IsBlocked;
        var oldReason = customer.BlockReason;
        customer.IsBlocked = request.IsBlocked;
        customer.BlockReason = reason;
        auditWriter.Write(tenantId,
            customer.IsBlocked ? AuditActions.CustomerBlocked : AuditActions.CustomerUnblocked,
            nameof(Operia.Domain.Entities.Customer), customer.Id, customer.FullName,
            new
            {
                OldState = oldState,
                NewState = customer.IsBlocked,
                OldReason = oldReason,
                NewReason = reason,
                CenterId = tenantId
            });
        try
        {
            await db.SaveChangesAsync(token);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ConflictException.FromCode(ApiErrorCodes.Customers.CustomerChanged);
        }
        return new(customer.Id, customer.IsBlocked, customer.BlockReason,
            Convert.ToBase64String(customer.Version));
    }
}
