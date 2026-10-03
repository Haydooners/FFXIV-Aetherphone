using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games;

internal enum GamesScreen : byte
{
    Root,
    Shelf,
    Playing,
    OnlineRoom,
}

internal enum GamesTab : byte
{
    Home,
    Together,
    Records,
    Search,
}

internal enum GamesShelf : byte
{
    Arcade = (byte)GameGenre.Arcade,
    Action = (byte)GameGenre.Action,
    Puzzle = (byte)GameGenre.Puzzle,
    Brain = (byte)GameGenre.Brain,
    Tabletop = (byte)GameGenre.Tabletop,
    Latest,
    All,
}

internal readonly record struct GamesRoute(GamesScreen Screen, GamesShelf Shelf)
{
    public static readonly GamesRoute Root = new(GamesScreen.Root, GamesShelf.All);
    public static readonly GamesRoute Playing = new(GamesScreen.Playing, GamesShelf.All);
    public static readonly GamesRoute OnlineRoom = new(GamesScreen.OnlineRoom, GamesShelf.All);

    public static GamesRoute ShelfOf(GamesShelf shelf) => new(GamesScreen.Shelf, shelf);

    public static GamesShelf ShelfFor(GameGenre genre) => (GamesShelf)genre;
}
