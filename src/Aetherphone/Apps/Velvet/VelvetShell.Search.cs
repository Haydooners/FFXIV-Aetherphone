using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float SearchFieldHeight = 36f;
    private const float SearchFieldTop = 8f;
    private const float SearchDebounceSeconds = 0.45f;
    private const int SearchMaxLength = 64;
    private const float SearchRowHeight = 64f;
    private const float SearchAvatarRadius = 22f;

    private static readonly Vector4 SearchClearFill = new(1f, 1f, 1f, 0.14f);

    private readonly VelvetFilterSelection searchInclude = new();
    private string searchQuery = string.Empty;
    private string searchApplied = string.Empty;
    private float searchDebounce;
    private bool searchFocusPending;

    private void OpenSearch()
    {
        searchQuery = string.Empty;
        searchApplied = string.Empty;
        searchDebounce = 0f;
        searchFocusPending = true;
        store.ClearSearch();
        router.Push(VelvetView.Search);
    }

    private void DrawSearch(Rect area)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Common.Search)))
        {
            router.Pop();
            return;
        }

        var pad = SocialChrome.CellPadX * scale;
        var fieldTop = area.Min.Y + VHeader.Height * scale + SearchFieldTop * scale;
        var fieldRect = new Rect(new Vector2(area.Min.X + pad, fieldTop),
            new Vector2(area.Max.X - pad, fieldTop + SearchFieldHeight * scale));
        var palette = VelvetTheme.Palette;
        SearchField.Draw(fieldRect, "##velvetPeopleSearch", Loc.T(L.Velvet.SearchPeopleHint), ref searchQuery,
            palette.FieldSurface, palette.MutedInk, palette.TitleInk, SearchClearFill, palette.BackdropBottom,
            SearchMaxLength, searchFocusPending);
        searchFocusPending = false;
        TickSearch();

        var list = new Rect(new Vector2(area.Min.X, fieldRect.Max.Y + SearchFieldTop * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(list))
        {
            var results = store.SearchResults;
            if (searchApplied.Length == 0)
            {
                DrawEmpty(list, Loc.T(L.Velvet.SearchPeopleHint), string.Empty);
                return;
            }

            if (results.Length == 0)
            {
                if (store.LoadingSearch || !store.SearchLoaded)
                {
                    DrawEmpty(list, Loc.T(L.Common.Searching), string.Empty);
                }
                else
                {
                    DrawEmpty(list, Loc.T(L.Velvet.SearchNone), Loc.T(L.Velvet.SearchNoneHint));
                }

                return;
            }

            Gap(2f);
            for (var index = 0; index < results.Length; index++)
            {
                DrawSearchRow(results[index]);
            }

            Gap(40f);
        }
    }

    private void TickSearch()
    {
        if (searchQuery == searchApplied)
        {
            return;
        }

        searchDebounce += ImGui.GetIO().DeltaTime;
        if (searchDebounce < SearchDebounceSeconds)
        {
            return;
        }

        searchApplied = searchQuery;
        searchDebounce = 0f;
        var filter = VelvetFilterSelection.Combine(searchInclude, mutes) with { HasPhoto = false };
        store.SearchPeople(searchApplied, filter);
    }

    private void DrawSearchRow(VelvetProfileDto user)
    {
        var name = DisplayNameOf(user.DisplayName, user.Handle);
        var model = new VRowModel
        {
            Title = name,
            Subtitle = SocialIdentity.ProfileMeta(user.Handle, RegionCodeOf(user)),
            Height = SearchRowHeight,
            Leading = VRowLeading.Avatar,
            AvatarRadius = SearchAvatarRadius,
            Name = name,
            World = string.Empty,
            AvatarUrl = user.AvatarUrl,
            Presence = user.Presence,
            FrameId = user.FrameId,
            RoleBadges = user.Badges,
            RoleBadgeIds = user.BadgeIds,
            UserId = user.UserId,
            Chevron = true,
        };
        if (VRow.Cell(in model, ui, theme, images, lodestone) == VRowHit.Body)
        {
            OpenProfile(user.UserId);
        }
    }
}
