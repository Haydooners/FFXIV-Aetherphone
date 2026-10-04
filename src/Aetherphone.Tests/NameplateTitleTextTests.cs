using System.Numerics;
using Aetherphone.Core.Honorific;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NameplateTitleTextTests
{
    [Fact]
    public void ShortTextIsKept()
    {
        Assert.Equal("Jam · 4KX 9QP", NameplateTitleText.Pair("Jam", "4KX 9QP"));
    }

    [Fact]
    public void LongTextIsCutToHonorificLimit()
    {
        var fitted = NameplateTitleText.Pair("Hosting", "Moonlit Bards of Ul'dah Night Out");

        Assert.Equal(NameplateTitleText.MaxLength, fitted.Length);
        Assert.EndsWith("…", fitted);
    }

    [Fact]
    public void EmptyDetailLeavesOnlyTheLead()
    {
        Assert.Equal("On air", NameplateTitleText.Pair("On air", "  "));
    }

    [Fact]
    public void NowPlayingDropsTheArtistBeforeCuttingTheSong()
    {
        var withArtist = NameplateTitleText.NowPlaying("Answers", "Susan Calloway");
        var withoutArtist = NameplateTitleText.NowPlaying("Flow (Feast of the Ancients)", "Susan Calloway");

        Assert.Equal("♪ Answers · Susan Calloway", withArtist);
        Assert.Equal("♪ Flow (Feast of the Ancients)", withoutArtist);
    }

    [Fact]
    public void NowPlayingWithoutTitleIsEmpty()
    {
        Assert.Equal(string.Empty, NameplateTitleText.NowPlaying(" ", "Artist"));
    }

    [Fact]
    public void HandleGetsExactlyOneMark()
    {
        Assert.Equal("@mira", NameplateTitleText.Handle("@mira"));
        Assert.Equal("@mira", NameplateTitleText.Handle("mira"));
        Assert.Equal(string.Empty, NameplateTitleText.Handle("@"));
    }

    [Fact]
    public void AppTagNamesTheApp()
    {
        Assert.Equal("Chirper · @mira", NameplateTitleText.AppTag("Chirper", "mira"));
        Assert.Equal(string.Empty, NameplateTitleText.AppTag("Velvet", string.Empty));
    }

    [Fact]
    public void JsonRoundTripsThroughTheReader()
    {
        var look = new TitleLook(new Vector3(1f, 0.5f, 0f), new Vector3(0.1f, 0.2f, 0.3f), null, 12, 1);
        var title = new NameplateTitle(NameplateTitleKind.Jam, "Jam · 4KX 9QP", look, true);

        var json = NameplateTitleText.ToJson(title);
        var read = NameplateTitleText.TryReadTitle(json, out var text, out var readLook);

        Assert.True(read);
        Assert.Equal(title.Text, text);
        Assert.Equal(look, readLook);
        Assert.Contains("\"IsPrefix\":true", json);
        Assert.Contains("\"IsOriginal\":false", json);
        Assert.DoesNotContain("Color3", json);
    }

    [Fact]
    public void ReaderIgnoresVanillaTitles()
    {
        const string vanilla = "{\"Title\":\"the Insatiable\",\"IsPrefix\":false,\"IsOriginal\":true}";

        Assert.False(NameplateTitleText.TryReadTitle(vanilla, out _, out _));
        Assert.False(NameplateTitleText.TryReadTitle(string.Empty, out _, out _));
        Assert.False(NameplateTitleText.TryReadTitle("not json", out _, out _));
    }

    [Fact]
    public void ReaderAcceptsHonorificFormatting()
    {
        const string honorific =
            "{\"Title\":\"Warrior of Light\",\"IsPrefix\":false,\"IsOriginal\":false," +
            "\"Color\":{\"X\":1.0,\"Y\":0.8,\"Z\":0.2},\"Glow\":null,\"Color3\":null," +
            "\"GradientColourSet\":3,\"GradientAnimationStyle\":0}";

        var read = NameplateTitleText.TryReadTitle(honorific, out var text, out var look);

        Assert.True(read);
        Assert.Equal("Warrior of Light", text);
        Assert.Null(look.Glow);
        Assert.Equal(3, look.GradientColourSet);
        Assert.Equal(0, look.GradientAnimationStyle);
    }
}
