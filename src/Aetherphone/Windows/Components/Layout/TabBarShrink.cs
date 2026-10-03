using Aetherphone.Core.Animation;

namespace Aetherphone.Windows.Components;

internal struct TabBarShrink
{
    public const float SmoothTime = 0.16f;
    public const float TopZone = 8f;
    public const float ShrinkTravel = 28f;
    public const float GrowTravel = 16f;
    public const float JumpLimit = 240f;

    private Spring spring;
    private float previous;
    private float anchor;
    private bool compact;
    private bool primed;

    public readonly bool Compact => compact;

    public readonly float Amount => spring.Value;

    public void Observe(float offset, float scale)
    {
        if (!primed)
        {
            primed = true;
            previous = offset;
            anchor = offset;
            compact = false;
            return;
        }

        var delta = offset - previous;
        previous = offset;
        if (offset <= TopZone * scale)
        {
            compact = false;
            anchor = offset;
            return;
        }

        if (MathF.Abs(delta) > JumpLimit * scale)
        {
            anchor = offset;
            return;
        }

        if (delta > 0f)
        {
            ObserveDown(offset, scale);
            return;
        }

        if (delta < 0f)
        {
            ObserveUp(offset, scale);
        }
    }

    private void ObserveDown(float offset, float scale)
    {
        if (compact)
        {
            anchor = offset;
            return;
        }

        if (offset - anchor < ShrinkTravel * scale)
        {
            return;
        }

        compact = true;
        anchor = offset;
    }

    private void ObserveUp(float offset, float scale)
    {
        if (!compact)
        {
            anchor = offset;
            return;
        }

        if (anchor - offset < GrowTravel * scale)
        {
            return;
        }

        compact = false;
        anchor = offset;
    }

    public float Step(float deltaSeconds) => spring.Step(compact ? 1f : 0f, SmoothTime, deltaSeconds);

    public void Expand()
    {
        compact = false;
        anchor = previous;
    }

    public void Reset()
    {
        primed = false;
        compact = false;
        spring.SnapTo(0f);
    }
}
