using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Customers.Queries.ListDirectCustomerSales;

public sealed class ListDirectCustomerSalesHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<ListDirectCustomerSalesQuery, DirectCustomerSalesDto?>
{
    public async Task<DirectCustomerSalesDto?> Handle(
        ListDirectCustomerSalesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw ForbiddenException.FromCode(ApiErrorCodes.Access.TenantContextRequired);
        var customerId = await db.Customers.AsNoTracking()
            .Where(customer => customer.TenantId == tenantId && customer.IsDirect)
            .Select(customer => customer.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (customerId is null)
        {
            return null;
        }

        var scopedSales = db.Sales.AsNoTracking().Where(sale =>
            sale.TenantId == tenantId && sale.CustomerId == customerId);
        var total = await scopedSales.CountAsync(cancellationToken);
        var page = Math.Clamp(request.Page, 1, 100000);
        var rows = await scopedSales.OrderByDescending(sale => sale.CreatedAt)
            .ThenByDescending(sale => sale.Id)
            .Skip((page - 1) * 20)
            .Take(20)
            .Select(sale => new
            {
                sale.Id,
                sale.CreatedAt,
                sale.CreatedBy,
                sale.BranchId,
                sale.GrandTotal,
                sale.Status,
                sale.ItemsSnapshotJson
            })
            .ToListAsync(cancellationToken);

        return new DirectCustomerSalesDto(customerId, total,
            rows.Select(row => new DirectCustomerSaleDto(
                row.Id,
                row.CreatedAt,
                row.CreatedBy,
                row.BranchId,
                row.GrandTotal,
                row.Status,
                ReadNames(row.ItemsSnapshotJson))).ToArray());
    }

    private static IReadOnlyList<string> ReadNames(string snapshotJson)
    {
        using var document = JsonDocument.Parse(snapshotJson);
        return document.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("Name").GetString() ?? string.Empty)
            .ToArray();
    }
}
