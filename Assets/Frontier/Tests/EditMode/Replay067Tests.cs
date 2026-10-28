using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // These fixtures compare the complete live state with a newly simulated
    // replay. No entity/resource grants or direct world mutations solve them.
    public sealed class Replay067Tests
    {
        string directory;
        [SetUp] public void SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"Frontier-replay-behavior-"+Guid.NewGuid().ToString("N"));
            Assert.IsFalse(Directory.Exists(directory));Directory.CreateDirectory(directory);
        }
        [TearDown] public void TearDown(){if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);}
        static ReplayIdentity Identity(RuleCatalog rules)=>ReplayIdentity.Create(rules,"replay-behavior-test","synthetic-build-067","synthetic-unity-067");
        sealed class Expected
        {
            public readonly int step;public readonly string world,simulation,guided,digest;
            public Expected(MatchSimulation sim,GuidedPlaySession lesson=null)
            {
                step=sim.StepCount;world=JsonUtility.ToJson(sim.World.CaptureState());
                var state=sim.CaptureState();state.accumulator=0;simulation=JsonUtility.ToJson(state);
                guided=lesson==null?null:JsonUtility.ToJson(lesson.CaptureState());digest=ReplayDigest.Compute(sim.World,sim,lesson);
            }
            public void Check(ReplaySession replay)
            {
                Assert.IsEmpty(replay.Error,"Replay must not silently stop at a divergence");Assert.AreEqual(step,replay.CurrentStep);
                Assert.AreEqual(world,JsonUtility.ToJson(replay.World.CaptureState()),"World, resources, queues, units, fog and event history at step "+step);
                var state=replay.Simulation.CaptureState();state.accumulator=0;
                Assert.AreEqual(simulation,JsonUtility.ToJson(state),"AI timers, memories and orders at step "+step);
                Assert.AreEqual(guided,replay.Guided==null?null:JsonUtility.ToJson(replay.Guided.CaptureState()),"Guided command witnesses and stages at step "+step);
                Assert.AreEqual(digest,ReplayDigest.Compute(replay.World,replay.Simulation,replay.Guided));
            }
        }
        static void Accepted(ReplayRecorder recorder,GameCommand command)
        {var result=recorder.Issue(command);Assert.IsTrue(result.accepted,result.reason+" step="+recorder.Simulation.StepCount);}
        static void Steps(MatchSimulation simulation,int count)
        {for(int i=0;i<count;i++)Assert.IsTrue(simulation.Step(),"Simulation stopped at "+simulation.StepCount);}
        static Vector2 BuildPoint(World world,string item)
        {
            var centre=world.OpeningTownPosition(0)+new Vector2(12,5);
            for(int distance=0;distance<35;distance++)for(int y=-distance;y<=distance;y++)for(int x=-distance;x<=distance;x++)
            {if(Math.Max(Math.Abs(x),Math.Abs(y))!=distance)continue;var point=centre+new Vector2(x,y);if(world.CanBuild(0,item,point)=="")return point;}
            Assert.Fail("No visible legal site for a paid "+item);return Vector2.zero;
        }
        static EntityState Wood(World world,int worker)=>world.Entities.Where(e=>e.Alive&&e.def.kind==EntityKind.Resource&&e.def.resource==ResourceKind.Wood&&world.IsVisible(0,e))
            .OrderBy(e=>(e.position-world.Get(worker).position).sqrMagnitude).First();
        static GameCommand Gather(World world,int[] workers)
        {var tree=Wood(world,workers[0]);return new GameCommand{player=0,kind=CommandKind.Order,actors=workers,order=OrderKind.Gather,target=tree.id,point=tree.position};}
        static void PlayToEnd(ReplaySession replay,int budget)
        {
            int guard=0;while(replay.CurrentStep<replay.EndStep&&string.IsNullOrEmpty(replay.Error)&&guard++<100000)
                Assert.Greater(replay.Advance(budget),0,"Replay failed to advance at "+replay.CurrentStep);
            Assert.IsEmpty(replay.Error);Assert.AreEqual(replay.EndStep,replay.CurrentStep);Assert.IsTrue(replay.Verified);
        }
        ReplayDocument Cold(ReplayDocument document,RuleCatalog rules,ReplayIdentity identity)
        {
            string path=ReplayStore.PathFor(directory,document.segmentId);ReplayStore.Write(path,document,rules,identity);
            Assert.Greater(new FileInfo(path).Length,0);var loaded=ReplayStore.Read(path,rules,identity);
            Assert.AreEqual(JsonUtility.ToJson(document),JsonUtility.ToJson(loaded),"Actual stored event-rich document must round-trip");return loaded;
        }

        [TestCase("generated_river",false)]
        [TestCase("generated_delta",true)]
        public void PaidFourSlotEconomyAndThreeAisReconstructAcrossBatchesColdReadAndBothSeekDirections(string map,bool ffa)
        {
            var rules=Catalog.Load();var identity=Identity(rules);
            var world=new World(rules,314159,new[]{"coast","river","france","ottoman"},ffa?new[]{0,1,2,3}:new[]{0,1,0,1},5,map);
            var simulation=new MatchSimulation(world,new[]{1,2,3},"easy");var expected=new Dictionary<int,Expected>();
            using(var recorder=new ReplayRecorder(simulation,identity,segmentId:"four-slots-"+(ffa?"ffa":"teams"),anchorInterval:60))
            {
                var town=world.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="town");
                int[] workers=world.Entities.Where(e=>e.Alive&&e.owner==0&&e.Worker).Select(e=>e.id).ToArray();
                var before=world.Players[0].spent;
                Accepted(recorder,new GameCommand{player=0,kind=CommandKind.Build,actors=workers.Take(3).ToArray(),item="house",point=BuildPoint(world,"house")});
                Accepted(recorder,new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item=world.WorkerDefinition(0).id,count=1});
                Accepted(recorder,Gather(world,workers.Skip(3).ToArray()));
                Assert.Greater(world.Players[0].spent.wood,before.wood);Assert.Greater(world.Players[0].spent.food,before.food);
                for(int step=1;step<=600;step++)
                {
                    Assert.IsTrue(simulation.Step());
                    if(step==299)Accepted(recorder,Gather(world,workers.Take(3).ToArray()));
                    if(step==40||step==137||step==299||step==600)expected.Add(step,new Expected(simulation));
                }
                Assert.IsTrue(world.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.id=="house"&&e.Complete));
                Assert.AreEqual(1,world.Events.Count(e=>e.kind=="UnitTrained"&&e.player==0&&e.message==world.WorkerDefinition(0).id));
                Assert.IsTrue(world.Events.Any(e=>e.kind=="Gather"&&e.player==0));
                foreach(int player in new[]{1,2,3})Assert.Greater(world.Players[player].spent.food+world.Players[player].spent.wood+world.Players[player].spent.coin,0,"AI slot "+player+" must really spend and act");
                var document=Cold(recorder.Finish(),rules,identity);Assert.Greater(document.events.Length,100);
                Assert.IsTrue(document.anchors.All(a=>a.world.events.Length==0),"Event ledger is stored once, not copied into every seek point");
                foreach(int budget in new[]{1,7,37})using(var replay=new ReplaySession(rules,document,identity)){PlayToEnd(replay,budget);expected[600].Check(replay);}
                using(var replay=new ReplaySession(rules,document,identity))
                {
                    foreach(int target in new[]{299,40,600,137}){Assert.IsTrue(replay.Seek(target),replay.Error);expected[target].Check(replay);}
                    Assert.IsTrue(replay.BeginSeek(299),replay.Error);Assert.AreEqual(240,replay.SeekAnchorStep);int iterations=0;
                    while(replay.IsSeeking&&iterations++<100){int advanced=replay.AdvanceSeek(3);Assert.That(advanced,Is.InRange(0,3));}
                    Assert.IsFalse(replay.IsSeeking);Assert.AreEqual(59,replay.LastSeekSimulatedSteps);expected[299].Check(replay);PlayToEnd(replay,19);expected[600].Check(replay);
                    Assert.AreEqual(!ffa,replay.World.AreAllies(0,2));Assert.AreEqual(4,replay.World.Players.Length);
                }
            }
        }

        [Test] public void SameBoundaryPauseRejectedCommandsAndMutableArgumentsKeepTheirOriginalOrderAndPayment()
        {
            var rules=Catalog.Load();var identity=Identity(rules);var world=new World(rules,91,"coast","river",3);var simulation=new MatchSimulation(world);
            using(var recorder=new ReplayRecorder(simulation,identity,segmentId:"boundary-order",anchorInterval:25))
            {
                int scout=world.Entities.First(e=>e.owner==0&&e.def.id=="explorer").id;
                int town=world.Entities.First(e=>e.owner==0&&e.def.id=="town").id;
                int[] actors={scout};var destination=world.Get(scout).position+new Vector2(4,2);
                var move=new GameCommand{player=0,kind=CommandKind.Order,actors=actors,order=OrderKind.Move,point=destination};Accepted(recorder,move);
                actors[0]=town;move.point=Vector2.zero;move.order=OrderKind.Hold;
                var spent=world.Players[0].spent;recorder.Pause(true,"manual_pause");Assert.IsFalse(simulation.Step());
                Assert.IsFalse(recorder.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town},item=world.WorkerDefinition(0).id,count=1}).accepted);
                Assert.IsFalse(recorder.Issue(null).accepted);Assert.AreEqual(spent.food,world.Players[0].spent.food);
                recorder.Pause(false,"manual_resume");
                Accepted(recorder,new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town},item=world.WorkerDefinition(0).id,count=1});
                Steps(simulation,300);var expected=new Expected(simulation);var document=Cold(recorder.Finish(),rules,identity);
                Assert.AreEqual(6,document.records.Length);Assert.IsTrue(document.records.All(r=>r.completedStep==0));
                CollectionAssert.AreEqual(Enumerable.Range(0,6),document.records.Select(r=>r.ordinal));
                CollectionAssert.AreEqual(new[]{scout},document.records[0].command.actors);Assert.AreEqual(destination,document.records[0].command.point);
                Assert.AreEqual(OrderKind.Move,document.records[0].command.order);Assert.IsFalse(document.records[2].accepted);Assert.IsFalse(document.records[3].hasCommand);
                Assert.AreEqual(1,world.Events.Count(e=>e.kind=="UnitTrained"&&e.player==0));
                using(var replay=new ReplaySession(rules,document,identity)){PlayToEnd(replay,8);expected.Check(replay);Assert.IsFalse(replay.World.Paused);}
            }
        }

        [Test] public void FlushConsumesARealUnpublishedIncomeBucketAndIsReplayedWithoutDoubleCrediting()
        {
            var rules=Catalog.Load();var identity=Identity(rules);var world=new World(rules,93,"coast","river",3);var simulation=new MatchSimulation(world);
            using(var recorder=new ReplayRecorder(simulation,identity,segmentId:"income-flush",anchorInterval:50))
            {
                int[] workers=world.Entities.Where(e=>e.Alive&&e.owner==0&&e.Worker).Select(e=>e.id).ToArray();Accepted(recorder,Gather(world,workers));
                int guard=0;while(!world.CaptureState().incomes.Any(i=>i.amount>0)&&guard++<1000)Steps(simulation,1);
                Assert.IsTrue(world.CaptureState().incomes.Any(i=>i.amount>0),"Use actual harvesting, not a manually injected bucket");
                float resource=world.Players[0].resources.wood,time=world.Time;int events=world.Events.Count;
                recorder.FlushEconomy();Assert.Greater(world.Events.Count,events);Assert.IsTrue(world.Events.Skip(events).Any(e=>e.kind=="Gather"&&e.amount>0));
                Assert.AreEqual(resource,world.Players[0].resources.wood);Assert.AreEqual(time,world.Time);
                int firstFlushEvents=world.Events.Count;recorder.FlushEconomy();Assert.AreEqual(firstFlushEvents,world.Events.Count);
                var atFlush=new Expected(simulation);Steps(simulation,47);var final=new Expected(simulation);var document=recorder.Finish();
                Assert.AreEqual(2,document.records.Count(r=>r.kind==ReplayOperation.FlushEconomy));
                using(var replay=new ReplaySession(rules,Cold(document,rules,identity),identity))
                {Assert.IsTrue(replay.Seek(atFlush.step),replay.Error);atFlush.Check(replay);PlayToEnd(replay,5);final.Check(replay);}
            }
        }

        [Test] public void RenderAccumulatorAndFrameSpeedDoNotChangeFixedStepReplayOrAiDecisions()
        {
            var rules=Catalog.Load();var identity=Identity(rules);var direct=new MatchSimulation(new World(rules,123,"coast","river",3),new[]{0,1},"easy");
            var framed=new MatchSimulation(new World(rules,123,"coast","river",3),new[]{0,1},"easy");
            using(var recorder=new ReplayRecorder(framed,identity,segmentId:"frame-batches",anchorInterval:31))
            {
                Steps(direct,240);float[] frames={.016f,.016f,.04f,.4f,.8f,.033f};int index=0;
                while(framed.StepCount<240)
                {
                    float remainder=framed.CaptureState().accumulator;
                    float budget=Mathf.Min(frames[index++%frames.Length],(240-framed.StepCount)*MatchSimulation.StepSeconds-remainder);
                    framed.AdvanceFrame(Mathf.Max(.000001f,budget));Assert.Less(index,10000);
                }
                Assert.AreEqual(240,framed.StepCount);framed.AdvanceFrame(.017f);Assert.AreEqual(240,framed.StepCount);
                Assert.Greater(framed.CaptureState().accumulator,0);Assert.AreEqual(new Expected(direct).world,new Expected(framed).world);
                Assert.AreEqual(ReplayDigest.Compute(direct.World,direct),ReplayDigest.Compute(framed.World,framed));
                var expected=new Expected(framed);using(var replay=new ReplaySession(rules,recorder.Finish(),identity)){PlayToEnd(replay,23);expected.Check(replay);}
            }
        }

        [Test] public void LoadedMatchStartsANewMonotonicSegmentAtItsActualStepAndCapturePrefixesStayIndependent()
        {
            var rules=Catalog.Load();var identity=Identity(rules);var original=new MatchSimulation(new World(rules,137,"coast","river",3),1);Steps(original,83);
            var world=World.Restore(rules,SnapshotCodec.Clone(original.World.CaptureState()));var simulation=MatchSimulation.Restore(world,SnapshotCodec.Clone(original.CaptureState()));
            using(var recorder=new ReplayRecorder(simulation,identity,segmentId:"loaded-segment",anchorInterval:40,matchId:"original-match",startKind:"load"))
            {
                Steps(simulation,17);int scout=world.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="explorer").id;
                Accepted(recorder,new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{scout},order=OrderKind.Move,point=world.Get(scout).position+new Vector2(5,2)});
                var prefix=recorder.Capture("manual_save");string frozen=JsonUtility.ToJson(prefix);var at100=new Expected(simulation);
                Steps(simulation,65);recorder.FlushEconomy();var expected=new Expected(simulation);var document=Cold(recorder.Finish("menu_exit"),rules,identity);
                Assert.AreEqual(frozen,JsonUtility.ToJson(prefix),"A later frame or flush cannot mutate an already offered save prefix");
                Assert.AreEqual(83,document.anchors[0].completedStep);Assert.AreEqual(165,document.anchors.Last().completedStep);
                Assert.AreEqual("load",document.startKind);Assert.AreEqual("original-match",document.matchId);Assert.IsTrue(document.records.All(r=>r.completedStep>=83));
                using(var replay=new ReplaySession(rules,document,identity))
                {Assert.AreEqual(83,replay.StartStep);PlayToEnd(replay,11);expected.Check(replay);Assert.IsTrue(replay.Seek(100),replay.Error);at100.Check(replay);}
                using(var replay=new ReplaySession(rules,prefix,identity)){PlayToEnd(replay,7);at100.Check(replay);Assert.AreEqual(100,replay.EndStep);}
            }
        }

        [Test] public void GuidedSelectionAndActualMovementReplayTheCompletedLessonIncludingItsAutomaticPause()
        {
            var rules=Catalog.Load();var identity=Identity(rules);
            using(var lesson=GuidedPlaySession.Start(rules,"lesson_selection","replay-selection-attempt"))
            using(var recorder=new ReplayRecorder(lesson.Simulation,identity,lesson,"guided-selection",10))
            {
                Steps(lesson.Simulation,5);var beforeSelection=new Expected(lesson.Simulation,lesson);Steps(lesson.Simulation,1);
                int[] selected=lesson.SuggestedActors.ToArray();int scout=selected[0];recorder.ObserveSelection(selected);selected[0]=lesson.CaptureState().town;
                Assert.AreEqual(GuidedPlayGoal.Move,lesson.Stage.Goal);
                Accepted(recorder,new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{scout},order=OrderKind.Move,point=lesson.ObjectivePoint});
                int guard=0;while(lesson.Status==GuidedPlayStatus.Running&&guard++<1000)Steps(lesson.Simulation,1);
                Assert.AreEqual(GuidedPlayStatus.Completed,lesson.Status);Assert.IsTrue(lesson.World.Paused);Assert.Greater(lesson.CompletedSeconds,0);
                Assert.AreEqual(-1,lesson.World.Winner,"Lesson completion is not a fabricated elimination win");
                var expected=new Expected(lesson.Simulation,lesson);var document=Cold(recorder.Finish("guided_completed"),rules,identity);
                var selection=document.records.Single(r=>r.kind==ReplayOperation.GuidedSelection);CollectionAssert.AreEqual(new[]{scout},selection.selection);
                var command=document.records.Single(r=>r.kind==ReplayOperation.Command);Assert.AreEqual(selection.completedStep,command.completedStep);Assert.Less(selection.ordinal,command.ordinal);
                using(var replay=new ReplaySession(rules,document,identity))
                {PlayToEnd(replay,17);expected.Check(replay);Assert.IsTrue(replay.Seek(5),replay.Error);beforeSelection.Check(replay);Assert.AreEqual(GuidedPlayGoal.Selection,replay.Guided.Stage.Goal);PlayToEnd(replay,1);expected.Check(replay);}
            }
        }

        static void SupplyStage(ReplayRecorder recorder)
        {
            var lesson=recorder.Guided;var stage=lesson.Stage;var state=lesson.CaptureState();var world=lesson.World;
            switch(stage.Goal)
            {
                case GuidedPlayGoal.GatherWood:Accepted(recorder,Gather(world,state.workers));break;
                case GuidedPlayGoal.House:case GuidedPlayGoal.Barracks:
                    string item=stage.Goal==GuidedPlayGoal.House?"house":"barracks";
                    Accepted(recorder,new GameCommand{player=0,kind=CommandKind.Build,actors=state.workers,item=item,point=BuildPoint(world,item)});break;
                case GuidedPlayGoal.Train:
                    int barracks=world.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="barracks"&&e.Complete).id;
                    Accepted(recorder,new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{barracks},item="musketeer",count=(int)stage.Required});break;
                default:Assert.Fail("Unexpected campaign stage "+stage.Goal);break;
            }
            int guard=0;while(lesson.Stage==stage&&lesson.Status==GuidedPlayStatus.Running&&guard++<3000)Steps(lesson.Simulation,1);
            Assert.AreNotSame(stage,lesson.Stage,"Real gather, paid construction and queue completion must advance "+stage.Id);
            Assert.AreEqual(GuidedPlayStatus.Running,lesson.Status,lesson.Reason);
        }
        [Test] public void PaidCampaignArmyTriggersOneRealRaidAfterGuidedCallbacksAndSeekDoesNotDuplicateIt()
        {
            var rules=Catalog.Load();var identity=Identity(rules);
            using(var lesson=GuidedPlaySession.Start(rules,"campaign_supply","replay-supply-attempt"))
            using(var recorder=new ReplayRecorder(lesson.Simulation,identity,lesson,"guided-raid",75))
            {
                int setupGrants=lesson.World.Events.Count(e=>e.kind=="GuidedScenarioGrant");Assert.Greater(setupGrants,0,"The authored controlled setup is explicit in the ledger");
                var initialSpent=lesson.World.Players[0].spent;Expected beforeRaid=null;int guard=0;
                while(lesson.Stage.Goal!=GuidedPlayGoal.Defend&&guard++<10)
                {if(lesson.Stage.Goal==GuidedPlayGoal.Train){beforeRaid=new Expected(lesson.Simulation,lesson);Steps(lesson.Simulation,1);}SupplyStage(recorder);}
                Assert.IsNotNull(beforeRaid);Assert.AreEqual(GuidedPlayGoal.Defend,lesson.Stage.Goal);
                Assert.AreEqual(1,lesson.World.Events.Count(e=>e.kind=="GuidedRaid"));
                Assert.AreEqual(4,lesson.World.Events.Count(e=>e.kind=="UnitTrained"&&e.player==0&&e.message=="musketeer"));
                Assert.Greater(lesson.World.Players[0].spent.food,initialSpent.food);Assert.Greater(lesson.World.Players[0].spent.wood,initialSpent.wood);
                var state=lesson.CaptureState();Vector2 raiderAtStart=lesson.World.Get(state.firstTarget).position;Steps(lesson.Simulation,80);
                Assert.Greater(Vector2.Distance(raiderAtStart,lesson.World.Get(state.firstTarget).position),1,"The generated raid must execute movement, not merely emit a marker");
                var expected=new Expected(lesson.Simulation,lesson);var document=Cold(recorder.Finish(),rules,identity);
                using(var replay=new ReplaySession(rules,document,identity))
                {
                    PlayToEnd(replay,31);expected.Check(replay);Assert.IsTrue(replay.Seek(beforeRaid.step),replay.Error);beforeRaid.Check(replay);
                    PlayToEnd(replay,3);expected.Check(replay);Assert.AreEqual(1,replay.World.Events.Count(e=>e.kind=="GuidedRaid"));
                    Assert.AreEqual(setupGrants,replay.World.Events.Count(e=>e.kind=="GuidedScenarioGrant"));
                }
            }
        }

        [Test] public void ChangedCommandOutcomeReportsItsExactBoundaryAndCannotContinueSilently()
        {
            var rules=Catalog.Load();var identity=Identity(rules);var world=new World(rules,177,"coast","river",3);var simulation=new MatchSimulation(world);
            ReplayDocument document;
            using(var recorder=new ReplayRecorder(simulation,identity,segmentId:"divergence",anchorInterval:30))
            {
                Steps(simulation,7);int town=world.Entities.First(e=>e.owner==0&&e.def.id=="town").id;
                Accepted(recorder,new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town},item=world.WorkerDefinition(0).id,count=1});Steps(simulation,40);document=recorder.Finish();
            }
            var changed=SnapshotCodec.Clone(document);changed.records[0].accepted=false;
            var tamper=Assert.Throws<ArgumentException>(()=>new ReplaySession(rules,changed,identity));StringAssert.Contains("input ledger digest mismatch",tamper.Message);
            // Re-authenticate the altered input ledger to exercise the deeper
            // semantic check, independently of the storage/checksum rejection.
            // The actual recorded world, events and outcomes remain unaltered.
            string inputHash=SnapshotCodec.Hash("frontier-replay-inputs-v1");int input=0;
            foreach(var anchor in changed.anchors)
            {while(input<anchor.recordIndex)inputHash=SnapshotCodec.Hash(inputHash+"\n"+JsonUtility.ToJson(changed.records[input++]));anchor.inputHash=inputHash;}
            ReplayValidation.Validate(rules,changed,identity);
            using(var replay=new ReplaySession(rules,changed,identity))
            {
                replay.Advance(47);Assert.AreEqual("Replay mismatch: command outcome at step 7 ordinal 0",replay.Error);
                Assert.IsFalse(replay.Verified);int stopped=replay.CurrentStep;float time=replay.World.Time;
                Assert.AreEqual(0,replay.Advance(47));Assert.AreEqual(stopped,replay.CurrentStep);Assert.AreEqual(time,replay.World.Time);
            }
        }

        [Test] public void UnrepresentableRejectedInputFreezesAPlayablePrefixWhileTheLiveMatchKeepsWorking()
        {
            var rules=Catalog.Load();var identity=Identity(rules);var world=new World(rules,179,"coast","river",3);var simulation=new MatchSimulation(world);
            using(var recorder=new ReplayRecorder(simulation,identity,segmentId:"bounded-prefix",anchorInterval:20))
            {
                Steps(simulation,30);var expected=new Expected(simulation);
                Assert.IsFalse(recorder.Issue(new GameCommand{player=0,kind=CommandKind.Order,point=new Vector2(float.NaN,0)}).accepted);
                Assert.IsNotEmpty(recorder.Error);Assert.IsFalse(recorder.IsRecording);
                int town=world.Entities.First(e=>e.owner==0&&e.def.id=="town").id;
                Accepted(recorder,new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town},item=world.WorkerDefinition(0).id,count=1});Steps(simulation,250);
                Assert.AreEqual(1,world.Events.Count(e=>e.kind=="UnitTrained"&&e.player==0),"Recording failure must not prevent legitimate live play");
                var document=Cold(recorder.Finish(),rules,identity);Assert.IsTrue(document.incomplete);Assert.IsNotEmpty(document.incompleteReason);
                Assert.AreEqual(30,document.anchors.Last().completedStep);Assert.IsEmpty(document.records);
                Assert.IsFalse(document.events.Any(e=>e.kind=="UnitTrained"&&e.player==0),"A truncated recording cannot claim future unrecorded gameplay");
                using(var replay=new ReplaySession(rules,document,identity)){PlayToEnd(replay,5);expected.Check(replay);}
            }
        }
    }
}
