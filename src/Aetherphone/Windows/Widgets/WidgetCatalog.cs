using Aetherphone.Apps.Calendar;
using Aetherphone.Apps.Casino.Widgets;
using Aetherphone.Apps.Games.Widgets;
using Aetherphone.Apps.Market.Widgets;
using Aetherphone.Apps.Muster.Widgets;
using Aetherphone.Apps.News.Widgets;
using Aetherphone.Apps.Notes.Widgets;
using Aetherphone.Apps.Settings.Widgets;
using Aetherphone.Apps.Shortcuts.Widgets;
using Aetherphone.Apps.Venues.Widgets;
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

        AddUtility(widgets, services);

        return new WidgetRegistry(widgets, services.Apps);
    }

    private static void AddUtility(List<IHomeWidget> widgets, WidgetServices services)
    {
        var phone = services.Phone;
        widgets.Add(new NoteWidget(phone.Configuration));
        widgets.Add(new RemindersWidget(phone.Configuration));
        widgets.Add(new ShortcutsWidget(phone.Shortcuts, phone.ShortcutRunner));
        widgets.Add(new QuickTogglesWidget(phone.Configuration, phone.Themes, phone.Calls));
        widgets.Add(new MusterWidget(phone.Musters));
        widgets.Add(new VenuesWidget(phone.Venues, phone.Configuration, phone.GameData, phone.RemoteImages,
            phone.Artwork));
        widgets.Add(new MarketWatchWidget(phone.MarketAlerts, phone.Textures));
        widgets.Add(new DailyGameWidget(phone.GameStats));
        widgets.Add(new DailySpinWidget(phone.CasinoSpin, phone.AethernetSession));
        widgets.Add(new LodestoneWidget(phone.News, phone.GameData, phone.RemoteImages));
    }
}
