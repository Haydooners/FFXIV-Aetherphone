using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Media;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal enum VelvetProfileTab
{
    About,
    Photos,
}

internal sealed partial class VelvetShell
{
    private const float ProfileAvatarRadius = 44f;
    private const float ProfileHeadTop = 14f;
    private const float ProfileStatsGap = 20f;
    private const float ProfileStatSpacing = 22f;
    private const float ProfileBlockGap = 10f;
    private const float ProfileTabHeight = 44f;
    private const float ProfileActionHeight = 40f;
    private const float ProfileBottomPad = 14f;
    private const float ProfileGridGap = 1.5f;
    private const float ProfileTabUnderline = 2f;
    private const float ProfileTabSmoothTime = 0.1f;
    private const int ProfileColumns = 3;

    private static readonly Vector4 RoleTone = new(0.62f, 0.22f, 0.60f, 1f);
    private static readonly Vector4 KinkTone = new(0.647f, 0.482f, 0.839f, 1f);
    private static readonly TextStyle ProfileNameStyle = new(1.35f, FontWeight.Bold);
    private static readonly TextStyle ProfileStatValueStyle = new(1.1f, FontWeight.Bold);
    private static readonly TextStyle ProfileTabStyle = new(1.02f, FontWeight.SemiBold);
    private static readonly TextStyle ProfileTabIdleStyle = new(1.02f, FontWeight.Medium);
    private static readonly UnderlineTabStyle ProfileTabsStyle = new(ProfileTabStyle, ProfileTabIdleStyle,
        VelvetTheme.TitleInk, VelvetTheme.MutedInk, VelvetTheme.Rose, ProfileTabUnderline, SocialChrome.CellPadX,
        ProfileTabSmoothTime);

    private readonly List<VelvetPostDto> galleryPosts = new();
    private VelvetProfileTab profileTab = VelvetProfileTab.About;
    private Spring profileTabSlide;

    private void DrawProfile(Rect area, string userId)
    {
        var scale = UiScale.Current;
        var user = store.ProfileUserId == userId ? store.ProfileUser : null;
        var title = user != null ? DisplayNameOf(user.DisplayName, user.Handle) : Loc.T(L.Velvet.ProfileTitle);
        if (VHeader.Push(area, title, 1))
        {
            router.Pop();
            return;
        }

        if (user != null && VIcon.Button(VHeader.Slot(area, 0), VHeader.IconRadius, PhoneIcons.Dots, VIcon.Overflow,
                VelvetTheme.TitleInk, Loc.T(L.Velvet.More), HoverLabelSide.Below))
        {
            OpenProfileMenu(user);
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + VHeader.Height * scale), area.Max);
        if (user == null)
        {
            if (store.ProfileLoading)
            {
                DrawEmpty(body, Loc.T(L.Common.Loading), string.Empty);
            }
            else if (store.ProfileFailed)
            {
                if (EmptyState.Draw(body, ui, PhoneIcons.CloudDownload, Loc.T(L.Velvet.ProfileUnavailable),
                        Loc.T(L.Velvet.ProfileUnavailableHint), Loc.T(L.Common.Retry)))
                {
                    store.OpenProfile(userId);
                }
            }
            else
            {
                EmptyState.Draw(body, ui, PhoneIcons.User, Loc.T(L.Velvet.ProfileUnavailable),
                    Loc.T(L.Velvet.ProfileUnavailableHint));
            }

            return;
        }

        DrawProfileBody(body, user);
    }

    private void DrawProfileBody(Rect body, VelvetProfileDto user)
    {
        var isMe = store.Me?.UserId == user.UserId;
        var connected = isMe || user.ConnectionState == VelvetConnectionState.Connected;
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawProfileHead(user, isMe, width);
            var picked = UnderlineTabs.Draw(Reserve(ProfileTabHeight), Loc.T(L.Velvet.CardAbout),
                Loc.T(L.Velvet.Photos), profileTab == VelvetProfileTab.Photos, ref profileTabSlide, VelvetInk.Shared,
                ProfileTabsStyle);
            if (picked >= 0)
            {
                profileTab = picked == 1 ? VelvetProfileTab.Photos : VelvetProfileTab.About;
            }

            Gap(14f);
            if (profileTab == VelvetProfileTab.Photos)
            {
                DrawCardPhotoGrid(user, width);
                DrawGallery(user, isMe, connected, width);
            }
            else
            {
                DrawProfileAbout(user, width);
            }

            Gap(40f);
        }
    }

    private void DrawProfileHead(VelvetProfileDto user, bool isMe, float width)
    {
        var scale = UiScale.Current;
        var pad = SocialChrome.CellPadX * scale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var innerLeft = origin.X + pad;
        var innerRight = origin.X + width - pad;
        var innerWidth = MathF.Max(1f, innerRight - innerLeft);
        var radius = ProfileAvatarRadius * scale;
        var frame = Frames.Of(user.FrameId);
        var frameReach = AvatarView.Reserve(frame, radius);
        var avatarCenter = new Vector2(innerLeft + frameReach + radius,
            origin.Y + ProfileHeadTop * scale + frameReach + radius);
        var ring = isMe
            ? VelvetTheme.Rose
            : user.ConnectionState == VelvetConnectionState.Connected
                ? VelvetTheme.Moonlight
                : (Vector4?)null;
        var name = DisplayNameOf(user.DisplayName, user.Handle);
        VAvatar.Draw(drawList, avatarCenter, radius, theme, name, isMe ? user.World : string.Empty, user.AvatarUrl,
            images, lodestone, -1, ring, frame);
        avatarLightbox.TryOpen(avatarCenter, radius, user.AvatarUrl, images);

        var statsTop = avatarCenter.Y - Typography.LineHeight(ProfileStatValueStyle);
        var statsLeft = avatarCenter.X + radius + frameReach + ProfileStatsGap * scale;
        DrawProfileStats(drawList, user, isMe, statsLeft, innerRight, statsTop);

        var nameTop = avatarCenter.Y + radius + frameReach + ProfileBlockGap * scale;
        UserName.DrawAuto(drawList, "velvet.profile.name." + user.UserId, name, user.Badges, user.BadgeIds, innerLeft,
            nameTop, innerWidth, ProfileNameStyle, VelvetTheme.TitleInk, theme, 2);

        var handleTop = nameTop + Typography.LineHeight(ProfileNameStyle);
        var handle = SocialIdentity.ProfileMeta(user.Handle, RegionCodeOf(user));
        var handleHeight = handle.Length > 0
            ? Typography.DrawWrappedLeft(new Vector2(innerLeft, handleTop), handle, VelvetTheme.MutedInk,
                TextStyles.Subheadline, innerWidth)
            : 0f;

        var intentTop = handleTop + handleHeight + 8f * scale;
        var intentHeight = Typography.DrawWrappedLeft(new Vector2(innerLeft, intentTop),
            VelvetIntent.Summary(user.LookingFor), VelvetTheme.RoseInk, TextStyles.Headline, innerWidth);

        var chipsTop = intentTop + intentHeight + ProfileBlockGap * scale;
        var chipsHeight = DrawProfileChips(drawList, user, innerLeft, innerRight,
            chipsTop + SocialChrome.MetaChipHeight * scale * 0.5f);

        var actionTop = chipsTop + chipsHeight + ProfileBlockGap * scale;
        DrawProfileAction(user, isMe, new Rect(new Vector2(innerLeft, actionTop),
            new Vector2(innerRight, actionTop + ProfileActionHeight * scale)));

        var bottom = actionTop + ProfileActionHeight * scale + ProfileBottomPad * scale;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bottom - origin.Y));
    }

    private void DrawProfileStats(ImDrawListPtr drawList, VelvetProfileDto user, bool isMe, float left, float right,
        float top)
    {
        var scale = UiScale.Current;
        var lineHeight = Typography.LineHeight(ProfileStatValueStyle);
        store.EnsureUserPosts(user.UserId);
        var photoCount = store.UserPostsUserId == user.UserId && store.UserPostsLoaded
            ? store.UserPostsTotal
            : CountFeedPostsBy(user.UserId);
        var cursor = SocialChrome.DrawStat(drawList, left, top, lineHeight, CountText.Compact(photoCount),
            Loc.T(L.Velvet.Photos), false, right, VelvetInk.Shared, ProfileStatValueStyle, TextStyles.Subheadline,
            out _);
        if (!isMe)
        {
            return;
        }

        SocialChrome.DrawStat(drawList, cursor + ProfileStatSpacing * scale, top, lineHeight,
            CountText.Compact(store.Connections.Length), Loc.T(L.Velvet.ProfileConnections), false, right,
            VelvetInk.Shared, ProfileStatValueStyle, TextStyles.Subheadline, out _);
    }

    private int CountFeedPostsBy(string userId)
    {
        var feed = store.Feed;
        var count = 0;
        for (var index = 0; index < feed.Length; index++)
        {
            if (feed[index].OwnerId == userId)
            {
                count++;
            }
        }

        return count;
    }

    private float DrawProfileChips(ImDrawListPtr drawList, VelvetProfileDto user, float left, float right,
        float centerY)
    {
        var scale = UiScale.Current;
        var cursor = left;
        if (user.RelationshipStatus != VelvetRelationship.NotSaying)
        {
            SocialChrome.DrawMetaChip(drawList, ref cursor, right, centerY, PhoneIcons.HeartHandshake,
                VelvetRelationship.Label(user.RelationshipStatus), VelvetInk.Shared, TextStyles.Footnote);
        }

        if (user.Pronouns.Length > 0)
        {
            SocialChrome.DrawMetaChip(drawList, ref cursor, right, centerY, PhoneIcons.User, user.Pronouns,
                VelvetInk.Shared, TextStyles.Footnote);
        }

        if (user.ShareTimeZone && user.UtcOffsetMinutes is { } offset)
        {
            SocialChrome.DrawMetaChip(drawList, ref cursor, right, centerY, PhoneIcons.Clock,
                SocialTimeZone.Describe(offset), VelvetInk.Shared, TextStyles.Footnote);
        }

        return cursor > left ? SocialChrome.MetaChipHeight * scale : 0f;
    }

    private void DrawProfileAction(VelvetProfileDto user, bool isMe, Rect rect)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = rect.Height * 0.5f;
        if (isMe)
        {
            if (SocialPill.Outline(drawList, rect, Loc.T(L.Velvet.EditProfile), VelvetInk.Shared,
                    TextStyles.SubheadlineEmphasized, rounding, VelvetInk.Shared.ButtonFill))
            {
                BeginEditProfile();
                router.Push(VelvetView.EditProfile);
            }

            return;
        }

        switch (user.ConnectionState)
        {
            case VelvetConnectionState.Connected:
                if (SocialPill.Accent(drawList, rect, Loc.T(L.Velvet.Message), VelvetInk.Shared,
                        TextStyles.SubheadlineEmphasized, rounding))
                {
                    OpenThread(user.UserId);
                }

                break;
            case VelvetConnectionState.OutgoingRequest:
                if (SocialPill.Outline(drawList, rect, Loc.T(L.Velvet.Requested), VelvetInk.Shared,
                        TextStyles.SubheadlineEmphasized, rounding, VelvetInk.Shared.ButtonFill))
                {
                    store.CancelRequest(user.UserId);
                }

                break;
            case VelvetConnectionState.IncomingRequest:
                if (SocialPill.Accent(drawList, rect, Loc.T(L.Velvet.Reply), VelvetInk.Shared,
                        TextStyles.SubheadlineEmphasized, rounding))
                {
                    OpenThread(user.UserId);
                }

                break;
            case VelvetConnectionState.Blocked:
                if (SocialPill.Outline(drawList, rect, Loc.T(L.Velvet.Unblock), VelvetInk.Shared,
                        TextStyles.SubheadlineEmphasized, rounding, VelvetInk.Shared.ButtonFill))
                {
                    store.Unblock(user.UserId);
                }

                break;
            default:
                if (SocialPill.Accent(drawList, rect, Loc.T(L.Velvet.IntroduceYourself), VelvetInk.Shared,
                        TextStyles.SubheadlineEmphasized, rounding))
                {
                    RequestIntro(user.UserId, DisplayNameOf(user.DisplayName, user.Handle));
                }

                break;
        }
    }

    private void DrawProfileAbout(VelvetProfileDto user, float width)
    {
        var scale = UiScale.Current;
        var pad = SocialChrome.CellPadX * scale;
        var innerWidth = MathF.Max(1f, width - pad * 2f);
        ImGui.Indent(pad);
        DrawIntroBlock(user, innerWidth);
        DrawAboutSection(L.Velvet.CardGender, VelvetGender.Labels(user.Gender), VChipStyle.Tint, VelvetTheme.Rose,
            innerWidth);
        DrawAboutSection(L.Velvet.CardSexuality, VelvetSexuality.Labels(user.Sexuality), VChipStyle.Tint,
            VelvetTheme.Rose, innerWidth);
        if (VelvetIntent.IncludesErp(user.LookingFor))
        {
            DrawAboutSection(L.Velvet.CardRole, VelvetTags.Parse(user.Dynamic), VChipStyle.Tint, RoleTone,
                innerWidth);
            DrawAboutSection(L.Velvet.CardKinks, user.Kinks ?? Array.Empty<string>(), VChipStyle.Tint, KinkTone,
                innerWidth);
        }

        DrawAboutSection(L.Velvet.CardTags, user.Tags, VChipStyle.Tint, VelvetTheme.Rose, innerWidth);
        DrawAboutSection(L.Velvet.CardLimits, user.Limits, VChipStyle.Outline, VelvetTheme.Gold, innerWidth);
        ImGui.Unindent(pad);
    }

    private void DrawIntroBlock(VelvetProfileDto user, float innerWidth)
    {
        if (user.Intro.Length == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var introKey = new TranslationKey(TranslationSurface.Bio, user.UserId);
        var introText = translation.View(introKey, user.Intro).Text;
        var introOrigin = ImGui.GetCursorScreenPos();
        var introHeight = Typography.DrawWrappedLeft(introOrigin, introText, VelvetTheme.BodyInk, TextStyles.Body,
            innerWidth);
        ImGui.Dummy(new Vector2(innerWidth, introHeight));
        var introLinkHeight = TranslateLink.Height(translation, introKey, user.IntroLang, scale);
        if (introLinkHeight <= 0f)
        {
            return;
        }

        TranslateLink.Draw(translation, confirm, introKey, user.IntroLang, user.Intro,
            new Vector2(introOrigin.X, introOrigin.Y + introHeight), innerWidth, VelvetTheme.MutedInk,
            VelvetTheme.RoseGlow, scale);
        ImGui.Dummy(new Vector2(innerWidth, introLinkHeight));
    }

    private void DrawAboutSection(LocString title, string[] tokens, VChipStyle style, Vector4 tone,
        float sectionWidth)
    {
        if (tokens.Length == 0)
        {
            return;
        }

        Gap(16f);
        VSectionHeader.Bar(Loc.T(title));
        Gap(4f);
        DrawDisplayTokens(tokens, style, tone, sectionWidth);
    }

    private void AskDisconnect(string userId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Velvet.DisconnectConfirmMessage),
            ConfirmLabel = Loc.T(L.Velvet.Disconnect),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            Sheet = true,
            Confirm = () =>
            {
                store.Disconnect(userId);
                router.Reset();
            },
        });
    }

    private void DrawGallery(VelvetProfileDto user, bool isMe, bool connected, float width)
    {
        var scale = UiScale.Current;
        store.EnsureUserPosts(user.UserId);
        var serverGallery = store.UserPostsUserId == user.UserId && store.UserPostsLoaded;
        var owned = galleryPosts;
        owned.Clear();
        int totalCount;
        if (serverGallery)
        {
            owned.AddRange(store.UserPosts);
            totalCount = store.UserPostsTotal;
        }
        else
        {
            if (!store.FeedLoaded && !store.LoadingFeed)
            {
                store.RefreshFeed();
            }

            var feed = store.Feed;
            for (var index = 0; index < feed.Length; index++)
            {
                if (feed[index].OwnerId == user.UserId)
                {
                    owned.Add(feed[index]);
                }
            }

            totalCount = owned.Count;
        }

        if (owned.Count == 0)
        {
            if (!isMe && !connected)
            {
                DrawLockedGallery(DisplayNameOf(user.DisplayName, user.Handle), width, totalCount);
                return;
            }

            var emptyOrigin = ImGui.GetCursorScreenPos();
            Typography.DrawWrappedCentered(new Vector2(emptyOrigin.X + width * 0.5f, emptyOrigin.Y + 24f * scale),
                isMe ? Loc.T(L.Velvet.NoPhotosMine) : Loc.T(L.Velvet.NoPhotosShared), VelvetTheme.MutedInk,
                TextStyles.Subheadline, width - 48f * scale);
            Gap(80f);
            return;
        }

        var cellGap = ProfileGridGap * scale;
        var cell = (width - cellGap * (ProfileColumns - 1)) / ProfileColumns;
        var rows = (owned.Count + ProfileColumns - 1) / ProfileColumns;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < owned.Count; index++)
        {
            var row = index / ProfileColumns;
            var column = index % ProfileColumns;
            var min = new Vector2(origin.X + column * (cell + cellGap), origin.Y + row * (cell + cellGap));
            var max = new Vector2(min.X + cell, min.Y + cell);
            var tileVeiled = SensitiveReveals.ShouldVeil(owned[index].Sensitive, owned[index].Id,
                configuration.ShowSensitiveContent);
            DrawMedia(drawList, min, max, owned[index].MediaUrl, 0f, veiled: tileVeiled);
            if (!tileVeiled && PostMedia.Photos(owned[index].MediaUrls, owned[index].MediaUrl).Length > 1)
            {
                MultiPhotoBadge.Draw(drawList, new Vector2(max.X - 8f * scale, min.Y + 8f * scale), scale);
            }

            if (UiInteract.Click(min, max))
            {
                store.EnsurePost(owned[index].Id);
                router.Push(VelvetView.PostDetail(owned[index].Id));
            }
        }

        var gridHeight = rows * cell + (rows - 1) * cellGap;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, gridHeight));

        if (serverGallery)
        {
            if (store.UserPostsLoadingMore)
            {
                InfiniteScroll.DrawLoadingRow(origin.X + width * 0.5f, VelvetTheme.MutedInk);
            }
            else if (store.HasMoreUserPosts && InfiniteScroll.ReachedBottom())
            {
                store.LoadMoreUserPosts();
            }
        }

        if (!isMe && !connected && totalCount > owned.Count)
        {
            Gap(14f);
            Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, ImGui.GetCursorScreenPos().Y),
                Loc.Plural(L.Velvet.ConnectToUnlock, totalCount - owned.Count), VelvetTheme.RoseInk,
                TextStyles.Callout, width - 48f * scale);
            Gap(30f);
        }
    }

    private void DrawCardPhotoGrid(VelvetProfileDto user, float width)
    {
        var photos = CardPhotos(user);
        if (photos.Length == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var cellGap = ProfileGridGap * scale;
        var cell = (width - cellGap * (ProfileColumns - 1)) / ProfileColumns;
        var rows = (photos.Length + ProfileColumns - 1) / ProfileColumns;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < photos.Length; index++)
        {
            var row = index / ProfileColumns;
            var column = index % ProfileColumns;
            var min = new Vector2(origin.X + column * (cell + cellGap), origin.Y + row * (cell + cellGap));
            var max = new Vector2(min.X + cell, min.Y + cell);
            DrawMedia(drawList, min, max, photos[index].Url, 0f);
            if (UiInteract.Click(min, max))
            {
                OpenPhotoViewer(photos[index].Url);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * cell + (rows - 1) * cellGap));
        Gap(ProfileGridGap);
    }

    private void DrawLockedGallery(string name, float width, int totalCount)
    {
        var scale = UiScale.Current;
        var cellGap = ProfileGridGap * scale;
        var cell = (width - cellGap * (ProfileColumns - 1)) / ProfileColumns;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        for (var column = 0; column < ProfileColumns; column++)
        {
            var min = new Vector2(origin.X + column * (cell + cellGap), origin.Y);
            var max = new Vector2(min.X + cell, min.Y + cell);
            VMediaTile.Conceal(drawList, min, max, 0f, string.Empty, 0f);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cell));
        Gap(14f);
        var teaser = totalCount > 0
            ? Loc.Plural(L.Velvet.ConnectToUnlock, totalCount)
            : Loc.T(L.Velvet.ConnectToSeePhotos, name);
        Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, ImGui.GetCursorScreenPos().Y), teaser,
            VelvetTheme.RoseInk, TextStyles.Callout, width - 48f * scale);
        Gap(34f);
    }

    private void DrawDisplayTokens(string[] tokens, VChipStyle style, Vector4 tone, float width = 0f)
    {
        if (tokens.Length == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        if (width <= 0f)
        {
            width = ImGui.GetContentRegionAvail().X;
        }

        chipModels.Clear();
        for (var index = 0; index < tokens.Length; index++)
        {
            chipModels.Add(new VChipModel(tokens[index], style, tone));
        }

        DrawChipFlow(width, scale);
    }
}
