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

    private readonly ChipRail categoryRail = new();
    private readonly ChipRail kindRail = new();

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
    };

    private readonly AppSkin ui = new(AppPalettes.Calculator);
    private readonly ViewRouter<RecruitScreen> router;
    private readonly RouterDraw<RecruitScreen> drawView;

    private RecruitListing? selectedListing;
    private ContentCategory selectedCategory = ContentCategory.Ultimate;
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
        AppHeader.Draw(context, DisplayName);

        using (AppSurface.Begin(area))
        {
            var width = ScrollLayout.StableContentWidth();
            var accent = Accent;
            
            ImGui.Dummy(new Vector2(0f, (AppHeader.Height + 4f) * scale));
            DrawCategoryChips(width, scale, theme, accent);
            ImGui.Dummy(new Vector2(0f, 6f * scale));

            DrawKindChips(width, scale, theme, accent);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            var renderedCount = 0;

            var listings = store.Listings;
            for (var listingIndex = 0; listingIndex < listings.Count; listingIndex++){

                var listing = listings[listingIndex];
                if (listing.Duty.Category != selectedCategory){
                    continue;
                }

                if (selectedKind.HasValue && listing.Kind != selectedKind.Value){
                    continue;
                }

                DrawListingCard(width, scale, theme, accent, listing);
                ImGui.Dummy(new Vector2(0f, 12f * scale));
                renderedCount++;
            }

            if (renderedCount == 0){
                Typography.DrawCentered(ImGui.GetWindowDrawList(),
                    ImGui.GetCursorScreenPos() + new Vector2(width * 0.5f, 40f * scale),
                    "No listings match your filter", theme.TextMuted, TextStyles.Body);
            }
        }

        if (ComposeFab.Draw(area, "##recruitCreateFab", Accent, IconGlyph.Of(FontAwesomeIcon.Plus), "New Listing")){
            router.Push(RecruitScreen.Create);
        }
    }


    private void DrawCategoryChips(float width, float scale, PhoneTheme theme, Vector4 accent)
    {
        Span<bool> active = stackalloc bool[AllCategories.Length];

        for (var categoryIndex = 0; categoryIndex < AllCategories.Length; categoryIndex++){
            active[categoryIndex] = AllCategories[categoryIndex] == selectedCategory;
        }

        var tappedCategoryIndex = categoryRail.Draw(ui, CategoryLabels, active);
        if (tappedCategoryIndex >= 0){
            selectedCategory = AllCategories[tappedCategoryIndex];
        }
    }

    private void DrawKindChips(float width, float scale, PhoneTheme theme, Vector4 accent)
    {
        Span<bool> active = stackalloc bool[4];
        active[0] = !selectedKind.HasValue;
        active[1] = selectedKind == ListingKind.PlayerLfg;
        active[2] = selectedKind == ListingKind.StaticLfm;
        active[3] = selectedKind == ListingKind.SingleNightFill;

        var tappedKindIndex = kindRail.Draw(ui, KindLabels, active);
        if (tappedKindIndex >= 0){
            selectedKind = tappedKindIndex switch{
                1 => ListingKind.PlayerLfg,
                2 => ListingKind.StaticLfm,
                3 => ListingKind.SingleNightFill,
                _ => null,
            };
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

    private static Vector4 KindBadgeColor(ListingKind kind)
    {
        return kind switch{
            ListingKind.StaticLfm => new Vector4(0.706f, 0.529f, 0.910f, 1.0f),
            ListingKind.PlayerLfg => new Vector4(0.902f, 0.878f, 0.549f, 1.0f),
            ListingKind.SingleNightFill => new Vector4(0.780f, 0.522f, 0.412f, 1.0f),
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
