using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ScenarioRevolutionBinding071Tests
    {
        sealed class Fixture
        {
            public World world,cold;
            public MatchSimulation simulation,coldSimulation;
            public ReplayRecorder recorder;
            public int town,lead,authored,deadBefore,deadAfter,guard;
            public PlayerState Player=>world.Players[0];
        }
        static Fixture Prepared(string civilization="coast",bool overrideDocumentCivilization=false)
        {
            var rules=SnapshotCodec.Clone(Catalog.Load());string worker=rules.Civ(civilization).workerUnit;
            var document=new ScenarioDocument{id="scenario-revolution-binding071",name="Authored political identity fixture",rulesHash=SnapshotCodec.RulesHash(rules),
                seed=314159,enableElimination=true,starts=new[]{
                    new ScenarioStart{player=0,team=0,civilization=overrideDocumentCivilization?"coast":civilization,age=4,town=new Vector2(22,24),explorer=new Vector2(35,24),
                        workers=new[]{new Vector2(12,18),new Vector2(14,16),new Vector2(17,14),new Vector2(26,14),new Vector2(30,16),new Vector2(32,18)},initialResources=new Cost(10000,10000,10000)},
                    new ScenarioStart{player=1,team=1,civilization="coast",age=1,town=new Vector2(104,100),explorer=new Vector2(97,96),
                        workers=new[]{new Vector2(99,103)},initialResources=new Cost(300,300,100)}},objects=new[]{
                    new ScenarioPlacement{id="authored-worker",definition=worker,owner=0,position=new Vector2(40,30)},
                    new ScenarioPlacement{id="dead-before",definition=worker,owner=0,position=new Vector2(42,30)},
                    new ScenarioPlacement{id="dead-after",definition=worker,owner=0,position=new Vector2(44,30)},
                    new ScenarioPlacement{id="authored-guard",definition="musketeer",owner=0,position=new Vector2(40,40)}},
                groups=new[]{new ScenarioGroup{id="authored-party",objectIds=new[]{"start-0-worker-0","authored-worker","dead-before","dead-after","authored-guard"}}}};
            Assert.IsEmpty(ScenarioCodec.Validate(document,rules));
            // Age IV and assembled funds are explicit authored opening inputs.
            // Every subsequent political change, movement and death is a command.
            var world=new World(rules,document,new[]{civilization,"coast"},new[]{0,1},5);
            var simulation=new MatchSimulation(world,Array.Empty<int>());
            var f=new Fixture{world=world,simulation=simulation,town=world.ScenarioEntity("start-0-town").id,lead=world.ScenarioEntity("start-0-worker-0").id,
                authored=world.ScenarioEntity("authored-worker").id,deadBefore=world.ScenarioEntity("dead-before").id,deadAfter=world.ScenarioEntity("dead-after").id,guard=world.ScenarioEntity("authored-guard").id};
            f.recorder=new ReplayRecorder(simulation,ReplayIdentity.Create(rules,"0.14.0","scenario-revolution-binding-test","test-engine"),segmentId:"authored-worker-role",anchorInterval:300);
            if(world.AutoRecruitSeconds(0)>0)Issue(f,new GameCommand{player=0,kind=CommandKind.ToggleRecruitment,actors=new[]{f.town}});
            Issue(f,new GameCommand{player=0,kind=CommandKind.Disband,actors=new[]{f.deadBefore}});
            Issue(f,new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{f.lead},order=OrderKind.Move,point=new Vector2(30,12)});
            Issue(f,new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{f.authored},order=OrderKind.Move,point=new Vector2(44,38)});Steps(f,8);
            return f;
        }
        static void Issue(Fixture f,GameCommand command)
        {
            var result=f.recorder.Issue(command);Assert.IsTrue(result.accepted,result.reason);
            if(f.cold!=null)
            {var other=f.cold.Issue(SnapshotCodec.Clone(command));Assert.AreEqual(result.accepted,other.accepted);Assert.AreEqual(result.reason,other.reason);}
        }
        static void Steps(Fixture f,float seconds)
        {
            for(int n=0;n<Mathf.CeilToInt(seconds/MatchSimulation.StepSeconds);n++)
            {Assert.IsTrue(f.simulation.Step());if(f.coldSimulation!=null)Assert.IsTrue(f.coldSimulation.Step());}
        }
        static void Until(Fixture f,Func<bool> condition,float seconds,string message)
        {
            for(int n=0;n<Mathf.CeilToInt(seconds/MatchSimulation.StepSeconds)&&!condition();n++)
            {Assert.IsTrue(f.simulation.Step(),message);if(f.coldSimulation!=null)Assert.IsTrue(f.coldSimulation.Step(),message);}
            Assert.IsTrue(condition(),message+"; time="+f.world.Time+" phase="+f.Player.politicalPhase);
        }
        static void CompleteRevolution(Fixture f)
        {
            Issue(f,new GameCommand{player=0,kind=CommandKind.Revolution,actors=new[]{f.town}});
            Until(f,()=>f.Player.politicalPhase==PoliticalPhase.Revolutionary,f.world.RevolutionDefinition(0).time+2,"Actual paid revolution must finish");
            Issue(f,new GameCommand{player=0,kind=CommandKind.Disband,actors=new[]{f.deadAfter}});
        }
        static void AtBoundary(Fixture f,string boundary)
        {
            if(boundary=="empire")return;
            if(boundary=="commitment")
            {Issue(f,new GameCommand{player=0,kind=CommandKind.Revolution,actors=new[]{f.town}});Steps(f,7);return;}
            CompleteRevolution(f);
            if(boundary=="rebuilding"||boundary=="rebuilt")
            {
                Issue(f,new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{f.town},item=World.CivilianRebuildingTechnology});Steps(f,7);
                if(boundary=="rebuilt")Until(f,()=>f.Player.politicalPhase==PoliticalPhase.CivilianRebuilt,62,"Actual paid civilian rebuilding must finish");
            }
            Issue(f,new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{f.lead},order=OrderKind.Move,point=new Vector2(35,20)});Steps(f,.4f);
        }
        static void ColdSlot(Fixture f)
        {
            string directory=Path.Combine(Path.GetTempPath(),"frontier-authored-revolution071-"+Guid.NewGuid().ToString("N"));
            try
            {
                var ids=new[]{f.lead,f.authored};var save=new SavedMatch{matchId=Guid.NewGuid().ToString("N"),appVersion="scenario-revolution071-test",world=f.world.CaptureState(),simulation=f.simulation.CaptureState(),
                    selection=ids,groups=new[]{new ControlGroupSnapshot{number=3,actors=ids}},cameraPosition=new Vector3(22,40,24),cameraFocus=new Vector2(22,24),cameraSize=26,simulationSpeed=1};
                string before=JsonUtility.ToJson(save),path=MatchSaveStore.SlotPath(directory,3);MatchSaveStore.Write(path,save);
                var loaded=MatchSaveStore.Read(path,save.appVersion);Assert.AreEqual(before,JsonUtility.ToJson(save),"Saving owns the source DTO");
                CollectionAssert.AreEqual(ids,loaded.selection);CollectionAssert.AreEqual(ids,loaded.groups[0].actors);
                f.cold=World.Restore(f.world.Rules,loaded.world);f.coldSimulation=MatchSimulation.Restore(f.cold,loaded.simulation);Equal(f);
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        static void Equal(Fixture f)
        {Assert.AreEqual(JsonUtility.ToJson(f.world.CaptureState()),JsonUtility.ToJson(f.cold.CaptureState()));Assert.AreEqual(JsonUtility.ToJson(f.simulation.CaptureState()),JsonUtility.ToJson(f.coldSimulation.CaptureState()));}
        static void ForwardReplay(Fixture f)
        {
            var recorded=f.recorder.Finish("authored-binding-verified");
            using(var replay=new ReplaySession(f.world.Rules,recorded,recorded.identity))
            {
                Assert.AreEqual(0,replay.StartStep);Assert.AreEqual(0,replay.CurrentStep,"No seek can replace the original forward reconstruction");
                while(replay.CurrentStep<replay.EndStep&&replay.Error=="")Assert.Greater(replay.Advance(500),0);
                Assert.AreEqual("",replay.Error);Assert.IsTrue(replay.Verified);
                Assert.AreEqual(JsonUtility.ToJson(f.world.CaptureState()),JsonUtility.ToJson(replay.World.CaptureState()));
                Assert.AreEqual(JsonUtility.ToJson(f.simulation.CaptureState()),JsonUtility.ToJson(replay.Simulation.CaptureState()));
            }
        }

        [TestCase("coast","empire")][TestCase("coast","commitment")][TestCase("coast","revolution")][TestCase("coast","rebuilding")][TestCase("coast","rebuilt")]
        [TestCase("river","empire")][TestCase("river","commitment")][TestCase("river","revolution")][TestCase("river","rebuilding")][TestCase("river","rebuilt")]
        [TestCase("france","empire")][TestCase("france","commitment")][TestCase("france","revolution")][TestCase("france","rebuilding")][TestCase("france","rebuilt")]
        [TestCase("ottoman","empire")][TestCase("ottoman","commitment")][TestCase("ottoman","revolution")][TestCase("ottoman","rebuilding")][TestCase("ottoman","rebuilt")]
        public void ActualAuthoredWorkersObjectsAndOrderedGroupsColdRestoreAndReplayEveryPoliticalBoundary(string civilization,string boundary)
        {
            var f=Prepared(civilization);using(f.recorder)
            {
                var original=f.world.ScenarioEntities("authored-party");var origins=original.Select(id=>f.world.Get(id).origin).ToArray();
                AtBoundary(f,boundary);Assert.IsFalse(f.world.Get(f.deadBefore).Alive);Assert.AreEqual(f.world.WorkerDefinition(0).id,f.world.Get(f.deadBefore).def.id);
                if(boundary=="revolution"||boundary=="rebuilding"||boundary=="rebuilt")
                {
                    Assert.IsTrue(f.world.Get(f.lead).def.revolutionary);Assert.IsTrue(f.world.Get(f.authored).def.revolutionary);
                    Assert.IsFalse(f.world.Get(f.deadAfter).Alive);Assert.IsTrue(f.world.Get(f.deadAfter).def.revolutionary);
                }
                Assert.AreEqual("musketeer",f.world.Get(f.guard).def.id);ColdSlot(f);Steps(f,3);Equal(f);
                CollectionAssert.AreEqual(original,f.cold.ScenarioEntities("authored-party"));
                CollectionAssert.AreEqual(origins,original.Select(id=>f.cold.Get(id).origin).ToArray());
                CollectionAssert.AreEqual(original,f.world.ScenarioEntities("authored-party"));ForwardReplay(f);
            }
        }

        [Test] public void ActualFrenchRosterOverrideKeepsCoureurBindingsThroughRevolutionAndCivilianRebuilding()
        {
            var f=Prepared("france",true);using(f.recorder)
            {
                Assert.AreEqual("coast",f.world.ScenarioDocument.starts[0].civilization);Assert.AreEqual("france",f.Player.civilization);
                Assert.AreEqual("coureur",f.world.Get(f.lead).def.id);AtBoundary(f,"rebuilt");ColdSlot(f);Steps(f,3);Equal(f);ForwardReplay(f);
            }
        }

        [TestCase("empire")][TestCase("pendingEmpire")][TestCase("missingIdentity")][TestCase("wrongIdentity")][TestCase("wrongCivilization")][TestCase("futureClock")]
        [TestCase("wrongOwner")][TestCase("wrongTeam")][TestCase("nonWorkerObject")][TestCase("otherSoldier")][TestCase("origin")]
        [TestCase("replacementBinding")][TestCase("groupOrder")][TestCase("duplicateBinding")]
        public void PoliticalRoleExceptionDoesNotRelaxAuthoredIdentityOrAcceptForgedConversions(string tamper)
        {
            var f=Prepared();using(f.recorder)
            {
                if(tamper=="pendingEmpire")AtBoundary(f,"commitment");else if(tamper!="empire")AtBoundary(f,"revolution");
                var snapshot=f.world.CaptureState();Assert.DoesNotThrow(()=>World.Restore(f.world.Rules,snapshot),"Untouched actual phase must restore before tampering");
                var player=snapshot.players[0].state;var lead=snapshot.entities.Single(e=>e.state.id==f.lead);
                switch(tamper)
                {
                    case "empire":case "pendingEmpire":lead.definition="revolutionary";break;
                    case "missingIdentity":player.revolutionId="";break;case "wrongIdentity":player.revolutionId="rev_france";break;
                    case "wrongCivilization":player.civilization="france";break;case "futureClock":player.revolutionAt=snapshot.time+1;break;
                    case "wrongOwner":lead.state.owner=1;break;case "wrongTeam":player.team=1;break;
                    case "nonWorkerObject":snapshot.entities.Single(e=>e.state.id==f.guard).definition="revolutionary";break;
                    case "otherSoldier":lead.definition="musketeer";break;case "origin":lead.state.origin+=Vector2.right;break;
                    case "replacementBinding":
                        snapshot.scenarioState.bindings.Single(b=>b.objectId=="start-0-worker-0").entityIds[0]=f.authored;
                        snapshot.scenarioState.bindings.Single(b=>b.objectId=="authored-worker").entityIds[0]=f.lead;break;
                    case "groupOrder":Array.Reverse(snapshot.scenarioState.bindings.Single(b=>b.objectId=="authored-party").entityIds);break;
                    case "duplicateBinding":snapshot.scenarioState.bindings.Single(b=>b.objectId=="authored-worker").entityIds[0]=f.lead;break;
                }
                string before=JsonUtility.ToJson(snapshot);Assert.Throws<ArgumentException>(()=>World.Restore(f.world.Rules,snapshot),tamper);
                Assert.AreEqual(before,JsonUtility.ToJson(snapshot),"Validation must neither hydrate definitions nor alter a rejected cold DTO");
            }
        }
    }
}
