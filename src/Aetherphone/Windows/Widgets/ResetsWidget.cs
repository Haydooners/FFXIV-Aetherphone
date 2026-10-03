using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Windows.Widgets;

internal sealed class ResetsWidget : IHomeWidget
{
    private const float TrackAlpha = 0.18f;
    private static readonly Vector4 WeeklyColor = new(0.36f, 0.65f, 1f, 1f);
    private static readonly Vector4 GrandCompanyColor = new(1f, 0.63f, 0.32f, 1f);

    private readonly CachedText[] countdowns = new CachedText[3];

    public string Id => "timers.resets";
    public string DisplayName => Loc.T(L.Apps.Timers);
    public string Description => Loc.T(L.Widgets.ResetsDescription);
    public string AppId => "timers";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var utcNow = DateTime.UtcNow;
        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, utcNow);
            return;
        }

        DrawMedium(context, ink, utcNow);
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, DateTime utcNow)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var pad = 13f * scale;
        WidgetText.Eyebrow(context.DrawList, new Vector2(bounds.Min.X + pad, bounds.Min.Y + pad),
            L.Timers.DailyReset, ink.Secondary, scale);
        var center = new Vector2(bounds.Center.X, bounds.Center.Y + 7f * scale);
        var radius = MathF.Min(bounds.Width, bounds.Height) * 0.5f - 27f * scale;
        DrawRing(context, ink, 0, center, radius, GameSchedule.NextDailyReset(utcNow), utcNow, TimeSpan.FromDays(1),
            context.Theme.Accent);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, DateTime utcNow)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var radius = MathF.Min(bounds.Height * 0.24f, bounds.Width * 0.11f);
        var centerY = bounds.Min.Y + bounds.Height * 0.42f;
        var columnMaxWidth = bounds.Width * 0.30f - 6f * scale;
        DrawColumn(context, ink, 0, new Vector2(bounds.Min.X + bounds.Width * 0.20f, centerY), radius,
            columnMaxWidth, Loc.T(L.Timers.DailyReset), GameSchedule.NextDailyReset(utcNow), utcNow,
            TimeSpan.FromDays(1), context.Theme.Accent);
        DrawColumn(context, ink, 1, new Vector2(bounds.Min.X + bounds.Width * 0.50f, centerY), radius,
            columnMaxWidth, Loc.T(L.Timers.WeeklyReset), GameSchedule.NextWeeklyReset(utcNow), utcNow,
            TimeSpan.FromDays(7), WeeklyColor);
        DrawColumn(context, ink, 2, new Vector2(bounds.Min.X + bounds.Width * 0.80f, centerY), radius,
            columnMaxWidth, Loc.T(L.Timers.GrandCompanyReset), GameSchedule.NextGrandCompanyReset(utcNow), utcNow,
            TimeSpan.FromDays(1), GrandCompanyColor);
    }

    private void DrawColumn(in WidgetContext context, in WidgetInk ink, int column, Vector2 center, float radius,
        float maxLabelWidth, string label, DateTime next, DateTime utcNow, TimeSpan period, Vector4 color)
    {
        DrawRing(context, ink, column, center, radius, next, utcNow, period, color);
        var labelWidth = MathF.Min(maxLabelWidth, WidgetText.EyebrowWidth(label, context.Scale));
        WidgetText.EyebrowMarquee(context.DrawList, new MarqueeId("timers.resets", column), label,
            new Vector2(center.X - labelWidth * 0.5f, center.Y + radius + 11f * context.Scale), labelWidth,
            ink.Secondary, context.Scale);
    }

    private void DrawRing(in WidgetContext context, in WidgetInk ink, int ring, Vector2 center, float radius,
        DateTime next, DateTime utcNow, TimeSpan period, Vector4 color)
    {
        var scale = context.Scale;
        var remaining = next - utcNow;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var fraction = 1f - (float)(remaining.TotalSeconds / period.TotalSeconds);
        var thickness = MathF.Max(3f * scale, radius * 0.14f);
        var drawList = context.DrawList;
        ProgressRing.Track(drawList, center, radius, thickness, ink.Accent(color with { W = TrackAlpha }));
        ProgressRing.Fill(drawList, center, radius, thickness, Math.Clamp(fraction, 0f, 1f), ink.Accent(color));
        var style = new TextStyle(MathF.Max(0.62f, radius / (34f * scale)), FontWeight.SemiBold);
        var text = WidgetText.Countdown(ref countdowns[ring], remaining);
        var width = WidgetText.TabularWidth(text, style);
        var height = Typography.Measure(text, style).Y;
        WidgetText.Tabular(drawList, new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f), text,
            ink.Primary, style);
    }

    public void Dispose()
    {
    }
}
