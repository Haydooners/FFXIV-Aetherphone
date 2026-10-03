using Aetherphone.Core;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class StatusIcons
{
    private const float NubWidth = 1.8f;
    private const float NubHeight = 4.4f;
    private const float BodyWidth = 22f;
    private const float BodyHeight = 11.5f;
    private const float BodyRadius = 3.8f;
    private const float ShellStroke = 1.2f;
    private const float ShellAlpha = 0.45f;
    private const float FillInset = 1.8f;
    private const float FillRadius = 2f;
    private const float MinFillFraction = 0.05f;
    private const float LabelGap = 6f;
    private const float WifiGap = 6f;
    private const float WifiWidth = 14f;
    private const float WifiOuterRadius = 8.5f;
    private const float WifiArcStep = 2.9f;
    private const float WifiStroke = 2.2f;
    private const float WifiDotRadius = 1.5f;
    private const float WifiApexOffset = 4.1f;
    private const float WifiStartAngle = -MathF.PI * 0.75f;
    private const float WifiEndAngle = -MathF.PI * 0.25f;
    private const int WifiArcCount = 3;
    private const int WifiArcSegments = 14;
    private const float SignalGap = 6f;
    private const float BarWidth = 3f;
    private const float BarGap = 1.6f;
    private const float BarRadius = 1.5f;
    private const float BarBaselineOffset = 5.9f;
    private const float DimAlpha = 0.30f;
    private const float RightPadding = 24f;
    private const float MinRightPadding = 8f;
    private const int LowBatteryPercent = 20;
    private const int CriticalBatteryPercent = 10;
    private const float CriticalPulseSpeed = 3.2f;
    private const int StrongLatencyMilliseconds = 120;
    private const int FairLatencyMilliseconds = 260;
    private static readonly float[] BarHeights = { 4.6f, 7f, 9.4f, 11.8f };

    public static float MeasureWidth(float scale, int percent)
    {
        var labelWidth = Typography.Measure(percent + "%", TextStyles.Footnote).X;
        return (NubWidth + BodyWidth) * scale + LabelGap * scale + labelWidth + WifiGap * scale + WifiWidth * scale +
               SignalGap * scale + SignalClusterWidth(scale);
    }

    public static void Draw(Rect screen, PhoneTheme theme, float rowCenterY, float minClusterLeft)
    {
        var scale = UiScale.Current;
        var device = Plugin.Device;
        var clusterWidth = MeasureWidth(scale, device.BatteryPercent);
        var nubRight = screen.Max.X - RightPadding * scale;
        if (nubRight - clusterWidth < minClusterLeft)
        {
            nubRight = MathF.Min(screen.Max.X - MinRightPadding * scale, minClusterLeft + clusterWidth);
        }

        var batteryLeft = DrawBattery(theme, rowCenterY, nubRight, device.BatteryPercent, device.Charging);
        var labelLeft = DrawBatteryLabel(theme, rowCenterY, batteryLeft, device.BatteryPercent, device.Charging);
        var wifiLeft = DrawWifi(theme, rowCenterY, labelLeft,
            WifiArcs(device.SignalBars, device.LatencyMilliseconds));
        DrawSignal(theme, rowCenterY, wifiLeft, device.SignalBars);
        ReportAnchors(scale, rowCenterY, nubRight, labelLeft, wifiLeft);
    }

    internal static int WifiArcs(int signalBars, int latencyMilliseconds)
    {
        if (signalBars <= 0)
        {
            return 0;
        }

        if (latencyMilliseconds <= StrongLatencyMilliseconds)
        {
            return WifiArcCount;
        }

        return latencyMilliseconds <= FairLatencyMilliseconds ? 2 : 1;
    }

    private static void ReportAnchors(float scale, float rowCenterY, float nubRight, float labelLeft, float wifiLeft)
    {
        if (!UiAnchors.Recording)
        {
            return;
        }

        var top = rowCenterY - 9f * scale;
        var bottom = rowCenterY + 9f * scale;
        UiAnchors.Report("chrome.battery",
            new Rect(new Vector2(labelLeft - 2f * scale, top), new Vector2(nubRight, bottom)));
        var signalWidth = SignalClusterWidth(scale);
        var signalLeft = wifiLeft - SignalGap * scale - signalWidth;
        UiAnchors.Report("chrome.signal",
            new Rect(new Vector2(signalLeft, top), new Vector2(signalLeft + signalWidth, bottom)));
    }

    private static float SignalClusterWidth(float scale) => (BarWidth * 4f + BarGap * 3f) * scale;

    private static float DrawBattery(PhoneTheme theme, float rowCenterY, float nubRight, int percent, bool charging)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var nubWidth = NubWidth * scale;
        var nubHeight = NubHeight * scale;
        var bodyWidth = BodyWidth * scale;
        var bodyHeight = BodyHeight * scale;
        var bodyMax = new Vector2(nubRight - nubWidth, rowCenterY + bodyHeight * 0.5f);
        var bodyMin = new Vector2(bodyMax.X - bodyWidth, rowCenterY - bodyHeight * 0.5f);
        var warning = WarningInk(theme, percent, charging);
        var shell = warning is { } warn
            ? Palette.WithAlpha(warn, 0.85f)
            : Palette.WithAlpha(theme.TextStrong, ShellAlpha);
        var shellColor = ImGui.GetColorU32(shell);
        drawList.AddRect(bodyMin, bodyMax, shellColor, BodyRadius * scale, ImDrawFlags.RoundCornersAll,
            ShellStroke * scale);
        var nubMin = new Vector2(bodyMax.X, rowCenterY - nubHeight * 0.5f);
        var nubMax = new Vector2(bodyMax.X + nubWidth, rowCenterY + nubHeight * 0.5f);
        drawList.AddRectFilled(nubMin, nubMax, shellColor, nubWidth * 0.5f, ImDrawFlags.RoundCornersRight);
        var fillColor = charging ? theme.ToggleOn : warning ?? theme.TextStrong;
        var inset = FillInset * scale;
        var trackLeft = bodyMin.X + inset;
        var trackWidth = bodyMax.X - inset - trackLeft;
        var fillWidth = trackWidth * Math.Clamp(percent / 100f, MinFillFraction, 1f);
        var fillMin = new Vector2(trackLeft, bodyMin.Y + inset);
        var fillMax = new Vector2(trackLeft + fillWidth, bodyMax.Y - inset);
        drawList.AddRectFilled(fillMin, fillMax, ImGui.GetColorU32(fillColor), FillRadius * scale);
        return bodyMin.X;
    }

    private static Vector4? WarningInk(PhoneTheme theme, int percent, bool charging)
    {
        if (charging || percent > LowBatteryPercent)
        {
            return null;
        }

        if (percent > CriticalBatteryPercent)
        {
            return theme.Danger;
        }

        var breath = 0.72f + 0.28f * (0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * CriticalPulseSpeed));
        return Palette.WithAlpha(theme.Danger, breath);
    }

    private static float DrawBatteryLabel(PhoneTheme theme, float rowCenterY, float batteryLeft, int percent,
        bool charging)
    {
        var scale = UiScale.Current;
        var label = percent + "%";
        var size = Typography.Measure(label, TextStyles.Footnote);
        var position = new Vector2(batteryLeft - LabelGap * scale - size.X, rowCenterY - size.Y * 0.5f);
        Typography.Draw(ImGui.GetWindowDrawList(), position, label,
            WarningInk(theme, percent, charging) ?? theme.TextStrong, TextStyles.Footnote);
        return position.X;
    }

    private static float DrawWifi(PhoneTheme theme, float rowCenterY, float labelLeft, int arcs)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var wifiLeft = labelLeft - WifiGap * scale - WifiWidth * scale;
        var apex = new Vector2(wifiLeft + WifiWidth * 0.5f * scale, rowCenterY + WifiApexOffset * scale);
        var lit = ImGui.GetColorU32(theme.TextStrong);
        var dim = ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, DimAlpha));
        for (var arcIndex = 0; arcIndex < WifiArcCount; arcIndex++)
        {
            var radius = (WifiOuterRadius - WifiArcStep * (WifiArcCount - 1 - arcIndex)) * scale;
            drawList.PathArcTo(apex, radius, WifiStartAngle, WifiEndAngle, WifiArcSegments);
            drawList.PathStroke(arcIndex < arcs ? lit : dim, ImDrawFlags.None, WifiStroke * scale);
        }

        drawList.AddCircleFilled(apex, WifiDotRadius * scale, arcs > 0 ? lit : dim, 12);
        return wifiLeft;
    }

    private static void DrawSignal(PhoneTheme theme, float rowCenterY, float wifiLeft, int bars)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var barWidth = BarWidth * scale;
        var barGap = BarGap * scale;
        var clusterLeft = wifiLeft - SignalGap * scale - SignalClusterWidth(scale);
        var baseline = rowCenterY + BarBaselineOffset * scale;
        var lit = ImGui.GetColorU32(theme.TextStrong);
        var dim = ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, DimAlpha));
        for (var index = 0; index < BarHeights.Length; index++)
        {
            var left = clusterLeft + index * (barWidth + barGap);
            var height = BarHeights[index] * scale;
            var min = new Vector2(left, baseline - height);
            var max = new Vector2(left + barWidth, baseline);
            drawList.AddRectFilled(min, max, index < bars ? lit : dim, BarRadius * scale, ImDrawFlags.RoundCornersTop);
        }
    }
}
