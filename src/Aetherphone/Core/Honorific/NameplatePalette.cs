namespace Aetherphone.Core.Honorific;

internal static class NameplatePalette
{
    public const int DarkGlowIndex = 9;

    public static readonly Vector3 DarkGlow = new(0.04f, 0.10f, 0.16f);

    public static readonly Vector3[] Colors =
    {
        new(1.00f, 1.00f, 1.00f),
        new(1.00f, 0.86f, 0.55f),
        new(1.00f, 0.62f, 0.38f),
        new(0.98f, 0.42f, 0.52f),
        new(0.86f, 0.55f, 1.00f),
        new(0.55f, 0.66f, 1.00f),
        new(0.42f, 0.84f, 1.00f),
        new(0.45f, 0.92f, 0.62f),
        new(0.80f, 0.80f, 0.84f),
        DarkGlow,
    };

    public static Vector3 At(int index) => Colors[Math.Clamp(index, 0, Colors.Length - 1)];
}
