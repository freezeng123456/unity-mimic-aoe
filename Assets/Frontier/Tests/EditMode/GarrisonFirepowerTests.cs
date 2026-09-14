using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Project contract, based on a secondary reference pending selected DE patch verification.
    public sealed class GarrisonFirepowerTests
    {
        private World world;
        private EntityState town, target;
        [SetUp] public void SetUp()
        {
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            world=new World(rules,42,"river","coast",3);
            foreach(var e in world.Entities.Where(e=>(e.def.tags&Tags.Guardian)!=0).ToArray())world.Remove(e);
            town=world.Spawn("town",0,new Vector2(60,25));
            target=world.Spawn("musketeer",1,new Vector2(72,25));target.hp=10000;
            Assert.IsTrue(world.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{target.id},order=OrderKind.Hold}).accepted);
            world.Fog.Tick(world.Entities);
        }
        private EntityState AddOccupant(string unit="villager",int owner=0)
        {
            var e=world.Spawn(unit,owner,town.position+Vector2.up*5);
            e.garrisonedIn=town.id;town.occupants.Add(e.id);return e;
        }
        private void Steps(int count){for(int i=0;i<count;i++)world.Tick(.1f);}
        private int Fires()=>world.Events.Count(e=>e.kind=="Fire"&&e.entity==town.id);
        private ProjectileState AwaitShot()
        {
            for(int i=0;i<60;i++)
            {world.Tick(.1f);var shot=world.Projectiles.FirstOrDefault(p=>p.source==town.id);if(shot!=null)return shot;}
            Assert.Fail("Expected town center to acquire target and emit a real projectile");return null;
        }
        [Test] public void EmptyTownDoesNotPrepareOrFireLandAttacks()
        {
            Assert.AreEqual(0,world.Attack(town));Steps(40);
            Assert.IsFalse(world.Events.Any(e=>e.entity==town.id&&(e.kind=="Fire"||e.kind=="AttackPrepared")));
        }
        [TestCase(1,9)] [TestCase(8,72)] [TestCase(10,90)] [TestCase(11,90)]
        public void FirepowerAndActualProjectileUseEligibleVillagersWithCap(int count,float expected)
        {
            for(int i=0;i<count;i++)AddOccupant();
            Assert.AreEqual(expected,world.Attack(town),.001f);
            Assert.AreEqual(expected,AwaitShot().damage,.001f);
        }
        [Test] public void ForeignDeadNonWorkerAndBrokenLinksDoNotContribute()
        {
            AddOccupant("musketeer");AddOccupant("villager",1);
            AddOccupant().garrisonedIn=0;AddOccupant().hp=0;town.occupants.Add(999999);
            Assert.AreEqual(0,world.Attack(town));Steps(35);Assert.AreEqual(0,Fires());
        }
        [Test] public void LeavingBeforeFireCancelsShotWithoutResettingCooldown()
        {
            AddOccupant();
            for(int i=0;i<40&&town.fireAt<0;i++)world.Tick(.1f);
            Assert.GreaterOrEqual(town.fireAt,0);float ready=town.attackReadyAt;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Ungarrison,actors=new[]{town.id}}).accepted);
            Steps(5);Assert.AreEqual(0,Fires());Assert.AreEqual(ready,town.attackReadyAt);
        }
        [Test] public void RealExitAndReentryDoNotBypassCooldownAndInflightDamageIsFixed()
        {
            var worker=world.Spawn("villager",0,town.position+Vector2.up*4);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Garrison,target=town.id}).accepted);
            var shot=AwaitShot();Assert.AreEqual(town.id,worker.garrisonedIn);
            Assert.AreEqual(9,shot.damage,.001f);float ready=town.attackReadyAt;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Ungarrison,actors=new[]{town.id}}).accepted);
            Assert.AreEqual(0,world.Attack(town));Assert.AreEqual(ready,town.attackReadyAt);
            // The projectile has already left the building. Its damage survives the exit.
            for(int i=0;i<20&&!world.Events.Any(e=>e.kind=="Damage"&&e.entity==town.id&&e.target==target.id);i++)world.Tick(.1f);
            var damage=world.Events.First(e=>e.kind=="Damage"&&e.entity==town.id&&e.target==target.id);
            Assert.AreEqual(9*(1-target.def.rangedResist),damage.amount,.001f);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Garrison,target=town.id}).accepted);
            while(world.Time+.11f<ready)world.Tick(.1f);
            Assert.AreEqual(1,Fires(),"Reentry cannot grant an immediate extra shot");
            Assert.AreEqual(ready,town.attackReadyAt);
        }
    }
}
