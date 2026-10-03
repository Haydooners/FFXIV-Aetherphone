using Aetherphone.Apps.Activity.Widgets;
using Aetherphone.Apps.Calendar;
using Aetherphone.Apps.Coin.Widgets;
using Aetherphone.Apps.Health.Widgets;
using Aetherphone.Apps.Photos.Widgets;
using Aetherphone.Apps.Skywatcher.Widgets;
using Aetherphone.Core.Home;

namespace Aetherphone.Windows.Widgets;

internal static class WidgetCatalog
{
    public static WidgetRegistry Build(WidgetServices services)
    {
        var phone = services.Phone;
        var widgets = new List<IHomeWidget>
        {
            new WeatherWidget(phone.Weather),
            new ClockWidget(),
            new CalendarWidget(phone.Configuration, services.CalendarEvents),
            new FeaturedPhotoWidget(services.Photos),
            new ResetsWidget(),
            new ActivityWidget(phone.Activity, phone.Configuration),
            new CoinBalanceWidget(phone.Coins, phone.AethernetSession),
            new WeatherWatchWidget(phone.Weather),
            new HydrationWidget(phone.Health),
        };

        return new WidgetRegistry(widgets, services.Apps);
    }
}
