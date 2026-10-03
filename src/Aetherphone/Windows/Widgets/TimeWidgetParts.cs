using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Widgets;

internal static class TimeWidgetParts
{
    private const int SplitCacheCapacity = 128;
    private const float FitStep = 0.05f;
    private const float TrackAlpha = 0.22f;
    private const float RingThicknessFactor = 0.13f;
    private const float RingThicknessMinimum = 3f;
    private const float EmptyGlyphUnits = 20f;
    private static readonly Vector4 DarkFace = new(0.17f, 0.17f, 0.19f, 1f);

    private readonly struct ClockSplit
    {
        public readonly string Head;
        public readonly string Tail;

        public ClockSplit(string head, string tail)
        {
            Head = head;
            Tail = tail;
        }
    }

    private static readonly Dictionary<string, ClockSplit> Splits = new(StringComparer.Ordinal);

    public static ClockFacePaint ClockPaint(in WidgetInk ink, float hours, Vector4 accent)
    {
        switch (ink.Mode)
        {
            case WidgetMode.Tinted:
            case WidgetMode.Clear:
                return new ClockFacePaint(ink.Fill, ink.Primary, ink.Accent(accent), 0f);
            case WidgetMode.Dark:
                return new ClockFacePaint(ink.Fade(DarkFace), ink.Primary, ink.Accent(accent), 0f);
        }

        var paint = AnalogClock.DayNight(hours, accent);
        return new ClockFacePaint(ink.Fade(paint.Face), ink.Fade(paint.Ink), ink.Fade(paint.Seconds), paint.Sheen);
    }

    public static float RingThickness(float radius, float scale) =>
        MathF.Max(RingThicknessMinimum * scale, radius * RingThicknessFactor);

    public static void Ring(ImDrawListPtr drawList, in WidgetInk ink, Vector2 center, float radius, float thickness,
        float fraction, Vector4 accent)
    {
        var color = ink.Accent(accent);
        ProgressRing.Track(drawList, center, radius, thickness, color with { W = color.W * TrackAlpha });
        ProgressRing.Fill(drawList, center, radius, thickness, Math.Clamp(fraction, 0f, 1f), color);
    }

    public static TextStyle Fitted(string text, in TextStyle style, float maxWidth, bool tabular)
    {
        var width = tabular ? WidgetText.TabularWidth(text, style) : Typography.Measure(text, style).X;
        if (width <= maxWidth || width <= 0f)
        {
            return style;
        }

        var factor = MathF.Floor(maxWidth / width / FitStep) * FitStep;
        return new TextStyle(style.Scale * MathF.Max(FitStep, factor), style.Weight);
    }

    public static float TabularCentered(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color,
        in TextStyle style, float maxWidth)
    {
        var fitted = Fitted(text, style, maxWidth, true);
        var width = WidgetText.TabularWidth(text, fitted);
        var height = Typography.Measure(text, fitted).Y;
        WidgetText.Tabular(drawList, new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f), text, color,
            fitted);
        return height;
    }

    public static float ClockWidth(string clock, in TextStyle style, in TextStyle suffixStyle)
    {
        var split = Split(clock);
        var width = WidgetText.TabularWidth(split.Head, style);
        return split.Tail.Length == 0 ? width : width + Typography.Measure(split.Tail, suffixStyle).X;
    }

    public static float Clock(ImDrawListPtr drawList, Vector2 position, string clock, Vector4 color,
        in TextStyle style, in TextStyle suffixStyle, Vector4 suffixColor)
    {
        var split = Split(clock);
        var width = WidgetText.Tabular(drawList, position, split.Head, color, style);
        var headHeight = Typography.Measure(split.Head, style).Y;
        if (split.Tail.Length > 0)
        {
            var tailSize = Typography.Measure(split.Tail, suffixStyle);
            var baseline = position.Y + headHeight * 0.78f;
            Typography.Draw(drawList, new Vector2(position.X + width, baseline - tailSize.Y * 0.78f), split.Tail,
                suffixColor, suffixStyle);
        }

        return headHeight;
    }

    public static float Wrapped(ImDrawListPtr drawList, Vector2 topLeft, string text, Vector4 color,
        in TextStyle style, float maxWidth, int maxLines)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
        {
            return 0f;
        }

        var lines = Typography.WrapText(text, style, maxWidth);
        var lineHeight = Typography.Measure(text, style).Y;
        var count = Math.Min(maxLines, lines.Length);
        for (var index = 0; index < count; index++)
        {
            Typography.Draw(drawList, new Vector2(topLeft.X, topLeft.Y + index * lineHeight), lines[index], color,
                style);
        }

        return count * lineHeight;
    }

    public static void Empty(in WidgetContext context, in WidgetInk ink, Rect area, FontAwesomeIcon icon,
        string title, string detail)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var glyph = EmptyGlyphUnits * scale;
        var gap = WidgetMetrics.RowGap * 2f * scale;
        var titleHeight = Typography.Measure(title, WidgetType.Headline).Y;
        var detailHeight = detail.Length > 0 ? Typography.Measure(detail, WidgetType.Caption).Y : 0f;
        var total = glyph + gap + titleHeight + (detail.Length > 0 ? WidgetMetrics.RowGap * scale + detailHeight : 0f);
        var top = area.Center.Y - total * 0.5f;
        ProgressRing.CenterIcon(drawList, new Vector2(area.Center.X, top + glyph * 0.5f), icon, ink.Tertiary,
            glyph);
        var titleTop = top + glyph + gap;
        CenteredLine(drawList, area, titleTop, title, ink.Secondary, WidgetType.Headline);
        if (detail.Length > 0)
        {
            CenteredLine(drawList, area, titleTop + titleHeight + WidgetMetrics.RowGap * scale, detail, ink.Tertiary,
                WidgetType.Caption);
        }
    }

    public static void Unavailable(in WidgetContext context, in WidgetInk ink, float top, string text)
    {
        var content = WidgetMetrics.Content(context);
        Wrapped(context.DrawList, new Vector2(content.Min.X, top), text, ink.Secondary, WidgetType.Body,
            content.Width, 4);
    }

    public static void Separator(ImDrawListPtr drawList, in WidgetInk ink, float left, float right, float y,
        float scale) =>
        drawList.AddLine(new Vector2(left, y), new Vector2(right, y), ImGui.GetColorU32(ink.Separator),
            MathF.Max(1f, 0.75f * scale));

    public static string RelativeDay(DateTime localMoment)
    {
        var today = DateTime.Today;
        var day = localMoment.Date;
        if (day == today)
        {
            return Loc.T(L.Clock.DayToday);
        }

        if (day == today.AddDays(1))
        {
            return Loc.T(L.Clock.DayTomorrow);
        }

        if (day == today.AddDays(-1))
        {
            return Loc.T(L.Clock.DayYesterday);
        }

        return day.ToString("ddd", Loc.Culture);
    }

    public static string DayAndClock(DateTime localMoment)
    {
        if (localMoment.Date == DateTime.Today)
        {
            return TimeText.Clock(localMoment);
        }

        var day = localMoment.Date == DateTime.Today.AddDays(1)
            ? Loc.T(L.Time.Tomorrow)
            : localMoment.ToString("ddd", Loc.Culture);
        return string.Concat(day, " ", TimeText.Clock(localMoment));
    }

    public static long MinuteKey(DateTime moment) => moment.Ticks / TimeSpan.TicksPerMinute;

    private static void CenteredLine(ImDrawListPtr drawList, Rect area, float top, string text, Vector4 color,
        in TextStyle style)
    {
        var fitted = WidgetText.Fit(text, area.Width, style, out var fittedScale);
        var width = Typography.Measure(fitted, fittedScale, style.Weight).X;
        Typography.Draw(drawList, new Vector2(area.Center.X - width * 0.5f, top), fitted, color, fittedScale,
            style.Weight);
    }

    private static ClockSplit Split(string clock)
    {
        if (Splits.TryGetValue(clock, out var split))
        {
            return split;
        }

        if (Splits.Count >= SplitCacheCapacity)
        {
            Splits.Clear();
        }

        var lastDigit = -1;
        var firstDigit = -1;
        for (var index = 0; index < clock.Length; index++)
        {
            if (!char.IsDigit(clock[index]))
            {
                continue;
            }

            if (firstDigit < 0)
            {
                firstDigit = index;
            }

            lastDigit = index;
        }

        split = lastDigit < 0 || firstDigit > 0 || lastDigit == clock.Length - 1
            ? new ClockSplit(clock, string.Empty)
            : new ClockSplit(clock[..(lastDigit + 1)], clock[(lastDigit + 1)..]);
        Splits[clock] = split;
        return split;
    }
}
