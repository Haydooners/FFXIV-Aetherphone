using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Widgets;

internal sealed class ActivityRingsWidget : IHomeWidget
{
    private const float ThicknessFactor = 0.24f;
    private const float GapFactor = 0.07f;
    private const int RingCount = 3;

    private readonly ActivityTracker tracker;
    private readonly Configuration configuration;
    private readonly CachedText[] legend = new CachedText[RingCount * 2];

    public string Id => "character.rings";
    public string DisplayName => Loc.T(L.Character.Activity);
    public string Description => Loc.T(L.Widgets.ActivityDescription);
    public string AppId => "character";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public ActivityRingsWidget(ActivityTracker tracker, Configuration configuration)
    {
        this.tracker = tracker;
        this.configuration = configuration;
    }

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var day = tracker.IsTracking ? tracker.Today : null;
        var sample = day is null && context.Preview;
        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, day, sample);
            return;
        }

        DrawMedium(context, ink, day, sample);
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, ActivityDay? day, bool sample)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var pad = 13f * scale;
        WidgetText.Eyebrow(context.DrawList, new Vector2(bounds.Min.X + pad, bounds.Min.Y + pad), DisplayName,
            ink.Secondary, scale);
        var center = new Vector2(bounds.Center.X, bounds.Center.Y + 7f * scale);
        var radius = MathF.Min(bounds.Width, bounds.Height) * 0.5f - 24f * scale;
        DrawRings(context, ink, center, radius, day, sample);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, ActivityDay? day, bool sample)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var radius = bounds.Height * 0.5f - 22f * scale;
        var ringsCenter = new Vector2(bounds.Min.X + 22f * scale + radius, bounds.Center.Y);
        DrawRings(context, ink, ringsCenter, radius, day, sample);
        var textLeft = ringsCenter.X + radius + 24f * scale;
        var rowStep = bounds.Height * 0.30f;
        DrawLegendRow(context, ink, new Vector2(textLeft, bounds.Center.Y - rowStep), ActivityRings.RingOneTint,
            Loc.T(L.Character.RingProgress), ProgressValue(day, sample));
        DrawLegendRow(context, ink, new Vector2(textLeft, bounds.Center.Y), ActivityRings.RingTwoTint,
            Loc.T(L.Character.RingAdventure), AdventureValue(day, sample));
        DrawLegendRow(context, ink, new Vector2(textLeft, bounds.Center.Y + rowStep), ActivityRings.RingThreeTint,
            Loc.T(L.Character.RingFortune), FortuneValue(day, sample));
    }

    private void DrawRings(in WidgetContext context, in WidgetInk ink, Vector2 center, float radius,
        ActivityDay? day, bool sample)
    {
        var thickness = radius * ThicknessFactor;
        var gap = radius * GapFactor;
        var middle = radius - thickness - gap;
        var inner = middle - thickness - gap;
        DrawRing(context, ink, center, radius, thickness, Fraction(0, day, sample), ActivityRings.RingOneTint);
        DrawRing(context, ink, center, middle, thickness, Fraction(1, day, sample), ActivityRings.RingTwoTint);
        DrawRing(context, ink, center, inner, thickness, Fraction(2, day, sample), ActivityRings.RingThreeTint);
    }

    private float Fraction(int ring, ActivityDay? day, bool sample)
    {
        if (sample)
        {
            return WidgetSamples.Fraction(ring);
        }

        if (day is null)
        {
            return 0f;
        }

        return ring switch
        {
            0 => ActivityGoals.ProgressFraction(configuration, day),
            1 => ActivityGoals.AdventureFraction(configuration, day),
            _ => ActivityGoals.FortuneFraction(configuration, day),
        };
    }

    private static void DrawRing(in WidgetContext context, in WidgetInk ink, Vector2 center, float radius,
        float thickness, float fraction, Vector4 tint)
    {
        ProgressRing.Track(context.DrawList, center, radius, thickness, ink.Fill);
        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped > 0.001f)
        {
            ProgressRing.Fill(context.DrawList, center, radius, thickness, clamped, ink.Accent(tint));
        }
    }

    private static void DrawLegendRow(in WidgetContext context, in WidgetInk ink, Vector2 position, Vector4 tint,
        string label, string value)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var labelHeight = WidgetText.EyebrowHeight();
        var valueSize = Typography.Measure(value, WidgetType.Headline);
        var gap = 2f * scale;
        var blockTop = position.Y - (labelHeight + gap + valueSize.Y) * 0.5f;
        var dot = 3.4f * scale;
        drawList.AddCircleFilled(new Vector2(position.X + dot, position.Y), dot, ImGui.GetColorU32(ink.Accent(tint)));
        var textLeft = position.X + dot * 2f + 7f * scale;
        WidgetText.Eyebrow(drawList, new Vector2(textLeft, blockTop), label, ink.Secondary, scale);
        Typography.Draw(drawList, new Vector2(textLeft, blockTop + labelHeight + gap), value, ink.Primary,
            WidgetType.Headline);
    }

    private string ProgressValue(ActivityDay? day, bool sample)
    {
        var fraction = Fraction(0, day, sample);
        return Percent(ref legend[0], (int)MathF.Round(Math.Clamp(fraction, 0f, 9.99f) * 100f));
    }

    private string AdventureValue(ActivityDay? day, bool sample)
    {
        if (sample)
        {
            return Percent(ref legend[RingCount + 1], (int)MathF.Round(WidgetSamples.Fraction(1) * 100f));
        }

        long duties = day?.DutiesCompleted ?? 0;
        long goal = configuration.ActivityGoalDuties;
        var key = duties * 1_000_003L + goal;
        return legend[1].IsCurrent(key)
            ? legend[1].Value
            : legend[1].Store(key, string.Concat(duties.ToString(), " / ", goal.ToString()));
    }

    private string FortuneValue(ActivityDay? day, bool sample)
    {
        if (sample)
        {
            return Percent(ref legend[RingCount + 2], (int)MathF.Round(WidgetSamples.Fraction(2) * 100f));
        }

        var gil = day?.GilEarned ?? 0;
        var goal = configuration.ActivityGoalGil;
        var key = unchecked(gil * 1_000_003L + goal);
        return legend[2].IsCurrent(key)
            ? legend[2].Value
            : legend[2].Store(key, string.Concat(NumberText.Compact(gil), " / ", NumberText.Compact(goal)));
    }

    private static string Percent(ref CachedText cache, int percent) =>
        cache.IsCurrent(percent) ? cache.Value : cache.Store(percent, string.Concat(percent.ToString(), "%"));

    public void Dispose()
    {
    }
}
