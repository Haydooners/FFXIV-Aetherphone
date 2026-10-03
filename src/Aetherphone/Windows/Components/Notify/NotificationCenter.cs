using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Input;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class NotificationCenter
{
    private const float CardGap = 10f;
    private const float GroupGap = 14f;
    private const float StackOffsetY = 6f;
    private const float StackScaleStep = 0.94f;
    private const float StackAlphaStep = 0.78f;
    private const float MoreLabelHeight = 14f;
    private const float HeaderHeight = 32f;
    private const float HeaderPad = 6f;
    private const float SummaryHeight = 36f;
    private const float SummaryGap = 10f;
    private const float SummaryPadX = 16f;
    private const float EmptyHeight = 72f;
    private const float WheelStep = 48f;
    private const float RevealWidth = 84f;
    private const float RevealGap = 8f;
    private const float RevealOpenFraction = 0.5f;
    private const float RevealHitFraction = 0.6f;
    private const float SwipeRightClamp = 10f;
    private const float SwipeCommitFraction = 0.42f;
    private const float SlideOutOvershoot = 40f;
    private const float SlideRestDistance = 2f;
    private const float TapSlop = 10f;
    private const float FailedSwipeTapFraction = 0.15f;
    private const float DragAxisThreshold = 6f;
    private const float ExpandSmoothTime = 0.20f;
    private const float SlideSmoothTime = 0.16f;
    private const float PillHeight = 26f;
    private const float PillPadX = 12f;
    private const float PillGap = 8f;
    private const float ChevronReach = 4f;
    private const float ChevronGap = 10f;
    private const float ChevronThickness = 1.6f;
    private const float HoverLift = 0.35f;
    private const string TitleMarquee = "notificationcenter.title.";
    private const string BodyMarquee = "notificationcenter.body.";
    private const string SummarySeparator = "  ·  ";
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly float[] StackScales = { 1f, StackScaleStep, StackScaleStep * StackScaleStep };
    private static readonly float[] StackAlphas = { 1f, StackAlphaStep, StackAlphaStep * StackAlphaStep };

    private readonly NotificationService notifications;
    private readonly NotificationRouter router;
    private readonly Action? navigated;
    private readonly NotificationGroups groups = new();
    private readonly Dictionary<string, GroupState> states = new(StringComparer.Ordinal);
    private readonly List<string> staleKeys = new();
    private readonly List<string> moreLabels = new();
    private readonly List<Candidate> candidates = new();
    private readonly DragTracker drag = new();
    private readonly KineticScroller scroller = new();
    private int builtVersion = -1;
    private LanguageInfo? builtLanguage;
    private string summaryLabel = string.Empty;
    private int summaryCount = -1;
    private long summaryAgeMinutes = -1;
    private LanguageInfo? summaryLanguage;
    private Rect interactionBounds;
    private float scrollY;
    private bool scrollGesture;
    private bool axisLocked;
    private bool hasDragTarget;
    private Candidate dragTarget;
    private float dragBase;
    private float swipeOffset;
    private bool slideActive;
    private bool slideRemoving;
    private Target slideTarget;
    private PhoneNotification? slideNotification;
    private float slideGoal;
    private Spring slide;
    private Rect clearButton;
    private bool clearButtonVisible;
    private bool overChild;

    public NotificationCenter(NotificationService notifications, NotificationRouter router, Action? navigated = null)
    {
        this.notifications = notifications;
        this.router = router;
        this.navigated = navigated;
    }

    public void Reset()
    {
        drag.Cancel();
        hasDragTarget = false;
        swipeOffset = 0f;
        scrollY = 0f;
        scroller.Reset();
        scrollGesture = false;
        axisLocked = false;
        slideActive = false;
        slideRemoving = false;
        slideNotification = null;
        clearButtonVisible = false;
        states.Clear();
        builtVersion = -1;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        DrawCore(ImGui.GetWindowDrawList(), body, context.Theme, scale, Metrics.Space.Lg * scale, 1f, true);
    }

    public void DrawOverlay(ImDrawListPtr drawList, Rect area, PhoneTheme theme, float opacity, bool interactive)
    {
        DrawCore(drawList, area, theme, UiScale.Current, 0f, opacity, interactive);
    }

    public float MeasureHeight(float scale)
    {
        EnsureBuilt();
        if (groups.Count == 0)
        {
            return EmptyHeight * scale;
        }

        return (SummaryHeight + SummaryGap) * scale + ContentHeight(scale);
    }

    private void DrawCore(ImDrawListPtr drawList, Rect body, PhoneTheme theme, float scale, float inset, float opacity,
        bool interactive)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        EnsureBuilt();
        overChild = false;
        if (groups.Count == 0)
        {
            Typography.DrawCentered(drawList, body.Center, Loc.T(L.Notifications.Empty),
                Palette.WithAlpha(theme.TextMuted, opacity));
        }
        else
        {
            var summary = new Rect(new Vector2(body.Min.X + inset, body.Min.Y),
                new Vector2(body.Max.X - inset, body.Min.Y + SummaryHeight * scale));
            DrawSummary(drawList, summary, theme, scale, opacity, interactive);
            var listArea = new Rect(new Vector2(body.Min.X + inset, summary.Max.Y + SummaryGap * scale),
                new Vector2(body.Max.X - inset, body.Max.Y));
            if (listArea.Height > 1f && listArea.Width > 1f)
            {
                DrawList(drawList, theme, listArea, scale, opacity, interactive);
            }
        }

        AdvanceAnimations(delta);
    }

    private void EnsureBuilt()
    {
        var language = Loc.Current;
        if (builtVersion == notifications.Version && ReferenceEquals(builtLanguage, language))
        {
            return;
        }

        builtVersion = notifications.Version;
        builtLanguage = language;
        groups.Rebuild(notifications.Recent);
        moreLabels.Clear();
        var list = groups.Groups;
        for (var index = 0; index < list.Count; index++)
        {
            var group = list[index];
            moreLabels.Add(group.Count > 1 ? Loc.T(L.Notifications.More, group.HiddenCount) : string.Empty);
        }

        SyncStates();
        if (slideActive && !slideRemoving)
        {
            slideActive = false;
            clearButtonVisible = false;
        }

        summaryCount = -1;
    }

    private void SyncStates()
    {
        foreach (var state in states.Values)
        {
            state.Seen = false;
        }

        var list = groups.Groups;
        for (var index = 0; index < list.Count; index++)
        {
            var group = list[index];
            if (!states.TryGetValue(group.Key, out var state))
            {
                state = new GroupState();
                states[group.Key] = state;
            }

            if (group.Count < 2)
            {
                state.Expanded = false;
            }

            state.Seen = true;
        }

        staleKeys.Clear();
        foreach (var pair in states)
        {
            if (!pair.Value.Seen)
            {
                staleKeys.Add(pair.Key);
            }
        }

        for (var index = 0; index < staleKeys.Count; index++)
        {
            states.Remove(staleKeys[index]);
        }
    }

    private void AdvanceAnimations(float delta)
    {
        foreach (var state in states.Values)
        {
            var target = state.Expanded ? 1f : 0f;
            state.Expand.Step(target, ExpandSmoothTime, delta);
            if (state.Expand.IsResting(target, TransitionTiming.RestPositionEpsilon,
                    TransitionTiming.RestVelocityEpsilon))
            {
                state.Expand.SnapTo(target);
            }
        }

        if (!slideActive)
        {
            return;
        }

        slide.Step(slideGoal, SlideSmoothTime, delta);
        if (slideRemoving)
        {
            if (slide.Value <= slideGoal + SlideRestDistance)
            {
                PerformRemoval();
                slideActive = false;
                slideRemoving = false;
            }

            return;
        }

        if (slideGoal == 0f && slide.IsResting(0f, 0.4f, 2f))
        {
            slide.SnapTo(0f);
            slideActive = false;
            clearButtonVisible = false;
        }
    }

    private void PerformRemoval()
    {
        if (slideNotification is { } dismissed)
        {
            router.Acknowledge(dismissed);
            slideNotification = null;
        }

        if (slideTarget.IsGroup)
        {
            notifications.RemoveGroup(slideTarget.Key);
        }
        else
        {
            notifications.Remove(slideTarget.Id);
        }
    }

    private void DrawSummary(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, float scale, float opacity,
        bool interactive)
    {
        Material.LiquidGlass(drawList, rect.Min, rect.Max, rect.Height * 0.5f, scale, GlassTone.Dark, 0f, opacity);
        var padX = SummaryPadX * scale;
        var clearLabel = Loc.T(L.Notifications.ClearAll);
        var clearSize = Typography.Measure(clearLabel, TextStyles.FootnoteEmphasized);
        var clearMin = new Vector2(rect.Max.X - padX - clearSize.X - PillPadX * scale, rect.Min.Y);
        var clearMax = rect.Max;
        var clearHovered = interactive && UiInteract.Hover(clearMin, clearMax);
        var accent = clearHovered ? Palette.Mix(theme.Accent, White, HoverLift) : theme.Accent;
        Typography.Draw(drawList, new Vector2(rect.Max.X - padX - clearSize.X, rect.Center.Y - clearSize.Y * 0.5f),
            clearLabel, Palette.WithAlpha(accent, opacity), TextStyles.FootnoteEmphasized);
        var textLeft = rect.Min.X + padX;
        var textMaxWidth = MathF.Max(1f, clearMin.X - PillGap * scale - textLeft);
        var summary = Typography.FitText(SummaryLabel(), textMaxWidth, TextStyles.Footnote);
        var summarySize = Typography.Measure(summary, TextStyles.Footnote);
        var muted = NotificationCard.MutedInk(GlassTone.Dark, theme);
        Typography.Draw(drawList, new Vector2(textLeft, rect.Center.Y - summarySize.Y * 0.5f), summary,
            Palette.WithAlpha(muted, muted.W * opacity), TextStyles.Footnote);
        if (!interactive)
        {
            return;
        }

        if (clearHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(clearMin, clearMax, clearHovered))
        {
            ClearAll();
        }
    }

    private string SummaryLabel()
    {
        var count = groups.TotalCount;
        var oldest = groups.OldestReceivedAt;
        var ageMinutes = oldest == default ? -1L : (long)Math.Max(0d, (DateTime.Now - oldest).TotalMinutes);
        var language = Loc.Current;
        if (count == summaryCount && ageMinutes == summaryAgeMinutes && ReferenceEquals(language, summaryLanguage))
        {
            return summaryLabel;
        }

        summaryCount = count;
        summaryAgeMinutes = ageMinutes;
        summaryLanguage = language;
        var waiting = Loc.Plural(L.Notifications.Waiting, count);
        summaryLabel = oldest == default
            ? waiting
            : string.Concat(waiting, SummarySeparator,
                Loc.T(L.Notifications.Oldest, TimeText.Ago(oldest.ToUniversalTime())));
        return summaryLabel;
    }

    private void ClearAll()
    {
        router.AcknowledgeAll();
        notifications.Clear();
        Reset();
    }

    private void ClearGroup(NotificationGroup group)
    {
        router.Acknowledge(group.Newest);
        notifications.RemoveGroup(group.Key);
    }

    private float ContentHeight(float scale)
    {
        var total = 0f;
        var list = groups.Groups;
        for (var index = 0; index < list.Count; index++)
        {
            if (!states.TryGetValue(list[index].Key, out var state))
            {
                continue;
            }

            total += BlockHeight(list[index].Count, state.Expand.Value, scale) + GroupGap * scale;
        }

        return total;
    }

    private void DrawList(ImDrawListPtr drawList, PhoneTheme theme, Rect listArea, float scale, float opacity,
        bool interactive)
    {
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var maxScroll = MathF.Max(0f, ContentHeight(scale) - listArea.Height);
        scroller.Scale = scale;
        scroller.SetBounds(maxScroll);

        if (drag.Active)
        {
            var delta = drag.Delta;
            if (!axisLocked && (MathF.Abs(delta.X) >= DragAxisThreshold * scale ||
                MathF.Abs(delta.Y) >= DragAxisThreshold * scale))
            {
                axisLocked = true;
                scrollGesture = MathF.Abs(delta.Y) > MathF.Abs(delta.X);
            }

            if (scrollGesture)
            {
                scroller.Move(ImGui.GetMousePos().Y, deltaSeconds);
                swipeOffset = dragBase;
            }
            else if (hasDragTarget)
            {
                swipeOffset = Math.Clamp(dragBase + delta.X, -dragTarget.Width, SwipeRightClamp * scale);
            }
        }
        else
        {
            scroller.Tick(deltaSeconds);
            if (interactive && UiInteract.HoverWindowOnly(listArea.Min, listArea.Max, false))
            {
                var wheel = ImGui.GetIO().MouseWheel;
                if (wheel != 0f)
                {
                    var basis = scroller.IsControlling ? scroller.Offset : scrollY;
                    scrollY = Math.Clamp(basis - wheel * WheelStep * scale, 0f, maxScroll);
                    scroller.Reset();
                    scroller.SetBounds(maxScroll);
                    scroller.SyncOffset(scrollY);
                }
            }
        }

        if (scroller.IsControlling)
        {
            scrollY = scroller.Offset;
        }
        else
        {
            scroller.SyncOffset(Math.Clamp(scrollY, 0f, maxScroll));
        }

        scrollY = Math.Clamp(scrollY, 0f, maxScroll);
        interactionBounds = listArea;
        if (interactive && (drag.Active || UiInteract.HoverWindowOnly(listArea.Min, listArea.Max, false)))
        {
            UiInteract.ReportGestureSurface();
        }

        candidates.Clear();
        drawList.PushClipRect(listArea.Min, listArea.Max, true);
        var y = listArea.Min.Y - scrollY;
        var list = groups.Groups;
        for (var index = 0; index < list.Count; index++)
        {
            var group = list[index];
            if (!states.TryGetValue(group.Key, out var state))
            {
                continue;
            }

            var progress = state.Expand.Value;
            var blockHeight = BlockHeight(group.Count, progress, scale);
            if (y + blockHeight >= listArea.Min.Y && y <= listArea.Max.Y)
            {
                DrawGroup(drawList, group, index, state, new Vector2(listArea.Min.X, y), listArea.Width, progress,
                    theme, scale, opacity, interactive);
            }

            y += blockHeight + GroupGap * scale;
        }

        drawList.PopClipRect();
        if (interactive && clearButtonVisible && slideActive && !slideRemoving && !drag.Active &&
            UiInteract.ClickedOutside(clearButton.Min, clearButton.Max, false))
        {
            slideGoal = 0f;
        }

        HandleGesture(scale, interactive);
    }

    private void DrawGroup(ImDrawListPtr drawList, NotificationGroup group, int groupIndex, GroupState state,
        Vector2 origin, float width, float progress, PhoneTheme theme, float scale, float opacity, bool interactive)
    {
        var count = group.Count;
        var cardHeight = NotificationCard.Height * scale;
        var layers = group.VisibleLayers;
        var collapsedHit = progress < 0.5f;
        var stacked = count > 1;
        var blockBottom = origin.Y + CollapsedHeight(count, scale);
        for (var index = count - 1; index >= 0; index--)
        {
            var collapsedRect = CollapsedRect(index, layers, origin, width, cardHeight, scale);
            var expandedTop = origin.Y + HeaderHeight * scale + index * (cardHeight + CardGap * scale);
            var expandedRect = new Rect(new Vector2(origin.X, expandedTop),
                new Vector2(origin.X + width, expandedTop + cardHeight));
            var rect = stacked ? Lerp(collapsedRect, expandedRect, progress) : collapsedRect;
            var glassAlpha = float.Lerp(CollapsedAlpha(index, layers), 1f, progress) * opacity;
            if (glassAlpha <= 0.01f)
            {
                continue;
            }

            var contentAlpha = index == 0 ? opacity : progress * opacity;
            var hittable = interactive && (collapsedHit ? index == 0 : progress > 0.5f);
            var actsOnGroup = collapsedHit && stacked;
            var notification = group.Items[index];
            var candidate = new Candidate(rect, actsOnGroup, group.Key, notification.Id, width, notification,
                actsOnGroup);
            var slideOffset = SlideFor(candidate);
            if (slideOffset < -1f && hittable)
            {
                DrawClearAction(drawList, rect, slideOffset, candidate, theme, scale, glassAlpha, interactive);
            }

            var drawRect = rect.Translate(new Vector2(slideOffset, 0f));
            NotificationCard.DrawGlass(drawList, drawRect, scale, glassAlpha, GlassTone.Dark);
            if (contentAlpha > 0.01f)
            {
                NotificationCard.DrawContent(drawList, drawRect, notification, theme, scale, contentAlpha,
                    GlassTone.Dark, false, TitleMarquee, BodyMarquee);
            }

            if (!hittable)
            {
                continue;
            }

            var hitRect = actsOnGroup
                ? new Rect(drawRect.Min, new Vector2(drawRect.Max.X, MathF.Max(drawRect.Max.Y, blockBottom)))
                : drawRect;
            candidates.Add(candidate with { Rect = hitRect });
        }

        if (!stacked)
        {
            return;
        }

        if (progress < 0.99f)
        {
            DrawMoreLabel(drawList, groupIndex, origin, width, layers, cardHeight, theme, scale,
                (1f - progress) * opacity);
        }

        if (progress > 0.01f)
        {
            var header = new Rect(origin, new Vector2(origin.X + width, origin.Y + HeaderHeight * scale));
            DrawHeader(drawList, group, state, header, theme, scale, progress * opacity,
                interactive && state.Expanded && progress > 0.5f);
        }
    }

    private void DrawMoreLabel(ImDrawListPtr drawList, int groupIndex, Vector2 origin, float width, int layers,
        float cardHeight, PhoneTheme theme, float scale, float alpha)
    {
        if (groupIndex >= moreLabels.Count || alpha <= 0.01f)
        {
            return;
        }

        var label = moreLabels[groupIndex];
        if (label.Length == 0)
        {
            return;
        }

        var top = origin.Y + cardHeight + (layers - 1) * StackOffsetY * scale;
        var center = new Vector2(origin.X + width * 0.5f, top + MoreLabelHeight * scale * 0.5f);
        var muted = NotificationCard.MutedInk(GlassTone.Dark, theme);
        Typography.DrawCentered(drawList, center, label, Palette.WithAlpha(muted, muted.W * alpha),
            TextStyles.Caption2);
    }

    private void DrawHeader(ImDrawListPtr drawList, NotificationGroup group, GroupState state, Rect rect,
        PhoneTheme theme, float scale, float alpha, bool interactive)
    {
        var pad = HeaderPad * scale;
        var lessLabel = Loc.T(L.Notifications.ShowLess);
        var lessSize = Typography.Measure(lessLabel, TextStyles.Footnote);
        var lessPos = new Vector2(rect.Max.X - pad - lessSize.X, rect.Center.Y - lessSize.Y * 0.5f);
        var accent = Palette.WithAlpha(theme.Accent, alpha);
        Typography.Draw(drawList, lessPos, lessLabel, accent, TextStyles.Footnote);
        var reach = ChevronReach * scale;
        var chevronTip = new Vector2(lessPos.X - ChevronGap * scale, rect.Center.Y - 1f * scale);
        var chevronColor = ImGui.GetColorU32(accent);
        drawList.AddLine(new Vector2(chevronTip.X - reach, chevronTip.Y + reach), chevronTip, chevronColor,
            ChevronThickness * scale);
        drawList.AddLine(chevronTip, new Vector2(chevronTip.X + reach, chevronTip.Y + reach), chevronColor,
            ChevronThickness * scale);

        var pillLabel = Loc.T(L.Notifications.ClearAll);
        var pillSize = Typography.Measure(pillLabel, TextStyles.FootnoteEmphasized);
        var pillHeight = PillHeight * scale;
        var pillMax = new Vector2(chevronTip.X - reach - PillGap * scale, rect.Center.Y + pillHeight * 0.5f);
        var pillMin = new Vector2(pillMax.X - pillSize.X - PillPadX * scale * 2f, rect.Center.Y - pillHeight * 0.5f);
        var pillHovered = interactive && UiInteract.Hover(pillMin, pillMax);
        Material.LiquidGlass(drawList, pillMin, pillMax, pillHeight * 0.5f, scale, GlassTone.Dark, 0f, alpha);
        Typography.DrawCentered(drawList, (pillMin + pillMax) * 0.5f, pillLabel, Palette.WithAlpha(White, alpha),
            TextStyles.FootnoteEmphasized);

        var titleMaxWidth = MathF.Max(1f, pillMin.X - PillGap * scale - (rect.Min.X + pad));
        var title = Typography.FitText(group.Newest.Title, titleMaxWidth, TextStyles.FootnoteEmphasized);
        var titleSize = Typography.Measure(title, TextStyles.FootnoteEmphasized);
        var muted = NotificationCard.MutedInk(GlassTone.Dark, theme);
        Typography.Draw(drawList, new Vector2(rect.Min.X + pad, rect.Center.Y - titleSize.Y * 0.5f), title,
            Palette.WithAlpha(muted, muted.W * alpha), TextStyles.FootnoteEmphasized);
        if (!interactive)
        {
            return;
        }

        if (pillHovered)
        {
            overChild = true;
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(pillMin, pillMax, pillHovered))
        {
            ClearGroup(group);
            return;
        }

        var headerHovered = !pillHovered && UiInteract.Hover(rect.Min, rect.Max);
        if (headerHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, rect.Max, headerHovered))
        {
            state.Expanded = false;
        }
    }

    private void DrawClearAction(ImDrawListPtr drawList, Rect rect, float slideOffset, in Candidate candidate,
        PhoneTheme theme, float scale, float alpha, bool interactive)
    {
        var revealWidth = RevealWidth * scale;
        var revealed = MathF.Min(-slideOffset, revealWidth);
        var gap = RevealGap * scale;
        var buttonMin = new Vector2(rect.Max.X - revealed + gap, rect.Min.Y);
        var buttonMax = rect.Max;
        var buttonWidth = buttonMax.X - buttonMin.X;
        if (buttonWidth <= 1f)
        {
            return;
        }

        var progress = Math.Clamp(revealed / revealWidth, 0f, 1f);
        var rounding = MathF.Min(NotificationCard.Rounding * scale, buttonWidth * 0.5f);
        var hovered = interactive && progress >= RevealHitFraction && !drag.Active &&
                      UiInteract.Hover(buttonMin, buttonMax);
        var fill = hovered ? Palette.Mix(theme.Danger, White, HoverLift * 0.5f) : theme.Danger;
        Squircle.Fill(drawList, buttonMin, buttonMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(fill, progress * alpha)));
        var label = Typography.FitText(Loc.T(L.Notifications.Clear), MathF.Max(1f, buttonWidth - gap * 2f),
            TextStyles.FootnoteEmphasized);
        Typography.DrawCentered(drawList, (buttonMin + buttonMax) * 0.5f, label,
            Palette.WithAlpha(White, progress * alpha), TextStyles.FootnoteEmphasized);
        clearButton = new Rect(buttonMin, buttonMax);
        clearButtonVisible = progress >= RevealHitFraction;
        if (!interactive)
        {
            return;
        }

        if (hovered)
        {
            overChild = true;
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(buttonMin, buttonMax, hovered))
        {
            SlideOut(candidate, scale);
        }
    }

    private void HandleGesture(float scale, bool interactive)
    {
        if (!drag.Active && interactive && !overChild &&
            UiInteract.Hover(interactionBounds.Min, interactionBounds.Max))
        {
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (!UiInteract.Hover(candidate.Rect.Min, candidate.Rect.Max))
                {
                    continue;
                }

                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    SlideOut(candidate, scale);
                    break;
                }

                if (drag.Begin(candidate.Rect))
                {
                    BeginDrag(candidate);
                }

                break;
            }
        }

        if (!drag.Released(out var totalDelta, out _))
        {
            return;
        }

        if (scrollGesture)
        {
            scroller.Release();
            scrollGesture = false;
            axisLocked = false;
            if (hasDragTarget && dragBase < 0f)
            {
                StartSlide(dragTarget, -RevealWidth * scale, false, dragBase);
            }

            hasDragTarget = false;
            return;
        }

        ResolveGesture(totalDelta, scale);
    }

    private void SlideOut(in Candidate candidate, float scale)
    {
        if (slideActive && slideRemoving && slideTarget.Matches(candidate))
        {
            return;
        }

        StartSlide(candidate, -(candidate.Width + SlideOutOvershoot * scale), true, SlideFor(candidate));
        hasDragTarget = false;
    }

    private void BeginDrag(in Candidate candidate)
    {
        if (slideActive && slideRemoving && slideTarget.Matches(candidate))
        {
            drag.Cancel();
            return;
        }

        UiInteract.CancelPendingTap();
        dragTarget = candidate;
        hasDragTarget = true;
        dragBase = slideActive && slideTarget.Matches(candidate) ? slide.Value : 0f;
        if (slideActive && slideTarget.Matches(candidate))
        {
            slideActive = false;
            clearButtonVisible = false;
        }

        swipeOffset = dragBase;
        scroller.Press(ImGui.GetMousePos().Y);
        scrollGesture = false;
        axisLocked = false;
    }

    private void ResolveGesture(Vector2 totalDelta, float scale)
    {
        if (!hasDragTarget)
        {
            return;
        }

        var candidate = dragTarget;
        hasDragTarget = false;
        scroller.CancelGesture();
        var total = dragBase + totalDelta.X;
        var width = candidate.Width;
        var revealWidth = RevealWidth * scale;
        var slop = TapSlop * scale;
        if (total <= -width * SwipeCommitFraction)
        {
            StartSlide(candidate, -(width + SlideOutOvershoot * scale), true, swipeOffset);
            return;
        }

        if (dragBase < 0f && MathF.Abs(totalDelta.X) < slop && MathF.Abs(totalDelta.Y) < slop)
        {
            StartSlide(candidate, 0f, false, swipeOffset);
            return;
        }

        if (total <= -revealWidth * RevealOpenFraction)
        {
            StartSlide(candidate, -revealWidth, false, swipeOffset);
            return;
        }

        var tapped = dragBase == 0f && MathF.Abs(totalDelta.Y) < slop &&
                     MathF.Abs(totalDelta.X) < width * FailedSwipeTapFraction;
        if (tapped)
        {
            swipeOffset = 0f;
            HandleTap(candidate);
            return;
        }

        StartSlide(candidate, 0f, false, swipeOffset);
    }

    private void StartSlide(in Candidate candidate, float goal, bool removing, float from)
    {
        slideTarget = Target.Of(candidate);
        slideNotification = removing ? candidate.Notification : null;
        slideGoal = goal;
        slideRemoving = removing;
        slide.SnapTo(from);
        slideActive = true;
        swipeOffset = 0f;
        clearButtonVisible = false;
    }

    private void HandleTap(in Candidate candidate)
    {
        if (candidate.ExpandsOnTap)
        {
            if (states.TryGetValue(candidate.Key, out var state))
            {
                state.Expanded = true;
            }

            return;
        }

        router.Open(candidate.Notification);
        navigated?.Invoke();
    }

    private float SlideFor(in Candidate candidate)
    {
        if (drag.Active && hasDragTarget && !scrollGesture && Target.Of(dragTarget).Matches(candidate))
        {
            return swipeOffset;
        }

        if (drag.Active && hasDragTarget && scrollGesture && Target.Of(dragTarget).Matches(candidate))
        {
            return dragBase;
        }

        if (slideActive && slideTarget.Matches(candidate))
        {
            return slide.Value;
        }

        return 0f;
    }

    private static Rect CollapsedRect(int index, int layers, Vector2 origin, float width, float cardHeight,
        float scale)
    {
        var layer = Math.Clamp(Math.Min(index, layers - 1), 0, StackScales.Length - 1);
        if (layer == 0)
        {
            return new Rect(origin, origin + new Vector2(width, cardHeight));
        }

        var factor = StackScales[layer];
        var layerWidth = width * factor;
        var layerHeight = cardHeight * factor;
        var bottom = origin.Y + cardHeight + layer * StackOffsetY * scale;
        var left = origin.X + (width - layerWidth) * 0.5f;
        return new Rect(new Vector2(left, bottom - layerHeight), new Vector2(left + layerWidth, bottom));
    }

    private static float CollapsedAlpha(int index, int layers)
    {
        if (index >= layers || index >= StackAlphas.Length)
        {
            return 0f;
        }

        return StackAlphas[index];
    }

    private static Rect Lerp(in Rect from, in Rect to, float amount) =>
        new(Vector2.Lerp(from.Min, to.Min, amount), Vector2.Lerp(from.Max, to.Max, amount));

    private static float CollapsedHeight(int count, float scale)
    {
        var layers = NotificationGroups.VisibleLayers(count);
        var height = NotificationCard.Height + Math.Max(0, layers - 1) * StackOffsetY;
        if (count > 1)
        {
            height += MoreLabelHeight;
        }

        return height * scale;
    }

    private static float ExpandedHeight(int count, float scale) =>
        (HeaderHeight + count * NotificationCard.Height + (count - 1) * CardGap) * scale;

    private static float BlockHeight(int count, float progress, float scale)
    {
        if (count <= 1)
        {
            return NotificationCard.Height * scale;
        }

        return float.Lerp(CollapsedHeight(count, scale), ExpandedHeight(count, scale), progress);
    }

    private sealed class GroupState
    {
        public Spring Expand;
        public bool Expanded;
        public bool Seen;
    }

    private readonly record struct Target(bool IsGroup, string Key, long Id)
    {
        public static Target Of(in Candidate candidate) => new(candidate.IsGroup, candidate.Key, candidate.Id);

        public bool Matches(in Candidate candidate)
        {
            if (IsGroup != candidate.IsGroup)
            {
                return false;
            }

            return IsGroup ? string.Equals(Key, candidate.Key, StringComparison.Ordinal) : Id == candidate.Id;
        }
    }

    private readonly record struct Candidate(
        Rect Rect,
        bool IsGroup,
        string Key,
        long Id,
        float Width,
        PhoneNotification Notification,
        bool ExpandsOnTap);
}
