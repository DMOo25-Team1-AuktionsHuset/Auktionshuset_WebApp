using Auktionshuset.Application.Billing;
using Xunit;

namespace Auktionshuset.Tests.Auctions;

public sealed class InvoiceAmountsTests
{
    [Theory]
    [InlineData("1000", "100", "200", "20", "1100")]
    [InlineData("0.05", "0.01", "0.01", "0", "0.06")]
    [InlineData("0.01", "0", "0", "0", "0.01")]
    [InlineData("9999999999999999.99", "1000000000000000", "2000000000000000", "200000000000000", "10999999999999999.99")]
    public void InclusiveVatAndPremiumAreCalculatedOnceForTheWholeLine(string hammer, string premium, string hammerVat, string premiumVat, string total)
    {
        decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var actual = InvoiceAmounts.Calculate(Parse(hammer), 10m, 25m);
        Assert.Equal(Parse(premium), actual.Premium);
        Assert.Equal(Parse(hammerVat), actual.HammerVat);
        Assert.Equal(Parse(premiumVat), actual.PremiumVat);
        Assert.Equal(Parse(total), actual.Total);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    public void InvalidWinningAmountsAreRejected(decimal hammer) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => InvoiceAmounts.Calculate(hammer, 10m, 25m));
}
