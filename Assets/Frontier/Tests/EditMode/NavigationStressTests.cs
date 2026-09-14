using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Frontier.Tests
{
    public sealed class NavigationStressTests
    {
        private World Arena()
        {
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            foreach(var d in rules.entities){d.damage=0;d.meleeDamage=0;d.siegeDamage=0;}
            var world=new World(rules);
            foreach(var e in world.Entities.ToArray())if(e.def.id!="town")world.Remove(e);
            foreach(var obstacle in world.TerrainObstacles)world.Navigation.SetBlocked(new Vector2(obstacle.x,obstacle.y),obstacle.z,false);
            for(int y=0;y<128;y++)if(y<57||y>71)world.Navigation.SetBlocked(new Vector2(64,y),.45f,true);
            return world;
        }
        private List<EntityState> SpawnGroup(World w,int count,Vector2 center)
        {
            var kinds=new[]{"pike","musketeer","hussar","dragoon","cannon"};
            var slots=w.Navigation.FormationSlots(center,count,2.8f);Assert.AreEqual(count,slots.Count);
            var units=new List<EntityState>();for(int i=0;i<count;i++)units.Add(w.Spawn(kinds[i%5],0,slots[i]));
            for(int i=0;i<units.Count;i++)for(int j=i+1;j<units.Count;j++)Assert.Greater(Vector2.Distance(units[i].position,units[j].position),units[i].def.radius+units[j].def.radius,"Fixture starts without overlap");
            return units;
        }
        private Dictionary<int,Vector2> Move(World w,List<EntityState> units,Vector2 point)
        {
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=units.Select(e=>e.id).ToArray(),order=OrderKind.Move,point=point}).accepted);
            var result=units.ToDictionary(e=>e.id,e=>e.orders.Peek().point);Assert.AreEqual(units.Count,result.Values.Distinct().Count());return result;
        }
        [TestCase(100)][TestCase(200)][TestCase(400)]
        public void MixedUnitsCrossOnlyGateAndReachEveryAssignedGoal(int count)
        {
            var w=Arena();var units=SpawnGroup(w,count,new Vector2(28,64));var goals=Move(w,units,new Vector2(99,64));
            Verify(w,units,goals,240);
        }
        [Test]
        public void OneHundredFriendlyMixedUnitsPassInOppositeDirections()
        {
            var w=Arena();var left=SpawnGroup(w,50,new Vector2(28,64));var right=SpawnGroup(w,50,new Vector2(99,64));
            var goals=Move(w,left,new Vector2(99,64));foreach(var pair in Move(w,right,new Vector2(28,64)))goals.Add(pair.Key,pair.Value);
            left.AddRange(right);Verify(w,left,goals,240);
        }
        private void Verify(World w,List<EntityState> units,Dictionary<int,Vector2> goals,int seconds)
        {
            for(int step=0;step<seconds*10;step++)
            {
                w.Tick(.1f);
                foreach(var e in units)Assert.IsTrue(w.Navigation.IsWalkable(e.position,e.def.radius),$"{e.id}/{e.def.id} penetrated an obstacle at tick {step}: {e.position}");
                if(units.All(e=>Vector2.Distance(e.position,goals[e.id])<1))break;
            }
            var missed=units.Where(e=>Vector2.Distance(e.position,goals[e.id])>=1).ToList();
            Assert.AreEqual(0,missed.Count,$"{missed.Count}/{units.Count} did not arrive: "+string.Join("; ",missed.Take(12).Select(e=>$"{e.id} {e.def.id} {e.position} -> {goals[e.id]} path {e.pathIndex}/{e.path.Count} [{string.Join(",",e.path)}] detour={e.detour}/{e.detourUntil} status={e.status}")));
        }
    }
}
