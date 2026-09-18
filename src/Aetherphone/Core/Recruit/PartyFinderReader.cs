using System;
using System.Collections.Generic;
using System.Text;
using Aetherphone.Apps.Recruit;
using Aetherphone.Core;
using Aetherphone.Core.Game;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Recruit;

internal static unsafe class PartyFinderReader
{
    public static event System.Action? OnListingsUpdate;
    private static readonly List<PartyFinderListing> cachedListings = new();
    private static bool isSilentRefresh;
    private static DateTime lastRefreshRequest = DateTime.MinValue;
    private static readonly TimeSpan RefreshCooldown = TimeSpan.FromSeconds(8);
    private static bool initialized;
    private static short savedX = 200;
    private static short savedY = 200;

    public static void Initialize()
    {
        if (initialized){
            return;
        }

        Plugin.PartyFinderGui.ReceiveListing += OnReceiveListing;

        initialized = true;

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "LookingForGroup", OnPreSetup);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "LookingForGroup", OnPreDraw);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "LookingForGroup", OnPostUpdate);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostClose, "LookingForGroup", OnPostClose);
    }

    public static void Dispose()
    {
        if (initialized){
            return;
        }

        Plugin.PartyFinderGui.ReceiveListing -= OnReceiveListing;

        initialized = false;

        Plugin.AddonLifecycle.UnregisterListener(OnPreSetup, OnPreDraw, OnPostUpdate, OnPostClose);
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
            DateTime.UtcNow
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

    private static void OnPostClose(AddonEvent type, AddonArgs addonArgs)
    {
        isSilentRefresh = false;
    }

    private static void OnPreSetup(AddonEvent type, AddonArgs addonArgs)
    {
        var addon = (AtkUnitBase*)addonArgs.Addon.Address;
        if (addon == null)
        {
            return;
        }

        if (isSilentRefresh)
        {
            if (addon->X > -5000 && addon->Y > -5000)
            {
                savedX = addon->X;
                savedY = addon->Y;
            }
            addon->IsVisible = false;
            addon->SetPosition(-9999, -9999);
        }
        else
        {
            addon->IsVisible = true;
            if (addon->X < 0 || addon->Y < 0)
            {
                addon->SetPosition(savedX > 0 ? savedX : (short)200, savedY > 0 ? savedY : (short)200);
            }
        }
    }

    private static void OnPreDraw(AddonEvent type, AddonArgs addonArgs)
    {
        var addon = (AtkUnitBase*)addonArgs.Addon.Address;
        if (addon == null)
        {
            return;
        }

        if (isSilentRefresh)
        {
            addon->IsVisible = false;
        }
        else
        {
            addon->IsVisible = true;
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
        
        if (agent->NumberOfListingsDisplayed > 0)
        {
            OnListingsUpdate?.Invoke();
            if (isSilentRefresh)
            {
                isSilentRefresh = false;
                Plugin.Framework.RunOnFrameworkThread(() =>
                {
                    var currentAgent = AgentLookingForGroup.Instance();
                    var currentAddon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("LookingForGroup").Address;
                    if (currentAddon != null)
                    {
                        currentAddon->SetPosition(savedX > 0 ? savedX : (short)200, savedY > 0 ? savedY : (short)200);
                        currentAddon->IsVisible = true;
                    }
                    if (currentAgent != null)
                    {
                        currentAgent->HideAddon();
                    }
                });
            }
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

        var agent = AgentLookingForGroup.Instance();
        if (agent == null){
            return false;
        }

        lastRefreshRequest = DateTime.UtcNow;
        var existingAddon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("LookingForGroup").Address;
        if (existingAddon != null && existingAddon->IsVisible){
            isSilentRefresh = false;
            return agent->RequestListingsUpdate();
        }

        isSilentRefresh = true;
        agent->ShowAddon();
        return true;
    }

    public static void Read(List<PartyFinderListing> destination)
    {
        if (!Plugin.ClientState.IsLoggedIn)
        {
            return;
        }
        var agent = AgentLookingForGroup.Instance();
        if (agent == null || agent->NumberOfListingsDisplayed == 0)
        {
            return;
        }
        destination.Clear();

        var activeIds = agent->Listings.ListingIds;
        var count = Math.Min((int)agent->NumberOfListingsDisplayed, activeIds.Length);
        lock (cachedListings)
        {
            for (var i = 0; i < count; i++)
            {
                var id = activeIds[i];
                var found = cachedListings.Find(l => l.ListingId == id);
                if (found != null)
                {
                    destination.Add(found);
                }
            }
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
