using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class AmbientSoundTests
    {
        [Test] public void OriginalLoopClipsHaveFiniteSignalAndClickFreeBoundaries()
        {
            foreach (var cue in new[] { AmbientCue.Wind, AmbientCue.FrontierTheme })
            {
                var clip = AmbientSoundClips.Create(cue);
                try
                {
                    Assert.AreEqual(1, clip.channels); Assert.AreEqual(22050, clip.frequency);
                    Assert.AreEqual(cue == AmbientCue.Wind ? 16 : 24, clip.length);
                    var data = new float[clip.samples]; Assert.IsTrue(clip.GetData(data, 0));
                    Assert.AreEqual(0, data[0]); Assert.AreEqual(0, data[data.Length - 1]);
                    double energy = 0; float peak = 0, boundaryJump = 0;
                    for (int i = 0; i < data.Length; i++)
                    {
                        Assert.IsFalse(float.IsNaN(data[i]) || float.IsInfinity(data[i]));
                        energy += data[i] * data[i]; peak = Mathf.Max(peak, Mathf.Abs(data[i]));
                        if (i > 0 && (i < 1103 || i > data.Length - 1103)) boundaryJump = Mathf.Max(boundaryJump, Mathf.Abs(data[i] - data[i - 1]));
                    }
                    Assert.Greater(energy, .01); Assert.That(peak, Is.InRange(.005f, .35f)); Assert.Less(boundaryJump, .03f);
                }
                finally { Object.DestroyImmediate(clip); }
            }
        }
    }
}
