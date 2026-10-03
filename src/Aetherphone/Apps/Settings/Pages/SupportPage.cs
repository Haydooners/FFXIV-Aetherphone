using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class SupportPage : ISettingsPage
{
    public static readonly Vector4 PatreonCoral = new(1f, 0.259f, 0.302f, 1f);
    private static readonly Vector4 MemberAmber = new(0.96f, 0.72f, 0.20f, 1f);
    private static readonly Vector4 NameOrchid = new(0.80f, 0.42f, 0.98f, 1f);
    private static readonly Vector4 AutoTeal = new(0.13f, 0.63f, 0.60f, 1f);
    private static readonly Vector4 ThanksRose = new(0.98f, 0.33f, 0.54f, 1f);
    private const float PerkRowHeight = 58f;
    private const float PerkTileUnits = 29f;
    private const float TierHeaderHeight = 64f;
    private const float TierPerkRowHeight = 36f;
    private const float TierPadding = 16f;
    private const float TierGap = 14f;
    private const float PopularChipHeight = 22f;
    private const float PopularChipPadX = 10f;
    private const float CheckSize = 12f;
    private const float SectionGap = 22f;

    private readonly ISettingsNavigator navigator;
    private readonly ISettingsPage accountPage;
    private readonly AethernetSession session;
    private readonly SupportCard card = new();

    public SupportPage(ISettingsNavigator navigator, ISettingsPage accountPage, AethernetSession session)
    {
        this.navigator = navigator;
        this.accountPage = accountPage;
        this.session = session;
    }

    public string Title => Loc.T(L.Settings.SupportAetherphone);
    public string Summary => Loc.T(L.Settings.SupportBecomeMember);
    public FontAwesomeIcon Icon => FontAwesomeIcon.Heart;
    public Vector4 Tint => PatreonCoral;

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            card.Draw(theme);
            ImGui.Dummy(new Vector2(0f, SectionGap * scale));
            SettingsSection.Header(Loc.T(L.Settings.SupportPerksTitle), theme);
            DrawPerks(theme, scale);
            if (PatreonTiers.All.Length > 0)
            {
                ImGui.Dummy(new Vector2(0f, SectionGap * scale));
                SettingsSection.Header(Loc.T(L.Settings.SupportTiersTitle), theme);
                DrawTiers(theme, scale);
            }

            ImGui.Dummy(new Vector2(0f, SectionGap * scale));
            SettingsSection.Header(Loc.T(L.Settings.SupportMembershipTitle), theme);
            DrawMembership(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private static void DrawPerks(PhoneTheme theme, float scale)
    {
        var card = GroupCard.Begin(theme, 4, PerkRowHeight);
        card.SeparatorInset = SettingsRow.TileTextInset;
        DrawPerk(card.NextRow(), FontAwesomeIcon.Star, MemberAmber, Loc.T(L.Settings.SupportPerkBadge), theme, scale);
        DrawPerk(card.NextRow(), FontAwesomeIcon.Signature, NameOrchid, Loc.T(L.Settings.SupportPerkName), theme,
            scale);
        DrawPerk(card.NextRow(), FontAwesomeIcon.Sync, AutoTeal, Loc.T(L.Settings.SupportPerkAuto), theme, scale);
        DrawPerk(card.NextRow(), FontAwesomeIcon.HandHoldingHeart, ThanksRose, Loc.T(L.Settings.SupportPerkThanks),
            theme, scale);
        card.End();
    }

    private static void DrawPerk(Rect row, FontAwesomeIcon icon, Vector4 tint, string text, PhoneTheme theme,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var tile = PerkTileUnits * scale;
        var tileMin = new Vector2(row.Min.X + Metrics.Space.Md * scale, row.Center.Y - tile * 0.5f);
        var tileMax = tileMin + new Vector2(tile, tile);
        var tileCenter = (tileMin + tileMax) * 0.5f;
        Squircle.FillVerticalGradient(drawList, tileMin, tileMax, tile * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(Palette.Lighten(tint, 0.10f)), ImGui.GetColorU32(Palette.Darken(tint, 0.14f)));
        Material.EdgeSquircle(drawList, tileMin, tileMax, tile * Metrics.Radius.TileFactor, scale);
        ProgressRing.CenterIcon(drawList, tileCenter, icon, Vector4.One, tile * 0.52f);
        var textLeft = tileMax.X + Metrics.Space.Md * scale;
        var maxWidth = MathF.Max(1f, row.Max.X - Metrics.Space.Md * scale - textLeft);
        var block = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, maxWidth);
        Typography.DrawWrappedLeft(new Vector2(textLeft, row.Center.Y - block.Y * 0.5f), text, theme.TextStrong,
            TextStyles.Subheadline, maxWidth);
    }

    private void DrawMembership(PhoneTheme theme)
    {
        var member = session.CurrentUser is { } user && (user.Badges & (int)AccountBadges.Patreon) != 0;
        var card = GroupCard.Begin(theme, 1);
        card.SeparatorInset = SettingsRow.TileTextInset;
        var value = Loc.T(member ? L.Settings.SupportMemberActive : L.Settings.SupportMemberInactive);
        if (SettingsRow.Link(card.NextRow(), FontAwesomeIcon.Star, MemberAmber, Loc.T(L.Account.PatreonLink), value,
                theme))
        {
            navigator.Open(accountPage);
        }

        card.End();
    }

    private static void DrawTiers(PhoneTheme theme, float scale)
    {
        var tiers = PatreonTiers.All;
        for (var index = 0; index < tiers.Length; index++)
        {
            if (index > 0)
            {
                ImGui.Dummy(new Vector2(0f, TierGap * scale));
            }

            DrawTier(in tiers[index], theme, scale);
        }
    }

    private static void DrawTier(in PatreonTier tier, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padding = TierPadding * scale;
        var headerHeight = TierHeaderHeight * scale;
        var perkRow = TierPerkRowHeight * scale;
        var height = headerHeight + tier.Perks.Length * perkRow + padding;
        var max = origin + new Vector2(width, height);
        var radius = Metrics.Radius.Grouped * scale;
        Squircle.Fill(drawList, origin, max, radius, ImGui.GetColorU32(theme.GroupedCard));
        Squircle.FillCap(drawList, origin, new Vector2(max.X, origin.Y + headerHeight), radius,
            ImGui.GetColorU32(Palette.WithAlpha(tier.Accent, 0.28f)), true);
        Material.EdgeSquircle(drawList, origin, max, radius, scale);
        var name = Loc.T(tier.Name);
        var nameSize = Typography.Measure(name, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(origin.X + padding, origin.Y + padding * 0.6f), name, theme.TextStrong,
            TextStyles.Headline);
        var price = Loc.T(L.Settings.SupportPerMonth, tier.Price);
        Typography.Draw(drawList, new Vector2(origin.X + padding, origin.Y + padding * 0.6f + nameSize.Y),
            price, Palette.WithAlpha(theme.TextStrong, 0.72f), TextStyles.Footnote);
        if (tier.Popular)
        {
            DrawPopularChip(drawList, new Vector2(max.X - padding, origin.Y + padding * 0.6f + nameSize.Y * 0.5f),
                tier.Accent, scale);
        }

        var rowTop = origin.Y + headerHeight;
        for (var index = 0; index < tier.Perks.Length; index++)
        {
            var rowCenterY = rowTop + perkRow * (index + 0.5f);
            var check = new Vector2(origin.X + padding + CheckSize * scale * 0.5f, rowCenterY);
            ProgressRing.CenterIcon(drawList, check, FontAwesomeIcon.Check, tier.Accent, CheckSize * scale);
            var textLeft = origin.X + padding + CheckSize * scale + Metrics.Space.Sm * scale;
            var maxWidth = MathF.Max(1f, max.X - padding - textLeft);
            var perk = Typography.FitText(Loc.T(tier.Perks[index]), maxWidth, TextStyles.Subheadline);
            var perkSize = Typography.Measure(perk, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(textLeft, rowCenterY - perkSize.Y * 0.5f), perk, theme.TextStrong,
                TextStyles.Subheadline);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static void DrawPopularChip(ImDrawListPtr drawList, Vector2 rightCenter, Vector4 accent, float scale)
    {
        var label = Loc.T(L.Settings.SupportMostPopular);
        var size = Typography.Measure(label, TextStyles.Caption2);
        var height = PopularChipHeight * scale;
        var width = size.X + PopularChipPadX * 2f * scale;
        var min = new Vector2(rightCenter.X - width, rightCenter.Y - height * 0.5f);
        var max = new Vector2(rightCenter.X, rightCenter.Y + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(accent));
        Typography.Draw(drawList, new Vector2(min.X + PopularChipPadX * scale, rightCenter.Y - size.Y * 0.5f), label,
            Vector4.One, TextStyles.Caption2);
    }
}
