using System;
using UnityEngine;

namespace Frontier
{
    public enum AmbientCue { Wind, FrontierTheme }

    // An original restrained wind bed and four-chord instrumental theme. Clips
    // are synthesized once per controller, not recreated by render frames.
    public static class AmbientSoundClips
    {
        public const int SampleRate = 22050;
        public static AudioClip Create(AmbientCue cue)
        {
            int seconds = cue == AmbientCue.Wind ? 16 : 24;
            int count = SampleRate * seconds; var data = new float[count];
            if (cue == AmbientCue.Wind)
            {
                var random = new System.Random(62031); float smooth = 0;
                for (int i = 0; i < count; i++)
                {
                    float time = (float)i / SampleRate;
                    smooth = Mathf.Lerp(smooth, (float)random.NextDouble() * 2 - 1, .035f);
                    float breeze = .65f + .2f * Mathf.Sin(2 * Mathf.PI * time / 8) + .15f * Mathf.Sin(2 * Mathf.PI * time / 16);
                    data[i] = smooth * breeze * .28f;
                }
            }
            else if (cue == AmbientCue.FrontierTheme)
            {
                // D minor / C major / G minor / A suspended. The timbre and
                // phrase are authored here; no external score is sampled.
                float[,] chords = { { 146.83f, 174.61f, 220f }, { 130.81f, 164.81f, 196f }, { 98f, 116.54f, 146.83f }, { 110f, 146.83f, 164.81f } };
                float[] melody = { 293.66f, 349.23f, 440f, 392f, 349.23f, 329.63f, 293.66f, 261.63f };
                for (int i = 0; i < count; i++)
                {
                    float time = (float)i / SampleRate; int chord = Mathf.Min(3, (int)(time / 6));
                    float local = time - chord * 6;
                    float pad = Mathf.Min(1, local / .65f) * Mathf.Min(1, (6 - local) / 1.2f), value = 0;
                    for (int note = 0; note < 3; note++)
                    {
                        float phase = 2 * Mathf.PI * chords[chord, note] * time;
                        value += (Mathf.Sin(phase) + .12f * Mathf.Sin(phase * 2)) * pad * .07f;
                    }
                    int phrase = Mathf.Min(7, (int)(time / 3)); float noteTime = time - phrase * 3;
                    float pluck = Mathf.Min(1, noteTime / .015f) * Mathf.Exp(-2.3f * noteTime);
                    value += Mathf.Sin(2 * Mathf.PI * melody[phrase] * noteTime) * pluck * .095f;
                    data[i] = Mathf.Clamp(value, -.35f, .35f);
                }
            }
            else throw new ArgumentOutOfRangeException(nameof(cue));
            // The short boundary fade prevents clicks when a loop restarts.
            int fade = Mathf.CeilToInt(SampleRate * .05f);
            for (int i = 0; i < fade; i++) { float gain = (float)i / fade; data[i] *= gain; data[count - 1 - i] *= gain; }
            data[0] = data[count - 1] = 0;
            var clip = AudioClip.Create("Original " + cue, count, 1, SampleRate, false); clip.SetData(data, 0); return clip;
        }
    }

    public sealed class AmbientAudio : MonoBehaviour
    {
        private GameController game;
        private PlayerSettingsService settings;
        private PlayerSettings options=PlayerSettings.Defaults();
        private World observedWorld;
        private AudioSource wind, music;
        private AudioClip windClip, musicClip;
        private bool paused;
        public float EffectiveEnvironmentVolume=>options.muted||!options.environmentEnabled?0:options.masterVolume*options.environmentVolume;
        public float EffectiveMusicVolume=>options.muted||!options.musicEnabled?0:options.masterVolume*options.musicVolume;
        public int SourceCount => wind != null && music != null ? 2 : 0;
        public bool WindPlaying => wind != null && wind.isPlaying;
        public bool MusicPlaying => music != null && music.isPlaying;

        public void Initialize(GameController controller)
        {
            Dispose(); game = controller;settings=game.Settings;options=settings.Current;settings.Changed+=ApplySettings;
            windClip = AmbientSoundClips.Create(AmbientCue.Wind); musicClip = AmbientSoundClips.Create(AmbientCue.FrontierTheme);
            wind = CreateSource(AmbientCue.Wind, windClip, .065f); music = CreateSource(AmbientCue.FrontierTheme, musicClip, .045f);
            ApplySettings();
        }

        private AudioSource CreateSource(AmbientCue cue, AudioClip clip, float volume)
        {
            var child = new GameObject("Original " + cue + " bed"); child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0;
            source.loop = true; source.volume = volume; source.clip = clip; return source;
        }

        private void Update()
        {
            if (game == null || wind == null || music == null) return;
            if (!ReferenceEquals(observedWorld, game.World)) { ResetMatch(); observedWorld = game.World; }
            if (game.InMenu || game.World == null || game.World.Winner >= 0 || options.muted || options.masterVolume<=0)
            { wind.Stop(); music.Stop(); return; }
            if (paused != game.IsPaused) SetPaused(game.IsPaused);
            if (paused) return;
            // Ambient wind describes only the viewed, currently visible terrain;
            // it never follows or reveals a hidden enemy source or location.
            bool visibleGround = game.World.Fog.At(game.ObserverPlayer, game.Controls.CameraFocus) == VisibilityState.Visible;
            PlayLoop(wind, EffectiveEnvironmentVolume>0 && visibleGround);
            PlayLoop(music, EffectiveMusicVolume>0);
        }

        private static void PlayLoop(AudioSource source, bool enabled)
        { if (!enabled) source.Stop(); else if (!source.isPlaying) source.Play(); }

        private void ApplySettings()
        {
            options=settings.Current;if(wind==null||music==null)return;
            wind.volume=.065f*EffectiveEnvironmentVolume;music.volume=.045f*EffectiveMusicVolume;
            wind.mute=music.mute=options.muted;
            if(EffectiveEnvironmentVolume<=0)wind.Stop();if(EffectiveMusicVolume<=0)music.Stop();
        }
        public void SetMuted(bool muted)
        {
            if (wind == null || music == null) return;
            wind.mute = music.mute = muted;
            if (muted) { wind.Stop(); music.Stop(); }
        }

        public void SetPaused(bool value)
        {
            paused = value;
            if (wind == null || music == null) return;
            if (value) { wind.Pause(); music.Pause(); }
            else if (game != null) { if(EffectiveEnvironmentVolume>0)wind.UnPause();if(EffectiveMusicVolume>0)music.UnPause(); }
        }

        public void ResetMatch()
        { wind?.Stop(); music?.Stop(); paused = false; observedWorld = null; }

        public void Dispose()
        {
            if(settings!=null)settings.Changed-=ApplySettings;settings=null;
            ResetMatch();
            foreach (var source in new[] { wind, music }) if (source != null)
                Destroy(source.gameObject);
            if (windClip != null) Destroy(windClip); if (musicClip != null) Destroy(musicClip);
            wind = music = null; windClip = musicClip = null; game = null;
        }
        private void OnDisable() { ResetMatch(); }
        private void OnDestroy() { Dispose(); }
    }
}
