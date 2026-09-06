using Aetherphone.Core.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class UnoRulesTests
{
    private const int RedTwo = 2;       
    private const int BlueTwo = 15;     
    private const int RedDrawTwo = 12;  
    private const int BlueDrawTwo = 25; 
    private const int Wild = 52;        
    private const int WildDrawFour = 53;

    [Fact]
    public void HouseRulesAllowsStackingDrawTwoOnDrawTwo(){

        int topCard = 12; // Red Draw 2
        int myCard = 25; // Blue Draw 2

        bool playableDefault = GameRoomWire.IsPlayable(
            myCard, 0, topCard, 
            GameRoomWire.RuleSetDefault, pendingDrawCount: 2
        );

        bool playableHouse = GameRoomWire.IsPlayable(myCard, 0, topCard, 
        GameRoomWire.RuleSetHouse, pendingDrawCount: 2
        );

        Assert.False(playableDefault);
        Assert.True(playableHouse);
    }

    [Fact]
    public void PendingDraw_HouseRulesAllowsDrawTwoStacking(){

        // Red +2 on top, holding Blue +2
        bool defaultRules = GameRoomWire.IsPlayable(
            BlueDrawTwo, activeColor: 0, topCard: RedDrawTwo, 
            GameRoomWire.RuleSetDefault, pendingDrawCount: 2
        );

        bool houseRules = GameRoomWire.IsPlayable(
            BlueDrawTwo, activeColor: 0, topCard: RedDrawTwo, 
            GameRoomWire.RuleSetHouse, pendingDrawCount: 2
        );

        Assert.False(defaultRules);
        Assert.True(houseRules);
    }
    
    [Fact]
    public void PendingDraw_HouseRulesAllowsWildDrawFourStacking(){

        // Red +2 on top, holding Wild +4
        bool defaultRules = GameRoomWire.IsPlayable(
            WildDrawFour, activeColor: 0, topCard: RedDrawTwo,
            GameRoomWire.RuleSetDefault, pendingDrawCount: 2
        );

        bool houseRules = GameRoomWire.IsPlayable(
            WildDrawFour, activeColor: 0, topCard: RedDrawTwo,
            GameRoomWire.RuleSetHouse, pendingDrawCount: 2
        );

        Assert.False(defaultRules);
        Assert.True(houseRules);
    }

    [Fact]
    public void PendingDraw_NormalMatchingCardBlockedInBothRuleSets(){

        // Red 2 on top of Red +2 during pending draw
        bool defaultRules = GameRoomWire.IsPlayable(
            RedTwo, activeColor: 0, topCard: RedDrawTwo, 
            GameRoomWire.RuleSetDefault, pendingDrawCount: 2
        );

        bool houseRules = GameRoomWire.IsPlayable(
            RedTwo, activeColor: 0, topCard: RedDrawTwo, 
            GameRoomWire.RuleSetHouse, pendingDrawCount: 2
        );

        Assert.False(defaultRules);
        Assert.False(houseRules);
    } 

    [Fact]
    public void NormalTurn_MatchingRankOrColorPlayableInBothRuleSets(){

        // Blue 2 played on Red 2 (matching rank 2, no pending draw)
        bool defaultRules = GameRoomWire.IsPlayable(
            BlueTwo, activeColor: 0, topCard: RedTwo, 
            GameRoomWire.RuleSetDefault, pendingDrawCount: 0
        );

        bool houseRules = GameRoomWire.IsPlayable(
            BlueTwo, activeColor: 0, topCard: RedTwo, 
            GameRoomWire.RuleSetHouse, pendingDrawCount: 0
        );

        Assert.True(defaultRules);
        Assert.True(houseRules);
    }

    [Fact]
    public void NormalTurn_WildCardsPlayableInBothRuleSets(){

        bool defaultWild = GameRoomWire.IsPlayable(
            Wild, activeColor: 1, topCard: RedTwo, 
            GameRoomWire.RuleSetDefault, pendingDrawCount: 0
        );

        bool houseWild = GameRoomWire.IsPlayable(
            Wild, activeColor: 1, topCard: RedTwo, 
            GameRoomWire.RuleSetHouse, pendingDrawCount: 0
        );

        Assert.True(defaultWild);
        Assert.True(houseWild);
    }

    [Fact]
    public void IsZero_IdentifiesZeroRankCards(){
        int redZero = 0;  // Red 0 (0 % 13 == 0)
        int blueZero = 13;// Blue 0 (13 % 13 == 0)
        int redTwo = 2;   // Red 2 (2 % 13 == 2)

        Assert.True(GameRoomWire.IsZero(redZero));
        Assert.True(GameRoomWire.IsZero(blueZero));
        Assert.False(GameRoomWire.IsZero(redTwo));
    }

    [Fact]
    public void IsSeven_IdentifiesSevenRankCards()
    {
        int redSeven = 7;   // Red 7 (7 % 13 == 7)
        int blueSeven = 20; // Blue 7 (20 % 13 == 7)
        int redTwo = 2;     // Red 2 (2 % 13 == 2)

        Assert.True(GameRoomWire.IsSeven(redSeven));
        Assert.True(GameRoomWire.IsSeven(blueSeven));
        Assert.False(GameRoomWire.IsSeven(redTwo));
    }

    [Fact]
    public void IsZero_CorrectlyIdentifiesZeroForEveryColor()
    {
        // Cards 0, 13, 26, 39 are the 0 rank cards for Red, Blue, Green, Yellow
        int redZero = 0;
        int blueZero = 13;
        int greenZero = 26;
        int yellowZero = 39;

        Assert.True(GameRoomWire.IsZero(redZero));
        Assert.True(GameRoomWire.IsZero(blueZero));
        Assert.True(GameRoomWire.IsZero(greenZero));
        Assert.True(GameRoomWire.IsZero(yellowZero));

        // Wilds and non-zero cards should return false
        Assert.False(GameRoomWire.IsZero(52)); // Wild
        Assert.False(GameRoomWire.IsZero(1));  // Red 1
    }

    [Fact]
    public void IsSeven_CorrectlyIdentifiesSevenForEveryColor()
    {
        // Cards 7, 20, 33, 46 are the 7 rank cards for Red, Blue, Green, Yellow
        int redSeven = 7;
        int blueSeven = 20;
        int greenSeven = 33;
        int yellowSeven = 46;

        Assert.True(GameRoomWire.IsSeven(redSeven));
        Assert.True(GameRoomWire.IsSeven(blueSeven));
        Assert.True(GameRoomWire.IsSeven(greenSeven));
        Assert.True(GameRoomWire.IsSeven(yellowSeven));
        
        // Wilds and non-seven cards should return false
        Assert.False(GameRoomWire.IsSeven(53)); // Wild +4
        Assert.False(GameRoomWire.IsSeven(8));  // Red 8
    }
    
}