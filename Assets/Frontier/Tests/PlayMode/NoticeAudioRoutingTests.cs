using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class NoticeAudioRoutingTests
    {
        [UnityTest] public IEnumerator CaptureReadsTheExistingListenerWithoutAddingAnIsolatedAudioSource()
        {
            var root = new GameObject("Native listener attachment fixture");
            try
            {
                var game = root.AddComponent<GameController>(); yield return null;
                var listener = game.GameCamera.GetComponent<AudioListener>();
                var capture = game.GameCamera.GetComponent<NativeAudioCapture>();
                Assert.IsNotNull(listener); Assert.IsNotNull(capture); Assert.AreSame(listener.gameObject, capture.gameObject);
                Assert.IsTrue(listener.enabled); Assert.IsNull(capture.GetComponent<AudioSource>());
                Assert.IsFalse(capture.Recording, "A normal startup must not begin diagnostic recording");
                Assert.AreEqual(12, game.Audio.SourceCount);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }

        [UnityTest] public IEnumerator DistinctOwnNoticesUseReservedSlotsAndOpponentNoticesStaySilent()
        {
            var root = new GameObject("Notice routing fixture");
            try
            {
                var game = root.AddComponent<GameController>(); yield return null; game.StartMatch(); game.enabled = false;
                string[] kinds = { "ShipmentArrived", "AgeAdvanced", "PopulationShortage", "ResourceDepleted", "BuildingComplete", "TreasureClaimed" };
                foreach (string kind in kinds)
                {
                    game.Audio.ResetMatch();
                    Assert.IsTrue(NoticeSoundClips.TryClassify(kind, out var cue));
                    game.World.Emit(kind, 1, 0, "Opponent strategic event", 0, 0, new Vector2(120, 120));
                    game.Audio.OnEvent(game.World.Events.Last()); Assert.AreEqual(0, game.Audio.PlayedCount);
                    game.World.Emit(kind, 0, 0, "Own strategic event", 0, 0, new Vector2(120, 120));
                    game.Audio.OnEvent(game.World.Events.Last());
                    Assert.AreEqual(1, game.Audio.PlayedCount); Assert.AreEqual(EventAudio.SoundCategory.Notice, game.Audio.LastCategory);
                    Assert.AreEqual(cue, game.Audio.LastNotice); Assert.AreEqual(12, game.Audio.SourceCount);
                    Assert.AreEqual(2, game.Audio.GetComponentsInChildren<AudioSource>().Count(s => s.gameObject.name == "Event audio 10" || s.gameObject.name == "Event audio 11"));
                    yield return null;
                }
                game.Muted = true; game.Audio.ResetMatch();
                game.World.Emit("AgeAdvanced", 0, 0, "Muted event"); game.Audio.OnEvent(game.World.Events.Last()); Assert.AreEqual(0, game.Audio.PlayedCount);
                game.Muted = false; game.World.Paused = true;
                game.Audio.OnEvent(game.World.Events.Last()); Assert.AreEqual(0, game.Audio.PlayedCount);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
    }
}
