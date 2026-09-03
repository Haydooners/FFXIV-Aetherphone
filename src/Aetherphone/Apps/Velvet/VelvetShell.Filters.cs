using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
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
    private const float FacetRevealSmoothTime = 0.14f;
    private const float FacetRevealPadY = 14f;
    private const float TagCategoryHeaderHeight = 24f;

    private readonly VelvetFilterSelection mutes = new();
    private VelvetFilterFacet expandedFacet = VelvetFilterFacet.None;
    private readonly Spring[] facetReveal = new Spring[FilterFacets.Length];
    private VelvetPage filterSurface = VelvetPage.Discover;

    private VelvetFilterSelection IncludeFor(VelvetPage surface) =>
        surface == VelvetPage.Feed ? feedInclude : discoverInclude;

    private void LoadMutes()
    {
        mutes.LoadFrom(configuration.VelvetMutes);
        if (mutes.Intent == 0 && mutes.Gender == 0 && mutes.Sexuality == 0 && mutes.Relationship == 0
            && mutes.Race == 0 && mutes.Roles.Count == 0)
        {
            return;
        }

        mutes.Intent = 0;
        mutes.Gender = 0;
        mutes.Sexuality = 0;
        mutes.Relationship = 0;
        mutes.Race = 0;
        mutes.Roles.Clear();
        SaveMutes();
    }

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
            for (var index = 0; index < FilterFacets.Length; index++)
            {
                changed |= DrawFacetSection(FilterFacets[index], include);
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

    private bool DrawFacetSection(VelvetFilterFacet facet, VelvetFilterSelection include)
    {
        var scale = UiScale.Current;
        var pad = SocialChrome.CellPadX * scale;
        var drawList = ImGui.GetWindowDrawList();
        var slot = (int)facet;
        var open = expandedFacet == facet;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var reveal = facetReveal[slot].Step(open ? 1f : 0f, FacetRevealSmoothTime, delta);
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
        var summary = SummaryFor(facet, include, false);
        var summarySize = Typography.Measure(summary, TextStyles.Subheadline);
        var chevronCenter = new Vector2(header.Max.X - pad - VIcon.Row * scale * 0.5f, centerY);
        Typography.Draw(drawList,
            new Vector2(chevronCenter.X - VIcon.Row * scale * 0.5f - 8f * scale - summarySize.X,
                centerY - summarySize.Y * 0.5f), summary, VelvetTheme.MutedInk, TextStyles.Subheadline);
        DrawFacetChevron(drawList, chevronCenter, reveal, scale);
        if (UiInteract.Click(header.Min, header.Max, hovered))
        {
            expandedFacet = open ? VelvetFilterFacet.None : facet;
        }

        var changed = false;
        if (reveal > 0.001f)
        {
            var innerWidth = MathF.Max(1f, header.Width - pad * 2f);
            var full = FacetContentHeight(facet, include, false, innerWidth);
            var visible = full * reveal;
            var origin = ImGui.GetCursorScreenPos();
            ImGui.PushClipRect(new Vector2(header.Min.X, origin.Y),
                new Vector2(header.Max.X, origin.Y + visible), true);
            ImGui.Indent(pad);
            changed = DrawFacetChips(facet, include, false, innerWidth);
            ImGui.Unindent(pad);
            ImGui.PopClipRect();
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(header.Width, visible));
        }

        FeedCell.Hairline(drawList, header.Min.X + pad, header.Max.X - pad, ImGui.GetCursorScreenPos().Y,
            VelvetTheme.Hairline);
        return changed;
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

    private void FillFacetChips(VelvetFilterFacet facet, VelvetFilterSelection target, bool hiding)
    {
        var tone = hiding ? VelvetTheme.Danger : VelvetTheme.Rose;
        chipModels.Clear();
        switch (facet)
        {
            case VelvetFilterFacet.Race:
                var races = VelvetRace.All;
                for (var index = 0; index < races.Length; index++)
                {
                    chipModels.Add(PickChip(VelvetRace.Label(gameData, races[index]), VelvetTheme.Moonlight,
                        VelvetRace.Has(target.Race, races[index]), hiding));
                }

                break;
            case VelvetFilterFacet.Intent:
                var defs = VelvetIntent.All;
                for (var index = 0; index < defs.Length; index++)
                {
                    chipModels.Add(PickChip(Loc.T(defs[index].Label), defs[index].Hue,
                        (target.Intent & defs[index].Flag) != 0, hiding));
                }

                break;
            case VelvetFilterFacet.Gender:
                FillMaskChips(VelvetGender.All, GenderLabelOf, target.Gender, tone, hiding);
                break;
            case VelvetFilterFacet.Sexuality:
                FillMaskChips(VelvetSexuality.All, SexualityLabelOf, target.Sexuality, tone, hiding);
                break;
            case VelvetFilterFacet.Relationship:
                var statuses = VelvetRelationship.All;
                for (var index = 0; index < statuses.Length; index++)
                {
                    chipModels.Add(PickChip(VelvetRelationship.Label(statuses[index]), tone,
                        (target.Relationship & (1 << statuses[index])) != 0, hiding));
                }

                break;
            case VelvetFilterFacet.Role:
                FillTokenChips(VelvetSuggestions.Roles, target.Roles, tone, hiding);
                break;
            case VelvetFilterFacet.Kinks:
                FillTokenChips(VelvetSuggestions.Kinks, target.Kinks, hiding ? tone : VelvetSuggestions.KinkHue,
                    hiding);
                break;
            case VelvetFilterFacet.Limits:
                FillTokenChips(VelvetSuggestions.Limits, target.Limits, hiding ? tone : VelvetTheme.Gold, hiding);
                break;
        }
    }

    private void FillRegionChips(VelvetFilterSelection target)
    {
        var codes = SocialRegion.Codes;
        chipModels.Clear();
        for (var index = 0; index < codes.Length; index++)
        {
            chipModels.Add(PickChip(codes[index], VelvetTheme.RegionAccent,
                (target.RegionMask & (1 << index)) != 0, false));
        }
    }

    private void FillMaskChips(int[] options, Func<int, string> labelOf, int mask, Vector4 tone, bool hiding)
    {
        for (var index = 0; index < options.Length; index++)
        {
            chipModels.Add(PickChip(labelOf(options[index]), tone, (mask & options[index]) != 0, hiding));
        }
    }

    private void FillTokenChips(string[] options, HashSet<string> target, Vector4 tone, bool hiding)
    {
        for (var index = 0; index < options.Length; index++)
        {
            chipModels.Add(PickChip(options[index], tone, target.Contains(options[index]), hiding));
        }
    }

    private float FacetContentHeight(VelvetFilterFacet facet, VelvetFilterSelection target, bool hiding, float width)
    {
        var scale = UiScale.Current;
        if (facet == VelvetFilterFacet.Region)
        {
            FillRegionChips(target);
            return VChipFlow.Measure(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(chipModels), width,
                scale) + FacetRevealPadY * scale;
        }

        if (facet == VelvetFilterFacet.Tags)
        {
            var categories = VelvetSuggestions.TagCategories;
            var total = 0f;
            for (var index = 0; index < categories.Length; index++)
            {
                FillTokenChips(categories[index].Tags, target.Tags, hiding ? VelvetTheme.Danger : categories[index].Hue,
                    hiding);
                total += TagCategoryHeaderHeight * scale
                    + VChipFlow.Measure(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(chipModels), width,
                        scale);
                chipModels.Clear();
                if (index < categories.Length - 1)
                {
                    total += 12f * scale;
                }
            }

            return total + FacetRevealPadY * scale;
        }

        FillFacetChips(facet, target, hiding);
        return VChipFlow.Measure(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(chipModels), width, scale)
            + FacetRevealPadY * scale;
    }

    private bool DrawFacetChips(VelvetFilterFacet facet, VelvetFilterSelection target, bool hiding, float width)
    {
        if (facet == VelvetFilterFacet.Region)
        {
            return DrawRegionChips(target, width);
        }

        if (facet == VelvetFilterFacet.Tags)
        {
            return DrawTagChips(target, hiding, width);
        }

        FillFacetChips(facet, target, hiding);
        var clicked = DrawChipFlow(width, UiScale.Current);
        return clicked >= 0 && ApplyFacetPick(facet, target, clicked);
    }

    private bool ApplyFacetPick(VelvetFilterFacet facet, VelvetFilterSelection target, int clicked)
    {
        switch (facet)
        {
            case VelvetFilterFacet.Race:
                target.Race = VelvetRace.Toggle(target.Race, VelvetRace.All[clicked]);
                return true;
            case VelvetFilterFacet.Intent:
                ToggleMask(ref target.Intent, VelvetIntent.All[clicked].Flag);
                return true;
            case VelvetFilterFacet.Gender:
                ToggleMask(ref target.Gender, VelvetGender.All[clicked]);
                return true;
            case VelvetFilterFacet.Sexuality:
                ToggleMask(ref target.Sexuality, VelvetSexuality.All[clicked]);
                return true;
            case VelvetFilterFacet.Relationship:
                ToggleMask(ref target.Relationship, 1 << VelvetRelationship.All[clicked]);
                return true;
            case VelvetFilterFacet.Role:
                return ToggleToken(target.Roles, VelvetSuggestions.Roles[clicked]);
            case VelvetFilterFacet.Kinks:
                return ToggleToken(target.Kinks, VelvetSuggestions.Kinks[clicked]);
            case VelvetFilterFacet.Limits:
                return ToggleToken(target.Limits, VelvetSuggestions.Limits[clicked]);
            default:
                return false;
        }
    }

    private static bool ToggleToken(HashSet<string> target, string token)
    {
        if (!target.Remove(token))
        {
            target.Add(token);
        }

        return true;
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

    private bool DrawRegionChips(VelvetFilterSelection target, float width)
    {
        var codes = SocialRegion.Codes;
        chipModels.Clear();
        for (var index = 0; index < codes.Length; index++)
        {
            chipModels.Add(PickChip(codes[index], VelvetTheme.RegionAccent,
                (target.RegionMask & (1 << index)) != 0, false));
        }

        var clicked = DrawChipFlow(width, UiScale.Current);
        if (clicked < 0)
        {
            return false;
        }

        target.RegionMask = SocialRegion.ToggleMask(target.RegionMask, clicked);
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
