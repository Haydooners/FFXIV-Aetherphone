using System.Text;
using Aetherphone.Apps.Recruit;
using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Recruit;

internal static unsafe class PartyFinderReader
{
    public static bool RequestServerData()
    {
        if (!Plugin.ClientState.IsLoggedIn){
            return false;
        }

        var gameMain = GameMain.Instance();
        if (gameMain == null || gameMain->CurrentContentFinderConditionId != 0){
            return false;
        }

        var agent = AgentLookingForGroup.Instance();
        if (agent == null){
            return false;
        }

        return agent->RequestListingsUpdate();
    }

    public static void Read(List<PartyFinderListing> into)
    {
        into.Clear();
        if (!Plugin.ClientState.IsLoggedIn){
            return;
        }

        var agent = AgentLookingForGroup.Instance();
        if (agent == null){
            return;
        }

        var conditionSheet = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>();
        var worldSheet = Plugin.DataManager.GetExcelSheet<World>();

        if (agent->ListingContentId == 0)
        {
            return;
        }

        var detailed = &agent->LastViewedListing;
        if (detailed == null || detailed->ListingId == 0){
            return;
        }

        if (agent->NumberOfListingsDisplayed > 0){
            var activeIds = agent->Listings.ListingIds;
            var isStillActive = false;

            for (var i = 0; i < agent->NumberOfListingsDisplayed && i < activeIds.Length; i++){
                if (activeIds[i] == detailed->ListingId){
                    isStillActive = true;
                    break;
                }
            }

            if (!isStillActive){
                return;
            }
        }

        if (detailed != null && detailed->ListingId != 0){
            var categoryName = detailed->Category switch{
                AgentLookingForGroup.DutyCategory.Dungeons => "Dungeons",
                AgentLookingForGroup.DutyCategory.Trials => "Trials",
                AgentLookingForGroup.DutyCategory.Raids => "Raids",
                AgentLookingForGroup.DutyCategory.HighEndDuty => "High-End Duty",
                AgentLookingForGroup.DutyCategory.PvP => "PvP",
                AgentLookingForGroup.DutyCategory.GoldSaucer => "Gold Saucer",
                AgentLookingForGroup.DutyCategory.FATEs => "FATEs",
                AgentLookingForGroup.DutyCategory.TreasureHunts => "Treasure Hunts",
                AgentLookingForGroup.DutyCategory.TheHunt => "The Hunt",
                AgentLookingForGroup.DutyCategory.DeepDungeons => "Deep Dungeons",
                AgentLookingForGroup.DutyCategory.FieldOperations => "Field Ops",
                AgentLookingForGroup.DutyCategory.VCDungeonFinder => "Criterion",
                _ => "Other",
            };
            var dutyName = categoryName == "Other" ? "Other / Player Event" : categoryName;
            if (categoryName != "Other" && detailed->DutyId > 0 && conditionSheet.TryGetRow(detailed->DutyId, out var condition)){
                var text = condition.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(text)){
                    dutyName = text;
                }
            }

            var leaderName = ReadUtf8((byte*)detailed + 0x390, 32);
            var comment = ReadUtf8((byte*)detailed + 0x3B0, 192);
            var worldName = worldSheet.TryGetRow(detailed->HomeWorld, out var world) ? world.Name.ExtractText() : string.Empty;

            into.Add(new PartyFinderListing(
                detailed->ListingId,
                dutyName,
                categoryName,
                comment,
                string.IsNullOrWhiteSpace(leaderName) ? "Party Leader" : leaderName,
                string.IsNullOrWhiteSpace(worldName) ? "Datacenter" : worldName,
                detailed->SlotsFilled,
                detailed->TotalSlots,
                detailed->AvgItemLv,
                DateTime.UtcNow
            ));
        }
    }

    private static string ReadUtf8(byte* ptr, int maxLen)
    {
        if (ptr == null){
            return string.Empty;
        }

        var len = 0;
        while (len < maxLen && ptr[len] != 0){
            len++;
        }

        return len > 0 ? Encoding.UTF8.GetString(ptr, len) : string.Empty;
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
