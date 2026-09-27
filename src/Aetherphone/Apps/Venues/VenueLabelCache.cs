using Aetherphone.Core.Localization;
using Aetherphone.Core.Venues;

namespace Aetherphone.Apps.Venues;

internal readonly record struct VenueCardText(VenueStatus Status, string Meta, string Stat, string Initial,
    string PressId);

internal sealed class VenueLabelCache
{
    private const int MaxPlusLabel = 99;

    private static readonly string[] PlusLabels = BuildPlusLabels();

    private readonly List<VenueCardText> feed = new();
    private readonly List<VenueCardText> rail = new();
    private LanguageInfo? language;
    private int formatVersion = -1;

    public string RailHeading { get; private set; } = string.Empty;
    public string FeedHeading { get; private set; } = string.Empty;

    public VenueCardText Feed(int index) => feed[index];

    public VenueCardText Rail(int index) => rail[index];

    public static string Plus(int count) => PlusLabels[Math.Clamp(count, 1, MaxPlusLabel)];

    public bool LanguageChanged()
    {
        if (ReferenceEquals(language, Loc.Current) && formatVersion == TimeText.FormatVersion)
        {
            return false;
        }

        language = Loc.Current;
        formatVersion = TimeText.FormatVersion;
        return true;
    }

    public void Rebuild(IReadOnlyList<VenueEvent> feedVenues, IReadOnlyList<VenueEvent> railVenues, int liveCount,
        DateTime nowUtc)
    {
        Fill(feed, feedVenues, nowUtc, "venues.card.");
        Fill(rail, railVenues, nowUtc, "venues.rail.");
        var liveHeading = Loc.Upper(Loc.T(L.Venues.LiveNowLabel));
        RailHeading = liveCount > 0 ? $"{liveHeading} · {liveCount.ToString(Loc.Culture)}" : liveHeading;
        FeedHeading = Loc.Culture.TextInfo.ToUpper(
            Loc.T(L.Venues.VenueCount, feedVenues.Count.ToString("N0", Loc.Culture)));
    }

    private static void Fill(List<VenueCardText> into, IReadOnlyList<VenueEvent> venues, DateTime nowUtc,
        string pressPrefix)
    {
        into.Clear();
        for (var index = 0; index < venues.Count; index++)
        {
            var venue = venues[index];
            into.Add(new VenueCardText(VenueFormat.Status(venue, nowUtc), VenueFormat.Meta(venue),
                StatOf(venue, nowUtc), InitialOf(venue.Title), pressPrefix + venue.Id));
        }
    }

    private static string StatOf(VenueEvent venue, DateTime nowUtc)
    {
        if (venue.LiveViewers > 0 && venue.IsConfirmedLive(nowUtc))
        {
            return venue.LiveViewers.ToString("N0", Loc.Culture);
        }

        return venue.AttendeeCount > 0 ? venue.AttendeeCount.ToString("N0", Loc.Culture) : string.Empty;
    }

    public static string InitialOf(string title)
    {
        for (var index = 0; index < title.Length; index++)
        {
            if (char.IsLetterOrDigit(title[index]))
            {
                return char.ToUpperInvariant(title[index]).ToString();
            }
        }

        return "?";
    }

    private static string[] BuildPlusLabels()
    {
        var labels = new string[MaxPlusLabel + 1];
        for (var index = 0; index <= MaxPlusLabel; index++)
        {
            labels[index] = "+" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return labels;
    }
}
