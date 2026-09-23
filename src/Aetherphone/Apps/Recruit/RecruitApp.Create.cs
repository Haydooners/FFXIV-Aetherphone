using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;


namespace Aetherphone.Apps.Recruit;

internal sealed partial class RecruitApp
{
    private readonly DropdownMenu dutyCategoryMenu = new();
    private readonly List<DropdownMenu.Item> dutyCategoryMenuItems = new();
    private readonly DropdownMenu dutyMenu = new();
    private readonly List<DropdownMenu.Item> dutyMenuItems = new();
    private ContentCategory createDutyCategory = ContentCategory.Ultimate;
    private ListingKind createKind = ListingKind.StaticLfm;
    private int createDutyIndex;
    private readonly HashSet<RaidRole> createRoles = new();
    private string createTitle = string.Empty;
    private string createDescription = string.Empty;
    private string createSchedule = string.Empty;
    private static readonly string[] RoleLabels = { "Tank", "Pure Healer", "Barrier Healer", "Melee", "Phys Ranged", "Caster" };
    private static readonly RaidRole[] Roles = { RaidRole.Tank, RaidRole.PureHealer, RaidRole.BarrierHealer, RaidRole.Melee, RaidRole.PhysRanged, RaidRole.Caster };
    private RaidDays createDays = RaidDays.Tuesday | RaidDays.Thursday;
    private const float TimeFieldHeight = 42f;
    private int createStartHour = 1200;
    private int createEndHour = 1300;
    private readonly RaidTimezone createTimezone = RaidTimezone.EST;

    private static readonly RaidDays[] AllRaidDays =
    {
        RaidDays.Monday,
        RaidDays.Tuesday,
        RaidDays.Wednesday,
        RaidDays.Thursday,
        RaidDays.Friday,
        RaidDays.Saturday,
        RaidDays.Sunday,
    };

    private static readonly string[] DayLabels = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }; 

    private static readonly ContentCategory[] DutyCategories =
    {
        ContentCategory.Ultimate,
        ContentCategory.Savage,
        ContentCategory.Criterion,
        ContentCategory.ExtremeFarm,
        ContentCategory.DeepDungeon,
        ContentCategory.Other,
    };

    private static readonly ListingKind[] Kinds =
    {
        ListingKind.StaticLfm,
        ListingKind.PlayerLfg,
        ListingKind.SingleNightFill,
    };

    private static readonly string[] DutyCategoryLabels =
    {
        "Ultimate",
        "Savage",
        "Criterion",
        "Extreme",
        "Deep Dungeon",
        "Casual / Other",
    };

    private void OpenCategoryMenu(Rect anchor)
    {
        dutyCategoryMenuItems.Clear();

        for (var index = 0; index < DutyCategories.Length; index++)
        {
            dutyCategoryMenuItems.Add(new DropdownMenu.Item(
                DutyCategoryLabels[index],
                Selected: DutyCategories[index] == createDutyCategory));
        }

       dutyCategoryMenu.Toggle("recruit_create_category", anchor);
    }

    private void OpenDutyMenu(Rect anchor, List<DutyInfo> duties)
    {
        dutyMenuItems.Clear();
        for (var index = 0; index < duties.Count; index++)
        {
            dutyMenuItems.Add(new DropdownMenu.Item(
                duties[index].Name,
                Selected: index == createDutyIndex));
        }

        dutyMenu.Toggle("recruit_create_duty", anchor);
    }

    private void DrawCreateScreen(in PhoneContext context, Rect area)
    {
        var scale = UiScale.Current;
        var theme = ui.Theme;
        var accent = Core.Apps.AppAccents.For("recruit");

        AppHeader.Draw(context, "New Listing", () => router.Pop());

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        using (AppSurface.Begin(body))
        {
            var pageScrollY = ImGui.GetScrollY();
            var width = ScrollLayout.StableContentWidth();
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            ui.SectionLabel("LISTING TYPE");
            DrawCreateKindChips();

            ImGui.Dummy(new Vector2(0f, 10f * scale));
            ui.SectionLabel("DUTY");
            DrawCreateDutyChips();

            ImGui.Dummy(new Vector2(0f, 10f * scale));
            ui.SectionLabel("ROLE");
            DrawCreateRoleChips();

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            ui.SectionLabel("TITLE");
            ImGui.SetNextItemWidth(width);

            ImGui.InputText("##createTitle", ref createTitle, 64);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            ui.SectionLabel("SCHEDULE / TIMES");
            DrawCreateDaysChips();

            ImGui.Dummy(new Vector2(0f, 10f * scale));
            createStartHour = DrawCreateTimeField("START TIME", createStartHour, scale);
            createEndHour = DrawCreateTimeField("END TIME", createEndHour, scale);

            ImGui.Dummy(new Vector2(0f, 10f * scale));
            ui.SectionLabel("DESCRIPTION / DETAILS");
            ImGui.SetNextItemWidth(width);

            ImGui.InputTextMultiline("##createDesc", ref createDescription, 256, new Vector2(width, 70f * scale));
            ImGui.Dummy(new Vector2(0f, 16f * scale));

            var canSubmit = !string.IsNullOrWhiteSpace(createTitle);
            if (canSubmit)
            {
                if (ImGui.Button("Post Listing", new Vector2(width, 36f * scale)))
                {
                    SubmitNewListing(context);
                }
            }
            else
            {
                ImGui.BeginDisabled();
                ImGui.Button("Post Listing (Enter Title)", new Vector2(width, 36f * scale));
                ImGui.EndDisabled();
            }
            ImGui.Dummy(new Vector2(0f, 20f * scale));
        }

        var pickedCategory = dutyCategoryMenu.Draw(
            area,
            theme,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(dutyCategoryMenuItems));
        if (pickedCategory >= 0)
        {
            createDutyCategory = DutyCategories[pickedCategory];
            createDutyIndex = 0;
        }

        var filteredDuties = new List<DutyInfo>();
        for (var index = 0; index < RecruitCatalog.Duties.Count; index++)
        {
            var duty = RecruitCatalog.Duties[index];
            if (duty.Category == createDutyCategory)
            {
                filteredDuties.Add(duty);
            }
        }

        var pickedDuty = dutyMenu.Draw(
            area,
            theme,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(dutyMenuItems));
        if (pickedDuty >= 0)
        {
            createDutyIndex = pickedDuty;
        }
    }

    private void DrawCreateKindChips()
    {
        var scale = UiScale.Current;
        var theme = ui.Theme;
        var cursorX = ImGui.GetCursorScreenPos().X;
        var centerY = ImGui.GetCursorScreenPos().Y + 12f * scale;

        for (var kindIndex = 0; kindIndex < Kinds.Length; kindIndex++){
            var kind = Kinds[kindIndex];
            var active = createKind == kind;
            var label = StaticKindLabels[kindIndex];
            if (AppSkin.FlowChip(ref cursorX, centerY, 6f * scale, label, active, theme)){
                createKind = kind;
            }
        }

        ImGui.Dummy(new Vector2(ScrollLayout.StableContentWidth(), 28f * scale));
    }

    private void DrawCreateDutyChips()
    {
        var scale = UiScale.Current;
        var width = ScrollLayout.StableContentWidth();

        var categoryIndex = Array.IndexOf(DutyCategories, createDutyCategory);
        var categoryLabel = categoryIndex >= 0 ? DutyCategoryLabels[categoryIndex] : "Select Category";

        if (ImGui.Button($"{categoryLabel}  v##catDrop", new Vector2(width, 34f * scale)))
        {
            var pos = ImGui.GetItemRectMin();
            var size = ImGui.GetItemRectSize();
            OpenCategoryMenu(new Rect(pos, pos + size));
        }
        ImGui.Dummy(new Vector2(0f, 8f * scale));

        var filteredDuties = new List<DutyInfo>();
        for (var index = 0; index < RecruitCatalog.Duties.Count; index++)
        {
            var duty = RecruitCatalog.Duties[index];
            if (duty.Category == createDutyCategory)
            {
                filteredDuties.Add(duty);
            }
        }

        var selectedDutyName = filteredDuties.Count > 0
            ? filteredDuties[Math.Clamp(createDutyIndex, 0, filteredDuties.Count - 1)].Name
            : "Select Duty";
        if (ImGui.Button($"{selectedDutyName}  v##dutyDrop", new Vector2(width, 34f * scale)))
        {
            var pos = ImGui.GetItemRectMin();
            var size = ImGui.GetItemRectSize();
            OpenDutyMenu(new Rect(pos, pos + size), filteredDuties);
        }
    }

    private void DrawCreateRoleChips()
    {
        var scale = UiScale.Current;
        var theme = ui.Theme;
        var originX = ImGui.GetCursorScreenPos().X;
        var cursorX = originX;
        var startY = ImGui.GetCursorScreenPos().Y;
        var centerY = startY + 12f * scale;
        var rowHeight = 30f * scale;
        var width = ScrollLayout.StableContentWidth();
        var maxX = originX + width;

        for (var roleIndex = 0; roleIndex < Roles.Length; roleIndex++){
            var role = Roles[roleIndex];
            var active = createRoles.Contains(role);
            var label = RoleLabels[roleIndex];
            var chipWidth = Typography.Measure(label, TextStyles.Caption1).X + 20f * scale;
            if (cursorX + chipWidth > maxX && cursorX > originX){
                cursorX = originX;
                centerY += rowHeight;
            }

            if (AppSkin.FlowChip(ref cursorX, centerY, 6f * scale, label, active, theme)){
                if (!createRoles.Add(role)){
                    if (createRoles.Count > 1){
                        createRoles.Remove(role);
                    }
                }
            }
        }

        var totalHeight = (centerY - startY) + rowHeight * 0.5f;
        ImGui.Dummy(new Vector2(width, totalHeight));

    }

    private void DrawCreateDaysChips()
    {
        var scale = UiScale.Current;
        var theme = ui.Theme;
        var originX = ImGui.GetCursorScreenPos().X;
        var cursorX = originX;
        var startY = ImGui.GetCursorScreenPos().Y;
        var centerY = startY + 12f * scale;
        var rowHeight = 30f * scale;
        var width = ScrollLayout.StableContentWidth();
        var maxX = originX + width;
        for (var index = 0; index < AllRaidDays.Length; index++){
            var day = AllRaidDays[index];
            var active = createDays.HasFlag(day);
            var label = DayLabels[index];
            var chipWidth = Typography.Measure(label, TextStyles.Caption1).X + 20f * scale;
            if (cursorX + chipWidth > maxX && cursorX > originX){
                cursorX = originX;
                centerY += rowHeight;
            }

            if (AppSkin.FlowChip(ref cursorX, centerY, 6f * scale, label, active, theme)){
                createDays ^= day;
                if (createDays == RaidDays.None){
                    createDays = day;
                }
            }
        }

        var totalHeight = (centerY - startY) + rowHeight * 0.5f;
        ImGui.Dummy(new Vector2(width, totalHeight));
    }

    private int DrawCreateTimeField(string label, int minuteOfDay, float scale)
    {
        ui.SectionLabel(label);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = TimeFieldHeight * scale;
        var edited = TimeOfDayField.Draw(ui,
            new Rect(origin, new Vector2(origin.X + width, origin.Y + height)), minuteOfDay, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));

        return edited;
    }

    private void SubmitNewListing(in PhoneContext context)
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        var authorName = localPlayer?.Name.ToString() ?? "Anonymous";
        var worldName = localPlayer?.HomeWorld.ValueNullable?.Name.ToString() ?? "Unknown";

        var filteredDuties = new List<DutyInfo>();
        for (var dutyIndex = 0; dutyIndex < RecruitCatalog.Duties.Count; dutyIndex++){
            var duty = RecruitCatalog.Duties[dutyIndex];
            if (duty.Category == createDutyCategory){
                filteredDuties.Add(duty);
            }
        }

        var startMinuteOfDay = createStartHour * 60;
        var endMinuteOfDay = createEndHour * 60;
        var scheduleText = $"{createStartHour}:00 - {createEndHour}:00 ({createTimezone})";

        var selectedDuty = filteredDuties.Count > 0 ? filteredDuties[Math.Clamp(createDutyIndex, 
            0, filteredDuties.Count -1)] : RecruitCatalog.Duties[0];

        var listing = new RecruitListing(
            Guid.NewGuid().ToString("N"),
            createTitle.Trim(),
            createDescription.Trim(),
            createKind,
            selectedDuty,
            StaticCategory.MidCore,
            createDays,
            createStartHour,
            createEndHour,
            createTimezone,
            new List<RaidRole>(createRoles),
            new List<string>(),
            authorName,
            worldName,
            DateTime.UtcNow
        );
        store.Add(listing);

        createTitle = string.Empty;
        createDescription = string.Empty;
        createSchedule = string.Empty;
        createRoles.Clear();
        createRoles.Add(RaidRole.Tank);

        router.Pop();
    }
}