using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VHeader
{
    public const float Height = 42f;

    private const float BackChipInset = 12f;
    private const float TitleGap = 10f;

    public static Vector2 Slot(Rect area, int index) => SocialChrome.HeaderSlot(area, index);

    public static float IconRadius => SocialChrome.HeaderIconRadius * UiScale.Current;

    public static bool Root(Rect area, string title, int bellBadge, int trailingSlots)
    {
        var scale = UiScale.Current;
        var midY = area.Min.Y + Height * scale * 0.5f;
        var titleLeft = area.Min.X + SocialChrome.CellPadX * scale;
        var titleRight = area.Max.X - (SocialChrome.CellPadX + SocialChrome.HeaderReserve(trailingSlots)) * scale;
        Marquee.DrawLeftAuto(new MarqueeId("vheader.root.", title), title, titleLeft,
            midY - Typography.Measure(title, TextStyles.Title3).Y * 0.5f, MathF.Max(1f, titleRight - titleLeft),
            TextStyles.Title3, VelvetTheme.TitleInk);
        return VIcon.Button(Slot(area, 0), IconRadius, PhoneIcons.Bell, VIcon.Header, VelvetTheme.TitleInk,
            Loc.T(L.Velvet.Activity), HoverLabelSide.Below, bellBadge);
    }

    public static bool Push(Rect area, string title, int trailingSlots = 0)
    {
        var scale = UiScale.Current;
        var chipRadius = SocialChrome.BackChipRadius * scale;
        var chipCenter = new Vector2(area.Min.X + BackChipInset * scale + chipRadius,
            area.Min.Y + Height * scale * 0.5f);
        var back = SocialChrome.DrawBackChip(ImGui.GetWindowDrawList(), chipCenter, chipRadius, VelvetInk.Shared);
        var backReserve = chipCenter.X + chipRadius + TitleGap * scale - area.Min.X;
        var trailingReserve = (SocialChrome.CellPadX + SocialChrome.HeaderReserve(trailingSlots)) * scale;
        var maxWidth = MathF.Max(1f, area.Width - MathF.Max(backReserve, trailingReserve) * 2f);
        Marquee.DrawCenteredAuto(new MarqueeId("vheader.push.", title), title, area.Center.X,
            chipCenter.Y - Typography.Measure(title, TextStyles.Title3).Y * 0.5f, maxWidth, TextStyles.Title3,
            VelvetTheme.TitleInk);
        return back;
    }
}
