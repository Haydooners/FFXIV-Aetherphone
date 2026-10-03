using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class VolumeRocker
{
    private const float StepCount = 16f;
    private const float RepeatDelay = 0.40f;
    private const float RepeatInterval = 0.09f;
    private const float HudHoldSeconds = 1.4f;
    private const float HudThickness = 7f;
    private const float HudInset = 7f;
    private const float HudReach = 1.25f;
    private const float HudTrackAlpha = 0.42f;

    private readonly PlaybackHub playback;
    private int armedDirection;
    private float heldSeconds;
    private float repeatClock;
    private float hudSeconds;
    private bool uncommitted;
    private Spring presence;

    public VolumeRocker(PlaybackHub playback)
    {
        this.playback = playback;
    }

    public void Update(Rect upSlot, Rect downSlot, RailSide side, PhoneTheme theme, float delta)
    {
        var up = RailKey.Update(upSlot, side, theme, HardwareKey.VolumeUp, Loc.T(L.Plugin.VolumeUpHint));
        var down = RailKey.Update(downSlot, side, theme, HardwareKey.VolumeDown, Loc.T(L.Plugin.VolumeDownHint));
        if (up.Clicked || down.Clicked)
        {
            armedDirection = up.Clicked ? 1 : -1;
            heldSeconds = 0f;
            repeatClock = 0f;
            Nudge(armedDirection);
        }
        else if (armedDirection != 0 && (armedDirection > 0 ? up.Held : down.Held))
        {
            heldSeconds += delta;
            if (heldSeconds >= RepeatDelay)
            {
                repeatClock += delta;
                while (repeatClock >= RepeatInterval)
                {
                    repeatClock -= RepeatInterval;
                    Nudge(armedDirection);
                }
            }
        }
        else
        {
            armedDirection = 0;
            Commit();
        }

        hudSeconds = MathF.Max(hudSeconds - delta, 0f);
        presence.Step(hudSeconds > 0f ? 1f : 0f, Motion.Appear, delta);
    }

    public void Settle(float delta)
    {
        armedDirection = 0;
        Commit();
        hudSeconds = MathF.Max(hudSeconds - delta, 0f);
        presence.Step(hudSeconds > 0f ? 1f : 0f, Motion.Appear, delta);
    }

    public void DrawHud(Rect screen, Rect upSlot, Rect downSlot, RailSide side)
    {
        var amount = Math.Clamp(presence.Value, 0f, 1f);
        if (amount < 0.01f)
        {
            return;
        }

        var scale = UiScale.Current;
        var thickness = HudThickness * scale;
        var inset = HudInset * scale;
        var slide = (1f - amount) * (thickness + inset);
        var level = Math.Clamp(playback.Volume, 0f, 1f);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = thickness * 0.5f;
        Rect track;
        Rect fill;
        if (side == RailSide.Bottom)
        {
            var span = (downSlot.Max.X - upSlot.Min.X) * HudReach;
            var centerX = (upSlot.Min.X + downSlot.Max.X) * 0.5f;
            var bottom = screen.Max.Y - inset + slide;
            track = new Rect(new Vector2(centerX - span * 0.5f, bottom - thickness),
                new Vector2(centerX + span * 0.5f, bottom));
            fill = new Rect(new Vector2(track.Max.X - track.Width * level, track.Min.Y), track.Max);
        }
        else
        {
            var span = (downSlot.Max.Y - upSlot.Min.Y) * HudReach;
            var centerY = (upSlot.Min.Y + downSlot.Max.Y) * 0.5f;
            var left = screen.Min.X + inset - slide;
            track = new Rect(new Vector2(left, centerY - span * 0.5f),
                new Vector2(left + thickness, centerY + span * 0.5f));
            fill = new Rect(new Vector2(track.Min.X, track.Max.Y - track.Height * level), track.Max);
        }

        Elevation.Draw(drawList, track.Min, track.Max, rounding, scale, 6f, 2f, 0.22f, amount);
        drawList.AddRectFilled(track.Min, track.Max,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, HudTrackAlpha * amount)), rounding);
        if (level > 0.001f)
        {
            drawList.AddRectFilled(fill.Min, fill.Max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f * amount)),
                rounding);
        }
    }

    private void Nudge(int direction)
    {
        var steps = MathF.Round(playback.Volume * StepCount) + direction;
        playback.Volume = Math.Clamp(steps / StepCount, 0f, 1f);
        uncommitted = true;
        hudSeconds = HudHoldSeconds;
    }

    private void Commit()
    {
        if (!uncommitted)
        {
            return;
        }

        uncommitted = false;
        playback.CommitVolume();
    }
}
