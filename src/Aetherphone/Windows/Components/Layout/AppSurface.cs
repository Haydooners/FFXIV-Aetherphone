using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class AppSurface
{
    public const float SidePadding = 16f;

    public static bool ActiveFreshVisit { get; private set; }

    public static Vector4? ScrollbarInk { get; set; }

    public static float LastScrollY { get; private set; }

    public static float ScrollOffsetThisFrame => lastScrollFrame == ImGui.GetFrameCount() ? LastScrollY : 0f;

    private static int lastScrollFrame = -1;
    private static float ambientBottomInset;

    public static BottomInsetScope ReserveBottom(float inset) => new(inset);

    public static SurfaceScope Begin(Rect area, bool disableMouseWheelScroll = false) =>
        BeginCore(area, SidePadding, disableMouseWheelScroll);

    public static SurfaceScope Begin(Rect area, float sidePadding, bool disableMouseWheelScroll = false) =>
        BeginCore(area, sidePadding, disableMouseWheelScroll);

    public static SurfaceScope BeginEdgeToEdge(Rect area, bool disableMouseWheelScroll = false) =>
        BeginCore(area, 0f, disableMouseWheelScroll);

    private static SurfaceScope BeginCore(Rect area, float horizontalPadding, bool disableMouseWheelScroll)
    {
        var scale = UiScale.Current;
        ImGui.SetCursorScreenPos(area.Min);
        var key = ImGui.GetID("##appSurface");
        var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding,
            new Vector2(horizontalPadding * scale, 8f * scale));
        var flags = DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground);
        if (disableMouseWheelScroll)
        {
            flags |= ImGuiWindowFlags.NoScrollWithMouse;
        }

        var scrollbar = ScrollbarInk is { } ink ? ScrollLayout.PushScrollbarInk(ink) : null;
        var child = ImRaii.Child("##appSurface", area.Size, false, flags);
        var freshVisit = ResetScrollOnNewVisit();
        ActiveFreshVisit = freshVisit;
        return new SurfaceScope(child, padding, scrollbar, DragScrollHost.Begin(key), freshVisit, ambientBottomInset);
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

    public ref struct BottomInsetScope
    {
        private readonly float previous;

        internal BottomInsetScope(float inset)
        {
            previous = ambientBottomInset;
            ambientBottomInset = MathF.Max(0f, inset);
        }

        public void Dispose() => ambientBottomInset = previous;
    }

    public ref struct SurfaceScope
    {
        private ImRaii.ChildDisposable child;
        private readonly IDisposable padding;
        private readonly IDisposable? scrollbar;
        private readonly DragScrollHost.Surface surface;
        private readonly bool freshVisit;
        private readonly float bottomInset;

        internal SurfaceScope(ImRaii.ChildDisposable child, IDisposable padding, IDisposable? scrollbar,
            DragScrollHost.Surface surface, bool freshVisit, float bottomInset)
        {
            this.child = child;
            this.padding = padding;
            this.scrollbar = scrollbar;
            this.surface = surface;
            this.freshVisit = freshVisit;
            this.bottomInset = bottomInset;
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
            if (bottomInset > 0f)
            {
                ImGui.Dummy(new Vector2(0f, bottomInset));
            }

            LastScrollY = ImGui.GetScrollY();
            lastScrollFrame = ImGui.GetFrameCount();
            child.Dispose();
            padding?.Dispose();
            scrollbar?.Dispose();
        }
    }
}
