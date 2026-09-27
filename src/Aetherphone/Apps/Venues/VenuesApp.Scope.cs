using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private const float ScopeRowHeight = 56f;
    private const float ScopeWorldRowHeight = 44f;
    private const float ScopeWorldIndent = 30f;

    private static readonly TextStyle ScopeWorldStyle = TextStyles.Body;

    private readonly Dictionary<string, string> worldCountLabels = new(StringComparer.OrdinalIgnoreCase);
    private string expandedDataCenter = string.Empty;
    private object? worldCountLanguage;

    private void DrawScopeScreen(Rect area)
    {
        var scale = UiScale.Current;
        CheckLanguage();
        SocialChrome.DrawScreenHeader(area, Loc.T(L.Venues.ScopeTitle), Ink, back, ScreenTitleStyle, 0f,
            string.Empty, true, true);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            DrawNearYouCard(scale);
            var regions = VenueGeography.Regions;
            for (var index = 0; index < regions.Length; index++)
            {
                DrawRegionCard(regions[index], scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawNearYouCard(float scale)
    {
        DrawSectionHeading(Loc.Upper(Loc.T(L.Venues.NearYouSection)), scale);
        var homeWorld = CurrentWorld();
        var home = VenueGeography.DataCenterOfWorld(homeWorld);
        var region = home is null ? null : VenueGeography.RegionById(home.RegionId);
        var rows = home is null ? 1 : region is null ? 3 : 4;
        var card = BeginScopeCard(rows * ScopeRowHeight * scale, scale);
        var row = 0;
        var kind = configuration.VenueScope;
        if (home is not null)
        {
            if (ScopeRow(card, row++, rows, homeWorld, Loc.T(L.Venues.YourWorld), kind == VenueScopeKind.MyWorld,
                    scale))
            {
                PickScope(VenueScopeKind.MyWorld, string.Empty);
            }

            if (ScopeRow(card, row++, rows, home.Name, Loc.T(L.Venues.YourDataCenter),
                    kind == VenueScopeKind.MyDataCenter, scale))
            {
                PickScope(VenueScopeKind.MyDataCenter, string.Empty);
            }

            if (region is not null && ScopeRow(card, row++, rows, Loc.T(region.Label), Loc.T(L.Venues.YourRegion),
                    kind == VenueScopeKind.MyRegion, scale))
            {
                PickScope(VenueScopeKind.MyRegion, string.Empty);
            }
        }

        if (ScopeRow(card, row, rows, Loc.T(L.Venues.Everywhere), Loc.T(L.Venues.EverywhereHint),
                kind == VenueScopeKind.Everywhere, scale))
        {
            PickScope(VenueScopeKind.Everywhere, string.Empty);
        }

        EndScopeCard(card, scale);
    }

    private void DrawRegionCard(VenueRegionInfo region, float scale)
    {
        DrawSectionHeading(Loc.Upper(Loc.T(region.Label)), scale);
        var dataCenters = region.DataCenters;
        var expanded = ExpandedIn(region);
        var rows = 1 + dataCenters.Length;
        var worldRows = expanded is null ? 0 : expanded.Worlds.Length;
        var card = BeginScopeCard(rows * ScopeRowHeight * scale + worldRows * ScopeWorldRowHeight * scale, scale);
        var kind = configuration.VenueScope;
        var value = configuration.VenueScopeValue;
        var regionValue = region.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (ScopeRow(card, 0, rows + worldRows, Loc.T(L.Venues.AllOfRegion, Loc.T(region.Label)), string.Empty,
                kind == VenueScopeKind.Region && value == regionValue, scale))
        {
            PickScope(VenueScopeKind.Region, regionValue);
        }

        var top = card.Min.Y + ScopeRowHeight * scale;
        for (var index = 0; index < dataCenters.Length; index++)
        {
            var dataCenter = dataCenters[index];
            var isExpanded = ReferenceEquals(dataCenter, expanded);
            var rowRect = new Rect(new Vector2(card.Min.X, top), new Vector2(card.Max.X, top + ScopeRowHeight * scale));
            var last = index == dataCenters.Length - 1 && !isExpanded;
            var selected = kind == VenueScopeKind.DataCenter &&
                           string.Equals(value, dataCenter.Name, StringComparison.OrdinalIgnoreCase);
            var action = DataCenterRow(rowRect, dataCenter, selected, isExpanded, last, scale);
            if (action == 1)
            {
                PickScope(VenueScopeKind.DataCenter, dataCenter.Name);
            }
            else if (action == 2)
            {
                expandedDataCenter = isExpanded ? string.Empty : dataCenter.Name;
            }

            top = rowRect.Max.Y;
            if (!isExpanded)
            {
                continue;
            }

            for (var worldIndex = 0; worldIndex < dataCenter.Worlds.Length; worldIndex++)
            {
                var world = dataCenter.Worlds[worldIndex];
                var worldRect = new Rect(new Vector2(card.Min.X, top),
                    new Vector2(card.Max.X, top + ScopeWorldRowHeight * scale));
                var worldSelected = kind == VenueScopeKind.World &&
                                    string.Equals(value, world, StringComparison.OrdinalIgnoreCase);
                var lastWorld = index == dataCenters.Length - 1 && worldIndex == dataCenter.Worlds.Length - 1;
                if (WorldRow(worldRect, world, worldSelected, lastWorld, scale))
                {
                    PickScope(VenueScopeKind.World, world);
                }

                top = worldRect.Max.Y;
            }
        }

        EndScopeCard(card, scale);
    }

    private VenueDataCenterInfo? ExpandedIn(VenueRegionInfo region)
    {
        if (expandedDataCenter.Length == 0)
        {
            return null;
        }

        for (var index = 0; index < region.DataCenters.Length; index++)
        {
            if (string.Equals(region.DataCenters[index].Name, expandedDataCenter, StringComparison.OrdinalIgnoreCase))
            {
                return region.DataCenters[index];
            }
        }

        return null;
    }

    private Rect BeginScopeCard(float height, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var card = new Rect(new Vector2(origin.X + pad, origin.Y), new Vector2(origin.X + width - pad, origin.Y + height));
        ui.Card(ImGui.GetWindowDrawList(), card.Min, card.Max, Metrics.Radius.Card * scale, elevated: true);
        return card;
    }

    private static void EndScopeCard(Rect card, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        ImGui.SetCursorScreenPos(new Vector2(card.Min.X - CellPadX * scale, card.Min.Y));
        ImGui.Dummy(new Vector2(width, card.Height + Metrics.Space.Sm * scale));
    }

    private bool ScopeRow(Rect card, int index, int count, string title, string subtitle, bool selected, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = ScopeRowHeight * scale;
        var min = new Vector2(card.Min.X, card.Min.Y + index * rowHeight);
        var max = new Vector2(card.Max.X, min.Y + rowHeight);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            DrawRowHover(drawList, min, max, index, count, Metrics.Radius.Card * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var inset = CardInset * scale;
        var centerY = min.Y + rowHeight * 0.5f;
        var radioCenter = new Vector2(max.X - inset - 11f * scale, centerY);
        DrawRadio(drawList, radioCenter, selected, scale);
        DrawRowText(drawList, min.X + inset, radioCenter.X - 20f * scale, centerY, title, subtitle, selected);
        if (index < count - 1)
        {
            FeedCell.Hairline(drawList, min.X + inset, max.X, max.Y, Ink.Hairline);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private int DataCenterRow(Rect row, VenueDataCenterInfo dataCenter, bool selected, bool expanded, bool last,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(Ink.HoverTint),
                last ? Metrics.Radius.Card * scale : 0f,
                last ? ImDrawFlags.RoundCornersBottom : ImDrawFlags.RoundCornersNone);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var rowClicked = UiInteract.Click(row.Min, row.Max, hovered);
        var inset = CardInset * scale;
        var centerY = row.Center.Y;
        var radioCenter = new Vector2(row.Max.X - inset - 11f * scale, centerY);
        DrawRadio(drawList, radioCenter, selected, scale);
        var chevronCenter = new Vector2(radioCenter.X - 36f * scale, centerY);
        var chevronExtent = new Vector2(16f * scale, 16f * scale);
        var chevronHovered = UiInteract.Hover(chevronCenter - chevronExtent, chevronCenter + chevronExtent);
        if (chevronHovered)
        {
            drawList.AddCircleFilled(chevronCenter, 16f * scale, ImGui.GetColorU32(Ink.FieldFill), 24);
        }

        PhoneIcon.Draw(drawList, chevronCenter, expanded ? PhoneIcons.ChevronUp : PhoneIcons.ChevronDown,
            chevronHovered ? Ink.TitleInk : Ink.MutedInk, 16f * scale);
        var chevronClicked = UiInteract.Click(chevronCenter - chevronExtent, chevronCenter + chevronExtent,
            chevronHovered);
        DrawRowText(drawList, row.Min.X + inset, chevronCenter.X - 22f * scale, centerY, dataCenter.Name,
            WorldCountLabel(dataCenter), selected);
        if (!last)
        {
            FeedCell.Hairline(drawList, row.Min.X + inset, row.Max.X, row.Max.Y, Ink.Hairline);
        }

        if (chevronClicked)
        {
            return 2;
        }

        return rowClicked ? 1 : 0;
    }

    private bool WorldRow(Rect row, string world, bool selected, bool last, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(Ink.HoverTint),
                last ? Metrics.Radius.Card * scale : 0f,
                last ? ImDrawFlags.RoundCornersBottom : ImDrawFlags.RoundCornersNone);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var inset = CardInset * scale;
        var left = row.Min.X + inset + ScopeWorldIndent * scale;
        var radioCenter = new Vector2(row.Max.X - inset - 11f * scale, row.Center.Y);
        DrawRadio(drawList, radioCenter, selected, scale);
        drawList.AddCircleFilled(new Vector2(left - 14f * scale, row.Center.Y), 2.5f * scale,
            ImGui.GetColorU32(Ink.FaintInk), 12);
        var height = Typography.LineHeight(ScopeWorldStyle);
        Typography.Draw(drawList, new Vector2(left, row.Center.Y - height * 0.5f),
            Typography.FitText(world, MathF.Max(1f, radioCenter.X - 20f * scale - left), ScopeWorldStyle),
            selected ? Ink.TitleInk : Ink.BodyInk, ScopeWorldStyle);
        if (!last)
        {
            FeedCell.Hairline(drawList, left, row.Max.X, row.Max.Y, Ink.Hairline);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static void DrawRadio(ImDrawListPtr drawList, Vector2 center, bool selected, float scale) =>
        PhoneIcon.Draw(drawList, center, selected ? PhoneIcons.CircleCheckFilled : PhoneIcons.Circle,
            selected ? Ink.Accent : Ink.FaintInk, 22f * scale);

    private static void DrawRowText(ImDrawListPtr drawList, float left, float right, float centerY, string title,
        string subtitle, bool selected)
    {
        var width = MathF.Max(1f, right - left);
        var titleHeight = Typography.LineHeight(RowTitleStyle);
        var subtitleHeight = subtitle.Length > 0 ? Typography.LineHeight(RowHelpStyle) : 0f;
        var top = centerY - (titleHeight + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(title, width, RowTitleStyle),
            selected ? Ink.AccentLink : Ink.TitleInk, RowTitleStyle);
        if (subtitle.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(left, top + titleHeight),
                Typography.FitText(subtitle, width, RowHelpStyle), Ink.MutedInk, RowHelpStyle);
        }
    }

    private string WorldCountLabel(VenueDataCenterInfo dataCenter)
    {
        if (!ReferenceEquals(worldCountLanguage, Loc.Current))
        {
            worldCountLabels.Clear();
            worldCountLanguage = Loc.Current;
        }

        if (worldCountLabels.TryGetValue(dataCenter.Name, out var label))
        {
            return label;
        }

        label = Loc.T(L.Venues.WorldCount, dataCenter.Worlds.Length.ToString(Loc.Culture));
        worldCountLabels[dataCenter.Name] = label;
        return label;
    }

    private void PickScope(VenueScopeKind kind, string value)
    {
        SetScope(kind, value);
        router.Pop();
    }
}
