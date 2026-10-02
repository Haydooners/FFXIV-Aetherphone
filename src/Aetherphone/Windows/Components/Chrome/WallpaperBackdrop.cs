using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class WallpaperBackdrop
{
    private const int FrameTolerance = 1;

    private static int recordedFrame = -1;
    private static Rect quad;
    private static ImTextureID lightHandle;
    private static Vector2 lightUv0;
    private static Vector2 lightUv1;
    private static bool hasDark;
    private static ImTextureID darkHandle;
    private static Vector2 darkUv0;
    private static Vector2 darkUv1;
    private static float darkness;

    public static bool Available => ImGui.GetFrameCount() - recordedFrame <= FrameTolerance && quad.Width > 0f &&
                                    quad.Height > 0f;

    public static void Record(Rect drawnQuad, ImTextureID light, Vector2 lightMinUv, Vector2 lightMaxUv)
    {
        recordedFrame = ImGui.GetFrameCount();
        quad = drawnQuad;
        lightHandle = light;
        lightUv0 = lightMinUv;
        lightUv1 = lightMaxUv;
        hasDark = false;
        darkness = 0f;
    }

    public static void RecordDark(ImTextureID dark, Vector2 darkMinUv, Vector2 darkMaxUv, float amount)
    {
        hasDark = true;
        darkHandle = dark;
        darkUv0 = darkMinUv;
        darkUv1 = darkMaxUv;
        darkness = amount;
    }

    public static bool Fill(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float opacity, float lens)
    {
        if (!Available || opacity <= 0f)
        {
            return false;
        }

        var center = (min + max) * 0.5f;
        var half = (max - min) * 0.5f / MathF.Max(lens, 1f);
        var sampleMin = center - half;
        var sampleMax = center + half;
        var (lightMin, lightMax) = Map(sampleMin, sampleMax, lightUv0, lightUv1);
        Squircle.FillImage(drawList, min, max, radius, lightHandle, Tint(opacity), lightMin, lightMax);
        if (!hasDark || darkness <= 0.001f)
        {
            return true;
        }

        var (darkMin, darkMax) = Map(sampleMin, sampleMax, darkUv0, darkUv1);
        Squircle.FillImage(drawList, min, max, radius, darkHandle, Tint(opacity * darkness), darkMin, darkMax);
        return true;
    }

    private static (Vector2 Min, Vector2 Max) Map(Vector2 sampleMin, Vector2 sampleMax, Vector2 uv0, Vector2 uv1)
    {
        var span = uv1 - uv0;
        var size = quad.Size;
        var mappedMin = uv0 + (sampleMin - quad.Min) / size * span;
        var mappedMax = uv0 + (sampleMax - quad.Min) / size * span;
        return (mappedMin, mappedMax);
    }

    private static uint Tint(float alpha) =>
        alpha >= 1f ? 0xFFFFFFFFu : ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
}
