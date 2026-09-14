using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class AttackInputContinuationTests : NativeInputFixture
    {
        void Isolate(){foreach(var e in game.World.Entities.Where(World.Guardian).ToArray())game.World.Remove(e);}
        EntityState Enemy(Vector2 point)
        {
            var e=SpawnHere("pike",1,point);
            Assert.IsTrue(game.World.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{e.id},order=OrderKind.Hold}).accepted);return e;
        }
        IEnumerator SelectByClick(EntityState unit)
        {
            var point=ScreenPoint(unit.position,1);AssertWorldPoint(point);yield return Click(point);
            Assert.AreEqual(unit.id,game.Controls.FirstSelected?.id);
        }
        [UnityTest]public IEnumerator RepeatedRightClickPreservesShotWhileReplacingOldShiftQueue()
        {
            Isolate();var w=game.World;var gun=SpawnHere("musketeer",0,new Vector2(72,25));var target=Enemy(new Vector2(80,25));w.Fog.Tick(w.Entities);
            yield return SelectByClick(gun);var point=ScreenPoint(target.position,1);AssertWorldPoint(point);yield return Click(point,MouseButton.Right);
            Assert.AreEqual(OrderKind.Attack,gun.orders.Single().kind);Assert.AreEqual(target.id,gun.orders.Peek().target);
            w.Tick(.1f);float fire=gun.fireAt,deadline=gun.attackReadyAt;Assert.Greater(fire,w.Time);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));yield return Ready();
            var ground=ScreenPoint(new Vector2(72,34));AssertWorldPoint(ground);yield return Click(ground,MouseButton.Right);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();
            Assert.AreEqual(2,gun.orders.Count);Assert.AreEqual(OrderKind.Move,gun.orders.Last().kind);Assert.AreEqual(fire,gun.fireAt);
            yield return Click(point,MouseButton.Right);
            Assert.AreEqual(1,gun.orders.Count,"Nonqueued repeated attack replaces the previous Shift tail");
            Assert.AreEqual(fire,gun.fireAt);Assert.AreEqual(deadline,gun.attackReadyAt);
            for(int i=0;i<12;i++){yield return Click(point,MouseButton.Right);w.Tick(.1f);}
            Assert.AreEqual(1,w.Events.Count(e=>e.kind=="Fire"&&e.entity==gun.id));Assert.Less(target.hp,target.def.hp);
            for(int i=0;i<60;i++)w.Tick(.1f);
            var shots=w.Events.Where(e=>e.kind=="Fire"&&e.entity==gun.id).ToArray();Assert.GreaterOrEqual(shots.Length,2);
            for(int i=1;i<shots.Length;i++)Assert.GreaterOrEqual(shots[i].time-shots[i-1].time,gun.def.period-.001f);
        }
        [UnityTest]public IEnumerator JHoldKeepsCannonStillAndFiresPastDeadAngleDistraction()
        {
            Isolate();var w=game.World;var cannon=SpawnHere("cannon",0,new Vector2(70,25));var close=Enemy(new Vector2(73,25));var far=Enemy(new Vector2(80,25));w.Fog.Tick(w.Entities);
            yield return SelectByClick(cannon);yield return Press(Key.J);Assert.IsTrue(cannon.hold);
            for(int i=0;i<60;i++)w.Tick(.1f);
            Assert.AreEqual(new Vector2(70,25),cannon.position);Assert.IsTrue(cannon.isDeployed);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Fire"&&e.entity==cannon.id&&e.target==far.id));Assert.Less(far.hp,far.def.hp);
            Assert.IsFalse(w.Events.Any(e=>e.kind=="Fire"&&e.entity==cannon.id&&e.target==close.id));
        }
    }
}
