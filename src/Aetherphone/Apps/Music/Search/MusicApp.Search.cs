using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float SearchBarHeight = 50f;
    private const int SearchQueryLimit = 120;
    private const string SearchContext = "search";

    private readonly List<CommunityStationDto> communityMatches = new();
    private Song[] searchResults = Array.Empty<Song>();
    private volatile bool searching;
    private bool hasSearched;
    private bool focusSearch;
    private CancellationTokenSource? searchFetch;
    private string searchDraft = string.Empty;
    private string lastSearchQuery = string.Empty;

    private void OpenSearchFor(string query)
    {
        searchDraft = query;
        BeginSearch(query);
        tab = MusicTab.Search;
        Router.Reset();
    }

    private void DrawSearch(in PhoneContext context)
    {
        var scale = UiScale.Current;
        community.EnsureFresh(false);
        var frame = BeginPage(context);
        var body = frame.Body;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            DrawSearchBar(scale);
            var placeholder = new Rect(new Vector2(body.Min.X, ImGui.GetCursorScreenPos().Y), Unobstructed(body).Max);
            MatchCommunityStations(lastSearchQuery.Trim());
            if (searching)
            {
                LoadingPulse.Draw(placeholder.Center, 13f * scale, ui.Accent, ui.MutedInk, Loc.T(L.Common.Searching));
            }
            else if (searchResults.Length == 0 && communityMatches.Count == 0)
            {
                EmptyState.Draw(placeholder, ui, FontAwesomeIcon.Search,
                    Loc.T(hasSearched ? L.Music.NoResults : L.Music.SearchEmptyTitle),
                    Loc.T(hasSearched ? L.Music.NoResultsSub : L.Music.SearchEmptySub));
            }
            else
            {
                DrawCommunityMatches(scale);
                DrawSongRows(searchResults, SearchContext, Loc.T(L.Music.SourceSearch));
            }
        }

        EndPage(in frame, context, Loc.T(L.Common.Search));
    }

    private void DrawSearchBar(float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var bar = new Rect(origin, origin + new Vector2(width, SearchBarHeight * scale));
        if (focusSearch)
        {
            focusSearch = false;
            ImGui.SetKeyboardFocusHere();
        }

        var submitted = SearchField.DrawSubmit(bar, "##musicSearch", Loc.T(L.Music.SearchSongs), ref searchDraft,
            ui.Palette, SearchQueryLimit, MusicUi.Inset);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bar.Height));
        if (submitted && !string.IsNullOrWhiteSpace(searchDraft))
        {
            BeginSearch(searchDraft);
        }
    }

    private void BeginSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        searchFetch?.Cancel();
        searchFetch?.Dispose();
        searchFetch = new CancellationTokenSource();
        searching = true;
        hasSearched = true;
        lastSearchQuery = query;
        searchResults = Array.Empty<Song>();
        library.RecordSearch(query);
        _ = SearchAsync(query, searchFetch.Token);
    }

    private async Task SearchAsync(string query, CancellationToken token)
    {
        var found = await songSearch.SearchAsync(query, SongSearchScope.Songs, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        searchResults = found;
        searching = false;
    }

    private void MatchCommunityStations(string query)
    {
        communityMatches.Clear();
        if (query.Length == 0)
        {
            return;
        }

        var stations = community.Stations;
        for (var index = 0; index < stations.Length; index++)
        {
            if (MatchesQuery(stations[index], query))
            {
                communityMatches.Add(stations[index]);
            }
        }
    }

    private static bool MatchesQuery(CommunityStationDto station, string query)
    {
        if (station.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || station.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var index = 0; index < station.Tags.Length; index++)
        {
            if (station.Tags[index].Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawCommunityMatches(float scale)
    {
        if (communityMatches.Count == 0)
        {
            return;
        }

        SectionHeader.Draw(ui, Loc.T(L.Music.CommunityMatches), false, MusicUi.Inset, 0f);
        for (var index = 0; index < communityMatches.Count; index++)
        {
            DrawCommunityRow(scale, communityMatches[index], FeedCell.PadX);
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DisposeSearch()
    {
        searchFetch?.Cancel();
        searchFetch?.Dispose();
    }
}
