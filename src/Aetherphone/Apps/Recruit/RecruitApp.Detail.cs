using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Recruit;

internal sealed partial class RecruitApp
{
    private void DrawDetailScreen(in PhoneContext context, Rect area)
    {
        if (selectedListing is null){
            router.Pop();
            return;
        }

        var scale = UiScale.Current;
        var theme = ui.Theme;
        var accent = Accent;
        var listing = selectedListing;
        AppHeader.Draw(context, listing.Duty.Name, () => router.Pop());
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            ImGui.Dummy(new Vector2(0f, 4f * scale));

            DrawDetailHeroCard(width, scale, theme, accent, listing);
            ImGui.Dummy(new Vector2(0f, 12f * scale));

            ui.SectionLabel("SCHEDULE & PLAYSTYLE");
            DrawDetailSpecsCard(width, scale, theme, accent, listing);
            ImGui.Dummy(new Vector2(0f, 12f * scale));

            ui.SectionLabel("ROLES");
            DrawDetailRoles(width, scale, theme, accent, listing);
            ImGui.Dummy(new Vector2(0f, 14f * scale));

            if (!string.IsNullOrWhiteSpace(listing.Description)){
                ui.SectionLabel("DESCRIPTION");
                DrawDetailDescription(width, scale, theme, listing.Description);
                ImGui.Dummy(new Vector2(0f, 16f * scale));
            }

            DrawDetailActions(width, scale, theme, accent, listing);
            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawDetailHeroCard(float width, float scale, PhoneTheme theme, Vector4 accent, RecruitListing listing)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var pad = 14f * scale;
        var titleHeight = Typography.MeasureWrappedBlock(listing.Title, TextStyles.Headline, width - pad * 2f).Y;
        var height = titleHeight + 44f * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, origin, max, 12f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.SurfaceMuted, 0.6f)));
        Squircle.Stroke(drawList, origin, max, 12f * scale, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.35f)), 1f * scale);

        var badgeY = origin.Y + 12f * scale;
        var badgeX = origin.X + pad;
        DrawSmallBadge(drawList, ref badgeX, badgeY, listing.Kind.ToString().ToUpperInvariant(), accent, scale);
        badgeX += 6f * scale;
        DrawSmallBadge(drawList, ref badgeX, badgeY, listing.Duty.Category.ToString(), theme.TextMuted, scale);

        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + 34f * scale), listing.Title,
            theme.TextStrong, TextStyles.Headline, width - pad * 2f);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawDetailSpecsCard(float width, float scale, PhoneTheme theme, Vector4 accent, RecruitListing listing)
    {
        var drawList = ImGui.GetWindowDrawList();
        var card = GroupCard.Begin(theme, 5);

        var row1 = card.NextRow();
        Typography.Draw(drawList, row1.Min + new Vector2(12f * scale, 6f * scale),
            "Raid Days", theme.TextMuted, TextStyles.Caption1);
        Typography.Draw(drawList, row1.Min + new Vector2(12f * scale, 22f * scale),
            RecruitCatalog.FormatDays(listing.SelectedDays), theme.TextStrong, TextStyles.Subheadline);

        var row2 = card.NextRow();
        Typography.Draw(drawList, row2.Min + new Vector2(12f * scale, 6f * scale),
            "Time Range", theme.TextMuted, TextStyles.Caption1);
        Typography.Draw(drawList, row2.Min + new Vector2(12f * scale, 22f * scale),
            RecruitCatalog.FormatTimeRange(listing.StartMinuteOfDay, listing.EndMinuteOfDay, listing.Timezone),
            theme.TextStrong, TextStyles.Subheadline);

        var row3 = card.NextRow();
        Typography.Draw(drawList, row3.Min + new Vector2(12f * scale, 6f * scale),
            "Playstyle", theme.TextMuted, TextStyles.Caption1);
        Typography.Draw(drawList, row3.Min + new Vector2(12f * scale, 22f * scale),
            RecruitCatalog.CategoryName(listing.Playstyle), theme.TextStrong, TextStyles.Subheadline);

        var row4 = card.NextRow();
        Typography.Draw(drawList, row4.Min + new Vector2(12f * scale, 6f * scale),
            "Posted By", theme.TextMuted, TextStyles.Caption1);
        Typography.Draw(drawList, row4.Min + new Vector2(12f * scale, 22f * scale),
            listing.AuthorName, theme.TextStrong, TextStyles.Subheadline);

        var row5 = card.NextRow();
        Typography.Draw(drawList, row5.Min + new Vector2(12f * scale, 6f * scale),
            "World / Data Center", theme.TextMuted, TextStyles.Caption1);
        Typography.Draw(drawList, row5.Min + new Vector2(12f * scale, 22f * scale),
            listing.WorldDc, theme.TextStrong, TextStyles.Subheadline);
        card.End();
    }

    private void DrawDetailRoles(float width, float scale, PhoneTheme theme, Vector4 accent, RecruitListing listing)
    {
        var drawList = ImGui.GetWindowDrawList();
        var originX = ImGui.GetCursorScreenPos().X;
        var cursorX = originX;
        var startY = ImGui.GetCursorScreenPos().Y;
        var cursorY = startY;
        var rowHeight = 28f * scale;
        var maxX = originX + width;

        for (var roleIndex = 0; roleIndex < listing.RolesNeeded.Count; roleIndex++){
            var role = listing.RolesNeeded[roleIndex];
            var roleName = RecruitCatalog.RoleName(role);
            var roleColor = RoleBadgeColor(role);
            var pillWidth = Typography.Measure(roleName, TextStyles.Footnote).X + 24f * scale;
            
            if (cursorX + pillWidth > maxX && cursorX > originX){
                cursorX = originX;
                cursorY += rowHeight + 8f * scale;
            }

            DrawDetailRolePill(drawList, new Vector2(cursorX, cursorY), roleName, roleColor, scale);
            cursorX += pillWidth + 8f * scale;
        }

        var totalHeight = (cursorY - startY) + rowHeight + 6f * scale;
        ImGui.Dummy(new Vector2(width, totalHeight));
    }

    private static float DrawDetailRolePill(ImDrawListPtr drawList, Vector2 pos, string text, Vector4 color, float scale)
    {
        var textSize = Typography.Measure(text, TextStyles.Footnote);
        var padX = 12f * scale;
        var padY = 5f * scale;
        var height = textSize.Y + padY * 2f;
        var min = pos;
        var max = new Vector2(pos.X + textSize.X + padX * 2f, pos.Y + height);

        Squircle.Fill(drawList, min, max, 6f * scale, ImGui.GetColorU32(Palette.WithAlpha(color, 0.20f)));
        Squircle.Stroke(drawList, min, max, 6f * scale, ImGui.GetColorU32(Palette.WithAlpha(color, 0.60f)), 1f * scale);
        Typography.Draw(drawList, new Vector2(min.X + padX, min.Y + padY), text, color, TextStyles.Footnote);

        return max.X - min.X;
    }

    private void DrawDetailDescription(float width, float scale, PhoneTheme theme, string description)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var pad = 12f * scale;
        var textHeight = Typography.MeasureWrappedBlock(description, TextStyles.Body, width - pad * 2f).Y;
        var height = textHeight + pad * 2f;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, origin, max, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(theme.SurfaceMuted, 0.35f)));
        Typography.DrawWrappedLeft(origin + new Vector2(pad, pad), description, theme.TextStrong, TextStyles.Body, width - pad * 2f);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawDetailActions(float width, float scale, PhoneTheme theme, Vector4 accent, RecruitListing listing)
    {
        if (ImGui.Button($"Send /tell to {listing.AuthorName}", new Vector2(width, 36f * scale))){
            ImGui.SetClipboardText($"/tell {listing.AuthorName}@{listing.WorldDc} ");
        }
    }

    private static void DrawSmallBadge(ImDrawListPtr drawList, ref float cursorX, float centerY, string text, Vector4 color, float scale)
    {
        var size = Typography.Measure(text, TextStyles.Caption2);
        var padX = 6f * scale;
        var padY = 3f * scale;
        var min = new Vector2(cursorX, centerY - size.Y * 0.5f - padY);
        var max = new Vector2(cursorX + size.X + padX * 2f, centerY + size.Y * 0.5f + padY);
        Squircle.Fill(drawList, min, max, 4f * scale, ImGui.GetColorU32(Palette.WithAlpha(color, 0.2f)));
        Typography.DrawCentered(drawList, new Vector2(cursorX + (size.X + padX * 2f) * 0.5f, centerY), text, color, TextStyles.Caption2);
        cursorX = max.X;
    }
}