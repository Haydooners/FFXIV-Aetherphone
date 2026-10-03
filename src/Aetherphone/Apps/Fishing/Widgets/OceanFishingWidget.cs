using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Interface;

namespace Aetherphone.Apps.Fishing.Widgets;

internal sealed class OceanFishingWidget : IHomeWidget
{
    private const int RefreshMilliseconds = 1000;
    private const int VoyageCount = 3;
    private const int RouteCount = 2;
    private const string RouteKey = "route";
    private const string IndigoValue = "indigo";
    private const string RubyValue = "ruby";
    private const float BadgeUnits = 26f;
    private const float GlyphUnits = 11f;
    private const float SoonMinutes = 30f;
    private const float BoardingRelevance = 1f;
    private const float SoonRelevance = 0.7f;
    private static readonly TimeSpan BoardingWindow = TimeSpan.FromMinutes(15);
    private static readonly Vector4 FishingAccent = AppAccents.For("fishing");

    private static readonly IReadOnlyList<WidgetOption> RouteOptions = new[]
    {
        new WidgetOption(RouteKey, L.WidgetsAdventure.OptionRoute,
            new[]
            {
                new WidgetChoice(IndigoValue, L.Fishing.IndigoRoute), new WidgetChoice(RubyValue, L.Fishing.RubyRoute),
            },
            IndigoValue),
    };

    private readonly OceanVoyageSlot[][] voyages =
    {
        new OceanVoyageSlot[VoyageCount], new OceanVoyageSlot[VoyageCount],
    };

    private readonly RefreshGate[] gates = new RefreshGate[RouteCount];
    private readonly CachedText[] heroTexts = new CachedText[RouteCount];
    private readonly CachedText[] detailTexts = new CachedText[RouteCount];
    private readonly CachedText[] clockTexts = new CachedText[RouteCount * VoyageCount];
    private readonly CachedText[] relativeTexts = new CachedText[RouteCount * VoyageCount];
    private readonly Dictionary<int, string> fishHints = new();

    public string Id => "fishing.voyage";
    public string DisplayName => Loc.T(L.WidgetsAdventure.FishingName);
    public string Description => Loc.T(L.WidgetsAdventure.FishingDescription);
    public string AppId => "fishing";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;
    public IReadOnlyList<WidgetOption> Options => RouteOptions;

    public float Relevance(string config)
    {
        var utcNow = DateTime.UtcNow;
        var voyage = GameSchedule.OceanFishing(utcNow, RouteOf(config));
        if (voyage.BoardingNow)
        {
            return BoardingRelevance;
        }

        return (voyage.NextBoardingUtc - utcNow).TotalMinutes <= SoonMinutes ? SoonRelevance : 0f;
    }

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var route = RouteOf(context.Config);
        var routeIndex = (int)route;
        var utcNow = DateTime.UtcNow;
        if (gates[routeIndex].Due(RefreshMilliseconds))
        {
            GameSchedule.UpcomingOceanVoyages(utcNow, route, voyages[routeIndex]);
        }

        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, routeIndex, utcNow);
            return;
        }

        DrawMedium(context, ink, route, routeIndex, utcNow);
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, int routeIndex, DateTime utcNow)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var drawList = context.DrawList;
        var next = voyages[routeIndex][0];
        var headerBottom = WidgetChrome.Header(context, ink, AppId,
            next.BoardingNow ? L.Fishing.NowBoarding : L.Fishing.NextVoyage, FishingAccent);
        var plan = OceanRoutes.Resolve(next.Destination, next.Time);
        var remaining = next.BoardingNow ? next.BoardingUtc + BoardingWindow - utcNow : next.BoardingUtc - utcNow;
        var hero = Countdown(ref heroTexts[routeIndex], remaining);
        var heroStyle = FitStyle(hero, WidgetType.DisplayCompact, content.Width);
        WidgetText.Tabular(drawList, new Vector2(content.Min.X, headerBottom + WidgetMetrics.Gutter * scale * 0.5f),
            hero, next.BoardingNow ? ink.Accent(FishingAccent) : ink.Primary, heroStyle);

        var captionHeight = AdventureWidgetArt.LineHeight(WidgetType.Caption);
        var headlineHeight = AdventureWidgetArt.LineHeight(WidgetType.Headline);
        var detailTop = content.Max.Y - captionHeight;
        var nameTop = detailTop - WidgetMetrics.RowGap * scale - headlineHeight;
        WidgetText.Draw(drawList, new Vector2(content.Min.X, nameTop), plan.RouteName, ink.Primary,
            WidgetType.Headline, content.Width);
        var glyph = GlyphUnits * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(content.Min.X + glyph * 0.5f, detailTop + captionHeight * 0.5f),
            FishingApp.TimeOfDayIcon(plan.TimeOfDay), ink.Accent(FishingApp.TimeOfDayTint(plan.TimeOfDay)), glyph);
        var detailLeft = content.Min.X + glyph + WidgetMetrics.RowGap * 2f * scale;
        WidgetText.Draw(drawList, new Vector2(detailLeft, detailTop), Detail(routeIndex, next, plan.TimeOfDay),
            ink.Secondary, WidgetType.Caption, MathF.Max(1f, content.Max.X - detailLeft));
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, OceanRoute route, int routeIndex,
        DateTime utcNow)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var headerBottom = WidgetChrome.Header(context, ink, AppId,
            route == OceanRoute.Ruby ? L.Fishing.RubyRoute : L.Fishing.IndigoRoute, FishingAccent);
        var slots = voyages[routeIndex];
        if (slots[0].BoardingNow)
        {
            var captionHeight = AdventureWidgetArt.LineHeight(WidgetType.Caption);
            var top = content.Min.Y + (headerBottom - content.Min.Y - captionHeight) * 0.5f;
            AdventureWidgetArt.RightAligned(context.DrawList, content.Max.X, top,
                WidgetText.Upper(L.Fishing.NowBoarding), ink.Accent(FishingAccent), WidgetType.Caption);
        }

        var body = new Rect(new Vector2(content.Min.X, headerBottom + gutter * 0.5f), content.Max);
        var rowHeight = body.Height / VoyageCount;
        for (var index = 0; index < VoyageCount; index++)
        {
            var top = body.Min.Y + index * rowHeight;
            DrawRow(context, ink, new Rect(new Vector2(body.Min.X, top), new Vector2(body.Max.X, top + rowHeight)),
                routeIndex, index, slots[index], utcNow);
        }
    }

    private void DrawRow(in WidgetContext context, in WidgetInk ink, Rect row, int routeIndex, int slotIndex,
        in OceanVoyageSlot slot, DateTime utcNow)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var plan = OceanRoutes.Resolve(slot.Destination, slot.Time);
        var badge = MathF.Min(BadgeUnits * scale, row.Height * 0.86f);
        AdventureWidgetArt.IconBadge(drawList, ink, new Vector2(row.Min.X + badge * 0.5f, row.Center.Y), badge,
            FishingApp.TimeOfDayIcon(plan.TimeOfDay), FishingApp.TimeOfDayTint(plan.TimeOfDay));

        var cacheIndex = routeIndex * VoyageCount + slotIndex;
        var local = slot.BoardingUtc.ToLocalTime();
        var clockKey = slot.BoardingUtc.Ticks;
        var clock = clockTexts[cacheIndex].IsCurrent(clockKey)
            ? clockTexts[cacheIndex].Value
            : clockTexts[cacheIndex].Store(clockKey, TimeText.Clock(local));
        var relative = Relative(cacheIndex, slot, utcNow);
        var bodyHeight = AdventureWidgetArt.LineHeight(WidgetType.Body);
        var captionHeight = AdventureWidgetArt.LineHeight(WidgetType.Caption);
        var blockTop = row.Center.Y - (bodyHeight + WidgetMetrics.RowGap * scale + captionHeight) * 0.5f;
        var subTop = blockTop + bodyHeight + WidgetMetrics.RowGap * scale;
        var clockWidth = AdventureWidgetArt.TabularRight(drawList, row.Max.X, blockTop, clock, ink.Primary,
            WidgetType.Body);
        var relativeWidth = Typography.Measure(relative, WidgetType.Caption).X;
        AdventureWidgetArt.RightAligned(drawList, row.Max.X, subTop, relative,
            slot.BoardingNow ? ink.Accent(FishingAccent) : ink.Secondary, WidgetType.Caption);

        var textLeft = row.Min.X + badge + gutter;
        var nameRight = row.Max.X - clockWidth - gutter;
        var hintRight = row.Max.X - relativeWidth - gutter;
        WidgetText.Draw(drawList, new Vector2(textLeft, blockTop), plan.RouteName, ink.Primary, WidgetType.Headline,
            MathF.Max(1f, nameRight - textLeft));
        var hasFish = plan.BlueFish.Length > 0;
        WidgetText.Draw(drawList, new Vector2(textLeft, subTop), hasFish ? FishHint(plan) : Loc.T(L.Fishing.NoBlueFish),
            hasFish ? ink.Secondary : ink.Tertiary, WidgetType.Caption, MathF.Max(1f, hintRight - textLeft));
    }

    private string Relative(int cacheIndex, in OceanVoyageSlot slot, DateTime utcNow)
    {
        if (slot.BoardingNow)
        {
            var closes = slot.BoardingUtc + BoardingWindow - utcNow;
            var closeKey = -1 - (long)closes.TotalSeconds;
            if (relativeTexts[cacheIndex].IsCurrent(closeKey))
            {
                return relativeTexts[cacheIndex].Value;
            }

            var seconds = Math.Max(0, (int)closes.TotalSeconds);
            return relativeTexts[cacheIndex].Store(closeKey,
                Loc.T(L.WidgetsAdventure.ClosesIn, TimeText.MinutesSeconds(seconds)));
        }

        var remaining = slot.BoardingUtc - utcNow;
        var key = (long)remaining.TotalMinutes;
        return relativeTexts[cacheIndex].IsCurrent(key)
            ? relativeTexts[cacheIndex].Value
            : relativeTexts[cacheIndex].Store(key, TimeFormat.Relative(remaining));
    }

    private string Detail(int routeIndex, in OceanVoyageSlot slot, OceanTimeOfDay timeOfDay)
    {
        var key = slot.BoardingUtc.Ticks;
        if (detailTexts[routeIndex].IsCurrent(key))
        {
            return detailTexts[routeIndex].Value;
        }

        return detailTexts[routeIndex].Store(key,
            string.Concat(FishingApp.TimeOfDayLabel(timeOfDay), " · ", TimeText.Clock(slot.BoardingUtc.ToLocalTime())));
    }

    private string FishHint(in OceanRoutePlan plan)
    {
        var key = plan.Destination * 256 + plan.Time;
        if (fishHints.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var hint = plan.BlueFish.Length == 1
            ? plan.BlueFish[0].Name
            : string.Concat(plan.BlueFish[0].Name, " · ", plan.BlueFish[1].Name);
        fishHints[key] = hint;
        return hint;
    }

    private static string Countdown(ref CachedText cache, TimeSpan remaining)
    {
        var totalSeconds = Math.Max(0, (int)remaining.TotalSeconds);
        if (cache.IsCurrent(totalSeconds))
        {
            return cache.Value;
        }

        var hours = totalSeconds / 3600;
        if (hours == 0)
        {
            return cache.Store(totalSeconds, TimeText.MinutesSeconds(totalSeconds));
        }

        var minutes = totalSeconds / 60 % 60;
        var seconds = totalSeconds % 60;
        return cache.Store(totalSeconds, string.Concat(hours.ToString(CultureInfo.InvariantCulture), ":",
            minutes.ToString("D2", CultureInfo.InvariantCulture), ":",
            seconds.ToString("D2", CultureInfo.InvariantCulture)));
    }

    private static TextStyle FitStyle(string text, in TextStyle style, float maxWidth)
    {
        var width = WidgetText.TabularWidth(text, style);
        if (width <= maxWidth || width <= 0f)
        {
            return style;
        }

        return new TextStyle(style.Scale * maxWidth / width, style.Weight);
    }

    private static OceanRoute RouteOf(string config) =>
        string.Equals(WidgetConfig.Get(config, RouteKey, IndigoValue), RubyValue, StringComparison.Ordinal)
            ? OceanRoute.Ruby
            : OceanRoute.Indigo;

    public void Dispose()
    {
    }
}
