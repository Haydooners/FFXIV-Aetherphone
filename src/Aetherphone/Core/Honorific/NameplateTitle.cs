namespace Aetherphone.Core.Honorific;

internal enum NameplateTitleKind : byte
{
    None,
    MogCast,
    Jam,
    RadioOnAir,
    Muster,
    AppTag,
    Busy,
    NowPlaying,
    Handle,
}

internal readonly record struct TitleLook(
    Vector3 Color,
    Vector3? Glow,
    Vector3? Color3,
    int? GradientColourSet,
    int? GradientAnimationStyle)
{
    public static TitleLook Solid(Vector3 color, Vector3 glow) => new(color, glow, null, null, null);
}

internal readonly record struct NameplateTitle(NameplateTitleKind Kind, string Text, TitleLook Look, bool Prefix)
{
    public static readonly NameplateTitle None = new(NameplateTitleKind.None, string.Empty, default, false);

    public bool IsNone => Kind == NameplateTitleKind.None;
}
