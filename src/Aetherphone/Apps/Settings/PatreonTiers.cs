using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Settings;

internal readonly struct PatreonTier
{
    public readonly LocString Name;
    public readonly string Price;
    public readonly Vector4 Accent;
    public readonly bool Popular;
    public readonly LocString[] Perks;

    public PatreonTier(LocString name, string price, Vector4 accent, bool popular, LocString[] perks)
    {
        Name = name;
        Price = price;
        Accent = accent;
        Popular = popular;
        Perks = perks;
    }
}

internal static class PatreonTiers
{
    public static readonly PatreonTier[] All = Array.Empty<PatreonTier>();
}
