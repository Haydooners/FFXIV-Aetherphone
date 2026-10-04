using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Coins;

internal sealed record CoinQuestClaim(string QuestId, CoinAwardDto Award);

internal sealed class CoinQuestStore : IDisposable
{
    private const int NotFoundStatus = 404;
    private const long FailureBackoffMilliseconds = 60_000;
    private const long MissingBackoffMilliseconds = 15 * 60_000;
    private const long RolloverGraceSeconds = 5;
    private const long MinimumIntervalMilliseconds = 15_000;

    private readonly AethernetSession session;
    private readonly CoinsClient coins;
    private readonly CoinStore wallet;
    private readonly StoreWork work = new("CoinQuests");

    private volatile CoinQuestBoardDto? board;
    private volatile string? claimingId;
    private volatile bool watching;
    private CoinQuestClaim? claimResult;
    private long blockedUntilTick;
    private long loadedAtTick;
    private long rolloverRequestedFor;
    private int fetching;
    private int generation;
    private string? lastAccountId;

    public CoinQuestStore(AethernetSession session, CoinsClient coins, CoinStore wallet)
    {
        this.session = session;
        this.coins = coins;
        this.wallet = wallet;
        lastAccountId = session.CurrentUser?.Id;
        session.Changed += OnSessionChanged;
        wallet.WalletRefreshed += OnWalletRefreshed;
    }

    public CoinQuestBoardDto? Board => board;

    public bool IsClaiming(string questId) => string.Equals(claimingId, questId, StringComparison.Ordinal);

    public bool AnyClaiming => claimingId is not null;

    public void Watch()
    {
        watching = true;
        Refresh(false);
    }

    public void Unwatch()
    {
        watching = false;
    }

    public void EnsureCurrent()
    {
        var current = board;
        if (current is null || current.ResetsAtUnix <= 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now < current.ResetsAtUnix + RolloverGraceSeconds
            || Interlocked.Read(ref rolloverRequestedFor) == current.ResetsAtUnix)
        {
            return;
        }

        Interlocked.Exchange(ref rolloverRequestedFor, current.ResetsAtUnix);
        Refresh(true);
    }

    public CoinQuestClaim? TakeClaimResult()
    {
        return Interlocked.Exchange(ref claimResult, null);
    }

    public void Claim(string questId)
    {
        if (claimingId is not null || !session.IsSignedIn)
        {
            return;
        }

        claimingId = questId;
        var ticket = Volatile.Read(ref generation);
        work.Run("claim", async token =>
        {
            var award = await coins.ClaimQuestAsync(questId, token).ConfigureAwait(false);
            if (award is null || Volatile.Read(ref generation) != ticket)
            {
                return;
            }

            Interlocked.Exchange(ref claimResult, new CoinQuestClaim(questId, award));
            if (!award.Granted)
            {
                Refresh(true);
                return;
            }

            var current = board;
            if (current is not null)
            {
                board = CoinQuests.WithClaimed(current, questId);
            }

            wallet.AbsorbLocalAward(award.Balance);
        }, () => claimingId = null);
    }

    private void OnWalletRefreshed()
    {
        if (!watching)
        {
            return;
        }

        Refresh(false);
    }

    private void Refresh(bool force)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now < Interlocked.Read(ref blockedUntilTick))
        {
            return;
        }

        var loadedAt = Interlocked.Read(ref loadedAtTick);
        if (!force && loadedAt != 0 && now - loadedAt < MinimumIntervalMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref fetching, 1) != 0)
        {
            return;
        }

        var ticket = Volatile.Read(ref generation);
        work.Run("board", async token =>
        {
            var status = 0;
            var fresh = await coins.QuestsAsync(token, failure => status = failure.StatusCode)
                .ConfigureAwait(false);
            if (Volatile.Read(ref generation) != ticket)
            {
                return;
            }

            if (fresh is null)
            {
                Backoff(status);
                return;
            }

            Interlocked.Exchange(ref blockedUntilTick, 0);
            Interlocked.Exchange(ref loadedAtTick, Environment.TickCount64);
            board = fresh.Quests is null ? null : fresh;
        }, () => Interlocked.Exchange(ref fetching, 0));
    }

    private void Backoff(int status)
    {
        var missing = status == NotFoundStatus;
        if (missing)
        {
            board = null;
        }

        var delay = missing ? MissingBackoffMilliseconds : FailureBackoffMilliseconds;
        Interlocked.Exchange(ref blockedUntilTick, Environment.TickCount64 + delay);
        Interlocked.Exchange(ref rolloverRequestedFor, 0);
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        Interlocked.Increment(ref generation);
        board = null;
        claimingId = null;
        Interlocked.Exchange(ref claimResult, null);
        Interlocked.Exchange(ref blockedUntilTick, 0);
        Interlocked.Exchange(ref rolloverRequestedFor, 0);
        Interlocked.Exchange(ref loadedAtTick, 0);
        if (watching)
        {
            Refresh(true);
        }
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        wallet.WalletRefreshed -= OnWalletRefreshed;
        work.Dispose();
    }
}
