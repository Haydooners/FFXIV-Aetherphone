using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Calendar;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Calendar;

internal sealed class CalendarWidget : IHomeWidget
{
    private const float RefreshIntervalSeconds = 20f;
    private const int LookaheadDays = 14;
    private const int MaxRows = 3;
    private const int SampleHour = 20;

    private readonly struct UpcomingEvent
    {
        public readonly string Name;
        public readonly DateTime Begin;
        public readonly Vector4 Color;
        public readonly string When;

        public UpcomingEvent(string name, DateTime begin, Vector4 color, string when)
        {
            Name = name;
            Begin = begin;
            Color = color;
            When = when;
        }
    }

    private readonly Configuration configuration;
    private readonly CalendarEvents events;
    private readonly List<UpcomingEvent> upcoming = new();
    private readonly List<UpcomingEvent> samples = new();
    private float sinceRefresh = RefreshIntervalSeconds;
    private int seenRevision = -1;
    private long samplesDay = -1;
    private CultureInfo? samplesCulture;
    private CachedText weekday;
    private CachedText dayNumber;
    private CachedText month;

    public CalendarWidget(Configuration configuration, CalendarEvents events)
    {
        this.configuration = configuration;
        this.events = events;
    }

    public string Id => "calendar.upcoming";
    public string DisplayName => Loc.T(L.Calendar.Title);
    public string Description => Loc.T(L.Widgets.CalendarDescription);
    public string AppId => "calendar";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public void Draw(in WidgetContext context)
    {
        events.Initialize();
        Advance(context.Delta, context.Theme.Accent);
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var rows = upcoming.Count == 0 && context.Preview ? Samples(context.Theme.Accent) : upcoming;
        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, rows);
            return;
        }

        DrawMedium(context, ink, rows);
    }

    private void Advance(float delta, Vector4 accent)
    {
        sinceRefresh += delta;
        if (sinceRefresh < RefreshIntervalSeconds && seenRevision == events.CustomRevision)
        {
            return;
        }

        sinceRefresh = 0f;
        seenRevision = events.CustomRevision;
        upcoming.Clear();
        var merged = CalendarEventMerger.Merge(events.Events, configuration.CalendarCustomEvents,
            configuration.CalendarGroups, configuration.CalendarGameEventsInWidget, CalendarSurface.Widget, accent);
        var now = DateTime.Now;
        for (var day = 0; day < LookaheadDays; day++)
        {
            var key = now.Date.AddDays(day).Ticks;
            if (!merged.TryGetValue(key, out var dayEvents))
            {
                continue;
            }

            for (var index = 0; index < dayEvents.Length; index++)
            {
                var entry = dayEvents[index];
                if (entry.End >= now || entry.Begin >= now)
                {
                    upcoming.Add(new UpcomingEvent(entry.Name, entry.Begin, entry.Color, WhenLabel(entry.Begin)));
                }
            }
        }

        upcoming.Sort(static (left, right) => left.Begin.CompareTo(right.Begin));
        if (upcoming.Count > MaxRows + 1)
        {
            upcoming.RemoveRange(MaxRows + 1, upcoming.Count - MaxRows - 1);
        }
    }

    private List<UpcomingEvent> Samples(Vector4 accent)
    {
        var today = DateTime.Today;
        if (samplesDay == today.Ticks && ReferenceEquals(samplesCulture, Loc.Culture))
        {
            return samples;
        }

        samplesDay = today.Ticks;
        samplesCulture = Loc.Culture;
        samples.Clear();
        for (var index = 0; index < WidgetSamples.Events.Length; index++)
        {
            var begin = today.AddDays(index).AddHours(SampleHour);
            samples.Add(new UpcomingEvent(Loc.T(WidgetSamples.Events[index]), begin, accent, WhenLabel(begin)));
        }

        return samples;
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, List<UpcomingEvent> rows)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var drawList = context.DrawList;
        var pad = 13f * scale;
        var now = DateTime.Now;
        var dayNumberTop = bounds.Min.Y + pad + 13f * scale;
        WidgetText.Eyebrow(drawList, new Vector2(bounds.Min.X + pad, bounds.Min.Y + pad), Weekday(now),
            ink.Accent(context.Theme.Accent), scale);
        var dayText = DayNumber(now);
        var dayNumberSize = Typography.Measure(dayText, TextStyles.LargeTitle);
        Typography.Draw(drawList, new Vector2(bounds.Min.X + pad, dayNumberTop), dayText, ink.Primary,
            TextStyles.LargeTitle);
        var dayNumberBottom = dayNumberTop + dayNumberSize.Y * 0.72f;

        var whenSize = Typography.Measure("0", TextStyles.Caption1);
        var fixedEventTop = bounds.Max.Y - pad - 32f * scale;
        var eventTop = MathF.Max(fixedEventTop, dayNumberBottom + 6f * scale);
        var whenTop = MathF.Max(bounds.Max.Y - pad - 15f * scale, eventTop + 17f * scale);
        if (whenTop + whenSize.Y > bounds.Max.Y + 4f * scale)
        {
            return;
        }

        if (rows.Count == 0)
        {
            Typography.Draw(drawList, new Vector2(bounds.Min.X + pad, whenTop),
                Typography.FitText(Loc.T(L.Home.NoEvents), bounds.Width - pad * 2f, TextStyles.Caption1),
                ink.Secondary, TextStyles.Caption1);
            return;
        }

        var first = rows[0];
        var firstNameMaxWidth = bounds.Width - pad * 2f;
        Marquee.DrawLeftAuto(drawList, "calendarwidget.small.name", first.Name, bounds.Min.X + pad, eventTop,
            firstNameMaxWidth, TextStyles.FootnoteEmphasized, ink.Primary);
        Typography.Draw(drawList, new Vector2(bounds.Min.X + pad, whenTop), first.When, ink.Secondary,
            TextStyles.Caption1);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, List<UpcomingEvent> rows)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var drawList = context.DrawList;
        var pad = 16f * scale;
        var now = DateTime.Now;
        var left = bounds.Min.X + pad;
        WidgetText.Eyebrow(drawList, new Vector2(left, bounds.Min.Y + pad), Weekday(now),
            ink.Accent(context.Theme.Accent), scale);
        Typography.Draw(drawList, new Vector2(left, bounds.Min.Y + pad + 14f * scale), DayNumber(now), ink.Primary,
            TextStyles.LargeTitle);
        Typography.Draw(drawList, new Vector2(left, bounds.Max.Y - pad - 16f * scale), Month(now), ink.Secondary,
            TextStyles.Caption1);
        var columnX = bounds.Min.X + bounds.Width * 0.36f;
        drawList.AddLine(new Vector2(columnX, bounds.Min.Y + pad), new Vector2(columnX, bounds.Max.Y - pad),
            ImGui.GetColorU32(ink.Separator), 1f * scale);
        var listLeft = columnX + pad;
        if (rows.Count == 0)
        {
            var empty = Loc.T(L.Home.NoEvents);
            Typography.Draw(drawList,
                new Vector2(listLeft, bounds.Center.Y - Typography.Measure(empty, TextStyles.Footnote).Y * 0.5f),
                Typography.FitText(empty, bounds.Max.X - pad - listLeft, TextStyles.Footnote), ink.Secondary,
                TextStyles.Footnote);
            return;
        }

        var count = Math.Min(MaxRows, rows.Count);
        var listPad = 6f * scale;
        var rowHeight = (bounds.Height - listPad * 2f) / MaxRows;
        var whenGlyphHeight = Typography.Measure("0", TextStyles.Caption1).Y * 0.72f;
        for (var index = 0; index < count; index++)
        {
            var entry = rows[index];
            var rowTop = bounds.Min.Y + listPad + index * rowHeight;
            var barRect = new Rect(new Vector2(listLeft, rowTop + 3f * scale),
                new Vector2(listLeft + 3f * scale, rowTop + rowHeight - 5f * scale));
            drawList.AddRectFilled(barRect.Min, barRect.Max, ImGui.GetColorU32(ink.Accent(entry.Color)),
                1.5f * scale);
            var textLeft = listLeft + 10f * scale;
            var maxWidth = bounds.Max.X - pad - textLeft;
            var nameGlyphHeight = Typography.Measure(entry.Name, TextStyles.FootnoteEmphasized).Y * 0.72f;
            var rowGap = MathF.Max(0f,
                MathF.Min(3f * scale, rowHeight - nameGlyphHeight - whenGlyphHeight));
            var nameTop = rowTop + rowHeight * 0.5f - (nameGlyphHeight + rowGap + whenGlyphHeight) * 0.5f;
            Marquee.DrawLeftAuto(drawList, new MarqueeId("calendarwidget.medium.name.", index), entry.Name, textLeft,
                nameTop, maxWidth, TextStyles.FootnoteEmphasized, ink.Primary);
            Typography.Draw(drawList, new Vector2(textLeft, nameTop + nameGlyphHeight + rowGap), entry.When,
                ink.Secondary, TextStyles.Caption1);
        }
    }

    private string Weekday(DateTime now)
    {
        var key = now.Date.Ticks;
        return weekday.IsCurrent(key) ? weekday.Value : weekday.Store(key, now.ToString("dddd", Loc.Culture));
    }

    private string DayNumber(DateTime now)
    {
        var key = now.Date.Ticks;
        return dayNumber.IsCurrent(key) ? dayNumber.Value : dayNumber.Store(key, now.Day.ToString(Loc.Culture));
    }

    private string Month(DateTime now)
    {
        var key = now.Date.Ticks;
        return month.IsCurrent(key) ? month.Value : month.Store(key, now.ToString("MMMM", Loc.Culture));
    }

    private static string WhenLabel(DateTime begin)
    {
        if (begin.Date == DateTime.Today)
        {
            return TimeText.Clock(begin);
        }

        return string.Concat(begin.ToString("ddd", Loc.Culture), " ", TimeText.Clock(begin));
    }

    public void Dispose()
    {
    }
}
