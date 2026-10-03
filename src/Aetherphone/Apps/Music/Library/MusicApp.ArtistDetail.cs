using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private void DrawArtistDetail(in PhoneContext context, in MusicRoute route)
    {
        var frame = BeginPage(context);
        EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.Microphone, route.Label,
            Loc.T(L.Music.ComingSoonSub));
        EndPage(in frame, context, route.Label);
    }
}
