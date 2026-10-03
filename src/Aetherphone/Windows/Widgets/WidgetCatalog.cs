using Aetherphone.Apps.AetherStream.Widgets;
using Aetherphone.Apps.Calendar;
using Aetherphone.Apps.Linkpearl.Widgets;
using Aetherphone.Apps.Message.Widgets;
using Aetherphone.Apps.Music.Widgets;
using Aetherphone.Apps.Notifications.Widgets;
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
            new NowPlayingWidget(phone.Playback, phone.SongHistory, phone.Media, phone.Http),
            new NowWatchingWidget(services.Video, phone.RemoteImages, phone.Http),
            new PeopleWidget(services.Messages, services.Messages.Contacts, phone.Configuration,
                phone.AethernetSession, phone.RemoteImages),
            new ChatsWidget(services.Messages, phone.Configuration, phone.AethernetSession, phone.RemoteImages),
            new TellsWidget(phone.ChatInbox, phone.ChatLog, phone.Lodestone),
            new RecentNotificationsWidget(phone.Notifications),
        };

        return new WidgetRegistry(widgets, services.Apps);
    }
}
