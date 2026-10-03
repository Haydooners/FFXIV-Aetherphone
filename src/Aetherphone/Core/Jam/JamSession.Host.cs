using Aetherphone.Core.Playback;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Jam;

internal sealed partial class JamSession
{
    // Comfortably inside the server's 30 s stale window even if one heartbeat is lost.
    private const float HeartbeatSeconds = 8f;
    private const double PositionJumpSeconds = 2.0;
    private const long AdvanceTimeoutMilliseconds = 6_000;

    // Seeding goes through the queue pacer (7 operations per 4.25 s), so 20 songs land in about 12 s.
    private const int SeedCap = 20;

    private bool publishRequested;
    private bool seedPending;
    private bool hasPublished;
    private float heartbeatTimer;
    private string lastPublishedVideoId = string.Empty;
    private double lastPublishedPosition;
    private long lastPublishedAtTicks;
    private bool lastPublishedPaused;
    private int advanceEntryId;
    private string advanceVideoId = string.Empty;
    private bool advanceArmed;
    private long advanceRequestedAtTicks;

    private void BecomeHost()
    {
        var wasGuest = Mode == JamMode.Listening;
        Mode = JamMode.Hosting;
        hub.Authority = hostAuthority;
        if (wasGuest)
        {
            ResetGuestState();
            hub.SetRate(1f);
        }

        publishRequested = true;
        if (seedPending)
        {
            seedPending = false;
            SeedFromLocalQueue();
        }
    }

    private void ResetHostState()
    {
        publishRequested = false;
        seedPending = false;
        hasPublished = false;
        heartbeatTimer = 0f;
        lastPublishedVideoId = string.Empty;
        lastPublishedPosition = 0d;
        lastPublishedAtTicks = 0;
        lastPublishedPaused = false;
        ClearAdvance();
    }

    private bool HandleHostIntent(in PlaybackIntent intent)
    {
        var context = new JamHostContext(hub.SongActive, queue.Length, hub.RepeatMode == SongRepeatMode.One);
        var decision = JamIntentRouting.ForHost(intent.Kind, context);
        switch (decision.Route)
        {
            case JamRoute.LocalThenPublish:
                publishRequested = true;
                return false;
            case JamRoute.Advance:
                RequestAdvance(0);
                return true;
            case JamRoute.AdvanceTo:
                RequestAdvance(intent.EntryId);
                return true;
            case JamRoute.PlayNow:
                PlayNowAsHost(intent.Songs, intent.Index);
                return true;
            case JamRoute.QueueNext:
            case JamRoute.QueueEnd:
                EnqueueAdd(SongOf(intent), decision.Route == JamRoute.QueueNext);
                return true;
            case JamRoute.QueueRemove:
                EnqueueRemove(intent.EntryId);
                return true;
            case JamRoute.QueueMove:
                EnqueueMove(intent.EntryId, intent.Index);
                return true;
            case JamRoute.PauseForEveryone:
                stopRequested = true;
                return true;
            default:
                return decision.Handled;
        }
    }

    private void SeedFromLocalQueue()
    {
        var localQueue = hub.Queue;
        var count = Math.Min(localQueue.QueuedCount, SeedCap);
        for (var index = 0; index < count; index++)
        {
            EnqueueAdd(localQueue.QueuedAt(index).Song, false);
        }

        hub.ClearQueued();
        localQueue.ClearAutoplay();
    }

    private void PlayNowAsHost(Song[] songs, int index)
    {
        if (songs.Length == 0)
        {
            return;
        }

        var start = Math.Clamp(index, 0, songs.Length - 1);
        ClearAdvance();
        hub.PlayRemote(songs[start], 0d, false);
        publishRequested = true;
        if (queue.Length > 0)
        {
            return;
        }

        var last = Math.Min(songs.Length, start + 1 + SeedCap);
        for (var songIndex = start + 1; songIndex < last; songIndex++)
        {
            EnqueueAdd(songs[songIndex], false);
        }
    }

    private void RequestAdvance(int entryId)
    {
        var index = entryId == 0 ? (queue.Length > 0 ? 0 : -1) : JamWire.IndexOfEntry(queue, entryId);
        if (index < 0)
        {
            return;
        }

        var target = queue[index];
        advanceEntryId = target.EntryId;
        advanceVideoId = target.Song.VideoId;
        advanceArmed = false;
        advanceRequestedAtTicks = Environment.TickCount64;
        Enqueue(new CallControl { Type = SignalType.JamQueueAdvance, EntryId = target.EntryId }, urgent: true);
    }

    private void ArmAdvanceIfPopped()
    {
        if (Mode == JamMode.Hosting && advanceEntryId != 0 && !advanceArmed
            && JamWire.IndexOfEntry(queue, advanceEntryId) < 0)
        {
            advanceArmed = true;
        }
    }

    private void OnHostStateEcho(CallControl message)
    {
        if (!advanceArmed || message.Track is not { } track
            || !string.Equals(track.VideoId, advanceVideoId, StringComparison.Ordinal))
        {
            return;
        }

        ClearAdvance();
        var song = JamWire.ToSong(track);
        if (hub.SongActive && string.Equals(hub.CurrentSong.VideoId, song.VideoId, StringComparison.Ordinal))
        {
            hub.Songs.Seek(0f);
            if (hub.IsPaused)
            {
                hub.ApplyTogglePlayPause();
            }
        }
        else
        {
            hub.PlayRemote(song, 0d, false);
        }

        publishRequested = true;
    }

    private void ClearAdvance()
    {
        advanceEntryId = 0;
        advanceVideoId = string.Empty;
        advanceArmed = false;
        advanceRequestedAtTicks = 0;
    }

    private void OnControlRequest(CallControl message)
    {
        if (Mode != JamMode.Hosting)
        {
            return;
        }

        switch (message.Action)
        {
            case JamControlAction.Play:
                if (hub.SongActive && hub.IsPaused)
                {
                    hub.ApplyTogglePlayPause();
                }

                break;
            case JamControlAction.Pause:
                if (hub.SongActive && !hub.IsPaused)
                {
                    hub.ApplyTogglePlayPause();
                }

                break;
            case JamControlAction.Seek:
                if (hub.CanSeek && message.PositionSeconds is { } seconds && double.IsFinite(seconds))
                {
                    hub.Songs.Seek((float)Math.Clamp(seconds, 0d, hub.Duration));
                }

                break;
            case JamControlAction.Next:
                hub.Next();
                break;
            case JamControlAction.Previous:
                hub.Previous();
                break;
            default:
                return;
        }

        publishRequested = true;
    }

    private void TickHost(float deltaSeconds, long now)
    {
        if (advanceEntryId != 0)
        {
            if (now - advanceRequestedAtTicks <= AdvanceTimeoutMilliseconds)
            {
                return;
            }

            ClearAdvance();
            publishRequested = true;
        }

        heartbeatTimer += deltaSeconds;
        if (!signals.Connected || disconnectedSinceTicks != 0 || awaitingSinceTicks != 0)
        {
            return;
        }

        var active = hub.SongActive;
        var song = active ? hub.CurrentSong : default;
        var videoId = song.VideoId ?? string.Empty;
        var paused = !active || hub.IsPaused || hub.IsBuffering;
        var position = active ? (double)hub.Position : 0d;
        var publish = publishRequested || !hasPublished
            || !string.Equals(videoId, lastPublishedVideoId, StringComparison.Ordinal)
            || paused != lastPublishedPaused;
        if (!publish && !paused)
        {
            var expected = lastPublishedPosition + (now - lastPublishedAtTicks) / 1000d;
            publish = Math.Abs(position - expected) > PositionJumpSeconds || heartbeatTimer >= HeartbeatSeconds;
        }

        if (!publish)
        {
            return;
        }

        signals.State(JamWire.ToTrack(song), position, paused);
        hasPublished = true;
        publishRequested = false;
        heartbeatTimer = 0f;
        lastPublishedVideoId = videoId;
        lastPublishedPosition = position;
        lastPublishedAtTicks = now;
        lastPublishedPaused = paused;
    }
}
