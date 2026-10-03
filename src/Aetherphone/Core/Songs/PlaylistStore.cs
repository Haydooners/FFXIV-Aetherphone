namespace Aetherphone.Core.Songs;

internal sealed class PlaylistStore
{
    private readonly LibraryStore library;

    public PlaylistStore(LibraryStore library)
    {
        this.library = library;
    }

    public IReadOnlyList<PlaylistRecord> All => library.Playlists;

    public int Count => library.Playlists.Count;

    public PlaylistRecord? Find(string id) => library.FindPlaylist(id);

    public int SongCount(string id) => Find(id) is { } record ? record.Songs.Count : 0;

    public string Create(string name) => library.CreatePlaylist(name);

    public void Rename(string id, string name) => library.RenamePlaylist(id, name);

    public void Delete(string id) => library.DeletePlaylist(id);

    public bool Contains(string id, string videoId) => library.PlaylistContains(id, videoId);

    public void Add(string id, in Song song) => library.AddToPlaylist(id, song);

    public void Remove(string id, string videoId) => library.RemoveFromPlaylist(id, videoId);

    public Song[] Songs(string id) => library.PlaylistSongs(id);
}
