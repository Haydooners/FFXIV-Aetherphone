using System;
using System.Collections.Generic;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Lumina.Excel.Sheets;

namespace Aetherphone.Apps.Recruit;

public enum PfDutyCategory : byte
{
    None = 0,
    DutyRoulette = 1,
    Dungeons = 2,
    Guildhests = 3,
    Trials = 4,
    Raids = 5,
    HighEndDuty = 6,
    PvP = 7,
    GoldSaucer = 8,
    FATEs = 9,
    TreasureHunt = 10,
    TheHunt = 11,
    GatheringForays = 12,
    DeepDungeons = 13,
    FieldOperations = 14,
    VCDungeonFinder = 15,
}

public sealed record PfDutyEntry(uint Id, string Name, PfDutyCategory Category, ushort SortKey = 0);

public sealed record PfJobItem(uint Id, string Name, string Abbr, uint IconId, bool IsClass = false);

internal sealed class PfSlotConfig
{
    public HashSet<uint> AllowedJobs { get; } = new();
    public void SetJobs(IEnumerable<uint> jobs)
    {
        AllowedJobs.Clear();
        foreach (var j in jobs)
        {
            AllowedJobs.Add(j);
        }
    }
    public PfSlotRole Role => PfCatalog.DetermineRole(AllowedJobs);
}

internal static class PfCatalog
{
    private static readonly Dictionary<PfDutyCategory, List<PfDutyEntry>> CachedDuties = new();
    private static bool initialized;

    private static readonly string[] TreasureHuntDuties =
    {
        "Timeworn Br'aaxskin Map", "Timeworn Lobo/Gargoyleskin Map",
        "Timeworn Ophiotauroskin Map", "Timeworn Kumbhiraskin Map",
        "Timeworn Zonureskin Map", "Timeworn Gazelleskin Map", "Timeworn Dragonskin Map",
    };

    private static readonly string[] HuntDuties =
    {
        "Dawntrail Hunt Train / Marks", "Endwalker Hunt Train / Marks",
        "Shadowbringers Hunt Train / Marks", "Stormblood Hunt Train / Marks",
        "Heavensward Hunt Train / Marks", "A Realm Reborn Marks",
    };

    private static readonly string[] FateDuties =
    {
        "Dawntrail Gemstone FATEs", "Endwalker Gemstone FATEs",
        "Shadowbringers Gemstone FATEs", "Special Achievement FATEs",
    };

    private static readonly string[] GatheringForayDuties =
    {
        "The Diadem", "Ocean Fishing",
    };

    private static readonly string[] FieldOperationDuties =
    {
        "The Forbidden Land, Eureka: Anemos", "The Forbidden Land, Eureka: Pagos",
        "The Forbidden Land, Eureka: Pyros", "The Forbidden Land, Eureka: Hydatos",
        "The Bozjan Southern Front", "Zadnor", "Delubrum Reginae",
    };

    public static readonly (PfDutyCategory Category, string Label, uint IconId)[] Categories =
    {
        (PfDutyCategory.None, "None", 0),
        (PfDutyCategory.DutyRoulette, "Duty Roulette", 61807),
        (PfDutyCategory.Dungeons, "Dungeons", 61801),
        (PfDutyCategory.Guildhests, "Guildhests", 61803),
        (PfDutyCategory.Trials, "Trials", 61804),
        (PfDutyCategory.Raids, "Raids", 61802),
        (PfDutyCategory.HighEndDuty, "High-end Duty", 60832),
        (PfDutyCategory.PvP, "PvP", 61806),
        (PfDutyCategory.GoldSaucer, "Gold Saucer", 61820),
        (PfDutyCategory.FATEs, "FATEs", 61809),
        (PfDutyCategory.TreasureHunt, "Treasure Hunt", 61808),
        (PfDutyCategory.TheHunt, "The Hunt", 61817),
        (PfDutyCategory.GatheringForays, "Gathering Forays", 61815),
        (PfDutyCategory.DeepDungeons, "Deep Dungeons", 61824),
        (PfDutyCategory.FieldOperations, "Field Operations", 61823),
        (PfDutyCategory.VCDungeonFinder, "V&C Dungeon Finder", 61828),
    };

    public static string GetCategoryLabel(PfDutyCategory category)
    {
        for (var i = 0; i < Categories.Length; i++)
        {
            if (Categories[i].Category == category)
            {
                return Categories[i].Label;
            }
        }
        return "None";
    }

    public static IReadOnlyList<PfDutyEntry> GetDutiesFor(PfDutyCategory category)
    {
        EnsureInitialized();
        if (CachedDuties.TryGetValue(category, out var list))
        {
            return list;
        }
        return Array.Empty<PfDutyEntry>();
    }

    private static void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        foreach (var (cat, _, _) in Categories)
        {
            CachedDuties[cat] = new List<PfDutyEntry>();
        }

        var rouletteSheet = Plugin.DataManager.GetExcelSheet<ContentRoulette>();
        foreach (var r in rouletteSheet)
        {
            var name = r.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name))
            {
                CachedDuties[PfDutyCategory.DutyRoulette].Add(new PfDutyEntry(r.RowId, name, PfDutyCategory.DutyRoulette, r.SortKey));
            }
        }

        var cfcSheet = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>();
        foreach (var row in cfcSheet)
        {
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var contentType = row.ContentType.ValueNullable?.RowId ?? 0;
            var isHighEnd = row.HighEndDuty;

            if (isHighEnd)
            {
                CachedDuties[PfDutyCategory.HighEndDuty].Add(new PfDutyEntry(row.RowId, name, PfDutyCategory.HighEndDuty, row.SortKey));
            }
            else if (contentType == 2)
            {
                CachedDuties[PfDutyCategory.Dungeons].Add(new PfDutyEntry(row.RowId, name, PfDutyCategory.Dungeons, row.SortKey));
            }
            else if (contentType == 3)
            {
                CachedDuties[PfDutyCategory.Guildhests].Add(new PfDutyEntry(row.RowId, name, PfDutyCategory.Guildhests, row.SortKey));
            }
            else if (contentType == 4)
            {
                CachedDuties[PfDutyCategory.Trials].Add(new PfDutyEntry(row.RowId, name, PfDutyCategory.Trials, row.SortKey));
            }
            else if (contentType == 5)
            {
                CachedDuties[PfDutyCategory.Raids].Add(new PfDutyEntry(row.RowId, name, PfDutyCategory.Raids, row.SortKey));
            }
            else if (contentType == 6)
            {
                CachedDuties[PfDutyCategory.PvP].Add(new PfDutyEntry(row.RowId, name, PfDutyCategory.PvP, row.SortKey));
            }
            else if (contentType == 19)
            {
                CachedDuties[PfDutyCategory.GoldSaucer].Add(new PfDutyEntry(row.RowId, name, PfDutyCategory.GoldSaucer, row.SortKey));
            }
            else if (contentType == 21)
            {
                CachedDuties[PfDutyCategory.DeepDungeons].Add(new PfDutyEntry(row.RowId, name, PfDutyCategory.DeepDungeons, row.SortKey));
            }
            else if (contentType == 30 || name.Contains("Criterion", StringComparison.OrdinalIgnoreCase) || name.Contains("Variant", StringComparison.OrdinalIgnoreCase))
            {
                CachedDuties[PfDutyCategory.VCDungeonFinder].Add(new PfDutyEntry(row.RowId, name, PfDutyCategory.VCDungeonFinder, row.SortKey));
            }
            else if (contentType == 9)
            {
                CachedDuties[PfDutyCategory.TreasureHunt].Add(new PfDutyEntry(row.RowId, name, PfDutyCategory.TreasureHunt, row.SortKey));
            }
        }

        AddSpecialtyDuties(PfDutyCategory.TreasureHunt, TreasureHuntDuties);
        AddSpecialtyDuties(PfDutyCategory.TheHunt, HuntDuties);
        AddSpecialtyDuties(PfDutyCategory.FATEs, FateDuties);
        AddSpecialtyDuties(PfDutyCategory.GatheringForays, GatheringForayDuties);
        AddSpecialtyDuties(PfDutyCategory.FieldOperations, FieldOperationDuties);

        foreach (var kvp in CachedDuties)
        {
            if (kvp.Key == PfDutyCategory.HighEndDuty || kvp.Key == PfDutyCategory.Raids)
            {
                kvp.Value.Sort((a, b) => b.SortKey.CompareTo(a.SortKey));
            }
            else
            {
                kvp.Value.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));
            }
        }
    }

    private static void AddSpecialtyDuties(PfDutyCategory category, string[] names)
    {
        for (var i = 0; i < names.Length; i++)
        {
            CachedDuties[category].Add(new PfDutyEntry(0, names[i], category));
        }
    }

    public static readonly PfJobItem[] Tanks =
    {
        new(19, "Paladin", "PLD", GameData.JobIconId(19)),
        new(21, "Warrior", "WAR", GameData.JobIconId(21)),
        new(32, "Dark Knight", "DRK", GameData.JobIconId(32)),
        new(37, "Gunbreaker", "GNB", GameData.JobIconId(37)),
    };

    public static readonly PfJobItem[] TankClasses =
    {
        new(1, "Gladiator", "GLA", GameData.JobIconId(1), true),
        new(3, "Marauder", "MRD", GameData.JobIconId(3), true),
    };

    public static readonly PfJobItem[] PureHealers =
    {
        new(24, "White Mage", "WHM", GameData.JobIconId(24)),
        new(33, "Astrologian", "AST", GameData.JobIconId(33)),
    };

    public static readonly PfJobItem[] BarrierHealers =
    {
        new(28, "Scholar", "SCH", GameData.JobIconId(28)),
        new(40, "Sage", "SGE", GameData.JobIconId(40)),
    };

    public static readonly PfJobItem[] HealerClasses =
    {
        new(6, "Conjurer", "CNJ", GameData.JobIconId(6), true),
    };

    public static readonly PfJobItem[] MeleeDps =
    {
        new(20, "Monk", "MNK", GameData.JobIconId(20)),
        new(22, "Dragoon", "DRG", GameData.JobIconId(22)),
        new(30, "Ninja", "NIN", GameData.JobIconId(30)),
        new(34, "Samurai", "SAM", GameData.JobIconId(34)),
        new(39, "Reaper", "RPR", GameData.JobIconId(39)),
        new(41, "Viper", "VPR", GameData.JobIconId(41)),
    };

    public static readonly PfJobItem[] MeleeClasses =
    {
        new(2, "Pugilist", "PGL", GameData.JobIconId(2), true),
        new(4, "Lancer", "LNC", GameData.JobIconId(4), true),
        new(29, "Rogue", "ROG", GameData.JobIconId(29), true),
    };

    public static readonly PfJobItem[] PhysRangedDps =
    {
        new(23, "Bard", "BRD", GameData.JobIconId(23)),
        new(31, "Machinist", "MCH", GameData.JobIconId(31)),
        new(38, "Dancer", "DNC", GameData.JobIconId(38)),
    };

    public static readonly PfJobItem[] PhysRangedClasses =
    {
        new(5, "Archer", "ARC", GameData.JobIconId(5), true),
    };

    public static readonly PfJobItem[] CasterDps =
    {
        new(25, "Black Mage", "BLM", GameData.JobIconId(25)),
        new(27, "Summoner", "SMN", GameData.JobIconId(27)),
        new(35, "Red Mage", "RDM", GameData.JobIconId(35)),
        new(42, "Pictomancer", "PCT", GameData.JobIconId(42)),
        new(36, "Blue Mage", "BLU", GameData.JobIconId(36)),
    };

    public static readonly PfJobItem[] CasterClasses =
    {
        new(7, "Thaumaturge", "THM", GameData.JobIconId(7), true),
        new(26, "Arcanist", "ACN", GameData.JobIconId(26), true),
    };

    public static readonly uint[] TankJobIds = { 19, 21, 32, 37, 1, 3 };
    public static readonly uint[] PureHealerJobIds = { 24, 33, 6 };
    public static readonly uint[] BarrierHealerJobIds = { 28, 40 };
    public static readonly uint[] AllHealerJobIds = { 24, 33, 28, 40, 6 };
    public static readonly uint[] MeleeJobIds = { 20, 22, 30, 34, 39, 41, 2, 4, 29 };
    public static readonly uint[] PhysRangedJobIds = { 23, 31, 38, 5 };
    public static readonly uint[] CasterJobIds = { 25, 27, 35, 42, 36, 7, 26 };
    public static readonly uint[] AllDpsJobIds = { 20, 22, 30, 34, 39, 41, 2, 4, 29, 23, 31, 38, 5, 25, 27, 35, 42, 36, 7, 26 };
    public static readonly uint[] AllClassIds = { 1, 2, 3, 4, 5, 6, 7, 26, 29 };

    public static readonly uint[] AllCombatJobIds =
    {
        19, 21, 32, 37, 1, 3,
        24, 33, 28, 40, 6,
        20, 22, 30, 34, 39, 41, 2, 4, 29,
        23, 31, 38, 5,
        25, 27, 35, 42, 36, 7, 26
    };

    public static readonly uint[][] HighEndComposition =
    {
        TankJobIds,
        TankJobIds,
        PureHealerJobIds,
        BarrierHealerJobIds,
        MeleeJobIds,
        MeleeJobIds,
        PhysRangedJobIds,
        CasterJobIds,
    };

    public static bool IsTank(uint id)
    {
        return id is 19 or 21 or 32 or 37 or 1 or 3;
    }

    public static bool IsHealer(uint id)
    {
        return id is 24 or 33 or 28 or 40 or 6;
    }

    public static bool IsPureHealer(uint id)
    {
        return id is 24 or 33 or 6;
    }

    public static bool IsBarrierHealer(uint id)
    {
        return id is 28 or 40;
    }

    public static bool IsMelee(uint id)
    {
        return id is 20 or 22 or 30 or 34 or 39 or 41 or 2 or 4 or 29;
    }

    public static bool IsPhysRanged(uint id)
    {
        return id is 23 or 31 or 38 or 5;
    }

    public static bool IsCaster(uint id)
    {
        return id is 25 or 27 or 35 or 42 or 36 or 7 or 26;
    }

    public static PfSlotRole DetermineRole(HashSet<uint> allowedJobs)
    {
        if (allowedJobs.Count == 0 || allowedJobs.Count >= AllCombatJobIds.Length - 5)
        {
            return PfSlotRole.Any;
        }
        var hasTank = false;
        var hasHealer = false;
        var hasDps = false;
        foreach (var j in allowedJobs)
        {
            if (IsTank(j))
            {
                hasTank = true;
            }
            else if (IsHealer(j))
            {
                hasHealer = true;
            }
            else
            {
                hasDps = true;
            }
        }
        if (hasTank && !hasHealer && !hasDps)
        {
            return PfSlotRole.Tank;
        }
        if (hasHealer && !hasTank && !hasDps)
        {
            return PfSlotRole.Healer;
        }
        if (hasDps && !hasTank && !hasHealer)
        {
            return PfSlotRole.Dps;
        }
        return PfSlotRole.Any;
    }

    public static void ApplyComposition(PfSlotConfig[] slots, uint[][] composition, uint leaderJobId)
    {
        var leaderEntryIndex = composition.Length - 1;
        for (var entryIndex = 0; entryIndex < composition.Length; entryIndex++)
        {
            if (Array.IndexOf(composition[entryIndex], leaderJobId) >= 0)
            {
                leaderEntryIndex = entryIndex;
                break;
            }
        }

        var slotIndex = 1;
        for (var entryIndex = 0; entryIndex < composition.Length && slotIndex < slots.Length; entryIndex++)
        {
            if (entryIndex == leaderEntryIndex)
            {
                continue;
            }

            slots[slotIndex].SetJobs(composition[entryIndex]);
            slotIndex++;
        }
    }

    public static ulong JobMaskFor(HashSet<uint> jobIds)
    {
        var mask = 0UL;
        foreach (var jobId in jobIds)
        {
            if (RecruitCatalog.JobIdToJobFlag(jobId) is { } flag)
            {
                mask |= (ulong)flag;
            }
        }

        return mask;
    }

    public static PfSlotRole RoleForJob(uint jobId)
    {
        if (IsTank(jobId))
        {
            return PfSlotRole.Tank;
        }

        if (IsHealer(jobId))
        {
            return PfSlotRole.Healer;
        }

        if (IsMelee(jobId) || IsPhysRanged(jobId) || IsCaster(jobId))
        {
            return PfSlotRole.Dps;
        }

        return PfSlotRole.Any;
    }

    public static Vector4 RoleColor(PfSlotRole role)
    {
        return role switch
        {
            PfSlotRole.Tank => new Vector4(0.22f, 0.45f, 0.85f, 0.95f),
            PfSlotRole.Healer => new Vector4(0.22f, 0.72f, 0.42f, 0.95f),
            PfSlotRole.Dps => new Vector4(0.85f, 0.28f, 0.28f, 0.95f),
            _ => new Vector4(0.35f, 0.35f, 0.38f, 0.85f),
        };
    }

    public static Vector4 GetSlotColor(PfSlotConfig slot, bool isLeader, uint leaderJobId)
    {
        var role = isLeader ? RoleForJob(leaderJobId) : slot.Role;
        return RoleColor(role);
    }


    public static void DrawSlotGlyph(ImDrawListPtr drawList, Vector2 min, Vector2 max, PfSlotConfig slot, float scale)
    {
        var center = (min + max) * 0.5f;
        if (slot.AllowedJobs.Count == 1)
        {
            using var enumerator = slot.AllowedJobs.GetEnumerator();
            if (enumerator.MoveNext())
            {
                var singleJobId = enumerator.Current;
                var iconId = GameData.JobIconId(singleJobId);
                var inset = 3f * scale;
                if (GameIconTile.Draw(drawList, Plugin.TextureProvider, iconId, min + new Vector2(inset), max - new Vector2(inset), 4f * scale, scale))
                {
                    return;
                }
            }
        }

        var icon = slot.Role switch
        {
            PfSlotRole.Tank => FontAwesomeIcon.ShieldAlt,
            PfSlotRole.Healer => FontAwesomeIcon.Heartbeat,
            PfSlotRole.Dps => FontAwesomeIcon.Crosshairs,
            _ => FontAwesomeIcon.User,
        };
        AppSkin.Icon(drawList, center, IconGlyph.Of(icon), new Vector4(1f, 1f, 1f, 0.95f), 0.70f);
    }
}
