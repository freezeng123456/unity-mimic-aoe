using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    // The focus entry point is invoked through SendMessage so this file remains
    // compilable against the 0.2.10 runtime, which has no PauseForFocusLoss API.
    public sealed class FocusPauseTests : NativeInputFixture
    {
        private EntityState Town()=>game.World.Entities.First(e=>e.owner==0&&e.def.id=="town");
        private EntityState Worker()=>game.World.Entities.First(e=>e.owner==0&&e.Worker);
        private void FocusLoss()=>game.SendMessage("PauseForFocusLoss",SendMessageOptions.DontRequireReceiver);
        private void QueueHeldTrain(Button button)
        {
            var p=ButtonCenter(button);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Left));
        }

        [UnityTest] public IEnumerator FocusLossFreezesWorldTimeAndProductionGatheringAndShipment()
        {
            var town=Town();var worker=Worker();var resource=game.World.Entities.First(e=>e.owner==0&&e.def.resource!=ResourceKind.None);
            worker.position=resource.position+Vector2.left; // Controlled close-range fixture, not a native player action.
            Assert.IsTrue(game.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Gather,target=resource.id}).accepted);
            var unit=game.Rules.entities.First(e=>e.kind==EntityKind.Unit&&e.producer==town.def.id&&e.civilization=="");
            var trained=game.World.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item=unit.id});Assert.IsTrue(trained.accepted);
            var card=game.World.Players[0].deck.First();game.World.Players[0].shipmentTokens=1;
            var shipped=game.World.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item=card});Assert.IsTrue(shipped.accepted);
            game.enabled=true;yield return new WaitForSecondsRealtime(.35f);game.enabled=false;
            Assert.Greater(game.World.Players[0].gathered.food,0);Assert.Greater(town.queue[0].elapsed,0);Assert.Greater(game.World.Players[0].shipments[0].elapsed,0);
            game.enabled=true;FocusLoss();yield return Ready();game.enabled=false;
            Assert.IsTrue(game.IsPaused);var t=game.World.Time;var food=game.World.Players[0].gathered.food;var q=town.queue[0].elapsed;var s=game.World.Players[0].shipments[0].elapsed;
            game.enabled=true;yield return new WaitForSecondsRealtime(.15f);game.enabled=false;
            Assert.AreEqual(t,game.World.Time,0.001f);Assert.AreEqual(food,game.World.Players[0].gathered.food,0.001f);Assert.AreEqual(q,town.queue[0].elapsed,0.001f);Assert.AreEqual(s,game.World.Players[0].shipments[0].elapsed,0.001f);
        }

        [UnityTest] public IEnumerator RepeatedFocusLossStaysPausedAndFocusGainDoesNotResume()
        {
            FocusLoss();FocusLoss();yield return Ready();
            game.SendMessage("OnApplicationFocus",true,SendMessageOptions.DontRequireReceiver);yield return Ready();
            Assert.IsTrue(game.IsPaused);Assert.IsTrue(game.Hud.ModalVisible);
        }

        [UnityTest] public IEnumerator ExistingManualPauseRemainsManualAndPaused()
        {
            game.TogglePause();yield return Ready();Assert.IsTrue(game.IsPaused);
            FocusLoss();yield return Ready();Assert.IsTrue(game.IsPaused);
            Assert.IsFalse(session.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("left the game window")));
        }

        [UnityTest] public IEnumerator FocusLossReplacesHomeAndAgeModalWithoutPaymentOrShipment()
        {
            var town=Town();game.Controls.Select(town);game.Hud.ShowHomeCity();yield return Ready();
            var before=game.World.Players[0].spent;var sent=game.World.Players[0].shipsSent;FocusLoss();yield return Ready();
            Assert.IsTrue(game.IsPaused);Assert.IsTrue(game.Hud.ModalVisible);Assert.IsTrue(session.GetComponentsInChildren<Text>().Any(t=>t.text=="MATCH PAUSED"));
            Assert.AreEqual(before.food,game.World.Players[0].spent.food,0.001f);Assert.AreEqual(sent,game.World.Players[0].shipsSent);
            game.TogglePause();yield return Ready();game.Controls.Select(town);game.Hud.ShowAgeChoices();yield return Ready();Assert.IsTrue(session.GetComponentsInChildren<Text>().Any(t=>t.text.StartsWith("ADVANCE TO AGE")));var spent=game.World.Players[0].spent;FocusLoss();yield return Ready();Assert.IsTrue(game.IsPaused);Assert.IsTrue(session.GetComponentsInChildren<Text>().Any(t=>t.text=="MATCH PAUSED"));Assert.AreEqual(spent.food,game.World.Players[0].spent.food,0.001f);
        }


        [UnityTest] public IEnumerator FocusLossRetainsSelectionAndGroupsButClearsPendingModes()
        {
            var worker=Worker();game.Controls.Select(worker);game.Controls.SaveGroup(1);
            game.Controls.BeginAttackMove();FocusLoss();yield return Ready();Assert.IsFalse(game.Controls.AttackMode);game.TogglePause();yield return Ready();
            game.Controls.BeginBuild("house");FocusLoss();yield return Ready();Assert.IsFalse(game.Controls.AttackMode);Assert.IsEmpty(game.Controls.BuildingToPlace);game.TogglePause();yield return Ready();
            game.Controls.BeginGarrison();FocusLoss();yield return Ready();
            Assert.AreEqual(worker.id,game.Controls.FirstSelected.id);Assert.AreEqual(1,game.Controls.GroupCount(1));Assert.IsFalse(game.Controls.AttackMode);Assert.IsFalse(game.Controls.GarrisonMode);Assert.IsEmpty(game.Controls.BuildingToPlace);
        }

        [UnityTest] public IEnumerator HeldTrainPressAcrossFocusLossReleaseDoesNotPay()
        {
            var town=Town();game.Controls.Select(town);yield return Ready();
            var button=FindButton("Settler");var point=ButtonCenter(button);
            var before=game.World.Players[0].spent;
            QueueHeldTrain(button);yield return Ready();FocusLoss();yield return Ready();InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Ready();
            Assert.AreEqual(before.food,game.World.Players[0].spent.food,0.001f);Assert.IsEmpty(town.queue);
        }

        [UnityTest] public IEnumerator ExplicitResumeRestoresProgression()
        {
            FocusLoss();yield return Ready();var before=game.World.Time;game.TogglePause();yield return Ready();game.enabled=true;yield return new WaitForSecondsRealtime(.15f);game.enabled=false;
            Assert.IsFalse(game.IsPaused);Assert.Greater(game.World.Time,before);
        }

        [UnityTest] public IEnumerator PauseXActsAsResumeAndRestartOrMenuCleansState()
        {
            game.TogglePause();yield return Ready();Assert.IsTrue(game.IsPaused);yield return Click(ButtonCenter(FindButton("X")));Assert.IsFalse(game.IsPaused);
            FocusLoss();yield return Ready();game.StartMatch();yield return Ready();Assert.IsFalse(game.IsPaused);Assert.IsFalse(game.Hud.ModalVisible);
            FocusLoss();yield return Ready();game.ReturnToMenu();yield return Ready();Assert.IsTrue(game.InMenu);Assert.IsNull(game.World);Assert.IsFalse(game.Hud.ModalVisible);
        }

        [UnityTest] public IEnumerator AutoplayBenchmarkMenuAndResultAreFocusPauseExempt()
        {
            game.Autoplay=true;FocusLoss();yield return Ready();Assert.IsFalse(game.IsPaused);
            game.Autoplay=false;game.BenchmarkMode=true;FocusLoss();yield return Ready();Assert.IsFalse(game.IsPaused);
            game.BenchmarkMode=false;Assert.IsTrue(game.World.Issue(new GameCommand{player=0,kind=CommandKind.Resign}).accepted);game.Hud.ShowResult();
            FocusLoss();yield return Ready();Assert.IsFalse(game.IsPaused);Assert.IsTrue(game.Hud.ResultVisible);Assert.AreEqual(1,game.World.Winner);
            game.ReturnToMenu();FocusLoss();yield return Ready();Assert.IsTrue(game.InMenu);Assert.IsFalse(game.IsPaused);
        }
    }
}
