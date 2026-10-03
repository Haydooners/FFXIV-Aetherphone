using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Widgets;

internal static class UtilityWidgetKit
{
    public const float RowThumb = 32f;
    public const float CheckRadius = 10f;
    public const float LineSpacing = 1.2f;
    private const float MessageGlyph = 22f;
    private const float MessageGap = 6f;
    private const int ClampCacheCapacity = 96;
    private const string Ellipsis = "…";

    private readonly record struct ClampKey(string Text, float MaxWidth, int MaxLines, float Scale, FontWeight Weight);

    private static readonly Dictionary<ClampKey, string[]> ClampCache = new();
    private static readonly string[] NoLines = Array.Empty<string>();

    public static bool Press(in WidgetContext context, int controlId, Rect rect, out bool hovered,
        out float pressScale)
    {
        var key = WidgetHits.Key(context, controlId);
        hovered = false;
        pressScale = WidgetHits.PressScale(key);
        if (!context.Interactive)
        {
            return false;
        }

        WidgetHits.Register(key, rect);
        hovered = UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return WidgetHits.ConsumeFired(key);
    }

    public static Rect Scaled(Rect rect, float factor)
    {
        if (factor == 1f)
        {
            return rect;
        }

        var center = rect.Center;
        var half = rect.Size * 0.5f * factor;
        return new Rect(center - half, center + half);
    }

    public static float Header(in WidgetContext context, in WidgetInk ink, string appId, string label,
        Vector4 accent, string trailing, Vector4 trailingColor)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var drawList = context.DrawList;
        var glyphSize = WidgetMetrics.GlyphSmall * scale;
        var tint = ink.Accent(accent);
        var eyebrowHeight = WidgetText.EyebrowHeight();
        var rowHeight = MathF.Max(glyphSize, eyebrowHeight);
        var centerY = content.Min.Y + rowHeight * 0.5f;
        var left = content.Min.X;
        var right = content.Max.X;
        if (trailing.Length > 0)
        {
            var trailingSize = Typography.Measure(trailing, WidgetType.Caption);
            Typography.Draw(drawList, new Vector2(right - trailingSize.X, centerY - trailingSize.Y * 0.5f), trailing,
                trailingColor, WidgetType.Caption);
            right -= trailingSize.X + WidgetMetrics.Gutter * scale;
        }

        if (appId.Length > 0)
        {
            AppIconTile.TryDrawGlyph(drawList, appId, new Vector2(left + glyphSize * 0.5f, centerY), glyphSize, tint);
            left += glyphSize + WidgetMetrics.Gutter * 0.5f * scale;
        }

        if (label.Length > 0)
        {
            WidgetText.EyebrowFit(drawList, new Vector2(left, centerY - eyebrowHeight * 0.5f), label,
                MathF.Max(1f, right - left), tint, scale);
        }

        return content.Min.Y + rowHeight;
    }

    public static void Message(in WidgetContext context, in WidgetInk ink, Rect area, FontAwesomeIcon icon,
        Vector4 accent, string line, string detail)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var maxWidth = area.Width;
        var glyph = MessageGlyph * scale;
        var gap = MessageGap * scale;
        var lineHeight = LineHeightOf(WidgetType.Headline);
        var detailHeight = LineHeightOf(WidgetType.Caption);
        var availableLines = Math.Max(1, (int)((area.Height - glyph - gap) / lineHeight));
        var lines = Clamp(line, WidgetType.Headline, maxWidth, Math.Min(2, availableLines));
        var detailLines = detail.Length == 0
            ? NoLines
            : Clamp(detail, WidgetType.Caption, maxWidth, 2);
        var showGlyph = area.Height >= glyph + gap + lines.Length * lineHeight;
        var total = lines.Length * lineHeight + (showGlyph ? glyph + gap : 0f);
        if (detailLines.Length > 0 && total + gap * 0.5f + detailLines.Length * detailHeight <= area.Height)
        {
            total += gap * 0.5f + detailLines.Length * detailHeight;
        }
        else
        {
            detailLines = NoLines;
        }

        var top = area.Center.Y - total * 0.5f;
        if (showGlyph)
        {
            ProgressRing.CenterIcon(drawList, new Vector2(area.Center.X, top + glyph * 0.5f), icon, ink.Accent(accent),
                glyph);
            top += glyph + gap;
        }

        top = DrawCentered(drawList, lines, new Vector2(area.Center.X, top), ink.Secondary, WidgetType.Headline,
            lineHeight);
        if (detailLines.Length == 0)
        {
            return;
        }

        DrawCentered(drawList, detailLines, new Vector2(area.Center.X, top + gap * 0.5f), ink.Tertiary,
            WidgetType.Caption, detailHeight);
    }

    public static void RedactedRows(ImDrawListPtr drawList, Rect area, in WidgetInk ink, int rows, bool thumbnail,
        float scale)
    {
        if (rows <= 0)
        {
            return;
        }

        var rowHeight = area.Height / rows;
        var bar = WidgetType.Headline.Scale * 11f * scale;
        for (var row = 0; row < rows; row++)
        {
            var top = area.Min.Y + row * rowHeight;
            var left = area.Min.X;
            var middle = top + rowHeight * 0.5f;
            if (thumbnail)
            {
                var side = MathF.Min(RowThumb * scale, rowHeight - 4f * scale);
                var thumb = new Rect(new Vector2(left, middle - side * 0.5f), new Vector2(left + side, middle + side * 0.5f));
                Squircle.Fill(drawList, thumb.Min, thumb.Max, side * 0.3f, ImGui.GetColorU32(ink.Fill));
                left += side + WidgetMetrics.Gutter * scale;
            }

            var width = area.Max.X - left;
            WidgetChrome.Redacted(drawList,
                new Rect(new Vector2(left, middle - bar - 2f * scale), new Vector2(left + width * 0.72f, middle - 2f * scale)),
                ink);
            WidgetChrome.Redacted(drawList,
                new Rect(new Vector2(left, middle + 3f * scale), new Vector2(left + width * 0.46f, middle + 3f * scale + bar * 0.8f)),
                ink);
        }
    }

    public static void CheckCircle(ImDrawListPtr drawList, Vector2 center, float radius, float fill, Vector4 accent,
        in WidgetInk ink, float scale)
    {
        if (fill <= 0.01f)
        {
            drawList.AddCircle(center, radius, ImGui.GetColorU32(ink.Tertiary), 32, 1.6f * scale);
            return;
        }

        drawList.AddCircle(center, radius, ImGui.GetColorU32(ink.Tertiary with { W = ink.Tertiary.W * (1f - fill) }),
            32, 1.6f * scale);
        drawList.AddCircleFilled(center, radius * (0.55f + 0.45f * fill), ImGui.GetColorU32(accent with
        {
            W = accent.W * fill,
        }), 32);
        var check = ImGui.GetColorU32(ink.OnAccent with { W = ink.OnAccent.W * fill });
        var thickness = 1.8f * scale;
        var start = center + new Vector2(-radius * 0.42f, 0f);
        var corner = center + new Vector2(-radius * 0.1f, radius * 0.34f);
        var end = center + new Vector2(radius * 0.44f, -radius * 0.32f);
        drawList.AddLine(start, corner, check, thickness);
        drawList.AddLine(corner, end, check, thickness);
    }

    public static float LineHeightOf(in TextStyle style) => Typography.Measure("Ay", style).Y * LineSpacing;

    public static string[] Clamp(string text, in TextStyle style, float maxWidth, int maxLines)
    {
        if (string.IsNullOrEmpty(text) || maxLines <= 0 || maxWidth <= 0f)
        {
            return NoLines;
        }

        var key = new ClampKey(text, MathF.Round(maxWidth), maxLines, style.Scale, style.Weight);
        if (ClampCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (ClampCache.Count >= ClampCacheCapacity)
        {
            ClampCache.Clear();
        }

        var wrapped = Typography.WrapText(text, style, maxWidth);
        string[] lines;
        if (wrapped.Length <= maxLines)
        {
            lines = wrapped;
        }
        else
        {
            lines = new string[maxLines];
            for (var index = 0; index < maxLines - 1; index++)
            {
                lines[index] = wrapped[index];
            }

            var last = wrapped[maxLines - 1].TrimEnd();
            lines[maxLines - 1] = Typography.FitText(string.Concat(last, Ellipsis), maxWidth, style);
        }

        ClampCache[key] = lines;
        return lines;
    }

    public static float DrawLines(ImDrawListPtr drawList, string[] lines, Vector2 topLeft, Vector4 color,
        in TextStyle style, float lineHeight)
    {
        var top = topLeft.Y;
        for (var index = 0; index < lines.Length; index++)
        {
            Typography.Draw(drawList, new Vector2(topLeft.X, top), lines[index], color, style);
            top += lineHeight;
        }

        return top;
    }

    public static float DrawCentered(ImDrawListPtr drawList, string[] lines, Vector2 topCenter, Vector4 color,
        in TextStyle style, float lineHeight)
    {
        var top = topCenter.Y;
        for (var index = 0; index < lines.Length; index++)
        {
            var width = Typography.Measure(lines[index], style).X;
            Typography.Draw(drawList, new Vector2(topCenter.X - width * 0.5f, top), lines[index], color, style);
            top += lineHeight;
        }

        return top;
    }

    public static float DrawFitted(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color,
        in TextStyle style, float maxWidth) =>
        WidgetText.Draw(drawList, position, text, color, style, MathF.Max(1f, maxWidth));

    public static void Pill(ImDrawListPtr drawList, Vector2 rightCenter, string label, Vector4 fill, Vector4 text,
        float scale)
    {
        var size = Typography.Measure(label, WidgetType.Eyebrow);
        var height = size.Y + 5f * scale;
        var width = size.X + 12f * scale;
        var min = new Vector2(rightCenter.X - width, rightCenter.Y - height * 0.5f);
        var max = new Vector2(rightCenter.X, rightCenter.Y + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(fill));
        Typography.Draw(drawList, new Vector2(min.X + 6f * scale, rightCenter.Y - size.Y * 0.5f), label, text,
            WidgetType.Eyebrow);
    }

    public static float PillWidth(string label, float scale) =>
        Typography.Measure(label, WidgetType.Eyebrow).X + 12f * scale;
}
