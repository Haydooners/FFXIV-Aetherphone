using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal enum VenueCardAction : byte
{
    None,
    Open,
    ToggleFavorite,
}

internal static class VenueCard
{
    public const float RailWidth = 148f;
    public const float RailHeight = 192f;

    private const float HeroAspect = 0.48f;
    private const float HeroMaxHeight = 210f;
    private const float PadX = SocialChrome.CellPadX;
    private const float PadTop = 12f;
    private const float PadBottom = 14f;
    private const float LogoSide = 42f;
    private const float LogoGap = 11f;
    private const float RowGap = 8f;
    private const float OverlayInset = 12f;
    private const float HeroScrimShare = 0.42f;
    private const float StatGlyph = 13f;
    private const float RailRounding = 18f;
    private const float RailPad = 11f;
    private const float RailScrimShare = 0.66f;
    private const float RailPressScale = 0.97f;

    private static readonly TextStyle TitleStyle = TextStyles.Headline;
    private static readonly TextStyle MetaStyle = TextStyles.Footnote;
    private static readonly TextStyle StatusStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle StatStyle = TextStyles.Caption1;
    private static readonly TextStyle RailTitleStyle = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle RailStatusStyle = TextStyles.Caption1;
    private static readonly Vector4 RailMutedInk = new(1f, 1f, 1f, 0.80f);
    private static readonly Vector4 FavoriteInk = new(1f, 0.80f, 0.26f, 1f);

    public static float FeedHeight(VenueEvent venue, in VenueCardText text, float width, float scale)
    {
        var header = MathF.Max(Typography.LineHeight(TitleStyle) + 2f * scale + Typography.LineHeight(MetaStyle),
            venue.LogoUrl is null ? 0f : LogoSide * scale);
        var height = HeroHeight(width, scale) + PadTop * scale + header + PadBottom * scale;
        if (HasStatusRow(text))
        {
            height += RowGap * scale + Typography.LineHeight(StatusStyle);
        }

        if (venue.Tags.Count > 0)
        {
            height += RowGap * scale + VenueChips.Height(scale);
        }

        return height;
    }

    public static VenueCardAction DrawFeed(VenueEvent venue, in VenueCardText text, bool favorite, in VenueArt art,
        SocialInk ink)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var height = FeedHeight(venue, text, width, scale);
        var origin = ImGui.GetCursorScreenPos();
        if (!ImGui.IsRectVisible(origin, origin + new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return VenueCardAction.None;
        }

        var cell = FeedCell.Begin(drawList, height, ink.HoverTint);
        var bounds = cell.Bounds;
        var hero = new Rect(bounds.Min, new Vector2(bounds.Max.X, bounds.Min.Y + HeroHeight(bounds.Width, scale)));
        VenueImage.Cover(drawList, hero, 0f, venue, text.Initial, art);
        MediaOverlay.BottomScrim(drawList, hero.Min, hero.Max, HeroScrimShare);
        var inset = OverlayInset * scale;
        DrawStatusPill(drawList, new Vector2(hero.Min.X + inset, hero.Min.Y + inset), text.Status.Kind, scale);
        var starCenter = new Vector2(hero.Max.X - inset - MediaOverlay.GlassButtonRadius * scale,
            hero.Min.Y + inset + MediaOverlay.GlassButtonRadius * scale);
        var starTapped = MediaOverlay.GlassButton(drawList, starCenter,
            favorite ? PhoneIcons.StarFilled : PhoneIcons.Star, string.Empty, scale, null,
            favorite ? FavoriteInk : MediaOverlay.White);
        PaintBody(drawList, venue, text, bounds, hero.Max.Y, art, ink, scale);
        FeedCell.End(drawList, cell, ink.Hairline);
        if (starTapped)
        {
            return VenueCardAction.ToggleFavorite;
        }

        return cell.Tapped ? VenueCardAction.Open : VenueCardAction.None;
    }

    public static bool DrawRail(ImDrawListPtr drawList, Rect rest, VenueEvent venue, in VenueCardText text,
        in VenueArt art, SocialInk ink, bool interactive)
    {
        var scale = UiScale.Current;
        var hovered = interactive && UiInteract.Hover(rest.Min, rest.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(text.PressId, pressed, RailPressScale);
        var half = rest.Size * 0.5f * press;
        var card = new Rect(rest.Center - half, rest.Center + half);
        var rounding = RailRounding * scale;
        Elevation.Card(drawList, card.Min, card.Max, rounding, scale, hovered ? 0.55f : 0.35f);
        VenueImage.Cover(drawList, card, rounding, venue, text.Initial, art);
        Squircle.FillVerticalGradient(drawList, new Vector2(card.Min.X, card.Max.Y - card.Height * RailScrimShare),
            card.Max, rounding, ImGui.GetColorU32(MediaOverlay.ScrimClear),
            ImGui.GetColorU32(MediaOverlay.ScrimDeep));
        if (hovered)
        {
            Squircle.Stroke(drawList, card.Min, card.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ink.AccentLink, 0.7f)), 1.2f * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = RailPad * scale;
        DrawStatusPill(drawList, new Vector2(card.Min.X + pad, card.Min.Y + pad), text.Status.Kind, scale);
        var textWidth = card.Width - pad * 2f;
        var statusHeight = Typography.LineHeight(RailStatusStyle);
        var titleHeight = Typography.LineHeight(RailTitleStyle);
        var statusTop = card.Max.Y - pad - statusHeight;
        var titleTop = statusTop - titleHeight - 1f * scale;
        Marquee.DrawLeftAuto(drawList, new MarqueeId("venues.rail.title.", venue.Id), venue.Title, card.Min.X + pad,
            titleTop, textWidth, RailTitleStyle, MediaOverlay.White);
        var statusLeft = card.Min.X + pad;
        if (text.Stat.Length > 0)
        {
            statusLeft += DrawStat(drawList, statusLeft, statusTop + statusHeight * 0.5f, text.Stat,
                venue.LiveViewers > 0 ? PhoneIcons.Eye : PhoneIcons.Users, RailMutedInk, scale) + 8f * scale;
        }

        var railStatus = text.Status.Label.Length > 0 ? text.Status.Label : venue.World;
        Typography.Draw(drawList, new Vector2(statusLeft, statusTop),
            Typography.FitText(railStatus, MathF.Max(1f, card.Max.X - pad - statusLeft), RailStatusStyle),
            RailMutedInk, RailStatusStyle);
        return interactive && UiInteract.Click(card.Min, card.Max, hovered);
    }

    private static void PaintBody(ImDrawListPtr drawList, VenueEvent venue, in VenueCardText text, Rect bounds,
        float top, in VenueArt art, SocialInk ink, float scale)
    {
        var pad = PadX * scale;
        var left = bounds.Min.X + pad;
        var right = bounds.Max.X - pad;
        var cursorY = top + PadTop * scale;
        var textLeft = left;
        var titleHeight = Typography.LineHeight(TitleStyle);
        var metaHeight = Typography.LineHeight(MetaStyle);
        var headerHeight = titleHeight + 2f * scale + metaHeight;
        if (venue.LogoUrl is not null)
        {
            var side = LogoSide * scale;
            var logo = new Rect(new Vector2(left, cursorY), new Vector2(left + side, cursorY + side));
            VenueImage.Logo(drawList, logo, side * 0.28f, venue, text.Initial, art);
            textLeft = logo.Max.X + LogoGap * scale;
            cursorY += MathF.Max(0f, (side - headerHeight) * 0.5f);
            headerHeight = MathF.Max(headerHeight, side);
        }

        var textWidth = MathF.Max(1f, right - textLeft);
        Marquee.DrawLeftAuto(drawList, new MarqueeId("venues.card.title.", venue.Id), venue.Title, textLeft, cursorY,
            textWidth, TitleStyle, ink.TitleInk);
        Typography.Draw(drawList, new Vector2(textLeft, cursorY + titleHeight + 2f * scale),
            Typography.FitText(text.Meta, textWidth, MetaStyle), ink.MutedInk, MetaStyle);
        cursorY = top + PadTop * scale + headerHeight;
        if (HasStatusRow(text))
        {
            cursorY += RowGap * scale;
            DrawStatusRow(drawList, venue, text, left, right, cursorY, ink, scale);
            cursorY += Typography.LineHeight(StatusStyle);
        }

        if (venue.Tags.Count > 0)
        {
            cursorY += RowGap * scale;
            DrawTags(drawList, venue, left, right, cursorY, scale);
        }
    }

    private static void DrawStatusRow(ImDrawListPtr drawList, VenueEvent venue, in VenueCardText text, float left,
        float right, float top, SocialInk ink, float scale)
    {
        var lineHeight = Typography.LineHeight(StatusStyle);
        var centerY = top + lineHeight * 0.5f;
        var statRight = right;
        if (text.Stat.Length > 0)
        {
            var statWidth = StatWidth(text.Stat, scale);
            DrawStat(drawList, right - statWidth, centerY, text.Stat,
                venue.LiveViewers > 0 ? PhoneIcons.Eye : PhoneIcons.Users, ink.MutedInk, scale);
            statRight = right - statWidth - 10f * scale;
        }

        if (text.Status.Label.Length == 0)
        {
            return;
        }

        var cursorX = left;
        var tint = StatusTint(text.Status.Kind, ink);
        if (text.Status.Kind == VenueStatusKind.Live)
        {
            MediaOverlay.LiveDot(drawList, new Vector2(cursorX + 5f * scale, centerY), tint, scale);
            cursorX += 16f * scale;
        }

        Typography.Draw(drawList, new Vector2(cursorX, top),
            Typography.FitText(text.Status.Label, MathF.Max(1f, statRight - cursorX), StatusStyle), tint,
            StatusStyle);
    }

    public static Vector4 StatusTint(VenueStatusKind kind, SocialInk ink) =>
        kind switch
        {
            VenueStatusKind.Live => MediaOverlay.LiveGreen,
            VenueStatusKind.Open => Palette.WithAlpha(MediaOverlay.LiveGreen, 0.82f),
            _ => ink.AccentLink,
        };

    public static void DrawStatusPill(ImDrawListPtr drawList, Vector2 topLeft, VenueStatusKind kind, float scale)
    {
        if (kind == VenueStatusKind.Live)
        {
            MediaOverlay.LivePill(drawList, topLeft, Loc.T(L.Common.Live), scale);
            return;
        }

        if (kind == VenueStatusKind.Open)
        {
            MediaOverlay.Pill(drawList, topLeft, Loc.T(L.Venues.OpenNow), MediaOverlay.Fill,
                MediaOverlay.LiveGreen, scale, string.Empty, true,
                Palette.WithAlpha(MediaOverlay.LiveGreen, 0.55f));
        }
    }

    private static float StatWidth(string stat, float scale) =>
        Typography.Measure(stat, StatStyle).X + (StatGlyph + 4f) * scale;

    private static float DrawStat(ImDrawListPtr drawList, float left, float centerY, string stat, string glyph,
        Vector4 color, float scale)
    {
        PhoneIcon.Draw(drawList, new Vector2(left + StatGlyph * scale * 0.5f, centerY), glyph, color,
            StatGlyph * scale);
        var size = Typography.Measure(stat, StatStyle);
        Typography.Draw(drawList, new Vector2(left + (StatGlyph + 4f) * scale, centerY - size.Y * 0.5f), stat, color,
            StatStyle);
        return (StatGlyph + 4f) * scale + size.X;
    }

    private static void DrawTags(ImDrawListPtr drawList, VenueEvent venue, float left, float right, float top,
        float scale)
    {
        var gap = 5f * scale;
        var cursor = left;
        for (var index = 0; index < venue.Tags.Count; index++)
        {
            var tag = venue.Tags[index];
            var width = VenueChips.Measure(tag, scale);
            var remaining = venue.Tags.Count - index;
            var reserve = remaining > 1 ? VenueChips.Measure(VenueLabelCache.Plus(remaining - 1), scale) + gap : 0f;
            if (cursor + width + reserve > right)
            {
                VenueChips.DrawNeutral(drawList, new Vector2(cursor, top), VenueLabelCache.Plus(remaining), scale);
                return;
            }

            VenueChips.Draw(drawList, new Vector2(cursor, top), tag, scale);
            cursor += width + gap;
        }
    }

    private static bool HasStatusRow(in VenueCardText text) => text.Status.Label.Length > 0 || text.Stat.Length > 0;

    private static float HeroHeight(float width, float scale) => MathF.Min(width * HeroAspect, HeroMaxHeight * scale);
}
