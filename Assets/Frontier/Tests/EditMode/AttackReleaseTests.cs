using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Fixed-step combat contract for deployed artillery.  The rule catalog is
    // cloned so these tests use the shipped cannon definition without mutating
    // the shared asset or relaxing deployment/range/windup values.
    public sealed class AttackReleaseTests
    {
        private World world; private EntityState cannon, target;

        [SetUp] public void SetUp()
        {
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            world=new World(rules,7331,"coast","river",5);
            foreach(var e in world.Entities.Where(World.Guardian).ToArray())world.Remove(e);
            cannon=world.Spawn("cannon",0,new Vector2(40,40));
            target=world.Spawn("cannon",1,new Vector2(48,40));target.hp=10000;
            Assert.IsTrue(world.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{target.id},order=OrderKind.Hold}).accepted);
            world.Fog.Tick(world.Entities);
        }
        private void Steps(int n){for(int i=0;i<n;i++)world.Tick(MatchSimulation.StepSeconds);}
        private GameCommand Attack()=>new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{cannon.id},order=OrderKind.Attack,target=target.id,point=target.position};
        private void Prepare()
        {
            Assert.IsTrue(world.Issue(Attack()).accepted);
            for(int i=0;i<100&&cannon.fireAt<0;i++)Steps(1);
            Assert.IsTrue(cannon.isDeployed,"Fixture must reach real deployment");
            Assert.Greater(cannon.fireAt,world.Time,"Fixture must stop during attack preparation");
        }
        private int Fires()=>world.Events.Count(e=>e.kind=="Fire"&&e.entity==cannon.id&&e.target==target.id);

        [Test] public void TargetEnteringMinimumRangeCancelsPreparationAndPreservesDeadline()
        {
            Prepare();float deadline=cannon.attackReadyAt;
            target.position=new Vector2(42,40);world.Fog.Tick(world.Entities);Steps(5);
            Assert.AreEqual(0,Fires());Assert.AreEqual(deadline,cannon.attackReadyAt,.001f);
            target.position=new Vector2(48,40);world.Fog.Tick(world.Entities);Steps(1);
            while(world.Time+MatchSimulation.StepSeconds<deadline){Steps(1);Assert.AreEqual(0,Fires(),"Cancelled shots must retain the original reload deadline");}
            for(int i=0;i<40&&Fires()==0;i++)Steps(1);
            Assert.AreEqual(1,Fires());
            Assert.GreaterOrEqual(world.Events.First(e=>e.kind=="Fire"&&e.entity==cannon.id).time,deadline);
        }

        [Test] public void ExactMinimumEdgeDistanceRemainsLegal()
        {
            target.position=new Vector2(40+cannon.def.minRange+cannon.def.radius+target.def.radius,40);world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(Attack()).accepted);Steps(30);
            Assert.AreEqual(1,Fires(),"The minimum range boundary is inclusive");
        }

        [Test] public void AlreadyFiredProjectileStillHitsAfterTargetEntersDeadAngle()
        {
            Prepare();for(int i=0;i<20&&Fires()==0;i++)Steps(1);
            Assert.IsTrue(world.Projectiles.Any(p=>p.source==cannon.id&&p.target==target.id));
            target.position=new Vector2(42,40);world.Fog.Tick(world.Entities);float hp=target.hp;Steps(30);
            Assert.Less(target.hp,hp);Assert.AreEqual(1,world.Events.Count(e=>e.kind=="Impact"&&e.target==target.id));
        }

        [Test] public void TargetDeathBeforeFireCancelsShotWithoutResettingDeadline()
        {
            Prepare();float deadline=cannon.attackReadyAt;
            world.Remove(target);Steps(6);
            Assert.AreEqual(0,Fires());Assert.AreEqual(deadline,cannon.attackReadyAt,.001f);Assert.IsFalse(world.Projectiles.Any(p=>p.source==cannon.id));
        }
    }
}
