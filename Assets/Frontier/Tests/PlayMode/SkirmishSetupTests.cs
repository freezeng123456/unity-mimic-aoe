using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Frontier.Tests
{
    public class SkirmishSetupTests
    {
        [UnityTest] public IEnumerator CaravanViewUsesVisibilityAndRestartReplacesItsState()
        {
            var go=new GameObject("Landmark view verification");
            try
            {
                var game=go.AddComponent<GameController>();yield return null;game.StartMatch();yield return null;
                var caravan=go.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Neutral caravan");Assert.IsFalse(caravan.gameObject.activeSelf);
                game.TogglePause();game.World.Fog.Reveal(0,game.World.CaravanPosition,8);yield return null;Assert.IsTrue(caravan.gameObject.activeSelf);
                game.StartMatch();yield return null;yield return null;
                Assert.IsTrue(caravan==null,"Old presentation object must be destroyed on restart");
                Assert.AreEqual(0,game.World.Players[0].treasuresClaimed);Assert.AreEqual(6,game.World.Treasures.Count);
                var replacement=go.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Neutral caravan");Assert.IsFalse(replacement.gameObject.activeSelf);
            }
            finally{Object.Destroy(go);}yield return null;
        }
        [UnityTest] public IEnumerator UnexploredOutcropsStayHiddenUntilObserved()
        {
            var go=new GameObject("Terrain visibility verification");
            try
            {
                var game=go.AddComponent<GameController>();yield return null;game.StartMatch();yield return null;yield return new WaitForSecondsRealtime(.4f);
                var rocks=go.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Navigation outcrop").ToArray();Assert.Greater(rocks.Length,0);
                Assert.IsTrue(rocks.All(r=>!r.gameObject.activeSelf));
                var rock=rocks[0];game.World.Fog.Reveal(0,new Vector2(rock.transform.position.x,rock.transform.position.z),5);
                game.TogglePause();yield return new WaitForSecondsRealtime(.4f);
                Assert.IsTrue(rock.gameObject.activeSelf,"Observed terrain appears through the same fog state as the minimap");
            }
            finally{Object.Destroy(go);}yield return null;
        }
        [UnityTest] public IEnumerator SelectedCivilizationsAgesAndDeckSurviveRestart()
        {
            var go=new GameObject("Setup verification");
            try
            {
                var game=go.AddComponent<GameController>();yield return null;
                game.PlayerCivilization="river";game.OpponentCivilization="coast";game.AgeLimit=5;
                game.SelectedDeck.Clear();game.SelectedDeck.Add("river_coin");game.SelectedDeck.Add("bankers");game.StartMatch();yield return null;
                Assert.AreEqual("river",game.World.Players[0].civilization);Assert.AreEqual("coast",game.World.Players[1].civilization);Assert.AreEqual(5,game.World.MaxAge);
                CollectionAssert.AreEquivalent(new[]{"river_coin","bankers"},game.World.Players[0].deck);
                var old=game.World;game.StartMatch();yield return null;Assert.AreNotSame(old,game.World);Assert.AreEqual(400,game.World.Players[0].resources.coin);
                CollectionAssert.AreEquivalent(new[]{"river_coin","bankers"},game.World.Players[0].deck);
            }
            finally{Object.Destroy(go);}yield return null;
        }
        [UnityTest] public IEnumerator IllegalConfiguredDeckCannotSilentlyStartWithAnotherDeck()
        {
            var go=new GameObject("Invalid deck verification");
            try
            {
                var game=go.AddComponent<GameController>();yield return null;game.PlayerCivilization="river";game.SelectedDeck.Clear();game.SelectedDeck.Add("pikes");game.StartMatch();yield return null;
                Assert.IsTrue(game.InMenu);Assert.IsNull(game.World);StringAssert.Contains("civilization",game.Notice);
            }
            finally{Object.Destroy(go);}yield return null;
        }
    }
}
