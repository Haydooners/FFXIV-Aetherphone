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
    private PfCategory? selectedPfCategory;
    private int currentPfPage = 1;
    private readonly DropdownMenu categoryFilterMenu = new();
    private readonly List<DropdownMenu.Item> categoryFilterItems = new();
    private ContentCategory? selectedCategory;
    private ListingKind? selectedKind;
    private PhoneContext currentContext;
    private DateTime lastPfPollTime = DateTime.MinValue;
    private static readonly TimeSpan PfAutoRefreshInterval = TimeSpan.FromSeconds(30);

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
        if (activeTab == RecruitTab.PartyFinder){
            lastPfPollTime = DateTime.UtcNow;
            store.RefreshPartyFinder(force: true);
        }
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
        router.Draw(content, AppSkin.Transparent, delta, drawView);
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
                        lastPfPollTime = DateTime.UtcNow;
                        store.RefreshPartyFinder(force: true);
                    }
                }
                else if (activeTab == RecruitTab.PartyFinder){
                    lastPfPollTime = DateTime.UtcNow;
                    store.RefreshPartyFinder(force: true);
                }
            }
        }
    }

    private void DrawPartyFinderTab(Rect area, float scale, PhoneTheme theme)
    {
        var accent = Accent;
        var headerHeight = AppHeader.Height * scale;
        var headerCenterY = area.Min.Y + headerHeight * 0.5f;
        Typography.DrawCentered(new Vector2(area.Center.X, headerCenterY), "Party Finder", theme.TextStrong, 1.15f, FontWeight.SemiBold);

        var refreshCenter = new Vector2(area.Max.X - 22f * scale, headerCenterY);
        if (ui.IconButton(refreshCenter, 14f * scale, IconGlyph.Of(FontAwesomeIcon.Sync), theme.TextMuted, AppSkin.Transparent, 0.85f, "Refresh")){
            lastPfPollTime = DateTime.UtcNow;
            store.RefreshPartyFinder(force: true);
        }

        var pfListings = store.PartyFinderListings;
        var totalMatchingCount = 0;
        for (var checkIndex = 0; checkIndex < pfListings.Count; checkIndex++){
            if (!selectedPfCategory.HasValue || MatchesPfCategory(pfListings[checkIndex], selectedPfCategory.Value)){
                totalMatchingCount++;
            }
        }
        var filterRowTop = area.Min.Y + headerHeight + 2f * scale;
        ImGui.SetCursorScreenPos(new Vector2(area.Min.X + 12f * scale, filterRowTop));
        DrawPartyFinderToolbar(area.Width, scale, theme, accent, totalMatchingCount);

        if ((DateTime.UtcNow - lastPfPollTime) > PfAutoRefreshInterval){
            lastPfPollTime = DateTime.UtcNow;
            store.RefreshPartyFinder();
        }

        var knownTotal = selectedPfCategory.HasValue
            ? totalMatchingCount
            : Math.Max(totalMatchingCount, PartyFinderReader.TotalListingsCount);
        const int pageSize = 50;
        var totalPages = Math.Max(1, (int)Math.Ceiling((float)knownTotal / pageSize));
        if (currentPfPage > totalPages){
            currentPfPage = totalPages;
        }
        if (currentPfPage < 1){
            currentPfPage = 1;
        }

        var pagerTop = filterRowTop + 36f * scale;
        var pagerHeight = 28f * scale;
        ImGui.SetCursorScreenPos(new Vector2(area.Min.X + 12f * scale, pagerTop));
        DrawPfPaginationBar(area.Width - 24f * scale, pagerHeight, scale, theme, accent, totalPages);

        var top = pagerTop + pagerHeight + 6f * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            var startIndex = (currentPfPage - 1) * pageSize;
            var endIndex = Math.Min(startIndex + pageSize, totalMatchingCount);
            var matchingIndex = 0;
            var renderedCount = 0;

            for (var index = 0; index < pfListings.Count; index++){
                var listing = pfListings[index];
                if (selectedPfCategory.HasValue && !MatchesPfCategory(listing, selectedPfCategory.Value)){
                    continue;
                }

                if (matchingIndex >= startIndex && matchingIndex < endIndex){
                    DrawPartyFinderCard(width, scale, theme, accent, listing);
                    ImGui.Dummy(new Vector2(0f, 8f * scale));
                    renderedCount++;
                }
                matchingIndex++;
            }

            if (renderedCount == 0){
                var emptyMsg = knownTotal > totalMatchingCount && currentPfPage > 1
                    ? $"Page {currentPfPage} listings are syncing...\nTap Refresh if they do not appear."
                    : "No in-game Party Finder listings found.\nTap Refresh to search.";
                Typography.DrawCentered(ImGui.GetWindowDrawList(),
                    ImGui.GetCursorScreenPos() + new Vector2(width * 0.5f, 40f * scale),
                    emptyMsg, theme.TextMuted, TextStyles.Body);
            }
            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }
    }

    private void DrawPfPaginationBar(float width, float height, float scale, PhoneTheme theme, Vector4 accent, int totalPages)
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);
        var bg = theme.SurfaceMuted;
        bg.W *= 0.5f;
        Squircle.Fill(drawList, min, max, 8f * scale, ImGui.GetColorU32(bg));
        Squircle.Stroke(drawList, min, max, 8f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Hairline, 0.4f)), 1f * scale);
        var btnWidth = 32f * scale;
        var hasPrev = currentPfPage > 1;
        var hasNext = currentPfPage < totalPages;
        var prevMin = min + new Vector2(2f * scale, 2f * scale);
        var prevMax = new Vector2(min.X + btnWidth, max.Y - 2f * scale);
        var prevHovered = ImGui.IsMouseHoveringRect(prevMin, prevMax);

        if (hasPrev && prevHovered){
            Squircle.Fill(drawList, prevMin, prevMax, 6f * scale, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.25f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (UiInteract.Click(prevMin, prevMax, prevHovered)){
                currentPfPage--;
            }
        }

        var prevColor = hasPrev ? (prevHovered ? theme.TextStrong : theme.TextStrong) : theme.TextMuted;
        Typography.DrawCentered(drawList, (prevMin + prevMax) * 0.5f, "<", prevColor, 0.9f, FontWeight.SemiBold);
        var nextMin = new Vector2(max.X - btnWidth, min.Y + 2f * scale);
        var nextMax = max - new Vector2(2f * scale, 2f * scale);
        var nextHovered = ImGui.IsMouseHoveringRect(nextMin, nextMax);
        if (hasNext && nextHovered){
            Squircle.Fill(drawList, nextMin, nextMax, 6f * scale, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.25f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (UiInteract.Click(nextMin, nextMax, nextHovered)){
                currentPfPage++;
                if (store.PartyFinderListings.Count < PartyFinderReader.TotalListingsCount){
                    store.RefreshPartyFinder();
                }
            }
        }

        var nextColor = hasNext ? (nextHovered ? theme.TextStrong : theme.TextStrong) : theme.TextMuted;
        Typography.DrawCentered(drawList, (nextMin + nextMax) * 0.5f, ">", nextColor, 0.9f, FontWeight.SemiBold);
        var centerPos = new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f);
        var pageText = $"Page {currentPfPage} of {totalPages}";
        Typography.DrawCentered(drawList, centerPos, pageText, theme.TextStrong, 0.85f, FontWeight.Medium);
        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y));
        ImGui.Dummy(new Vector2(width, 0));
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

    private void DrawPartyFinderToolbar(float totalWidth, float scale, PhoneTheme theme, Vector4 accent, int count)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = 32f * scale;
        var usableWidth = totalWidth - 24f * scale;
        var barMin = origin;
        var barMax = new Vector2(barMin.X + usableWidth, barMin.Y + height);
        var hovered = UiInteract.Hover(barMin, barMax);

        if (hovered){
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        
        var isFiltered = selectedPfCategory.HasValue;
        var bg = isFiltered
            ? Palette.WithAlpha(accent, hovered ? 0.22f : 0.14f)
            : (hovered ? Palette.WithAlpha(theme.GroupedCard, 0.95f) : Palette.WithAlpha(theme.GroupedCard, 0.70f));
        var border = isFiltered
            ? Palette.WithAlpha(accent, hovered ? 0.75f : 0.55f)
            : Palette.WithAlpha(theme.TextMuted, hovered ? 0.35f : 0.18f);
        Squircle.Fill(drawList, barMin, barMax, Metrics.Radius.Field * scale, ImGui.GetColorU32(bg));
        Squircle.Stroke(drawList, barMin, barMax, Metrics.Radius.Field * scale, ImGui.GetColorU32(border), 1f * scale);

        var centerY = (barMin.Y + barMax.Y) * 0.5f;
        var iconColor = isFiltered ? accent : (hovered ? theme.TextStrong : theme.TextMuted);
        var currentIcon = selectedPfCategory.HasValue ? PfCategoryIcon(selectedPfCategory.Value) : IconGlyph.Of(FontAwesomeIcon.Filter);

        var iconCenter = new Vector2(barMin.X + 16f * scale, centerY);
        AppSkin.Icon(drawList, iconCenter, currentIcon, iconColor, 0.75f);

        var textStartX = barMin.X + 32f * scale;
        var currentLabel = selectedPfCategory.HasValue ? PfCategoryLabels[(int)selectedPfCategory.Value] : "All Duties";
        if (isFiltered){
            var prefix = "Category: ";
            var prefixSize = Typography.Measure(prefix, TextStyles.Caption2);
            Typography.Draw(drawList, new Vector2(textStartX, centerY - prefixSize.Y * 0.5f), prefix, theme.TextMuted, TextStyles.Caption2);
            var valueStartX = textStartX + prefixSize.X;
            var valueSize = Typography.Measure(currentLabel, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(valueStartX, centerY - valueSize.Y * 0.5f), currentLabel, theme.TextStrong, TextStyles.Caption1);
        }
        else{
            var labelSize = Typography.Measure(currentLabel, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(textStartX, centerY - labelSize.Y * 0.5f), currentLabel, hovered ? theme.TextStrong : theme.TextMuted, TextStyles.Caption1);
        }

        var chevronRight = barMax.X - 14f * scale;
        AppSkin.Icon(drawList, new Vector2(chevronRight, centerY), IconGlyph.Of(FontAwesomeIcon.ChevronDown), iconColor, 0.60f);
        if (count > 0){
            var countText = count == 1 ? "1 listing" : $"{count} listings";
            var countSize = Typography.Measure(countText, TextStyles.Caption2);
            var countPos = new Vector2(chevronRight - 14f * scale - countSize.X, centerY - countSize.Y * 0.5f);
            Typography.Draw(drawList, countPos, countText, theme.TextMuted, TextStyles.Caption2);
        }
        if (UiInteract.Click(barMin, barMax, hovered)){
            OpenPfCategoryFilterMenu(new Rect(barMin, barMax));
        }
    }

    private void OpenPfCategoryFilterMenu(Rect anchor)
    {
        categoryFilterItems.Clear();
        categoryFilterItems.Add(new DropdownMenu.Item("All Duties", Selected: !selectedPfCategory.HasValue));
        for (var index = 1; index < PfCategoryLabels.Length; index++){
            var category = (PfCategory)index;
            categoryFilterItems.Add(new DropdownMenu.Item(PfCategoryLabels[index], Selected: selectedPfCategory == category));
        }
        categoryFilterMenu.Toggle("recruit_pf_category_filter", anchor);
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

    private void DrawPartyFinderCard(float width, float scale, PhoneTheme theme, Vector4 accent, PartyFinderListing listing)
    {
        var min = ImGui.GetCursorScreenPos();
        var padX = 14f * scale;
        var padY = 12f * scale;
        var contentWidth = width - padX * 2f;
        var titleSize = Typography.Measure(listing.DutyName, 1.05f, FontWeight.SemiBold);

        var hasComment = !string.IsNullOrWhiteSpace(listing.Comment);
        var commentHeight = hasComment
            ? Typography.MeasureWrappedBlock(listing.Comment, TextStyles.Footnote, contentWidth).Y
            : 0f;
        
        var metaText = $"Posted by: {listing.AuthorName} · {listing.WorldName}";
        var metaSize = Typography.Measure(metaText, TextStyles.Caption1);

        var badgeHeight = 18f * scale;
        var badgeToTitleGap = 8f * scale;
        var titleToCommentGap = hasComment ? 6f * scale : 0f;
        var commentToMetaGap = 10f * scale;
        var cardHeight = padY + badgeHeight + badgeToTitleGap + titleSize.Y + titleToCommentGap + commentHeight + commentToMetaGap + metaSize.Y + padY;

        var max = min + new Vector2(width, cardHeight);
        var hovered = UiInteract.Hover(min, max);

        if (hovered){
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        if (UiInteract.Click(min, max, hovered)){
            var opened = PartyFinderReader.OpenListing(listing.ListingId);
            if (!opened){
                store.RefreshPartyFinder();
            }
        }

        var drawList = ImGui.GetWindowDrawList();
        var cardBg = hovered ? Palette.WithAlpha(theme.GroupedCard, 0.98f) : theme.GroupedCard;
        var cardBorder = hovered ? Palette.WithAlpha(accent, 0.6f) : Palette.WithAlpha(theme.Separator, 0.4f);
        Squircle.Fill(drawList, min, max, Metrics.Radius.Card * scale, ImGui.GetColorU32(cardBg));
        Squircle.Stroke(drawList, min, max, Metrics.Radius.Card * scale, ImGui.GetColorU32(cardBorder), (hovered ? 1.5f : 1f) * scale);

        var closeSize = 20f * scale;
        var closeMin = new Vector2(max.X - padX - closeSize, min.Y + padY);
        var closeMax = closeMin + new Vector2(closeSize, closeSize);
        var closeHovered = UiInteract.Hover(closeMin, closeMax);
    
        if (closeHovered){
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(drawList, (closeMin + closeMax) * 0.5f, "X", 
            closeHovered ? theme.Danger : theme.TextMuted, 0.85f, FontWeight.Bold);
            
        if (UiInteract.Click(closeMin, closeMax, closeHovered)){
            store.RemovePartyFinderListing(listing.ListingId);
            return;
        }

        var pfBadgeColor = new Vector4(0.38f, 0.72f, 0.88f, 1.0f);
        var pfTextSize = Typography.Measure("PARTY FINDER", 0.75f, FontWeight.SemiBold);
        var pfBadgeMin = min + new Vector2(padX, padY);
        var pfBadgeMax = pfBadgeMin + new Vector2(pfTextSize.X + 12f * scale, 18f * scale);
        Squircle.Fill(drawList, pfBadgeMin, pfBadgeMax, 4f * scale, ImGui.GetColorU32(Palette.WithAlpha(pfBadgeColor, 0.22f)));
        Typography.DrawCentered(drawList, (pfBadgeMin + pfBadgeMax) * 0.5f, "PARTY FINDER", pfBadgeColor, 0.75f, FontWeight.SemiBold);

        var catTextSize = Typography.Measure(listing.CategoryName.ToUpperInvariant(), 0.75f, FontWeight.SemiBold);
        var catBadgeMin = new Vector2(pfBadgeMax.X + 6f * scale, pfBadgeMin.Y);
        var catBadgeMax = catBadgeMin + new Vector2(catTextSize.X + 12f * scale, 18f * scale);
        Squircle.Fill(drawList, catBadgeMin, catBadgeMax, 4f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.SurfaceMuted, 0.7f)));
        Typography.DrawCentered(drawList, (catBadgeMin + catBadgeMax) * 0.5f, listing.CategoryName.ToUpperInvariant(), theme.TextMuted, 0.75f, FontWeight.SemiBold);

        var curY = pfBadgeMax.Y + badgeToTitleGap;
        Typography.Draw(drawList, new Vector2(min.X + padX, curY), listing.DutyName, theme.TextStrong, 1.05f, FontWeight.SemiBold);
        curY += titleSize.Y;

        if (hasComment){
            curY += titleToCommentGap;
            var drawnCommentHeight = Typography.DrawWrappedLeft(new Vector2(min.X + padX, curY), listing.Comment, theme.TextMuted, TextStyles.Footnote, contentWidth);
            curY += drawnCommentHeight;
        }

        curY += commentToMetaGap;
        Typography.Draw(drawList, new Vector2(min.X + padX, curY), metaText, theme.TextMuted, TextStyles.Caption1);
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
