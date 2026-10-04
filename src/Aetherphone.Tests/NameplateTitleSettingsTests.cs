using Aetherphone.Core.Honorific;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NameplateTitleSettingsTests
{
    [Fact]
    public void NormalizeListsEveryStatusOnceInCatalogOrder()
    {
        var settings = new NameplateTitleSettings();

        settings.Normalize();

        Assert.Equal(NameplateStatusCatalog.All.Length, settings.Order.Length);
        for (var index = 0; index < settings.Order.Length; index++)
        {
            Assert.Equal(NameplateStatusCatalog.All[index].Status, settings.Order[index]);
        }
    }

    [Fact]
    public void NormalizeKeepsTheUsersOrderAndDropsDuplicates()
    {
        var settings = new NameplateTitleSettings
        {
            Order = new[] { NameplateStatus.NowPlaying, NameplateStatus.Custom, NameplateStatus.NowPlaying },
        };

        settings.Normalize();

        Assert.Equal(NameplateStatus.NowPlaying, settings.Order[0]);
        Assert.Equal(NameplateStatus.Custom, settings.Order[1]);
        Assert.Equal(NameplateStatusCatalog.All.Length, settings.Order.Length);
        Assert.Single(settings.Order, status => status == NameplateStatus.NowPlaying);
    }

    [Fact]
    public void NormalizeSlotsNewStatusesAboveTheIdleFallbacks()
    {
        var settings = new NameplateTitleSettings
        {
            Order = new[]
            {
                NameplateStatus.NowPlaying, NameplateStatus.Handle, NameplateStatus.Custom,
            },
        };

        settings.Normalize();

        var handle = Array.IndexOf(settings.Order, NameplateStatus.Handle);
        Assert.Equal(NameplateStatus.NowPlaying, settings.Order[0]);
        Assert.Equal(settings.Order.Length - 2, handle);
        Assert.Equal(NameplateStatus.Custom, settings.Order[^1]);
        Assert.True(Array.IndexOf(settings.Order, NameplateStatus.SlotsWin) <
                    Array.IndexOf(settings.Order, NameplateStatus.Gamba));
    }

    [Fact]
    public void FirstNormalizeCarriesOldCombinedSwitchesOver()
    {
        var settings = new NameplateTitleSettings { Statuses = NameplateStatus.Chirper | NameplateStatus.InCall };

        settings.Normalize();

        Assert.True(settings.Shows(NameplateStatus.Aethergram));
        Assert.True(settings.Shows(NameplateStatus.DoNotDisturb));
    }

    [Fact]
    public void MoveSwapsNeighboursAndIgnoresTheEdges()
    {
        var settings = new NameplateTitleSettings();
        settings.Normalize();
        var first = settings.Order[0];
        var second = settings.Order[1];

        settings.Move(1, -1);
        settings.Move(0, -1);

        Assert.Equal(second, settings.Order[0]);
        Assert.Equal(first, settings.Order[1]);
    }

    [Fact]
    public void NormalizeClampsTheTurnInterval()
    {
        var settings = new NameplateTitleSettings { TurnSeconds = 0 };

        settings.Normalize();

        Assert.Equal(NameplateLongTitles.TakeTurns, settings.LongTitles);
        Assert.Equal(NameplateTitleSettings.MinimumTurnSeconds, settings.TurnSeconds);
    }

    [Fact]
    public void BlankTemplateFallsBackToDefault()
    {
        var settings = new NameplateTitleSettings();

        settings.SetTemplate(NameplateStatus.Velvet, "  My Velvet: [handle]  ");
        Assert.Equal("My Velvet: [handle]", settings.Template(NameplateStatus.Velvet));

        settings.SetTemplate(NameplateStatus.Velvet, "   ");
        Assert.Equal(string.Empty, settings.Template(NameplateStatus.Velvet));
    }
}
