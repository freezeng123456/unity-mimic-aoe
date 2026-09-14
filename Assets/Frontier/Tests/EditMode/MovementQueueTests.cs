using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class MovementQueueTests
    {
        World w;EntityState gun;
        [SetUp]public void Setup()
        {
            w=new World(Catalog.Load(),5302,"coast","coast",5);foreach(var e in w.Entities.Where(World.Guardian).ToArray())w.Remove(e);
            gun=w.Spawn("musketeer",0,new Vector2(40,30));
        }
        void Order(OrderKind kind,Vector2 point,bool append=false)
        {var r=w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{gun.id},order=kind,point=point,append=append});Assert.IsTrue(r.accepted,r.reason);}
        void Steps(int n){for(int i=0;i<n;i++)w.Tick(.1f);}
        [Test]public void SameAttackMoveKeepsPreparingShotAndReplacesShiftTail()
        {
            var enemy=w.Spawn("pike",1,new Vector2(48,30));w.Fog.Tick(w.Entities);
            Assert.IsTrue(w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{enemy.id},order=OrderKind.Hold}).accepted);
            var first=new Vector2(60,30);Order(OrderKind.AttackMove,first);Steps(1);float fire=gun.fireAt,deadline=gun.attackReadyAt;Assert.Greater(fire,w.Time);
            Order(OrderKind.Move,new Vector2(60,40),true);Assert.AreEqual(2,gun.orders.Count);
            Order(OrderKind.AttackMove,first);Assert.AreEqual(1,gun.orders.Count);Assert.AreEqual(deadline,gun.attackReadyAt);
            Assert.AreEqual(fire,gun.fireAt,"Reconfirming the same attack-move intent must not cancel the current preparation");
            Steps(7);Assert.AreEqual(1,w.Events.Count(e=>e.kind=="Fire"&&e.entity==gun.id));Assert.Less(enemy.hp,enemy.def.hp);
        }
        [Test]public void ReconfirmingMoveReplacesTheOldShiftTail()
        {
            var first=new Vector2(58,30);Order(OrderKind.Move,first);Order(OrderKind.Move,new Vector2(58,40),true);Steps(1);
            Order(OrderKind.Move,first);Assert.AreEqual(1,gun.orders.Count);Steps(100);
            Assert.AreEqual(0,gun.orders.Count);Assert.Less(Vector2.Distance(first,gun.position),.3f);
        }
        [Test]public void AppendingMovePreservesCurrentTravelAndReachesBothWaypoints()
        {
            var first=new Vector2(58,30);var last=new Vector2(58,38);Order(OrderKind.Move,first);Steps(10);Order(OrderKind.Move,last,true);
            bool visited=false;for(int i=0;i<100;i++){Steps(1);if(Vector2.Distance(gun.position,first)<.5f)visited=true;}
            Assert.IsTrue(visited);Assert.AreEqual(0,gun.orders.Count);Assert.Less(Vector2.Distance(last,gun.position),.3f);
        }
        [Test]public void RepeatedUnreachableMoveWaitsForWallRemoval()
        {
            for(int y=0;y<128;y++)w.Navigation.SetBlocked(new Vector2(60,y),.45f,true);
            var goal=new Vector2(80,30);
            for(int i=0;i<50;i++){Order(OrderKind.Move,goal);Steps(1);Assert.Less(gun.position.x,60);Assert.AreEqual(1,gun.orders.Count);}
            for(int y=0;y<128;y++)w.Navigation.SetBlocked(new Vector2(60,y),.45f,false);
            Steps(500);Assert.AreEqual(0,gun.orders.Count);Assert.Less(Vector2.Distance(goal,gun.position),.3f);
        }
    }
}
