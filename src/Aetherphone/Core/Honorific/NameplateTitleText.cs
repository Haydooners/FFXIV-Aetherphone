using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Aetherphone.Core.Honorific;

internal static class NameplateTitleText
{
    public const int MaxLength = 32;
    public const string Separator = " · ";
    public const string NowPlayingMark = "♪ ";
    private const char Ellipsis = '…';
    private const char HandleMark = '@';

    public static string Fit(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length <= MaxLength)
        {
            return trimmed;
        }

        return string.Concat(trimmed.AsSpan(0, MaxLength - 1).TrimEnd(), Ellipsis.ToString());
    }

    public static string Pair(string lead, string detail)
    {
        var cleanDetail = detail.Trim();
        return cleanDetail.Length == 0 ? Fit(lead) : Fit(string.Concat(lead, Separator, cleanDetail));
    }

    public static string NowPlaying(string title, string artist)
    {
        var cleanTitle = title.Trim();
        var cleanArtist = artist.Trim();
        if (cleanTitle.Length == 0)
        {
            return string.Empty;
        }

        if (cleanArtist.Length > 0)
        {
            var full = string.Concat(NowPlayingMark, cleanTitle, Separator, cleanArtist);
            if (full.Length <= MaxLength)
            {
                return full;
            }
        }

        return Fit(string.Concat(NowPlayingMark, cleanTitle));
    }

    public static string Handle(string handle)
    {
        var clean = handle.Trim().TrimStart(HandleMark);
        return clean.Length == 0 ? string.Empty : Fit(string.Concat(HandleMark.ToString(), clean));
    }

    public static string AppTag(string appName, string handle)
    {
        var clean = handle.Trim().TrimStart(HandleMark);
        return clean.Length == 0 ? string.Empty : Pair(appName, string.Concat(HandleMark.ToString(), clean));
    }

    public static string ToJson(in NameplateTitle title)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("Title", title.Text);
            writer.WriteBoolean("IsPrefix", title.Prefix);
            writer.WriteBoolean("IsOriginal", false);
            WriteVector(writer, "Color", title.Look.Color);
            if (title.Look.Glow is { } glow)
            {
                WriteVector(writer, "Glow", glow);
            }

            if (title.Look.Color3 is { } third)
            {
                WriteVector(writer, "Color3", third);
            }

            if (title.Look.GradientColourSet is { } colourSet)
            {
                writer.WriteNumber("GradientColourSet", colourSet);
            }

            if (title.Look.GradientAnimationStyle is { } animation)
            {
                writer.WriteNumber("GradientAnimationStyle", animation);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public static bool TryReadTitle(string json, out string title, out TitleLook look)
    {
        title = string.Empty;
        look = default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("IsOriginal", out var original) && original.ValueKind == JsonValueKind.True)
            {
                return false;
            }

            if (root.TryGetProperty("Title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String)
            {
                title = titleElement.GetString() ?? string.Empty;
            }

            var color = ReadVector(root, "Color") ?? NameplatePalette.Colors[0];
            look = new TitleLook(color, ReadVector(root, "Glow"), ReadVector(root, "Color3"),
                ReadInt(root, "GradientColourSet"), ReadInt(root, "GradientAnimationStyle"));
            return title.Length > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void WriteVector(Utf8JsonWriter writer, string name, Vector3 value)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Z", value.Z);
        writer.WriteEndObject();
    }

    private static Vector3? ReadVector(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new Vector3(ReadComponent(element, "X"), ReadComponent(element, "Y"), ReadComponent(element, "Z"));
    }

    private static float ReadComponent(JsonElement element, string name) =>
        element.TryGetProperty(name, out var component) && component.ValueKind == JsonValueKind.Number
            ? component.GetSingle()
            : 0f;

    private static int? ReadInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number &&
        element.TryGetInt32(out var value)
            ? value
            : null;
}
