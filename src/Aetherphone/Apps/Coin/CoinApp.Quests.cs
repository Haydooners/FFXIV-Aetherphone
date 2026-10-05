using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const float QuestPad = 12f;
    private const float QuestLineGap = 6f;
    private const float QuestBarHeight = 4f;
    private const float QuestCheckSize = 18f;
    private const float QuestFooterGap = 8f;
    private const string FrozenReason = "frozen";
    private const string PausedReason = "paused";
    private const string DailyCapReason = "daily_cap";
    private const string RuleCapReason = "rule_cap";

    private CachedText[] questTitles = Array.Empty<CachedText>();
    private CachedText[] questProgress = Array.Empty<CachedText>();
    private Spring[] questFill = Array.Empty<Spring>();
    private CachedText questResetText;
    private Vector2 questClaimAnchor;

    private void PrimeQuests()
    {
        for (var index = 0; index < questFill.Length; index++)
        {
            questFill[index].SnapTo(0f);
        }
    }

    private void ConsumeQuestClaim()
    {
        var claim = quests.TakeClaimResult();
        if (claim is null)
        {
            return;
        }

        var award = claim.Award;
        if (award is { Granted: true, Amount: > 0 })
        {
            UiFeedback.Play(UiSound.Payout);
            floats.Spawn(Loc.T(L.Coin.CheckInReward, NumberText.Group(award.Amount)), questClaimAnchor);
            return;
        }

        if (string.Equals(award.Reason, FrozenReason, StringComparison.Ordinal))
        {
            confirm.Alert(Loc.T(L.Coin.FrozenAlertTitle), Loc.T(L.Coin.FrozenAlertBody), Loc.T(L.Common.Close));
            return;
        }

        if (string.Equals(award.Reason, PausedReason, StringComparison.Ordinal))
        {
            floats.Spawn(Loc.T(L.Coin.PausedTitle), questClaimAnchor, true);
            return;
        }

        if (string.Equals(award.Reason, DailyCapReason, StringComparison.Ordinal)
            || string.Equals(award.Reason, RuleCapReason, StringComparison.Ordinal))
        {
            floats.Spawn(Loc.T(L.Coin.CapReached), questClaimAnchor, true);
        }
    }

    private float DrawQuests(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        quests.EnsureCurrent();
        var board = quests.Board;
        if (board is null || board.Quests.Length == 0)
        {
            return origin.Y;
        }

        var items = board.Quests;
        EnsureQuestSlots(items.Length);
        var headerTop = origin.Y + CoinArt.SectionGap * scale;
        var cursorY = headerTop + CoinArt.SectionHeader(drawList, new Vector2(origin.X, headerTop), width,
            Loc.T(L.Coin.QuestsTitle), ui.TitleInk, 0f, scale);
        var total = 0f;
        for (var index = 0; index < items.Length; index++)
        {
            total += QuestRowHeight(items[index], index, width, scale);
        }

        var min = new Vector2(origin.X, cursorY);
        var max = new Vector2(origin.X + width, cursorY + total);
        var footHeight = Typography.LineHeight(TextStyles.Footnote);
        var bottom = max.Y + QuestFooterGap * scale + footHeight;
        if (!ImGui.IsRectVisible(min, new Vector2(max.X, bottom)))
        {
            return bottom;
        }

        CoinArt.Card(drawList, ui, min, max, scale);
        var rowTop = min.Y;
        for (var index = 0; index < items.Length; index++)
        {
            var height = QuestRowHeight(items[index], index, width, scale);
            var row = new Rect(new Vector2(min.X, rowTop), new Vector2(max.X, rowTop + height));
            if (index > 0)
            {
                DrawRowHairline(drawList, row, scale);
            }

            DrawQuestRow(drawList, row, items[index], index, scale);
            rowTop += height;
        }

        DrawQuestFooter(drawList, new Vector2(origin.X + Metrics.Space.Lg * scale, max.Y + QuestFooterGap * scale),
            max.X - Metrics.Space.Lg * scale, board.ResetsAtUnix, scale);
        return bottom;
    }

    private void EnsureQuestSlots(int count)
    {
        if (questTitles.Length >= count)
        {
            return;
        }

        questTitles = new CachedText[count];
        questProgress = new CachedText[count];
        questFill = new Spring[count];
    }

    private float QuestRowHeight(CoinQuestDto quest, int index, float width, float scale)
    {
        var textWidth = QuestTextWidth(quest, width, scale);
        var titleHeight = Typography.MeasureWrappedBlock(QuestTitle(quest, index), TextStyles.BodyEmphasized,
            textWidth).Y;
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var content = MathF.Max(CoinArt.RowTile * scale, titleHeight + QuestLineGap * scale + lineHeight);
        return MathF.Max(content + QuestPad * scale * 2f, CoinArt.RowHeight * scale);
    }

    private float QuestTextWidth(CoinQuestDto quest, float width, float scale)
    {
        var trailing = QuestTrailingWidth(quest, scale);
        var reserve = trailing > 0f ? trailing + CoinArt.ValueGap * scale : 0f;
        return MathF.Max(1f, width - Metrics.Space.Lg * scale * 2f - (CoinArt.RowTile + CoinArt.TextGap) * scale -
                             reserve);
    }

    private float QuestTrailingWidth(CoinQuestDto quest, float scale)
    {
        if (quest.Claimed)
        {
            return QuestCheckSize * scale;
        }

        if (CoinQuests.IsClaimable(quest))
        {
            return CoinArt.CapsuleWidth(Loc.T(L.Coin.QuestClaim), Button.SmallHeight * scale);
        }

        return quest.Amount > 0
            ? CoinArt.PriceWidth(NumberText.Group(quest.Amount), TextStyles.SubheadlineEmphasized)
            : 0f;
    }

    private void DrawQuestRow(ImDrawListPtr drawList, Rect row, CoinQuestDto quest, int index, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var appId = quest.App.Length == 0 ? Id : quest.App;
        var openable = !string.Equals(appId, Id, StringComparison.Ordinal) && navigation.IsAvailable(appId);
        var claimable = CoinQuests.IsClaimable(quest);
        var trailingWidth = QuestTrailingWidth(quest, scale);
        var trailingRight = row.Max.X - pad;
        var capsuleHeight = Button.SmallHeight * scale;
        var capsule = new Rect(new Vector2(trailingRight - trailingWidth, row.Center.Y - capsuleHeight * 0.5f),
            new Vector2(trailingRight, row.Center.Y + capsuleHeight * 0.5f));
        var overCapsule = claimable && UiInteract.Hover(capsule.Min, capsule.Max);
        var hovered = openable && !overCapsule && CoinArt.RowInteraction(drawList, ui, row, scale);

        var tile = CoinArt.RowTile * scale;
        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, row.Center.Y);
        IconTile.DrawApp(drawList, appId, tileCenter, tile, IconTile.Surface(AppAccents.For(appId)));

        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        var textWidth = QuestTextWidth(quest, row.Width, scale);
        var textRight = textLeft + textWidth;
        var title = QuestTitle(quest, index);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.BodyEmphasized, textWidth).Y;
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var blockHeight = titleHeight + QuestLineGap * scale + lineHeight;
        var top = row.Center.Y - blockHeight * 0.5f;
        Typography.DrawWrappedLeft(new Vector2(textLeft, top), title, ui.TitleInk, TextStyles.BodyEmphasized,
            textWidth);

        var lineTop = top + titleHeight + QuestLineGap * scale;
        var complete = CoinQuests.IsComplete(quest);
        var progress = quest.Claimed ? Loc.T(L.Coin.QuestClaimed) : QuestProgressText(quest, index);
        var progressStyle = TextStyles.FootnoteEmphasized;
        var progressWidth = WidgetText.TabularWidth(progress, progressStyle);
        WidgetText.Tabular(drawList, new Vector2(textRight - progressWidth, lineTop), progress,
            complete ? ui.Accent : ui.MutedInk, progressStyle);
        var barRight = textRight - progressWidth - CoinArt.ValueGap * scale;
        if (barRight > textLeft)
        {
            var barHeight = QuestBarHeight * scale;
            var barTop = lineTop + (lineHeight - barHeight) * 0.5f;
            var fill = Math.Clamp(Step(ref questFill[index], CoinQuests.Fraction(quest), Motion.Sheet), 0f, 1f);
            CoinArt.Bar(drawList, new Vector2(textLeft, barTop), new Vector2(barRight, barTop + barHeight), fill,
                Palette.WithAlpha(ui.TitleInk, EarnBarTrackAlpha), ui.Accent);
        }

        DrawQuestTrailing(drawList, quest, index, capsule, claimable, scale);
        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            navigation.Open(appId);
        }
    }

    private void DrawQuestTrailing(ImDrawListPtr drawList, CoinQuestDto quest, int index, Rect capsule,
        bool claimable, float scale)
    {
        if (quest.Claimed)
        {
            ProgressRing.CenterIcon(drawList, capsule.Center, FontAwesomeIcon.CheckCircle, ui.Accent,
                QuestCheckSize * scale);
            return;
        }

        if (claimable)
        {
            var claiming = quests.IsClaiming(quest.Id);
            var pressed = CoinArt.Capsule(drawList, ui, ImGui.GetID($"coin.quest.claim{index}"), capsule,
                Loc.T(L.Coin.QuestClaim), CapsuleTone.Filled, !quests.AnyClaiming);
            if (claiming || !pressed)
            {
                return;
            }

            questClaimAnchor = new Vector2(capsule.Center.X, capsule.Min.Y - 6f * scale);
            UiFeedback.Play(UiSound.Tap);
            quests.Claim(quest.Id);
            return;
        }

        if (quest.Amount <= 0)
        {
            return;
        }

        var style = TextStyles.SubheadlineEmphasized;
        var lineHeight = Typography.LineHeight(style);
        var amount = NumberText.Group(quest.Amount);
        CoinArt.Price(drawList, new Vector2(capsule.Max.X - CoinArt.PriceWidth(amount, style),
            capsule.Center.Y - lineHeight * 0.5f), amount, ui.MutedInk, style);
    }

    private void DrawQuestFooter(ImDrawListPtr drawList, Vector2 topLeft, float right, long resetsAtUnix,
        float scale)
    {
        if (resetsAtUnix <= 0)
        {
            return;
        }

        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var glyph = ResetGlyph * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(topLeft.X + glyph * 0.5f, topLeft.Y + lineHeight * 0.5f),
            FontAwesomeIcon.HourglassHalf, ui.MutedInk, glyph);
        var textLeft = topLeft.X + glyph + Metrics.Space.Xs * scale;
        var text = questResetText.IsCurrent(resetsAtUnix)
            ? questResetText.Value
            : questResetText.Store(resetsAtUnix, Loc.T(L.Coin.QuestsReset, TimeText.Clock(resetsAtUnix)));
        Typography.Draw(drawList, new Vector2(textLeft, topLeft.Y),
            Typography.FitText(text, MathF.Max(1f, right - textLeft), TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
    }

    private string QuestTitle(CoinQuestDto quest, int index)
    {
        ref var cache = ref questTitles[index];
        var key = ((long)quest.Id.GetHashCode() << 32) | (uint)quest.Target;
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, CoinQuests.Title(quest));
    }

    private string QuestProgressText(CoinQuestDto quest, int index)
    {
        ref var cache = ref questProgress[index];
        var key = CoinQuests.TextKey(quest) ^ ((long)quest.Id.GetHashCode() << 16);
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, CoinQuests.ProgressText(quest));
    }
}
