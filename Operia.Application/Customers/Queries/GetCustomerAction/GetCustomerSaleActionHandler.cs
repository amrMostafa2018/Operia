using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;
using Operia.SharedKernel.Interfaces;

namespace Operia.Application.Customers.Queries.GetCustomerAction;

public sealed class GetCustomerSaleActionHandler(
    IApplicationDbContext db, ICurrentUserService currentUser, IBranchScope branchScope)
    : IRequestHandler<GetCustomerSaleActionQuery, CustomerBlockActionDto>
{
    public async Task<CustomerBlockActionDto> Handle(
        GetCustomerSaleActionQuery request, CancellationToken token)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var allowed = await branchScope.GetAllowedBranchIdsAsync(
            currentUser.UserId ?? string.Empty, token);
        var customer = await (from sale in db.Sales.AsNoTracking()
                              join buyer in db.Customers.AsNoTracking()
                                  on sale.CustomerId equals buyer.Id
                              where sale.TenantId == tenantId && buyer.TenantId == tenantId &&
                                    sale.Id == request.SaleId && sale.CustomerId == request.CustomerId &&
                                    !buyer.IsDirect && allowed.Contains(sale.BranchId)
                              select buyer).SingleOrDefaultAsync(token)
            ?? throw ApiNotFoundException.FromCode(ApiErrorCodes.Sales.NotFound);

        return new(customer.Id, customer.FullName, customer.MobileNumber,
            Convert.ToBase64String(customer.Version), customer.IsBlocked, customer.BlockReason);
    }
}
