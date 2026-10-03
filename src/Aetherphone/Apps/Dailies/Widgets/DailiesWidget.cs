using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Dailies;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Dailies.Widgets;

internal sealed class DailiesWidget : IHomeWidget
{
    private const int RefreshMilliseconds = 2000;
    private const string CadenceKey = "cadence";
    private const string DailyValue = "daily";
    private const string WeeklyValue = "weekly";
    private const string WeeklyIntent = "dailies.tab.weekly";
    private const float RowMinimumUnits = 30f;
    private const float RowMaximumUnits = 40f;
    private const float BadgeUnits = 22f;
    private const float MarkUnits = 20f;
    private const float HeroRingUnits = 62f;
    private const float RingColumnFraction = 0.34f;
    private const float CountFitFraction = 1.15f;
    private const float RelevanceDailyHours = 4f;
    private const float RelevanceWeeklyHours = 24f;
    private const int ControlBase = 100;

    private static readonly Vector4 DailiesAccent = AppAccents.For("dailies");

    private static readonly IReadOnlyList<WidgetOption> CadenceOptions = new[]
    {
        new WidgetOption(CadenceKey, L.WidgetsAdventure.OptionList,
            new[] { new WidgetChoice(DailyValue, L.Dailies.Daily), new WidgetChoice(WeeklyValue, L.Dailies.Weekly) },
            DailyValue),
    };

    private readonly GameData gameData;
    private readonly DailyCheckStore checkStore;
    private readonly DailyAutoStatus[] statuses = new DailyAutoStatus[DailyCatalog.Items.Length];
    private readonly bool[] done = new bool[DailyCatalog.Items.Length];
    private readonly int[] order = new int[DailyCatalog.Items.Length];
    private readonly CachedText[] valueTexts = new CachedText[DailyCatalog.Items.Length];
    private readonly CachedText[] resetTexts = new CachedText[2];
    private readonly CachedText[] countTexts = new CachedText[2];
    private readonly CachedText[] heroTexts = new CachedText[2];
    private readonly CachedText[] tallyTexts = new CachedText[2];
    private readonly EasedFraction[] rings = new EasedFraction[4];
    private RefreshGate refresh;
    private bool hasData;

    public DailiesWidget(Configuration configuration, GameData gameData)
    {
        this.gameData = gameData;
        checkStore = new DailyCheckStore(configuration);
    }

    public string Id => "dailies.checklist";
    public string DisplayName => Loc.T(L.Apps.Dailies);
    public string Description => Loc.T(L.WidgetsAdventure.DailiesDescription);
    public string AppId => "dailies";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;
    public IReadOnlyList<WidgetOption> Options => CadenceOptions;

    public WidgetRoute Target(in WidgetContext context) =>
        CadenceOf(context.Config) == DailyCadence.Weekly
            ? WidgetRoute.Tab(AppId, WeeklyIntent)
            : WidgetRoute.App(AppId);

    public float Relevance(string config)
    {
        if (!hasData)
        {
            return 0f;
        }

        var cadence = CadenceOf(config);
        var utcNow = DateTime.UtcNow;
        CountProgress(cadence, false, out var trackedCount, out var doneCount);
        if (doneCount >= trackedCount)
        {
            return 0f;
        }

        var window = cadence == DailyCadence.Weekly ? RelevanceWeeklyHours : RelevanceDailyHours;
        var hoursLeft = (float)(NextReset(cadence, utcNow) - utcNow).TotalHours;
        if (hoursLeft > window)
        {
            return 0.15f;
        }

        return 0.5f + 0.4f * (1f - Math.Clamp(hoursLeft / window, 0f, 1f));
    }

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var cadence = CadenceOf(context.Config);
        var loggedIn = AdventureWidgetArt.IsLoggedIn;
        var sample = !loggedIn && context.Preview;
        var utcNow = DateTime.UtcNow;
        if (loggedIn && refresh.Due(RefreshMilliseconds))
        {
            Refresh(utcNow);
        }

        if (!loggedIn && !sample)
        {
            var top = Header(context, ink, cadence);
            AdventureWidgetArt.Message(context, ink, top, FontAwesomeIcon.UserCircle,
                Loc.T(L.WidgetsAdventure.LogIn));
            return;
        }

        switch (context.Size)
        {
            case WidgetSize.Small:
                DrawSmall(context, ink, cadence, sample, utcNow);
                return;
            case WidgetSize.Large:
                DrawLarge(context, ink, cadence, sample, utcNow);
                return;
            default:
                DrawMedium(context, ink, cadence, sample, utcNow);
                return;
        }
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, DailyCadence cadence, bool sample,
        DateTime utcNow)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var headerBottom = Header(context, ink, cadence);
        var resetText = ResetText(cadence, utcNow);
        var captionHeight = AdventureWidgetArt.LineHeight(WidgetType.Caption);
        var captionTop = content.Max.Y - captionHeight;
        WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, captionTop), resetText, ink.Secondary,
            WidgetType.Caption, content.Width);
        var ringTop = headerBottom + gutter;
        var ringBottom = captionTop - gutter;
        var radius = MathF.Max(1f, MathF.Min(ringBottom - ringTop, content.Width) * 0.5f);
        var center = new Vector2(content.Center.X, (ringTop + ringBottom) * 0.5f);
        DrawRing(context, ink, cadence, sample, center, radius);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, DailyCadence cadence, bool sample,
        DateTime utcNow)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var headerBottom = Header(context, ink, cadence);
        DrawHeaderReset(context, ink, cadence, utcNow, content, headerBottom);
        var body = new Rect(new Vector2(content.Min.X, headerBottom + gutter), content.Max);
        var diameter = MathF.Min(body.Height, body.Width * RingColumnFraction);
        var radius = diameter * 0.5f;
        DrawRing(context, ink, cadence, sample, new Vector2(body.Min.X + radius, body.Center.Y), radius);
        var list = new Rect(new Vector2(body.Min.X + diameter + gutter * 2f, body.Min.Y), body.Max);
        DrawList(context, ink, cadence, sample, utcNow, list);
    }

    private void DrawLarge(in WidgetContext context, in WidgetInk ink, DailyCadence cadence, bool sample,
        DateTime utcNow)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var drawList = context.DrawList;
        var headerBottom = Header(context, ink, cadence);
        DrawHeaderReset(context, ink, cadence, utcNow, content, headerBottom);
        var diameter = HeroRingUnits * scale;
        var heroTop = headerBottom + gutter * 1.5f;
        var radius = diameter * 0.5f;
        var ringCenter = new Vector2(content.Min.X + radius, heroTop + radius);
        DrawRing(context, ink, cadence, sample, ringCenter, radius);
        CountProgress(cadence, sample, out var trackedCount, out var doneCount);
        var remaining = trackedCount - doneCount;
        var textLeft = content.Min.X + diameter + gutter * 2f;
        var textWidth = MathF.Max(1f, content.Max.X - textLeft);
        var heroIndex = CadenceIndex(cadence);
        var title = remaining <= 0
            ? Loc.T(L.Dailies.AllDone)
            : heroTexts[heroIndex].IsCurrent(remaining)
                ? heroTexts[heroIndex].Value
                : heroTexts[heroIndex].Store(remaining, Loc.T(L.WidgetsAdventure.ToDo, remaining));
        var titleHeight = AdventureWidgetArt.LineHeight(WidgetType.Title);
        var tallyHeight = AdventureWidgetArt.LineHeight(WidgetType.Body);
        var blockTop = ringCenter.Y - (titleHeight + WidgetMetrics.RowGap * scale + tallyHeight) * 0.5f;
        WidgetText.Draw(drawList, new Vector2(textLeft, blockTop), title, ink.Primary, WidgetType.Title, textWidth);
        var tallyKey = doneCount * 1000L + trackedCount;
        var tally = tallyTexts[heroIndex].IsCurrent(tallyKey)
            ? tallyTexts[heroIndex].Value
            : tallyTexts[heroIndex].Store(tallyKey,
                string.Concat(doneCount.ToString(Loc.Culture), " / ", trackedCount.ToString(Loc.Culture)));
        WidgetText.Tabular(drawList, new Vector2(textLeft, blockTop + titleHeight + WidgetMetrics.RowGap * scale),
            tally, ink.Secondary, WidgetType.Body);
        var separatorY = heroTop + diameter + gutter * 1.5f;
        drawList.AddLine(new Vector2(content.Min.X, separatorY), new Vector2(content.Max.X, separatorY),
            ImGui.GetColorU32(ink.Separator), MathF.Max(1f, scale));
        var list = new Rect(new Vector2(content.Min.X, separatorY + gutter), content.Max);
        DrawList(context, ink, cadence, sample, utcNow, list);
    }

    private float Header(in WidgetContext context, in WidgetInk ink, DailyCadence cadence) =>
        WidgetChrome.Header(context, ink, AppId, cadence == DailyCadence.Weekly ? L.Dailies.Weekly : L.Dailies.Daily,
            DailiesAccent);

    private void DrawHeaderReset(in WidgetContext context, in WidgetInk ink, DailyCadence cadence, DateTime utcNow,
        Rect content, float headerBottom)
    {
        var text = ResetText(cadence, utcNow);
        var height = AdventureWidgetArt.LineHeight(WidgetType.Caption);
        var top = content.Min.Y + (headerBottom - content.Min.Y - height) * 0.5f;
        AdventureWidgetArt.RightAligned(context.DrawList, content.Max.X, top, text, ink.Secondary, WidgetType.Caption);
    }

    private void DrawRing(in WidgetContext context, in WidgetInk ink, DailyCadence cadence, bool sample,
        Vector2 center, float radius)
    {
        CountProgress(cadence, sample, out var trackedCount, out var doneCount);
        var target = trackedCount == 0 ? 1f : doneCount / (float)trackedCount;
        var ringIndex = CadenceIndex(cadence) + (context.Preview ? 2 : 0);
        var fraction = rings[ringIndex].Step(target, context.Delta);
        AdventureWidgetArt.Ring(context, ink, center, radius, fraction, DailiesAccent);
        var inner = AdventureWidgetArt.RingInnerRadius(context, radius);
        var remaining = trackedCount - doneCount;
        if (remaining <= 0)
        {
            ProgressRing.CenterIcon(context.DrawList, center, FontAwesomeIcon.Check, ink.Accent(DailiesAccent),
                inner * 0.8f);
            return;
        }

        var text = WidgetText.Integer(ref countTexts[CadenceIndex(cadence)], remaining);
        var style = WidgetType.DisplayCompact;
        var fitted = Typography.FitScale(text, inner * CountFitFraction, style.Scale, style.Scale * 0.4f,
            style.Weight);
        var fittedStyle = new TextStyle(fitted, style.Weight);
        var size = new Vector2(WidgetText.TabularWidth(text, fittedStyle), Typography.Measure(text, fittedStyle).Y);
        WidgetText.Tabular(context.DrawList, center - size * 0.5f, text, ink.Primary, fittedStyle);
    }

    private void DrawList(in WidgetContext context, in WidgetInk ink, DailyCadence cadence, bool sample,
        DateTime utcNow, Rect area)
    {
        var count = BuildOrder(cadence, sample);
        if (count == 0 || area.Height <= 0f)
        {
            return;
        }

        var scale = context.Scale;
        var fit = Math.Max(1, (int)(area.Height / (RowMinimumUnits * scale)));
        var rows = Math.Min(fit, count);
        var rowHeight = MathF.Min(RowMaximumUnits * scale, area.Height / rows);
        for (var rowIndex = 0; rowIndex < rows; rowIndex++)
        {
            var top = area.Min.Y + rowIndex * rowHeight;
            var row = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + rowHeight));
            DrawRow(context, ink, row, order[rowIndex], sample, utcNow);
        }
    }

    private void DrawRow(in WidgetContext context, in WidgetInk ink, Rect row, int itemIndex, bool sample,
        DateTime utcNow)
    {
        var item = DailyCatalog.Items[itemIndex];
        var drawList = context.DrawList;
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var badge = BadgeUnits * scale;
        AdventureWidgetArt.IconBadge(drawList, ink, new Vector2(row.Min.X + badge * 0.5f, row.Center.Y), badge,
            item.Icon, item.Accent);
        var isDone = IsDone(itemIndex, sample);
        float textRight;
        if (item.Tracking == DailyTracking.Manual)
        {
            var control = WidgetMetrics.ControlSmall * scale;
            var center = new Vector2(row.Max.X - control * 0.5f, row.Center.Y);
            var next = WidgetControls.Toggle(context, ink, ControlBase + itemIndex, center, WidgetMetrics.ControlSmall,
                FontAwesomeIcon.Check, isDone, item.Accent);
            if (next != isDone && !sample)
            {
                checkStore.SetChecked(item, next, utcNow);
                done[itemIndex] = next;
            }

            textRight = center.X - control * 0.5f - gutter;
        }
        else if (!isDone && !sample && IsValueTracking(item.Tracking) && statuses[itemIndex].Available)
        {
            var status = statuses[itemIndex];
            var goal = status.Goal;
            var progress = Math.Clamp(goal - status.Remaining, 0, goal);
            var key = progress * 10000L + goal;
            var text = valueTexts[itemIndex].IsCurrent(key)
                ? valueTexts[itemIndex].Value
                : valueTexts[itemIndex].Store(key,
                    string.Concat(progress.ToString(Loc.Culture), "/", goal.ToString(Loc.Culture)));
            var height = AdventureWidgetArt.LineHeight(WidgetType.Caption);
            var width = AdventureWidgetArt.TabularRight(drawList, row.Max.X, row.Center.Y - height * 0.5f, text,
                ink.Secondary, WidgetType.Caption);
            textRight = row.Max.X - width - gutter;
        }
        else
        {
            var mark = MarkUnits * scale;
            AdventureWidgetArt.Mark(drawList, ink, new Vector2(row.Max.X - WidgetMetrics.ControlSmall * scale * 0.5f,
                row.Center.Y), mark, isDone, item.Accent, scale);
            textRight = row.Max.X - WidgetMetrics.ControlSmall * scale - gutter;
        }

        var textLeft = row.Min.X + badge + gutter;
        var name = Loc.T(item.Label);
        var nameHeight = AdventureWidgetArt.LineHeight(WidgetType.Body);
        WidgetText.Draw(drawList, new Vector2(textLeft, row.Center.Y - nameHeight * 0.5f), name,
            isDone ? ink.Secondary : ink.Primary, WidgetType.Body, MathF.Max(1f, textRight - textLeft));
    }

    private int BuildOrder(DailyCadence cadence, bool sample)
    {
        var items = DailyCatalog.Items;
        var count = 0;
        for (var pass = 0; pass < 2; pass++)
        {
            var wantDone = pass == 1;
            for (var index = 0; index < items.Length; index++)
            {
                if (items[index].Cadence != cadence || !DailyProgress.IsTracked(items[index]) ||
                    IsDone(index, sample) != wantDone)
                {
                    continue;
                }

                order[count] = index;
                count++;
            }
        }

        return count;
    }

    private void CountProgress(DailyCadence cadence, bool sample, out int trackedCount, out int doneCount)
    {
        var items = DailyCatalog.Items;
        trackedCount = 0;
        doneCount = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Cadence != cadence || !DailyProgress.IsTracked(items[index]))
            {
                continue;
            }

            trackedCount++;
            if (IsDone(index, sample))
            {
                doneCount++;
            }
        }
    }

    private bool IsDone(int index, bool sample) => sample ? WidgetSamples.Checked(index) : done[index];

    private void Refresh(DateTime utcNow)
    {
        var items = DailyCatalog.Items;
        for (var index = 0; index < items.Length; index++)
        {
            var status = DailyProgress.ReadStatus(gameData, items[index]);
            statuses[index] = status;
            done[index] = !DailyProgress.IsOutstanding(items[index], status, checkStore, utcNow);
        }

        hasData = true;
    }

    private string ResetText(DailyCadence cadence, DateTime utcNow)
    {
        var remaining = NextReset(cadence, utcNow) - utcNow;
        var key = (long)remaining.TotalMinutes;
        var index = CadenceIndex(cadence);
        if (resetTexts[index].IsCurrent(key))
        {
            return resetTexts[index].Value;
        }

        return resetTexts[index].Store(key, Loc.T(L.Dailies.Resets, TimeFormat.Relative(remaining)));
    }

    private static DateTime NextReset(DailyCadence cadence, DateTime utcNow) =>
        cadence == DailyCadence.Weekly ? GameSchedule.NextWeeklyReset(utcNow) : GameSchedule.NextDailyReset(utcNow);

    private static bool IsValueTracking(DailyTracking tracking) =>
        tracking is DailyTracking.BeastTribeAllowances or DailyTracking.CustomDeliveries
            or DailyTracking.WondrousTails or DailyTracking.HuntBills or DailyTracking.DomanEnclave;

    private static int CadenceIndex(DailyCadence cadence) => cadence == DailyCadence.Weekly ? 1 : 0;

    private static DailyCadence CadenceOf(string config) =>
        string.Equals(WidgetConfig.Get(config, CadenceKey, DailyValue), WeeklyValue, StringComparison.Ordinal)
            ? DailyCadence.Weekly
            : DailyCadence.Daily;

    public void Dispose()
    {
    }
}
