using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Platform;

internal sealed class ServerBarEntry : IDisposable
{
    private const BitmapFontIcon Icon = BitmapFontIcon.Aethernet;

    private readonly IDtrBarEntry entry;
    private readonly Configuration configuration;
    private readonly NotificationService notifications;
    private int unread;

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
        unread = configuration.IsAppBadgeEnabled(NotificationChannels.NotificationsAppId)
            ? notifications.UnreadCount
            : 0;
        entry.Text = BuildText();
        entry.Tooltip = BuildTooltip();
    }

    public void Dispose()
    {
        notifications.Changed -= Refresh;
        configuration.BadgeSettingsChanged -= Refresh;
        entry.Remove();
    }

    private SeString BuildText()
    {
        var builder = new SeStringBuilder().AddIcon(Icon).AddText(AepConstants.ServerBarTag);
        if (unread > 0)
        {
            builder.AddText(string.Concat(" ", unread.ToString(Loc.Culture)));
        }

        return builder.Build();
    }

    private SeString BuildTooltip()
    {
        var unreadLine = unread > 0
            ? Loc.Plural(L.Plugin.ServerBarUnread, unread)
            : Loc.T(L.Plugin.ServerBarNoUnread);
        return new SeStringBuilder()
            .AddText(AepConstants.Name)
            .Add(NewLinePayload.Payload)
            .AddText(unreadLine)
            .Add(NewLinePayload.Payload)
            .AddText(Loc.T(L.Plugin.ServerBarClickHint))
            .Build();
    }
}
