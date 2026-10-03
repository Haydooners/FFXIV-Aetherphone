using Aetherphone.Core;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal static partial class MinimizedPhoneRenderer
{
    public static float PcDiscDiameter(float scale) => DiscRadius * 2f * scale;

    public static void DrawPcMusicSection(ImDrawListPtr dl, Rect rect, in MediaSessionSnapshot snapshot,
        IDalamudTextureWrap? texture, float clock, float alpha, float scale, PhoneTheme theme)
    {
        var centerX = rect.Center.X;
        var radius = DiscRadius * scale;
        var discCenter = new Vector2(centerX, rect.Min.Y + radius);
        PcMediaView.DrawDisc(dl, discCenter, radius, texture, snapshot, MusicAccent, alpha);
        var style = new TextStyle(Text(0.78f), FontWeight.SemiBold);
        var title = PcMediaView.Title(snapshot);
        var titleTop = discCenter.Y + radius + TitleGap * scale;
        var titleHeight = Typography.Measure(title, style).Y;
        Marquee.DrawCenteredAuto(dl, "minimized.pcmusic.title", title, centerX, titleTop, rect.Width, style,
            Palette.WithAlpha(theme.TextStrong, alpha));
        var equalizerCenter = new Vector2(centerX,
            titleTop + titleHeight + EqualizerGap * scale + EqualizerHeight * scale * 0.5f);
        Equalizer.Draw(dl, equalizerCenter, scale, EqualizerHeight * scale, clock, MusicAccent, alpha,
            snapshot.IsPlaying);
    }

    public static MinimizedControlResult DrawPcMusicTransport(ImDrawListPtr dl, Rect row,
        in MediaSessionSnapshot snapshot, PhoneTheme theme, float alpha, bool active, float scale)
    {
        var centerY = row.Center.Y;
        var centerX = row.Center.X;
        var small = TransportSmall * scale;
        var large = TransportLarge * scale;
        var stride = TransportStride * scale;
        var playCenter = new Vector2(centerX, centerY);
        var previousCenter = new Vector2(centerX - stride, centerY);
        var nextCenter = new Vector2(centerX + stride, centerY);
        var canPrevious = snapshot.CanPrevious;
        var canNext = snapshot.CanNext;
        var canToggle = snapshot.CanPlayPause;
        var hovered = active && ((canToggle && Hovered(playCenter, large)) ||
                                 (canPrevious && Hovered(previousCenter, small)) ||
                                 (canNext && Hovered(nextCenter, small)));
        var action = MinimizedControl.None;
        var ink = theme.TextStrong;
        if (canPrevious && TransportButton.Draw(previousCenter, small, TransportAction.Previous, MusicAccent, ink,
                alpha, active, dl))
        {
            action = MinimizedControl.Previous;
        }

        if (canNext && TransportButton.Draw(nextCenter, small, TransportAction.Next, MusicAccent, ink, alpha, active,
                dl))
        {
            action = MinimizedControl.Next;
        }

        if (TransportButton.Draw(playCenter, large, snapshot.IsPlaying ? TransportAction.Pause : TransportAction.Play,
                MusicAccent, ink, canToggle ? alpha : alpha * 0.4f, active && canToggle, dl))
        {
            action = MinimizedControl.PlayPause;
        }

        return new MinimizedControlResult(action, hovered);
    }
}
