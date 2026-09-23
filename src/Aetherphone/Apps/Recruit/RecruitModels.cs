using System;
using System.Collections.Generic;

namespace Aetherphone.Apps.Recruit;

internal enum ListingKind
{
    StaticLfm,
    PlayerLfg,
    SingleNightFill,
    PartyFinder,
}

internal enum StaticCategory 
{
    Casual,
    MidCore,
    SemiHardcore,
    Hardcore,
}

internal enum ContentCategory
{
    All, 
    Savage,
    Ultimate,
    Criterion,
    ExtremeFarm,
    DeepDungeon,
    Other,
}

internal enum RaidRole
{
    Tank,
    PureHealer,
    BarrierHealer,
    Melee,
    PhysRanged,
    Caster,
}

internal enum PfCategory
{
    All = 0,
    HighEnd = 1,
    Raids = 2,
    Trials = 3,
    Dungeons = 4,
    DeepDungeon = 5,
    FieldOps = 6,
    HuntAndMaps = 7,
    Other = 8,
}

[Flags]
internal enum RaidDays : byte
{
    None = 0,
    Monday = 1 << 0,
    Tuesday = 1 << 1,
    Wednesday = 1 << 2,
    Thursday = 1 << 3,
    Friday = 1 << 4,
    Saturday = 1 << 5,
    Sunday = 1 << 6,
}

internal enum RaidTimezone
{
    EST,
    CST,
    PST,
    GMT,
    JST,
}

internal sealed record DutyInfo(string Name, ContentCategory Category);

internal static class RecruitCatalog
{
    public static readonly IReadOnlyList<DutyInfo> Duties = new List<DutyInfo>
    {
        new("Dancing Mad (UMAD)", ContentCategory.Ultimate),
        new("Futures Rewritten (FRU)", ContentCategory.Ultimate),
        new("The Omega Protocol (TOP)", ContentCategory.Ultimate),
        new("Dragonsong's Reprise (DSR)", ContentCategory.Ultimate),
        new("The Epic of Alexader (TEA)", ContentCategory.Ultimate),
        new("The Weapon's Refrain (UWU)", ContentCategory.Ultimate),
        new("The Unending Coil of Bahamut (UCOB)", ContentCategory.Ultimate),

        new ("AAC Heavyweight M9S", ContentCategory.Savage),
        new ("AAC Heavyweight M10S", ContentCategory.Savage),
        new ("AAC Heavyweight M11S", ContentCategory.Savage),
        new ("AAC Heavyweight M12S", ContentCategory.Savage),

        new("Another Aloalo Island (Criterion)", ContentCategory.Criterion),
        new("Another Mount Rokkon (Criterion)", ContentCategory.Criterion),
        new("Another Sil'dihn Subterrane (Criterion)", ContentCategory.Criterion),

        new("Everkeep (Extreme)", ContentCategory.ExtremeFarm),
        new("Worqor Zormor (Extreme)", ContentCategory.ExtremeFarm),
        new("Sphene's Burden (Extreme)", ContentCategory.ExtremeFarm),
        new("Recollection (Extreme)", ContentCategory.ExtremeFarm),
        new("Necron's Embrace (Extreme)", ContentCategory.ExtremeFarm),
        new("The Windward Wilds (Extreme)", ContentCategory.ExtremeFarm),
        new("Hell on Rails (Extreme)", ContentCategory.ExtremeFarm),
        new("The Unmaking (Extreme)", ContentCategory.ExtremeFarm),

        new("Palace of the Dead", ContentCategory.DeepDungeon),
        new("Heaven-on-High", ContentCategory.DeepDungeon),
        new("Eureka Orthos", ContentCategory.DeepDungeon),
        new("Pilgrim's Traverse", ContentCategory.DeepDungeon),

        new("Old Savage", ContentCategory.Other),
        new("Old Extreme", ContentCategory.Other),
        new("Field Ops", ContentCategory.Other),
    };

    public static List<DutyInfo> DutiesFor(ContentCategory category)
    {
        var result = new List<DutyInfo>();
        for (var index = 0; index < Duties.Count; index++)
        {
            if (Duties[index].Category == category)
            {
                result.Add(Duties[index]);
            }
        }
        return result;
    }

    public static string FormatDays(RaidDays days)
    {
        if (days == RaidDays.None)
        {
            return "No Days Set";
        }

        var selected = new List<string>();
        if ((days & RaidDays.Monday) != 0) { selected.Add("Mon"); }
        if ((days & RaidDays.Tuesday) != 0) { selected.Add("Tue"); }
        if ((days & RaidDays.Wednesday) != 0) { selected.Add("Wed"); }
        if ((days & RaidDays.Thursday) != 0) { selected.Add("Thu"); }
        if ((days & RaidDays.Friday) != 0) { selected.Add("Fri"); }
        if ((days & RaidDays.Saturday) != 0) { selected.Add("Sat"); }
        if ((days & RaidDays.Sunday) != 0) { selected.Add("Sun"); }

        return string.Join(" / ", selected);
    }

    public static string FormatTimeOfDay(int minuteOfDay)
    {
        var hour = (minuteOfDay / 60) % 24;
        var minute = minuteOfDay % 60;
        var period = hour >= 12 ? "PM" : "AM";
        var displayHour = hour % 12;
        if (displayHour == 0)
        {
            displayHour = 12;
        }
        return $"{displayHour}:{minute:D2} {period}";
    }

    public static string FormatTimeRange(int startMinuteOfDay, int endMinuteOfDay, RaidTimezone timezone)
    {
        var startStr = FormatTimeOfDay(startMinuteOfDay);
        var endStr = FormatTimeOfDay(endMinuteOfDay);
        return $"{startStr} - {endStr} {timezone}";
    }

    public static string KindName(ListingKind kind)
    {
        return kind switch
        {
            ListingKind.StaticLfm => "Static LFM",
            ListingKind.PlayerLfg => "Player LFG",
            ListingKind.SingleNightFill => "Fill",
            ListingKind.PartyFinder => "Party Finder",
            _ => kind.ToString(),
        };
    }

    public static string CategoryName(StaticCategory category)
    {
        return category switch
        {
            StaticCategory.Hardcore => "Hardcore (HC)",
            StaticCategory.SemiHardcore => "Semi-Hardcore (sHC)",
            StaticCategory.MidCore => "Midcore (MC)",
            StaticCategory.Casual => "Casual (SC)",
            _ => category.ToString(),
        };
    }

    public static string RoleName(RaidRole role)
    {
        return role switch
        {
            RaidRole.Tank => "Tank",
            RaidRole.PureHealer => "Pure Healer (WHM/AST)",
            RaidRole.BarrierHealer => "Barrier Healer (SCH/SGE)",
            RaidRole.Melee => "Melee DPS",
            RaidRole.PhysRanged => "Phys Ranged DPS",
            RaidRole.Caster => "Caster DPS",
            _ => role.ToString(),
        };
    }
}

internal sealed record PartyFinderListing(
    ulong ListingId,
    string DutyName,
    string CategoryName,
    string Comment,
    string AuthorName,
    string WorldName,
    byte SlotsFilled,
    byte TotalSlots,
    ushort ItemLevel,
    DateTime ReadAt
);

internal sealed record RecruitListing(
    string Id,
    string Title,
    string Description,
    ListingKind Kind,
    DutyInfo Duty,
    StaticCategory Playstyle,
    RaidDays SelectedDays,
    int StartMinuteOfDay,
    int EndMinuteOfDay,
    RaidTimezone Timezone,
    List<RaidRole> RolesNeeded,
    List<string> Tags,
    string AuthorName,
    string WorldDc,
    DateTime PostedAt)
{
    public string FormattedSchedule =>
        $"{RecruitCatalog.FormatDays(SelectedDays)} ({RecruitCatalog.FormatTimeRange(StartMinuteOfDay, 
        EndMinuteOfDay, Timezone)})";
    
    public static List<RecruitListing> CreateSampleListings()
    {
        return new List<RecruitListing>
        {
            new(
                Id: "1",
                Title: "FRU P3 Transition Prog - Need Barrier & Caster",
                Description: "Looking for a barrier healer and caster for some FRU P3 Prog onward!",
                Kind: ListingKind.StaticLfm,
                Duty: RecruitCatalog.Duties[0],
                Playstyle: StaticCategory.MidCore,
                SelectedDays: RaidDays.Tuesday | RaidDays.Thursday | RaidDays.Saturday,
                StartMinuteOfDay: 20 * 60,       
                EndMinuteOfDay: 23 * 60,         
                Timezone: RaidTimezone.EST,
                RolesNeeded: new List<RaidRole> { RaidRole.BarrierHealer, RaidRole.Caster },
                Tags: new List<string> { "Midcore", "Voice Req" },
                AuthorName: "Haydoon Dragoon",
                WorldDc: "Cactuar / Aether",
                PostedAt: DateTime.Now.AddHours(-2)
            ),
            new(
                Id: "2",
                Title: "M4S Fill NEEDED TONIGHT at 9 PM EST",
                Description: "Need 1 tank for a quick weekly reclear. Discord voice optional.",
                Kind: ListingKind.SingleNightFill,
                Duty: RecruitCatalog.Duties[9],
                Playstyle: StaticCategory.Hardcore,
                SelectedDays: RaidDays.Saturday,
                StartMinuteOfDay: 21 * 60,       
                EndMinuteOfDay: 24 * 60 % 1440,  
                Timezone: RaidTimezone.EST,
                RolesNeeded: new List<RaidRole> { RaidRole.Tank },
                Tags: new List<string> { "Reclear", "Single Night" },
                AuthorName: "Haydoon Dragoon",
                WorldDc: "Excalibur / Primal",
                PostedAt: DateTime.Now.AddMinutes(-35)
            ),
            new(
                Id: "3",
                Title: "Melee LFG for Hardcore Savage/Ultimate",
                Description: "VPR Main looking for a Savage / Ultimate static to speed prog UMAD, and week 1 Evercold Savage",
                Kind: ListingKind.PlayerLfg,
                Duty: RecruitCatalog.Duties[1],
                Playstyle: StaticCategory.Hardcore,
                SelectedDays: RaidDays.Monday | RaidDays.Wednesday | RaidDays.Friday,
                StartMinuteOfDay: 19 * 60,       
                EndMinuteOfDay: 22 * 60,         
                Timezone: RaidTimezone.PST,
                RolesNeeded: new List<RaidRole> { RaidRole.Melee, RaidRole.Caster },
                Tags: new List<string> { "Hardcore"},
                AuthorName: "Haydoon Dragoon",
                WorldDc: "Crystal / Balmung",
                PostedAt: DateTime.Now.AddHours(-5)
            ),
            new(
                Id: "4",
                Title: "Mwrsdhhyr",
                Description: "EHawerghaewrg",
                Kind: ListingKind.SingleNightFill,
                Duty: RecruitCatalog.Duties[1],
                Playstyle: StaticCategory.Hardcore,
                SelectedDays: RaidDays.Monday | RaidDays.Wednesday | RaidDays.Friday,
                StartMinuteOfDay: 19 * 60,       
                EndMinuteOfDay: 22 * 60,         
                Timezone: RaidTimezone.PST,
                RolesNeeded: new List<RaidRole> { RaidRole.Melee, RaidRole.Caster },
                Tags: new List<string> { "Hardcore"},
                AuthorName: "Haydoon Dragoon",
                WorldDc: "Crystal / Balmung",
                PostedAt: DateTime.Now.AddHours(-5)
            ),
        };
    }
}