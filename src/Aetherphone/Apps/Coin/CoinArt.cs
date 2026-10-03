using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal enum CapsuleTone : byte
{
    Filled,
    Tinted,
    Quiet,
}

internal static class CoinArt
{
    public const float CardRadius = Metrics.Radius.Widget;
    public const float RowHeight = 62f;
    public const float SectionHeaderHeight = 40f;
    public const float SectionGap = 22f;
    public const float HeaderGap = 6f;
    public const float BottomPad = 28f;
    public const float RowTile = 36f;
    public const float TextGap = 12f;
    public const float ValueGap = 10f;
    public const float LineGap = 2f;
    public const float CapsuleHeight = 32f;
    public const float LargeCapsuleHeight = 46f;

    public static readonly Vector4 GainInk = new(0.30f, 0.84f, 0.46f, 1f);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private const float WashInset = 4f;
    private const float WashRadius = 16f;
    private const float PressedWash = 1.6f;
    private const float TintedAlpha = 0.16f;
    private const float TintedHoverAlpha = 0.24f;
    private const float QuietAlpha = 0.08f;
    private const float DisabledAlpha = 0.45f;
    private const float HoverDarken = 0.12f;
    private const float StateTileSize = 48f;
    private const float StateGlyphSize = 22f;
    private const float StateGap = 14f;
    private const float StateLineGap = 4f;
    private const float ScreenTileSize = 68f;
    private const float ScreenGlyphSize = 30f;
    private const float ScreenTitleGap = 18f;
    private const float ScreenHintGap = 6f;
    private const float ScreenActionGap = 22f;
    private const float ScreenMaxText = 280f;
    private const float ScreenTextInset = 48f;
    private const float ScreenLift = 36f;
    private const float ScreenActionWidth = 180f;

    public static void Card(ImDrawListPtr drawList, AppSkin ui, Vector2 min, Vector2 max, float scale) =>
        ui.Card(drawList, min, max, CardRadius * scale, true);

    public static void Reserve(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y)));
    }

    public static float SectionHeader(ImDrawListPtr drawList, Vector2 origin, float width, string title, Vector4 ink,
        float trailingReserve, float scale)
    {
        var height = SectionHeaderHeight * scale;
        var fitted = Typography.FitText(title, MathF.Max(1f, width - trailingReserve), TextStyles.Title3);
        var size = Typography.Measure(fitted, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(origin.X, origin.Y + (height - size.Y) * 0.5f), fitted, ink,
            TextStyles.Title3);
        return height;
    }

    public static float SectionTitle(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, string title,
        string trailing, out bool trailingClicked, float scale)
    {
        trailingClicked = false;
        var reserve = trailing.Length > 0
            ? Typography.Measure(trailing, TextStyles.Body).X + ValueGap * scale
            : 0f;
        var height = SectionHeader(drawList, origin, width, title, ui.TitleInk, reserve, scale);
        if (trailing.Length == 0)
        {
            return height;
        }

        var size = Typography.Measure(trailing, TextStyles.Body);
        var tapHeight = MathF.Max(height, Metrics.Size.TapTarget * scale);
        var hitMin = new Vector2(origin.X + width - size.X - ValueGap * scale, origin.Y + (height - tapHeight) * 0.5f);
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

    public static bool RowInteraction(ImDrawListPtr drawList, AppSkin ui, Rect row, float scale)
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

    public static void Labels(ImDrawListPtr drawList, float left, float right, float centerY, string title,
        string subtitle, Vector4 titleInk, Vector4 subtitleInk, float scale)
    {
        var width = MathF.Max(1f, right - left);
        var fittedTitle = Typography.FitText(title, width, TextStyles.BodyEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        if (subtitle.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(left, centerY - titleHeight * 0.5f), fittedTitle, titleInk,
                TextStyles.BodyEmphasized);
            return;
        }

        var fittedSubtitle = Typography.FitText(subtitle, width, TextStyles.Footnote);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = centerY - (titleHeight + LineGap * scale + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), fittedTitle, titleInk, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(left, top + titleHeight + LineGap * scale), fittedSubtitle,
            subtitleInk, TextStyles.Footnote);
    }

    public static float Value(ImDrawListPtr drawList, float right, float centerY, string value, Vector4 ink)
    {
        if (value.Length == 0)
        {
            return 0f;
        }

        var width = WidgetText.TabularWidth(value, TextStyles.Headline);
        var height = Typography.LineHeight(TextStyles.Headline);
        WidgetText.Tabular(drawList, new Vector2(right - width, centerY - height * 0.5f), value, ink,
            TextStyles.Headline);
        return width;
    }

    public static float Price(ImDrawListPtr drawList, Vector2 topLeft, string amount, Vector4 ink, in TextStyle style)
    {
        var lineHeight = Typography.LineHeight(style);
        var glyph = lineHeight * CurrencyGlyph.GlyphFraction;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, new Vector2(topLeft.X + glyph * 0.5f,
            topLeft.Y + lineHeight * 0.5f), glyph);
        var reserve = CurrencyGlyph.Reserve(lineHeight);
        var width = WidgetText.Tabular(drawList, new Vector2(topLeft.X + reserve, topLeft.Y), amount, ink, style);
        return reserve + width;
    }

    public static float PriceWidth(string amount, in TextStyle style) =>
        CurrencyGlyph.Reserve(Typography.LineHeight(style)) + WidgetText.TabularWidth(amount, style);

    public static void Bar(ImDrawListPtr drawList, Vector2 min, Vector2 max, float fraction, Vector4 track,
        Vector4 fill)
    {
        var radius = (max.Y - min.Y) * 0.5f;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(track), radius);
        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped <= 0f)
        {
            return;
        }

        var width = MathF.Max(max.Y - min.Y, (max.X - min.X) * clamped);
        drawList.AddRectFilled(min, new Vector2(min.X + width, max.Y), ImGui.GetColorU32(fill), radius);
    }

    public static bool Capsule(ImDrawListPtr drawList, AppSkin ui, uint id, Rect rect, string label,
        CapsuleTone tone, bool enabled, in TextStyle style)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var factor = PressFx.Scale(id, pressed, Motion.PressScaleControl);
        var half = rect.Size * 0.5f * factor;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var radius = (max.Y - min.Y) * 0.5f;
        var accent = ui.Accent;
        Vector4 fill;
        Vector4 ink;
        switch (tone)
        {
            case CapsuleTone.Filled:
                fill = hovered ? Palette.Mix(accent, new Vector4(0f, 0f, 0f, 1f), HoverDarken) : accent;
                ink = White;
                break;
            case CapsuleTone.Tinted:
                fill = Palette.WithAlpha(accent, hovered ? TintedHoverAlpha : TintedAlpha);
                ink = accent;
                break;
            default:
                fill = Palette.WithAlpha(ui.TitleInk, hovered ? QuietAlpha * 1.5f : QuietAlpha);
                ink = ui.TitleInk;
                break;
        }

        if (!enabled)
        {
            fill = Palette.WithAlpha(fill, fill.W * DisabledAlpha);
            ink = Palette.WithAlpha(ink, DisabledAlpha + 0.15f);
        }

        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(fill));
        var fitted = Typography.FitText(label, MathF.Max(1f, (max.X - min.X) - radius), style);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, fitted, ink, style);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static bool PriceCapsule(ImDrawListPtr drawList, AppSkin ui, uint id, Rect rect, string amount,
        bool enabled, in TextStyle style)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var factor = PressFx.Scale(id, pressed, Motion.PressScaleControl);
        var half = rect.Size * 0.5f * factor;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var fill = Palette.WithAlpha(ui.Accent, hovered ? TintedHoverAlpha : TintedAlpha);
        var ink = ui.Accent;
        if (!enabled)
        {
            fill = Palette.WithAlpha(fill, fill.W * DisabledAlpha);
            ink = Palette.WithAlpha(ink, DisabledAlpha + 0.15f);
        }

        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f, ImGui.GetColorU32(fill));
        var width = PriceWidth(amount, style);
        var lineHeight = Typography.LineHeight(style);
        Price(drawList, new Vector2((min.X + max.X - width) * 0.5f, (min.Y + max.Y - lineHeight) * 0.5f), amount, ink,
            style);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static float CapsuleWidth(string label, float height, in TextStyle style) =>
        Typography.Measure(label, style).X + height * 1.1f;

    public static float PanelHeight(string title, string body, float width, float scale)
    {
        var textWidth = PanelTextWidth(width, scale);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Headline, textWidth).Y;
        var bodyHeight = body.Length == 0 ? 0f : Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, textWidth).Y;
        return MathF.Max(StateTileSize * scale, titleHeight + StateLineGap * scale + bodyHeight) +
               Metrics.Space.Lg * 2f * scale;
    }

    public static void Panel(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, float height,
        FontAwesomeIcon icon, Vector4 tint, string title, string body, float scale)
    {
        Card(drawList, ui, origin, new Vector2(origin.X + width, origin.Y + height), scale);
        var pad = Metrics.Space.Lg * scale;
        var tileSize = StateTileSize * scale;
        var tileMin = new Vector2(origin.X + pad, origin.Y + pad);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor, IconTile.Surface(tint));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, StateGlyphSize * scale);
        var textLeft = tileMax.X + StateGap * scale;
        var textWidth = PanelTextWidth(width, scale);
        var titleHeight = Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad), title, ui.TitleInk,
            TextStyles.Headline, textWidth);
        if (body.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad + titleHeight + StateLineGap * scale),
                body, ui.MutedInk, TextStyles.Subheadline, textWidth);
        }
    }

    public static float DrawPanel(AppSkin ui, Vector2 origin, float width, FontAwesomeIcon icon, Vector4 tint,
        string title, string body, float scale)
    {
        var height = PanelHeight(title, body, width, scale);
        Panel(ImGui.GetWindowDrawList(), ui, origin, width, height, icon, tint, title, body, scale);
        return origin.Y + height;
    }

    public static bool StateScreen(ImDrawListPtr drawList, AppSkin ui, Rect body, FontAwesomeIcon icon, string title,
        string hint, string action, uint actionId, float scale)
    {
        var centerX = body.Center.X;
        var tileSize = ScreenTileSize * scale;
        var tileTop = body.Center.Y - ScreenLift * scale - tileSize;
        var tileMin = new Vector2(centerX - tileSize * 0.5f, tileTop);
        var tileMax = new Vector2(centerX + tileSize * 0.5f, tileTop + tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, ScreenGlyphSize * scale);
        var maxWidth = MathF.Min(body.Width - ScreenTextInset * scale, ScreenMaxText * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + ScreenTitleGap * scale), maxWidth);
        var hintBottom = Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
            new Vector2(centerX, titleBottom + ScreenHintGap * scale), maxWidth);
        if (action.Length == 0)
        {
            return false;
        }

        var height = CapsuleHeight * scale * 1.15f;
        var width = MathF.Min(maxWidth, MathF.Max(ScreenActionWidth * scale,
            CapsuleWidth(action, height, TextStyles.Headline)));
        var top = hintBottom + ScreenActionGap * scale;
        var rect = new Rect(new Vector2(centerX - width * 0.5f, top), new Vector2(centerX + width * 0.5f, top + height));
        return Capsule(drawList, ui, actionId, rect, action, CapsuleTone.Filled, true, TextStyles.Headline);
    }

    private static float PanelTextWidth(float width, float scale) =>
        MathF.Max(1f, width - Metrics.Space.Lg * 2f * scale - (StateTileSize + StateGap) * scale);
}
