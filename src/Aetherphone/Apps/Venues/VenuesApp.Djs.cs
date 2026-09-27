using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private const float DjItemWidth = 78f;
    private const float DjItemGap = 4f;
    private const float DjAvatar = 60f;
    private const float DjRing = 2.6f;
    private const float DjRingGap = 3f;
    private const float DjTopPad = 6f;
    private const float DjBadgeHeight = 16f;
    private const float DjTrayHeight = 112f;
    private const float DjHoverGrow = 0.07f;

    private static readonly TextStyle DjNameStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle DjMetaStyle = TextStyles.Caption1;
    private static readonly TextStyle DjBadgeStyle = new(0.62f, FontWeight.Bold);
    private static readonly Vector4 DjInitialInk = new(1f, 1f, 1f, 0.92f);

    private void RebuildDjLabels()
    {
        djViewers.Clear();
        djMeta.Clear();
        djInitials.Clear();
        djIds.Clear();
        var djs = sections.Djs;
        for (var index = 0; index < djs.Count; index++)
        {
            var dj = djs[index];
            var viewersLabel = dj.Viewers > 0 ? VenueFormat.Viewers(dj.Viewers) : Loc.T(L.Venues.LiveNowLabel);
            djViewers.Add(viewersLabel);
            djMeta.Add(dj.Genres.Count > 0 ? dj.Genres[0] : viewersLabel);
            djInitials.Add(VenueLabelCache.InitialOf(dj.Name));
            djIds.Add("venues.dj." + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private void DrawDjTray(float scale)
    {
        var djs = sections.Djs;
        if (djs.Count == 0)
        {
            return;
        }

        DrawSectionHeading(djHeading, scale);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var itemWidth = DjItemWidth * scale;
        var gap = DjItemGap * scale;
        var height = DjTrayHeight * scale;
        var content = pad * 2f + djs.Count * itemWidth + (djs.Count - 1) * gap;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        djRail.Begin(row, content);
        var interactive = djRail.Interactive;
        drawList.PushClipRect(row.Min, row.Max, true);
        for (var index = 0; index < djs.Count; index++)
        {
            var left = origin.X + pad + index * (itemWidth + gap) - djRail.Offset;
            if (left + itemWidth < row.Min.X || left > row.Max.X)
            {
                continue;
            }

            var item = new Rect(new Vector2(left, origin.Y), new Vector2(left + itemWidth, origin.Y + height));
            if (DrawDjItem(drawList, item, djs[index], index, row, interactive, scale))
            {
                OpenDjSheet(index);
            }
        }

        djRail.DrawArrows(drawList, row, content, pad);
        drawList.PopClipRect();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private bool DrawDjItem(ImDrawListPtr drawList, Rect item, VenueDj dj, int index, Rect clip, bool interactive,
        float scale)
    {
        var hovered = interactive && clip.Contains(ImGui.GetMousePos()) && UiInteract.Hover(item.Min, item.Max);
        var eased = HoverFx.Amount(djIds[index], hovered);
        var press = PressFx.Scale(djIds[index], hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left), 0.94f);
        var radius = DjAvatar * scale * 0.5f * (1f + DjHoverGrow * eased) * press;
        var center = new Vector2(item.Center.X, item.Min.Y + DjTopPad * scale + radius + DjRingGap * scale);
        var pulse = 0.6f + 0.4f * Pulse.Wave(Pulse.Calm);
        var ringRadius = radius + DjRingGap * scale;
        drawList.AddCircle(center, ringRadius, ImGui.GetColorU32(Palette.WithAlpha(MediaOverlay.LiveGreen, pulse)),
            48, DjRing * scale);
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var avatar = images.Get(dj.AvatarUrl);
        if (avatar is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(avatar.Size);
            Squircle.FillImage(drawList, min, max, radius, avatar.Handle, 0xFFFFFFFFu, uv0, uv1);
        }
        else
        {
            Squircle.FillImage(drawList, min, max, radius, artwork.HandleForName(dj.Name), 0xFFFFFFFFu);
            Typography.DrawCentered(drawList, center, djInitials[index], DjInitialInk, radius / (22f * scale),
                FontWeight.Bold);
        }

        if (eased > 0.001f)
        {
            drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f * eased)), 48,
                1.5f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        DrawDjBadge(drawList, new Vector2(center.X, center.Y + ringRadius), scale);
        var textWidth = item.Width - 4f * scale;
        var nameTop = center.Y + ringRadius + DjBadgeHeight * scale * 0.5f + 5f * scale;
        var name = Typography.FitText(dj.Name, textWidth, DjNameStyle);
        Typography.DrawCentered(drawList, new Vector2(item.Center.X, nameTop + Typography.LineHeight(DjNameStyle) * 0.5f),
            name, Ink.TitleInk, DjNameStyle);
        var metaTop = nameTop + Typography.LineHeight(DjNameStyle);
        Typography.DrawCentered(drawList, new Vector2(item.Center.X, metaTop + Typography.LineHeight(DjMetaStyle) * 0.5f),
            Typography.FitText(djMeta[index], textWidth, DjMetaStyle), Ink.MutedInk, DjMetaStyle);
        return interactive && UiInteract.Click(item.Min, item.Max, hovered);
    }

    private static void DrawDjBadge(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var label = Loc.T(L.Common.Live);
        var size = Typography.Measure(label, DjBadgeStyle);
        var height = DjBadgeHeight * scale;
        var width = size.X + 12f * scale;
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        var rim = new Vector2(2f * scale, 2f * scale);
        Squircle.Fill(drawList, min - rim, max + rim, height * 0.5f + rim.X, ImGui.GetColorU32(Ink.BackdropTop));
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(MediaOverlay.LiveGreen));
        Typography.DrawCentered(drawList, center, label, MediaOverlay.LiveInk, DjBadgeStyle);
    }

    private void OpenDjSheet(int index)
    {
        var dj = sections.Djs[index];
        var place = dj.World.Length > 0 && !string.Equals(dj.World, dj.Place, StringComparison.OrdinalIgnoreCase)
            ? dj.Place.Length > 0 ? $"{dj.Place} · {dj.World}" : dj.World
            : dj.Place;
        sheetDj = dj;
        djSheet.Open(dj, djViewers[index], place, djInitials[index]);
    }

    private void DrawDjSheet(Rect screen)
    {
        if (!djSheet.CapturesPointer || sheetDj is null)
        {
            return;
        }

        var action = djSheet.Draw(screen, Ink, images, artwork);
        if (action == VenueDjSheetAction.Twitch && !string.IsNullOrEmpty(sheetDj.TwitchUrl))
        {
            UrlActions.AskThenOpen(sheetDj.TwitchUrl);
        }
        else if (action == VenueDjSheetAction.Venue)
        {
            OpenDjVenue(sheetDj);
        }
    }

    private void OpenDjVenue(VenueDj dj)
    {
        if (dj.VenueId is { } venueId)
        {
            var events = venues.Events;
            for (var index = 0; index < events.Count; index++)
            {
                if (string.Equals(events[index].Id, venueId, StringComparison.Ordinal))
                {
                    OpenDetail(events[index]);
                    return;
                }
            }
        }
    }
}
