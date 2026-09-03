using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VHeader
{
    public const float Height = 42f;
    public const float IconPitch = 36f;

    private const float IconInset = 20f;
    private const float TitleGap = 8f;

    public static Vector2 Slot(Rect area, int index)
    {
        var scale = UiScale.Current;
        return new Vector2(area.Max.X - (IconInset + index * IconPitch) * scale,
            area.Min.Y + Height * scale * 0.5f);
    }

    public static float Reserve(int slots) => slots <= 0 ? 0f : slots * IconPitch + TitleGap;

    public static bool Root(Rect area, string title, int bellBadge, int trailingSlots)
    {
        var scale = UiScale.Current;
        var midY = area.Min.Y + Height * scale * 0.5f;
        Marquee.DrawLeftAuto(new MarqueeId("vheader.root.", title), title, area.Min.X + 4f * scale,
            midY - Typography.Measure(title, TextStyles.Title3).Y * 0.5f,
            MathF.Max(1f, area.Width - Reserve(trailingSlots) * scale), TextStyles.Title3, VelvetTheme.TitleInk);
        return VIcon.Button(Slot(area, 0), 16f * scale, PhoneIcons.Bell, VIcon.Header, VelvetTheme.TitleInk,
            Loc.T(L.Velvet.Activity), HoverLabelSide.Below, bellBadge);
    }

    public static bool Push(Rect area, string title, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var midY = area.Min.Y + Height * scale * 0.5f;
        var center = new Vector2(area.Min.X + 16f * scale, midY);
        var hitMin = new Vector2(area.Min.X, area.Min.Y);
        var hitMax = new Vector2(area.Min.X + 46f * scale, area.Min.Y + Height * scale);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var back = BackButton.Draw("velvet.back", center, 15f * scale, VelvetTheme.TitleInk, hovered, scale,
            shadow: true);
        Marquee.DrawCenteredAuto(new MarqueeId("vheader.push.", title), title, area.Center.X,
            midY - Typography.Measure(title, TextStyles.Title3).Y * 0.5f, area.Width - 92f * scale, TextStyles.Title3,
            VelvetTheme.TitleInk);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return back;
    }
}
