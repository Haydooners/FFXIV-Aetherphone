using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Widgets;

internal struct CachedText
{
    private long key;
    private bool filled;
    private string? value;
    private CultureInfo? culture;
    private int timeFormat;

    public string Value => value ?? string.Empty;

    public bool IsCurrent(long candidate) =>
        filled && key == candidate && ReferenceEquals(culture, Loc.Culture) && timeFormat == TimeText.FormatVersion;

    public string Store(long candidate, string text)
    {
        key = candidate;
        filled = true;
        value = text;
        culture = Loc.Culture;
        timeFormat = TimeText.FormatVersion;
        return text;
    }

    public void Reset() => filled = false;
}

internal static class WidgetText
{
    private const int DigitCacheCapacity = 12;

    private struct DigitAdvance
    {
        public float Scale;
        public FontWeight Weight;
        public float Advance;
    }

    private static readonly DigitAdvance[] DigitAdvances = new DigitAdvance[DigitCacheCapacity];
    private static int digitCount;
    private static int digitGeneration = -1;

    public static string Upper(LocString entry) => Loc.Upper(Loc.T(entry));

    public static string Upper(string text) => Loc.Upper(text);

    public static string Fit(string text, float maxWidth, in TextStyle style, out float fittedScale)
    {
        fittedScale = Typography.FitScale(text, maxWidth, style.Scale, style.Scale * WidgetType.MinimumFit,
            style.Weight);
        return Typography.FitText(text, maxWidth, fittedScale, style.Weight);
    }

    public static float Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color,
        in TextStyle style, float maxWidth)
    {
        var fitted = Fit(text, maxWidth, style, out var scale);
        Typography.Draw(drawList, position, fitted, color, scale, style.Weight);
        return Typography.Measure(fitted, scale, style.Weight).Y;
    }

    public static float Tracked(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color,
        in TextStyle style, float tracking)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var packed = ImGui.GetColorU32(color);
            var cursor = position;
            var index = 0;
            while (index < text.Length)
            {
                var length = char.IsHighSurrogate(text[index]) && index + 1 < text.Length ? 2 : 1;
                var glyph = text.AsSpan(index, length);
                drawList.AddText(font, fontSize, cursor, packed, glyph);
                cursor.X += ImGui.CalcTextSize(glyph).X;
                index += length;
                if (index < text.Length)
                {
                    cursor.X += tracking;
                }
            }

            return cursor.X - position.X;
        }
    }

    public static float TrackedWidth(string text, in TextStyle style, float tracking)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        return Typography.Measure(text, style).X + tracking * Math.Max(0, text.Length - 1);
    }

    public static float Eyebrow(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, float scale) =>
        Tracked(drawList, position, Upper(text), color, WidgetType.Eyebrow, WidgetType.EyebrowTracking * scale);

    public static float Eyebrow(ImDrawListPtr drawList, Vector2 position, LocString text, Vector4 color,
        float scale) =>
        Tracked(drawList, position, Upper(text), color, WidgetType.Eyebrow, WidgetType.EyebrowTracking * scale);

    public static float EyebrowWidth(string text, float scale) =>
        TrackedWidth(Upper(text), WidgetType.Eyebrow, WidgetType.EyebrowTracking * scale);

    public static float EyebrowHeight() => Typography.Measure("A", WidgetType.Eyebrow).Y;

    public static void EyebrowFit(ImDrawListPtr drawList, Vector2 position, string text, float maxWidth,
        Vector4 color, float scale)
    {
        var upper = Upper(text);
        var tracking = WidgetType.EyebrowTracking * scale;
        if (TrackedWidth(upper, WidgetType.Eyebrow, tracking) <= maxWidth)
        {
            Tracked(drawList, position, upper, color, WidgetType.Eyebrow, tracking);
            return;
        }

        var budget = MathF.Max(1f, maxWidth - tracking * Math.Max(0, upper.Length - 1));
        var clipped = Typography.FitText(upper, budget, WidgetType.Eyebrow);
        Tracked(drawList, position, clipped, color, WidgetType.Eyebrow, tracking);
    }

    public static void EyebrowMarquee(ImDrawListPtr drawList, MarqueeId id, string text, Vector2 position,
        float maxWidth, Vector4 color, float scale)
    {
        var upper = Upper(text);
        var tracking = WidgetType.EyebrowTracking * scale;
        var fullWidth = TrackedWidth(upper, WidgetType.Eyebrow, tracking);
        if (fullWidth <= maxWidth)
        {
            Tracked(drawList, position, upper, color, WidgetType.Eyebrow, tracking);
            return;
        }

        var height = EyebrowHeight();
        if (!UiInteract.Hover(position, position + new Vector2(maxWidth, height)))
        {
            EyebrowFit(drawList, position, text, maxWidth, color, scale);
            return;
        }

        var offset = Marquee.Offset(id, fullWidth - maxWidth);
        var slack = 4f * scale;
        drawList.PushClipRect(new Vector2(position.X, position.Y - slack),
            new Vector2(position.X + maxWidth, position.Y + height + slack), true);
        Tracked(drawList, position with { X = position.X - offset }, upper, color, WidgetType.Eyebrow, tracking);
        drawList.PopClipRect();
    }

    public static float Tabular(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color,
        in TextStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        var advance = DigitAdvanceFor(style);
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var packed = ImGui.GetColorU32(color);
            var cursor = position;
            for (var index = 0; index < text.Length; index++)
            {
                var glyph = text.AsSpan(index, 1);
                var width = ImGui.CalcTextSize(glyph).X;
                if (char.IsAsciiDigit(text[index]))
                {
                    drawList.AddText(font, fontSize, cursor with { X = cursor.X + (advance - width) * 0.5f }, packed,
                        glyph);
                    cursor.X += advance;
                    continue;
                }

                drawList.AddText(font, fontSize, cursor, packed, glyph);
                cursor.X += width;
            }

            return cursor.X - position.X;
        }
    }

    public static float TabularWidth(string text, in TextStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        var advance = DigitAdvanceFor(style);
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            var width = 0f;
            for (var index = 0; index < text.Length; index++)
            {
                width += char.IsAsciiDigit(text[index]) ? advance : ImGui.CalcTextSize(text.AsSpan(index, 1)).X;
            }

            return width;
        }
    }

    public static string Countdown(ref CachedText cache, TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var key = (long)remaining.TotalMinutes;
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        var text = remaining.TotalDays >= 1
            ? string.Concat(((int)remaining.TotalDays).ToString(), "d ", remaining.Hours.ToString(), "h")
            : string.Concat(((int)remaining.TotalHours).ToString(), ":", remaining.Minutes.ToString("D2"));
        return cache.Store(key, text);
    }

    public static string Seconds(ref CachedText cache, TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var key = (long)remaining.TotalSeconds;
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, TimeText.MinutesSeconds((int)key));
    }

    public static string Number(ref CachedText cache, long value) =>
        cache.IsCurrent(value) ? cache.Value : cache.Store(value, NumberText.Group(value));

    public static string Integer(ref CachedText cache, long value) =>
        cache.IsCurrent(value) ? cache.Value : cache.Store(value, value.ToString(Loc.Culture));

    private static float DigitAdvanceFor(in TextStyle style)
    {
        var generation = Plugin.Fonts.Generation;
        if (generation != digitGeneration)
        {
            digitGeneration = generation;
            digitCount = 0;
        }

        for (var index = 0; index < digitCount; index++)
        {
            var entry = DigitAdvances[index];
            if (entry.Scale == style.Scale && entry.Weight == style.Weight)
            {
                return entry.Advance;
            }
        }

        var advance = 0f;
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Span<char> digit = stackalloc char[1];
            for (var value = 0; value < 10; value++)
            {
                digit[0] = (char)('0' + value);
                advance = MathF.Max(advance, ImGui.CalcTextSize((ReadOnlySpan<char>)digit).X);
            }
        }

        if (digitCount == DigitCacheCapacity)
        {
            digitCount = 0;
        }

        DigitAdvances[digitCount++] = new DigitAdvance { Scale = style.Scale, Weight = style.Weight, Advance = advance };
        return advance;
    }
}
