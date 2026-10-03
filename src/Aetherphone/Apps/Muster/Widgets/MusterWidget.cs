using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Muster.Widgets;

internal sealed class MusterWidget : IHomeWidget
{
    private const string AppKey = MusterStore.AppId;
    private const float RefreshSeconds = 2f;
    private const float WatchSeconds = 1f;
    private const int MaxEntries = 4;
    private const int MediumRows = 2;
    private const float IconDisc = 32f;
    private const float ButtonHeight = 28f;
    private const float ButtonWidth = 74f;
    private const long HourSeconds = 3600;
    private static readonly long[] SampleOffsets = { 25 * 60, 2 * HourSeconds };
    private static readonly int[] SampleCategories = { MusterCategories.TreasureHunt, MusterCategories.Social };
    private static readonly Vector4 LiveColor = AccentRing.Green;

    private readonly struct Row
    {
        public readonly MusterDto? Dto;
        public readonly int Sample;
        public readonly long StartsAt;
        public readonly long EndsAt;
        public readonly int Category;
        public readonly bool Mine;

        public Row(MusterDto? dto, int sample, long startsAt, long endsAt, int category, bool mine)
        {
            Dto = dto;
            Sample = sample;
            StartsAt = startsAt;
            EndsAt = endsAt;
            Category = category;
            Mine = mine;
        }

        public string Title => Dto is { Description.Length: > 0 }
            ? Dto.Description
            : Dto is null ? Loc.T(WidgetSamples.Musters[Sample]) : Loc.T(MusterCategories.Label(Category));

        public string Host => Dto?.HostCharacter ?? WidgetSamples.Names[Sample];

        public string Id => Dto?.Id ?? string.Empty;
    }

    private readonly MusterStore store;
    private readonly List<MusterDto> entries = new();
    private readonly List<Row> rows = new();
    private readonly List<Row> sampleRows = new();
    private readonly Dictionary<string, bool> pendingRsvp = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CachedText> subtitles = new(StringComparer.Ordinal);
    private readonly CachedText[] sampleSubtitles = new CachedText[2];
    private readonly object rsvpLock = new();
    private float sinceRefresh = RefreshSeconds;
    private float sinceWatch = WatchSeconds;
    private MusterDto[]? seenGoing;
    private MusterDto? seenMine;
    private long sampleAnchor;
    private CachedText heroText;
    private CachedText captionText;

    public MusterWidget(MusterStore store)
    {
        this.store = store;
    }

    public string Id => "muster.next";
    public string DisplayName => Loc.T(L.Apps.Muster);
    public string Description => Loc.T(L.WidgetsUtility.MusterDescription);
    public string AppId => AppKey;
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public WidgetRoute Target(in WidgetContext context)
    {
        if (context.Size != WidgetSize.Small || rows.Count == 0)
        {
            return WidgetRoute.App(AppKey);
        }

        return WidgetRoute.To(AppKey, WidgetRouteKind.Muster, rows[0].Id);
    }

    public float Relevance(string config)
    {
        if (!store.IsSignedIn)
        {
            return 0f;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var soonest = SecondsUntilCommitted(now);
        if (soonest < 0)
        {
            return 0f;
        }

        return soonest <= HourSeconds ? 0.9f : soonest <= 3 * HourSeconds ? 0.5f : 0.15f;
    }

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var signedIn = store.IsSignedIn;
        if (signedIn && !context.Preview && context.Opacity > 0f)
        {
            Watch(context.Delta);
        }

        if (signedIn)
        {
            Refresh(context.Delta);
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sample = context.Preview && (!signedIn || rows.Count == 0);
        var source = sample ? Samples(nowUnix) : rows;
        var accent = AppAccents.For(AppKey);
        var content = WidgetMetrics.Content(context);
        if (!sample && !signedIn)
        {
            var top = WidgetChrome.Header(context, ink, AppKey, L.Apps.Muster, accent);
            UtilityWidgetKit.Message(context, ink, new Rect(new Vector2(content.Min.X, top), content.Max),
                FontAwesomeIcon.UserFriends, accent, Loc.T(L.WidgetsUtility.MusterSignIn), string.Empty);
            return;
        }

        if (source.Count == 0)
        {
            var top = WidgetChrome.Header(context, ink, AppKey, L.Apps.Muster, accent);
            var area = new Rect(new Vector2(content.Min.X, top + WidgetMetrics.Gutter * context.Scale), content.Max);
            if (!store.Primed)
            {
                UtilityWidgetKit.RedactedRows(context.DrawList, area, ink,
                    context.Size == WidgetSize.Small ? 2 : MediumRows, context.Size != WidgetSize.Small, context.Scale);
                return;
            }

            UtilityWidgetKit.Message(context, ink, area, FontAwesomeIcon.UserFriends, accent,
                Loc.T(L.WidgetsUtility.NoMeetups), Loc.T(L.WidgetsUtility.NoMeetupsHint));
            return;
        }

        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, source[0], nowUnix, accent);
            return;
        }

        DrawMedium(context, ink, source, nowUnix, accent, sample);
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, in Row row, long nowUnix, Vector4 accent)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var content = WidgetMetrics.Content(context);
        var top = WidgetChrome.Header(context, ink, AppKey, Loc.T(MusterCategories.Label(row.Category)), accent);
        top += WidgetMetrics.Gutter * scale;
        var started = nowUnix >= row.StartsAt;
        var heroStyle = WidgetType.DisplayCompact;
        if (started)
        {
            var live = Loc.T(L.Island.Live);
            UtilityWidgetKit.DrawFitted(drawList, new Vector2(content.Min.X, top), live, ink.Accent(LiveColor),
                WidgetType.Title, content.Width);
            top += UtilityWidgetKit.LineHeightOf(WidgetType.Title);
        }
        else
        {
            var hero = WidgetText.Countdown(ref heroText, TimeSpan.FromSeconds(row.StartsAt - nowUnix));
            WidgetText.Tabular(drawList, new Vector2(content.Min.X, top), hero, ink.Primary, heroStyle);
            top += Typography.Measure(hero, heroStyle).Y;
        }

        var caption = Caption(ref captionText, row, started);
        UtilityWidgetKit.DrawFitted(drawList, new Vector2(content.Min.X, top), caption, ink.Secondary,
            WidgetType.Caption, content.Width);

        var hostHeight = UtilityWidgetKit.LineHeightOf(WidgetType.Caption);
        var titleHeight = UtilityWidgetKit.LineHeightOf(WidgetType.Headline);
        var hostTop = content.Max.Y - hostHeight;
        UtilityWidgetKit.DrawFitted(drawList, new Vector2(content.Min.X, hostTop), row.Host, ink.Tertiary,
            WidgetType.Caption, content.Width);
        var titleLines = UtilityWidgetKit.Clamp(row.Title, WidgetType.Headline, content.Width, 1);
        UtilityWidgetKit.DrawLines(drawList, titleLines, new Vector2(content.Min.X, hostTop - titleHeight),
            ink.Primary, WidgetType.Headline, titleHeight);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, List<Row> source, long nowUnix,
        Vector4 accent, bool sample)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var content = WidgetMetrics.Content(context);
        var top = WidgetChrome.Header(context, ink, AppKey, L.Apps.Muster, accent) + WidgetMetrics.RowGap * scale;
        var count = Math.Min(MediumRows, source.Count);
        var rowHeight = (content.Max.Y - top) / MediumRows;
        var disc = IconDisc * scale;
        var buttonWidth = ButtonWidth * scale;
        var buttonHeight = ButtonHeight * scale;
        for (var index = 0; index < count; index++)
        {
            var row = source[index];
            var rowTop = top + index * rowHeight;
            var rowRect = new Rect(new Vector2(content.Min.X, rowTop), new Vector2(content.Max.X, rowTop + rowHeight));
            var buttonRect = new Rect(new Vector2(rowRect.Max.X - buttonWidth, rowRect.Center.Y - buttonHeight * 0.5f),
                new Vector2(rowRect.Max.X, rowRect.Center.Y + buttonHeight * 0.5f));
            if (row.Dto is not null)
            {
                WidgetControls.Link(context, ink, index * 2,
                    new Rect(rowRect.Min, new Vector2(buttonRect.Min.X - WidgetMetrics.RowGap * scale, rowRect.Max.Y)),
                    WidgetRoute.To(AppKey, WidgetRouteKind.Muster, row.Dto.Id));
            }

            var discCenter = new Vector2(rowRect.Min.X + disc * 0.5f, rowRect.Center.Y);
            drawList.AddCircleFilled(discCenter, disc * 0.5f, ImGui.GetColorU32(ink.Fill), 32);
            ProgressRing.CenterIcon(drawList, discCenter, MusterCategories.Icon(row.Category), ink.Accent(accent),
                disc * 0.46f);

            var textLeft = discCenter.X + disc * 0.5f + WidgetMetrics.Gutter * scale;
            var textWidth = buttonRect.Min.X - WidgetMetrics.Gutter * scale - textLeft;
            var titleHeight = UtilityWidgetKit.LineHeightOf(WidgetType.Headline);
            var subtitleHeight = UtilityWidgetKit.LineHeightOf(WidgetType.Caption);
            var textTop = rowRect.Center.Y - (titleHeight + subtitleHeight) * 0.5f;
            UtilityWidgetKit.DrawFitted(drawList, new Vector2(textLeft, textTop), row.Title, ink.Primary,
                WidgetType.Headline, textWidth);
            var started = nowUnix >= row.StartsAt;
            var subtitle = sample
                ? Subtitle(ref sampleSubtitles[Math.Clamp(row.Sample, 0, 1)], row, nowUnix)
                : Subtitle(ref SubtitleCache(row.Id), row, nowUnix);
            UtilityWidgetKit.DrawFitted(drawList, new Vector2(textLeft, textTop + titleHeight), subtitle,
                started ? ink.Accent(LiveColor) : ink.Secondary, WidgetType.Caption, textWidth);
            DrawAction(context, ink, index, row, buttonRect, accent);
            if (index < count - 1)
            {
                drawList.AddLine(new Vector2(textLeft, rowRect.Max.Y), new Vector2(rowRect.Max.X, rowRect.Max.Y),
                    ImGui.GetColorU32(ink.Separator), MathF.Max(1f, 0.5f * scale));
            }
        }
    }

    private void DrawAction(in WidgetContext context, in WidgetInk ink, int index, in Row row, Rect buttonRect,
        Vector4 accent)
    {
        if (row.Mine)
        {
            var label = WidgetText.Upper(L.Island.Hosting);
            UtilityWidgetKit.Pill(context.DrawList, new Vector2(buttonRect.Max.X, buttonRect.Center.Y), label,
                ink.Fill, ink.Accent(accent), context.Scale);
            return;
        }

        var going = IsGoing(row);
        var fired = WidgetControls.Button(context, ink, index * 2 + 1, buttonRect,
            going ? FontAwesomeIcon.Check : FontAwesomeIcon.Plus,
            Loc.T(going ? L.WidgetsUtility.Going : L.WidgetsUtility.Join), going ? default : accent);
        if (!fired || row.Dto is null)
        {
            return;
        }

        var wanted = !going;
        var id = row.Dto.Id;
        lock (rsvpLock)
        {
            pendingRsvp[id] = wanted;
        }

        store.SetRsvp(id, wanted, _ =>
        {
            lock (rsvpLock)
            {
                pendingRsvp.Remove(id);
            }
        });
    }

    private bool IsGoing(in Row row)
    {
        if (row.Dto is null)
        {
            return row.Sample == 0;
        }

        lock (rsvpLock)
        {
            if (pendingRsvp.TryGetValue(row.Dto.Id, out var pending))
            {
                return pending;
            }
        }

        return store.IsGoing(row.Dto.Id);
    }

    private void Watch(float delta)
    {
        sinceWatch += delta;
        if (sinceWatch < WatchSeconds)
        {
            return;
        }

        sinceWatch = 0f;
        store.NoteWatched();
        if (!store.DirectoryLoadedOnce && !store.DirectoryLoading && !store.DirectoryFailed)
        {
            store.RefreshDirectory();
        }
    }

    private void Refresh(float delta)
    {
        sinceRefresh += delta;
        var going = store.GoingMusters;
        var mine = store.Mine;
        if (sinceRefresh < RefreshSeconds && ReferenceEquals(going, seenGoing) && ReferenceEquals(mine, seenMine))
        {
            return;
        }

        sinceRefresh = 0f;
        seenGoing = going;
        seenMine = mine;
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        entries.Clear();
        if (mine is not null && mine.EndsAtUnix > nowUnix)
        {
            entries.Add(mine);
        }

        AddLive(going, nowUnix);
        var committed = entries.Count;
        AddLive(store.ContactMusters, nowUnix);
        AddLive(store.Directory, nowUnix);
        SortRange(0, committed);
        SortRange(committed, entries.Count - committed);
        rows.Clear();
        var count = Math.Min(MaxEntries, entries.Count);
        for (var index = 0; index < count; index++)
        {
            var dto = entries[index];
            rows.Add(new Row(dto, 0, dto.StartsAtUnix, dto.EndsAtUnix, dto.Category, ReferenceEquals(dto, mine)));
        }
    }

    private void AddLive(MusterDto[] source, long nowUnix)
    {
        for (var index = 0; index < source.Length; index++)
        {
            var candidate = source[index];
            if (candidate.EndsAtUnix <= nowUnix || Contains(candidate.Id))
            {
                continue;
            }

            entries.Add(candidate);
        }
    }

    private bool Contains(string id)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (string.Equals(entries[index].Id, id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void SortRange(int start, int length)
    {
        if (length > 1)
        {
            entries.Sort(start, length, StartComparer.Instance);
        }
    }

    private long SecondsUntilCommitted(long nowUnix)
    {
        var soonest = long.MaxValue;
        var mine = store.Mine;
        if (mine is not null && mine.EndsAtUnix > nowUnix)
        {
            soonest = Math.Max(0, mine.StartsAtUnix - nowUnix);
        }

        var going = store.GoingMusters;
        for (var index = 0; index < going.Length; index++)
        {
            if (going[index].EndsAtUnix > nowUnix)
            {
                soonest = Math.Min(soonest, Math.Max(0, going[index].StartsAtUnix - nowUnix));
            }
        }

        return soonest == long.MaxValue ? -1 : soonest;
    }

    private List<Row> Samples(long nowUnix)
    {
        var anchor = nowUnix / 60;
        if (anchor == sampleAnchor && sampleRows.Count > 0)
        {
            return sampleRows;
        }

        sampleAnchor = anchor;
        sampleRows.Clear();
        var start = anchor * 60;
        for (var index = 0; index < SampleOffsets.Length; index++)
        {
            var startsAt = start + SampleOffsets[index];
            sampleRows.Add(new Row(null, index, startsAt, startsAt + 2 * HourSeconds, SampleCategories[index], false));
        }

        return sampleRows;
    }

    private ref CachedText SubtitleCache(string id) =>
        ref CollectionsMarshal.GetValueRefOrAddDefault(subtitles, id, out _);

    private static string Subtitle(ref CachedText cache, in Row row, long nowUnix)
    {
        var started = nowUnix >= row.StartsAt;
        var key = started ? -row.EndsAt : (row.StartsAt - nowUnix) / 60 * 31 + row.StartsAt;
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        var when = started
            ? Loc.T(L.WidgetsUtility.HappeningNow)
            : Loc.T(L.Island.StartsIn, TimeText.Until(row.StartsAt));
        return cache.Store(key, string.Concat(when, " · ", row.Host));
    }

    private static string Caption(ref CachedText cache, in Row row, bool started)
    {
        var moment = started ? row.EndsAt : row.StartsAt;
        var key = started ? -moment : moment;
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        var clock = TimeText.Clock(moment);
        return cache.Store(key, Loc.T(started ? L.WidgetsUtility.EndsAt : L.WidgetsUtility.StartsAt, clock));
    }

    public void Dispose()
    {
    }

    private sealed class StartComparer : IComparer<MusterDto>
    {
        public static readonly StartComparer Instance = new();

        public int Compare(MusterDto? left, MusterDto? right) =>
            (left?.StartsAtUnix ?? 0).CompareTo(right?.StartsAtUnix ?? 0);
    }
}
