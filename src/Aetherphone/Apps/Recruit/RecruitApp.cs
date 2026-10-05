using System;
using System.Collections.Generic;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Recruit;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Game.Gui.PartyFinder.Types;

namespace Aetherphone.Apps.Recruit;

internal enum RecruitScreen
{
    Browse,
    Detail,
    PfDetail,
    Create,
};

internal enum RecruitTab
{
    PartyFinder,
    Statics,
};

internal sealed partial class RecruitApp : IPhoneApp
{
    public string Id => "recruit";
    public string DisplayName => Loc.T(L.Apps.Recruit);
    public string Glyph => "R";
    public Vector4 Accent => AppAccents.For("recruit");
    public int BadgeCount => 0;

    private readonly RecruitStore store;

    private static readonly ContentCategory[] AllCategories =
    {
        ContentCategory.Ultimate,
        ContentCategory.Savage,
        ContentCategory.ExtremeFarm,
        ContentCategory.Criterion,
        ContentCategory.DeepDungeon,
        ContentCategory.Other,
    };

    private static readonly string[] CategoryLabels =
    {
        "Ultimate",
        "Savage",
        "Extreme",
        "Criterion",
        "Deep Dungeon",
        "Other",
    };

    private static readonly string[] StaticKindLabels =
    {
        "All",
        "LFM",
        "LFG",
        "Fill",
    };

    private readonly AppSkin ui = new(AppPalettes.Neutral(AppAccents.For("recruit")));
    private readonly ViewRouter<RecruitScreen> router;
    private readonly RouterDraw<RecruitScreen> drawView;

    private RecruitListing? selectedListing;
    private PartyFinderListing? selectedPfListing;
    private readonly DropdownMenu categoryFilterMenu = new();
    private readonly List<DropdownMenu.Item> categoryFilterItems = new();
    private readonly DropdownMenu kindFilterMenu = new();
    private readonly List<DropdownMenu.Item> kindFilterItems = new();
    private ContentCategory? selectedCategory;
    private ListingKind? selectedKind;
    private PhoneContext currentContext;

    private RecruitTab activeTab = RecruitTab.Statics;
    private readonly BottomTabBar bottomNav = new();
    private readonly NavTab[] navTabs = new NavTab[3];

    public RecruitApp(RecruitStore store)
    {
        this.store = store;
        router = new ViewRouter<RecruitScreen>(RecruitScreen.Browse);
        drawView = DrawView;
    }

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        currentContext = context;
        var scale = UiScale.Current;
        ui.Theme = context.Theme;
        var content = context.Content;

        var screen = SceneChrome.ScreenFrom(content, context.Theme, scale);
        ui.Backdrop(screen);
        categoryFilterMenu.Gate();
        kindFilterMenu.Gate();
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        using (InputShield.Engage(PartyFinderCapturesPointer))
        {
            router.Draw(content, AppSkin.Transparent, delta, drawView);
        }
        DrawPartyFinderOverlays(screen, context.Theme);
    }

    private void DrawView(RecruitScreen view, Rect area, int depth)
    {
        switch (view)
        {
            case RecruitScreen.Browse:
                DrawRoot(area);
                break;
            case RecruitScreen.Detail:
                DrawDetailScreen(currentContext, area);
                break;
            case RecruitScreen.PfDetail:
                DrawPartyFinderDetailScreen(currentContext, area);
                break;
            case RecruitScreen.Create:
                DrawCreateScreen(currentContext, area);
                break;
        }
    }

    private void DrawRoot(Rect area)
    {
        var scale = UiScale.Current;
        var theme = ui.Theme;
        var barHeight = BottomTabBar.LabelledHeight * scale;
        var navRect = new Rect(new Vector2(area.Min.X, area.Max.Y - barHeight), area.Max);
        var tabArea = new Rect(area.Min, new Vector2(area.Max.X, navRect.Min.Y));

        switch (activeTab)
        {
            case RecruitTab.PartyFinder:
                DrawPartyFinderTab(tabArea, scale, theme);
                break;
            case RecruitTab.Statics:
            default:
                DrawStaticsTab(tabArea, scale, theme);
                break;
        }

        DrawBottomNav(navRect);
        var pickedCategory = categoryFilterMenu.Draw(
            area,
            theme,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(categoryFilterItems));
        if (pickedCategory >= 0){
            selectedCategory = pickedCategory == 0 ? null : AllCategories[pickedCategory - 1];
        }

        var pickedKind = kindFilterMenu.Draw(
            area,
            theme,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(kindFilterItems));
        if (pickedKind >= 0)
        {
            selectedKind = pickedKind switch
            {
                1 => ListingKind.PlayerLfg,
                2 => ListingKind.StaticLfm,
                3 => ListingKind.SingleNightFill,
                _ => null,
            };
        }
    }

    private void DrawBottomNav(Rect bar)
    {
        var pfCount = store.PartyFinderListings.Count;
        navTabs[0] = new NavTab(FontAwesomeIcon.Search, "Statics");
        navTabs[1] = new NavTab(FontAwesomeIcon.Plus, "New Listing", Raised: true);
        navTabs[2] = new NavTab(FontAwesomeIcon.Users, "Party Finder", Badge: pfCount);
        var activeSlot = activeTab == RecruitTab.PartyFinder ? 2 : 0;
        var tapped = bottomNav.Draw(bar, ui, currentContext.Theme, navTabs, activeSlot, showLabels: true);
        if (tapped >= 0){
            if (tapped == 1){
                router.Push(RecruitScreen.Create);
            }
            else{
                var nextTab = tapped == 2 ? RecruitTab.PartyFinder : RecruitTab.Statics;
                if (nextTab != activeTab){
                    activeTab = nextTab;
                    if (activeTab == RecruitTab.PartyFinder){
                        OnPartyFinderOpened();
                    }
                }
                else if (activeTab == RecruitTab.PartyFinder){
                    OnPartyFinderOpened();
                }
            }
        }
    }

    private void DrawStaticsTab(Rect area, float scale, PhoneTheme theme)
    {
        var accent = Accent;
        var headerHeight = 36f * scale;
        var headerY = area.Min.Y + 8f * scale;

        Typography.Draw(new Vector2(area.Min.X + 14f * scale, headerY), "Statics", theme.TextStrong, 1.25f, FontWeight.Bold);

        var filterBtnWidth = 72f * scale;
        var allBtnWidth = 52f * scale;
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
        
        var filterLabelX = filterMin.X + 10f * scale;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(filterLabelX, btnY + 5f * scale), "Filter", theme.TextStrong, TextStyles.Caption1);
        AppSkin.Icon(ImGui.GetWindowDrawList(), new Vector2(filterMax.X - 14f * scale, (filterMin.Y + filterMax.Y) * 0.5f),
            IconGlyph.Of(FontAwesomeIcon.SlidersH), theme.TextStrong, 0.75f);
        if (UiInteract.Click(filterMin, filterMax, filterHovered))
        {
            OpenCategoryFilterMenu(new Rect(filterMin, filterMax));
        }

        var allMaxX = filterMin.X - 6f * scale;
        allBtnWidth = 58f * scale;
        var allMin = new Vector2(allMaxX - allBtnWidth, btnY);
        var allMax = new Vector2(allMaxX, btnY + btnHeight);
        var allHovered = UiInteract.Hover(allMin, allMax);
        if (allHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var allActive = selectedKind.HasValue;
        var allBg = allActive
            ? Palette.WithAlpha(accent, allHovered ? 0.28f : 0.18f)
            : (allHovered ? Palette.WithAlpha(theme.SurfaceMuted, 0.95f) : Palette.WithAlpha(theme.SurfaceMuted, 0.70f));
        var allBorder = allActive
            ? Palette.WithAlpha(accent, allHovered ? 0.85f : 0.65f)
            : Palette.WithAlpha(theme.Separator, allHovered ? 0.6f : 0.3f);
        Squircle.Fill(ImGui.GetWindowDrawList(), allMin, allMax, 7f * scale, ImGui.GetColorU32(allBg));
        Squircle.Stroke(ImGui.GetWindowDrawList(), allMin, allMax, 7f * scale, ImGui.GetColorU32(allBorder), 1f * scale);
        var kindText = selectedKind switch
        {
            ListingKind.PlayerLfg => "LFG",
            ListingKind.StaticLfm => "LFM",
            ListingKind.SingleNightFill => "Fill",
            _ => "All",
        };

        var kindInk = allActive ? accent : theme.TextStrong;
        var allLabelX = allMin.X + 8f * scale;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(allLabelX, btnY + 5f * scale), kindText, kindInk, TextStyles.Caption1);
        AppSkin.Icon(ImGui.GetWindowDrawList(), new Vector2(allMax.X - 11f * scale, (allMin.Y + allMax.Y) * 0.5f),
            IconGlyph.Of(FontAwesomeIcon.ChevronDown), kindInk, 0.55f);
        if (UiInteract.Click(allMin, allMax, allHovered))
        {
            categoryFilterMenu.Close();
            OpenKindFilterMenu(new Rect(allMin, allMax));
        }

        var top = headerY + headerHeight + 8f * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var listings = store.Listings;
            var renderedCount = 0;
            for (var listingIndex = 0; listingIndex < listings.Count; listingIndex++)
            {
                var listing = listings[listingIndex];
                if (listing.Kind == ListingKind.PartyFinder)
                {
                    continue;
                }
                if (selectedKind.HasValue && listing.Kind != selectedKind.Value)
                {
                    continue;
                }
                if (selectedCategory.HasValue && listing.Duty.Category != selectedCategory.Value)
                {
                    continue;
                }
                DrawListingCard(width, scale, theme, accent, listing);
                ImGui.Dummy(new Vector2(0f, 10f * scale));
                renderedCount++;
            }
            if (renderedCount == 0)
            {
                Typography.DrawCentered(ImGui.GetWindowDrawList(),
                    ImGui.GetCursorScreenPos() + new Vector2(width * 0.5f, 40f * scale),
                    "No listings match your filter", theme.TextMuted, TextStyles.Body);
            }
            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }
    }

    private void OpenKindFilterMenu(Rect anchor)
    {
        kindFilterItems.Clear();
        kindFilterItems.Add(new DropdownMenu.Item("All", Selected: !selectedKind.HasValue));
        kindFilterItems.Add(new DropdownMenu.Item("LFG", Selected: selectedKind == ListingKind.PlayerLfg));
        kindFilterItems.Add(new DropdownMenu.Item("LFM", Selected: selectedKind == ListingKind.StaticLfm));
        kindFilterItems.Add(new DropdownMenu.Item("Fill", Selected: selectedKind == ListingKind.SingleNightFill));
        kindFilterMenu.Toggle("recruit_kind_filter", anchor);
    }

    private void OpenCategoryFilterMenu(Rect anchor)
    {
        categoryFilterItems.Clear();
        categoryFilterItems.Add(new DropdownMenu.Item("All Duties", Selected: !selectedCategory.HasValue));

        for (var index = 0; index < AllCategories.Length; index++){
            var cat = AllCategories[index];
            categoryFilterItems.Add(new DropdownMenu.Item(CategoryLabels[index], Selected: selectedCategory == cat));
        }

        categoryFilterMenu.Toggle("recruit_category_filter", anchor);
    }

    private void DrawListingCard(float width, float scale, PhoneTheme theme, Vector4 accent, RecruitListing listing)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var padX = 14f * scale;
        var padY = 12f * scale;
        var cardHeight = 156f * scale;
        var max = new Vector2(origin.X + width, origin.Y + cardHeight);
        var hovered = UiInteract.Hover(origin, max);
        var cardBg = ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, hovered ? 0.98f : 0.85f));
        var cardBorder = ImGui.GetColorU32(hovered ? Palette.WithAlpha(accent, 0.55f) : Palette.WithAlpha(theme.Separator, 0.35f));
        Squircle.Fill(drawList, origin, max, 14f * scale, cardBg);
        Squircle.Stroke(drawList, origin, max, 14f * scale, cardBorder, (hovered ? 1.5f : 1f) * scale);

        var kindLabel = listing.Kind switch
        {
            ListingKind.StaticLfm => "LFM",
            ListingKind.PlayerLfg => "PLAYER",
            ListingKind.SingleNightFill => "FILL",
            _ => "STATIC",
        };
        
        DrawTagPill(drawList, origin + new Vector2(padX, padY), kindLabel, theme, scale);
        var playstyleText = RecruitCatalog.CategoryName(listing.Playstyle).ToUpperInvariant();
        var playstyleSize = Typography.Measure(playstyleText, TextStyles.Caption2);
        DrawTagPill(drawList, new Vector2(max.X - padX - (playstyleSize.X + 16f * scale), origin.Y + padY),
            playstyleText, theme, scale);

        var dutyText = Typography.FitText(listing.Duty.Name, width - padX * 2f, TextStyles.Caption1);
        Typography.Draw(drawList, origin + new Vector2(padX, 36f * scale), dutyText, theme.TextMuted, TextStyles.Caption1);

        var titleText = Typography.FitText(listing.Title, width - padX * 2f, 1.05f, FontWeight.Bold);
        Typography.Draw(drawList, origin + new Vector2(padX, 52f * scale), titleText, theme.TextStrong, 1.05f, FontWeight.Bold);

        var dividerY = origin.Y + 80f * scale;
        drawList.AddLine(new Vector2(origin.X + padX, dividerY),
            new Vector2(max.X - padX, dividerY),
            ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.25f)), 1f * scale);

        var schedText = Typography.FitText(listing.FormattedSchedule, width - padX * 2f, TextStyles.Caption1);
        Typography.Draw(drawList, origin + new Vector2(padX, 88f * scale), schedText, theme.TextMuted, TextStyles.Caption1);

        var roleOffset = padX;
        var roleY = origin.Y + 112f * scale;
        for (var roleIndex = 0; roleIndex < listing.RolesNeeded.Count; roleIndex++)
        {
            var role = listing.RolesNeeded[roleIndex];
            var roleName = RecruitCatalog.RoleName(role);
            var roleColor = RoleColor(role);
            var roleIcon = RoleIcon(role);
            var pillWidth = DrawRolePill(drawList, new Vector2(origin.X + roleOffset, roleY), roleName, roleIcon, roleColor, scale);
            roleOffset += pillWidth + 8f * scale;
        }
        if (UiInteract.Click(origin, max, hovered))
        {
            selectedListing = listing;
            router.Push(RecruitScreen.Detail);
        }
        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private static float DrawTagPill(ImDrawListPtr drawList, Vector2 pos, string text, PhoneTheme theme, float scale)
    {
        var textSize = Typography.Measure(text, TextStyles.Caption2);
        var padX = 8f * scale;
        var padY = 3f * scale;
        var min = pos;
        var max = new Vector2(pos.X + textSize.X + padX * 2f, pos.Y + textSize.Y + padY * 2f);
        Squircle.Fill(drawList, min, max, 5f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.SurfaceMuted, 0.85f)));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, text, theme.TextStrong, TextStyles.Caption2);
        return max.X - min.X;
    }

    private static float DrawRolePill(ImDrawListPtr drawList, Vector2 pos, string text, FontAwesomeIcon icon, Vector4 color, float scale)
    {
        var textSize = Typography.Measure(text, TextStyles.Caption2);
        var iconSize = 10f * scale;
        var padX = 8f * scale;
        var padY = 4f * scale;
        var gap = 6f * scale;
        var pillWidth = padX * 2f + textSize.X + gap + iconSize;
        var pillHeight = textSize.Y + padY * 2f;
        var min = pos;
        var max = min + new Vector2(pillWidth, pillHeight);

        Squircle.Fill(drawList, min, max, 6f * scale, ImGui.GetColorU32(Palette.WithAlpha(color, 0.16f)));
        Squircle.Stroke(drawList, min, max, 6f * scale, ImGui.GetColorU32(Palette.WithAlpha(color, 0.70f)), 1f * scale);
        var textY = (min.Y + max.Y) * 0.5f - textSize.Y * 0.5f;
        Typography.Draw(drawList, new Vector2(min.X + padX, textY), text, color, TextStyles.Caption2);
        var iconCenter = new Vector2(min.X + padX + textSize.X + gap + iconSize * 0.5f, (min.Y + max.Y) * 0.5f);
        AppSkin.Icon(drawList, iconCenter, IconGlyph.Of(icon), color, 0.70f);

        return pillWidth;
    }

    private static string DutyCategoryLabel(ContentCategory category)
    {
        var idx = Array.IndexOf(AllCategories, category);
        return idx >= 0 && idx < CategoryLabels.Length ? CategoryLabels[idx] : category.ToString();
    }

    public static FontAwesomeIcon RoleIcon(RaidRole role) => role switch
    {
        RaidRole.Tank => FontAwesomeIcon.ShieldAlt,
        RaidRole.PureHealer or RaidRole.BarrierHealer => FontAwesomeIcon.Heartbeat,
        RaidRole.PhysRanged => FontAwesomeIcon.Crosshairs,
        RaidRole.Caster => FontAwesomeIcon.Magic,
        _ => FontAwesomeIcon.FistRaised,
    };

    public static FontAwesomeIcon PfSlotIcon(PfSlotRole role) => role switch
    {
        PfSlotRole.Tank => FontAwesomeIcon.ShieldAlt,
        PfSlotRole.Healer => FontAwesomeIcon.Heartbeat,
        _ => FontAwesomeIcon.FistRaised,
    };

    public static Vector4 RoleColor(RaidRole role) => role switch
    {
        RaidRole.Tank => AccentRing.Azure,
        RaidRole.PureHealer or RaidRole.BarrierHealer => AccentRing.Green,
        _ => AccentRing.Rose,
    };

    private static Vector4 KindBadgeColor(ListingKind kind)
    {
        return kind switch{
            ListingKind.StaticLfm => new Vector4(0.706f, 0.529f, 0.910f, 1.0f),
            ListingKind.PlayerLfg => new Vector4(0.902f, 0.878f, 0.549f, 1.0f),
            ListingKind.SingleNightFill => new Vector4(0.780f, 0.522f, 0.412f, 1.0f),
            ListingKind.PartyFinder => new Vector4(0.38f, 0.72f, 0.88f, 1.0f),
            _ => new Vector4(0.70f, 0.70f, 0.70f, 1f),
        };
    }

    private static Vector4 RoleBadgeColor(RaidRole role) => RoleColor(role);

    public void Dispose()
    {
    }
}
