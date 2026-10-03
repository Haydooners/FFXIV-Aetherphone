using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float MiniPlayerHeight = 56f;
    private const float MiniPlayerGap = 8f;
    private const float MiniArtUnits = 40f;
    private const float MiniButtonRadius = 16f;
    private const float NowPlayingArtFraction = 0.62f;
    private const float TransportGlyphFraction = 0.5f;

    private readonly Sheet nowPlayingSheet = new();
    private Spring miniPresence;

    private bool NowPlayingCapturesPointer => nowPlayingSheet.CapturesPointer;

    private void ResetNowPlaying()
    {
        nowPlayingSheet.CloseImmediately();
        miniPresence.SnapTo(playback.IsActive ? 1f : 0f);
    }

    private void ResumeNowPlaying() => miniPresence.SnapTo(playback.IsActive ? 1f : 0f);

    private void OpenNowPlaying()
    {
        if (playback.IsActive)
        {
            nowPlayingSheet.Open();
        }
    }

    private float MiniPlayerInset(float scale, float delta)
    {
        if (!playback.IsActive && nowPlayingSheet.IsOpen)
        {
            nowPlayingSheet.Close();
        }

        var presence = Math.Clamp(miniPresence.Step(playback.IsActive ? 1f : 0f, Motion.Appear, delta), 0f, 1f);
        return (MiniPlayerHeight + MiniPlayerGap) * scale * presence;
    }

    private void DrawMiniPlayer(Rect stage, float scale)
    {
        var presence = Math.Clamp(miniPresence.Value, 0f, 1f);
        if (presence <= 0.01f || !playback.IsActive)
        {
            return;
        }

        var height = MiniPlayerHeight * scale;
        var slide = (height + MiniPlayerGap * scale) * (1f - presence);
        var side = Metrics.Space.GlassInset * scale;
        var min = new Vector2(stage.Min.X + side, stage.Max.Y - height + slide);
        var max = new Vector2(stage.Max.X - side, stage.Max.Y + slide);
        using var layer = ScreenLayer.Begin("music.mini", new Rect(new Vector2(stage.Min.X, min.Y), stage.Max), false);
        var drawList = ImGui.GetWindowDrawList();
        var radius = height * 0.5f;
        Material.ThemedGlass(drawList, min, max, radius, scale, ui.BackdropColor, TabBar.GlassOpacity);
        var artSide = MiniArtUnits * scale;
        var artMin = new Vector2(min.X + (height - artSide) * 0.5f, min.Y + (height - artSide) * 0.5f);
        ArtworkTile.Draw(drawList, images, artMin, artSide, playback.ArtworkUrl, playback.Title);
        var buttonCenter = new Vector2(max.X - radius, (min.Y + max.Y) * 0.5f);
        var textLeft = artMin.X + artSide + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, buttonCenter.X - MiniButtonRadius * scale - Metrics.Space.Sm * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = (min.Y + max.Y - titleHeight - subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(playback.Title, textWidth, TextStyles.FootnoteEmphasized), ui.TitleInk,
            TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
            Typography.FitText(playback.Subtitle, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        var overToggle = UiInteract.Hover(buttonCenter - new Vector2(MiniButtonRadius * scale),
            buttonCenter + new Vector2(MiniButtonRadius * scale));
        if (!overToggle && UiInteract.HoverClick(min, max))
        {
            OpenNowPlaying();
        }

        var glyph = IconGlyph.Of(playback.IsPlaying ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play);
        if (ui.IconButton(buttonCenter, MiniButtonRadius * scale, glyph, ui.TitleInk, AppSkin.Transparent, 0.9f))
        {
            playback.TogglePlayPause();
        }
    }

    private void DrawNowPlayingSheet(Rect screen, float scale)
    {
        if (!nowPlayingSheet.CapturesPointer)
        {
            return;
        }

        using var layer = ScreenLayer.Begin("music.nowPlaying", screen, false);
        var frame = nowPlayingSheet.Begin(ImGui.GetWindowDrawList(), screen, theme,
            SheetDetents.Fitted(screen.Height * SheetMetrics.LargeFraction), SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return;
        }

        var drawList = frame.DrawList;
        var content = frame.Content;
        var inset = Metrics.Space.Xl * scale;
        var artSide = MathF.Min(content.Width - inset * 2f, content.Height * NowPlayingArtFraction);
        var artMin = new Vector2(content.Center.X - artSide * 0.5f, content.Min.Y + inset);
        ArtworkTile.Draw(drawList, images, artMin, artSide, playback.ArtworkUrl, playback.Title,
            ArtworkTile.HeroRadiusFraction);
        var textWidth = content.Width - inset * 2f;
        var titleTop = artMin.Y + artSide + inset;
        Typography.Draw(drawList, new Vector2(content.Min.X + inset, titleTop),
            Typography.FitText(playback.Title, textWidth, TextStyles.Title3), frame.Ink, TextStyles.Title3);
        var subtitleTop = titleTop + Typography.LineHeight(TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(content.Min.X + inset, subtitleTop),
            Typography.FitText(playback.Subtitle, textWidth, TextStyles.Body), frame.Ink with { W = 0.6f },
            TextStyles.Body);
        var transportY = subtitleTop + Typography.LineHeight(TextStyles.Body) + inset * 2f;
        var spacing = content.Width * 0.25f;
        var radius = Metrics.Size.TapTarget * scale * 0.5f;
        var ink = ImGui.GetColorU32(frame.Ink);
        var previousCenter = new Vector2(content.Center.X - spacing, transportY);
        MediaGlyph.Previous(drawList, previousCenter, radius * TransportGlyphFraction, ink);
        if (TransportTapped(previousCenter, radius, frame.Interactive))
        {
            playback.Previous();
        }

        var toggleCenter = new Vector2(content.Center.X, transportY);
        if (playback.IsPlaying)
        {
            MediaGlyph.Pause(drawList, toggleCenter, radius * TransportGlyphFraction, ink);
        }
        else
        {
            MediaGlyph.Play(drawList, toggleCenter, radius * TransportGlyphFraction, ink);
        }

        if (TransportTapped(toggleCenter, radius, frame.Interactive))
        {
            playback.TogglePlayPause();
        }

        var nextCenter = new Vector2(content.Center.X + spacing, transportY);
        MediaGlyph.Next(drawList, nextCenter, radius * TransportGlyphFraction, ink);
        if (TransportTapped(nextCenter, radius, frame.Interactive))
        {
            playback.Next();
        }

        nowPlayingSheet.End(in frame);
    }

    private static bool TransportTapped(Vector2 center, float radius, bool interactive)
    {
        if (!interactive)
        {
            return false;
        }

        var hit = new Vector2(radius, radius);
        var hovered = UiInteract.HoverWindowOnly(center - hit, center + hit);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - hit, center + hit, hovered);
    }
}
