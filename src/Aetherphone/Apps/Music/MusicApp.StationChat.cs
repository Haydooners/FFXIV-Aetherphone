using Aetherphone.Apps.Music.Radio.Live;
using Aetherphone.Core;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float ComposerBaseHeight = 54f;
    private const float ComposerFieldInsetY = 9f;
    private const int ComposerMaxLines = 3;
    private const float ComposerButtonRadius = 17f;
    private const float ChatAvatarRadius = 13f;
    private const float ChatRowPadY = 6f;
    private const float ChatRowGap = 9f;
    private const float ChatBodyGap = 2f;
    private const float FollowTolerance = 6f;
    private const float JumpScroll = 10_000_000f;
    private const double LongPressSeconds = 0.45;
    private const float LongPressSlop = 6f;
    private const float ReactionRise = 190f;
    private const float ReactionSize = 22f;
    private const float ReactionLaneSpacing = 12f;
    private const float ReactionSwayWidth = 14f;
    private const float ReactionMineScale = 1.15f;
    private const float ReactionTraySlot = 38f;
    private const float ReactionTrayHeight = 46f;
    private const float JumpPillHeight = 30f;
    private const float BadgePadX = 5f;
    private const float OwnRowAlpha = 0.10f;
    private const float PinnedTintAlpha = 0.14f;
    private const int CounterWarning = 10;

    private static readonly TextStyle ChatBodyStyle = TextStyles.Subheadline;
    private static readonly TextStyle ChatNameStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle BadgeStyle = new(0.66f, FontWeight.Bold);

    private static readonly string[] ReactionShortcodes =
    {
        "heart", "fire", "clap", "joy", "notes", "star_struck", "dancer", "raised_hands",
    };

    private readonly RadioChatFollow chatFollow = new();
    private readonly SoftWrapEditor chatEditor = new();
    private readonly Dictionary<long, RadioChatRow> chatRowsById = new();
    private readonly RadioChatRow?[] chatRows = new RadioChatRow?[RadioRoomSession.MessageCapacity];
    private readonly List<long> staleChatRows = new();
    private readonly string[] reactionFiles = new string[RadioRoomSession.ReactionKinds];
    private readonly RadioCountLabel newMessagesLabel = new();
    private readonly RadioCountLabel counterLabel = new();
    private readonly RadioCountLabel durationLabel = new();
    private readonly RadioFittedText composerLockFit = new();
    private readonly RadioFittedText jumpPillFit = new();
    private readonly RadioWrappedText pinnedText = new();
    private int chatRowCount;
    private int chatRowsVersion = -1;
    private int chatRowStamp;
    private bool reactionFilesReady;
    private bool reactionTrayOpen;
    private long pressedMessageId = -1;
    private double pressedAt;
    private Vector2 pressedOrigin;
    private LanguageInfo? muteLanguage;
    private string muteDuration = string.Empty;
    private string muteText = string.Empty;

    private RadioComposerLock ChatLock()
    {
        var status = string.Equals(room.StationId, roomStationId, StringComparison.Ordinal)
            ? room.Status
            : RadioRoomStatus.Attaching;
        return RadioLiveRules.Evaluate(session.IsBanned, session.IsSignedIn, status, room.IsMuted(),
            room.CanModerate);
    }

    private void DrawStationChat(Rect panel, float scale)
    {
        SyncChatRows();
        var lockState = ChatLock();
        var growth = lockState == RadioComposerLock.None ? chatEditor.Growth(ComposerMaxLines) : 0f;
        var composerHeight = ComposerBaseHeight * scale + growth;
        var composer = new Rect(new Vector2(panel.Min.X, panel.Max.Y - composerHeight), panel.Max);
        var top = panel.Min.Y + DrawPinnedNotice(panel, scale);
        var list = new Rect(new Vector2(panel.Min.X, top), new Vector2(panel.Max.X, composer.Min.Y));
        DrawChatTranscript(list, lockState, scale);
        DrawFloatingReactions(list, scale);
        DrawJumpPill(list, scale);
        DrawChatComposer(composer, lockState, scale);
    }

    private void SyncChatRows()
    {
        if (chatRowsVersion == room.Version)
        {
            return;
        }

        chatRowsVersion = room.Version;
        chatRowStamp++;
        chatRowCount = room.MessageCount;
        for (var index = 0; index < chatRowCount; index++)
        {
            var entry = room.MessageAt(index);
            if (!chatRowsById.TryGetValue(entry.MessageId, out var row))
            {
                row = new RadioChatRow();
                chatRowsById[entry.MessageId] = row;
            }

            if (!ReferenceEquals(row.Entry, entry))
            {
                row.Assign(entry, NameInkFor(entry));
            }

            row.Stamp = chatRowStamp;
            chatRows[index] = row;
        }

        for (var index = chatRowCount; index < chatRows.Length; index++)
        {
            chatRows[index] = null;
        }

        if (chatRowsById.Count <= chatRowCount)
        {
            return;
        }

        staleChatRows.Clear();
        foreach (var pair in chatRowsById)
        {
            if (pair.Value.Stamp != chatRowStamp)
            {
                staleChatRows.Add(pair.Key);
            }
        }

        for (var index = 0; index < staleChatRows.Count; index++)
        {
            chatRowsById.Remove(staleChatRows[index]);
        }
    }

    private Vector4 NameInkFor(RadioChatEntry entry)
    {
        if (entry.IsDj)
        {
            return ui.Accent;
        }

        return entry.IsMine ? ui.TitleInk : SenderTint.Of(entry.DisplayName);
    }

    private int CountNewerThan(long messageId)
    {
        var count = 0;
        for (var index = chatRowCount - 1; index >= 0; index--)
        {
            if (chatRows[index]!.Entry.MessageId <= messageId)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private float DrawPinnedNotice(Rect panel, float scale)
    {
        if (room.Pinned is not { Length: > 0 } pinned)
        {
            return 0f;
        }

        var inset = Metrics.Space.Lg * scale;
        var pad = Metrics.Space.Md * scale;
        var min = new Vector2(panel.Min.X + inset, panel.Min.Y + Metrics.Space.Xs * scale);
        var right = panel.Max.X - inset;
        var closeReserve = room.CanModerate ? 26f * scale : 0f;
        var textLeft = min.X + pad + 20f * scale;
        var textWidth = MathF.Max(1f, right - pad - closeReserve - textLeft);
        pinnedText.Wrap(pinned, textWidth, ChatBodyStyle);
        var captionHeight = Typography.LineHeight(TextStyles.Caption2);
        var height = pad * 2f + captionHeight + Metrics.Space.Xxs * scale + pinnedText.Height;
        var max = new Vector2(right, min.Y + height);
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, min, max, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, PinnedTintAlpha)));
        Squircle.Stroke(drawList, min, max, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, PinnedTintAlpha * 2f)), Metrics.Stroke.Hairline);
        AppSkin.Icon(drawList, new Vector2(min.X + pad + 7f * scale, min.Y + pad + captionHeight * 0.5f),
            IconGlyph.Of(FontAwesomeIcon.Thumbtack), ui.Accent, 0.7f);
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + pad), Loc.T(L.Music.Live.PinnedByHost), ui.Accent,
            TextStyles.Caption2);
        pinnedText.Draw(drawList, new Vector2(textLeft, min.Y + pad + captionHeight + Metrics.Space.Xxs * scale),
            ui.TitleInk, ChatBodyStyle);

        if (room.CanModerate)
        {
            var closeCenter = new Vector2(right - pad - 6f * scale, min.Y + pad + captionHeight * 0.5f);
            if (ui.IconButton(closeCenter, 11f * scale, IconGlyph.Of(FontAwesomeIcon.Times), ui.MutedInk,
                    AppSkin.Transparent, 0.7f, Loc.T(L.Music.Live.Unpin)))
            {
                room.Unpin();
            }
        }

        return height + Metrics.Space.Sm * scale;
    }

    private void DrawChatTranscript(Rect list, RadioComposerLock lockState, float scale)
    {
        if (chatRowCount == 0)
        {
            DrawChatEmpty(list, lockState, scale);
            return;
        }

        ImGui.PushID("radio.chat");
        using (AppSurface.ReserveBottom(0f))
        {
            using var surface = AppSurface.Begin(list);
            var tailId = chatRows[chatRowCount - 1]!.Entry.MessageId;
            var newer = CountNewerThan(chatFollow.SeenTailId);
            if (chatFollow.Update(ImGui.GetScrollY(), ImGui.GetScrollMaxY(), FollowTolerance * scale, list.Height,
                    tailId, newer))
            {
                surface.JumpTo(JumpScroll);
            }

            var width = ScrollLayout.StableContentWidth();
            for (var index = 0; index < chatRowCount; index++)
            {
                DrawChatRow(chatRows[index]!, width, scale);
            }
        }

        ImGui.PopID();
    }

    private void DrawChatEmpty(Rect list, RadioComposerLock lockState, float scale)
    {
        switch (lockState)
        {
            case RadioComposerLock.SignedOut:
                EmptyState.Draw(list, ui, FontAwesomeIcon.UserSlash, Loc.T(L.Music.Live.LockSignedOut),
                    Loc.T(L.Music.StationSignedOutSub));
                return;
            case RadioComposerLock.Connecting:
                LoadingPulse.Draw(list.Center, 14f * scale, ui.Accent, ui.MutedInk, Loc.T(L.Music.Live.LockConnecting));
                return;
            case RadioComposerLock.Unavailable:
                if (EmptyState.Draw(list, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Music.Live.LockUnavailable),
                        Loc.T(L.Music.StationOfflineSub), Loc.T(L.Common.Retry)))
                {
                    RetryStationRoom();
                }

                return;
            default:
                EmptyState.Draw(list, ui, FontAwesomeIcon.Comments, Loc.T(L.Music.Live.ChatEmptyTitle),
                    Loc.T(L.Music.Live.ChatEmptySub));
                return;
        }
    }

    private void DrawChatRow(RadioChatRow row, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var avatarRadius = ChatAvatarRadius * scale;
        var padY = ChatRowPadY * scale;
        var sidePad = Metrics.Space.Xs * scale;
        var textLeft = origin.X + sidePad + avatarRadius * 2f + ChatRowGap * scale;
        var right = origin.X + width - sidePad;
        row.EnsureBody(MathF.Max(1f, right - textLeft), ChatBodyStyle);
        var headerHeight = Typography.LineHeight(ChatNameStyle);
        var height = MathF.Max(avatarRadius * 2f, headerHeight + ChatBodyGap * scale + row.BodyHeight) + padY * 2f;
        var size = new Vector2(width, height);
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            return;
        }

        var entry = row.Entry;
        var min = origin;
        var max = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        if (entry.IsMine)
        {
            Squircle.Fill(drawList, min, max, Metrics.Radius.Md * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, OwnRowAlpha)));
        }
        else if (hovered)
        {
            Squircle.Fill(drawList, min, max, Metrics.Radius.Md * scale, ImGui.GetColorU32(ui.HoverWash));
        }

        var avatarCenter = new Vector2(min.X + sidePad + avatarRadius, min.Y + padY + avatarRadius);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, entry.DisplayName, string.Empty,
            entry.AvatarUrl, images, lodestone, 0.8f, 24);

        var headerY = min.Y + padY;
        var menuRadius = 10f * scale;
        var menuCenter = new Vector2(right - menuRadius, headerY + headerHeight * 0.5f);
        var timeWidth = Typography.Measure(entry.TimeLabel, TextStyles.Caption2).X;
        var badge = BadgeFor(entry);
        var badgeWidth = badge.Length > 0 ? Typography.Measure(badge, BadgeStyle).X + BadgePadX * 2f * scale : 0f;
        var badgeGap = badge.Length > 0 ? Metrics.Space.Xs * scale : 0f;
        var nameRoom = right - MathF.Max(timeWidth, menuRadius * 2f) - Metrics.Space.Sm * scale - badgeWidth
                       - badgeGap - textLeft;
        var name = row.NameFor(nameRoom, ChatNameStyle);
        Typography.Draw(drawList, new Vector2(textLeft, headerY), name, row.NameInk, ChatNameStyle);
        if (badge.Length > 0)
        {
            var badgeMin = new Vector2(textLeft + Typography.Measure(name, ChatNameStyle).X + badgeGap,
                headerY + headerHeight * 0.12f);
            DrawChatBadge(drawList, badgeMin, badgeWidth, headerHeight * 0.76f, badge, entry.IsDj, scale);
        }

        var menuTapped = false;
        if (hovered)
        {
            menuTapped = ui.IconButton(menuCenter, menuRadius, IconGlyph.Of(FontAwesomeIcon.EllipsisH), ui.MutedInk,
                AppSkin.Transparent, 0.7f, Loc.T(L.Music.Live.MessageOptions));
        }
        else
        {
            Typography.Draw(drawList,
                new Vector2(right - timeWidth,
                    headerY + (headerHeight - Typography.LineHeight(TextStyles.Caption2)) * 0.5f), entry.TimeLabel,
                ui.MutedInk, TextStyles.Caption2);
        }

        DrawChatBody(drawList, row, new Vector2(textLeft, headerY + headerHeight + ChatBodyGap * scale));
        var overMenu = hovered && Vector2.DistanceSquared(ImGui.GetMousePos(), menuCenter) <= menuRadius * menuRadius;
        if (menuTapped)
        {
            OpenChatMenu(entry);
        }
        else
        {
            TrackChatPress(entry, hovered && !overMenu, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
    }

    private string BadgeFor(RadioChatEntry entry)
    {
        if (entry.IsDj)
        {
            return Loc.T(L.Music.Live.DjBadge);
        }

        return entry.IsMine && room.IsModerator ? Loc.T(L.Music.Live.ModBadge) : string.Empty;
    }

    private void DrawChatBadge(ImDrawListPtr drawList, Vector2 min, float width, float height, string label,
        bool dj, float scale)
    {
        var fill = dj ? ui.Accent : ui.Theme.ToggleOn;
        var max = new Vector2(min.X + width, min.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.35f, ImGui.GetColorU32(fill));
        var size = Typography.Measure(label, BadgeStyle);
        Typography.Draw(drawList, new Vector2(min.X + BadgePadX * scale, min.Y + (height - size.Y) * 0.5f), label,
            ui.Palette.BackdropBottom, BadgeStyle);
    }

    private void DrawChatBody(ImDrawListPtr drawList, RadioChatRow row, Vector2 origin)
    {
        using (Plugin.Fonts.Push(ChatBodyStyle.Scale, ChatBodyStyle.Weight))
        {
            if (row.Rich is { } rich)
            {
                RichText.Draw(drawList, rich, origin, new RichTextInk(ui.BodyInk, ui.Accent, ui.Accent), out var hit);
                if (hit.Kind == RichTextRunKind.Link && hit.Clicked)
                {
                    UrlActions.AskThenOpen(rich.Urls[hit.TargetIndex]);
                }

                return;
            }

            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var ink = ImGui.GetColorU32(ui.BodyInk);
            var lines = row.Lines;
            for (var index = 0; index < lines.Length; index++)
            {
                drawList.AddText(font, fontSize, new Vector2(origin.X, origin.Y + index * row.LineHeight), ink,
                    lines[index]);
            }
        }
    }

    private void TrackChatPress(RadioChatEntry entry, bool hovered, float scale)
    {
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            OpenChatMenu(entry);
            return;
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pressedMessageId = entry.MessageId;
            pressedAt = ImGui.GetTime();
            pressedOrigin = ImGui.GetMousePos();
            return;
        }

        if (pressedMessageId != entry.MessageId)
        {
            return;
        }

        var slop = LongPressSlop * scale;
        var drifted = Vector2.DistanceSquared(ImGui.GetMousePos(), pressedOrigin) > slop * slop;
        if (!hovered || drifted || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pressedMessageId = -1;
            return;
        }

        if (ImGui.GetTime() - pressedAt < LongPressSeconds)
        {
            return;
        }

        pressedMessageId = -1;
        UiInteract.CancelPendingTap();
        OpenChatMenu(entry);
    }

    private void EnsureReactionFiles()
    {
        if (reactionFilesReady || !EmojiCatalog.Ready)
        {
            return;
        }

        for (var index = 0; index < ReactionShortcodes.Length; index++)
        {
            reactionFiles[index] = EmojiCatalog.TryResolve(ReactionShortcodes[index], out var file)
                ? file
                : string.Empty;
        }

        reactionFilesReady = true;
    }

    private void DrawFloatingReactions(Rect list, float scale)
    {
        var count = room.ReactionCount;
        if (count == 0)
        {
            return;
        }

        EnsureReactionFiles();
        if (!reactionFilesReady)
        {
            return;
        }

        var now = Environment.TickCount64;
        var drawList = ImGui.GetWindowDrawList();
        var rise = MathF.Min(ReactionRise * scale, list.Height * 0.75f);
        var baseX = list.Max.X - Metrics.Space.Xxl * scale;
        var baseY = list.Max.Y - Metrics.Space.Sm * scale;
        drawList.PushClipRect(list.Min, list.Max, true);
        for (var index = 0; index < count; index++)
        {
            var pulse = room.ReactionAt(index);
            var file = reactionFiles[pulse.Reaction];
            if (string.IsNullOrEmpty(file))
            {
                continue;
            }

            var lane = RadioLiveRules.ReactionLane(pulse.AtTick, pulse.Reaction);
            var pose = RadioLiveRules.Pose(now - pulse.AtTick, RadioRoomSession.ReactionLifetimeMilliseconds, lane);
            var fontSize = ReactionSize * scale * pose.Scale * (pulse.IsMine ? ReactionMineScale : 1f);
            var side = EmojiRender.LineHeight(fontSize);
            var center = new Vector2(baseX - lane * ReactionLaneSpacing * scale + pose.Sway * ReactionSwayWidth * scale,
                baseY - side * 0.5f - pose.Rise * rise);
            EmojiRender.Draw(drawList, file, center - new Vector2(side * 0.5f, side * 0.5f), fontSize, pose.Alpha);
        }

        drawList.PopClipRect();
    }

    private void DrawJumpPill(Rect list, float scale)
    {
        if (chatFollow.Pinned || chatRowCount == 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var height = JumpPillHeight * scale;
        var bottom = list.Max.Y - Metrics.Space.Sm * scale;
        Vector2 min;
        Vector2 max;
        if (chatFollow.Unseen > 0)
        {
            var label = newMessagesLabel.Plural(L.Music.Live.NewMessages, chatFollow.Unseen);
            var width = MathF.Min(list.Width - Metrics.Space.Xxl * scale,
                Typography.Measure(label, TextStyles.FootnoteEmphasized).X + height + Metrics.Space.Lg * scale);
            min = new Vector2(list.Center.X - width * 0.5f, bottom - height);
            max = new Vector2(list.Center.X + width * 0.5f, bottom);
            Material.ThemedGlass(drawList, min, max, height * 0.5f, scale, theme);
            AppSkin.Icon(drawList, new Vector2(min.X + height * 0.5f + Metrics.Space.Xxs * scale, min.Y + height * 0.5f),
                IconGlyph.Of(FontAwesomeIcon.ArrowDown), ui.Accent, 0.65f);
            var fitted = jumpPillFit.Fit(label, width - height - Metrics.Space.Sm * scale,
                TextStyles.FootnoteEmphasized);
            var size = Typography.Measure(fitted, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(min.X + height, min.Y + (height - size.Y) * 0.5f), fitted, ui.Accent,
                TextStyles.FootnoteEmphasized);
        }
        else
        {
            min = new Vector2(list.Center.X - height * 0.5f, bottom - height);
            max = new Vector2(list.Center.X + height * 0.5f, bottom);
            Material.ThemedGlass(drawList, min, max, height * 0.5f, scale, theme);
            AppSkin.Icon(drawList, (min + max) * 0.5f, IconGlyph.Of(FontAwesomeIcon.ArrowDown), ui.Accent, 0.65f);
        }

        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            chatFollow.RequestJump();
        }
    }

    private void DrawChatComposer(Rect bar, RadioComposerLock lockState, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y), ImGui.GetColorU32(ui.Hairline),
            Metrics.Stroke.Hairline);
        var inset = Metrics.Space.Md * scale;
        var buttonRadius = ComposerButtonRadius * scale;
        var rowCenterY = bar.Max.Y - ComposerBaseHeight * scale * 0.5f;
        var canReact = room.IsAttached && lockState is RadioComposerLock.None or RadioComposerLock.Muted;
        var reactCenter = new Vector2(bar.Min.X + inset + buttonRadius, rowCenterY);
        var fieldLeft = bar.Min.X + inset;
        var overReactionToggle = false;
        if (canReact)
        {
            fieldLeft = reactCenter.X + buttonRadius + Metrics.Space.Sm * scale;
            overReactionToggle = UiInteract.Hover(reactCenter - new Vector2(buttonRadius, buttonRadius),
                reactCenter + new Vector2(buttonRadius, buttonRadius));
            if (ui.IconButton(reactCenter, buttonRadius, IconGlyph.Of(FontAwesomeIcon.Heart),
                    reactionTrayOpen ? ui.Accent : ui.MutedInk, Palette.WithAlpha(ui.FieldSurface, 0.9f), 0.8f,
                    Loc.T(L.Music.Live.React)))
            {
                reactionTrayOpen = !reactionTrayOpen;
            }
        }
        else
        {
            reactionTrayOpen = false;
        }

        if (lockState != RadioComposerLock.None)
        {
            DrawComposerLock(drawList, new Rect(new Vector2(fieldLeft, bar.Min.Y + ComposerFieldInsetY * scale),
                new Vector2(bar.Max.X - inset, bar.Max.Y - ComposerFieldInsetY * scale)), lockState, scale);
            DrawReactionTray(bar, reactCenter, overReactionToggle, scale);
            return;
        }

        var sendCenter = new Vector2(bar.Max.X - inset - buttonRadius, rowCenterY);
        var field = new Rect(new Vector2(fieldLeft, bar.Min.Y + ComposerFieldInsetY * scale),
            new Vector2(sendCenter.X - buttonRadius - Metrics.Space.Sm * scale, bar.Max.Y - ComposerFieldInsetY * scale));
        var submitted = SubmitField.Multiline(field, "##radioChatComposer", Loc.T(L.Music.Live.ChatHint), chatEditor,
            theme, RadioRoomSession.MaxChatLength, ComposerMaxLines, FontAwesomeIcon.Comment);
        var canSend = chatEditor.HasContent && room.CanSendChat();
        drawList.AddCircleFilled(sendCenter, buttonRadius,
            ImGui.GetColorU32(canSend ? ui.Accent : Palette.WithAlpha(ui.FieldSurface, 0.9f)), 32);
        var sendTapped = ui.IconButton(sendCenter, buttonRadius, IconGlyph.Of(FontAwesomeIcon.PaperPlane),
            canSend ? ui.Palette.BackdropBottom : ui.MutedInk, AppSkin.Transparent, 0.8f, Loc.T(L.Music.Live.Send));
        DrawChatCounter(drawList, field, scale);
        DrawReactionTray(bar, reactCenter, overReactionToggle, scale);
        if ((submitted || sendTapped) && canSend && room.SendChat(chatEditor.Text))
        {
            chatEditor.Adopt(string.Empty);
            chatFollow.RequestJump();
            UiFeedback.Play(UiSound.MessageSent);
        }
    }

    private void DrawChatCounter(ImDrawListPtr drawList, Rect field, float scale)
    {
        var length = chatEditor.Text.Length;
        if (!RadioLiveRules.ShowsCounter(length, RadioRoomSession.MaxChatLength))
        {
            return;
        }

        var remaining = RadioRoomSession.MaxChatLength - length;
        var label = counterLabel.Number(remaining);
        var size = Typography.Measure(label, TextStyles.Caption2);
        var ink = remaining <= CounterWarning ? ui.Theme.Danger : ui.MutedInk;
        Typography.Draw(drawList,
            new Vector2(field.Max.X - size.X - Metrics.Space.Md * scale, field.Min.Y - size.Y - Metrics.Space.Xxs * scale),
            label, ink, TextStyles.Caption2);
    }

    private void DrawComposerLock(ImDrawListPtr drawList, Rect field, RadioComposerLock lockState, float scale)
    {
        Squircle.Fill(drawList, field.Min, field.Max, field.Height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.FieldSurface, 0.7f)));
        var glyphCenter = new Vector2(field.Min.X + Metrics.Space.Lg * scale, field.Center.Y);
        AppSkin.Icon(drawList, glyphCenter, IconGlyph.Of(FontAwesomeIcon.Lock), ui.MutedInk, 0.7f);
        var textLeft = glyphCenter.X + Metrics.Space.Md * scale;
        var fitted = composerLockFit.Fit(LockText(lockState), field.Max.X - Metrics.Space.Md * scale - textLeft,
            TextStyles.Footnote);
        var size = Typography.Measure(fitted, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, field.Center.Y - size.Y * 0.5f), fitted, ui.MutedInk,
            TextStyles.Footnote);
    }

    private string LockText(RadioComposerLock lockState)
    {
        return lockState switch
        {
            RadioComposerLock.Suspended => Loc.T(L.Music.Live.LockSuspended),
            RadioComposerLock.SignedOut => Loc.T(L.Music.Live.LockSignedOut),
            RadioComposerLock.Unavailable => Loc.T(L.Music.Live.LockUnavailable),
            RadioComposerLock.Muted => MuteText(),
            _ => Loc.T(L.Music.Live.LockConnecting),
        };
    }

    private string MuteText()
    {
        var remaining = RadioLiveRules.Remaining(room.MutedUntilUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var duration = remaining.Unit switch
        {
            RadioDurationUnit.Seconds => durationLabel.Format(L.Music.Live.DurationSeconds, remaining.Major),
            RadioDurationUnit.Minutes => durationLabel.Format(L.Music.DurationMinutes, remaining.Major),
            _ => durationLabel.Format(L.Music.DurationHoursMinutes, remaining.Major, remaining.Minor),
        };

        if (ReferenceEquals(duration, muteDuration) && ReferenceEquals(muteLanguage, Loc.Current))
        {
            return muteText;
        }

        muteDuration = duration;
        muteLanguage = Loc.Current;
        muteText = Loc.T(L.Music.Live.LockMuted, duration);
        return muteText;
    }

    private void DrawReactionTray(Rect bar, Vector2 toggleCenter, bool overToggle, float scale)
    {
        if (!reactionTrayOpen)
        {
            return;
        }

        EnsureReactionFiles();
        var slot = ReactionTraySlot * scale;
        var height = ReactionTrayHeight * scale;
        var width = slot * RadioRoomSession.ReactionKinds + Metrics.Space.Sm * 2f * scale;
        var left = MathF.Max(bar.Min.X + Metrics.Space.Sm * scale, toggleCenter.X - ComposerButtonRadius * scale);
        left = MathF.Min(left, bar.Max.X - Metrics.Space.Sm * scale - width);
        var min = new Vector2(left, bar.Min.Y - height - Metrics.Space.Xs * scale);
        var max = new Vector2(left + width, min.Y + height);
        var drawList = ImGui.GetWindowDrawList();
        Material.ThemedGlass(drawList, min, max, height * 0.5f, scale, theme);
        var enabled = room.CanReact();
        var overTray = UiInteract.Hover(min, max);
        for (var index = 0; index < RadioRoomSession.ReactionKinds; index++)
        {
            var slotMin = new Vector2(min.X + Metrics.Space.Sm * scale + index * slot, min.Y);
            var slotMax = new Vector2(slotMin.X + slot, max.Y);
            var hovered = UiInteract.Hover(slotMin, slotMax);
            var fontSize = ReactionSize * scale * (hovered ? 1.15f : 1f);
            var side = EmojiRender.LineHeight(fontSize);
            var center = (slotMin + slotMax) * 0.5f;
            var file = reactionFiles[index];
            if (!string.IsNullOrEmpty(file))
            {
                EmojiRender.Draw(drawList, file, center - new Vector2(side * 0.5f, side * 0.5f), fontSize,
                    enabled ? 1f : 0.45f);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(slotMin, slotMax, hovered) && enabled)
            {
                room.React(index);
            }
        }

        if (UiInteract.ClickedOutside(overTray || overToggle))
        {
            reactionTrayOpen = false;
        }
    }
}
