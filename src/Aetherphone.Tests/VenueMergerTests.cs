using Aetherphone.Core.Rolladeck;
using Aetherphone.Core.Venues;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VenueMergerTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 3, 0, 0, DateTimeKind.Utc);

    private static VenueEvent Listing(string id, string title, int ward = 25, int plot = 28,
        DateTime? start = null, DateTime? end = null, string? banner = null) =>
        new()
        {
            Id = id,
            Sources = VenueSources.FfxivVenues,
            Title = title,
            Host = string.Empty,
            Description = string.Empty,
            DataCenter = "Light",
            World = "Shiva",
            LocationLine = $"Lavender Beds, W{ward}, P{plot}",
            PlaceLine = $"Shiva · Lavender Beds, W{ward}, P{plot}",
            TeleportCode = null,
            BannerUrl = banner,
            LogoUrl = null,
            StartUtc = start,
            EndUtc = end,
            Tags = Array.Empty<string>(),
            WebsiteUrl = null,
            DiscordUrl = null,
            ListingUrl = null,
            AttendeeCount = 0,
            Address = VenueAddress.Of("Shiva", "Lavender Beds", ward, plot),
        };

    private static DirectoryVenueEntry Directory(string id, string name, string district = "The Lavender Beds",
        int ward = 25, int plot = 28) =>
        new()
        {
            Id = id,
            Name = name,
            Slug = id,
            Server = "shiva",
            Datacenter = "Light",
            District = district,
            Ward = ward,
            Plot = plot,
            LogoUrl = "https://example.com/logo.png",
            BannerUrl = "https://example.com/banner.png",
            Lifestream = "/li Shiva Lavender Beds 25 28",
            Amenities = ["dj", "lgbtqia", "nsfw"],
        };

    private static OpenVenueEntry Open(string name, int ward = 25, int plot = 28, string? dj = "DJ Nyx") =>
        new()
        {
            Name = name,
            Slug = "open-slug",
            Server = "Shiva",
            District = "Lavender Beds",
            Ward = ward,
            Plot = plot,
            DjName = dj,
            DjTwitch = "https://www.twitch.tv/nyx",
        };

    [Fact]
    public void Merge_JoinsDirectoryEntryOntoListingByAddress()
    {
        var merged = VenueMerger.Merge([Listing("ffxiv:a", "Paradise")], [Directory("r1", "Paradise Nightclub")], [],
            [], Now, Now);

        var venue = Assert.Single(merged);
        Assert.Equal("ffxiv:a", venue.Id);
        Assert.Equal(VenueSources.FfxivVenues | VenueSources.Rolladeck, venue.Sources);
        Assert.Equal("https://example.com/banner.png", venue.BannerUrl);
        Assert.Equal("https://example.com/logo.png", venue.LogoUrl);
        Assert.Equal("Shiva Lavender Beds 25 28", venue.TeleportCode);
        Assert.Equal("https://xivrolladeck.com/venue/r1", venue.RolladeckUrl);
    }

    [Fact]
    public void Merge_KeepsListingBannerOverDirectoryBanner()
    {
        var merged = VenueMerger.Merge([Listing("ffxiv:a", "Paradise", banner: "https://own/banner.png")],
            [Directory("r1", "Paradise")], [], [], Now, Now);

        Assert.Equal("https://own/banner.png", Assert.Single(merged).BannerUrl);
    }

    [Fact]
    public void Merge_AddsDirectoryOnlyVenuesWithAmenityTags()
    {
        var merged = VenueMerger.Merge([Listing("ffxiv:a", "Paradise")], [Directory("r2", "Voidsent", ward: 3, plot: 7)],
            [], [], Now, Now);

        Assert.Equal(2, merged.Length);
        var added = merged[1];
        Assert.Equal("rolladeck:r2", added.Id);
        Assert.Equal(VenueSources.Rolladeck, added.Sources);
        Assert.False(added.HasOpening);
        Assert.Equal(["18+", "DJ", "LGBTQIA+"], added.Tags);
    }

    [Fact]
    public void Merge_OpenVenueConfirmsLiveUntilConfirmationLapses()
    {
        var merged = VenueMerger.Merge([Listing("ffxiv:a", "Paradise")], [], [Open("Paradise")], [], Now, Now);

        var venue = Assert.Single(merged);
        Assert.True(venue.IsConfirmedLive(Now));
        Assert.Equal(VenueLiveState.Confirmed, venue.LiveState(Now));
        Assert.Equal("DJ Nyx", venue.LiveHeadline);
        Assert.Equal("https://www.twitch.tv/nyx", venue.TwitchUrl);
        Assert.False(venue.IsConfirmedLive(Now + VenueMerger.ConfirmationLifetime + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Merge_OpenVenueWithoutListingCreatesVenue()
    {
        var merged = VenueMerger.Merge([], [], [Open("Club Nova", ward: 9, plot: 12)], [], Now, Now);

        var venue = Assert.Single(merged);
        Assert.Equal("Club Nova", venue.Title);
        Assert.True(venue.IsConfirmedLive(Now));
    }

    [Fact]
    public void Merge_LiveDjAtAddressAddsViewers()
    {
        var dj = new LiveDjEntry
        {
            DjName = "DJ Yams",
            Server = "Shiva",
            District = "Lavender Beds",
            Ward = 25,
            Plot = 28,
            ViewerCount = 56,
            TwitchUrl = "https://www.twitch.tv/yams",
        };

        var venue = Assert.Single(VenueMerger.Merge([Listing("ffxiv:a", "Paradise")], [], [], [dj], Now, Now));

        Assert.Equal(56, venue.LiveViewers);
        Assert.Equal("DJ Yams", venue.LiveHeadline);
        Assert.True(venue.IsConfirmedLive(Now));
    }

    [Fact]
    public void Merge_EntriesWithoutFullAddressNeverJoin()
    {
        var merged = VenueMerger.Merge([Listing("ffxiv:a", "Paradise")], [Directory("r1", "Other", plot: 0)], [], [],
            Now, Now);

        Assert.Equal(2, merged.Length);
    }

    [Fact]
    public void ScheduledOpen_WithoutEnd_ExpiresAfterFourHours()
    {
        var venue = Listing("ffxiv:a", "Paradise", start: Now.AddHours(-1));

        Assert.True(venue.IsScheduledOpen(Now));
        Assert.False(venue.IsScheduledOpen(Now.AddHours(4)));
    }

    [Fact]
    public void Mapper_KeepsFfxivVenueWithoutScheduledOpening()
    {
        var dto = new FfxivVenueDto
        {
            Id = "abc",
            Name = "Quiet Lounge",
            Location = new FfxivLocationDto { World = "Shiva", DataCenter = "Light", District = "Mist", Ward = 4, Plot = 9 },
        };

        var venue = VenueMapper.FromFfxiv(dto, Now);

        Assert.NotNull(venue);
        Assert.False(venue.HasOpening);
        Assert.Equal("Shiva · Mist, W4, P9", venue.PlaceLine);
        Assert.Equal("Shiva Mist W4 P9", venue.TeleportCode);
        Assert.True(venue.Address.IsKnown);
    }

    [Theory]
    [InlineData("/li Shiva Lavender Beds 25 28", "Shiva Lavender Beds 25 28")]
    [InlineData("Shiva Lavender Beds 25 28", "Shiva Lavender Beds 25 28")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void TeleportDestination_StripsCommandPrefix(string? input, string? expected) =>
        Assert.Equal(expected, RolladeckText.TeleportDestination(input));

    [Fact]
    public void Query_DirectorySortsByTitleAndRailPutsConfirmedFirst()
    {
        var scheduled = Listing("ffxiv:b", "Bravo", ward: 2, plot: 2, start: Now.AddMinutes(-30), end: Now.AddHours(2));
        var confirmedSource = Listing("ffxiv:c", "Charlie", ward: 3, plot: 3);
        var idle = Listing("ffxiv:a", "Alpha", ward: 1, plot: 1);
        var merged = VenueMerger.Merge([scheduled, confirmedSource, idle], [], [Open("Charlie", ward: 3, plot: 3)], [],
            Now, Now);
        var query = new VenueQuery();
        var key = new VenueQueryKey(1, VenueTimeFilter.All, VenueFilter.SourceAll, string.Empty, false, 0, 0,
            string.Empty, 0);

        query.Update(key, merged, [], [], Now);

        Assert.Equal(["Alpha", "Bravo", "Charlie"], Titles(query.Feed));
        Assert.Equal(["Charlie", "Bravo"], Titles(query.Rail));
        Assert.Equal(2, query.LiveCount);
    }

    [Fact]
    public void Query_LiveFilterKeepsOnlyLiveVenuesAndSkipsRebuildForSameKey()
    {
        var live = Listing("ffxiv:b", "Bravo", start: Now.AddMinutes(-5), end: Now.AddHours(1));
        var later = Listing("ffxiv:a", "Alpha", ward: 1, plot: 1, start: Now.AddHours(5), end: Now.AddHours(8));
        var query = new VenueQuery();
        var key = new VenueQueryKey(1, VenueTimeFilter.LiveNow, VenueFilter.SourceAll, string.Empty, false, 0, 0,
            string.Empty, 0);

        Assert.True(query.Update(key, [live, later], [], [], Now));
        Assert.False(query.Update(key, [live, later], [], [], Now));
        Assert.Equal(["Bravo"], Titles(query.Feed));
    }

    [Fact]
    public void Query_SearchMatchesLiveHeadline()
    {
        var merged = VenueMerger.Merge([Listing("ffxiv:a", "Paradise")], [], [Open("Paradise")], [], Now, Now);
        var query = new VenueQuery();
        var key = new VenueQueryKey(1, VenueTimeFilter.All, VenueFilter.SourceAll, string.Empty, false, 0, 0, "nyx", 0);

        query.Update(key, merged, [], [], Now);

        Assert.Equal(["Paradise"], Titles(query.Feed));
    }

    private static string[] Titles(IReadOnlyList<VenueEvent> venues)
    {
        var titles = new string[venues.Count];
        for (var index = 0; index < venues.Count; index++)
        {
            titles[index] = venues[index].Title;
        }

        return titles;
    }
}
