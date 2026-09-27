using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Venues;

internal sealed partial class VenuesApp : IPhoneApp, ISpotlightVenues
{
    private const float CellPadX = SocialChrome.CellPadX;
    private const float HeaderIconSize = 21f;
    private const float ScopePillHeight = 28f;

    private static readonly TextStyle WordmarkStyle = new(1.4f, FontWeight.Bold);
    private static readonly TextStyle ScopePillStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle ScreenTitleStyle = new(1.13f, FontWeight.Bold);
    private static readonly SocialInk Ink = new(AppPalettes.Venues);

    public string Id => "venues";
    public string DisplayName => Loc.T(L.Apps.Venues);
    public string Glyph => "V";
    public int BadgeCount => 0;

    private readonly VenuesService venues;
    private readonly RemoteImageCache images;
    private readonly ArtworkCache artwork;
    private readonly GameData gameData;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly TranslationService translation;
    private readonly AppSkin ui = new(AppPalettes.Venues);
    private readonly ViewRouter<VenueRoute> router;
    private readonly RouterDraw<VenueRoute> drawView;
    private readonly Action back;
    private readonly VenueQuery query = new();
    private readonly VenueLabelCache labels = new();
    private readonly List<string> selectedTags = new();
    private readonly DropdownMenu scopeMenu = new();
    private readonly DropdownMenu.Item[] scopeItems = new DropdownMenu.Item[2];
    private readonly Dictionary<string, string> venueLanguages = new(StringComparer.Ordinal);
    private string search = string.Empty;
    private bool favoritesOnly;
    private int favoritesStamp;
    private int tagsStamp;
    private bool lifestreamAvailable;
    private string pendingVenueId = string.Empty;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private Rect screenRect;

    public VenuesApp(VenuesService venues, RemoteImageCache images, ArtworkCache artwork, GameData gameData,
        Configuration configuration, ConfirmService confirm, TranslationService translation)
    {
        this.venues = venues;
        this.images = images;
        this.artwork = artwork;
        this.gameData = gameData;
        this.configuration = configuration;
        this.confirm = confirm;
        this.translation = translation;
        router = new ViewRouter<VenueRoute>(VenueRoute.List);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        search = string.Empty;
        ResetHomeScroll();
        lifestreamAvailable = LifestreamBridge.IsAvailable();
        venues.EnsureFresh(false);
    }

    public void OnClosed()
    {
        router.Reset();
        search = string.Empty;
        scopeMenu.Close();
    }

    public void RequestVenue(string venueId) => pendingVenueId = venueId;

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        venues.EnsureFresh(false);
        ConsumePendingVenue();
        var screen = SceneChrome.ScreenFrom(context.Content, theme, UiScale.Current);
        screenRect = screen;
        ui.Backdrop(screen);
        scopeMenu.Gate();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        DrawScopeMenu(screen);
    }

    private void ConsumePendingVenue()
    {
        if (pendingVenueId.Length == 0)
        {
            return;
        }

        var wanted = pendingVenueId;
        pendingVenueId = string.Empty;
        var events = venues.Events;
        for (var index = 0; index < events.Count; index++)
        {
            if (string.Equals(events[index].Id, wanted, StringComparison.Ordinal))
            {
                OpenDetail(events[index], false);
                return;
            }
        }
    }

    private void DrawView(VenueRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route.Screen)
        {
            case VenueScreen.Filters:
                DrawFilters(area);
                break;
            case VenueScreen.Detail:
                DrawDetail(area, route.Venue!);
                break;
            default:
                DrawHome(area);
                break;
        }
    }

    private string CurrentDataCenter()
    {
        if (configuration.VenueAllDataCenters)
        {
            return string.Empty;
        }

        return gameData.DataCenterName(gameData.LocalCurrentWorldId);
    }

    private void RefreshQuery()
    {
        if (labels.LanguageChanged())
        {
            query.Invalidate();
        }

        var nowUtc = DateTime.UtcNow;
        var key = new VenueQueryKey(venues.Version, configuration.VenueTimeFilter, configuration.VenueSourceFilter,
            CurrentDataCenter(), favoritesOnly, favoritesStamp, tagsStamp, search, nowUtc.Ticks / TimeSpan.TicksPerMinute);
        if (query.Update(key, venues.Events, configuration.VenueFavorites, selectedTags, nowUtc))
        {
            labels.Rebuild(query.Feed, query.Rail, query.LiveCount, nowUtc);
        }
    }

    private void OpenDetail(VenueEvent venue, bool animate = true)
    {
        detailScrollY = 0f;
        router.Push(VenueRoute.Detail(venue), animate);
    }

    private void DrawScopeMenu(Rect screen)
    {
        var dataCenter = gameData.DataCenterName(gameData.LocalCurrentWorldId);
        var hasHome = dataCenter.Length > 0;
        scopeItems[0] = new DropdownMenu.Item(hasHome ? dataCenter : Loc.T(L.Venues.DataCenter),
            Selected: hasHome && !configuration.VenueAllDataCenters);
        scopeItems[1] = new DropdownMenu.Item(Loc.T(L.Venues.AllDataCentersLong),
            Selected: !hasHome || configuration.VenueAllDataCenters);
        var picked = scopeMenu.Draw(screen, theme, scopeItems);
        if (picked < 0)
        {
            return;
        }

        var allDataCenters = picked == 1 || !hasHome;
        if (allDataCenters == configuration.VenueAllDataCenters)
        {
            return;
        }

        configuration.VenueAllDataCenters = allDataCenters;
        configuration.Save();
        ResetHomeScroll();
    }

    private bool FiltersActive => selectedTags.Count > 0 || configuration.VenueSourceFilter != VenueFilter.SourceAll;

    private int ActiveFilterCount =>
        selectedTags.Count + (configuration.VenueSourceFilter != VenueFilter.SourceAll ? 1 : 0);

    private bool IsFavorite(string id) => VenueFilter.Contains(configuration.VenueFavorites, id);

    private void ToggleFavorite(string id)
    {
        if (!configuration.VenueFavorites.Remove(id))
        {
            configuration.VenueFavorites.Add(id);
        }

        favoritesStamp++;
        configuration.Save();
    }

    private bool IsTagSelected(string tag) => VenueFilter.Contains(selectedTags, tag);

    private void ToggleTag(string tag)
    {
        tagsStamp++;
        for (var index = 0; index < selectedTags.Count; index++)
        {
            if (string.Equals(selectedTags[index], tag, StringComparison.OrdinalIgnoreCase))
            {
                selectedTags.RemoveAt(index);
                return;
            }
        }

        selectedTags.Add(tag);
    }

    private void ResetFilters()
    {
        selectedTags.Clear();
        tagsStamp++;
        configuration.VenueSourceFilter = VenueFilter.SourceAll;
        configuration.Save();
    }

    private static bool DrawHeaderIcon(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip,
        bool highlighted = false, int badge = 0) =>
        SocialChrome.DrawHeaderIcon(drawList, center, SocialChrome.HeaderIconRadius * UiScale.Current, glyph,
            HeaderIconSize, tooltip, Ink, Ink.MutedInk, highlighted, badge);

    private float DrawScreenHeader(Rect area, string title, int trailingSlots = 0) =>
        SocialChrome.DrawScreenHeader(area, title, Ink, back, ScreenTitleStyle,
            SocialChrome.HeaderReserve(trailingSlots), string.Empty, true, true);

    private void Teleport(VenueEvent venue)
    {
        if (!venue.CanTeleport)
        {
            return;
        }

        if (lifestreamAvailable)
        {
            LifestreamBridge.Travel(venue.TeleportCode!);
            return;
        }

        ImGui.SetClipboardText(LifestreamBridge.TravelCommand(venue.TeleportCode!));
        ShellToast.Show();
    }

    public void Dispose()
    {
    }
}
