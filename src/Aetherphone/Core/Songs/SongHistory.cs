namespace Aetherphone.Core.Songs;

internal sealed class SongHistory
{
    private readonly LibraryStore library;

    public SongHistory(LibraryStore library)
    {
        this.library = library;
    }

    public Song[] Recent(int max) => library.RecentlyPlayed(max);

    public void Record(in Song song) => library.RecordPlay(song);
}
