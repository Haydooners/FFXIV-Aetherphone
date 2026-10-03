using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private Song[] playlistSongs = Array.Empty<Song>();
    private string playlistSongsId = string.Empty;
    private int playlistSongsVersion = -1;

    private void DrawPlaylistDetail(in PhoneContext context, in MusicRoute route)
    {
        if (playlistSongsVersion != library.Version ||
            !string.Equals(playlistSongsId, route.Key, StringComparison.Ordinal))
        {
            playlistSongsVersion = library.Version;
            playlistSongsId = route.Key;
            playlistSongs = library.PlaylistSongs(route.Key);
        }

        var title = PlaylistTitle(route.Key);
        var frame = BeginPage(context);
        if (playlistSongs.Length == 0)
        {
            EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.ListUl, Loc.T(L.Music.PlaylistEmptyTitle),
                Loc.T(L.Music.PlaylistEmptySub));
            EndPage(in frame, context, title);
            return;
        }

        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            DrawSongRows(playlistSongs, route.Key, title);
        }

        EndPage(in frame, context, title);
    }
}
