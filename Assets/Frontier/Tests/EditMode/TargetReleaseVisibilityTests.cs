using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class TargetReleaseVisibilityTests
    {
        private World world; private EntityState cannon, target;
        [SetUp] public void SetUp()
        {
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            world=new World(rules,7332,"coast","river",5);
            foreach(var e in world.Entities.Where(World.Guardian).ToArray())world.Remove(e);
            cannon=world.Spawn("cannon",0,new Vector2(40,40));
            target=world.Spawn("cannon",1,new Vector2(48,40));target.hp=10000;
            Assert.IsTrue(world.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{target.id},order=OrderKind.Hold}).accepted);
            world.Fog.Tick(world.Entities);
        }
        private void Steps(int n){for(int i=0;i<n;i++)world.Tick(MatchSimulation.StepSeconds);}
        private void Prepare()
        {
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{cannon.id},order=OrderKind.Attack,target=target.id,point=target.position}).accepted);
            for(int i=0;i<100&&cannon.fireAt<0;i++)Steps(1);
            Assert.IsTrue(cannon.isDeployed);Assert.Greater(cannon.fireAt,world.Time);
        }
        private int Fires()=>world.Events.Count(e=>e.kind=="Fire"&&e.entity==cannon.id&&e.target==target.id);

        [Test] public void PreparedShotIsCancelledWhenTargetLeavesVision()
        {
            Prepare();float deadline=cannon.attackReadyAt;
            target.position=new Vector2(100,100);world.Fog.Tick(world.Entities);
            Assert.IsFalse(world.IsVisible(0,target));Steps(8);
            Assert.AreEqual(0,Fires());Assert.IsFalse(world.Projectiles.Any(p=>p.source==cannon.id));Assert.AreEqual(deadline,cannon.attackReadyAt,.001f);
        }

        [Test] public void PreparedShotIsCancelledWhenTargetGarrisonsThroughRealCommand()
        {
            var town=world.Spawn("town",1,new Vector2(50,40));world.Fog.Tick(world.Entities);Prepare();float deadline=cannon.attackReadyAt;
            Assert.IsTrue(world.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{target.id},order=OrderKind.Garrison,target=town.id,point=town.position}).accepted);
            for(int i=0;i<100&&target.garrisonedIn==0;i++)Steps(1);
            Assert.AreEqual(town.id,target.garrisonedIn,"Target must enter the enemy TC through the real garrison order");Steps(8);
            Assert.AreEqual(0,Fires());Assert.IsFalse(world.Projectiles.Any(p=>p.source==cannon.id));Assert.AreEqual(deadline,cannon.attackReadyAt,.001f);
        }
    }
}
