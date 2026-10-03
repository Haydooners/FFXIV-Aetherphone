using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private void DrawJamLobby(in PhoneContext context)
    {
        var frame = BeginPage(context);
        EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.Users, Loc.T(L.Music.Jam.Title),
            Loc.T(L.Music.ComingSoonSub));
        EndPage(in frame, context, Loc.T(L.Music.Jam.Title));
    }

    private void DrawJamHomeCard(float scale)
    {
    }

    private float DrawJamNowPlayingBadge(ImDrawListPtr drawList, Vector2 min, float width, float scale)
    {
        return 0f;
    }
}
