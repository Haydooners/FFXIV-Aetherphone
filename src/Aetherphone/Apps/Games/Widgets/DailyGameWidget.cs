using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Widgets;

internal sealed class DailyGameWidget : IHomeWidget
{
    private const string AppKey = "games";
    private const float RefreshSeconds = 20f;
    private const float IconUnits = 42f;
    private const float BadgeUnits = 16f;
    private const float GradientLift = 0.08f;
    private const float GradientDrop = 0.28f;
    private const float SecondaryAlpha = 0.78f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Ember = AccentRing.Orange;

    private readonly GameStatsStore stats;
    private IMiniGame? game;
    private float sinceRefresh = RefreshSeconds;
    private CachedText streakText;

    public DailyGameWidget(GameStatsStore stats)
    {
        this.stats = stats;
    }

    public string Id => "games.daily";
    public string DisplayName => Loc.T(L.WidgetsUtility.DailyGameName);
    public string Description => Loc.T(L.WidgetsUtility.DailyGameDescription);
    public string AppId => AppKey;
    public WidgetSizeSet Sizes => WidgetSizeSet.Small;

    public float Relevance(string config)
    {
        if (stats.DailyDone)
        {
            return 0f;
        }

        return stats.DailyStreak > 0 ? 0.7f : 0.35f;
    }

    public void Draw(in WidgetContext context)
    {
        Refresh(context);
        var ink = WidgetInk.From(context);
        if (game is null)
        {
            WidgetChrome.Container(context);
            UtilityWidgetKit.Message(context, ink, WidgetMetrics.Content(context), FontAwesomeIcon.Gamepad,
                AppAccents.For(AppKey), Loc.T(L.Apps.Games), string.Empty);
            return;
        }

        var accent = game.Accent;
        var colored = ink.Mode is WidgetMode.FullColor or WidgetMode.Dark;
        if (colored)
        {
            WidgetChrome.Container(context, Palette.Lighten(accent, GradientLift), Palette.Darken(accent, GradientDrop));
        }
        else
        {
            WidgetChrome.Container(context);
        }

        var primary = colored ? ink.Fade(White) : ink.Primary;
        var secondary = colored ? ink.Fade(White, SecondaryAlpha) : ink.Secondary;
        var drawList = context.DrawList;
        var scale = context.Scale;
        var content = WidgetMetrics.Content(context);
        var icon = IconUnits * scale;
        var iconMin = content.Min;
        var iconMax = iconMin + new Vector2(icon, icon);
        if (!AppIconTile.TryDraw(drawList, game.Id, accent, iconMin, iconMax, icon * Metrics.Radius.TileFactor,
                ink.Opacity, false, scale))
        {
            AppIconArt.TryDraw(drawList, game.Id, (iconMin + iconMax) * 0.5f, icon, primary, Palette.Darken(accent, 0.16f));
        }

        var done = stats.DailyDone;
        var streak = stats.DailyStreak;
        if (done || streak > 0)
        {
            var badge = BadgeUnits * scale;
            var badgeCenter = new Vector2(content.Max.X - badge * 0.5f, content.Min.Y + badge * 0.5f);
            ProgressRing.CenterIcon(drawList, badgeCenter, done ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.Fire,
                done ? primary : colored ? ink.Fade(White) : ink.Accent(Ember), badge);
        }

        var captionHeight = UtilityWidgetKit.LineHeightOf(WidgetType.Caption);
        var titleHeight = UtilityWidgetKit.LineHeightOf(WidgetType.Title);
        var eyebrowHeight = WidgetText.EyebrowHeight();
        var captionTop = content.Max.Y - captionHeight;
        var titleSpace = captionTop - (iconMax.Y + WidgetMetrics.Gutter * scale + eyebrowHeight);
        var titleLines = UtilityWidgetKit.Clamp(game.Title, WidgetType.Title, content.Width,
            Math.Clamp((int)(titleSpace / titleHeight), 1, 2));
        var titleTop = captionTop - titleLines.Length * titleHeight;
        WidgetText.EyebrowFit(drawList, new Vector2(content.Min.X, titleTop - eyebrowHeight - WidgetMetrics.RowGap * scale),
            Loc.T(L.WidgetsUtility.TodaysGame), content.Width, secondary, scale);
        UtilityWidgetKit.DrawLines(drawList, titleLines, new Vector2(content.Min.X, titleTop), primary,
            WidgetType.Title, titleHeight);
        UtilityWidgetKit.DrawFitted(drawList, new Vector2(content.Min.X, captionTop), Caption(done, streak), secondary,
            WidgetType.Caption, content.Width);
    }

    private void Refresh(in WidgetContext context)
    {
        sinceRefresh += context.Delta;
        if (game is not null && sinceRefresh < RefreshSeconds)
        {
            return;
        }

        sinceRefresh = 0f;
        game = context.Actions.App(AppKey) is GamesApp games ? games.DailyGame : null;
    }

    private string Caption(bool done, int streak)
    {
        if (done)
        {
            return Loc.T(L.WidgetsUtility.PlayedToday);
        }

        if (streak <= 0)
        {
            return Loc.T(L.WidgetsUtility.StartStreak);
        }

        return streakText.IsCurrent(streak)
            ? streakText.Value
            : streakText.Store(streak, Loc.T(L.WidgetsUtility.Streak, streak));
    }

    public void Dispose()
    {
    }
}
