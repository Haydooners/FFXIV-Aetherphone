using System.Text;
using Aetherphone.Apps.Recruit;
using Aetherphone.Core.GameChat;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.PartyFinder.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherphone.Core.Recruit;

internal enum PfPostStart : byte
{
    Started,
    NotLoggedIn,
    Busy,
}

internal static unsafe class PartyFinderWriter
{
    // ButtonClick param of the Recruit Members Button
    private const int RecruitMembersButtonParam = 2; 
    private const long OpenCriteriaTimeoutMilliseconds = 10 * 1000;

    private const string ConditionAddon = "LookingForGroupCondition";
    private const string ListingsAddon = "LookingForGroup";
    private const long PendingTimeoutMilliseconds = 3 * 60 * 1000;
    private const long PostCloseGraceMilliseconds = 90 * 1000;
    private const int CommentByteLimit = 192;
    private const ushort NoPassword = 10000;
    private const byte NormalPartySlots = 8;
    private const byte NormalPartyGroups = 1;

    private static PfDraft? pending;
    private static Action? onPosted;
    private static ulong ownContentId;
    private static long startedAtMilliseconds;
    private static bool listening;
    private static bool filledThisPost;
    private static bool criteriaOpenRequested;
    private static bool conditionClosed;
    private static long closedAtMilliseconds;
    private static bool populating;

    public static bool IsPending => pending is not null && !conditionClosed && !IsExpired();

    private static bool IsExpired()
    {
        var now = Environment.TickCount64;
        if (conditionClosed)
        {
            return now - closedAtMilliseconds >= PostCloseGraceMilliseconds;
        }

        return now - startedAtMilliseconds >= PendingTimeoutMilliseconds;
    }

    public static PfPostStart Begin(PfDraft draft, ulong contentId, Action posted)
    {
        if (!Plugin.ClientState.IsLoggedIn || contentId == 0)
        {
            return PfPostStart.NotLoggedIn;
        }

        if (IsBusy())
        {
            return PfPostStart.Busy;
        }

        pending = draft;
        onPosted = posted;
        ownContentId = contentId;
        startedAtMilliseconds = Environment.TickCount64;
        filledThisPost = false;
        criteriaOpenRequested = false;
        conditionClosed = false;

        if (!listening)
        {   
            Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, ListingsAddon, OnListingsUpdate);
            Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, ConditionAddon, OnConditionReady);
            Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, ConditionAddon, OnConditionClosed);
            listening = true;
        }

        if (!PartyFinderReader.IsWindowOpen())
        {
            ChatSender.TrySend("/partyfinder");
        }

        return PfPostStart.Started;
    }

    public static void NoticeListing(ulong listingContentId)
    {
        if (pending is null || listingContentId != ownContentId)
        {
            return;
        }

        if (IsExpired())
        {
            Clear();
            return;
        }

        var posted = onPosted;
        Clear();
        posted?.Invoke();
    }

    public static void Cancel() => Clear();

    private static void OnListingsUpdate(AddonEvent type, AddonArgs args)
    {
        if (pending is null || criteriaOpenRequested || filledThisPost)
        {
            return;
        }

        if (Environment.TickCount64 - startedAtMilliseconds > OpenCriteriaTimeoutMilliseconds)
        {
            criteriaOpenRequested = true;
            return;
        }

        if (Plugin.GameGui.GetAddonByName(ConditionAddon).Address != nint.Zero)
        {
            criteriaOpenRequested = true;
            return;
        }

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon is null || !addon->IsVisible || !addon->IsReady)
        {
            return;
        }

        criteriaOpenRequested = true;
        var atkEvent = stackalloc AtkEvent[1];
        var atkEventData = stackalloc AtkEventData[1];
        addon->ReceiveEvent(AtkEventType.ButtonClick, RecruitMembersButtonParam, atkEvent, atkEventData);
    }

    private static void OnConditionReady(AddonEvent type, AddonArgs args)
    {
        if (pending is not { } draft || filledThisPost)
        {
            return;
        }

        if (IsExpired())
        {
            Clear();
            return;
        }

        var agent = AgentLookingForGroup.Instance();
        if (agent is null)
        {
            return;
        }

        filledThisPost = true;
        Apply(ref agent->StoredRecruitmentInfo, draft);
        ApplyItemLevel(agent, draft.MinItemLevel);
        populating = true;
        agent->PopulateRecruitmentCriteriaPopup(false, false);
        populating = false;
    }

    private static void OnConditionClosed(AddonEvent type, AddonArgs args)
    {
        var closingAgent = AgentLookingForGroup.Instance();
        if (pending is null || populating)
        {
            return;
        }

        conditionClosed = true;
        closedAtMilliseconds = Environment.TickCount64;
    }

    private static void Apply(ref AgentLookingForGroup.RecruitmentSub recruitment, PfDraft draft)
    {
        recruitment.SelectedCategory = ToGameCategory(draft.Category);
        recruitment.SelectedDutyId = (ushort)draft.DutyId;
        recruitment.Objective = ToGameObjective(draft.Objective);
        recruitment.CompletionStatus = ToGameCompletion(draft.CompletionStatus);
        recruitment.DutyFinderSettingFlags = ToGameDutySettings(draft);
        recruitment.LootRule = (AgentLookingForGroup.LootRule)draft.LootRule;
        recruitment.LanguageFlags = (AgentLookingForGroup.Language)draft.Languages;
        recruitment.Password = draft.Passcode ?? NoPassword;
        recruitment.LimitRecruitingToWorld = draft.LimitToWorldServer ? (byte)0 : (byte)1;
        recruitment.OnePlayerPerJob = draft.OnePlayerPerJob ? (byte)1 : (byte)0;
        recruitment.NumberOfGroups = NormalPartyGroups;
        recruitment.NumberOfSlotsInMainParty = NormalPartySlots;

        var slotFlags = recruitment.SlotFlags;
        for (var slotIndex = 1; slotIndex < NormalPartySlots && slotIndex < draft.SlotJobMasks.Length; slotIndex++)
        {
            slotFlags[slotIndex] = draft.SlotJobMasks[slotIndex];
        }

        var comment = recruitment.Comment;
        comment.Clear();
        var written = Encoding.UTF8.GetBytes(Truncate(draft.Comment, CommentByteLimit - 1), comment);
        comment[written] = 0;
    }

    private static void ApplyItemLevel(AgentLookingForGroup* agent, ushort minItemLevel)
    {
        if (minItemLevel == 0)
        {
            agent->AvgItemLvEnabled = 0;
            return;
        }

        agent->AvgItemLv = minItemLevel;
        agent->AvgItemLvEnabled = 1;
    }

    private static AgentLookingForGroup.DutyCategory ToGameCategory(PfDutyCategory category)
    {
        if (category == PfDutyCategory.None)
        {
            return AgentLookingForGroup.DutyCategory.None;
        }

        return (AgentLookingForGroup.DutyCategory)(1u << (int)category);
    }

    private static AgentLookingForGroup.Objective ToGameObjective(ObjectiveFlags objective)
    {
        return objective switch
        {
            ObjectiveFlags.Practice => AgentLookingForGroup.Objective.Practice,
            ObjectiveFlags.DutyCompletion => AgentLookingForGroup.Objective.DutyCompletion,
            ObjectiveFlags.Loot => AgentLookingForGroup.Objective.Loot,
            _ => AgentLookingForGroup.Objective.None,
        };
    }

    private static AgentLookingForGroup.CompletionStatus ToGameCompletion(PfCompletionStatus status)
    {
        return status switch
        {
            PfCompletionStatus.DutyComplete => AgentLookingForGroup.CompletionStatus.DutyComplete,
            PfCompletionStatus.DutyIncomplete => AgentLookingForGroup.CompletionStatus.DutyIncomplete,
            _ => AgentLookingForGroup.CompletionStatus.None,
        };
    }

    private static AgentLookingForGroup.DutyFinderSetting ToGameDutySettings(PfDraft draft)
    {
        var flags = AgentLookingForGroup.DutyFinderSetting.None;
        if (draft.UnrestrictedParty)
        {
            flags |= AgentLookingForGroup.DutyFinderSetting.UnrestrictedParty;
        }

        if (draft.MinimumIl)
        {
            flags |= AgentLookingForGroup.DutyFinderSetting.MinimumIL;
        }

        if (draft.SilenceEcho)
        {
            flags |= AgentLookingForGroup.DutyFinderSetting.SilenceEcho;
        }

        return flags;
    }

    private static string Truncate(string text, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
        {
            return text;
        }

        var length = text.Length;
        while (length > 0 && Encoding.UTF8.GetByteCount(text.AsSpan(0, length)) > maxBytes)
        {
            length--;
        }

        return text[..length];
    }

    private static bool IsBusy()
    {
        if (Plugin.Condition[ConditionFlag.InCombat] ||
            Plugin.Condition[ConditionFlag.BoundByDuty] ||
            Plugin.Condition[ConditionFlag.BetweenAreas] ||
            Plugin.Condition[ConditionFlag.BetweenAreas51] ||
            Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent])
        {
            return true;
        }

        var gameMain = GameMain.Instance();
        return gameMain is null || gameMain->CurrentContentFinderConditionId != 0;
    }

    private static void Clear()
    {
        pending = null;
        onPosted = null;
        ownContentId = 0;
        filledThisPost = false;
        criteriaOpenRequested = false;
        conditionClosed = false;
        if (listening)
        {
            Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, ListingsAddon, OnListingsUpdate);
            Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, ConditionAddon, OnConditionReady);
            Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, ConditionAddon, OnConditionClosed);
            listening = false;
        }
    }
}
