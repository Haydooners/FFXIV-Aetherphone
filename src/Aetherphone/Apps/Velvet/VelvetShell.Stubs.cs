using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private enum PostSheetAction
    {
        View,
        Audience,
        Delete,
        Report,
        Block,
    }

    private enum ProfileMenuAction
    {
        Settings,
        Report,
        NotInterested,
        Disconnect,
        Block,
    }

    private readonly ActionSheet.Item[] postSheetItems = new ActionSheet.Item[3];
    private readonly PostSheetAction[] postSheetActions = new PostSheetAction[3];
    private readonly ActionSheet.Item[] threadSheetItems = new ActionSheet.Item[1];
    private readonly ActionSheet profileMenu = new();
    private readonly ActionSheet.Item[] profileMenuItems = new ActionSheet.Item[4];
    private readonly ProfileMenuAction[] profileMenuActions = new ProfileMenuAction[4];
    private int profileMenuCount;
    private string profileMenuUserId = string.Empty;
    private string profileMenuName = string.Empty;
    private int postSheetCount;
    private bool sheetPostInFeed;
    private string postSheetTitle = string.Empty;
    private VelvetPostDto? sheetPost;
    private string? sheetThreadId;
}
