using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class WorldSaveTests
    {
        static string State(World world)=>JsonUtility.ToJson(world.CaptureState());
        static World NewWorld(string map="frontier",string civilization="coast")=>new World(Catalog.Load(),271828,civilization,"river",5,map);
        [TestCase("frontier")][TestCase("river")][TestCase("winding")][TestCase("tropical")][TestCase("tributaries")][TestCase("estuary")]
        public void AllAuthoredMapsRoundTripAndContinueWithTheSameWorldState(string map)
        {
            var original=NewWorld(map);var worker=original.Entities.First(e=>e.owner==0&&e.Worker);
            var source=original.Entities.First(e=>e.def.id=="berry"&&original.IsVisible(0,e));
            Assert.IsTrue(original.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Gather,target=source.id}).accepted);
            for(int i=0;i<125;i++)original.Tick(.1f);
            var restored=World.Restore(original.Rules,JsonUtility.FromJson<WorldSnapshot>(State(original)));
            Assert.AreEqual(State(original),State(restored),"Cold JSON roundtrip retains exact state");
            for(int i=0;i<500;i++){original.Tick(.1f);restored.Tick(.1f);}
            Assert.AreEqual(State(original),State(restored),"Gather income, visibility, caravan and IDs continue identically");
        }
        [TestCase("coast")][TestCase("river")][TestCase("france")][TestCase("ottoman")]
        public void CivilizationsRetainQueuesShipmentsOrdersAndAutomaticRecruitment(string civilization)
        {
            var w=NewWorld("river",civilization);var p=w.Players[0];p.age=3;p.resources=new Cost(10000,10000,10000);p.populationCap=200;
            var town=w.Entities.First(e=>e.owner==0&&e.def.id=="town");
            if(civilization!="ottoman")Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item=w.WorkerDefinition(0).id}).accepted);
            var dock=w.Spawn("dock",0,new Vector2(55,28));Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{dock.id},item="gunboat"}).accepted);
            var worker=w.Entities.First(e=>e.owner==0&&e.Worker);var foundation=w.Spawn("house",0,new Vector2(40,28),false);
            w.Fog.Reveal(0,foundation.position,20);Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Build,target=foundation.id}).accepted);
            p.deck.Clear();p.deck.AddRange(DeckLibrary.Preset(w.Rules,civilization,"economy"));p.shipmentTokens=1;
            var card=p.deck.Select(w.Rules.Card).First(c=>c.age<=p.age&&c.count==0&&c.tech=="");Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item=card.id}).accepted);
            for(int i=0;i<43;i++)w.Tick(.1f);
            var clone=World.Restore(w.Rules,w.CaptureState());Assert.AreEqual(State(w),State(clone));
            for(int i=0;i<600;i++){w.Tick(.1f);clone.Tick(.1f);}Assert.AreEqual(State(w),State(clone));
        }
        [Test] public void DetachedSnapshotCannotChangeWhenTheOriginalTicks()
        {
            var w=NewWorld();var state=w.CaptureState();string frozen=JsonUtility.ToJson(state);
            w.Players[0].resources.food+=80;w.Entities[0].hp-=10;w.Entities[0].orders.Enqueue(new UnitOrder(OrderKind.Move,new Vector2(42,42)));w.Tick(.1f);
            Assert.AreEqual(frozen,JsonUtility.ToJson(state));
        }
        [Test] public void ReportFlushAfterColdLoadPreservesTheActualIncomeEventOrder()
        {
            var world=NewWorld();
            var firstResource=world.Spawn("berry",-1,new Vector2(50,10));
            var secondResource=world.Spawn("berry",-1,new Vector2(55,10));
            var lowerId=world.Spawn("villager",0,new Vector2(55,8));
            var higherId=world.Spawn("villager",0,new Vector2(50,8));
            world.Fog.Tick(world.Entities);
            // The higher ID starts first, so live bucket insertion order is
            // deliberately opposite to the cold snapshot's canonical order.
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{higherId.id},order=OrderKind.Gather,target=firstResource.id}).accepted);
            world.Tick(.1f);world.Tick(.1f);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{lowerId.id},order=OrderKind.Gather,target=secondResource.id}).accepted);
            world.Tick(.1f);world.Tick(.1f);
            var snapshot=world.CaptureState();
            Assert.AreEqual(2,snapshot.incomes.Count(i=>i.amount>0&&(i.source==higherId.id||i.source==lowerId.id)),"Both ordinary gatherers must have real unflushed income");
            var restored=World.Restore(world.Rules,JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(snapshot)));
            int events=world.Events.Count;world.FlushEconomyLog();restored.FlushEconomyLog();
            CollectionAssert.AreEqual(new[]{lowerId.id,higherId.id},world.Events.Skip(events).Where(e=>e.kind=="Gather").Select(e=>e.entity).ToArray());
            Assert.AreEqual(State(world),State(restored),"Saving a report after resuming cannot reorder income events");
        }
        [Test] public void SavesKeepGarrisonProjectilesAndFogHistory()
        {
            var w=NewWorld("river");var town=w.Entities.First(e=>e.owner==0&&e.def.id=="town");var worker=w.Entities.First(e=>e.owner==0&&e.Worker);
            worker.garrisonedIn=town.id;town.occupants.Add(worker.id);
            var ship=w.Spawn("gunboat",0,new Vector2(64,25));var enemy=w.Spawn("gunboat",1,new Vector2(64,31));w.Fog.Reveal(0,new Vector2(64,30),12);
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{ship.id},order=OrderKind.Attack,target=enemy.id}).accepted);
            for(int i=0;i<50&&w.Projectiles.Count==0;i++)w.Tick(.1f);Assert.Greater(w.Projectiles.Count,0);
            w.Fog.Reveal(0,new Vector2(100,10),3);w.Fog.Tick(w.Entities);Assert.AreEqual(VisibilityState.Explored,w.Fog.At(0,new Vector2(100,10)));
            var restored=World.Restore(w.Rules,w.CaptureState());Assert.AreEqual(State(w),State(restored));
            for(int i=0;i<40;i++){w.Tick(.1f);restored.Tick(.1f);}Assert.AreEqual(State(w),State(restored));
        }
        [Test] public void ImplicitPursuitKeepsItsNullOrderRecoveryQueueAndColdContinuation()
        {
            var world=NewWorld();foreach(var entity in world.Entities.Where(e=>e.def.id!="town").ToArray())world.Remove(entity);
            // The accepted quarter-meter corridor fixture has no integer-node
            // route through these parked allies, but has an exact body-clear gap.
            world.Navigation.ConfigureTerrain(point=>point.y==54||point.y==59);
            var low=world.Spawn("pike",0,new Vector2(48,56.25f));var high=world.Spawn("pike",0,new Vector2(46,56.25f));
            var guards=new[]{world.Spawn("hussar",0,new Vector2(55,55.08f)),world.Spawn("hussar",0,new Vector2(55,57.42f))};
            var enemy=world.Spawn("villager",1,new Vector2(63,56.9f));
            foreach(var entity in guards.Concat(new[]{low,high,enemy}))
                Assert.IsTrue(world.Issue(new GameCommand{player=entity.owner,kind=CommandKind.Order,order=OrderKind.Hold,actors=new[]{entity.id}}).accepted);
            for(int i=0;i<31;i++)world.Tick(.1f);
            foreach(var actor in new[]{low,high})
                Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Stop,actors=new[]{actor.id}}).accepted);
            world.Fog.Tick(world.Entities);
            // Stop keeps the ordinary enemy-scan cooldown. Wait for that real
            // scan instead of assigning a target or replacing the implicit order.
            for(int i=0;i<10&&(low.attackTarget!=enemy.id||high.attackTarget!=enemy.id);i++)world.Tick(.1f);
            foreach(var actor in new[]{low,high})
            {
                Assert.IsEmpty(actor.orders,"The pursuit must be automatic rather than a serialized Attack order");
                Assert.AreEqual(enemy.id,actor.attackTarget);Assert.AreEqual("Moving",actor.status);
                // Stage the same legitimate stalled boundary as the navigation
                // fairness test. The production planner creates every request.
                actor.recoveryProgressAt=world.Time-3;actor.recoveryProgressPosition=actor.position;
            }
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var recover=typeof(World).GetMethod("TryAllyRecovery",flags);
            Assert.IsTrue((bool)recover.Invoke(world,new object[]{low,low.pathGoal}),"The real low-ID query consumes this tick's fine slot");
            Assert.IsFalse((bool)recover.Invoke(world,new object[]{high,high.pathGoal}));
            Assert.IsTrue((bool)typeof(World).GetField("fineRecoveryDeferred",flags).GetValue(world));
            Assert.IsFalse((bool)recover.Invoke(world,new object[]{low,low.pathGoal}));
            var snapshot=world.CaptureState();CollectionAssert.AreEqual(new[]{high.id,low.id},snapshot.fineRecoveryQueue,
                "Null-order implicit pursuits retain the actual high-before-low FIFO; no queue entries are injected by this fixture");
            var restored=World.Restore(world.Rules,JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(snapshot)));
            Assert.AreEqual(State(world),State(restored));
            world.Tick(.1f);restored.Tick(.1f);
            Assert.IsTrue(high.pathAvoidsAllies,"The restored request must continue real implicit pursuit through the fine corridor");
            Assert.AreEqual(State(world),State(restored),"The first fine reservation must go to the same implicit pursuer after loading");
            for(int i=0;i<20;i++)
            {
                foreach(var actor in new[]{low,high})
                {
                    Assert.IsTrue(world.Navigation.ClearLine(actor.previousPosition,actor.position,actor.def.radius));
                    foreach(var guard in guards)
                    {
                        Vector2 segment=actor.position-actor.previousPosition;
                        float t=segment.sqrMagnitude>.000001f?Mathf.Clamp01(Vector2.Dot(guard.position-actor.previousPosition,segment)/segment.sqrMagnitude):0;
                        Assert.GreaterOrEqual(Vector2.Distance(actor.previousPosition+segment*t,guard.position),actor.def.radius+guard.def.radius);
                    }
                }
                world.Tick(.1f);restored.Tick(.1f);Assert.AreEqual(State(world),State(restored),"Implicit continuation tick "+i);
            }
        }
        [Test] public void UnsupportedOrMalformedWorldNeverRestores()
        {
            var w=NewWorld();var s=w.CaptureState();s.schema++;Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,s));
            s=w.CaptureState();s.rulesHash="different";Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,s));
            s=w.CaptureState();s.entities[1].state.id=s.entities[0].state.id;Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,s));
            s=w.CaptureState();s.players[0].state.resources.food=float.NaN;Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,s));
            s=w.CaptureState();s.visibility[100]=3;Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,s));
            s=w.CaptureState();s.caravanWaypoint=-1;Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,s));
            s=w.CaptureState();s.entities[0].queue=new[]{new ProductionJob{kind="Research",item="unknown"}};Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,s));
        }
        [Test] public void AtomicSlotOverwriteAndCorruptionDetectionPreserveThePreviousCheckpoint()
        {
            string directory=Path.Combine(Path.GetTempPath(),"Frontier-save-test-"+Guid.NewGuid().ToString("N"));string path=MatchSaveStore.SlotPath(directory,1);
            try
            {
                var w=NewWorld();var sim=new MatchSimulation(w,1);var save=new SavedMatch{matchId="test",appVersion="0.7.0",world=w.CaptureState(),simulation=sim.CaptureState()};
                MatchSaveStore.Write(path,save);Assert.AreEqual(0,MatchSaveStore.Read(path).world.time);
                Assert.Throws<IOException>(()=>MatchSaveStore.Read(path,"0.6.0"));
                sim.Step();save.world=w.CaptureState();save.simulation=sim.CaptureState();MatchSaveStore.Write(path,save);Assert.AreEqual(w.Time,MatchSaveStore.Read(path).world.time);
                string previous=File.ReadAllText(path);save.world=null;Assert.Throws<ArgumentException>(()=>MatchSaveStore.Write(path,save));Assert.AreEqual(previous,File.ReadAllText(path));
                var envelope=JsonUtility.FromJson<SaveEnvelope>(previous);envelope.payload+=" ";File.WriteAllText(path,JsonUtility.ToJson(envelope));Assert.Throws<IOException>(()=>MatchSaveStore.Read(path));
                Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp-*"));
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
}
