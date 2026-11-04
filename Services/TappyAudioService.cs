#if ANDROID
using Android.Media;
#endif

namespace Tappy.Services;

public interface ITappyAudioService
{
    bool IsMuted { get; set; }
    void PlayPopSound(int noteHint = -1);
    void PlayBubblePop(int noteHint = -1);
    void PlayXylophoneNote(int keyIndex);
    void PlaySparkleSound(int noteHint = -1);
    void PlayUiTap();
    void PlayUnlockChime();
}

/// <summary>
/// Studio-grade procedural audio synthesizer for infant sensory play.
/// Pre-computes warm marimba pops, crisp bubble chirps, xylophone bell notes,
/// and shimmering firework chimes in memory for zero-latency polyphonic playback.
/// </summary>
public sealed class TappyAudioService : ITappyAudioService, IDisposable
{
    private static readonly double[] ScaleFrequencies =
    [
        523.25,  // 0: C5
        587.33,  // 1: D5
        659.25,  // 2: E5
        698.46,  // 3: F5
        783.99,  // 4: G5
        880.00,  // 5: A5
        987.77,  // 6: B5
        1046.50, // 7: C6
        1174.66, // 8: D6
        1318.51  // 9: E6
    ];

    private static readonly int[] PentatonicIndices = [0, 1, 2, 4, 5, 7, 8, 9];

    private int _rollingNoteIndex;
    public bool IsMuted { get; set; }

#if ANDROID
    private const int SampleRate = 24000;
    private readonly List<AudioTrack> _marimbaTracks = new();
    private readonly List<AudioTrack> _bubbleTracks = new();
    private readonly List<AudioTrack> _xylophoneTracks = new();
    private readonly List<AudioTrack> _sparkleTracks = new();
    private AudioTrack? _uiTapTrack;
    private AudioTrack? _unlockChimeTrack;
    private readonly object _audioLock = new();
    private bool _isInitialized;
#endif

    public TappyAudioService()
    {
#if ANDROID
        InitializeAndroidSoundBanks();
#endif
    }

    public void PlayPopSound(int noteHint = -1)
    {
        if (IsMuted)
        {
            return;
        }

        int idx = noteHint >= 0 ? noteHint : Interlocked.Increment(ref _rollingNoteIndex);
#if ANDROID
        PlayFromBank(_marimbaTracks, idx);
#endif
    }

    public void PlayBubblePop(int noteHint = -1)
    {
        if (IsMuted)
        {
            return;
        }

        int idx = noteHint >= 0 ? noteHint : Interlocked.Increment(ref _rollingNoteIndex);
#if ANDROID
        PlayFromBank(_bubbleTracks, idx);
#endif
    }

    public void PlayXylophoneNote(int keyIndex)
    {
        if (IsMuted)
        {
            return;
        }

#if ANDROID
        PlayFromBank(_xylophoneTracks, Math.Max(0, keyIndex));
#endif
    }

    public void PlaySparkleSound(int noteHint = -1)
    {
        if (IsMuted)
        {
            return;
        }

        int idx = noteHint >= 0 ? noteHint : Interlocked.Increment(ref _rollingNoteIndex);
#if ANDROID
        PlayFromBank(_sparkleTracks, idx);
#endif
    }

    public void PlayUiTap()
    {
        if (IsMuted)
        {
            return;
        }

#if ANDROID
        PlaySingleTrack(_uiTapTrack);
#endif
    }

    public void PlayUnlockChime()
    {
        if (IsMuted)
        {
            return;
        }

#if ANDROID
        PlaySingleTrack(_unlockChimeTrack);
#endif
    }

#if ANDROID
    private void PlayFromBank(List<AudioTrack> bank, int index)
    {
        try
        {
            lock (_audioLock)
            {
                if (!_isInitialized || bank.Count == 0)
                {
                    return;
                }

                AudioTrack track = bank[Math.Abs(index) % bank.Count];
                if (track.PlayState == PlayState.Playing)
                {
                    track.Stop();
                }
                track.ReloadStaticData();
                track.Play();
            }
        }
        catch
        {
            // Ignore transient audio contention during rapid multi-finger smushes
        }
    }

    private void PlaySingleTrack(AudioTrack? track)
    {
        if (track is null)
        {
            return;
        }

        try
        {
            lock (_audioLock)
            {
                if (track.PlayState == PlayState.Playing)
                {
                    track.Stop();
                }
                track.ReloadStaticData();
                track.Play();
            }
        }
        catch
        {
            // Ignore transient audio contention
        }
    }

    private void InitializeAndroidSoundBanks()
    {
        try
        {
            var audioAttributes = new AudioAttributes.Builder()
                ?.SetUsage(AudioUsageKind.Game)
                ?.SetContentType(AudioContentType.Sonification)
                ?.Build();

            var audioFormat = new AudioFormat.Builder()
                ?.SetEncoding(Encoding.Pcm16bit)
                ?.SetSampleRate(SampleRate)
                ?.SetChannelMask(ChannelOut.Mono)
                ?.Build();

            if (audioAttributes is null || audioFormat is null)
            {
                return;
            }

            foreach (int scaleIdx in PentatonicIndices)
            {
                double freq = ScaleFrequencies[scaleIdx];
                _marimbaTracks.Add(CreateStaticTrack(audioAttributes, audioFormat, SynthesizeMarimbaPop(freq, 150)));
                _bubbleTracks.Add(CreateStaticTrack(audioAttributes, audioFormat, SynthesizeBubbleChirp(freq, 110)));
                _xylophoneTracks.Add(CreateStaticTrack(audioAttributes, audioFormat, SynthesizeXylophoneBell(freq, 260)));
                _sparkleTracks.Add(CreateStaticTrack(audioAttributes, audioFormat, SynthesizeSparkleHarp(freq * 1.25, 220)));
            }

            _uiTapTrack = CreateStaticTrack(audioAttributes, audioFormat, SynthesizeMarimbaPop(659.25, 65));
            _unlockChimeTrack = CreateStaticTrack(audioAttributes, audioFormat, SynthesizeAscendingChime());

            _isInitialized = true;
        }
        catch
        {
            _isInitialized = false;
        }
    }

    private static AudioTrack CreateStaticTrack(AudioAttributes attrs, AudioFormat format, short[] samples)
    {
        var track = new AudioTrack(
            attrs,
            format,
            samples.Length * sizeof(short),
            AudioTrackMode.Static,
            AudioManager.AudioSessionIdGenerate);
        track.Write(samples, 0, samples.Length);
        return track;
    }

    private static short[] SynthesizeMarimbaPop(double baseFreq, int durationMs)
    {
        int n = (SampleRate * durationMs) / 1000;
        var buf = new short[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SampleRate;
            double p = (double)i / n;
            double attack = Math.Min(1.0, i / (SampleRate * 0.0035));
            double env = attack * Math.Exp(-6.2 * p);
            double freq = baseFreq * (1.0 + (0.06 * Math.Exp(-18.0 * p)));
            double w = (0.76 * Math.Sin(Math.Tau * freq * t))
                     + (0.18 * Math.Sin(Math.Tau * freq * 2.0 * t) * Math.Exp(-10.0 * p))
                     + (0.06 * Math.Sin(Math.Tau * freq * 4.0 * t) * Math.Exp(-18.0 * p));
            buf[i] = (short)(w * env * short.MaxValue * 0.85);
        }
        return buf;
    }

    private static short[] SynthesizeBubbleChirp(double baseFreq, int durationMs)
    {
        int n = (SampleRate * durationMs) / 1000;
        var buf = new short[n];
        double phase = 0.0;
        for (int i = 0; i < n; i++)
        {
            double p = (double)i / n;
            double attack = Math.Min(1.0, i / (SampleRate * 0.0025));
            double env = attack * Math.Sin(Math.PI * p) * Math.Exp(-2.2 * p);
            double instFreq = baseFreq * (0.75 + (0.85 * p * p));
            phase += Math.Tau * instFreq / SampleRate;
            double w = (0.85 * Math.Sin(phase)) + (0.15 * Math.Sin(phase * 2.0));
            buf[i] = (short)(w * env * short.MaxValue * 0.82);
        }
        return buf;
    }

    private static short[] SynthesizeXylophoneBell(double baseFreq, int durationMs)
    {
        int n = (SampleRate * durationMs) / 1000;
        var buf = new short[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SampleRate;
            double p = (double)i / n;
            double attack = Math.Min(1.0, i / (SampleRate * 0.002));
            double env = attack * Math.Exp(-4.8 * p);
            double w = (0.68 * Math.Sin(Math.Tau * baseFreq * t))
                     + (0.22 * Math.Sin(Math.Tau * baseFreq * 2.0 * t) * Math.Exp(-7.0 * p))
                     + (0.10 * Math.Sin(Math.Tau * baseFreq * 3.0 * t) * Math.Exp(-12.0 * p));
            buf[i] = (short)(w * env * short.MaxValue * 0.86);
        }
        return buf;
    }

    private static short[] SynthesizeSparkleHarp(double baseFreq, int durationMs)
    {
        int n = (SampleRate * durationMs) / 1000;
        var buf = new short[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SampleRate;
            double p = (double)i / n;
            double attack = Math.Min(1.0, i / (SampleRate * 0.003));
            double env = attack * Math.Exp(-5.2 * p);
            double w = (0.58 * Math.Sin(Math.Tau * baseFreq * t))
                     + (0.28 * Math.Sin(Math.Tau * baseFreq * 1.498 * t))
                     + (0.14 * Math.Sin(Math.Tau * baseFreq * 2.0 * t));
            buf[i] = (short)(w * env * short.MaxValue * 0.80);
        }
        return buf;
    }

    private static short[] SynthesizeAscendingChime()
    {
        const int durationMs = 280;
        int n = (SampleRate * durationMs) / 1000;
        var buf = new short[n];
        double[] chord = [523.25, 659.25, 783.99, 1046.50];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SampleRate;
            double p = (double)i / n;
            int noteStep = Math.Min(chord.Length - 1, (int)(p * chord.Length));
            double localP = (p * chord.Length) - noteStep;
            double env = Math.Sin(Math.PI * Math.Min(1.0, localP * 1.2)) * (1.0 - (0.35 * p));
            double freq = chord[noteStep];
            double w = (0.75 * Math.Sin(Math.Tau * freq * t)) + (0.25 * Math.Sin(Math.Tau * freq * 2.0 * t));
            buf[i] = (short)(w * env * short.MaxValue * 0.72);
        }
        return buf;
    }
#endif

    public void Dispose()
    {
#if ANDROID
        lock (_audioLock)
        {
            DisposeBank(_marimbaTracks);
            DisposeBank(_bubbleTracks);
            DisposeBank(_xylophoneTracks);
            DisposeBank(_sparkleTracks);
            _uiTapTrack?.Release();
            _uiTapTrack?.Dispose();
            _unlockChimeTrack?.Release();
            _unlockChimeTrack?.Dispose();
        }
#endif
    }

#if ANDROID
    private static void DisposeBank(List<AudioTrack> tracks)
    {
        foreach (AudioTrack track in tracks)
        {
            try
            {
                track.Release();
                track.Dispose();
            }
            catch
            {
                // Ignore cleanup exceptions
            }
        }
        tracks.Clear();
    }
#endif
}

