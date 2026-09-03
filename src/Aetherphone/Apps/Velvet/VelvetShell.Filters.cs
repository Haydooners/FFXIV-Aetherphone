using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private readonly VelvetFilterSelection mutes = new();
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

        if ((include.Any || mutes.Any) && ui.HeaderAction(area, Loc.T(L.Velvet.FilterClearAll), true))
        {
            include.Clear();
            mutes.Clear();
            ApplyMutesEverywhere();
            RefreshFilterSummaries();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            Gap(8f);
            for (var index = 0; index < FilterFacets.Length; index++)
            {
                var facet = FilterFacets[index];
                var row = new VRowModel
                {
                    Title = Loc.T(FacetTitle(facet)),
                    Value = filterSummaries[index],
                    Height = 52f,
                    Leading = VRowLeading.None,
                    Chevron = true,
                };
                if (VRow.Cell(in row, ui, theme, images, lodestone) == VRowHit.Body)
                {
                    router.Push(VelvetView.FilterFacet(((int)facet).ToString(Loc.Culture)));
                }
            }

            Gap(40f);
        }
    }

    private void DrawFilterFacet(Rect area, string argument)
    {
        var scale = UiScale.Current;
        if (!int.TryParse(argument, out var raw) || raw < 0 || raw >= FilterFacets.Length)
        {
            router.Pop();
            return;
        }

        var facet = (VelvetFilterFacet)raw;
        var include = IncludeFor(filterSurface);
        if (VHeader.Push(area, Loc.T(FacetTitle(facet))))
        {
            router.Pop();
            return;
        }

        var changedInclude = false;
        var changedMutes = false;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            Gap(10f);
            if (facet != VelvetFilterFacet.Region)
            {
                ui.HelpText(Loc.T(L.Velvet.FilterFacetHint));
                Gap(14f);
            }

            switch (facet)
            {
                case VelvetFilterFacet.Region:
                    changedInclude |= DrawRegionFilterRow(include);
                    break;
                case VelvetFilterFacet.Race:
                    DrawRaceFilterChips(include, ref changedInclude, ref changedMutes);
                    break;
                case VelvetFilterFacet.Intent:
                    DrawIntentFilterChips(include, ref changedInclude, ref changedMutes);
                    break;
                case VelvetFilterFacet.Gender:
                    DrawGenderFilterChips(include, ref changedInclude, ref changedMutes);
                    break;
                case VelvetFilterFacet.Sexuality:
                    DrawSexualityFilterChips(include, ref changedInclude, ref changedMutes);
                    break;
                case VelvetFilterFacet.Relationship:
                    DrawRelationshipFilterChips(include, ref changedInclude, ref changedMutes);
                    break;
                case VelvetFilterFacet.Role:
                    DrawTriStateTokenChips(VelvetSuggestions.Roles, VelvetTheme.Rose, include.Roles, mutes.Roles,
                        ref changedInclude, ref changedMutes);
                    break;
                case VelvetFilterFacet.Kinks:
                    DrawTriStateTokenChips(VelvetSuggestions.Kinks, VelvetSuggestions.KinkHue, include.Kinks,
                        mutes.Kinks, ref changedInclude, ref changedMutes);
                    break;
                case VelvetFilterFacet.Limits:
                    DrawTriStateTokenChips(VelvetSuggestions.Limits, VelvetTheme.Gold, include.Limits, mutes.Limits,
                        ref changedInclude, ref changedMutes);
                    break;
                default:
                    DrawTagsFilterChips(include, ref changedInclude, ref changedMutes);
                    break;
            }

            Gap(40f);
        }

        if (changedMutes)
        {
            ApplyMutesEverywhere();
            RefreshFilterSummaries();
            return;
        }

        if (changedInclude)
        {
            ApplyFilters(filterSurface);
            RefreshFilterSummaries();
        }
    }

    private void DrawRaceFilterChips(VelvetFilterSelection include, ref bool changedInclude, ref bool changedMutes)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var races = VelvetRace.All;
        chipModels.Clear();
        for (var index = 0; index < races.Length; index++)
        {
            chipModels.Add(TriStateChip(VelvetRace.Label(gameData, races[index]), VelvetTheme.Moonlight, include.Race,
                mutes.Race, VelvetRace.Bit(races[index])));
        }

        var clicked = DrawChipFlow(width, scale);
        if (clicked < 0)
        {
            return;
        }

        CycleMaskState(ref include.Race, ref mutes.Race, VelvetRace.Bit(races[clicked]), ref changedInclude,
            ref changedMutes);
    }


    private bool DrawRegionFilterRow(VelvetFilterSelection include)
    {
        var scale = UiScale.Current;
        var codes = SocialRegion.Codes;
        var labels = regionLabels;
        labels[0] = Loc.T(L.Velvet.RegionAny);
        var current = 0;
        for (var index = 0; index < codes.Length; index++)
        {
            labels[index + 1] = codes[index];
            if (string.Equals(include.Region, codes[index], StringComparison.Ordinal))
            {
                current = index + 1;
            }
        }

        var picked = VSegmented.Draw("velvetFilterRegion", Reserve(34f), labels, current, scale);
        if (picked < 0 || picked == current)
        {
            return false;
        }

        include.Region = picked == 0 ? string.Empty : codes[picked - 1];
        return true;
    }

    private void DrawIntentFilterChips(VelvetFilterSelection include, ref bool changedInclude, ref bool changedMutes)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var defs = VelvetIntent.All;
        chipModels.Clear();
        for (var index = 0; index < defs.Length; index++)
        {
            var def = defs[index];
            chipModels.Add(TriStateChip(Loc.T(def.Label), def.Hue, include.Intent, mutes.Intent, def.Flag));
        }

        var clicked = DrawChipFlow(width, scale);
        if (clicked < 0)
        {
            return;
        }

        CycleMaskState(ref include.Intent, ref mutes.Intent, defs[clicked].Flag, ref changedInclude, ref changedMutes);
    }

    private void DrawGenderFilterChips(VelvetFilterSelection include, ref bool changedInclude, ref bool changedMutes)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var options = VelvetGender.All;
        chipModels.Clear();
        for (var index = 0; index < options.Length; index++)
        {
            chipModels.Add(TriStateChip(VelvetGender.Label(options[index]), VelvetTheme.Rose, include.Gender,
                mutes.Gender, options[index]));
        }

        var clicked = DrawChipFlow(width, scale);
        if (clicked < 0)
        {
            return;
        }

        CycleMaskState(ref include.Gender, ref mutes.Gender, options[clicked], ref changedInclude, ref changedMutes);
    }

    private void DrawSexualityFilterChips(VelvetFilterSelection include, ref bool changedInclude,
        ref bool changedMutes)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var options = VelvetSexuality.All;
        chipModels.Clear();
        for (var index = 0; index < options.Length; index++)
        {
            chipModels.Add(TriStateChip(VelvetSexuality.Label(options[index]), VelvetTheme.Rose, include.Sexuality,
                mutes.Sexuality, options[index]));
        }

        var clicked = DrawChipFlow(width, scale);
        if (clicked < 0)
        {
            return;
        }

        CycleMaskState(ref include.Sexuality, ref mutes.Sexuality, options[clicked], ref changedInclude,
            ref changedMutes);
    }

    private void DrawRelationshipFilterChips(VelvetFilterSelection include, ref bool changedInclude,
        ref bool changedMutes)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var statuses = VelvetRelationship.All;
        chipModels.Clear();
        for (var index = 0; index < statuses.Length; index++)
        {
            chipModels.Add(TriStateChip(VelvetRelationship.Label(statuses[index]), VelvetTheme.Rose,
                include.Relationship, mutes.Relationship, 1 << statuses[index]));
        }

        var clicked = DrawChipFlow(width, scale);
        if (clicked < 0)
        {
            return;
        }

        CycleMaskState(ref include.Relationship, ref mutes.Relationship, 1 << statuses[clicked], ref changedInclude,
            ref changedMutes);
    }

    private void DrawTagsFilterChips(VelvetFilterSelection include, ref bool changedInclude, ref bool changedMutes)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var categories = VelvetSuggestions.TagCategories;
        for (var index = 0; index < categories.Length; index++)
        {
            var category = categories[index];
            var headerOrigin = ImGui.GetCursorScreenPos();
            Typography.Draw(headerOrigin, Loc.Upper(Loc.T(category.Title)),
                VelvetTheme.Lerp(category.Hue, VelvetTheme.OnAccent, 0.30f), TextStyles.SubheadlineEmphasized);
            ImGui.SetCursorScreenPos(headerOrigin);
            ImGui.Dummy(new Vector2(width, 24f * scale));
            DrawTriStateTokenChips(category.Tags, category.Hue, include.Tags, mutes.Tags, ref changedInclude,
                ref changedMutes);
            if (index < categories.Length - 1)
            {
                Gap(12f);
            }
        }
    }

    private void DrawTriStateTokenChips(string[] options, Vector4 accent, HashSet<string> include,
        HashSet<string> exclude, ref bool changedInclude, ref bool changedMutes)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        chipModels.Clear();
        for (var index = 0; index < options.Length; index++)
        {
            var token = options[index];
            if (include.Contains(token))
            {
                chipModels.Add(new VChipModel(token, VChipStyle.Solid, accent, PhoneIcons.Check));
            }
            else if (exclude.Contains(token))
            {
                chipModels.Add(new VChipModel(token, VChipStyle.Solid, VelvetTheme.Danger, PhoneIcons.Ban));
            }
            else
            {
                chipModels.Add(new VChipModel(token, VChipStyle.Ghost, VelvetTheme.Moonlight));
            }
        }

        var clicked = DrawChipFlow(width, scale);
        if (clicked < 0)
        {
            return;
        }

        CycleTokenState(include, exclude, options[clicked], ref changedInclude, ref changedMutes);
    }

    private static VChipModel TriStateChip(string label, Vector4 accent, int include, int exclude, int flag)
    {
        if ((include & flag) != 0)
        {
            return new VChipModel(label, VChipStyle.Solid, accent, PhoneIcons.Check);
        }

        if ((exclude & flag) != 0)
        {
            return new VChipModel(label, VChipStyle.Solid, VelvetTheme.Danger, PhoneIcons.Ban);
        }

        return new VChipModel(label, VChipStyle.Ghost, VelvetTheme.Moonlight);
    }

    private static void CycleMaskState(ref int include, ref int exclude, int flag, ref bool changedInclude,
        ref bool changedMutes)
    {
        if ((include & flag) != 0)
        {
            include &= ~flag;
            exclude |= flag;
            changedInclude = true;
            changedMutes = true;
            return;
        }

        if ((exclude & flag) != 0)
        {
            exclude &= ~flag;
            changedMutes = true;
            return;
        }

        include |= flag;
        changedInclude = true;
    }

    private static void CycleTokenState(HashSet<string> include, HashSet<string> exclude, string token,
        ref bool changedInclude, ref bool changedMutes)
    {
        if (include.Remove(token))
        {
            exclude.Add(token);
            changedInclude = true;
            changedMutes = true;
            return;
        }

        if (exclude.Remove(token))
        {
            changedMutes = true;
            return;
        }

        include.Add(token);
        changedInclude = true;
    }
}
