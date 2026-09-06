using Aetherphone.Apps.Velvet.Kit;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VelvetPostTagTests
{
    [Fact]
    public void PostTagTokensCoverEveryCategory()
    {
        var expected = 0;
        for (var index = 0; index < VelvetSuggestions.PostTagCategories.Length; index++)
        {
            expected += VelvetSuggestions.PostTagCategories[index].Tags.Length;
        }

        Assert.Equal(expected, VelvetSuggestions.PostTagTokens.Length);
        for (var index = 0; index < VelvetSuggestions.PostTagCategories.Length; index++)
        {
            var tags = VelvetSuggestions.PostTagCategories[index].Tags;
            for (var tagIndex = 0; tagIndex < tags.Length; tagIndex++)
            {
                Assert.Contains(tags[tagIndex], VelvetSuggestions.PostTagTokens);
            }
        }
    }

    [Fact]
    public void PostTagTokensSurviveTheCsvWireFormat()
    {
        var tokens = VelvetSuggestions.PostTagTokens;
        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            Assert.DoesNotContain(',', token);
            Assert.DoesNotContain('\n', token);
            Assert.Equal(token.ToLowerInvariant(), token);
            Assert.True(token.Length <= 40, token);
        }
    }

    [Fact]
    public void EveryPostTagTokenResolvesToALabel()
    {
        var tokens = VelvetSuggestions.PostTagTokens;
        for (var index = 0; index < tokens.Length; index++)
        {
            var label = VelvetTokenLabels.Of(tokens[index]);
            Assert.NotEqual(tokens[index], label);
            Assert.Equal(tokens[index], label.ToLowerInvariant());
        }
    }
}
