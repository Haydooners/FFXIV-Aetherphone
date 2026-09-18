using Aetherphone.Apps.Recruit;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Recruit;

internal sealed class RecruitStore : IDisposable
{
    private readonly AethernetSession session;
    private readonly RealtimeSignalBus signals;
    private readonly List<RecruitListing> listings = new();
    private readonly List<PartyFinderListing> pfListings = new();
    private readonly RecruitClient client;
    private readonly object lockObj = new();
    public bool IsSignedIn => session.IsSignedIn;
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

    public void Dispose()
    {
        PartyFinderReader.OnListingsUpdate -= HandlePartyFinderUpdated;
        PartyFinderReader.Dispose();
    }
}