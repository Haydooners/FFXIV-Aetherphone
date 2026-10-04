namespace Aetherphone.Core.Honorific;

[Flags]
internal enum NameplateStatus
{
    None = 0,
    MogCast = 1 << 0,
    Jam = 1 << 1,
    RadioOnAir = 1 << 2,
    Muster = 1 << 3,
    SocialApps = 1 << 4,
    Velvet = 1 << 5,
    Busy = 1 << 6,
    NowPlaying = 1 << 7,
    Handle = 1 << 8,
    Default = MogCast | Jam | RadioOnAir | Muster,
}

internal enum NameplateTitleStyle
{
    AppColors,
    MatchMine,
    Custom,
}

internal enum NameplateHandleApp
{
    Chirper,
    Aethergram,
}

internal sealed class NameplateTitleSettings
{
    public bool Enabled { get; set; }
    public NameplateStatus Statuses { get; set; } = NameplateStatus.Default;
    public NameplateTitleStyle Style { get; set; }
    public int CustomColor { get; set; }
    public int CustomGlow { get; set; } = NameplatePalette.DarkGlowIndex;
    public bool Prefix { get; set; }
    public bool JamShowsName { get; set; }
    public bool IncludePcMedia { get; set; }
    public NameplateHandleApp HandleApp { get; set; }

    public bool Shows(NameplateStatus status) => (Statuses & status) != 0;

    public void Set(NameplateStatus status, bool shown) =>
        Statuses = shown ? Statuses | status : Statuses & ~status;
}
