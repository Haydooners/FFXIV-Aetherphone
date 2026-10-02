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

    private readonly AppSkin ui = new(AppPalettes.Tinted(new Vector4(0.027f, 0.569f, 0.408f, 1.0f)));
    private readonly ViewRouter<RecruitScreen> router;
    private readonly RouterDraw<RecruitScreen> drawView;

    private RecruitListing? selectedListing;
    private readonly DropdownMenu categoryFilterMenu = new();
    private readonly List<DropdownMenu.Item> categoryFilterItems = new();
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
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        using (InputShield.Engage(PartyFinderCapturesPointer || categoryFilterMenu.Open))
        {
            router.Draw(content, AppSkin.Transparent, delta, drawView);
        }
        DrawPartyFinderOverlays(screen, context.Theme);
    }

    private void DrawView(RecruitScreen view, Rect area, int depth)
    {
        switch (view){
            case RecruitScreen.Browse:
                DrawRoot(area);
                break;
            case RecruitScreen.Detail:
                DrawDetailScreen(currentContext, area);
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
            if (activeTab == RecruitTab.PartyFinder){
                selectedPfCategory = pickedCategory == 0 ? null : (PfCategory)pickedCategory;
                currentPfPage = 1;
            }
            else{
                selectedCategory = pickedCategory == 0 ? null : AllCategories[pickedCategory - 1];
            }
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
        var headerHeight = AppHeader.Height * scale;
        var headerCenterY = area.Min.Y + headerHeight * 0.5f;
        Typography.DrawCentered(new Vector2(area.Center.X, headerCenterY), "Statics", theme.TextStrong, 1.15f, FontWeight.SemiBold);
        var filterRowTop = area.Min.Y + headerHeight + 2f * scale;
        ImGui.SetCursorScreenPos(new Vector2(area.Min.X + 12f * scale, filterRowTop));
        DrawFilterAndKindRow(area.Width, scale, theme, accent);
        var top = filterRowTop + 34f * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            var listings = store.Listings;
            var renderedCount = 0;

            for (var listingIndex = 0; listingIndex < listings.Count; listingIndex++){
                var listing = listings[listingIndex];
                if (listing.Kind == ListingKind.PartyFinder){
                    continue;
                }
                if (selectedKind.HasValue && listing.Kind != selectedKind.Value){
                    continue;
                }
                if (selectedCategory.HasValue && listing.Duty.Category != selectedCategory.Value){
                    continue;
                }
                DrawListingCard(width, scale, theme, accent, listing);
                ImGui.Dummy(new Vector2(0f, 8f * scale));
                renderedCount++;
            }
            if (renderedCount == 0){
                Typography.DrawCentered(ImGui.GetWindowDrawList(),
                    ImGui.GetCursorScreenPos() + new Vector2(width * 0.5f, 40f * scale),
                    "No listings match your filter", theme.TextMuted, TextStyles.Body);
            }
            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }
    }

    private void DrawFilterAndKindRow(float totalWidth, float scale, PhoneTheme theme, Vector4 accent)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = 28f * scale;
        var usableWidth = totalWidth - 24f * scale;
        var filterLabel = selectedCategory.HasValue ? DutyCategoryLabel(selectedCategory.Value) : "All Duties";
        var labelSize = Typography.Measure(filterLabel, TextStyles.Caption1);
        var chevronReserve = 14f * scale;
        var filterPillWidth = 96f * scale;
        var filterMin = origin;
        var filterMax = new Vector2(filterMin.X + filterPillWidth, filterMin.Y + height);
        var filterHovered = UiInteract.Hover(filterMin, filterMax);

        if (filterHovered){
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var isFiltered = selectedCategory.HasValue;
        var filterBg = isFiltered
            ? Palette.WithAlpha(accent, filterHovered ? 0.28f : 0.18f)
            : (filterHovered ? Palette.WithAlpha(theme.GroupedCard, 0.95f) : theme.GroupedCard);
        var filterBorder = isFiltered
            ? Palette.WithAlpha(accent, 0.70f)
            : Palette.WithAlpha(theme.TextMuted, filterHovered ? 0.40f : 0.20f);
        Squircle.Fill(drawList, filterMin, filterMax, 7f * scale, ImGui.GetColorU32(filterBg));
        Squircle.Stroke(drawList, filterMin, filterMax, 7f * scale, ImGui.GetColorU32(filterBorder), 1f * scale);

        var filterInk = isFiltered ? theme.TextStrong : (filterHovered ? theme.TextStrong : theme.TextMuted);
        var maxTextWidth = filterPillWidth - chevronReserve - 12f * scale;
        var fittedText = Typography.FitText(filterLabel, maxTextWidth, TextStyles.Caption1);
        var fittedSize = Typography.Measure(fittedText, TextStyles.Caption1);
        var totalContentWidth = fittedSize.X + 4f * scale + 10f * scale;
        var contentStartX = (filterMin.X + filterMax.X - totalContentWidth) * 0.5f;
        var textCenterY = (filterMin.Y + filterMax.Y) * 0.5f;

        Typography.Draw(drawList, new Vector2(contentStartX, textCenterY - fittedSize.Y * 0.5f), fittedText, filterInk, TextStyles.Caption1);
        var chevronCenter = new Vector2(contentStartX + fittedSize.X + 4f * scale + 5f * scale, textCenterY);
        AppSkin.Icon(drawList, chevronCenter, IconGlyph.Of(FontAwesomeIcon.ChevronDown), filterInk, 0.60f);

        if (UiInteract.Click(filterMin, filterMax, filterHovered)){
            OpenCategoryFilterMenu(new Rect(filterMin, filterMax));
        }

        var sepX = filterMax.X + 7f * scale;
        drawList.AddLine(
            new Vector2(sepX, origin.Y + 4f * scale),
            new Vector2(sepX, origin.Y + height - 4f * scale),
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextMuted, 0.25f)), 1f * scale);
        var chipsStartX = sepX + 7f * scale;
        var chipsAvailableWidth = (origin.X + usableWidth) - chipsStartX;
        var chipGap = 5f * scale;
        var chipWidth = (chipsAvailableWidth - (StaticKindLabels.Length - 1) * chipGap) / StaticKindLabels.Length;

        for (var index = 0; index < StaticKindLabels.Length; index++){
            var label = StaticKindLabels[index];
            var active = index switch{
                0 => !selectedKind.HasValue,
                1 => selectedKind == ListingKind.StaticLfm,
                2 => selectedKind == ListingKind.PlayerLfg,
                3 => selectedKind == ListingKind.SingleNightFill,
                _ => false,
            };

            var min = new Vector2(chipsStartX + index * (chipWidth + chipGap), origin.Y);
            var max = new Vector2(min.X + chipWidth, min.Y + height);
            var hovered = UiInteract.Hover(min, max);
            var fill = active
                ? Palette.WithAlpha(theme.Accent, 0.92f)
                : (hovered ? Palette.WithAlpha(theme.GroupedCard, 0.95f) : theme.GroupedCard);
            Squircle.Fill(drawList, min, max, 7f * scale, ImGui.GetColorU32(fill));
            if (!active){
                Squircle.Stroke(drawList, min, max, 7f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(theme.TextMuted, hovered ? 0.35f : 0.15f)), 1f * scale);
            }

            var ink = active ? theme.TextStrong : (hovered ? theme.TextStrong : theme.TextMuted);
            Typography.DrawCentered(drawList, (min + max) * 0.5f, label, ink, 0.88f, active ? FontWeight.SemiBold : FontWeight.Medium);
            if (hovered){
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (UiInteract.Click(min, max, hovered)){
                selectedKind = index switch{
                    1 => ListingKind.StaticLfm,
                    2 => ListingKind.PlayerLfg,
                    3 => ListingKind.SingleNightFill,
                    _ => null,
                };
            }
        }
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
        var cardHeight = 152f * scale;
        var max = new Vector2(origin.X + width, origin.Y + cardHeight);
        var hovered = UiInteract.Hover(origin, max);

        var cardBg = ImGui.GetColorU32(Palette.WithAlpha(theme.SurfaceMuted, hovered ? 0.75f : 0.55f));
        var cardBorder = ImGui.GetColorU32(hovered ? Palette.WithAlpha(accent, 0.65f) : Palette.WithAlpha(theme.TextMuted, 0.18f));
        Squircle.Fill(drawList, origin, max, Metrics.Radius.Card * scale, cardBg);
        Squircle.Stroke(drawList, origin, max, Metrics.Radius.Card * scale, cardBorder, (hovered ? 1.5f : 1f) * scale);

        var kindColor = KindBadgeColor(listing.Kind);
        var kindLabel = RecruitCatalog.KindName(listing.Kind).ToUpperInvariant();
        DrawBadgePill(drawList, origin + new Vector2(14f * scale, 12f * scale), kindLabel, kindColor, scale);
        var playstyleText = RecruitCatalog.CategoryName(listing.Playstyle);
        var playstyleSize = Typography.Measure(playstyleText, TextStyles.Caption2);
        DrawBadgePill(drawList, origin + new Vector2(width - 14f * scale - (playstyleSize.X + 14f * scale), 12f * scale),
            playstyleText, theme.TextMuted, scale);

        var dutyText = Typography.FitText(listing.Duty.Name, width - 28f * scale, TextStyles.Caption1);
        Typography.Draw(drawList, origin + new Vector2(14f * scale, 34f * scale), dutyText, theme.TextMuted, TextStyles.Caption1);

        var titleText = Typography.FitText(listing.Title, width - 28f * scale, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, origin + new Vector2(14f * scale, 50f * scale), titleText, theme.TextStrong, TextStyles.SubheadlineEmphasized);

        var dividerY = origin.Y + 74f * scale;
        drawList.AddLine(new Vector2(origin.X + 14f * scale, dividerY),
            new Vector2(origin.X + width - 14f * scale, dividerY),
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextMuted, 0.12f)), 1f * scale);

        var schedText = Typography.FitText(listing.FormattedSchedule, width - 28f * scale, TextStyles.Caption2);
        Typography.Draw(drawList, origin + new Vector2(14f * scale, 80f * scale), schedText, theme.TextStrong, TextStyles.Caption2);

        var roleOffset = 14f * scale;
        var roleY = origin.Y + 100f * scale;
        for (var roleIndex = 0; roleIndex < listing.RolesNeeded.Count; roleIndex++)
        {
            var role = listing.RolesNeeded[roleIndex];
            var roleName = RecruitCatalog.RoleName(role);
            var roleColor = RoleBadgeColor(role);
            var pillWidth = DrawBadgePill(drawList, new Vector2(origin.X + roleOffset, roleY), roleName, roleColor, scale);
            roleOffset += pillWidth + 6f * scale;
        }

        var authorText = Typography.FitText($"{listing.AuthorName} · {listing.WorldDc}", width - 28f * scale, TextStyles.Caption2);
        Typography.Draw(drawList, origin + new Vector2(14f * scale, 126f * scale), authorText, theme.TextMuted, TextStyles.Caption2);

        if (UiInteract.Click(origin, max, hovered))
        {
            selectedListing = listing;
            router.Push(RecruitScreen.Detail);
        }
        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private static float DrawBadgePill(ImDrawListPtr drawList, Vector2 pos, string text, Vector4 color, float scale)
    {
        var textSize = Typography.Measure(text, TextStyles.Caption2);
        var padX = 7f * scale;
        var padY = 3f * scale;
        var min = pos;
        var max = new Vector2(pos.X + textSize.X + padX * 2f, pos.Y + textSize.Y + padY * 2f);

        Squircle.Fill(drawList, min, max, 5f * scale, ImGui.GetColorU32(Palette.WithAlpha(color, 0.18f)));
        Squircle.Stroke(drawList, min, max, 5f * scale, ImGui.GetColorU32(Palette.WithAlpha(color, 0.45f)), 1f * scale);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, text, color, TextStyles.Caption2);

        return max.X - min.X;
    }

    private static string DutyCategoryLabel(ContentCategory category)
    {
        var idx = Array.IndexOf(AllCategories, category);
        return idx >= 0 && idx < CategoryLabels.Length ? CategoryLabels[idx] : category.ToString();
    }

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

    private static Vector4 RoleBadgeColor(RaidRole role)
    {
        return role switch{
            RaidRole.Tank => new Vector4(0.28f, 0.58f, 0.95f, 1f),
            RaidRole.PureHealer or RaidRole.BarrierHealer => new Vector4(0.28f, 0.82f, 0.48f, 1f),
            _ => new Vector4(0.92f, 0.38f, 0.38f, 1f),
        };
    }

    public void Dispose()
    {
    }
}
