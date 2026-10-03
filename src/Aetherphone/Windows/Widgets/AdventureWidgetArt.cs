using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Home;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;

namespace Aetherphone.Windows.Widgets;

internal struct RefreshGate
{
    private long dueTick;

    public bool Due(int intervalMilliseconds)
    {
        var now = Environment.TickCount64;
        if (now < dueTick)
        {
            return false;
        }

        dueTick = now + intervalMilliseconds;
        return true;
    }

    public void Expire() => dueTick = 0;
}

internal struct EasedFraction
{
    private Spring spring;
    private bool primed;

    public float Step(float target, float deltaSeconds)
    {
        var clamped = Math.Clamp(target, 0f, 1f);
        if (!primed)
        {
            primed = true;
            spring.SnapTo(clamped);
            return clamped;
        }

        return Math.Clamp(spring.Step(clamped, Motion.Appear, deltaSeconds), 0f, 1f);
    }
}

internal static class AdventureWidgetArt
{
    private const float MessageGlyphUnits = 30f;
    private const float MessageIconFraction = 0.46f;
    private const int MessageMaxLines = 3;
    private const float RingThicknessFactor = 0.15f;
    private const float RingThicknessMinimumUnits = 3f;
    private const float RingTrackAlpha = 0.2f;

    public static bool IsLoggedIn => Plugin.ClientState.IsLoggedIn;

    public static float LineHeight(in TextStyle style) => Typography.Measure("Ag", style).Y;

    public static void Message(in WidgetContext context, in WidgetInk ink, float top, FontAwesomeIcon icon,
        string text) =>
        Message(context, ink, top, icon, text, string.Empty);

    public static void Message(in WidgetContext context, in WidgetInk ink, float top, FontAwesomeIcon icon,
        string text, string detail)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var drawList = context.DrawList;
        var glyph = MessageGlyphUnits * scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var rowGap = WidgetMetrics.RowGap * scale;
        var detailHeight = detail.Length > 0 ? LineHeight(WidgetType.Caption) + rowGap : 0f;
        if (context.Size == WidgetSize.Small)
        {
            var textHeight = WrappedHeight(text, WidgetType.Body, content.Width, MessageMaxLines);
            var textTop = content.Max.Y - textHeight - detailHeight;
            var glyphCenter = new Vector2(content.Min.X + glyph * 0.5f,
                MathF.Max(top + gutter + glyph * 0.5f, textTop - gutter - glyph * 0.5f));
            MessageGlyph(drawList, ink, glyphCenter, glyph, icon);
            Wrapped(drawList, new Vector2(content.Min.X, textTop), text, ink.Secondary, WidgetType.Body,
                content.Width, MessageMaxLines);
            if (detail.Length > 0)
            {
                WidgetText.Draw(drawList, new Vector2(content.Min.X, textTop + textHeight + rowGap), detail,
                    ink.Tertiary, WidgetType.Caption, content.Width);
            }

            return;
        }

        var textLeft = content.Min.X + glyph + gutter * 1.5f;
        var textWidth = MathF.Max(1f, content.Max.X - textLeft);
        var bodyHeight = WrappedHeight(text, WidgetType.Body, textWidth, MessageMaxLines);
        var blockHeight = bodyHeight + detailHeight;
        var centerY = (top + content.Max.Y) * 0.5f;
        var blockTop = centerY - blockHeight * 0.5f;
        MessageGlyph(drawList, ink, new Vector2(content.Min.X + glyph * 0.5f, centerY), glyph, icon);
        Wrapped(drawList, new Vector2(textLeft, blockTop), text, ink.Secondary, WidgetType.Body, textWidth,
            MessageMaxLines);
        if (detail.Length > 0)
        {
            WidgetText.Draw(drawList, new Vector2(textLeft, blockTop + bodyHeight + rowGap), detail, ink.Tertiary,
                WidgetType.Caption, textWidth);
        }
    }

    public static float Wrapped(ImDrawListPtr drawList, Vector2 topLeft, string text, Vector4 color,
        in TextStyle style, float maxWidth, int maxLines)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        var lines = Typography.WrapText(text, style, MathF.Max(1f, maxWidth));
        var lineHeight = LineHeight(style);
        var count = Math.Min(lines.Length, maxLines);
        for (var lineIndex = 0; lineIndex < count; lineIndex++)
        {
            var line = lineIndex == count - 1 && lines.Length > count
                ? Typography.FitText(lines[lineIndex], maxWidth * 0.9f, style)
                : lines[lineIndex];
            Typography.Draw(drawList, new Vector2(topLeft.X, topLeft.Y + lineIndex * lineHeight), line, color, style);
        }

        return count * lineHeight;
    }

    public static float WrappedHeight(string text, in TextStyle style, float maxWidth, int maxLines)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        var lines = Typography.WrapText(text, style, MathF.Max(1f, maxWidth));
        return Math.Min(lines.Length, maxLines) * LineHeight(style);
    }

    public static void RightAligned(ImDrawListPtr drawList, float right, float top, string text, Vector4 color,
        in TextStyle style)
    {
        var width = Typography.Measure(text, style).X;
        Typography.Draw(drawList, new Vector2(right - width, top), text, color, style);
    }

    public static float TabularRight(ImDrawListPtr drawList, float right, float top, string text, Vector4 color,
        in TextStyle style)
    {
        var width = WidgetText.TabularWidth(text, style);
        WidgetText.Tabular(drawList, new Vector2(right - width, top), text, color, style);
        return width;
    }

    public static bool GameIcon(ImDrawListPtr drawList, uint iconId, Vector2 min, Vector2 max, float radius,
        in WidgetInk ink)
    {
        if (iconId == 0 || ink.Opacity <= 0f)
        {
            return false;
        }

        var texture = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
        if (texture.Handle == 0)
        {
            return false;
        }

        drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One,
            ImGui.GetColorU32(ink.ImageTint), radius);
        return true;
    }

    public static void Bar(ImDrawListPtr drawList, Rect rect, float fraction, Vector4 track, Vector4 fill)
    {
        var radius = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(track));
        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped <= 0f)
        {
            return;
        }

        var width = MathF.Max(rect.Height, rect.Width * clamped);
        Squircle.Fill(drawList, rect.Min, new Vector2(rect.Min.X + width, rect.Max.Y), radius,
            ImGui.GetColorU32(fill));
    }

    public static void Ring(in WidgetContext context, in WidgetInk ink, Vector2 center, float radius, float fraction,
        Vector4 accent)
    {
        var thickness = MathF.Max(RingThicknessMinimumUnits * context.Scale, radius * RingThicknessFactor);
        var tint = ink.Accent(accent);
        ProgressRing.Track(context.DrawList, center, radius, thickness, tint with { W = tint.W * RingTrackAlpha });
        if (fraction <= 0f)
        {
            return;
        }

        ProgressRing.Fill(context.DrawList, center, radius, thickness, Math.Clamp(fraction, 0f, 1f), tint);
    }

    public static float RingInnerRadius(in WidgetContext context, float radius) =>
        radius - MathF.Max(RingThicknessMinimumUnits * context.Scale, radius * RingThicknessFactor);

    public static void Mark(ImDrawListPtr drawList, in WidgetInk ink, Vector2 center, float diameter, bool done,
        Vector4 accent, float scale)
    {
        var radius = diameter * 0.5f;
        if (done)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ink.Accent(accent)), 32);
            ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Check, ink.OnAccent, diameter * 0.46f);
            return;
        }

        drawList.AddCircle(center, radius - 0.75f * scale, ImGui.GetColorU32(ink.Tertiary), 32, 1.5f * scale);
    }

    public static void IconBadge(ImDrawListPtr drawList, in WidgetInk ink, Vector2 center, float diameter,
        FontAwesomeIcon icon, Vector4 accent)
    {
        var tint = ink.Accent(accent);
        drawList.AddCircleFilled(center, diameter * 0.5f, ImGui.GetColorU32(tint with { W = tint.W * 0.2f }), 32);
        ProgressRing.CenterIcon(drawList, center, icon, tint, diameter * 0.5f);
    }

    public static void Placeholder(in WidgetContext context, in WidgetInk ink, Rect area, int rows)
    {
        if (rows <= 0)
        {
            return;
        }

        var scale = context.Scale;
        var rowHeight = area.Height / rows;
        var barHeight = MathF.Min(10f * scale, rowHeight * 0.32f);
        for (var rowIndex = 0; rowIndex < rows; rowIndex++)
        {
            var top = area.Min.Y + rowIndex * rowHeight + (rowHeight - barHeight * 2f - 4f * scale) * 0.5f;
            var titleWidth = area.Width * (rowIndex % 2 == 0 ? 0.62f : 0.48f);
            WidgetChrome.Redacted(context.DrawList,
                new Rect(new Vector2(area.Min.X, top), new Vector2(area.Min.X + titleWidth, top + barHeight)), ink);
            WidgetChrome.Redacted(context.DrawList,
                new Rect(new Vector2(area.Min.X, top + barHeight + 4f * scale),
                    new Vector2(area.Min.X + titleWidth * 0.6f, top + barHeight * 1.8f + 4f * scale)), ink);
        }
    }

    private static void MessageGlyph(ImDrawListPtr drawList, in WidgetInk ink, Vector2 center, float diameter,
        FontAwesomeIcon icon)
    {
        drawList.AddCircleFilled(center, diameter * 0.5f, ImGui.GetColorU32(ink.Fill), 32);
        ProgressRing.CenterIcon(drawList, center, icon, ink.Secondary, diameter * MessageIconFraction);
    }
}
