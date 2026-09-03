using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float FilterFooterHeight = 64f;
    private const float FilterToggleRowHeight = 52f;
    private const float FilterSectionHeaderHeight = 52f;
    private const int ActiveWithinDefaultDays = 7;

    private readonly VelvetFilterSelection mutes = new();
    private VelvetFilterFacet expandedFacet = VelvetFilterFacet.None;
    private readonly string[] regionLabels = new string[SocialRegion.Codes.Length + 1];
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
            discoverInclude.Region);

    private void ApplyFeedFilters() =>
        store.SetFeedFilter(VelvetFilterSelection.Combine(feedInclude, mutes), feedInclude.Region);

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
        RefreshFilterSummaries();
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

        if (include.Any && ui.HeaderAction(area, Loc.T(L.Velvet.FilterClearAll), true))
        {
            include.Clear();
            ApplyFilters(filterSurface);
            RefreshFilterSummaries();
        }

        var footerHeight = FilterFooterHeight * scale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale),
            new Vector2(area.Max.X, area.Max.Y - footerHeight));
        var changed = false;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            Gap(6f);
            changed |= DrawAppliedFilterRail(include);
            changed |= DrawQuickFilters(include);
            for (var index = 0; index < FilterFacets.Length; index++)
            {
                changed |= DrawFacetSection(FilterFacets[index], include);
            }

            Gap(18f);
            VSectionHeader.Overline(Loc.T(L.Velvet.SafetyHeader), string.Empty, SocialChrome.CellPadX * scale);
            if (DrawFacetRow(L.Velvet.HiddenTitle, HiddenSummary(), PhoneIcons.EyeOff))
            {
                RefreshHiddenSummaries();
                router.Push(VelvetView.Hidden);
            }

            Gap(30f);
        }

        DrawFilterFooter(new Rect(new Vector2(area.Min.X, area.Max.Y - footerHeight), area.Max));
        if (changed)
        {
            ApplyFilters(filterSurface);
            RefreshFilterSummaries();
        }
    }

    private bool DrawAppliedFilterRail(VelvetFilterSelection include)
    {
        var scale = UiScale.Current;
        var inset = SocialChrome.CellPadX * scale;
        if (!include.Any)
        {
            return false;
        }

        var width = ScrollLayout.StableContentWidth();
        DrawActiveFilters(width - inset * 2f, filterSurface, inset);
        return false;
    }

    private bool DrawQuickFilters(VelvetFilterSelection include)
    {
        var changed = false;
        var hasPhoto = include.HasPhoto;
        if (DrawToggleRow(Loc.T(L.Velvet.FilterHasPhoto), PhoneIcons.Photo, ref hasPhoto))
        {
            include.HasPhoto = hasPhoto;
            changed = true;
        }

        var active = include.ActiveWithinDays > 0;
        if (DrawToggleRow(Loc.T(L.Velvet.FilterActiveRecently), PhoneIcons.Clock, ref active))
        {
            include.ActiveWithinDays = active ? ActiveWithinDefaultDays : 0;
            changed = true;
        }

        Gap(4f);
        return changed;
    }

    private bool DrawToggleRow(string label, string glyph, ref bool value)
    {
        var scale = UiScale.Current;
        var pad = SocialChrome.CellPadX * scale;
        var drawList = ImGui.GetWindowDrawList();
        var row = Reserve(FilterToggleRowHeight);
        var centerY = row.Center.Y;
        PhoneIcon.Draw(drawList, new Vector2(row.Min.X + pad + VIcon.Row * scale * 0.5f, centerY), glyph,
            value ? VelvetTheme.RoseInk : VelvetTheme.MutedInk, VIcon.Row * scale);
        var labelLeft = row.Min.X + pad + VIcon.Row * scale + 12f * scale;
        var trackWidth = 44f * scale;
        var trackHeight = 26f * scale;
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - Typography.LineHeight(TextStyles.Body) * 0.5f),
            Typography.FitText(label, MathF.Max(1f, row.Max.X - pad - trackWidth - 12f * scale - labelLeft),
                TextStyles.Body), VelvetTheme.TitleInk, TextStyles.Body);
        var trackMin = new Vector2(row.Max.X - pad - trackWidth, centerY - trackHeight * 0.5f);
        var toggled = Toggle.Draw("velvetFilterToggle." + label, new Rect(trackMin,
            new Vector2(trackMin.X + trackWidth, trackMin.Y + trackHeight)), value, theme);
        FeedCell.Hairline(drawList, row.Min.X + pad, row.Max.X - pad, row.Max.Y, VelvetTheme.Hairline);
        if (toggled == value)
        {
            return false;
        }

        value = toggled;
        return true;
    }

    private bool DrawFacetSection(VelvetFilterFacet facet, VelvetFilterSelection include)
    {
        var scale = UiScale.Current;
        var pad = SocialChrome.CellPadX * scale;
        var drawList = ImGui.GetWindowDrawList();
        var open = expandedFacet == facet;
        var header = Reserve(FilterSectionHeaderHeight);
        var centerY = header.Center.Y;
        var hovered = UiInteract.Hover(header.Min, header.Max);
        if (hovered)
        {
            drawList.AddRectFilled(header.Min, header.Max, ImGui.GetColorU32(VelvetTheme.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var title = Loc.T(FacetTitle(facet));
        Typography.Draw(drawList, new Vector2(header.Min.X + pad, centerY - Typography.LineHeight(
            TextStyles.BodyEmphasized) * 0.5f), title, VelvetTheme.TitleInk, TextStyles.BodyEmphasized);
        var summary = SummaryFor(facet, include, false);
        var summarySize = Typography.Measure(summary, TextStyles.Subheadline);
        var chevronCenter = new Vector2(header.Max.X - pad - VIcon.Row * scale * 0.5f, centerY);
        Typography.Draw(drawList,
            new Vector2(chevronCenter.X - VIcon.Row * scale * 0.5f - 8f * scale - summarySize.X,
                centerY - summarySize.Y * 0.5f), summary, VelvetTheme.MutedInk, TextStyles.Subheadline);
        PhoneIcon.Draw(drawList, chevronCenter, open ? PhoneIcons.ChevronDown : PhoneIcons.ChevronRight,
            VelvetTheme.MutedInk, VIcon.Row * scale);
        if (UiInteract.Click(header.Min, header.Max, hovered))
        {
            expandedFacet = open ? VelvetFilterFacet.None : facet;
        }

        if (!open)
        {
            FeedCell.Hairline(drawList, header.Min.X + pad, header.Max.X - pad, header.Max.Y, VelvetTheme.Hairline);
            return false;
        }

        ImGui.Indent(pad);
        Gap(2f);
        var changed = DrawFacetChips(facet, include, false, MathF.Max(1f, header.Width - pad * 2f));
        Gap(12f);
        ImGui.Unindent(pad);
        FeedCell.Hairline(drawList, header.Min.X + pad, header.Max.X - pad, ImGui.GetCursorScreenPos().Y,
            VelvetTheme.Hairline);
        return changed;
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

    private bool DrawFacetChips(VelvetFilterFacet facet, VelvetFilterSelection target, bool hiding, float width)
    {
        var tone = hiding ? VelvetTheme.Danger : VelvetTheme.Rose;
        return facet switch
        {
            VelvetFilterFacet.Region => DrawRegionFilterRow(target),
            VelvetFilterFacet.Race => DrawRaceChips(target, hiding, width),
            VelvetFilterFacet.Intent => DrawIntentChips(target, hiding, width),
            VelvetFilterFacet.Gender => DrawMaskChips(VelvetGender.All, GenderLabelOf, ref target.Gender, tone,
                hiding, width),
            VelvetFilterFacet.Sexuality => DrawMaskChips(VelvetSexuality.All, SexualityLabelOf, ref target.Sexuality,
                tone, hiding, width),
            VelvetFilterFacet.Relationship => DrawRelationshipChips(target, hiding, width),
            VelvetFilterFacet.Role => DrawTokenChips(VelvetSuggestions.Roles, target.Roles, tone, hiding, width),
            VelvetFilterFacet.Kinks => DrawTokenChips(VelvetSuggestions.Kinks, target.Kinks,
                hiding ? tone : VelvetSuggestions.KinkHue, hiding, width),
            VelvetFilterFacet.Limits => DrawTokenChips(VelvetSuggestions.Limits, target.Limits,
                hiding ? tone : VelvetTheme.Gold, hiding, width),
            _ => DrawTagChips(target, hiding, width),
        };
    }

    private void DrawHidden(Rect area)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Velvet.HiddenTitle), 2))
        {
            router.Pop();
            return;
        }

        if (mutes.Any && ui.HeaderAction(area, Loc.T(L.Velvet.FilterClearAll), true))
        {
            mutes.Clear();
            ApplyMutesEverywhere();
            RefreshHiddenSummaries();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            Gap(10f);
            DrawInsetHelpText(Loc.T(L.Velvet.HiddenHint));
            Gap(10f);
            for (var index = 0; index < HiddenFacets.Length; index++)
            {
                if (DrawFacetRow(HiddenFacets[index], hiddenSummaries[index]))
                {
                    router.Push(VelvetView.HiddenFacet(((int)HiddenFacets[index]).ToString(Loc.Culture)));
                }
            }

            Gap(40f);
        }
    }

    private bool DrawFacetRow(VelvetFilterFacet facet, string summary) =>
        DrawFacetRow(FacetTitle(facet), summary, null);

    private bool DrawFacetRow(LocString title, string summary, string? glyph)
    {
        var row = new VRowModel
        {
            Title = Loc.T(title),
            Value = summary,
            Height = 52f,
            Leading = glyph is null ? VRowLeading.None : VRowLeading.IconTile,
            TileIcon = glyph ?? string.Empty,
            TileTint = VelvetTheme.Gold,
            Chevron = true,
        };
        return VRow.Cell(in row, ui, theme, images, lodestone) == VRowHit.Body;
    }

    private void DrawHiddenFacet(Rect area, string argument)
    {
        var scale = UiScale.Current;
        if (!int.TryParse(argument, out var raw) || !Enum.IsDefined(typeof(VelvetFilterFacet), raw))
        {
            router.Pop();
            return;
        }

        var facet = (VelvetFilterFacet)raw;
        if (VHeader.Push(area, Loc.T(FacetTitle(facet))))
        {
            router.Pop();
            return;
        }

        var changed = false;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            Gap(10f);
            ui.HelpText(Loc.T(L.Velvet.HiddenHint));
            Gap(14f);
            changed = DrawFacetChips(facet, mutes, true, ImGui.GetContentRegionAvail().X);
            Gap(40f);
        }

        if (changed)
        {
            ApplyMutesEverywhere();
            RefreshHiddenSummaries();
        }
    }

    private bool DrawRegionFilterRow(VelvetFilterSelection target)
    {
        var scale = UiScale.Current;
        var codes = SocialRegion.Codes;
        var labels = regionLabels;
        labels[0] = Loc.T(L.Velvet.RegionAny);
        var current = 0;
        for (var index = 0; index < codes.Length; index++)
        {
            labels[index + 1] = codes[index];
            if (string.Equals(target.Region, codes[index], StringComparison.Ordinal))
            {
                current = index + 1;
            }
        }

        var picked = VSegmented.Draw("velvetFilterRegion", Reserve(34f), labels, current, scale);
        if (picked < 0 || picked == current)
        {
            return false;
        }

        target.Region = picked == 0 ? string.Empty : codes[picked - 1];
        return true;
    }

    private bool DrawRaceChips(VelvetFilterSelection target, bool hiding, float width)
    {
        var races = VelvetRace.All;
        chipModels.Clear();
        for (var index = 0; index < races.Length; index++)
        {
            chipModels.Add(PickChip(VelvetRace.Label(gameData, races[index]), VelvetTheme.Moonlight,
                VelvetRace.Has(target.Race, races[index]), hiding));
        }

        var clicked = DrawChipFlow(width, UiScale.Current);
        if (clicked < 0)
        {
            return false;
        }

        target.Race = VelvetRace.Toggle(target.Race, races[clicked]);
        return true;
    }

    private bool DrawIntentChips(VelvetFilterSelection target, bool hiding, float width)
    {
        var defs = VelvetIntent.All;
        chipModels.Clear();
        for (var index = 0; index < defs.Length; index++)
        {
            chipModels.Add(PickChip(Loc.T(defs[index].Label), defs[index].Hue,
                (target.Intent & defs[index].Flag) != 0, hiding));
        }

        var clicked = DrawChipFlow(width, UiScale.Current);
        if (clicked < 0)
        {
            return false;
        }

        ToggleMask(ref target.Intent, defs[clicked].Flag);
        return true;
    }

    private bool DrawMaskChips(int[] options, Func<int, string> labelOf, ref int mask, Vector4 tone,
        bool hiding, float width)
    {
        chipModels.Clear();
        for (var index = 0; index < options.Length; index++)
        {
            chipModels.Add(PickChip(labelOf(options[index]), tone, (mask & options[index]) != 0, hiding));
        }

        var clicked = DrawChipFlow(width, UiScale.Current);
        if (clicked < 0)
        {
            return false;
        }

        ToggleMask(ref mask, options[clicked]);
        return true;
    }

    private bool DrawRelationshipChips(VelvetFilterSelection target, bool hiding, float width)
    {
        var statuses = VelvetRelationship.All;
        chipModels.Clear();
        for (var index = 0; index < statuses.Length; index++)
        {
            chipModels.Add(PickChip(VelvetRelationship.Label(statuses[index]), VelvetTheme.Rose,
                (target.Relationship & (1 << statuses[index])) != 0, hiding));
        }

        var clicked = DrawChipFlow(width, UiScale.Current);
        if (clicked < 0)
        {
            return false;
        }

        ToggleMask(ref target.Relationship, 1 << statuses[clicked]);
        return true;
    }

    private bool DrawTagChips(VelvetFilterSelection target, bool hiding, float width)
    {
        var scale = UiScale.Current;
        var categories = VelvetSuggestions.TagCategories;
        var changed = false;
        for (var index = 0; index < categories.Length; index++)
        {
            var category = categories[index];
            var headerOrigin = ImGui.GetCursorScreenPos();
            Typography.Draw(headerOrigin, Loc.Upper(Loc.T(category.Title)),
                VelvetTheme.Lerp(category.Hue, VelvetTheme.OnAccent, 0.30f), TextStyles.SubheadlineEmphasized);
            ImGui.SetCursorScreenPos(headerOrigin);
            ImGui.Dummy(new Vector2(width, 24f * scale));
            changed |= DrawTokenChips(category.Tags, target.Tags, hiding ? VelvetTheme.Danger : category.Hue,
                hiding, width);
            if (index < categories.Length - 1)
            {
                Gap(12f);
            }
        }

        return changed;
    }

    private bool DrawTokenChips(string[] options, HashSet<string> target, Vector4 tone, bool hiding,
        float width)
    {
        chipModels.Clear();
        for (var index = 0; index < options.Length; index++)
        {
            chipModels.Add(PickChip(options[index], tone, target.Contains(options[index]), hiding));
        }

        var clicked = DrawChipFlow(width, UiScale.Current);
        if (clicked < 0)
        {
            return false;
        }

        var token = options[clicked];
        if (!target.Remove(token))
        {
            target.Add(token);
        }

        return true;
    }

    private static VChipModel PickChip(string label, Vector4 tone, bool picked, bool hiding)
    {
        if (!picked)
        {
            return new VChipModel(label, VChipStyle.Ghost, VelvetTheme.Moonlight);
        }

        return hiding
            ? new VChipModel(label, VChipStyle.Solid, VelvetTheme.Danger, PhoneIcons.EyeOff)
            : new VChipModel(label, VChipStyle.Solid, tone, PhoneIcons.Check);
    }

    private static void ToggleMask(ref int mask, int flag) => mask = (mask & flag) != 0 ? mask & ~flag : mask | flag;
}
