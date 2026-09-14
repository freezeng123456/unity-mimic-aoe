using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class ResourceContextTests : NativeInputFixture
    {
        private EntityState berry;
        private IEnumerator PrepareResource()
        {
            berry=SpawnHere("berry",-1,site+new Vector2(8,4));
            game.World.Fog.Reveal(0,berry.position,12);
            yield return Ready();
            AssertWorldPoint(ScreenPoint(berry.position));
        }

        [UnityTest] public IEnumerator PureWorkerRightResourceGathers()
        {
            yield return PrepareResource();
            var worker=SpawnHere("villager",0,site+Vector2.left*5);game.Controls.Select(worker);
            yield return Click(ScreenPoint(berry.position),MouseButton.Right);
            Assert.AreEqual(OrderKind.Gather,worker.orders.Single().kind);
            Assert.AreEqual(berry.id,worker.orders.Single().target);
        }

        [UnityTest] public IEnumerator NonWorkerRightResourceMovesWithNoTarget()
        {
            yield return PrepareResource();
            var soldier=SpawnHere("musketeer",0,site+Vector2.left*5);game.Controls.Select(soldier);
            yield return Click(ScreenPoint(berry.position),MouseButton.Right);
            Assert.AreEqual(OrderKind.Move,soldier.orders.Single().kind);
            Assert.AreEqual(0,soldier.orders.Single().target);
        }

        [UnityTest] public IEnumerator MixedSelectionSplitsResourceContextAndPreservesShiftPerEvent()
        {
            yield return PrepareResource();
            var worker=SpawnHere("villager",0,site+Vector2.left*5);
            var soldier=SpawnHere("musketeer",0,site+Vector2.left*7);
            game.Controls.Select(worker);game.Controls.Select(soldier,true);
            var prior=site+new Vector2(3,-8);
            Assert.IsTrue(game.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id,soldier.id},order=OrderKind.Move,point=prior}).accepted);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));
            yield return Click(ScreenPoint(berry.position),MouseButton.Right);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();
            Assert.AreEqual(new[]{OrderKind.Move,OrderKind.Gather},worker.orders.Select(o=>o.kind).ToArray());Assert.AreEqual(berry.id,worker.orders.Last().target);
            Assert.AreEqual(new[]{OrderKind.Move,OrderKind.Move},soldier.orders.Select(o=>o.kind).ToArray());Assert.AreEqual(0,soldier.orders.Last().target);
        }

        [UnityTest] public IEnumerator TownRightResourceUsesRallyAndDoesNotIssueWorkerGather()
        {
            yield return PrepareResource();
            var town=SpawnHere("town",0,site+Vector2.left*3);game.Controls.Select(town);
            yield return Click(ScreenPoint(berry.position),MouseButton.Right);
            Assert.That(Vector2.Distance(town.rally,berry.position),Is.LessThan(.75f));
            Assert.IsFalse(game.World.Events.Any(e=>e.kind=="Gather"&&e.entity==town.id),"Town resource right-click must not issue a worker gather order");
            Assert.IsTrue(game.World.Events.Any(e=>e.kind=="RallySet"&&e.entity==town.id&&e.target==berry.id),"Resource rally target must survive the UI-to-World command path");
        }

        [UnityTest] public IEnumerator NonExplorerRightTreasureMovesInsteadOfClaiming()
        {
            var treasure=SpawnHere("treasure_xp",-1,site+new Vector2(8,4));game.World.Fog.Reveal(0,treasure.position,12);
            var soldier=SpawnHere("musketeer",0,site+Vector2.left*5);game.Controls.Select(soldier);
            yield return Click(ScreenPoint(treasure.position),MouseButton.Right);
            Assert.AreEqual(OrderKind.Move,soldier.orders.Single().kind);Assert.AreEqual(0,soldier.orders.Single().target);
        }
    }
}
