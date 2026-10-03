using System.Collections.Concurrent;
using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Core.Media;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Photos;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Widgets;

internal sealed class PhotosWidget : IHomeWidget
{
    private const float ListRefreshSeconds = 20f;
    private const float SlideSeconds = 12f;
    private const float FadeSeconds = 0.8f;

    private readonly PhotoLibrary library;
    private readonly ConcurrentDictionary<string, IDalamudTextureWrap> ready = new();
    private readonly ConcurrentDictionary<string, byte> loading = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();
    private readonly CancellationTokenSource cancellation = new();
    private string[] paths = Array.Empty<string>();
    private float sinceListRefresh = ListRefreshSeconds;
    private float sinceSlide;
    private float fade = 1f;
    private int index;
    private int previousIndex = -1;

    public PhotosWidget(PhotoLibrary library)
    {
        this.library = library;
    }

    public string Id => "photos.shuffle";
    public string DisplayName => Loc.T(L.Apps.Photos);
    public string Description => Loc.T(L.Widgets.PhotosDescription);
    public string AppId => "photos";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public void Draw(in WidgetContext context)
    {
        Advance(context.Delta);
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        if (paths.Length == 0)
        {
            DrawEmpty(context, ink);
            return;
        }

        var radius = WidgetChrome.Radius(context.Scale) * 0.95f;
        if (previousIndex >= 0 && fade < 1f && previousIndex < paths.Length)
        {
            DrawPhoto(context, paths[previousIndex], radius, ink.ImageTint);
        }

        if (index < paths.Length)
        {
            var tint = ink.ImageTint;
            DrawPhoto(context, paths[index], radius, tint with { W = tint.W * (previousIndex >= 0 ? fade : 1f) });
        }

        WidgetChrome.Edge(context);
    }

    private void Advance(float delta)
    {
        sinceListRefresh += delta;
        if (sinceListRefresh >= ListRefreshSeconds || paths.Length == 0 && sinceListRefresh >= 5f)
        {
            sinceListRefresh = 0f;
            paths = library.List();
            if (index >= paths.Length)
            {
                index = 0;
                previousIndex = -1;
            }
        }

        if (paths.Length < 2)
        {
            return;
        }

        sinceSlide += delta;
        if (fade < 1f)
        {
            fade = MathF.Min(1f, fade + delta / FadeSeconds);
        }

        if (sinceSlide < SlideSeconds)
        {
            return;
        }

        sinceSlide = 0f;
        previousIndex = index;
        index = (index + 1) % paths.Length;
        fade = 0f;
        EvictDistant();
    }

    private void DrawPhoto(in WidgetContext context, string path, float radius, Vector4 tint)
    {
        if (Get(path) is not { } wrap)
        {
            return;
        }

        var bounds = context.Bounds;
        var (uv0, uv1) = ImageFit.Cover(wrap.Width, wrap.Height, bounds.Width, bounds.Height);
        context.DrawList.AddImageRounded(wrap.Handle, bounds.Min, bounds.Max, uv0, uv1, ImGui.GetColorU32(tint), radius,
            ImDrawFlags.RoundCornersAll);
    }

    private static void DrawEmpty(in WidgetContext context, in WidgetInk ink)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        AppIconTile.TryDrawGlyph(context.DrawList, "photos", bounds.Center - new Vector2(0f, 8f * scale),
            34f * scale * AppIconTextures.GlyphFraction, ink.Secondary);
        Typography.DrawCentered(context.DrawList, new Vector2(bounds.Center.X, bounds.Center.Y + 20f * scale),
            Loc.T(L.Photos.NoPhotos), ink.Secondary, WidgetType.Caption);
    }

    private IDalamudTextureWrap? Get(string path)
    {
        if (ready.TryGetValue(path, out var wrap))
        {
            return wrap;
        }

        if (failed.ContainsKey(path) || !loading.TryAdd(path, 0))
        {
            return null;
        }

        _ = LoadAsync(path);
        return null;
    }

    private async Task LoadAsync(string path)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
            var wrap = await ImageProcessor.DecodeToTextureAsync(Plugin.TextureProvider, bytes, path,
                ImageProcessor.MaxLocalDecodePixels, token).ConfigureAwait(false);
            if (!ready.TryAdd(path, wrap))
            {
                wrap.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            failed.TryAdd(path, 0);
            AepLog.Warning(exception, $"[PhotosWidget] failed to load {path}");
        }
        finally
        {
            loading.TryRemove(path, out _);
        }
    }

    private void EvictDistant()
    {
        if (ready.Count <= 4 || paths.Length == 0)
        {
            return;
        }

        var keep = new HashSet<string>(4)
        {
            paths[index],
            paths[(index + 1) % paths.Length],
        };
        if (previousIndex >= 0 && previousIndex < paths.Length)
        {
            keep.Add(paths[previousIndex]);
        }

        foreach (var pair in ready)
        {
            if (!keep.Contains(pair.Key) && ready.TryRemove(pair.Key, out var wrap))
            {
                wrap.Dispose();
            }
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
        foreach (var pair in ready)
        {
            pair.Value.Dispose();
        }

        ready.Clear();
    }
}
