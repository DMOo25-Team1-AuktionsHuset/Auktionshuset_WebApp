using Auktionshuset.Domain;
using Xunit;

namespace Auktionshuset.Tests.Device;

public class DeviceEncoderRulesTests
{
    [Fact]
    public void ReferenceUsesHighestBidOrPositiveStartingPrice()
    {
        Assert.Equal(100.25m, DeviceEncoderRules.Reference(50.50m, 100.25m));
        Assert.Equal(50.50m, DeviceEncoderRules.Reference(50.50m, null));
        Assert.Null(DeviceEncoderRules.Reference(0m, null));
    }

    [Theory]
    [InlineData(0.01, 1)]
    [InlineData(149.99, 1)]
    [InlineData(150, 2)]
    [InlineData(1250.50, 13)]
    public void StepRoundsOnePercentToWholeKroner(decimal reference, decimal expected) =>
        Assert.Equal(expected, DeviceEncoderRules.Step(reference));

    [Fact]
    public void TurningPreservesOreAndClampsAtReference()
    {
        Assert.Equal(102.25m, DeviceEncoderRules.Move(100.25m, 100.25m, 2));
        Assert.Equal(100.25m, DeviceEncoderRules.Move(102.25m, 100.25m, -10));
    }

    [Fact]
    public void HigherBidRaisesDraftButNeverLowersIt()
    {
        Assert.Equal(105.75m, DeviceEncoderRules.OnHighestBid(100.25m, 105.75m));
        Assert.Equal(110.25m, DeviceEncoderRules.OnHighestBid(110.25m, 105.75m));
    }

    [Theory]

    [InlineData(100.25, 100.25, 110)]
    [InlineData(0.01, 0.01, 2)]
    [InlineData(1, 1, 2)]
    public void PressRoundsAndExceedsExistingBidByAtLeastOneKrone(decimal reference, decimal highest, decimal expected) =>
        Assert.Equal(expected, DeviceEncoderRules.Press(reference, highest));

    [Fact]
    public void PressWithoutExistingBidRoundsReference() =>
        Assert.Equal(116m, DeviceEncoderRules.Press(105m, null));

    [Fact]
    public void RejectsAmountsWithFractionsOfOre() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DeviceEncoderRules.Reference(1.001m, null));
}
