using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundSplit.Core
{
    /// <summary>Plays a short two-note "ding-dong" on one device so you can tell which device is which.</summary>
    internal static class TestTone
    {
        private static int _busy;

        public static Task PlayAsync(string deviceId) => Task.Run(() =>
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) return;
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                using var device = enumerator.GetDevice(deviceId);
                var mix = device.AudioClient.MixFormat;
                var rate = mix.SampleRate;
                var channels = mix.Channels;

                ISampleProvider Note(double freq, int ms) =>
                    new FadeOut(new SignalGenerator(rate, channels) { Type = SignalGeneratorType.Sin, Frequency = freq, Gain = 0.18 }
                        .Take(TimeSpan.FromMilliseconds(ms)), rate, channels, ms);

                var chime = new ConcatenatingSampleProvider(new[] { Note(880, 220), Note(660, 380) });

                using var output = new WasapiOut(device, AudioClientShareMode.Shared, true, 80);
                using var done = new ManualResetEventSlim(false);
                output.PlaybackStopped += (_, _) => done.Set();
                output.Init(chime);
                output.Play();
                done.Wait(TimeSpan.FromSeconds(3));
            }
            catch { /* device busy or exclusive mode: ignore */ }
            finally { Interlocked.Exchange(ref _busy, 0); }
        });

        /// <summary>Linear fade-out over the whole note so it sounds like a soft bell instead of a click.</summary>
        private sealed class FadeOut : ISampleProvider
        {
            private readonly ISampleProvider _src;
            private readonly long _total;
            private long _pos;

            public FadeOut(ISampleProvider src, int rate, int channels, int ms)
            {
                _src = src;
                _total = (long)rate * channels * ms / 1000;
            }

            public WaveFormat WaveFormat => _src.WaveFormat;

            public int Read(float[] buffer, int offset, int count)
            {
                var n = _src.Read(buffer, offset, count);
                for (var i = 0; i < n; i++)
                {
                    var t = Math.Clamp(1.0 - (double)(_pos + i) / _total, 0, 1);
                    buffer[offset + i] *= (float)(t * t);
                }
                _pos += n;
                return n;
            }
        }
    }
}
