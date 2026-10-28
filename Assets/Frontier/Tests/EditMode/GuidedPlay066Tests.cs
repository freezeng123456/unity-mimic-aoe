using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class GuidedPlay066Tests
    {
        static void Accepted(GuidedPlaySession s,GameCommand c)
        {var result=s.Issue(c);Assert.IsTrue(result.accepted,result.reason+" stage="+s.Stage?.Id+" t="+s.World.Time);}
        static void Order(GuidedPlaySession s,int[] actors,OrderKind order,Vector2 point,int target=0)
            =>Accepted(s,new GameCommand{player=0,kind=CommandKind.Order,actors=actors,order=order,point=point,target=target});
        static void Until(GuidedPlaySession s,Func<bool> condition,float seconds,string reason)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds*10)&&!condition()&&s.Status==GuidedPlayStatus.Running;i++)s.Simulation.Step();
            string units=string.Join("; ",s.World.Entities.Where(e=>e.owner==0&&e.Alive&&(e.id==s.CaptureState().transport||s.CaptureState().soldiers.Contains(e.id))).Select(e=>$"{e.id}:{e.status}@{e.position} cargo={e.garrisonedIn}"));
            Assert.IsTrue(condition(),reason+" stage="+s.Stage?.Id+" status="+s.Status+" reason="+s.Reason+" time="+s.World.Time+" units="+units);
        }
        static Vector2 BuildPoint(GuidedPlaySession s,string item)
        {
            var w=s.World;var centre=w.OpeningTownPosition(0)+new Vector2(12,5);
            for(int distance=0;distance<35;distance++)for(int y=-distance;y<=distance;y++)for(int x=-distance;x<=distance;x++)
            {if(Math.Max(Math.Abs(x),Math.Abs(y))!=distance)continue;var p=centre+new Vector2(x,y);if(w.CanBuild(0,item,p)=="")return p;}
            Assert.Fail("No visible paid construction site for "+item);return Vector2.zero;
        }
        static void CompleteCurrent(GuidedPlaySession s)
        {
            var stage=s.Stage;var state=s.CaptureState();var w=s.World;
            switch(stage.Goal)
            {
                case GuidedPlayGoal.Selection:s.ObserveSelection(new[]{state.scout});break;
                case GuidedPlayGoal.Move:Order(s,s.SuggestedActors,OrderKind.Move,s.ObjectivePoint);break;
                case GuidedPlayGoal.GatherWood:
                    var tree=w.Entities.Where(e=>e.Alive&&e.def.resource==ResourceKind.Wood&&e.def.kind==EntityKind.Resource&&w.IsVisible(0,e)).OrderBy(e=>(e.position-w.Get(state.workers[0]).position).sqrMagnitude).First();
                    Order(s,state.workers.Where(id=>w.Get(id).Alive).ToArray(),OrderKind.Gather,tree.position,tree.id);break;
                case GuidedPlayGoal.House:case GuidedPlayGoal.Barracks:
                    string item=stage.Goal==GuidedPlayGoal.House?"house":"barracks";
                    var before=w.Players[0].spent;Accepted(s,new GameCommand{player=0,kind=CommandKind.Build,actors=state.workers,item=item,point=BuildPoint(s,item)});
                    Assert.Greater(w.Players[0].spent.wood,before.wood,"Building must be actually paid");break;
                case GuidedPlayGoal.Age:
                    string choice=w.Rules.ageChoices.First(c=>c.civilization==w.Players[0].civilization&&c.age==2).id;
                    Accepted(s,new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{state.town},choice=choice});break;
                case GuidedPlayGoal.Train:
                    var barracks=w.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="barracks"&&e.Complete);
                    Accepted(s,new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{barracks.id},item="musketeer",count=(int)stage.Required});break;
                case GuidedPlayGoal.SendShipment:
                    string card=w.Players[0].deck.First(id=>w.ShipmentReason(0,id)=="");Accepted(s,new GameCommand{player=0,kind=CommandKind.Ship,item=card});break;
                case GuidedPlayGoal.ShipmentArrival:break;
                case GuidedPlayGoal.DestroyFirst:case GuidedPlayGoal.DestroySecond:
                    int target=stage.Goal==GuidedPlayGoal.DestroyFirst?state.firstTarget:state.secondTarget;
                    var actor=w.Get(state.soldiers[0]);
                    if(!w.IsVisible(0,w.Get(target)))
                    {
                        Order(s,state.soldiers,OrderKind.Move,w.Navigation.NearestWalkable(w.Get(target).position-new Vector2(10,0),actor.def.radius));
                        Until(s,()=>w.IsVisible(0,w.Get(target)),120,"Target must be actually revealed before targeted attack");
                    }
                    Order(s,state.soldiers.Where(id=>w.Get(id).Alive).ToArray(),OrderKind.Attack,w.Get(target).position,target);break;
                case GuidedPlayGoal.Contact:Order(s,new[]{state.scout},OrderKind.Move,state.contactPoint);break;
                case GuidedPlayGoal.TradingPost:
                    Accepted(s,new GameCommand{player=0,kind=CommandKind.Build,actors=state.workers.Where(id=>w.Get(id).Alive&&(w.Get(id).position-state.contactPoint).sqrMagnitude<225).ToArray(),item="trade_post",point=w.Get(state.neutralSite).position});break;
                case GuidedPlayGoal.Alliance:case GuidedPlayGoal.Recruit:
                    Accepted(s,new GameCommand{player=0,kind=stage.Goal==GuidedPlayGoal.Alliance?CommandKind.NeutralAlliance:CommandKind.NeutralRecruit,target=state.neutralSite});break;
                case GuidedPlayGoal.Embark:
                    Accepted(s,new GameCommand{player=0,kind=CommandKind.Embark,actors=state.soldiers,target=state.transport,point=state.boardingPoint});break;
                case GuidedPlayGoal.Land:
                    Accepted(s,new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{state.transport},point=state.landingPoint});break;
                case GuidedPlayGoal.HoldLanding:Order(s,state.soldiers,OrderKind.Hold,state.landingPoint);break;
                case GuidedPlayGoal.Defend:
                    var army=w.Entities.Where(e=>e.Alive&&e.owner==0&&e.Military&&!e.Worker&&e.def.id!="explorer"&&!e.def.waterborne).Select(e=>e.id).ToArray();
                    Order(s,army,OrderKind.AttackMove,w.Get(state.firstTarget).position);break;
                default:Assert.Fail("Missing genuine stage solution");break;
            }
            Until(s,()=>s.Stage!=stage||s.Status==GuidedPlayStatus.Completed,300,"Accepted orders and actual simulation must complete "+stage.Id);
        }
        static void Solve(GuidedPlaySession s)
        {int guard=0;while(s.Status==GuidedPlayStatus.Running&&guard++<20)CompleteCurrent(s);Assert.AreEqual(GuidedPlayStatus.Completed,s.Status,s.Reason);}
        [TestCase("lesson_selection")][TestCase("lesson_economy")][TestCase("lesson_age_army")][TestCase("lesson_shipments")][TestCase("lesson_targeting")][TestCase("lesson_allies")][TestCase("lesson_transport")]
        [TestCase("campaign_supply")][TestCase("campaign_bridge")][TestCase("campaign_estuary")]
        public void EveryScenarioIsSolvedByAcceptedRealOrdersAndWorldResults(string id)
        {
            using(var s=GuidedPlaySession.Start(Catalog.Load(),id))
            {
                var beginning=s.CaptureState();Solve(s);Assert.IsTrue(s.World.Paused);Assert.IsTrue(s.Checklist.All(c=>c.Complete));
                Assert.AreEqual(s.Definition.Seed,s.World.Seed);Assert.Greater(s.CaptureState().commands.Length,0);
                Assert.AreEqual(s.Definition.Stages.Count,s.CaptureState().stageEvents.Length);
                Assert.AreEqual(0,s.Simulation.CaptureState().controllers.Length,"No hidden economic AI controls the authored exercises");
                Assert.IsTrue(s.World.Events.Any(e=>e.kind=="GuidedScenarioSetup"&&e.message==s.StartingSetupDescription));
                if(s.Definition.ScenarioGrant.food+s.Definition.ScenarioGrant.wood+s.Definition.ScenarioGrant.coin>0)Assert.IsTrue(s.World.Events.Any(e=>e.kind=="GuidedScenarioGrant"));
                if(beginning.transport>0)
                {
                    Assert.IsTrue(s.World.Events.Any(e=>e.kind=="Embarked"));Assert.IsTrue(s.World.Events.Any(e=>e.kind=="Disembarked"));
                    Assert.Greater(Vector2.Distance(beginning.boardingPoint,beginning.landingPoint),20);
                    Assert.IsTrue(s.World.IsWater(s.World.Get(beginning.transport).position));
                }
            }
        }
        static GuidedPlaySession PersistAndRestore(GuidedPlaySession original)
        {
            string directory=Path.Combine(Path.GetTempPath(),"frontier-guided-match-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try
            {
                string path=MatchSaveStore.SlotPath(directory,1);
                MatchSaveStore.Write(path,new SavedMatch{matchId=original.AttemptId,appVersion="guided-test",world=original.World.CaptureState(),simulation=original.Simulation.CaptureState(),guided=original.CaptureState(),cameraPosition=Vector3.zero,cameraFocus=Vector2.zero,cameraSize=26,simulationSpeed=1});
                var saved=MatchSaveStore.Read(path,"guided-test");Assert.IsNotNull(saved.guided);
                var world=World.Restore(original.World.Rules,saved.world);var scheduler=MatchSimulation.Restore(world,saved.simulation);
                int eventCount=world.Events.Count;int entityCount=world.Entities.Count;var resources=world.Players[0].resources;
                var restored=GuidedPlaySession.Restore(world,scheduler,saved.guided);
                Assert.AreEqual(eventCount,world.Events.Count);Assert.AreEqual(entityCount,world.Entities.Count);Assert.AreEqual(resources.food,world.Players[0].resources.food);Assert.AreEqual(resources.wood,world.Players[0].resources.wood);Assert.AreEqual(resources.coin,world.Players[0].resources.coin);
                return restored;
            }
            finally{Directory.Delete(directory,true);}
        }
        [TestCase("lesson_selection")][TestCase("lesson_economy")][TestCase("lesson_age_army")][TestCase("lesson_shipments")][TestCase("lesson_targeting")][TestCase("lesson_allies")][TestCase("lesson_transport")]
        [TestCase("campaign_supply")][TestCase("campaign_bridge")][TestCase("campaign_estuary")]
        public void EveryScenarioContinuesAfterActualSlotRoundTripAtAStageBoundary(string id)
        {
            using(var original=GuidedPlaySession.Start(Catalog.Load(),id))
            {
                CompleteCurrent(original);Assert.AreEqual(GuidedPlayStatus.Running,original.Status);original.World.Paused=true;
                using(var restored=PersistAndRestore(original))
                {
                    Assert.AreEqual(original.AttemptId,restored.AttemptId);Assert.AreEqual(original.CaptureState().stageIndex,restored.CaptureState().stageIndex);
                    Assert.AreEqual(JsonUtility.ToJson(original.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));restored.World.Paused=false;Solve(restored);
                }
            }
        }
        [Test] public void QueuedAgeAndPaidArmyRestoreMidJobWithoutAnotherPaymentOrDuplicateBirth()
        {
            using(var original=GuidedPlaySession.Start(Catalog.Load(),"lesson_age_army"))
            {
                var state=original.CaptureState();string choice=original.World.Rules.ageChoices.First(c=>c.civilization=="coast"&&c.age==2).id;
                Accepted(original,new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{state.town},choice=choice});for(int i=0;i<20;i++)original.Simulation.Step();
                float ageFoodSpent=original.World.Players[0].spent.food;original.World.Paused=true;
                using(var age=PersistAndRestore(original))
                {
                    age.World.Paused=false;Until(age,()=>age.Stage.Goal==GuidedPlayGoal.Barracks,180,"Existing age job must finish after restore");Assert.AreEqual(ageFoodSpent,age.World.Players[0].spent.food);
                    CompleteCurrent(age);var barracks=age.World.Entities.First(e=>e.owner==0&&e.def.id=="barracks"&&e.Complete);
                    Accepted(age,new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{barracks.id},item="musketeer",count=2});for(int i=0;i<20;i++)age.Simulation.Step();
                    float paidFood=age.World.Players[0].spent.food;age.World.Paused=true;
                    using(var army=PersistAndRestore(age))
                    {
                        army.World.Paused=false;Until(army,()=>army.Status==GuidedPlayStatus.Completed,90,"Restored paid army queue must actually complete");
                        Assert.AreEqual(paidFood,army.World.Players[0].spent.food);Assert.AreEqual(2,army.World.Events.Count(e=>e.kind=="UnitTrained"&&e.player==0&&e.message=="musketeer"));Assert.AreEqual(2,army.World.Entities.Count(e=>e.Alive&&e.owner==0&&e.def.id=="musketeer"));
                    }
                }
            }
        }
        [Test] public void RejectedCommandsForeignPlayersAndInitialUnitsCannotCompleteStages()
        {
            using(var s=GuidedPlaySession.Start(Catalog.Load(),"lesson_age_army"))
            {
                var initial=s.CaptureState();Assert.IsFalse(s.Issue(new GameCommand{player=1,kind=CommandKind.Resign}).accepted);
                Assert.IsFalse(s.Issue(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{initial.scout},choice="made_up"}).accepted);
                Assert.AreEqual(0,s.CaptureState().commands.Length);Assert.AreEqual(0,s.CaptureState().stageIndex);
                s.World.Players[0].age=2;s.Tick();Assert.AreEqual(0,s.CaptureState().stageIndex,"Changing state alone is not an accepted age command");
            }
        }
        [Test] public void DestroyingCriticalBaseFailsAndRetryReusesSeedWithNewAttempt()
        {
            using(var s=GuidedPlaySession.Start(Catalog.Load(),"campaign_supply"))
            {
                string attempt=s.AttemptId;var state=s.CaptureState();Accepted(s,new GameCommand{player=0,kind=CommandKind.Demolish,actors=new[]{state.town}});
                Assert.AreEqual(GuidedPlayStatus.Failed,s.Status);Assert.IsTrue(s.World.Paused);
                using(var retry=s.Retry()){Assert.AreEqual(s.World.Seed,retry.World.Seed);Assert.AreNotEqual(attempt,retry.AttemptId);Assert.AreEqual(0,retry.CaptureState().stageIndex);Assert.AreEqual(GuidedPlayStatus.Running,retry.Status);}
            }
        }
        [Test] public void ColdRestorePreservesCargoStageAndCommandsWithoutSpawningOrGranting()
        {
            using(var s=GuidedPlaySession.Start(Catalog.Load(),"lesson_transport"))
            {
                CompleteCurrent(s);Assert.AreEqual(GuidedPlayGoal.Land,s.Stage.Goal);s.World.Paused=true;
                var guided=s.CaptureState();var world=World.Restore(s.World.Rules,s.World.CaptureState());var scheduler=MatchSimulation.Restore(world,s.Simulation.CaptureState());
                string original=JsonUtility.ToJson(world.CaptureState());int events=world.Events.Count;
                using(var cold=GuidedPlaySession.Restore(world,scheduler,guided))
                {
                    Assert.AreEqual(original,JsonUtility.ToJson(world.CaptureState()));Assert.AreEqual(events,world.Events.Count);Assert.AreEqual(s.AttemptId,cold.AttemptId);
                    Assert.IsTrue(guided.soldiers.All(id=>world.Get(id).garrisonedIn==guided.transport));world.Paused=false;Solve(cold);
                }
            }
        }
        [Test] public void AlteredScenarioStageCommandWitnessAndFutureRevisionAreRejectedWithoutMutation()
        {
            using(var s=GuidedPlaySession.Start(Catalog.Load(),"lesson_selection"))
            {
                CompleteCurrent(s);var state=s.CaptureState();string original=JsonUtility.ToJson(s.World.CaptureState());
                foreach(Action<GuidedPlaySnapshot> corrupt in new Action<GuidedPlaySnapshot>[] {x=>x.catalogRevision++,x=>x.definitionId="missing",x=>x.stageIndex++,x=>x.movePoint+=Vector2.one,x=>x.stageEvents[0]=x.startEventIndex})
                {var invalid=SnapshotCodec.Clone(state);corrupt(invalid);Assert.Throws<ArgumentException>(()=>GuidedPlaySession.Restore(s.World,s.Simulation,invalid));Assert.AreEqual(original,JsonUtility.ToJson(s.World.CaptureState()));}
            }
        }
        [Test] public void DisposalStopsObservationAndScenarioCompletionIsIndependentFromWorldWinner()
        {
            var s=GuidedPlaySession.Start(Catalog.Load(),"lesson_selection");Solve(s);Assert.AreEqual(-1,s.World.Winner,"Lesson objective is separate from elimination victory");
            var terminal=s.CaptureState();s.Dispose();s.Tick();Assert.AreEqual(terminal.revision,s.Revision);
        }
        [Test] public void ColdCompletedResultCanBeRecordedOnlyOnceAndUnlocksNextCampaignChapter()
        {
            using(var first=GuidedPlaySession.Start(Catalog.Load(),"campaign_supply"))
            {
                Solve(first);var store=AccountProgressStore.Load(memoryOnly:true);Assert.IsFalse(store.IsUnlocked("campaign_bridge"));
                Assert.IsTrue(store.RecordCompletion(first),store.LastError);Assert.IsTrue(store.RecordCompletion(first),store.LastError);Assert.AreEqual(1,store.CompletionCount(first.Definition.Id));Assert.IsTrue(store.IsUnlocked("campaign_bridge"));
                var world=World.Restore(first.World.Rules,first.World.CaptureState());var sim=MatchSimulation.Restore(world,first.Simulation.CaptureState());
                using(var restored=GuidedPlaySession.Restore(world,sim,first.CaptureState())){Assert.IsTrue(store.RecordCompletion(restored),store.LastError);Assert.AreEqual(1,store.CompletionCount(first.Definition.Id));}
                using(var second=GuidedPlaySession.Start(Catalog.Load(),first.NextId)){Solve(second);Assert.IsTrue(store.RecordCompletion(second),store.LastError);Assert.IsTrue(store.IsUnlocked("campaign_estuary"));}
            }
        }
        [Test] public void DurableAccountKeepsBestTimeAndMalformedUnknownOrFutureFilesAreNeverOverwritten()
        {
            string directory=Path.Combine(Path.GetTempPath(),"frontier-guided-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try
            {
                var store=AccountProgressStore.Load(directory);Assert.IsFalse(store.MemoryOnly);
                using(var s=GuidedPlaySession.Start(Catalog.Load(),"lesson_selection")){Solve(s);Assert.IsTrue(store.RecordCompletion(s),store.LastError);Assert.IsTrue(store.RecordCompletion(s));Assert.AreEqual(s.CompletedSeconds,AccountProgressStore.Load(directory).BestSeconds(s.Definition.Id));}
                byte[] valid=File.ReadAllBytes(store.FilePath);
                string[] invalid={"{broken}","{\"schema\":2,\"payload\":\"\",\"sha256\":\"\"}","{\"schema\":1,\"schema\":1}","{\"schema\":1,\"unknown\":0}",AccountJson("{\"schema\":1,\"catalogRevision\":1,\"records\":[{\"id\":\"future_chapter\",\"lastAttemptId\":\"once\",\"completions\":1,\"bestSeconds\":5,\"attempts\":[\"once\"]}]}"),AccountJson("{\"schema\":2,\"catalogRevision\":1,\"records\":[]}")};
                foreach(string json in invalid)
                {
                    File.WriteAllText(store.FilePath,json);byte[] before=File.ReadAllBytes(store.FilePath);var blocked=AccountProgressStore.Load(directory);Assert.IsFalse(blocked.CanWrite);Assert.IsNotEmpty(blocked.LastError);
                    using(var s=GuidedPlaySession.Start(Catalog.Load(),"lesson_selection")){Solve(s);Assert.IsFalse(blocked.RecordCompletion(s));}CollectionAssert.AreEqual(before,File.ReadAllBytes(store.FilePath));
                }
                File.WriteAllBytes(store.FilePath,valid);Assert.IsTrue(AccountProgressStore.Load(directory).CanWrite);Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp-*"));
            }
            finally{Directory.Delete(directory,true);}
        }
        static string AccountJson(string payload)=>JsonUtility.ToJson(new TestAccountEnvelope{schema=1,payload=payload,sha256=SnapshotCodec.Hash(payload)});
        [Serializable] sealed class TestAccountEnvelope {public int schema;public string payload,sha256;}
        [Test] public void UnknownIdsFailedResultsAndOutOfOrderCampaignCannotUnlockProgress()
        {
            var store=AccountProgressStore.Load(memoryOnly:true);Assert.IsFalse(store.IsUnlocked("missing"));Assert.AreEqual(-1,store.BestSeconds("missing"));
            using(var running=GuidedPlaySession.Start(Catalog.Load(),"lesson_selection"))Assert.IsFalse(store.RecordCompletion(running));
            using(var skipped=GuidedPlaySession.Start(Catalog.Load(),"campaign_bridge")){Solve(skipped);Assert.IsFalse(store.RecordCompletion(skipped));Assert.IsFalse(store.IsUnlocked("campaign_estuary"));}
        }
    }
}
