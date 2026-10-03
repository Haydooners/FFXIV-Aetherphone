using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Polls;

internal sealed partial class PollsApp : IPhoneApp
{
    private const float RefreshSeconds = 60f;
    private const float SegmentHeight = 32f;
    private const float SegmentGap = 14f;
    private const float CardGap = 14f;
    private const float BannerHeight = 26f;
    private const float BottomPad = 24f;
    private const int SkeletonCards = 2;
    private const int SkeletonOptions = 3;
    private const int OpenTab = 0;
    private const int EndedTab = 1;

    public string Id => "polls";
    public string DisplayName => Loc.T(L.Apps.Polls);
    public string Glyph => "Po";
    public int BadgeCount => store.UnvotedCount;
    public bool HasBadge => true;

    private readonly PollsStore store;
    private readonly AppSkin ui = new(AppPalettes.Polls);
    private readonly PullToRefresh listRefresh = new();
    private readonly Dictionary<string, PollMotion> motions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PollText> texts = new(StringComparer.Ordinal);
    private readonly List<PollDto> openPolls = new();
    private readonly List<PollDto> endedPolls = new();
    private readonly List<PollDto> combinedPolls = new();
    private readonly HashSet<string> keepOpen = new(StringComparer.Ordinal);
    private readonly string[] tabLabels = new string[2];
    private readonly Action refreshAll;

    private PollDto[]? partitionOpenSource;
    private PollDto[]? partitionEndedSource;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private PollVoteFailure? announcedFailure;
    private string widestPercent = string.Empty;
    private string widestPercentLang = string.Empty;
    private float sinceRefresh;
    private int activeTab;

    public PollsApp(AethernetSession session, PollsClient client, AppInstaller installer, RealtimeSignalBus signals)
    {
        store = new PollsStore(session, client, installer.Gate("polls"), signals);
        refreshAll = RefreshAll;
    }

    public void OnOpened()
    {
        sinceRefresh = 0f;
        activeTab = OpenTab;
        ResetPartition();
        store.Refresh();
    }

    private void RefreshAll()
    {
        ResetPartition();
        store.Refresh();
    }

    private void ResetPartition()
    {
        keepOpen.Clear();
        partitionOpenSource = null;
        partitionEndedSource = null;
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;

        var area = context.Content;
        var scale = UiScale.Current;
        ui.Backdrop(SceneChrome.ScreenFrom(area, theme, scale));
        ui.Body(area);

        var navBar = AppHeader.BeginLargeTitle(context);
        var body = navBar.Body;
        if (store.IsSignedIn)
        {
            DrawSignedIn(body, scale);
        }
        else
        {
            TourHolds.Hold(Id);
            DrawSignedOut(body);
        }

        AppHeader.EndLargeTitle(in navBar, context, "polls.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawSignedOut(Rect body)
    {
        using (AppSurface.Begin(body))
        {
            if (PollsArt.StateScreen(body, ui, FontAwesomeIcon.UserLock, Loc.T(L.Polls.SignInTitle),
                    Loc.T(L.Polls.SignInRequired), Loc.T(L.Polls.OpenSettings)))
            {
                navigation.Open("settings");
            }
        }
    }

    private void DrawSignedIn(Rect body, float scale)
    {
        TickRefresh();
        SyncPartition();
        AnnounceFailure();
        var showTabs = store.EndedSupported;
        if (!showTabs)
        {
            activeTab = OpenTab;
        }

        using var surface = AppSurface.Begin(body);
        listRefresh.Draw(body, surface.Pull, surface.Dragging, store.Loading || store.EndedLoading, ui.MutedInk,
            refreshAll);
        if (showTabs)
        {
            DrawTabs(scale);
        }

        if (!store.LoadedOnce)
        {
            TourHolds.Hold(Id);
            if (store.ListFailed && !store.Loading)
            {
                DrawLoadFailed(body, false);
                return;
            }

            DrawSkeletons(scale);
            return;
        }

        if (activeTab == EndedTab)
        {
            TourHolds.Hold(Id);
            DrawEndedTab(body, scale);
            return;
        }

        if (store.ListFailed)
        {
            DrawRefreshBanner(scale);
        }

        var list = showTabs ? openPolls : combinedPolls;
        if (list.Count == 0)
        {
            TourHolds.Hold(Id);
            DrawEmpty(body, false);
            return;
        }

        TourHolds.Release(Id);
        DrawCards(list, scale, true);
        if (store.LoadingMore)
        {
            InfiniteScroll.DrawLoadingRow(body.Center.X, ui.MutedInk);
        }
        else if (store.HasMore && InfiniteScroll.ReachedBottom())
        {
            store.LoadMore();
        }

        ImGui.Dummy(new Vector2(0f, BottomPad * scale));
    }

    private void DrawEndedTab(Rect body, float scale)
    {
        store.EnsureEnded();
        if (endedPolls.Count == 0)
        {
            if (store.EndedLoading || (!store.EndedLoadedOnce && !store.EndedFailed))
            {
                DrawSkeletons(scale);
                return;
            }

            if (store.EndedFailed)
            {
                DrawLoadFailed(body, true);
                return;
            }

            DrawEmpty(body, true);
            return;
        }

        if (store.EndedFailed)
        {
            DrawRefreshBanner(scale);
        }

        DrawCards(endedPolls, scale, false);
        if (store.EndedLoadingMore)
        {
            InfiniteScroll.DrawLoadingRow(body.Center.X, ui.MutedInk);
        }
        else if (store.EndedHasMore && InfiniteScroll.ReachedBottom())
        {
            store.LoadMoreEnded();
        }

        ImGui.Dummy(new Vector2(0f, BottomPad * scale));
    }

    private void DrawCards(List<PollDto> list, float scale, bool tourTab)
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var width = ImGui.GetContentRegionAvail().X;
        for (var index = 0; index < list.Count; index++)
        {
            DrawCard(list[index], width, scale, nowUnix, tourTab && index == 0);
        }
    }

    private void DrawTabs(float scale)
    {
        var top = ImGui.GetCursorScreenPos();
        var row = new Rect(top, new Vector2(top.X + ImGui.GetContentRegionAvail().X, top.Y + SegmentHeight * scale));
        UiAnchors.Report("polls.tab.ended", new Rect(new Vector2(row.Center.X, row.Min.Y), row.Max));
        tabLabels[OpenTab] = Loc.T(L.Polls.Open);
        tabLabels[EndedTab] = Loc.T(L.Polls.Ended);
        var selected = SegmentStrip.Draw("polls.tabs", row, tabLabels, activeTab, ui.Palette, SegmentHeight);
        if (selected != activeTab)
        {
            activeTab = selected;
            ResetPartition();
            SyncPartition();
            UiFeedback.Play(UiSound.Tap);
        }

        ImGui.Dummy(new Vector2(0f, (SegmentHeight + SegmentGap) * scale - ImGui.GetStyle().ItemSpacing.Y));
    }

    private void DrawSkeletons(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var height = PollsArt.SkeletonHeight(SkeletonOptions, scale);
        var phase = (float)ImGui.GetTime();
        for (var index = 0; index < SkeletonCards; index++)
        {
            var origin = ImGui.GetCursorScreenPos();
            PollsArt.SkeletonCard(drawList, ui, origin, width, height, SkeletonOptions, scale, phase);
            ImGui.Dummy(new Vector2(width, height + CardGap * scale - ImGui.GetStyle().ItemSpacing.Y));
        }
    }

    private void DrawLoadFailed(Rect body, bool ended)
    {
        var reason = ended ? store.EndedFailureText : store.ListFailureText;
        if (!PollsArt.StateScreen(body, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Common.LoadFailed),
                reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint), Loc.T(L.Common.Retry)))
        {
            return;
        }

        UiFeedback.Play(UiSound.Refresh);
        RefreshAll();
    }

    private void DrawRefreshBanner(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = BannerHeight * scale;
        var centerY = origin.Y + height * 0.5f;
        var iconSize = Typography.LineHeight(TextStyles.Footnote) * 0.7f;
        PollsArt.Glyph(drawList, new Vector2(origin.X + iconSize * 0.5f, centerY), FontAwesomeIcon.ExclamationCircle,
            ui.MutedInk, iconSize);
        var text = Loc.T(L.Polls.RefreshFailed);
        var textLeft = origin.X + iconSize + Metrics.Space.Xs * scale;
        var fitted = Typography.FitText(text, ImGui.GetContentRegionAvail().X - (textLeft - origin.X),
            TextStyles.Footnote);
        var textHeight = Typography.Measure(fitted, TextStyles.Footnote).Y;
        Typography.Draw(drawList, new Vector2(textLeft, centerY - textHeight * 0.5f), fitted, ui.MutedInk,
            TextStyles.Footnote.Scale, TextStyles.Footnote.Weight);
        ImGui.Dummy(new Vector2(0f, height));
    }

    private void DrawEmpty(Rect body, bool ended)
    {
        PollsArt.StateScreen(body, ui, ended ? FontAwesomeIcon.Flag : FontAwesomeIcon.CheckSquare,
            Loc.T(ended ? L.Polls.EmptyEndedTitle : L.Polls.EmptyOpenTitle),
            Loc.T(ended ? L.Polls.EmptyEndedHint : L.Polls.EmptyOpenHint), string.Empty);
    }

    private void TickRefresh()
    {
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh < RefreshSeconds || store.Loading)
        {
            return;
        }

        sinceRefresh = 0f;
        store.Refresh();
    }

    private void SyncPartition()
    {
        var openSource = store.Polls;
        var endedSource = store.Ended;
        if (ReferenceEquals(openSource, partitionOpenSource) && ReferenceEquals(endedSource, partitionEndedSource))
        {
            return;
        }

        partitionOpenSource = openSource;
        partitionEndedSource = endedSource;
        PollRules.Partition(openSource, endedSource, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), keepOpen, openPolls,
            endedPolls);
        combinedPolls.Clear();
        combinedPolls.AddRange(openPolls);
        combinedPolls.AddRange(endedPolls);
        for (var index = 0; index < openPolls.Count; index++)
        {
            keepOpen.Add(openPolls[index].Id);
        }
    }

    private void AnnounceFailure()
    {
        var failure = store.VoteFailure;
        if (failure is null || ReferenceEquals(failure, announcedFailure))
        {
            return;
        }

        announcedFailure = failure;
        UiFeedback.Play(UiSound.Caution);
    }

    private string WidestPercent()
    {
        var code = Loc.Current.Code;
        if (widestPercentLang != code)
        {
            widestPercentLang = code;
            widestPercent = Loc.T(L.Polls.Percent, 100);
        }

        return widestPercent;
    }

    private PollText TextFor(PollDto poll, long nowUnix)
    {
        if (!texts.TryGetValue(poll.Id, out var text))
        {
            text = new PollText();
            texts[poll.Id] = text;
        }

        text.Sync(poll, nowUnix);
        return text;
    }

    private PollMotion MotionFor(PollDto poll)
    {
        if (!motions.TryGetValue(poll.Id, out var motion))
        {
            motion = new PollMotion();
            motions[poll.Id] = motion;
        }

        if (motion.Fills.Length != poll.Options.Length)
        {
            motion.Fills = new Spring[poll.Options.Length];
            motion.Selection = new Spring[poll.Options.Length];
            motion.Primed = false;
        }

        return motion;
    }

    public void Dispose()
    {
        store.Dispose();
    }
}
