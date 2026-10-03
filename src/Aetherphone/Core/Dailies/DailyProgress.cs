using Aetherphone.Core.Game;

namespace Aetherphone.Core.Dailies;

internal static class DailyProgress
{
    public static DailyAutoStatus ReadStatus(GameData gameData, in DailyItem item)
    {
        return item.Tracking switch
        {
            DailyTracking.Manual => DailyAutoStatus.Unavailable,
            DailyTracking.DutyRoulettes => DailiesReader.ReadDutyRoulettes(gameData.DailyBonusRouletteRowIds()),
            DailyTracking.HuntBills => DailiesReader.ReadHuntBills(gameData.WeeklyHuntBillIndices(),
                gameData.HuntOrderTypeSheet(), gameData.HuntOrderSheet()),
            _ => DailiesReader.Read(item.Tracking, item.Goal),
        };
    }

    public static bool IsTracked(in DailyItem item) => item.Tracking != DailyTracking.Levequests;

    public static bool IsOutstanding(in DailyItem item, in DailyAutoStatus status, DailyCheckStore checkStore,
        DateTime utcNow)
    {
        if (!IsTracked(item))
        {
            return false;
        }

        if (item.Tracking != DailyTracking.Manual && status.Available)
        {
            return !status.Complete;
        }

        return !checkStore.IsChecked(item, utcNow);
    }
}
