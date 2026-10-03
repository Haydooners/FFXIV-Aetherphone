using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float LibraryRowHeight = 50f;
    private const float LibraryGlyphScale = 0.9f;
    private const float LibraryGlyphBox = 28f;
    private const int RecentlyPlayedListCount = 100;
    private const int TopSongsCount = 100;
    private const string LibrarySongsContext = "library.songs";

    private static readonly string[] SongListContexts =
    [
        "list.none", "list.loved", "list.downloaded", "list.recentlyAdded", "list.recentlyPlayed", "list.topSongs",
        "list.genre",
    ];

    private readonly Song[][] songListCaches = new Song[SongListContexts.Length][];
    private readonly int[] songListVersions = [-1, -1, -1, -1, -1, -1, -1];
    private Song[] librarySongs = Array.Empty<Song>();
    private int librarySongsVersion = -1;

    private void DrawLibrary(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var frame = BeginPage(context);
        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            if (DrawLibraryLink(scale, FontAwesomeIcon.ListUl, Loc.T(L.Music.LibraryPlaylists)))
            {
                Push(MusicRoute.Of(MusicScreen.LibraryPlaylists));
            }

            if (DrawLibraryLink(scale, FontAwesomeIcon.Microphone, Loc.T(L.Music.LibraryArtists)))
            {
                Push(MusicRoute.Of(MusicScreen.LibraryArtists));
            }

            if (DrawLibraryLink(scale, FontAwesomeIcon.Music, Loc.T(L.Music.LibrarySongs)))
            {
                Push(MusicRoute.Of(MusicScreen.LibrarySongs));
            }

            if (DrawLibraryLink(scale, FontAwesomeIcon.Heart, Loc.T(L.Music.LovedSongs)))
            {
                Push(MusicRoute.Songs(SongListKind.Loved));
            }

            if (DrawLibraryLink(scale, FontAwesomeIcon.ArrowDown, Loc.T(L.Music.Downloaded)))
            {
                Push(MusicRoute.Songs(SongListKind.Downloaded));
            }
        }

        EndPage(in frame, context, Loc.T(L.Music.TabLibrary));
    }

    private bool DrawLibraryLink(float scale, FontAwesomeIcon icon, string label)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, LibraryRowHeight * scale, ui.HoverWash);
        var inset = MusicUi.Inset * scale;
        var centerY = cell.Bounds.Min.Y + cell.Bounds.Height * 0.5f;
        var glyphBox = LibraryGlyphBox * scale;
        AppSkin.Icon(drawList, new Vector2(cell.Bounds.Min.X + inset + glyphBox * 0.5f, centerY), IconGlyph.Of(icon),
            ui.Accent, LibraryGlyphScale);
        var textLeft = cell.Bounds.Min.X + inset + glyphBox + Metrics.Space.Md * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - labelHeight * 0.5f),
            Typography.FitText(label, cell.Bounds.Max.X - inset * 2f - textLeft, TextStyles.Body), ui.TitleInk,
            TextStyles.Body);
        AppSkin.Icon(drawList, new Vector2(cell.Bounds.Max.X - inset, centerY),
            IconGlyph.Of(FontAwesomeIcon.ChevronRight), ui.MutedInk, LibraryGlyphScale * 0.7f);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        return cell.Tapped;
    }

    private void DrawLibraryPlaylists(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var frame = BeginPage(context);
        var playlists = library.Playlists;
        if (playlists.Count == 0)
        {
            EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.ListUl, Loc.T(L.Music.NoPlaylistsYet),
                Loc.T(L.Music.PlaylistEmptySub));
            EndPage(in frame, context, Loc.T(L.Music.LibraryPlaylists));
            return;
        }

        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            for (var index = 0; index < playlists.Count; index++)
            {
                var playlist = playlists[index];
                var cover = playlist.Songs.Count > 0 ? playlist.Songs[0].ThumbnailUrl : string.Empty;
                if (DrawArtRow(scale, cover, playlist.Name, MusicUi.SongCount(playlist.Songs.Count)))
                {
                    Push(MusicRoute.Playlist(playlist.Id));
                }
            }
        }

        EndPage(in frame, context, Loc.T(L.Music.LibraryPlaylists));
    }

    private void DrawLibraryArtists(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var frame = BeginPage(context);
        var artists = library.Artists;
        if (artists.Count == 0)
        {
            EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.Microphone,
                Loc.T(L.Music.LibraryEmptyTitle), Loc.T(L.Music.LibraryEmptySub));
            EndPage(in frame, context, Loc.T(L.Music.LibraryArtists));
            return;
        }

        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            for (var index = 0; index < artists.Count; index++)
            {
                var artist = artists[index];
                if (DrawArtRow(scale, artist.ThumbnailUrl, artist.Name, string.Empty))
                {
                    Push(MusicRoute.Artist(artist.ChannelId, artist.Name));
                }
            }
        }

        EndPage(in frame, context, Loc.T(L.Music.LibraryArtists));
    }

    private void DrawLibrarySongs(in PhoneContext context)
    {
        if (librarySongsVersion != library.Version)
        {
            librarySongsVersion = library.Version;
            librarySongs = library.LibrarySongs();
        }

        DrawSongListPage(context, librarySongs, Loc.T(L.Music.LibrarySongs), LibrarySongsContext);
    }

    private void DrawSongList(in PhoneContext context, in MusicRoute route)
    {
        var slot = (int)route.List;
        if (songListVersions[slot] != library.Version)
        {
            songListVersions[slot] = library.Version;
            songListCaches[slot] = LoadSongList(route.List);
        }

        DrawSongListPage(context, songListCaches[slot], SongListTitle(route), SongListContexts[slot]);
    }

    private Song[] LoadSongList(SongListKind kind)
    {
        return kind switch
        {
            SongListKind.Loved => library.LovedSongs(),
            SongListKind.Downloaded => DownloadedSongs(),
            SongListKind.RecentlyAdded => library.LibrarySongs(),
            SongListKind.RecentlyPlayed => library.RecentlyPlayed(RecentlyPlayedListCount),
            SongListKind.TopSongs => library.MostPlayed(TopSongsCount),
            _ => Array.Empty<Song>(),
        };
    }

    private Song[] DownloadedSongs()
    {
        var ids = library.DownloadedIds();
        var found = new List<Song>(ids.Length);
        for (var index = 0; index < ids.Length; index++)
        {
            if (library.FindSong(ids[index]) is { } record)
            {
                found.Add(record.ToSong());
            }
        }

        return found.ToArray();
    }

    private void DrawSongListPage(in PhoneContext context, Song[] songs, string title, string contextId)
    {
        var frame = BeginPage(context);
        if (songs.Length == 0)
        {
            EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.Music, Loc.T(L.Music.LibraryEmptyTitle),
                Loc.T(L.Music.LibraryEmptySub));
            EndPage(in frame, context, title);
            return;
        }

        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            DrawSongRows(songs, contextId, title);
        }

        EndPage(in frame, context, title);
    }

    private bool DrawArtRow(float scale, string artworkUrl, string title, string subtitle)
    {
        var height = SongRow.Height * scale;
        var width = ScrollLayout.StableContentWidth();
        if (!ImGui.IsRectVisible(new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return false;
        }

        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, height, ui.HoverWash);
        var inset = MusicUi.Inset * scale;
        var side = ArtworkTile.Side(ArtworkTile.RowArt);
        var artMin = new Vector2(cell.Bounds.Min.X + inset, cell.Bounds.Min.Y + (height - side) * 0.5f);
        ArtworkTile.Draw(drawList, images, artMin, side, artworkUrl, title);
        var textLeft = artMin.X + side + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, cell.Bounds.Max.X - inset - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Body);
        var subtitleHeight = subtitle.Length > 0 ? Typography.LineHeight(TextStyles.Subheadline) : 0f;
        var top = cell.Bounds.Min.Y + (height - titleHeight - subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(title, textWidth, TextStyles.Body),
            ui.TitleInk, TextStyles.Body);
        if (subtitle.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
                Typography.FitText(subtitle, textWidth, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
        }

        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        return cell.Tapped;
    }

    private void DrawImport(in PhoneContext context)
    {
        var frame = BeginPage(context);
        EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.FileImport, Loc.T(L.Music.ImportPlaylist),
            Loc.T(L.Music.ComingSoonSub));
        EndPage(in frame, context, Loc.T(L.Music.ImportPlaylist));
    }
}
