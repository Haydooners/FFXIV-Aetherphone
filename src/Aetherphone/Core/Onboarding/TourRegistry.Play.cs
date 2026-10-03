using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddPlayTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "games", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.GamesTitle, L.Onboarding.GamesBody),
                GuideStep.Point(L.Onboarding.GamesFeaturedTitle, L.Onboarding.GamesFeaturedBody, "games.featured"),
                GuideStep.Point(L.Onboarding.GamesLibraryTitle, L.Onboarding.GamesLibraryBody, "games.library"),
            });
        Add(tours, "casino", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Casino, L.Onboarding.CasinoBody),
                GuideStep.Point(L.Onboarding.CasinoChipsTitle, L.Onboarding.CasinoChipsBody, "casino.chipbar"),
                GuideStep.Point(L.Onboarding.CasinoSpinTitle, L.Onboarding.CasinoSpinBody, "casino.spin"),
                GuideStep.Point(L.Onboarding.CasinoFloorTitle, L.Onboarding.CasinoFloorBody, "casino.games"),
                GuideStep.Point(L.Onboarding.CasinoRecordsTitle, L.Onboarding.CasinoRecordsBody, "casino.records"),
                GuideStep.Point(L.Onboarding.CasinoLimitsTitle, L.Onboarding.CasinoLimitsBody, "casino.limits"),
                GuideStep.Tap(L.Onboarding.CasinoLiveTitle, L.Onboarding.CasinoLiveBody, "casino.tabs",
                    "casino.tab.live"),
                GuideStep.Point(L.Onboarding.CasinoRoomsTitle, L.Onboarding.CasinoRoomsBody, "casino.live.rooms"),
            });
        Add(tours, "coin", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Coin, L.Onboarding.CoinBody),
                GuideStep.Point(L.Onboarding.CoinBalanceTitle, L.Onboarding.CoinBalanceBody, "coin.balance"),
                GuideStep.Point(L.Onboarding.CoinCheckInTitle, L.Onboarding.CoinCheckInBody, "coin.checkin"),
                GuideStep.Point(L.Onboarding.CoinEarnTitle, L.Onboarding.CoinEarnBody, "coin.earn"),
                GuideStep.Tap(L.Onboarding.CoinShopTitle, L.Onboarding.CoinShopBody, "coin.tabs", "coin.tab.shop"),
                GuideStep.Note(L.Onboarding.CoinFairTitle, L.Onboarding.CoinFairBody),
            });
        Add(tours, "clock", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Clock, L.Onboarding.ClockIntroBody),
                GuideStep.Tap(L.Onboarding.ClockTabsTitle, L.Onboarding.ClockTabsBody, "clock.tabs",
                    "clock.tab.alarms"),
                GuideStep.Point(L.Onboarding.ClockAddTitle, L.Onboarding.ClockAddBody, "clock.add"),
            });
        Add(tours, "calendar", 2,
            new[]
            {
                GuideStep.Point(L.Calendar.Title, L.Onboarding.CalendarBody, "calendar.grid"),
                GuideStep.Point(L.Onboarding.CalendarAgendaTitle, L.Onboarding.CalendarAgendaBody, "calendar.agenda"),
                GuideStep.Point(L.Calendar.NewEvent, L.Onboarding.CalendarAddBody, "calendar.new"),
            });
        Add(tours, "calculator", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Calculator, L.Onboarding.CalculatorBody),
                GuideStep.Point(L.Onboarding.CalculatorTapeTitle, L.Onboarding.CalculatorTapeBody,
                    "calculator.display"),
            });
        Add(tours, "timers", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Timers, L.Onboarding.TimersBody),
                GuideStep.Point(L.Onboarding.TimersResetsTitle, L.Onboarding.TimersResetsBody, "timers.resets"),
                GuideStep.Point(L.Onboarding.TimersRemindersTitle, L.Onboarding.TimersRemindersBody,
                    "timers.reminders"),
            });
    }
}
