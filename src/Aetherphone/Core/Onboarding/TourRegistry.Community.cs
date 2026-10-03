using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddCommunityTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "velvet", 4,
            new[]
            {
                GuideStep.Point(L.Onboarding.VelvetDiscoverTitle, L.Onboarding.VelvetDiscoverBody,
                    "velvet.discover.card"),
                GuideStep.Point(L.Onboarding.VelvetSearchTitle, L.Onboarding.VelvetSearchBody,
                    "velvet.discover.search"),
                GuideStep.Point(L.Onboarding.VelvetFilterTitle, L.Onboarding.VelvetFilterBody, "velvet.discover.filter"),
                GuideStep.Tap(L.Onboarding.VelvetFeedTitle, L.Onboarding.VelvetFeedBody, "velvet.tab.feed",
                    "velvet.tab.feed"),
                GuideStep.Tap(L.Onboarding.VelvetMessagesTitle, L.Onboarding.VelvetMessagesBody, "velvet.tab.messages",
                    "velvet.tab.messages"),
                GuideStep.Tap(L.Onboarding.VelvetProfileTitle, L.Onboarding.VelvetProfileBody, "velvet.tab.me",
                    "velvet.tab.me"),
                GuideStep.Note(L.Onboarding.VelvetKindTitle, L.Onboarding.VelvetKindBody, "velvet.tab.discover"),
            });
        Add(tours, "muster", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Muster, L.Onboarding.MusterBody),
                GuideStep.Point(L.Onboarding.MusterScopeTitle, L.Onboarding.MusterScopeBody, "muster.scope"),
                GuideStep.Point(L.Onboarding.MusterCategoriesTitle, L.Onboarding.MusterCategoriesBody,
                    "muster.categories"),
                GuideStep.Point(L.Onboarding.MusterStartTitle, L.Onboarding.MusterStartBody, "muster.start"),
                GuideStep.Note(L.Onboarding.MusterSafetyTitle, L.Onboarding.MusterSafetyBody),
            });
        Add(tours, "yellowpages", 1,
            new[]
            {
                GuideStep.Note(L.Apps.YellowPages, L.Onboarding.YellowPagesBody),
                GuideStep.Point(L.Onboarding.YellowPagesScopeTitle, L.Onboarding.YellowPagesScopeBody,
                    "yellowpages.scope"),
                GuideStep.Point(L.Onboarding.YellowPagesSearchTitle, L.Onboarding.YellowPagesSearchBody,
                    "yellowpages.search"),
                GuideStep.Point(L.Onboarding.YellowPagesPostTitle, L.Onboarding.YellowPagesPostBody,
                    "yellowpages.tab.post"),
                GuideStep.Point(L.Onboarding.YellowPagesInquiriesTitle, L.Onboarding.YellowPagesInquiriesBody,
                    "yellowpages.tab.inquiries"),
                GuideStep.Note(L.Onboarding.YellowPagesSafetyTitle, L.Onboarding.YellowPagesSafetyBody),
            });
    }
}
