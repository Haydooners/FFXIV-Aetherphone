using Aetherphone.Core.Localization;

namespace Aetherphone.Windows.Widgets;

internal static class WidgetSamples
{
    public const long CoinBalance = 12450;
    public const long CoinEarnedToday = 180;
    public const long CoinDailyCap = 300;
    public const long Gil = 1284500;

    public static readonly string[] Names = { "Aerin Vale", "Koto Mizu", "Rhea Solace", "Bram Ashford" };
    public static readonly string[] Worlds = { "Ultros", "Cactuar", "Balmung", "Twintania" };
    public static readonly LocString[] Events = { L.Widgets.SampleEvent, L.Widgets.SampleEventLater };

    public static float Fraction(int index) => index switch
    {
        0 => 0.72f,
        1 => 0.45f,
        _ => 0.88f,
    };
}
