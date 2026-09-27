namespace Aetherphone.Core.Venues;

internal readonly record struct VenueQueryKey(
    int DataVersion,
    VenueTimeFilter Time,
    int Source,
    string DataCenter,
    bool FavoritesOnly,
    int FavoritesStamp,
    int TagsStamp,
    string Search,
    long Minute);

internal sealed class VenueQuery
{
    public const int MaxRail = 12;

    private readonly List<VenueEvent> feed = new();
    private readonly List<VenueEvent> rail = new();
    private readonly FeedOrder feedOrder = new();
    private readonly RailOrder railOrder = new();
    private VenueQueryKey key;
    private bool built;
    private int liveCount;

    public IReadOnlyList<VenueEvent> Feed => feed;
    public IReadOnlyList<VenueEvent> Rail => rail;
    public int LiveCount => liveCount;
    public int Revision { get; private set; }

    public void Invalidate() => built = false;

    public bool Update(in VenueQueryKey wanted, IReadOnlyList<VenueEvent> source, IReadOnlyList<string> favorites,
        IReadOnlyList<string> selectedTags, DateTime nowUtc)
    {
        if (built && key == wanted)
        {
            return false;
        }

        key = wanted;
        built = true;
        Revision++;
        feed.Clear();
        rail.Clear();
        liveCount = 0;
        var query = wanted.Search.Trim();
        var today = DateTime.Now.Date;
        for (var index = 0; index < source.Count; index++)
        {
            var venue = source[index];
            if (!VenueFilter.MatchesSource(venue, wanted.Source) ||
                !VenueFilter.MatchesDataCenter(venue, wanted.DataCenter) ||
                !VenueFilter.MatchesTags(venue, selectedTags))
            {
                continue;
            }

            var live = venue.IsLive(nowUtc);
            if (live)
            {
                liveCount++;
                rail.Add(venue);
            }

            if (wanted.FavoritesOnly && !VenueFilter.Contains(favorites, venue.Id))
            {
                continue;
            }

            if (!VenueFilter.MatchesTime(venue, wanted.Time, live, nowUtc, today))
            {
                continue;
            }

            if (query.Length > 0 && !VenueFilter.MatchesSearch(venue, query))
            {
                continue;
            }

            feed.Add(venue);
        }

        railOrder.Now = nowUtc;
        rail.Sort(railOrder);
        if (rail.Count > MaxRail)
        {
            rail.RemoveRange(MaxRail, rail.Count - MaxRail);
        }

        feedOrder.Now = nowUtc;
        feedOrder.Time = wanted.Time;
        feed.Sort(feedOrder);
        return true;
    }

    private sealed class RailOrder : IComparer<VenueEvent>
    {
        public DateTime Now;

        public int Compare(VenueEvent? left, VenueEvent? right)
        {
            var byState = VenueFilter.CompareLiveState(left!, right!, Now);
            if (byState != 0)
            {
                return byState;
            }

            var byViewers = right!.LiveViewers.CompareTo(left!.LiveViewers);
            return byViewers != 0 ? byViewers : string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class FeedOrder : IComparer<VenueEvent>
    {
        public DateTime Now;
        public VenueTimeFilter Time;

        public int Compare(VenueEvent? left, VenueEvent? right)
        {
            if (Time == VenueTimeFilter.All)
            {
                return string.Compare(left!.Title, right!.Title, StringComparison.OrdinalIgnoreCase);
            }

            var byState = VenueFilter.CompareLiveState(left!, right!, Now);
            if (byState != 0)
            {
                return byState;
            }

            if (Time == VenueTimeFilter.LiveNow)
            {
                var byViewers = right!.LiveViewers.CompareTo(left!.LiveViewers);
                if (byViewers != 0)
                {
                    return byViewers;
                }
            }

            var leftStart = left!.StartUtc ?? DateTime.MaxValue;
            var rightStart = right!.StartUtc ?? DateTime.MaxValue;
            var byStart = leftStart.CompareTo(rightStart);
            return byStart != 0 ? byStart : string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
        }
    }
}

internal static class VenueFilter
{
    public const int SourceAll = 0;
    public const int SourceFfxiv = 1;
    public const int SourcePartake = 2;
    public const int SourceRolladeck = 3;
    public const int SourceCount = 4;
    private const int UpcomingWindowDays = 7;

    public static void CollectTags(IReadOnlyList<VenueEvent> source, int sourceFilter, string dataCenter,
        SortedSet<string> into)
    {
        into.Clear();
        for (var index = 0; index < source.Count; index++)
        {
            var venue = source[index];
            if (!MatchesSource(venue, sourceFilter) || !MatchesDataCenter(venue, dataCenter))
            {
                continue;
            }

            for (var tagIndex = 0; tagIndex < venue.Tags.Count; tagIndex++)
            {
                into.Add(venue.Tags[tagIndex]);
            }
        }
    }

    public static int CompareLiveState(VenueEvent left, VenueEvent right, DateTime nowUtc) =>
        ((int)right.LiveState(nowUtc)).CompareTo((int)left.LiveState(nowUtc));

    public static bool MatchesSource(VenueEvent venue, int sourceFilter)
    {
        return sourceFilter switch
        {
            SourceFfxiv => (venue.Sources & VenueSources.FfxivVenues) != 0,
            SourcePartake => (venue.Sources & VenueSources.Partake) != 0,
            SourceRolladeck => (venue.Sources & VenueSources.Rolladeck) != 0,
            _ => true,
        };
    }

    public static bool MatchesDataCenter(VenueEvent venue, string dataCenter) =>
        dataCenter.Length == 0 || string.Equals(venue.DataCenter, dataCenter, StringComparison.OrdinalIgnoreCase);

    public static bool MatchesTime(VenueEvent venue, VenueTimeFilter time, bool live, DateTime nowUtc,
        DateTime localToday)
    {
        switch (time)
        {
            case VenueTimeFilter.LiveNow:
                return live;
            case VenueTimeFilter.Today:
                return live || OpensOn(venue, localToday);
            case VenueTimeFilter.Upcoming:
                return live || (venue.StartUtc is { } start && start > nowUtc &&
                                start <= nowUtc.AddDays(UpcomingWindowDays));
            default:
                return true;
        }
    }

    public static bool MatchesTags(VenueEvent venue, IReadOnlyList<string> selectedTags)
    {
        for (var index = 0; index < selectedTags.Count; index++)
        {
            if (!Contains(venue.Tags, selectedTags[index]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool MatchesSearch(VenueEvent venue, string query)
    {
        if (Found(venue.Title, query) || Found(venue.Host, query) || Found(venue.PlaceLine, query) ||
            Found(venue.DataCenter, query) || Found(venue.LiveHeadline, query))
        {
            return true;
        }

        for (var index = 0; index < venue.Tags.Count; index++)
        {
            if (Found(venue.Tags[index], query))
            {
                return true;
            }
        }

        return false;
    }

    public static bool Contains(IReadOnlyList<string> values, string target)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool OpensOn(VenueEvent venue, DateTime localDay)
    {
        if (venue.StartUtc is not { } start)
        {
            return false;
        }

        var startDate = start.ToLocalTime().Date;
        if (startDate == localDay)
        {
            return true;
        }

        return venue.EndUtc is { } end && startDate <= localDay && end.ToLocalTime().Date >= localDay;
    }

    private static bool Found(string value, string query) =>
        value.Length > 0 && value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
