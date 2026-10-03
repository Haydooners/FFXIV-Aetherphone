using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PressFx
{
    public const float DefaultPressedScale = 0.95f;
    public const float IconPressedScale = 0.93f;
    public const float CardPressedScale = 0.98f;
    public const float PressSmoothTime = 0.07f;
    public const float ReleaseSmoothTime = 0.16f;
    private const float SmoothTime = 0.09f;
    private static readonly Dictionary<uint, Spring> Springs = new();

    public static float Scale(string id, bool pressed, float pressedScale = DefaultPressedScale) =>
        Toward(id, pressed ? pressedScale : 1f);

    public static float Press(string id, bool pressed, float pressedScale) =>
        Toward(id, pressed ? pressedScale : 1f, pressed ? PressSmoothTime : ReleaseSmoothTime);

    public static float Toward(string id, float target) => Toward(id, target, SmoothTime);

    private static float Toward(string id, float target, float smoothTime)
    {
        var key = ImGui.GetID(id);
        if (!Springs.TryGetValue(key, out var spring))
        {
            spring = new Spring(1f);
        }

        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        spring.Step(target, smoothTime, deltaSeconds);
        Springs[key] = spring;
        return spring.Value;
    }
}
