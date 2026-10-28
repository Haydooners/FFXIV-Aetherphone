using System;
using System.Collections.Generic;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Game;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Recruit;
using Aetherphone.Windows.Components;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Recruit;

internal sealed partial class RecruitApp
{
    private int createStep;
    private ListingKind createKind = ListingKind.PlayerLfg;
    private readonly HashSet<string> createMindsets = new(StringComparer.OrdinalIgnoreCase) { "Midcore", "Prog" };
    private string createTitle = string.Empty;
    private readonly HashSet<RaidRole> createRoles = new() { RaidRole.Tank };
    private string createDescription = string.Empty;
    private readonly List<string> createTags = new() { "healer", "shield", "lgbt" };
    private string newTagBuffer = string.Empty;
    private string createStratUrl = string.Empty;
    private string createExtendedNotes = string.Empty;
    private string lastCheckedStratUrl = string.Empty;
    private bool createStratUrlValid = true;
    private int notesCounterLength = -1;
    private string notesCounterText = string.Empty;
    private const int StratUrlMaxLength = 256;
    private const int ExtendedNotesMaxLength = 1000;
    private RaidDays createDays = RaidDays.Monday | RaidDays.Tuesday | RaidDays.Wednesday;
    private int createStartHour12 = 8;
    private int createStartMinute = 0;
    private bool createStartIsPm = true;
    private int createEndHour12 = 12;
    private int createEndMinute = 30;
    private bool createEndIsPm = false;
    private readonly DropdownMenu kindMenu = new();
    private readonly List<DropdownMenu.Item> kindMenuItems = new();
    private readonly DropdownMenu dutyPickerMenu = new();
    private readonly List<DropdownMenu.Item> dutyPickerMenuItems = new();

    private PfPartyType createPartyType = PfPartyType.Normal;
    private PfDutyCategory createPfDutyCategory = PfDutyCategory.HighEndDuty;
    private PfDutyEntry? createPfSelectedDuty = null;
    private string createObjective = "None";
    private string createComment = string.Empty;
    private bool createRemoveRoleRestrictions;
    private bool createUnselectClasses = true;
    private bool createOnePlayerPerJob = true;

    private readonly SheetSurface rolePickerSheet = new("recruit.create.rolePicker");
    private int editingSlotIndex = -1;
    private readonly HashSet<uint> tempSlotJobs = new();
    private readonly PfSlotConfig[] createPfSlots = InitializeDefaultPfSlots();

    private static PfSlotConfig[] InitializeDefaultPfSlots()
    {
        var slots = new PfSlotConfig[8];
        for (var i = 0; i < 8; i++)
        {
            slots[i] = new PfSlotConfig();
        }
        slots[1].SetJobs(PfCatalog.TankJobIds);
        slots[2].SetJobs(PfCatalog.PureHealerJobIds);
        slots[3].SetJobs(PfCatalog.BarrierHealerJobIds);
        slots[4].SetJobs(PfCatalog.MeleeJobIds);
        slots[5].SetJobs(PfCatalog.PhysRangedJobIds);
        slots[6].SetJobs(PfCatalog.CasterJobIds);
        slots[7].SetJobs(PfCatalog.AllDpsJobIds);

        return slots;
    }

    private readonly DropdownMenu categoryPickerMenu = new();
    private readonly List<DropdownMenu.Item> categoryPickerMenuItems = new();
    private readonly DropdownMenu objectiveMenu = new();
    private readonly List<DropdownMenu.Item> objectiveMenuItems = new();
    private bool createLimitToWorldServer;
    private bool createIsPrivate;
    private string createPasscode = string.Empty;
    private PfCompletionStatus createCompletionStatus = PfCompletionStatus.None;
    private int createMinIlvl;
    private bool createUnrestrictedParty;
    private bool createMinimumIl;
    private bool createSilenceEcho;
    private PfLootRule createLootRule = PfLootRule.Normal;
    private PfLanguageFlags createLanguages = PfLanguageFlags.English;

    private string createMinIlvlText = string.Empty;
    private const int MaxItemLevel = 999;

    private static readonly string[] CompletionLabels = { "Any", "Duty Complete", "Duty Incomplete" };
    private static readonly string[] LootRuleLabels = { "Normal", "Greed Only", "Lootmaster" };
    private static readonly (string Label, PfLanguageFlags Flag)[] LanguageOptions =
    {
        ("JP", PfLanguageFlags.Japanese),
        ("EN", PfLanguageFlags.English),
        ("DE", PfLanguageFlags.German),
        ("FR", PfLanguageFlags.French),
    };

    private static readonly string[] MindsetOptions =
    {
        "Casual",
        "Midcore",
        "Hardcore",
        "Prog",
        "Beginner-Friendly",
    };

    private static readonly (string Label, RaidRole Role)[] RoleOptions =
    {
        ("Tank", RaidRole.Tank),
        ("Pure Healer", RaidRole.PureHealer),
        ("Shield Healer", RaidRole.BarrierHealer),
        ("Caster", RaidRole.Caster),
        ("Melee", RaidRole.Melee),
        ("Phys Ranged", RaidRole.PhysRanged),
    };

    private static readonly (string Label, RaidDays Day)[] DayButtons =
    {
        ("Su", RaidDays.Sunday),
        ("M", RaidDays.Monday),
        ("Tu", RaidDays.Tuesday),
        ("W", RaidDays.Wednesday),
        ("Th", RaidDays.Thursday),
        ("F", RaidDays.Friday),
        ("Sa", RaidDays.Saturday),
    };

    private static readonly ContentCategory[] DutyCategories =
    {
        ContentCategory.Savage,
        ContentCategory.Ultimate,
        ContentCategory.ExtremeFarm,
        ContentCategory.Criterion,
        ContentCategory.DeepDungeon,
        ContentCategory.Other,
    };

    private static readonly string[] ObjectiveOptions =
    {
        "None",
        "Practice",
        "Duty Completion",
        "Loot",
    };

    private void DrawCreateScreen(in PhoneContext context, Rect area)
    {
        kindMenu.Gate();
        dutyPickerMenu.Gate();
        categoryPickerMenu.Gate();
        objectiveMenu.Gate();

        var anyMenuOpen = kindMenu.Open || dutyPickerMenu.Open || categoryPickerMenu.Open || objectiveMenu.Open;

        var scale = UiScale.Current;
        var theme = ui.Theme;
        var drawList = ImGui.GetWindowDrawList();
        var headerHeight = 44f * scale;
        var headerMin = area.Min;
        var headerMax = new Vector2(area.Max.X, headerMin.Y + headerHeight);
        DrawCreateHeader(drawList, headerMin, headerMax, scale, theme, context);
        var indicatorBandHeight = Metrics.Space.Lg * scale;
        var indicatorBand = new Rect(new Vector2(area.Min.X, headerMax.Y), new Vector2(area.Max.X, headerMax.Y + indicatorBandHeight));
        DrawCreateStepIndicators(drawList, indicatorBand, scale, theme);
        var top = indicatorBand.Max.Y;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();

            switch (createStep)
            {
                case 0:
                DrawCreateStep1(width, scale, theme);
                break;
                case 1:
                    if (createKind == ListingKind.PartyFinder)
                    {
                        DrawCreatePfStep2(width, scale, theme);
                    }
                    else
                    {
                        DrawCreateStep2(width, scale, theme);
                    }
                    break;
                case 2:
                    if (createKind == ListingKind.PartyFinder)
                    {
                        DrawCreatePfStep3(width, scale, theme);
                    }
                    else
                    {
                        DrawCreateStep3(width, scale, theme);
                    }
                    break;
            }
            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
        var pickedKind = kindMenu.Draw(area, theme, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(kindMenuItems));
        if (pickedKind >= 0)
        {
            createKind = pickedKind switch
            {
                0 => ListingKind.PlayerLfg,
                1 => ListingKind.StaticLfm,
                2 => ListingKind.SingleNightFill,
                3 => ListingKind.PartyFinder,
                _ => ListingKind.PlayerLfg,
            };
        }

        var pickedDuty = dutyPickerMenu.Draw(area, theme, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(dutyPickerMenuItems));
        if (pickedDuty >= 0)
        {
            var duties = PfCatalog.GetDutiesFor(createPfDutyCategory);
            if (pickedDuty == 0)
            {
                createPfSelectedDuty = null;
            }
            else if (pickedDuty - 1 < duties.Count)
            {
                createPfSelectedDuty = duties[pickedDuty - 1];
            }
        }

        var pickedCategory = categoryPickerMenu.Draw(area, theme, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(categoryPickerMenuItems));
        if (pickedCategory >= 0 && pickedCategory < PfCatalog.Categories.Length)
        {
            createPfDutyCategory = PfCatalog.Categories[pickedCategory].Category;
            createPfSelectedDuty = null;
        }

        var pickedObjective = objectiveMenu.Draw(area, theme, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(objectiveMenuItems));
        if (pickedObjective >= 0)
        {
            createObjective = ObjectiveOptions[pickedObjective];
        }

        var sheetTitle = editingSlotIndex >= 0 ? $"Slot {editingSlotIndex + 1} Roles" : "Role Selection";
        rolePickerSheet.Draw(area, theme, sheetTitle, 0.90f, DrawRolePickerModal);
    }

    private void DrawCreateHeader(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale, PhoneTheme theme, in PhoneContext context)
    {
        var centerY = (min.Y + max.Y) * 0.5f;
        var backWidth = createStep == 0 ? 56f * scale : 32f * scale;
        var backMin = new Vector2(min.X + 12f * scale, centerY - 14f * scale);
        var backMax = new Vector2(backMin.X + backWidth, centerY + 14f * scale);
        var backHovered = UiInteract.Hover(backMin, backMax);
        if (backHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        if (createStep == 0)
        {
            AppSkin.Icon(drawList, new Vector2(backMin.X + 6f * scale, centerY), IconGlyph.Of(FontAwesomeIcon.ChevronLeft), theme.TextStrong, 0.75f);
            Typography.Draw(drawList, new Vector2(backMin.X + 18f * scale, centerY - 7f * scale), "back", theme.TextStrong, TextStyles.Subheadline);
        }
        else
        {
            AppSkin.Icon(drawList, (backMin + backMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.ChevronLeft), theme.TextStrong, 0.90f);
        }
        if (UiInteract.Click(backMin, backMax, backHovered))
        {
            if (createStep > 0)
            {
                createStep--;
            }
            else
            {
                router.Pop();
            }
        }
        var titleCenter = new Vector2((min.X + max.X) * 0.5f, centerY);
        Typography.DrawCentered(drawList, titleCenter, "New Listing", theme.TextStrong, TextStyles.Headline.Scale, FontWeight.Bold);
        var nextLabel = createStep == 2 ? "Post" : "Next";
        var nextBtnWidth = 48f * scale;
        var nextBtnHeight = 26f * scale;
        var nextMin = new Vector2(max.X - nextBtnWidth - 12f * scale, centerY - nextBtnHeight * 0.5f);
        var nextMax = new Vector2(nextMin.X + nextBtnWidth, nextMin.Y + nextBtnHeight);
        RefreshStratUrlValidity();
        var postBlocked = IsCreateStepBlocked();
        var nextHovered = !postBlocked && UiInteract.Hover(nextMin, nextMax);
        if (nextHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        var accentColor = new Vector4(0.24f, 0.55f, 0.95f, 1f);
        var nextFill = postBlocked
            ? Palette.WithAlpha(accentColor, 0.35f)
            : nextHovered ? Palette.WithAlpha(accentColor, 0.85f) : accentColor;
        var nextLabelColor = postBlocked ? new Vector4(1f, 1f, 1f, 0.55f) : new Vector4(1f, 1f, 1f, 1f);
        Squircle.Fill(drawList, nextMin, nextMax, nextBtnHeight * 0.5f, ImGui.GetColorU32(nextFill));
        Typography.DrawCentered(drawList, (nextMin + nextMax) * 0.5f, nextLabel, nextLabelColor, TextStyles.Caption1.Scale, FontWeight.Bold);
        if (UiInteract.Click(nextMin, nextMax, nextHovered))
        {
            if (createStep < 2)
            {
                createStep++;
            }
            else
            {
                SubmitNewListing(context);
            }
        }
    }

    private bool IsCreateStepBlocked()
    {
        if (createKind != ListingKind.PartyFinder)
        {
            return false;
        }

        if (createStep == 1)
        {
            return createPfDutyCategory != PfDutyCategory.None && createPfSelectedDuty == null;
        }

        if (createStep == 2)
        {
            return !createStratUrlValid || (createIsPrivate && createPasscode.Length == 0);
        }

        return false;
    }

    private void DrawCreateStepIndicators(ImDrawListPtr drawList, Rect band, float scale, PhoneTheme theme)
    {
        var pillWidth = 34f * scale;
        var pillHeight = 3.5f * scale;
        var gap = 7f * scale;
        var totalWidth = 3 * pillWidth + 2 * gap;
        var startX = band.Center.X - totalWidth * 0.5f;
        var top = band.Center.Y - pillHeight * 0.5f;
        for (var stepIndex = 0; stepIndex < 3; stepIndex++)
        {
            var pillMin = new Vector2(startX + stepIndex * (pillWidth + gap), top);
            var pillMax = pillMin + new Vector2(pillWidth, pillHeight);
            var active = stepIndex == createStep;
            var pillColor = active ? new Vector4(1f, 1f, 1f, 0.95f) : Palette.WithAlpha(theme.TextMuted, 0.30f);
            Squircle.Fill(drawList, pillMin, pillMax, pillHeight * 0.5f, ImGui.GetColorU32(pillColor));
        }
    }


    private void DrawCreateStep1(float width, float scale, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();

        Typography.Draw(ImGui.GetCursorScreenPos(), "What are you posting?", theme.TextStrong, TextStyles.Headline.Scale, FontWeight.Bold);
        ImGui.Dummy(new Vector2(0f, 20f * scale));
        DrawListingTypeCard(drawList, width, scale, theme,
            ListingKind.PartyFinder,
            FontAwesomeIcon.Users,
            "Enhanced Party Finder",
            "Live in game PF Listing, enhanced by Beacon with WTFDIG Strats, Macros, and more.");
        DrawListingTypeCard(drawList, width, scale, theme,
            ListingKind.PlayerLfg,
            FontAwesomeIcon.User,
            "Looking for Group (LFG)",
            "Single player looking for a static.");
        DrawListingTypeCard(drawList, width, scale, theme,
            ListingKind.StaticLfm,
            FontAwesomeIcon.ShieldAlt,
            "Looking for Members (LFM)",
            "Static Member / Leader looking for new members.");
        DrawListingTypeCard(drawList, width, scale, theme,
            ListingKind.SingleNightFill,
            FontAwesomeIcon.Clock,
            "Single-Night Fill",
            "Looking for a substitute for tongihts raid or later in the week");
    }

    private void DrawListingTypeCard(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme,
        ListingKind kind, FontAwesomeIcon icon, string title, string hint)
    {
        var origin = ImGui.GetCursorScreenPos();
        var pad = 12f * scale;
        var tileSide = 44f * scale;
        var textLeft = origin.X + pad + tileSide + 12f * scale;
        var textRightPad = 28f * scale;
        var hintWidth = origin.X + width - pad - textLeft - textRightPad;
        var titleHeight = Typography.Measure(title, TextStyles.Headline).Y;
        var hintTop = origin.Y + pad + titleHeight + 4f * scale;
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, hintWidth).Y;
        var height = MathF.Max(68f * scale, (hintTop - origin.Y) + hintHeight + pad);
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = 14f * scale;
        var selected = createKind == kind;
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var bg = selected
            ? Palette.WithAlpha(theme.Accent, hovered ? 0.22f : 0.16f)
            : Palette.WithAlpha(theme.GroupedCard, hovered ? 0.95f : 0.85f);
        var border = selected
            ? theme.Accent
            : Palette.WithAlpha(theme.Separator, hovered ? 0.60f : 0.35f);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(bg));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(border), selected ? 1.5f * scale : 1f * scale);

        var tileMin = new Vector2(origin.X + pad, origin.Y + pad);
        var tileMax = tileMin + new Vector2(tileSide, tileSide);
        Squircle.FillVerticalGradient(drawList, tileMin, tileMax, 10f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(theme.Accent, 0.12f), 0.92f)),
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Darken(theme.Accent, 0.22f), 0.92f)));
        Material.Sheen(drawList, tileMin, tileMax, 10f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f)),
            1f * scale, 1.2f * scale);
        AppSkin.Icon(drawList, (tileMin + tileMax) * 0.5f, IconGlyph.Of(icon), new Vector4(1f, 1f, 1f, 1f), 0.95f);

        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + pad), title, theme.TextStrong, TextStyles.Headline.Scale, FontWeight.Bold);
        Typography.DrawWrappedLeft(new Vector2(textLeft, hintTop), hint, theme.TextMuted, TextStyles.Footnote, hintWidth);

        if (selected)
        {
            var checkPos = new Vector2(max.X - pad - 6f * scale, origin.Y + pad + 10f * scale);
            AppSkin.Icon(drawList, checkPos, IconGlyph.Of(FontAwesomeIcon.CheckCircle), theme.Accent, 0.90f);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            createKind = kind;
        }
        ImGui.Dummy(new Vector2(width, height + 10f * scale));
    }

    private void DrawCreateStep2(float width, float scale, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        DrawFieldLabel("Listing Title", theme);
        var inputMin = ImGui.GetCursorScreenPos();
        var inputMax = inputMin + new Vector2(width, 42f * scale);
        Squircle.Fill(drawList, inputMin, inputMax, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, 0.85f)));
        Squircle.Stroke(drawList, inputMin, inputMax, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.40f)), 1f * scale);
        ImGui.SetCursorScreenPos(new Vector2(inputMin.X + 10f * scale, inputMin.Y + 10f * scale));
        ImGui.SetNextItemWidth(width - 20f * scale);
        ImGui.InputText("##createTitle", ref createTitle, 64);
        ImGui.SetCursorScreenPos(inputMin);
        ImGui.Dummy(new Vector2(width, 42f * scale + 14f * scale));
        DrawFieldLabel("Role", theme);
        
        var originX = ImGui.GetCursorScreenPos().X;
        var cursorX = originX;
        var startY = ImGui.GetCursorScreenPos().Y;
        var rowHeight = ChipRail.RowHeight * scale;
        var centerY = startY + ChipRail.ChipHeight * 0.5f * scale;
        var maxX = originX + width;
        for (var i = 0; i < RoleOptions.Length; i++)
        {
            var (label, role) = RoleOptions[i];
            var active = createRoles.Contains(role);
            var chipWidth = Typography.Measure(label, ChipRail.LabelStyle).X + ChipRail.ChipPadX * 2f * scale;
            if (cursorX + chipWidth > maxX && cursorX > originX)
            {
                cursorX = originX;
                centerY += rowHeight;
            }
            if (AppSkin.FlowChip(ref cursorX, centerY, 6f * scale, label, active, theme))
            {
                if (!createRoles.Add(role))
                {
                    if (createRoles.Count > 1)
                    {
                        createRoles.Remove(role);
                    }
                }
            }
        }

        var rolesTotalHeight = (centerY - startY) + ChipRail.ChipHeight * 0.5f * scale;
        ImGui.Dummy(new Vector2(width, rolesTotalHeight + 6f * scale));
        DrawFieldLabel("Info", theme);
        var descMin = ImGui.GetCursorScreenPos();
        var descHeight = 110f * scale;
        var descMax = descMin + new Vector2(width, descHeight);
        Squircle.Fill(drawList, descMin, descMax, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, 0.85f)));
        Squircle.Stroke(drawList, descMin, descMax, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.40f)), 1f * scale);
        ImGui.SetCursorScreenPos(new Vector2(descMin.X + 10f * scale, descMin.Y + 10f * scale));
        ImGui.SetNextItemWidth(width - 20f * scale);
        ImGui.InputTextMultiline("##createDesc", ref createDescription, 512, new Vector2(width - 20f * scale, descHeight - 20f * scale));
        ImGui.SetCursorScreenPos(descMin);
        ImGui.Dummy(new Vector2(width, descHeight + 14f * scale));
        var tagHeaderPos = ImGui.GetCursorScreenPos();
        Typography.Draw(tagHeaderPos, "Tags", theme.TextStrong, TextStyles.Headline.Scale, FontWeight.Bold);
        var tagLabelSize = Typography.Measure("Tags", TextStyles.Headline.Scale, FontWeight.Bold);
        Typography.Draw(new Vector2(tagHeaderPos.X + tagLabelSize.X + 8f * scale, tagHeaderPos.Y + 3f * scale), "Add a tag, press Enter", theme.TextMuted, TextStyles.Caption2);
        ImGui.Dummy(new Vector2(0f, 22f * scale));
        DrawTagsContainer(drawList, width, scale, theme);
    }

    private void DrawCreatePfStep2(float width, float scale, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var gap = 6f * scale;

        var tabCount = 3;
        var tabH = 30f * scale;
        var tabW = (width - gap * (tabCount - 1)) / tabCount;
        var origin = ImGui.GetCursorScreenPos();
        PfPartyType[] partyTypes = { PfPartyType.Normal, PfPartyType.Alliance, PfPartyType.CustomMatch };
        string[] partyLabels = { "Normal", "Alliance", "Custom Match" };
        for (var i = 0; i < tabCount; i++)
        {
            var pType = partyTypes[i];
            var tMin = new Vector2(origin.X + i * (tabW + gap), origin.Y);
            var tMax = tMin + new Vector2(tabW, tabH);
            var tActive = createPartyType == pType;
            var tHovered = UiInteract.Hover(tMin, tMax);
            if (tHovered) {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            var tBg = tActive ? theme.Accent : Palette.WithAlpha(theme.GroupedCard, tHovered ? 0.95f : 0.75f);
            Squircle.Fill(drawList, tMin, tMax, 6f * scale, ImGui.GetColorU32(tBg));
            Squircle.Stroke(drawList, tMin, tMax, 6f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, tActive ? 0f : 0.35f)), 1f * scale);
            Typography.DrawCentered(drawList, (tMin + tMax) * 0.5f, partyLabels[i], tActive ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong, TextStyles.Caption1.Scale, FontWeight.Bold);
            if (UiInteract.Click(tMin, tMax, tHovered))
            {
                createPartyType = pType;
            }
        }
        ImGui.Dummy(new Vector2(width, tabH + 16f * scale));

        var categoryLabel = PfCatalog.GetCategoryLabel(createPfDutyCategory);
        DrawDropdownTrigger(drawList, width, scale, theme, categoryLabel, () =>
        {
            categoryPickerMenuItems.Clear();
            for (var i = 0; i < PfCatalog.Categories.Length; i++)
            {
                var catInfo = PfCatalog.Categories[i];
                categoryPickerMenuItems.Add(new DropdownMenu.Item(catInfo.Label, Selected: createPfDutyCategory == catInfo.Category));
            }
            var p = ImGui.GetItemRectMin();
            categoryPickerMenu.Toggle("recruit_create_cat_pick", new Rect(p, p + ImGui.GetItemRectSize()));
        });

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        var dutyLabel = createPfSelectedDuty != null ? createPfSelectedDuty.Name : "Select Duty (None)";
        DrawDropdownTrigger(drawList, width, scale, theme, dutyLabel, () =>
        {
            dutyPickerMenuItems.Clear();
            dutyPickerMenuItems.Add(new DropdownMenu.Item("None", Selected: createPfSelectedDuty == null));
            var duties = PfCatalog.GetDutiesFor(createPfDutyCategory);
            for (var i = 0; i < duties.Count; i++)
            {
                var d = duties[i];
                dutyPickerMenuItems.Add(new DropdownMenu.Item(d.Name, Selected: createPfSelectedDuty?.Id == d.Id && createPfSelectedDuty?.Name == d.Name));
            }
            var p = ImGui.GetItemRectMin();
            dutyPickerMenu.Toggle("recruit_create_duty_pick", new Rect(p, p + ImGui.GetItemRectSize()));
        });
        if (createPfDutyCategory != PfDutyCategory.None && createPfSelectedDuty == null)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            var hintHeight = Typography.DrawWrappedLeft(ImGui.GetCursorScreenPos(), "Select a duty to continue.",
                theme.TextMuted, TextStyles.Caption1, width);
            ImGui.Dummy(new Vector2(width, hintHeight));
        }
        ImGui.Dummy(new Vector2(0f, 16f * scale));

        DrawFieldLabel("Objective", theme);
        DrawDropdownTrigger(drawList, width, scale, theme, createObjective, () =>
        {
            objectiveMenuItems.Clear();
            for (var i = 0; i < ObjectiveOptions.Length; i++)
            {
                var opt = ObjectiveOptions[i];
                objectiveMenuItems.Add(new DropdownMenu.Item(opt, Selected: createObjective == opt));
            }
            var p = ImGui.GetItemRectMin();
            objectiveMenu.Toggle("recruit_create_obj_pick", new Rect(p, p + ImGui.GetItemRectSize()));
        });
        ImGui.Dummy(new Vector2(0f, 16f * scale));

        var commentLabelPos = ImGui.GetCursorScreenPos();
        Typography.Draw(commentLabelPos, "Comment", theme.TextStrong, TextStyles.Headline.Scale, FontWeight.Bold);
        var counterText = $"{createComment.Length}/192";
        var counterSize = Typography.Measure(counterText, TextStyles.Caption1);
        Typography.Draw(new Vector2(commentLabelPos.X + width - counterSize.X, commentLabelPos.Y + 2f * scale),
            counterText, createComment.Length >= 180 ? theme.Accent : theme.TextMuted, TextStyles.Caption1);
        ImGui.Dummy(new Vector2(0f, 22f * scale));
        var commentMin = ImGui.GetCursorScreenPos();
        var commentHeight = 68f * scale;
        var commentMax = commentMin + new Vector2(width, commentHeight);
        Squircle.Fill(drawList, commentMin, commentMax, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, 0.85f)));
        Squircle.Stroke(drawList, commentMin, commentMax, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.40f)), 1f * scale);
        ImGui.SetCursorScreenPos(new Vector2(commentMin.X + 10f * scale, commentMin.Y + 10f * scale));
        ImGui.SetNextItemWidth(width - 20f * scale);
        ImGui.InputTextMultiline("##createPfComment", ref createComment, 192, new Vector2(width - 20f * scale, commentHeight - 20f * scale));
        ImGui.SetCursorScreenPos(commentMin);
        ImGui.Dummy(new Vector2(width, commentHeight + 16f * scale));

        DrawFieldLabel("Roles", theme);

        var slotCount = 8;
        var slotGap = 5f * scale;
        var slotSide = (width - slotGap * (slotCount - 1)) / slotCount;
        var slotOrigin = ImGui.GetCursorScreenPos();
        var leaderJobId = Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;

        for (var s = 0; s < slotCount; s++)
        {
            var sMin = new Vector2(slotOrigin.X + s * (slotSide + slotGap), slotOrigin.Y);
            var sMax = sMin + new Vector2(slotSide, slotSide);
            var isLeader = s == 0;
            var hovered = UiInteract.Hover(sMin, sMax);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var slotConfig = createPfSlots[s];
            var slotColor = PfCatalog.GetSlotColor(slotConfig, isLeader, leaderJobId);
            Squircle.Fill(drawList, sMin, sMax, 6f * scale, ImGui.GetColorU32(slotColor));
            Squircle.Stroke(drawList, sMin, sMax, 6f * scale, ImGui.GetColorU32(hovered ? theme.TextStrong : Palette.WithAlpha(theme.Separator, 0.45f)), 1f * scale);
            if (isLeader)
            {
                var inset = 3f * scale;
                var drewIcon = leaderJobId != 0 && GameIconTile.Draw(drawList, Plugin.TextureProvider,
                GameData.JobIconId(leaderJobId), sMin + new Vector2(inset), sMax - new Vector2(inset), 4f * scale, scale);
                if (!drewIcon)
                {
                    AppSkin.Icon(drawList, (sMin + sMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.Check), new Vector4(1f, 1f, 1f, 1f), 0.70f);
                }
            }
            else
            {
                PfCatalog.DrawSlotGlyph(drawList, sMin, sMax, slotConfig, scale);
            }

            if (UiInteract.Click(sMin, sMax, hovered))
            {
                editingSlotIndex = s;
                tempSlotJobs.Clear();
                foreach (var j in slotConfig.AllowedJobs)
                {
                    tempSlotJobs.Add(j);
                }
                rolePickerSheet.Open();
            }
        }

        ImGui.Dummy(new Vector2(width, slotSide + 12f * scale));
        if (createPartyType == PfPartyType.Normal)
        {
            DrawHighEndPresetButton(width, scale);
        }

        var prevRemove = createRemoveRoleRestrictions;
        DrawPfCheckbox(drawList, width, scale, theme, "Remove role restrictions for all remaining openings.", ref createRemoveRoleRestrictions);
        if (createRemoveRoleRestrictions != prevRemove && createRemoveRoleRestrictions)
        {
            for (var i = 1; i < 8; i++)
            {
                createPfSlots[i].SetJobs(PfCatalog.AllCombatJobIds);
            }
        }

        var prevUnselect = createUnselectClasses;
        DrawPfCheckbox(drawList, width, scale, theme, "Unselect Classes", ref createUnselectClasses);
        if (createUnselectClasses != prevUnselect && createUnselectClasses)
        {
            RemoveClassesFromSlots();
        }

        DrawPfCheckbox(drawList, width, scale, theme, "One Player per Job", ref createOnePlayerPerJob);
    }

    private void DrawHighEndPresetButton(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = Button.Height(ButtonSize.Small) * scale;
        var rect = new Rect(origin, origin + new Vector2(width, height));
        if (Button.Draw(rect, "HighEndPreset", ControlInk.From(ui), ButtonStyle.Tinted))
        {
            ApplyHighEndComposition();
        }

        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void ApplyHighEndComposition()
    {
        var leaderJobId = Plugin.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        PfCatalog.ApplyComposition(createPfSlots, PfCatalog.HighEndComposition, leaderJobId);
        createRemoveRoleRestrictions = false;
        if (createUnselectClasses)
        {
            RemoveClassesFromSlots();
        }
    }

    private void RemoveClassesFromSlots()
    {
        for (var slotIndex = 1; slotIndex < createPfSlots.Length; slotIndex++)
        {
            for (var classIndex = 0; classIndex < PfCatalog.AllClassIds.Length; classIndex++)
            {
                createPfSlots[slotIndex].AllowedJobs.Remove(PfCatalog.AllClassIds[classIndex]);
            }
        }
    }

    private void DrawCreatePfStep3(float width, float scale, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var ink = ControlInk.From(ui);
        DrawPfGameSettings(drawList, width, scale, theme);
        
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxl * scale));
        Typography.Draw(drawList, ImGui.GetCursorScreenPos(), "Aetherphone Extras", theme.TextStrong, TextStyles.Title3.Scale, FontWeight.Bold);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxl * scale));

        var noticeHeight = Typography.DrawWrappedLeft(ImGui.GetCursorScreenPos(),
            "These extras only show for players using Aetherphone. They are not added to the in-game listing.",
            theme.TextMuted, TextStyles.Footnote, width);
        ImGui.Dummy(new Vector2(width, noticeHeight + Metrics.Space.Lg * scale));

        DrawFieldLabel("WTFDIG Strats", theme);
        var fieldOrigin = ImGui.GetCursorScreenPos();
        var fieldHeight = SearchBar.HeightUnits * scale;
        var field = new Rect(fieldOrigin, fieldOrigin + new Vector2(width, fieldHeight));
        SearchBar.Surface(drawList, field, ink);
        GlassField.Text(field, "##createStratUrl", "https://wtfdig.info/...", ref createStratUrl, theme, scale,
            StratUrlMaxLength, false, ImGuiInputTextFlags.None);
        ImGui.SetCursorScreenPos(fieldOrigin);
        ImGui.Dummy(new Vector2(width, fieldHeight));

        RefreshStratUrlValidity();
        if (!createStratUrlValid)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            var errorHeight = Typography.DrawWrappedLeft(ImGui.GetCursorScreenPos(),
                "Only wtfdig.info links are supported.", theme.Danger, TextStyles.Caption1, width);
            ImGui.Dummy(new Vector2(width, errorHeight));
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

        var labelPosition = ImGui.GetCursorScreenPos();
        Typography.Draw(drawList, labelPosition, "Additional Notes", theme.TextStrong, TextStyles.Headline.Scale, FontWeight.Bold);
        RefreshNotesCounter();
        var counterSize = Typography.Measure(notesCounterText, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(labelPosition.X + width - counterSize.X, labelPosition.Y + 2f * scale),
            notesCounterText, theme.TextMuted, TextStyles.Caption1);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));

        var notesOrigin = ImGui.GetCursorScreenPos();
        var notesHeight = Metrics.Size.FieldMultiline * scale;
        var notesMax = notesOrigin + new Vector2(width, notesHeight);
        Squircle.Fill(drawList, notesOrigin, notesMax, Metrics.Radius.Field * scale,
            ImGui.GetColorU32(Surfaces.Fill(ink, FillLevel.Tertiary)));
        var inset = Metrics.Space.Sm * scale;
        var notesSize = new Vector2(width - inset * 2f, notesHeight - inset * 2f);
        ImGui.SetCursorScreenPos(notesOrigin + new Vector2(inset));
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.FrameBgHovered, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.FrameBgActive, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            var wrapWidth = notesSize.X - ImGui.GetStyle().FramePadding.X * 2f - Metrics.Space.Xxs * scale;
            SoftWrapField.Multiline("##createPfNotes", ref createExtendedNotes, ExtendedNotesMaxLength, notesSize, wrapWidth);
        }

        ImGui.SetCursorScreenPos(notesOrigin);
        ImGui.Dummy(new Vector2(width, notesHeight));
    }

    private void DrawPfGameSettings(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme)
    {
        Typography.Draw(drawList, ImGui.GetCursorScreenPos(), "Party Settings", theme.TextStrong, TextStyles.Title3.Scale, FontWeight.Bold);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxl * scale));

        DrawFieldLabel("Completion Status", theme);
        createCompletionStatus = (PfCompletionStatus)DrawSegmentedRow(drawList, width, scale, theme,
            CompletionLabels, (int)createCompletionStatus);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

        DrawFieldLabel("Loot Rules", theme);
        createLootRule = (PfLootRule)DrawSegmentedRow(drawList, width, scale, theme, LootRuleLabels, (int)createLootRule);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

        DrawFieldLabel("Languages", theme);
        DrawLanguageRow(drawList, width, scale, theme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));

        DrawFieldLabel("Average Item Level", theme);
        DrawDigitField(drawList, width, scale, theme, "##createMinIlvl", "No requirement", ref createMinIlvlText, 4);
        createMinIlvl = int.TryParse(createMinIlvlText, out var itemLevel) ? Math.Clamp(itemLevel, 0, MaxItemLevel) : 0;
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));

        DrawPfCheckbox(drawList, width, scale, theme, "Limit to Home World", ref createLimitToWorldServer);
        DrawPfCheckbox(drawList, width, scale, theme, "Unrestricted Party", ref createUnrestrictedParty);
        DrawPfCheckbox(drawList, width, scale, theme, "Minimum IL", ref createMinimumIl);
        DrawPfCheckbox(drawList, width, scale, theme, "Silence Echo", ref createSilenceEcho);
        DrawPfCheckbox(drawList, width, scale, theme, "Private (Passcode Required)", ref createIsPrivate);
        if (createIsPrivate)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            DrawDigitField(drawList, width, scale, theme, "##createPasscode", "4-digit passcode", ref createPasscode, 5);
            if (createPasscode.Length == 0)
            {
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
                var hintHeight = Typography.DrawWrappedLeft(ImGui.GetCursorScreenPos(),
                    "Enter a passcode to post a private listing.", theme.TextMuted, TextStyles.Caption1, width);
                ImGui.Dummy(new Vector2(width, hintHeight));
            }
        }
    }

    private void RefreshStratUrlValidity()
    {
        if (string.Equals(createStratUrl, lastCheckedStratUrl, StringComparison.Ordinal))
        {
            return;
        }

        lastCheckedStratUrl = createStratUrl;
        createStratUrlValid = string.IsNullOrWhiteSpace(createStratUrl) || StratLinks.TryNormalizeWtfdig(createStratUrl, out _);
    }

    private void RefreshNotesCounter()
    {
        if (createExtendedNotes.Length == notesCounterLength)
        {
            return;
        }

        notesCounterLength = createExtendedNotes.Length;
        notesCounterText = $"{notesCounterLength}/{ExtendedNotesMaxLength}";
    }

    private int DrawSegmentedRow(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme,
        string[] labels, int selectedIndex)
    {
        var gap = Metrics.Space.Xs * scale;
        var height = 30f * scale;
        var segmentWidth = (width - gap * (labels.Length - 1)) / labels.Length;
        var origin = ImGui.GetCursorScreenPos();
        var result = selectedIndex;
        for (var index = 0; index < labels.Length; index++)
        {
            var segmentMin = new Vector2(origin.X + index * (segmentWidth + gap), origin.Y);
            var segmentMax = segmentMin + new Vector2(segmentWidth, height);
            var label = Typography.FitText(labels[index], segmentWidth - Metrics.Space.Sm * scale, TextStyles.Caption1);
            if (DrawSegment(drawList, segmentMin, segmentMax, label, index == selectedIndex, scale, theme))
            {
                result = index;
            }
        }

        ImGui.Dummy(new Vector2(width, height));
        return result;
    }

    private void DrawLanguageRow(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme)
    {
        var gap = Metrics.Space.Xs * scale;
        var height = 30f * scale;
        var segmentWidth = (width - gap * (LanguageOptions.Length - 1)) / LanguageOptions.Length;
        var origin = ImGui.GetCursorScreenPos();
        for (var index = 0; index < LanguageOptions.Length; index++)
        {
            var (label, flag) = LanguageOptions[index];
            var segmentMin = new Vector2(origin.X + index * (segmentWidth + gap), origin.Y);
            var segmentMax = segmentMin + new Vector2(segmentWidth, height);
            if (DrawSegment(drawList, segmentMin, segmentMax, label, (createLanguages & flag) != 0, scale, theme))
            {
                var next = createLanguages ^ flag;
                if (next != PfLanguageFlags.None)
                {
                    createLanguages = next;
                }
            }
        }

        ImGui.Dummy(new Vector2(width, height));
    }

    private static bool DrawSegment(ImDrawListPtr drawList, Vector2 segmentMin, Vector2 segmentMax,
        string label, bool active, float scale, PhoneTheme theme)
    {
        var hovered = UiInteract.Hover(segmentMin, segmentMax);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var fill = active ? theme.Accent : Palette.WithAlpha(theme.GroupedCard, hovered ? 0.95f : 0.75f);
        Squircle.Fill(drawList, segmentMin, segmentMax, 6f * scale, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, segmentMin, segmentMax, 6f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, active ? 0f : 0.35f)), 1f * scale);
        Typography.DrawCentered(drawList, (segmentMin + segmentMax) * 0.5f, label,
            active ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong, TextStyles.Caption1.Scale, FontWeight.Bold);
        return UiInteract.Click(segmentMin, segmentMax, hovered);
    }

    private void DrawDigitField(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme,
        string imguiId, string hint, ref string text, int maxLength)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = SearchBar.HeightUnits * scale;
        var field = new Rect(origin, origin + new Vector2(width, height));
        SearchBar.Surface(drawList, field, ControlInk.From(ui));
        GlassField.Text(field, imguiId, hint, ref text, theme, scale, maxLength, false, ImGuiInputTextFlags.CharsDecimal);
        for (var index = 0; index < text.Length; index++)
        {
            if (!char.IsAsciiDigit(text[index]))
            {
                text = string.Concat(text.AsSpan(0, index), text.AsSpan(index + 1));
                index--;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }


    private void DrawPfCheckbox(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme, string label, ref bool value)
    {
        var origin = ImGui.GetCursorScreenPos();
        var boxSize = 18f * scale;
        var rowHeight = 28f * scale;
        var hitMax = new Vector2(origin.X + width, origin.Y + rowHeight);
        var hovered = UiInteract.Hover(origin, hitMax);
        if (hovered) 
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var boxMin = new Vector2(origin.X, origin.Y + (rowHeight - boxSize) * 0.5f);
        var boxMax = boxMin + new Vector2(boxSize, boxSize);
        Squircle.Fill(drawList, boxMin, boxMax, 4f * scale, ImGui.GetColorU32(value ? theme.Accent : Palette.WithAlpha(theme.GroupedCard, 0.9f)));
        Squircle.Stroke(drawList, boxMin, boxMax, 4f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, value ? 0f : 0.5f)), 1f * scale);
        if (value)
        {
            AppSkin.Icon(drawList, (boxMin + boxMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.Check), new Vector4(1f, 1f, 1f, 1f), 0.70f);
        }
        Typography.Draw(drawList, new Vector2(boxMax.X + 10f * scale, origin.Y + (rowHeight - Typography.Measure(label, TextStyles.Caption1).Y) * 0.5f),
            label, theme.TextStrong, TextStyles.Caption1);
        if (UiInteract.Click(origin, hitMax, hovered))
        {
            value = !value;
        }
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void DrawTagsContainer(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme)
    {
        var boxMin = ImGui.GetCursorScreenPos();
        var minHeight = 44f * scale;
        var pad = 8f * scale;
        var curX = boxMin.X + pad;
        var curY = boxMin.Y + pad;
        var rowH = 26f * scale;
        var chipGap = 6f * scale;
        var maxRight = boxMin.X + width - pad;
        int? toRemove = null;

        for (var i = 0; i < createTags.Count; i++)
        {
            var tagText = $"#{createTags[i]}";
            var textW = Typography.Measure(tagText, TextStyles.Caption1).X;
            var chipW = textW + 24f * scale;
            if (curX + chipW > maxRight && curX > boxMin.X + pad)
            {
                curX = boxMin.X + pad;
                curY += rowH + chipGap;
            }
            var chipMin = new Vector2(curX, curY);
            var chipMax = chipMin + new Vector2(chipW, rowH);
            Squircle.Fill(drawList, chipMin, chipMax, rowH * 0.5f, ImGui.GetColorU32(new Vector4(0.24f, 0.55f, 0.95f, 0.85f)));
            Typography.Draw(drawList, new Vector2(chipMin.X + 8f * scale, chipMin.Y + 5f * scale), tagText, new Vector4(1f, 1f, 1f, 1f), TextStyles.Caption1);
            var closeCenter = new Vector2(chipMax.X - 9f * scale, (chipMin.Y + chipMax.Y) * 0.5f);
            AppSkin.Icon(drawList, closeCenter, PhoneIcons.X, new Vector4(1f, 1f, 1f, 0.80f), 0.55f);
            var closeHitMin = closeCenter - new Vector2(8f * scale);
            var closeHitMax = closeCenter + new Vector2(8f * scale);
            if (UiInteract.Click(closeHitMin, closeHitMax, UiInteract.Hover(closeHitMin, closeHitMax)))
            {
                toRemove = i;
            }
            curX += chipW + chipGap;
        }

        if (toRemove.HasValue)
        {
            createTags.RemoveAt(toRemove.Value);
        }
        var totalH = MathF.Max(minHeight, (curY + rowH + pad) - boxMin.Y);
        var boxMax = boxMin + new Vector2(width, totalH);
        Squircle.Stroke(drawList, boxMin, boxMax, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.40f)), 1f * scale);
        ImGui.SetCursorScreenPos(new Vector2(curX, curY));
        ImGui.SetNextItemWidth(MathF.Max(80f * scale, maxRight - curX));

        if (ImGui.InputText("##newTagInput", ref newTagBuffer, 24, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            var cleaned = newTagBuffer.Trim().TrimStart('#');
            if (!string.IsNullOrWhiteSpace(cleaned) && !createTags.Contains(cleaned))
            {
                createTags.Add(cleaned);
                newTagBuffer = string.Empty;
            }
        }
        ImGui.SetCursorScreenPos(boxMin);
        ImGui.Dummy(new Vector2(width, totalH + 6f * scale));
    }

    private void DrawCreateStep3(float width, float scale, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        DrawFieldLabel("Availability", theme);
        Typography.Draw(ImGui.GetCursorScreenPos(), "Some helper copy about when the raiders can raid and stuff.", theme.TextMuted, TextStyles.Caption2);
        ImGui.Dummy(new Vector2(0f, 16f * scale));
        Typography.Draw(ImGui.GetCursorScreenPos(), "Your clock is UTC-4. Everyone sees the schedule in their own clock, with the UTC time underneath.", Palette.WithAlpha(theme.TextMuted, 0.75f), TextStyles.Caption2);
        ImGui.Dummy(new Vector2(0f, 22f * scale));
        DrawFieldLabel("Days", theme);

        var dayBtnGap = 6f * scale;
        var dayBtnWidth = (width - dayBtnGap * 6f) / 7f;
        var dayBtnHeight = 32f * scale;
        var daysOrigin = ImGui.GetCursorScreenPos();
        for (var i = 0; i < DayButtons.Length; i++)
        {
            var (label, day) = DayButtons[i];
            var active = createDays.HasFlag(day);
            var dMin = new Vector2(daysOrigin.X + i * (dayBtnWidth + dayBtnGap), daysOrigin.Y);
            var dMax = dMin + new Vector2(dayBtnWidth, dayBtnHeight);
            var dHovered = UiInteract.Hover(dMin, dMax);
            if (dHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            var bg = active
                ? new Vector4(0.24f, 0.55f, 0.95f, 1f)
                : Palette.WithAlpha(theme.GroupedCard, dHovered ? 0.95f : 0.70f);
            Squircle.Fill(drawList, dMin, dMax, 8f * scale, ImGui.GetColorU32(bg));
            Squircle.Stroke(drawList, dMin, dMax, 8f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, active ? 0f : 0.35f)), 1f * scale);
            var textCol = active ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong;
            Typography.DrawCentered(drawList, (dMin + dMax) * 0.5f, label, textCol, TextStyles.Caption1.Scale, FontWeight.Bold);
            if (UiInteract.Click(dMin, dMax, dHovered))
            {
                createDays ^= day;
                if (createDays == RaidDays.None)
                {
                    createDays = day;
                }
            }
        }

        ImGui.Dummy(new Vector2(width, dayBtnHeight + 18f * scale));
        DrawFieldLabel("Start Time", theme);
        DrawTimeStepper(drawList, width, scale, theme, ref createStartHour12, ref createStartMinute, ref createStartIsPm, "start");
        ImGui.Dummy(new Vector2(0f, 16f * scale));
        DrawFieldLabel("End Time", theme);
        DrawTimeStepper(drawList, width, scale, theme, ref createEndHour12, ref createEndMinute, ref createEndIsPm, "end");
    }

    private void DrawTimeStepper(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme,
        ref int hour, ref int minute, ref bool isPm, string id)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = 36f * scale;
        var gap = 8f * scale;
        var colWidth = (width - gap * 2f) / 3f;
        var hourDelta = DrawChevronBox(drawList, new Vector2(origin.X, origin.Y), new Vector2(origin.X + colWidth, origin.Y + height),
            hour.ToString(), scale, theme);
        if (hourDelta < 0)
        {
            hour = hour <= 1 ? 12 : hour - 1;
        }
        else if (hourDelta > 0)
        {
            hour = hour >= 12 ? 1 : hour + 1;
        }
        var minX = origin.X + colWidth + gap;
        var minDelta = DrawChevronBox(drawList, new Vector2(minX, origin.Y), new Vector2(minX + colWidth, origin.Y + height),
            minute.ToString("D2"), scale, theme);
        if (minDelta < 0)
        {
            minute = (minute - 15 + 60) % 60;
        }
        else if (minDelta > 0)
        {
            minute = (minute + 15) % 60;
        }
        var amPmX = origin.X + (colWidth + gap) * 2f;
        var amPmMin = new Vector2(amPmX, origin.Y);
        var amPmMax = amPmMin + new Vector2(colWidth, height);
        Squircle.Fill(drawList, amPmMin, amPmMax, 8f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, 0.85f)));
        Squircle.Stroke(drawList, amPmMin, amPmMax, 8f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.35f)), 1f * scale);
        var halfW = colWidth * 0.5f;
        var amMin = amPmMin;
        var amMax = new Vector2(amPmMin.X + halfW, amPmMax.Y);
        var pmMin = new Vector2(amPmMin.X + halfW, amPmMin.Y);
        var pmMax = amPmMax;
        var activeBg = new Vector4(0.24f, 0.55f, 0.95f, 1f);
        if (!isPm)
        {
            Squircle.Fill(drawList, amMin, amMax, 8f * scale, ImGui.GetColorU32(activeBg));
        }
        else
        {
            Squircle.Fill(drawList, pmMin, pmMax, 8f * scale, ImGui.GetColorU32(activeBg));
        }
        Typography.DrawCentered(drawList, (amMin + amMax) * 0.5f, "AM", !isPm ? new Vector4(1f, 1f, 1f, 1f) : theme.TextMuted, TextStyles.Caption1.Scale, FontWeight.Bold);
        Typography.DrawCentered(drawList, (pmMin + pmMax) * 0.5f, "PM", isPm ? new Vector4(1f, 1f, 1f, 1f) : theme.TextMuted, TextStyles.Caption1.Scale, FontWeight.Bold);
        if (UiInteract.Click(amMin, amMax, UiInteract.Hover(amMin, amMax)))
        {
            isPm = false;
        }
        if (UiInteract.Click(pmMin, pmMax, UiInteract.Hover(pmMin, pmMax)))
        {
            isPm = true;
        }
        ImGui.Dummy(new Vector2(width, height));
    }

    private int DrawChevronBox(ImDrawListPtr drawList, Vector2 min, Vector2 max, string val, float scale, PhoneTheme theme)
    {
        Squircle.Fill(drawList, min, max, 8f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, 0.85f)));
        Squircle.Stroke(drawList, min, max, 8f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.35f)), 1f * scale);
        var arrowW = 28f * scale;
        var leftArrowMin = min;
        var leftArrowMax = new Vector2(min.X + arrowW, max.Y);
        var rightArrowMin = new Vector2(max.X - arrowW, min.Y);
        var rightArrowMax = max;
        AppSkin.Icon(drawList, (leftArrowMin + leftArrowMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.ChevronLeft), theme.TextMuted, 0.65f);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, val, theme.TextStrong, TextStyles.Headline.Scale, FontWeight.Bold);
        AppSkin.Icon(drawList, (rightArrowMin + rightArrowMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.ChevronRight), theme.TextMuted, 0.65f);
        if (UiInteract.Click(leftArrowMin, leftArrowMax, UiInteract.Hover(leftArrowMin, leftArrowMax)))
        {
            return -1;
        }
        if (UiInteract.Click(rightArrowMin, rightArrowMax, UiInteract.Hover(rightArrowMin, rightArrowMax)))
        {
            return 1;
        }
        return 0;
    }

    private void DrawFieldLabel(string text, PhoneTheme theme)
    {
        Typography.Draw(ImGui.GetCursorScreenPos(), text, theme.TextStrong, TextStyles.Headline.Scale, FontWeight.Bold);
        ImGui.Dummy(new Vector2(0f, 18f * UiScale.Current));
    }

    private void DrawDropdownTrigger(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme, string label, Action onClick)
    {
        var min = ImGui.GetCursorScreenPos();
        var height = 38f * scale;
        var max = min + new Vector2(width, height);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        Squircle.Fill(drawList, min, max, 8f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, hovered ? 0.95f : 0.85f)));
        Squircle.Stroke(drawList, min, max, 8f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.35f)), 1f * scale);
        Typography.Draw(drawList, new Vector2(min.X + 12f * scale, min.Y + (height - Typography.Measure(label, TextStyles.Subheadline).Y) * 0.5f), label, theme.TextStrong, TextStyles.Subheadline);
        AppSkin.Icon(drawList, new Vector2(max.X - 14f * scale, (min.Y + max.Y) * 0.5f), IconGlyph.Of(FontAwesomeIcon.ChevronDown), theme.TextMuted, 0.65f);
        if (UiInteract.Click(min, max, hovered))
        {
            onClick();
        }
        ImGui.Dummy(new Vector2(width, height));
    }

    private void SubmitNewListing(in PhoneContext context)
    {
        if (createKind == ListingKind.PartyFinder)
        {
            SubmitPartyFinderListing();
            return;
        }

        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        var authorName = localPlayer?.Name.ToString() ?? string.Empty;
        var worldName = localPlayer?.HomeWorld.ValueNullable?.Name.ToString() ?? string.Empty;
        var selectedDuty = createPfSelectedDuty != null
            ? new DutyInfo(createPfSelectedDuty.Name, ContentCategory.Other)
            : new DutyInfo("None", ContentCategory.Other);
        var startMinuteOfDay = ((createStartHour12 % 12) + (createStartIsPm ? 12 : 0)) * 60 + createStartMinute;
        var endMinuteOfDay = ((createEndHour12 % 12) + (createEndIsPm ? 12 : 0)) * 60 + createEndMinute;
        var playstyle = createMindsets.Contains("Hardcore")
            ? StaticCategory.Hardcore
            : createMindsets.Contains("Casual")
                ? StaticCategory.Casual
                : StaticCategory.MidCore;
        var listing = new RecruitListing(
            Guid.NewGuid().ToString("N"),
            string.IsNullOrWhiteSpace(createTitle) ? string.Empty : createTitle.Trim(),
            createDescription.Trim(),
            createKind,
            selectedDuty,
            playstyle,
            createDays,
            startMinuteOfDay,
            endMinuteOfDay,
            RaidTimezone.EST,
            new List<RaidRole>(createRoles),
            new List<string>(createTags),
            authorName,
            worldName,
            DateTime.UtcNow
        );

        store.Add(listing);
        ResetCreateForm();
        router.Pop();
    }

    private PfDraft BuildPfDraft()
    {
        var masks = new ulong[createPfSlots.Length];
        for (var slotIndex = 0; slotIndex < createPfSlots.Length; slotIndex++)
        {
            masks[slotIndex] = PfCatalog.JobMaskFor(createPfSlots[slotIndex].AllowedJobs);
        }

        var objective = createObjective switch
        {
            "Practice" => ObjectiveFlags.Practice,
            "Duty Completion" => ObjectiveFlags.DutyCompletion,
            "Loot" => ObjectiveFlags.Loot,
            _ => ObjectiveFlags.None,
        };
        ushort? passcode = createIsPrivate && ushort.TryParse(createPasscode, out var code) && code <= 9999 ? code : null;

        return new PfDraft(
            createPfDutyCategory,
            createPfSelectedDuty?.Id ?? 0,
            objective,
            createCompletionStatus,
            createLootRule,
            createLanguages,
            (ushort)createMinIlvl,
            createOnePlayerPerJob,
            createLimitToWorldServer,
            createUnrestrictedParty,
            createMinimumIl,
            createSilenceEcho,
            passcode,
            createComment.Trim(),
            masks);
    }

    private void SubmitPartyFinderListing()
    {
        var stratUrl = StratLinks.TryNormalizeWtfdig(createStratUrl, out var normalizedStratUrl) ? normalizedStratUrl : null;
        var extendedNotes = string.IsNullOrWhiteSpace(createExtendedNotes) ? null : createExtendedNotes.Trim();
        var dutyId = createPfSelectedDuty?.Id ?? 0;
        var contentId = InventoryReader.ReadLocalContentId();
        var draft = BuildPfDraft();
        var started = PartyFinderWriter.Begin(draft, contentId,
            () => store.PublishPfExtras(contentId, dutyId, stratUrl, extendedNotes));

        if (started != PfPostStart.Started)
        {
            ShellToast.Show(started switch
            {
                PfPostStart.Busy => "You can't recruit right now.",
                _ => "Log in to a character to post a listing.",
            });
            return;
        }

        ShellToast.Show("Press Recruit Members in the Party Finder to post.");
        ResetCreateForm();
        router.Pop();
    }

    private void ResetCreateForm()
    {
        createStep = 0;
        createTitle = string.Empty;
        createDescription = string.Empty;
        createPfSelectedDuty = null;
        createComment = string.Empty;
        createStratUrl = string.Empty;
        createExtendedNotes = string.Empty;
        createIsPrivate = false;
        createPasscode = string.Empty;
        createMinIlvlText = string.Empty;
    }

    private void DrawRolePickerModal(Rect content)
    {
        if (editingSlotIndex < 0 || editingSlotIndex >= createPfSlots.Length)
        {
            return;
        }
        var scale = UiScale.Current;
        var theme = ui.Theme;
        var drawList = ImGui.GetWindowDrawList();

        ImGui.SetCursorScreenPos(content.Min);

        var presetH = 26f * scale;
        var chipGap = 5f * scale;
        var curX = content.Min.X;
        var curY = content.Min.Y;

        DrawRolePresetChip(ref curX, curY, presetH, chipGap, "Tank", PfCatalog.TankJobIds, new Vector4(0.24f, 0.55f, 0.95f, 1f), scale, theme);
        DrawRolePresetChip(ref curX, curY, presetH, chipGap, "Pure HLR", PfCatalog.PureHealerJobIds, new Vector4(0.25f, 0.78f, 0.45f, 1f), scale, theme);
        DrawRolePresetChip(ref curX, curY, presetH, chipGap, "Barrier HLR", PfCatalog.BarrierHealerJobIds, new Vector4(0.25f, 0.78f, 0.45f, 1f), scale, theme);
        DrawRolePresetChip(ref curX, curY, presetH, chipGap, "All HLR", PfCatalog.AllHealerJobIds, new Vector4(0.25f, 0.78f, 0.45f, 1f), scale, theme);
        curX = content.Min.X;
        curY += presetH + 6f * scale;
        DrawRolePresetChip(ref curX, curY, presetH, chipGap, "Melee", PfCatalog.MeleeJobIds, new Vector4(0.92f, 0.32f, 0.32f, 1f), scale, theme);
        DrawRolePresetChip(ref curX, curY, presetH, chipGap, "Phys RNG", PfCatalog.PhysRangedJobIds, new Vector4(0.92f, 0.32f, 0.32f, 1f), scale, theme);
        DrawRolePresetChip(ref curX, curY, presetH, chipGap, "Caster", PfCatalog.CasterJobIds, new Vector4(0.92f, 0.32f, 0.32f, 1f), scale, theme);
        DrawRolePresetChip(ref curX, curY, presetH, chipGap, "All DPS", PfCatalog.AllDpsJobIds, new Vector4(0.92f, 0.32f, 0.32f, 1f), scale, theme);
        DrawRolePresetChip(ref curX, curY, presetH, chipGap, "Free", PfCatalog.AllCombatJobIds, new Vector4(0.60f, 0.60f, 0.65f, 1f), scale, theme);
        var presetsTotalHeight = (presetH * 2) + 6f * scale + 12f * scale;
        var scrollStartY = content.Min.Y + presetsTotalHeight;

        var bottomBtnH = 34f * scale;
        var bottomGap = 6f * scale;
        var bottomBarY = content.Max.Y - bottomBtnH - bottomGap;

        var scrollRect = new Rect(new Vector2(content.Min.X, scrollStartY), new Vector2(content.Max.X, bottomBarY - 8f * scale));
        ImGui.SetCursorScreenPos(scrollRect.Min);
        using (AppSurface.Begin(scrollRect))
        {
            var w = ScrollLayout.StableContentWidth();
            DrawJobCategorySection("Tank", PfCatalog.Tanks, PfCatalog.TankClasses, w, scale, theme);
            DrawJobCategorySection("Pure Healer", PfCatalog.PureHealers, PfCatalog.HealerClasses, w, scale, theme);
            DrawJobCategorySection("Barrier Healer", PfCatalog.BarrierHealers, System.Array.Empty<PfJobItem>(), w, scale, theme);
            DrawJobCategorySection("Melee DPS", PfCatalog.MeleeDps, PfCatalog.MeleeClasses, w, scale, theme);
            DrawJobCategorySection("Physical Ranged DPS", PfCatalog.PhysRangedDps, PfCatalog.PhysRangedClasses, w, scale, theme);
            DrawJobCategorySection("Magical Ranged DPS", PfCatalog.CasterDps, PfCatalog.CasterClasses, w, scale, theme);
            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }

        var btnGap = 8f * scale;
        var cancelW = (content.Width - btnGap) * 0.36f;
        var doneW = content.Width - cancelW - btnGap;

        var cancelMin = new Vector2(content.Min.X, bottomBarY);
        var cancelMax = cancelMin + new Vector2(cancelW, bottomBtnH);
        var cancelHovered = UiInteract.Hover(cancelMin, cancelMax);
        if (cancelHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        Squircle.Fill(drawList, cancelMin, cancelMax, 8f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, cancelHovered ? 0.95f : 0.70f)));
        Squircle.Stroke(drawList, cancelMin, cancelMax, 8f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.45f)), 1f * scale);
        Typography.DrawCentered(drawList, (cancelMin + cancelMax) * 0.5f, "Cancel", theme.TextStrong, TextStyles.Caption1.Scale, FontWeight.Medium);
        if (UiInteract.Click(cancelMin, cancelMax, cancelHovered))
        {
            rolePickerSheet.Close();
        }

        var doneMin = new Vector2(cancelMax.X + btnGap, bottomBarY);
        var doneMax = doneMin + new Vector2(doneW, bottomBtnH);
        var doneHovered = UiInteract.Hover(doneMin, doneMax);
        if (doneHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        var doneBg = doneHovered ? Palette.WithAlpha(theme.Accent, 0.85f) : theme.Accent;
        Squircle.Fill(drawList, doneMin, doneMax, 8f * scale, ImGui.GetColorU32(doneBg));
        Typography.DrawCentered(drawList, (doneMin + doneMax) * 0.5f, "Done", new Vector4(1f, 1f, 1f, 1f), TextStyles.Caption1.Scale, FontWeight.Bold);
        if (UiInteract.Click(doneMin, doneMax, doneHovered))
        {
            createPfSlots[editingSlotIndex].SetJobs(tempSlotJobs);
            rolePickerSheet.Close();
        }
    }

    private void DrawRolePresetChip(ref float cursorX, float posY, float height, float gap, string label, uint[] jobIds, Vector4 color, float scale, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var labelSize = Typography.Measure(label, TextStyles.Caption2.Scale, FontWeight.SemiBold);
        var chipW = labelSize.X + 16f * scale;
        var min = new Vector2(cursorX, posY);
        var max = min + new Vector2(chipW, height);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var allSelected = true;
        for (var i = 0; i < jobIds.Length; i++)
        {
            if (!tempSlotJobs.Contains(jobIds[i]))
            {
                allSelected = false;
                break;
            }
        }

        var fill = allSelected ? Palette.WithAlpha(color, 0.85f) : Palette.WithAlpha(theme.GroupedCard, hovered ? 0.95f : 0.70f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, min, max, height * 0.5f, ImGui.GetColorU32(allSelected ? color : Palette.WithAlpha(theme.Separator, 0.35f)), 1f * scale);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, allSelected ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong, TextStyles.Caption2.Scale, FontWeight.SemiBold);
        if (UiInteract.Click(min, max, hovered))
        {
            if (allSelected)
            {
                for (var i = 0; i < jobIds.Length; i++)
                {
                    tempSlotJobs.Remove(jobIds[i]);
                }
            }
            else
            {
                for (var i = 0; i < jobIds.Length; i++)
                {
                    tempSlotJobs.Add(jobIds[i]);
                }
            }
        }
        cursorX = max.X + gap;
    }

    private void DrawJobCategorySection(string title, PfJobItem[] jobs, PfJobItem[] classes, float width, float scale, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        Typography.Draw(ImGui.GetCursorScreenPos(), title, theme.TextMuted, TextStyles.Caption1.Scale, FontWeight.Bold);
        ImGui.Dummy(new Vector2(0f, 16f * scale));
        var tileSide = 38f * scale;
        var gap = 7f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var cols = (int)((width + gap) / (tileSide + gap));
        if (cols < 4)
        {
            cols = 4;
        }

        for (var i = 0; i < jobs.Length; i++)
        {
            var job = jobs[i];
            var col = i % cols;
            var row = i / cols;
            var jMin = new Vector2(origin.X + col * (tileSide + gap), origin.Y + row * (tileSide + gap));
            var jMax = jMin + new Vector2(tileSide, tileSide);
            DrawJobTile(drawList, jMin, jMax, job, scale, theme);
        }

        var totalRows = (jobs.Length + cols - 1) / cols;
        ImGui.Dummy(new Vector2(width, totalRows * (tileSide + gap) + 6f * scale));
        if (!createUnselectClasses && classes.Length > 0)
        {
            origin = ImGui.GetCursorScreenPos();
            for (var c = 0; c < classes.Length; c++)
            {
                var cl = classes[c];
                var cMin = new Vector2(origin.X + c * (tileSide + gap), origin.Y);
                var cMax = cMin + new Vector2(tileSide, tileSide);
                DrawJobTile(drawList, cMin, cMax, cl, scale, theme);
            }
            ImGui.Dummy(new Vector2(width, tileSide + 12f * scale));
        }
    }

    private void DrawJobTile(ImDrawListPtr drawList, Vector2 min, Vector2 max, PfJobItem job, float scale, PhoneTheme theme)
    {
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var isSelected = tempSlotJobs.Contains(job.Id);
        var bg = isSelected ? Palette.WithAlpha(theme.GroupedCard, 0.95f) : Palette.WithAlpha(theme.GroupedCard, 0.35f);
        Squircle.Fill(drawList, min, max, 6f * scale, ImGui.GetColorU32(bg));
        var border = isSelected ? theme.Accent : Palette.WithAlpha(theme.Separator, hovered ? 0.6f : 0.25f);
        Squircle.Stroke(drawList, min, max, 6f * scale, ImGui.GetColorU32(border), isSelected ? 2f * scale : 1f * scale);
        var iconInset = 3f * scale;
        GameIconTile.Draw(drawList, Plugin.TextureProvider, job.IconId, min + new Vector2(iconInset), max - new Vector2(iconInset), 4f * scale, scale);

        if (!isSelected)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f)), 6f * scale);
        }
        if (UiInteract.Click(min, max, hovered))
        {
            if (isSelected)
            {
                tempSlotJobs.Remove(job.Id);
            }
            else
            {
                tempSlotJobs.Add(job.Id);
            }
        }
    }
}