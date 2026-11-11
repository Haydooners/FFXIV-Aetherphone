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

internal sealed record PfSlotDto(
    byte Role,
    bool IsFilled,
    string Label,
    byte JobId
);

internal sealed record PfListingSyncDto(
    string ListingId,
    string DutyName,
    string CategoryName,
    string Comment,
    string AuthorName,
    string WorldName,
    byte SlotsFilled,
    byte TotalSlots,
    ushort ItemLevel,
    long ReadAtUnix,
    PfSlotDto[] Slots,
    string HostContentId
);

internal sealed record SyncPfListingsRequest(
    PfListingSyncDto[] Listings);

internal sealed record PfRefreshStatusDto(
    bool RefreshRequested);

