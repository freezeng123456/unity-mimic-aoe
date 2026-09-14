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
    // Real input edges and UGUI dispatch. Callback timestamps only observe the
    // button; they never invoke it or replace the command under test.
    public sealed class NonModalPointerTests : NativeInputFixture
    {
        private double actionAt;
        private int Payments()=>game.World.Events.Count(e=>e.kind=="Payment"&&e.player==0);
        private int Refunds()=>game.World.Events.Count(e=>e.kind=="Refund"&&e.player==0);
        private void Down(Vector2 p)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Left));
        private void Up(Vector2 p)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=p});
        private IEnumerator ObserveClick(Button button)
        {
            actionAt=double.NaN;
            button.onClick.AddListener(()=>actionAt=Time.realtimeSinceStartupAsDouble);
            yield return Click(ButtonCenter(button));
            Assert.IsFalse(double.IsNaN(actionAt),"Actual UGUI callback must have fired");
        }
        private void AssertFresh()
        {
            Assert.Less(Time.realtimeSinceStartupAsDouble-actionAt,.22,
                "The entire interval from the actual UI callback through the next world action must stay below 220ms");
        }

        [UnityTest] public IEnumerator TrainThenGroundSelectThenBuildUsesFreshNonModalActions()
        {
            var town=SpawnHere("town",0,site);var worker=SpawnHere("villager",0,site+Vector2.right*7);
            game.Controls.Select(town);yield return Ready();
            var before=Payments();var food=game.World.Players[0].resources.food;
            yield return ObserveClick(FindButton("Settler"));
            Assert.AreEqual(before+1,Payments());Assert.AreEqual(1,town.queue.Count);
            Assert.AreEqual(food-100,game.World.Players[0].resources.food,.001f);
            var point=ScreenPoint(worker.position,1);AssertWorldPoint(point);
            yield return Click(point);AssertFresh();
            Assert.AreEqual(worker.id,game.Controls.FirstSelected?.id,"A distinct ground click after training must select the worker immediately");
            yield return Press(Key.B);Assert.IsTrue(game.Hud.ModalVisible);
            Assert.AreEqual(before+1,Payments());Assert.AreEqual(1,town.queue.Count);
        }

        [UnityTest] public IEnumerator CancelTrainingThenRightClickResourceSetsRallyAndRefundsOnce()
        {
            var town=SpawnHere("town",0,site);var berry=SpawnHere("berry",-1,site+new Vector2(8,4));
            game.Controls.Select(town);yield return Ready();var food=game.World.Players[0].resources.food;
            yield return Click(ButtonCenter(FindButton("Settler")));
            Assert.AreEqual(1,town.queue.Count);Assert.AreEqual(food-100,game.World.Players[0].resources.food,.001f);
            var before=Payments();var refunds=Refunds();
            var queueButton=session.GetComponentsInChildren<Button>().Single(b=>b.transform.parent.name=="Production queue");
            yield return ObserveClick(queueButton);
            Assert.IsEmpty(town.queue);Assert.AreEqual(refunds+1,Refunds());
            Assert.AreEqual(food,game.World.Players[0].resources.food,.001f);
            var point=ScreenPoint(berry.position);AssertWorldPoint(point);
            yield return Click(point,MouseButton.Right);AssertFresh();
            Assert.AreEqual(berry.id,town.rallyTarget,"A distinct resource click after cancel must set the rally immediately");
            Assert.AreEqual(before,Payments());Assert.AreEqual(refunds+1,Refunds());
            Assert.AreEqual(food,game.World.Players[0].resources.food,.001f);
        }

        [UnityTest] public IEnumerator AttackMoveButtonThenFreshGroundClickCreatesOneAttackMove()
        {
            var explorer=SpawnHere("explorer",0,site);game.Controls.Select(explorer);yield return Ready();
            yield return ObserveClick(FindButton("ATTACK MOVE"));Assert.IsTrue(game.Controls.AttackMode);
            var point=site+new Vector2(8,5);AssertWorldPoint(ScreenPoint(point));
            yield return Click(ScreenPoint(point));AssertFresh();
            Assert.IsFalse(game.Controls.AttackMode,"A distinct ground click must complete attack-move targeting immediately");
            Assert.AreEqual(1,explorer.orders.Count);Assert.AreEqual(OrderKind.AttackMove,explorer.orders.Single().kind);
        }

        [UnityTest] public IEnumerator UiDownMoveToGroundUpDoesNotInvokeButtonOrWorldAction()
        {
            var town=SpawnHere("town",0,site);var worker=SpawnHere("villager",0,site+Vector2.right*7);
            game.Controls.Select(town);yield return Ready();var payment=Payments();var food=game.World.Players[0].resources.food;
            var source=ButtonCenter(FindButton("Settler"));var destination=ScreenPoint(worker.position,1);AssertWorldPoint(destination);
            Down(source);yield return Ready();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=destination});Up(destination);yield return Ready();
            Assert.AreEqual(town.id,game.Controls.FirstSelected?.id);Assert.AreEqual(payment,Payments());
            Assert.AreEqual(food,game.World.Players[0].resources.food,.001f);
            Assert.IsEmpty(town.queue);Assert.IsEmpty(worker.orders);Assert.IsFalse(game.Hud.ModalVisible);
        }
    }
}
