using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Jam;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Radio;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Honorific;

internal sealed class NameplateTitleService : IDisposable
{
    public const string ChirperAppId = "chirper";
    public const string AethergramAppId = "aethergram";
    private const string MusicAppId = "music";
    private const string VideoAppId = "aetherstream";
    private const float EvaluateSeconds = 1f;
    private const float VerifySeconds = 5f;
    private const float ProbeSeconds = 10f;
    private const string SampleCode = "4KX9QP";

    private readonly Configuration configuration;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly AethernetSession session;
    private readonly PlaybackHub playback;
    private readonly PcMediaSource pcMedia;
    private readonly JamSession jam;
    private readonly RadioRoomRouter radioRooms;
    private readonly MusterStore musters;
    private readonly CallHub calls;
    private readonly HonorificBridge bridge;
    private readonly NameplateTitleSettings signedOutSettings = new();
    private WatchAlongSession? watchAlong;
    private Func<IPhoneApp?> foregroundApp = static () => null;
    private IReadOnlyList<IPhoneApp> apps = Array.Empty<IPhoneApp>();
    private NameplateTitleSettings settings;
    private ulong settingsContentId;
    private float evaluateTimer = EvaluateSeconds;
    private float verifyTimer;
    private float probeTimer = ProbeSeconds;
    private bool reapplyRequested;
    private bool applied;
    private string appliedJson = string.Empty;
    private string appliedText = string.Empty;
    private TitleLook? ownLook;

    public NameplateTitleService(Configuration configuration, IFramework framework, IClientState clientState,
        IObjectTable objectTable, AethernetSession session, PlaybackHub playback, PcMediaSource pcMedia,
        JamSession jam, RadioRoomRouter radioRooms, MusterStore musters, CallHub calls, HonorificBridge bridge)
    {
        this.configuration = configuration;
        this.framework = framework;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.session = session;
        this.playback = playback;
        this.pcMedia = pcMedia;
        this.jam = jam;
        this.radioRooms = radioRooms;
        this.musters = musters;
        this.calls = calls;
        this.bridge = bridge;
        settings = signedOutSettings;
        framework.Update += OnUpdate;
        clientState.TerritoryChanged += OnTerritoryChanged;
        clientState.Logout += OnLogout;
    }

    public bool Available { get; private set; }
    public NameplateTitle Current { get; private set; } = NameplateTitle.None;
    public NameplateTitle Preview { get; private set; } = NameplateTitle.None;
    public string CharacterName { get; private set; } = string.Empty;
    public NameplateTitleSettings Settings => settings;
    public bool HasOwnLook => ownLook.HasValue;

    public void Bind(WatchAlongSession watchAlongSession, Func<IPhoneApp?> foreground, IReadOnlyList<IPhoneApp> phoneApps)
    {
        watchAlong = watchAlongSession;
        foregroundApp = foreground;
        apps = phoneApps;
    }

    public void Commit()
    {
        if (settingsContentId != 0)
        {
            configuration.NameplateTitleByCharacter[settingsContentId] = settings;
        }

        configuration.Save();
        evaluateTimer = EvaluateSeconds;
        if (applied && settings.Style == NameplateTitleStyle.MatchMine && ownLook is null)
        {
            bridge.TryClearLocalTitle();
            applied = false;
            appliedJson = string.Empty;
            appliedText = string.Empty;
        }
    }

    private NameplateTitle Example() =>
        Build(NameplateTitleKind.Jam,
            NameplateTitleText.Pair(Loc.T(L.Nameplate.Jam), PartyCode.Display(SampleCode)),
            AppAccents.For(MusicAppId));

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        clientState.TerritoryChanged -= OnTerritoryChanged;
        clientState.Logout -= OnLogout;
        if (applied)
        {
            bridge.TryClearLocalTitle();
            applied = false;
        }

        bridge.Dispose();
    }

    private void OnTerritoryChanged(uint territory) => reapplyRequested = true;

    private void OnLogout(int type, int code)
    {
        applied = false;
        appliedJson = string.Empty;
        appliedText = string.Empty;
        ownLook = null;
    }

    private void OnUpdate(IFramework updatingFramework)
    {
        var delta = (float)updatingFramework.UpdateDelta.TotalSeconds;
        if (bridge.ConsumeDisposing())
        {
            Available = false;
            applied = false;
        }

        if (bridge.ConsumeReady())
        {
            probeTimer = ProbeSeconds;
            reapplyRequested = true;
        }

        probeTimer += delta;
        if (probeTimer >= ProbeSeconds)
        {
            probeTimer = 0f;
            Available = bridge.Probe();
        }

        evaluateTimer += delta;
        if (evaluateTimer < EvaluateSeconds && !reapplyRequested)
        {
            return;
        }

        evaluateTimer = 0f;
        ResolveSettings();
        var player = objectTable.LocalPlayer;
        if (player is null)
        {
            Current = NameplateTitle.None;
            Preview = Example();
            return;
        }

        if (CharacterName.Length == 0 || !player.Name.TextValue.Equals(CharacterName, StringComparison.Ordinal))
        {
            CharacterName = player.Name.TextValue;
        }

        if (Available && !applied)
        {
            CaptureOwnLook();
        }

        Current = settings.Enabled ? Resolve() : NameplateTitle.None;
        Preview = Current.IsNone ? Example() : Current;
        if (!Available)
        {
            return;
        }

        verifyTimer += EvaluateSeconds;
        Apply(Current);
    }

    private void ResolveSettings()
    {
        var contentId = session.PlayingContentId;
        if (contentId == settingsContentId)
        {
            return;
        }

        settingsContentId = contentId;
        if (contentId == 0)
        {
            settings = signedOutSettings;
            return;
        }

        if (!configuration.NameplateTitleByCharacter.TryGetValue(contentId, out var stored))
        {
            stored = new NameplateTitleSettings();
            configuration.NameplateTitleByCharacter[contentId] = stored;
        }

        settings = stored;
        ownLook = null;
    }

    private void Apply(in NameplateTitle title)
    {
        if (title.IsNone)
        {
            if (applied)
            {
                bridge.TryClearLocalTitle();
                applied = false;
                appliedJson = string.Empty;
                appliedText = string.Empty;
            }

            reapplyRequested = false;
            return;
        }

        var json = NameplateTitleText.ToJson(title);
        var changed = !string.Equals(json, appliedJson, StringComparison.Ordinal);
        if (!changed && !reapplyRequested && (!NeedsVerify() || StillShowing()))
        {
            return;
        }

        if (bridge.TrySetLocalTitle(json))
        {
            applied = true;
            appliedJson = json;
            appliedText = title.Text;
        }

        reapplyRequested = false;
    }

    private bool NeedsVerify()
    {
        if (verifyTimer < VerifySeconds)
        {
            return false;
        }

        verifyTimer = 0f;
        return true;
    }

    private bool StillShowing() =>
        bridge.TryGetLocalTitle(out var json) && NameplateTitleText.TryReadTitle(json, out var shown, out _) &&
        string.Equals(shown, appliedText, StringComparison.Ordinal);

    private void CaptureOwnLook()
    {
        if (applied || settings.Style != NameplateTitleStyle.MatchMine)
        {
            return;
        }

        if (!bridge.TryGetLocalTitle(out var json))
        {
            return;
        }

        ownLook = NameplateTitleText.TryReadTitle(json, out _, out var look) ? look : null;
    }

    private NameplateTitle Resolve()
    {
        if (settings.Shows(NameplateStatus.MogCast) && watchAlong is { IsHosting: true, IsPartyOpen: true } party &&
            party.RoomCode.Length > 0)
        {
            return Build(NameplateTitleKind.MogCast,
                NameplateTitleText.Pair(Loc.T(L.Apps.AetherStream), PartyCode.Display(party.RoomCode)),
                AppAccents.For(VideoAppId));
        }

        if (settings.Shows(NameplateStatus.Jam) && jam.IsHost)
        {
            var detail = settings.JamShowsName && jam.Title.Length > 0 ? jam.Title : jam.DisplayCode;
            if (detail.Length > 0)
            {
                return Build(NameplateTitleKind.Jam, NameplateTitleText.Pair(Loc.T(L.Nameplate.Jam), detail),
                    AppAccents.For(MusicAppId));
            }
        }

        var room = radioRooms.Room;
        if (settings.Shows(NameplateStatus.RadioOnAir) && room.IsDj && room.IsLive)
        {
            var station = playback.RadioActive ? playback.Radio.CurrentStation : string.Empty;
            return Build(NameplateTitleKind.RadioOnAir,
                NameplateTitleText.Pair(Loc.T(L.Nameplate.OnAir), station), AccentRing.Orange);
        }

        if (settings.Shows(NameplateStatus.Muster) && HostedMusterLive() is { } muster)
        {
            return Build(NameplateTitleKind.Muster,
                NameplateTitleText.Pair(Loc.T(L.Nameplate.Hosting),
                    Loc.T(MusterCategories.Label(muster.Category))),
                AppAccents.For(MusterStore.AppId));
        }

        var appTag = ResolveAppTag();
        if (!appTag.IsNone)
        {
            return appTag;
        }

        if (settings.Shows(NameplateStatus.Busy))
        {
            if (calls.Snapshot().InCall)
            {
                return Build(NameplateTitleKind.Busy, Loc.T(L.Nameplate.OnACall), AccentRing.Slate);
            }

            if (configuration.DoNotDisturb)
            {
                return Build(NameplateTitleKind.Busy, Loc.T(L.Nameplate.DoNotDisturb), AccentRing.Slate);
            }
        }

        if (settings.Shows(NameplateStatus.NowPlaying) && configuration.ShareListeningActivity)
        {
            var song = ResolveNowPlaying();
            if (song.Length > 0)
            {
                return Build(NameplateTitleKind.NowPlaying, song, AppAccents.For(MusicAppId));
            }
        }

        if (settings.Shows(NameplateStatus.Handle))
        {
            var appId = settings.HandleApp == NameplateHandleApp.Aethergram ? AethergramAppId : ChirperAppId;
            var handle = NameplateTitleText.Handle(ResolveHandle(appId));
            if (handle.Length > 0)
            {
                return Build(NameplateTitleKind.Handle, handle, AppAccents.For(appId));
            }
        }

        return NameplateTitle.None;
    }

    private MusterDto? HostedMusterLive()
    {
        var mine = musters.Mine;
        var userId = session.CurrentUser?.Id;
        if (mine is null || userId is null || !string.Equals(mine.HostId, userId, StringComparison.Ordinal))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return now >= mine.StartsAtUnix && now < mine.EndsAtUnix ? mine : null;
    }

    private NameplateTitle ResolveAppTag()
    {
        var app = foregroundApp();
        if (app is not INameplateHandleSource source || !settings.Shows(source.TagStatus))
        {
            return NameplateTitle.None;
        }

        var text = NameplateTitleText.AppTag(app.DisplayName, source.ResolveNameplateHandle());
        return text.Length == 0 ? NameplateTitle.None : Build(NameplateTitleKind.AppTag, text, app.Accent);
    }

    private string ResolveNowPlaying()
    {
        if (playback.IsPlaying)
        {
            if (playback.SongActive)
            {
                return NameplateTitleText.NowPlaying(playback.Title, playback.Subtitle);
            }

            var track = playback.RadioNowPlaying;
            return NameplateTitleText.NowPlaying(track.Length > 0 ? track : playback.Title, string.Empty);
        }

        if (!settings.IncludePcMedia)
        {
            return string.Empty;
        }

        ref readonly var media = ref pcMedia.Current;
        return media.IsPlaying ? NameplateTitleText.NowPlaying(media.Title, media.Artist) : string.Empty;
    }

    private string ResolveHandle(string appId)
    {
        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index] is INameplateHandleSource source &&
                string.Equals(apps[index].Id, appId, StringComparison.Ordinal))
            {
                var handle = source.ResolveNameplateHandle();
                if (handle.Length > 0)
                {
                    return handle;
                }
            }
        }

        return session.CurrentUser?.Handle ?? string.Empty;
    }

    private NameplateTitle Build(NameplateTitleKind kind, string text, Vector4 accent)
    {
        if (text.Length == 0)
        {
            return NameplateTitle.None;
        }

        return new NameplateTitle(kind, text, LookFor(accent), settings.Prefix);
    }

    private TitleLook LookFor(Vector4 accent)
    {
        switch (settings.Style)
        {
            case NameplateTitleStyle.MatchMine when ownLook is { } own:
                return own;
            case NameplateTitleStyle.Custom:
                return TitleLook.Solid(NameplatePalette.At(settings.CustomColor),
                    NameplatePalette.At(settings.CustomGlow));
            default:
                return TitleLook.Solid(new Vector3(accent.X, accent.Y, accent.Z), NameplatePalette.DarkGlow);
        }
    }
}
