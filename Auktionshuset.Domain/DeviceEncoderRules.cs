namespace Auktionshuset.Domain;

/// <summary>Device draft calculations only; these are not web bid validation rules.</summary>
public static class DeviceEncoderRules
{
    public static decimal? Reference(decimal startingPrice, decimal? highestBid)
    {
        Validate(startingPrice);
        if (highestBid is not null)
        {
            Validate(highestBid.Value);
            if (highestBid <= 0) throw new ArgumentOutOfRangeException(nameof(highestBid));
            return highestBid;
        }

        return startingPrice > 0 ? startingPrice : null;
    }

    public static decimal Step(decimal reference)
    {
        ValidateReference(reference);
        return Math.Max(1m, RoundKroner(reference * 0.01m));
    }

    public static decimal Move(decimal draft, decimal reference, int detents)
    {
        Validate(draft);
        ValidateReference(reference);
        return Math.Max(reference, draft + Step(reference) * detents);
    }

    public static decimal OnHighestBid(decimal draft, decimal highestBid)
    {
        Validate(draft);
        ValidateReference(highestBid);
        return Math.Max(draft, highestBid);
    }

    public static decimal Press(decimal reference, decimal? highestBid)
    {
        ValidateReference(reference);
        if (highestBid is not null) ValidateReference(highestBid.Value);
        var amount = RoundKroner(reference * 1.10m);
        return highestBid is not null && amount <= highestBid.Value
            ? decimal.Ceiling(highestBid.Value + 1m)
            : amount;
    }

    private static decimal RoundKroner(decimal amount) => decimal.Round(amount, 0, MidpointRounding.AwayFromZero);

    private static void ValidateReference(decimal amount)
    {
        Validate(amount);
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
    }

    private static void Validate(decimal amount)
    {
        if (amount < 0 || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative and have at most øre precision.");
    }
}
