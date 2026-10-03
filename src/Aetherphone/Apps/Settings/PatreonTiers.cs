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
    public readonly string Price;
    public readonly Vector4 Accent;
    public readonly bool Popular;
    public readonly string FrameId;
    public readonly PatreonPerk[] Perks;

    public PatreonTier(string name, string price, Vector4 accent, bool popular, string frameId, PatreonPerk[] perks)
    {
        Name = name;
        Price = price;
        Accent = accent;
        Popular = popular;
        FrameId = frameId;
        Perks = perks;
    }
}

internal static class PatreonTiers
{
    public const string BadgeId = "patreon";

    private static readonly Vector4 ShardTeal = new(0.25f, 0.78f, 0.80f, 1f);
    private static readonly Vector4 CrystalViolet = new(0.62f, 0.44f, 0.98f, 1f);
    private static readonly Vector4 ClusterGold = new(0.98f, 0.72f, 0.25f, 1f);

    public static readonly PatreonTier[] All =
    {
        new("Shard", "$3", ShardTeal, false, "", new[]
        {
            new PatreonPerk(L.Settings.SupportPerkDiscordRole),
            new PatreonPerk(L.Settings.SupportPerkColoredName),
            new PatreonPerk(L.Settings.SupportPerkFrame),
        }),
        new("Crystal", "$10", CrystalViolet, true, "", new[]
        {
            new PatreonPerk(L.Settings.SupportPerkDiscordRole),
            new PatreonPerk(L.Settings.SupportPerkDirectAccess),
            new PatreonPerk(L.Settings.SupportPerkEarlyAccess),
            new PatreonPerk(L.Settings.SupportPerkColoredName),
            new PatreonPerk(L.Settings.SupportPerkFrame),
        }),
        new("Cluster", "$20", ClusterGold, false, "", new[]
        {
            new PatreonPerk(L.Settings.SupportPerkDiscordRole),
            new PatreonPerk(L.Settings.SupportPerkDirectAccess),
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
