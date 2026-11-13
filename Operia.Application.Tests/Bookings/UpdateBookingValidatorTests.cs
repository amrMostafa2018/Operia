using FluentAssertions;
using Operia.Application.Bookings.Commands.CreateBooking;
using Operia.Application.Bookings.Commands.UpdateBooking;
using Operia.SharedKernel.Errors;
using Xunit;

namespace Operia.Application.Tests.Bookings;

public sealed class UpdateBookingValidatorTests
{
    private readonly UpdateBookingValidator _validator = new();

    [Fact]
    public void UpdateBooking_EmptyItems_FailsWithItemsRequired()
    {
        var result = _validator.Validate(new UpdateBookingCommand("booking-1", "version-1", []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorCode == ApiErrorCodes.Bookings.ItemsRequired);
        ApiErrorCatalog.GetMessage(ApiErrorCodes.Bookings.ItemsRequired, "en")
            .Should().Be("Choose at least one service before saving changes.");
        ApiErrorCatalog.GetMessage(ApiErrorCodes.Bookings.ItemsRequired, "ar")
            .Should().Be("اختر خدمة واحدة على الأقل قبل حفظ التعديلات.");
    }

    [Fact]
    public void UpdateBooking_WithItem_DoesNotFailItemsRequired()
    {
        var result = _validator.Validate(
            new UpdateBookingCommand(
                "booking-1",
                "version-1",
                [new CreateBookingItemInput("package-1", null, 1)]));

        result.Errors.Should().NotContain(error => error.ErrorCode == ApiErrorCodes.Bookings.ItemsRequired);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateBooking_UnlistedSnapshot_RequiresNamePriceAndDuration()
    {
        var result = _validator.Validate(
            new UpdateBookingCommand(
                "booking-1",
                "version-1",
                [new CreateBookingItemInput("", null, 1, "unlisted")]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorCode == ApiErrorCodes.Sales.UnlistedNameInvalid);
        result.Errors.Should().Contain(error => error.ErrorCode == ApiErrorCodes.Sales.UnlistedPriceInvalid);
        result.Errors.Should().Contain(error => error.ErrorCode == ApiErrorCodes.Sales.UnlistedDurationInvalid);
    }

    [Fact]
    public void UpdateBooking_SecondUnlistedSnapshot_FailsWithUnlistedLimit()
    {
        var result = _validator.Validate(
            new UpdateBookingCommand(
                "booking-1",
                "version-1",
                [
                    new CreateBookingItemInput("", null, 1, "unlisted", null, "Peel", 80m, 30),
                    new CreateBookingItemInput("", null, 1, "unlisted", null, "Mask", 40m, 20)
                ]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.ErrorCode == ApiErrorCodes.Sales.UnlistedLimit);
    }
}
