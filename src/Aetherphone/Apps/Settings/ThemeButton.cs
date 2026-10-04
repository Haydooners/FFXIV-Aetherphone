using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Settings;

internal static class ThemeButton
{
    public static bool Draw(string label, PhoneTheme theme, float width = -1f) =>
        Layout(label, theme, width, Button.LargeHeight, ButtonStyle.Gray, true);

    public static bool Primary(string label, PhoneTheme theme, bool enabled = true) =>
        Layout(label, theme, -1f, Button.LargeHeight, ButtonStyle.Prominent, enabled);

    public static bool Ghost(string label, PhoneTheme theme) =>
        Layout(label, theme, -1f, Button.RegularHeight, ButtonStyle.Plain, true);

    private static bool Layout(string label, PhoneTheme theme, float width, float heightUnits, ButtonStyle style,
        bool enabled)
    {
        var origin = ImGui.GetCursorScreenPos();
        var size = new Vector2(width < 0f ? ImGui.GetContentRegionAvail().X : width, heightUnits * UiScale.Current);
        ImGui.Dummy(size);
        return Button.Draw(new Rect(origin, origin + size), label, ControlInk.From(theme), style, enabled: enabled);
    }
}
