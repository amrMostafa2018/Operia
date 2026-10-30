using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Operia.Application.Auth;
using Operia.Application.Bookings.Queries.GetBookingHistory;
using Operia.Application.Bookings.Queries.GetCalendarBookings;
using Operia.Application.Bookings.Queries.FindBookingCustomer;
using Operia.Application.Bookings.Queries.ListBookings;
using Operia.Application.Bookings.Commands.CreateBooking;
using Operia.Application.Bookings.Commands.CancelBooking;
using Operia.Application.Bookings.Commands.CloseBooking;
using Operia.Application.Bookings.Commands.UpdateBooking;
using Operia.Application.Bookings.Queries.ExportBookings;
using Operia.Application.Bookings.Queries.GetBookingPaymentMethods;
using System.Text;

namespace Operia.Controllers;

/// <summary>Exposes policy-protected booking commands and queries through HTTP.</summary>
[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(IMediator mediator) : ControllerBase
{
    /// <summary>Creates a booking and returns its persisted identity and version.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.BookingsManage)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await mediator.Send(command, cancellationToken);
        return Created($"/api/bookings/{booking.Id}", booking);
    }

    /// <summary>Cancels a Booked appointment using its client-supplied row version.</summary>
    [HttpPost("{id}/cancel")]
    [Authorize(Policy = Policies.BookingsCancel)]
    public Task<CancelBookingResult> Cancel(
        string id,
        CancelBookingRequest request,
        CancellationToken cancellationToken) =>
        mediator.Send(new CancelBookingCommand(id, request.Version), cancellationToken);

    /// <summary>Closes a Booked appointment, finalises per-item usage, and marks it Completed.</summary>
    [HttpPost("{id}/close")]
    [Authorize(Policy = Policies.BookingsManage)]
    public Task<CloseBookingResult> Close(
        string id,
        CloseBookingRequest request,
        CancellationToken cancellationToken) =>
        mediator.Send(new CloseBookingCommand(id, request.Version, request.Items), cancellationToken);

    /// <summary>Updates booking items or payment choice without changing its Package session.</summary>
    [HttpPut("{id}")]
    [Authorize(Policy = Policies.BookingsManage)]
    public Task<UpdateBookingResult> Update(string id, UpdateBookingRequest request, CancellationToken cancellationToken) =>
        mediator.Send(new UpdateBookingCommand(id, request.Version, request.Items, request.PaymentMethod), cancellationToken);

    /// <summary>Finds a registered customer and eligible owned purchases by mobile number.</summary>
    [HttpGet("customers/by-mobile")]
    [Authorize(Policy = Policies.BookingsManage)]
    public Task<BookingCustomerDto?> FindCustomer(
        [FromQuery] string mobile,
        [FromQuery] string? bookingId,
        CancellationToken cancellationToken) =>
        mediator.Send(new FindBookingCustomerQuery(mobile, bookingId), cancellationToken);

    /// <summary>Lists enabled payment identifiers without exposing account settings.</summary>
    [HttpGet("payment-methods")]
    [Authorize(Policy = Policies.BookingsRead)]
    public Task<IReadOnlyList<string>> PaymentMethods(CancellationToken cancellationToken) =>
        mediator.Send(new GetBookingPaymentMethodsQuery(), cancellationToken);

    /// <summary>Returns branch-scoped bookings and active holds for a calendar date range.</summary>
    [HttpGet("calendar")]
    [Authorize(Policy = Policies.BookingsRead)]
    public Task<CalendarBookingsResult> Calendar(
        [FromQuery] string branchId,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] string? employeeId = null,
        CancellationToken cancellationToken = default) =>
        mediator.Send(new GetCalendarBookingsQuery(branchId, fromDate, toDate, employeeId), cancellationToken);

    /// <summary>Returns a filtered page and summary of bookings visible to the caller.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.BookingsRead)]
    public Task<BookingListResult> List(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? customerMobile = null,
        [FromQuery] string? customerName = null,
        [FromQuery] string? branchId = null,
        [FromQuery] string? employeeId = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default) =>
        mediator.Send(
            new ListBookingsQuery(fromDate, toDate, pageNumber, pageSize, search, customerMobile, customerName, branchId, employeeId, status),
            cancellationToken);

    /// <summary>Exports the caller's filtered bookings as UTF-8 CSV.</summary>
    [HttpGet("export")]
    [Authorize(Policy = Policies.BookingsExport)]
    public async Task<FileContentResult> Export(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] string? customerMobile = null,
        [FromQuery] string? customerName = null,
        [FromQuery] string? employeeId = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var csv = await mediator.Send(new ExportBookingsQuery(fromDate, toDate, customerMobile, customerName, employeeId, status), cancellationToken);
        return File(
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(csv),
            "text/csv; charset=utf-8",
            $"bookings-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv");
    }

    /// <summary>Returns the recorded edits for an accessible booking.</summary>
    [HttpGet("{id}/history")]
    [Authorize(Policy = Policies.BookingsRead)]
    public Task<IReadOnlyList<BookingHistoryDto>> History(string id, CancellationToken cancellationToken) =>
        mediator.Send(new GetBookingHistoryQuery(id), cancellationToken);
}
