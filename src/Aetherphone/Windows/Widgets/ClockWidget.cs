using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Windows.Widgets;

internal sealed class ClockWidget : IHomeWidget
{
    public string Id => "clock.faces";
    public string DisplayName => Loc.T(L.Apps.Clock);
    public string Description => Loc.T(L.Widgets.ClockDescription);
    public string AppId => "clock";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context);
            return;
        }

        DrawMedium(context);
    }

    private static void DrawSmall(in WidgetContext context)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var radius = MathF.Min(bounds.Width, bounds.Height) * 0.5f - 16f * scale;
        var now = DateTime.Now;
        AnalogClock.Draw(bounds.Center, radius, now.Hour, now.Minute, now.Second + now.Millisecond / 1000f,
            context.Theme);
    }

    private static void DrawMedium(in WidgetContext context)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var ink = WidgetInk.From(context);
        var radius = MathF.Min(bounds.Height * 0.30f, bounds.Width * 0.17f);
        var faceCenterY = bounds.Min.Y + bounds.Height * 0.42f - 8f * scale;
        var now = DateTime.Now;
        var bell = EorzeaTime.Now();
        var bellSeconds = EorzeaTime.CurrentSeconds() % 60;
        DrawFace(context, ink, new Vector2(bounds.Min.X + bounds.Width * 0.27f, faceCenterY), radius, now.Hour,
            now.Minute, now.Second + now.Millisecond / 1000f, Loc.T(L.Home.Local), TimeText.Clock(now), scale);
        DrawFace(context, ink, new Vector2(bounds.Min.X + bounds.Width * 0.73f, faceCenterY), radius, bell.Hour,
            bell.Minute, bellSeconds, Loc.T(L.Home.Eorzea), bell.Formatted, scale);
    }

    private static void DrawFace(in WidgetContext context, in WidgetInk ink, Vector2 center, float radius,
        float hours, float minutes, float seconds, string label, string digital, float scale)
    {
        AnalogClock.Draw(center, radius, hours, minutes, seconds, context.Theme);
        var labelWidth = WidgetText.EyebrowWidth(label, scale);
        var labelTop = center.Y + radius + 9f * scale;
        WidgetText.Eyebrow(context.DrawList, new Vector2(center.X - labelWidth * 0.5f, labelTop), label,
            ink.Secondary, scale);
        Typography.DrawCentered(context.DrawList, new Vector2(center.X, labelTop + 19f * scale), digital,
            ink.Primary, TextStyles.SubheadlineEmphasized);
    }

    public void Dispose()
    {
    }
}
