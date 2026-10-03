using Aetherphone.Apps.Games.Online;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games;

internal static class GamesHubArt
{
    public const float SectionHeight = 34f;
    public const float SectionGap = 22f;
    private const float SeeAllChevron = 11f;
    private const float SeeAllGap = 4f;
    private const float PillHoverDarken = 0.10f;
    private const float StateTileSize = 76f;
    private const float StateGlyphSize = 34f;
    private const float StateLift = 44f;
    private const float StateTitleGap = 18f;
    private const float StateHintGap = 6f;
    private const float StateActionGap = 20f;
    private const float StateActionHeight = 40f;
    private const float StateActionMinWidth = 150f;
    private const float StateTextInset = 56f;
    private const float StateMaxText = 290f;
    private const float MedallionRim = 2f;
    private const float MedallionArt = 1.3f;

    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);

    public static bool Pill(ImDrawListPtr drawList, string id, Rect rect, string label, Vector4 fill, Vector4 ink,
        in TextStyle style, bool enabled = true)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(id, pressed, Motion.PressScaleControl);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var tone = !enabled ? Palette.WithAlpha(fill, fill.W * 0.4f)
            : hovered ? Palette.Mix(fill, Black with { W = fill.W }, PillHoverDarken) : fill;
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f, ImGui.GetColorU32(tone));
        var fitted = Typography.FitText(label, MathF.Max(1f, rect.Width - rect.Height * 0.6f), style);
        Typography.DrawCentered(drawList, rect.Center, fitted, enabled ? ink : Palette.WithAlpha(ink, 0.6f), style);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static float PillWidth(string label, float height, in TextStyle style) =>
        Typography.Measure(label, style).X + height * 1.1f;

    public static bool Section(ImDrawListPtr drawList, AppSkin ui, float left, float top, float width, string title,
        string action, string id)
    {
        var scale = UiScale.Current;
        var height = SectionHeight * scale;
        var centerY = top + height * 0.5f;
        var actionWidth = 0f;
        var clicked = false;
        if (action.Length > 0)
        {
            var labelSize = Typography.Measure(action, TextStyles.Body);
            var chevron = SeeAllChevron * scale;
            actionWidth = labelSize.X + SeeAllGap * scale + chevron;
            var min = new Vector2(left + width - actionWidth, top);
            var max = new Vector2(left + width, top + height);
            var hovered = UiInteract.Hover(min, max);
            var ink = hovered ? Palette.Mix(ui.Accent, ui.TitleInk, 0.25f) : ui.Accent;
            Typography.Draw(drawList, new Vector2(min.X, centerY - labelSize.Y * 0.5f), action, ink, TextStyles.Body);
            PhoneIcon.Draw(drawList, new Vector2(max.X - chevron * 0.5f, centerY), PhoneIcons.ChevronRight, ink,
                chevron);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            clicked = UiInteract.Click(min, max, hovered);
            ReportAnchor(id, new Rect(min, max));
        }

        var titleWidth = MathF.Max(1f, width - actionWidth - Metrics.Space.Md * scale);
        var fitted = Typography.FitText(title, titleWidth, TextStyles.Title3);
        var titleHeight = Typography.Measure(fitted, TextStyles.Title3).Y;
        Typography.Draw(drawList, new Vector2(left, centerY - titleHeight * 0.5f), fitted, ui.TitleInk,
            TextStyles.Title3);
        return clicked;
    }

    public static bool StateScreen(ImDrawListPtr drawList, AppSkin ui, Rect body, FontAwesomeIcon icon, string title,
        string hint, string action, string id)
    {
        var scale = UiScale.Current;
        var centerX = body.Center.X;
        var tileSize = StateTileSize * scale;
        var tileTop = MathF.Max(body.Min.Y + Metrics.Space.Xl * scale, body.Center.Y - StateLift * scale - tileSize);
        var tileMin = new Vector2(centerX - tileSize * 0.5f, tileTop);
        var tileMax = new Vector2(centerX + tileSize * 0.5f, tileTop + tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, StateGlyphSize * scale);
        var maxWidth = MathF.Min(body.Width - StateTextInset * scale, StateMaxText * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + StateTitleGap * scale), maxWidth);
        var hintBottom = hint.Length == 0
            ? titleBottom
            : Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
                new Vector2(centerX, titleBottom + StateHintGap * scale), maxWidth);
        if (action.Length == 0)
        {
            return false;
        }

        var height = StateActionHeight * scale;
        var width = MathF.Min(maxWidth, MathF.Max(StateActionMinWidth * scale,
            PillWidth(action, height, TextStyles.Headline)));
        var top = hintBottom + StateActionGap * scale;
        var rect = new Rect(new Vector2(centerX - width * 0.5f, top), new Vector2(centerX + width * 0.5f, top + height));
        return Pill(drawList, id, rect, action, ui.Accent, AccentRing.Ink, TextStyles.Headline);
    }

    public static void Medallion(ImDrawListPtr drawList, string kind, Vector2 center, float radius, Vector4 rim,
        float scale)
    {
        var accent = OnlineGameArt.Accent(kind);
        drawList.AddCircleFilled(center, radius + MedallionRim * scale, ImGui.GetColorU32(rim), 40);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.Darken(accent, 0.30f)), 40);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.Lighten(accent, 0.35f) with { W = 0.55f }), 40,
            1f * scale);
        OnlineGameArt.Draw(drawList, kind, center, radius * MedallionArt, scale);
    }

    public static void ReportAnchor(string key, Rect rect)
    {
        if (!Core.Onboarding.UiAnchors.Recording)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var top = MathF.Max(rect.Min.Y, drawList.GetClipRectMin().Y);
        var bottom = MathF.Min(rect.Max.Y, drawList.GetClipRectMax().Y);
        if (bottom <= top)
        {
            return;
        }

        Core.Onboarding.UiAnchors.Report(key, new Rect(new Vector2(rect.Min.X, top), new Vector2(rect.Max.X, bottom)));
    }
}
