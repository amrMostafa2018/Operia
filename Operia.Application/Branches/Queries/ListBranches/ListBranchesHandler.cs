using Microsoft.EntityFrameworkCore;
using Operia.Application.Branches;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Branches.Queries.ListBranches;

public sealed class ListBranchesHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser) : MediatR.IRequestHandler<ListBranchesQuery, BranchListResult>
{
    public async Task<BranchListResult> Handle(
        ListBranchesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = BranchMapper.RequireTenant(currentUser);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var pageNumber = Math.Max(request.PageNumber, 1);
        var query = db.Branches.AsNoTracking().Where(branch => branch.TenantId == tenantId);
        var totalTenantCount = await query.CountAsync(cancellationToken);
        var search = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(branch =>
                branch.Name.Contains(search)
                || branch.Address.Contains(search)
                || branch.PhoneNumber.Contains(search));
        }

        var descending = string.Equals(
            request.SortDirection,
            "desc",
            StringComparison.OrdinalIgnoreCase);

        query = request.SortBy?.ToLowerInvariant() switch
        {
            "address" => descending
                ? query.OrderByDescending(branch => branch.Address)
                : query.OrderBy(branch => branch.Address),
            "phonenumber" or "phone" => descending
                ? query.OrderByDescending(branch => branch.PhoneNumber)
                : query.OrderBy(branch => branch.PhoneNumber),
            _ => descending
                ? query.OrderByDescending(branch => branch.Name)
                : query.OrderBy(branch => branch.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(branch => BranchMapper.ToDto(branch))
            .ToListAsync(cancellationToken);

        return new BranchListResult(
            items,
            pageNumber,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize),
            totalTenantCount);
    }
}
