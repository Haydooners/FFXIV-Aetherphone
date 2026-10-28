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

internal sealed record PfExtrasDto(
    string ContentId,
    string? StratUrl,
    string? Notes,
    long UpdatedAtUnix
);

internal sealed record PublishPfExtrasRequest(
    string ContentId,
    uint DutyId,
    string? StratUrl,
    string? Notes
);

internal sealed record PfExtrasLookupRequest(string[] ContentIds);

