using Aetherphone.Apps.Recruit;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Media;
using Aetherphone.Core.Lodestone;
using System.Globalization;

namespace Aetherphone.Core.Recruit;

internal sealed class RecruitStore : IDisposable
{
    private readonly AethernetSession session;
    private readonly RealtimeSignalBus signals;
    private readonly List<RecruitListing> listings = new();
    private readonly List<PartyFinderListing> pfListings = new();
    private readonly RecruitClient client;
    private readonly object lockObj = new();
    private const int PfExtrasLookupBatchLimit = 100;
    private const long PfExtrasRecheckMilliseconds = 5 * 60 * 1000;
    private const bool PfExtrasBackendEnabled = false;
    private readonly Dictionary<ulong, long> pfExtrasCheckedAt = new();
    private volatile Dictionary<ulong, PfExtras> pfExtras = new();
    private volatile bool pfExtrasLookupInFlight;
    private string? lastAccountId;
    public bool IsSignedIn => session.IsSignedIn;
    public UserDto? CurrentUser => session.CurrentUser;
    public IReadOnlyList<PartyFinderListing> PartyFinderListings => pfListings;
    public event Action? OnPartyFinderUpdated;

    public RecruitStore(AethernetSession session, RecruitClient client, RealtimeSignalBus signals)
    {
        this.session = session;
        this.client = client;
        this.signals = signals;

        listings.AddRange(RecruitListing.CreateSampleListings());
        PartyFinderReader.Initialize();
        PartyFinderReader.OnListingsUpdate += HandlePartyFinderUpdated;
        session.Changed += OnSessionChanged;

    }

    public IReadOnlyList<RecruitListing> Listings
    {
        get
        {
            lock (lockObj)
            {
                return listings.ToArray();
            }
        }
    }

    private void HandlePartyFinderUpdated()
    {
        lock (lockObj){
            PartyFinderReader.Read(pfListings);
        }
        RequestMissingPfExtras();
        OnPartyFinderUpdated?.Invoke();
    }

    public void RefreshPartyFinder(bool force = false)
    {
        lock (lockObj){
            pfListings.RemoveAll(listing => (DateTime.UtcNow - listing.ReadAt).TotalMinutes > 15);
        }

        PartyFinderReader.TriggerSilentRefresh(force);
    }

    public void RemovePartyFinderListing(ulong listingId)
    {
        pfListings.RemoveAll(l => l.ListingId == listingId);
    }

    public void Add(RecruitListing listing)
    {
        lock (lockObj)
        {
            listings.Insert(0, listing);
        }

        if (!session.IsSignedIn)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try{
                var roleIds = new int[listing.RolesNeeded.Count];
                for (var i = 0; i < listing.RolesNeeded.Count; i++){
                    roleIds[i] = (int)listing.RolesNeeded[i];
                }
                var request = new CreateRecruitRequest(
                    listing.Title,
                    listing.Description,
                    listing.FormattedSchedule,
                    (int)listing.Kind,
                    listing.Duty.Name,
                    (int)listing.Duty.Category,
                    (int)listing.SelectedDays,
                    listing.StartMinuteOfDay,
                    listing.EndMinuteOfDay,
                    (int)listing.Timezone,
                    roleIds,
                    listing.AuthorName,
                    listing.WorldDc
                );

                await client.CreateAsync(request, CancellationToken.None);
            }
            catch (Exception ex){
                AepLog.Warning($"[RecruitStore] Failed to post listing to backend: {ex.Message}");
            }
        });
    }

    public void Refresh(bool force = false)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        _ = Task.Run(async () =>{
            try {
                var remote = await client.ListAsync(CancellationToken.None);
                if (remote is not null){
                    lock (lockObj){
                        listings.Clear();
                        for (var index = 0; index < remote.Length; index++){
                            var dto = remote[index];

                            DutyInfo? duty = null;
                            for (var dutyIndex = 0; dutyIndex < RecruitCatalog.Duties.Count; dutyIndex++){
                                if (RecruitCatalog.Duties[dutyIndex].Name == dto.DutyName){
                                    duty = RecruitCatalog.Duties[dutyIndex];
                                    break;
                                }
                            }
                            duty ??= new DutyInfo(dto.DutyName, (ContentCategory)dto.Category);

                            var roles = new List<RaidRole>();
                            for (var roleIndex = 0; roleIndex < dto.RolesNeeded.Length; roleIndex++){
                                roles.Add((RaidRole)dto.RolesNeeded[roleIndex]);
                            }
                            listings.Add(new RecruitListing(
                                dto.Id,
                                dto.Title,
                                dto.Description,
                                (ListingKind)dto.Kind,
                                duty,
                                (StaticCategory)dto.Category,
                                (RaidDays)dto.Days,
                                dto.StartMinuteOfDay,
                                dto.EndMinuteOfDay,
                                (RaidTimezone)dto.Timezone,
                                roles,
                                new List<string>(),
                                dto.AuthorName,
                                dto.World,
                                DateTimeOffset.FromUnixTimeSeconds(dto.CreatedAtUnix).UtcDateTime
                            ));
                        }
                    }
                }
            }
            catch (Exception ex) {
                AepLog.Warning($"[RecruitStore] Failed to sync listings: {ex.Message}");
            }
        });
    }

    public void Remove(RecruitListing listing)
    {
        lock (lockObj)
        {
            listings.Remove(listing);
        }
    }

    public PfExtras? PfExtrasFor(ulong hostContentId)
    {
        return pfExtras.TryGetValue(hostContentId, out var extras) ? extras : null;
    }

    public void PublishPfExtras(ulong contentId, uint dutyId, string? stratUrl, string? notes)
    {
        if (!PfExtrasBackendEnabled || !session.IsSignedIn || contentId == 0)
        {
            return;
        }

        var next = new Dictionary<ulong, PfExtras>(pfExtras);
        if (stratUrl is null && notes is null)
        {
            next.Remove(contentId);
        }
        else
        {
            next[contentId] = new PfExtras(stratUrl, notes);
        }

        pfExtras = next;
        var key = contentId.ToString(CultureInfo.InvariantCulture);
        _ = Task.Run(async () =>
        {
            try
            {
                if (stratUrl is null && notes is null)
                {
                    await client.ClearPfExtrasAsync(key, CancellationToken.None).ConfigureAwait(false);
                    return;
                }

                var request = new PublishPfExtrasRequest(key, dutyId, stratUrl, notes);
                await client.PublishPfExtrasAsync(request, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[RecruitStore] Failed to publish PF extras: {exception.Message}");
            }
        });
    }

    private void RequestMissingPfExtras()
    {
        if (!PfExtrasBackendEnabled || !session.IsSignedIn || pfExtrasLookupInFlight)
        {
            return;
        }

        var now = Environment.TickCount64;
        var pendingIds = new List<ulong>();
        lock (lockObj)
        {
            for (var index = 0; index < pfListings.Count && pendingIds.Count < PfExtrasLookupBatchLimit; index++)
            {
                var contentId = pfListings[index].HostContentId;
                if (contentId == 0)
                {
                    continue;
                }

                if (pfExtrasCheckedAt.TryGetValue(contentId, out var checkedAt) && now - checkedAt < PfExtrasRecheckMilliseconds)
                {
                    continue;
                }

                pfExtrasCheckedAt[contentId] = now;
                pendingIds.Add(contentId);
            }
        }

        if (pendingIds.Count == 0)
        {
            return;
        }

        var keys = new string[pendingIds.Count];
        for (var index = 0; index < pendingIds.Count; index++)
        {
            keys[index] = pendingIds[index].ToString(CultureInfo.InvariantCulture);
        }

        pfExtrasLookupInFlight = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var found = await client.LookupPfExtrasAsync(new PfExtrasLookupRequest(keys), CancellationToken.None)
                    .ConfigureAwait(false);
                if (found is not null)
                {
                    MergePfExtras(pendingIds, found);
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[RecruitStore] Failed to look up PF extras: {exception.Message}");
            }
            finally
            {
                pfExtrasLookupInFlight = false;
            }
        });
    }

    private void MergePfExtras(List<ulong> requestedIds, PfExtrasDto[] found)
    {
        var next = new Dictionary<ulong, PfExtras>(pfExtras);
        for (var index = 0; index < requestedIds.Count; index++)
        {
            next.Remove(requestedIds[index]);
        }

        for (var index = 0; index < found.Length; index++)
        {
            var dto = found[index];
            if (!ulong.TryParse(dto.ContentId, NumberStyles.None, CultureInfo.InvariantCulture, out var contentId))
            {
                continue;
            }

            var stratUrl = dto.StratUrl is not null && StratLinks.TryNormalizeWtfdig(dto.StratUrl, out var normalized)
                ? normalized
                : null;
            var notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes;
            if (stratUrl is null && notes is null)
            {
                continue;
            }

            next[contentId] = new PfExtras(stratUrl, notes);
        }

        pfExtras = next;
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        pfExtras = new Dictionary<ulong, PfExtras>();
        lock (lockObj)
        {
            pfExtrasCheckedAt.Clear();
        }
    }


    public void Dispose()
    {
        PartyFinderReader.OnListingsUpdate -= HandlePartyFinderUpdated;
        session.Changed -= OnSessionChanged;
        PartyFinderReader.Dispose();
    }
}