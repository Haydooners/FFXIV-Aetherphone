using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Home;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Widgets;

internal struct WidgetCadence
{
    private long dueAt;

    public bool Due(int intervalMilliseconds)
    {
        var now = Environment.TickCount64;
        if (now < dueAt)
        {
            return false;
        }

        dueAt = now + intervalMilliseconds;
        return true;
    }

    public void Expire() => dueAt = 0;
}

internal sealed class InstanceStates<T> where T : class, new()
{
    private readonly Dictionary<string, T> states = new(StringComparer.Ordinal);

    public T For(string instanceKey)
    {
        if (states.TryGetValue(instanceKey, out var state))
        {
            return state;
        }

        state = new T();
        states[instanceKey] = state;
        return state;
    }
}

internal struct EasedValue
{
    private const float SmoothSeconds = 0.32f;

    private Spring spring;
    private int frame;
    private bool started;

    public float Step(float target, float delta, bool animate)
    {
        var current = ImGui.GetFrameCount();
        if (started && frame == current)
        {
            return spring.Value;
        }

        frame = current;
        if (!animate)
        {
            started = true;
            spring.SnapTo(target);
            return target;
        }

        if (!started)
        {
            started = true;
            spring.SnapTo(0f);
        }

        return spring.Step(target, SmoothSeconds, MathF.Min(delta, 0.1f));
    }
}

internal static class WidgetRings
{
    private const float Top = -MathF.PI / 2f;
    private const int TrackSegments = 72;
    private const float SegmentsPerRadian = 11f;
    private const float HeadShadowAlpha = 0.38f;
    private const float HeadOverlayRadians = 1.4f;

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, float thickness, float fraction,
        Vector4 color, Vector4 track)
    {
        drawList.AddCircle(center, radius, ImGui.GetColorU32(track), TrackSegments, thickness);
        if (fraction <= 0.002f)
        {
            return;
        }

        var packed = ImGui.GetColorU32(color);
        var lap = MathF.Min(fraction, 1f);
        Arc(drawList, center, radius, thickness, Top, Top + lap * MathF.Tau, packed);
        if (fraction <= 1f)
        {
            return;
        }

        var overflow = fraction - 1f;
        overflow -= MathF.Floor(overflow);
        var head = Top + overflow * MathF.Tau;
        var cap = thickness * 0.5f;
        var direction = new Vector2(MathF.Cos(head), MathF.Sin(head));
        var tangent = new Vector2(-direction.Y, direction.X);
        drawList.AddCircleFilled(center + direction * radius + tangent * cap * 0.35f, cap * 1.12f,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, HeadShadowAlpha * color.W)), 24);
        Arc(drawList, center, radius, thickness, head - MathF.Min(HeadOverlayRadians, overflow * MathF.Tau + 0.01f),
            head, packed);
    }

    private static void Arc(ImDrawListPtr drawList, Vector2 center, float radius, float thickness, float from,
        float to, uint color)
    {
        var segments = Math.Max(4, (int)MathF.Ceiling((to - from) * SegmentsPerRadian));
        drawList.PathClear();
        drawList.PathArcTo(center, radius, from, to, segments);
        drawList.PathStroke(color, ImDrawFlags.None, thickness);
        var cap = thickness * 0.5f;
        drawList.AddCircleFilled(center + new Vector2(MathF.Cos(from), MathF.Sin(from)) * radius, cap, color, 20);
        drawList.AddCircleFilled(center + new Vector2(MathF.Cos(to), MathF.Sin(to)) * radius, cap, color, 20);
    }
}

internal static class WidgetMessage
{
    private const int MaxTitleLines = 2;
    private const int MaxDetailLines = 3;

    public static void Draw(in WidgetContext context, Vector4 primary, Vector4 secondary, string title, string detail)
    {
        var content = WidgetMetrics.Content(context);
        var width = content.Width;
        var drawList = context.DrawList;
        var gap = WidgetMetrics.RowGap * context.Scale;
        var titleLines = Typography.WrapText(title, WidgetType.Headline, width);
        var detailLines = detail.Length > 0 ? Typography.WrapText(detail, WidgetType.Caption, width) : Array.Empty<string>();
        var titleCount = Math.Min(MaxTitleLines, titleLines.Length);
        var detailCount = Math.Min(MaxDetailLines, detailLines.Length);
        var titleHeight = Typography.Measure("Ag", WidgetType.Headline).Y;
        var detailHeight = Typography.Measure("Ag", WidgetType.Caption).Y;
        var bottom = content.Max.Y;
        for (var lineIndex = detailCount - 1; lineIndex >= 0; lineIndex--)
        {
            bottom -= detailHeight;
            Typography.Draw(drawList, new Vector2(content.Min.X, bottom),
                Typography.FitText(detailLines[lineIndex], width, WidgetType.Caption), secondary,
                WidgetType.Caption);
        }

        if (detailCount > 0)
        {
            bottom -= gap;
        }

        for (var lineIndex = titleCount - 1; lineIndex >= 0; lineIndex--)
        {
            bottom -= titleHeight;
            Typography.Draw(drawList, new Vector2(content.Min.X, bottom),
                Typography.FitText(titleLines[lineIndex], width, WidgetType.Headline), primary,
                WidgetType.Headline);
        }
    }
}

internal static class WidgetShimmer
{
    private const float ShimmerSeconds = 1.6f;

    public static void Block(ImDrawListPtr drawList, Rect rect, float radius, in WidgetInk ink)
    {
        if (ink.Opacity <= 0f || rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        var wave = 0.5f + 0.5f * MathF.Cos((float)(ImGui.GetTime() / ShimmerSeconds) * MathF.Tau);
        var alpha = ink.Fill.W * (0.7f + 0.3f * wave);
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(ink.Fill with { W = alpha }));
    }
}
