namespace Auktionshuset.Application.Billing;

public sealed record InvoiceAmounts(decimal HammerPrice, decimal Premium, decimal HammerVat,
    decimal PremiumVat, decimal Total)
{
    public static InvoiceAmounts Calculate(decimal wholeLineHammerPrice, decimal premiumPercent, decimal vatPercent)
    {
        if (wholeLineHammerPrice <= 0 || wholeLineHammerPrice > 9999999999999999.99m
            || decimal.Round(wholeLineHammerPrice, 2) != wholeLineHammerPrice
            || premiumPercent < 0 || premiumPercent > 100 || vatPercent < 0 || vatPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(wholeLineHammerPrice));
        var premium = Round(wholeLineHammerPrice * premiumPercent / 100m);
        return new(wholeLineHammerPrice, premium,
            Round(wholeLineHammerPrice * vatPercent / (100m + vatPercent)),
            Round(premium * vatPercent / (100m + vatPercent)), wholeLineHammerPrice + premium);
    }

    private static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
