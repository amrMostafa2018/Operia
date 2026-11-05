using MediatR;
using Operia.Application.Customers.Queries.ListCustomerOperations;

namespace Operia.Application.Customers.Queries.ExportCustomerOperations;

public sealed class ExportCustomerOperationsHandler(CustomerOperationsReader reader)
    : IRequestHandler<ExportCustomerOperationsQuery, byte[]>
{
    public async Task<byte[]> Handle(
        ExportCustomerOperationsQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await reader.ReadAsync(request.Filter, cancellationToken);
        return CustomerOperationsWorkbook.Write(rows);
    }
}
