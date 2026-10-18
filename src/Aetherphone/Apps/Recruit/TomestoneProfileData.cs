using System;
using System.Collections.Generic;

namespace Aetherphone.Apps.Recruit;

internal sealed record TomestoneProgFight(string BossName, float HpPercent);
internal sealed record TomestoneSavageTier(string Series, string TierName, bool IsCleared);

internal sealed class TomestoneRaidBadge
{
    public string Code { get; }
    public string Name { get; }
    public bool IsCleared { get; set; }
    public TomestoneRaidBadge(string code, string name, bool isCleared)
    {
        Code = code;
        Name = name;
        IsCleared = isCleared;
    }
}

internal sealed class TomestoneProfileData
{
    public string CharacterId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? CustomWebUrl { get; set; }
    public string WebUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CustomWebUrl))
            {
                return CustomWebUrl;
            }
            if (!string.IsNullOrWhiteSpace(CharacterId))
            {
                var slug = string.IsNullOrWhiteSpace(CharacterName)
                    ? "character"
                    : Uri.EscapeDataString(CharacterName.Trim().ToLowerInvariant().Replace(' ', '-'));
                return $"https://tomestone.gg/character/{CharacterId}/{slug}";
            }
            return "https://tomestone.gg";
        }
    }

    public TomestoneProgFight? Progression { get; set; }
    public List<TomestoneSavageTier> SavageTiers { get; set; } = new()
    {
        new("The Arcadion", "Light-Heavyweight", false),
        new("The Arcadion", "Cruiserweight", false),
        new("The Arcadion", "Heavyweight", false),
    };

    public List<TomestoneRaidBadge> Ultimates { get; set; } = new()
    {
        new("UWU", "The Weapon's Refrain", false),
        new("UCOB", "The Unending Coil of Bahamut", false),
        new("TEA", "The Epic of Alexander", false),
        new("DSR", "Dragonsong's Reprise", false),
        new("TOP", "The Omega Protocol", false),
        new("FRU", "Futures Rewritten", false),
        new("DMU", "Dancing Mad", false),
    };

    public List<TomestoneRaidBadge> Extremes { get; set; } = new()
    {
        new("VAL", "Worqor Lar Dor (Valigarmanda)", false),
        new("ZOR", "Everkeep (Zoraal Ja)", false),
        new("SPH", "Sphene's Burden (Queen Eternal)", false),
        new("ZEL", "Recollection (Zelenia)", false),
        new("NEC", "Necron's Embrace (Necron)", false),
        new("DT", "Hell on Rails (Doom Train)", false),
        new("ENU", "The Unmaking (Enuo)", false),
    };
}