using System.Globalization;
using Aetherphone.Apps.Settings;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SettingsSearchTests
{
    private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;

    [Fact]
    public void EmptyQueryMatchesEveryRow()
    {
        Assert.True(SettingsSearch.Matches(Compare, "Appearance", string.Empty));
        Assert.True(SettingsSearch.Matches(Compare, string.Empty, string.Empty));
    }

    [Fact]
    public void EmptyTextNeverMatchesAQuery()
    {
        Assert.False(SettingsSearch.Matches(Compare, string.Empty, "a"));
    }

    [Theory]
    [InlineData("Appearance", "appear")]
    [InlineData("Appearance", "ANCE")]
    [InlineData("Notifications and Badges", "badges")]
    public void MatchesIgnoreCaseAnywhereInTheText(string text, string query)
    {
        Assert.True(SettingsSearch.Matches(Compare, text, query));
    }

    [Theory]
    [InlineData("Taille du téléphone", "telephone")]
    [InlineData("Telefongröße", "grosse")]
    [InlineData("Resolución", "RESOLUCION")]
    [InlineData("Sonido", "Sonído")]
    public void MatchesIgnoreAccents(string text, string query)
    {
        Assert.True(SettingsSearch.Matches(Compare, text, query));
    }

    [Theory]
    [InlineData("Appearance", "sound")]
    [InlineData("Privacy", "privacyy")]
    public void UnrelatedQueriesDoNotMatch(string text, string query)
    {
        Assert.False(SettingsSearch.Matches(Compare, text, query));
    }

    [Fact]
    public void PagesMatchOnTitleOrSummary()
    {
        Assert.True(SettingsSearch.MatchesPage(Compare, "Sounds", "Ringtone, Notification Sound", "ringtone"));
        Assert.True(SettingsSearch.MatchesPage(Compare, "Sounds", string.Empty, "sound"));
        Assert.False(SettingsSearch.MatchesPage(Compare, "Sounds", "Ringtone", "wallpaper"));
    }
}
