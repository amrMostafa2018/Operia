using MediatR;

namespace Operia.Application.Customers.Queries.ListCustomerOperations;

public sealed class ListCustomerOperationsHandler(CustomerOperationsReader reader)
    : IRequestHandler<ListCustomerOperationsQuery, CustomerOperationsPageDto>
{
    public async Task<CustomerOperationsPageDto> Handle(
        ListCustomerOperationsQuery request,
        CancellationToken cancellationToken)
    {
        var data = await reader.ReadAsync(request.Filter, cancellationToken);
        var rows = data.Rows;
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 5, 100);
        var skip = ((long)page - 1) * pageSize;
        return new CustomerOperationsPageDto(
            rows.Count,
            page,
            pageSize,
            rows.Sum(x => x.Total ?? 0m),
            rows.Sum(x => x.Collected ?? 0m),
            rows.Sum(x => x.Remaining ?? 0m),
            skip >= rows.Count ? [] : rows.Skip((int)skip).Take(pageSize).ToArray(),
            rows.Sum(x => x.Refunded), rows.Sum(x => x.NetCollected ?? 0m),
            data.TimezoneId);
    }
}
