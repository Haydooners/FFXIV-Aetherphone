using Aetherphone.Core.Home;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal sealed class WidgetHost
{
    private readonly Configuration configuration;

    public WidgetHost(WidgetActions actions, Configuration configuration)
    {
        Actions = actions;
        this.configuration = configuration;
    }

    public WidgetActions Actions { get; }

    public WidgetMode Mode => WidgetModes.From(configuration.IconAppearance);

    public WidgetContext Tile(ImDrawListPtr drawList, Rect bounds, PhoneTheme theme, HomeTile tile, float scale,
        float delta, float opacity, bool interactive) =>
        new(drawList, bounds, theme, tile.Size, scale, delta, opacity, Mode, theme.Accent, interactive, false,
            tile.InstanceKey, tile.Config, Actions);

    public WidgetContext Preview(ImDrawListPtr drawList, Rect bounds, PhoneTheme theme, WidgetSize size, float scale,
        float delta) =>
        new(drawList, bounds, theme, size, scale, delta, 1f, Mode, theme.Accent, false, true,
            WidgetContext.PreviewKey, string.Empty, Actions);

    public void OpenTarget(HomeTile tile, Rect origin, PhoneTheme theme, float scale)
    {
        if (tile.Widget is not { } widget)
        {
            return;
        }

        var context = Tile(ImGui.GetWindowDrawList(), origin, theme, tile, scale, 0f, 1f, false);
        var route = widget.Target(context);
        Actions.Open(route.IsEmpty ? WidgetRoute.App(widget.AppId) : route, origin);
    }
}
