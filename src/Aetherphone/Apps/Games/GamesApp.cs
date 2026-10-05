using Aetherphone.Apps.Games.Beat;
using Aetherphone.Apps.Games.Blade;
using Aetherphone.Apps.Games.Breakout;
using Aetherphone.Apps.Games.BubbleShooter;
using Aetherphone.Apps.Games.CapMan;
using Aetherphone.Apps.Games.Chess;
using Aetherphone.Apps.Games.Coil;
using Aetherphone.Apps.Games.CrystalDrop;
using Aetherphone.Apps.Games.Doom;
using Aetherphone.Apps.Games.Flap;
using Aetherphone.Apps.Games.Flow;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.GemSwap;
using Aetherphone.Apps.Games.Hop;
using Aetherphone.Apps.Games.Invaders;
using Aetherphone.Apps.Games.Nonogram;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Apps.Games.Pairs;
using Aetherphone.Apps.Games.Reversi;
using Aetherphone.Apps.Games.Simon;
using Aetherphone.Apps.Games.Skyfall;
using Aetherphone.Apps.Games.Snake;
using Aetherphone.Apps.Games.Solitaire;
using Aetherphone.Apps.Games.Squadron;
using Aetherphone.Apps.Games.Stack;
using Aetherphone.Apps.Games.Sudoku;
using Aetherphone.Apps.Games.Sweeper;
using Aetherphone.Apps.Games.Swoop;
using Aetherphone.Apps.Games.Tetris;
using Aetherphone.Apps.Games.Trivia;
using Aetherphone.Apps.Games.Twenty48;
using Aetherphone.Apps.Games.Updraft;
using Aetherphone.Apps.Games.WaterSort;
using Aetherphone.Apps.Games.Whack;
using Aetherphone.Apps.Games.WordRun;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Changelog;
using Aetherphone.Core.Game;
using Aetherphone.Core.Games;
using Aetherphone.Core.Honorific;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp : IPhoneApp, ITabRouteTarget, INameplateActivitySource
{
    private readonly struct CoinSessionChip
    {
        public readonly string Label;
        public readonly float Fraction;
        public readonly bool Qualified;
        public readonly bool CoolingDown;
        public readonly bool Visible;

        public CoinSessionChip(string label, float fraction, bool qualified, bool coolingDown = false)
        {
            Label = label;
            Fraction = fraction;
            Qualified = qualified;
            CoolingDown = coolingDown;
            Visible = true;
        }
    }

    public const string HomeTabRoute = "games.tab.home";
    public const string TogetherTabRoute = "games.tab.together";
    public const string RecordsTabRoute = "games.tab.records";
    public const string SearchTabRoute = "games.tab.search";
    private const string PlayRoutePrefix = "games.play.";
    private const int TabCount = 4;
    private const float HeaderHeight = 42f;
    private const float LandscapeBackRadius = 16f;
    private const float LandscapeBackInset = 10f;
    private const float CoinChipRingRadius = 7f;
    private const float CoinChipGap = 5f;
    private const float CoinChipReserve = 72f;
    private const string CoinChipTooltipId = "games.coinChip";
    private const float PausedFadeSeconds = 0.12f;
    private const int FeaturedStep = 5;

    private static readonly string[] TabIds = [HomeTabRoute, TogetherTabRoute, RecordsTabRoute, SearchTabRoute];

    private readonly GameStatsStore stats;
    private readonly Configuration configuration;
    private readonly Core.Coins.CoinStore coins;
    private readonly Core.Coins.CoinGameSessionTracker coinSessions;
    private readonly Windows.Components.CoinFloat coinFloats = new();
    private readonly GameRoomsStore gameRooms;
    private readonly OnlineHub onlineHub;
    private readonly OnlineRoomView onlineRoom;
    private readonly IMiniGame[] games;
    private readonly GamesLibrary library;
    private readonly AppSkin ui = new(AppPalettes.Games);
    private readonly ViewRouter<GamesRoute> router;
    private readonly RouterDraw<GamesRoute> drawView;
    private readonly Action back;
    private readonly TabBar tabBar = new();
    private readonly TabItem[] tabItems = new TabItem[TabCount];
    private readonly string[] countLabels;
    private Spring pausedVeil = new(0f);
    private Rect screenRect;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private IMiniGame? currentGame;
    private GamesTab tab;
    private string pendingRoute = string.Empty;
    private LanguageInfo? countLanguage;
    private int featuredIndex;
    private float frameSeconds;
    public string Id => "games";
    public string DisplayName => Loc.T(L.Apps.Games);
    public string Glyph => ">";
    public int BadgeCount => configuration.HasUnseenFeaturePin(NewFeaturePins.Games) ? 1 : 0;
    public bool BadgeAsDot => true;

    public GamesApp(GameStatsStore stats, GameData gameData, ITextureProvider textures,
        Core.Coins.CoinStore coins, Core.Coins.CoinGameSessionTracker coinSessions,
        GameRoomsStore gameRooms, Configuration configuration)
    {
        this.stats = stats;
        this.configuration = configuration;
        this.coins = coins;
        this.coinSessions = coinSessions;
        this.gameRooms = gameRooms;
        onlineHub = new OnlineHub(gameRooms, OpenOnlineRoom);
        onlineRoom = new OnlineRoomView(gameRooms);
        games = new IMiniGame[]
        {
            new SweeperApp(), new PairsApp(), new GemSwapApp(), new TetrisApp(), new Twenty48App(),
            new WaterSortApp(), new BreakoutApp(), new BubbleShooterApp(), new NonogramApp(), new FlowApp(),
            new SolitaireApp(), new SimonApp(), new FlapApp(), new ReversiApp(), new WhackApp(), new SnakeApp(),
            new SudokuApp(), new ChessApp(), new StackApp(), new CrystalDropApp(), new BeatApp(), new BladeApp(),
            new TriviaApp(gameData, textures), new SkyfallApp(), new InvadersApp(), new CapManApp(), new HopApp(), new SquadronApp(), new DoomApp(), new WordRunApp(gameData),
            new CoilApp(), new UpdraftApp(), new SwoopApp(),
        };
        library = new GamesLibrary(games, stats);
        countLabels = new string[library.Entries.Length + 1];
        RebuildLayout();
        router = new ViewRouter<GamesRoute>(GamesRoute.Root);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public IMiniGame DailyGame => games[FeaturedIndex()];

    public GamesLibrary Library => library;

    public static string PlayRoute(string gameId) => PlayRoutePrefix + gameId;

    public bool TryNameplateActivity(NameplateStatus status, out NameplateValues values)
    {
        values = NameplateValues.Empty;
        if (status != NameplateStatus.Games)
        {
            return false;
        }

        var title = PlayingTitle();
        if (title.Length == 0)
        {
            return false;
        }

        values = NameplateValues.Empty with { Game = title };
        return true;
    }

    private string PlayingTitle()
    {
        if (currentGame is not null)
        {
            return currentGame.Title;
        }

        if (router.Current.Screen != GamesScreen.OnlineRoom || gameRooms.Room.State is not { } room)
        {
            return string.Empty;
        }

        return Loc.T(GamesOnlineText.GameName(room.Snapshot.GameKind));
    }

    public void OpenTab(string tab) => pendingRoute = tab ?? string.Empty;

    private void RebuildLayout()
    {
        featuredIndex = FeaturedIndex();
        stats.DailyGameId = games[featuredIndex].Id;
        library.Rebuild();
    }

    private int FeaturedIndex()
    {
        var serverFeatured = coins.Wallet?.FeaturedGameId;
        if (!string.IsNullOrEmpty(serverFeatured))
        {
            for (var index = 0; index < games.Length; index++)
            {
                if (string.Equals(games[index].Id, serverFeatured, StringComparison.Ordinal))
                {
                    return index;
                }
            }
        }

        return GameStatsStore.TodayIndex * FeaturedStep % games.Length;
    }

    public void OnOpened()
    {
        configuration.MarkFeaturePinSeen(NewFeaturePins.Games);
        router.Reset();
        tab = GamesTab.Home;
        RebuildLayout();
        ResetLauncher();
        onlineHub.Reset();
    }

    public void OnClosed()
    {
        CloseCurrentGame();
        gameRooms.Exit();
        AppLandscape.Release(Id);
        router.Reset();
        pendingRoute = string.Empty;
    }

    public void Dispose()
    {
        for (var index = 0; index < games.Length; index++)
        {
            games[index].Dispose();
        }
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        frameSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        library.EnsureLanguage();
        SyncCountLabels();
        screenRect = SceneChrome.ScreenFrom(context.Content, theme, UiScale.Current);
        ConsumePendingRoute();
        if (!router.IsTransitioning && router.Current.Screen is GamesScreen.Root or GamesScreen.Shelf)
        {
            onlineHub.Consume();
        }

        if (router.IsTransitioning || router.Current.Screen != GamesScreen.Playing)
        {
            ui.Backdrop(screenRect);
        }

        var appArea = SceneChrome.AppAreaFrom(context.Content, theme, UiScale.Current);
        router.Draw(appArea, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        if (!router.IsTransitioning && router.Current.Screen is GamesScreen.Root or GamesScreen.Shelf
            && currentGame is not null)
        {
            CloseCurrentGame();
        }

        var award = coinSessions.TakeAward(out _);
        if (award is not null)
        {
            var anchor = new Vector2(context.Content.Center.X, context.Content.Min.Y + 96f * UiScale.Current);
            if (award.Granted && award.Amount > 0)
            {
                coinFloats.Spawn(Loc.T(L.Coin.CheckInReward, NumberText.Group(award.Amount)), anchor);
                coins.AbsorbLocalAward(award.Balance);
            }
            else if (award.Reason == "too_short")
            {
                coinFloats.Spawn(Loc.T(L.Coin.SessionTooShort), anchor, true);
            }
        }

        coinFloats.Draw(ImGui.GetWindowDrawList(), Core.Apps.AppAccents.For("coin"), theme.TextMuted,
            ImGui.GetIO().DeltaTime);
    }

    private void SyncCountLabels()
    {
        if (ReferenceEquals(countLanguage, Loc.Current))
        {
            return;
        }

        countLanguage = Loc.Current;
        Array.Clear(countLabels);
        roomsLabelCount = -1;
        dailyEyebrow = Loc.Culture.TextInfo.ToUpper(Loc.T(L.Games.Daily));
        recordsText.Reset();
        streakText.Reset();
        recordCountText.Reset();
        onlineHub.ResetLabels();
    }

    private void ConsumePendingRoute()
    {
        if (pendingRoute.Length == 0 || router.IsTransitioning)
        {
            return;
        }

        var route = pendingRoute;
        pendingRoute = string.Empty;
        if (router.Current.Screen is GamesScreen.OnlineRoom or GamesScreen.Playing)
        {
            return;
        }

        if (route.StartsWith(PlayRoutePrefix, StringComparison.Ordinal))
        {
            var game = FindGame(route.AsSpan(PlayRoutePrefix.Length));
            if (game is null)
            {
                return;
            }

            router.Reset();
            tab = GamesTab.Home;
            OpenGame(game, false);
            return;
        }

        for (var index = 0; index < TabIds.Length; index++)
        {
            if (string.Equals(TabIds[index], route, StringComparison.Ordinal))
            {
                router.Reset();
                SelectTab((GamesTab)index);
                return;
            }
        }
    }

    private IMiniGame? FindGame(ReadOnlySpan<char> id)
    {
        for (var index = 0; index < games.Length; index++)
        {
            if (id.SequenceEqual(games[index].Id))
            {
                return games[index];
            }
        }

        return null;
    }

    private void DrawView(GamesRoute route, Rect area, int depth)
    {
        var context = new PhoneContext(area, theme, navigation);
        switch (route.Screen)
        {
            case GamesScreen.Playing:
                ImGui.GetWindowDrawList().AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(theme.AppBackground));
                DrawActiveGame(context);
                return;
            case GamesScreen.OnlineRoom:
                ImGui.GetWindowDrawList().AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(theme.AppBackground));
                SyncOnlineRoomLandscape();
                onlineRoom.Draw(context, LeaveOnlineRoom, ui,
                    AppLandscape.Held(Id) && context.Content.IsLandscape(), TabTitle(GamesTab.Together));
                return;
            case GamesScreen.Shelf:
                PaintViewBackdrop(area);
                DrawShelfPage(context, route.Shelf);
                return;
            default:
                PaintViewBackdrop(area);
                DrawRoot(context, area);
                return;
        }
    }

    private void PaintViewBackdrop(Rect area)
    {
        ui.Body(area);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(area.Min, area.Max, true);
        GameScene.Ambient(drawList, area, games[featuredIndex].Accent);
        drawList.PopClipRect();
    }

    private void DrawRoot(in PhoneContext context, Rect area)
    {
        using (TabBar.ReserveContent(UiScale.Current))
        {
            ImGui.PushID(TabIds[(int)tab]);
            switch (tab)
            {
                case GamesTab.Together:
                    DrawTogether(context);
                    break;
                case GamesTab.Records:
                    DrawRecords(context);
                    break;
                case GamesTab.Search:
                    DrawSearch(context);
                    break;
                default:
                    DrawHome(context);
                    break;
            }

            ImGui.PopID();
        }

        DrawTabBar(area);
    }

    private void DrawTabBar(Rect area)
    {
        tabItems[(int)GamesTab.Home] = new TabItem(Loc.T(L.GamesHub.TabHome), IconGlyph.Of(FontAwesomeIcon.Gamepad),
            AnchorKey: HomeTabRoute);
        tabItems[(int)GamesTab.Together] = new TabItem(Loc.T(L.Games.OnlineTitle),
            IconGlyph.Of(FontAwesomeIcon.UserFriends), AnchorKey: TogetherTabRoute);
        tabItems[(int)GamesTab.Records] = new TabItem(Loc.T(L.GamesHub.TabRecords),
            IconGlyph.Of(FontAwesomeIcon.Trophy), AnchorKey: RecordsTabRoute);
        tabItems[(int)GamesTab.Search] = new TabItem(Loc.T(L.Common.Search), IconGlyph.Of(FontAwesomeIcon.Search),
            AnchorKey: SearchTabRoute);
        var result = tabBar.Draw(area, ui, tabItems, (int)tab);
        if (result.Tapped < 0)
        {
            return;
        }

        SelectTab((GamesTab)result.Tapped);
    }

    private void SelectTab(GamesTab wanted)
    {
        if (wanted == GamesTab.Search)
        {
            focusSearch = true;
        }

        if (wanted == GamesTab.Together)
        {
            gameRooms.EnsureFresh();
        }

        if (wanted == tab)
        {
            return;
        }

        tab = wanted;
        entrance = 0f;
    }

    private string TabTitle(GamesTab target) => target switch
    {
        GamesTab.Together => Loc.T(L.Games.OnlineTitle),
        GamesTab.Records => Loc.T(L.GamesHub.TabRecords),
        GamesTab.Search => Loc.T(L.Common.Search),
        _ => DisplayName,
    };

    private void OpenOnlineHub(string preferredKind)
    {
        onlineHub.Highlight(preferredKind);
        if (router.Depth > 1)
        {
            router.Reset();
        }

        SelectTab(GamesTab.Together);
    }

    private void OpenOnlineRoom(string roomId, string gameKind)
    {
        stats.MarkPlayed(GamesLibrary.OnlineEntryId(gameKind));
        onlineRoom.Enter();
        router.Push(GamesRoute.OnlineRoom);
    }

    private void LeaveOnlineRoom()
    {
        AppLandscape.Release(Id);
        gameRooms.Exit();
        router.Pop();
        library.Rebuild();
    }

    private void SyncOnlineRoomLandscape()
    {
        if (onlineRoom.WantsLandscape)
        {
            AppLandscape.Request(Id);
            return;
        }

        AppLandscape.Release(Id);
    }

    private void DrawActiveGame(in PhoneContext context)
    {
        var game = currentGame!;
        var scale = UiScale.Current;
        var content = context.Content;
        var landscape = game.WantsLandscape && AppLandscape.Held(Id) && content.IsLandscape();
        Rect body;
        if (landscape)
        {
            body = content;
        }
        else
        {
            var chip = BuildCoinSessionChip();
            if (chip.Visible)
            {
                AppHeader.Draw(context, "games.header.title", game.Title, CoinChipReserve * scale, back);
                DrawCoinSessionChip(chip, content, context.Theme, scale);
            }
            else
            {
                AppHeader.Draw(context, game.Title, back);
            }

            body = new Rect(new Vector2(content.Min.X, content.Min.Y + HeaderHeight * scale), content.Max);
        }

        using (AppSurface.Begin(body))
        {
            var attentive = GameFocus.Active;
            var frameSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
            game.Draw(new GameContext(body, context.Theme, stats, attentive ? frameSeconds : 0f));
            pausedVeil.Step(!attentive && game.RunsOnAClock ? 1f : 0f, PausedFadeSeconds, frameSeconds);
            DrawPausedVeil(body, context.Theme);
            if (landscape)
            {
                DrawLandscapeBack(body, context.Theme, scale);
            }
        }
    }

    private void DrawLandscapeBack(Rect body, PhoneTheme theme, float scale)
    {
        var radius = LandscapeBackRadius * scale;
        var center = body.Min + new Vector2(radius + LandscapeBackInset * scale, radius + LandscapeBackInset * scale);
        if (GameHud.LandscapeBack(center, radius, theme))
        {
            back();
        }
    }

    private CoinSessionChip BuildCoinSessionChip()
    {
        var wallet = coins.Wallet;
        if (wallet is not null && RuleExhausted(wallet, "game.session") && RuleExhausted(wallet, "game.deep"))
        {
            return default;
        }

        var seconds = coinSessions.OpenSessionSeconds;
        if (seconds < 0)
        {
            var cooldown = coinSessions.CooldownSeconds;
            return cooldown > 0
                ? new CoinSessionChip(TimeText.Duration(cooldown), coinSessions.CooldownProgress, false, true)
                : default;
        }

        var minSeconds = coinSessions.OpenMinSeconds;
        if (seconds < minSeconds)
        {
            return new CoinSessionChip(TimeText.Duration(minSeconds - seconds), seconds / (float)minSeconds, false);
        }

        var deepSeconds = coinSessions.OpenDeepSeconds;
        if (seconds < deepSeconds)
        {
            return new CoinSessionChip(TimeText.Duration(deepSeconds - seconds),
                (seconds - minSeconds) / (float)(deepSeconds - minSeconds), true);
        }

        return new CoinSessionChip(string.Empty, 1f, true);
    }

    private static void DrawCoinSessionChip(in CoinSessionChip chip, Rect content, PhoneTheme theme, float scale)
    {
        var accent = chip.CoolingDown ? theme.TextMuted : AppAccents.For("coin");
        var ringRadius = CoinChipRingRadius * scale;
        var thickness = Metrics.Stroke.Ring * scale;
        var rowCenterY = content.Min.Y + HeaderHeight * scale * 0.5f;
        var right = content.Max.X - Metrics.Space.Md * scale;
        var textSize = chip.Label.Length > 0 ? Typography.Measure(chip.Label, TextStyles.Caption1) : Vector2.Zero;
        var labelSpan = chip.Label.Length > 0 ? textSize.X + CoinChipGap * scale : 0f;
        var ringCenter = new Vector2(right - labelSpan - ringRadius, rowCenterY);
        ProgressRing.Track(ringCenter, ringRadius, thickness, Palette.WithAlpha(accent, 0.28f));
        ProgressRing.Fill(ringCenter, ringRadius, thickness, chip.Fraction, accent);
        if (chip.CoolingDown)
        {
            ProgressRing.CenterIcon(ImGui.GetWindowDrawList(), ringCenter, FontAwesomeIcon.HourglassHalf, accent,
                ringRadius * 0.95f);
        }
        else if (chip.Qualified)
        {
            ProgressRing.CenterIcon(ImGui.GetWindowDrawList(), ringCenter, FontAwesomeIcon.Check, accent,
                ringRadius * 1.05f);
        }
        else
        {
            CurrencyGlyph.Draw(ImGui.GetWindowDrawList(), CurrencyKind.Coins, ringCenter, ringRadius * 1.1f);
        }

        var hoverHalfHeight = HeaderHeight * scale * 0.35f;
        var hoverRect = new Rect(new Vector2(ringCenter.X - ringRadius - thickness, rowCenterY - hoverHalfHeight),
            new Vector2(right, rowCenterY + hoverHalfHeight));
        HoverTooltip.Show(CoinChipTooltipId, hoverRect, CoinChipHint(chip));
        if (chip.Label.Length == 0)
        {
            return;
        }

        Typography.DrawCentered(new Vector2(right - textSize.X * 0.5f, rowCenterY), chip.Label,
            chip.CoolingDown ? theme.TextMuted : theme.TextStrong, TextStyles.Caption1);
    }

    private static string CoinChipHint(in CoinSessionChip chip)
    {
        if (chip.CoolingDown)
        {
            return Loc.T(L.Games.CoinTimerCooldownHint);
        }

        if (!chip.Qualified)
        {
            return Loc.T(L.Games.CoinTimerHint);
        }

        return chip.Label.Length > 0 ? Loc.T(L.Games.CoinTimerDeepHint) : Loc.T(L.Games.CoinTimerDoneHint);
    }

    private static bool RuleExhausted(CoinWalletDto wallet, string ruleId)
    {
        for (var index = 0; index < wallet.Rules.Length; index++)
        {
            ref readonly var rule = ref wallet.Rules[index];
            if (string.Equals(rule.RuleId, ruleId, StringComparison.Ordinal))
            {
                return rule.PeriodCap > 0 && rule.EarnedThisPeriod >= rule.PeriodCap;
            }
        }

        return false;
    }

    private void DrawPausedVeil(Rect body, PhoneTheme theme)
    {
        var alpha = Math.Clamp(pausedVeil.Value, 0f, 1f);
        if (alpha <= 0.01f)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(body.Min, body.Max,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.72f * alpha)));
        var center = body.Center;
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y - 12f * scale), Loc.T(L.Games.Paused),
            new Vector4(1f, 1f, 1f, alpha), TextStyles.Title2);
        Typography.DrawWrappedCentered(drawList, new Vector2(center.X, center.Y + 14f * scale),
            Loc.T(L.Games.PausedHint), new Vector4(1f, 1f, 1f, 0.7f * alpha), TextStyles.Subheadline,
            MathF.Min(body.Width - 48f * scale, 260f * scale));
    }

    private void OpenGame(IMiniGame game, bool animate = true)
    {
        currentGame = game;
        game.Open();
        coinSessions.GameOpened(game.Id);
        stats.MarkPlayed(game.Id);
        if (game.WantsLandscape)
        {
            AppLandscape.Request(Id);
        }

        router.Push(GamesRoute.Playing, animate);
    }

    private void CloseCurrentGame()
    {
        AppLandscape.Release(Id);
        if (currentGame is null)
        {
            return;
        }

        currentGame.Close();
        currentGame = null;
        coinSessions.GameClosed();
        library.Rebuild();
    }
}
