using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Shell;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LiveBackdropPlanTests
{
    private static readonly Rect Viewport = new(Vector2.Zero, new Vector2(2560f, 1440f));

    [Fact]
    public void LadderHalvesThePhoneRegionAtEveryLevelAndRoundsUp()
    {
        var region = new Vector2(361f, 781f);
        Assert.Equal((181, 391), LiveBackdropPlan.LevelSize(region, 0));
        Assert.Equal((91, 196), LiveBackdropPlan.LevelSize(region, 1));
        Assert.Equal((46, 98), LiveBackdropPlan.LevelSize(region, 2));
        Assert.Equal((23, 49), LiveBackdropPlan.LevelSize(region, 3));
    }

    [Fact]
    public void LadderNeverProducesAnEmptyTexture()
    {
        Assert.Equal((1, 1), LiveBackdropPlan.LevelSize(new Vector2(1f, 1f), LiveBackdropPlan.Depth - 1));
        Assert.Equal((1, 1), LiveBackdropPlan.LevelSize(Vector2.Zero, 0));
        Assert.Equal((1, 1), LiveBackdropPlan.LevelSize(new Vector2(-40f, -10f), 0));
    }

    [Fact]
    public void SmoothLevelMatchesTheQuarterLevel()
    {
        var region = new Vector2(720f, 1560f);
        Assert.Equal(LiveBackdropPlan.LevelSize(region, 1), LiveBackdropPlan.SmoothSize(region));
        Assert.Equal((180, 390), LiveBackdropPlan.SmoothSize(region));
    }

    [Fact]
    public void WindowInsideTheViewportMapsToItsUvAndFillsTheWholeTarget()
    {
        var screen = new Rect(new Vector2(640f, 360f), new Vector2(1280f, 1080f));
        var window = LiveBackdropPlan.Window(screen, Viewport);
        Assert.True(window.Visible);
        Assert.Equal(new Vector2(0.25f, 0.25f), window.Uv0);
        Assert.Equal(new Vector2(0.5f, 0.75f), window.Uv1);
        Assert.Equal(Vector2.Zero, window.DestinationMin);
        Assert.Equal(Vector2.One, window.DestinationMax);
    }

    [Fact]
    public void WindowHangingOffTheLeftEdgeClampsUvAndShrinksTheDestination()
    {
        var screen = new Rect(new Vector2(-200f, 100f), new Vector2(200f, 900f));
        var window = LiveBackdropPlan.Window(screen, Viewport);
        Assert.True(window.Visible);
        Assert.Equal(0f, window.Uv0.X);
        Assert.Equal(200f / 2560f, window.Uv1.X, 5);
        Assert.Equal(0.5f, window.DestinationMin.X, 5);
        Assert.Equal(1f, window.DestinationMax.X, 5);
        Assert.Equal(0f, window.DestinationMin.Y, 5);
        Assert.Equal(1f, window.DestinationMax.Y, 5);
    }

    [Fact]
    public void WindowHangingOffTheBottomRightCornerKeepsTheVisibleFractionUnstretched()
    {
        var screen = new Rect(new Vector2(2400f, 1200f), new Vector2(2800f, 2000f));
        var window = LiveBackdropPlan.Window(screen, Viewport);
        Assert.True(window.Visible);
        Assert.Equal(Vector2.One, window.Uv1);
        Assert.Equal(Vector2.Zero, window.DestinationMin);
        Assert.Equal(0.4f, window.DestinationMax.X, 5);
        Assert.Equal(0.3f, window.DestinationMax.Y, 5);
    }

    [Fact]
    public void WindowEntirelyOutsideTheViewportIsNotVisible()
    {
        var screen = new Rect(new Vector2(3000f, 100f), new Vector2(3400f, 900f));
        Assert.False(LiveBackdropPlan.Window(screen, Viewport).Visible);
    }

    [Fact]
    public void WindowWithNoViewportOrNoScreenIsNotVisible()
    {
        var screen = new Rect(new Vector2(100f, 100f), new Vector2(500f, 900f));
        Assert.False(LiveBackdropPlan.Window(screen, new Rect(Vector2.Zero, Vector2.Zero)).Visible);
        Assert.False(LiveBackdropPlan.Window(new Rect(screen.Min, screen.Min), Viewport).Visible);
    }

    [Fact]
    public void ViewportOffsetIsSubtractedBeforeMapping()
    {
        var offsetViewport = new Rect(new Vector2(100f, 50f), new Vector2(1100f, 1050f));
        var screen = new Rect(new Vector2(600f, 550f), new Vector2(1100f, 1050f));
        var window = LiveBackdropPlan.Window(screen, offsetViewport);
        Assert.Equal(new Vector2(0.5f, 0.5f), window.Uv0);
        Assert.Equal(Vector2.One, window.Uv1);
    }

    [Fact]
    public void CompositeCaptureIsDampedAndWorldCaptureIsNot()
    {
        Assert.Equal(0.7f, LiveBackdropPlan.FeedbackBlend(LiveGlassSource.Composite));
        Assert.Equal(1f, LiveBackdropPlan.FeedbackBlend(LiveGlassSource.World));
    }
}
