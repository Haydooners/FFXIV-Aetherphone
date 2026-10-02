using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherphone.Core.Platform;

internal sealed unsafe class ServerBarEntry : IDisposable
{
    private const string AddonName = "_DTR";
    private const string IdleArtwork = "server-bar";
    private const string UnreadArtwork = "server-bar-unread";
    private const float ArtworkPixels = 15f;
    private const string ArtworkSlot = "     ";
    private const ushort ArtworkSlotWidth = 17;
    private const BitmapFontIcon FallbackIcon = BitmapFontIcon.Aethernet;

    private readonly IDtrBarEntry entry;
    private readonly Configuration configuration;
    private readonly NotificationService notifications;
    private readonly string? idleArtworkPath;
    private readonly string? unreadArtworkPath;
    private int unread;

    public ServerBarEntry(IDtrBar bar, Configuration configuration, NotificationService notifications, Action onClick)
    {
        this.configuration = configuration;
        this.notifications = notifications;
        var iconDirectory = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Icons");
        idleArtworkPath = ResolveArtwork(iconDirectory, IdleArtwork);
        unreadArtworkPath = ResolveArtwork(iconDirectory, UnreadArtwork);
        entry = bar.Get(AepConstants.Name);
        entry.OnClick = _ => onClick();
        if (idleArtworkPath is not null)
        {
            entry.MinimumWidth = ArtworkSlotWidth;
        }

        notifications.Changed += Refresh;
        configuration.BadgeSettingsChanged += Refresh;
        Plugin.PluginInterface.UiBuilder.Draw += Draw;
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
        Plugin.PluginInterface.UiBuilder.Draw -= Draw;
        notifications.Changed -= Refresh;
        configuration.BadgeSettingsChanged -= Refresh;
        entry.Remove();
    }

    private void Draw()
    {
        var artworkPath = unread > 0 ? unreadArtworkPath ?? idleArtworkPath : idleArtworkPath;
        if (artworkPath is null || Plugin.GameGui.GameUiHidden)
        {
            return;
        }

        var bounds = entry.ScreenBounds;
        if (bounds.Max == Vector2.Zero)
        {
            return;
        }

        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(AddonName).Address;
        if (addon == null || !addon->IsVisible || addon->RootNode == null)
        {
            return;
        }

        var texture = Plugin.TextureProvider.GetFromFile(artworkPath).GetWrapOrDefault();
        if (texture is null || texture.Handle == nint.Zero)
        {
            return;
        }

        var scale = addon->RootNode->ScaleX;
        var size = MathF.Round(ArtworkPixels * scale);
        var slotHeight = (bounds.Max.Y - bounds.Min.Y) * scale;
        var min = new Vector2(MathF.Round(bounds.Min.X), MathF.Round(bounds.Min.Y + (slotHeight - size) * 0.5f));
        ImGui.GetBackgroundDrawList().AddImage(texture.Handle, min, new Vector2(min.X + size, min.Y + size));
    }

    private SeString BuildText()
    {
        var builder = new SeStringBuilder();
        if (idleArtworkPath is null)
        {
            builder.AddIcon(FallbackIcon).AddText(AepConstants.ServerBarTag);
        }
        else
        {
            builder.AddText(string.Concat(ArtworkSlot, AepConstants.ServerBarTag));
        }

        if (unread > 0)
        {
            builder.AddText(string.Concat(" [", unread.ToString(Loc.Culture), "]"));
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

    private static string? ResolveArtwork(string directory, string name)
    {
        var path = Path.Combine(directory, name + ".png");
        if (!File.Exists(path))
        {
            return null;
        }

        Plugin.TextureSubstitution.InvalidatePaths(new[] { path });
        return path;
    }
}
