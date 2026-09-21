using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Operia.Application.Bookings.Common;
using Operia.Application.Common.Authorization;
using Operia.Application.Common.Interfaces;
using Operia.Domain.Entities;
using Operia.Domain.Enums;

namespace Operia.Application.Bookings.Queries.ExportBookings;

/// <summary>Executes the export bookings operation within the current tenant.</summary>
public sealed class ExportBookingsHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IBranchScope branchScope) : IRequestHandler<ExportBookingsQuery, string>
{
    /// <summary>Exports visible bookings matching the requested filters as safe CSV.</summary>
    public async Task<string> Handle(ExportBookingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = BookingAccess.RequireTenant(currentUser);
        var branches = await BookingAccess.GetAllowedBranchesAsync(currentUser, branchScope, cancellationToken);
        var query = db.Bookings.AsNoTracking().Where(x =>
            x.TenantId == tenantId && branches.Contains(x.BranchId) &&
            x.ScheduledDate >= request.FromDate && x.ScheduledDate <= request.ToDate);
        query = ApplyFilters(query, request);
        if (currentUser.IsInRole(Roles.Staff))
        {
            var userId = currentUser.UserId!;
            query = query.Where(x => x.Employee != null && x.Employee.IdentityUserId == userId);
        }

        var rows = await query
            .OrderBy(x => x.ScheduledDate)
            .ThenBy(x => x.StartMinutes)
            .Select(x => new
            {
                x.BookingNumber,
                x.CustomerMobile,
                x.CustomerName,
                Service = x.Items.OrderBy(item => item.CreatedAt).Select(item => item.Name).FirstOrDefault() ?? string.Empty,
                Branch = x.Branch!.Name,
                Employee = x.Employee!.FullName,
                x.ScheduledDate,
                x.StartMinutes,
                x.EndMinutes,
                Status = x.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder("Booking Number,Customer Mobile,Customer Name,Service,Branch,Employee,Scheduled Date,From,To,Status\r\n");
        foreach (var row in rows)
        {
            csv.AppendJoin(',',
                Escape(row.BookingNumber),
                Escape(row.CustomerMobile),
                Escape(row.CustomerName),
                Escape(row.Service),
                Escape(row.Branch),
                Escape(row.Employee),
                row.ScheduledDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                FormatMinutes(row.StartMinutes),
                FormatMinutes(row.EndMinutes),
                row.Status);
            csv.Append("\r\n");
        }
        return csv.ToString();
    }

    /// <summary>Applies booking filters to the already tenant- and branch-scoped query.</summary>
    private static IQueryable<Booking> ApplyFilters(IQueryable<Booking> query, ExportBookingsQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.CustomerMobile))
        {
            var mobile = request.CustomerMobile.Trim();
            query = query.Where(x => x.CustomerMobile.Contains(mobile));
        }
        if (!string.IsNullOrWhiteSpace(request.CustomerName))
        {
            var name = request.CustomerName.Trim();
            query = query.Where(x => x.CustomerName.Contains(name));
        }
        if (!string.IsNullOrWhiteSpace(request.EmployeeId))
        {
            query = query.Where(x => x.EmployeeId == request.EmployeeId);
        }
        if (Enum.TryParse<BookingStatus>(request.Status, true, out var status))
        {
            query = query.Where(x => x.Status == status);
        }
        return query;
    }

    /// <summary>Quotes and escapes a value for a safe CSV field.</summary>
    private static string Escape(string value)
    {
        var trimmedStart = value.TrimStart();
        var spreadsheetSafe = trimmedStart.Length > 0 && trimmedStart[0] is '=' or '+' or '-' or '@'
            ? $"'{value}"
            : value;
        return $"\"{spreadsheetSafe.Replace("\"", "\"\"")}\"";
    }

    /// <summary>Formats minutes after midnight as a 24-hour clock value.</summary>
    private static string FormatMinutes(int minutes) =>
        $"{minutes / 60:00}:{minutes % 60:00}";
}
