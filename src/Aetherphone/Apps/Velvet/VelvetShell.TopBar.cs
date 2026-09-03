using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float TopBarIconSize = 26f;
    private const float LogoSize = 30f;
    private const float LogoGap = 10f;
    private const float FeedTabRowHeight = 44f;
    private const float FeedTabUnderline = 2f;
    private const float FeedTabSmoothTime = 0.09f;

    private static readonly TextStyle WordmarkStyle = new(1.4f, FontWeight.Bold);
    private static readonly TextStyle FeedTabStyle = new(1.07f, FontWeight.SemiBold);
    private static readonly TextStyle FeedTabIdleStyle = new(1.07f, FontWeight.Medium);
    private static readonly UnderlineTabStyle FeedTabsStyle = new(FeedTabStyle, FeedTabIdleStyle,
        VelvetTheme.TitleInk, VelvetTheme.MutedInk, VelvetTheme.Rose, FeedTabUnderline, SocialChrome.CellPadX,
        FeedTabSmoothTime);

    private Spring feedTabSlide;

    private void DrawHomeTopBar(Rect area, bool showFilters)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + VHeader.Height * scale * 0.5f;
        var logoSize = LogoSize * scale;
        var logoCenter = new Vector2(area.Min.X + SocialChrome.CellPadX * scale + logoSize * 0.5f, rowCenterY);
        if (!AppIconTextures.TryDrawArtwork(drawList, Id, logoCenter, logoSize, VelvetTheme.RoseInk))
        {
            PhoneIcon.Draw(drawList, logoCenter, PhoneIcons.Moon, VelvetTheme.RoseInk, logoSize);
        }

        var titleLeft = logoCenter.X + logoSize * 0.5f + LogoGap * scale;
        var titleRight = SocialChrome.HeaderSlot(area, showFilters ? 1 : 0).X
            - SocialChrome.HeaderIconRadius * scale - 8f * scale;
        var titleHeight = Typography.LineHeight(WordmarkStyle);
        var title = Typography.FitText(DisplayName, MathF.Max(1f, titleRight - titleLeft), WordmarkStyle);
        var titleSize = Typography.Measure(title, WordmarkStyle);
        var titleMin = new Vector2(titleLeft - 6f * scale, rowCenterY - titleHeight * 0.5f - 4f * scale);
        var titleMax = new Vector2(titleLeft + titleSize.X + 6f * scale, rowCenterY + titleHeight * 0.5f + 4f * scale);
        UiInteract.HoverHighlight(drawList, titleMin, titleMax, 8f * scale);
        Typography.Draw(drawList, new Vector2(titleLeft, rowCenterY - titleHeight * 0.5f), title, VelvetTheme.TitleInk,
            WordmarkStyle);
        if (UiInteract.HoverClick(titleMin, titleMax))
        {
            RefreshFeed();
        }

        if (store.LoadingFeed)
        {
            LoadingPulse.Spinner(new Vector2(titleMax.X + 12f * scale, rowCenterY), 7f * scale, VelvetTheme.RoseInk);
        }

        if (showFilters && SocialChrome.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 1),
                SocialChrome.HeaderIconRadius * scale, PhoneIcons.AdjustmentsHorizontal, TopBarIconSize,
                Loc.T(L.Velvet.FiltersTitle), VelvetInk.Shared, VelvetTheme.TitleInk,
                IncludeFor(activeTab).Any || mutes.Any))
        {
            OpenFilters(activeTab);
        }

        var activityCenter = SocialChrome.HeaderSlot(area, 0);
        UiAnchors.Report("velvet.activity", AnchorBox(activityCenter, 18f * scale));
        if (SocialChrome.DrawHeaderIcon(drawList, activityCenter, SocialChrome.HeaderIconRadius * scale,
                PhoneIcons.Heart, TopBarIconSize, Loc.T(L.Velvet.Activity), VelvetInk.Shared, VelvetTheme.TitleInk,
                false, social.UnseenCount(Id)))
        {
            activityFeed.Invalidate();
            router.Push(VelvetView.Activity);
        }
    }

    private Rect DrawFeedScopeTabs(Rect area)
    {
        var scale = UiScale.Current;
        var row = new Rect(new Vector2(area.Min.X, area.Min.Y),
            new Vector2(area.Max.X, area.Min.Y + FeedTabRowHeight * scale));
        var picked = UnderlineTabs.Draw(row, Loc.T(L.Velvet.FeedScopeAll), Loc.T(L.Velvet.FeedScopeConnections),
            store.FeedScope == VelvetFeedScope.Connections, ref feedTabSlide, VelvetInk.Shared, FeedTabsStyle);
        if (picked >= 0)
        {
            var scope = picked == 1 ? VelvetFeedScope.Connections : VelvetFeedScope.All;
            if (scope != store.FeedScope)
            {
                store.SetFeedScope(scope);
                feedScrollTopPending = true;
            }
        }

        return new Rect(new Vector2(area.Min.X, row.Max.Y), area.Max);
    }
}
