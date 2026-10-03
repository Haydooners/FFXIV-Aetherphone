using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Swoop;

internal static class SwoopIcon
{
    private const int HillColumns = 16;

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float extent, uint ink, uint hole)
    {
        DrawHills(drawList, center, extent, ink);
        for (var dot = 0; dot < 3; dot++)
        {
            var offset = new Vector2(-0.62f + dot * 0.2f, 0.02f - dot * 0.13f);
            drawList.AddCircleFilled(center + offset * extent, extent * (0.05f + dot * 0.025f), ink, 12);
        }

        var bird = center + new Vector2(0.24f, -0.34f) * extent;
        var radius = extent * 0.34f;
        drawList.AddTriangleFilled(bird + new Vector2(-radius * 0.7f, -radius * 0.1f), bird + new Vector2(-radius * 1.35f, -radius * 0.6f),
            bird + new Vector2(-radius * 1.25f, radius * 0.25f), ink);
        drawList.AddCircleFilled(bird, radius, ink, 28);
        drawList.AddTriangleFilled(bird + new Vector2(radius * 0.82f, -radius * 0.2f), bird + new Vector2(radius * 1.38f, radius * 0.02f),
            bird + new Vector2(radius * 0.82f, radius * 0.24f), ink);
        drawList.AddCircleFilled(bird + new Vector2(radius * 0.38f, -radius * 0.28f), radius * 0.2f, hole, 14);
        SwoopShapes.Ellipse(drawList, bird + new Vector2(-radius * 0.22f, radius * 0.12f), radius * 0.46f, radius * 0.2f, -0.35f, hole, 18);
    }

    private static void DrawHills(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        var bottom = center.Y + extent;
        var previous = HillPoint(center, extent, -1f);
        for (var column = 1; column <= HillColumns; column++)
        {
            var current = HillPoint(center, extent, -1f + 2f * column / HillColumns);
            drawList.AddQuadFilled(previous, current, new Vector2(current.X, bottom), new Vector2(previous.X, bottom), ink);
            previous = current;
        }
    }

    private static Vector2 HillPoint(Vector2 center, float extent, float x) =>
        center + new Vector2(x, 0.55f + 0.28f * MathF.Sin(2.2f * x + 0.6f)) * extent;
}
