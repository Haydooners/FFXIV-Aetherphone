using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float MessagesSearchHeight = 52f;
    private const float MessagesHeadingHeight = 44f;

    private static readonly TextStyle MessagesHeadingStyle = TextStyles.Title3;
    private static readonly TextStyle MessagesLinkStyle = TextStyles.SubheadlineEmphasized;

    private readonly List<VelvetThreadDto> chatsFiltered = new();
    private VelvetThreadDto[] chatsFilterSource = Array.Empty<VelvetThreadDto>();
    private string chatsFilterQuery = string.Empty;
    private string chatsDraft = string.Empty;
    private string introName = string.Empty;
    private string introText = string.Empty;

    private void DrawMessages(Rect area)
    {
        var scale = UiScale.Current;
        var pad = SocialChrome.CellPadX * scale;
        using (AppSurface.BeginEdgeToEdge(area))
        {
            var width = ScrollLayout.StableContentWidth();
            if (messagesTab == VelvetMessagesTab.Chats)
            {
                var searchOrigin = ImGui.GetCursorScreenPos();
                SearchField.Draw(new Rect(new Vector2(searchOrigin.X + pad, searchOrigin.Y),
                        new Vector2(searchOrigin.X + width - pad, searchOrigin.Y + MessagesSearchHeight * scale)),
                    "##velvetChatSearch", Loc.T(L.Common.Search), ref chatsDraft, VelvetTheme.Palette);
                ImGui.SetCursorScreenPos(searchOrigin);
                ImGui.Dummy(new Vector2(width, MessagesSearchHeight * scale));
            }

            DrawMessagesHeading(width, pad);
            if (messagesTab == VelvetMessagesTab.Chats)
            {
                DrawChatsList(area);
            }
            else
            {
                DrawRequestsList(area);
            }
        }
    }

    private void DrawMessagesHeading(float width, float pad)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = MessagesHeadingHeight * scale;
        var centerY = origin.Y + height * 0.5f;
        var showingRequests = messagesTab == VelvetMessagesTab.Requests;
        var heading = Loc.T(showingRequests ? L.Velvet.Requests : L.Velvet.ChatsTab);
        var requestCount = store.RequestCount;
        var link = showingRequests
            ? Loc.T(L.Velvet.ChatsTab)
            : requestCount > 0 ? Loc.T(L.Velvet.RequestsCount, requestCount) : Loc.T(L.Velvet.Requests);
        var linkSize = Typography.Measure(link, MessagesLinkStyle);
        var linkMin = new Vector2(origin.X + width - pad - linkSize.X, centerY - linkSize.Y * 0.5f);
        var linkMax = new Vector2(origin.X + width - pad, centerY + linkSize.Y * 0.5f);
        var headingHeight = Typography.LineHeight(MessagesHeadingStyle);
        Typography.Draw(drawList, new Vector2(origin.X + pad, centerY - headingHeight * 0.5f),
            Typography.FitText(heading, MathF.Max(1f, linkMin.X - 12f * scale - origin.X - pad),
                MessagesHeadingStyle), VelvetTheme.TitleInk, MessagesHeadingStyle);
        var hovered = UiInteract.Hover(linkMin, linkMax);
        Typography.Draw(drawList, linkMin, link, VelvetTheme.RoseInk, MessagesLinkStyle);
        if (hovered)
        {
            drawList.AddLine(new Vector2(linkMin.X, linkMax.Y), linkMax, ImGui.GetColorU32(VelvetTheme.RoseInk), 1f);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(linkMin, linkMax, hovered))
        {
            messagesTab = showingRequests ? VelvetMessagesTab.Chats : VelvetMessagesTab.Requests;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void RefreshChatsFilter(VelvetThreadDto[] threads)
    {
        var query = chatsDraft.Trim();
        if (ReferenceEquals(threads, chatsFilterSource) &&
            string.Equals(query, chatsFilterQuery, StringComparison.Ordinal))
        {
            return;
        }

        chatsFilterSource = threads;
        chatsFilterQuery = query;
        chatsFiltered.Clear();
        for (var index = 0; index < threads.Length; index++)
        {
            var thread = threads[index];
            if (query.Length == 0 || ChatRowMatches(thread, query))
            {
                chatsFiltered.Add(thread);
            }
        }
    }

    private static bool ChatRowMatches(VelvetThreadDto thread, string query) =>
        thread.OtherDisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || thread.OtherHandle.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void DrawChatsList(Rect listRect)
    {
        var scale = UiScale.Current;
        if (!store.ThreadsLoaded && !store.LoadingThreads)
        {
            store.RefreshThreads();
        }

        if (!store.ConnectionsLoaded && !store.LoadingConnections)
        {
            store.RefreshConnections();
        }

        var threads = store.Threads;
        RefreshChatsFilter(threads);
        if (chatsFiltered.Count == 0)
        {
            var empty = new Rect(new Vector2(listRect.Min.X, ImGui.GetCursorScreenPos().Y), listRect.Max);
            if (threads.Length > 0)
            {
                DrawEmpty(empty, Loc.T(L.Social.ListEmpty), string.Empty);
            }
            else
            {
                DrawEmpty(empty, Loc.T(L.Velvet.MessagesEmpty), Loc.T(L.Velvet.MessagesEmptyHint));
            }

            return;
        }

        Gap(2f);
        for (var index = 0; index < chatsFiltered.Count; index++)
        {
            var thread = chatsFiltered[index];
            var preview = string.IsNullOrEmpty(thread.LastMessagePreview)
                ? Loc.T(L.Velvet.ThreadEmpty)
                : ChatText.ListPreview(thread.LastMessagePreview);
            var model = new VRowModel
            {
                Title = DisplayNameOf(thread.OtherDisplayName, thread.OtherHandle),
                Subtitle = preview,
                Height = 64f,
                Leading = VRowLeading.Avatar,
                AvatarRadius = 22f,
                Name = DisplayNameOf(thread.OtherDisplayName, thread.OtherHandle),
                World = string.Empty,
                AvatarUrl = thread.OtherAvatarUrl,
                Presence = thread.Presence,
                Time = TimeText.Short(thread.LastMessageAtUnix),
                Badge = thread.UnreadCount,
            };
            var hit = VRow.Cell(in model, ui, theme, images, lodestone);
            if (hit == VRowHit.Body)
            {
                OpenThread(thread.OtherUserId);
            }
            else if (hit == VRowHit.Overflow)
            {
                OpenThreadSheet(thread.OtherUserId);
            }
        }

        if (store.LoadingMoreThreads)
        {
            InfiniteScroll.DrawLoadingRow(listRect.Center.X, VelvetTheme.MutedInk);
        }
        else if (store.HasMoreThreads && InfiniteScroll.ReachedBottom())
        {
            store.LoadMoreThreads();
        }

        Gap(40f);
    }

    private void DrawRequestsList(Rect listRect)
    {
        var scale = UiScale.Current;
        if (!store.RequestsLoaded && !store.LoadingRequests)
        {
            store.RefreshRequests();
        }

        if (!store.SentRequestsLoaded && !store.LoadingSentRequests)
        {
            store.RefreshSentRequests();
        }

        var requests = store.Requests;
        var sent = store.SentRequests;
        if (requests.Length == 0 && sent.Length == 0)
        {
            DrawEmpty(new Rect(new Vector2(listRect.Min.X, ImGui.GetCursorScreenPos().Y), listRect.Max),
                Loc.T(L.Velvet.RequestsEmpty), Loc.T(L.Velvet.RequestsEmptyHint));
            return;
        }

        Gap(4f);
        if (requests.Length > 0)
        {
            VSectionHeader.Overline(Loc.T(L.Velvet.Requests), requests.Length.ToString(Loc.Culture),
                FeedCell.PadX * scale);
            for (var index = 0; index < requests.Length; index++)
            {
                DrawRequestRow(requests[index]);
            }
        }

        if (sent.Length > 0)
        {
            Gap(14f);
            VSectionHeader.Overline(Loc.T(L.Velvet.SentRequests), sent.Length.ToString(Loc.Culture),
                FeedCell.PadX * scale);
            for (var index = 0; index < sent.Length; index++)
            {
                var request = sent[index];
                var model = new VRowModel
                {
                    Title = DisplayNameOf(request.DisplayName, request.Handle),
                    Subtitle = "@" + request.Handle,
                    Height = 60f,
                    Leading = VRowLeading.Avatar,
                    AvatarRadius = 20f,
                    Name = DisplayNameOf(request.DisplayName, request.Handle),
                    AvatarUrl = request.AvatarUrl,
                    Pill = Loc.T(L.Velvet.Requested),
                    PillFilled = false,
                    PillEnabled = true,
                };
                var hit = VRow.Cell(in model, ui, theme, images, lodestone);
                if (hit == VRowHit.Pill)
                {
                    store.CancelRequest(request.UserId);
                }
                else if (hit == VRowHit.Body)
                {
                    OpenThread(request.UserId);
                }
            }
        }

        Gap(40f);
    }

    private void DrawRequestRow(VelvetConnectionDto request)
    {
        var model = new VRowModel
        {
            Title = DisplayNameOf(request.DisplayName, request.Handle),
            Subtitle = IntroLineOf(request),
            Height = 64f,
            Leading = VRowLeading.Avatar,
            AvatarRadius = 22f,
            Name = DisplayNameOf(request.DisplayName, request.Handle),
            AvatarUrl = request.AvatarUrl,
            Pill = Loc.T(L.Velvet.Accept),
            PillFilled = true,
            PillEnabled = true,
            Decline = true,
        };
        var hit = VRow.Cell(in model, ui, theme, images, lodestone);
        switch (hit)
        {
            case VRowHit.Pill:
                store.AcceptRequest(request.UserId);
                OpenThread(request.UserId);
                break;
            case VRowHit.Decline:
                store.DeclineRequest(request.UserId);
                break;
            case VRowHit.Body:
                OpenRequest(request.UserId);
                break;
        }
    }

    private void OpenThreadSheet(string otherId)
    {
        sheetThreadId = otherId;
        threadSheetItems[0] = new ActionSheet.Item(Loc.T(L.Velvet.DeleteConversation), string.Empty, true);
        threadSheet.Open();
    }

    private void DrawThreadSheet(Rect screen)
    {
        if (!threadSheet.CapturesPointer)
        {
            return;
        }

        var picked = threadSheet.Draw(screen, ActionSheetStyle.From(ui), threadSheetItems, Loc.T(L.Common.Cancel),
            false);
        if (picked == 0 && sheetThreadId is { } otherId)
        {
            AskDeleteConversation(otherId);
        }
    }

    private void AskDeleteConversation(string otherId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Velvet.DeleteConversation),
            Message = Loc.T(L.Velvet.DeleteConversationMessage),
            ConfirmLabel = Loc.T(L.Velvet.DeleteConfirm),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            Sheet = true,
            Danger = true,
            Confirm = () => DeleteConversation(otherId),
        });
    }

    private void DeleteConversation(string otherId)
    {
        var current = router.Current;
        var threadOpen = current.Screen == VelvetScreenId.Thread && current.Arg == otherId;
        store.DeleteThread(otherId);
        if (threadOpen)
        {
            router.Pop();
        }
    }

    private void OpenRequest(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        router.Push(VelvetView.RequestDetail(userId));
    }

    private VelvetConnectionDto? FindRequest(string userId)
    {
        var requests = store.Requests;
        for (var index = 0; index < requests.Length; index++)
        {
            if (requests[index].UserId == userId)
            {
                return requests[index];
            }
        }

        return null;
    }

    private string ResolveRequestIntro(VelvetConnectionDto request)
    {
        if (store.CurrentThreadId == request.UserId)
        {
            var messages = store.Messages;
            for (var index = 0; index < messages.Length; index++)
            {
                var message = messages[index];
                if (message.Deleted || message.Kind != 0 || message.Body.Trim().Length == 0)
                {
                    continue;
                }

                if (message.EncVersion != 0 && store.DecryptionState(message.Id).IsPlaceholder)
                {
                    continue;
                }

                return ChatText.ListPreview(message.Body);
            }
        }

        return string.IsNullOrWhiteSpace(request.Intro) ? Loc.T(L.Velvet.WantsToConnect) : request.Intro;
    }

    private void DrawRequestDetail(Rect area, string userId)
    {
        var request = FindRequest(userId);
        var name = request is { } found ? DisplayNameOf(found.DisplayName, found.Handle) : Loc.T(L.Velvet.Requests);
        if (VHeader.Push(area, name))
        {
            router.Pop();
            return;
        }

        if (request is not { } req)
        {
            router.Pop();
            return;
        }

        if (store.CurrentThreadId != userId)
        {
            store.OpenThread(userId);
        }

        var introText = ResolveRequestIntro(req);
        var scale = UiScale.Current;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            Gap(26f);
            var width = ImGui.GetContentRegionAvail().X;
            var drawList = ImGui.GetWindowDrawList();
            var top = ImGui.GetCursorScreenPos();
            var centerX = top.X + width * 0.5f;
            var radius = 46f * scale;
            var avatarCenter = new Vector2(centerX, top.Y + radius);
            VAvatar.Draw(drawList, avatarCenter, radius, theme, name, string.Empty, req.AvatarUrl, images, lodestone, -1,
                VelvetTheme.Moonlight);

            var nameY = avatarCenter.Y + radius + 14f * scale;
            Typography.DrawCentered(new Vector2(centerX, nameY), name, VelvetTheme.TitleInk, TextStyles.Title2);
            var lineBottom = nameY + Typography.Measure(name, TextStyles.Title2).Y;
            if (req.Handle.Length > 0)
            {
                lineBottom += 6f * scale;
                var handle = "@" + req.Handle;
                Typography.DrawCentered(new Vector2(centerX, lineBottom), handle, VelvetTheme.MutedInk,
                    TextStyles.Subheadline);
                lineBottom += Typography.Measure(handle, TextStyles.Subheadline).Y;
            }

            ImGui.SetCursorScreenPos(top);
            ImGui.Dummy(new Vector2(width, lineBottom - top.Y + 22f * scale));
            if (UiInteract.HoverClick(new Vector2(avatarCenter.X - radius, avatarCenter.Y - radius),
                    new Vector2(avatarCenter.X + radius, avatarCenter.Y + radius)))
            {
                OpenProfile(userId);
            }

            var pad = 14f * scale;
            var innerWidth = width - pad * 2f;
            var textSize = Typography.MeasureWrappedBlock(introText, TextStyles.Body, innerWidth);
            var cardHeight = textSize.Y + pad * 2f;
            var cardOrigin = ImGui.GetCursorScreenPos();
            Squircle.Fill(drawList, cardOrigin, new Vector2(cardOrigin.X + width, cardOrigin.Y + cardHeight),
                Metrics.Radius.Md * scale, VelvetTheme.Alpha(VelvetTheme.TitleInk, 0.06f).Packed());
            Typography.DrawWrappedLeft(new Vector2(cardOrigin.X + pad, cardOrigin.Y + pad), introText,
                VelvetTheme.BodyInk, TextStyles.Body, innerWidth);
            ImGui.SetCursorScreenPos(cardOrigin);
            ImGui.Dummy(new Vector2(width, cardHeight));

            Gap(26f);
            if (ui.PillButton(Reserve(48f), Loc.T(L.Velvet.Accept), true))
            {
                store.AcceptRequest(userId);
                router.Pop(false);
                OpenThread(userId);
            }

            Gap(10f);
            if (ui.GhostButton(Reserve(44f), Loc.T(L.Phone.Decline)))
            {
                store.DeclineRequest(userId);
                router.Pop();
            }

            Gap(6f);
            if (ui.GhostButton(Reserve(42f), Loc.T(L.Social.ViewProfile)))
            {
                OpenProfile(userId);
            }

            Gap(30f);
        }
    }

    private void RequestIntro(string userId, string displayName)
    {
        introName = displayName;
        introText = string.Empty;
        router.Push(VelvetView.Intro(userId));
    }

    private void DrawIntro(Rect area, string userId)
    {
        var scale = UiScale.Current;
        if (VHeader.Push(area, Loc.T(L.Velvet.IntroTitle)))
        {
            router.Pop();
            return;
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            Gap(30f);
            var drawList = ImGui.GetWindowDrawList();
            var width = ImGui.GetContentRegionAvail().X;
            var centerX = ImGui.GetCursorScreenPos().X + width * 0.5f;
            var moonY = ImGui.GetCursorScreenPos().Y + 22f * scale;
            VelvetArt.Moon(drawList, new Vector2(centerX, moonY), 18f * scale, VelvetTheme.Moonlight,
                VelvetTheme.GroundTop);
            Gap(80f);
            Typography.DrawWrappedCentered(new Vector2(centerX, ImGui.GetCursorScreenPos().Y),
                Loc.T(L.Velvet.IntroduceYourselfTo, introName), VelvetTheme.TitleInk, TextStyles.Title3,
                width - 48f * scale);
            Gap(50f);

            ui.Field(Loc.T(L.Velvet.YourIntro), "##introText", ref introText, 140, true);
            ui.HelpText(Loc.T(L.Velvet.IntroSheetHint));
            Gap(16f);

            var sendRect = Reserve(46f);
            var canSend = introText.Trim().Length > 0;
            if (canSend)
            {
                if (ui.PillButton(sendRect, Loc.T(L.Velvet.SendIntro), true))
                {
                    SendIntro(userId);
                }
            }
            else
            {
                Squircle.Fill(drawList, sendRect.Min, sendRect.Max, sendRect.Height * 0.5f,
                    VelvetTheme.Alpha(VelvetTheme.Rose, 0.35f).Packed());
                Typography.DrawCentered(sendRect.Center, Loc.T(L.Velvet.SendIntro),
                    VelvetTheme.Alpha(VelvetTheme.OnAccent, 0.6f),
                    0.9f, FontWeight.SemiBold);
            }

            Gap(40f);
        }
    }

    private void SendIntro(string userId)
    {
        store.SendIntro(userId, introText.Trim(), _ => { });
        introText = string.Empty;
        router.Pop();
    }

    private static string IntroLineOf(VelvetConnectionDto request) =>
        string.IsNullOrWhiteSpace(request.Intro) ? Loc.T(L.Velvet.WantsToConnect) : request.Intro;
}
