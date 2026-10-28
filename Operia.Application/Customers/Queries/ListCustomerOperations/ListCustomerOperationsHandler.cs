using MediatR;

namespace Operia.Application.Customers.Queries.ListCustomerOperations;

public sealed class ListCustomerOperationsHandler(CustomerOperationsReader reader)
    : IRequestHandler<ListCustomerOperationsQuery, CustomerOperationsPageDto>
{
    public async Task<CustomerOperationsPageDto> Handle(
        ListCustomerOperationsQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await reader.ReadAsync(request.Filter, cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 5, 100);
        var skip = ((long)page - 1) * pageSize;
        return new CustomerOperationsPageDto(
            rows.Count,
            page,
            pageSize,
            rows.Sum(x => x.Total),
            rows.Sum(x => x.Collected),
            rows.Sum(x => x.Remaining),
            skip >= rows.Count ? [] : rows.Skip((int)skip).Take(pageSize).ToArray());
    }
}
