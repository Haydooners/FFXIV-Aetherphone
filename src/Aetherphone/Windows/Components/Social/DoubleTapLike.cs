using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class DoubleTapLike
{
    private const float BurstDuration = 0.9f;
    private const float BurstHold = 0.55f;
    private const float BurstSize = 84f;
    private const float PopDuration = 0.22f;
    private const float RiseSpeed = 46f;
    private const float BackOvershoot = 2.70158f;

    private string burstPostId = string.Empty;
    private string swallowPostId = string.Empty;
    private double burstStart;

    public bool Tapped(Rect rect, string postId)
    {
        if (UiInteract.Hover(rect.Min, rect.Max) && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            swallowPostId = postId;
            burstPostId = postId;
            burstStart = ImGui.GetTime();
            return true;
        }

        if (swallowPostId == postId && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            swallowPostId = string.Empty;
        }

        return false;
    }

    public bool SwallowedTap(string postId)
    {
        if (swallowPostId != postId)
        {
            return false;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            swallowPostId = string.Empty;
        }

        return true;
    }

    public void DrawBurst(ImDrawListPtr drawList, Rect rect, string postId)
    {
        if (burstPostId != postId)
        {
            return;
        }

        var elapsed = (float)(ImGui.GetTime() - burstStart);
        if (elapsed >= BurstDuration)
        {
            burstPostId = string.Empty;
            return;
        }

        var scale = UiScale.Current;
        var appear = Math.Clamp(elapsed / PopDuration, 0f, 1f);
        var back = appear - 1f;
        var pop = MathF.Max(1f + back * back * (BackOvershoot * back + BackOvershoot - 1f), 0.05f);
        var alpha = elapsed < BurstHold ? 1f : 1f - (elapsed - BurstHold) / (BurstDuration - BurstHold);
        var rise = elapsed < BurstHold ? 0f : (elapsed - BurstHold) * RiseSpeed * scale;
        var center = new Vector2(rect.Center.X, rect.Center.Y - rise);
        var size = BurstSize * scale * pop;
        PhoneIcon.Draw(drawList, center + new Vector2(0f, 2f * scale), PhoneIcons.HeartFilled,
            new Vector4(0f, 0f, 0f, 0.35f * alpha), size);
        PhoneIcon.Draw(drawList, center, PhoneIcons.HeartFilled, new Vector4(1f, 1f, 1f, alpha), size);
    }
}
