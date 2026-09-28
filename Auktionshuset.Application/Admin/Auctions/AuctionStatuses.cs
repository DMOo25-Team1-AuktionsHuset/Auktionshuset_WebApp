namespace Auktionshuset.Application.Admin.Auctions;

/// <summary>
/// The status values an auction can have. The status is derived from the auction's time span.
/// </summary>
public static class AuctionStatuses
{
    public const string Upcoming = "Kommende";
    public const string Live = "Live";
    public const string Ended = "Afsluttet";

    /// <summary>
    /// Derives the status of an auction from its start and optional end time.
    /// </summary>
    /// <param name="startsAt">The moment the auction starts.</param>
    /// <param name="endsAt">The moment the auction ends, or <see langword="null"/> if it has no end time.</param>
    /// <param name="now">The current moment used for the comparison.</param>
    /// <returns><see cref="Upcoming"/>, <see cref="Live"/> or <see cref="Ended"/>.</returns>
    public static string Derive(DateTime startsAt, DateTime? endsAt, DateTime now)
    {
        if (now < startsAt)
        {
            return Upcoming;
        }

        return endsAt is null || now < endsAt.Value ? Live : Ended;
    }

    /// <summary>
    /// Gets every status value, in the order they are offered as filters in the UI.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Upcoming, Live, Ended];
}
