using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Aethergram;

internal static class AethergramInk
{
    public static readonly SocialInk Shared = new(AppPalettes.Aethergram);

    public static Vector4 MutedInk => Shared.MutedInk;

    public static readonly Vector4 SeenRing = new(1f, 1f, 1f, 0.28f);

    public static readonly Vector4[] StoryRingStops =
    [
        new(1f, 0.863f, 0.502f, 1f), new(0.969f, 0.435f, 0.216f, 1f), new(0.882f, 0.188f, 0.424f, 1f),
        new(0.514f, 0.227f, 0.706f, 1f),
    ];
}
