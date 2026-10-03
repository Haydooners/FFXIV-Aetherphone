using Aetherphone.Apps.Calendar;
using Aetherphone.Apps.Skywatcher;
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
        };

        return new WidgetRegistry(widgets, services.Apps);
    }
}
