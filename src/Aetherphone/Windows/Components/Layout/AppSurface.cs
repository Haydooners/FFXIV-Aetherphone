using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class AppSurface
{
    public const float SidePadding = 16f;
    private const float TopPadding = 8f;
    private const float NavBarSnapTolerance = 0.5f;

    private static int depth;
    private static bool navBarArmed;
    private static float navBarBodyTop;
    private static float navBarInset;

    public static bool ActiveFreshVisit { get; private set; }

    public static Vector4? ScrollbarInk { get; set; }

    public static bool NavBarConsumed { get; private set; }

    public static float NavBarScrollY { get; private set; }

    public static void ArmNavBar(float bodyTop, float topInset)
    {
        navBarArmed = true;
        navBarBodyTop = bodyTop;
        navBarInset = topInset;
        NavBarConsumed = false;
        NavBarScrollY = 0f;
    }

    public static void DisarmNavBar() => navBarArmed = false;

    public static SurfaceScope Begin(Rect area, bool disableMouseWheelScroll = false) =>
        BeginCore(area, SidePadding, disableMouseWheelScroll);

    public static SurfaceScope Begin(Rect area, float sidePadding, bool disableMouseWheelScroll = false) =>
        BeginCore(area, sidePadding, disableMouseWheelScroll);

    public static SurfaceScope BeginEdgeToEdge(Rect area, bool disableMouseWheelScroll = false) =>
        BeginCore(area, 0f, disableMouseWheelScroll);

    private static SurfaceScope BeginCore(Rect area, float horizontalPadding, bool disableMouseWheelScroll)
    {
        var scale = UiScale.Current;
        var hostsNavBar = navBarArmed && depth == 0 &&
                          MathF.Abs(area.Min.Y - navBarBodyTop) <= NavBarSnapTolerance;
        if (hostsNavBar)
        {
            area = new Rect(new Vector2(area.Min.X, area.Min.Y - navBarInset), area.Max);
        }

        ImGui.SetCursorScreenPos(area.Min);
        var key = ImGui.GetID("##appSurface");
        var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding,
            new Vector2(horizontalPadding * scale, TopPadding * scale));
        var flags = DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground);
        if (disableMouseWheelScroll)
        {
            flags |= ImGuiWindowFlags.NoScrollWithMouse;
        }

        var scrollbar = ScrollbarInk is { } ink ? ScrollLayout.PushScrollbarInk(ink) : null;
        var child = ImRaii.Child("##appSurface", area.Size, false, flags);
        var freshVisit = ResetScrollOnNewVisit();
        ActiveFreshVisit = freshVisit;
        var surface = DragScrollHost.Begin(key);
        if (hostsNavBar)
        {
            ReserveNavBarBand(freshVisit);
        }

        depth++;
        return new SurfaceScope(child, padding, scrollbar, surface, freshVisit);
    }

    private static void ReserveNavBarBand(bool freshVisit)
    {
        navBarArmed = false;
        NavBarConsumed = true;
        NavBarScrollY = freshVisit ? 0f : ImGui.GetScrollY();
        var style = ImGui.GetStyle();
        var reserve = MathF.Max(0f, navBarInset - style.WindowPadding.Y - style.ItemSpacing.Y);
        ImGui.Dummy(new Vector2(0f, reserve));
    }

    public static bool ResetScrollOnNewVisit()
    {
        var visit = AppVisits.Active;
        if (visit == 0)
        {
            return false;
        }

        var storage = ImGui.GetStateStorage();
        var stampKey = ImGui.GetID("##appSurfaceVisit");
        if (storage.GetInt(stampKey, 0) == visit)
        {
            return false;
        }

        storage.SetInt(stampKey, visit);
        ImGui.SetScrollY(0f);
        return true;
    }

    public ref struct SurfaceScope
    {
        private ImRaii.ChildDisposable child;
        private readonly IDisposable padding;
        private readonly IDisposable? scrollbar;
        private readonly DragScrollHost.Surface surface;
        private readonly bool freshVisit;

        internal SurfaceScope(ImRaii.ChildDisposable child, IDisposable padding, IDisposable? scrollbar,
            DragScrollHost.Surface surface, bool freshVisit)
        {
            this.child = child;
            this.padding = padding;
            this.scrollbar = scrollbar;
            this.surface = surface;
            this.freshVisit = freshVisit;
        }

        public readonly float Pull => surface.Pull;

        public readonly bool Dragging => surface.Dragging;

        public readonly bool Scrolling => surface.Scrolling;

        public readonly bool FreshVisit => freshVisit;

        public readonly void JumpToTop() => surface.JumpToTop();

        public readonly void JumpTo(float scrollY) => surface.JumpTo(scrollY);

        public readonly void CancelDrag() => surface.CancelDrag();

        public void Dispose()
        {
            ActiveFreshVisit = false;
            depth = Math.Max(0, depth - 1);
            child.Dispose();
            padding?.Dispose();
            scrollbar?.Dispose();
        }
    }
}
