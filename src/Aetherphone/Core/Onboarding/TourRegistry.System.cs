using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddSystemTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "settings", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.SettingsTitle, L.Onboarding.SettingsBody),
                GuideStep.Point(L.Onboarding.SettingsAccountTitle, L.Onboarding.SettingsAccountBody,
                    "settings.account"),
                GuideStep.Point(L.Onboarding.SettingsAppearanceTitle, L.Onboarding.SettingsAppearanceBody,
                    "settings.row.appearance"),
                GuideStep.Point(L.Onboarding.SettingsTutorialsTitle, L.Onboarding.SettingsTutorialsBody,
                    "settings.row.tutorials"),
            });
        Add(tours, "appstore", 1,
            new[]
            {
                GuideStep.Note(L.Apps.AppStore, L.Onboarding.AppStoreBody),
                GuideStep.Point(L.Onboarding.AppStoreGetTitle, L.Onboarding.AppStoreGetBody, "appstore.row"),
                GuideStep.Tap(L.Onboarding.AppStoreBrowseTitle, L.Onboarding.AppStoreBrowseBody, "appstore.tab.apps",
                    "appstore.tab.apps"),
                GuideStep.Point(L.Onboarding.AppStoreSearchTitle, L.Onboarding.AppStoreSearchBody,
                    "appstore.tab.search"),
                GuideStep.Note(L.Onboarding.AppStoreRemoveTitle, L.Onboarding.AppStoreRemoveBody),
            });
        Add(tours, "shortcuts", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Shortcuts, L.Onboarding.ShortcutsBody),
                GuideStep.Point(L.Onboarding.ShortcutsNewTitle, L.Onboarding.ShortcutsNewBody, "shortcuts.new"),
                GuideStep.Point(L.Onboarding.ShortcutsLibraryTitle, L.Onboarding.ShortcutsLibraryBody,
                    "shortcuts.library"),
                GuideStep.Point(L.Onboarding.ShortcutsImportTitle, L.Onboarding.ShortcutsImportBody,
                    "shortcuts.import"),
                GuideStep.Tap(L.Onboarding.ShortcutsPluginsTitle, L.Onboarding.ShortcutsPluginsBody, "shortcuts.tabs",
                    "shortcuts.tab.plugins"),
                GuideStep.Note(L.Onboarding.ShortcutsHomeTitle, L.Onboarding.ShortcutsHomeBody),
            });
        Add(tours, "news", 2,
            new[]
            {
                GuideStep.Note(L.Apps.News, L.Onboarding.NewsBody),
                GuideStep.Point(L.Onboarding.NewsCategoriesTitle, L.Onboarding.NewsCategoriesBody, "news.categories"),
                GuideStep.Point(L.Onboarding.NewsReadTitle, L.Onboarding.NewsReadBody, "news.feed"),
                GuideStep.Point(L.Onboarding.NewsRefreshTitle, L.Onboarding.NewsRefreshBody, "news.refresh"),
            });
        Add(tours, "feedback", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Feedback, L.Onboarding.FeedbackIntroBody),
                GuideStep.Point(L.Onboarding.FeedbackWriteTitle, L.Onboarding.FeedbackWriteBody, "feedback.input"),
                GuideStep.Point(L.Onboarding.FeedbackSendTitle, L.Onboarding.FeedbackSendBody, "feedback.send"),
                GuideStep.Note(L.Onboarding.FeedbackPrivacyTitle, L.Onboarding.FeedbackPrivacyBody),
            });
        Add(tours, "health", 1,
            new[]
            {
                GuideStep.Note(L.Health.Title, L.Onboarding.HealthBody),
                GuideStep.Point(L.Onboarding.HealthTodayTitle, L.Onboarding.HealthTodayBody, "health.today"),
                GuideStep.Tap(L.Onboarding.HealthTabsTitle, L.Onboarding.HealthTabsBody, "health.tabs",
                    "health.tab.goals"),
                GuideStep.Note(L.Onboarding.HealthGoalsTitle, L.Onboarding.HealthGoalsBody),
                GuideStep.Note(L.Onboarding.HealthPrivacyTitle, L.Onboarding.HealthPrivacyBody),
            });
    }
}
