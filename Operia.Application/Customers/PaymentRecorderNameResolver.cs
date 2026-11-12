using Microsoft.EntityFrameworkCore;
using Operia.Application.Common.Interfaces;

namespace Operia.Application.Customers;

internal static class PaymentRecorderNameResolver
{
    public static async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        IApplicationDbContext db,
        IIdentityService identity,
        string tenantId,
        IEnumerable<string> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var names = await db.Employees.AsNoTracking()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.IdentityUserId))
            .ToDictionaryAsync(x => x.IdentityUserId, x => x.FullName, cancellationToken);

        var missing = ids.Where(id => !names.ContainsKey(id)).ToArray();
        if (missing.Length == 0)
        {
            return names;
        }

        var users = await identity.GetUsersSummaryAsync(missing, cancellationToken);
        foreach (var userId in missing)
        {
            if (!users.TryGetValue(userId, out var summary))
            {
                continue;
            }

            var display = !string.IsNullOrWhiteSpace(summary.FullName)
                ? summary.FullName
                : summary.UserName;
            if (!string.IsNullOrWhiteSpace(display))
            {
                names[userId] = display;
            }
        }

        return names;
    }

    public static string? DisplayName(string? userId, IReadOnlyDictionary<string, string> names) =>
        string.IsNullOrWhiteSpace(userId)
            ? null
            : names.TryGetValue(userId, out var name) ? name : null;
}
