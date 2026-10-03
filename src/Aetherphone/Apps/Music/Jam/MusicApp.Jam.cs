using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private void DrawJamLobby(in PhoneContext context)
    {
        var frame = BeginPage(context);
        EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.Users, Loc.T(L.Music.Jam),
            Loc.T(L.Music.ComingSoonSub));
        EndPage(in frame, context, Loc.T(L.Music.Jam));
    }
}
