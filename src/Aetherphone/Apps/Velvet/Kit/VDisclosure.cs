using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VDisclosure
{
    public const float HeaderHeight = 52f;
    public const float PanelPadX = 12f;
    public const float PanelPadY = 8f;
    public const float RevealPadY = 8f;
    public const float RevealSmoothTime = 0.14f;
    private const float PanelRadius = 14f;
    private const float TileSize = 24f;
    private const float TileGlyph = 15f;
    private const float TileGap = 10f;
    private const float SummaryGap = 8f;
    private const float TitleSummaryGap = 12f;

    public static bool Header(ImDrawListPtr drawList, in Rect row, string glyph, string title, string summary,
        float reveal, float padX)
    {
        var scale = UiScale.Current;
        var centerY = row.Center.Y;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(VelvetTheme.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var textLeft = row.Min.X + padX;
        if (glyph.Length > 0)
        {
            var tile = TileSize * scale;
            var tileMin = new Vector2(textLeft, centerY - tile * 0.5f);
            var tileMax = new Vector2(textLeft + tile, centerY + tile * 0.5f);
            Squircle.Fill(drawList, tileMin, tileMax, Metrics.Radius.Sm * scale,
                VelvetTheme.Alpha(VelvetTheme.Rose, 0.20f).Packed());
            PhoneIcon.Draw(drawList, new Vector2((tileMin.X + tileMax.X) * 0.5f, centerY), glyph, VelvetTheme.RoseInk,
                TileGlyph * scale);
            textLeft = tileMax.X + TileGap * scale;
        }

        var chevronCenter = new Vector2(row.Max.X - padX - VIcon.Row * scale * 0.5f, centerY);
        var summaryRight = chevronCenter.X - VIcon.Row * scale * 0.5f - SummaryGap * scale;
        var titleWidth = Typography.Measure(title, TextStyles.Headline).X;
        var summaryMaxWidth = MathF.Max(1f, summaryRight - textLeft - titleWidth - TitleSummaryGap * scale);
        var fittedSummary = Typography.FitText(summary, summaryMaxWidth, TextStyles.Subheadline);
        var summarySize = Typography.Measure(fittedSummary, TextStyles.Subheadline);
        Typography.Draw(drawList,
            new Vector2(textLeft, centerY - Typography.LineHeight(TextStyles.Headline) * 0.5f), title,
            VelvetTheme.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(summaryRight - summarySize.X, centerY - summarySize.Y * 0.5f),
            fittedSummary, VelvetTheme.MutedInk, TextStyles.Subheadline);
        Chevron(drawList, chevronCenter, reveal, scale);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    public static void Chevron(ImDrawListPtr drawList, Vector2 center, float reveal, float scale)
    {
        var size = VIcon.Row * scale;
        if (reveal < 0.999f)
        {
            PhoneIcon.Draw(drawList, center, PhoneIcons.ChevronRight,
                VelvetTheme.Alpha(VelvetTheme.MutedInk, 1f - reveal), size);
        }

        if (reveal > 0.001f)
        {
            PhoneIcon.Draw(drawList, center, PhoneIcons.ChevronDown, VelvetTheme.Alpha(VelvetTheme.RoseInk, reveal),
                size);
        }
    }

    public static void Well(ImDrawListPtr drawList, in Rect panel, float scale)
    {
        var radius = PanelRadius * scale;
        Squircle.Fill(drawList, panel.Min, panel.Max, radius, VelvetTheme.Alpha(VelvetTheme.Sunken, 0.85f).Packed());
        Squircle.Stroke(drawList, panel.Min, panel.Max, radius,
            VelvetTheme.Alpha(VelvetTheme.CardStroke, 0.45f).Packed(), 1f);
    }
}
