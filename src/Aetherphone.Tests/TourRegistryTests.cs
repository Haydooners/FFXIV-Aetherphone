using Aetherphone.Core.Onboarding;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TourRegistryTests
{
    // Expected values were read out of TourRegistry before its 410 line BuildTours
    // was split across six partial files, so this table is the pre split behaviour.
    private static readonly Dictionary<string, (int Version, int StepCount)> Expected = new()
    {
        { "messages", (4, 6) },
        { "music", (3, 7) },
        { "character", (3, 3) },
        { "chirper", (3, 6) },
        { "aethergram", (3, 6) },
        { "collections", (3, 3) },
        { "wallet", (3, 2) },
        { "inventory", (3, 2) },
        { "settings", (3, 5) },
        { "camera", (4, 6) },
        { "photos", (3, 5) },
        { "news", (3, 3) },
        { "notes", (3, 6) },
        { "dailies", (3, 4) },
        { "velvet", (5, 8) },
        { "notifications", (3, 2) },
        { "message", (3, 6) },
        { "polls", (5, 2) },
        { "muster", (2, 4) },
        { "yellowpages", (2, 6) },
        { "jobs", (2, 3) },
        { "announcements", (2, 2) },
        { "housing", (2, 4) },
        { "feedback", (4, 4) },
        { "appstore", (2, 5) },
        { "health", (2, 5) },
        { "shortcuts", (2, 6) },
        { "aetherstream", (3, 6) },
        { "games", (3, 4) },
        { "clock", (3, 5) },
        { "calendar", (3, 5) },
        { "calculator", (3, 2) },
        { "timers", (4, 3) },
        { "coin", (2, 5) },
        { "casino", (2, 5) },
        { "skywatcher", (3, 5) },
        { "market", (3, 7) },
        { "strats", (2, 5) },
        { "venues", (4, 6) },
        { "maps", (4, 5) },
        { "fishing", (3, 4) },
        { "hunts", (6, 6) },
    };

    [Fact]
    public void EveryAppTourResolvesWithItsOriginalVersionAndStepCount()
    {
        foreach (var (appId, expected) in Expected)
        {
            Assert.True(TourRegistry.TryGetAppTour(appId, out var sequence), $"no tour registered for '{appId}'");
            Assert.Equal(appId, sequence.Id);
            Assert.Equal(appId, sequence.RequiredAppId);
            Assert.Equal(expected.Version, sequence.ContentVersion);
            Assert.Equal(expected.StepCount, sequence.Steps.Length);
            Assert.True(sequence.IsValid);
        }
    }

    [Fact]
    public void NoTourIsRegisteredBeyondTheExpectedSet()
    {
        Assert.Equal(42, Expected.Count);
        foreach (var appId in new[] { "welcome", "nope", "", "Messages" })
        {
            Assert.False(TourRegistry.TryGetAppTour(appId, out _), $"'{appId}' should not have an app tour");
        }
    }

    [Fact]
    public void TheWelcomeSequenceIsSeparateFromTheAppTours()
    {
        var welcome = TourRegistry.GetWelcome();
        Assert.Equal(TourRegistry.WelcomeId, welcome.Id);
        Assert.Null(welcome.RequiredAppId);
        Assert.True(welcome.IsValid);
    }
}
