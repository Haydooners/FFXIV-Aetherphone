using Aetherphone.Core;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Settings;

internal readonly struct PatreonPerk
{
    public readonly LocString Label;
    public readonly bool ComingSoon;

    public PatreonPerk(LocString label, bool comingSoon = false)
    {
        Label = label;
        ComingSoon = comingSoon;
    }
}

internal readonly struct PatreonTier
{
    public readonly string Name;
    public readonly Vector4 Accent;
    public readonly bool Popular;
    public readonly string FrameId;
    public readonly string[] BadgeIds;
    public readonly string JoinUrl;
    public readonly PatreonPerk[] Perks;

    public PatreonTier(string name, Vector4 accent, bool popular, string frameId, string[] badgeIds,
        string joinUrl, PatreonPerk[] perks)
    {
        Name = name;
        Accent = accent;
        Popular = popular;
        FrameId = frameId;
        BadgeIds = badgeIds;
        JoinUrl = joinUrl;
        Perks = perks;
    }
}

internal static class PatreonTiers
{
    public const string MemberBadgeId = "legacy-patreon";
    public const string GildedBadgeId = "global-patreon-gilded";
    private const string MembershipUrl = AepConstants.PatreonUrl + "/membership";
    private static readonly string[] MemberBadges = { MemberBadgeId };
    private static readonly string[] GildedBadges = { GildedBadgeId };

    private static readonly Vector4 ShardTeal = new(0.25f, 0.78f, 0.80f, 1f);
    private static readonly Vector4 CrystalViolet = new(0.62f, 0.44f, 0.98f, 1f);
    private static readonly Vector4 ClusterGold = new(0.98f, 0.72f, 0.25f, 1f);

    public static readonly PatreonTier[] All =
    {
        new("Shard", ShardTeal, false, "", MemberBadges, MembershipUrl, new[]
        {
            new PatreonPerk(L.Settings.SupportPerkDiscordRole),
            new PatreonPerk(L.Settings.SupportPerkColoredName),
            new PatreonPerk(L.Settings.SupportPerkFrame),
        }),
        new("Crystal", CrystalViolet, true, "", MemberBadges, MembershipUrl, new[]
        {
            new PatreonPerk(L.Settings.SupportPerkDiscordRole),
            new PatreonPerk(L.Settings.SupportPerkEarlyAccess),
            new PatreonPerk(L.Settings.SupportPerkColoredName),
            new PatreonPerk(L.Settings.SupportPerkFrame),
        }),
        new("Cluster", ClusterGold, false, "", GildedBadges, MembershipUrl, new[]
        {
            new PatreonPerk(L.Settings.SupportPerkDiscordRole),
            new PatreonPerk(L.Settings.SupportPerkEarlyAccess),
            new PatreonPerk(L.Settings.SupportPerkColoredName),
            new PatreonPerk(L.Settings.SupportPerkFrame),
            new PatreonPerk(L.Settings.SupportPerkExclusiveLooks, true),
            new PatreonPerk(L.Settings.SupportPerkIdleAnimation, true),
            new PatreonPerk(L.Settings.SupportPerkMerch, true),
            new PatreonPerk(L.Settings.SupportPerkNotificationVfx, true),
        }),
    };

    public static readonly string[] Names = { All[0].Name, All[1].Name, All[2].Name };
}
