using System;
using System.Collections.Generic;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Recruit;

internal sealed partial class RecruitApp
{
    private void DrawProfileTab(Rect area, float scale, PhoneTheme theme)
    {
        tomestoneClient.EnsureLoaded(store.CurrentUser);

        var headerHeight = 36f * scale;
        var headerY = area.Min.Y + 8f * scale;

        Typography.Draw(new Vector2(area.Min.X + 14f * scale, headerY), 
            "Profile", theme.TextStrong, 1.25f, FontWeight.Bold);
        var syncIcon = tomestoneClient.IsLoading ? FontAwesomeIcon.Sync : FontAwesomeIcon.CheckCircle;
        var syncColor = tomestoneClient.IsLoading ? theme.Accent : new Vector4(0.35f, 0.85f, 0.45f, 0.9f);
        var syncPos = new Vector2(area.Max.X - 28f * scale, headerY + 8f * scale);
        AppSkin.Icon(ImGui.GetWindowDrawList(), syncPos, IconGlyph.Of(syncIcon), syncColor, 0.75f);

        var top = headerY + headerHeight + 8f * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            var profile = tomestoneClient.Profile;

            ImGui.Dummy(new Vector2(0f, 4f * scale));
            DrawUserProfileHeader(width, scale, theme, profile);
            ImGui.Dummy(new Vector2(0f, 16f * scale));

            if (profile.Progression is not null)
            {
                DrawProgressingSection(width, scale, theme, profile.Progression);
                ImGui.Dummy(new Vector2(0f, 18f * scale));
            }

            DrawSavageSection(width, scale, theme, profile.SavageTiers);
            ImGui.Dummy(new Vector2(0f, 18f * scale));

            DrawUltimatesSection(width, scale, theme, profile.Ultimates);
            ImGui.Dummy(new Vector2(0f, 18f * scale));

            DrawExtremesSection(width, scale, theme, profile.Extremes);
            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawPosterProfile(in PhoneContext context, Rect area)
    {
        var scale = UiScale.Current;
        var theme = ui.Theme;
        var drawList = ImGui.GetWindowDrawList();

        var headerHeight = 44f * scale;
        var headerMin = area.Min;
        var headerMax = new Vector2(area.Max.X, headerMin.Y + headerHeight);
        var centerY = (headerMin.Y + headerMax.Y) * 0.5f;
        var backMin = new Vector2(headerMin.X + 12f * scale, centerY - 14f * scale);
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

        var body = new Rect(new Vector2(area.Min.X, headerMax.Y), area.Max);
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            var profile = posterClient.Profile;

            if (posterClient.IsLoading || profile is null)
            {
                return;
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
            DrawUserProfileHeader(width, scale, theme, profile);
            ImGui.Dummy(new Vector2(0f, 16f * scale));

            if (profile.Progression is not null)
            {
                DrawProgressingSection(width, scale, theme, profile.Progression);
                ImGui.Dummy(new Vector2(0f, 18f * scale));
            }

            DrawSavageSection(width, scale, theme, profile.SavageTiers);
            ImGui.Dummy(new Vector2(0f, 18f * scale));

            DrawUltimatesSection(width, scale, theme, profile.Ultimates);
            ImGui.Dummy(new Vector2(0f, 18f * scale));

            DrawExtremesSection(width, scale, theme, profile.Extremes);
            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawUserProfileHeader(float width, float scale, PhoneTheme theme, TomestoneProfileData profile)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var avatarRadius = 26f * scale;
        var avatarCenter = new Vector2(origin.X + avatarRadius + 4f * scale, origin.Y + avatarRadius + 2f * scale);
        var currentUser = store.CurrentUser;
        var localPlayerName = Plugin.ObjectTable.LocalPlayer?.Name.TextValue;
        
        var name = !string.IsNullOrWhiteSpace(profile.CharacterName)
            ? profile.CharacterName
            : (currentUser?.DisplayName ?? currentUser?.Name ?? localPlayerName ?? "Character");
        var world = !string.IsNullOrWhiteSpace(profile.World)
            ? profile.World
            : (currentUser?.World ?? Plugin.ObjectTable.LocalPlayer?.HomeWorld.ValueNullable?.Name.ExtractText() ?? string.Empty);

        var isSelf = string.Equals(name, currentUser?.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, currentUser?.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, localPlayerName, StringComparison.OrdinalIgnoreCase);
        var aetherphoneAvatar = isSelf ? currentUser?.AvatarUrl : null;
        var avatarUrl = !string.IsNullOrWhiteSpace(aetherphoneAvatar)
            ? aetherphoneAvatar
            : profile.AvatarUrl;
        var frame = isSelf ? Frames.Of(currentUser?.FrameId) : null;

        if (images is not null && lodestone is not null){
            AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, name, 
                world, avatarUrl, images, lodestone, 1.0f, 48, 1f, frame);
        }
        else{
            drawList.AddCircleFilled(avatarCenter, avatarRadius, ImGui.GetColorU32(theme.Accent), 48);
            Typography.DrawCentered(drawList, avatarCenter, Initials.Of(name), new Vector4(1f, 1f, 1f, 1f), 1.0f);
        }

        var textLeft = avatarCenter.X + avatarRadius + 14f * scale;
        var nameY = origin.Y + 6f * scale;
        var worldY = nameY + 22f * scale;
        var iconGap = 10f * scale;
        var iconSize = 14f * scale;

        var maxNameWidth = width - (textLeft - origin.X) - iconSize - iconGap - 8f * scale;
        var displayName = Typography.FitText(name, maxNameWidth, 1.15f, FontWeight.Bold);
        var nameSize = Typography.Measure(displayName, 1.15f, FontWeight.Bold);
        var linkIconPos = new Vector2(textLeft + nameSize.X + iconGap, nameY + 9f * scale);

        var clickMin = new Vector2(textLeft - 4f * scale, nameY - 2f * scale);
        var clickMax = new Vector2(linkIconPos.X + iconSize + 4f * scale, nameY + 20f * scale);
        var hovered = UiInteract.Hover(clickMin, clickMax);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        if (UiInteract.Click(clickMin, clickMax, hovered))
        {
            var url = profile.WebUrl;
            if (!string.IsNullOrWhiteSpace(url))
            {
                Aetherphone.Windows.UrlActions.OpenInBrowser(url);
            }
        }

        var nameColor = hovered ? theme.Accent : theme.TextStrong;
        Typography.Draw(drawList, new Vector2(textLeft, nameY), displayName, nameColor, 1.15f, FontWeight.Bold);
        var iconColor = hovered ? theme.Accent : Palette.WithAlpha(theme.TextMuted, 0.6f);
        AppSkin.Icon(drawList, linkIconPos, IconGlyph.Of(FontAwesomeIcon.ExternalLinkAlt), iconColor, 0.65f);
        Typography.Draw(drawList, new Vector2(textLeft, worldY), world, theme.TextMuted, TextStyles.Subheadline);

        ImGui.Dummy(new Vector2(width, avatarRadius * 2f + 8f * scale));
    }

    private void DrawProgressingSection(float width, float scale, PhoneTheme theme, TomestoneProgFight prog)
    {
        var drawList = ImGui.GetWindowDrawList();
        Typography.Draw(ImGui.GetCursorScreenPos(), "Progressing", theme.TextStrong, TextStyles.Headline);
        ImGui.Dummy(new Vector2(0f, 24f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var cardHeight = 64f * scale;
        var max = new Vector2(origin.X + width, origin.Y + cardHeight);

        Squircle.Fill(drawList, origin, max, 12f * scale, ImGui.GetColorU32(theme.GroupedCard));
        Squircle.Stroke(drawList, origin, max, 12f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.40f)), 1f * scale);
        var labelText = $"{prog.BossName} {prog.HpPercent:0.00}%";
        Typography.Draw(drawList, new Vector2(origin.X + 14f * scale, origin.Y + 11f * scale), labelText, theme.TextStrong, TextStyles.Body);
        var barY = origin.Y + 37f * scale;
        var barHeight = 11f * scale;
        var barLeft = origin.X + 14f * scale;
        var barWidth = width - 28f * scale;
        var barBgMin = new Vector2(barLeft, barY);
        var barBgMax = new Vector2(barLeft + barWidth, barY + barHeight);
        Squircle.Fill(drawList, barBgMin, barBgMax, barHeight * 0.5f, ImGui.GetColorU32(new Vector4(0.18f, 0.16f, 0.28f, 0.95f)));

        var progRatio = Math.Clamp(prog.HpPercent / 100f, 0f, 1f);
        if (progRatio > 0f)
        {
            var fillMax = new Vector2(barLeft + barWidth * progRatio, barY + barHeight);
            Squircle.Fill(drawList, barBgMin, fillMax, barHeight * 0.5f, ImGui.GetColorU32(new Vector4(0.68f, 0.44f, 1.0f, 0.95f)));
        }

        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private void DrawSavageSection(float width, float scale, PhoneTheme theme, IReadOnlyList<TomestoneSavageTier> tiers)
    {
        Typography.Draw(ImGui.GetCursorScreenPos(), "Savage", theme.TextStrong, TextStyles.Headline);
        ImGui.Dummy(new Vector2(0f, 24f * scale));
        for (var i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            DrawSavageTierCard(width, scale, theme, tier);
            if (i < tiers.Count - 1)
            {
                ImGui.Dummy(new Vector2(0f, 8f * scale));
            }
        }
    }

    private void DrawSavageTierCard(float width, float scale, PhoneTheme theme, TomestoneSavageTier tier)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var cardHeight = 60f * scale;
        var max = new Vector2(origin.X + width, origin.Y + cardHeight);
        Squircle.Fill(drawList, origin, max, 12f * scale, ImGui.GetColorU32(theme.GroupedCard));
        Squircle.Stroke(drawList, origin, max, 12f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.Separator, 0.40f)), 1f * scale);
        var textX = origin.X + 14f * scale;
        Typography.Draw(drawList, new Vector2(textX, origin.Y + 9f * scale), tier.Series, theme.TextMuted, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(textX, origin.Y + 26f * scale), tier.TierName, theme.TextStrong, TextStyles.Body);
        var rightCenterY = origin.Y + cardHeight * 0.5f;
        var iconPos = new Vector2(origin.X + width - 24f * scale, rightCenterY);
        if (tier.IsCleared)
        {
            AppSkin.Icon(drawList, iconPos, IconGlyph.Of(FontAwesomeIcon.CheckCircle), new Vector4(0.35f, 0.85f, 0.45f, 0.95f), 1.0f);
        }
        else
        {
            AppSkin.Icon(drawList, iconPos, IconGlyph.Of(FontAwesomeIcon.Lock), new Vector4(0.48f, 0.48f, 0.54f, 0.80f), 0.85f);
        }

        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private void DrawUltimatesSection(float width, float scale, PhoneTheme theme, IReadOnlyList<TomestoneRaidBadge> ultimates)
    {
        Typography.Draw(ImGui.GetCursorScreenPos(), "Ultimates", theme.TextStrong, TextStyles.Headline);
        ImGui.Dummy(new Vector2(0f, 24f * scale));

        var columns = 3;
        var colWidth = width / columns;
        var startPos = ImGui.GetCursorScreenPos();
        var radius = 24f * scale;
        var rowHeight = radius * 2f + 32f * scale;
        for (var i = 0; i < ultimates.Count; i++)
        {
            var col = i % columns;
            var row = i / columns;
            var center = new Vector2(startPos.X + colWidth * col + colWidth * 0.5f, startPos.Y + row * rowHeight + radius + 4f * scale);
            DrawUltimateDiamondBadge(center, radius, scale, theme, ultimates[i]);
        }

        var totalRows = (ultimates.Count + columns - 1) / columns;
        ImGui.Dummy(new Vector2(width, totalRows * rowHeight));
    }

    private void DrawUltimateDiamondBadge(Vector2 center, float radius, float scale, PhoneTheme theme, TomestoneRaidBadge badge)
    {
        var drawList = ImGui.GetWindowDrawList();
        Span<Vector2> pts = stackalloc Vector2[4];
        pts[0] = new Vector2(center.X, center.Y - radius);
        pts[1] = new Vector2(center.X + radius, center.Y);
        pts[2] = new Vector2(center.X, center.Y + radius);
        pts[3] = new Vector2(center.X - radius, center.Y);

        if (badge.IsCleared)
        {
            var bgCol = ImGui.GetColorU32(new Vector4(0.24f, 0.08f, 0.38f, 0.95f));
            var borderCol = ImGui.GetColorU32(new Vector4(0.88f, 0.28f, 1.0f, 1.0f));
            drawList.AddConvexPolyFilled(ref pts[0], 4, bgCol);
            drawList.AddPolyline(ref pts[0], 4, borderCol, ImDrawFlags.Closed, 2f * scale);
            Span<Vector2> inner = stackalloc Vector2[4];
            var innerR = radius - 3.5f * scale;

            inner[0] = new Vector2(center.X, center.Y - innerR);
            inner[1] = new Vector2(center.X + innerR, center.Y);
            inner[2] = new Vector2(center.X, center.Y + innerR);
            inner[3] = new Vector2(center.X - innerR, center.Y);
            drawList.AddPolyline(ref inner[0], 4, borderCol, ImDrawFlags.Closed, 1f * scale);
            AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.Dragon), new Vector4(1f, 1f, 1f, 0.95f), 0.95f);
            Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + radius + 11f * scale), badge.Code, new Vector4(0.95f, 0.35f, 0.98f, 1f), TextStyles.Caption1);
        }
        else
        {
            var bgCol = ImGui.GetColorU32(new Vector4(0.14f, 0.14f, 0.18f, 0.85f));
            var borderCol = ImGui.GetColorU32(new Vector4(0.28f, 0.28f, 0.34f, 0.65f));

            drawList.AddConvexPolyFilled(ref pts[0], 4, bgCol);
            drawList.AddPolyline(ref pts[0], 4, borderCol, ImDrawFlags.Closed, 1.5f * scale);
            AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.Lock), new Vector4(0.48f, 0.48f, 0.54f, 0.80f), 0.85f);
            Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + radius + 11f * scale), badge.Code, new Vector4(0.45f, 0.45f, 0.52f, 0.9f), TextStyles.Caption1);
        }
    }

    private void DrawExtremesSection(float width, float scale, PhoneTheme theme, IReadOnlyList<TomestoneRaidBadge> extremes)
    {
        Typography.Draw(ImGui.GetCursorScreenPos(), "Extremes", theme.TextStrong, TextStyles.Headline);
        ImGui.Dummy(new Vector2(0f, 24f * scale));

        var columns = 3;
        var colWidth = width / columns;
        var startPos = ImGui.GetCursorScreenPos();
        var radius = 24f * scale;
        var rowHeight = radius * 2f + 32f * scale;
        for (var i = 0; i < extremes.Count; i++)
        {
            var col = i % columns;
            var row = i / columns;
            var center = new Vector2(startPos.X + colWidth * col + colWidth * 0.5f, startPos.Y + row * rowHeight + radius + 4f * scale);
            DrawExtremeHexagonBadge(center, radius, scale, theme, extremes[i]);
        }

        var totalRows = (extremes.Count + columns - 1) / columns;
        ImGui.Dummy(new Vector2(width, totalRows * rowHeight));
    }

    private void DrawExtremeHexagonBadge(Vector2 center, float radius, float scale, PhoneTheme theme, TomestoneRaidBadge badge)
    {
        var drawList = ImGui.GetWindowDrawList();
        Span<Vector2> pts = stackalloc Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            var angle = MathF.PI / 3f * i - MathF.PI / 6f;
            pts[i] = new Vector2(center.X + radius * MathF.Cos(angle), center.Y + radius * MathF.Sin(angle));
        }

        if (badge.IsCleared)
        {
            var bgCol = ImGui.GetColorU32(new Vector4(0.40f, 0.08f, 0.08f, 0.95f));
            var borderCol = ImGui.GetColorU32(new Vector4(1.0f, 0.22f, 0.18f, 1.0f));
            drawList.AddConvexPolyFilled(ref pts[0], 6, bgCol);
            drawList.AddPolyline(ref pts[0], 6, borderCol, ImDrawFlags.Closed, 2f * scale);
            Span<Vector2> inner = stackalloc Vector2[6];

            var innerR = radius - 3.5f * scale;
            for (var i = 0; i < 6; i++)
            {
                var angle = MathF.PI / 3f * i - MathF.PI / 6f;
                inner[i] = new Vector2(center.X + innerR * MathF.Cos(angle), center.Y + innerR * MathF.Sin(angle));
            }

            drawList.AddPolyline(ref inner[0], 6, borderCol, ImDrawFlags.Closed, 1f * scale);
            AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.Dragon), new Vector4(1f, 1f, 1f, 0.95f), 0.95f);
            Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + radius + 11f * scale), badge.Code, new Vector4(1.0f, 0.25f, 0.20f, 1f), TextStyles.Caption1);
        }
        else
        {
            var bgCol = ImGui.GetColorU32(new Vector4(0.14f, 0.14f, 0.18f, 0.85f));
            var borderCol = ImGui.GetColorU32(new Vector4(0.28f, 0.28f, 0.34f, 0.65f));
            drawList.AddConvexPolyFilled(ref pts[0], 6, bgCol);
            drawList.AddPolyline(ref pts[0], 6, borderCol, ImDrawFlags.Closed, 1.5f * scale);
            AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.Lock), new Vector4(0.48f, 0.48f, 0.54f, 0.80f), 0.85f);
            Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + radius + 11f * scale), badge.Code, new Vector4(0.45f, 0.45f, 0.52f, 0.9f), TextStyles.Caption1);
        }
    }
}