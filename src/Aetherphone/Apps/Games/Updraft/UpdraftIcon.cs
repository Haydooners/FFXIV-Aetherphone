using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Updraft;

internal static class UpdraftIcon
{
    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        drawList.AddCircleFilled(center, extent * 0.8f, ink, 32);
        drawList.AddCircleFilled(center, extent * 0.35f, hole, 24);
    }
}
