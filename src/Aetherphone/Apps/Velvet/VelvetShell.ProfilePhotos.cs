using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float ProfilePhotoGap = 2f;
    private const float ProfilePhotosBottomPad = 40f;

    private string profilePhotosStartId = string.Empty;
    private bool profilePhotosJumpPending;

    private void OpenProfilePhotos(string userId, string photoId)
    {
        profilePhotosStartId = photoId;
        profilePhotosJumpPending = true;
        router.Push(VelvetView.ProfilePhotos(userId));
    }

    private VelvetProfileDto? LoadedProfile(string userId)
    {
        if (store.Me is { } me && me.UserId == userId)
        {
            return me;
        }

        return store.ProfileUserId == userId ? store.ProfileUser : null;
    }

    private void DrawProfilePhotos(Rect area, string userId)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Velvet.Photos)))
        {
            router.Pop();
            return;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        var photos = LoadedProfile(userId) is { } user ? CardPhotos(user) : NoCardPhotos;
        using (var surface = AppSurface.BeginEdgeToEdge(body))
        {
            if (photos.Length == 0)
            {
                DrawEmpty(body, Loc.T(L.Velvet.NoPhotosShared), string.Empty);
                return;
            }

            var width = ScrollLayout.StableContentWidth();
            var drawList = ImGui.GetWindowDrawList();
            var contentTop = ImGui.GetCursorPosY();
            var jumpY = -1f;
            for (var index = 0; index < photos.Length; index++)
            {
                var photo = photos[index];
                if (profilePhotosJumpPending
                    && string.Equals(photo.Id, profilePhotosStartId, StringComparison.Ordinal))
                {
                    jumpY = ImGui.GetCursorPosY() - contentTop;
                }

                var height = PostAspects.TallDisplayHeight(width, photo.Width, photo.Height);
                var min = ImGui.GetCursorScreenPos();
                var max = new Vector2(min.X + width, min.Y + height);
                DrawMedia(drawList, min, max, photo.Url, 0f);
                if (UiInteract.Click(min, max))
                {
                    OpenPhotoViewer(photo.Url);
                }

                ImGui.Dummy(new Vector2(width, height));
                Gap(ProfilePhotoGap);
            }

            Gap(ProfilePhotosBottomPad);
            if (!profilePhotosJumpPending)
            {
                return;
            }

            profilePhotosJumpPending = false;
            if (jumpY >= 0f)
            {
                surface.JumpTo(jumpY);
            }
        }
    }
}
