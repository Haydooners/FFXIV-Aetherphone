using System;
using System.Collections.Generic;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Recruit;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Game.Gui.PartyFinder.Types;

namespace Aetherphone.Apps.Recruit;

internal sealed partial class RecruitApp 
{
    private static readonly string[] PfCategoryLabels =
    {
        "All Duties",
        "High-End",
        "Raids",
        "Trials",
        "Dungeons",
        "Deep Dungeon",
        "Field Ops",
        "Hunts & Maps",
        "Other",
    };

    private PfCategory? selectedPfCategory;
    private DateTime lastPfPollTime = DateTime.MinValue;
    private static readonly TimeSpan PfAutoRefreshInterval = TimeSpan.FromSeconds(60);

    private readonly PfFilterCriteria pfFilterCriteria = new();
    private readonly SheetSurface pfFilterSheet = new("recruit.pfFilter");
    private readonly DropdownMenu pfFilterDutyTypeMenu = new();
    private readonly List<DropdownMenu.Item> pfFilterDutyTypeItems = new();
    private readonly DropdownMenu pfFilterDutyMenu = new();
    private readonly List<DropdownMenu.Item> pfFilterDutyItems = new();
    private readonly List<string> cachedSpecificDuties = new();
    private bool PartyFinderCapturesPointer =>
        activeTab == RecruitTab.PartyFinder &&
        (pfFilterSheet.CapturesPointer || pfFilterDutyTypeMenu.Open || pfFilterDutyMenu.Open);

    private void OnPartyFinderOpened()
    {
        lastPfPollTime = DateTime.UtcNow;
        store.RefreshPartyFinder(force: true);
    }

    private void DrawPartyFinderOverlays(Rect screen, PhoneTheme theme)
    {
        pfFilterDutyTypeMenu.Gate();
        pfFilterDutyMenu.Gate();
        if (activeTab != RecruitTab.PartyFinder)
        {
            return;
        }
        using (InputShield.Engage(pfFilterDutyTypeMenu.Open || pfFilterDutyMenu.Open))
        {
            pfFilterSheet.Draw(screen, theme, "Search Criteria", 0.78f, DrawPartyFinderFilterModal);
        }
        var pickedDutyType = pfFilterDutyTypeMenu.Draw(
            screen,
            theme,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(pfFilterDutyTypeItems));
        if (pickedDutyType >= 0)
        {
            if (pickedDutyType == 0)
            {
                pfFilterCriteria.DutyCategory = null;
                selectedPfCategory = null;
            }
            else
            {
                var cat = (PfCategory)pickedDutyType;
                pfFilterCriteria.DutyCategory = cat;
                selectedPfCategory = cat;
            }
            pfFilterCriteria.SpecificDuty = string.Empty;
        }
        var pickedDuty = pfFilterDutyMenu.Draw(
            screen,
            theme,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(pfFilterDutyItems));
        if (pickedDuty >= 0)
        {
            if (pickedDuty == 0)
            {
                pfFilterCriteria.SpecificDuty = string.Empty;
            }
            else if (pickedDuty - 1 < cachedSpecificDuties.Count)
            {
                pfFilterCriteria.SpecificDuty = cachedSpecificDuties[pickedDuty - 1];
            }
        }
    }

    private void DrawPartyFinderTab(Rect area, float scale, PhoneTheme theme)
    {
        var accent = Accent;
        var headerHeight = 36f * scale;
        var headerY = area.Min.Y + 8f * scale;

        Typography.Draw(new Vector2(area.Min.X + 14f * scale, headerY), "Party Finder", theme.TextStrong, 1.25f, FontWeight.Bold);

        var pfListings = store.PartyFinderListings;
        var totalMatchingCount = 0;
        for (var checkIndex = 0; checkIndex < pfListings.Count; checkIndex++)
        {
            var listing = pfListings[checkIndex];
            if ((!selectedPfCategory.HasValue || MatchesPfCategory(listing, selectedPfCategory.Value))
                && MatchesPfCriteria(listing, pfFilterCriteria))
            {
                totalMatchingCount++;
            }
        }

        var badgeText = totalMatchingCount.ToString();
        var badgeTextSize = Typography.Measure(badgeText, TextStyles.Caption1);
        var badgeCenter = new Vector2(area.Min.X + 145f * scale, headerY + 12f * scale);
        var badgeRadius = MathF.Max(10f * scale, badgeTextSize.X * 0.5f + 4f * scale);
        ImGui.GetWindowDrawList().AddCircleFilled(badgeCenter, badgeRadius,
            ImGui.GetColorU32(Palette.WithAlpha(theme.SurfaceMuted, 0.90f)));
        Typography.DrawCentered(ImGui.GetWindowDrawList(), badgeCenter, badgeText, theme.TextStrong, TextStyles.Caption1);

        var filterBtnWidth = 72f * scale;
        var btnHeight = 26f * scale;
        var btnY = headerY + 2f * scale;
        var filterMaxX = area.Max.X - 12f * scale;
        var filterMin = new Vector2(filterMaxX - filterBtnWidth, btnY);
        var filterMax = new Vector2(filterMaxX, btnY + btnHeight);
        var filterHovered = UiInteract.Hover(filterMin, filterMax);
        if (filterHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Squircle.Fill(ImGui.GetWindowDrawList(), filterMin, filterMax, 7f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.SurfaceMuted, filterHovered ? 0.95f : 0.70f)));
        Squircle.Stroke(ImGui.GetWindowDrawList(), filterMin, filterMax, 7f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, filterHovered ? 0.6f : 0.3f)), 1f * scale);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(filterMin.X + 10f * scale, btnY + 5f * scale), "Filter", theme.TextStrong, TextStyles.Caption1);
        AppSkin.Icon(ImGui.GetWindowDrawList(), new Vector2(filterMax.X - 14f * scale, (filterMin.Y + filterMax.Y) * 0.5f),
            IconGlyph.Of(FontAwesomeIcon.SlidersH), theme.TextStrong, 0.75f);
        if (UiInteract.Click(filterMin, filterMax, filterHovered))
        {
            pfFilterSheet.Open();
        }
        if ((DateTime.UtcNow - lastPfPollTime) > PfAutoRefreshInterval)
        {
            lastPfPollTime = DateTime.UtcNow;
            store.RefreshPartyFinder();
        }

        var top = headerY + headerHeight + 8f * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var renderedCount = 0;
            for (var index = 0; index < pfListings.Count; index++)
            {
                var listing = pfListings[index];
                if ((!selectedPfCategory.HasValue || MatchesPfCategory(listing, selectedPfCategory.Value))
                    && MatchesPfCriteria(listing, pfFilterCriteria))
                {
                    DrawPartyFinderCard(width, scale, theme, accent, listing);
                    ImGui.Dummy(new Vector2(0f, 10f * scale));
                    renderedCount++;
                }
            }
            if (renderedCount == 0)
            {
                Typography.DrawCentered(ImGui.GetWindowDrawList(),
                    ImGui.GetCursorScreenPos() + new Vector2(width * 0.5f, 40f * scale),
                    "No Party Finder listings found", theme.TextMuted, TextStyles.Body);
            }
            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }
    }

    private static string PfCategoryIcon(PfCategory category) => category switch
    {
        PfCategory.HighEnd => IconGlyph.Of(FontAwesomeIcon.Skull),
        PfCategory.Raids => IconGlyph.Of(FontAwesomeIcon.ShieldAlt),
        PfCategory.Trials => IconGlyph.Of(FontAwesomeIcon.Crosshairs),
        PfCategory.Dungeons => IconGlyph.Of(FontAwesomeIcon.Dungeon),
        PfCategory.DeepDungeon => IconGlyph.Of(FontAwesomeIcon.LayerGroup),
        PfCategory.FieldOps => IconGlyph.Of(FontAwesomeIcon.MapMarkedAlt),
        PfCategory.HuntAndMaps => IconGlyph.Of(FontAwesomeIcon.Compass),
        PfCategory.Other => IconGlyph.Of(FontAwesomeIcon.EllipsisH),
        _ => IconGlyph.Of(FontAwesomeIcon.ListUl),
    };

    private static bool MatchesPfCategory(PartyFinderListing listing, PfCategory category)
    {
        var categoryName = listing.CategoryName;
        return category switch
        {
            PfCategory.HighEnd =>
                categoryName.Contains("HighEnd", StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains("High-End", StringComparison.OrdinalIgnoreCase),

            PfCategory.Raids =>
                categoryName.Contains("Raid", StringComparison.OrdinalIgnoreCase) &&
                !categoryName.Contains("HighEnd", StringComparison.OrdinalIgnoreCase),

            PfCategory.Trials =>
                categoryName.Contains("Trial", StringComparison.OrdinalIgnoreCase) &&
                !categoryName.Contains("HighEnd", StringComparison.OrdinalIgnoreCase),

            PfCategory.Dungeons =>
                (categoryName.Contains("Dungeon", StringComparison.OrdinalIgnoreCase) &&
                 !categoryName.Contains("Deep", StringComparison.OrdinalIgnoreCase)) ||
                categoryName.Contains("Roulette", StringComparison.OrdinalIgnoreCase),

            PfCategory.DeepDungeon =>
                categoryName.Contains("Deep", StringComparison.OrdinalIgnoreCase),

            PfCategory.FieldOps =>
                categoryName.Contains("Field", StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains("Foray", StringComparison.OrdinalIgnoreCase),

            PfCategory.HuntAndMaps =>
                categoryName.Contains("Hunt", StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains("Treasure", StringComparison.OrdinalIgnoreCase),

            PfCategory.Other =>
                categoryName.Contains("Other", StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains("None", StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains("GoldSaucer", StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains("Quest", StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains("Fate", StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains("PvP", StringComparison.OrdinalIgnoreCase),
            _ => true,
        };
    }

    private void DrawPartyFinderCard(float width, float scale, PhoneTheme theme, Vector4 accent, PartyFinderListing listing)
    {
        var min = ImGui.GetCursorScreenPos();
        var padX = 14f * scale;
        var padY = 12f * scale;
        var contentWidth = width - padX * 2f;
        var cardHeight = 152f * scale;
        var max = min + new Vector2(width, cardHeight);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var drawList = ImGui.GetWindowDrawList();
        var cardBg = hovered ? Palette.WithAlpha(theme.GroupedCard, 0.98f) : theme.GroupedCard;
        var cardBorder = hovered ? Palette.WithAlpha(accent, 0.6f) : Palette.WithAlpha(theme.Separator, 0.35f);
        Squircle.Fill(drawList, min, max, 14f * scale, ImGui.GetColorU32(cardBg));
        Squircle.Stroke(drawList, min, max, 14f * scale, ImGui.GetColorU32(cardBorder), (hovered ? 1.5f : 1f) * scale);

        var catName = !string.IsNullOrWhiteSpace(listing.CategoryName) ? listing.CategoryName.ToUpperInvariant() : "DUTY";
        DrawTagPill(drawList, min + new Vector2(padX, padY), catName, theme, scale);

        var titleY = min.Y + padY + 22f * scale;
        var titleColor = hovered ? new Vector4(0.24f, 0.72f, 0.98f, 1f) : theme.TextStrong;
        var titleFitted = Typography.FitText(listing.DutyName, contentWidth, 1.05f, FontWeight.Bold);
        Typography.Draw(drawList, new Vector2(min.X + padX, titleY), titleFitted, titleColor, 1.05f, FontWeight.Bold);

        var commentY = titleY + 22f * scale;
        if (!string.IsNullOrWhiteSpace(listing.Comment))
        {
            var commentFitted = Typography.FitText(listing.Comment, contentWidth, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(min.X + padX, commentY), commentFitted, theme.TextMuted, TextStyles.Footnote);
        }

        var slotsY = min.Y + 84f * scale;
        DrawPfSlotsRow(drawList, new Vector2(min.X + padX, slotsY), listing.Slots, scale, theme);

        var metaText = $"Posted by: {listing.AuthorName} - {listing.WorldName}";
        Typography.Draw(drawList, new Vector2(min.X + padX, min.Y + cardHeight - padY - 14f * scale),
            Typography.FitText(metaText, contentWidth, TextStyles.Caption1), theme.TextMuted, TextStyles.Caption1);
        if (UiInteract.Click(min, max, hovered))
        {
            selectedPfListing = listing;
            router.Push(RecruitScreen.PfDetail);
        }
        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private static void DrawPfSlotsRow(ImDrawListPtr drawList, Vector2 pos, IReadOnlyList<PfSlotInfo>? slots, float scale, PhoneTheme theme)
    {
        var boxSize = 18f * scale;
        var gap = 4f * scale;
        var currentX = pos.X;
        var totalSlots = slots != null && slots.Count > 0 ? Math.Min(8, slots.Count) : 8;

        for (var i = 0; i < totalSlots; i++)
        {
            var min = new Vector2(currentX, pos.Y);
            var max = min + new Vector2(boxSize, boxSize);
            var center = (min + max) * 0.5f;
            if (slots != null && i < slots.Count)
            {
                var slot = slots[i];
                var baseColor = slot.Role switch
                {
                    PfSlotRole.Tank => AccentRing.Azure,
                    PfSlotRole.Healer => AccentRing.Green,
                    _ => AccentRing.Rose,
                };
                if (slot.IsFilled)
                {
                    Squircle.Fill(drawList, min, max, 3.5f * scale, ImGui.GetColorU32(baseColor));
                    AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.Check), new Vector4(1f, 1f, 1f, 1f), 0.60f);
                }
                else
                {
                    Squircle.Fill(drawList, min, max, 3.5f * scale, ImGui.GetColorU32(Palette.WithAlpha(baseColor, 0.25f)));
                    Squircle.Stroke(drawList, min, max, 3.5f * scale, ImGui.GetColorU32(Palette.WithAlpha(baseColor, 0.55f)), 1f * scale);
                }
            }
            else
            {
                var emptyColor = Palette.WithAlpha(new Vector4(0.70f, 0.68f, 0.62f, 1f), 0.30f);
                Squircle.Fill(drawList, min, max, 3.5f * scale, ImGui.GetColorU32(emptyColor));
            }
            currentX += boxSize + gap;
        }
    }

    private bool MatchesPfCriteria(PartyFinderListing listing, PfFilterCriteria criteria)
    {
        if (!criteria.IsActive)
        {
            return true;
        }
        if (criteria.DutyCategory.HasValue && !MatchesPfCategory(listing, criteria.DutyCategory.Value))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(criteria.SpecificDuty))
        {
            var match = listing.DutyName.Contains(criteria.SpecificDuty, StringComparison.OrdinalIgnoreCase)
                || criteria.SpecificDuty.Contains(listing.DutyName, StringComparison.OrdinalIgnoreCase);
            if (!match)
            {
                return false;
            }
        }
        if (criteria.CanJoinAsCurrentJob)
        {
            var localPlayer = Plugin.ObjectTable.LocalPlayer;
            if (localPlayer != null)
            {
                var currentJobFlag = RecruitCatalog.JobIdToJobFlag(localPlayer.ClassJob.RowId);
                if (currentJobFlag == null || !listing.AcceptsJob(currentJobFlag.Value))
                {
                    return false;
                }
            }
        }
        if (criteria.TankOpen && !listing.HasOpenRole(PfSlotRole.Tank))
        {
            return false;
        }
        if (criteria.HealerOpen && !listing.HasOpenRole(PfSlotRole.Healer))
        {
            return false;
        }
        if (criteria.DpsOpen && !listing.HasOpenRole(PfSlotRole.Dps))
        {
            return false;
        }
        if (criteria.Objective != PfObjectiveFilter.Any)
        {
            var match = criteria.Objective switch
            {
                PfObjectiveFilter.Practice => listing.Objective.HasFlag(ObjectiveFlags.Practice),
                PfObjectiveFilter.DutyCompletion => listing.Objective.HasFlag(ObjectiveFlags.DutyCompletion),
                PfObjectiveFilter.Loot => listing.Objective.HasFlag(ObjectiveFlags.Loot),
                _ => true,
            };
            if (!match)
            {
                return false;
            }
        }
        if (criteria.DutyCompleteOnly && !listing.Conditions.HasFlag(ConditionFlags.DutyComplete))
        {
            return false;
        }
        if (criteria.DutyIncompleteOnly && !listing.Conditions.HasFlag(ConditionFlags.DutyIncomplete))
        {
            return false;
        }
        if (criteria.UnrestrictedParty && !listing.DutySettings.HasFlag(DutyFinderSettingsFlags.UnrestrictedParty))
        {
            return false;
        }
        if (criteria.MinimalItemLevel && listing.ItemLevel == 0)
        {
            return false;
        }
        if (criteria.SameWorldOnly)
        {
            var localHomeWorld = Plugin.ObjectTable.LocalPlayer?.HomeWorld.RowId ?? 0u;
            if (localHomeWorld != 0 && listing.HomeWorldId != localHomeWorld)
            {
                return false;
            }
        }
        return true;
    }

    private void DrawPartyFinderFilterModal(Rect content)
    {
        var scale = UiScale.Current;
        var theme = currentContext.Theme;
        var accent = Core.Apps.AppAccents.For("recruit");
        var drawList = ImGui.GetWindowDrawList();
        var resetText = "Reset";
        var resetSize = Typography.Measure(resetText, 0.85f, FontWeight.Medium);
        var resetMin = new Vector2(content.Max.X - resetSize.X - 12f * scale, content.Min.Y - 26f * scale);
        var resetMax = new Vector2(content.Max.X, content.Min.Y - 4f * scale);
        var resetHovered = UiInteract.Hover(resetMin, resetMax);
        if (resetHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var resetColor = pfFilterCriteria.IsActive
            ? (resetHovered ? accent : Palette.WithAlpha(accent, 0.85f))
            : theme.TextMuted;
        Typography.Draw(drawList, new Vector2(resetMin.X, (resetMin.Y + resetMax.Y - resetSize.Y) * 0.5f), resetText, resetColor, 0.85f, FontWeight.Medium);
        if (pfFilterCriteria.IsActive && UiInteract.Click(resetMin, resetMax, resetHovered))
        {
            pfFilterCriteria.Reset();
        }

        using (AppSurface.Begin(new Rect(content.Min, new Vector2(content.Max.X, content.Max.Y - 46f * scale))))
        {
            var toggleWidth = ScrollLayout.StableContentWidth();
            var gap = 6f * scale;
            DrawFilterSectionHeader("DUTY TYPE", theme, scale);
            var dutyTypeLabel = pfFilterCriteria.DutyCategory.HasValue
                ? PfCategoryLabels[(int)pfFilterCriteria.DutyCategory.Value]
                : "All Duties";
            var dutyTypeIcon = pfFilterCriteria.DutyCategory.HasValue
                ? PfCategoryIcon(pfFilterCriteria.DutyCategory.Value)
                : IconGlyph.Of(FontAwesomeIcon.ListUl);
            DrawFilterDropdownField(
                dutyTypeLabel,
                dutyTypeIcon,
                pfFilterCriteria.DutyCategory.HasValue,
                toggleWidth,
                scale,
                theme,
                accent,
                drawList,
                OpenPfFilterDutyTypeMenu
            );

            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawFilterSectionHeader("SPECIFIC DUTY", theme, scale);
            var specificDutyLabel = !string.IsNullOrWhiteSpace(pfFilterCriteria.SpecificDuty)
                ? pfFilterCriteria.SpecificDuty
                : (pfFilterCriteria.DutyCategory.HasValue ? $"All {dutyTypeLabel} Duties" : "All Duties");
            var specificDutyIcon = !string.IsNullOrWhiteSpace(pfFilterCriteria.SpecificDuty)
                ? IconGlyph.Of(FontAwesomeIcon.Crosshairs)
                : IconGlyph.Of(FontAwesomeIcon.LayerGroup);
            DrawFilterDropdownField(
                specificDutyLabel,
                specificDutyIcon,
                !string.IsNullOrWhiteSpace(pfFilterCriteria.SpecificDuty),
                toggleWidth,
                scale,
                theme,
                accent,
                drawList,
                OpenPfFilterDutyMenu
            );

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            DrawFilterSectionHeader("ROLES SOUGHT", theme, scale);
            var localPlayer = Plugin.ObjectTable.LocalPlayer;
            var currentJobAbbr = localPlayer?.ClassJob.ValueNullable?.Abbreviation.ToString();
            var myJobLabel = string.IsNullOrWhiteSpace(currentJobAbbr)
                ? "Can Join as Current Job"
                : $"Can Join as Current Job ({currentJobAbbr})";
            ImGui.SetNextItemWidth(toggleWidth);
            var toggleHeight = 30f * scale;
            var toggleMin = ImGui.GetCursorScreenPos();
            var toggleMax = new Vector2(toggleMin.X + toggleWidth, toggleMin.Y + toggleHeight);
            var toggleHovered = UiInteract.Hover(toggleMin, toggleMax);
            if (toggleHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var toggleActive = pfFilterCriteria.CanJoinAsCurrentJob;
            var toggleBg = toggleActive
                ? Palette.WithAlpha(accent, 0.22f)
                : (toggleHovered ? Palette.WithAlpha(theme.GroupedCard, 0.90f) : Palette.WithAlpha(theme.GroupedCard, 0.60f));
            var toggleBorder = toggleActive
                ? Palette.WithAlpha(accent, 0.70f)
                : Palette.WithAlpha(theme.TextMuted, toggleHovered ? 0.35f : 0.18f);
            Squircle.Fill(drawList, toggleMin, toggleMax, 6f * scale, ImGui.GetColorU32(toggleBg));
            Squircle.Stroke(drawList, toggleMin, toggleMax, 6f * scale, ImGui.GetColorU32(toggleBorder), 1f * scale);
            var checkCenter = new Vector2(toggleMin.X + 16f * scale, (toggleMin.Y + toggleMax.Y) * 0.5f);
            if (toggleActive)
            {
                AppSkin.Icon(drawList, checkCenter, IconGlyph.Of(FontAwesomeIcon.Check), accent, 0.75f);
            }
            else
            {
                drawList.AddCircle(checkCenter, 5f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.TextMuted, 0.4f)), 16, 1.2f * scale);
            }

            var myJobTextSize = Typography.Measure(myJobLabel, 0.85f, FontWeight.Medium);
            var myJobTextPos = new Vector2(toggleMin.X + 30f * scale, (toggleMin.Y + toggleMax.Y - myJobTextSize.Y) * 0.5f);
            Typography.Draw(drawList, myJobTextPos, myJobLabel, toggleActive ? theme.TextStrong : theme.TextMuted, 0.85f, FontWeight.Medium);
            if (UiInteract.Click(toggleMin, toggleMax, toggleHovered))
            {
                pfFilterCriteria.CanJoinAsCurrentJob = !pfFilterCriteria.CanJoinAsCurrentJob;
            }

            ImGui.Dummy(new Vector2(toggleWidth, toggleHeight + 6f * scale));
            var roleRowX = ImGui.GetCursorScreenPos().X;
            var roleRowY = ImGui.GetCursorScreenPos().Y + 12f * scale;
            if (DrawCustomFilterChip(ref roleRowX, roleRowY, gap, "Tank", pfFilterCriteria.TankOpen, AccentRing.Azure, theme))
            {
                pfFilterCriteria.TankOpen = !pfFilterCriteria.TankOpen;
            }
            if (DrawCustomFilterChip(ref roleRowX, roleRowY, gap, "Healer", pfFilterCriteria.HealerOpen, AccentRing.Green, theme))
            {
                pfFilterCriteria.HealerOpen = !pfFilterCriteria.HealerOpen;
            }
            if (DrawCustomFilterChip(ref roleRowX, roleRowY, gap, "DPS", pfFilterCriteria.DpsOpen, AccentRing.Rose, theme))
            {
                pfFilterCriteria.DpsOpen = !pfFilterCriteria.DpsOpen;
            }

            ImGui.Dummy(new Vector2(toggleWidth, 28f * scale));
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            DrawFilterSectionHeader("OBJECTIVE", theme, scale);
            var objRowX = ImGui.GetCursorScreenPos().X;
            var objRowY = ImGui.GetCursorScreenPos().Y + 12f * scale;
            if (AppSkin.FlowChip(ref objRowX, objRowY, gap, "Any", pfFilterCriteria.Objective == PfObjectiveFilter.Any, theme))
            {
                pfFilterCriteria.Objective = PfObjectiveFilter.Any;
            }
            if (AppSkin.FlowChip(ref objRowX, objRowY, gap, "Practice", pfFilterCriteria.Objective == PfObjectiveFilter.Practice, theme))
            {
                pfFilterCriteria.Objective = pfFilterCriteria.Objective == PfObjectiveFilter.Practice ? PfObjectiveFilter.Any : PfObjectiveFilter.Practice;
            }
            if (AppSkin.FlowChip(ref objRowX, objRowY, gap, "Completion", pfFilterCriteria.Objective == PfObjectiveFilter.DutyCompletion, theme))
            {
                pfFilterCriteria.Objective = pfFilterCriteria.Objective == PfObjectiveFilter.DutyCompletion ? PfObjectiveFilter.Any : PfObjectiveFilter.DutyCompletion;
            }
            if (AppSkin.FlowChip(ref objRowX, objRowY, gap, "Loot", pfFilterCriteria.Objective == PfObjectiveFilter.Loot, theme))
            {
                pfFilterCriteria.Objective = pfFilterCriteria.Objective == PfObjectiveFilter.Loot ? PfObjectiveFilter.Any : PfObjectiveFilter.Loot;
            }

            ImGui.Dummy(new Vector2(toggleWidth, 28f * scale));
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            DrawFilterSectionHeader("CONDITIONS", theme, scale);
            var condRowX = ImGui.GetCursorScreenPos().X;
            var condRowY = ImGui.GetCursorScreenPos().Y + 12f * scale;
            if (AppSkin.FlowChip(ref condRowX, condRowY, gap, "Duty Complete", pfFilterCriteria.DutyCompleteOnly, theme))
            {
                pfFilterCriteria.DutyCompleteOnly = !pfFilterCriteria.DutyCompleteOnly;
                if (pfFilterCriteria.DutyCompleteOnly)
                {
                    pfFilterCriteria.DutyIncompleteOnly = false;
                }
            }
            if (AppSkin.FlowChip(ref condRowX, condRowY, gap, "Duty Incomplete", pfFilterCriteria.DutyIncompleteOnly, theme))
            {
                pfFilterCriteria.DutyIncompleteOnly = !pfFilterCriteria.DutyIncompleteOnly;
                if (pfFilterCriteria.DutyIncompleteOnly)
                {
                    pfFilterCriteria.DutyCompleteOnly = false;
                }
            }

            ImGui.Dummy(new Vector2(toggleWidth, 28f * scale));
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            DrawFilterSectionHeader("DUTY SETTINGS & AREA", theme, scale);
            var setRowX = ImGui.GetCursorScreenPos().X;
            var setRowY = ImGui.GetCursorScreenPos().Y + 12f * scale;
            if (AppSkin.FlowChip(ref setRowX, setRowY, gap, "Unsynced", pfFilterCriteria.UnrestrictedParty, theme))
            {
                pfFilterCriteria.UnrestrictedParty = !pfFilterCriteria.UnrestrictedParty;
            }
            if (AppSkin.FlowChip(ref setRowX, setRowY, gap, "Min iLvl", pfFilterCriteria.MinimalItemLevel, theme))
            {
                pfFilterCriteria.MinimalItemLevel = !pfFilterCriteria.MinimalItemLevel;
            }
            if (AppSkin.FlowChip(ref setRowX, setRowY, gap, "Same World", pfFilterCriteria.SameWorldOnly, theme))
            {
                pfFilterCriteria.SameWorldOnly = !pfFilterCriteria.SameWorldOnly;
            }
            ImGui.Dummy(new Vector2(toggleWidth, 34f * scale));
        }       
        var btnHeight = 36f * scale;
        var btnMin = new Vector2(content.Min.X, content.Max.Y - btnHeight);
        var btnMax = new Vector2(content.Max.X, content.Max.Y);
        var btnHovered = UiInteract.Hover(btnMin, btnMax);
        if (btnHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var btnBg = Palette.WithAlpha(accent, btnHovered ? 0.95f : 0.85f);
        Squircle.Fill(drawList, btnMin, btnMax, 8f * scale, ImGui.GetColorU32(btnBg));
        var applyLabel = "Apply Filters";
        Typography.DrawCentered(drawList, (btnMin + btnMax) * 0.5f, applyLabel, new Vector4(1f, 1f, 1f, 1f), 0.95f, FontWeight.SemiBold);
        if (UiInteract.Click(btnMin, btnMax, btnHovered))
        {
            pfFilterSheet.Close();
        }
    }

    private void OpenPfFilterDutyTypeMenu(Rect anchor)
    {
        pfFilterDutyTypeItems.Clear();
        pfFilterDutyTypeItems.Add(new DropdownMenu.Item("All Duties", Selected: !pfFilterCriteria.DutyCategory.HasValue));
        for (var index = 1; index < PfCategoryLabels.Length; index++)
        {
            var category = (PfCategory)index;
            pfFilterDutyTypeItems.Add(new DropdownMenu.Item(
                PfCategoryLabels[index],
                Selected: pfFilterCriteria.DutyCategory == category));
        }

        pfFilterDutyTypeMenu.Toggle("recruit_filter_duty_type", anchor);
    }

    private void OpenPfFilterDutyMenu(Rect anchor)
    {
        pfFilterDutyItems.Clear();
        cachedSpecificDuties.Clear();
        var catLabel = pfFilterCriteria.DutyCategory.HasValue
            ? PfCategoryLabels[(int)pfFilterCriteria.DutyCategory.Value]
            : "All";
        var allLabel = pfFilterCriteria.DutyCategory.HasValue
            ? $"All {catLabel} Duties"
            : "All Duties";
        pfFilterDutyItems.Add(new DropdownMenu.Item(allLabel, Selected: string.IsNullOrWhiteSpace(pfFilterCriteria.SpecificDuty)));
        var duties = GetDutiesForCategory(pfFilterCriteria.DutyCategory);
        for (var index = 0; index < duties.Count; index++)
        {
            var dName = duties[index];
            cachedSpecificDuties.Add(dName);
            pfFilterDutyItems.Add(new DropdownMenu.Item(
                dName,
                Selected: string.Equals(pfFilterCriteria.SpecificDuty, dName, StringComparison.OrdinalIgnoreCase)));
        }

        pfFilterDutyMenu.Toggle("recruit_filter_specific_duty", anchor);
    }

    private static void DrawFilterDropdownField(
        string label,
        string glyph,
        bool isFiltered,
        float width,
        float scale,
        PhoneTheme theme,
        Vector4 accent,
        ImDrawListPtr drawList,
        Action<Rect> onClick)
    {
        var height = 34f * scale;
        var min = ImGui.GetCursorScreenPos();
        var max = new Vector2(min.X + width, min.Y + height);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var bg = isFiltered
            ? Palette.WithAlpha(accent, hovered ? 0.22f : 0.14f)
            : (hovered ? Palette.WithAlpha(theme.GroupedCard, 0.95f) : Palette.WithAlpha(theme.GroupedCard, 0.65f));
        var border = isFiltered
            ? Palette.WithAlpha(accent, hovered ? 0.75f : 0.55f)
            : Palette.WithAlpha(theme.TextMuted, hovered ? 0.35f : 0.18f);
        Squircle.Fill(drawList, min, max, 8f * scale, ImGui.GetColorU32(bg));
        Squircle.Stroke(drawList, min, max, 8f * scale, ImGui.GetColorU32(border), 1f * scale);
        var centerY = (min.Y + max.Y) * 0.5f;
        var iconColor = isFiltered ? accent : (hovered ? theme.TextStrong : theme.TextMuted);
        var iconCenter = new Vector2(min.X + 16f * scale, centerY);
        AppSkin.Icon(drawList, iconCenter, glyph, iconColor, 0.75f);
        var textStartX = min.X + 32f * scale;
        var maxTextWidth = width - 56f * scale;
        var fittedText = Typography.FitText(label, maxTextWidth, TextStyles.Caption1);
        var fittedSize = Typography.Measure(fittedText, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(textStartX, centerY - fittedSize.Y * 0.5f), fittedText,
            isFiltered ? theme.TextStrong : (hovered ? theme.TextStrong : theme.TextMuted), TextStyles.Caption1);
        var chevronRight = max.X - 14f * scale;
        AppSkin.Icon(drawList, new Vector2(chevronRight, centerY), IconGlyph.Of(FontAwesomeIcon.ChevronDown), iconColor, 0.60f);
        if (UiInteract.Click(min, max, hovered))
        {
            onClick(new Rect(min, max));
        }
        ImGui.Dummy(new Vector2(width, height));
    }

    private List<string> GetDutiesForCategory(PfCategory? category)
    {
        var result = new List<string>();
        var listings = store.PartyFinderListings;
        for (var i = 0; i < listings.Count; i++)
        {
            var l = listings[i];
            if (category.HasValue && !MatchesPfCategory(l, category.Value))
            {
                continue;
            }
            if (!string.IsNullOrWhiteSpace(l.DutyName) && !result.Contains(l.DutyName))
            {
                result.Add(l.DutyName);
            }
        }

        if (result.Count == 0)
        {
            for (var i = 0; i < RecruitCatalog.Duties.Count; i++)
            {
                var duty = RecruitCatalog.Duties[i];
                if (category.HasValue)
                {
                    var matchesCat = category.Value switch
                    {
                        PfCategory.HighEnd => duty.Category is ContentCategory.Ultimate or ContentCategory.Savage or ContentCategory.ExtremeFarm or ContentCategory.Criterion,
                        PfCategory.Raids => duty.Category is ContentCategory.Savage,
                        PfCategory.Trials => duty.Category is ContentCategory.ExtremeFarm,
                        PfCategory.DeepDungeon => duty.Category is ContentCategory.DeepDungeon,
                        PfCategory.Other => duty.Category is ContentCategory.Other,
                        _ => false,
                    };
                    if (!matchesCat)
                    {
                        continue;
                    }
                }
                if (!result.Contains(duty.Name))
                {
                    result.Add(duty.Name);
                }
            }
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static bool DrawCustomFilterChip(ref float cursorX, float centerY, float gap, string label, bool active, Vector4 roleColor, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var textSize = Typography.Measure(label, 0.8f, FontWeight.Medium);
        var height = 28f * scale;
        var width = textSize.X + 22f * scale;
        var min = new Vector2(cursorX, centerY - height * 0.5f);
        var max = new Vector2(cursorX + width, centerY + height * 0.5f);
        var hovered = UiInteract.Hover(min, max);
        var fill = active ? Palette.WithAlpha(roleColor, 0.88f) : theme.GroupedCard;
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(fill));
        if (active)
        {
            Squircle.Stroke(drawList, min, max, height * 0.5f, ImGui.GetColorU32(roleColor), 1f * scale);
        }
        else if (hovered)
        {
            Squircle.Stroke(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(roleColor, 0.4f)), 1f * scale);
        }

        var ink = active ? new Vector4(1f, 1f, 1f, 1f) : (hovered ? theme.TextStrong : theme.TextMuted);
        Typography.Draw(drawList, new Vector2(min.X + (width - textSize.X) * 0.5f, centerY - textSize.Y * 0.5f),
            label, ink, 0.8f, FontWeight.Medium);
        cursorX = max.X + gap;
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private static void DrawFilterSectionHeader(string label, PhoneTheme theme, float scale)
    {
        var pos = ImGui.GetCursorScreenPos();
        Typography.Draw(ImGui.GetWindowDrawList(), pos, label, theme.TextMuted, 0.72f, FontWeight.Bold);
        ImGui.Dummy(new Vector2(0f, 16f * scale));
    }

    private void DrawPartyFinderDetailScreen(in PhoneContext context, Rect area)
    {
        if (selectedPfListing is null)
        {
            router.Pop();
            return;
        }
        var scale = UiScale.Current;
        var theme = ui.Theme;
        var listing = selectedPfListing;
        var padX = 16f * scale;

        var headerY = area.Min.Y + 8f * scale;
        var backCenter = new Vector2(area.Min.X + padX + 8f * scale, headerY + 12f * scale);
        if (ui.IconButton(backCenter, 14f * scale, IconGlyph.Of(FontAwesomeIcon.ChevronLeft), theme.TextStrong, AppSkin.Transparent, 0.90f, "Back"))
        {
            router.Pop();
            return;
        }

        var top = headerY + 36f * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            var originX = ImGui.GetCursorScreenPos().X;
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var catName = !string.IsNullOrWhiteSpace(listing.CategoryName) ? listing.CategoryName.ToUpperInvariant() : "DUTY";
            DrawTagPill(ImGui.GetWindowDrawList(), new Vector2(originX, ImGui.GetCursorScreenPos().Y), catName, theme, scale);
            ImGui.Dummy(new Vector2(0f, 22f * scale));
            var titleColor = new Vector4(0.24f, 0.74f, 0.98f, 1f);
            Typography.Draw(new Vector2(originX, ImGui.GetCursorScreenPos().Y), listing.DutyName, titleColor, 1.25f, FontWeight.Bold);
            ImGui.Dummy(new Vector2(0f, 26f * scale));
            var metaY = ImGui.GetCursorScreenPos().Y;
            Typography.Draw(new Vector2(originX, metaY), "Location", theme.TextMuted, TextStyles.Subheadline);
            Typography.Draw(new Vector2(originX + 75f * scale, metaY), listing.WorldName, theme.TextStrong, TextStyles.Subheadline);
            ImGui.Dummy(new Vector2(0f, 20f * scale));
            var timeY = ImGui.GetCursorScreenPos().Y;
            Typography.Draw(new Vector2(originX, timeY), "Time Limit", theme.TextMuted, TextStyles.Subheadline);
            Typography.Draw(new Vector2(originX + 85f * scale, timeY), "22 minutes remaining", theme.TextStrong, TextStyles.Subheadline);
            ImGui.Dummy(new Vector2(0f, 24f * scale));
            if (!string.IsNullOrWhiteSpace(listing.Comment))
            {
                var descHeight = Typography.DrawWrappedLeft(new Vector2(originX, ImGui.GetCursorScreenPos().Y),
                    listing.Comment, theme.TextMuted, TextStyles.Subheadline, width);
                ImGui.Dummy(new Vector2(0f, descHeight + 16f * scale));
            }
            
            var leaderY = ImGui.GetCursorScreenPos().Y;
            Typography.Draw(new Vector2(originX, leaderY), "Leader", theme.TextStrong, 1.05f, FontWeight.Bold);
            Typography.Draw(new Vector2(originX + 65f * scale, leaderY), listing.AuthorName, theme.TextStrong, 1.05f, FontWeight.Medium);
            ImGui.Dummy(new Vector2(0f, 32f * scale));
            DrawPfDetailSlotsGrid(width, scale, theme, listing.Slots);
            ImGui.Dummy(new Vector2(0f, 20f * scale));
            DrawDetailActionButtons(width, scale, theme, listing);
            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private static void DrawPfDetailSlotsGrid(float width, float scale, PhoneTheme theme, IReadOnlyList<PfSlotInfo>? slots)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var pad = 12f * scale;
        var gap = 8f * scale;
        var tileWidth = (width - pad * 2f - gap * 3f) / 4f;
        var tileHeight = tileWidth;
        var containerHeight = pad * 2f + tileHeight * 2f + gap;
        var containerMax = origin + new Vector2(width, containerHeight);

        Squircle.Fill(drawList, origin, containerMax, 14f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, 0.90f)));
        for (var i = 0; i < 8; i++)
        {
            var col = i % 4;
            var row = i / 4;
            var tileMin = new Vector2(origin.X + pad + col * (tileWidth + gap), origin.Y + pad + row * (tileHeight + gap));
            var tileMax = tileMin + new Vector2(tileWidth, tileHeight);
            var center = (tileMin + tileMax) * 0.5f;
            if (slots != null && i < slots.Count)
            {
                var slot = slots[i];
                var baseColor = slot.Role switch
                {
                    PfSlotRole.Tank => AccentRing.Azure,
                    PfSlotRole.Healer => AccentRing.Green,
                    _ => AccentRing.Rose,
                };

                Squircle.Fill(drawList, tileMin, tileMax, 8f * scale, ImGui.GetColorU32(baseColor));
                var iconDrawn = false;
                if (slot.JobId > 0)
                {
                    var iconId = GameData.JobIconId(slot.JobId);
                    var iconInset = 8f * scale;
                    iconDrawn = GameIconTile.Draw(drawList, Plugin.TextureProvider, iconId,
                        tileMin + new Vector2(iconInset), tileMax - new Vector2(iconInset), 4f * scale, scale);
                }
                if (!iconDrawn)
                {
                    var roleIcon = PfSlotIcon(slot.Role);
                    AppSkin.Icon(drawList, center, IconGlyph.Of(roleIcon), new Vector4(1f, 1f, 1f, 0.95f), 1.1f);
                }
            }
            else
            {
                var emptyColor = Palette.WithAlpha(new Vector4(0.70f, 0.68f, 0.62f, 1f), 0.25f);
                Squircle.Fill(drawList, tileMin, tileMax, 8f * scale, ImGui.GetColorU32(emptyColor));
            }
        }
        ImGui.Dummy(new Vector2(width, containerHeight));
    }

    private void DrawDetailActionButtons(float width, float scale, PhoneTheme theme, PartyFinderListing listing)
    {
        var drawList = ImGui.GetWindowDrawList();
        var btnHeight = 42f * scale;

        var joinMin = ImGui.GetCursorScreenPos();
        var joinMax = joinMin + new Vector2(width, btnHeight);
        var joinHovered = UiInteract.Hover(joinMin, joinMax);
        if (joinHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        Squircle.Fill(drawList, joinMin, joinMax, 10f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, joinHovered ? 0.98f : 0.85f)));
        Squircle.Stroke(drawList, joinMin, joinMax, 10f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, joinHovered ? 0.6f : 0.35f)), 1f * scale);
        Typography.DrawCentered(drawList, (joinMin + joinMax) * 0.5f, "Join Party", theme.TextStrong, 1.0f, FontWeight.Bold);
        if (UiInteract.Click(joinMin, joinMax, joinHovered))
        {
            PartyFinderReader.OpenListing(listing.ListingId);
        }
        ImGui.Dummy(new Vector2(width, btnHeight + 8f * scale));

        var tellMin = ImGui.GetCursorScreenPos();
        var tellMax = tellMin + new Vector2(width, btnHeight);
        var tellHovered = UiInteract.Hover(tellMin, tellMax);
        if (tellHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        Squircle.Fill(drawList, tellMin, tellMax, 10f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, tellHovered ? 0.98f : 0.85f)));
        Squircle.Stroke(drawList, tellMin, tellMax, 10f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, tellHovered ? 0.6f : 0.35f)), 1f * scale);
        Typography.DrawCentered(drawList, (tellMin + tellMax) * 0.5f, "Send/Tell", theme.TextStrong, 1.0f, FontWeight.Bold);
        if (UiInteract.Click(tellMin, tellMax, tellHovered))
        {
            ImGui.SetClipboardText($"/tell {listing.AuthorName}@{listing.WorldName} ");
        }
        ImGui.Dummy(new Vector2(width, btnHeight));
    }
}