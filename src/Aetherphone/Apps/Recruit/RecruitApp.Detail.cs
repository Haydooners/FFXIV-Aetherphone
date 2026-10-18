using System;
using System.Collections.Generic;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Recruit;

internal sealed partial class RecruitApp
{
    private static readonly (string Label, RaidDays Day)[] DetailDayButtons =
    {
        ("Su", RaidDays.Sunday),
        ("M", RaidDays.Monday),
        ("Tu", RaidDays.Tuesday),
        ("W", RaidDays.Wednesday),
        ("Th", RaidDays.Thursday),
        ("F", RaidDays.Friday),
        ("Sa", RaidDays.Saturday),
    };

    private void DrawDetailScreen(in PhoneContext context, Rect area)
    {
        if (selectedListing is null)
        {
            router.Pop();
            return;
        }

        var scale = UiScale.Current;
        var theme = ui.Theme;
        var drawList = ImGui.GetWindowDrawList();
        var listing = selectedListing;
        var headerHeight = 44f * scale;
        var headerMin = area.Min;
        var headerMax = new Vector2(area.Max.X, headerMin.Y + headerHeight);
        DrawDetailHeader(drawList, headerMin, headerMax, scale, theme);
        var top = headerMax.Y;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            DrawDetailBadges(drawList, width, scale, theme, listing);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawDetailTitle(width, scale, theme, listing.Title);
            ImGui.Dummy(new Vector2(0f, 14f * scale));
            DrawDetailRoleSection(drawList, width, scale, theme, listing);
            ImGui.Dummy(new Vector2(0f, 16f * scale));
            var sepY = ImGui.GetCursorScreenPos().Y;
            var sepOriginX = ImGui.GetCursorScreenPos().X;
            drawList.AddLine(new Vector2(sepOriginX, sepY), new Vector2(sepOriginX + width, sepY),
                ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.25f)), 1f * scale);
            ImGui.Dummy(new Vector2(width, 16f * scale));
            if (!string.IsNullOrWhiteSpace(listing.Description))
            {
                var descHeight = Typography.DrawWrappedLeft(ImGui.GetCursorScreenPos(), listing.Description, theme.TextStrong,
                    TextStyles.Subheadline, width);
                ImGui.Dummy(new Vector2(width, descHeight + 16f * scale));
            }
            if (listing.Tags != null && listing.Tags.Count > 0)
            {
                DrawDetailTags(drawList, width, scale, theme, listing.Tags);
                ImGui.Dummy(new Vector2(0f, 18f * scale));
            }
            DrawDetailRaidTimes(drawList, width, scale, theme, listing);
            ImGui.Dummy(new Vector2(0f, 18f * scale));
            DrawDetailContact(drawList, width, scale, theme, listing);
            ImGui.Dummy(new Vector2(0f, 22f * scale));
            DrawDetailBottomActions(drawList, width, scale, theme, listing);
            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawDetailHeader(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale, PhoneTheme theme)
    {
        var centerY = (min.Y + max.Y) * 0.5f;
        var backMin = new Vector2(min.X + 12f * scale, centerY - 14f * scale);
        var backMax = new Vector2(backMin.X + 32f * scale, centerY + 14f * scale);
        var backHovered = UiInteract.Hover(backMin, backMax);
        if (backHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        AppSkin.Icon(drawList, (backMin + backMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.ChevronLeft), theme.TextStrong, 0.90f);
        if (UiInteract.Click(backMin, backMax, backHovered))
        {
            router.Pop();
        }
        var titleCenter = new Vector2((min.X + max.X) * 0.5f, centerY);
        Typography.DrawCentered(drawList, titleCenter, "Post", theme.TextStrong, TextStyles.Headline.Scale, FontWeight.Bold);
    }

    private void DrawDetailBadges(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme, RecruitListing listing)
    {
        var badges = new List<string>();
        var playstyleName = listing.Playstyle switch
        {
            StaticCategory.MidCore => "MIDCORE",
            StaticCategory.Hardcore => "HARDCORE",
            StaticCategory.SemiHardcore => "SEMI-HARDCORE",
            _ => "CASUAL",
        };
        badges.Add(playstyleName);
        if (listing.Title.Contains("prog", StringComparison.OrdinalIgnoreCase) ||
            listing.Description.Contains("prog", StringComparison.OrdinalIgnoreCase))
        {
            badges.Add("PROG");
        }
        var originX = ImGui.GetCursorScreenPos().X;
        var cursorX = originX;
        var startY = ImGui.GetCursorScreenPos().Y;
        var chipHeight = 22f * scale;
        for (var i = 0; i < badges.Count; i++)
        {
            var text = badges[i];
            var textWidth = Typography.Measure(text, TextStyles.Caption2).X;
            var chipWidth = textWidth + 18f * scale;
            var chipMin = new Vector2(cursorX, startY);
            var chipMax = new Vector2(cursorX + chipWidth, startY + chipHeight);
            Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, 0.85f)));
            Squircle.Stroke(drawList, chipMin, chipMax, chipHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.40f)), 1f * scale);
            Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, text, theme.TextStrong, TextStyles.Caption2.Scale, FontWeight.Bold);
            cursorX += chipWidth + 6f * scale;
        }
        ImGui.Dummy(new Vector2(width, chipHeight));
    }

    private void DrawDetailTitle(float width, float scale, PhoneTheme theme, string title)
    {
        var origin = ImGui.GetCursorScreenPos();
        var titleHeight = Typography.DrawWrappedLeft(origin, title, theme.TextStrong, TextStyles.Title1, width);
        ImGui.Dummy(new Vector2(width, titleHeight));
    }

    private void DrawDetailRoleSection(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme, RecruitListing listing)
    {
        Typography.Draw(ImGui.GetCursorScreenPos(), "Role", theme.TextStrong, TextStyles.Headline.Scale, FontWeight.Bold);
        ImGui.Dummy(new Vector2(0f, 20f * scale));
        var originX = ImGui.GetCursorScreenPos().X;
        var cursorX = originX;
        var startY = ImGui.GetCursorScreenPos().Y;
        var pillHeight = 28f * scale;
        var roles = listing.RolesNeeded != null && listing.RolesNeeded.Count > 0
            ? listing.RolesNeeded
            : new List<RaidRole> { RaidRole.Tank };
        for (var i = 0; i < roles.Count; i++)
        {
            var role = roles[i];
            var label = role switch
            {
                RaidRole.PureHealer => "Pure Healer",
                RaidRole.BarrierHealer => "Shield Healer",
                RaidRole.PhysRanged => "Phys Ranged",
                _ => role.ToString(),
            };
            var roleIcon = RoleIcon(role);
            var textW = Typography.Measure(label, TextStyles.Caption1).X;
            var pillW = textW + 36f * scale;
            var pillMin = new Vector2(cursorX, startY);
            var pillMax = new Vector2(cursorX + pillW, startY + pillHeight);
            Squircle.Fill(drawList, pillMin, pillMax, 8f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, 0.90f)));
            Squircle.Stroke(drawList, pillMin, pillMax, 8f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.40f)), 1f * scale);
            Typography.Draw(drawList, new Vector2(pillMin.X + 10f * scale, pillMin.Y + 6f * scale), label,
                theme.TextStrong, TextStyles.Caption1);
            AppSkin.Icon(drawList, new Vector2(pillMax.X - 12f * scale, (pillMin.Y + pillMax.Y) * 0.5f),
                IconGlyph.Of(roleIcon), theme.TextMuted, 0.65f);
            cursorX += pillW + 8f * scale;
        }
        ImGui.Dummy(new Vector2(width, pillHeight));
    }

    private void DrawDetailTags(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme, List<string> tags)
    {
        var originX = ImGui.GetCursorScreenPos().X;
        var cursorX = originX;
        var startY = ImGui.GetCursorScreenPos().Y;
        var cursorY = startY;
        var rowHeight = 24f * scale;
        var maxX = originX + width;
        for (var i = 0; i < tags.Count; i++)
        {
            var text = tags[i].StartsWith('#') ? tags[i] : $"#{tags[i]}";
            var textW = Typography.Measure(text, TextStyles.Caption1).X;
            var chipW = textW + 20f * scale;
            if (cursorX + chipW > maxX && cursorX > originX)
            {
                cursorX = originX;
                cursorY += rowHeight + 6f * scale;
            }
            var chipMin = new Vector2(cursorX, cursorY);
            var chipMax = new Vector2(cursorX + chipW, cursorY + rowHeight);
            Squircle.Fill(drawList, chipMin, chipMax, rowHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, 0.90f)));
            Squircle.Stroke(drawList, chipMin, chipMax, rowHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.35f)), 1f * scale);
            Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, text, theme.TextStrong, TextStyles.Caption1);
            cursorX += chipW + 6f * scale;
        }
        var totalH = (cursorY - startY) + rowHeight;
        ImGui.Dummy(new Vector2(width, totalH));
    }

    private void DrawDetailRaidTimes(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme, RecruitListing listing)
    {
        var blueAccent = new Vector4(0.24f, 0.55f, 0.98f, 1f);
        Typography.Draw(ImGui.GetCursorScreenPos(), "Raid Times", blueAccent, TextStyles.Headline.Scale, FontWeight.Bold);
        ImGui.Dummy(new Vector2(0f, 22f * scale));
        var dayBtnGap = 6f * scale;
        var dayBtnWidth = (width - dayBtnGap * 6f) / 7f;
        var dayBtnHeight = 32f * scale;
        var daysOrigin = ImGui.GetCursorScreenPos();
        for (var i = 0; i < DetailDayButtons.Length; i++)
        {
            var (label, day) = DetailDayButtons[i];
            var active = listing.SelectedDays.HasFlag(day);
            var dMin = new Vector2(daysOrigin.X + i * (dayBtnWidth + dayBtnGap), daysOrigin.Y);
            var dMax = dMin + new Vector2(dayBtnWidth, dayBtnHeight);
            var bg = active
                ? blueAccent
                : Palette.WithAlpha(theme.GroupedCard, 0.70f);
            Squircle.Fill(drawList, dMin, dMax, 8f * scale, ImGui.GetColorU32(bg));
            Squircle.Stroke(drawList, dMin, dMax, 8f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, active ? 0f : 0.35f)), 1f * scale);
            var textCol = active ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong;
            Typography.DrawCentered(drawList, (dMin + dMax) * 0.5f, label, textCol, TextStyles.Caption1.Scale, FontWeight.Bold);
        }
        ImGui.Dummy(new Vector2(width, dayBtnHeight + 12f * scale));
        var timeRangeStr = RecruitCatalog.FormatTimeRange(listing.StartMinuteOfDay, listing.EndMinuteOfDay, listing.Timezone);
        Typography.Draw(ImGui.GetCursorScreenPos(), timeRangeStr, theme.TextStrong, TextStyles.Headline.Scale, FontWeight.Bold);
        ImGui.Dummy(new Vector2(0f, 20f * scale));
    }

    private void DrawDetailContact(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme, RecruitListing listing)
    {
        Typography.Draw(ImGui.GetCursorScreenPos(), "Contact", theme.TextStrong, TextStyles.Headline.Scale, FontWeight.Bold);
        ImGui.Dummy(new Vector2(0f, 20f * scale));
        var cardMin = ImGui.GetCursorScreenPos();
        var cardHeight = 62f * scale;
        var cardMax = cardMin + new Vector2(width, cardHeight);

        var cardHovered = UiInteract.Hover(cardMin, cardMax);
        if (cardHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var bgAlpha = cardHovered ? 1.0f : 0.85f;
        Squircle.Fill(drawList, cardMin, cardMax, 12f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard, bgAlpha)));
        var borderColor = cardHovered ? theme.Accent : Palette.WithAlpha(theme.Separator, 0.35f);
        Squircle.Stroke(drawList, cardMin, cardMax, 12f * scale, ImGui.GetColorU32(borderColor), 1f * scale);

        var cleanWorld = ExtractCleanWorld(listing.WorldDc);
        if (!string.IsNullOrWhiteSpace(cleanWorld))
        {
            posterClient.LoadCharacter(listing.AuthorName, cleanWorld);
        }
        var avatarRadius = 18f * scale;
        var avatarCenter = new Vector2(cardMin.X + 16f * scale + avatarRadius, (cardMin.Y + cardMax.Y) * 0.5f);
        var currentUser = store.CurrentUser;
        var localPlayerName = Plugin.ObjectTable.LocalPlayer?.Name.TextValue;
        var isSelf = string.Equals(listing.AuthorName, currentUser?.Name, StringComparison.OrdinalIgnoreCase)
                  || string.Equals(listing.AuthorName, localPlayerName, StringComparison.OrdinalIgnoreCase);
        var aetherphoneAvatar = isSelf ? currentUser?.AvatarUrl : null;

        var tomestoneAvatar = string.Equals(posterClient.Profile.CharacterName, listing.AuthorName, StringComparison.OrdinalIgnoreCase)
            ? posterClient.Profile.AvatarUrl
            : null;
        var avatarUrl = !string.IsNullOrWhiteSpace(aetherphoneAvatar)
            ? aetherphoneAvatar
            : tomestoneAvatar;
        var frame = isSelf ? Frames.Of(currentUser?.FrameId) : null;
        if (images is not null && lodestone is not null)
        {
            AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, listing.AuthorName,
                cleanWorld, avatarUrl, images, lodestone, 0.85f, 32, 1f, frame);
        }
        else
        {
            drawList.AddCircleFilled(avatarCenter, avatarRadius, ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, 0.25f)));
            drawList.AddCircle(avatarCenter, avatarRadius, ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, 0.60f)), 32, 1.5f * scale);
            Typography.DrawCentered(drawList, avatarCenter, Initials.Of(listing.AuthorName), theme.TextStrong, TextStyles.Subheadline.Scale, FontWeight.Bold);
        }

        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var nameY = cardMin.Y + 12f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, nameY), listing.AuthorName, theme.TextStrong, TextStyles.Headline.Scale, FontWeight.Bold);
        var handleY = nameY + 20f * scale;

        var handleText = !string.IsNullOrWhiteSpace(listing.WorldDc)
            ? $"@{listing.WorldDc.ToLowerInvariant().Replace(" ", "")}"
            : "@username";
        Typography.Draw(drawList, new Vector2(textLeft, handleY), handleText, theme.TextMuted, TextStyles.Caption1);

        if (UiInteract.Click(cardMin, cardMax, cardHovered)){
           // var cleanWorld = ExtractCleanWorld(listing.WorldDc);

            posterClient.LoadCharacter(listing.AuthorName, cleanWorld);
            router.Push(RecruitScreen.PosterProfile);
        }
        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private static string ExtractCleanWorld(string? worldDc)
    {
        if (string.IsNullOrWhiteSpace(worldDc)){
            return string.Empty;
        }

        var parts = worldDc.Trim().TrimStart('@').Split(['/', ' ', '['], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0){
            return string.Empty;
        }

        var world = parts[0];
        return char.ToUpperInvariant(world[0]) + world[1..].ToLowerInvariant();
    }

    private void DrawDetailBottomActions(ImDrawListPtr drawList, float width, float scale, PhoneTheme theme, RecruitListing listing)
    {
        var origin = ImGui.GetCursorScreenPos();
        var btnHeight = 38f * scale;
        var gap = 8f * scale;
        var iconBtnWidth = 38f * scale;
        var messageWidth = width - (iconBtnWidth * 2f + gap * 2f);
        var msgMin = origin;
        var msgMax = new Vector2(origin.X + messageWidth, origin.Y + btnHeight);
        var msgHovered = UiInteract.Hover(msgMin, msgMax);
        if (msgHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var blueAccent = new Vector4(0.42f, 0.62f, 0.96f, 1f);
        Squircle.Fill(drawList, msgMin, msgMax, btnHeight * 0.5f,
            ImGui.GetColorU32(msgHovered ? Palette.WithAlpha(blueAccent, 0.85f) : blueAccent));
        Typography.DrawCentered(drawList, (msgMin + msgMax) * 0.5f, "Message", new Vector4(1f, 1f, 1f, 1f),
            TextStyles.Subheadline.Scale, FontWeight.Bold);
        if (UiInteract.Click(msgMin, msgMax, msgHovered))
        {
            ImGui.SetClipboardText($"/tell {listing.AuthorName}@{listing.WorldDc} ");
        }

        var bmMin = new Vector2(msgMax.X + gap, origin.Y);
        var bmMax = new Vector2(bmMin.X + iconBtnWidth, origin.Y + btnHeight);
        var bmHovered = UiInteract.Hover(bmMin, bmMax);
        if (bmHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Squircle.Fill(drawList, bmMin, bmMax, btnHeight * 0.5f,
            ImGui.GetColorU32(bmHovered ? Palette.WithAlpha(blueAccent, 0.85f) : blueAccent));
        AppSkin.Icon(drawList, (bmMin + bmMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.Bookmark),
            new Vector4(1f, 1f, 1f, 1f), 0.70f);
        var shareMin = new Vector2(bmMax.X + gap, origin.Y);
        var shareMax = new Vector2(shareMin.X + iconBtnWidth, origin.Y + btnHeight);
        var shareHovered = UiInteract.Hover(shareMin, shareMax);
        if (shareHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Squircle.Fill(drawList, shareMin, shareMax, btnHeight * 0.5f,
            ImGui.GetColorU32(shareHovered ? Palette.WithAlpha(blueAccent, 0.85f) : blueAccent));
        AppSkin.Icon(drawList, (shareMin + shareMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.Share),
            new Vector4(1f, 1f, 1f, 1f), 0.70f);
        if (UiInteract.Click(shareMin, shareMax, shareHovered))
        {
            ImGui.SetClipboardText($"[Aetherphone PF] {listing.Title} - {listing.AuthorName} ({listing.WorldDc})");
        }

        ImGui.Dummy(new Vector2(width, btnHeight));
    }
}