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
}

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
        "Casual / Other",
    };

    private static readonly string[] KindLabels =
    {
        "All",
        "LFG",
        "LFM",
        "Fill",
        "PF",
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

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        router.Draw(content, AppSkin.Transparent, delta, drawView);
    }

    private void DrawView(RecruitScreen view, Rect area, int depth)
    {
        switch (view){
            case RecruitScreen.Browse:
                DrawBrowseScreen(currentContext, area);
                break;
            case RecruitScreen.Detail:
                DrawDetailScreen(currentContext, area);
                break;
            case RecruitScreen.Create:
                DrawCreateScreen(currentContext, area);
                break;
        }
    }

    private void DrawBrowseScreen(in PhoneContext context, Rect area)
    {
        var scale = UiScale.Current;
        var theme = ui.Theme;
        var accent = Accent;

        var headerHeight = AppHeader.Height * scale;
        var headerCenterY = area.Min.Y + headerHeight * 0.5f;

        Typography.DrawCentered(new Vector2(area.Center.X, headerCenterY), "Recruit", theme.TextStrong, 1.15f, FontWeight.SemiBold);

        var filterLabel = selectedCategory.HasValue ? DutyCategoryLabel(selectedCategory.Value) : "Filter";
        var filterText = $"{filterLabel}";
        var filterSize = Typography.Measure(filterText, TextStyles.Subheadline);
        var filterWidth = MathF.Max(72f * scale, filterSize.X + 24f * scale);
        var filterHeight = 30f * scale;
        var filterMin = new Vector2(area.Min.X + 2f * scale, headerCenterY - filterHeight * 0.5f);
        var filterMax = new Vector2(filterMin.X + filterWidth, filterMin.Y + filterHeight);
        var filterHovered = UiInteract.Hover(filterMin, filterMax);
        var filterDrawList = ImGui.GetWindowDrawList();

        Squircle.Fill(filterDrawList, filterMin, filterMax, 6f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.SurfaceMuted, filterHovered ? 0.9f : 0.6f)));
        Squircle.Stroke(filterDrawList, filterMin, filterMax, 6f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(filterHovered ? accent : theme.TextMuted, 0.35f)), 1f * scale);
        Typography.DrawCentered(filterDrawList, (filterMin + filterMax) * 0.5f, filterText, theme.TextStrong, TextStyles.Subheadline);

        if (UiInteract.Click(filterMin, filterMax, filterHovered)){
            OpenCategoryFilterMenu(new Rect(filterMin, filterMax));
        }

        var refreshMax = new Vector2(area.Max.X - 12f * scale, filterMin.Y + 28f * scale);
        var refreshMin = new Vector2(refreshMax.X - 72f * scale, filterMin.Y);
        var refreshHovered = UiInteract.Hover(refreshMin, refreshMax);

        if (refreshHovered){
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Squircle.Fill(filterDrawList, refreshMin, refreshMax, 6f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, refreshHovered ? 0.95f : 0.70f)));
        Squircle.Stroke(filterDrawList, refreshMin, refreshMax, 6f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(refreshHovered ? accent : theme.TextMuted, 0.35f)), 1f * scale);
        Typography.DrawCentered(filterDrawList, (refreshMin + refreshMax) * 0.5f, "Refresh", theme.TextStrong, TextStyles.Subheadline);

        if (UiInteract.Click(refreshMin, refreshMax, refreshHovered))
        {
            store.RefreshPartyFinder();
        }

        var chipsTopY = area.Min.Y + headerHeight + 8f * scale;
        ImGui.SetCursorScreenPos(new Vector2(area.Min.X + 12f * scale, chipsTopY + 2f * scale));
        DrawKindChips(area.Width, scale, theme, accent);

        var top = chipsTopY + 40f * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body)){

            var width = ScrollLayout.StableContentWidth();
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            
            var isPfSelected = selectedKind == ListingKind.PartyFinder;
            if (isPfSelected)
            {
                var pfListings = store.PartyFinderListings;
                for (var index = 0; index < pfListings.Count; index++){
                    DrawPartyFinderCard(width, scale, theme, accent, pfListings[index]);
                    ImGui.Dummy(new Vector2(0f, 8f * scale));
                }

                if (pfListings.Count == 0)
                {
                    Typography.DrawCentered(ImGui.GetWindowDrawList(),
                        ImGui.GetCursorScreenPos() + new Vector2(width * 0.5f, 40f * scale),
                        "No in-game Party Finder listings in memory.\nOpen Party Finder in-game to refresh.", theme.TextMuted, TextStyles.Body);
                }
            }
            else{
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

                ImGui.Dummy(new Vector2(0f, 60f * scale));
            }
        }

        if (ComposeFab.Draw(area, "##recruitCreateFab", Accent, IconGlyph.Of(FontAwesomeIcon.Plus), "New Listing")){
            router.Push(RecruitScreen.Create);
        }

        var pickedCategory = categoryFilterMenu.Draw(
            area,
            theme,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(categoryFilterItems));

        if (pickedCategory >= 0){
            selectedCategory = pickedCategory == 0 ? null : AllCategories[pickedCategory - 1];
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

    private void DrawKindChips(float width, float scale, PhoneTheme theme, Vector4 accent)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = 32f * scale;
        var gap = 5f * scale;
        var totalWidth = width - 24f * scale;
        var chipWidth = (totalWidth - (KindLabels.Length - 1) * gap) / KindLabels.Length;

        for (var index = 0; index < KindLabels.Length; index++){
            var label = KindLabels[index];
            var active = index switch{
                0 => !selectedKind.HasValue,
                1 => selectedKind == ListingKind.PlayerLfg,
                2 => selectedKind == ListingKind.StaticLfm,
                3 => selectedKind == ListingKind.SingleNightFill,
                4 => selectedKind == ListingKind.PartyFinder,
                _ => false,
            };

            var min = new Vector2(origin.X + index * (chipWidth + gap), origin.Y);
            var max = new Vector2(min.X + chipWidth, min.Y + height);
            var hovered = UiInteract.Hover(min, max);
            var fill = active
                ? Palette.WithAlpha(theme.Accent, 0.92f)
                : (hovered ? Palette.WithAlpha(theme.GroupedCard, 0.95f) : theme.GroupedCard);
            Squircle.Fill(drawList, min, max, 8f * scale, ImGui.GetColorU32(fill));
            var ink = active || hovered ? theme.TextStrong : theme.TextMuted;
            Typography.DrawCentered(drawList, (min + max) * 0.5f, label, ink, 0.92f, FontWeight.Medium);

            if (hovered){
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered)){
                selectedKind = index switch{
                    1 => ListingKind.PlayerLfg,
                    2 => ListingKind.StaticLfm,
                    3 => ListingKind.SingleNightFill,
                    4 => ListingKind.PartyFinder,
                    _ => null,
                };

                if (selectedKind == ListingKind.PartyFinder){
                    store.RefreshPartyFinder();
                }
            }
        }
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
        var commentHeight = Typography.MeasureWrapped(listing.Comment, contentWidth, TextStyles.Footnote.Scale, TextStyles.Footnote.Weight);
        var metaText = $"Posted by: {listing.AuthorName} · {listing.WorldName}";
        var metaSize = Typography.Measure(metaText, TextStyles.Caption1);
        var cardHeight = padY + 22f * scale + 6f * scale + titleSize.Y + 6f * scale + commentHeight + 8f * scale + metaSize.Y + padY;
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

        var curY = min.Y + padY + 26f * scale;
        Typography.Draw(drawList, new Vector2(min.X + padX, curY), listing.DutyName, theme.TextStrong, 1.05f, FontWeight.SemiBold);

        curY += titleSize.Y + 6f * scale;
        Typography.DrawWrappedLeft(new Vector2(min.X + padX, curY), listing.Comment, theme.TextMuted,TextStyles.Footnote, contentWidth);

        curY += commentHeight + 8f * scale;
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
