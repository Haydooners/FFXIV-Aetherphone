namespace Aetherphone.Core.Songs;

[Serializable]
internal sealed class MusicLibraryData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public List<PlaylistRecord> Playlists { get; set; } = new();
    public List<SongRecord> Songs { get; set; } = new();
    public List<string> Loved { get; set; } = new();
    public List<string> Downloads { get; set; } = new();
    public List<PlayRecord> Plays { get; set; } = new();
    public List<ArtistRecord> Artists { get; set; } = new();
    public List<string> RecentSearches { get; set; } = new();
}

[Serializable]
internal sealed class PlayRecord
{
    public SongRecord Song { get; set; } = new();
    public int Count { get; set; }
    public long FirstPlayedUnix { get; set; }
    public long LastPlayedUnix { get; set; }
}

[Serializable]
internal sealed class ArtistRecord
{
    public string ChannelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public long FollowedUnix { get; set; }
}
