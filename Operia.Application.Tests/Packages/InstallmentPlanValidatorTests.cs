using FluentAssertions;
using Operia.Application.Packages.Commands.CreatePackage;
using Operia.Application.Packages.DTOs;
using Operia.SharedKernel.Errors;
using Xunit;

namespace Operia.Application.Tests.Packages;

public sealed class InstallmentPlanValidatorTests
{
    private readonly CreatePackageValidator _validator = new();

    [Fact]
    public async Task BalancedPlan_WithUnequalAmounts_IsAccepted()
    {
        var command = Plan() with
        {
            Installments = [
                new PackageInstallmentInput(4000m, 3),
                new PackageInstallmentInput(6000m, 4),
                new PackageInstallmentInput(5000m, 3)
            ]
        };

        (await _validator.ValidateAsync(command)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UnbalancedAmounts_AreRejected()
    {
        var command = Plan() with
        {
            Installments = [
                new PackageInstallmentInput(5000m, 3),
                new PackageInstallmentInput(5000m, 4),
                new PackageInstallmentInput(4000m, 3)
            ]
        };

        var errors = (await _validator.ValidateAsync(command)).Errors;
        errors.Should().Contain(error => error.ErrorCode ==
            ApiErrorCodes.Packages.PackageInstallmentTotalMismatch);
    }

    [Fact]
    public async Task PlanThatDoesNotReleaseEverySession_IsRejected()
    {
        var command = Plan() with
        {
            Installments = [
                new PackageInstallmentInput(5000m, 2),
                new PackageInstallmentInput(5000m, 2),
                new PackageInstallmentInput(5000m, 2)
            ]
        };

        var errors = (await _validator.ValidateAsync(command)).Errors;
        errors.Should().Contain(error => error.ErrorCode ==
            ApiErrorCodes.Packages.PackageInstallmentSessionsInvalid);
    }

    [Fact]
    public async Task TenSessions_CannotReleaseEightBeforeFullPayment()
    {
        var command = Plan() with
        {
            Installments = [
                new PackageInstallmentInput(5000m, 4),
                new PackageInstallmentInput(5000m, 4),
                new PackageInstallmentInput(5000m, 2)
            ]
        };

        var errors = (await _validator.ValidateAsync(command)).Errors;
        errors.Should().Contain(error => error.ErrorCode ==
            ApiErrorCodes.Packages.PackageInstallmentReleaseCap);
    }

    [Fact]
    public async Task FifteenSessions_ExactHalfRoundsDownToTen()
    {
        var command = Plan() with
        {
            SessionCount = 15,
            Installments = [
                new PackageInstallmentInput(5000m, 5),
                new PackageInstallmentInput(5000m, 6),
                new PackageInstallmentInput(5000m, 4)
            ]
        };

        var errors = (await _validator.ValidateAsync(command)).Errors;
        errors.Should().Contain(error => error.ErrorCode ==
            ApiErrorCodes.Packages.PackageInstallmentReleaseCap);

        var valid = command with
        {
            Installments = [
                new PackageInstallmentInput(5000m, 5),
                new PackageInstallmentInput(5000m, 5),
                new PackageInstallmentInput(5000m, 5)
            ]
        };
        (await _validator.ValidateAsync(valid)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SingleSession_CannotCarryInstallmentPlan()
    {
        var command = Plan() with { OfferType = "singleSession", SessionCount = null };

        var errors = (await _validator.ValidateAsync(command)).Errors;
        errors.Should().Contain(error => error.ErrorCode ==
            ApiErrorCodes.Packages.PackageInstallmentOnlyPackage);
    }

    [Fact]
    public async Task PulsePackage_CannotCarrySessionReleasePlan()
    {
        var command = Plan() with
        {
            SessionCount = null,
            PulseCount = 1000,
            Installments = [
                new PackageInstallmentInput(5000m, 0),
                new PackageInstallmentInput(5000m, 0),
                new PackageInstallmentInput(5000m, 0)
            ]
        };

        var errors = (await _validator.ValidateAsync(command)).Errors;
        errors.Should().Contain(error => error.ErrorCode ==
            ApiErrorCodes.Packages.PackageInstallmentSessionsInvalid);
    }

    private static CreatePackageCommand Plan() => new(
        "Ten sessions", true, "package", "", "category-1", null,
        30, 10, null, 12, 18000m, "", null,
        true, true, 20000m, 5000m, 3,
        [
            new PackageInstallmentInput(5000m, 3),
            new PackageInstallmentInput(5000m, 4),
            new PackageInstallmentInput(5000m, 3)
        ]);
}
