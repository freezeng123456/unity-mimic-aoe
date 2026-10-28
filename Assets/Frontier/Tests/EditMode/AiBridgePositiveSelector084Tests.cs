using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Declared funded Highland Age-III strategy fixture on the original authored map.
    // The initial wallet/age are prepared mechanics inputs; all new buildings,
    // the ship and twelve soldiers are paid public production with real time.
    // No source entity is removed/spawned, and no candidate/mission is injected.
    public sealed class AiBridgePositiveSelector084Tests
    {
        sealed class Fixture
        {
            public readonly World W;
            public readonly EntityState Builder,Explorer,Town;
            public readonly AiDifficultyProfile Difficulty;
            public readonly AiArmyPlan072 Army;
            public readonly AiWaterEconomy Water;
            public readonly AiSearchPlan Search;
            public readonly AiRevolution071 Revolution;
            public readonly AiCivilizationEconomy Civilization;
            public readonly AiMedicalSupport076 Medical;
            public readonly AiBridgeTransport084 Bridge;
            public readonly List<int> PaidSoldiers=new List<int>();
            public EntityState Ship;
            public Fixture()
            {
                W=new World(SnapshotCodec.Clone(Catalog.Load()),42,"highland","coast",5,MapTopology.Tributaries);
                Assert.AreEqual(1,W.Players[0].age);Assert.AreEqual(new Cost(300,350,100),W.Players[0].resources);
                W.Players[0].age=3;W.Players[0].resources=new Cost(15000,15000,15000);
                Town=W.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id=="town");
                Builder=W.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker);
                Explorer=W.Entities.Single(e=>e.Alive&&e.owner==0&&(e.def.tags&Tags.Explorer)!=0);
                Difficulty=AiDifficultyProfile.From(W.Rules,"standard");
                Army=new AiArmyPlan072(W,0,Difficulty);Water=new AiWaterEconomy(W,0);
                Search=new AiSearchPlan(W,0,W.OpeningTownPosition(1));Revolution=new AiRevolution071(W,0,Difficulty);
                Civilization=new AiCivilizationEconomy(W,0);Medical=new AiMedicalSupport076(W,0);
                Bridge=new AiBridgeTransport084(W,0,Army,Revolution,Medical);Army.BindTransport(Bridge.Owns);
            }
            public void SaveEvidence(string stage)
            {
                WriteWorldEvidence(stage,W);
                string output=EvidenceDirectory();
                File.WriteAllText(Path.Combine(output,stage+"-army.json"),JsonUtility.ToJson(Army.CaptureState(),true));
                File.WriteAllText(Path.Combine(output,stage+"-bridge-candidate.json"),Bridge.Candidate==null?"null":JsonUtility.ToJson(Bridge.Candidate,true));
                File.WriteAllText(Path.Combine(output,stage+"-bridge-mission.json"),Bridge.Mission==null?"null":JsonUtility.ToJson(Bridge.Mission,true));
                File.WriteAllText(Path.Combine(output,stage+"-medical.json"),JsonUtility.ToJson(Medical.CaptureState(),true));
            }
            public List<EntityState> Own()=>W.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
            public List<EntityState> Visible()=>W.Entities.Where(e=>AiResourceContinuity072.EconomicThreat(W,0,e)).ToList();
            public void Step()
            {Assert.Less(W.Time,3600,"New standalone fixture budget; original paid-chain limits are untouched");W.Tick(.1f);}
            public void Until(Func<bool> predicate,float seconds,string reason)
            {
                for(int i=0;i<Mathf.CeilToInt(seconds*10)&&!predicate()&&!W.IsMatchEnded;i++)Step();
                Assert.IsTrue(predicate(),reason+" t="+W.Time+" units="+string.Join(" | ",Own().Where(e=>e.def.kind==EntityKind.Unit).Select(e=>e.id+"/"+e.def.id+" "+e.position+" "+e.status)));
            }
            public void Order(EntityState unit,OrderKind kind,Vector2 point=default,int target=0)=>Accepted(W.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{unit.id},order=kind,point=point,target=target}));
            public void Paid(GameCommand command,Cost cost)
            {
                var before=W.Players[0].resources;var spent=W.Players[0].spent;int cursor=W.Events.Count;
                Assert.IsTrue(W.CanAfford(0,cost));command.player=0;Accepted(W.Issue(command));
                Equal(before+cost*-1,W.Players[0].resources);Equal(spent+cost,W.Players[0].spent);
                var payment=W.Events.Skip(cursor).Single(e=>e.kind=="Payment"&&e.player==0);
                Assert.IsTrue(payment.hasEconomy);Equal(cost*-1,payment.economy.actualDelta);
            }
            public EntityState Build(string id,Vector2? fixedPoint=null)
            {
                Vector2 point=default;bool found=false;
                if(fixedPoint.HasValue)
                {
                    point=fixedPoint.Value;Order(Builder,OrderKind.Move,point-W.DockWaterDirection(point)*4);
                    Until(()=>Builder.orders.Count==0&&W.Fog.At(0,point)==VisibilityState.Visible,120,"Real survey of paid fixed dock");
                    found=W.CanBuild(0,id,point)=="";
                }
                else for(int y=-17;y<=17&&!found;y++)for(int x=-17;x<=17&&!found;x++)
                {var candidate=W.BuildPoint(0,id,Town.position+new Vector2(x,y));if(W.CanBuild(0,id,candidate)==""){point=candidate;found=true;}}
                Assert.IsTrue(found,"Real legal paid building "+id+" reason="+W.CanBuild(0,id,point));int cursor=W.Events.Count;
                Paid(new GameCommand{kind=CommandKind.Build,actors=new[]{Builder.id},item=id,point=point},W.EntityCost(0,W.Rules.Entity(id)));
                var building=W.Entities.Last(e=>e.Alive&&e.owner==0&&e.def.id==id);
                Until(()=>building.Complete,150,"Actual public construction "+id);
                Assert.IsTrue(W.Events.Skip(cursor).Any(e=>e.kind=="BuildingComplete"&&e.entity==building.id));return building;
            }
            public EntityState Train(EntityState producer,string id)
            {
                var definition=W.Rules.Entity(id);Assert.IsNotNull(definition,"Actual unit catalog ID "+id);
                Assert.AreEqual(definition.producer,producer.def.id,"Real unit producer "+id);
                Assert.AreEqual("",W.ProductionReason(0,producer,id),"Every real civilization/age/queue/housing/payment gate must pass for "+id);
                if(producer.def.id=="workshop")Assert.Greater(W.AssignedArtisans(producer),0,"A paid workshop queue needs physically present original workers");
                int used=W.Players[0].usedPopulation,reserved=W.Players[0].reservedPopulation,cursor=W.Events.Count;
                Paid(new GameCommand{kind=CommandKind.Train,actors=new[]{producer.id},item=id,count=1},W.UnitCost(0,definition));
                Assert.AreEqual(used,W.Players[0].usedPopulation);Assert.AreEqual(reserved+definition.population,W.Players[0].reservedPopulation);
                Until(()=>W.Events.Skip(cursor).Any(e=>e.kind=="UnitTrained"&&e.entity==producer.id&&e.message==id),definition.time+15,"Actual paid production "+id);
                var trained=W.Events.Skip(cursor).Single(e=>e.kind=="UnitTrained"&&e.entity==producer.id&&e.message==id);
                var unit=W.Get(trained.target);Assert.IsTrue(unit.Alive&&unit.owner==0&&unit.def.id==id);
                Assert.AreEqual(reserved,W.Players[0].reservedPopulation);return unit;
            }
            public void Review(Vector2 destination)
            {var own=Own();Army.Review(own,own.Where(e=>e.Military&&!e.def.waterborne).ToList(),Visible(),destination,true,Search,Water,Revolution,Civilization,Medical);}
            public void ClearOriginalPortGuardians()
            {
                var points=new[]{ConfluenceContent082.Dock,ConfluenceContent082.SouthLanding,ConfluenceContent082.NorthLanding,ConfluenceContent082.NorthMine.Position,ConfluenceContent082.NorthWood(3).Position,new Vector2(49,42),new Vector2(37,61)};
                var touched=W.Entities.Where(e=>e.Alive&&World.Guardian(e)&&points.Any(p=>Vector2.Distance(p,e.origin)<=W.Rules.landmarks.guardLeash+2)).Select(e=>e.id).ToHashSet();
                var ids=W.Treasures.Where(t=>t.guards.Any(touched.Contains)).SelectMany(t=>t.guards).Concat(touched).ToHashSet();
                var guardians=W.Entities.Where(e=>e.Alive&&World.Guardian(e)&&ids.Contains(e.id)).OrderBy(e=>(e.origin-Explorer.position).sqrMagnitude).ThenBy(e=>e.id).ToArray();
                Assert.IsNotEmpty(guardians);int cursor=W.Events.Count;
                foreach(var guardian in guardians)
                {
                    if(!guardian.Alive)continue;Order(Explorer,OrderKind.Move,guardian.position);
                    Until(()=>W.IsVisible(0,guardian),90,"Original explorer physically sees original guardian");
                    Order(Explorer,OrderKind.Attack,target:guardian.id);Until(()=>!guardian.Alive,60,"Real guardian combat");Assert.IsTrue(Explorer.Alive);
                }
                Order(Explorer,OrderKind.Hold);Assert.IsTrue(W.Events.Skip(cursor).Any(e=>e.kind=="Damage"&&World.Guardian(W.Get(e.target))));
            }
            public void PreparePaidOriginalWave()
            {
                ClearOriginalPortGuardians();for(int i=0;i<3;i++)Build("house");
                var dock=Build("dock",ConfluenceContent082.Dock);Ship=Train(dock,"transport_ship");
                var workshop=Build("workshop");var barracks=Build("barracks");
                Assert.AreEqual("highland",W.Players[0].civilization);Assert.IsTrue(W.Rules.Civ("highland").manufacturing);
                var artisans=Own().Where(e=>e.Worker&&e.id!=Builder.id).OrderBy(e=>e.id).Take(2).ToArray();Assert.AreEqual(2,artisans.Length);
                foreach(var worker in artisans)Order(worker,OrderKind.Manufacture,target:workshop.id);
                Until(()=>W.AssignedArtisans(workshop)==2,120,"Original workers physically staff their paid Guild Workshop");
                Assert.IsTrue(artisans.All(e=>e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Manufacture&&e.orders.Peek().target==workshop.id&&W.EdgeDistance(e,workshop)<=1.5f));
                Vector2 rally=Town.position+new Vector2(9,10);
                foreach(var producer in new[]{workshop,barracks})Accepted(W.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{producer.id},point=rally}));
                for(int i=0;i<3;i++)PaidSoldiers.Add(Train(workshop,"culverin").id);
                for(int i=0;i<9;i++)PaidSoldiers.Add(Train(barracks,"musketeer").id);
                Order(Builder,OrderKind.Move,Town.position+new Vector2(8,-5));
                for(int i=0;i<2400&&!W.IsMatchEnded&&(Army.Phase!=AiArmyPhase072.March||Army.GuardMembers.Length<2);i++){Review(W.OpeningTownPosition(1));Step();}
                Assert.AreEqual(AiArmyPhase072.March,Army.Phase);Assert.AreEqual(10,Army.WaveMembers.Length);Assert.AreEqual(2,Army.GuardMembers.Length);
                Assert.IsTrue(Army.WaveMembers.All(PaidSoldiers.Contains));Assert.IsTrue(Army.GuardMembers.All(PaidSoldiers.Contains));
                Assert.AreEqual(3,Army.WaveMembers.Count(id=>W.Get(id).def.id=="culverin"),"Stable paid IDs form a genuine mixed wave with real capacity costs");
            }
        }
        [Serializable] sealed class Evidence
        {
            public int schema=2,originalWave,guardCount,selectedPassengers,capacity,stagingArrived,stagingStillMoving;
            public string scope="Funded Highland AgeIII original-map module strategy fixture; real public paid construction/production and actual muster. Public terrain objective supplied to army Review; fixture uses ordinary Move for same-goal exact logistics in both worlds, including its own final exact Move after production AI Completed. No claim that production AI issues that final Move or of a normal-opening global-Skirmish policy.";
            public string rulesHash,preCommitWorldHash,failure;
            public Vector2 destination,boarding,landing;
            public int[] members;
            public float modeledDirect,modeledTransport,actualLandSeconds,actualFerrySeconds,maxOriginDrift,stagingSeconds;
            public bool committed,landComplete,ferryComplete,ferryExactMoveIssued;
            public int landWinner=-1,ferryWinner=-1;public IntPointSnapshot[] landGoals=Array.Empty<IntPointSnapshot>(),ferryGoals=Array.Empty<IntPointSnapshot>();
        }
        [Serializable] sealed class WorldEvidence084
        {public int schema=1;public string stage,sourceFingerprint,worldSha256,rulesSha256;public float time;}
        static string EvidenceDirectory()
        {
            string output=Environment.GetEnvironmentVariable("FRONTIER084_EVIDENCE");
            if(string.IsNullOrEmpty(output))output=Path.Combine(TestContext.CurrentContext.WorkDirectory,"bridge-positive-selector084");
            Directory.CreateDirectory(output);return output;
        }
        static void WriteWorldEvidence(string stage,World world,WorldSnapshot exact=null)
        {
            string output=EvidenceDirectory(),snapshot=JsonUtility.ToJson(exact??world.CaptureState(),true),rules=JsonUtility.ToJson(world.Rules,true);
            File.WriteAllText(Path.Combine(output,stage+"-world.json"),snapshot);File.WriteAllText(Path.Combine(output,stage+"-rules.json"),rules);
            File.WriteAllText(Path.Combine(output,stage+"-snapshot-receipt.json"),JsonUtility.ToJson(new WorldEvidence084{stage=stage,sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),time=world.Time,worldSha256=SnapshotCodec.Hash(snapshot),rulesSha256=SnapshotCodec.Hash(rules)},true));
            TestContext.WriteLine("Actual bridge selector World saved "+Path.Combine(output,stage+"-world.json"));
        }
        static void Accepted(CommandResult result)=>Assert.IsTrue(result.accepted,result.reason);
        static void Equal(Cost expected,Cost actual)
        {Assert.AreEqual(expected.food,actual.food,.002f);Assert.AreEqual(expected.wood,actual.wood,.002f);Assert.AreEqual(expected.coin,actual.coin,.002f);}
        static void ObserveGoals(World world,int[] ids,Dictionary<int,Vector2> goals,Vector2 objective,ref int eventCursor)
        {
            // Resolve and completion can occur in one World.Tick. Preserve
            // the actual typed resolution event even if that Move dequeued
            // before observation; never infer its endpoint from where it stopped.
            for(;eventCursor<world.Events.Count;eventCursor++)
            {
                var ev=world.Events[eventCursor];
                if(ev.kind=="DestinationAdjusted"&&Array.IndexOf(ids,ev.entity)>=0&&ev.message.StartsWith("Move destination ",StringComparison.Ordinal))goals[ev.entity]=ev.position;
            }
            foreach(int id in ids)
            {
                var e=world.Get(id);if(e==null||!e.Alive||e.garrisonedIn!=0||e.orders.Count==0)continue;
                var order=e.orders.Peek();if(order.kind!=OrderKind.Move)continue;
                Assert.LessOrEqual((order.point-objective).sqrMagnitude,18*18,"No substituted public strategic objective");
                goals[id]=order.hasResolvedPoint?order.resolvedPoint:order.point;
            }
        }
        static bool Arrived(World world,int[] ids,Dictionary<int,Vector2> goals)=>ids.All(id=>
        {var e=world.Get(id);return e!=null&&e.Alive&&e.garrisonedIn==0&&e.orders.Count==0&&goals.TryGetValue(id,out var goal)&&(e.position-goal).sqrMagnitude<=.1f*.1f;});
        [Test] public void RealPaidMixedMarchWaveSelectsFerryAndMeasuresExactMoveLogisticsForSameObjective()
        {
            var f=new Fixture();var w=f.W;World landWorld=null;var landGoals=new Dictionary<int,Vector2>();var ferryGoals=new Dictionary<int,Vector2>();var record=new Evidence{rulesHash=SnapshotCodec.RulesHash(w.Rules)};
            try
            {
                f.PreparePaidOriginalWave();record.originalWave=f.Army.WaveMembers.Length;record.guardCount=f.Army.GuardMembers.Length;
                var originalWave=f.Army.WaveMembers;var guards=f.Army.GuardMembers;
                f.SaveEvidence("00-paid-march-before-south-advance");
                // A normal public advance is interrupted by a new public
                // strategic intent after20 seconds. It does NOT claim that
                // every member arrived. The real selector must price the
                // actual dispersed bodies and their complete boarding legs.
                Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=originalWave,order=OrderKind.Move,point=ConfluenceContent082.SouthLanding+new Vector2(-5,-4)}));
                float stagingAt=w.Time;for(int i=0;i<200;i++)f.Step();record.stagingSeconds=w.Time-stagingAt;
                record.stagingArrived=originalWave.Count(id=>w.Get(id).orders.Count==0);
                record.stagingStillMoving=originalWave.Length-record.stagingArrived;
                f.SaveEvidence("01-actual-partial-south-advance");
                Assert.IsTrue(originalWave.All(id=>w.Get(id).Alive&&w.Get(id).garrisonedIn==0));
                CollectionAssert.AreEqual(originalWave,f.Army.WaveMembers);CollectionAssert.AreEqual(guards,f.Army.GuardMembers);
                record.destination=ConfluenceContent082.NorthLanding;f.Review(record.destination);
                Assert.Less((f.Army.Destination-record.destination).sqrMagnitude,.0001f);
                // This logistics comparison uses the ordinary public Move
                // order on BOTH original worlds. AttackMove can correctly win
                // a battle en route and end a match before a terrain-point
                // journey; that is not an arrived land-route timing sample.
                Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=originalWave,order=OrderKind.Move,point=record.destination}));
                f.SaveEvidence("02-actual-public-north-intent");
                Assert.IsTrue(originalWave.All(id=>w.Get(id).orders.Count>0&&w.Get(id).orders.Peek().kind==OrderKind.Move));
                WorldSnapshot preCommit=null;float nextReview=w.Time;int reviewed=0;
                for(int i=0;i<900&&f.Bridge.Mission==null&&!w.IsMatchEnded;i++)
                {
                    if(w.Time+.001f>=nextReview)
                    {
                        preCommit=SnapshotCodec.Clone(w.CaptureState());f.Bridge.Tick(f.Own(),f.Visible(),false);reviewed++;
                        Assert.LessOrEqual(f.Bridge.LastRouteQueries,6);
                        var c=f.Bridge.Candidate;
                        if(c!=null)for(int n=0;n<c.members.Length;n++)record.maxOriginDrift=Mathf.Max(record.maxOriginDrift,Vector2.Distance(c.origins[n],w.Get(c.members[n]).position));
                        if(f.Bridge.Mission!=null)break;
                        nextReview+=f.Difficulty.DecisionSeconds;
                    }
                    f.Step();
                }
                Assert.IsNotNull(f.Bridge.Mission,"Real selector did not commit after "+reviewed+" reviews; last="+f.Bridge.LastReview+" cursor="+(f.Bridge.Candidate?.cursor??-1));
                WriteWorldEvidence("03-exact-precommit",w,preCommit);f.SaveEvidence("04-actual-selector-committed");
                var mission=SnapshotCodec.Clone(f.Bridge.Mission);record.committed=true;record.members=mission.members;record.selectedPassengers=mission.members.Length;
                record.modeledDirect=mission.directSeconds;record.modeledTransport=mission.transportSeconds;record.boarding=mission.boardingShore;record.landing=mission.landingShore;
                record.capacity=mission.members.Sum(id=>w.TransportSlotCost(w.Get(id)));Assert.LessOrEqual(record.capacity,f.Ship.def.transportCapacity);
                Assert.GreaterOrEqual(record.selectedPassengers,4);Assert.IsTrue(mission.members.All(originalWave.Contains));Assert.IsFalse(mission.members.Any(guards.Contains));
                Assert.GreaterOrEqual(mission.directSeconds-mission.transportSeconds,20);Assert.LessOrEqual(mission.transportSeconds,mission.directSeconds*.8f);
                Assert.IsNotNull(preCommit);record.preCommitWorldHash=SnapshotCodec.Hash(JsonUtility.ToJson(preCommit));
                var land=World.Restore(w.Rules,preCommit);landWorld=land;float start=w.Time;
                int landGoalEventCursor=land.Events.Count,ferryGoalEventCursor=w.Events.Count;
                ObserveGoals(land,mission.members,landGoals,record.destination,ref landGoalEventCursor);nextReview=w.Time+f.Difficulty.DecisionSeconds;
                for(int i=0;i<3600&&(!record.landComplete||!record.ferryComplete);i++)
                {
                    if(!record.ferryComplete&&!w.IsMatchEnded)
                    {
                        if(f.Bridge.Mission!=null&&w.Time+.001f>=nextReview)
                        {f.Bridge.Tick(f.Own(),f.Visible(),false);Assert.LessOrEqual(f.Bridge.LastRouteQueries,6);nextReview+=f.Difficulty.DecisionSeconds;}
                        if(f.Bridge.Mission==null&&!record.ferryExactMoveIssued)
                        {
                            Assert.IsTrue(w.Events.Any(e=>e.kind=="AIBridgeTransportCompleted"&&e.entity==f.Ship.id&&e.time>=mission.startedAt),"A returned/aborted mission cannot be retargeted into a fake success");
                            Assert.IsTrue(mission.members.All(id=>w.Get(id).Alive&&w.Get(id).garrisonedIn==0));
                            // Production correctly resumes AttackMove with its
                            // normal completion radius. This test additionally
                            // pays real time for a public Move to the same
                            // objective so its original0.1m physical arrival
                            // gate remains exact, rather than relaxing it.
                            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=mission.members,order=OrderKind.Move,point=record.destination}));
                            record.ferryExactMoveIssued=true;ferryGoalEventCursor=w.Events.Count;f.SaveEvidence("04b-fixture-public-exact-final-move");
                        }
                        if(record.ferryExactMoveIssued)ObserveGoals(w,mission.members,ferryGoals,record.destination,ref ferryGoalEventCursor);
                        f.Step();if(record.ferryExactMoveIssued)ObserveGoals(w,mission.members,ferryGoals,record.destination,ref ferryGoalEventCursor);
                    }
                    if(!record.landComplete&&!land.IsMatchEnded)
                    {ObserveGoals(land,mission.members,landGoals,record.destination,ref landGoalEventCursor);land.Tick(.1f);ObserveGoals(land,mission.members,landGoals,record.destination,ref landGoalEventCursor);}
                    if(!record.landComplete&&Arrived(land,mission.members,landGoals)){record.landComplete=true;record.actualLandSeconds=land.Time-start;}
                    if(!record.ferryComplete&&f.Bridge.Mission==null&&record.ferryExactMoveIssued&&Arrived(w,mission.members,ferryGoals)){record.ferryComplete=true;record.actualFerrySeconds=w.Time-start;}
                    // A legitimate natural match result is terminal. Preserve
                    // it and judge the actual journey evidence; never forbid
                    // victory, resume a finished world or call it arrival.
                    if(w.IsMatchEnded&&!record.ferryComplete||land.IsMatchEnded&&!record.landComplete)break;
                }
                Assert.IsTrue(record.landComplete,"Paired exact public Move route did not arrive; winner="+land.Winner+" (victory/death/timeout alone is not an arrived timing sample)");
                Assert.IsTrue(record.ferryComplete,"Real cargo did not finish its exact public Move goals; winner="+w.Winner);
                Assert.IsTrue(w.Events.Any(e=>e.kind=="AIBridgeTransportCompleted"&&e.entity==f.Ship.id));
                foreach(int id in mission.members)Assert.IsTrue(w.Events.Any(e=>e.kind=="Disembarked"&&e.entity==id&&e.target==f.Ship.id));
                Assert.IsFalse(w.Events.Any(e=>e.kind=="Embarked"&&guards.Contains(e.entity)));
                f.Review(record.destination);Assert.IsTrue(mission.members.All(f.Army.WaveMembers.Contains));Assert.IsTrue(mission.members.All(id=>!f.Bridge.Owns(id)));
                Assert.GreaterOrEqual(record.actualLandSeconds-record.actualFerrySeconds,20,"Actual full public journeys must keep the original absolute saving");
                Assert.LessOrEqual(record.actualFerrySeconds,record.actualLandSeconds*.8f,"Actual full public journeys must keep the original relative saving");
                Assert.AreEqual(record.rulesHash,SnapshotCodec.RulesHash(w.Rules));
            }
            catch(Exception ex){record.failure=ex.Message;throw;}
            finally
            {
                record.ferryWinner=w.Winner;record.landWinner=landWorld?.Winner??-1;record.landGoals=SnapshotCodec.Points(landGoals);record.ferryGoals=SnapshotCodec.Points(ferryGoals);
                f.SaveEvidence("05-actual-final-or-failure");if(landWorld!=null)WriteWorldEvidence("06-actual-land-final-or-failure",landWorld);
                string output=EvidenceDirectory();File.WriteAllText(Path.Combine(output,"bridge-positive-selector084.json"),JsonUtility.ToJson(record,true));
                TestContext.WriteLine(JsonUtility.ToJson(record,true));
            }
        }
    }
}
