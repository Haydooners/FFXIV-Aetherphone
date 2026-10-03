using Aetherphone.Apps.Calendar;
using Aetherphone.Apps.Dailies.Widgets;
using Aetherphone.Apps.Fishing.Widgets;
using Aetherphone.Apps.Housing.Widgets;
using Aetherphone.Apps.Hunts.Widgets;
using Aetherphone.Apps.Jobs.Widgets;
using Aetherphone.Apps.Maps.Widgets;
using Aetherphone.Apps.Skywatcher;
using Aetherphone.Apps.Wallet.Widgets;
using Aetherphone.Core.Home;

namespace Aetherphone.Windows.Widgets;

internal static class WidgetCatalog
{
    public static WidgetRegistry Build(WidgetServices services)
    {
        var phone = services.Phone;
        var widgets = new List<IHomeWidget>
        {
            new SkywatcherWidget(phone.Weather),
            new ClockWidget(),
            new CalendarWidget(phone.Configuration, services.CalendarEvents),
            new PhotosWidget(services.Photos),
            new ResetsWidget(),
            new ActivityRingsWidget(phone.Activity, phone.Configuration),
            new CoinWidget(phone.Coins, phone.AethernetSession),
            new DailiesWidget(phone.Configuration, phone.GameData),
            new OceanFishingWidget(),
            new CurrenciesWidget(phone.GameData, phone.CharacterWatch, phone.Activity),
            new JobWidget(phone.GameData),
            new HuntsLiveWidget(phone.Hunts, phone.HuntMobCatalog, phone.Configuration),
            new HousingLotteryWidget(phone.Housing),
            new TeleportWidget(phone.Maps, phone.Configuration),
        };

        return new WidgetRegistry(widgets, services.Apps);
    }
}
