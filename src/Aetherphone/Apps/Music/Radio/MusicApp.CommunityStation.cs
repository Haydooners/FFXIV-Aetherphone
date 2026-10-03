using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float StationHeaderHeight = 190f;
    private const float StationHeaderPillOffset = 62f;
    private const float StationHeaderHostOffset = 30f;
    private const float StationPlayRadius = 26f;
    private const int TwitchLinkKind = 0;
    private const int RecentTrackRows = 12;
    private const int RecentTrackPreviewRows = 5;
    private const float TrackRowHeight = 44f;
    private const float TrackSquareSize = 26f;
    private const float OnAirCardHeight = 52f;

    private static readonly string[] LinkLabels =
    {
        "Twitch", "YouTube", "Discord", "Bluesky", "X", "Ko-fi", "Patreon",
    };

    private readonly ChipRail stationTagRail = new();
    private readonly ChipRail linkRail = new();
    private readonly string[] linkLabels = new string[7];
    private readonly bool[] linkActive = new bool[7];
    private readonly string[] linkTargets = new string[7];
    private RadioTrackDto[] splitTrackSource = Array.Empty<RadioTrackDto>();
    private string[] trackTitles = Array.Empty<string>();
    private string[] trackArtists = Array.Empty<string>();
    private string openedStationId = string.Empty;
    private bool showAllTracks;

    private CommunityStationDto? ViewedStation(string stationId)
    {
        if (community.TryResolve(stationId, out var station))
        {
            return station;
        }

        if (!string.Equals(openedStationId, stationId, StringComparison.Ordinal))
        {
            openedStationId = stationId;
            community.OpenStation(stationId, null);
        }

        return null;
    }

    private string CommunityStationTitle(string stationId)
    {
        return community.TryResolve(stationId, out var station) ? station.Name : Loc.T(L.Music.CommunityRadio);
    }

    private void DrawCommunityStation(in PhoneContext context, in MusicRoute route)
    {
        var scale = UiScale.Current;
        community.EnsureFresh(true);
        var station = ViewedStation(route.Key);
        var frame = BeginPage(context);
        if (station is null)
        {
            DrawStationPlaceholder(Unobstructed(frame.Body), scale);
            EndPage(in frame, context, Loc.T(L.Music.CommunityRadio));
            return;
        }

        community.EnsureTracks(station.Id);
        using (AppSurface.Begin(frame.Body))
        {
            DrawStationHeader(scale, station);
            DrawStationActions(scale, station);
            DrawStationBody(scale, station);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }

        EndPage(in frame, context, station.Name);
    }

    private void SearchForTrack(string title) => OpenSearchFor(title);

    private void DrawStationHeader(float scale, CommunityStationDto station)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = StationHeaderHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        DrawStationArt(drawList, min, max, station, Metrics.Radius.Card * scale, ImDrawFlags.RoundCornersTop);

        var scrimTop = new Vector2(min.X, min.Y + height * 0.32f);
        var clear = ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.BackdropTop, 0f));
        var solid = ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.BackdropTop, 0.94f));
        drawList.AddRectFilledMultiColor(scrimTop, max, clear, clear, solid, solid);

        var inset = min.X + Metrics.Space.Md * scale;
        var available = width - Metrics.Space.Md * 2f * scale;
        var pillY = max.Y - StationHeaderPillOffset * scale;
        if (station.IsLive)
        {
            var listening = string.Format(Loc.T(L.Music.ListeningCount), station.Listeners);
            LivePill.Draw(drawList, new Vector2(inset, pillY), LiveLabel(listening), ui.Theme.Danger, clock, scale);
        }
        else
        {
            var resting = Typography.FitText(StationHeaderStatus(station), available, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(inset, pillY), resting, ui.MutedInk, TextStyles.Caption1);
        }

        DrawHost(drawList, station, inset, max.Y - StationHeaderHostOffset * scale, available, scale);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawStationPlaceholder(Rect body, float scale)
    {
        switch (community.StationState)
        {
            case CommunityStationLoad.NotFound:
                EmptyState.Draw(body, ui, FontAwesomeIcon.BroadcastTower, Loc.T(L.Music.StationGone),
                    Loc.T(L.Music.StationGoneSub));
                return;
            case CommunityStationLoad.SignedOut:
                EmptyState.Draw(body, ui, FontAwesomeIcon.UserSlash, Loc.T(L.Music.StationSignedOut),
                    Loc.T(L.Music.StationSignedOutSub));
                return;
            case CommunityStationLoad.Unavailable:
                if (EmptyState.Draw(body, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Music.StationOffline),
                        Loc.T(L.Music.StationOfflineSub), Loc.T(L.Common.Retry)))
                {
                    community.RetryStation();
                }

                return;
            default:
                LoadingPulse.Draw(body.Center, 16f * scale, ui.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
                return;
        }
    }

    private void DrawHost(ImDrawListPtr drawList, CommunityStationDto station, float left, float top, float width,
        float scale)
    {
        var display = station.OwnerDisplayName.Length > 0
            ? station.OwnerDisplayName
            : station.OwnerHandle.Length > 0
                ? "@" + station.OwnerHandle
                : string.Empty;
        if (display.Length == 0)
        {
            return;
        }

        var label = string.Format(Loc.T(L.Music.HostedBy), display);
        var radius = 11f * scale;
        var gap = 7f * scale;
        var available = width - 32f * scale - radius * 2f - gap;
        var fitted = Typography.FitText(label, available, TextStyles.Caption1);
        var rowLeft = left;
        var center = new Vector2(rowLeft + radius, top + radius);

        if (station.OwnerAvatarUrl.Length > 0 && images.Sized(station.OwnerAvatarUrl, radius * 2f) is { } avatar)
        {
            drawList.AddImageRounded(avatar.Handle, center - new Vector2(radius, radius),
                center + new Vector2(radius, radius), Vector2.Zero, Vector2.One, 0xFFFFFFFFu, radius,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ui.FieldSurface), 24);
            var initials = Initials.Of(display);
            var initialsSize = Typography.Measure(initials, TextStyles.Caption2);
            Typography.Draw(drawList, new Vector2(center.X - initialsSize.X * 0.5f, center.Y - initialsSize.Y * 0.5f),
                initials, ui.MutedInk, TextStyles.Caption2);
        }

        UserName.DrawAuto(drawList, "music.station.host", fitted, station.OwnerBadges, station.OwnerBadgeIds,
            rowLeft + radius * 2f + gap, top + radius - Typography.Measure(fitted, TextStyles.Caption1).Y * 0.5f,
            available, TextStyles.Caption1, ui.MutedInk, theme);
    }

    private static string StationHeaderStatus(CommunityStationDto station)
    {
        var resting = OffAirMark(station);
        if (station.Followers == 0)
        {
            return resting;
        }

        return resting + " · " + Loc.Plural(L.Music.StationFollowers, station.Followers);
    }

    private void DrawStationActions(float scale, CommunityStationDto station)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var radius = StationPlayRadius * scale;
        var rowHeight = radius * 2f;
        var current = IsCurrentCommunityStation(station);
        var playable = station.IsLive || current;
        var owned = community.Mine is { } mine && string.Equals(mine.Station.Id, station.Id, StringComparison.Ordinal);
        var playCenter = new Vector2(origin.X + width - radius - Metrics.Space.Md * scale, origin.Y + radius);

        if (!owned)
        {
            var followLabel = station.IsFollowing ? Loc.T(L.Music.FollowingStation) : Loc.T(L.Music.FollowStation);
            if (!playable && !station.IsFollowing)
            {
                followLabel = Loc.T(L.Music.NotifyWhenLive);
            }

            var followWidth = MathF.Min(Typography.Measure(followLabel, TextStyles.Callout).X + 34f * scale,
                width - radius * 2f - Metrics.Space.Xl * scale);
            var followMin = new Vector2(origin.X + Metrics.Space.Md * scale, origin.Y + radius - 18f * scale);
            var followRect = new Rect(followMin, followMin + new Vector2(followWidth, 36f * scale));
            if (ui.GhostButton(followRect, followLabel))
            {
                community.ToggleFollow(station);
            }
        }

        if (playable)
        {
            if (MusicRenderer.PlayButton("music.station.play", playCenter, radius, ui.Accent, ui.Palette.BackdropBottom,
                    current && playback.IsPlaying))
            {
                if (current)
                {
                    playback.TogglePlayPause();
                }
                else
                {
                    PlayCommunityStation(station);
                }
            }
        }
        else
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddCircleFilled(playCenter, radius, ImGui.GetColorU32(ui.FieldSurface), 32);
            AppSkin.Icon(drawList, playCenter, IconGlyph.Of(FontAwesomeIcon.Bell), ui.MutedInk, 1f);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + Metrics.Space.Md * scale));
    }

    private static string LinkUrl(CommunityStationDto station, int kind)
    {
        for (var index = 0; index < station.Links.Length; index++)
        {
            if (station.Links[index].Kind == kind)
            {
                return station.Links[index].Url;
            }
        }

        return string.Empty;
    }

    private void DrawWatchOnTwitch(float scale, CommunityStationDto station)
    {
        var url = LinkUrl(station, TwitchLinkKind);
        if (url.Length == 0)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var buttonWidth = MathF.Min(width - 32f * scale, 220f * scale);
        var buttonMin = new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y);
        var buttonRect = new Rect(buttonMin, buttonMin + new Vector2(buttonWidth, 36f * scale));
        if (ui.GhostButton(buttonRect, Loc.T(L.Music.WatchOnTwitch)))
        {
            Windows.UrlActions.AskThenOpen(url);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 46f * scale));
    }

    private void DrawStationBody(float scale, CommunityStationDto station)
    {
        var width = ScrollLayout.StableContentWidth();
        DrawWatchOnTwitch(scale, station);
        DrawStationTagRail(scale, station);
        var track = NowPlayingFor(station);
        if (track.Length > 0)
        {
            DrawOnAirCard(scale, track);
        }

        if (!station.IsLive && ScheduleLine(station) is { Length: > 0 } schedule)
        {
            DrawStationParagraph(scale, schedule, ui.TitleInk, TextStyles.Callout, width);
        }

        if (station.Description.Length > 0)
        {
            DrawStationParagraph(scale, station.Description, ui.BodyInk, TextStyles.Subheadline, width);
        }

        DrawStationLinks(scale, station);
        DrawRecentTracks(scale, width);

        var reportOrigin = ImGui.GetCursorScreenPos();
        var reportWidth = MathF.Min(width - 32f * scale, 200f * scale);
        var reportMin = new Vector2(reportOrigin.X + (width - reportWidth) * 0.5f, reportOrigin.Y + 8f * scale);
        var reportRect = new Rect(reportMin, reportMin + new Vector2(reportWidth, 34f * scale));
        if (ui.GhostButton(reportRect, Loc.T(L.Music.ReportStation)))
        {
            ReportStation(station);
        }

        ImGui.SetCursorScreenPos(reportOrigin);
        ImGui.Dummy(new Vector2(width, 50f * scale));
    }

    private void DrawStationTagRail(float scale, CommunityStationDto station)
    {
        if (station.Tags.Length == 0)
        {
            return;
        }

        var count = Math.Min(station.Tags.Length, MaxStationTags);
        for (var index = 0; index < count; index++)
        {
            tagFilterLabels[index] = station.Tags[index];
            tagFilterActive[index] = false;
        }

        var tapped = stationTagRail.Draw(ui, tagFilterLabels.AsSpan(0, count), tagFilterActive.AsSpan(0, count));
        ImGui.Dummy(new Vector2(0f, 8f * scale));
        if (tapped >= 0)
        {
            OpenCommunityWithTag(station.Tags[tapped]);
        }
    }

    private void DrawOnAirCard(float scale, string track)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var height = OnAirCardHeight * scale;
        var min = new Vector2(origin.X + Metrics.Space.Md * scale, origin.Y);
        var max = new Vector2(origin.X + width - Metrics.Space.Md * scale, origin.Y + height);
        Squircle.Fill(drawList, min, max, Metrics.Radius.Md * scale, ImGui.GetColorU32(ui.Palette.CardFill));
        var lampCenter = new Vector2(min.X + 20f * scale, (min.Y + max.Y) * 0.5f);
        Equalizer.Draw(drawList, lampCenter, scale, 16f * scale, clock, ui.Accent, 1f, playback.IsPlaying);
        var textLeft = lampCenter.X + 18f * scale;
        var available = max.X - Metrics.Space.Md * scale - textLeft;
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + 10f * scale), Loc.T(L.Music.OnAirNow), ui.MutedInk,
            TextStyles.Caption2);
        var fitted = Typography.FitText(track, available, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + 26f * scale), fitted, ui.TitleInk,
            TextStyles.BodyEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void EnsureTrackSplits()
    {
        var source = community.Tracks;
        if (ReferenceEquals(source, splitTrackSource))
        {
            return;
        }

        splitTrackSource = source;
        trackTitles = new string[source.Length];
        trackArtists = new string[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var raw = source[index].Title;
            var cut = raw.IndexOf(" - ", StringComparison.Ordinal);
            if (cut <= 0)
            {
                trackTitles[index] = raw;
                trackArtists[index] = string.Empty;
                continue;
            }

            trackArtists[index] = raw[..cut];
            trackTitles[index] = raw[(cut + 3)..];
        }
    }

    private void DrawRecentTracks(float scale, float width)
    {
        if (community.TracksLoading)
        {
            SectionHeader.Draw(ui, Loc.T(L.Music.LastPlayed), false, 0f);
            InfiniteScroll.DrawLoadingRow(ImGui.GetCursorScreenPos().X + width * 0.5f, ui.MutedInk);
            return;
        }

        var recent = community.Tracks;
        if (recent.Length == 0)
        {
            return;
        }

        EnsureTrackSplits();
        SectionHeader.Draw(ui, Loc.T(L.Music.LastPlayed), false, 0f);
        var shown = Math.Min(recent.Length, showAllTracks ? RecentTrackRows : RecentTrackPreviewRows);
        for (var index = 0; index < shown; index++)
        {
            DrawTrackRow(scale, recent[index], index);
        }

        if (showAllTracks || recent.Length <= RecentTrackPreviewRows)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = TrackRowHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var label = Loc.T(L.Music.ShowAll);
        var size = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(min.X + Metrics.Space.Md * scale, min.Y + (rowHeight - size.Y) * 0.5f), label, ui.Accent,
            TextStyles.SubheadlineEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
        if (UiInteract.Click(min, max, hovered))
        {
            showAllTracks = true;
        }
    }

    private void DrawTrackRow(float scale, RadioTrackDto track, int index)
    {
        var rowHeight = TrackRowHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, rowHeight, ui.HoverWash);
        var min = cell.Bounds.Min;
        var max = cell.Bounds.Max;
        var squareSize = TrackSquareSize * scale;
        var squareMin = new Vector2(min.X + Metrics.Space.Md * scale, min.Y + (rowHeight - squareSize) * 0.5f);
        drawList.AddImageRounded(artwork.HandleForName(track.Title), squareMin,
            squareMin + new Vector2(squareSize, squareSize), Vector2.Zero, Vector2.One, 0xFFFFFFFFu, 6f * scale,
            ImDrawFlags.RoundCornersAll);

        var stamp = TimeText.Ago(track.PlayedAtUnix);
        var stampWidth = Typography.Measure(stamp, TextStyles.Caption2).X;
        var textLeft = squareMin.X + squareSize + 10f * scale;
        var textWidth = max.X - Metrics.Space.Md * scale - stampWidth - 10f * scale - textLeft;
        var artist = index < trackArtists.Length ? trackArtists[index] : string.Empty;
        var title = index < trackTitles.Length ? trackTitles[index] : track.Title;
        if (artist.Length == 0)
        {
            var single = Typography.FitText(title, textWidth, TextStyles.Subheadline);
            var singleSize = Typography.Measure(single, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + (rowHeight - singleSize.Y) * 0.5f), single,
                ui.BodyInk, TextStyles.Subheadline);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + 7f * scale),
                Typography.FitText(title, textWidth, TextStyles.Subheadline), ui.BodyInk, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + 24f * scale),
                Typography.FitText(artist, textWidth, TextStyles.Caption2), ui.MutedInk, TextStyles.Caption2);
        }

        Typography.Draw(drawList, new Vector2(max.X - Metrics.Space.Md * scale - stampWidth,
            min.Y + (rowHeight - Typography.Measure(stamp, TextStyles.Caption2).Y) * 0.5f), stamp, ui.MutedInk,
            TextStyles.Caption2);

        if (cell.Tapped)
        {
            SearchForTrack(track.Title);
        }

        FeedCell.End(drawList, cell, ui.Hairline);
    }

    private void DrawStationParagraph(float scale, string text, Vector4 color, TextStyle style, float width)
    {
        var origin = ImGui.GetCursorScreenPos();
        var wrapWidth = width - 32f * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + 16f * scale, origin.Y), text, color, style,
            wrapWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 12f * scale));
    }

    private void DrawStationLinks(float scale, CommunityStationDto station)
    {
        if (station.Links.Length == 0)
        {
            return;
        }

        var count = 0;
        for (var index = 0; index < station.Links.Length && count < linkLabels.Length; index++)
        {
            var link = station.Links[index];
            if (link.Kind < 0 || link.Kind >= LinkLabels.Length || link.Kind == TwitchLinkKind)
            {
                continue;
            }

            linkLabels[count] = LinkLabels[link.Kind];
            linkActive[count] = false;
            linkTargets[count] = link.Url;
            count++;
        }

        if (count == 0)
        {
            return;
        }

        var tapped = linkRail.Draw(ui, linkLabels.AsSpan(0, count), linkActive.AsSpan(0, count));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        if (tapped >= 0)
        {
            Windows.UrlActions.AskThenOpen(linkTargets[tapped]);
        }
    }

    private void ReportStation(CommunityStationDto station)
    {
        var stationId = station.Id;
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Music.ReportStationTitle),
            Submit = (reason, done) => SubmitStationReport(stationId, reason, done),
        });
    }

    private void SubmitStationReport(string stationId, string? reason, Action<bool> done)
    {
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                succeeded = await aethernet.Safety.ReportAsync("radio_station", stationId, reason, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "[Radio] station report failed");
            }

            done(succeeded);
        });
    }
}
