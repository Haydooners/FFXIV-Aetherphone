using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal static class VenuesArt
{
    public const float SectionHeaderHeight = 40f;
    public const float SectionGap = 22f;
    public const float HeaderGap = 6f;
    public const float CardGap = 12f;
    public const float RowHeight = 68f;
    public const float RowThumb = 48f;
    public const float RowPad = 14f;
    public const float TextGap = 12f;
    public const float LineGap = 2f;
    public const float BottomPad = 24f;
    public const float PillHeight = 30f;
    public const float PillGlyph = 14f;
    public const float PillPad = 12f;
    public const float PillGap = 8f;
    public const float StateMinHeight = 300f;

    private const float WashInset = 4f;
    private const float WashRadius = 16f;
    private const float PressedWash = 1.6f;
    private const float StateTileSize = 68f;
    private const float StateGlyphSize = 30f;
    private const float StateTitleGap = 18f;
    private const float StateHintGap = 6f;
    private const float StateActionGap = 20f;
    private const float StateActionHeight = 40f;
    private const float StateActionPad = 44f;
    private const float StateActionMinWidth = 150f;
    private const float StateMaxText = 280f;
    private const float StateTextInset = 48f;
    private const float StateLift = 40f;
    private const float DisabledAlpha = 0.45f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static float CardRadius(float scale) => Metrics.Radius.Widget * scale;

    public static void Card(ImDrawListPtr drawList, AppSkin ui, Vector2 min, Vector2 max, float scale) =>
        ui.Card(drawList, min, max, CardRadius(scale), true);

    public static float SectionHeader(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, string title,
        string trailing, out bool trailingClicked, float scale)
    {
        trailingClicked = false;
        var height = SectionHeaderHeight * scale;
        var reserve = trailing.Length > 0 ? Typography.Measure(trailing, TextStyles.Body).X + TextGap * scale : 0f;
        var fitted = Typography.FitText(title, MathF.Max(1f, width - reserve), TextStyles.Title3);
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(origin.X, origin.Y + (height - titleHeight) * 0.5f), fitted,
            ui.TitleInk, TextStyles.Title3);
        if (trailing.Length == 0)
        {
            return height;
        }

        var size = Typography.Measure(trailing, TextStyles.Body);
        var tapHeight = MathF.Max(height, Metrics.Size.TapTarget * scale);
        var hitMin = new Vector2(origin.X + width - size.X - TextGap * scale, origin.Y + (height - tapHeight) * 0.5f);
        var hitMax = new Vector2(origin.X + width, hitMin.Y + tapHeight);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var ink = hovered ? Palette.Lighten(ui.Accent, 0.15f) : ui.Accent;
        Typography.Draw(drawList, new Vector2(origin.X + width - size.X, origin.Y + (height - size.Y) * 0.5f),
            trailing, ink, TextStyles.Body);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        trailingClicked = UiInteract.Click(hitMin, hitMax, hovered);
        return height;
    }

    public static bool RowWash(ImDrawListPtr drawList, AppSkin ui, Rect row, float scale)
    {
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (!hovered)
        {
            return false;
        }

        var inset = WashInset * scale;
        var wash = ImGui.IsMouseDown(ImGuiMouseButton.Left)
            ? Palette.WithAlpha(ui.HoverTint, ui.HoverTint.W * PressedWash)
            : ui.HoverTint;
        Squircle.Fill(drawList, row.Min + new Vector2(inset, inset), row.Max - new Vector2(inset, inset),
            WashRadius * scale, ImGui.GetColorU32(wash));
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return true;
    }

    public static void Hairline(ImDrawListPtr drawList, AppSkin ui, float left, float right, float y) =>
        drawList.AddLine(new Vector2(left, y), new Vector2(right, y), ImGui.GetColorU32(ui.Hairline),
            Metrics.Stroke.Hairline);

    public static float StateHeight(Rect body, float scale) => MathF.Max(StateMinHeight * scale, body.Height * 0.8f);

    public static bool StateScreen(ImDrawListPtr drawList, AppSkin ui, Rect body, string glyph, string title,
        string hint, string actionLabel, float scale)
    {
        var centerX = body.Center.X;
        var tileSize = StateTileSize * scale;
        var tileTop = body.Center.Y - StateLift * scale - tileSize;
        var tileMin = new Vector2(centerX - tileSize * 0.5f, tileTop);
        var tileMax = new Vector2(centerX + tileSize * 0.5f, tileTop + tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        PhoneIcon.Draw(drawList, (tileMin + tileMax) * 0.5f, glyph, AccentRing.Ink, StateGlyphSize * scale);
        var maxWidth = MathF.Min(body.Width - StateTextInset * scale, StateMaxText * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + StateTitleGap * scale), maxWidth);
        var bottom = hint.Length > 0
            ? Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
                new Vector2(centerX, titleBottom + StateHintGap * scale), maxWidth)
            : titleBottom;
        if (actionLabel.Length == 0)
        {
            return false;
        }

        var natural = Typography.Measure(actionLabel, TextStyles.SubheadlineEmphasized).X + StateActionPad * scale;
        var width = Math.Clamp(natural, StateActionMinWidth * scale, MathF.Max(StateActionMinWidth * scale, maxWidth));
        var top = bottom + StateActionGap * scale;
        var rect = new Rect(new Vector2(centerX - width * 0.5f, top),
            new Vector2(centerX + width * 0.5f, top + StateActionHeight * scale));
        return PillButton(drawList, ui, rect, ImGui.GetID("venues.state.action"), actionLabel, string.Empty, true);
    }

    public static bool PillButton(ImDrawListPtr drawList, AppSkin ui, Rect rect, uint key, string label,
        string glyph, bool filled, bool enabled = true)
    {
        var scale = UiScale.Current;
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(key, down, PressFx.ControlPressedScale);
        var half = new Vector2(rect.Width, rect.Height) * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var radius = (max.Y - min.Y) * 0.5f;
        var accent = ui.Accent;
        var fill = filled
            ? hovered ? Palette.Mix(accent, White, 0.12f) : accent
            : hovered ? Palette.Mix(ui.FieldSurface, White, 0.06f) : ui.FieldSurface;
        if (!enabled)
        {
            fill = Palette.WithAlpha(fill, fill.W * DisabledAlpha);
        }

        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(fill));
        var ink = filled ? AccentRing.Ink : ui.TitleInk;
        if (!enabled)
        {
            ink = Palette.WithAlpha(ink, DisabledAlpha);
        }

        var style = TextStyles.SubheadlineEmphasized;
        var glyphSize = glyph.Length > 0 ? PillGlyph * scale : 0f;
        var glyphGap = glyph.Length > 0 ? 6f * scale : 0f;
        var labelMax = MathF.Max(1f, max.X - min.X - radius - glyphSize - glyphGap);
        var fitted = Typography.FitText(label, labelMax, style);
        var labelSize = Typography.Measure(fitted, style);
        var contentLeft = (min.X + max.X - glyphSize - glyphGap - labelSize.X) * 0.5f;
        var centerY = (min.Y + max.Y) * 0.5f;
        if (glyphSize > 0f)
        {
            PhoneIcon.Draw(drawList, new Vector2(contentLeft + glyphSize * 0.5f, centerY), glyph, ink, glyphSize);
        }

        Typography.Draw(drawList, new Vector2(contentLeft + glyphSize + glyphGap, centerY - labelSize.Y * 0.5f),
            fitted, ink, style);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static float GlassPillWidth(string label, string glyph, bool chevron, float scale)
    {
        var width = Typography.Measure(label, TextStyles.FootnoteEmphasized).X + PillPad * 2f * scale;
        if (glyph.Length > 0)
        {
            width += (PillGlyph + 6f) * scale;
        }

        return chevron ? width + (PillGlyph + 2f) * scale : width;
    }

    public static bool GlassPill(ImDrawListPtr drawList, AppSkin ui, Rect rect, uint key, string label, string glyph,
        bool chevron, bool accent)
    {
        var scale = UiScale.Current;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(key, down, PressFx.ControlPressedScale);
        var half = new Vector2(rect.Width, rect.Height) * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var radius = (max.Y - min.Y) * 0.5f;
        if (accent)
        {
            Material.AccentGlass(drawList, min, max, radius, scale, ui.Accent);
        }
        else
        {
            Material.ThemedGlass(drawList, min, max, radius, scale, ui.Theme);
        }

        var ink = accent ? AccentRing.Ink : ui.TitleInk;
        var style = TextStyles.FootnoteEmphasized;
        var centerY = (min.Y + max.Y) * 0.5f;
        var left = min.X + PillPad * scale;
        if (glyph.Length > 0)
        {
            PhoneIcon.Draw(drawList, new Vector2(left + PillGlyph * scale * 0.5f, centerY), glyph,
                accent ? ink : ui.Accent, PillGlyph * scale);
            left += (PillGlyph + 6f) * scale;
        }

        var right = max.X - PillPad * scale - (chevron ? (PillGlyph + 2f) * scale : 0f);
        var fitted = Typography.FitText(label, MathF.Max(1f, right - left), style);
        var labelHeight = Typography.LineHeight(style);
        Typography.Draw(drawList, new Vector2(left, centerY - labelHeight * 0.5f), fitted, ink, style);
        if (chevron)
        {
            PhoneIcon.Draw(drawList, new Vector2(max.X - PillPad * scale - PillGlyph * scale * 0.5f, centerY),
                PhoneIcons.ChevronDown, Palette.WithAlpha(ink, 0.8f), PillGlyph * 0.85f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static void ReserveTo(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(1f, bottom - origin.Y)));
    }
}
