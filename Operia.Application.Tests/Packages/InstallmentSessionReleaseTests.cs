using FluentAssertions;
using Operia.Application.Packages;
using Xunit;

namespace Operia.Application.Tests.Packages;

public sealed class InstallmentSessionReleaseTests
{
    [Fact]
    public void FailedAndRepeatedPayments_DoNotReleaseAdditionalSessions()
    {
        InstallmentSessionRelease.AvailableAfterPayment(10, 3, 7, false, false)
            .Should().Be(3);
        InstallmentSessionRelease.AvailableAfterPayment(10, 7, 7, true, false)
            .Should().Be(7);
    }

    [Fact]
    public void FullPayment_ReleasesTheRemainderOnce()
    {
        var released = InstallmentSessionRelease.AvailableAfterPayment(10, 7, 7, true, true);
        released.Should().Be(10);
        InstallmentSessionRelease.AvailableAfterPayment(10, released, 7, true, true)
            .Should().Be(10);
    }

    [Fact]
    public void HalfSessionLimit_RoundsDown()
    {
        InstallmentSessionRelease.AvailableAfterPayment(15, 0, 11, true, false)
            .Should().Be(10);
    }

    [Fact]
    public void OneSessionPackage_KeepsItsOnlySessionLockedUntilFullPayment()
    {
        InstallmentSessionRelease.AvailableAfterPayment(1, 0, 1, true, false)
            .Should().Be(0);
    }
}
