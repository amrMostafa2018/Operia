using MediatR;
using Operia.Application.Customers.Queries.ListCustomerOperations;

namespace Operia.Application.Customers.Queries.ExportCustomerOperations;

public sealed record ExportCustomerOperationsQuery(CustomerOperationsFilter Filter) : IRequest<byte[]>;
