using Aetherphone.Core.Songs;
using NAudio.Wave;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TrackVoiceTests
{
    [Fact]
    public void A_seek_the_source_refuses_faults_the_voice_instead_of_throwing_into_the_mixer()
    {
        var voice = new TrackVoice(new RefusingSeekReader(), 30, 0f);
        var buffer = new float[TrackVoice.OutputChannels * 256];

        var written = voice.Read(buffer, 0, buffer.Length);

        Assert.Equal(buffer.Length, written);
        Assert.True(voice.Faulted);
        Assert.True(voice.Finished);
    }

    private sealed class RefusingSeekReader : ISongAudioReader, ISampleProvider
    {
        public WaveFormat WaveFormat { get; } =
            WaveFormat.CreateIeeeFloatWaveFormat(TrackVoice.OutputSampleRate, TrackVoice.OutputChannels);

        public TimeSpan TotalTime => TimeSpan.FromMinutes(3);

        public TimeSpan CurrentTime
        {
            get => TimeSpan.Zero;
            set => throw new IOException("stream reopen failed");
        }

        public ISampleProvider ToSampleProvider() => this;

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        public void Dispose()
        {
        }
    }
}
