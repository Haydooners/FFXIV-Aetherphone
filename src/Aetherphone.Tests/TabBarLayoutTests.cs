using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TabBarLayoutTests
{
    private const float Tolerance = 1e-3f;
    private static readonly Rect Area = new(new Vector2(100f, 200f), new Vector2(460f, 900f));

    public static TheoryData<float> Scales()
    {
        var data = new TheoryData<float>();
        float[] scales = { 1f, 1.5f, 2f };
        for (var index = 0; index < scales.Length; index++)
        {
            data.Add(scales[index]);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void FullCapsuleFloatsInsetFromTheSidesAndAboveTheBottom(float scale)
    {
        var capsule = TabBarLayout.FullCapsule(Area, scale, false);
        Assert.Equal(TabBarLayout.Height * scale, capsule.Height, Tolerance);
        Assert.Equal(Area.Min.X + TabBarLayout.SideInset * scale, capsule.Min.X, Tolerance);
        Assert.Equal(Area.Max.X - TabBarLayout.SideInset * scale, capsule.Max.X, Tolerance);
        Assert.Equal(Area.Max.Y - TabBarLayout.BottomInset * scale, capsule.Max.Y, Tolerance);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ActionCircleTakesTheTrailingRoomTheCapsuleGivesUp(float scale)
    {
        var capsule = TabBarLayout.FullCapsule(Area, scale, true);
        var circle = TabBarLayout.ActionCircle(Area, scale);
        Assert.Equal(TabBarLayout.ActionDiameter * scale, circle.Width, Tolerance);
        Assert.Equal(TabBarLayout.ActionDiameter * scale, circle.Height, Tolerance);
        Assert.Equal(Area.Max.X - TabBarLayout.SideInset * scale, circle.Max.X, Tolerance);
        Assert.Equal(circle.Min.X - TabBarLayout.ActionGap * scale, capsule.Max.X, Tolerance);
        Assert.Equal(capsule.Center.Y, circle.Center.Y, Tolerance);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void CellsPartitionTheInnerWidthEvenly(int count)
    {
        var capsule = TabBarLayout.FullCapsule(Area, 1f, false);
        var inner = capsule.Width - TabBarLayout.CapsulePadding * 2f;
        for (var index = 0; index < count; index++)
        {
            var cell = TabBarLayout.Cell(capsule, count, index, 1f);
            Assert.Equal(inner / count, cell.Width, Tolerance);
            Assert.Equal(capsule.Min.Y, cell.Min.Y, Tolerance);
            Assert.Equal(capsule.Max.Y, cell.Max.Y, Tolerance);
            if (index > 0)
            {
                Assert.Equal(TabBarLayout.Cell(capsule, count, index - 1, 1f).Max.X, cell.Min.X, Tolerance);
            }
        }

        Assert.Equal(capsule.Min.X + TabBarLayout.CapsulePadding, TabBarLayout.Cell(capsule, count, 0, 1f).Min.X,
            Tolerance);
        Assert.Equal(capsule.Max.X - TabBarLayout.CapsulePadding,
            TabBarLayout.Cell(capsule, count, count - 1, 1f).Max.X, Tolerance);
    }

    [Fact]
    public void IconSitsAboveTheLabelInsideTheCell()
    {
        var capsule = TabBarLayout.FullCapsule(Area, 1f, false);
        var cell = TabBarLayout.Cell(capsule, 4, 1, 1f);
        var icon = TabBarLayout.IconCenter(cell, 1f);
        var label = TabBarLayout.LabelCenter(cell, 1f);
        Assert.Equal(cell.Center.X, icon.X, Tolerance);
        Assert.Equal(cell.Center.X, label.X, Tolerance);
        Assert.True(icon.Y - TabBarLayout.IconSize * 0.5f >= cell.Min.Y);
        Assert.True(icon.Y + TabBarLayout.IconSize * 0.5f < label.Y);
        Assert.True(label.Y < cell.Max.Y);
    }

    [Fact]
    public void HighlightStaysInsideItsCell()
    {
        var capsule = TabBarLayout.FullCapsule(Area, 1f, false);
        var cell = TabBarLayout.Cell(capsule, 3, 2, 1f);
        var highlight = TabBarLayout.Highlight(cell, 1f);
        Assert.Equal(cell.Min.X + TabBarLayout.HighlightInset, highlight.Min.X, Tolerance);
        Assert.Equal(cell.Max.X - TabBarLayout.HighlightInset, highlight.Max.X, Tolerance);
        Assert.Equal(cell.Height - TabBarLayout.HighlightInset * 2f, highlight.Height, Tolerance);
    }

    [Fact]
    public void CompactCapsuleCentresWithoutAnActionAndHugsTheLeadingEdgeWithOne()
    {
        var full = TabBarLayout.FullCapsule(Area, 1f, false);
        var width = TabBarLayout.CompactWidth(40f, 1f);
        var centred = TabBarLayout.CompactCapsule(full, width, false);
        Assert.Equal(full.Center.X, centred.Center.X, Tolerance);
        Assert.Equal(width, centred.Width, Tolerance);
        Assert.Equal(full.Min.Y, centred.Min.Y, Tolerance);
        Assert.Equal(full.Max.Y, centred.Max.Y, Tolerance);

        var fullWithAction = TabBarLayout.FullCapsule(Area, 1f, true);
        var leading = TabBarLayout.CompactCapsule(fullWithAction, width, true);
        Assert.Equal(fullWithAction.Min.X, leading.Min.X, Tolerance);
        Assert.Equal(width, leading.Width, Tolerance);
    }

    [Fact]
    public void CompactWidthFollowsTheLabelWithinItsBounds()
    {
        Assert.Equal(TabBarLayout.CompactMinWidth, TabBarLayout.CompactWidth(0f, 1f), Tolerance);
        Assert.Equal(60f + TabBarLayout.CompactLabelPadding * 2f, TabBarLayout.CompactWidth(60f, 1f), Tolerance);
        Assert.Equal(TabBarLayout.CompactMaxWidth, TabBarLayout.CompactWidth(500f, 1f), Tolerance);
        Assert.Equal(TabBarLayout.CompactMinWidth * 2f, TabBarLayout.CompactWidth(0f, 2f), Tolerance);
    }

    [Fact]
    public void CapsuleInterpolatesBetweenFullAndCompact()
    {
        var full = TabBarLayout.FullCapsule(Area, 1f, false);
        var compact = TabBarLayout.CompactCapsule(full, TabBarLayout.CompactWidth(30f, 1f), false);
        Assert.Equal(full, TabBarLayout.Capsule(full, compact, 0f));
        Assert.Equal(compact, TabBarLayout.Capsule(full, compact, 1f));
        var half = TabBarLayout.Capsule(full, compact, 0.5f);
        Assert.Equal((full.Width + compact.Width) * 0.5f, half.Width, Tolerance);
        Assert.Equal(full.Height, half.Height, Tolerance);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ContentInsetClearsTheBarAndItsGap(float scale)
    {
        var inset = TabBarLayout.ContentInset(scale);
        Assert.Equal((TabBarLayout.Height + TabBarLayout.BottomInset + TabBarLayout.ContentGap) * scale, inset,
            Tolerance);
        var content = TabBarLayout.ContentArea(Area, scale);
        Assert.Equal(Area.Max.Y - inset, content.Max.Y, Tolerance);
        Assert.Equal(Area.Min, content.Min);
        var zone = TabBarLayout.Zone(Area, scale);
        Assert.Equal(TabBarLayout.FullCapsule(Area, scale, false).Min.Y, zone.Min.Y, Tolerance);
        Assert.Equal(Area.Max, zone.Max);
    }
}
