using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SocialPill
{
    public static bool Accent(ImDrawListPtr drawList, Rect rect, string label, SocialInk ink, in TextStyle style,
        float rounding, bool enabled = true) =>
        Button.Draw(drawList, rect, label, InkOf(ink), ButtonStyle.Prominent, enabled: enabled);

    public static bool Outline(ImDrawListPtr drawList, Rect rect, string label, SocialInk ink, in TextStyle style,
        float rounding, Vector4 fill) =>
        Button.Draw(drawList, rect, label, InkOf(ink), ButtonStyle.Tinted);

    public static bool Flat(ImDrawListPtr drawList, Rect rect, string label, Vector4 fill, Vector4 hoverFill,
        Vector4 stroke, Vector4 labelInk, in TextStyle style, float rounding)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var face = Paint(drawList, rect, ImGui.GetID(label), fill, hoverFill, stroke, rect.Height * 0.5f, hovered);
        var labelStyle = Button.LabelStyle(face.Height);
        var fitted = Typography.FitText(label, MathF.Max(1f, face.Width - face.Height), labelStyle);
        Typography.DrawCentered(drawList, face.Center, fitted, labelInk, labelStyle);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static bool Icon(ImDrawListPtr drawList, Rect rect, string glyph, string tooltip, Vector4 fill,
        Vector4 hoverFill, Vector4 glyphInk, float iconSize, float rounding)
    {
        var scale = UiScale.Current;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var face = Paint(drawList, rect, ImGui.GetID(tooltip.Length > 0 ? tooltip : glyph), fill, hoverFill, default,
            MathF.Min(rounding, rect.Height * 0.5f), hovered);
        PhoneIcon.Draw(drawList, face.Center, glyph, glyphInk, iconSize * scale * (face.Width / rect.Width));
        HoverTooltip.Show(rect, tooltip, HoverLabelSide.Below);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static Rect Paint(ImDrawListPtr drawList, Rect rect, uint key, Vector4 fill, Vector4 hoverFill,
        Vector4 stroke, float radius, bool hovered)
    {
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var pose = MotionButton.Animate(rect, (int)key, hovered, pressed);
        var face = pose.Face;
        var faceRadius = radius * (face.Height / MathF.Max(rect.Height, 0.0001f));
        Squircle.Fill(drawList, face.Min, face.Max, faceRadius,
            ImGui.GetColorU32(Vector4.Lerp(fill, hoverFill, pose.Hover)));
        if (stroke.W > 0f)
        {
            Squircle.Stroke(drawList, face.Min, face.Max, faceRadius, ImGui.GetColorU32(stroke), 1f);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return face;
    }

    private static ControlInk InkOf(SocialInk ink) => new(ink.Accent, ink.TitleInk, ink.MutedInk, ink.Danger);
}
