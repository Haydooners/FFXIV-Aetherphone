using Aetherphone.Core.Radio;

namespace Aetherphone.Core.Coins;

internal enum RadioListenStep : byte
{
    None,
    Start,
    End,
    Switch,
}

internal static class CoinRadioListen
{
    public static RadioListenStep Decide(string listeningStationId, RadioPlaybackState state, string stationId,
        bool signedIn)
    {
        var hasStation = signedIn && stationId.Length > 0;
        var playing = hasStation && state == RadioPlaybackState.Playing;
        if (listeningStationId.Length == 0)
        {
            return playing ? RadioListenStep.Start : RadioListenStep.None;
        }

        var sameStation = hasStation && string.Equals(listeningStationId, stationId, StringComparison.Ordinal);
        if (sameStation && (playing || IsTransient(state)))
        {
            return RadioListenStep.None;
        }

        return playing ? RadioListenStep.Switch : RadioListenStep.End;
    }

    private static bool IsTransient(RadioPlaybackState state) =>
        state is RadioPlaybackState.Buffering or RadioPlaybackState.Reconnecting;
}
