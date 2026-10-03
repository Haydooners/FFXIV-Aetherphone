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

    public const long TomestoneWeekly = 450;
    public const long TomestoneWeeklyCap = 900;
    public const uint JobClassJobId = 21;
    public const int JobLevel = 100;
    public const int JobItemLevel = 745;
    public const float JobExperience = 0.62f;
    public const int HousingPhaseHours = 53;
    public const int HousingOpenPlots = 14;

    public static readonly uint[] GearsetJobIds = { 19, 24, 25 };
    public static readonly uint[] AetheryteIds = { 8, 2, 9, 111 };
    public static readonly string[] HuntMarks = { "Neyoozoteel", "Kirlirger the Abhorrent", "Ihnuxokiy" };
    public static readonly int[] HuntMinutesAgo = { 4, 17, 31 };
    public static readonly int[] HousingWards = { 5, 12, 21 };
    public static readonly int[] HousingPlots = { 12, 3, 27 };

    public static bool Checked(int index) => index % 3 == 0;

    public static long CurrencyAmount(int index) => 120 + (index * 937 + 311) % 1880;
}
