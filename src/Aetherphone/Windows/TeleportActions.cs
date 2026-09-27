using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows;

internal static class TeleportActions
{
    public static void AskThenTravel(ConfirmService confirm, string placeName, string address, string destination,
        bool lifestreamAvailable)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Common.TravelTitle, placeName),
            Message = string.Empty,
            Sections =
            [
                ConfirmSection.Paragraph(Loc.T(lifestreamAvailable ? L.Common.TravelWarning : L.Common.TravelCopyWarning)),
                ConfirmSection.Chip(Loc.T(L.Common.OpenLinkDestination), address.Length > 0 ? address : destination),
            ],
            ConfirmLabel = Loc.T(lifestreamAvailable ? L.Common.TravelConfirm : L.Common.TravelCopyConfirm),
            CancelLabel = Loc.T(L.Common.Cancel),
            Confirm = () => Travel(destination, lifestreamAvailable),
        });
    }

    private static void Travel(string destination, bool lifestreamAvailable)
    {
        if (lifestreamAvailable)
        {
            LifestreamBridge.Travel(destination);
            return;
        }

        ImGui.SetClipboardText(LifestreamBridge.TravelCommand(destination));
        ShellToast.Show();
    }
}
