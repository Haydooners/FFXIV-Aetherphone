using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private string editDisplayName = string.Empty;
    private string editHandle = string.Empty;
    private string editIntro = string.Empty;
    private string editPronouns = string.Empty;
    private int editGender;
    private int editSexuality;
    private int editIntent;
    private int editRelationship;
    private readonly List<string> editRole = new();
    private readonly List<string> editKinks = new();
    private readonly List<string> editTags = new();
    private readonly List<string> editLimits = new();
    private volatile bool editBusy;
    private volatile bool editSaveSucceeded;
    private volatile bool editSaveFailed;
    private bool avatarEditing;
    private bool photoEditing;

    private void BeginEditProfile()
    {
        var me = store.Me;
        if (me is null)
        {
            return;
        }

        editDisplayName = me.DisplayName;
        editHandle = me.Handle;
        editIntro = me.Intro;
        editPronouns = me.Pronouns;
        editGender = VelvetGender.Sanitize(me.Gender);
        editSexuality = VelvetSexuality.Sanitize(me.Sexuality);
        editIntent = VelvetIntent.Sanitize(me.LookingFor);
        editRelationship = me.RelationshipStatus;
        editRole.Clear();
        editRole.AddRange(VelvetTags.Parse(me.Dynamic));
        editKinks.Clear();
        editKinks.AddRange(me.Kinks ?? Array.Empty<string>());
        editTags.Clear();
        editTags.AddRange(me.Tags);
        editLimits.Clear();
        editLimits.AddRange(me.Limits);
        editSaveSucceeded = false;
        editSaveFailed = false;
        avatarEditing = false;
        photoEditing = false;
    }

    private void DrawEditProfile(Rect area)
    {
        var scale = UiScale.Current;
        if (avatarEditing)
        {
            var context = new PhoneContext(area, theme, navigation);
            if (avatar.Draw(area, context, ui.Accent))
            {
                avatarEditing = false;
            }

            return;
        }

        if (photoEditing)
        {
            var context = new PhoneContext(area, theme, navigation);
            if (cardPhotos.Draw(area, context, ui.Accent))
            {
                photoEditing = false;
            }

            return;
        }

        if (editSaveSucceeded)
        {
            editSaveSucceeded = false;
            router.Pop(false);
            return;
        }

        if (VHeader.Push(area, Loc.T(L.Velvet.EditProfile)))
        {
            if (!HasUnsavedEdits())
            {
                router.Pop();
                return;
            }

            confirm.Ask(new ConfirmRequest
            {
                Message = Loc.T(L.Velvet.DiscardEdits),
                ConfirmLabel = Loc.T(L.Velvet.DiscardEditsConfirm),
                CancelLabel = Loc.T(L.Velvet.KeepEditing),
                Sheet = true,
                Confirm = () => router.Pop(),
            });
            return;
        }

        if (ui.HeaderAction(area, editBusy ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.Save), !editBusy))
        {
            SaveProfile();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            if (editSaveFailed)
            {
                Gap(10f);
                WrapText(Loc.T(L.Velvet.SaveFailed), VelvetTheme.Danger, TextStyles.Callout);
                Gap(4f);
            }

            Gap(8f);
            DrawEditPhotos();
            Gap(18f);
            DrawEditAvatar();
            Gap(14f);

            VSectionHeader.Card(PhoneIcons.User, Loc.T(L.Velvet.CardIdentity));
            Gap(4f);
            ui.Field(Loc.T(L.Velvet.DisplayNameLabel), "##ed_name", ref editDisplayName, 40, false);
            ui.Field(Loc.T(L.Velvet.HandleLabel), "##ed_handle", ref editHandle, 15, false);
            Gap(16f);

            VSectionHeader.Card(PhoneIcons.Feather, Loc.T(L.Velvet.CardAbout));
            Gap(4f);
            ui.Field(Loc.T(L.Velvet.IntroduceYourself), "##ed_intro", ref editIntro, 400, true);
            ui.Field(Loc.T(L.Velvet.PronounsLabel), "##ed_pronouns", ref editPronouns, 40, false);
            Gap(16f);

            VSectionHeader.Card(PhoneIcons.Gender, Loc.T(L.Velvet.CardGender));
            Gap(6f);
            DrawGenderPicker(ref editGender);
            Gap(16f);

            VSectionHeader.Card(PhoneIcons.Rainbow, Loc.T(L.Velvet.CardSexuality));
            Gap(6f);
            DrawSexualityPicker(ref editSexuality);
            Gap(16f);

            VSectionHeader.Card(PhoneIcons.Compass, Loc.T(L.Velvet.CardIntent));
            Gap(6f);
            DrawIntentEditor();
            Gap(16f);

            VSectionHeader.Card(PhoneIcons.Heart, Loc.T(L.Velvet.CardRole));
            Gap(6f);
            DrawTagFlow(VelvetSuggestions.Roles, editRole, VelvetTheme.Rose, true);
            Gap(16f);

            VSectionHeader.Card(PhoneIcons.Flame, Loc.T(L.Velvet.CardKinks));
            Gap(6f);
            DrawTagFlow(VelvetSuggestions.Kinks, editKinks, new Vector4(0.647f, 0.482f, 0.839f, 1f), true);
            Gap(16f);

            VSectionHeader.Card(PhoneIcons.Shield, Loc.T(L.Velvet.CardLimits));
            Gap(6f);
            DrawTagFlow(VelvetSuggestions.Limits, editLimits, VelvetTheme.Gold, true);
            Gap(16f);

            VSectionHeader.Card(PhoneIcons.HeartHandshake, Loc.T(L.Velvet.CardRelationship));
            Gap(6f);
            DrawRelationshipEditor();
            Gap(16f);

            VSectionHeader.Card(PhoneIcons.Hash, Loc.T(L.Velvet.CardTags));
            Gap(6f);
            DrawCategoryPicker(VelvetSuggestions.TagCategories, editTags);
            Gap(16f);

            Gap(40f);
        }
    }

    private const int MaxCardPhotos = 6;
    private const int CardPhotoColumns = 3;
    private const float CardPhotoAspect = 0.8f;
    private const float CardPhotoGap = 8f;
    private const float CardPhotoRounding = 12f;
    private const float CardPhotoBadgeHeight = 20f;
    private const float CardPhotoBadgePad = 8f;
    private const float CardPhotoBadgeInset = 8f;
    private const float CardPhotoSpinnerRadius = 8f;
    private const float PreviewButtonHeight = 38f;
    private const int PhotoSheetMaxItems = 2;

    private readonly ActionSheet photoSheet = new();
    private readonly ActionSheet.Item[] photoSheetItems = new ActionSheet.Item[PhotoSheetMaxItems];
    private int photoSheetCount;
    private string photoSheetPhotoId = string.Empty;
    private bool photoSheetCanCover;

    private void DrawEditPhotos()
    {
        var scale = UiScale.Current;
        var photos = store.Me is { } me ? CardPhotos(me) : NoCardPhotos;
        VSectionHeader.Card(PhoneIcons.Photo, Loc.T(L.Velvet.PhotosSection));
        Gap(6f);
        var width = ScrollLayout.StableContentWidth();
        var gap = CardPhotoGap * scale;
        var cell = (width - gap * (CardPhotoColumns - 1)) / CardPhotoColumns;
        var cellHeight = cell / CardPhotoAspect;
        var rows = (MaxCardPhotos + CardPhotoColumns - 1) / CardPhotoColumns;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var busy = store.CardPhotoBusy;
        for (var slot = 0; slot < MaxCardPhotos; slot++)
        {
            var row = slot / CardPhotoColumns;
            var column = slot % CardPhotoColumns;
            var min = new Vector2(origin.X + column * (cell + gap), origin.Y + row * (cellHeight + gap));
            var max = new Vector2(min.X + cell, min.Y + cellHeight);
            if (slot < photos.Length)
            {
                if (DrawCardPhotoTile(drawList, min, max, photos[slot], slot == 0))
                {
                    OpenPhotoSheet(photos[slot].Id, slot);
                }

                continue;
            }

            var next = slot == photos.Length;
            if (DrawEmptyPhotoSlot(drawList, min, max, next, next && busy) && !busy)
            {
                cardPhotos.Open();
                photoEditing = true;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * cellHeight + (rows - 1) * gap));
        Gap(8f);
        ui.HelpText(Loc.T(L.Velvet.PhotosHint));
        Gap(8f);
        if (ui.GhostButton(Reserve(PreviewButtonHeight), Loc.T(L.Velvet.PreviewCard)))
        {
            OpenCardPreview();
        }
    }

    private bool DrawCardPhotoTile(ImDrawListPtr drawList, Vector2 min, Vector2 max, VelvetCardPhotoDto photo,
        bool cover)
    {
        var scale = UiScale.Current;
        var rounding = CardPhotoRounding * scale;
        DrawCoverImage(drawList, min, max, photo.Url, rounding, string.Empty);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            Squircle.Stroke(drawList, min, max, rounding, VelvetTheme.RoseInk.Packed(), Metrics.Stroke.Thin * scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (cover)
        {
            var label = Loc.T(L.Velvet.CoverBadge);
            var textSize = Typography.Measure(label, TextStyles.Caption2);
            var pad = CardPhotoBadgePad * scale;
            var badgeMin = new Vector2(min.X + CardPhotoBadgeInset * scale,
                max.Y - CardPhotoBadgeInset * scale - CardPhotoBadgeHeight * scale);
            var badgeMax = new Vector2(badgeMin.X + textSize.X + pad * 2f, max.Y - CardPhotoBadgeInset * scale);
            Squircle.Fill(drawList, badgeMin, badgeMax, CardPhotoBadgeHeight * scale * 0.5f, VelvetTheme.Rose.Packed());
            Typography.Draw(drawList,
                new Vector2(badgeMin.X + pad, (badgeMin.Y + badgeMax.Y) * 0.5f - textSize.Y * 0.5f), label,
                VelvetTheme.OnAccent, TextStyles.Caption2);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private static bool DrawEmptyPhotoSlot(ImDrawListPtr drawList, Vector2 min, Vector2 max, bool next, bool busy)
    {
        var scale = UiScale.Current;
        var rounding = CardPhotoRounding * scale;
        var hovered = next && !busy && UiInteract.Hover(min, max);
        Squircle.Fill(drawList, min, max, rounding, (hovered ? VelvetTheme.CardHi : VelvetTheme.PlumWell).Packed());
        Squircle.Stroke(drawList, min, max, rounding,
            (next ? VelvetTheme.Alpha(VelvetTheme.RoseInk, 0.55f) : VelvetTheme.Hairline).Packed(),
            Metrics.Stroke.Hairline * scale);
        var center = (min + max) * 0.5f;
        if (busy)
        {
            LoadingPulse.Spinner(center, CardPhotoSpinnerRadius * scale, VelvetTheme.RoseInk);
        }
        else
        {
            PhoneIcon.Draw(drawList, center, PhoneIcons.Plus, next ? VelvetTheme.RoseInk : VelvetTheme.Faint,
                VIcon.CardAction * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private void OpenPhotoSheet(string photoId, int index)
    {
        photoSheetPhotoId = photoId;
        photoSheetCanCover = index > 0;
        photoSheetCount = 0;
        if (photoSheetCanCover)
        {
            photoSheetItems[photoSheetCount] = new ActionSheet.Item(Loc.T(L.Velvet.MakeCover));
            photoSheetCount++;
        }

        photoSheetItems[photoSheetCount] = new ActionSheet.Item(Loc.T(L.Velvet.RemovePhoto), string.Empty, true);
        photoSheetCount++;
        photoSheet.Open();
    }

    private void DrawPhotoSheet(Rect screen)
    {
        if (!photoSheet.CapturesPointer)
        {
            return;
        }

        var picked = photoSheet.Draw(screen, ActionSheetStyle.From(ui), photoSheetItems.AsSpan(0, photoSheetCount),
            Loc.T(L.Common.Cancel), false, string.Empty);
        if (picked < 0)
        {
            return;
        }

        if (photoSheetCanCover && picked == 0)
        {
            store.MakeCardPhotoCover(photoSheetPhotoId);
            return;
        }

        store.RemoveCardPhoto(photoSheetPhotoId);
    }

    private void DrawEditAvatar()
    {
        var scale = UiScale.Current;
        var block = Reserve(160f);
        var drawList = ImGui.GetWindowDrawList();
        var radius = 46f * scale;
        var center = new Vector2(block.Center.X, block.Min.Y + 8f * scale + radius);
        var me = store.Me;
        VAvatar.Draw(drawList, center, radius, theme, DisplayNameOf(editDisplayName, editHandle),
            me?.World ?? string.Empty, me?.AvatarUrl, images, lodestone, -1, VelvetTheme.Rose);

        var badgeCenter = new Vector2(center.X + radius * 0.70f, center.Y + radius * 0.70f);
        drawList.AddCircleFilled(badgeCenter, 14f * scale, VelvetTheme.GroundBottom.Packed(), 24);
        drawList.AddCircleFilled(badgeCenter, 12f * scale, VelvetTheme.Rose.Packed(), 24);
        PhoneIcon.Draw(drawList, badgeCenter, PhoneIcons.Camera, VelvetTheme.OnAccent, VIcon.Small * scale);

        var avatarMin = new Vector2(center.X - radius, center.Y - radius);
        var avatarMax = new Vector2(center.X + radius, center.Y + radius);
        var avatarHovered = UiInteract.Hover(avatarMin, avatarMax);
        if (avatarHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pillWidth = 150f * scale;
        var pillTop = center.Y + radius + 16f * scale;
        var changeRect = new Rect(new Vector2(block.Center.X - pillWidth * 0.5f, pillTop),
            new Vector2(block.Center.X + pillWidth * 0.5f, pillTop + 34f * scale));
        var pillClicked = ui.GhostButton(changeRect, Loc.T(L.Velvet.ChangePhoto));
        if (pillClicked || (avatarHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left)))
        {
            avatar.Open();
            avatarEditing = true;
        }
    }

    private void DrawIntentEditor()
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        chipModels.Clear();
        for (var index = 0; index < VelvetIntent.All.Length; index++)
        {
            var def = VelvetIntent.All[index];
            var selected = VelvetIntent.Has(editIntent, def.Flag);
            chipModels.Add(new VChipModel(Loc.T(def.Label), selected ? VChipStyle.Solid : VChipStyle.Ghost,
                selected ? def.Hue : VelvetTheme.Moonlight, def.Glyph));
        }

        var clicked = DrawChipFlow(width, scale);
        if (clicked >= 0)
        {
            editIntent = VelvetIntent.Toggle(editIntent, VelvetIntent.All[clicked].Flag);
        }
    }

    private void DrawGenderPicker(ref int gender)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var options = VelvetGender.All;
        chipModels.Clear();
        for (var index = 0; index < options.Length; index++)
        {
            var value = options[index];
            var selected = VelvetGender.Has(gender, value);
            chipModels.Add(new VChipModel(VelvetGender.Label(value), selected ? VChipStyle.Solid : VChipStyle.Ghost,
                selected ? VelvetTheme.Rose : VelvetTheme.Moonlight));
        }

        var clicked = DrawChipFlow(width, scale);
        if (clicked >= 0)
        {
            gender = VelvetGender.Toggle(gender, options[clicked]);
        }
    }

    private void DrawSexualityPicker(ref int sexuality)
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var options = VelvetSexuality.All;
        chipModels.Clear();
        for (var index = 0; index < options.Length; index++)
        {
            var value = options[index];
            var selected = VelvetSexuality.Has(sexuality, value);
            chipModels.Add(new VChipModel(VelvetSexuality.Label(value), selected ? VChipStyle.Solid : VChipStyle.Ghost,
                selected ? VelvetTheme.Rose : VelvetTheme.Moonlight));
        }

        var clicked = DrawChipFlow(width, scale);
        if (clicked >= 0)
        {
            sexuality = VelvetSexuality.Toggle(sexuality, options[clicked]);
        }
    }

    private void DrawRelationshipEditor()
    {
        var scale = UiScale.Current;
        var width = ImGui.GetContentRegionAvail().X;
        var options = VelvetRelationship.All;
        chipModels.Clear();
        for (var index = 0; index < options.Length; index++)
        {
            var value = options[index];
            var selected = editRelationship == value;
            chipModels.Add(new VChipModel(VelvetRelationship.Label(value), selected ? VChipStyle.Solid : VChipStyle.Ghost,
                selected ? VelvetTheme.Rose : VelvetTheme.Moonlight));
        }

        var clicked = DrawChipFlow(width, scale);
        if (clicked >= 0)
        {
            editRelationship = options[clicked];
        }
    }

    private bool HasUnsavedEdits()
    {
        if (store.Me is not { } me)
        {
            return false;
        }

        return !string.Equals(editDisplayName, me.DisplayName, StringComparison.Ordinal)
               || !string.Equals(editHandle, me.Handle, StringComparison.Ordinal)
               || !string.Equals(editIntro, me.Intro, StringComparison.Ordinal)
               || !string.Equals(editPronouns, me.Pronouns, StringComparison.Ordinal)
               || editGender != VelvetGender.Sanitize(me.Gender)
               || editSexuality != VelvetSexuality.Sanitize(me.Sexuality)
               || editIntent != VelvetIntent.Sanitize(me.LookingFor)
               || editRelationship != me.RelationshipStatus
               || Differs(editRole, VelvetTags.Parse(me.Dynamic))
               || Differs(editKinks, me.Kinks)
               || Differs(editTags, me.Tags)
               || Differs(editLimits, me.Limits);
    }

    private static bool Differs(List<string> edited, IReadOnlyList<string>? saved)
    {
        var count = saved?.Count ?? 0;
        if (edited.Count != count)
        {
            return true;
        }

        for (var index = 0; index < edited.Count; index++)
        {
            if (!string.Equals(edited[index], saved![index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void SaveProfile()
    {
        if (editBusy)
        {
            return;
        }

        editBusy = true;
        editSaveFailed = false;
        var me = store.Me;
        var identityChanged = me is not null &&
            (editDisplayName.Trim() != me.DisplayName || editHandle.Trim() != me.Handle);
        var dynamic = VelvetTags.Join(editRole.ToArray());
        var request = new UpdateVelvetProfileRequest(editIntro.Trim(), editPronouns.Trim(), dynamic, editTags.ToArray(),
            editLimits.ToArray(), VelvetIntent.Sanitize(editIntent), editRelationship, null,
            Gender: VelvetGender.Sanitize(editGender), Sexuality: VelvetSexuality.Sanitize(editSexuality),
            Kinks: editKinks.ToArray());
        if (identityChanged)
        {
            store.UpdateIdentity(editDisplayName.Trim(), editHandle.Trim(),
                identitySaved => store.UpdateProfile(request,
                    profileSaved => CompleteSave(identitySaved && profileSaved)));
        }
        else
        {
            store.UpdateProfile(request, CompleteSave);
        }
    }

    private void CompleteSave(bool succeeded)
    {
        if (succeeded)
        {
            editSaveSucceeded = true;
        }
        else
        {
            editSaveFailed = true;
        }

        editBusy = false;
    }
}
