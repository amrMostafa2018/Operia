using MediatR;

namespace Operia.Application.Revenue.Queries.ExportActivityRevenue;

public sealed record ExportActivityRevenueQuery(
    DateOnly? FromDate, DateOnly? ToDate, string? BranchId, string? ServiceId)
    : IRequest<byte[]>;
