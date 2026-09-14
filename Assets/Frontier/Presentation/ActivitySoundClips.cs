using System;
using UnityEngine;

namespace Frontier
{
    // Short original placeholder cues for activity feedback. These are synthesized
    // at runtime so the verification fixture carries no third-party audio asset.
    // They are deliberately presentation cues, not final recordings.
    public static class ActivitySoundClips
    {
        private const int SampleRate = 22050;

        public static AudioClip Create(EventAudio.SoundCategory category)
        {
            float frequency, seconds, noise, tone;
            int seed;
            switch (category)
            {
                case EventAudio.SoundCategory.Footstep:
                    frequency = 135; seconds = .10f; noise = .72f; tone = .28f; seed = 1901; break;
                case EventAudio.SoundCategory.Hoof:
                    frequency = 205; seconds = .13f; noise = .38f; tone = .62f; seed = 1903; break;
                case EventAudio.SoundCategory.Wheel:
                    frequency = 92; seconds = .22f; noise = .30f; tone = .70f; seed = 1907; break;
                case EventAudio.SoundCategory.Woodcut:
                    frequency = 175; seconds = .19f; noise = .55f; tone = .45f; seed = 1913; break;
                case EventAudio.SoundCategory.Mine:
                    frequency = 265; seconds = .16f; noise = .32f; tone = .68f; seed = 1919; break;
                case EventAudio.SoundCategory.Handling:
                    frequency = 320; seconds = .085f; noise = .66f; tone = .34f; seed = 1921; break;
                case EventAudio.SoundCategory.Build:
                    frequency = 118; seconds = .27f; noise = .48f; tone = .52f; seed = 1927; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, "Activity cue required");
            }

            int count = Mathf.Max(2, Mathf.CeilToInt(SampleRate * seconds));
            var samples = new float[count];
            var random = new System.Random(seed);
            float filteredNoise = 0;
            float phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                float elapsed = t * seconds;
                float attack = Mathf.Clamp01(elapsed / .002f);
                float release = Mathf.Clamp01((seconds - elapsed) / .02f);
                float decay = category == EventAudio.SoundCategory.Wheel ? 2.2f : 6f;
                float envelope = attack * release * Mathf.Exp(-decay * t);
                float sampleNoise = (float)random.NextDouble() * 2 - 1;
                filteredNoise = Mathf.Lerp(filteredNoise, sampleNoise, .32f);
                phase += 2 * Mathf.PI * frequency / SampleRate;
                float resonance = Mathf.Sin(phase);
                if (category == EventAudio.SoundCategory.Mine)
                    resonance = (resonance + .35f * Mathf.Sin(phase * 4.7f) + .2f * Mathf.Sin(phase * 7.1f)) / 1.55f;
                float click = resonance * tone + filteredNoise * noise;
                samples[i] = Mathf.Clamp(click * envelope * .42f, -.7f, .7f);
            }
            samples[0] = 0;
            samples[count - 1] = 0;
            var clip = AudioClip.Create("Activity " + category, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
