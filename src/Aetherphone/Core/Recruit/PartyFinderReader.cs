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
    private static int syncedPagesCount = 1;
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
            // Filled slots (true) come before open slots (false)
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
        syncedPagesCount = 1;
    }

    private static void OnReceiveEvent(AddonEvent type, AddonArgs addonArgs)
    {
        if (addonArgs is AddonReceiveEventArgs receiveArgs)
        {
            Plugin.Log.Information($"[PF Event] Type={receiveArgs.AtkEventType} Param={receiveArgs.EventParam}");
        }
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
                var text = textNode->NodeText.ToString();
                if (int.TryParse(text, out var parsedTotal) && parsedTotal > 0)
                {
                    TotalListingsCount = parsedTotal;
                }
            }
        }
        catch
        {
            TotalListingsCount = Math.Max(50, (int)agent->NumberOfListingsDisplayed);
        }
        
        OnListingsUpdate?.Invoke();
        var totalPages = Math.Max(1, (int)Math.Ceiling((float)TotalListingsCount / 50));
        var maxSyncPages = Math.Min(totalPages, 5);
        if (openedForSync && !closeScheduled && agent->NumberOfListingsDisplayed > 0)
        {
            if (syncedPagesCount < maxSyncPages)
            {
                closeScheduled = true;
                _ = Task.Delay(600).ContinueWith(_ =>
                {
                    Plugin.Framework.RunOnFrameworkThread(() =>
                    {
                        if (!openedForSync)
                        {
                            return;
                        }
                        var currentAddon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("LookingForGroup").Address;
                        if (currentAddon != null && currentAddon->IsVisible)
                        {
                            syncedPagesCount++;
                            closeScheduled = false;
                            ChangePage(currentAddon, 1);
                            return;
                        }
                        CloseSyncAddon();
                    });
                });
            }
            else
            {
                closeScheduled = true;
                _ = Task.Delay(600).ContinueWith(_ =>
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
        }
    }

    private static void ChangePage(AtkUnitBase* addon, int direction)
    {
        if (addon == null)
        {
            return;
        }

        var buttonParam = direction > 0 ? 9 : 8;
        Plugin.Log.Information($"[PF PageTurn] Turning page using native ButtonClick Param={buttonParam}...");
        var atkEvent = stackalloc AtkEvent[1];
        var atkEventData = stackalloc AtkEventData[1];
        addon->ReceiveEvent(AtkEventType.ButtonClick, buttonParam, atkEvent, atkEventData);
    }

    private static void CloseSyncAddon()
    {
        openedForSync = false;
        closeScheduled = false;
        syncedPagesCount = 1;
        OnListingsUpdate?.Invoke();
        var currentAddon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("LookingForGroup").Address;

        if (currentAddon != null && currentAddon->IsVisible)
        {
            ChatSender.TrySend("/partyfinder");
        }
    }

    private static AtkComponentButton* FindNextPageButton(AtkUnitBase* addon)
    {
        if (addon == null)  { 
            return null;
        }
        AtkTextNode* pageTextNode = null;
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type != 3){
                continue;
            }
            var textNode = (AtkTextNode*)node;
            var text = textNode->NodeText.ToString().Trim();
            if (text.Length >= 3 && text.Contains('/'))
            {
                var slashIdx = text.IndexOf('/');
                var left = text[..slashIdx].Trim();
                var right = text[(slashIdx + 1)..].Trim();
                if (int.TryParse(left, out _) && int.TryParse(right, out _))
                {
                    pageTextNode = textNode;
                    break;
                }
            }
        }

        if (pageTextNode == null){
            return null;
        }
        AtkComponentButton* bestNextBtn = null;
        var bestDist = float.MaxValue;
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type != 1006){
                continue;
            }
            var compNode = (AtkComponentNode*)node;
            if (compNode->Component == null || (int)compNode->Component->GetComponentType() != 1){
                continue;
            }
            if (Math.Abs(node->Y - pageTextNode->Y) > 20){
                continue;
            }
            if (node->X > pageTextNode->X)
            {
                var dist = node->X - pageTextNode->X;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestNextBtn = (AtkComponentButton*)compNode->Component;
                }
            }
        }
        return bestNextBtn;
    }

    private static void ClickButton(AtkComponentButton* button, AtkUnitBase* addon)
    {
        if (button == null){ 
            return; 
        }
        var atkEvent = stackalloc AtkEvent[1];
        var atkEventData = stackalloc AtkEventData[1];
        button->ReceiveEvent(AtkEventType.ButtonClick, 0, atkEvent, atkEventData);
        if (button->OwnerNode != null && addon != null)
        {
            addon->ReceiveEvent(AtkEventType.ButtonClick, (int)button->OwnerNode->NodeId, atkEvent, atkEventData);
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

    private static string ReadUtf8(byte* pointer, int maximumLength)
    {
        if (pointer == null){
            return string.Empty;
        }

        var length = 0;
        while (length < maximumLength && pointer[length] != 0){
            length++;
        }
        return length > 0 ? Encoding.UTF8.GetString(pointer, length) : string.Empty;
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
