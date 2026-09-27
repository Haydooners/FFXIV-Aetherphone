using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal enum VenueDjSheetAction : byte
{
    None,
    Twitch,
    Venue,
}

internal sealed class VenueDjSheet
{
    private const float RevealSmoothTime = 0.11f;
    private const float MaxDim = 0.45f;
    private const float Rounding = 24f;
    private const float PadX = 18f;
    private const float GrabberWidth = 38f;
    private const float GrabberHeight = 4.5f;
    private const float AvatarSize = 68f;
    private const float RingGap = 3f;
    private const float RingWeight = 2.6f;
    private const float ButtonHeight = 46f;
    private const float ButtonGap = 10f;
    private const float BottomPad = 26f;
    private const int MaxTitleLines = 4;
    private const float PanelLift = 0.08f;
    private const float PanelAlpha = 0.97f;

    private static readonly TextStyle NameStyle = TextStyles.Title3;
    private static readonly TextStyle PlaceStyle = TextStyles.Subheadline;
    private static readonly TextStyle ViewersStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle SectionStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle TitleStyle = TextStyles.Body;
    private static readonly TextStyle ButtonStyle = TextStyles.Headline;
    private static readonly Vector4 GrabberFill = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 PanelStroke = new(1f, 1f, 1f, 0.10f);
    private static readonly Vector4 InitialInk = new(1f, 1f, 1f, 0.92f);

    private Spring reveal;
    private bool open;
    private int openedFrame;
    private VenueDj? dj;
    private string viewers = string.Empty;
    private string place = string.Empty;
    private string initial = string.Empty;
    private string[] titleLines = Array.Empty<string>();
    private float titleWidth = -1f;

    public bool CapturesPointer => open || !reveal.IsResting(0f, 0.001f, 0.005f);

    public void Open(VenueDj target, string viewersLabel, string placeLine, string initialLetter)
    {
        dj = target;
        viewers = viewersLabel;
        place = placeLine;
        initial = initialLetter;
        titleWidth = -1f;
        open = true;
        openedFrame = ImGui.GetFrameCount();
    }

    public void Close() => open = false;

    public void Gate()
    {
        if (open)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public VenueDjSheetAction Draw(Rect screen, SocialInk ink, RemoteImageCache images, ArtworkCache artwork)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, RevealSmoothTime, delta);
        if (dj is null)
        {
            return VenueDjSheetAction.None;
        }

        if (!open && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            return VenueDjSheetAction.None;
        }

        var target = dj;
        var scale = UiScale.Current;
        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        var slide = Easing.EaseOutQuint(opacity);
        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, false);
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
        var padX = PadX * scale;
        var contentWidth = screen.Width - padX * 2f;
        EnsureTitleLines(target.Title, contentWidth);
        var hasTwitch = !string.IsNullOrEmpty(target.TwitchUrl);
        var hasVenue = target.VenueId is not null;
        var panelHeight = PanelHeight(target, hasTwitch, hasVenue, scale);
        var panelTop = screen.Max.Y - panelHeight + panelHeight * (1f - slide);
        var panelMin = new Vector2(screen.Min.X, panelTop);
        var panelMax = new Vector2(screen.Max.X, screen.Max.Y + Rounding * scale);
        var rounding = Rounding * scale;
        Squircle.Fill(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(ink.BackdropTop, PanelLift), PanelAlpha * opacity)));
        Squircle.Stroke(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(PanelStroke, PanelStroke.W * opacity)), 1f);
        var grabberMin = new Vector2(screen.Center.X - GrabberWidth * scale * 0.5f, panelTop + 8f * scale);
        drawList.AddRectFilled(grabberMin, grabberMin + new Vector2(GrabberWidth, GrabberHeight) * scale,
            ImGui.GetColorU32(Palette.WithAlpha(GrabberFill, GrabberFill.W * opacity)), GrabberHeight * scale * 0.5f);
        var left = screen.Min.X + padX;
        var right = screen.Max.X - padX;
        var cursorY = grabberMin.Y + (GrabberHeight + 16f) * scale;
        cursorY = DrawHeader(drawList, target, images, artwork, ink, left, right, cursorY, opacity, scale);
        if (titleLines.Length > 0)
        {
            cursorY += 14f * scale;
            Typography.Draw(drawList, new Vector2(left, cursorY), Loc.Upper(Loc.T(L.Venues.NowPlaying)),
                Palette.WithAlpha(ink.FaintInk, opacity), SectionStyle);
            cursorY += Typography.LineHeight(SectionStyle) + 4f * scale;
            var lineHeight = Typography.LineHeight(TitleStyle);
            for (var index = 0; index < titleLines.Length; index++)
            {
                Typography.Draw(drawList, new Vector2(left, cursorY), titleLines[index],
                    Palette.WithAlpha(ink.BodyInk, opacity), TitleStyle);
                cursorY += lineHeight;
            }
        }

        if (target.Genres.Count > 0)
        {
            cursorY += 10f * scale;
            VenueCard.DrawChipRow(drawList, target.Genres, left, right, cursorY, scale);
            cursorY += VenueChips.Height(scale);
        }

        var interactive = open && opacity > 0.5f;
        var action = VenueDjSheetAction.None;
        cursorY += 20f * scale;
        if (hasTwitch)
        {
            var button = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + ButtonHeight * scale));
            var hovered = interactive && UiInteract.HoverWindowOnly(button.Min, button.Max, false);
            AccentPill.Paint(drawList, button.Min, button.Max, button.Height * 0.5f, hovered, ink.Accent,
                ink.AccentDeep, ink.AccentShadow, opacity);
            Typography.DrawCentered(drawList, button.Center, Loc.T(L.Venues.WatchOnTwitch),
                Palette.WithAlpha(ink.White, opacity), ButtonStyle);
            if (Pressed(hovered))
            {
                action = VenueDjSheetAction.Twitch;
            }

            cursorY = button.Max.Y + ButtonGap * scale;
        }

        if (hasVenue)
        {
            var button = new Rect(new Vector2(left, cursorY), new Vector2(right, cursorY + ButtonHeight * scale));
            var hovered = interactive && UiInteract.HoverWindowOnly(button.Min, button.Max, false);
            Squircle.Fill(drawList, button.Min, button.Max, button.Height * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(hovered ? ink.AccentWash : ink.ChipFill, opacity)));
            Squircle.Stroke(drawList, button.Min, button.Max, button.Height * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(ink.AccentLink, opacity)), 1.5f * scale);
            Typography.DrawCentered(drawList, button.Center, Loc.T(L.Venues.GoToVenue),
                Palette.WithAlpha(ink.AccentLink, opacity), ButtonStyle);
            if (Pressed(hovered))
            {
                action = VenueDjSheetAction.Venue;
            }
        }

        drawList.PopClipRect();
        if (action != VenueDjSheetAction.None)
        {
            Close();
            return action;
        }

        if (interactive && ImGui.GetFrameCount() != openedFrame && ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            !UiInteract.HoverWindowOnly(panelMin, panelMax, false))
        {
            Close();
        }

        return VenueDjSheetAction.None;
    }

    private static bool Pressed(bool hovered)
    {
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return false;
        }

        UiFeedback.Play(UiSound.Tap);
        return true;
    }

    private float DrawHeader(ImDrawListPtr drawList, VenueDj target, RemoteImageCache images, ArtworkCache artwork,
        SocialInk ink, float left, float right, float top, float opacity, float scale)
    {
        var radius = AvatarSize * scale * 0.5f;
        var ringRadius = radius + RingGap * scale;
        var center = new Vector2(left + ringRadius, top + ringRadius);
        var pulse = 0.6f + 0.4f * Pulse.Wave(Pulse.Calm);
        drawList.AddCircle(center, ringRadius,
            ImGui.GetColorU32(Palette.WithAlpha(MediaOverlay.LiveGreen, pulse * opacity)), 48, RingWeight * scale);
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var tint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, opacity));
        var avatar = images.Get(target.AvatarUrl);
        if (avatar is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(avatar.Size);
            Squircle.FillImage(drawList, min, max, radius, avatar.Handle, tint, uv0, uv1);
        }
        else
        {
            Squircle.FillImage(drawList, min, max, radius, artwork.HandleForName(target.Name), tint);
            Typography.DrawCentered(drawList, center, initial, Palette.WithAlpha(InitialInk, opacity),
                radius / (22f * scale), FontWeight.Bold);
        }

        var textLeft = center.X + ringRadius + 14f * scale;
        var textWidth = MathF.Max(1f, right - textLeft);
        var nameHeight = Typography.LineHeight(NameStyle);
        var placeHeight = place.Length > 0 ? Typography.LineHeight(PlaceStyle) : 0f;
        var viewersHeight = Typography.LineHeight(ViewersStyle);
        var textTop = center.Y - (nameHeight + placeHeight + viewersHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop), Typography.FitText(target.Name, textWidth, NameStyle),
            Palette.WithAlpha(ink.TitleInk, opacity), NameStyle);
        textTop += nameHeight;
        if (place.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, textTop), Typography.FitText(place, textWidth, PlaceStyle),
                Palette.WithAlpha(ink.MutedInk, opacity), PlaceStyle);
            textTop += placeHeight;
        }

        MediaOverlay.LiveDot(drawList, new Vector2(textLeft + 5f * scale, textTop + viewersHeight * 0.5f),
            MediaOverlay.LiveGreen, scale);
        Typography.Draw(drawList, new Vector2(textLeft + 16f * scale, textTop),
            Typography.FitText(viewers, MathF.Max(1f, textWidth - 16f * scale), ViewersStyle),
            Palette.WithAlpha(MediaOverlay.LiveGreen, opacity), ViewersStyle);
        return center.Y + ringRadius;
    }

    private float PanelHeight(VenueDj target, bool hasTwitch, bool hasVenue, float scale)
    {
        var height = (8f + GrabberHeight + 16f) * scale + AvatarSize * scale + RingGap * 2f * scale;
        if (titleLines.Length > 0)
        {
            height += 14f * scale + Typography.LineHeight(SectionStyle) + 4f * scale +
                      titleLines.Length * Typography.LineHeight(TitleStyle);
        }

        if (target.Genres.Count > 0)
        {
            height += 10f * scale + VenueChips.Height(scale);
        }

        height += 20f * scale;
        if (hasTwitch)
        {
            height += ButtonHeight * scale + ButtonGap * scale;
        }

        if (hasVenue)
        {
            height += ButtonHeight * scale;
        }

        return height + BottomPad * scale;
    }

    private void EnsureTitleLines(string title, float width)
    {
        if (MathF.Abs(width - titleWidth) < 0.5f)
        {
            return;
        }

        titleWidth = width;
        if (title.Length == 0)
        {
            titleLines = Array.Empty<string>();
            return;
        }

        var wrapped = Typography.WrapText(title, TitleStyle, width);
        if (wrapped.Length <= MaxTitleLines)
        {
            titleLines = wrapped;
            return;
        }

        var kept = new string[MaxTitleLines];
        Array.Copy(wrapped, kept, MaxTitleLines);
        kept[MaxTitleLines - 1] = Typography.FitText(kept[MaxTitleLines - 1] + " …", width, TitleStyle);
        titleLines = kept;
    }
}
