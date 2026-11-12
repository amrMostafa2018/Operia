using MediatR;

namespace Operia.Application.Revenue.Queries.ExportActivityRevenue;

public sealed record ExportActivityRevenueQuery(
    DateOnly? FromDate,
    DateOnly? ToDate,
    string? BranchId,
    string? ServiceId,
    string? Search = null,
    string? Status = null,
    string? Mobile = null,
    string? CustomerName = null,
    string? EmployeeId = null) : IRequest<byte[]>;
