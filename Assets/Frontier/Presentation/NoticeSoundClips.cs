using System;
using UnityEngine;

namespace Frontier
{
    public enum NoticeCue { None, Shipment, AgeAdvanced, PopulationShortage, ResourceDepleted, BuildingComplete, Treasure }

    // Project-original melodic cues. Each event has a distinct contour; no
    // external recording or commercial game audio is used.
    public static class NoticeSoundClips
    {
        public static bool TryClassify(string kind, out NoticeCue cue)
        {
            switch (kind)
            {
                case "ShipmentArrived": cue = NoticeCue.Shipment; return true;
                case "AgeAdvanced": cue = NoticeCue.AgeAdvanced; return true;
                case "PopulationShortage": cue = NoticeCue.PopulationShortage; return true;
                case "ResourceDepleted": cue = NoticeCue.ResourceDepleted; return true;
                case "BuildingComplete": cue = NoticeCue.BuildingComplete; return true;
                case "TreasureClaimed": cue = NoticeCue.Treasure; return true;
                default: cue = NoticeCue.None; return false;
            }
        }

        public static AudioClip Create(NoticeCue cue)
        {
            const int rate = 22050;
            float[] frequencies; float seconds, stagger, noteLength;
            switch (cue)
            {
                case NoticeCue.Shipment: frequencies = new[] { 523.25f, 783.99f }; seconds = .33f; stagger = .14f; noteLength = .19f; break;
                case NoticeCue.AgeAdvanced: frequencies = new[] { 392f, 493.88f, 587.33f, 783.99f }; seconds = .56f; stagger = .085f; noteLength = .30f; break;
                case NoticeCue.PopulationShortage: frequencies = new[] { 330f, 330f }; seconds = .29f; stagger = .15f; noteLength = .14f; break;
                case NoticeCue.ResourceDepleted: frequencies = new[] { 440f, 293.66f }; seconds = .24f; stagger = .08f; noteLength = .16f; break;
                case NoticeCue.BuildingComplete: frequencies = new[] { 392f, 523.25f }; seconds = .28f; stagger = .09f; noteLength = .19f; break;
                case NoticeCue.Treasure: frequencies = new[] { 659.25f, 783.99f, 1046.50f }; seconds = .37f; stagger = .09f; noteLength = .19f; break;
                default: throw new ArgumentOutOfRangeException(nameof(cue));
            }
            int count = Mathf.CeilToInt(rate * seconds); var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float time = (float)i / rate, sum = 0;
                for (int note = 0; note < frequencies.Length; note++)
                {
                    float elapsed = time - note * stagger;
                    if (elapsed < 0 || elapsed >= noteLength) continue;
                    float envelope = Mathf.Min(1, elapsed / .004f) * Mathf.Min(1, (noteLength - elapsed) / .025f) * Mathf.Exp(-3 * elapsed / noteLength);
                    float phase = 2 * Mathf.PI * frequencies[note] * elapsed;
                    sum += envelope * (Mathf.Sin(phase) + .12f * Mathf.Sin(phase * 2)) * .25f;
                }
                data[i] = Mathf.Clamp(sum, -.6f, .6f);
            }
            data[0] = data[count - 1] = 0;
            var clip = AudioClip.Create("Notice " + cue, count, 1, rate, false); clip.SetData(data, 0); return clip;
        }
    }
}
