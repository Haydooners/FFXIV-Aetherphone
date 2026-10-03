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
    private const int HomeRecentCount = 20;
    private const string HomeRecentContext = "home.recent";

    private readonly ShelfRail homeRecentRail = new();
    private Song[] homeRecent = Array.Empty<Song>();
    private int homeRecentVersion = -1;

    private void DrawHome(in PhoneContext context)
    {
        var scale = UiScale.Current;
        EnsureHomeRecent();
        var frame = BeginPage(context);
        if (homeRecent.Length == 0)
        {
            EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.Music, Loc.T(L.Music.SearchEmptyTitle),
                Loc.T(L.Music.SearchEmptySub));
            EndPage(in frame, context, Loc.T(L.Music.TabHome));
            return;
        }

        using (AppSurface.BeginEdgeToEdge(frame.Body))
        {
            DrawHomeRecentShelf(scale);
            ImGui.Dummy(new Vector2(0f, MusicUi.SectionGap * scale));
        }

        EndPage(in frame, context, Loc.T(L.Music.TabHome));
    }

    private void EnsureHomeRecent()
    {
        if (homeRecentVersion == library.Version)
        {
            return;
        }

        homeRecentVersion = library.Version;
        homeRecent = library.RecentlyPlayed(HomeRecentCount);
    }

    private void DrawHomeRecentShelf(float scale)
    {
        var side = ArtworkTile.Side(ArtworkTile.Standard);
        homeRecentRail.Begin(ui, Loc.T(L.Music.RecentlyPlayed), true, homeRecent.Length, side,
            ArtworkTile.CardHeight(side));
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < homeRecent.Length; index++)
        {
            if (!homeRecentRail.Tile(index, out var tile))
            {
                continue;
            }

            var song = homeRecent[index];
            var hovered = homeRecentRail.Hover(tile);
            ArtworkTile.Draw(drawList, images, tile.Min, side, song.ThumbnailUrl, song.Title);
            ArtworkTile.DrawPressed(drawList, tile.Min, side, hovered);
            ArtworkTile.DrawCaption(drawList, ui, tile.Min, side, song.Title, song.Author, kit.IsCurrent(song));
            if (hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
            {
                songMenu.Open(song);
            }

            if (homeRecentRail.Tapped(tile, hovered))
            {
                PlayFrom(homeRecent, index, HomeRecentContext, Loc.T(L.Music.RecentlyPlayed));
            }
        }

        homeRecentRail.End();
        if (homeRecentRail.SeeAllTapped)
        {
            Push(MusicRoute.Songs(SongListKind.RecentlyPlayed));
        }
    }
}
