using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private const float SearchRowHeight = 52f;
    private const float SearchPillHeight = 40f;
    private const float SectionRowHeight = 34f;
    private const float RailGap = 10f;
    private const float RailArrowRadius = 14f;
    private const float RailPageFraction = 0.8f;
    private const float RailPageSmoothTime = 0.16f;
    private const float EmptyStateTop = 70f;
    private const int PageSize = 30;
    private const int ChipCount = 5;
    private const int FavoritesChip = 4;

    private static readonly TextStyle SectionStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle SeeAllStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle EmptyTitleStyle = TextStyles.Headline;
    private static readonly TextStyle EmptyBodyStyle = TextStyles.Subheadline;

    private readonly ChipRail timeRail = new();
    private readonly string[] chipLabels = new string[ChipCount];
    private readonly bool[] chipActive = new bool[ChipCount];
    private readonly KineticScroller railScroller = new();
    private bool railPressed;
    private bool railPaging;
    private float railPageTarget;
    private Spring railPageSpring;
    private int visibleCards = PageSize;

    private VenueArt Art => new(images, artwork);

    private void ResetHomeScroll()
    {
        visibleCards = PageSize;
        railScroller.Reset();
        railPaging = false;
        timeRail.Reset();
    }

    private void DrawHome(Rect area)
    {
        var scale = UiScale.Current;
        RefreshQuery();
        DrawTopBar(area, scale);
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            DrawSearchRow(scale);
            var showRail = query.Rail.Count > 0 && configuration.VenueTimeFilter != VenueTimeFilter.LiveNow &&
                           search.Length == 0;
            if (showRail)
            {
                DrawRailHeading(scale);
                DrawLiveRail(scale);
            }

            DrawTimeChips(scale, showRail);
            if (query.Feed.Count == 0)
            {
                DrawEmptyState(listRect, scale);
            }
            else
            {
                DrawSectionHeading(labels.FeedHeading, scale);
                DrawFeed(scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawTopBar(Rect area, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var wordmark = DisplayName;
        var wordmarkSize = Typography.Measure(wordmark, WordmarkStyle);
        var wordmarkLeft = area.Min.X + CellPadX * scale;
        Typography.Draw(drawList, new Vector2(wordmarkLeft, rowCenterY - wordmarkSize.Y * 0.5f), wordmark,
            Ink.TitleInk, WordmarkStyle);
        if (venues.Busy)
        {
            LoadingPulse.Spinner(new Vector2(wordmarkLeft + wordmarkSize.X + 14f * scale, rowCenterY), 7f * scale,
                Ink.Accent);
        }

        if (DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 0), PhoneIcons.Refresh, Loc.T(L.Common.Refresh)))
        {
            venues.EnsureFresh(true);
        }

        var filtersCenter = SocialChrome.HeaderSlot(area, 1);
        if (DrawHeaderIcon(drawList, filtersCenter, PhoneIcons.AdjustmentsHorizontal, Loc.T(L.Venues.Filters),
                FiltersActive, ActiveFilterCount))
        {
            router.Push(VenueRoute.Filters);
        }

        var radius = SocialChrome.HeaderIconRadius * scale;
        var pillRight = filtersCenter.X - radius - 8f * scale;
        var pill = DrawScopePill(new Vector2(pillRight, rowCenterY), scale);
        UiAnchors.Report("venues.chips", new Rect(pill.Min, filtersCenter + new Vector2(radius, radius)));
    }

    private Rect DrawScopePill(Vector2 rightCenter, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var dataCenter = CurrentDataCenter();
        var label = dataCenter.Length > 0 ? dataCenter : Loc.T(L.Venues.AllDataCenters);
        var labelSize = Typography.Measure(label, ScopePillStyle);
        var width = labelSize.X + 30f * scale;
        var half = ScopePillHeight * scale * 0.5f;
        var rect = new Rect(new Vector2(rightCenter.X - width, rightCenter.Y - half),
            new Vector2(rightCenter.X, rightCenter.Y + half));
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var open = scopeMenu.IsOpenFor("venues.scope");
        Squircle.Fill(drawList, rect.Min, rect.Max, half,
            ImGui.GetColorU32(open ? Ink.AccentWash : hovered ? Ink.ChipHover : Ink.ChipFill));
        Squircle.Stroke(drawList, rect.Min, rect.Max, half,
            ImGui.GetColorU32(open ? Palette.WithAlpha(Ink.AccentLink, 0.6f) : Ink.ChipStroke), 1f);
        Typography.Draw(drawList, new Vector2(rect.Min.X + 11f * scale, rect.Center.Y - labelSize.Y * 0.5f), label,
            Ink.AccentLink, ScopePillStyle);
        PhoneIcon.Draw(drawList, new Vector2(rect.Max.X - 11f * scale, rect.Center.Y), PhoneIcons.ChevronDown,
            Palette.WithAlpha(Ink.AccentLink, 0.85f), 12f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            scopeMenu.Toggle("venues.scope", rect);
        }

        return rect;
    }

    private void DrawSearchRow(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var top = origin.Y + (SearchRowHeight - SearchPillHeight) * scale * 0.5f;
        var searchRect = new Rect(new Vector2(origin.X + pad, top),
            new Vector2(origin.X + width - pad, top + SearchPillHeight * scale));
        UiAnchors.Report("venues.search", searchRect);
        SearchField.Draw(searchRect, "##venueSearch", Loc.T(L.Venues.Search), ref search, AppPalettes.Venues, 80);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, SearchRowHeight * scale));
    }

    private void DrawSectionHeading(string label, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = SectionRowHeight * scale;
        var labelHeight = Typography.LineHeight(SectionStyle);
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(origin.X + CellPadX * scale, origin.Y + height - labelHeight - 6f * scale), label,
            Ink.FaintInk, SectionStyle);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawRailHeading(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        DrawSectionHeading(labels.RailHeading, scale);
        var drawList = ImGui.GetWindowDrawList();
        var height = SectionRowHeight * scale;
        var seeAll = Loc.T(L.Venues.SeeAll);
        var size = Typography.Measure(seeAll, SeeAllStyle);
        var textTop = origin.Y + height - size.Y - 6f * scale;
        var min = new Vector2(origin.X + width - CellPadX * scale - size.X, textTop);
        var max = min + size;
        var hitMin = min - new Vector2(8f * scale, 6f * scale);
        var hitMax = max + new Vector2(8f * scale, 6f * scale);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        Typography.Draw(drawList, min, seeAll, hovered ? Ink.TitleInk : Ink.AccentLink, SeeAllStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(hitMin, hitMax, hovered))
        {
            SetTimeFilter(VenueTimeFilter.LiveNow);
        }
    }

    private void DrawLiveRail(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var cardWidth = VenueCard.RailWidth * scale;
        var cardHeight = VenueCard.RailHeight * scale;
        var gap = RailGap * scale;
        var count = query.Rail.Count;
        var content = pad * 2f + count * cardWidth + (count - 1) * gap;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + cardHeight));
        var maxOffset = MathF.Max(0f, content - width);
        UiAnchors.Report("venues.live", row);
        railScroller.Scale = scale;
        railScroller.SetBounds(maxOffset);
        HandleRailDrag(row);
        StepRailPaging();
        var interactive = !railScroller.IsDragging;
        drawList.PushClipRect(row.Min, row.Max, true);
        var art = Art;
        for (var index = 0; index < count; index++)
        {
            var left = origin.X + pad + index * (cardWidth + gap) - railScroller.Offset;
            if (left + cardWidth < row.Min.X || left > row.Max.X)
            {
                continue;
            }

            var rest = new Rect(new Vector2(left, origin.Y), new Vector2(left + cardWidth, origin.Y + cardHeight));
            var venue = query.Rail[index];
            if (VenueCard.DrawRail(drawList, rest, venue, labels.Rail(index), art, Ink, interactive))
            {
                OpenDetail(venue);
            }
        }

        DrawRailArrows(drawList, row, maxOffset, scale);
        drawList.PopClipRect();
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + Metrics.Space.Sm * scale));
    }

    private void DrawRailArrows(ImDrawListPtr drawList, Rect row, float maxOffset, float scale)
    {
        var offset = railScroller.Offset;
        var page = row.Width * RailPageFraction;
        var inset = CellPadX * scale;
        if (offset > 0.5f && DrawRailArrow(drawList, new Vector2(row.Min.X + inset, row.Center.Y),
                PhoneIcons.ChevronLeft, scale))
        {
            PageRail(offset - page, maxOffset);
        }

        if (offset < maxOffset - 0.5f && DrawRailArrow(drawList, new Vector2(row.Max.X - inset, row.Center.Y),
                PhoneIcons.ChevronRight, scale))
        {
            PageRail(offset + page, maxOffset);
        }
    }

    private static bool DrawRailArrow(ImDrawListPtr drawList, Vector2 center, string glyph, float scale)
    {
        var radius = RailArrowRadius * scale;
        var extent = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - extent, center + extent);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(hovered ? MediaOverlay.HoverFill : MediaOverlay.Fill), 28);
        PhoneIcon.Draw(drawList, center, glyph, MediaOverlay.White, 16f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - extent, center + extent, hovered);
    }

    private void PageRail(float target, float maxOffset)
    {
        railPageSpring = new Spring(railScroller.Offset);
        railPageTarget = Math.Clamp(target, 0f, maxOffset);
        railPaging = true;
    }

    private void StepRailPaging()
    {
        if (!railPaging)
        {
            return;
        }

        if (railScroller.IsDragging)
        {
            railPaging = false;
            return;
        }

        var offset = railPageSpring.Step(railPageTarget, RailPageSmoothTime,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        if (railPageSpring.IsResting(railPageTarget, 0.5f, 1f))
        {
            offset = railPageTarget;
            railPaging = false;
        }

        railScroller.SyncOffset(offset);
    }

    private void HandleRailDrag(Rect row)
    {
        var io = ImGui.GetIO();
        var deltaSeconds = io.DeltaTime;
        var mouseX = io.MousePos.X;
        var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var shouldBlock = false;
        if (railPressed)
        {
            if (down)
            {
                var wasDragging = railScroller.IsDragging;
                railScroller.Move(mouseX, deltaSeconds);
                if (!wasDragging && railScroller.IsDragging)
                {
                    UiInteract.CancelPendingTap();
                }

                shouldBlock = railScroller.IsDragging;
            }
            else
            {
                shouldBlock = railScroller.IsDragging;
                railScroller.Release();
                railPressed = false;
                railScroller.Tick(deltaSeconds);
            }
        }
        else if (down && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && UiInteract.Hover(row.Min, row.Max) &&
                 !UiInteract.InputBlocked)
        {
            railScroller.Press(mouseX);
            railPressed = true;
        }
        else if (!railPaging)
        {
            railScroller.Tick(deltaSeconds);
        }

        if (shouldBlock)
        {
            UiInteract.BlockThisFrame();
        }
    }

    private void DrawTimeChips(float scale, bool railShown)
    {
        chipLabels[0] = Loc.T(L.Venues.LiveNow);
        chipLabels[1] = Loc.T(L.Venues.Today);
        chipLabels[2] = Loc.T(L.Venues.ThisWeek);
        chipLabels[3] = Loc.T(L.Venues.Directory);
        chipLabels[FavoritesChip] = Loc.T(L.Venues.Favorites);
        var current = (int)configuration.VenueTimeFilter;
        for (var index = 0; index < FavoritesChip; index++)
        {
            chipActive[index] = index == current;
        }

        chipActive[FavoritesChip] = favoritesOnly;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var height = ChipRail.RowHeight * scale;
        var top = origin.Y + Metrics.Space.Xs * scale;
        var row = new Rect(new Vector2(origin.X + pad, top), new Vector2(origin.X + width - pad, top + height));
        if (!railShown)
        {
            UiAnchors.Report("venues.live", row);
        }

        var tapped = timeRail.Draw(row, ui, chipLabels, chipActive, false, "venues.time");
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Xs * 2f * scale));
        if (tapped == FavoritesChip)
        {
            favoritesOnly = !favoritesOnly;
            visibleCards = PageSize;
            return;
        }

        if (tapped >= 0)
        {
            SetTimeFilter((VenueTimeFilter)tapped);
        }
    }

    private void SetTimeFilter(VenueTimeFilter filter)
    {
        visibleCards = PageSize;
        if (configuration.VenueTimeFilter == filter)
        {
            return;
        }

        configuration.VenueTimeFilter = filter;
        configuration.Save();
    }

    private void DrawFeed(float scale)
    {
        var feed = query.Feed;
        var count = Math.Min(feed.Count, visibleCards);
        var art = Art;
        for (var index = 0; index < count; index++)
        {
            var venue = feed[index];
            var action = VenueCard.DrawFeed(venue, labels.Feed(index), IsFavorite(venue.Id), art, Ink);
            if (action == VenueCardAction.Open)
            {
                OpenDetail(venue);
            }
            else if (action == VenueCardAction.ToggleFavorite)
            {
                ToggleFavorite(venue.Id);
            }
        }

        if (feed.Count <= count)
        {
            return;
        }

        if (InfiniteScroll.ReachedBottom())
        {
            visibleCards += PageSize;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        LoadingPulse.Spinner(new Vector2(origin.X + width * 0.5f, origin.Y + 20f * scale), 7f * scale, Ink.Accent);
        ImGui.Dummy(new Vector2(width, 40f * scale));
    }

    private void DrawEmptyState(Rect body, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var drawList = ImGui.GetWindowDrawList();
        if (venues.State is VenueState.Loading or VenueState.Idle && venues.Events.Count == 0)
        {
            Skeleton.Feed(drawList, new Rect(new Vector2(origin.X + CellPadX * scale, origin.Y + 8f * scale),
                new Vector2(origin.X + width - CellPadX * scale, body.Max.Y - 12f * scale)), scale);
            ImGui.Dummy(new Vector2(width, body.Max.Y - origin.Y));
            return;
        }

        var failed = venues.State == VenueState.Failed && venues.Events.Count == 0;
        var centerX = origin.X + width * 0.5f;
        var iconCenter = new Vector2(centerX, origin.Y + EmptyStateTop * scale);
        drawList.AddCircleFilled(iconCenter, 30f * scale, ImGui.GetColorU32(Ink.AccentWash), 40);
        PhoneIcon.Draw(drawList, iconCenter, failed ? PhoneIcons.InfoCircle : PhoneIcons.MapPin, Ink.AccentLink,
            26f * scale);
        var maxWidth = MathF.Max(1f, width - CellPadX * 2f * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList,
            failed ? Loc.T(L.Venues.Failed) : Loc.T(L.Venues.NoVenues), EmptyTitleStyle, Ink.TitleInk,
            new Vector2(centerX, iconCenter.Y + 46f * scale), maxWidth);
        var bottom = titleBottom;
        if (failed)
        {
            var label = Loc.T(L.Venues.Retry);
            var retryWidth = Typography.Measure(label, TextStyles.SubheadlineEmphasized).X + 44f * scale;
            var retry = new Rect(new Vector2(centerX - retryWidth * 0.5f, titleBottom + 14f * scale),
                new Vector2(centerX + retryWidth * 0.5f, titleBottom + 50f * scale));
            if (SocialPill.Accent(drawList, retry, label, Ink, TextStyles.SubheadlineEmphasized, retry.Height * 0.5f))
            {
                venues.EnsureFresh(true);
            }

            bottom = retry.Max.Y;
        }
        else
        {
            bottom = Typography.DrawWrappedCentered(drawList, Loc.T(L.Venues.EmptyHint), EmptyBodyStyle,
                Ink.MutedInk, new Vector2(centerX, titleBottom + 6f * scale), maxWidth);
        }

        ImGui.Dummy(new Vector2(width, bottom - origin.Y + Metrics.Space.Lg * scale));
    }
}
