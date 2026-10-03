using Aetherphone.Core;
using Aetherphone.Core.SystemMedia;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed partial class MinimizedPhone
{
    private readonly PcMediaSource pcMedia;
    private MediaSessionSnapshot pcSnapshot = MediaSessionSnapshot.Empty;
    private bool pcMusicShown;

    private bool ReadPcMusic()
    {
        ref readonly var current = ref pcMedia.Current;
        if (!PcMediaSource.IsLive(current))
        {
            return false;
        }

        pcSnapshot = current;
        pcMusicShown = true;
        return true;
    }

    private bool ShowsPcMusic()
    {
        if (playback.IsActive)
        {
            pcMusicShown = false;
        }

        return pcMusicShown;
    }

    private float DrawPcMusic(ImDrawListPtr dl, Rect screen, float y, float presence)
    {
        var scale = frameScale;
        var compactHeight = MusicHeight * scale;
        var expandedHeight = MusicExpandedHeight * scale * frameExpandEased;
        var section = SectionRect(screen, y, (compactHeight + expandedHeight) * presence);
        musicHovered = frameBodyHovered && presence > 0.9f && UiInteract.Hover(section.Min, section.Max);
        dl.PushClipRect(section.Min, section.Max, true);
        var compact = new Rect(section.Min, new Vector2(section.Max.X, y + compactHeight));
        var sectionAlpha = frameAlpha * presence;
        var texture = pcMedia.Artwork(pcSnapshot, MinimizedPhoneRenderer.PcDiscDiameter(scale));
        MinimizedPhoneRenderer.DrawPcMusicSection(dl, compact, pcSnapshot, texture, clock, sectionAlpha, scale,
            frameTheme);
        if (expandedHeight > 0.5f)
        {
            var row = new Rect(new Vector2(section.Min.X, compact.Max.Y),
                new Vector2(section.Max.X, compact.Max.Y + expandedHeight));
            var active = frameInteractive && frameExpandEased > ControlThreshold;
            var result = MinimizedPhoneRenderer.DrawPcMusicTransport(dl, row, pcSnapshot, frameTheme,
                sectionAlpha * frameExpandEased, active, scale);
            ApplyPcMusicControl(result.Action);
            controlHovered |= result.Hovered;
        }

        dl.PopClipRect();
        return section.Max.Y;
    }

    private void ApplyPcMusicControl(MinimizedControl control)
    {
        switch (control)
        {
            case MinimizedControl.Previous:
                pcMedia.Previous();
                break;
            case MinimizedControl.Next:
                pcMedia.Next();
                break;
            case MinimizedControl.PlayPause:
                pcMedia.TogglePlayPause(pcSnapshot);
                break;
        }
    }
}
