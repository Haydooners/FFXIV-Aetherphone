using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp
{
    private const float ToggleCardPad = 14f;
    private const float ToggleWidth = 46f;
    private const float ToggleHeight = 28f;

    private static readonly TextStyle ToggleTitleStyle = TextStyles.BodyEmphasized;
    private static readonly TextStyle ToggleHelpStyle = TextStyles.Footnote;

    private readonly ChipRail sourceRail = new();
    private readonly string[] sourceLabels = new string[VenueFilter.SourceCount];
    private readonly bool[] sourceActive = new bool[VenueFilter.SourceCount];
    private readonly SortedSet<string> tagSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> tagList = new();
    private (int Version, int Source, string DataCenter) tagStamp = (-1, -1, string.Empty);

    private void DrawFilters(Rect area)
    {
        var scale = UiScale.Current;
        var reset = Loc.T(L.Venues.ResetFilters);
        var reserve = FiltersActive ? AppSkin.HeaderActionWidth(reset) / scale + 8f : 0f;
        SocialChrome.DrawScreenHeader(area, Loc.T(L.Venues.Filters), Ink, back, ScreenTitleStyle, reserve,
            string.Empty, true, true);
        if (FiltersActive && ui.HeaderAction(area, reset, true))
        {
            ResetFilters();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            DrawSectionHeading(Loc.Upper(Loc.T(L.Venues.Sources)), scale);
            DrawSourceChips(scale);
            DrawSectionHeading(Loc.Upper(Loc.T(L.Venues.Tags)), scale);
            DrawTagFlow(scale);
            DrawSectionHeading(Loc.Upper(Loc.T(L.Apps.Notifications)), scale);
            DrawNotifyToggle(scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawSourceChips(float scale)
    {
        sourceLabels[VenueFilter.SourceAll] = Loc.T(L.Venues.AllSources);
        sourceLabels[VenueFilter.SourceFfxiv] = Loc.T(L.Venues.SourceFfxiv);
        sourceLabels[VenueFilter.SourcePartake] = Loc.T(L.Venues.SourcePartake);
        sourceLabels[VenueFilter.SourceRolladeck] = Loc.T(L.Venues.SourceRolladeck);
        for (var index = 0; index < sourceActive.Length; index++)
        {
            sourceActive[index] = index == configuration.VenueSourceFilter;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var height = ChipRail.RowHeight * scale;
        var row = new Rect(new Vector2(origin.X + pad, origin.Y), new Vector2(origin.X + width - pad, origin.Y + height));
        var tapped = sourceRail.Draw(row, ui, sourceLabels, sourceActive);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        if (tapped < 0 || tapped == configuration.VenueSourceFilter)
        {
            return;
        }

        configuration.VenueSourceFilter = tapped;
        configuration.Save();
        visibleCards = PageSize;
    }

    private void RefreshTagList()
    {
        var dataCenter = CurrentDataCenter();
        var stamp = (venues.Version, configuration.VenueSourceFilter, dataCenter);
        if (stamp == tagStamp)
        {
            return;
        }

        tagStamp = stamp;
        VenueFilter.CollectTags(venues.Events, configuration.VenueSourceFilter, dataCenter, tagSet);
        tagList.Clear();
        foreach (var tag in tagSet)
        {
            tagList.Add(tag);
        }
    }

    private void DrawTagFlow(float scale)
    {
        RefreshTagList();
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        if (tagList.Count == 0)
        {
            Typography.Draw(drawList, new Vector2(origin.X + CellPadX * scale, origin.Y), Loc.T(L.Venues.NoVenues),
                Ink.MutedInk, TextStyles.Subheadline);
            ImGui.Dummy(new Vector2(width, Typography.LineHeight(TextStyles.Subheadline) + Metrics.Space.Sm * scale));
            return;
        }

        var left = origin.X + CellPadX * scale;
        var right = origin.X + width - CellPadX * scale;
        var gap = Metrics.Space.Sm * scale;
        var chipHeight = VenueChips.LargeHeight(scale);
        var lineHeight = chipHeight + gap;
        var cursorX = left;
        var cursorY = origin.Y;
        for (var index = 0; index < tagList.Count; index++)
        {
            var tag = tagList[index];
            var chipWidth = VenueChips.MeasureLarge(tag, scale);
            if (cursorX + chipWidth > right && cursorX > left)
            {
                cursorX = left;
                cursorY += lineHeight;
            }

            var min = new Vector2(cursorX, cursorY);
            var max = new Vector2(cursorX + chipWidth, cursorY + chipHeight);
            var hovered = UiInteract.Hover(min, max);
            VenueChips.DrawLarge(drawList, min, tag, IsTagSelected(tag), hovered, scale);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                ToggleTag(tag);
                visibleCards = PageSize;
            }

            cursorX += chipWidth + gap;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursorY - origin.Y + lineHeight));
    }

    private void DrawNotifyToggle(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var inner = ToggleCardPad * scale;
        var cardLeft = origin.X + pad;
        var cardRight = origin.X + width - pad;
        var toggleWidth = ToggleWidth * scale;
        var textLeft = cardLeft + inner;
        var textWidth = MathF.Max(1f, cardRight - inner - toggleWidth - 12f * scale - textLeft);
        var titleHeight = Typography.LineHeight(ToggleTitleStyle);
        var helpHeight = Typography.CountWrappedLines(Loc.T(L.Venues.NotifyNewHelp), ToggleHelpStyle, textWidth) *
                         Typography.LineHeight(ToggleHelpStyle);
        var cardHeight = inner * 2f + titleHeight + 3f * scale + helpHeight;
        var cardMax = new Vector2(cardRight, origin.Y + cardHeight);
        ui.Card(drawList, new Vector2(cardLeft, origin.Y), cardMax, Metrics.Radius.Card * scale, elevated: true);
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + inner), Loc.T(L.Venues.NotifyNew), Ink.TitleInk,
            ToggleTitleStyle);
        Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + inner + titleHeight + 3f * scale),
            Loc.T(L.Venues.NotifyNewHelp), Ink.MutedInk, ToggleHelpStyle, textWidth);
        var toggleMin = new Vector2(cardRight - inner - toggleWidth, origin.Y + (cardHeight - ToggleHeight * scale) * 0.5f);
        var toggle = new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, ToggleHeight * scale));
        var next = Toggle.Draw("venues.notifyNew", toggle, configuration.VenueNotifyNewEvents, theme);
        if (next != configuration.VenueNotifyNewEvents)
        {
            configuration.VenueNotifyNewEvents = next;
            configuration.Save();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + Metrics.Space.Sm * scale));
    }
}
