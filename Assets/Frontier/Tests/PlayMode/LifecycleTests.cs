using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class LifecycleTests
    {
        [UnityTest]
        public IEnumerator MatchPauseRestartRebuildsRuntime()
        {
            var go=new GameObject("GameController");
            try
            {
                var controller=go.AddComponent<GameController>();
                yield return null;
                controller.StartMatch();
                yield return null;
                Assert.That(controller.World,Is.Not.Null);
                Assert.That(controller.GameCamera,Is.Not.Null);
                Assert.That(controller.Hud,Is.Not.Null);
                var first=controller.World;var firstEntity=first.Entities[0];var t=first.Time;
                controller.TogglePause();Assert.That(controller.IsPaused,Is.True);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(first.Time,Is.EqualTo(t).Within(.0001f));
                controller.ReturnToMenu();Assert.That(controller.InMenu,Is.True);
                controller.StartMatch();yield return null;
                Assert.That(controller.World,Is.Not.SameAs(first));
                Assert.That(controller.World.Seed,Is.EqualTo(314159));
                Assert.That(controller.World.Players[0].usedPopulation,Is.EqualTo(first.Players[0].usedPopulation));
                Assert.That(controller.World.Get(firstEntity.id),Is.Not.SameAs(firstEntity));
            }
            finally { Object.Destroy(go); }
            yield return null;
        }
    }
}
