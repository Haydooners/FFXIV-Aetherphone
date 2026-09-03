using Aetherphone.Core.Game;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VelvetRace
{
    public static readonly int[] All = { 1, 2, 4, 5, 6, 7, 8 };

    private static readonly int AllowedMask = MaskOf(All);

    public static int Bit(int raceId) => raceId >= 1 && raceId <= 8 ? 1 << (raceId - 1) : 0;

    public static int Sanitize(int mask) => mask & AllowedMask;

    public static bool Has(int mask, int raceId) => (mask & Bit(raceId)) != 0;

    public static int Toggle(int mask, int raceId) =>
        Has(mask, raceId) ? mask & ~Bit(raceId) : mask | Bit(raceId);

    public static int Count(int mask)
    {
        var count = 0;
        for (var index = 0; index < All.Length; index++)
        {
            if (Has(mask, All[index]))
            {
                count++;
            }
        }

        return count;
    }

    public static string Label(GameData gameData, int raceId) => gameData.RaceName((uint)raceId, false);

    private static int MaskOf(int[] raceIds)
    {
        var mask = 0;
        for (var index = 0; index < raceIds.Length; index++)
        {
            mask |= Bit(raceIds[index]);
        }

        return mask;
    }
}
