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
    // The cases use real InputSystem edges and UGUI pointer dispatch; no
    // Button.onClick.Invoke shortcut is used.
    public sealed class SelectionActionTests : NativeInputFixture
    {
        private EntityState Pathfinder()=>game.World.Entities.First(e=>e.owner==0&&e.def.id=="explorer");
        private EntityState Town()=>game.World.Entities.First(e=>e.owner==0&&e.def.id=="town");
        private int PlayerPayments()=>game.World.Events.Count(e=>e.kind=="Payment"&&e.player==0);
        private void QueueKey(Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        }
        private void QueueClick(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
        }
        private Button Action(string prefix)=>session.GetComponentsInChildren<Button>().Single(b=>b.name.StartsWith(prefix));

        [UnityTest] public IEnumerator TBeforePointerDownSettlesNewActionsAndTrainsSettler()
        {
            game.Controls.Select(Pathfinder());yield return Ready();
            var stalePoint=ButtonCenter(Action("ATTACK MOVE"));
            var before=PlayerPayments();QueueKey(Key.T);QueueClick(stalePoint);yield return Ready();yield return Ready();
            Assert.AreEqual(Town().id,game.Controls.FirstSelected.id,"T must select the town center before the stale pointer is dispatched");
            Assert.IsFalse(game.Controls.AttackMode,"The former explorer action slot must not leave AttackMove active");
            Assert.IsFalse(game.Controls.GarrisonMode);
            Assert.IsEmpty(game.Controls.BuildingToPlace);
            Assert.AreEqual(1,Town().queue.Count,"The click after T must use the settled town action layout");
            Assert.AreEqual(before+1,PlayerPayments());
        }

        [UnityTest] public IEnumerator TownSelectionThenSettlerActionUsesRebuiltActionLayout()
        {
            game.Controls.Select(Pathfinder());yield return Ready();
            QueueKey(Key.T);yield return Ready();yield return Ready();
            Assert.AreEqual(Town().id,game.Controls.FirstSelected.id);
            var before=PlayerPayments();var train=Action("Settler");
            QueueClick(ButtonCenter(train));yield return Ready();yield return Ready();
            Assert.AreEqual(1,Town().queue.Count,"The current town action must train exactly one settler");
            Assert.AreEqual(before+1,PlayerPayments());
        }

        [UnityTest] public IEnumerator VContextShortcutDoesNotReuseAFormerActionSlot()
        {
            game.Controls.Select(Town());yield return Ready();
            var stalePoint=ButtonCenter(Action("Settler"));
            QueueKey(Key.V);QueueClick(stalePoint);yield return Ready();yield return Ready();
            Assert.IsTrue(game.Controls.FirstSelected.Worker,"V must select an idle settler");
            Assert.IsEmpty(Town().queue,"The former town action must not train a settler");
            Assert.AreEqual(0,PlayerPayments());
            Assert.IsTrue(game.Controls.AttackMode,"A fresh click after V must invoke the new settler's AttackMove button");
            Assert.IsFalse(game.Controls.GarrisonMode);Assert.IsEmpty(game.Controls.BuildingToPlace);
        }

        [UnityTest] public IEnumerator PointerDownBeforeTThenReleaseCancelsOldActionCapture()
        {
            game.Controls.Select(Pathfinder());yield return Ready();
            var stalePoint=ButtonCenter(Action("ATTACK MOVE"));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=stalePoint}.WithButton(MouseButton.Left));
            QueueKey(Key.T);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=stalePoint});
            yield return Ready();yield return Ready();
            Assert.AreEqual(Town().id,game.Controls.FirstSelected.id);
            Assert.IsFalse(game.Controls.AttackMode);Assert.IsFalse(game.Controls.GarrisonMode);Assert.IsEmpty(game.Controls.BuildingToPlace);
            Assert.IsEmpty(Town().queue,"Releasing an old-context pointer after T must not invoke the rebuilt action slot");
            Assert.AreEqual(0,PlayerPayments());
        }

        [UnityTest] public IEnumerator TBeforeHeldPointerSettlesWithoutWaitingForRelease()
        {
            game.Controls.Select(Pathfinder());yield return Ready();
            var point=ButtonCenter(Action("ATTACK MOVE"));QueueKey(Key.T);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));
            yield return Ready();yield return Ready();
            Assert.AreEqual(Town().id,game.Controls.FirstSelected.id);
            Assert.IsNotNull(Action("Settler"),"A physical held button must not prevent the new action layout from settling");
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Ready();yield return Ready();
            Assert.IsFalse(game.Controls.AttackMode);Assert.AreEqual(1,Town().queue.Count);Assert.AreEqual(1,PlayerPayments());
        }

        [UnityTest] public IEnumerator SameTownHeldPointerTrainsExactlyOnce()
        {
            game.Controls.Select(Town());yield return Ready();
            var point=ButtonCenter(Action("Settler"));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));
            yield return Ready();yield return Ready();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Ready();yield return Ready();
            Assert.AreEqual(1,Town().queue.Count);Assert.AreEqual(1,PlayerPayments());
            Assert.IsFalse(game.Controls.AttackMode);
        }
    }
}
