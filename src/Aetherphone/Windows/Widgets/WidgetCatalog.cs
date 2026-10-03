using Aetherphone.Apps.Activity.Widgets;
using Aetherphone.Apps.Calendar.Widgets;
using Aetherphone.Apps.Clock.Widgets;
using Aetherphone.Apps.Coin.Widgets;
using Aetherphone.Apps.Dailies.Widgets;
using Aetherphone.Apps.Fishing.Widgets;
using Aetherphone.Apps.Health.Widgets;
using Aetherphone.Apps.Housing.Widgets;
using Aetherphone.Apps.Hunts.Widgets;
using Aetherphone.Apps.Jobs.Widgets;
using Aetherphone.Apps.Maps.Widgets;
using Aetherphone.Apps.Photos.Widgets;
using Aetherphone.Apps.Skywatcher.Widgets;
using Aetherphone.Apps.Timers.Widgets;
using Aetherphone.Apps.Wallet.Widgets;
using Aetherphone.Core.Home;

namespace Aetherphone.Windows.Widgets;

internal static class WidgetCatalog
{
    public static WidgetRegistry Build(WidgetServices services)
    {
        var phone = services.Phone;
        var calendarFeed = new CalendarWidgetFeed(phone.Configuration, services.CalendarEvents);
        var widgets = new List<IHomeWidget>
        {
            new WeatherWidget(phone.Weather),
            new ClockWidget(phone.Configuration),
            new AlarmWidget(phone.Configuration, phone.AlarmRinger),
            new TimerWidget(phone.Configuration, phone.AlarmRinger),
            new UpNextWidget(calendarFeed),
            new MonthWidget(),
            new FeaturedPhotoWidget(services.Photos),
            new ResetsWidget(),
            new VenturesWidget(),
            new ActivityWidget(phone.Activity, phone.Configuration),
            new CoinBalanceWidget(phone.Coins, phone.AethernetSession),
            new WeatherWatchWidget(phone.Weather),
            new HydrationWidget(phone.Health),
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
