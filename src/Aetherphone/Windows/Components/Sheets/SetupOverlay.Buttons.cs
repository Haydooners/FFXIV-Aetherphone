using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed partial class SetupOverlay
{
    private const int ButtonMotionSlots = 12;
    private const int ButtonStaleFrames = 30;
    private const float HoverLiftScale = 0.03f;
    private const float PressDepthScale = 0.045f;
    private const float ButtonSheenSeconds = 0.65f;
    private const float RimReachUnits = 90f;
    private const int HaloLayers = 7;

    private struct ButtonMotion
    {
        public uint Id;
        public int LastFrame;
        public Spring Hover;
        public Spring Press;
        public float SheenClock;
        public bool WasHovered;
    }

    private readonly record struct ButtonPose(Rect Face, float Hover, float Press, float Sheen);

    private static readonly ButtonMotion[] ButtonMotions = new ButtonMotion[ButtonMotionSlots];

    private static ButtonPose Animate(Rect rect, string key, bool hovered, bool pressed)
    {
        var frame = ImGui.GetFrameCount();
        var delta = ImGui.GetIO().DeltaTime;
        ref var motion = ref MotionFor(ImGui.GetID($"setup.button.{key}"), frame);
        if (motion.LastFrame != frame - 1 && motion.LastFrame != frame)
        {
            motion.Hover.SnapTo(0f);
            motion.Press.SnapTo(0f);
            motion.SheenClock = float.MaxValue;
            motion.WasHovered = false;
        }

        motion.LastFrame = frame;
        if (hovered && !motion.WasHovered)
        {
            motion.SheenClock = 0f;
        }

        motion.WasHovered = hovered;
        motion.SheenClock = motion.SheenClock >= float.MaxValue ? float.MaxValue : motion.SheenClock + delta;
        var hover = Math.Clamp(motion.Hover.Step(hovered ? 1f : 0f, Motion.HoverLift, delta), 0f, 1f);
        var press = Math.Clamp(motion.Press.Step(pressed ? 1f : 0f, pressed ? Motion.PressIn : Motion.Release, delta),
            0f, 1f);
        var grow = 1f + HoverLiftScale * hover - PressDepthScale * press;
        var center = rect.Center;
        var half = rect.Size * 0.5f * grow;
        var sheen = motion.SheenClock / ButtonSheenSeconds;
        return new ButtonPose(new Rect(center - half, center + half), hover, press, sheen <= 1f ? sheen : -1f);
    }

    private static ref ButtonMotion MotionFor(uint id, int frame)
    {
        var free = -1;
        for (var slotIndex = 0; slotIndex < ButtonMotions.Length; slotIndex++)
        {
            if (ButtonMotions[slotIndex].Id == id)
            {
                return ref ButtonMotions[slotIndex];
            }

            if (free < 0 && frame - ButtonMotions[slotIndex].LastFrame > ButtonStaleFrames)
            {
                free = slotIndex;
            }
        }

        var slot = free >= 0 ? free : 0;
        ButtonMotions[slot] = new ButtonMotion { Id = id, LastFrame = -2, SheenClock = float.MaxValue };
        return ref ButtonMotions[slot];
    }

    private static void DrawHalo(ImDrawListPtr drawList, Rect face, float radius, Vector4 tint, float strength)
    {
        if (strength <= 0.001f)
        {
            return;
        }

        var scale = UiScale.Current;
        var color = ImGui.GetColorU32(tint with { W = tint.W * strength / HaloLayers });
        for (var layerIndex = 0; layerIndex < HaloLayers; layerIndex++)
        {
            var spread = (layerIndex + 1) * 2.2f * scale;
            var drop = new Vector2(0f, spread * 0.35f);
            Squircle.Fill(drawList, face.Min - new Vector2(spread, spread) + drop,
                face.Max + new Vector2(spread, spread) + drop, radius + spread, color);
        }
    }

    private static void DrawRimLight(ImDrawListPtr drawList, Rect face, float radius, float strength)
    {
        if (strength <= 0.001f)
        {
            return;
        }

        var scale = UiScale.Current;
        Squircle.StrokeNear(drawList, face.Min, face.Max, radius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.75f * strength)), 1.4f * scale, ImGui.GetMousePos(),
            RimReachUnits * scale);
    }

    private static bool Primary(ImDrawListPtr drawList, Rect rect, string text, float alpha, bool live,
        bool enabled = true)
    {
        if (alpha <= 0.001f)
        {
            return false;
        }

        var scale = UiScale.Current;
        var hovered = live && enabled && UiInteract.Hover(rect.Min, rect.Max);
        var pose = Animate(rect, text, hovered, hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left));
        var face = pose.Face;
        var radius = face.Height * 0.5f;
        if (enabled)
        {
            DrawHalo(drawList, face, radius, BrandMark.Violet, (0.10f + 0.32f * pose.Hover) * alpha);
            Squircle.FillVerticalGradient(drawList, face.Min, face.Max, radius,
                ImGui.GetColorU32(Fade(Vector4.Lerp(PrimaryTop, PrimaryHoverTop, pose.Hover), alpha)),
                ImGui.GetColorU32(Fade(Vector4.Lerp(PrimaryBottom, PrimaryHoverBottom, pose.Hover), alpha)));
            BrandMark.Sheen(drawList, face.Min, face.Max, radius, pose.Sheen, alpha);
            Squircle.StrokeDirectional(drawList, face.Min, face.Max, radius,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (0.35f + 0.2f * pose.Hover) * alpha)), 1.2f * scale,
                new Vector2(0f, -1f), 2f);
            DrawRimLight(drawList, face, radius, pose.Hover * alpha);
        }
        else
        {
            Squircle.Fill(drawList, face.Min, face.Max, radius, ImGui.GetColorU32(Fade(ink.Disabled, alpha)));
        }

        var label = enabled ? Fade(PrimaryInk, alpha) : Fade(ink.DisabledText, alpha);
        Typography.DrawCentered(drawList, face.Center,
            Typography.FitText(text, face.Width - Metrics.Space.Xl * scale, TextStyles.Headline), label,
            TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return live && enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static bool Secondary(ImDrawListPtr drawList, Rect rect, string label, float alpha, bool live)
    {
        if (alpha <= 0.001f)
        {
            return false;
        }

        var scale = UiScale.Current;
        var hovered = live && UiInteract.Hover(rect.Min, rect.Max);
        var pose = Animate(rect, label, hovered, hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left));
        var face = pose.Face;
        var radius = face.Height * 0.5f;
        DrawHalo(drawList, face, radius, new Vector4(0f, 0f, 0f, 1f), 0.18f * pose.Hover * alpha);
        Material.LiquidGlass(drawList, face.Min, face.Max, radius, scale, glass, 0f, alpha);
        BrandMark.Sheen(drawList, face.Min, face.Max, radius, pose.Sheen, 0.8f * alpha);
        DrawRimLight(drawList, face, radius, pose.Hover * alpha);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(drawList, face.Center,
            Typography.FitText(label, face.Width - Metrics.Space.Xl * scale, TextStyles.Headline),
            Fade(ink.Strong, alpha), TextStyles.Headline);
        return live && UiInteract.Click(rect.Min, rect.Max, hovered);
    }
}
