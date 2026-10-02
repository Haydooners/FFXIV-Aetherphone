using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Platform;

internal sealed class ServerBarEntry : IDisposable
{
    private const BitmapFontIcon Icon = BitmapFontIcon.Aethernet;
    private const int LargestBoxedNumber = 31;

    private readonly IDtrBarEntry entry;
    private readonly Configuration configuration;
    private readonly NotificationService notifications;

    public ServerBarEntry(IDtrBar bar, Configuration configuration, NotificationService notifications, Action onClick)
    {
        this.configuration = configuration;
        this.notifications = notifications;
        entry = bar.Get(AepConstants.Name);
        entry.OnClick = _ => onClick();
        notifications.Changed += Refresh;
        configuration.BadgeSettingsChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        var unread = configuration.IsAppBadgeEnabled(NotificationChannels.NotificationsAppId)
            ? notifications.UnreadCount
            : 0;
        entry.Text = BuildText(unread);
        entry.Tooltip = BuildTooltip(unread);
    }

    public void Dispose()
    {
        notifications.Changed -= Refresh;
        configuration.BadgeSettingsChanged -= Refresh;
        entry.Remove();
    }

    private static SeString BuildText(int unread)
    {
        var builder = new SeStringBuilder().AddIcon(Icon).AddText(AepConstants.Name);
        if (unread <= 0)
        {
            return builder.Build();
        }

        return builder.AddText(string.Concat(" ", BadgeText(unread))).Build();
    }

    private static string BadgeText(int unread)
    {
        if (unread > LargestBoxedNumber)
        {
            return unread.ToString(Loc.Culture);
        }

        var glyph = (SeIconChar)((int)SeIconChar.BoxedNumber0 + unread);
        return glyph.ToIconString();
    }

    private static string BuildTooltip(int unread)
    {
        var unreadLine = unread > 0
            ? Loc.Plural(L.Plugin.ServerBarUnread, unread)
            : Loc.T(L.Plugin.ServerBarNoUnread);
        return string.Concat(AepConstants.Name, "\n", unreadLine, "\n", Loc.T(L.Plugin.ServerBarClickHint));
    }
}
