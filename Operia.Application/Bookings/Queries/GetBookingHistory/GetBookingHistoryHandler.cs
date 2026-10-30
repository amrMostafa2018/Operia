using System.Text.Json.Nodes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Exceptions;
using Operia.Application.Common.Interfaces;
using Operia.SharedKernel.Errors;

namespace Operia.Application.Bookings.Queries.GetBookingHistory;

/// <summary>Executes the get booking history operation within the current tenant.</summary>
public sealed class GetBookingHistoryHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope) : IRequestHandler<GetBookingHistoryQuery, IReadOnlyList<BookingHistoryDto>>
{
    /// <summary>Returns the audit timeline for a booking visible to the caller.</summary>
    public async Task<IReadOnlyList<BookingHistoryDto>> Handle(
        GetBookingHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var bookingQuery = db.Bookings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == request.BookingId);
        if (currentUser.IsInRole(Roles.Staff))
        {
            var userId = currentUser.UserId!;
            bookingQuery = bookingQuery.Where(x =>
                x.Employee != null && x.Employee.IdentityUserId == userId);
        }

        var branchId = await bookingQuery
            .Select(x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken);

        if (branchId is null)
        {
            throw ApiNotFoundException.FromCode(ApiErrorCodes.Bookings.NotFound);
        }

        var allowedBranches = await BookingAccess.GetAllowedBranchesAsync(currentUser, branchScope, cancellationToken);
        BookingAccess.RequireBranch(allowedBranches, branchId);

        var history = await db.BookingHistory
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BookingId == request.BookingId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new BookingHistoryDto(
                x.Id,
                x.Action,
                x.ChangedByUserId,
                x.ChangedByDisplayName,
                x.CreatedAt,
                x.ChangesJson))
            .ToListAsync(cancellationToken);

        var itemNames = await db.BookingItems
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BookingId == request.BookingId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                PackageName = x.Package != null ? x.Package.Name : null
            })
            .ToListAsync(cancellationToken);
        var namesByItemId = itemNames.ToDictionary(
            x => x.Id,
            x => FirstName(x.Name, x.PackageName),
            StringComparer.Ordinal);

        return history
            .Select(entry => entry.Action == "Closed"
                ? entry with { ChangesJson = FillClosedItemNames(entry.ChangesJson, namesByItemId) }
                : entry)
            .ToList();
    }

    /// <summary>Fills a missing closed-line name from the booking item, leaving stored notes in place.</summary>
    private static string? FillClosedItemNames(
        string? changesJson,
        IReadOnlyDictionary<string, string> namesByItemId)
    {
        if (string.IsNullOrWhiteSpace(changesJson))
        {
            return changesJson;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(changesJson);
        }
        catch (System.Text.Json.JsonException)
        {
            return changesJson;
        }

        var items = root?["Items"]?.AsArray() ?? root?["items"]?.AsArray();
        if (items is null)
        {
            return changesJson;
        }

        foreach (var item in items)
        {
            if (item is not JsonObject line)
            {
                continue;
            }

            var bookingItemId = ReadString(line, "BookingItemId", "bookingItemId");
            var name = ReadString(line, "Name", "name");
            if (!string.IsNullOrWhiteSpace(name) ||
                bookingItemId is null ||
                !namesByItemId.TryGetValue(bookingItemId, out var resolved) ||
                string.IsNullOrWhiteSpace(resolved))
            {
                continue;
            }

            line["Name"] = resolved;
        }

        return root?.ToJsonString();
    }

    private static string? ReadString(JsonObject line, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (line[key] is not JsonValue value ||
                !value.TryGetValue<string>(out var text) ||
                string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            return text.Trim();
        }

        return null;
    }

    private static string FirstName(string? itemName, string? packageName) =>
        !string.IsNullOrWhiteSpace(itemName) ? itemName.Trim() :
        !string.IsNullOrWhiteSpace(packageName) ? packageName.Trim() :
        string.Empty;
}
