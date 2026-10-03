using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct TabItem(string Label, string Glyph, string ActiveGlyph = "", int Badge = 0,
    string? AnchorKey = null, bool CustomIcon = false)
{
    public string GlyphFor(bool active) => active && ActiveGlyph.Length > 0 ? ActiveGlyph : Glyph;
}

internal readonly record struct TabBarAction(string Glyph, string Label, int Badge = 0, string? AnchorKey = null);

internal readonly record struct TabBarResult(int Tapped, bool ActionTapped)
{
    public static readonly TabBarResult None = new(-1, false);
}

internal readonly record struct TabItemPose(Vector2 IconCenter, float Scale, float Alpha)
{
    public float AvatarRadius(float uiScale) => TabBarLayout.AvatarRadius * uiScale * Scale;

    public float AvatarRingRadius(float uiScale) => AvatarRadius(uiScale) + TabBarLayout.AvatarRingGap * uiScale;
}

internal interface ITabIconDrawer
{
    void DrawTabIcon(ImDrawListPtr drawList, int index, TabItemPose pose, bool active);
}

internal sealed class TabBar
{
    public const float GlassOpacity = 0.86f;
    public const float FlatGlassOpacity = 0.95f;
    private const float LayerHeadroomUnits = 12f;
    private const float HighlightTint = 0.22f;
    private const float HighlightRimAlpha = 0.16f;
    private const float PressDepth = 1f - Motion.PressScaleControl;
    private const float MaxFrameSeconds = 0.1f;
    private const float BadgeScale = 0.8f;
    private const float BadgeOffsetX = 10f;
    private const float BadgeOffsetY = 9f;
    private const float CompactThreshold = 0.5f;
    private const float HiddenAlpha = 0.02f;
    private const float ClipCornerFraction = 0.5f;
    private static readonly Vector4 MutedOnDarkGlass = new(0.93f, 0.94f, 0.97f, 0.70f);
    private static readonly Vector4 StrongOnDarkGlass = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 HighlightRim = new(1f, 1f, 1f, 1f);

    private Spring[] hover = Array.Empty<Spring>();
    private Spring[] press = Array.Empty<Spring>();
    private TabItemPose[] poses = Array.Empty<TabItemPose>();
    private TabBarShrink shrink;
    private Spring highlightX;
    private Spring actionHover;
    private Spring actionPress;
    private bool highlightSettled;
    private int lastFrame = -2;
    private int lastActive = -1;

    public Rect Bounds { get; private set; }

    public float Shrink => shrink.Amount;

    public static float ContentInset(float scale) => TabBarLayout.ContentInset(scale);

    private static float CurrentGlassOpacity => WallpaperBackdrop.Available ? GlassOpacity : FlatGlassOpacity;

    private static bool BarHover(Vector2 min, Vector2 max) =>
        !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(min, max);

    public static Rect ContentArea(Rect area, float scale) => TabBarLayout.ContentArea(area, scale);

    public static Rect Zone(Rect area, float scale) => TabBarLayout.Zone(area, scale);

    public static AppSurface.BottomInsetScope ReserveContent(float scale) =>
        AppSurface.ReserveBottom(ContentInset(scale));

    public TabItemPose Pose(int index) => index >= 0 && index < poses.Length ? poses[index] : default;

    public TabBarResult Draw(Rect area, AppSkin ui, ReadOnlySpan<TabItem> items, int active,
        TabBarAction? action = null, ITabIconDrawer? icons = null) =>
        Draw(area, ui, items, active, AppSurface.ScrollOffsetThisFrame, action, icons);

    public TabBarResult Draw(Rect area, AppSkin ui, ReadOnlySpan<TabItem> items, int active, float scrollOffset,
        TabBarAction? action = null, ITabIconDrawer? icons = null)
    {
        if (items.Length == 0)
        {
            return TabBarResult.None;
        }

        EnsureCapacity(items.Length);
        var scale = UiScale.Current;
        var frame = ImGui.GetFrameCount();
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        SyncVisit(frame, active);
        shrink.Observe(scrollOffset, scale);
        var amount = Math.Clamp(shrink.Step(delta), 0f, 1f);

        var hasAction = action.HasValue;
        var activeIndex = Math.Clamp(active, 0, items.Length - 1);
        var full = TabBarLayout.FullCapsule(area, scale, hasAction);
        var activeLabelWidth = Typography.Measure(items[activeIndex].Label, TextStyles.Caption2).X;
        var compact = TabBarLayout.CompactCapsule(full, TabBarLayout.CompactWidth(activeLabelWidth, scale), hasAction);
        var capsule = TabBarLayout.Capsule(full, compact, amount);
        Bounds = capsule;

        var theme = ui.Theme;
        var backdrop = ui.BackdropColor;
        var tone = Material.ToneFor(backdrop);
        var zone = TabBarLayout.Zone(area, scale);
        var layerRect = new Rect(new Vector2(zone.Min.X, zone.Min.Y - LayerHeadroomUnits * scale), zone.Max);
        using var layer = ScreenLayer.Begin("tabbar", layerRect, false);
        UiInteract.HoverOverlay(layerRect);
        var drawList = ImGui.GetWindowDrawList();
        var radius = capsule.Height * 0.5f;
        Material.ThemedGlass(drawList, capsule.Min, capsule.Max, radius, scale, backdrop, CurrentGlassOpacity);

        var activeCell = TabBarLayout.Cell(full, items.Length, activeIndex, scale);
        StepHighlight(activeCell.Center.X, delta);
        DrawHighlight(drawList, activeCell, ui.Accent, 1f - amount, Math.Clamp(press[activeIndex].Value, 0f, 1f),
            scale);

        var compactMode = amount > CompactThreshold;
        var compactIconCenter = TabBarLayout.IconCenter(compact, scale);
        var compactLabelCenter = TabBarLayout.LabelCenter(compact, scale);
        var inactiveInk = tone == GlassTone.Dark ? MutedOnDarkGlass : theme.TextMuted;
        var labelPadding = TabBarLayout.LabelSidePadding * 2f * scale;
        var cornerClip = radius * ClipCornerFraction;
        var result = TabBarResult.None;
        drawList.PushClipRect(new Vector2(capsule.Min.X + cornerClip, capsule.Min.Y),
            new Vector2(capsule.Max.X - cornerClip, capsule.Max.Y), true);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var cell = TabBarLayout.Cell(full, items.Length, index, scale);
            var isActive = index == activeIndex;
            var iconCenter = Vector2.Lerp(TabBarLayout.IconCenter(cell, scale), compactIconCenter, amount);
            var labelCenter = Vector2.Lerp(TabBarLayout.LabelCenter(cell, scale), compactLabelCenter, amount);
            var fade = 1f - amount;
            var alpha = isActive ? 1f : fade * fade;
            var hovered = !compactMode && BarHover(cell.Min, cell.Max);
            hover[index].Step(hovered ? 1f : 0f, Motion.HoverLift, delta);
            var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            press[index].Step(pressed ? 1f : 0f, pressed ? Motion.PressIn : Motion.Release, delta);
            var itemScale = (1f - PressDepth * Math.Clamp(press[index].Value, 0f, 1f))
                            * (1f + Motion.HoverLiftIcon * Math.Clamp(hover[index].Value, 0f, 1f));
            poses[index] = new TabItemPose(iconCenter, itemScale, alpha);
            if (item.AnchorKey is { } anchorKey)
            {
                UiAnchors.Report(anchorKey, cell);
            }

            if (alpha > HiddenAlpha)
            {
                var baseInk = isActive ? ui.Accent : inactiveInk;
                var ink = Palette.WithAlpha(baseInk, baseInk.W * alpha);
                if (item.CustomIcon && icons is not null)
                {
                    icons.DrawTabIcon(drawList, index, poses[index], isActive);
                }
                else
                {
                    PhoneIcon.Draw(drawList, iconCenter, item.GlyphFor(isActive), ink,
                        TabBarLayout.IconSize * scale * itemScale);
                }

                var labelLimit = isActive
                    ? MathF.Max(1f, cell.Width + (compact.Width - cell.Width) * amount - labelPadding)
                    : MathF.Max(1f, cell.Width - labelPadding);
                var label = Typography.FitText(item.Label, labelLimit, TextStyles.Caption2);
                Typography.DrawCentered(drawList, labelCenter, label, ink, TextStyles.Caption2);
                if (item.Badge > 0 && alpha > CompactThreshold)
                {
                    AppBadge.Draw(iconCenter + new Vector2(BadgeOffsetX, -BadgeOffsetY) * scale, item.Badge, theme,
                        scale * BadgeScale);
                }
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (!compactMode && UiInteract.Click(cell.Min, cell.Max, hovered))
            {
                result = new TabBarResult(index, false);
            }
        }

        drawList.PopClipRect();
        if (compactMode)
        {
            var capsuleHovered = BarHover(capsule.Min, capsule.Max);
            if (capsuleHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(capsule.Min, capsule.Max, capsuleHovered))
            {
                shrink.Expand();
            }
        }

        if (action is { } trailing && DrawAction(drawList, area, ui, trailing, tone, backdrop, delta, scale))
        {
            result = new TabBarResult(result.Tapped, true);
        }

        return result;
    }

    private void SyncVisit(int frame, int active)
    {
        var resumed = frame - lastFrame > 1;
        if (resumed)
        {
            shrink.Reset();
            highlightSettled = false;
            actionHover.SnapTo(0f);
            actionPress.SnapTo(0f);
            for (var index = 0; index < hover.Length; index++)
            {
                hover[index].SnapTo(0f);
                press[index].SnapTo(0f);
            }
        }
        else if (active != lastActive)
        {
            shrink.Reset();
        }

        lastFrame = frame;
        lastActive = active;
    }

    private void EnsureCapacity(int count)
    {
        if (hover.Length == count)
        {
            return;
        }

        hover = new Spring[count];
        press = new Spring[count];
        poses = new TabItemPose[count];
    }

    private void StepHighlight(float targetX, float delta)
    {
        if (!highlightSettled)
        {
            highlightX.SnapTo(targetX);
            highlightSettled = true;
            return;
        }

        highlightX.Step(targetX, Motion.TabBar, delta);
    }

    private void DrawHighlight(ImDrawListPtr drawList, Rect activeCell, Vector4 accent, float alpha, float pressAmount,
        float scale)
    {
        if (alpha <= HiddenAlpha)
        {
            return;
        }

        var highlight = TabBarLayout.Highlight(activeCell, scale)
            .Translate(new Vector2(highlightX.Value - activeCell.Center.X, 0f));
        var half = highlight.Size * 0.5f * (1f - PressDepth * pressAmount);
        var min = highlight.Center - half;
        var max = highlight.Center + half;
        Squircle.Fill(drawList, min, max, half.Y, ImGui.GetColorU32(Palette.WithAlpha(accent, HighlightTint * alpha)));
        Squircle.Stroke(drawList, min, max, half.Y,
            ImGui.GetColorU32(Palette.WithAlpha(HighlightRim, HighlightRimAlpha * alpha)), 1f * scale);
    }

    private bool DrawAction(ImDrawListPtr drawList, Rect area, AppSkin ui, in TabBarAction action, GlassTone tone,
        Vector4 backdrop, float delta, float scale)
    {
        var circle = TabBarLayout.ActionCircle(area, scale);
        var hovered = BarHover(circle.Min, circle.Max);
        actionHover.Step(hovered ? 1f : 0f, Motion.HoverLift, delta);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        actionPress.Step(pressed ? 1f : 0f, pressed ? Motion.PressIn : Motion.Release, delta);
        var grow = (1f - PressDepth * Math.Clamp(actionPress.Value, 0f, 1f))
                   * (1f + Motion.HoverLiftIcon * Math.Clamp(actionHover.Value, 0f, 1f));
        var half = circle.Size * 0.5f * grow;
        var min = circle.Center - half;
        var max = circle.Center + half;
        Material.ThemedGlass(drawList, min, max, half.Y, scale, backdrop, CurrentGlassOpacity);
        var ink = tone == GlassTone.Dark ? StrongOnDarkGlass : ui.Theme.TextStrong;
        PhoneIcon.Draw(drawList, circle.Center, action.Glyph, ink, TabBarLayout.IconSize * scale * grow);
        if (action.Badge > 0)
        {
            AppBadge.Draw(circle.Center + new Vector2(BadgeOffsetX, -BadgeOffsetY) * scale, action.Badge, ui.Theme,
                scale * BadgeScale);
        }

        if (action.AnchorKey is { } anchorKey)
        {
            UiAnchors.Report(anchorKey, circle);
        }

        HoverTooltip.Show(circle, action.Label, HoverLabelSide.Above);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(circle.Min, circle.Max, hovered);
    }
}
