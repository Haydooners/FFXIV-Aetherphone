using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Casino;

internal static class CasinoRecentGames
{
    private const string KindPrefix = "casino.";

    public static string ClientGameId(string wireKind) =>
        wireKind.StartsWith(KindPrefix, StringComparison.Ordinal) ? wireKind[KindPrefix.Length..] : wireKind;

    public static int Collect(CasinoRoundHistoryDto[] rounds, ReadOnlySpan<string> playable, Span<int> picked)
    {
        var count = 0;
        for (var roundIndex = 0; roundIndex < rounds.Length && count < picked.Length; roundIndex++)
        {
            var gameIndex = IndexOf(playable, rounds[roundIndex].GameKind);
            if (gameIndex < 0 || Contains(picked[..count], gameIndex))
            {
                continue;
            }

            picked[count] = gameIndex;
            count++;
        }

        return count;
    }

    private static int IndexOf(ReadOnlySpan<string> playable, string wireKind)
    {
        if (!wireKind.StartsWith(KindPrefix, StringComparison.Ordinal))
        {
            return -1;
        }

        var gameId = wireKind.AsSpan(KindPrefix.Length);
        for (var index = 0; index < playable.Length; index++)
        {
            if (gameId.Equals(playable[index], StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool Contains(ReadOnlySpan<int> picked, int gameIndex)
    {
        for (var index = 0; index < picked.Length; index++)
        {
            if (picked[index] == gameIndex)
            {
                return true;
            }
        }

        return false;
    }
}
