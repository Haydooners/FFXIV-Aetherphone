using NAudio.Dsp;
using NAudio.Wave;

namespace Aetherphone.Core.Songs;

internal sealed class TrackVoice : ISampleProvider
{
    public const int OutputSampleRate = 48_000;
    public const int OutputChannels = 2;
    public const float MinimumRate = 0.9f;
    public const float MaximumRate = 1.1f;

    private static readonly WaveFormat MixFormat =
        WaveFormat.CreateIeeeFloatWaveFormat(OutputSampleRate, OutputChannels);

    private readonly ISongAudioReader reader;
    private readonly ISampleProvider source;
    private readonly WdlResampler resampler = new();
    private readonly object gate = new();
    private readonly int sourceChannels;
    private readonly int sourceSampleRate;
    private float[] resampled = Array.Empty<float>();
    private long sourceFramesConsumed;
    private double baseSeconds;
    private double pendingSeekSeconds = -1;
    private float gain;
    private float targetGain = 1f;
    private float gainStep;
    private float rate = 1f;
    private bool rateDirty = true;
    private volatile bool finished;
    private volatile bool sourceEnded;
    private volatile bool faulted;

    public TrackVoice(ISongAudioReader reader, double startSeconds, float fadeInSeconds)
    {
        this.reader = reader;
        source = reader.ToSampleProvider();
        sourceChannels = Math.Max(1, source.WaveFormat.Channels);
        sourceSampleRate = Math.Max(1, source.WaveFormat.SampleRate);
        resampler.SetMode(true, 2, false);
        resampler.SetFilterParms();
        resampler.SetFeedMode(false);
        if (startSeconds > 0)
        {
            pendingSeekSeconds = startSeconds;
        }

        if (fadeInSeconds > 0f)
        {
            gain = 0f;
            gainStep = 1f / (fadeInSeconds * OutputSampleRate);
        }
        else
        {
            gain = 1f;
        }
    }

    public WaveFormat WaveFormat => MixFormat;
    public bool Finished => finished;
    public bool SourceEnded => sourceEnded;
    public bool Faulted => faulted;
    public bool FadingOut => targetGain <= 0f;
    public double DurationSeconds => reader.TotalTime.TotalSeconds;

    public double PositionSeconds
    {
        get
        {
            lock (gate)
            {
                return pendingSeekSeconds >= 0
                    ? pendingSeekSeconds
                    : baseSeconds + (double)sourceFramesConsumed / sourceSampleRate;
            }
        }
    }

    public float Rate
    {
        get => rate;
        set
        {
            var clamped = Math.Clamp(value, MinimumRate, MaximumRate);
            lock (gate)
            {
                if (Math.Abs(clamped - rate) < 0.0005f)
                {
                    return;
                }

                rate = clamped;
                rateDirty = true;
            }
        }
    }

    public void Seek(double seconds)
    {
        lock (gate)
        {
            pendingSeekSeconds = Math.Max(0, seconds);
        }
    }

    public void FadeOut(float seconds)
    {
        lock (gate)
        {
            targetGain = 0f;
            gainStep = seconds <= 0f ? 1f : 1f / (seconds * OutputSampleRate);
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (finished)
        {
            return 0;
        }

        lock (gate)
        {
            SafeApplyPendingSeek();
            if (rateDirty)
            {
                resampler.SetRates(sourceSampleRate * (double)rate, OutputSampleRate);
                rateDirty = false;
            }

            var outputFrames = count / OutputChannels;
            var producedFrames = sourceEnded ? 0 : SafeResample(outputFrames);
            WriteFrames(buffer, offset, producedFrames, outputFrames);
            if ((producedFrames == 0 && sourceEnded) || (targetGain <= 0f && gain <= 0f))
            {
                finished = true;
            }

            return count;
        }
    }

    public void DisposeReader()
    {
        lock (gate)
        {
            finished = true;
            reader.Dispose();
        }
    }

    private void SafeApplyPendingSeek()
    {
        try
        {
            ApplyPendingSeek();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Song voice seek failed");
            faulted = true;
            sourceEnded = true;
        }
    }

    private void ApplyPendingSeek()
    {
        if (pendingSeekSeconds < 0 || faulted)
        {
            return;
        }

        var duration = reader.TotalTime.TotalSeconds;
        var target = duration > 0 ? Math.Min(pendingSeekSeconds, duration) : pendingSeekSeconds;
        reader.CurrentTime = TimeSpan.FromSeconds(target);
        baseSeconds = target;
        sourceFramesConsumed = 0;
        pendingSeekSeconds = -1;
        sourceEnded = false;
        resampler.Reset();
    }

    private int SafeResample(int outputFrames)
    {
        try
        {
            return Resample(outputFrames);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Song voice read failed");
            faulted = true;
            sourceEnded = true;
            return 0;
        }
    }

    private int Resample(int outputFrames)
    {
        var needed = outputFrames * sourceChannels;
        if (resampled.Length < needed)
        {
            resampled = new float[needed];
        }

        var framesWanted = resampler.ResamplePrepare(outputFrames, sourceChannels, out var inputBuffer,
            out var inputOffset);
        var samplesRead = source.Read(inputBuffer, inputOffset, framesWanted * sourceChannels);
        var framesRead = samplesRead / sourceChannels;
        if (framesRead == 0)
        {
            sourceEnded = true;
        }

        sourceFramesConsumed += framesRead;
        return resampler.ResampleOut(resampled, 0, framesRead, outputFrames, sourceChannels);
    }

    private void WriteFrames(float[] buffer, int offset, int producedFrames, int outputFrames)
    {
        for (var frame = 0; frame < outputFrames; frame++)
        {
            StepGain();
            var outputIndex = offset + frame * OutputChannels;
            if (frame >= producedFrames)
            {
                buffer[outputIndex] = 0f;
                buffer[outputIndex + 1] = 0f;
                continue;
            }

            var sourceIndex = frame * sourceChannels;
            var left = resampled[sourceIndex];
            var right = sourceChannels > 1 ? resampled[sourceIndex + 1] : left;
            buffer[outputIndex] = left * gain;
            buffer[outputIndex + 1] = right * gain;
        }
    }

    private void StepGain()
    {
        if (gain < targetGain)
        {
            gain = Math.Min(targetGain, gain + gainStep);
        }
        else if (gain > targetGain)
        {
            gain = Math.Max(targetGain, gain - gainStep);
        }
    }
}
