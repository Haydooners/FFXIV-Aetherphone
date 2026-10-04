using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VToggle
{
    public const float TrackWidth = 48f;
    public const float TrackHeight = 28f;

    public static bool Draw(string id, Rect row, bool value, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var trackMin = new Vector2(row.Max.X - TrackWidth * scale, row.Center.Y - TrackHeight * 0.5f * scale);
        var trackMax = new Vector2(row.Max.X, row.Center.Y + TrackHeight * 0.5f * scale);
        return Toggle.Draw(id, new Rect(trackMin, trackMax), value, theme);
    }
}
