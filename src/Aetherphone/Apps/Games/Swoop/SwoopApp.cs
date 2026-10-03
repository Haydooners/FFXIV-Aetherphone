using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Swoop;

internal sealed class SwoopApp : IMiniGame
{
    private const string GameId = "swoop";
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Swoop.Title);
    public GameGenre Genre => GameGenre.Action;

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
