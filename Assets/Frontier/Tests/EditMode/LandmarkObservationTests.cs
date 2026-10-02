using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class LandmarkObservationTests
    {
        static World Opening(int seed=314159)=>new World(Catalog.Load(),seed,"coast","river",5);
        static void Tick(World world,float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.1f);i++)world.Tick(.1f);}
        static EntityState Explorer(World world,int player)=>world.Entities.First(e=>e.owner==player&&(e.def.tags&Tags.Explorer)!=0);
        [TestCase(314159)][TestCase(271828)]public void AuthoredOpeningLogsActualPathsToEverySocketCampAndGuardianForBothPlayers(int seed)
        {
            // Distinct seed labels exercise the same fixed authored map.
            var world=Opening(seed);int objects=world.TradeSockets.Count+world.Treasures.Sum(t=>1+t.guards.Count);
            var events=world.Events.Where(e=>e.kind=="LandmarkReachability").ToArray();Assert.AreEqual(objects*2,events.Length);
            for(int player=0;player<2;player++)
            {
                var town=world.Entities.First(e=>e.owner==player&&e.def.id=="town");
                Assert.IsTrue(world.FindSpawnPositions(town,1,Catalog.Load().Entity("villager").radius,out var exits));
                var records=events.Where(e=>e.player==player).ToArray();Assert.AreEqual(objects,records.Length);Assert.AreEqual(objects,records.Select(e=>e.entity).Distinct().Count());
                foreach(var record in records)
                {
                    var observation=JsonUtility.FromJson<LandmarkReachabilityObservation>(record.message);var target=world.Get(record.entity);
                    Assert.AreEqual(seed,observation.seed);Assert.AreEqual(player,observation.player);Assert.AreEqual(town.id,observation.openingTown);
                    Assert.AreEqual(exits[0],observation.from);Assert.AreEqual(target.position,observation.position);Assert.AreEqual(target.def.id,observation.objectType);
                    Assert.IsTrue(observation.openingExitAvailable&&observation.startWalkable&&observation.destinationWalkable&&observation.reachable,target.def.id+" "+target.position);
                    Assert.AreEqual(1,record.amount);var path=world.Navigation.FindPath(exits[0],target.position,observation.radius);Assert.IsNotEmpty(path);Assert.AreEqual(target.position,path.Last());
                    Assert.AreEqual(path.Count,observation.pathNodes);Assert.IsTrue(world.TradeSockets.Any(s=>s.entity==observation.nearestSocket));
                    if(World.Guardian(target))Assert.IsTrue(world.Treasures.Any(t=>t.entity==observation.camp&&t.guards.Contains(target.id)));
                }
                var opening=Catalog.Load().Civ(world.Players[player].civilization).startingResources;
                Assert.AreEqual(opening.food,world.Players[player].resources.food);Assert.AreEqual(opening.wood,world.Players[player].resources.wood);Assert.AreEqual(opening.coin,world.Players[player].resources.coin);
            }
            Assert.IsFalse(world.Events.Any(e=>e.kind=="Payment"||e.kind=="TradeDelivery"||e.kind=="TreasureClaimed"));
        }
        [Test]public void BlockedAuthoredSocketIsRecordedUnreachableRatherThanUsingSnappedDestination()
        {
            // Negative map-data fixture only, never a standard game sample.
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));rules.landmarks.sockets[0].position=new Vector2(24,28);
            var world=new World(rules,314159,"coast","river",5);int id=world.TradeSockets[0].entity;
            var records=world.Events.Where(e=>e.kind=="LandmarkReachability"&&e.entity==id).ToArray();Assert.AreEqual(2,records.Length);
            foreach(var record in records)
            {
                var observation=JsonUtility.FromJson<LandmarkReachabilityObservation>(record.message);
                Assert.IsFalse(observation.destinationWalkable);Assert.IsFalse(observation.reachable);Assert.AreEqual(0,record.amount);Assert.AreEqual(0,observation.pathNodes);
            }
        }
        [Test]public void FirstDiscoveryUsesActualVisibleFogSeparatelyForEachPlayerAndDoesNotRepeat()
        {
            var world=Opening();var socket=world.Get(world.TradeSockets[0].entity);
            Assert.AreNotEqual(VisibilityState.Visible,world.Fog.At(0,socket.position));Assert.AreNotEqual(VisibilityState.Visible,world.Fog.At(1,socket.position));
            Assert.IsFalse(world.Events.Any(e=>e.kind=="LandmarkDiscovered"&&e.entity==socket.id));
            var initial=world.Events.Where(e=>e.kind=="LandmarkDiscovered").ToArray();Assert.IsNotEmpty(initial);
            foreach(var observed in initial){Assert.AreEqual(0,observed.time);Assert.AreEqual(VisibilityState.Visible,world.Fog.At(observed.player,observed.position));Assert.AreNotEqual(0,observed.visibilityMask&(1<<observed.player));}
            var own=Explorer(world,0);Vector2 home=own.position;own.position=socket.position+Vector2.up*6;Tick(world,.5f);
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="LandmarkDiscovered"&&e.entity==socket.id&&e.player==0));Assert.AreEqual(0,world.Events.Count(e=>e.kind=="LandmarkDiscovered"&&e.entity==socket.id&&e.player==1));
            own.position=home;Tick(world,.5f);Assert.AreEqual(VisibilityState.Explored,world.Fog.At(0,socket.position));
            own.position=socket.position+Vector2.up*6;Tick(world,.5f);Assert.AreEqual(1,world.Events.Count(e=>e.kind=="LandmarkDiscovered"&&e.entity==socket.id&&e.player==0));
            Explorer(world,1).position=socket.position+Vector2.up*6;Tick(world,.5f);Assert.AreEqual(1,world.Events.Count(e=>e.kind=="LandmarkDiscovered"&&e.entity==socket.id&&e.player==1));
            var fresh=Opening();Assert.IsFalse(fresh.Events.Any(e=>e.kind=="LandmarkDiscovered"&&e.entity==socket.id));
        }
        [Test]public void HiddenEnemyTradingPostHasNoObserverDiscoveryUntilItsFogCellIsVisible()
        {
            // A controlled enemy post isolates discovery from construction cost.
            var world=Opening();var site=world.Get(world.TradeSockets[0].entity);var post=world.Spawn("trade_post",1,site.position);Tick(world,.5f);
            Assert.AreNotEqual(VisibilityState.Visible,world.Fog.At(0,post.position));Assert.IsFalse(world.Events.Any(e=>e.kind=="LandmarkDiscovered"&&e.entity==post.id&&e.player==0));
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="LandmarkDiscovered"&&e.entity==post.id&&e.player==1));
            Explorer(world,0).position=post.position+Vector2.up*6;Tick(world,.5f);
            var discovered=world.Events.Where(e=>e.kind=="LandmarkDiscovered"&&e.entity==post.id&&e.player==0).ToArray();Assert.AreEqual(1,discovered.Length);
            var observation=JsonUtility.FromJson<LandmarkDiscoveryObservation>(discovered[0].message);Assert.AreEqual("trade_post",observation.objectType);Assert.AreEqual(post.position,observation.position);Assert.AreNotEqual(0,discovered[0].visibilityMask&1);
            Tick(world,1);Assert.AreEqual(1,world.Events.Count(e=>e.kind=="LandmarkDiscovered"&&e.entity==post.id&&e.player==0));
        }
    }
}
