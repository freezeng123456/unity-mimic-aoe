using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class AmbientAudioTests
    {
        [UnityTest] public IEnumerator TwoLoopSourcesAreReusedAndRespectMutePauseMenuAndSettings()
        {
            var root = new GameObject("Original ambience lifecycle fixture");
            try
            {
                var game = root.AddComponent<GameController>(); yield return null;
                var bed = game.Ambience; var sources = bed.GetComponentsInChildren<AudioSource>(); var clips = sources.Select(s => s.clip).ToArray();
                Assert.AreEqual(2, bed.SourceCount); Assert.IsTrue(sources.All(s => s.loop && !s.playOnAwake && !s.isPlaying));
                game.StartMatch(); game.enabled = false; yield return null;
                Assert.IsTrue(bed.MusicPlaying); Assert.IsTrue(bed.WindPlaying);
                game.EnvironmentSoundEnabled = false; game.MusicEnabled = false; yield return null;
                Assert.IsFalse(bed.MusicPlaying); Assert.IsFalse(bed.WindPlaying);
                game.EnvironmentSoundEnabled = true; game.MusicEnabled = true; yield return null;
                game.TogglePause(); yield return null; Assert.IsFalse(bed.MusicPlaying); Assert.IsFalse(bed.WindPlaying);
                game.Muted = true; Assert.IsTrue(sources.All(s => s.mute));
                game.TogglePause(); yield return null; Assert.IsFalse(bed.MusicPlaying); Assert.IsFalse(bed.WindPlaying);
                game.Muted = false; yield return null; Assert.IsTrue(bed.MusicPlaying);
                game.Controls.Focus(new Vector2(120, 120)); yield return null;
                Assert.AreNotEqual(VisibilityState.Visible, game.World.Fog.At(0, game.Controls.CameraFocus));
                Assert.IsFalse(bed.WindPlaying, "Viewed unexplored terrain cannot produce localized environment feedback");
                game.ReturnToMenu(); yield return null; Assert.IsFalse(bed.MusicPlaying); Assert.IsFalse(bed.WindPlaying);
                game.StartMatch(); yield return null; Assert.AreEqual(2, bed.SourceCount); CollectionAssert.AreEqual(clips, sources.Select(s => s.clip).ToArray());
                Assert.AreEqual(12, game.Audio.SourceCount, "The two continuous beds cannot consume event and activity slots");
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
    }
}
