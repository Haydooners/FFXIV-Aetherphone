using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class SupportPage : ISettingsPage
{
    public static readonly Vector4 PatreonCoral = new(1f, 0.259f, 0.302f, 1f);
    private readonly SupportCard card = new();

    public string Title => Loc.T(L.Settings.SupportAetherphone);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Heart;
    public Vector4 Tint => PatreonCoral;

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            card.Draw(context.Theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }
}
