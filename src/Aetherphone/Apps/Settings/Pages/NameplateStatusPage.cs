using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Honorific;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class NameplateStatusPage : ISettingsPage
{
    private const int MaxChildren = 4;

    private static readonly string[] ChildIds = BuildChildIds();

    private readonly NameplateTitleService titles;
    private readonly ISettingsNavigator navigator;
    private readonly NameplateStatusPage? childPage;
    private readonly NameplateStage stage = new();
    private readonly string[] handleLabels = new string[3];
    private readonly string[] longTitleLabels = new string[2];
    private NameplateStatus status = NameplateStatus.Jam;
    private string buffer = string.Empty;
    private bool editing;
    private string sampleTemplate = string.Empty;
    private NameplateTitle sample = NameplateTitle.None;
    private long sampleTick;
    private int turnSecondsShown;
    private string turnSecondsTemplate = string.Empty;
    private string turnSecondsLine = string.Empty;
    private string lengthSource = string.Empty;
    private string lengthTemplate = string.Empty;
    private string lengthLine = string.Empty;
    private string tokensTemplate = string.Empty;
    private string tokensLine = string.Empty;

    public NameplateStatusPage(NameplateTitleService titles, ISettingsNavigator navigator,
        NameplateStatusPage? childPage)
    {
        this.titles = titles;
        this.navigator = navigator;
        this.childPage = childPage;
    }

    public string Title => Loc.T(NameplateStatusCatalog.For(status).Label);

    public string Summary => string.Empty;

    public FontAwesomeIcon Icon => NameplateStatusCatalog.For(status).Icon;

    public Vector4 Tint => NameplateStatusCatalog.For(status).Tint;

    public bool IsHidden => true;

    public void Show(NameplateStatus shownStatus)
    {
        status = shownStatus;
        buffer = titles.Settings.Template(shownStatus);
        editing = false;
        sampleTemplate = string.Empty;
        tokensTemplate = string.Empty;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            DrawPreview(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawShowSwitch(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            DrawText(theme, scale);
            DrawExtras(theme, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private void DrawPreview(PhoneTheme theme, float scale)
    {
        var template = titles.TemplateFor(status);
        var tick = status == NameplateStatus.NowPlaying ? titles.TurnTick : 0L;
        if (!ReferenceEquals(template, sampleTemplate) || tick != sampleTick)
        {
            sampleTemplate = template;
            sampleTick = tick;
            sample = titles.Sample(status);
        }

        var name = titles.CharacterName.Length > 0 ? titles.CharacterName : Loc.T(L.Nameplate.Example);
        stage.Draw(theme, scale, name, sample);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(LengthLine(), theme);
    }

    private void DrawShowSwitch(PhoneTheme theme)
    {
        ref readonly var info = ref NameplateStatusCatalog.For(status);
        var settings = titles.Settings;
        var shown = settings.Shows(status);
        var card = GroupCard.Begin(theme, 1);
        var next = SettingsRow.Switch(card.NextRow(), info.Icon, info.Tint, Loc.T(L.Nameplate.Show), shown, theme,
            Loc.T(info.Hint), "nameplate.detail.show");
        card.End();
        if (next == shown)
        {
            return;
        }

        settings.Set(status, next);
        titles.Commit();
    }

    private void DrawText(PhoneTheme theme, float scale)
    {
        var custom = status == NameplateStatus.Custom;
        SettingsSection.Header(Loc.T(L.Nameplate.Text), theme);
        var hint = custom ? Loc.T(L.Nameplate.CustomPlaceholder) : NameplateTitleService.DefaultTemplate(status);
        var changed = SettingsForm.TextField("##nameplate.template", hint, ref buffer, theme,
            NameplateTitleSettings.MaxTemplateLength, ImGuiInputTextFlags.None, out var active);
        if (changed)
        {
            titles.Settings.SetTemplate(status, buffer);
            titles.Refresh();
        }

        if (editing && !active)
        {
            titles.Commit();
        }

        editing = active;
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(custom ? Loc.T(L.Nameplate.CustomHint) : Loc.T(L.Nameplate.TextHint), theme);
        var tokens = TokensLine();
        if (tokens.Length > 0)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            SettingsSection.Hint(tokens, theme);
        }

        if (custom || titles.Settings.Template(status).Length == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        var resetCard = GroupCard.Begin(theme, 1);
        var reset = SettingsRow.Action(resetCard.NextRow(), Loc.T(L.Nameplate.Reset), theme.Accent, theme);
        resetCard.End();
        if (!reset)
        {
            return;
        }

        buffer = string.Empty;
        titles.Settings.SetTemplate(status, string.Empty);
        titles.Commit();
    }

    private void DrawExtras(PhoneTheme theme, float scale)
    {
        var settings = titles.Settings;
        if (status == NameplateStatus.NowPlaying)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
            var card = GroupCard.Begin(theme, 1);
            var pcMedia = SettingsRow.Bool(card.NextRow(), Loc.T(L.Nameplate.PcMedia), settings.IncludePcMedia, theme,
                "nameplate.pcMedia");
            card.End();
            if (pcMedia != settings.IncludePcMedia)
            {
                settings.IncludePcMedia = pcMedia;
                titles.Commit();
            }

            DrawLongTitles(theme, scale);
            return;
        }

        if (status == NameplateStatus.Gamba)
        {
            DrawChildren(theme, scale);
            return;
        }

        if (status != NameplateStatus.Handle)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        SettingsSection.Header(Loc.T(L.Nameplate.HandleApp), theme);
        handleLabels[0] = Loc.T(L.Apps.Chirper);
        handleLabels[1] = Loc.T(L.Apps.Aethergram);
        handleLabels[2] = Loc.T(L.Apps.Velvet);
        var appCard = GroupCard.Begin(theme, 1);
        var picked = SegmentStrip.Draw("nameplate.handleApp", appCard.NextRow(), handleLabels, (int)settings.HandleApp,
            theme);
        appCard.End();
        if (picked == (int)settings.HandleApp)
        {
            return;
        }

        settings.HandleApp = (NameplateHandleApp)picked;
        sampleTemplate = string.Empty;
        titles.Commit();
    }

    private void DrawChildren(PhoneTheme theme, float scale)
    {
        Span<NameplateStatus> children = stackalloc NameplateStatus[MaxChildren];
        var count = NameplateStatusCatalog.ChildrenOf(status, children);
        if (count == 0 || childPage is null)
        {
            return;
        }

        var settings = titles.Settings;
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        SettingsSection.Header(Loc.T(L.Casino.GameSlots), theme);
        var card = GroupCard.Begin(theme, count);
        for (var index = 0; index < count; index++)
        {
            var child = children[index];
            ref readonly var info = ref NameplateStatusCatalog.For(child);
            var value = Loc.T(settings.Shows(child) ? L.Common.On : L.Common.Off);
            if (!SettingsRow.Link(card.NextRow(), info.Icon, info.Tint, Loc.T(info.Label), value, theme,
                    id: ChildIds[NameplateStatusCatalog.IndexOf(child)]))
            {
                continue;
            }

            childPage.Show(child);
            navigator.Open(childPage);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(Loc.T(L.Nameplate.GambaSlotsHint), theme);
    }

    private void DrawLongTitles(PhoneTheme theme, float scale)
    {
        var settings = titles.Settings;
        var takeTurns = settings.LongTitles == NameplateLongTitles.TakeTurns;
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        SettingsSection.Header(Loc.T(L.Nameplate.LongTitles), theme);
        longTitleLabels[0] = Loc.T(L.Nameplate.TakeTurns);
        longTitleLabels[1] = Loc.T(L.Nameplate.Shorten);
        var card = GroupCard.Begin(theme, takeTurns ? 3 : 1);
        var picked = SegmentStrip.Draw("nameplate.longTitles", card.NextRow(), longTitleLabels,
            (int)settings.LongTitles, theme);
        if (takeTurns)
        {
            DrawTurnSeconds(ref card, theme);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        SettingsSection.Hint(Loc.T(takeTurns ? L.Nameplate.TakeTurnsHint : L.Nameplate.ShortenHint), theme);
        if (picked == (int)settings.LongTitles)
        {
            return;
        }

        settings.LongTitles = (NameplateLongTitles)picked;
        sampleTemplate = string.Empty;
        titles.Commit();
    }

    private void DrawTurnSeconds(ref GroupCard card, PhoneTheme theme)
    {
        const float smallest = NameplateTitleSettings.MinimumTurnSeconds;
        const float span = NameplateTitleSettings.MaximumTurnSeconds - NameplateTitleSettings.MinimumTurnSeconds;
        var settings = titles.Settings;
        SettingsRow.Info(card.NextRow(), Loc.T(L.Nameplate.SwitchEvery), TurnSecondsLine(settings.TurnSeconds),
            theme, "nameplate.switchEvery");
        var slider = Slider.Draw("nameplate.turnSeconds", card.NextRow(), (settings.TurnSeconds - smallest) / span,
            theme, 0f, 0f);
        var seconds = (int)MathF.Round(smallest + slider.Value * span);
        if ((slider.Dragging || slider.Released) && seconds != settings.TurnSeconds)
        {
            settings.TurnSeconds = seconds;
        }

        if (slider.Released)
        {
            titles.Commit();
        }
    }

    private string TurnSecondsLine(int seconds)
    {
        var template = Loc.T(L.Nameplate.TurnSeconds);
        if (seconds != turnSecondsShown || !ReferenceEquals(template, turnSecondsTemplate))
        {
            turnSecondsShown = seconds;
            turnSecondsTemplate = template;
            turnSecondsLine = string.Format(template, seconds);
        }

        return turnSecondsLine;
    }

    private string LengthLine()
    {
        var template = Loc.T(L.Nameplate.Length);
        if (!ReferenceEquals(sample.Text, lengthSource) || !ReferenceEquals(template, lengthTemplate))
        {
            lengthSource = sample.Text;
            lengthTemplate = template;
            lengthLine = string.Format(template, sample.Text.Length, NameplateTitleText.MaxLength);
        }

        return lengthLine;
    }

    private string TokensLine()
    {
        var tokens = NameplateStatusCatalog.For(status).Tokens;
        if (tokens.Length == 0)
        {
            return string.Empty;
        }

        var template = Loc.T(L.Nameplate.Tokens);
        if (!ReferenceEquals(template, tokensTemplate))
        {
            tokensTemplate = template;
            tokensLine = string.Format(template, tokens);
        }

        return tokensLine;
    }

    private static string[] BuildChildIds()
    {
        var ids = new string[NameplateStatusCatalog.All.Length];
        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = "nameplate.child." + NameplateStatusCatalog.All[index].Status;
        }

        return ids;
    }
}
