using Aetherphone.Core;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class WallpaperBackdrop
{
    private const int FrameTolerance = 1;
    private const int BrightnessTaps = 3;
    private const float BrightPixelThreshold = 0.65f;

    private static int recordedFrame = -1;
    private static Rect quad;
    private static ImTextureID lightHandle;
    private static Vector2 lightUv0;
    private static Vector2 lightUv1;
    private static float[]? lightGrid;
    private static bool hasDark;
    private static ImTextureID darkHandle;
    private static Vector2 darkUv0;
    private static Vector2 darkUv1;
    private static float[]? darkGrid;
    private static float darkness;

    public static bool Available => ImGui.GetFrameCount() - recordedFrame <= FrameTolerance && quad.Width > 0f &&
                                    quad.Height > 0f;

    public static void Record(Rect drawnQuad, ImTextureID light, Vector2 lightMinUv, Vector2 lightMaxUv,
        float[]? grid)
    {
        recordedFrame = ImGui.GetFrameCount();
        quad = drawnQuad;
        lightHandle = light;
        lightUv0 = lightMinUv;
        lightUv1 = lightMaxUv;
        lightGrid = grid;
        hasDark = false;
        darkness = 0f;
    }

    public static void RecordDark(ImTextureID dark, Vector2 darkMinUv, Vector2 darkMaxUv, float amount, float[]? grid)
    {
        hasDark = true;
        darkHandle = dark;
        darkUv0 = darkMinUv;
        darkUv1 = darkMaxUv;
        darkGrid = grid;
        darkness = amount;
    }

    public static bool Fill(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float opacity, float lens,
        float band, float refraction)
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
        var lightTint = Tint(opacity);
        Squircle.FillImage(drawList, min, max, radius, lightHandle, lightTint, lightMin, lightMax);
        Squircle.FillImageEdge(drawList, min, max, radius, band, lightHandle, lightTint, lightMin, lightMax,
            refraction);
        if (!hasDark || darkness <= 0.001f)
        {
            return true;
        }

        var (darkMin, darkMax) = Map(sampleMin, sampleMax, darkUv0, darkUv1);
        var darkTint = Tint(opacity * darkness);
        Squircle.FillImage(drawList, min, max, radius, darkHandle, darkTint, darkMin, darkMax);
        Squircle.FillImageEdge(drawList, min, max, radius, band, darkHandle, darkTint, darkMin, darkMax, refraction);
        return true;
    }

    public static float Brightness(Vector2 min, Vector2 max)
    {
        if (!Available || lightGrid is null)
        {
            return -1f;
        }

        var (lightMin, lightMax) = Map(min, max, lightUv0, lightUv1);
        var light = Sample(lightGrid, lightMin, lightMax);
        if (!hasDark || darkness <= 0.001f || darkGrid is null)
        {
            return light;
        }

        var (darkMin, darkMax) = Map(min, max, darkUv0, darkUv1);
        return light + (Sample(darkGrid, darkMin, darkMax) - light) * darkness;
    }

    private static float Sample(float[] grid, Vector2 uvMin, Vector2 uvMax)
    {
        const int size = WallpaperLibrary.BrightnessSampleSize;
        var lumaSum = 0f;
        var brightCount = 0;
        for (var row = 0; row < BrightnessTaps; row++)
        {
            var v = uvMin.Y + (uvMax.Y - uvMin.Y) * ((row + 0.5f) / BrightnessTaps);
            var y = Math.Clamp((int)(Math.Clamp(v, 0f, 1f) * (size - 1) + 0.5f), 0, size - 1);
            for (var column = 0; column < BrightnessTaps; column++)
            {
                var u = uvMin.X + (uvMax.X - uvMin.X) * ((column + 0.5f) / BrightnessTaps);
                var x = Math.Clamp((int)(Math.Clamp(u, 0f, 1f) * (size - 1) + 0.5f), 0, size - 1);
                var luma = grid[y * size + x];
                lumaSum += luma;
                if (luma >= BrightPixelThreshold)
                {
                    brightCount++;
                }
            }
        }

        const float taps = BrightnessTaps * BrightnessTaps;
        return Math.Clamp(0.5f * (lumaSum / taps) + 0.5f * (brightCount / taps), 0f, 1f);
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
