using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddSocialTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "chirper", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Chirper, L.Onboarding.ChirperBody),
                GuideStep.Point(L.Onboarding.ChirperTabsTitle, L.Onboarding.ChirperTabsBody, "chirper.tabs"),
                GuideStep.Point(L.Onboarding.ChirperPostTitle, L.Onboarding.ChirperPostBody, "chirper.compose"),
                GuideStep.Point(L.Onboarding.ChirperSearchTitle, L.Onboarding.ChirperSearchBody, "chirper.search"),
                GuideStep.Point(L.Onboarding.ChirperActivityTitle, L.Onboarding.ChirperActivityBody,
                    "chirper.activity"),
                GuideStep.Note(L.Onboarding.ChirperKindTitle, L.Onboarding.ChirperKindBody),
            });
        Add(tours, "aethergram", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Aethergram, L.Onboarding.AethergramBody),
                GuideStep.Point(L.Onboarding.AethergramShareTitle, L.Onboarding.AethergramShareBody,
                    "aethergram.compose"),
                GuideStep.Tap(L.Onboarding.AethergramSearchTitle, L.Onboarding.AethergramSearchBody,
                    "aethergram.tab.search", "aethergram.tab.search"),
                GuideStep.Point(L.Onboarding.AethergramActivityTitle, L.Onboarding.AethergramActivityBody,
                    "aethergram.activity"),
                GuideStep.Tap(L.Onboarding.AethergramProfileTitle, L.Onboarding.AethergramProfileBody,
                    "aethergram.tab.profile", "aethergram.tab.profile"),
                GuideStep.Note(L.Onboarding.AethergramSafeTitle, L.Onboarding.AethergramSafeBody),
                GuideStep.Note(L.Onboarding.AethergramKindTitle, L.Onboarding.AethergramKindBody),
            });
        Add(tours, "polls", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Polls, L.Onboarding.PollsBody),
                GuideStep.Point(L.Onboarding.PollsVoteTitle, L.Onboarding.PollsVoteBody, "polls.card"),
                GuideStep.Point(L.Onboarding.PollsResultsTitle, L.Onboarding.PollsResultsBody, "polls.card"),
            });
        Add(tours, "announcements", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Announcements, L.Onboarding.AnnouncementsBody),
                GuideStep.Point(L.Onboarding.AnnouncementsCardTitle, L.Onboarding.AnnouncementsCardBody,
                    "announcements.card"),
                GuideStep.Note(L.Onboarding.AnnouncementsQuietTitle, L.Onboarding.AnnouncementsQuietBody),
            });
    }
}
