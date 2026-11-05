using MediatR;

namespace Operia.Application.Bookings.Queries.SearchBookingCustomers;

public sealed record SearchBookingCustomersQuery(string Term)
    : IRequest<IReadOnlyList<BookingCustomerSearchResult>>;

public sealed record BookingCustomerSearchResult(string Id, string FullName, string MobileNumber);
