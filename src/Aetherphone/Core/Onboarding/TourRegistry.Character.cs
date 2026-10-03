using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddCharacterTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "character", 3,
            new[]
            {
                GuideStep.Point(L.Onboarding.ActivityRingsTitle, L.Onboarding.ActivityRingsBody, "character.rings",
                    GuideGesture.None),
                GuideStep.TryUntil(L.Onboarding.ActivityHistoryTitle, L.Onboarding.ActivityHistoryBody,
                    "character.tab.history", GuideGesture.Tap, "character.week"),
                GuideStep.Point(L.Onboarding.ActivityWeekTitle, L.Onboarding.ActivityWeekBody, "character.week",
                    GuideGesture.None),
            });
        Add(tours, "collections", 3,
            new[]
            {
                GuideStep.TryUntil(L.Onboarding.CollectionsOpenTitle, L.Onboarding.CollectionsOpenBody,
                    "collections.tile.mounts", GuideGesture.Tap, "collections.search"),
                GuideStep.TryTap(L.Onboarding.CollectionsShowMissingTitle, L.Onboarding.CollectionsShowMissingBody,
                    "collections.filter.missing"),
                GuideStep.TryUntil(L.Onboarding.CollectionsItemTitle, L.Onboarding.CollectionsItemBody,
                    "collections.row", GuideGesture.Tap, "collections.detail"),
            });
        Add(tours, "inventory", 3,
            new[]
            {
                GuideStep.Point(L.Onboarding.InventoryFindTitle, L.Onboarding.InventoryFindBody, "inventory.search",
                    GuideGesture.None),
                GuideStep.TryUntil(L.Onboarding.InventoryOpenTitle, L.Onboarding.InventoryOpenBody,
                    "inventory.storage", GuideGesture.Tap, "inventory.source"),
            });
        Add(tours, "jobs", 2,
            new[]
            {
                GuideStep.Point(L.Onboarding.JobsEquipTitle, L.Onboarding.JobsEquipBody, "jobs.row",
                    GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.JobsSortTitle, L.Onboarding.JobsSortBody, "jobs.row.menu",
                    GuideGesture.Tap),
                GuideStep.TryUntil(L.Onboarding.JobsGroupsTitle, L.Onboarding.JobsGroupsBody, "jobs.categories",
                    GuideGesture.Tap, "jobs.categories.menu"),
            });
        Add(tours, "dailies", 3,
            new[]
            {
                GuideStep.Point(L.Onboarding.DailiesProgressTitle, L.Onboarding.DailiesProgressBody, "dailies.hero",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.DailiesAutoTitle, L.Onboarding.DailiesAutoBody, "dailies.auto",
                    GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.DailiesTickTitle, L.Onboarding.DailiesTickBody, "dailies.manual"),
                GuideStep.TryTap(L.Onboarding.DailiesWeeklyTitle, L.Onboarding.DailiesWeeklyBody,
                    "dailies.tab.weekly"),
            });
        Add(tours, "housing", 2,
            new[]
            {
                GuideStep.Point(L.Onboarding.HousingWhereTitle, L.Onboarding.HousingWhereBody, "housing.context",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.HousingNarrowTitle, L.Onboarding.HousingNarrowBody, "housing.filters",
                    GuideGesture.None),
                GuideStep.TryUntil(L.Onboarding.HousingPlotTitle, L.Onboarding.HousingPlotBody, "housing.map",
                    GuideGesture.Tap, "housing.sheet"),
                GuideStep.Span(L.Onboarding.HousingTrackTitle, L.Onboarding.HousingTrackBody, "housing.sheet.watch",
                    "housing.sheet.remind"),
            });
        Add(tours, "wallet", 3,
            new[]
            {
                GuideStep.Point(L.Onboarding.WalletBalanceTitle, L.Onboarding.WalletBalanceBody, "wallet.gil",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.WalletCapTitle, L.Onboarding.WalletCapBody, "wallet.capped",
                    GuideGesture.None),
            });
    }
}
