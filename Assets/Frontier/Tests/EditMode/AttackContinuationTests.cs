using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class AttackContinuationTests
    {
        World w;
        [SetUp]public void SetUp()
        {
            w=new World(Catalog.Load(),5201,"coast","coast",5);
            foreach(var e in w.Entities.Where(World.Guardian).ToArray())w.Remove(e);
        }
        EntityState Spawn(string id,int owner,Vector2 point)
        {var e=w.Spawn(id,owner,point);w.Fog.Tick(w.Entities);Order(e,OrderKind.Hold);return e;}
        void Order(EntityState e,OrderKind kind,EntityState target=null,Vector2 point=default,bool append=false)
        {var r=w.Issue(new GameCommand{player=e.owner,kind=CommandKind.Order,actors=new[]{e.id},order=kind,target=target?.id??0,point=target?.position??point,append=append});Assert.IsTrue(r.accepted,r.reason);}
        void Steps(int n){for(int i=0;i<n;i++)w.Tick(.1f);}
        int Fires(EntityState e,EntityState target)=>w.Events.Count(x=>x.kind=="Fire"&&x.entity==e.id&&x.target==target.id);

        [Test]public void HeldCannonIgnoresDeadAngleEnemyWhenAnotherTargetIsShootable()
        {
            var cannon=Spawn("cannon",0,new Vector2(40,40));var close=Spawn("pike",1,new Vector2(43,40));var far=Spawn("pike",1,new Vector2(50,40));
            Assert.Less(w.EdgeDistance(cannon,close),cannon.def.minRange);Assert.Greater(w.EdgeDistance(cannon,far),cannon.def.minRange);
            Steps(60);Assert.IsTrue(cannon.Alive&&close.Alive);Assert.AreEqual(new Vector2(40,40),cannon.position);
            Assert.Greater(Fires(cannon,far),0,"A held cannon must select an eligible target instead of remaining locked on the dead angle");
            Assert.Less(far.hp,far.def.hp);Assert.AreEqual(0,Fires(cannon,close));
        }
        [Test]public void HeldCannonReleasesRememberedTargetThatMovesInsideDeadAngle()
        {
            var cannon=Spawn("cannon",0,new Vector2(40,40));var close=Spawn("pike",1,new Vector2(48,40));var far=Spawn("pike",1,new Vector2(55,40));
            Steps(1);Assert.AreEqual(close.id,cannon.attackTarget);Order(close,OrderKind.Move,point:new Vector2(43,40));Steps(15);
            Assert.Less(w.EdgeDistance(cannon,close),cannon.def.minRange);Assert.IsTrue(w.IsVisible(0,close));
            Steps(60);Assert.IsTrue(cannon.Alive);Assert.AreEqual(new Vector2(40,40),cannon.position);
            Assert.Greater(Fires(cannon,far),0,"Remembered invalid targets must obey the same eligibility as fresh acquisition");
        }
        [Test]public void HeldMusketeerReleasesRememberedTargetOutsideRangeButStillVisible()
        {
            var gun=Spawn("musketeer",0,new Vector2(40,40));var first=Spawn("pike",1,new Vector2(48,40));var alternate=Spawn("pike",1,new Vector2(49,45));
            Steps(1);Assert.AreEqual(first.id,gun.attackTarget);Order(first,OrderKind.Move,point:new Vector2(56,40));
            for(int i=0;i<100&&first.orders.Count>0;i++)Steps(1);Order(first,OrderKind.Hold);
            Assert.Greater(w.EdgeDistance(gun,first),gun.def.range);Assert.IsTrue(w.IsVisible(0,first));Steps(50);
            Assert.AreEqual(new Vector2(40,40),gun.position);Assert.Greater(Fires(gun,alternate),0);
        }
        [Test]public void RepeatingIdenticalDirectAttackPreservesPreparationAndNormalShotSpacing()
        {
            var gun=Spawn("musketeer",0,new Vector2(40,40));var target=Spawn("pike",1,new Vector2(48,40));Order(gun,OrderKind.Attack,target);
            for(int i=0;i<80;i++){Order(gun,OrderKind.Attack,target);Steps(1);}
            var shots=w.Events.Where(e=>e.kind=="Fire"&&e.entity==gun.id).ToArray();Assert.GreaterOrEqual(shots.Length,2,"Repeated confirmation of the same attack must not keep cancelling its preparation");
            for(int i=1;i<shots.Length;i++)Assert.GreaterOrEqual(shots[i].time-shots[i-1].time,gun.def.period-.001f,"Repeated commands do not bypass the weapon period");
            Assert.Less(target.hp,target.def.hp);
        }
    }
}
