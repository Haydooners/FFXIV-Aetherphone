using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Windows.Widgets;

internal sealed class CoinWidget : IHomeWidget
{
    private readonly CoinStore coins;
    private readonly AethernetSession session;
    private RollingValue balance;
    private CachedText balanceText;
    private CachedText progressText;

    public CoinWidget(CoinStore coins, AethernetSession session)
    {
        this.coins = coins;
        this.session = session;
    }

    public string Id => "coin.balance";
    public string DisplayName => Loc.T(L.Apps.Coin);
    public string Description => Loc.T(L.Widgets.CoinDescription);
    public string AppId => "coin";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var bounds = context.Bounds;
        var scale = context.Scale;
        var pad = 13f * scale;
        var accent = ink.Accent(AppAccents.For(AppId));

        WidgetText.Eyebrow(context.DrawList, new Vector2(bounds.Min.X + pad, bounds.Min.Y + pad), L.Coin.Balance,
            accent, scale);

        var wallet = coins.Wallet;
        var sample = context.Preview && wallet is null && session.CurrentUser is null;
        var target = sample ? WidgetSamples.CoinBalance : wallet?.Balance ?? session.CurrentUser?.Coins ?? 0;
        balance.Update((int)Math.Clamp(target, 0, int.MaxValue), context.Delta);

        var text = WidgetText.Number(ref balanceText, balance.Display);
        var valueStyle = WidgetType.Hero(context.Size);
        var valueScale = valueStyle.Scale * balance.PopScale;
        var fitted = Typography.FitScale(text, bounds.Width - pad * 2f, valueScale, 0.9f, valueStyle.Weight);
        var valueSize = Typography.Measure(text, fitted, valueStyle.Weight);
        var center = new Vector2(bounds.Center.X, bounds.Center.Y + 4f * scale);
        Typography.Draw(context.DrawList, new Vector2(center.X - valueSize.X * 0.5f, center.Y - valueSize.Y * 0.5f),
            text, ink.Primary, fitted, valueStyle.Weight);

        if (context.Size != WidgetSize.Medium || wallet is null && !sample)
        {
            return;
        }

        var earned = sample ? WidgetSamples.CoinEarnedToday : wallet!.EarnedToday;
        var cap = sample ? WidgetSamples.CoinDailyCap : wallet!.DailyCap;
        var progress = ProgressText(earned, cap);
        var progressSize = Typography.Measure(progress, TextStyles.Footnote);
        Typography.Draw(context.DrawList,
            new Vector2(bounds.Center.X - progressSize.X * 0.5f, bounds.Max.Y - pad - progressSize.Y),
            progress, ink.Secondary, TextStyles.Footnote);
    }

    private string ProgressText(long earned, long cap)
    {
        var key = earned * 1_000_003L + cap;
        if (progressText.IsCurrent(key))
        {
            return progressText.Value;
        }

        return progressText.Store(key, Loc.T(L.Coin.CapProgress, NumberText.Group(earned), NumberText.Group(cap)));
    }

    public void Dispose()
    {
    }
}
