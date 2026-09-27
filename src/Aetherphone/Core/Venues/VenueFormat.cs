using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Venues;

internal enum VenueStatusKind : byte
{
    None,
    Upcoming,
    Open,
    Live,
}

internal readonly record struct VenueStatus(VenueStatusKind Kind, string Label);

internal static class VenueFormat
{
    public static VenueStatus Status(VenueEvent venue, DateTime nowUtc)
    {
        var ends = EndsAt(venue, nowUtc);
        if (venue.IsConfirmedLive(nowUtc))
        {
            var headline = venue.LiveHeadline.Length > 0 ? venue.LiveHeadline : Loc.T(L.Venues.LiveNowLabel);
            return new VenueStatus(VenueStatusKind.Live,
                ends.Length > 0 ? $"{headline} · {Loc.T(L.Venues.UntilTime, ends)}" : headline);
        }

        if (venue.IsScheduledOpen(nowUtc))
        {
            return new VenueStatus(VenueStatusKind.Open,
                ends.Length > 0 ? Loc.T(L.Venues.OpenUntil, ends) : Loc.T(L.Venues.OpenNow));
        }

        if (venue.StartUtc is not { } start || start <= nowUtc)
        {
            return new VenueStatus(VenueStatusKind.None, string.Empty);
        }

        var moment = TimeText.FutureMoment(new DateTimeOffset(start).ToUnixTimeSeconds());
        var label = (venue.Sources & VenueSources.Partake) != 0
            ? Loc.T(L.Venues.StartsAt, moment)
            : Loc.T(L.Venues.OpensAt, moment);
        return new VenueStatus(VenueStatusKind.Upcoming, label);
    }

    public static string Window(VenueEvent venue)
    {
        if (venue.StartUtc is not { } start)
        {
            return string.Empty;
        }

        var startText = TimeText.FutureMoment(new DateTimeOffset(start).ToUnixTimeSeconds());
        return venue.EndUtc is { } end ? $"{startText} – {TimeText.Clock(end.ToLocalTime())}" : startText;
    }

    public static string Meta(VenueEvent venue)
    {
        var world = venue.World.Length > 0 && venue.DataCenter.Length > 0
            ? $"{venue.World} ({venue.DataCenter})"
            : venue.World.Length > 0 ? venue.World : venue.DataCenter;
        return VenueMapper.BuildPlaceLine(world, venue.LocationLine);
    }

    public static string Viewers(int count) => Loc.T(L.Venues.Watching, count.ToString("N0", Loc.Culture));

    private static string EndsAt(VenueEvent venue, DateTime nowUtc) =>
        venue.EndUtc is { } end && end > nowUtc ? TimeText.Clock(end.ToLocalTime()) : string.Empty;
}
