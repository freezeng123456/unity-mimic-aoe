using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class AttackLifecycleTests
    {
        World w;
        [SetUp]public void SetUp(){w=new World(Catalog.Load(),5202,"coast","coast",5);foreach(var e in w.Entities.Where(World.Guardian).ToArray())w.Remove(e);}
        EntityState Spawn(string id,int owner,Vector2 point){var e=w.Spawn(id,owner,point);w.Fog.Tick(w.Entities);Order(e,OrderKind.Hold);return e;}
        void Order(EntityState e,OrderKind kind,EntityState target=null,Vector2 point=default,bool append=false)
        {var r=w.Issue(new GameCommand{player=e.owner,kind=CommandKind.Order,actors=new[]{e.id},order=kind,target=target?.id??0,point=target?.position??point,append=append});Assert.IsTrue(r.accepted,r.reason);}
        void Steps(int n){for(int i=0;i<n;i++)w.Tick(.1f);}

        [Test]public void AttackMoveKillsTwoEncounteredTargetsThenRunsNextWaypoint()
        {
            var gun=Spawn("musketeer",0,new Vector2(40,30));var first=Spawn("pike",1,new Vector2(48,30));var second=Spawn("pike",1,new Vector2(60,30));
            var waypoint=new Vector2(70,30);var last=new Vector2(70,38);
            Order(gun,OrderKind.AttackMove,point:waypoint);var attackMove=gun.orders.Peek();Order(gun,OrderKind.Move,point:last,append:true);
            bool visited=false;
            for(int i=0;i<900;i++){w.Tick(.1f);if(Vector2.Distance(gun.position,waypoint)<2)visited=true;}
            Assert.IsFalse(first.Alive||second.Alive);Assert.IsTrue(gun.Alive);Assert.IsTrue(visited);
            foreach(var target in new[]{first,second})Assert.IsTrue(w.Events.Any(e=>e.kind=="Damage"&&e.entity==gun.id&&e.target==target.id));
            Assert.AreEqual(waypoint,attackMove.point);Assert.AreEqual(0,gun.orders.Count);Assert.Less(Vector2.Distance(gun.position,last),.5f);
        }
        [Test]public void ChangingDirectTargetCancelsPreparedShotButRetainsReload()
        {
            var gun=Spawn("musketeer",0,new Vector2(40,40));var first=Spawn("pike",1,new Vector2(48,40));var second=Spawn("pike",1,new Vector2(48,44));
            Order(gun,OrderKind.Attack,first);Steps(1);Assert.Greater(gun.fireAt,w.Time);float deadline=gun.attackReadyAt;
            Order(gun,OrderKind.Attack,second);Assert.AreEqual(-1,gun.fireAt);Assert.AreEqual(deadline,gun.attackReadyAt);
            while(w.Time+.1f<deadline)Steps(1);Assert.IsFalse(w.Events.Any(e=>e.kind=="Fire"&&e.entity==gun.id));
            Steps(8);Assert.IsTrue(w.Events.Any(e=>e.kind=="Fire"&&e.entity==gun.id&&e.target==second.id));
            Assert.IsFalse(w.Events.Any(e=>e.kind=="Fire"&&e.entity==gun.id&&e.target==first.id));
        }
        [Test]public void ExplicitAttackStillPursuesOutOfRangeTarget()
        {
            var gun=Spawn("musketeer",0,new Vector2(40,30));var target=Spawn("pike",1,new Vector2(56,30));
            Assert.Greater(w.EdgeDistance(gun,target),gun.def.range);Order(gun,OrderKind.Attack,target);Steps(60);
            Assert.Greater(gun.position.x,41);Assert.IsTrue(w.Events.Any(e=>e.kind=="Fire"&&e.entity==gun.id&&e.target==target.id));
        }
        [Test]public void HeldCannonFiresAfterOnlyDeadAngleDistractionIsRemoved()
        {
            var gun=Spawn("cannon",0,new Vector2(40,40));var close=Spawn("pike",1,new Vector2(43,40));var far=Spawn("pike",1,new Vector2(50,40));
            Steps(60);w.Remove(close);Steps(60);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Fire"&&e.entity==gun.id&&e.target==far.id));
        }
        [Test]public void GunFiresAfterRepeatedAttackCommandsCease()
        {
            var gun=Spawn("musketeer",0,new Vector2(40,40));var target=Spawn("pike",1,new Vector2(48,40));
            for(int i=0;i<80;i++){Order(gun,OrderKind.Attack,target);Steps(1);}int before=w.Events.Count(e=>e.kind=="Fire"&&e.entity==gun.id);
            Steps(40);Assert.Greater(w.Events.Count(e=>e.kind=="Fire"&&e.entity==gun.id),before);
        }
    }
}
