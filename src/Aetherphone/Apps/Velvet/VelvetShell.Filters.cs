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

        if (include.Any && ui.HeaderAction(area, Loc.T(L.Velvet.FilterClearAll), true))
        {
            include.Clear();
            ApplyFilters(filterSurface);
            RefreshFilterSummaries();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(body))
        {
            Gap(10f);
            DrawInsetHelpText(Loc.T(L.Velvet.FilterPickHint));
            Gap(10f);
            for (var index = 0; index < FilterFacets.Length; index++)
            {
                if (DrawFacetRow(FilterFacets[index], filterSummaries[index]))
                {
                    router.Push(VelvetView.FilterFacet(((int)FilterFacets[index]).ToString(Loc.Culture)));
                }
            }

            Gap(18f);
            VSectionHeader.Overline(Loc.T(L.Velvet.SafetyHeader), string.Empty, SocialChrome.CellPadX * scale);
            if (DrawFacetRow(L.Velvet.HiddenTitle, HiddenSummary(), PhoneIcons.EyeOff))
            {
                RefreshHiddenSummaries();
                router.Push(VelvetView.Hidden);
            }

            Gap(40f);
        }
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

    private void DrawFilterFacet(Rect area, string argument) => DrawFacetPicker(area, argument, false);

    private void DrawHiddenFacet(Rect area, string argument) => DrawFacetPicker(area, argument, true);

    private void DrawFacetPicker(Rect area, string argument, bool hiding)
    {
        var scale = UiScale.Current;
        if (!int.TryParse(argument, out var raw) || !Enum.IsDefined(typeof(VelvetFilterFacet), raw))
        {
            router.Pop();
            return;
        }

        var facet = (VelvetFilterFacet)raw;
        var target = hiding ? mutes : IncludeFor(filterSurface);
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
            ui.HelpText(Loc.T(hiding ? L.Velvet.HiddenHint : L.Velvet.FilterPickHint));
            Gap(14f);
            var tone = hiding ? VelvetTheme.Danger : VelvetTheme.Rose;
            switch (facet)
            {
                case VelvetFilterFacet.Region:
                    changed |= DrawRegionFilterRow(target);
                    break;
                case VelvetFilterFacet.Race:
                    changed |= DrawRaceChips(target, hiding);
                    break;
                case VelvetFilterFacet.Intent:
                    changed |= DrawIntentChips(target, hiding);
                    break;
                case VelvetFilterFacet.Gender:
                    changed |= DrawMaskChips(VelvetGender.All, GenderLabelOf, ref target.Gender, tone, hiding);
                    break;
                case VelvetFilterFacet.Sexuality:
                    changed |= DrawMaskChips(VelvetSexuality.All, SexualityLabelOf, ref target.Sexuality, tone,
                        hiding);
                    break;
                case VelvetFilterFacet.Relationship:
                    changed |= DrawRelationshipChips(target, hiding);
                    break;
                case VelvetFilterFacet.Role:
                    changed |= DrawTokenChips(VelvetSuggestions.Roles, target.Roles, tone, hiding);
                    break;
                case VelvetFilterFacet.Kinks:
                    changed |= DrawTokenChips(VelvetSuggestions.Kinks, target.Kinks,
                        hiding ? tone : VelvetSuggestions.KinkHue, hiding);
                    break;
                case VelvetFilterFacet.Limits:
                    changed |= DrawTokenChips(VelvetSuggestions.Limits, target.Limits,
                        hiding ? tone : VelvetTheme.Gold, hiding);
                    break;
                default:
                    changed |= DrawTagChips(target, hiding);
                    break;
            }

            Gap(40f);
        }

        if (!changed)
        {
            return;
        }

        if (hiding)
        {
            ApplyMutesEverywhere();
            RefreshHiddenSummaries();
            return;
        }

        ApplyFilters(filterSurface);
        RefreshFilterSummaries();
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

    private bool DrawRaceChips(VelvetFilterSelection target, bool hiding)
    {
        var races = VelvetRace.All;
        chipModels.Clear();
        for (var index = 0; index < races.Length; index++)
        {
            chipModels.Add(PickChip(VelvetRace.Label(gameData, races[index]), VelvetTheme.Moonlight,
                VelvetRace.Has(target.Race, races[index]), hiding));
        }

        var clicked = DrawChipFlow(ImGui.GetContentRegionAvail().X, UiScale.Current);
        if (clicked < 0)
        {
            return false;
        }

        target.Race = VelvetRace.Toggle(target.Race, races[clicked]);
        return true;
    }

    private bool DrawIntentChips(VelvetFilterSelection target, bool hiding)
    {
        var defs = VelvetIntent.All;
        chipModels.Clear();
        for (var index = 0; index < defs.Length; index++)
        {
            chipModels.Add(PickChip(Loc.T(defs[index].Label), defs[index].Hue,
                (target.Intent & defs[index].Flag) != 0, hiding));
        }

        var clicked = DrawChipFlow(ImGui.GetContentRegionAvail().X, UiScale.Current);
        if (clicked < 0)
        {
            return false;
        }

        ToggleMask(ref target.Intent, defs[clicked].Flag);
        return true;
    }

    private bool DrawMaskChips(int[] options, Func<int, string> labelOf, ref int mask, Vector4 tone, bool hiding)
    {
        chipModels.Clear();
        for (var index = 0; index < options.Length; index++)
        {
            chipModels.Add(PickChip(labelOf(options[index]), tone, (mask & options[index]) != 0, hiding));
        }

        var clicked = DrawChipFlow(ImGui.GetContentRegionAvail().X, UiScale.Current);
        if (clicked < 0)
        {
            return false;
        }

        ToggleMask(ref mask, options[clicked]);
        return true;
    }

    private bool DrawRelationshipChips(VelvetFilterSelection target, bool hiding)
    {
        var statuses = VelvetRelationship.All;
        chipModels.Clear();
        for (var index = 0; index < statuses.Length; index++)
        {
            chipModels.Add(PickChip(VelvetRelationship.Label(statuses[index]), VelvetTheme.Rose,
                (target.Relationship & (1 << statuses[index])) != 0, hiding));
        }

        var clicked = DrawChipFlow(ImGui.GetContentRegionAvail().X, UiScale.Current);
        if (clicked < 0)
        {
            return false;
        }

        ToggleMask(ref target.Relationship, 1 << statuses[clicked]);
        return true;
    }

    private bool DrawTagChips(VelvetFilterSelection target, bool hiding)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
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
            changed |= DrawTokenChips(category.Tags, target.Tags, hiding ? VelvetTheme.Danger : category.Hue, hiding);
            if (index < categories.Length - 1)
            {
                Gap(12f);
            }
        }

        return changed;
    }

    private bool DrawTokenChips(string[] options, HashSet<string> target, Vector4 tone, bool hiding)
    {
        chipModels.Clear();
        for (var index = 0; index < options.Length; index++)
        {
            chipModels.Add(PickChip(options[index], tone, target.Contains(options[index]), hiding));
        }

        var clicked = DrawChipFlow(ImGui.GetContentRegionAvail().X, UiScale.Current);
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
