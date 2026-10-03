using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Updraft;

internal sealed class UpdraftApp : IMiniGame
{
    private const string GameId = "updraft";
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Updraft.Title);
    public GameGenre Genre => GameGenre.Arcade;

    public void Open()
    {
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    public void Draw(in GameContext context)
    {
        GameScene.Ambient(ImGui.GetWindowDrawList(), context.Body, Accent);
    }
}
