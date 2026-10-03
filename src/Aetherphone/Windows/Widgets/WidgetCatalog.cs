using Aetherphone.Apps.Calendar.Widgets;
using Aetherphone.Apps.Clock.Widgets;
using Aetherphone.Apps.Skywatcher;
using Aetherphone.Apps.Timers.Widgets;
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
            new SkywatcherWidget(phone.Weather),
            new ClockWidget(phone.Configuration),
            new AlarmWidget(phone.Configuration, phone.AlarmRinger),
            new TimerWidget(phone.Configuration, phone.AlarmRinger),
            new UpNextWidget(calendarFeed),
            new MonthWidget(),
            new PhotosWidget(services.Photos),
            new ResetsWidget(),
            new VenturesWidget(),
            new ActivityRingsWidget(phone.Activity, phone.Configuration),
            new CoinWidget(phone.Coins, phone.AethernetSession),
        };

        return new WidgetRegistry(widgets, services.Apps);
    }
}
