using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class DynamicDestinationInputTests : NativeInputFixture
    {
        [UnityTest] public IEnumerator ShiftRightClickQueueSurvivesBuildingAtFirstDestination()
        {
            var w=game.World;
            foreach(var e in w.Entities.Where(World.Guardian).ToArray())w.Remove(e);
            w.Players[0].resources=new Cost(1000,2000,1000);
            var first=new Vector2(52,60);var last=new Vector2(48,80);
            var unit=SpawnHere("pike",0,new Vector2(36,60));
            var builder=SpawnHere("villager",0,first+Vector2.up*3);
            w.Fog.Tick(w.Entities);game.Controls.Select(unit);
            game.Controls.Focus(first);yield return Ready();
            AssertWorldPoint(ScreenPoint(first));yield return Click(ScreenPoint(first),MouseButton.Right);
            Assert.AreEqual(1,unit.orders.Count);Assert.AreEqual(OrderKind.Move,unit.orders.Peek().kind);
            var originalOrder=unit.orders.Peek();var requested=originalOrder.point;
            Assert.Less(Vector2.Distance(requested,first),.1f);
            game.Controls.Focus(last);yield return Ready();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));yield return Ready();
            AssertWorldPoint(ScreenPoint(last));yield return Click(ScreenPoint(last),MouseButton.Right);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();
            Assert.AreEqual(2,unit.orders.Count,"Shift must append through the production input controller");
            Assert.AreSame(originalOrder,unit.orders.Peek());
            var second=unit.orders.Last();Assert.Less(Vector2.Distance(second.point,last),.1f);
            float wood=w.Players[0].resources.wood;
            var result=w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{builder.id},item="house",point=first});
            Assert.IsTrue(result.accepted,result.reason);var house=w.Get(builder.orders.Peek().target);
            Assert.AreEqual(wood-house.def.cost.wood,w.Players[0].resources.wood,.001f);
            Assert.IsFalse(w.Navigation.IsWalkable(requested,unit.def.radius));
            bool approached=false,ranSecond=false;
            for(int i=0;i<900;i++)
            {
                var previous=unit.position;w.Tick(.1f);
                Assert.IsTrue(w.Navigation.IsWalkable(unit.position,unit.def.radius));
                Assert.LessOrEqual(Vector2.Distance(previous,unit.position),w.Speed(unit)*.1f+.001f,"Resolution cannot teleport the unit");
                Assert.AreEqual(requested,originalOrder.point,"The original click is retained as intent");
                if(unit.orders.Contains(originalOrder)&&Vector2.Distance(unit.position,first)<5)approached=true;
                if(unit.orders.Count>0&&unit.orders.Peek()==second)ranSecond=true;
            }
            Assert.IsTrue(originalOrder.hasResolvedPoint);Assert.IsTrue(approached);Assert.IsTrue(ranSecond);
            Assert.AreEqual(0,unit.orders.Count);Assert.Less(Vector2.Distance(unit.position,second.point),.5f);
            Assert.IsTrue(house.Alive);
        }
    }
}
