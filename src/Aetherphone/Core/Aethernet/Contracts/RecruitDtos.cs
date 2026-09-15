namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record RecruitListingDto(
    string Id,
    string Title,
    string Description,
    string Schedule,
    int Kind,
    string DutyName,
    int Category,
    int Days,
    int StartMinuteOfDay,
    int EndMinuteOfDay,
    int Timezone,
    int[] RolesNeeded,
    string AuthorName,
    string World,
    long CreatedAtUnix
);

internal sealed record CreateRecruitRequest(
    string Title,
    string Description,
    string ScheduleText,
    int Kind,
    string DutyName,
    int Category,
    int Days,
    int StartMinuteOfDay,
    int EndMinuteOfDay,
    int Timezone,
    int[] RolesNeeded,
    string AuthorName,
    string WorldName
);
