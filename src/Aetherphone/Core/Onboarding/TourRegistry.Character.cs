using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddCharacterTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "character", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Character, L.Onboarding.CharacterBody),
                GuideStep.Point(L.Onboarding.CharacterRingsTitle, L.Onboarding.CharacterRingsBody, "character.rings"),
                GuideStep.Point(L.Onboarding.CharacterSummaryTitle, L.Onboarding.CharacterSummaryBody,
                    "character.summary"),
            });
        Add(tours, "collections", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Collections, L.Onboarding.CollectionsBody),
                GuideStep.Tap(L.Onboarding.CollectionsCategoryTitle, L.Onboarding.CollectionsCategoryBody,
                    "collections.tile.mounts", "collections.category.mounts"),
                GuideStep.Point(L.Onboarding.CollectionsSearchTitle, L.Onboarding.CollectionsSearchBody,
                    "collections.search"),
                GuideStep.Point(L.Onboarding.CollectionsMissingTitle, L.Onboarding.CollectionsMissingBody,
                    "collections.filters"),
            });
        Add(tours, "inventory", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Inventory, L.Onboarding.InventoryBody),
                GuideStep.Point(L.Onboarding.InventorySummaryTitle, L.Onboarding.InventorySummaryBody,
                    "inventory.summary"),
                GuideStep.Point(L.Onboarding.InventorySourcesTitle, L.Onboarding.InventorySourcesBody,
                    "inventory.sources"),
                GuideStep.Point(L.Onboarding.InventorySearchTitle, L.Onboarding.InventorySearchBody,
                    "inventory.search"),
            });
        Add(tours, "jobs", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Jobs, L.Onboarding.JobsBody),
                GuideStep.Point(L.Onboarding.JobsSwitchTitle, L.Onboarding.JobsSwitchBody, "jobs.row"),
                GuideStep.Point(L.Onboarding.JobsCategoriesTitle, L.Onboarding.JobsCategoriesBody, "jobs.categories"),
                GuideStep.Point(L.Onboarding.JobsColorTitle, L.Onboarding.JobsColorBody, "jobs.color"),
            });
        Add(tours, "dailies", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Dailies, L.Onboarding.DailiesBody),
                GuideStep.Tap(L.Onboarding.DailiesCadenceTitle, L.Onboarding.DailiesCadenceBody, "dailies.cadence",
                    "dailies.tab.weekly"),
            });
        Add(tours, "housing", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Housing, L.Onboarding.HousingBody),
                GuideStep.Point(L.Onboarding.HousingContextTitle, L.Onboarding.HousingContextBody, "housing.context"),
                GuideStep.Point(L.Onboarding.HousingMapTitle, L.Onboarding.HousingMapBody, "housing.map"),
                GuideStep.Point(L.Onboarding.HousingPhaseTitle, L.Onboarding.HousingPhaseBody, "housing.phase"),
                GuideStep.Point(L.Onboarding.HousingFiltersTitle, L.Onboarding.HousingFiltersBody, "housing.filters"),
                GuideStep.Point(L.Onboarding.HousingWatchTitle, L.Onboarding.HousingWatchBody, "housing.watchlist"),
                GuideStep.Note(L.Onboarding.HousingDataTitle, L.Onboarding.HousingDataBody),
            });
        Add(tours, "wallet", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Wallet, L.Onboarding.WalletBody),
                GuideStep.Point(L.Onboarding.WalletGilTitle, L.Onboarding.WalletGilBody, "wallet.gil"),
                GuideStep.Point(L.Onboarding.WalletCurrenciesTitle, L.Onboarding.WalletCurrenciesBody,
                    "wallet.currencies"),
            });
    }
}
