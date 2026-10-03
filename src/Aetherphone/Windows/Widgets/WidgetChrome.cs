using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Widgets;

internal static class WidgetChrome
{
    private const float OwnBackgroundDim = 0.35f;
    private const float OwnBackgroundVeil = 0.08f;
    private const float TintWash = 0.12f;
    private const float ClearVeil = 0.08f;
    private const float ShimmerSeconds = 1.4f;
    private const float ShimmerSpread = 0.004f;
    private const float ShimmerFloor = 0.65f;
    private static readonly Vector4 LightCard = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 DarkCard = new(28f / 255f, 28f / 255f, 30f / 255f, 1f);
    private static readonly Vector4 NearBlackCard = new(20f / 255f, 20f / 255f, 22f / 255f, 1f);

    public static float Radius(float scale) => WidgetMetrics.ContainerRadius(scale);

    public static void Container(in WidgetContext context)
    {
        var drawList = context.DrawList;
        var bounds = context.Bounds;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var radius = Radius(scale);
        switch (context.Mode)
        {
            case WidgetMode.Tinted:
                Material.LiquidGlass(drawList, bounds.Min, bounds.Max, radius, scale, GlassTone.Dark, 0f, opacity);
                Squircle.Fill(drawList, bounds.Min, bounds.Max, radius,
                    ImGui.GetColorU32(context.Tint with { W = TintWash * opacity }));
                return;
            case WidgetMode.Clear:
                Material.LiquidGlass(drawList, bounds.Min, bounds.Max, radius, scale, GlassTone.Light,
                    AppIconCache.GlassBrightness, opacity);
                Material.Veil(drawList, bounds.Min, bounds.Max, ClearVeil * opacity, radius);
                return;
            case WidgetMode.Dark:
                Opaque(drawList, bounds, radius, scale, NearBlackCard, opacity);
                return;
            default:
                Opaque(drawList, bounds, radius, scale, WidgetInk.IsLightTheme(context.Theme) ? LightCard : DarkCard,
                    opacity);
                return;
        }
    }

    public static void Container(in WidgetContext context, Vector4 top, Vector4 bottom)
    {
        if (context.Mode is WidgetMode.Tinted or WidgetMode.Clear)
        {
            Container(context);
            return;
        }

        var drawList = context.DrawList;
        var bounds = context.Bounds;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var radius = Radius(scale);
        if (context.Mode == WidgetMode.Dark)
        {
            top = Dimmed(top);
            bottom = Dimmed(bottom);
        }

        Squircle.FillVerticalGradient(drawList, bounds.Min, bounds.Max, radius,
            ImGui.GetColorU32(top with { W = top.W * opacity }),
            ImGui.GetColorU32(bottom with { W = bottom.W * opacity }));
        Material.Veil(drawList, bounds.Min, bounds.Max, OwnBackgroundVeil * opacity, radius);
        Material.EdgeSquircle(drawList, bounds.Min, bounds.Max, radius, scale, opacity);
    }

    public static void Edge(in WidgetContext context) =>
        Material.EdgeSquircle(context.DrawList, context.Bounds.Min, context.Bounds.Max, Radius(context.Scale),
            context.Scale, context.Opacity);

    public static float Header(in WidgetContext context, in WidgetInk ink, string appId, LocString label,
        Vector4 accent) =>
        Header(context, ink, appId, Loc.T(label), accent);

    public static float Header(in WidgetContext context, in WidgetInk ink, string appId, string label,
        Vector4 accent)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var glyphSize = WidgetMetrics.GlyphSmall * scale;
        var tint = ink.Accent(accent);
        var eyebrowHeight = WidgetText.EyebrowHeight();
        var rowHeight = MathF.Max(glyphSize, eyebrowHeight);
        var centerY = content.Min.Y + rowHeight * 0.5f;
        var left = content.Min.X;
        if (appId.Length > 0)
        {
            AppIconTile.TryDrawGlyph(context.DrawList, appId, new Vector2(left + glyphSize * 0.5f, centerY), glyphSize,
                tint);
            left += glyphSize + WidgetMetrics.Gutter * 0.5f * scale;
        }

        if (label.Length > 0)
        {
            WidgetText.EyebrowFit(context.DrawList, new Vector2(left, centerY - eyebrowHeight * 0.5f), label,
                MathF.Max(1f, content.Max.X - left), tint, scale);
        }

        return content.Min.Y + rowHeight;
    }

    public static void Redacted(ImDrawListPtr drawList, Rect rect, in WidgetInk ink)
    {
        if (ink.Opacity <= 0f || rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        var phase = (float)(ImGui.GetTime() / ShimmerSeconds) - rect.Min.X * ShimmerSpread;
        var wave = 0.5f + 0.5f * MathF.Cos(phase * MathF.Tau);
        var alpha = ink.Fill.W * (ShimmerFloor + (1f - ShimmerFloor) * wave);
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(ink.Fill with { W = alpha }));
    }

    private static void Opaque(ImDrawListPtr drawList, Rect bounds, float radius, float scale, Vector4 fill,
        float opacity)
    {
        Squircle.Fill(drawList, bounds.Min, bounds.Max, radius, ImGui.GetColorU32(fill with { W = opacity }));
        Material.EdgeSquircle(drawList, bounds.Min, bounds.Max, radius, scale, opacity);
    }

    private static Vector4 Dimmed(Vector4 color) =>
        new(color.X * OwnBackgroundDim, color.Y * OwnBackgroundDim, color.Z * OwnBackgroundDim, color.W);
}
