using FluentAssertions;
using Operia.Application.Bookings.Commands.CreateBooking;
using Operia.Application.Bookings.Queries.SearchBookingCustomers;
using Operia.SharedKernel.Errors;
using Xunit;

namespace Operia.Application.Tests.Bookings;

public sealed class BookingCustomerInputValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public void CustomerSearch_RejectsShortTermsWithBilingualCode(string term)
    {
        var result = new SearchBookingCustomersValidator()
            .Validate(new SearchBookingCustomersQuery(term));

        result.Errors.Should().ContainSingle()
            .Which.ErrorCode.Should().Be(ApiErrorCodes.Bookings.CustomerSearchTermInvalid);
        ApiErrorCatalog.GetMessage(ApiErrorCodes.Bookings.CustomerSearchTermInvalid, "en")
            .Should().NotBeNullOrWhiteSpace();
        ApiErrorCatalog.GetMessage(ApiErrorCodes.Bookings.CustomerSearchTermInvalid, "ar")
            .Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CreateBooking_RequiresRegisteredCustomerUnlessDirectIsSelected()
    {
        var registered = Booking(null, false);
        var direct = Booking(null, true);
        var mixed = Booking("customer-1", true);
        var validator = new CreateBookingValidator();

        validator.Validate(registered).Errors.Should().Contain(error =>
            error.ErrorCode == ApiErrorCodes.Bookings.CustomerSelectionRequired);
        validator.Validate(direct).IsValid.Should().BeTrue();
        validator.Validate(mixed).Errors.Should().Contain(error =>
            error.ErrorCode == ApiErrorCodes.Bookings.DirectCustomerSelectionInvalid);
    }

    private static CreateBookingCommand Booking(string? customerId, bool direct) =>
        new("request-1", customerId, "branch-1", "employee-1", new DateOnly(2026, 10, 28),
            600, 630, [new CreateBookingItemInput("service-1", null, 1)], DirectCustomer: direct);
}
