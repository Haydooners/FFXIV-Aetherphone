using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Market.Widgets;

internal sealed class MarketWatchWidget : IHomeWidget
{
    private const string AppKey = "market";
    private const int RefreshMilliseconds = 2000;
    private const int MaxRows = 7;
    private const int TextCacheLimit = 64;
    private const float IconUnits = 28f;
    private const float RowUnits = 40f;
    private const float DotUnits = 3f;

    private sealed class RowText
    {
        public CachedText Rule;
        public CachedText Price;
    }

    private readonly MarketAlertService alerts;
    private readonly ITextureProvider textures;
    private readonly List<MarketAlert> buffer = new();
    private readonly Dictionary<MarketAlert, RowText> texts = new();
    private readonly CachedText[] samplePrices = new CachedText[4];
    private WidgetRefresh refresh;
    private int[] ranks = Array.Empty<int>();
    private int triggered;
    private CachedText trailingText;

    public MarketWatchWidget(MarketAlertService alerts, ITextureProvider textures)
    {
        this.alerts = alerts;
        this.textures = textures;
    }

    public string Id => "market.alerts";
    public string DisplayName => Loc.T(L.WidgetsUtility.MarketName);
    public string Description => Loc.T(L.WidgetsUtility.MarketDescription);
    public string AppId => AppKey;
    public WidgetSizeSet Sizes => WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public float Relevance(string config) => alerts.TriggeredCount > 0 ? 0.85f : 0f;

    public void Draw(in WidgetContext context)
    {
        Refresh();
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var drawList = context.DrawList;
        var scale = context.Scale;
        var content = WidgetMetrics.Content(context);
        var accentRaw = AppAccents.For(AppKey);
        var accent = ink.Accent(accentRaw);
        var sample = context.Preview && buffer.Count == 0;
        var shownTriggered = sample ? 1 : triggered;
        var trailing = shownTriggered > 0 ? Trailing(shownTriggered) : string.Empty;
        var top = WidgetChrome.Header(context, ink, AppKey, Loc.T(L.WidgetsUtility.MarketName), accentRaw,
            trailing, accent);
        var body = new Rect(new Vector2(content.Min.X, top + WidgetMetrics.RowGap * scale), content.Max);
        var total = sample ? WidgetSamples.MarketItems.Length : buffer.Count;
        if (total == 0)
        {
            WidgetChrome.Message(context, ink, body, FontAwesomeIcon.Coins, accentRaw,
                Loc.T(L.WidgetsUtility.NoAlerts), Loc.T(L.WidgetsUtility.NoAlertsHint));
            return;
        }

        var capacity = Math.Clamp((int)(body.Height / (RowUnits * scale)), 1, MaxRows);
        var rowHeight = body.Height / capacity;
        var rows = Math.Min(capacity, total);
        for (var index = 0; index < rows; index++)
        {
            var rowRect = new Rect(new Vector2(body.Min.X, body.Min.Y + index * rowHeight),
                new Vector2(body.Max.X, body.Min.Y + (index + 1) * rowHeight));
            if (sample)
            {
                DrawSample(context, ink, index, rowRect, accent);
            }
            else
            {
                DrawAlert(context, ink, index, rowRect, buffer[index], accent);
            }

            if (index < rows - 1)
            {
                var left = rowRect.Min.X + (IconUnits + WidgetMetrics.Gutter) * scale;
                WidgetChrome.Separator(context, ink, left, rowRect.Max.X, rowRect.Max.Y);
            }
        }
    }

    private void DrawAlert(in WidgetContext context, in WidgetInk ink, int index, Rect rowRect, MarketAlert alert,
        Vector4 accent)
    {
        WidgetControls.Link(context, ink, index, rowRect, WidgetRoute.MarketItem(alert.ItemId));
        var drawList = context.DrawList;
        var icon = Icon(rowRect, context.Scale);
        if (alert.IconId != 0)
        {
            var texture = textures.GetFromGameIcon(new GameIconLookup(alert.IconId)).GetWrapOrEmpty();
            Squircle.FillImage(drawList, icon.Min, icon.Max, icon.Width * 0.22f, texture.Handle,
                ImGui.GetColorU32(ink.ImageTint));
        }
        else
        {
            DrawPlaceholderIcon(drawList, icon, ink, accent);
        }

        var text = TextFor(alert);
        var rule = Rule(ref text.Rule, alert.Below, alert.Threshold, alert.ScopeName, alert.HqOnly);
        var price = alert.LastSeenPrice > 0
            ? WidgetText.Number(ref text.Price, alert.LastSeenPrice)
            : Loc.T(L.WidgetsUtility.Checking);
        var priceColor = !alert.Enabled ? ink.Tertiary
            : alert.Triggered ? accent
            : alert.LastSeenPrice > 0 ? ink.Primary : ink.Tertiary;
        DrawText(context, ink, rowRect, icon, alert.ItemName, rule, price, priceColor,
            alert.Triggered && alert.Enabled, alert.LastSeenPrice > 0, accent);
    }

    private void DrawSample(in WidgetContext context, in WidgetInk ink, int index, Rect rowRect, Vector4 accent)
    {
        var icon = Icon(rowRect, context.Scale);
        DrawPlaceholderIcon(context.DrawList, icon, ink, accent);
        var price = WidgetText.Number(ref samplePrices[index], WidgetSamples.MarketPrices[index]);
        var rule = WidgetSamples.Worlds[index % WidgetSamples.Worlds.Length];
        var hit = index == 0;
        DrawText(context, ink, rowRect, icon, WidgetSamples.MarketItems[index], rule, price,
            hit ? accent : ink.Primary, hit, true, accent);
    }

    private static void DrawText(in WidgetContext context, in WidgetInk ink, Rect rowRect, Rect icon, string name,
        string rule, string price, Vector4 priceColor, bool triggered, bool tabular, Vector4 accent)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var priceStyle = WidgetType.Headline;
        var priceWidth = tabular
            ? WidgetText.TabularWidth(price, priceStyle)
            : Typography.Measure(price, WidgetType.Caption).X;
        var priceHeight = Typography.Measure(price, tabular ? priceStyle : WidgetType.Caption).Y;
        var priceLeft = rowRect.Max.X - priceWidth;
        var priceTop = rowRect.Center.Y - priceHeight * 0.5f;
        if (tabular)
        {
            WidgetText.Tabular(drawList, new Vector2(priceLeft, priceTop), price, priceColor, priceStyle);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(priceLeft, priceTop), price, priceColor, WidgetType.Caption);
        }

        if (triggered)
        {
            var dot = DotUnits * scale;
            drawList.AddCircleFilled(new Vector2(priceLeft - dot * 2.5f, rowRect.Center.Y), dot,
                ImGui.GetColorU32(accent), 16);
            priceLeft -= dot * 4f;
        }

        var left = icon.Max.X + WidgetMetrics.Gutter * scale;
        var width = priceLeft - WidgetMetrics.Gutter * scale - left;
        var titleHeight = WidgetText.SpacedLineHeight(WidgetType.Headline);
        var ruleHeight = WidgetText.SpacedLineHeight(WidgetType.Caption);
        var top = rowRect.Center.Y - (titleHeight + ruleHeight) * 0.5f;
        WidgetText.Draw(drawList, new Vector2(left, top), name, ink.Primary, WidgetType.Headline, width);
        WidgetText.Draw(drawList, new Vector2(left, top + titleHeight), rule, ink.Secondary,
            WidgetType.Caption, width);
    }

    private static void DrawPlaceholderIcon(ImDrawListPtr drawList, Rect icon, in WidgetInk ink, Vector4 accent)
    {
        Squircle.Fill(drawList, icon.Min, icon.Max, icon.Width * 0.22f, ImGui.GetColorU32(ink.Fill));
        ProgressRing.CenterIcon(drawList, icon.Center, FontAwesomeIcon.Coins, accent, icon.Height * 0.5f);
    }

    private static Rect Icon(Rect rowRect, float scale)
    {
        var side = MathF.Min(IconUnits * scale, rowRect.Height - 4f * scale);
        var min = new Vector2(rowRect.Min.X, rowRect.Center.Y - side * 0.5f);
        return new Rect(min, min + new Vector2(side, side));
    }

    private void Refresh()
    {
        if (!refresh.Due(RefreshMilliseconds))
        {
            return;
        }

        alerts.CopyInto(buffer);
        Order();

        if (texts.Count > TextCacheLimit)
        {
            texts.Clear();
        }
    }

    private RowText TextFor(MarketAlert alert)
    {
        ref var text = ref CollectionsMarshal.GetValueRefOrAddDefault(texts, alert, out _);
        text ??= new RowText();
        return text;
    }

    private string Trailing(int count) =>
        trailingText.IsCurrent(count)
            ? trailingText.Value
            : trailingText.Store(count, Loc.T(L.WidgetsUtility.Triggered, count));

    private static string Rule(ref CachedText cache, bool below, long threshold, string scope, bool hqOnly)
    {
        var key = (threshold << 3) ^ (below ? 1L : 0L) ^ (hqOnly ? 2L : 0L) ^ ((long)scope.GetHashCode() << 32);
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        var arrow = below ? "≤ " : "≥ ";
        var text = string.Concat(arrow, MarketFormat.Gil(threshold), " · ", scope);
        if (hqOnly)
        {
            text = string.Concat(text, " · ", Loc.T(L.Common.Hq));
        }

        return cache.Store(key, text);
    }

    private void Order()
    {
        var count = buffer.Count;
        if (ranks.Length < count)
        {
            ranks = new int[count];
        }

        triggered = 0;
        for (var index = 0; index < count; index++)
        {
            var rank = RankOf(buffer[index]);
            ranks[index] = rank;
            if (rank == 0)
            {
                triggered++;
            }
        }

        for (var index = 1; index < count; index++)
        {
            var alert = buffer[index];
            var rank = ranks[index];
            var cursor = index - 1;
            while (cursor >= 0 && Precedes(rank, alert, ranks[cursor], buffer[cursor]))
            {
                buffer[cursor + 1] = buffer[cursor];
                ranks[cursor + 1] = ranks[cursor];
                cursor--;
            }

            buffer[cursor + 1] = alert;
            ranks[cursor + 1] = rank;
        }
    }

    private static int RankOf(MarketAlert alert)
    {
        if (!alert.Enabled)
        {
            return 2;
        }

        return alert.Triggered ? 0 : 1;
    }

    private static bool Precedes(int rank, MarketAlert alert, int otherRank, MarketAlert other) =>
        rank != otherRank
            ? rank < otherRank
            : string.Compare(alert.ItemName, other.ItemName, StringComparison.CurrentCultureIgnoreCase) < 0;

    public void Dispose()
    {
    }
}
