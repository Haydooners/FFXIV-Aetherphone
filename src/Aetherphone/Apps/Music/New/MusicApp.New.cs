using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private void DrawNew(in PhoneContext context)
    {
        var frame = BeginPage(context);
        EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.Star, Loc.T(L.Music.NewEmptyTitle),
            Loc.T(L.Music.NewEmptySub));
        EndPage(in frame, context, Loc.T(L.Music.TabNew));
    }

    private void DrawGenreDetail(in PhoneContext context, in MusicRoute route)
    {
        var frame = BeginPage(context);
        EmptyState.Draw(Unobstructed(frame.Body), ui, FontAwesomeIcon.Music, route.Label,
            Loc.T(L.Music.ComingSoonSub));
        EndPage(in frame, context, route.Label);
    }
}
