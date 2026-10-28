using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class NoticeSoundTests
    {
        [Test] public void StrategicEventsHaveDistinctOriginalFiniteEnvelopeClips()
        {
            string[] kinds = { "ShipmentArrived", "AgeAdvanced", "PopulationShortage", "ResourceDepleted", "BuildingComplete", "TreasureClaimed" };
            var cues = new HashSet<NoticeCue>(); var fingerprints = new HashSet<int>();
            foreach (string kind in kinds)
            {
                Assert.IsTrue(NoticeSoundClips.TryClassify(kind, out var cue)); Assert.IsTrue(cues.Add(cue));
                var clip = NoticeSoundClips.Create(cue);
                try
                {
                    Assert.AreEqual(1, clip.channels); Assert.AreEqual(22050, clip.frequency); Assert.That(clip.length, Is.InRange(.2f, .6f));
                    var data = new float[clip.samples]; Assert.IsTrue(clip.GetData(data, 0));
                    Assert.AreEqual(0, data[0]); Assert.AreEqual(0, data[data.Length - 1]);
                    float peak = 0; int nonzero = 0, fingerprint = 19;
                    for (int i = 0; i < data.Length; i++)
                    {
                        Assert.IsFalse(float.IsNaN(data[i]) || float.IsInfinity(data[i])); peak = Mathf.Max(peak, Mathf.Abs(data[i]));
                        if (Mathf.Abs(data[i]) > .001f) nonzero++;
                        if (i % 71 == 0) unchecked { fingerprint = fingerprint * 31 + Mathf.RoundToInt(data[i] * 32767); }
                    }
                    Assert.That(peak, Is.InRange(.05f, .6f)); Assert.Greater(nonzero, 100);
                    Assert.IsTrue(fingerprints.Add(fingerprint), "Different notices must not all reuse one generic beep");
                }
                finally { Object.DestroyImmediate(clip); }
            }
            Assert.IsFalse(NoticeSoundClips.TryClassify("Fire", out var unrelated)); Assert.AreEqual(NoticeCue.None, unrelated);
        }
    }
}
