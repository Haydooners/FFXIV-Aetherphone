using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal enum VelvetFilterFacet
{
    Region,
    Race,
    Intent,
    Gender,
    Sexuality,
    Relationship,
    Role,
    Kinks,
    Limits,
    Tags,
}

internal enum VelvetOptionState
{
    Any,
    Shown,
    Hidden,
}

internal sealed partial class VelvetShell
{
    private const float FilterFooterHeight = 64f;
    private const float FilterSectionHeaderHeight = 52f;
    private const float FilterOptionRowHeight = 44f;
    private const float FilterGroupHeaderHeight = 28f;
    private const float FilterMarkRadius = 14f;
    private const float FilterMarkGap = 8f;
    private const float FilterRevealSmoothTime = 0.14f;
    private const float FilterRevealPadY = 8f;

    private static readonly VelvetFilterFacet[] FilterFacets =
    {
        VelvetFilterFacet.Region,
        VelvetFilterFacet.Race,
        VelvetFilterFacet.Intent,
        VelvetFilterFacet.Gender,
        VelvetFilterFacet.Sexuality,
        VelvetFilterFacet.Relationship,
        VelvetFilterFacet.Role,
        VelvetFilterFacet.Kinks,
        VelvetFilterFacet.Limits,
        VelvetFilterFacet.Tags,
    };

    private readonly VelvetFilterSelection mutes = new();
    private readonly Spring[] facetReveal = new Spring[FilterFacets.Length];
    private readonly List<string> facetLabels = new();
    private int expandedFacets;
    private VelvetPage filterSurface = VelvetPage.Discover;

    private VelvetFilterSelection IncludeFor(VelvetPage surface) =>
        surface == VelvetPage.Feed ? feedInclude : discoverInclude;

    private void LoadMutes() => mutes.LoadFrom(configuration.VelvetMutes);

    private void SaveMutes()
    {
        mutes.SaveInto(configuration.VelvetMutes);
        configuration.Save();
    }

    private void ApplyDiscoverFilters() =>
        store.RefreshDiscover(VelvetFilterSelection.Combine(discoverInclude, mutes), discoverApplied.Trim(),
            SocialRegion.FilterCsv(discoverInclude.RegionMask) ?? string.Empty);

    private void ApplyFeedFilters() =>
        store.SetFeedFilter(VelvetFilterSelection.Combine(feedInclude, mutes),
            SocialRegion.FilterCsv(feedInclude.RegionMask) ?? string.Empty);

    private void ApplyFilters(VelvetPage surface)
    {
        if (surface == VelvetPage.Feed)
        {
            ApplyFeedFilters();
            return;
        }

        ApplyDiscoverFilters();
    }

    private void ApplyMutesEverywhere()
    {
        SaveMutes();
        ApplyDiscoverFilters();
        ApplyFeedFilters();
    }

    private void OpenFilters(VelvetPage surface)
    {
        filterSurface = surface;
        router.Push(VelvetView.Filters);
    }

    private void DrawFilters(Rect area)
    {
        var scale = UiScale.Current;
        var include = IncludeFor(filterSurface);
        if (VHeader.Push(area, Loc.T(L.Velvet.FiltersTitle), 2))
        {
            router.Pop();
            return;
        }

        if ((include.Any || mutes.Any) && ui.HeaderAction(area, Loc.T(L.Velvet.FilterClearAll), true))
        {
            include.Clear();
            mutes.Clear();
            ApplyMutesEverywhere();
        }

        var footerHeight = FilterFooterHeight * scale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale),
            new Vector2(area.Max.X, area.Max.Y - footerHeight));
        var changedInclude = false;
        var changedMutes = false;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            Gap(8f);
            DrawInsetHelpText(Loc.T(L.Velvet.FilterPickHint));
            Gap(8f);
            for (var index = 0; index < FilterFacets.Length; index++)
            {
                DrawFacetSection(FilterFacets[index], include, index, ref changedInclude, ref changedMutes);
            }

            Gap(30f);
        }

        DrawFilterFooter(new Rect(new Vector2(area.Min.X, area.Max.Y - footerHeight), area.Max));
        if (changedMutes)
        {
            ApplyMutesEverywhere();
            return;
        }

        if (changedInclude)
        {
            ApplyFilters(filterSurface);
        }
    }

    private void DrawFilterFooter(Rect footer)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        SocialChrome.PaintBarBackdrop(ui, drawList, footer, screenRect);
        FeedCell.Hairline(drawList, footer.Min.X, footer.Max.X, footer.Min.Y + 1f, VelvetTheme.Hairline);
        var pad = SocialChrome.CellPadX * scale;
        var buttonHeight = 44f * scale;
        var rect = new Rect(new Vector2(footer.Min.X + pad, footer.Center.Y - buttonHeight * 0.5f),
            new Vector2(footer.Max.X - pad, footer.Center.Y + buttonHeight * 0.5f));
        if (SocialPill.Accent(drawList, rect, Loc.T(L.Velvet.FilterShowResults), VelvetInk.Shared,
                TextStyles.SubheadlineEmphasized, buttonHeight * 0.5f))
        {
            router.Pop();
        }
    }

    private void DrawFacetSection(VelvetFilterFacet facet, VelvetFilterSelection include, int slot,
        ref bool changedInclude, ref bool changedMutes)
    {
        var scale = UiScale.Current;
        var pad = SocialChrome.CellPadX * scale;
        var drawList = ImGui.GetWindowDrawList();
        var open = (expandedFacets & (1 << slot)) != 0;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var reveal = facetReveal[slot].Step(open ? 1f : 0f, FilterRevealSmoothTime, delta);
        var header = Reserve(FilterSectionHeaderHeight);
        var centerY = header.Center.Y;
        var hovered = UiInteract.Hover(header.Min, header.Max);
        if (hovered)
        {
            drawList.AddRectFilled(header.Min, header.Max, ImGui.GetColorU32(VelvetTheme.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.Draw(drawList, new Vector2(header.Min.X + pad,
                centerY - Typography.LineHeight(TextStyles.BodyEmphasized) * 0.5f), Loc.T(FacetTitle(facet)),
            VelvetTheme.TitleInk, TextStyles.BodyEmphasized);
        var summary = FacetSummary(facet, include);
        var summarySize = Typography.Measure(summary, TextStyles.Subheadline);
        var chevronCenter = new Vector2(header.Max.X - pad - VIcon.Row * scale * 0.5f, centerY);
        Typography.Draw(drawList,
            new Vector2(chevronCenter.X - VIcon.Row * scale * 0.5f - 8f * scale - summarySize.X,
                centerY - summarySize.Y * 0.5f), summary, VelvetTheme.MutedInk, TextStyles.Subheadline);
        DrawFacetChevron(drawList, chevronCenter, reveal, scale);
        if (UiInteract.Click(header.Min, header.Max, hovered))
        {
            expandedFacets ^= 1 << slot;
        }

        if (reveal > 0.001f)
        {
            var full = FacetContentHeight(facet) * scale;
            var visible = full * reveal;
            var origin = ImGui.GetCursorScreenPos();
            ImGui.PushClipRect(new Vector2(header.Min.X, origin.Y),
                new Vector2(header.Max.X, origin.Y + visible), true);
            DrawFacetOptions(facet, include, header, reveal >= 0.999f, ref changedInclude, ref changedMutes);
            ImGui.PopClipRect();
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(header.Width, visible));
        }

        FeedCell.Hairline(drawList, header.Min.X + pad, header.Max.X - pad, ImGui.GetCursorScreenPos().Y,
            VelvetTheme.Hairline);
    }

    private static void DrawFacetChevron(ImDrawListPtr drawList, Vector2 center, float reveal, float scale)
    {
        var size = VIcon.Row * scale;
        if (reveal < 0.999f)
        {
            PhoneIcon.Draw(drawList, center, PhoneIcons.ChevronRight,
                VelvetTheme.Alpha(VelvetTheme.MutedInk, 1f - reveal), size);
        }

        if (reveal > 0.001f)
        {
            PhoneIcon.Draw(drawList, center, PhoneIcons.ChevronDown,
                VelvetTheme.Alpha(VelvetTheme.RoseInk, reveal), size);
        }
    }

    private void DrawFacetOptions(VelvetFilterFacet facet, VelvetFilterSelection include, Rect header, bool live,
        ref bool changedInclude, ref bool changedMutes)
    {
        if (facet != VelvetFilterFacet.Tags)
        {
            FillFacetLabels(facet);
            for (var index = 0; index < facetLabels.Count; index++)
            {
                DrawOptionRow(facet, include, index, facetLabels[index], header, live, ref changedInclude,
                    ref changedMutes);
            }

            Gap(FilterRevealPadY);
            return;
        }

        var categories = VelvetSuggestions.TagCategories;
        var cursor = 0;
        for (var index = 0; index < categories.Length; index++)
        {
            DrawGroupHeader(Loc.T(categories[index].Title), categories[index].Hue, header);
            var tags = categories[index].Tags;
            for (var tagIndex = 0; tagIndex < tags.Length; tagIndex++)
            {
                DrawOptionRow(facet, include, cursor, tags[tagIndex], header, live, ref changedInclude,
                    ref changedMutes);
                cursor++;
            }
        }

        Gap(FilterRevealPadY);
    }

    private static void DrawGroupHeader(string label, Vector4 hue, Rect header)
    {
        var scale = UiScale.Current;
        var pad = SocialChrome.CellPadX * scale;
        var row = Reserve(FilterGroupHeaderHeight);
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(header.Min.X + pad, row.Center.Y - Typography.LineHeight(TextStyles.FootnoteEmphasized) * 0.5f),
            Loc.Upper(label), VelvetTheme.Lerp(hue, VelvetTheme.OnAccent, 0.3f), TextStyles.FootnoteEmphasized);
    }

    private void DrawOptionRow(VelvetFilterFacet facet, VelvetFilterSelection include, int optionIndex, string label,
        Rect header, bool live, ref bool changedInclude, ref bool changedMutes)
    {
        var scale = UiScale.Current;
        var pad = SocialChrome.CellPadX * scale;
        var drawList = ImGui.GetWindowDrawList();
        var row = Reserve(FilterOptionRowHeight);
        var centerY = row.Center.Y;
        var state = OptionState(facet, include, optionIndex);
        var radius = FilterMarkRadius * scale;
        var hideCenter = new Vector2(header.Max.X - pad - radius, centerY);
        var showCenter = new Vector2(hideCenter.X - radius * 2f - FilterMarkGap * scale, centerY);
        var labelInk = state switch
        {
            VelvetOptionState.Shown => VelvetTheme.TitleInk,
            VelvetOptionState.Hidden => VelvetTheme.Faint,
            _ => VelvetTheme.BodyInk,
        };
        Typography.Draw(drawList,
            new Vector2(header.Min.X + pad, centerY - Typography.LineHeight(TextStyles.Body) * 0.5f),
            Typography.FitText(label, MathF.Max(1f, showCenter.X - radius - 12f * scale - header.Min.X - pad),
                TextStyles.Body), labelInk, TextStyles.Body);

        var showHit = DrawOptionMark(drawList, showCenter, radius, PhoneIcons.Check, VelvetTheme.Rose,
            state == VelvetOptionState.Shown, live);
        var hideHit = DrawOptionMark(drawList, hideCenter, radius, PhoneIcons.X, VelvetTheme.Danger,
            state == VelvetOptionState.Hidden, live);
        FeedCell.Hairline(drawList, header.Min.X + pad, header.Max.X - pad, row.Max.Y,
            VelvetTheme.Alpha(VelvetTheme.Hairline, 0.55f));
        if (!showHit && !hideHit)
        {
            return;
        }

        var next = showHit
            ? state == VelvetOptionState.Shown ? VelvetOptionState.Any : VelvetOptionState.Shown
            : state == VelvetOptionState.Hidden ? VelvetOptionState.Any : VelvetOptionState.Hidden;
        var touchedHidden = state == VelvetOptionState.Hidden || next == VelvetOptionState.Hidden;
        SetOptionState(facet, include, optionIndex, next);
        if (touchedHidden)
        {
            changedMutes = true;
            return;
        }

        changedInclude = true;
    }

    private static bool DrawOptionMark(ImDrawListPtr drawList, Vector2 center, float radius, string glyph,
        Vector4 tone, bool active, bool live)
    {
        var scale = UiScale.Current;
        var extent = new Vector2(radius, radius);
        var hovered = live && UiInteract.Hover(center - extent, center + extent);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(active
            ? tone
            : hovered ? VelvetTheme.Alpha(tone, 0.28f) : VelvetInk.Shared.FieldFill), 28);
        PhoneIcon.Draw(drawList, center, glyph, active ? VelvetTheme.OnAccent : VelvetTheme.MutedInk,
            VIcon.Chip * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return live && UiInteract.Click(center - extent, center + extent, hovered);
    }
}
