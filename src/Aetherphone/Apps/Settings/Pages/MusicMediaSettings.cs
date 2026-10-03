using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Settings.Pages;

internal static class MusicMediaSettings
{
    public const string AppId = "music";
    private const int RowCount = 2;

    public static void Draw(Configuration configuration, PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Music.PcMedia.SettingsHeader), theme);
        var card = GroupCard.Begin(theme, RowCount);
        var show = SettingsRow.Bool(card.NextRow(), Loc.T(L.Music.PcMedia.ShowWindowsMedia),
            configuration.ShowWindowsMedia, theme, "settings.music.showWindowsMedia");
        var publish = SettingsRow.Bool(card.NextRow(), Loc.T(L.Music.PcMedia.PublishToWindowsMedia),
            configuration.PublishToWindowsMedia, theme, "settings.music.publishToWindowsMedia");
        card.End();
        SettingsSection.Hint(Loc.T(L.Music.PcMedia.SettingsHint), theme);
        if (show == configuration.ShowWindowsMedia && publish == configuration.PublishToWindowsMedia)
        {
            return;
        }

        configuration.ShowWindowsMedia = show;
        configuration.PublishToWindowsMedia = publish;
        configuration.Save();
    }
}
