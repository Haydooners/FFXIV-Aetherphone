using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddGameContentTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "skywatcher", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.SkywatcherTitle, L.Onboarding.SkywatcherBody),
                GuideStep.Point(L.Onboarding.SkywatcherCurrentTitle, L.Onboarding.SkywatcherCurrentBody,
                    "skywatcher.current"),
                GuideStep.Point(L.Onboarding.SkywatcherForecastTitle, L.Onboarding.SkywatcherForecastBody,
                    "skywatcher.forecast"),
            });
        Add(tours, "market", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.MarketTitle, L.Onboarding.MarketBody),
                GuideStep.Point(L.Onboarding.MarketSearchTitle, L.Onboarding.MarketSearchBody, "market.search"),
                GuideStep.Point(L.Onboarding.MarketScopeTitle, L.Onboarding.MarketScopeBody, "market.scope"),
                GuideStep.Note(L.Onboarding.MarketStatsTitle, L.Onboarding.MarketStatsBody),
            });
        Add(tours, "maps", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Maps, L.Onboarding.MapsBody),
                GuideStep.Point(L.Onboarding.MapsLocationTitle, L.Onboarding.MapsLocationBody, "maps.location"),
                GuideStep.Point(L.Onboarding.MapsSearchTitle, L.Onboarding.MapsSearchBody, "maps.search"),
                GuideStep.Note(L.Onboarding.MapsStarTitle, L.Onboarding.MapsStarBody),
            });
        Add(tours, "hunts", 5,
            new[]
            {
                GuideStep.Note(L.Apps.Hunts, L.Onboarding.HuntsBody),
                GuideStep.Point(L.Onboarding.HuntsSignInTitle, L.Onboarding.HuntsSignInBody, "hunts.auth"),
                GuideStep.Point(L.Onboarding.HuntsGuideTitle, L.Onboarding.HuntsGuideBody, "hunts.guide"),
                GuideStep.Tap(L.Onboarding.HuntsSettingsTitle, L.Onboarding.HuntsSettingsBody, "hunts.settings",
                    "hunts.tab.settings"),
                GuideStep.Point(L.Onboarding.HuntsMapMarkersTitle, L.Onboarding.HuntsMapMarkersBody,
                    "hunts.settings.nativeMapMarkers"),
            });
        Add(tours, "fishing", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Fishing, L.Onboarding.FishingBody),
                GuideStep.Point(L.Onboarding.FishingHeroTitle, L.Onboarding.FishingHeroBody, "fishing.hero"),
                GuideStep.Point(L.Onboarding.FishingBlueTitle, L.Onboarding.FishingBlueBody, "fishing.bluefish"),
                GuideStep.Point(L.Onboarding.FishingUpcomingTitle, L.Onboarding.FishingUpcomingBody,
                    "fishing.upcoming"),
            });
        Add(tours, "venues", 3,
            new[]
            {
                GuideStep.Note(L.Onboarding.VenuesTitle, L.Onboarding.VenuesBody),
                GuideStep.Point(L.Onboarding.VenuesLiveTitle, L.Onboarding.VenuesLiveBody, "venues.live"),
                GuideStep.Point(L.Onboarding.VenuesCategoriesTitle, L.Onboarding.VenuesCategoriesBody,
                    "venues.categories"),
                GuideStep.Point(L.Onboarding.VenuesFilterTitle, L.Onboarding.VenuesFilterBody, "venues.chips"),
                GuideStep.Point(L.Onboarding.VenuesSearchTitle, L.Onboarding.VenuesSearchBody, "venues.search"),
            });
        Add(tours, "strats", 1,
            new[]
            {
                GuideStep.Note(L.Onboarding.StratsTitle, L.Onboarding.StratsBody),
                GuideStep.Point(L.Onboarding.StratsFightsTitle, L.Onboarding.StratsFightsBody, "strats.fights"),
                GuideStep.Point(L.Onboarding.StratsRoleTitle, L.Onboarding.StratsRoleBody, "strats.role"),
                GuideStep.Point(L.Onboarding.StratsChipsTitle, L.Onboarding.StratsChipsBody, "strats.chips"),
            });
    }
}
