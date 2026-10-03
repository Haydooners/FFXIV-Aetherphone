using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;

namespace Aetherphone.Apps.Feedback;

internal enum FeedbackHistoryState : byte
{
    Unknown,
    Loading,
    Ready,
    Unsupported,
}

internal readonly record struct FeedbackSendResult(bool Succeeded, AepFailure Failure);

internal sealed class FeedbackStore : IDisposable
{
    private const int MaxImageDimension = 1600;

    private readonly AethernetSession session;
    private readonly FeedbackClient client;
    private readonly MediaClient media;
    private readonly StoreWork work = new StoreWork("Feedback");

    private volatile bool posting;
    private volatile int uploadedImages;
    private volatile int totalImages;
    private volatile MyFeedbackDto[] history = Array.Empty<MyFeedbackDto>();
    private volatile string? historyCursor;
    private volatile bool loadingMore;
    private volatile bool refreshing;
    private volatile bool everLoaded;
    private volatile FeedbackHistoryState historyState;
    private int historyRevision;
    private string? historyOwnerId;

    public FeedbackStore(AethernetSession session, FeedbackClient client, MediaClient media)
    {
        this.session = session;
        this.client = client;
        this.media = media;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public bool Posting => posting;

    public int UploadedImages => uploadedImages;

    public int TotalImages => totalImages;

    public MyFeedbackDto[] History => history;

    public int HistoryRevision => Volatile.Read(ref historyRevision);

    public FeedbackHistoryState HistoryState => historyState;

    public bool HistoryVisible =>
        historyState == FeedbackHistoryState.Ready || (historyState == FeedbackHistoryState.Loading && everLoaded);

    public bool HasMoreHistory => historyCursor is not null;

    public bool LoadingMoreHistory => loadingMore;

    public bool RefreshingHistory => refreshing;

    public void ResetHistorySupport()
    {
        if (historyState == FeedbackHistoryState.Unsupported)
        {
            historyState = FeedbackHistoryState.Unknown;
        }
    }

    public void RefreshHistory()
    {
        if (!session.IsSignedIn || refreshing)
        {
            return;
        }

        var userId = session.CurrentUser?.Id;
        if (!string.Equals(userId, historyOwnerId, StringComparison.Ordinal))
        {
            historyOwnerId = userId;
            everLoaded = false;
            history = Array.Empty<MyFeedbackDto>();
            historyCursor = null;
            historyState = FeedbackHistoryState.Unknown;
            Interlocked.Increment(ref historyRevision);
        }

        var hadItems = historyState == FeedbackHistoryState.Ready;
        refreshing = true;
        if (!hadItems)
        {
            historyState = FeedbackHistoryState.Loading;
        }

        work.Run("history", async token =>
        {
            var reported = AepFailure.None;
            var page = await client.MineAsync(null, token, failure => reported = failure).ConfigureAwait(false);
            if (page is null)
            {
                ApplyHistoryFailure(reported, hadItems);
                return;
            }

            history = page.Items ?? Array.Empty<MyFeedbackDto>();
            historyCursor = page.NextCursor;
            everLoaded = true;
            historyState = FeedbackHistoryState.Ready;
            Interlocked.Increment(ref historyRevision);
        }, () => refreshing = false);
    }

    public void LoadMoreHistory()
    {
        var cursor = historyCursor;
        if (cursor is null || loadingMore || refreshing || historyState != FeedbackHistoryState.Ready)
        {
            return;
        }

        loadingMore = true;
        work.Run("history more", async token =>
        {
            var page = await client.MineAsync(cursor, token).ConfigureAwait(false);
            if (page?.Items is null)
            {
                return;
            }

            history = IdentifiedMerge.MergeById(history, page.Items, ByNewestFirst);
            historyCursor = page.NextCursor;
            Interlocked.Increment(ref historyRevision);
        }, () => loadingMore = false);
    }

    private void ApplyHistoryFailure(AepFailure reported, bool hadItems)
    {
        if (hadItems && reported.Kind != AepFailureKind.Server)
        {
            historyState = FeedbackHistoryState.Ready;
            return;
        }

        history = Array.Empty<MyFeedbackDto>();
        historyCursor = null;
        historyState = FeedbackHistoryState.Unsupported;
        Interlocked.Increment(ref historyRevision);
    }

    private static int ByNewestFirst(MyFeedbackDto first, MyFeedbackDto second) =>
        second.CreatedAtUnix.CompareTo(first.CreatedAtUnix);

    public void Compose(string text, FeedbackCategory category, string context, string[] imagePaths,
        Action<FeedbackSendResult> onComplete)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0 || posting)
        {
            return;
        }

        posting = true;
        uploadedImages = 0;
        totalImages = imagePaths.Length;
        var reported = AepFailure.None;
        var wireCategory = FeedbackKinds.Of(category).WireName;
        work.Run("compose", async token =>
        {
            var keys = await UploadImagesAsync(imagePaths, token, failure => reported = failure).ConfigureAwait(false);
            if (keys is null)
            {
                return false;
            }

            var created = await client.CreateAsync(trimmed, keys, wireCategory, context, token,
                failure => reported = failure).ConfigureAwait(false);
            return created is not null;
        }, succeeded =>
        {
            var failure = succeeded || reported.Failed ? reported : AepFailure.Transport(AepFailureKind.Offline);
            if (!succeeded)
            {
                AepLog.Warning($"Feedback failed to send: {failure.Describe()}");
            }

            onComplete(new FeedbackSendResult(succeeded, succeeded ? AepFailure.None : failure));
        }, () => posting = false);
    }

    private async Task<string[]?> UploadImagesAsync(string[] imagePaths, CancellationToken token,
        Action<AepFailure> onFailure)
    {
        if (imagePaths.Length == 0)
        {
            return Array.Empty<string>();
        }

        var keys = new string[imagePaths.Length];
        for (var index = 0; index < imagePaths.Length; index++)
        {
            var baked = ImageProcessor.BakeJpeg(imagePaths[index], MaxImageDimension);
            var upload = await media.UploadUrlAsync("image/jpeg", "feedback", token, onFailure).ConfigureAwait(false);
            if (upload is null)
            {
                return null;
            }

            var uploaded = await media.UploadImageAsync(upload.UploadUrl, baked.Bytes, "image/jpeg", token, onFailure)
                .ConfigureAwait(false);
            if (!uploaded)
            {
                return null;
            }

            keys[index] = upload.Key;
            uploadedImages = index + 1;
        }

        return keys;
    }

    public void Dispose()
    {
        work.Dispose();
    }
}
