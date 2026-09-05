using Aetherphone.Core;
using Aetherphone.Core.Message;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const byte ThreadActInfo = 0;
    private const byte ThreadActSearch = 1;
    private const byte ThreadActTranslate = 2;
    private const byte ThreadActMute = 3;
    private const byte ThreadActWallpaper = 4;
    private const byte ThreadActEncryption = 5;
    private const byte ThreadActStarred = 6;
    private const byte ThreadActDelete = 7;
    private const float ThreadHeaderAvatarRadius = 18f;
    private const float ThreadHeaderAvatarGap = 6f;
    private const float ThreadHeaderNameGap = 10f;
    private const float BubbleRounding = 9f;

    private static readonly TextStyle ThreadNameStyle = TextStyles.Headline;
    private static readonly TextStyle ThreadSubStyle = new(0.76f, FontWeight.Regular);

    private readonly ActionSheet threadSheet = new();
    private readonly ActionSheet.Item[] threadSheetItems = new ActionSheet.Item[8];
    private readonly byte[] threadSheetActions = new byte[8];
    private int threadSheetCount;
    private string threadSheetTitle = string.Empty;
    private string? threadSheetConversationId;

    private sealed class ThreadView : ChatThreadView<ChatMessageDto, ConversationDto>
    {
        private readonly MessageApp app;
        private ConversationMemberDto[] memberLineSource = Array.Empty<ConversationMemberDto>();
        private string memberLine = string.Empty;

        public ThreadView(MessageApp app)
            : base(app.store, app.ui, app.images, app.lodestone, app.http, app.library, app.configuration,
                app.confirm, app.report, app.translation, app.wallpaperImages, app.encryptionHelp, ThreadPollSeconds,
                TypingSendSeconds)
        {
            this.app = app;
        }

        protected override PhoneTheme Theme => app.theme;
        protected override IPhoneApp Owner => app;
        protected override INavigator Navigation => app.navigation;
        protected override Action BackAction => app.back;
        protected override string MyUserId => store.MyUserId;
        protected override Vector4 Accent => ui.Accent;
        protected override string EmptyText => Loc.T(L.Message.ThreadEmpty);
        protected override string LogTag => "Message";
        protected override string PickerTitle => Loc.T(L.Common.SendPhoto);
        protected override string ImportLabel => Loc.T(L.Common.ImportFromPc);
        protected override string NoPhotosLabel => Loc.T(L.Common.NoPhotos);
        protected override string SaveLabel => Loc.T(L.Common.SaveToGallery);
        protected override string SavedLabel => Loc.T(L.Common.SavedToGallery);
        protected override bool IsGroupThread => app.store.Conversation?.IsGroup ?? false;
        protected override ChatComposerStyle ComposerStyle => ChatComposerStyle.Plus;
        protected override string ComposerHint => Loc.T(L.DirectMessages.StartChat);

        protected override ChatBubbleStyle BubbleStyle => new(app.activeTheme.OutgoingBubble,
            MessageThemes.OutgoingInk, MessageThemes.IncomingBubble, MessageThemes.IncomingInk, BubbleRounding, true);

        public bool SearchOpen => searchController.Open;

        public void ToggleSearch() => searchController.Toggle();

        public bool CanTranslate => TranslationAvailable;

        public bool TranslatingThread(string threadId) => IsConversationTranslated(threadId);

        public void ToggleTranslation(string threadId) => ToggleConversationTranslation(threadId);

        protected override void PaintTranscriptBackdrop(Rect listRect)
        {
            var conversationId = store.CurrentThreadId ?? string.Empty;
            MessageWallpapers.Paint(ImGui.GetWindowDrawList(), listRect,
                MessageWallpapers.Effective(configuration, conversationId), configuration.MessageWallpaperPattern,
                app.wallpaperImages);
        }

        protected override bool IsDeleted(ChatMessageDto message) => message.Deleted;

        protected override string SenderIdOf(ChatMessageDto message) => message.SenderId;

        protected override int KindOf(ChatMessageDto message) => message.Kind;

        protected override string? BodyOf(ChatMessageDto message) => message.Body;

        protected override int EncVersionOf(ChatMessageDto message) => message.EncVersion;

        protected override byte[]? DecryptSealed(ChatMessageDto message, string? threadId, byte[] sealedBytes) =>
            app.store.DecryptMedia(message, sealedBytes);

        protected override void OpenImageView(string messageId) => app.router.Push(MessageRoute.ImageView(messageId));

        protected override void OpenReactions(string messageId) => app.router.Push(MessageRoute.Reactions(messageId));

        protected override void PushImagePickerScreen(string threadId) => app.router.Push(MessageRoute.ChatImage(threadId));

        protected override void PopScreen() => app.router.Pop();

        protected override void OpenEncryptionInfo(string threadId)
        {
            var conversation = app.store.Conversation;
            if (conversation is not null)
            {
                app.router.Push(MessageRoute.Encryption(conversation.Id));
            }
        }

        protected override void OnThreadSwitchingFrom(string previousThreadId)
        {
            if (!composer.IsEditing)
            {
                SaveDraft(previousThreadId);
            }
        }

        protected override void OnThreadOpened(string threadId)
        {
            composer.Draft = configuration.MessageDrafts.GetValueOrDefault(threadId, string.Empty);
        }

        protected override void OnDraftConsumed(string threadId) => ClearDraft(threadId);

        private void SaveDraft(string conversationId)
        {
            var trimmed = composer.Draft.Trim();
            var drafts = configuration.MessageDrafts;
            if (trimmed.Length == 0)
            {
                if (drafts.Remove(conversationId))
                {
                    configuration.Save();
                }

                return;
            }

            if (drafts.GetValueOrDefault(conversationId) == trimmed)
            {
                return;
            }

            drafts[conversationId] = trimmed;
            configuration.Save();
        }

        private void ClearDraft(string conversationId)
        {
            if (configuration.MessageDrafts.Remove(conversationId))
            {
                configuration.Save();
            }
        }

        protected override void BeginReply(string messageId)
        {
            var message = FindMessage(messageId);
            if (message is null || message.Kind == 2)
            {
                return;
            }

            var senderName = message.SenderId == MyUserId
                ? Loc.T(L.Message.You)
                : message.SenderDisplayName;
            composer.BeginReply(messageId, senderName, ChatText.QuotePreview(message.Body, message.Kind));
        }

        protected override ChatMenuModel BuildMenuModel()
        {
            return new ChatMenuModel
            {
                Ui = ui,
                ShowReactions = true,
                CanReply = true,
                CanForward = true,
                CanCopy = true,
                CanStar = true,
                CanEdit = true,
                CanInfo = true,
                CanDelete = true,
                CanReport = true,
                CanTranslate = true,
                IsStarred = app.IsStarred,
                MyReactionTo = store.MyReactionTo,
                OnReply = BeginReply,
                OnForward = id => app.router.Push(MessageRoute.Forward(id)),
                OnCopy = CopyMessage,
                OnStar = app.ToggleStar,
                OnEdit = BeginEdit,
                OnInfo = id =>
                {
                    app.store.RefreshDetail();
                    app.router.Push(MessageRoute.MessageInfo(id));
                },
                OnDelete = AskDeleteMessage,
                OnReport = OpenReportMessage,
                OnTranslate = TranslateMessage,
                OnReact = store.SetReaction,
            };
        }

        protected override void DrawAboveTranscript(ref Rect listRect, string threadId)
        {
            var conversation = app.store.Conversation;
            if (IsGroupThread || conversation is null || !app.store.HasRotationNotice(conversation.OtherUserId))
            {
                return;
            }

            var dismissUserId = conversation.OtherUserId;
            var text = Loc.T(L.Encryption.SafetyChanged, DirectMessagesStore.DisplayTitle(conversation));
            ChatHeaderControls.DrawBanner(ui, ref listRect, text, ui.MutedInk,
                () => app.store.ClearRotationNotice(dismissUserId));
        }

        protected override void DrawHeader(Rect area, string threadId)
        {
            var conversation = app.store.Conversation;
            var isGroup = conversation?.IsGroup ?? false;
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var header = app.PaintHeaderBand(area);
            var rowCenterY = header.Center.Y;
            var chipRadius = SocialChrome.BackChipRadius * scale;
            var chipCenter = new Vector2(area.Min.X + 12f * scale + chipRadius, rowCenterY);
            if (SocialChrome.DrawBackChip(drawList, chipCenter, chipRadius, app.ink))
            {
                BackAction();
            }

            var slots = 1;
            var callable = !isGroup && conversation is not null && app.calls.Enabled
                && app.contacts.Find(conversation.OtherUserId) is { IsMutual: true };
            if (callable)
            {
                slots = 2;
            }

            if (app.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 0), PhoneIcons.DotsVertical,
                    Loc.T(L.Message.MoreOptions)) && conversation is not null)
            {
                app.OpenThreadSheet(conversation);
            }

            if (callable && app.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 1), PhoneIcons.Phone,
                    Loc.T(L.Friends.Call)) && conversation is not null
                && app.contacts.Find(conversation.OtherUserId) is { } callTarget)
            {
                app.StartCall(callTarget);
            }

            var avatarRadius = ThreadHeaderAvatarRadius * scale;
            var avatarCenter = new Vector2(chipCenter.X + chipRadius + ThreadHeaderAvatarGap * scale + avatarRadius,
                rowCenterY);
            var name = conversation is null ? app.DisplayName : DirectMessagesStore.DisplayTitle(conversation);
            if (conversation is null)
            {
                app.DrawGroupAvatar(drawList, avatarCenter, avatarRadius, name, null);
            }
            else
            {
                app.DrawConversationAvatar(drawList, conversation, avatarCenter, avatarRadius);
            }

            var nameLeft = avatarCenter.X + avatarRadius + ThreadHeaderNameGap * scale;
            var nameRight = area.Max.X - (CellPadX + SocialChrome.HeaderReserve(slots)) * scale;
            var nameWidth = MathF.Max(1f, nameRight - nameLeft);
            var subtitle = conversation is null
                ? string.Empty
                : isGroup ? GroupSubtitle(conversation) : app.PresenceText(conversation);
            var subtitleInk = !isGroup && conversation is { Presence: 1 } ? app.ink.AccentLink : app.ink.MutedInk;
            var titleId = "messageapp.thread.title." + (conversation?.Id ?? "self");
            var nameHeight = Typography.LineHeight(ThreadNameStyle);
            if (subtitle.Length == 0)
            {
                var soloTop = rowCenterY - nameHeight * 0.5f;
                var soloHovering = UiInteract.Hover(new Vector2(nameLeft, soloTop),
                    new Vector2(nameRight, soloTop + nameHeight));
                Marquee.DrawLeft(drawList, titleId, name, nameLeft, soloTop, nameWidth, ThreadNameStyle,
                    app.ink.TitleInk, soloHovering);
            }
            else
            {
                var subHeight = Typography.LineHeight(ThreadSubStyle);
                var top = rowCenterY - (nameHeight + subHeight) * 0.5f;
                var hovering = UiInteract.Hover(new Vector2(nameLeft, top), new Vector2(nameRight, top + nameHeight));
                Marquee.DrawLeft(drawList, titleId, name, nameLeft, top, nameWidth, ThreadNameStyle, app.ink.TitleInk,
                    hovering);
                Typography.Draw(drawList, new Vector2(nameLeft, top + nameHeight),
                    Typography.FitText(subtitle, nameWidth, ThreadSubStyle), subtitleInk, ThreadSubStyle);
            }

            if (conversation is null)
            {
                return;
            }

            var hitMin = new Vector2(avatarCenter.X - avatarRadius, header.Min.Y);
            var hitMax = new Vector2(nameRight, header.Max.Y);
            if (!UiInteract.HoverClick(hitMin, hitMax))
            {
                return;
            }

            if (isGroup)
            {
                app.router.Push(MessageRoute.GroupInfo(conversation.Id));
            }
            else if (app.contacts.Find(conversation.OtherUserId) is not null)
            {
                app.router.Push(MessageRoute.Contact(conversation.OtherUserId));
            }
        }

        private string GroupSubtitle(ConversationDto conversation)
        {
            var members = app.store.Members;
            if (members.Length == 0)
            {
                return Loc.T(L.DirectMessages.MembersCount, conversation.MemberCount);
            }

            if (ReferenceEquals(members, memberLineSource))
            {
                return memberLine;
            }

            memberLineSource = members;
            var builder = new System.Text.StringBuilder(64);
            var myId = MyUserId;
            for (var index = 0; index < members.Length; index++)
            {
                if (!members[index].IsActive)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(members[index].UserId == myId ? Loc.T(L.Message.You) : MemberLabel(members[index]));
            }

            memberLine = builder.ToString();
            return memberLine;
        }

        protected override TranscriptMessage[] MapTranscript(ChatMessageDto[] source)
        {
            var isGroup = IsGroupThread;
            var mapped = new TranscriptMessage[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                var message = source[index];
                if (message.Kind == 2)
                {
                    mapped[index] = new TranscriptMessage(message.Id, message.SenderId, SystemText(message), 2,
                        message.CreatedAtUnix, 0, 0, null, string.Empty, default);
                    continue;
                }

                var senderName = isGroup ? message.SenderDisplayName : string.Empty;
                var tint = isGroup ? SenderTint.Of(message.SenderDisplayName) : default;
                if (message.Deleted)
                {
                    mapped[index] = new TranscriptMessage(message.Id, message.SenderId,
                        Loc.T(L.Message.DeletedBody), 0, message.CreatedAtUnix, 0, 0, null, senderName, tint,
                        TranscriptFlags.Deleted);
                    continue;
                }

                var replySender = string.Empty;
                var replyBody = string.Empty;
                var replyKind = message.ReplyKind;
                if (message.ReplyToId is not null)
                {
                    replySender = message.ReplySenderId == MyUserId
                        ? Loc.T(L.Message.You)
                        : message.ReplySenderName ?? Loc.T(L.Message.OriginalUnavailable);
                    replyKind = ChatText.EffectiveKind(message.ReplyBody, replyKind);
                    replyBody = ChatText.QuotePreview(message.ReplyBody, replyKind);
                }

                TranscriptReaction[]? reactions = null;
                var summaries = message.Reactions;
                if (summaries is { Length: > 0 })
                {
                    reactions = new TranscriptReaction[summaries.Length];
                    for (var summaryIndex = 0; summaryIndex < summaries.Length; summaryIndex++)
                    {
                        reactions[summaryIndex] = new TranscriptReaction(summaries[summaryIndex].Token,
                            summaries[summaryIndex].Count, summaries[summaryIndex].Mine);
                    }
                }

                mapped[index] = new TranscriptMessage(message.Id, message.SenderId, message.Body, message.Kind,
                    message.CreatedAtUnix, message.MediaWidth, message.MediaHeight, message.ReadAtUnix, senderName,
                    tint, MessageFlags(message), message.ReplyToId, replySender, replyBody, replyKind,
                    message.DurationSecs, reactions, message.SenderBadges, message.SenderBadgeIds);
            }

            return mapped;
        }

        private byte MessageFlags(ChatMessageDto message)
        {
            byte flags = 0;
            if (message.Forwarded)
            {
                flags |= TranscriptFlags.Forwarded;
            }

            if (message.EditedAtUnix is not null)
            {
                flags |= TranscriptFlags.Edited;
            }

            if (message.EncVersion == 0)
            {
                return flags;
            }

            var state = store.DecryptionState(message.Id);
            flags |= TranscriptFlags.Encrypted;
            if (state.IsPlaceholder)
            {
                flags |= TranscriptFlags.Placeholder;
            }
            else if (state.State == Aetherphone.Core.Crypto.DmBodyState.Decrypted && !state.Verified)
            {
                flags |= TranscriptFlags.Unverified;
            }

            return flags;
        }

        private static string SystemText(ChatMessageDto message)
        {
            var actor = message.SenderDisplayName;
            var body = message.Body ?? string.Empty;
            var separator = (char)0x1F;
            var separatorIndex = body.IndexOf(separator);
            var token = separatorIndex >= 0 ? body.Substring(0, separatorIndex) : body;
            var argument = separatorIndex >= 0 ? body.Substring(separatorIndex + 1) : string.Empty;
            return token switch
            {
                "created" => Loc.T(L.DirectMessages.SysCreated, actor),
                "added" => Loc.T(L.DirectMessages.SysAdded, actor, argument),
                "removed" => Loc.T(L.DirectMessages.SysRemoved, actor, argument),
                "left" => Loc.T(L.DirectMessages.SysLeft, actor),
                "renamed" => Loc.T(L.DirectMessages.SysRenamed, actor, argument),
                "promoted" => Loc.T(L.DirectMessages.SysPromoted, actor, argument),
                "demoted" => Loc.T(L.DirectMessages.SysDemoted, actor, argument),
                "photo" => Loc.T(L.DirectMessages.SysPhoto, actor),
                "description" => Loc.T(L.DirectMessages.SysDescription, actor),
                _ => body,
            };
        }
    }

    private Rect PaintHeaderBand(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var band = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale));
        drawList.AddRectFilled(band.Min, band.Max, ImGui.GetColorU32(MessageThemes.TopBar));
        drawList.AddLine(new Vector2(band.Min.X, band.Max.Y), band.Max, ImGui.GetColorU32(ui.Hairline), 1f);
        return band;
    }

    private void OpenThreadSheet(ConversationDto conversation)
    {
        threadSheetConversationId = conversation.Id;
        threadSheetTitle = DirectMessagesStore.DisplayTitle(conversation);
        var count = 0;
        var isGroup = conversation.IsGroup;
        if (isGroup || contacts.Find(conversation.OtherUserId) is not null)
        {
            threadSheetItems[count] = new ActionSheet.Item(Loc.T(isGroup ? L.Message.GroupInfo : L.Message.ContactInfo),
                isGroup ? PhoneIcons.Users : PhoneIcons.UserCircle);
            threadSheetActions[count++] = ThreadActInfo;
        }

        threadSheetItems[count] = new ActionSheet.Item(Loc.T(L.Common.Search), PhoneIcons.Search,
            Selected: threadView.SearchOpen);
        threadSheetActions[count++] = ThreadActSearch;
        if (threadView.CanTranslate)
        {
            var translating = threadView.TranslatingThread(conversation.Id);
            threadSheetItems[count] = new ActionSheet.Item(
                Loc.T(translating ? L.Translate.ChatOn : L.Translate.ChatToggle), PhoneIcons.Language,
                Selected: translating);
            threadSheetActions[count++] = ThreadActTranslate;
        }

        threadSheetItems[count] = new ActionSheet.Item(
            Loc.T(conversation.Muted ? L.Message.UnmuteAction : L.Message.MuteAction),
            conversation.Muted ? PhoneIcons.Bell : PhoneIcons.BellOff);
        threadSheetActions[count++] = ThreadActMute;
        threadSheetItems[count] = new ActionSheet.Item(Loc.T(L.Message.Wallpaper), PhoneIcons.Wallpaper);
        threadSheetActions[count++] = ThreadActWallpaper;
        threadSheetItems[count] = new ActionSheet.Item(Loc.T(L.Encryption.InfoTitle),
            store.EncryptingCurrent ? PhoneIcons.Lock : PhoneIcons.LockOpen);
        threadSheetActions[count++] = ThreadActEncryption;
        if (StarredCountIn(conversation.Id) > 0)
        {
            threadSheetItems[count] = new ActionSheet.Item(Loc.T(L.Message.StarredTitle), PhoneIcons.Star);
            threadSheetActions[count++] = ThreadActStarred;
        }

        threadSheetItems[count] = new ActionSheet.Item(Loc.T(L.Message.DeleteConversation), PhoneIcons.Trash, true);
        threadSheetActions[count++] = ThreadActDelete;
        threadSheetCount = count;
        threadSheet.Open();
    }

    private void DrawThreadSheet(Rect screen)
    {
        if (!threadSheet.CapturesPointer)
        {
            return;
        }

        var picked = threadSheet.Draw(screen, ActionSheetStyle.From(ui), threadSheetItems.AsSpan(0, threadSheetCount),
            Loc.T(L.Common.Cancel), false, threadSheetTitle);
        if (picked < 0 || threadSheetConversationId is not { } conversationId)
        {
            return;
        }

        var conversation = store.Conversation;
        switch (threadSheetActions[picked])
        {
            case ThreadActInfo:
                if (conversation is { IsGroup: true })
                {
                    router.Push(MessageRoute.GroupInfo(conversationId));
                }
                else if (conversation is not null)
                {
                    router.Push(MessageRoute.Contact(conversation.OtherUserId));
                }

                break;
            case ThreadActSearch:
                threadView.ToggleSearch();
                break;
            case ThreadActTranslate:
                threadView.ToggleTranslation(conversationId);
                break;
            case ThreadActMute:
                store.SetMuted(conversationId, !(conversation?.Muted ?? false), _ => { });
                break;
            case ThreadActWallpaper:
                router.Push(MessageRoute.ChatWallpaper(conversationId));
                break;
            case ThreadActEncryption:
                router.Push(MessageRoute.Encryption(conversationId));
                break;
            case ThreadActStarred:
                router.Push(MessageRoute.StarredIn(conversationId));
                break;
            case ThreadActDelete:
                AskDeleteConversation(conversationId);
                break;
        }
    }

    private bool IsStarred(string messageId)
    {
        var starred = configuration.MessageStarredMessages;
        for (var index = 0; index < starred.Count; index++)
        {
            if (starred[index].MessageId == messageId)
            {
                return true;
            }
        }

        return false;
    }

    private int StarredCountIn(string conversationId)
    {
        var starred = configuration.MessageStarredMessages;
        var count = 0;
        for (var index = 0; index < starred.Count; index++)
        {
            if (starred[index].ConversationId == conversationId)
            {
                count++;
            }
        }

        return count;
    }

    private void ToggleStar(string messageId)
    {
        var starred = configuration.MessageStarredMessages;
        for (var index = 0; index < starred.Count; index++)
        {
            if (starred[index].MessageId == messageId)
            {
                starred.RemoveAt(index);
                configuration.Save();
                return;
            }
        }

        var message = store.FindMessage(messageId);
        var conversation = store.Conversation;
        if (message is null || message.Deleted || conversation is null)
        {
            return;
        }

        starred.Add(new StarredMessage
        {
            ConversationId = conversation.Id,
            MessageId = messageId,
            ConversationTitle = DirectMessagesStore.DisplayTitle(conversation),
            SenderName = message.SenderId == store.MyUserId ? Loc.T(L.Message.You) : message.SenderDisplayName,
            Preview = ChatText.QuotePreview(message.Body, message.Kind),
            Kind = ChatText.EffectiveKind(message.Body, message.Kind),
            CreatedAtUnix = message.CreatedAtUnix,
            StarredAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
        configuration.Save();
    }

    private string PresenceText(ConversationDto? conversation)
    {
        if (conversation is null)
        {
            return string.Empty;
        }

        if (conversation.Presence == 1)
        {
            return Loc.T(L.Message.PresenceOnline);
        }

        if (conversation.LastSeenAtUnix is { } lastSeen)
        {
            return Loc.T(L.Message.PresenceLastSeen, FormatStamp(lastSeen));
        }

        return string.Empty;
    }
}
