using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TabBarShrinkTests
{
    private const float SixtyHertz = 1f / 60f;
    private const int SettleFrames = 90;

    private static TabBarShrink Scrolled(params float[] offsets)
    {
        var shrink = new TabBarShrink();
        for (var index = 0; index < offsets.Length; index++)
        {
            shrink.Observe(offsets[index], 1f);
        }

        return shrink;
    }

    [Fact]
    public void StartsExpanded()
    {
        var shrink = Scrolled(0f);
        Assert.False(shrink.Compact);
        Assert.Equal(0f, shrink.Amount);
    }

    [Fact]
    public void ShrinksOnlyAfterScrollingDownTheTravelDistance()
    {
        var shy = Scrolled(0f, 10f, 20f, TabBarShrink.ShrinkTravel - 1f);
        Assert.False(shy.Compact);
        var committed = Scrolled(0f, 10f, 20f, TabBarShrink.ShrinkTravel);
        Assert.True(committed.Compact);
    }

    [Fact]
    public void GrowsBackAfterScrollingUpTheGrowDistance()
    {
        var shrink = Scrolled(0f, 40f, 80f, 120f);
        Assert.True(shrink.Compact);
        shrink.Observe(120f - TabBarShrink.GrowTravel + 1f, 1f);
        Assert.True(shrink.Compact);
        shrink.Observe(120f - TabBarShrink.GrowTravel, 1f);
        Assert.False(shrink.Compact);
    }

    [Fact]
    public void ReachingTheTopAlwaysExpands()
    {
        var shrink = Scrolled(0f, 60f, 120f);
        Assert.True(shrink.Compact);
        shrink.Observe(TabBarShrink.TopZone, 1f);
        Assert.False(shrink.Compact);
    }

    [Fact]
    public void ATeleportDoesNotShrinkTheBar()
    {
        var shrink = Scrolled(0f, TabBarShrink.JumpLimit + 100f);
        Assert.False(shrink.Compact);
        shrink.Observe(TabBarShrink.JumpLimit + 100f + TabBarShrink.ShrinkTravel - 1f, 1f);
        Assert.False(shrink.Compact);
        shrink.Observe(TabBarShrink.JumpLimit + 100f + TabBarShrink.ShrinkTravel, 1f);
        Assert.True(shrink.Compact);
    }

    [Fact]
    public void AnUpwardWiggleRestartsTheDownwardTravel()
    {
        var shrink = Scrolled(0f, 20f, 15f, 15f + TabBarShrink.ShrinkTravel - 1f);
        Assert.False(shrink.Compact);
        shrink.Observe(15f + TabBarShrink.ShrinkTravel, 1f);
        Assert.True(shrink.Compact);
    }

    [Fact]
    public void TravelScalesWithTheUiScale()
    {
        var shrink = new TabBarShrink();
        shrink.Observe(0f, 2f);
        shrink.Observe(TabBarShrink.ShrinkTravel + 1f, 2f);
        Assert.False(shrink.Compact);
        shrink.Observe(TabBarShrink.ShrinkTravel * 2f, 2f);
        Assert.True(shrink.Compact);
    }

    [Fact]
    public void AmountSettlesWithoutOvershoot()
    {
        var shrink = Scrolled(0f, 60f, 120f);
        var previous = 0f;
        for (var frame = 0; frame < SettleFrames; frame++)
        {
            var amount = shrink.Step(SixtyHertz);
            Assert.True(amount >= previous - 1e-4f);
            Assert.True(amount <= 1f + 1e-4f);
            previous = amount;
        }

        Assert.True(shrink.Amount > 0.98f);
        shrink.Observe(0f, 1f);
        for (var frame = 0; frame < SettleFrames; frame++)
        {
            var amount = shrink.Step(SixtyHertz);
            Assert.True(amount >= -1e-4f);
        }

        Assert.True(shrink.Amount < 0.02f);
    }

    [Fact]
    public void ExpandAndResetReturnToTheFullBar()
    {
        var shrink = Scrolled(0f, 60f, 120f);
        shrink.Step(0.5f);
        shrink.Expand();
        Assert.False(shrink.Compact);
        shrink.Observe(130f, 1f);
        Assert.False(shrink.Compact);

        var reset = Scrolled(0f, 60f, 120f);
        reset.Step(0.5f);
        reset.Reset();
        Assert.False(reset.Compact);
        Assert.Equal(0f, reset.Amount);
        reset.Observe(500f, 1f);
        Assert.False(reset.Compact);
    }
}
