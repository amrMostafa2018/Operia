namespace Operia.Domain.Interfaces;

public static class BranchScopedExtensions
{
    public static IQueryable<T> ScopedToBranches<T>(this IQueryable<T> query, IReadOnlyCollection<string> scope)
        where T : IBranchScoped
    {
        if (scope is null || scope.Count == 0)
        {
            return query.Where(x => false);
        }

        var branchIds = scope as string[] ?? scope.ToArray();
        return query.Where(x => branchIds.Contains(x.BranchId));
    }
}
