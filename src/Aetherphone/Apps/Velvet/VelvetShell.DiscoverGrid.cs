using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const int GridFillBelow = 8;
    private const float GridGap = 10f;
    private const float GridCardAspect = 0.82f;
    private const float GridCoverFocus = 0.2f;
    private const float GridDetailGlyphGap = 6f;
    private const float GridHoverLift = 4f;
    private const float GridHoverSmoothTime = 0.11f;
    private const float GridPressShrink = 0.97f;
    private const float GridShadowOpacity = 0.35f;
    private const float GridFooterTop = 18f;
    private const float GridBottomPad = 28f;

    private readonly List<GridLabel> gridLabels = new();
    private readonly List<VelvetFitItem> gridFitScratch = new();
    private LanguageInfo? gridLabelsLanguage;
    private string gridHiddenLabel = string.Empty;
    private int gridHiddenCount = -1;
    private bool gridScrollTopPending;
    private bool deckModeSynced;

    private readonly record struct GridLabel(string NameId, string MetaLine, string PhotoBadge, string Fit,
        bool FitWarns);

    private void DrawDiscoverGrid(Rect body)
    {
        RefillDeck(GridFillBelow);
        if (deck.Count == 0)
        {
            DrawDeckEmpty(body);
            return;
        }

        EnsureStamps();
        EnsureGridLabels();
        var scale = UiScale.Current;
        using (var surface = AppSurface.BeginEdgeToEdge(body))
        {
            if (gridScrollTopPending)
            {
                surface.JumpToTop();
                gridScrollTopPending = false;
            }

            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            var inset = DeckCardInset * scale;
            var gap = GridGap * scale;
            var cardWidth = MathF.Max(1f, width - inset * 2f);
            var cardHeight = cardWidth * GridCardAspect;
            var origin = ImGui.GetCursorScreenPos();
            for (var index = 0; index < deck.Count; index++)
            {
                var min = new Vector2(origin.X + inset, origin.Y + inset + index * (cardHeight + gap));
                var card = new Rect(min, new Vector2(min.X + cardWidth, min.Y + cardHeight));
                if (index == 0)
                {
                    UiAnchors.Report("velvet.discover.card", card);
                }

                if (ImGui.IsRectVisible(card.Min, card.Max))
                {
                    DrawGridCard(drawList, index, card, scale);
                }
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, inset + deck.Count * cardHeight + (deck.Count - 1) * gap));
            DrawGridFooter(origin.X + width * 0.5f, width);
            Gap(GridBottomPad);
        }
    }

    private void DrawGridCard(ImDrawListPtr drawList, int index, Rect card, float scale)
    {
        var profile = deck[index];
        var label = gridLabels[index];
        var name = DisplayNameOf(profile.DisplayName, profile.Handle);
        var photos = CardPhotos(profile);
        var hovered = UiInteract.Hover(card.Min, card.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var eased = VAnim.Toggle(label.NameId, hovered, delta, GridHoverSmoothTime);
        var grow = GridHoverLift * scale * eased
            - card.Width * 0.5f * (1f - PressFx.Scale(label.NameId, pressed, GridPressShrink));
        var body = new Rect(card.Min - new Vector2(grow, grow), card.Max + new Vector2(grow, grow));
        var radius = DeckCoverRadius * scale;
        var pad = DeckCoverPad * scale;
        Elevation.Card(drawList, body.Min, body.Max, radius, scale, GridShadowOpacity * (1f + eased));
        var coverUrl = photos.Length > 0 ? photos[0].Url : profile.AvatarUrl ?? string.Empty;
        DrawCoverImage(drawList, body.Min, body.Max, coverUrl, radius, name, GridCoverFocus);
        Squircle.FillVerticalGradient(drawList, new Vector2(body.Min.X, body.Max.Y - body.Height * DeckScrimShare),
            body.Max, radius, VelvetTheme.Alpha(VelvetTheme.GroundBottom, 0f).Packed(),
            VelvetTheme.Alpha(VelvetTheme.GroundBottom, 0.94f).Packed());
        if (eased > 0.001f)
        {
            Squircle.Stroke(drawList, body.Min, body.Max, radius,
                VelvetTheme.Alpha(VelvetTheme.OnAccent, DeckRimAlpha * eased).Packed(), DeckRimWeight * scale);
        }

        var badgeLeft = DrawDeckSeenBadge(drawList, profile.UserId, body, body.Min.X + pad, scale);
        if (label.PhotoBadge.Length > 0)
        {
            DrawDeckCoverBadge(drawList, label.PhotoBadge, PhoneIcons.Photo, VelvetTheme.RoseInk, body, badgeLeft,
                scale);
        }

        DrawDeckPresence(drawList, profile.Presence, body, pad, scale);

        var textLeft = body.Min.X + pad;
        var textWidth = MathF.Max(1f, body.Width - pad * 2f);
        var lineGap = DeckCoverLineGap * scale;
        var intentHeight = Typography.LineHeight(DeckIntentStyle);
        var bottom = body.Max.Y - pad;
        if (label.Fit.Length > 0)
        {
            bottom -= intentHeight;
            DrawGridFit(drawList, in label, textLeft, bottom, textWidth, scale);
            bottom -= lineGap;
        }

        var intentY = bottom - intentHeight;
        Typography.Draw(drawList, new Vector2(textLeft, intentY),
            Typography.FitText(VelvetIntent.Summary(profile.LookingFor), textWidth, DeckIntentStyle),
            VelvetTheme.RoseInk, DeckIntentStyle);
        bottom = intentY - lineGap;
        if (label.MetaLine.Length > 0)
        {
            var metaY = bottom - Typography.LineHeight(DeckMetaStyle);
            Typography.Draw(drawList, new Vector2(textLeft, metaY),
                Typography.FitText(label.MetaLine, textWidth, DeckMetaStyle), VelvetTheme.BodyInk, DeckMetaStyle);
            bottom = metaY - lineGap;
        }

        var nameY = bottom - Typography.LineHeight(DeckNameStyle);
        UserName.Draw(drawList, label.NameId, name, profile.Badges, profile.BadgeIds, textLeft, nameY, textWidth,
            DeckNameStyle, VelvetTheme.TitleInk, hovered, false);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            OpenProfile(profile.UserId);
        }
    }

    private static void DrawGridFit(ImDrawListPtr drawList, in GridLabel label, float left, float top, float width,
        float scale)
    {
        var glyphSize = VIcon.Chip * scale;
        var glyphCenter = new Vector2(left + glyphSize * 0.5f, top + Typography.LineHeight(DeckIntentStyle) * 0.5f);
        PhoneIcon.Draw(drawList, glyphCenter, label.FitWarns ? PhoneIcons.Ban : PhoneIcons.Check,
            label.FitWarns ? VelvetTheme.Danger : VelvetTheme.Online, glyphSize);
        var textLeft = left + glyphSize + GridDetailGlyphGap * scale;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(label.Fit, MathF.Max(1f, width - glyphSize - GridDetailGlyphGap * scale),
                DeckIntentStyle),
            label.FitWarns ? VelvetTheme.ToneInk(VelvetTheme.Danger) : VelvetTheme.BodyInk, DeckIntentStyle);
    }

    private void DrawGridFooter(float centerX, float width)
    {
        if (store.LoadingMoreDiscover)
        {
            InfiniteScroll.DrawLoadingRow(centerX, VelvetTheme.MutedInk);
            return;
        }

        if (store.HasMoreDiscover)
        {
            if (!store.LoadingDiscover && InfiniteScroll.ReachedBottom())
            {
                store.LoadMoreDiscover();
            }

            return;
        }

        var passCount = store.PassCount;
        if (passCount == 0 || store.LoadingDiscover)
        {
            return;
        }

        if (gridHiddenCount != passCount)
        {
            gridHiddenCount = passCount;
            gridHiddenLabel = Loc.T(L.Velvet.DeckPassedHidden, passCount);
        }

        var scale = UiScale.Current;
        Gap(GridFooterTop);
        var origin = ImGui.GetCursorScreenPos();
        var hintBottom = Typography.DrawWrappedCentered(ImGui.GetWindowDrawList(), gridHiddenLabel,
            TextStyles.Footnote, VelvetTheme.MutedInk, new Vector2(centerX, origin.Y),
            MathF.Max(1f, width - FeedCell.PadX * 2f * scale));
        var actionTop = hintBottom + EndActionGap * scale;
        var body = new Rect(new Vector2(centerX - width * 0.5f, origin.Y),
            new Vector2(centerX + width * 0.5f, actionTop + EndActionHeight * scale));
        if (DrawEndAction(body, ref actionTop, Loc.T(L.Velvet.DeckShowAgain), ConfirmButtonTone.Neutral,
                "velvet.grid.showAgain"))
        {
            ShowPassesAgain();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, actionTop - origin.Y));
    }

    private void EnsureGridLabels()
    {
        if (ReferenceEquals(gridLabelsLanguage, Loc.Current) && gridLabels.Count == deck.Count)
        {
            return;
        }

        FillGridLabels(0);
    }

    private void FillGridLabels(int from)
    {
        gridLabelsLanguage = Loc.Current;
        gridHiddenCount = -1;
        if (gridLabels.Count > from)
        {
            gridLabels.RemoveRange(from, gridLabels.Count - from);
        }

        for (var index = from; index < deck.Count; index++)
        {
            gridLabels.Add(GridLabelOf(deck[index]));
        }
    }

    private GridLabel GridLabelOf(VelvetProfileDto profile)
    {
        var nameId = "velvet.grid.name." + profile.UserId;
        var metaLine = GridMetaLine(profile);
        var photoCount = CardPhotos(profile).Length;
        var photoBadge = photoCount > 1 ? Loc.Plural(L.Velvet.PhotoBadge, photoCount) : string.Empty;
        if (deckMe is { } me)
        {
            VelvetFit.Describe(me, profile, gridFitScratch);
            var headline = VelvetFit.Headline(gridFitScratch);
            if (headline >= 0)
            {
                var item = gridFitScratch[headline];
                return new GridLabel(nameId, metaLine, photoBadge, FitLabel(in item), VelvetFit.Warns(item.Kind));
            }
        }

        return new GridLabel(nameId, metaLine, photoBadge, string.Empty, false);
    }

    private string GridMetaLine(VelvetProfileDto profile)
    {
        var region = RegionCodeOf(profile);
        var race = profile.Race > 0 ? VelvetRace.Label(gameData, profile.Race) : string.Empty;
        if (region.Length == 0)
        {
            return race;
        }

        return race.Length > 0 ? string.Concat(region, FilterSummarySeparator, race) : region;
    }
}
