using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Frontier
{
    // An immutable stopped buffer is written only by the main thread. Append is
    // the DSP-thread boundary: no Unity API, file IO, allocations or sample edits.
    public sealed class NativePcmCaptureBuffer
    {
        private readonly object gate = new object();
        private readonly float[] samples;
        private readonly int frameLimit, channelLimit;
        private bool accepting = true;
        private int channels, frames, blocks, formatErrors;
        private long discarded, excludedAtLimit;
        public int SampleRate { get; }

        public NativePcmCaptureBuffer(int sampleRate, int maximumFrames, int maximumChannels = 8)
        {
            if (sampleRate < 8000 || sampleRate > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (maximumFrames < 1 || maximumFrames > sampleRate * 20) throw new ArgumentOutOfRangeException(nameof(maximumFrames));
            if (maximumChannels < 1 || maximumChannels > 8) throw new ArgumentOutOfRangeException(nameof(maximumChannels));
            SampleRate = sampleRate; frameLimit = maximumFrames; channelLimit = maximumChannels;
            samples = new float[checked(maximumFrames * maximumChannels)];
        }

        public void Append(float[] data, int inputChannels)
        {
            if (data == null) return;
            lock (gate)
            {
                if (!accepting || frames >= frameLimit) return;
                blocks++;
                if (inputChannels < 1 || inputChannels > channelLimit || (channels != 0 && inputChannels != channels))
                { formatErrors++; discarded += data.Length; accepting = false; return; }
                if (channels == 0) channels = inputChannels;
                int availableFrames = data.Length / channels;
                int copiedFrames = Math.Min(availableFrames, frameLimit - frames);
                int count = copiedFrames * channels;
                Array.Copy(data, 0, samples, frames * channels, count);
                frames += copiedFrames;
                discarded += data.Length % channels;
                excludedAtLimit += (availableFrames - copiedFrames) * (long)channels;
            }
        }

        public bool Complete { get { lock (gate) return frames >= frameLimit || !accepting; } }
        public bool FormatChanged { get { lock (gate) return formatErrors > 0; } }

        public NativePcmSnapshot Stop()
        {
            lock (gate)
            {
                accepting = false;
                return new NativePcmSnapshot(samples, SampleRate, channels, frames, blocks, formatErrors, discarded, excludedAtLimit);
            }
        }
    }

    public sealed class NativePcmSnapshot
    {
        // The audio thread never touches this array after Stop returns.
        public readonly float[] Samples;
        public readonly int SampleRate, Channels, Frames, Blocks, FormatErrors;
        public readonly long DiscardedSamples, SamplesExcludedAtDurationLimit;
        public int Count => Frames * Channels;
        public NativePcmSnapshot(float[] samples, int sampleRate, int channels, int frames, int blocks, int formatErrors, long discarded, long excluded)
        { Samples = samples; SampleRate = sampleRate; Channels = channels; Frames = frames; Blocks = blocks; FormatErrors = formatErrors; DiscardedSamples = discarded; SamplesExcludedAtDurationLimit = excluded; }
    }

    [Serializable] public sealed class NativeAudioState
    { public double realtime; public float worldTime; public bool paused, muted, focused, inMenu, musicEnabled, environmentSoundEnabled; }

    [Serializable] public sealed class NativeAudioReceipt
    {
        public int schemaVersion = 1;
        public string scope = "Native AudioListener DSP output; captures in-game mix, not microphone, physical speakers or a listening verdict.";
        public string wavFile, wavSha256, utcStart, utcEnd, appVersion, buildGuid, unityVersion, platform, matchId, currentMatchId, stopReason;
        public string listenerObjectName, listenerObjectEntityId, captureObjectEntityId;
        public int seed, startFrame, endFrame, sampleRate, channels, frames, samples, dspBlocks, formatErrors, nonfiniteSamples, clippedSamples, omittedStateChanges;
        public int eventSources, ambientSources, eventAudioPlayedStart, eventAudioPlayedEnd;
        public long discardedSamples, samplesExcludedAtDurationLimit;
        public double realtimeStart, realtimeEnd, audioSeconds, rms;
        public float worldTimeStart, worldTimeEnd, peak, requestedSeconds = 20;
        public float simulationSpeed;
        public bool hasDspData, validFormat, validSignal, singleSession, listenerEnabledAtStart, listenerHasAudioSource, autoplay, benchmarkMode;
        public NativeAudioState[] stateChanges;
    }

    // Attach to the existing listener, never to an AudioSource. Reading data does
    // not clear, replace, normalize or otherwise change the listener's output.
    public sealed class NativeAudioCapture : MonoBehaviour
    {
        public const int MaximumSeconds = 20;
        private GameController game;
        private World capturedWorld;
        private volatile NativePcmCaptureBuffer buffer;
        private NativeAudioReceipt receipt;
        private readonly List<NativeAudioState> states = new List<NativeAudioState>(128);
        private double deadline;
        private volatile bool audioConfigurationChanged;
        private bool observedState, lastPaused, lastMuted, lastFocused, lastInMenu, lastMusic, lastEnvironment;
        public bool Recording => buffer != null;
        public string LastReceiptPath { get; private set; }

        public void Initialize(GameController controller)
        {
            if (Recording) Finish("reinitialized", false);
            game = controller;
        }

        public bool Begin()
        {
            if (game == null || Recording || !isActiveAndEnabled) return false;
            var listener = GetComponent<AudioListener>();
            if (Application.isBatchMode || listener == null || !listener.enabled)
            { Debug.LogWarning("FRONTIER_AUDIO_CAPTURE_UNAVAILABLE no active interactive listener"); return false; }
            try
            {
                int rate = AudioSettings.outputSampleRate;
                // Allocate once before publishing the buffer to the DSP thread.
                var pending = new NativePcmCaptureBuffer(rate, rate * MaximumSeconds);
                capturedWorld = game.World;
                receipt = new NativeAudioReceipt
                {
                    utcStart = DateTime.UtcNow.ToString("O"), appVersion = Application.version,
                    buildGuid = Application.buildGUID, unityVersion = Application.unityVersion,
                    platform = Application.platform.ToString(), matchId = game.MatchId,
                    listenerObjectName = listener.gameObject.name, listenerObjectEntityId = listener.gameObject.GetEntityId().ToString(),
                    captureObjectEntityId = gameObject.GetEntityId().ToString(), listenerEnabledAtStart = listener.enabled,
                    listenerHasAudioSource = GetComponent<AudioSource>() != null,
                    seed = capturedWorld?.Seed ?? 0, startFrame = Time.frameCount,
                    worldTimeStart = capturedWorld?.Time ?? 0,
                    realtimeStart = Time.realtimeSinceStartupAsDouble, autoplay = game.Autoplay, benchmarkMode = game.BenchmarkMode,
                    simulationSpeed = game.Autoplay ? game.SimulationSpeed : 1,
                    eventSources = game.Audio?.SourceCount ?? 0, ambientSources = game.Ambience?.SourceCount ?? 0,
                    eventAudioPlayedStart = game.Audio?.PlayedCount ?? 0
                };
                states.Clear(); observedState = false; RecordState(); deadline = receipt.realtimeStart + MaximumSeconds; audioConfigurationChanged = false;
                buffer = pending;
                game.Notify("Audio capture started (20 seconds). Press F10 to stop.");
                return true;
            }
            catch (Exception ex)
            { Debug.LogError("FRONTIER_AUDIO_CAPTURE_FAILED " + ex.GetType().Name + ": " + ex.Message); return false; }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            var target = buffer;
            if (target != null) target.Append(data, channels);
        }

        private void Update()
        {
            bool pressed = Application.isFocused && Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame;
            if (pressed) { if (Recording) Finish("f10"); else Begin(); }
            if (Recording)
            {
                RecordState();
                if (game == null || game.MatchId != receipt.matchId) Finish("match_changed");
                else if (audioConfigurationChanged) Finish("audio_configuration_changed");
                else if (AudioSettings.outputSampleRate != buffer.SampleRate) Finish("sample_rate_changed");
                else if (buffer.FormatChanged) Finish("channel_format_changed");
                else if (buffer.Complete) Finish("audio_window_complete");
                else if (Time.realtimeSinceStartupAsDouble >= deadline) Finish("wall_clock_limit");
            }
        }

        private void RecordState()
        {
            if (receipt == null) return;
            bool paused = game != null && game.IsPaused, muted = game != null && game.Muted;
            bool focused = Application.isFocused, inMenu = game == null || game.InMenu;
            bool music = game != null && game.MusicEnabled, environment = game != null && game.EnvironmentSoundEnabled;
            if (observedState && paused == lastPaused && muted == lastMuted && focused == lastFocused && inMenu == lastInMenu && music == lastMusic && environment == lastEnvironment) return;
            observedState = true; lastPaused = paused; lastMuted = muted; lastFocused = focused; lastInMenu = inMenu; lastMusic = music; lastEnvironment = environment;
            if (states.Count < 128)
                states.Add(new NativeAudioState { realtime = Time.realtimeSinceStartupAsDouble, worldTime = capturedWorld?.Time ?? 0,
                    paused = paused, muted = muted, focused = focused, inMenu = inMenu, musicEnabled = music, environmentSoundEnabled = environment });
            else receipt.omittedStateChanges++;
        }

        public void Finish(string reason = "requested", bool notify = true)
        {
            var target = buffer;
            if (target == null) return;
            RecordState();
            // Stop waits only for the current bounded Array.Copy, then the DSP
            // thread cannot mutate these samples while the main thread writes.
            var pcm = target.Stop(); buffer = null;
            try
            {
                receipt.utcEnd = DateTime.UtcNow.ToString("O"); receipt.realtimeEnd = Time.realtimeSinceStartupAsDouble;
                receipt.worldTimeEnd = capturedWorld?.Time ?? 0; receipt.endFrame = Time.frameCount;
                receipt.currentMatchId = game != null ? game.MatchId : ""; receipt.stopReason = reason;
                receipt.eventAudioPlayedEnd = game != null ? game.Audio?.PlayedCount ?? 0 : receipt.eventAudioPlayedStart;
                receipt.singleSession = game != null && game.MatchId == receipt.matchId && ReferenceEquals(capturedWorld, game.World) && reason != "reinitialized";
                PopulatePcmReceipt(pcm, receipt, !audioConfigurationChanged && reason != "sample_rate_changed");
                receipt.stateChanges = states.ToArray();
                string directory = Path.Combine(Application.persistentDataPath, "AudioCaptures"); Directory.CreateDirectory(directory);
                string stem = "Frontier-Audio-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N");
                receipt.wavFile = stem + ".wav";
                string path = Path.Combine(directory, stem);
                using (var output = File.Create(path + ".wav.partial")) WriteWave(output, pcm);
                File.Move(path + ".wav.partial", path + ".wav");
                using (var input = File.OpenRead(path + ".wav"))
                using (var hash = SHA256.Create()) receipt.wavSha256 = BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                // The receipt is the completion record, written after the WAV.
                File.WriteAllText(path + ".json", JsonUtility.ToJson(receipt, true)); LastReceiptPath = path + ".json";
                string status = !receipt.hasDspData ? "EMPTY" : !receipt.singleSession ? "SESSION_CHANGED" : !receipt.validFormat ? "FORMAT_CHANGED" : !receipt.validSignal ? "SIGNAL_INVALID" : "OK";
                Debug.Log("FRONTIER_AUDIO_CAPTURE_" + status + " " + LastReceiptPath);
                if (notify && game != null) game.Notify(receipt.hasDspData ? "Audio saved to AudioCaptures." : "No native audio samples were received.");
            }
            catch (Exception ex)
            {
                Debug.LogError("FRONTIER_AUDIO_CAPTURE_FAILED " + ex.GetType().Name + ": " + ex.Message);
                if (notify && game != null) game.Notify("Audio capture could not be saved.");
            }
            finally { receipt = null; capturedWorld = null; states.Clear(); }
        }

        public static void CalculateMetrics(NativePcmSnapshot pcm, NativeAudioReceipt result)
        {
            result.peak = 0; result.rms = 0; result.clippedSamples = 0; result.nonfiniteSamples = 0;
            double square = 0; int finite = 0;
            for (int i = 0; i < pcm.Count; i++)
            {
                float sample = pcm.Samples[i];
                if (float.IsNaN(sample) || float.IsInfinity(sample)) { result.nonfiniteSamples++; continue; }
                float absolute = Math.Abs(sample); result.peak = Math.Max(result.peak, absolute);
                if (absolute >= 1) result.clippedSamples++;
                square += (double)sample * sample; finite++;
            }
            result.rms = finite > 0 ? Math.Sqrt(square / finite) : 0;
        }

        public static void PopulatePcmReceipt(NativePcmSnapshot pcm, NativeAudioReceipt result, bool configurationUnchanged = true)
        {
            result.sampleRate = pcm.SampleRate; result.channels = pcm.Channels; result.frames = pcm.Frames;
            result.samples = pcm.Count; result.dspBlocks = pcm.Blocks; result.formatErrors = pcm.FormatErrors;
            result.discardedSamples = pcm.DiscardedSamples; result.samplesExcludedAtDurationLimit = pcm.SamplesExcludedAtDurationLimit;
            result.hasDspData = pcm.Count > 0; result.audioSeconds = (double)pcm.Frames / pcm.SampleRate;
            result.validFormat = result.hasDspData && pcm.FormatErrors == 0 && pcm.DiscardedSamples == 0 && configurationUnchanged;
            CalculateMetrics(pcm, result);
            result.validSignal = result.validFormat && result.nonfiniteSamples == 0 && result.clippedSamples == 0;
        }

        public static void WriteWave(Stream output, NativePcmSnapshot pcm)
        {
            // Empty captures still carry a valid header; their receipt explicitly
            // says no DSP data. Never synthesize a successful sounding sample.
            int channels = Math.Max(1, pcm.Channels), bytes = checked(pcm.Count * 2);
            using (var writer = new BinaryWriter(output, System.Text.Encoding.ASCII, true))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(bytes + 36);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)channels); writer.Write(pcm.SampleRate);
                writer.Write(pcm.SampleRate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
                for (int i = 0; i < pcm.Count; i++)
                {
                    float sample = pcm.Samples[i];
                    if (float.IsNaN(sample) || float.IsInfinity(sample)) sample = 0;
                    writer.Write((short)Math.Round(Math.Max(-1, Math.Min(1, sample)) * 32767));
                }
            }
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged) { audioConfigurationChanged = true; }
        private void OnEnable() { AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged; }
        private void OnApplicationQuit() { Finish("quit", false); }
        private void OnDisable() { AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged; Finish("disabled", false); }
    }
}
