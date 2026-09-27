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
    private const float DjRowHeight = 60f;
    private const float DjAvatar = 40f;
    private const float DjRing = 2f;
    private const float DjRingGap = 2.5f;

    private static readonly Vector4 DjInitialInk = new(1f, 1f, 1f, 0.92f);

    private readonly List<VenueDj> detailDjs = new();
    private readonly List<string> detailDjMeta = new();
    private readonly List<string> detailDjInitials = new();

    private void CollectDetailDjs(string venueId)
    {
        detailDjs.Clear();
        detailDjMeta.Clear();
        detailDjInitials.Clear();
        var djs = venues.Djs;
        for (var index = 0; index < djs.Count; index++)
        {
            var dj = djs[index];
            if (!string.Equals(dj.VenueId, venueId, StringComparison.Ordinal))
            {
                continue;
            }

            var viewers = dj.Viewers > 0 ? VenueFormat.Viewers(dj.Viewers) : Loc.T(L.Venues.LiveNowLabel);
            detailDjs.Add(dj);
            detailDjMeta.Add(dj.Genres.Count > 0 ? $"{dj.Genres[0]} · {viewers}" : viewers);
            detailDjInitials.Add(VenueLabelCache.InitialOf(dj.Name));
        }
    }

    private void DrawDetailDjs(float scale)
    {
        var count = detailDjs.Count;
        if (count == 0)
        {
            return;
        }

        DrawSectionHeading(Loc.Upper(Loc.T(L.Venues.DjsOnAir)), scale);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var rowHeight = DjRowHeight * scale;
        var card = new Rect(new Vector2(origin.X + pad, origin.Y),
            new Vector2(origin.X + width - pad, origin.Y + count * rowHeight));
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        for (var index = 0; index < count; index++)
        {
            var rowMin = new Vector2(card.Min.X, card.Min.Y + index * rowHeight);
            var rowMax = new Vector2(card.Max.X, rowMin.Y + rowHeight);
            DrawDetailDjRow(drawList, rowMin, rowMax, index, count, rounding, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, card.Height + Metrics.Space.Sm * scale));
    }

    private void DrawDetailDjRow(ImDrawListPtr drawList, Vector2 rowMin, Vector2 rowMax, int index, int count,
        float rounding, float scale)
    {
        var dj = detailDjs[index];
        var hasTwitch = !string.IsNullOrEmpty(dj.TwitchUrl);
        var hovered = hasTwitch && UiInteract.Hover(rowMin, rowMax);
        if (hovered)
        {
            drawList.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(Ink.HoverTint), rounding,
                count == 1 ? ImDrawFlags.RoundCornersAll :
                index == 0 ? ImDrawFlags.RoundCornersTop :
                index == count - 1 ? ImDrawFlags.RoundCornersBottom : ImDrawFlags.RoundCornersNone);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var inset = 14f * scale;
        var centerY = rowMin.Y + (rowMax.Y - rowMin.Y) * 0.5f;
        var radius = DjAvatar * scale * 0.5f;
        var ringRadius = radius + DjRingGap * scale;
        var center = new Vector2(rowMin.X + inset + ringRadius, centerY);
        var pulse = 0.6f + 0.4f * Pulse.Wave(Pulse.Calm);
        drawList.AddCircle(center, ringRadius, ImGui.GetColorU32(Palette.WithAlpha(MediaOverlay.LiveGreen, pulse)),
            32, DjRing * scale);
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
            Typography.DrawCentered(drawList, center, detailDjInitials[index], DjInitialInk, radius / (22f * scale),
                FontWeight.Bold);
        }

        var textLeft = center.X + ringRadius + 12f * scale;
        var textRight = rowMax.X - inset - (hasTwitch ? 26f * scale : 0f);
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var nameHeight = Typography.LineHeight(InfoValueStyle);
        var metaHeight = Typography.LineHeight(CaptionStyle);
        var textTop = centerY - (nameHeight + 2f * scale + metaHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(dj.Name, textWidth, InfoValueStyle), Ink.TitleInk, InfoValueStyle);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight + 2f * scale),
            Typography.FitText(detailDjMeta[index], textWidth, CaptionStyle), Ink.MutedInk, CaptionStyle);
        if (hasTwitch)
        {
            PhoneIcon.Draw(drawList, new Vector2(rowMax.X - inset - 8f * scale, centerY), PhoneIcons.ExternalLink,
                Ink.AccentLink, 16f * scale);
        }

        if (index < count - 1)
        {
            FeedCell.Hairline(drawList, textLeft, rowMax.X - inset, rowMax.Y, Ink.Hairline);
        }

        if (hasTwitch && UiInteract.Click(rowMin, rowMax, hovered))
        {
            UrlActions.AskThenOpen(dj.TwitchUrl!);
        }
    }
}
