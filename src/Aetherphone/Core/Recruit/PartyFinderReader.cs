using System;
using System.Collections.Generic;
using System.Text;
using Aetherphone.Apps.Recruit;
using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.GameChat;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Recruit;

internal static unsafe class PartyFinderReader
{
    public static event System.Action? OnListingsUpdate;
    private static readonly List<PartyFinderListing> cachedListings = new();
    private static bool openedForSync;
    private static bool closeScheduled;
    private static bool syncInProgress;
    private static int lastListingCount;
    private static int syncedPagesCount = 1;
    private static DateTime lastListingReceivedTime = DateTime.MinValue;
    private static DateTime lastRefreshRequest = DateTime.MinValue;
    private static readonly TimeSpan RefreshCooldown = TimeSpan.FromSeconds(5);
    private static bool initialized;
    public static int TotalListingsCount { get; private set; } = 50;
    public static int CurrentPageNumber { get; private set; } = 1;

    public static void Initialize()
    {
        if (initialized){
            return;
        }

        Plugin.PartyFinderGui.ReceiveListing += OnReceiveListing;

        initialized = true;

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "LookingForGroup", OnPostUpdate);
    Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostClose, "LookingForGroup", OnPostClose);
    }

    public static void Dispose()
    {
        if (!initialized)
        {
            return;
        }

        Plugin.PartyFinderGui.ReceiveListing -= OnReceiveListing;

        initialized = false;

        Plugin.AddonLifecycle.UnregisterListener(OnPostUpdate, OnPostClose);
    }

    private static void OnReceiveListing(IPartyFinderListing listing, IPartyFinderListingEventArgs args)
    {
        lastListingReceivedTime = DateTime.UtcNow;

        var dutyName = listing.Duty.ValueNullable?.Name.ExtractText();
        if (string.IsNullOrWhiteSpace(dutyName))
        {
            dutyName = listing.Category.ToString();
        }

        var worldName = listing.HomeWorld.ValueNullable?.Name.ExtractText() ?? string.Empty;
        var leaderName = listing.Name.TextValue;
        var comment = listing.Description.TextValue;
        var totalSlots = (byte)(listing.SlotsFilled + listing.SlotsAvailable);
        var slots = BuildSlotsList(listing);

        var item = new PartyFinderListing(
            listing.Id,
            dutyName,
            listing.Category.ToString(),
            comment,
            string.IsNullOrWhiteSpace(leaderName) ? "Party Leader" : leaderName,
            string.IsNullOrWhiteSpace(worldName) ? "Datacenter" : worldName,
            listing.SlotsFilled,
            totalSlots,
            listing.MinimumItemLevel,
            DateTime.UtcNow,
            slots,
            listing.HomeWorld.RowId,
            listing.Objective,
            listing.Conditions,
            listing.DutyFinderSettings,
            BuildOpenAcceptedJobs(listing)
        );

        lock (cachedListings)
        {
            var idx = cachedListings.FindIndex(l => l.ListingId == listing.Id);
            if (idx >= 0)
            {
                cachedListings[idx] = item;
            }
            else
            {
                cachedListings.Add(item);
            }
            if (cachedListings.Count > TotalListingsCount)
            {
                TotalListingsCount = cachedListings.Count;
            }
        }
    }

    private static IReadOnlyList<JobFlags> BuildOpenAcceptedJobs(IPartyFinderListing listing)
    {
        var result = new List<JobFlags>();
        var rawJobs = new List<byte>(listing.RawJobsPresent);
        var slots = new List<PartyFinderSlot>(listing.Slots);
        for (var i = 0; i < slots.Count; i++)
        {
            var isFilled = i < rawJobs.Count && rawJobs[i] != 0;
            if (!isFilled)
            {
                foreach (var job in slots[i].Accepting)
                {
                    if (!result.Contains(job))
                    {
                        result.Add(job);
                    }
                }
            }
        }
        return result;
    }

    private static IReadOnlyList<PfSlotInfo> BuildSlotsList(IPartyFinderListing listing)
    {
        var list = new List<PfSlotInfo>();
        var rawJobs = new List<byte>(listing.RawJobsPresent);
        var slots = new List<PartyFinderSlot>(listing.Slots);
        var totalSlots = Math.Max(rawJobs.Count, slots.Count);
        for (var i = 0; i < totalSlots; i++)
        {
            var jobId = i < rawJobs.Count ? rawJobs[i] : (byte)0;
            if (jobId != 0)
            {
                var role = GetRoleFromJobId(jobId);
                var label = role switch
                {
                    PfSlotRole.Tank => "T",
                    PfSlotRole.Healer => "H",
                    PfSlotRole.Dps => "D",
                    _ => "?",
                };
                list.Add(new PfSlotInfo(role, true, label));
            }
            else if (i < slots.Count)
            {
                var role = DetermineSlotRole(slots[i]);
                var label = role switch
                {
                    PfSlotRole.Tank => "T",
                    PfSlotRole.Healer => "H",
                    PfSlotRole.Dps => "D",
                    _ => "?",
                };
                list.Add(new PfSlotInfo(role, false, label));
            }
        }

        list.Sort((a, b) =>
        {
            var roleA = GetRoleSortOrder(a.Role);
            var roleB = GetRoleSortOrder(b.Role);
            if (roleA != roleB)
            {
                return roleA.CompareTo(roleB);
            }
            return b.IsFilled.CompareTo(a.IsFilled);
        });
        return list;
    }

    private static PfSlotRole GetRoleFromJobId(byte jobId) => jobId switch
    {
        1 or 3 or 19 or 21 or 32 or 37 => PfSlotRole.Tank,
        6 or 24 or 28 or 33 or 40 => PfSlotRole.Healer,
        _ => PfSlotRole.Dps,
    };

    private static PfSlotRole DetermineSlotRole(PartyFinderSlot slot)
    {
        var hasTank = false;
        var hasHealer = false;
        var hasDps = false;
        foreach (var job in slot.Accepting)
        {
            if (IsTankJobFlag(job))
            {
                hasTank = true;
            }
            else if (IsHealerJobFlag(job))
            {
                hasHealer = true;
            }
            else
            {
                hasDps = true;
            }
        }
        if (hasTank && !hasHealer && !hasDps)
        {
            return PfSlotRole.Tank;
        }
        if (hasHealer && !hasTank && !hasDps)
        {
            return PfSlotRole.Healer;
        }
        if (hasDps && !hasTank && !hasHealer)
        {
            return PfSlotRole.Dps;
        }
        if (hasTank)
        {
            return PfSlotRole.Tank;
        }
        if (hasHealer)
        {
            return PfSlotRole.Healer;
        }
        if (hasDps)
        {
            return PfSlotRole.Dps;
        }
        return PfSlotRole.Any;
    }

    private static int GetRoleSortOrder(PfSlotRole role) => role switch
    {
        PfSlotRole.Tank => 0,
        PfSlotRole.Healer => 1,
        PfSlotRole.Dps => 2,
        _ => 3,
    };

    private static bool IsTankJobFlag(JobFlags job) =>
        job is JobFlags.Paladin or JobFlags.Warrior or JobFlags.DarkKnight or JobFlags.Gunbreaker 
            or JobFlags.Gladiator or JobFlags.Marauder;
    private static bool IsHealerJobFlag(JobFlags job) =>
        job is JobFlags.WhiteMage or JobFlags.Scholar or JobFlags.Astrologian or JobFlags.Sage 
            or JobFlags.Conjurer;

    private static void OnPostClose(AddonEvent type, AddonArgs addonArgs)
    {
        openedForSync = false;
        closeScheduled = false;
        syncInProgress = false;
        lastListingCount = 0;
        syncedPagesCount = 1;
    }

    private static void OnPostUpdate(AddonEvent type, AddonArgs addonArgs)
    {
        var agent = AgentLookingForGroup.Instance();
        var addon = (AtkUnitBase*)addonArgs.Addon.Address;
        if (agent == null || addon == null)
        {
            return;
        }
        try
        {
            var lfgAddon = (AddonLookingForGroup*)addon;
            var textNode = lfgAddon->CategoryCountTextNodes[0].Value;
            if (textNode != null)
            {
                var rawText = textNode->NodeText.ToString();
                var digits = new StringBuilder();
                for (var i = 0; i < rawText.Length; i++)
                {
                    if (char.IsDigit(rawText[i]))
                    {
                        digits.Append(rawText[i]);
                    }
                }
                if (digits.Length > 0 && int.TryParse(digits.ToString(), out var parsedTotal) && parsedTotal > 0)
                {
                    TotalListingsCount = Math.Max(TotalListingsCount, parsedTotal);
                }
            }
        }
        catch 
        {
            TotalListingsCount = Math.Max(TotalListingsCount, (int)agent->NumberOfListingsDisplayed);
        }
        lock (cachedListings)
        {
            TotalListingsCount = Math.Max(TotalListingsCount, cachedListings.Count);
        }
        OnListingsUpdate?.Invoke();
        if (!openedForSync || closeScheduled || syncInProgress)
        {
            return;
        }
        int currentCount;
        lock (cachedListings)
        {
            currentCount = cachedListings.Count;
        }
        if (currentCount == 0 && agent->NumberOfListingsDisplayed == 0)
        {
            return;
        }
        var timeSinceLastListing = DateTime.UtcNow - lastListingReceivedTime;
        if (timeSinceLastListing < TimeSpan.FromMilliseconds(200))
        {
            return;
        }

        if (syncedPagesCount == 1)
        {
            if (currentCount < 50 && agent->NumberOfListingsDisplayed < 50)
            {
                ScheduleClose();
                return;
            }
            AdvanceToNextPage(addon, currentCount);
            return;
        }
        var newlyAdded = currentCount - lastListingCount;
        AepLog.Info($"[PF Sync] Page {syncedPagesCount} evaluated: newlyAdded={newlyAdded}, totalCached={currentCount}");
        if (newlyAdded < 50 || syncedPagesCount >= 5)
        {
            ScheduleClose();
            return;
        }
        AdvanceToNextPage(addon, currentCount);
    }

    private static void AdvanceToNextPage(AtkUnitBase* addon, int currentCount)
    {
        syncInProgress = true;
        lastListingCount = currentCount;
        syncedPagesCount++;
        ChangePage(addon, 1);
        _ = Task.Delay(550).ContinueWith(_ =>
        {
            Plugin.Framework.RunOnFrameworkThread(() =>
            {
                syncInProgress = false;
            });
        });
    }

    private static void ScheduleClose()
    {
        closeScheduled = true;
        _ = Task.Delay(250).ContinueWith(_ =>
        {
            Plugin.Framework.RunOnFrameworkThread(() =>
            {
                if (openedForSync)
                {
                    CloseSyncAddon();
                }
            });
        });
    }

    private static void ChangePage(AtkUnitBase* addon, int direction)
    {
        if (addon == null)
        {
            return;
        }
        var buttonParam = direction > 0 ? 9 : 8;
        AepLog.Info($"[PF PageTurn] Turning page using native ButtonClick Param={buttonParam}...");
        var atkEvent = stackalloc AtkEvent[1];
        var atkEventData = stackalloc AtkEventData[1];
        addon->ReceiveEvent(AtkEventType.ButtonClick, buttonParam, atkEvent, atkEventData);
    }

    private static void CloseSyncAddon()
    {
        openedForSync = false;
        closeScheduled = false;
        syncInProgress = false;
        lastListingCount = 0;
        syncedPagesCount = 1;
        OnListingsUpdate?.Invoke();
        var currentAddon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("LookingForGroup").Address;
        if (currentAddon != null && currentAddon->IsVisible)
        {
            ChatSender.TrySend("/partyfinder");
        }
    }

    public static bool TriggerSilentRefresh(bool force = false)
    {
        if (!Plugin.ClientState.IsLoggedIn){
            return false;
        }
        if (Plugin.Condition[ConditionFlag.InCombat] ||
            Plugin.Condition[ConditionFlag.BoundByDuty] ||
            Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent])
        {
            return false;
        }
        var gameMain = GameMain.Instance();
        if (gameMain == null || gameMain->CurrentContentFinderConditionId != 0){
            return false;
        }
        if (!force && (DateTime.UtcNow - lastRefreshRequest) < RefreshCooldown){
            return false;
        }

        lastRefreshRequest = DateTime.UtcNow;
        var existingAddon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("LookingForGroup").Address;
        if (existingAddon != null && existingAddon->IsVisible){
            openedForSync = false;
            var agent = AgentLookingForGroup.Instance();
            return agent != null && agent->RequestListingsUpdate();
        }

        openedForSync = true;
        closeScheduled = false;
        syncInProgress = false;
        lastListingCount = 0;
        syncedPagesCount = 1;
        lock (cachedListings)
        {
            cachedListings.Clear();
        }
        Plugin.Framework.RunOnFrameworkThread(() =>
        {
            ChatSender.TrySend("/partyfinder");
        });
        _ = Task.Delay(6000).ContinueWith(_ =>
        {
            if (openedForSync)
            {
                Plugin.Framework.RunOnFrameworkThread(() =>
                {
                    if (openedForSync)
                    {
                        openedForSync = false;
                        closeScheduled = false;
                        syncInProgress = false;
                        lastListingCount = 0;
                        OnListingsUpdate?.Invoke();
                        var currentAddon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("LookingForGroup").Address;
                        if (currentAddon != null && currentAddon->IsVisible)
                        {
                            ChatSender.TrySend("/partyfinder");
                        }
                    }
                });
            }
        });
        return true;
    }

    public static void Read(List<PartyFinderListing> destination)
    {
        if (!Plugin.ClientState.IsLoggedIn){
            return;
        }
        lock (cachedListings)
        {
            destination.Clear();
            destination.AddRange(cachedListings);
        }
    }

    public static bool OpenListing(ulong listingId)
    {
        if (!Plugin.ClientState.IsLoggedIn){
            return false;
        }

        var agent = AgentLookingForGroup.Instance();
        if (agent == null){
            return false;
        }

        return agent->OpenListing(listingId);
    }
}
