using MediatR;
using Operia.Application.Revenue.Queries.GetActivityRevenueReport;

namespace Operia.Application.Revenue.Queries.ExportActivityRevenue;

public sealed class ExportActivityRevenueHandler(IMediator mediator)
    : IRequestHandler<ExportActivityRevenueQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportActivityRevenueQuery request, CancellationToken token)
    {
        var report = await mediator.Send(new GetActivityRevenueReportQuery(
            request.FromDate, request.ToDate, request.BranchId, request.ServiceId,
            AllRows: true), token);
        return ActivityRevenueWorkbook.Write(report);
    }
}
