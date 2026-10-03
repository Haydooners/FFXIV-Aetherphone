using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Apps.Feedback;

internal enum FeedbackCategory : byte
{
    Bug,
    Idea,
    Praise,
    Other,
}

internal readonly record struct FeedbackKind(
    FeedbackCategory Category,
    string WireName,
    FontAwesomeIcon Icon,
    Vector4 Tint,
    LocString Title,
    LocString Subtitle,
    LocString Prompt,
    LocString Placeholder,
    LocString Thanks);

internal static class FeedbackKinds
{
    public static readonly FeedbackKind[] All =
    {
        new(FeedbackCategory.Bug, "bug", FontAwesomeIcon.Bug, AccentRing.Red, L.Feedback.KindBug,
            L.Feedback.KindBugSubtitle, L.Feedback.PromptBug, L.Feedback.PlaceholderBug, L.Feedback.ThanksBug),
        new(FeedbackCategory.Idea, "idea", FontAwesomeIcon.Lightbulb, AccentRing.Gold, L.Feedback.KindIdea,
            L.Feedback.KindIdeaSubtitle, L.Feedback.PromptIdea, L.Feedback.PlaceholderIdea, L.Feedback.ThanksIdea),
        new(FeedbackCategory.Praise, "praise", FontAwesomeIcon.Heart, AccentRing.Rose, L.Feedback.KindPraise,
            L.Feedback.KindPraiseSubtitle, L.Feedback.PromptPraise, L.Feedback.PlaceholderPraise,
            L.Feedback.ThanksPraise),
        new(FeedbackCategory.Other, "other", FontAwesomeIcon.CommentDots, AccentRing.Indigo, L.Feedback.KindOther,
            L.Feedback.KindOtherSubtitle, L.Feedback.PromptOther, L.Feedback.PlaceholderOther,
            L.Feedback.ThanksOther),
    };

    public static ref readonly FeedbackKind Of(FeedbackCategory category) => ref All[(int)category];

    public static FeedbackCategory Parse(string? wireName)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index].WireName, wireName, StringComparison.OrdinalIgnoreCase))
            {
                return All[index].Category;
            }
        }

        return FeedbackCategory.Other;
    }
}

internal enum FeedbackStatus : byte
{
    Received,
    Resolved,
    Closed,
}

internal static class FeedbackStatuses
{
    public static FeedbackStatus Parse(string? wireName)
    {
        if (string.Equals(wireName, "resolved", StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackStatus.Resolved;
        }

        if (string.Equals(wireName, "dismissed", StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackStatus.Closed;
        }

        return FeedbackStatus.Received;
    }

    public static LocString Label(FeedbackStatus status) => status switch
    {
        FeedbackStatus.Resolved => L.Feedback.StatusResolved,
        FeedbackStatus.Closed => L.Feedback.StatusClosed,
        _ => L.Feedback.StatusReceived,
    };

    public static LocString Explanation(FeedbackStatus status) => status switch
    {
        FeedbackStatus.Resolved => L.Feedback.StatusResolvedHint,
        FeedbackStatus.Closed => L.Feedback.StatusClosedHint,
        _ => L.Feedback.StatusReceivedHint,
    };

    public static Vector4 Tint(FeedbackStatus status) => status switch
    {
        FeedbackStatus.Resolved => AccentRing.Green,
        FeedbackStatus.Closed => AccentRing.Slate,
        _ => AccentRing.Azure,
    };
}
