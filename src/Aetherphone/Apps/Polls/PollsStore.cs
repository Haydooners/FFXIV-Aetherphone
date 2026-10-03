using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Polls;

internal sealed class PollsStore : IDisposable
{
    // poll.ping announces every create/close/reopen, so the timer is only the fallback
    // for a dropped socket; anything shorter just re-downloads an unchanged page.
    private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromMinutes(45);

    private readonly AethernetSession session;
    private readonly PollsClient client;
    private readonly AppGate gate;
    private readonly RealtimeSignalBus signals;
    private readonly StoreWork work = new StoreWork("Polls");
    private readonly Lock voteGate = new();
    private readonly Dictionary<string, VoteTicket> tickets = new(StringComparer.Ordinal);
    private readonly FailureSlot listFailure = new();
    private readonly FailureSlot endedFailure = new();

    private volatile PollDto[] polls = Array.Empty<PollDto>();
    private volatile PollDto[] endedServer = Array.Empty<PollDto>();
    private volatile PollDto[] endedLocal = Array.Empty<PollDto>();
    private volatile PollDto[] endedView = Array.Empty<PollDto>();
    private volatile string? pollsCursor;
    private volatile string? endedCursor;
    private volatile bool loadingMore;
    private volatile bool pagedDeeper;
    private volatile bool loading;
    private volatile bool loadedOnce;
    private volatile bool listFailed;
    private volatile bool endedLoading;
    private volatile bool endedLoadingMore;
    private volatile bool endedLoadedOnce;
    private volatile bool endedFailed;
    private volatile bool endedStale = true;
    private volatile bool endedSupported = true;
    private volatile bool pingRefreshRequested;
    private volatile PollVoteFailure? voteFailure;
    private int voteSequence;
    private DateTime lastBackgroundRefreshUtc = DateTime.MinValue;

    public PollsStore(AethernetSession session, PollsClient client, AppGate gate, RealtimeSignalBus signals)
    {
        this.session = session;
        this.client = client;
        this.gate = gate;
        this.signals = signals;
        signals.PollsPinged += OnPollsPinged;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    private void OnPollsPinged()
    {
        pingRefreshRequested = true;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public PollDto[] Polls => polls;

    public PollDto[] Ended => endedView;

    public bool Loading => loading;

    public bool LoadingMore => loadingMore;

    public bool HasMore => pollsCursor is not null;

    public bool LoadedOnce => loadedOnce;

    public bool ListFailed => listFailed;

    public string ListFailureText => listFailure.Text();

    public bool EndedSupported => endedSupported;

    public bool EndedLoading => endedLoading;

    public bool EndedLoadingMore => endedLoadingMore;

    public bool EndedHasMore => endedCursor is not null;

    public bool EndedLoadedOnce => endedLoadedOnce;

    public bool EndedFailed => endedFailed;

    public string EndedFailureText => endedFailure.Text();

    public PollVoteFailure? VoteFailure => voteFailure;

    public int UnvotedCount
    {
        get
        {
            if (!session.IsSignedIn)
            {
                return 0;
            }

            var snapshot = polls;
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var count = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (PollRules.NeedsVote(snapshot[index], nowUnix))
                {
                    count++;
                }
            }

            return count;
        }
    }

    public void Refresh()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        endedStale = true;
        if (loading)
        {
            return;
        }

        loading = true;
        var lang = Loc.Current.Code;
        work.Run("polls refresh", async token =>
        {
            var failure = AepFailure.None;
            var page = await client.ListAsync(null, lang, token, received => failure = received)
                .ConfigureAwait(false);
            if (page is null)
            {
                listFailure.Set(failure);
                listFailed = true;
                return;
            }

            listFailure.Clear();
            listFailed = false;
            lock (voteGate)
            {
                var items = KeepPendingVotes(page.Items);
                RetireDeparted(items, page.NextCursor is null);
                if (pagedDeeper)
                {
                    polls = IdentifiedMerge.MergeById(DropDeparted(polls, items), items, PollRules.NewestFirst);
                }
                else
                {
                    polls = items;
                    pollsCursor = page.NextCursor;
                }
            }

            loadedOnce = true;
        }, () => loading = false);
    }

    public void LoadMore()
    {
        var cursor = pollsCursor;
        if (!session.IsSignedIn || cursor is null || loadingMore || loading)
        {
            return;
        }

        loadingMore = true;
        pagedDeeper = true;
        var lang = Loc.Current.Code;
        work.Run("polls more", async token =>
        {
            var page = await client.ListAsync(cursor, lang, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            lock (voteGate)
            {
                polls = IdentifiedMerge.MergeById(polls, KeepPendingVotes(page.Items), PollRules.NewestFirst);
                pollsCursor = page.NextCursor;
            }
        }, () => loadingMore = false);
    }

    public void EnsureEnded()
    {
        if (!session.IsSignedIn || !endedSupported || endedLoading || !endedStale)
        {
            return;
        }

        endedLoading = true;
        endedStale = false;
        var lang = Loc.Current.Code;
        work.Run("polls ended", async token =>
        {
            var failure = AepFailure.None;
            var page = await client.ListEndedAsync(null, lang, token, received => failure = received)
                .ConfigureAwait(false);
            if (page is null)
            {
                endedFailure.Set(failure);
                endedFailed = true;
                return;
            }

            endedFailure.Clear();
            endedFailed = false;
            if (PollRules.AnyOpen(page.Items))
            {
                endedSupported = false;
                return;
            }

            lock (voteGate)
            {
                endedServer = page.Items;
                endedCursor = page.NextCursor;
                RebuildEndedView();
            }

            endedLoadedOnce = true;
        }, () => endedLoading = false);
    }

    public void LoadMoreEnded()
    {
        var cursor = endedCursor;
        if (!session.IsSignedIn || cursor is null || endedLoadingMore || endedLoading)
        {
            return;
        }

        endedLoadingMore = true;
        var lang = Loc.Current.Code;
        work.Run("polls ended more", async token =>
        {
            var page = await client.ListEndedAsync(cursor, lang, token).ConfigureAwait(false);
            if (page is null || PollRules.AnyOpen(page.Items))
            {
                return;
            }

            lock (voteGate)
            {
                endedServer = IdentifiedMerge.MergeById(endedServer, page.Items, PollRules.CompareEnded);
                endedCursor = page.NextCursor;
                RebuildEndedView();
            }
        }, () => endedLoadingMore = false);
    }

    public bool Vote(PollDto poll, int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= poll.Options.Length || poll.MyVote == optionIndex)
        {
            return false;
        }

        return Submit(poll, optionIndex);
    }

    public bool ClearVote(PollDto poll)
    {
        if (poll.MyVote < 0)
        {
            return false;
        }

        return Submit(poll, -1);
    }

    private bool Submit(PollDto poll, int target)
    {
        if (PollRules.IsClosed(poll, DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
        {
            voteFailure = new PollVoteFailure(poll.Id, Loc.T(L.Failure.PollClosed), DateTime.UtcNow);
            return false;
        }

        int sequence;
        lock (voteGate)
        {
            var current = Find(poll.Id) ?? poll;
            var baseline = tickets.TryGetValue(poll.Id, out var pending) ? pending.Baseline : current;
            sequence = ++voteSequence;
            tickets[poll.Id] = new VoteTicket(sequence, baseline);
            polls = CopyOnWrite.Replace(polls, PollRules.ApplyVote(current, target));
        }

        ClearFailureFor(poll.Id);
        var lang = Loc.Current.Code;
        work.Run("vote", async token =>
        {
            var failure = AepFailure.None;
            var result = target < 0
                ? await client.ClearVoteAsync(poll.Id, lang, token, received => failure = received)
                    .ConfigureAwait(false)
                : await client.VoteAsync(poll.Id, target, lang, token, received => failure = received)
                    .ConfigureAwait(false);
            Settle(poll.Id, sequence, result, failure);
        });
        return true;
    }

    private void Settle(string pollId, int sequence, PollDto? result, AepFailure failure)
    {
        var closedByServer = failure.Code == FailureCodes.PollClosed;
        lock (voteGate)
        {
            if (!tickets.TryGetValue(pollId, out var ticket) || ticket.Sequence != sequence)
            {
                return;
            }

            tickets.Remove(pollId);
            if (result is not null)
            {
                polls = CopyOnWrite.Replace(polls, result);
                return;
            }

            var restored = closedByServer
                ? PollRules.MarkClosed(ticket.Baseline, DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                : ticket.Baseline;
            polls = CopyOnWrite.Replace(polls, restored);
        }

        var reason = FailureText.Resolve(failure);
        voteFailure = new PollVoteFailure(pollId, reason.Length > 0 ? reason : Loc.T(L.Polls.VoteFailed),
            DateTime.UtcNow);
        pingRefreshRequested = true;
    }

    private void ClearFailureFor(string pollId)
    {
        var current = voteFailure;
        if (current is not null && current.PollId == pollId)
        {
            voteFailure = null;
        }
    }

    private PollDto? Find(string pollId)
    {
        var snapshot = polls;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == pollId)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    private PollDto[] KeepPendingVotes(PollDto[] incoming)
    {
        if (tickets.Count == 0)
        {
            return incoming;
        }

        var merged = incoming;
        for (var index = 0; index < incoming.Length; index++)
        {
            if (!tickets.ContainsKey(incoming[index].Id) || Find(incoming[index].Id) is not { } local)
            {
                continue;
            }

            if (ReferenceEquals(merged, incoming))
            {
                merged = (PollDto[])incoming.Clone();
            }

            merged[index] = local;
        }

        return merged;
    }

    private void RetireDeparted(PollDto[] incoming, bool complete)
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var oldestIncoming = OldestCreated(incoming);
        var snapshot = polls;
        var retired = endedLocal;
        var changed = false;
        for (var index = 0; index < snapshot.Length; index++)
        {
            var poll = snapshot[index];
            if (!Departed(poll, incoming, complete, oldestIncoming) || !PollRules.IsClosed(poll, nowUnix))
            {
                continue;
            }

            retired = IdentifiedMerge.MergeById(retired, new[] { PollRules.MarkClosed(poll, nowUnix) },
                PollRules.CompareEnded);
            changed = true;
        }

        for (var index = 0; index < incoming.Length; index++)
        {
            var reopened = CopyOnWrite.RemoveById(retired, incoming[index].Id);
            changed |= !ReferenceEquals(reopened, retired);
            retired = reopened;
        }

        if (!changed)
        {
            return;
        }

        endedLocal = retired;
        RebuildEndedView();
    }

    private static PollDto[] DropDeparted(PollDto[] existing, PollDto[] incoming)
    {
        var oldestIncoming = OldestCreated(incoming);
        var kept = existing;
        for (var index = 0; index < existing.Length; index++)
        {
            if (Departed(existing[index], incoming, false, oldestIncoming))
            {
                kept = CopyOnWrite.RemoveById(kept, existing[index].Id);
            }
        }

        return kept;
    }

    private static bool Departed(PollDto poll, PollDto[] incoming, bool complete, long oldestIncoming)
    {
        for (var index = 0; index < incoming.Length; index++)
        {
            if (incoming[index].Id == poll.Id)
            {
                return false;
            }
        }

        return complete || poll.CreatedAtUnix >= oldestIncoming;
    }

    private static long OldestCreated(PollDto[] polls)
    {
        if (polls.Length == 0)
        {
            return long.MaxValue;
        }

        var oldest = long.MaxValue;
        for (var index = 0; index < polls.Length; index++)
        {
            oldest = Math.Min(oldest, polls[index].CreatedAtUnix);
        }

        return oldest;
    }

    private void RebuildEndedView()
    {
        endedView = IdentifiedMerge.MergeById(endedLocal, endedServer, PollRules.CompareEnded);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn || !gate.Open)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!pingRefreshRequested && now - lastBackgroundRefreshUtc < BackgroundRefreshInterval)
        {
            return;
        }

        pingRefreshRequested = false;
        lastBackgroundRefreshUtc = now;
        Refresh();
    }

    public void Dispose()
    {
        signals.PollsPinged -= OnPollsPinged;
        Plugin.Framework.Update -= OnFrameworkUpdate;
        work.Dispose();
    }

    private readonly record struct VoteTicket(int Sequence, PollDto Baseline);
}

internal sealed record PollVoteFailure(string PollId, string Message, DateTime AtUtc);
