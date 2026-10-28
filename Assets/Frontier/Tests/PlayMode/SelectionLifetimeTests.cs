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
    public sealed class SelectionLifetimeTests : NativeInputFixture
    {
        private EntityState Town()=>game.World.Entities.First(e=>e.owner==0&&e.def.id=="town");
        private EntityState Explorer()=>game.World.Entities.First(e=>e.owner==0&&e.def.id=="explorer");
        private EntityState Worker()=>game.World.Entities.First(e=>e.owner==0&&e.Worker);
        private int Events(string kind)=>game.World.Events.Count(e=>e.player==0&&e.kind==kind);
        private void Down(Vector2 p)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Left));
        private void Up(Vector2 p)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=p});
        private void KeyEdge(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));InputSystem.QueueStateEvent(keyboard,new KeyboardState());}

        [UnityTest] public IEnumerator ClearedSelectionCancelsHeldTrainButton()
        {
            game.Controls.Select(Town());yield return Ready();var p=ButtonCenter(FindButton("Settler"));
            Down(p);yield return Ready();game.Controls.Select(null);yield return Ready();Up(p);yield return Ready();
            Assert.IsNull(game.Controls.FirstSelected);Assert.IsEmpty(Town().queue);Assert.AreEqual(0,Events("Payment"));
            Assert.IsFalse(game.Controls.AttackMode);
        }
        [UnityTest] public IEnumerator LeaderDeathCancelsCaptureAndSelectsSurvivingContext()
        {
            var leader=Explorer();game.Controls.Select(leader);game.Controls.Select(Town(),true);yield return Ready();
            var p=ButtonCenter(FindButton("ATTACK MOVE"));Down(p);yield return Ready();
            game.World.ApplyDamage(leader,100000,DamageKind.Siege,1);yield return Ready();Up(p);yield return Ready();
            Assert.AreEqual(Town().id,game.Controls.FirstSelected.id);Assert.IsFalse(game.Controls.Selected.Contains(leader.id));
            Assert.IsFalse(game.Controls.AttackMode);Assert.IsEmpty(Town().queue);Assert.AreEqual(0,Events("Payment"));
            Assert.IsNotNull(FindButton("Settler"));
        }
        [UnityTest] public IEnumerator MembershipChangeWithSameLeaderCancelsOldCapture()
        {
            game.Controls.Select(Explorer());yield return Ready();var p=ButtonCenter(FindButton("ATTACK MOVE"));
            Down(p);yield return Ready();game.Controls.Select(Worker(),true);yield return Ready();Up(p);yield return Ready();
            Assert.AreEqual(Explorer().id,game.Controls.FirstSelected.id);Assert.AreEqual(2,game.Controls.Selected.Count);
            Assert.IsFalse(game.Controls.AttackMode);
        }
        [UnityTest] public IEnumerator GlobalHomeButtonCaptureSurvivesTownShortcut()
        {
            game.Controls.Select(Explorer());yield return Ready();var p=ButtonCenter(FindButton("HOME CITY"));
            Down(p);KeyEdge(Key.T);Up(p);yield return Ready();yield return Ready();
            Assert.AreEqual(Town().id,game.Controls.FirstSelected.id);Assert.IsTrue(game.Hud.ModalVisible);
            Assert.AreEqual(0,Events("Payment"));
        }
        [UnityTest] public IEnumerator PeriodicRefreshDoesNotCancelSameContextHeldButton()
        {
            game.Controls.Select(Town());yield return Ready();var p=ButtonCenter(FindButton("Settler"));
            Down(p);yield return Ready();yield return new WaitForSecondsRealtime(1.1f);Up(p);yield return Ready();
            Assert.AreEqual(1,Town().queue.Count);Assert.AreEqual(1,Events("Payment"));
        }
        [UnityTest] public IEnumerator ConsecutiveNativeTrainAndCancelClicksSettleExactlyTwice()
        {
            game.Controls.Select(Town());yield return Ready();var p=ButtonCenter(FindButton("Settler"));
            Down(p);Up(p);Down(p);Up(p);yield return Ready();yield return Ready();
            Assert.AreEqual(2,Town().queue.Count);Assert.AreEqual(2,Events("Payment"));
            var queue=session.GetComponentsInChildren<Button>().Where(b=>b.transform.parent.name=="Production queue").OrderBy(b=>b.transform.GetSiblingIndex()).ToArray();
            Assert.AreEqual(2,queue.Length);p=ButtonCenter(queue[0]);Down(p);Up(p);Down(p);Up(p);yield return Ready();yield return Ready();
            Assert.IsEmpty(Town().queue);Assert.AreEqual(2,Events("Refund"));
            Assert.AreEqual(300,game.World.Players[0].resources.food,.001f);
        }
        [UnityTest] public IEnumerator SelectionChangeCancelsHeldQueueRefund()
        {
            game.Controls.Select(Town());yield return Ready();yield return Click(ButtonCenter(FindButton("Settler")));yield return Ready();
            var queue=session.GetComponentsInChildren<Button>().Single(b=>b.transform.parent.name=="Production queue");
            var p=ButtonCenter(queue);Down(p);yield return Ready();KeyEdge(Key.V);Up(p);yield return Ready();yield return Ready();
            Assert.IsTrue(game.Controls.FirstSelected.Worker);Assert.AreEqual(1,Town().queue.Count);Assert.AreEqual(0,Events("Refund"));
        }
        [UnityTest] public IEnumerator HeldQueueCancelCannotCancelSuccessorAfterProductionCompletes()
        {
            var town=Town();game.Controls.Select(town);yield return Ready();
            var train=ButtonCenter(FindButton("Settler"));Down(train);Up(train);Down(train);Up(train);yield return Ready();yield return Ready();
            Assert.AreEqual(2,town.queue.Count);Assert.AreEqual(2,Events("Payment"));
            var first=town.queue[0];var successor=town.queue[1];first.elapsed=first.duration-.1f;
            var queue=session.GetComponentsInChildren<Button>().Where(b=>b.transform.parent.name=="Production queue").OrderBy(b=>b.transform.GetSiblingIndex()).ToArray();
            var p=ButtonCenter(queue[0]);Down(p);yield return Ready();
            int trained=game.World.Players[0].trained;game.World.Tick(.2f);yield return Ready();
            Assert.AreEqual(trained+1,game.World.Players[0].trained,"The held click must cross an actual production completion");
            Assert.AreEqual(1,town.queue.Count);Assert.AreSame(successor,town.queue[0]);
            var food=game.World.Players[0].resources.food;int reserved=game.World.Players[0].reservedPopulation;
            Up(p);yield return Ready();yield return Ready();
            Assert.AreEqual(1,town.queue.Count,"Releasing a completed order's button must leave its successor queued");
            Assert.AreSame(successor,town.queue[0]);Assert.AreEqual(0,Events("Refund"));
            Assert.AreEqual(food,game.World.Players[0].resources.food,.001f);Assert.AreEqual(reserved,game.World.Players[0].reservedPopulation);
        }
        [UnityTest] public IEnumerator HeldQueueCancelFollowsClickedJobWhenEarlierProductionCompletes()
        {
            var town=Town();game.Controls.Select(town);yield return Ready();
            var train=ButtonCenter(FindButton("Settler"));Down(train);Up(train);Down(train);Up(train);yield return Ready();yield return Ready();
            Assert.AreEqual(2,town.queue.Count);Assert.AreEqual(2,Events("Payment"));
            var first=town.queue[0];var clicked=town.queue[1];first.elapsed=first.duration-.1f;
            var queue=session.GetComponentsInChildren<Button>().Where(b=>b.transform.parent.name=="Production queue").OrderBy(b=>b.transform.GetSiblingIndex()).ToArray();
            var p=ButtonCenter(queue[1]);Down(p);yield return Ready();
            int trained=game.World.Players[0].trained;game.World.Tick(.2f);yield return Ready();
            Assert.AreEqual(trained+1,game.World.Players[0].trained,"The earlier order must complete while the later order's button is held");
            Assert.AreEqual(1,town.queue.Count);Assert.AreSame(clicked,town.queue[0]);
            var food=game.World.Players[0].resources.food;int reserved=game.World.Players[0].reservedPopulation;
            Up(p);yield return Ready();yield return Ready();
            Assert.IsEmpty(town.queue,"The originally clicked order must remain cancellable after moving to the front");
            Assert.AreEqual(1,Events("Refund"));Assert.AreEqual(food+clicked.paid.food,game.World.Players[0].resources.food,.001f);
            Assert.AreEqual(reserved-clicked.population,game.World.Players[0].reservedPopulation);
        }
    }
}
