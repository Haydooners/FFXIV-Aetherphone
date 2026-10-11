using System;
using System.Collections.Generic;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;


namespace Aetherphone.Apps.Recruit;

internal sealed partial class RecruitApp
{
    private int createStep;
    private ListingKind createKind = ListingKind.PlayerLfg;
    private int createDutyIndex = -1;
    private readonly HashSet<string> createMindsets = new(StringComparer.OrdinalIgnoreCase) { "Midcore", "Prog" };
    private string createTitle = string.Empty;
    private readonly HashSet<RaidRole> createRoles = new() { RaidRole.Tank };
    private string createDescription = string.Empty;
    private readonly List<string> createTags = new() { "healer", "shield", "lgbt" };
    private string newTagBuffer = string.Empty;
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

    private void DrawCreateScreen(in PhoneContext context, Rect area)
    {
        var scale = UiScale.Current;
        var theme = ui.Theme;
        var drawList = ImGui.GetWindowDrawList();
        var headerHeight = 44f * scale;
        var headerMin = area.Min;
        var headerMax = new Vector2(area.Max.X, headerMin.Y + headerHeight);
        DrawCreateHeader(drawList, headerMin, headerMax, scale, theme, context);
        var top = headerMax.Y;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawCreateStepIndicators(drawList, width, scale, theme);
            switch (createStep)
            {
                case 0:
                    DrawCreateStep1(width, scale, theme);
                    break;
                case 1:
                    DrawCreateStep2(width, scale, theme);
                    break;
                case 2:
                    DrawCreateStep3(width, scale, theme);
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
                _ => ListingKind.SingleNightFill,
            };
        }
        var pickedDuty = dutyPickerMenu.Draw(area, theme, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(dutyPickerMenuItems));
        if (pickedDuty >= 0)
        {
            createDutyIndex = pickedDuty - 1; // 0 is "None", >= 1 is duty index
        }
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
        var nextHovered = UiInteract.Hover(nextMin, nextMax);
        if (nextHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        var accentColor = new Vector4(0.24f, 0.55f, 0.95f, 1f);
        Squircle.Fill(drawList, nextMin, nextMax, nextBtnHeight * 0.5f, ImGui.GetColorU32(nextHovered ? Palette.WithAlpha(accentColor, 0.85f) : accentColor));
        Typography.DrawCentered(drawList, (nextMin + nextMax) * 0.5f, nextLabel, new Vector4(1f, 1f, 1f, 1f), TextStyles.Caption1.Scale, FontWeight.Bold);
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

    private void DrawCreateStepIndicators(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme)
    {
        var pillWidth = 34f * scale;
        var pillHeight = 3.5f * scale;
        var gap = 7f * scale;
        var totalWidth = 3 * pillWidth + 2 * gap;
        var startX = ImGui.GetCursorScreenPos().X + (width - totalWidth) * 0.5f;
        var currentY = ImGui.GetCursorScreenPos().Y + 2f * scale;
        for (var s = 0; s < 3; s++)
        {
            var pMin = new Vector2(startX + s * (pillWidth + gap), currentY);
            var pMax = pMin + new Vector2(pillWidth, pillHeight);
            var active = s == createStep;
            var pillColor = active ? new Vector4(1f, 1f, 1f, 0.95f) : Palette.WithAlpha(theme.TextMuted, 0.30f);
            Squircle.Fill(drawList, pMin, pMax, pillHeight * 0.5f, ImGui.GetColorU32(pillColor));
        }
        ImGui.Dummy(new Vector2(width, pillHeight + 14f * scale));
    }

    private void DrawCreateStep1(float width, float scale, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        DrawFieldLabel("Listing Type", theme);
        var kindLabel = createKind switch
        {
            ListingKind.PlayerLfg => "LFG",
            ListingKind.StaticLfm => "LFM",
            _ => "Fill",
        };
        DrawDropdownTrigger(drawList, width, scale, theme, kindLabel, () =>
        {
            kindMenuItems.Clear();
            kindMenuItems.Add(new DropdownMenu.Item("LFG", Selected: createKind == ListingKind.PlayerLfg));
            kindMenuItems.Add(new DropdownMenu.Item("LFM", Selected: createKind == ListingKind.StaticLfm));
            kindMenuItems.Add(new DropdownMenu.Item("Fill", Selected: createKind == ListingKind.SingleNightFill));
            var p = ImGui.GetItemRectMin();
            kindMenu.Toggle("recruit_create_kind", new Rect(p, p + ImGui.GetItemRectSize()));
        });
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        DrawFieldLabel("Duty", theme);
        var dutyLabel = createDutyIndex >= 0 && createDutyIndex < RecruitCatalog.Duties.Count
            ? RecruitCatalog.Duties[createDutyIndex].Name
            : "None";
        DrawDropdownTrigger(drawList, width, scale, theme, dutyLabel, () =>
        {
            dutyPickerMenuItems.Clear();
            dutyPickerMenuItems.Add(new DropdownMenu.Item("None", Selected: createDutyIndex < 0));
            for (var i = 0; i < RecruitCatalog.Duties.Count; i++)
            {
                dutyPickerMenuItems.Add(new DropdownMenu.Item(RecruitCatalog.Duties[i].Name, Selected: i == createDutyIndex));
            }
            var p = ImGui.GetItemRectMin();
            dutyPickerMenu.Toggle("recruit_create_duty_pick", new Rect(p, p + ImGui.GetItemRectSize()));
        });
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        DrawFieldLabel("Mindset", theme);
        Typography.Draw(ImGui.GetCursorScreenPos(), "select all that apply", theme.TextMuted, TextStyles.Caption2);
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var originX = ImGui.GetCursorScreenPos().X;
        var cursorX = originX;
        var startY = ImGui.GetCursorScreenPos().Y;
        var centerY = startY + 12f * scale;
        var rowHeight = 32f * scale;
        var maxX = originX + width;
        for (var i = 0; i < MindsetOptions.Length; i++)
        {
            var opt = MindsetOptions[i];
            var active = createMindsets.Contains(opt);
            var chipWidth = Typography.Measure(opt, TextStyles.Caption1).X + 22f * scale;
            if (cursorX + chipWidth > maxX && cursorX > originX)
            {
                cursorX = originX;
                centerY += rowHeight;
            }
            if (AppSkin.FlowChip(ref cursorX, centerY, 6f * scale, opt, active, theme))
            {
                if (!createMindsets.Add(opt))
                {
                    createMindsets.Remove(opt);
                }
            }
        }
        var totalHeight = (centerY - startY) + rowHeight * 0.5f;
        ImGui.Dummy(new Vector2(width, totalHeight));
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
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        var authorName = localPlayer?.Name.ToString() ?? "Instant Queues";
        var worldName = localPlayer?.HomeWorld.ValueNullable?.Name.ToString() ?? "Midgardsormr";
        var selectedDuty = createDutyIndex >= 0 && createDutyIndex < RecruitCatalog.Duties.Count
            ? RecruitCatalog.Duties[createDutyIndex]
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
            string.IsNullOrWhiteSpace(createTitle) ? "Scrub tank looking for a home" : createTitle.Trim(),
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
        createStep = 0;
        createTitle = string.Empty;
        createDescription = string.Empty;
        router.Pop();
    }
}