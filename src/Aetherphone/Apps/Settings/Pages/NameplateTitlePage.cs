using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Honorific;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class NameplateTitlePage : ISettingsPage
{
    private const float StageHeight = 132f;
    private const float StageInset = 18f;
    private const float NameTitleGap = 4f;
    private const float HaloRadius = 1.4f;
    private const float SwatchRadius = 10f;
    private const float SwatchGap = 8f;
    private const float SwatchRing = 2f;
    private const string OpenQuote = "《";
    private const string CloseQuote = "》";

    private static readonly Vector4 StageTop = new(0.05f, 0.09f, 0.13f, 1f);
    private static readonly Vector4 StageBottom = new(0.08f, 0.19f, 0.18f, 1f);
    private static readonly Vector4 NameInk = new(0.95f, 0.96f, 1f, 1f);
    private static readonly Vector4 NameHalo = new(0.04f, 0.16f, 0.29f, 1f);
    private static readonly Vector4 PageTint = new(0.11f, 0.50f, 0.58f, 1f);
    private static readonly Vector4 BusyTint = new(0.45f, 0.47f, 0.55f, 1f);
    private static readonly Vector4 RadioTint = AccentRing.Orange;

    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Nameplate.Enabled),
        new(L.Nameplate.MogCast),
        new(L.Nameplate.JamRow),
        new(L.Nameplate.Radio),
        new(L.Nameplate.Muster),
        new(L.Nameplate.SocialApps),
        new(L.Nameplate.Busy),
        new(L.Nameplate.NowPlaying),
        new(L.Nameplate.Handle),
        new(L.Nameplate.Look),
        new(L.Nameplate.Position),
    };

    private readonly NameplateTitleService titles;
    private readonly string[] styleLabels = new string[3];
    private readonly string[] positionLabels = new string[2];
    private readonly string[] handleLabels = new string[2];
    private string quotedSource = string.Empty;
    private string quotedTitle = string.Empty;
    private string characterSourceName = string.Empty;
    private string characterSourceTemplate = string.Empty;
    private string characterLine = string.Empty;

    public NameplateTitlePage(NameplateTitleService titles)
    {
        this.titles = titles;
    }

    public string Title => Loc.T(L.Nameplate.Title);

    public string Summary => !titles.Settings.Enabled ? Loc.T(L.Nameplate.Off) : titles.Current.Text;

    public FontAwesomeIcon Icon => FontAwesomeIcon.IdBadge;

    public Vector4 Tint => PageTint;

    public ReadOnlySpan<SettingsEntry> Entries => Searchable;

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            if (!titles.Available)
            {
                DrawNotInstalled(theme, scale);
                return;
            }

            DrawStage(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawMasterSwitch(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawStatuses(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawLook(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private static void DrawNotInstalled(PhoneTheme theme, float scale)
    {
        var card = GroupCard.Begin(theme, StageHeight);
        var stage = card.Bounds;
        card.End();
        PaintStage(stage, scale);
        var center = stage.Center;
        Typography.DrawCenteredHalo(center, Loc.T(L.Nameplate.NotInstalledTitle), NameInk, NameHalo,
            HaloRadius * scale, stage.Width - StageInset * 2f * scale, TextStyles.Headline);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        SettingsSection.Hint(Loc.T(L.Nameplate.NotInstalledBody), theme);
    }

    private void DrawStage(PhoneTheme theme, float scale)
    {
        var live = !titles.Current.IsNone;
        var shown = titles.Preview;
        if (shown.IsNone)
        {
            return;
        }

        var card = GroupCard.Begin(theme, StageHeight);
        var stage = card.Bounds;
        card.End();
        PaintStage(stage, scale);
        var name = titles.CharacterName.Length > 0 ? titles.CharacterName : Loc.T(L.Nameplate.Example);
        var maxWidth = stage.Width - StageInset * 2f * scale;
        var nameHeight = Typography.LineHeight(TextStyles.Title3);
        var titleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var gap = NameTitleGap * scale;
        var blockTop = stage.Center.Y - (nameHeight + gap + titleHeight) * 0.5f;
        var titleAbove = shown.Prefix;
        var nameCenterY = titleAbove ? blockTop + titleHeight + gap + nameHeight * 0.5f : blockTop + nameHeight * 0.5f;
        var titleCenterY = titleAbove ? blockTop + titleHeight * 0.5f : blockTop + nameHeight + gap + titleHeight * 0.5f;
        Typography.DrawCenteredHalo(new Vector2(stage.Center.X, nameCenterY), name, NameInk, NameHalo,
            HaloRadius * scale, maxWidth, TextStyles.Title3);
        var look = shown.Look;
        var glow = look.Glow ?? look.Color3 ?? NameplatePalette.DarkGlow;
        Typography.DrawCenteredHalo(new Vector2(stage.Center.X, titleCenterY), Quoted(shown.Text),
            new Vector4(look.Color, 1f), new Vector4(glow, 1f), HaloRadius * scale, maxWidth,
            TextStyles.SubheadlineEmphasized);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var caption = live
            ? Loc.T(L.Nameplate.Live)
            : titles.Settings.Enabled
                ? Loc.T(L.Nameplate.NothingActive)
                : Loc.T(L.Nameplate.Example);
        SettingsSection.Hint(caption, theme);
    }

    private static void PaintStage(Rect stage, float scale) =>
        Squircle.FillVerticalGradient(ImGui.GetWindowDrawList(), stage.Min, stage.Max,
            Metrics.Radius.Grouped * scale, ImGui.GetColorU32(StageTop), ImGui.GetColorU32(StageBottom));

    private void DrawMasterSwitch(PhoneTheme theme, float scale)
    {
        var settings = titles.Settings;
        var card = GroupCard.Begin(theme, 1);
        var enabled = SettingsRow.Switch(card.NextRow(), FontAwesomeIcon.IdBadge, PageTint, Loc.T(L.Nameplate.Enabled),
            settings.Enabled, theme, id: "nameplate.enabled");
        card.End();
        if (enabled != settings.Enabled)
        {
            settings.Enabled = enabled;
            titles.Commit();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(Loc.T(L.Nameplate.PublicHint), theme);
        if (titles.CharacterName.Length == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
        SettingsSection.Hint(CharacterLine(), theme);
    }

    private void DrawStatuses(PhoneTheme theme, float scale)
    {
        var settings = titles.Settings;
        SettingsSection.Header(Loc.T(L.Nameplate.Statuses), theme);
        var rows = 9;
        if (settings.Shows(NameplateStatus.Jam))
        {
            rows++;
        }

        if (settings.Shows(NameplateStatus.NowPlaying))
        {
            rows++;
        }

        if (settings.Shows(NameplateStatus.Handle))
        {
            rows++;
        }

        var card = GroupCard.Begin(theme, rows);
        var changed = StatusRow(card.NextRow(), NameplateStatus.MogCast, FontAwesomeIcon.Tv,
            AppAccents.For("aetherstream"), L.Nameplate.MogCast, L.Nameplate.MogCastHint, "nameplate.mogcast", theme);
        changed |= StatusRow(card.NextRow(), NameplateStatus.Jam, FontAwesomeIcon.Music, AppAccents.For("music"),
            L.Nameplate.JamRow, L.Nameplate.JamHint, "nameplate.jam", theme);
        if (settings.Shows(NameplateStatus.Jam))
        {
            var showsName = SettingsRow.Bool(card.NextRow(), Loc.T(L.Nameplate.JamShowsName), settings.JamShowsName,
                theme, "nameplate.jamName");
            if (showsName != settings.JamShowsName)
            {
                settings.JamShowsName = showsName;
                changed = true;
            }
        }

        changed |= StatusRow(card.NextRow(), NameplateStatus.RadioOnAir, FontAwesomeIcon.BroadcastTower, RadioTint,
            L.Nameplate.Radio, L.Nameplate.RadioHint, "nameplate.radio", theme);
        changed |= StatusRow(card.NextRow(), NameplateStatus.Muster, FontAwesomeIcon.Users, AppAccents.For("muster"),
            L.Nameplate.Muster, L.Nameplate.MusterHint, "nameplate.muster", theme);
        changed |= StatusRow(card.NextRow(), NameplateStatus.SocialApps, FontAwesomeIcon.At,
            AppAccents.For(NameplateTitleService.ChirperAppId), L.Nameplate.SocialApps, L.Nameplate.SocialAppsHint,
            "nameplate.social", theme);
        changed |= StatusRow(card.NextRow(), NameplateStatus.Velvet, FontAwesomeIcon.Heart, AppAccents.For("velvet"),
            L.Apps.Velvet, L.Nameplate.VelvetHint, "nameplate.velvet", theme);
        changed |= StatusRow(card.NextRow(), NameplateStatus.Busy, FontAwesomeIcon.Moon, BusyTint, L.Nameplate.Busy,
            L.Nameplate.BusyHint, "nameplate.busy", theme);
        changed |= StatusRow(card.NextRow(), NameplateStatus.NowPlaying, FontAwesomeIcon.Headphones,
            AppAccents.For("music"), L.Nameplate.NowPlaying, L.Nameplate.NowPlayingHint, "nameplate.nowPlaying",
            theme);
        if (settings.Shows(NameplateStatus.NowPlaying))
        {
            var pcMedia = SettingsRow.Bool(card.NextRow(), Loc.T(L.Nameplate.PcMedia), settings.IncludePcMedia, theme,
                "nameplate.pcMedia");
            if (pcMedia != settings.IncludePcMedia)
            {
                settings.IncludePcMedia = pcMedia;
                changed = true;
            }
        }

        changed |= StatusRow(card.NextRow(), NameplateStatus.Handle, FontAwesomeIcon.UserTag,
            AppAccents.For(NameplateTitleService.AethergramAppId), L.Nameplate.Handle, L.Nameplate.HandleHint,
            "nameplate.handle", theme);
        if (settings.Shows(NameplateStatus.Handle))
        {
            handleLabels[0] = Loc.T(L.Apps.Chirper);
            handleLabels[1] = Loc.T(L.Apps.Aethergram);
            var picked = SegmentStrip.Draw("nameplate.handleApp", card.NextRow(), handleLabels,
                (int)settings.HandleApp, theme);
            if (picked != (int)settings.HandleApp)
            {
                settings.HandleApp = (NameplateHandleApp)picked;
                changed = true;
            }
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(Loc.T(L.Nameplate.StatusesHint), theme);
        if (changed)
        {
            titles.Commit();
        }
    }

    private bool StatusRow(Rect row, NameplateStatus status, FontAwesomeIcon icon, Vector4 tint, LocString label,
        LocString hint, string id, PhoneTheme theme)
    {
        var settings = titles.Settings;
        var shown = settings.Shows(status);
        var next = SettingsRow.Switch(row, icon, tint, Loc.T(label), shown, theme, Loc.T(hint), id);
        if (next == shown)
        {
            return false;
        }

        settings.Set(status, next);
        return true;
    }

    private void DrawLook(PhoneTheme theme, float scale)
    {
        var settings = titles.Settings;
        SettingsSection.Header(Loc.T(L.Nameplate.Look), theme);
        styleLabels[0] = Loc.T(L.Nameplate.StyleApp);
        styleLabels[1] = Loc.T(L.Nameplate.StyleMine);
        styleLabels[2] = Loc.T(L.Nameplate.StyleCustom);
        var styleCard = GroupCard.Begin(theme, 1);
        var style = SegmentStrip.Draw("nameplate.style", styleCard.NextRow(), styleLabels, (int)settings.Style, theme);
        styleCard.End();
        var changed = false;
        if (style != (int)settings.Style)
        {
            settings.Style = (NameplateTitleStyle)style;
            changed = true;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(StyleHint(settings.Style), theme);
        if (settings.Style == NameplateTitleStyle.Custom)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            var color = DrawSwatches(Loc.T(L.Nameplate.TextColor), settings.CustomColor, theme, scale);
            if (color >= 0)
            {
                settings.CustomColor = color;
                changed = true;
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            var glow = DrawSwatches(Loc.T(L.Nameplate.GlowColor), settings.CustomGlow, theme, scale);
            if (glow >= 0)
            {
                settings.CustomGlow = glow;
                changed = true;
            }
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        SettingsSection.Header(Loc.T(L.Nameplate.Position), theme);
        positionLabels[0] = Loc.T(L.Nameplate.Above);
        positionLabels[1] = Loc.T(L.Nameplate.Below);
        var positionCard = GroupCard.Begin(theme, 1);
        var position = SegmentStrip.Draw("nameplate.position", positionCard.NextRow(), positionLabels,
            settings.Prefix ? 0 : 1, theme);
        positionCard.End();
        if (position != (settings.Prefix ? 0 : 1))
        {
            settings.Prefix = position == 0;
            changed = true;
        }

        if (changed)
        {
            titles.Commit();
        }
    }

    private string StyleHint(NameplateTitleStyle style) => style switch
    {
        NameplateTitleStyle.MatchMine when titles.HasOwnLook => Loc.T(L.Nameplate.StyleMineHint),
        NameplateTitleStyle.MatchMine => Loc.T(L.Nameplate.StyleMineMissing),
        NameplateTitleStyle.AppColors => Loc.T(L.Nameplate.StyleAppHint),
        _ => string.Empty,
    };

    private static int DrawSwatches(string header, int selected, PhoneTheme theme, float scale)
    {
        SettingsSection.Header(header, theme);
        var card = GroupCard.Begin(theme, 1);
        var row = card.NextRow();
        card.End();
        var colors = NameplatePalette.Colors;
        var radius = SwatchRadius * scale;
        var gap = SwatchGap * scale;
        var available = row.Width;
        var step = MathF.Min(radius * 2f + gap, available / colors.Length);
        var drawRadius = MathF.Min(radius, step * 0.5f - SwatchRing * scale);
        var startX = row.Min.X + (available - step * colors.Length) * 0.5f + step * 0.5f;
        var drawList = ImGui.GetWindowDrawList();
        var picked = -1;
        for (var index = 0; index < colors.Length; index++)
        {
            var center = new Vector2(startX + step * index, row.Center.Y);
            var min = center - new Vector2(drawRadius, drawRadius);
            var max = center + new Vector2(drawRadius, drawRadius);
            drawList.AddCircleFilled(center, drawRadius, ImGui.GetColorU32(new Vector4(colors[index], 1f)), 32);
            drawList.AddCircle(center, drawRadius, ImGui.GetColorU32(theme.Hairline), 32, scale);
            var hovered = UiInteract.Hover(min, max);
            if (index == selected)
            {
                drawList.AddCircle(center, drawRadius + SwatchRing * scale * 1.5f, ImGui.GetColorU32(theme.Accent),
                    32, SwatchRing * scale);
            }
            else if (hovered)
            {
                drawList.AddCircle(center, drawRadius + SwatchRing * scale * 1.5f,
                    ImGui.GetColorU32(theme.TextMuted), 32, SwatchRing * scale * 0.5f);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                picked = index;
            }
        }

        return picked == selected ? -1 : picked;
    }

    private string Quoted(string text)
    {
        if (!ReferenceEquals(text, quotedSource))
        {
            quotedSource = text;
            quotedTitle = string.Concat(OpenQuote, text, CloseQuote);
        }

        return quotedTitle;
    }

    private string CharacterLine()
    {
        var template = Loc.T(L.Nameplate.Character);
        var name = titles.CharacterName;
        if (!ReferenceEquals(template, characterSourceTemplate) || !ReferenceEquals(name, characterSourceName))
        {
            characterSourceTemplate = template;
            characterSourceName = name;
            characterLine = string.Format(template, name);
        }

        return characterLine;
    }
}
