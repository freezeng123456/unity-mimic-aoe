using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class RevolutionShipment071Tests
    {
        sealed class Fixture
        {
            public World world,coldWorld;
            public MatchSimulation simulation,coldSimulation;
            public ReplayRecorder recorder;
            public int town;
            public PlayerState Player=>world.Players[0];
        }
        static Fixture Prepared()
        {
            // Explicit age-IV workforce/housing and exactly one revolution's
            // funds isolate arrival starvation. No funds, entities, capacity,
            // HP or coordinates are injected after commands begin.
            var world=new World(SnapshotCodec.Clone(Catalog.Load()),314159,"coast","coast",5);
            foreach(var entity in world.Entities.ToArray())world.Remove(entity);
            var town=world.Spawn("town",0,new Vector2(20,20));world.Spawn("town",1,new Vector2(110,110));
            for(int i=0;i<20;i++)world.Spawn("villager",0,new Vector2(32+i%5*1.6f,18+i/5*1.6f));
            var player=world.Players[0];player.age=4;player.resources=world.RevolutionDefinition(0).cost;
            player.deck.Clear();player.deck.Add("settlers");
            world.AwardXp(0,world.Rules.initialXp*3+world.Rules.xpIncrease*3,town.id,"Explicit three earned opportunities fixture");
            Assert.AreEqual(3,player.shipmentTokens);Assert.AreEqual(20,player.usedPopulation);Assert.AreEqual(20,player.populationCap);
            world.Fog.Tick(world.Entities);var simulation=new MatchSimulation(world,Array.Empty<int>());
            var f=new Fixture{world=world,simulation=simulation,town=town.id};
            f.recorder=new ReplayRecorder(simulation,ReplayIdentity.Create(world.Rules,"0.14.0","revolution-shipment-test","test-engine"),
                segmentId:"full-housing-supply",anchorInterval:3000);return f;
        }
        static void Issue(Fixture f,GameCommand command)
        {
            var result=f.recorder.Issue(command);Assert.IsTrue(result.accepted,result.reason);
            if(f.coldWorld!=null)
            {
                var restored=f.coldWorld.Issue(SnapshotCodec.Clone(command));
                Assert.AreEqual(result.accepted,restored.accepted);Assert.AreEqual(result.reason,restored.reason);
            }
        }
        static void Step(Fixture f)
        {Assert.IsTrue(f.simulation.Step());if(f.coldSimulation!=null)Assert.IsTrue(f.coldSimulation.Step());}
        static void Steps(Fixture f,float seconds)
        {for(int i=0;i<Mathf.CeilToInt(seconds/MatchSimulation.StepSeconds);i++)Step(f);}
        static void Until(Fixture f,Func<bool> condition,float seconds,string message)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/MatchSimulation.StepSeconds)&&!condition();i++)Step(f);
            Assert.IsTrue(condition(),message+" t="+f.world.Time+" balance="+f.Player.resources+" shipments="+string.Join("; ",f.Player.shipments.Select(s=>s.card+" "+s.elapsed+" "+s.waiting)));
        }
        static EntityState[] Crates(World w)=>w.Entities.Where(e=>e.Alive&&e.owner==0&&e.def.id.StartsWith("crate_",StringComparison.Ordinal)).ToArray();
        static void ZeroBalance(PlayerState player)
        {Assert.AreEqual(0,player.resources.food,.002f);Assert.AreEqual(0,player.resources.wood,.002f);Assert.AreEqual(0,player.resources.coin,.002f);}
        static void EqualCold(Fixture f)
        {
            Assert.AreEqual(JsonUtility.ToJson(f.world.CaptureState()),JsonUtility.ToJson(f.coldWorld.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(f.simulation.CaptureState()),JsonUtility.ToJson(f.coldSimulation.CaptureState()));
        }
        static void ColdSlot(Fixture f,int[] originalWorkers)
        {
            string directory=Path.Combine(Path.GetTempPath(),"frontier-revolution-supply071-"+Guid.NewGuid().ToString("N"));
            try
            {
                var save=new SavedMatch{matchId="supply-slot-"+Guid.NewGuid().ToString("N"),appVersion="revolution-supply071-test",world=f.world.CaptureState(),simulation=f.simulation.CaptureState(),
                    groups=new[]{new ControlGroupSnapshot{number=2,actors=originalWorkers}},selection=originalWorkers,cameraSize=26,cameraFocus=new Vector2(20,20),cameraPosition=new Vector3(20,40,20),simulationSpeed=1};
                string path=MatchSaveStore.SlotPath(directory,2);MatchSaveStore.Write(path,save);var loaded=MatchSaveStore.Read(path,save.appVersion);
                CollectionAssert.AreEqual(originalWorkers,loaded.groups[0].actors);CollectionAssert.AreEqual(originalWorkers,loaded.selection);
                f.coldWorld=World.Restore(f.world.Rules,loaded.world);f.coldSimulation=MatchSimulation.Restore(f.coldWorld,loaded.simulation);EqualCold(f);
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }

        [TestCase("grandfathered-worker")][TestCase("revolution-reserve")]
        public void BlockedFullHousingTroopCannotStarvePaidSuppliesAndRealCratesFundRebuilding(string headKind)
        {
            var f=Prepared();using(f.recorder)
            {
                var original=f.world.Entities.Where(e=>e.Alive&&e.owner==0&&e.Worker).Select(e=>e.id).ToArray();
                string troop=headKind=="grandfathered-worker"?"settlers":"rev_coast_reserve";
                if(headKind=="grandfathered-worker")Issue(f,new GameCommand{player=0,kind=CommandKind.Ship,item=troop});
                Issue(f,new GameCommand{player=0,kind=CommandKind.Revolution,actors=new[]{f.town}});ZeroBalance(f.Player);
                Until(f,()=>f.Player.politicalPhase==PoliticalPhase.Revolutionary,92,"Paid conversion must complete without a resource grant");
                Assert.IsTrue(original.All(id=>f.world.Get(id).def.revolutionary));Assert.AreEqual(20,f.Player.usedPopulation);Assert.IsEmpty(Crates(f.world));
                if(headKind=="revolution-reserve")Issue(f,new GameCommand{player=0,kind=CommandKind.Ship,item=troop});
                Until(f,()=>f.Player.shipments[0].elapsed>=f.world.Rules.Card(troop).travel&&f.Player.shipments[0].waiting!="",37,"Actual troop travel must finish and remain blocked by real housing");
                var head=f.Player.shipments.Single();Assert.AreEqual(troop,head.card);StringAssert.Contains("population",head.waiting.ToLowerInvariant());
                Assert.AreEqual(2,f.Player.shipmentTokens);float heldElapsed=head.elapsed;
                Issue(f,new GameCommand{player=0,kind=CommandKind.Ship,item="rev_coast_food"});
                Issue(f,new GameCommand{player=0,kind=CommandKind.Ship,item="rev_coast_engineering"});
                Assert.AreEqual(0,f.Player.shipmentTokens);Assert.AreEqual(3,f.Player.shipments.Count);Steps(f,17);
                Assert.IsEmpty(Crates(f.world),"A blocked troop is not permission for instant supply delivery");ZeroBalance(f.Player);
                ColdSlot(f,original);Steps(f,17);
                Assert.IsEmpty(Crates(f.world),"The supply must retain its own 35-second journey after cold recovery");ZeroBalance(f.Player);
                Until(f,()=>f.Player.shipments.Count==1,40,"Food and engineering supplies must pass the housing-blocked troop using their own travel clocks");
                EqualCold(f);Assert.AreSame(head,f.Player.shipments.Single(),"Removing a later supply must not remove the queue head");
                Assert.AreEqual(heldElapsed,head.elapsed);Assert.AreEqual(troop,head.card);Assert.AreEqual(20,f.Player.usedPopulation);Assert.AreEqual(0,f.Player.reservedPopulation);
                Assert.AreEqual(20,f.world.Entities.Count(e=>e.Alive&&e.owner==0&&e.def.revolutionary));ZeroBalance(f.Player);
                var crates=Crates(f.world);Assert.AreEqual(3,crates.Length);CollectionAssert.AreEquivalent(new[]{"crate_food","crate_wood","crate_coin"},crates.Select(e=>e.def.id));
                Assert.AreEqual(1000,crates.Single(e=>e.def.id=="crate_food").remaining,.002f);Assert.AreEqual(800,crates.Single(e=>e.def.id=="crate_wood").remaining,.002f);Assert.AreEqual(400,crates.Single(e=>e.def.id=="crate_coin").remaining,.002f);
                var used=new HashSet<int>();
                foreach(var crate in crates)
                {
                    var collector=original.Select(f.world.Get).Where(e=>!used.Contains(e.id)).OrderBy(e=>Vector2.Distance(e.position,crate.position)).First();used.Add(collector.id);
                    Issue(f,new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{collector.id},order=OrderKind.Gather,target=crate.id});
                }
                Until(f,()=>Crates(f.world).Length==0,240,"Militia must really walk to and consume all own physical supply crates");EqualCold(f);
                Assert.AreEqual(1000,f.Player.gathered.food,.05f);Assert.AreEqual(800,f.Player.gathered.wood,.05f);Assert.AreEqual(400,f.Player.gathered.coin,.05f);
                Assert.Greater(f.world.Events.Count(e=>e.kind=="Gather"&&used.Contains(e.entity)),0);Assert.AreSame(head,f.Player.shipments.Single());
                var rebuild=f.world.Rules.Tech(World.CivilianRebuildingTechnology);var balance=f.Player.resources;
                Issue(f,new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{f.town},item=rebuild.id});
                Assert.AreEqual(balance.food-rebuild.cost.food,f.Player.resources.food,.002f);Assert.AreEqual(balance.coin-rebuild.cost.coin,f.Player.resources.coin,.002f);
                Until(f,()=>f.Player.politicalPhase==PoliticalPhase.CivilianRebuilt,rebuild.time+2,"The collected supplies must finance the actual paid rebuilding queue");
                Steps(f,5);EqualCold(f);Assert.IsTrue(f.Player.technologies.Contains(rebuild.id));Assert.AreEqual(20,f.Player.usedPopulation);
                Assert.IsTrue(original.All(id=>f.world.Get(id).Alive&&f.world.Get(id).def.revolutionary));Assert.AreSame(head,f.Player.shipments.Single());Assert.AreEqual(heldElapsed,head.elapsed);
                Assert.AreEqual(2,f.world.Events.Count(e=>e.kind=="ShipmentArrived"&&e.player==0),"Exactly the two supply shipments arrived; the troop job still waits");
                Assert.AreEqual(3,f.world.Events.Count(e=>e.kind=="ShipmentSent"&&e.player==0));Assert.AreEqual(1,f.world.Events.Count(e=>e.kind=="CivilianRebuilt"&&e.player==0));
                Assert.IsTrue(f.Player.sentCards.Contains(troop));Assert.IsTrue(f.Player.sentCards.Contains("rev_coast_food"));Assert.IsTrue(f.Player.sentCards.Contains("rev_coast_engineering"));
                if(headKind=="grandfathered-worker")
                {
                    // Full housing is still a real constraint after rebuilding.
                    // A public, non-refunding disband releases one slot; paid
                    // training reserves it before the old two-settler arrival.
                    var dismissed=f.world.Get(original.First(id=>!used.Contains(id)));var funds=f.Player.resources;var refunded=f.Player.refunded;float xp=f.Player.xp;
                    Issue(f,new GameCommand{player=0,kind=CommandKind.Disband,actors=new[]{dismissed.id}});
                    Assert.IsFalse(dismissed.Alive);Assert.AreEqual(19,f.Player.usedPopulation);Assert.AreEqual(funds,f.Player.resources);Assert.AreEqual(refunded,f.Player.refunded);Assert.AreEqual(xp,f.Player.xp);
                    var workerCost=f.world.UnitCost(0,f.world.WorkerDefinition(0));
                    Issue(f,new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{f.town},item=f.world.WorkerDefinition(0).id});
                    Assert.AreEqual(1,f.Player.reservedPopulation);Assert.AreEqual(funds.food-workerCost.food,f.Player.resources.food,.002f);Assert.AreSame(head,f.Player.shipments.Single());
                    Until(f,()=>f.world.Entities.Any(e=>e.Alive&&e.owner==0&&e.Worker),f.world.WorkerDefinition(0).time+2,"One disbanded slot must deliver a real, paid civilian worker");
                    var builder=f.world.Entities.Single(e=>e.Alive&&e.owner==0&&e.Worker);Assert.AreEqual(20,f.Player.usedPopulation);Assert.AreEqual(0,f.Player.reservedPopulation);
                    Assert.AreSame(head,f.Player.shipments.Single(),"The one available slot cannot receive the old two-settler shipment");
                    bool found=false;Vector2 site=Vector2.zero;
                    foreach(var candidate in f.world.Navigation.FormationSlots(new Vector2(30,30),200,2))
                        if(f.world.CanBuild(0,"house",candidate)==""){site=candidate;found=true;break;}
                    Assert.IsTrue(found,"The recovered worker must find an actually legal visible house site");var wood=f.Player.resources.wood;
                    Issue(f,new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{builder.id},item="house",point=site});
                    var house=f.world.Entities.Last(e=>e.Alive&&e.owner==0&&e.def.id=="house");
                    Assert.AreEqual(wood-f.world.EntityCost(0,house.def).wood,f.Player.resources.wood,.002f);
                    Until(f,()=>house.Complete,80,"The paid worker must physically construct the house that restores housing");
                    Until(f,()=>!f.Player.shipments.Contains(head),3,"The unchanged old shipment may arrive once the real completed house provides space");
                    Steps(f,2);EqualCold(f);Assert.IsEmpty(f.Player.shipments);Assert.AreEqual(30,f.Player.populationCap);Assert.AreEqual(23,f.Player.usedPopulation);
                    Assert.AreEqual(4,f.world.Entities.Count(e=>e.Alive&&e.owner==0&&e.Worker),"Paid worker, two accepted settlers and one actual Manor gift");
                    Assert.AreEqual(19,f.world.Entities.Count(e=>e.Alive&&e.owner==0&&e.def.revolutionary));Assert.AreEqual(1,f.Player.trained);
                    Assert.AreEqual(3,f.world.Events.Count(e=>e.kind=="ShipmentArrived"&&e.player==0),"The troop arrives exactly once after the two exact supply arrivals");
                    Steps(f,5);Assert.AreEqual(23,f.Player.usedPopulation);EqualCold(f);
                }
                var recording=f.recorder.Finish("supply-rebuilding-verified");
                using(var replay=new ReplaySession(f.world.Rules,recording,recording.identity))
                {
                    Assert.AreEqual(0,replay.StartStep,"Reconstruction must include every recorded command from the initial fixture");
                    Assert.AreEqual(replay.StartStep,replay.CurrentStep,"Forward reconstruction must start at the first anchor, with no seek to a later checkpoint");
                    while(replay.CurrentStep<replay.EndStep&&replay.Error=="")Assert.Greater(replay.Advance(1000),0,"Forward replay must progress");
                    Assert.AreEqual("",replay.Error);Assert.IsTrue(replay.Verified);Assert.AreEqual(JsonUtility.ToJson(f.world.CaptureState()),JsonUtility.ToJson(replay.World.CaptureState()));
                    Assert.AreEqual(JsonUtility.ToJson(f.simulation.CaptureState()),JsonUtility.ToJson(replay.Simulation.CaptureState()));
                }
            }
        }
    }
}
