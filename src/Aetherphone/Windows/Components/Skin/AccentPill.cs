using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class AccentPill
{
    private const string KeySeed = "##accentPill";
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static void Paint(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, bool hovered,
        Vector4 accent, Vector4 accentDeep, Vector4 shadowInk, float opacity = 1f)
    {
        var ink = new ControlInk(accent with { W = 1f }, White, White, accent);
        Button.Surface(drawList, new Rect(min, max), ink, ButtonStyle.Prominent, ButtonRole.Normal, true, hovered,
            FrameKeys.Next(KeySeed), opacity, rounding);
    }
}
