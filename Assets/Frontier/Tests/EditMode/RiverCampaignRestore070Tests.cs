using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Synthetic fault fixtures below test checkpoint validation. They never
    // stand in for the real-command winning paths in RiverContent070Tests.
    public sealed class RiverCampaignRestore070Tests
    {
        RuleCatalog rules;
        [SetUp] public void SetUp(){rules=Catalog.Load();}
        static ScenarioObjectiveState Goal(WorldSnapshot snapshot,string id)=>snapshot.scenarioState.objectiveStates.Single(o=>o.id==id);
        static void Reject(RuleCatalog rules,GuidedPlaySession source,WorldSnapshot snapshot)
        {
            World world=null;string before=null;string original=JsonUtility.ToJson(source.World.CaptureState());
            Assert.Throws<ArgumentException>(()=>
            {
                world=World.Restore(rules,snapshot);var simulation=MatchSimulation.Restore(world,source.Simulation.CaptureState());
                before=JsonUtility.ToJson(world.CaptureState());GuidedPlaySession.Restore(world,simulation,source.CaptureState());
            });
            if(before!=null)Assert.AreEqual(before,JsonUtility.ToJson(world.CaptureState()),"Rejecting a cold chapter must not evaluate goals, create witnesses or change the world");
            Assert.AreEqual(original,JsonUtility.ToJson(source.World.CaptureState()));
        }
        static WorldSnapshot CompletedSurveyFixture(GuidedPlaySession source)
        {
            var snapshot=source.World.CaptureState();snapshot.time=1;
            var goal=Goal(snapshot,"survey");goal.status="completed";goal.reason="Units reached the region";goal.progress=1;goal.elapsed=1;
            snapshot.events=snapshot.events.Concat(new[]{new SimEvent{kind="ScenarioObjectiveCompleted",player=0,time=1,message="survey: "+goal.reason}}).ToArray();
            return snapshot;
        }

        [TestCase(RiverContent070.Scout)][TestCase(RiverContent070.Convoy)][TestCase(RiverContent070.Retreat)]
        public void StatusOnlyCompletionCannotGenerateGuidedWitnessesOrUnlockTheNextChapter(string id)
        {
            using(var source=GuidedPlaySession.Start(rules,id))
            {
                var snapshot=source.World.CaptureState();
                foreach(var goal in snapshot.scenarioDocument.objectives.Where(g=>g.required))Goal(snapshot,goal.id).status="completed";
                Reject(rules,source,snapshot);
                Assert.AreEqual(GuidedPlayStatus.Running,source.Status);Assert.AreEqual(0,source.CaptureState().stageEvents.Length);
                var account=AccountProgressStore.Load(memoryOnly:true);Assert.IsFalse(account.RecordCompletion(source));Assert.AreEqual(0,account.CompletedCount);
            }
        }
        [Test] public void RejectedStatusOnlyColdSlotKeepsItsOriginalBytes()
        {
            string directory=Path.Combine(Path.GetTempPath(),"frontier-authored-restore070-"+Guid.NewGuid().ToString("N"));
            try
            {
                using(var source=GuidedPlaySession.Start(rules,RiverContent070.Scout))
                {
                    var snapshot=source.World.CaptureState();Goal(snapshot,"survey").status="completed";
                    string path=MatchSaveStore.SlotPath(directory,1);
                    MatchSaveStore.Write(path,new SavedMatch{matchId=source.AttemptId,appVersion="restore070-tests",world=snapshot,
                        simulation=source.Simulation.CaptureState(),guided=source.CaptureState(),cameraSize=26,simulationSpeed=1});
                    byte[] original=File.ReadAllBytes(path);var saved=MatchSaveStore.Read(path,"restore070-tests");
                    var world=World.Restore(rules,saved.world);var simulation=MatchSimulation.Restore(world,saved.simulation);
                    Assert.Throws<ArgumentException>(()=>GuidedPlaySession.Restore(world,simulation,saved.guided));
                    CollectionAssert.AreEqual(original,File.ReadAllBytes(path));
                }
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [TestCase("reason")][TestCase("player")][TestCase("time")][TestCase("duplicate")]
        public void TerminalObjectiveMustMatchOneExactExistingResultEvent(string fault)
        {
            using(var source=GuidedPlaySession.Start(rules,RiverContent070.Scout))
            {
                var snapshot=CompletedSurveyFixture(source);var ev=snapshot.events.Last();
                if(fault=="reason")Goal(snapshot,"survey").reason="A different completion reason";
                if(fault=="player")ev.player=1;
                if(fault=="time")ev.time=-1;
                if(fault=="duplicate")snapshot.events=snapshot.events.Concat(new[]{SnapshotCodec.Clone(ev)}).ToArray();
                Reject(rules,source,snapshot);
            }
        }
        [Test] public void AResultEventCannotBeHiddenByChangingTheObjectiveBackToActive()
        {
            using(var source=GuidedPlaySession.Start(rules,RiverContent070.Scout))
            {
                var snapshot=CompletedSurveyFixture(source);var goal=Goal(snapshot,"survey");goal.status="active";goal.reason="";
                Reject(rules,source,snapshot);
            }
        }
        [Test] public void AnActiveLaterGoalCannotBypassItsIncompletePrerequisite()
        {
            using(var source=GuidedPlaySession.Start(rules,RiverContent070.Scout))
            {
                var snapshot=source.World.CaptureState();Goal(snapshot,"blockade").status="active";
                Reject(rules,source,snapshot);
            }
        }
        [Test] public void CompletedGoalCannotPrecedeItsPrerequisiteResultEvent()
        {
            using(var source=GuidedPlaySession.Start(rules,RiverContent070.Scout))
            {
                var snapshot=CompletedSurveyFixture(source);var blockade=Goal(snapshot,"blockade");
                blockade.status="completed";blockade.reason="All bound targets destroyed";blockade.progress=1;blockade.elapsed=1;
                int opening=source.CaptureState().startEventIndex;
                var events=snapshot.events.ToList();events.Insert(opening+1,new SimEvent{kind="ScenarioObjectiveCompleted",player=0,time=0,message="blockade: "+blockade.reason});
                snapshot.events=events.ToArray();Reject(rules,source,snapshot);
            }
        }
        [Test] public void HoldProgressCannotCreateFortySecondsFromAnUnadvancedClock()
        {
            using(var source=GuidedPlaySession.Start(rules,RiverContent070.Retreat))
            {
                var snapshot=source.World.CaptureState();Goal(snapshot,"hold").progress=40;
                Reject(rules,source,snapshot);
            }
        }
        [Test] public void ACompletedObjectiveEventDoesNotReplaceItsRequiredProgress()
        {
            using(var source=GuidedPlaySession.Start(rules,RiverContent070.Scout))
            {
                var snapshot=CompletedSurveyFixture(source);Goal(snapshot,"survey").progress=0;
                Reject(rules,source,snapshot);
            }
        }
        [TestCase(RiverContent070.Scout)][TestCase(RiverContent070.Convoy)][TestCase(RiverContent070.Retreat)]
        public void GenuineInitialColdChapterAdoptsWithoutCreatingAnyEventsOrBodies(string id)
        {
            using(var source=GuidedPlaySession.Start(rules,id))
            {
                var world=World.Restore(rules,source.World.CaptureState());var simulation=MatchSimulation.Restore(world,source.Simulation.CaptureState());
                string before=JsonUtility.ToJson(world.CaptureState());
                using(var cold=GuidedPlaySession.Restore(world,simulation,source.CaptureState()))
                {
                    Assert.AreEqual(before,JsonUtility.ToJson(world.CaptureState()));Assert.AreEqual(GuidedPlayStatus.Running,cold.Status);
                    Assert.AreEqual(source.AttemptId,cold.AttemptId);Assert.AreEqual(0,cold.CaptureState().stageEvents.Length);
                }
            }
        }
    }
}
