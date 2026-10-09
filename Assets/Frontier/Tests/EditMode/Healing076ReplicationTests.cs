using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Explicit controlled fixtures: prepared patients/resources are in the initial
    // state, not an ordinary-match or native-GUI claim. Authority owns all later HP.
    public sealed class Healing076ReplicationTests
    {
        const string Match="healing076-match",Epoch="healing076-epoch";
        static string State(World world,bool events=true)=>JsonUtility.ToJson(SnapshotCodec.Clone(world.CaptureState(events)));
        static T Wire<T>(string kind,T value)
        {
            var envelope=LanWire.Decode(LanWire.Encode(kind,value,Match,Epoch));
            Assert.AreEqual(kind,envelope.kind);return LanWire.Body<T>(envelope);
        }
        static World Prepared(out EntityState healer,out EntityState patient)
        {
            var world=new World(SnapshotCodec.Clone(Catalog.Load()),314159,
                new[]{"ottoman","coast","france","river"},new[]{0,1,0,1},5,MapTopology.Frontier);
            foreach(var guardian in world.Entities.Where(World.Guardian).ToArray())world.Remove(guardian);
            healer=world.Spawn("imam",0,world.Navigation.NearestWalkable(new Vector2(30,30),.4f));
            patient=world.Spawn("pike",2,world.Navigation.NearestWalkable(healer.position+Vector2.right*3,.4f));
            patient.hp=world.MaxHp(patient)*.5f;
            world.Fog.Tick(world.Entities);return world;
        }
        static GameCommand Heal(EntityState healer,EntityState target)=>new GameCommand{player=healer.owner,
            kind=CommandKind.Order,actors=new[]{healer.id},order=OrderKind.Heal,target=target.id,point=target.position};
        static void Step(MatchSimulation simulation,int count)
        {for(int i=0;i<count;i++)Assert.IsTrue(simulation.Step(),"Stopped at "+simulation.StepCount);}

        [Test] public void WireAuthorityAppliesAlliedHealOnceAndReplicaCopiesActualReceiptsWithoutTicking()
        {
            var world=Prepared(out var healer,out var patient);var simulation=new MatchSimulation(world,Array.Empty<int>());
            var identity=ReplayIdentity.Create(world.Rules,"0.19.0-test","healing076-controlled","test-unity");
            using(var recorder=new ReplayRecorder(simulation,identity,segmentId:"healing-wire",anchorInterval:7))
            {
                int applied=0;var authority=new LanAuthority(world,Match,Epoch,c=>{applied++;return recorder.Issue(c);},(paused,reason)=>recorder.Pause(paused,reason));
                authority.BindPeer("host",0,1);
                var previous=Wire("full",LanProjection.Full(world,Match,Epoch,1,0));
                var replica=new LanReplica(world.Rules);replica.Install(previous);
                var request=Wire("command",new LanCommandRequest{matchId=Match,epoch=Epoch,peerId="host",generation=1,
                    sequence=1,operation=LanOperation.Command,command=Heal(healer,patient)});
                var pending=authority.Submit(request);Assert.IsFalse(pending.terminal);
                Assert.AreEqual(pending.hostSequence,authority.Submit(request).hostSequence);
                var ack=Wire("ack",authority.Drain(0,2).Single());Assert.IsTrue(ack.accepted,ack.reason);
                Assert.AreEqual(1,applied);Assert.AreEqual(ack.hostSequence,authority.Submit(request).hostSequence);
                Assert.IsEmpty(authority.Drain(0,2));
                float hp=patient.hp;Step(simulation,20);Assert.Greater(patient.hp,hp);
                var next=Wire("full",LanProjection.Full(world,Match,Epoch,3,simulation.StepCount));
                var delta=Wire("delta",LanProjection.Delta(previous,next,world.Events));
                Assert.IsTrue(delta.events.Any(e=>e.hasHealing));Assert.IsTrue(replica.Apply(delta),replica.Error);
                Assert.AreEqual(State(world,false),State(replica.World,false));
                Assert.AreEqual(patient.hp,replica.World.Get(patient.id).hp);
                var source=world.Events.First(e=>e.hasHealing);var copy=replica.World.Events.First(e=>e.hasHealing);
                Assert.AreEqual(JsonUtility.ToJson(source.healing),JsonUtility.ToJson(copy.healing));
                Assert.AreNotSame(source.healing,copy.healing,"Receipt is owned, not aliased");
                string before=State(replica.World);Assert.IsFalse(replica.Apply(delta));Assert.AreEqual(before,State(replica.World));
                Assert.AreEqual(1,applied);
            }
        }
        [Test] public void BoundPeerCannotUseAlliedHealerOrEnemyPatientAndForgedHealthReceiptIsAtomic()
        {
            var world=Prepared(out var healer,out var patient);var ally=world.Spawn("priest",2,healer.position+Vector2.up*2);
            var enemy=world.Spawn("pike",1,patient.position+Vector2.right*6);enemy.hp=world.MaxHp(enemy)*.5f;
            Assert.IsTrue(world.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{enemy.id},order=OrderKind.Hold}).accepted);world.Fog.Tick(world.Entities);
            var authority=new LanAuthority(world,Match,Epoch,c=>world.Issue(c),(paused,reason)=>world.Paused=paused);authority.BindPeer("host",0,1);
            var command=Heal(ally,patient);command.player=0;
            var request=new LanCommandRequest{matchId=Match,epoch=Epoch,peerId="host",generation=1,sequence=1,operation=LanOperation.Command,command=command};
            string before=State(world);var rejected=authority.Submit(Wire("command",request));
            Assert.IsTrue(rejected.terminal);Assert.IsFalse(rejected.accepted);Assert.AreEqual(before,State(world));
            command=Heal(healer,enemy);Assert.IsFalse(world.Issue(command).accepted);Assert.AreEqual(before,State(world));
            var previous=LanProjection.Full(world,Match,Epoch,1,0);var replica=new LanReplica(world.Rules);replica.Install(Wire("full",previous));
            Assert.IsTrue(world.Issue(Heal(healer,patient)).accepted);var simulation=new MatchSimulation(world,Array.Empty<int>());Step(simulation,10);
            var next=LanProjection.Full(world,Match,Epoch,2,10);var delta=Wire("delta",LanProjection.Delta(previous,next,world.Events));
            delta.events.First(e=>e.hasHealing).healing.actual=0;
            string replicaBefore=State(replica.World);Assert.IsFalse(replica.Apply(delta));Assert.AreEqual(replicaBefore,State(replica.World));Assert.AreEqual(1,replica.Revision);
            replica.Install(Wire("full",next));Assert.AreEqual(State(world,false),State(replica.World,false));
        }
        [Test] public void DiskCheckpointRetainsCombatLockAndContinuesSameHealthOrdersAndEventLedger()
        {
            var world=Prepared(out var healer,out var patient);patient.healingBlockedUntil=2;
            Assert.IsTrue(world.Issue(Heal(healer,patient)).accepted);var simulation=new MatchSimulation(world,Array.Empty<int>());Step(simulation,5);
            float hp=patient.hp;Assert.IsFalse(world.Events.Any(e=>e.hasHealing));
            string directory=Path.Combine(Path.GetTempPath(),"frontier-healing076-save-"+Guid.NewGuid().ToString("N"));
            try
            {
                string path=MatchSaveStore.SlotPath(directory,1);
                MatchSaveStore.Write(path,new SavedMatch{matchId="healing-cold",appVersion="0.19.0",world=world.CaptureState(),simulation=simulation.CaptureState(),cameraSize=26,cameraFocus=Vector2.zero,selection=new[]{healer.id},groups=Array.Empty<ControlGroupSnapshot>()});
                var saved=MatchSaveStore.Read(path,"0.19.0");var coldWorld=World.Restore(world.Rules,saved.world);var cold=MatchSimulation.Restore(coldWorld,saved.simulation);
                Assert.AreEqual(State(world),State(coldWorld));Assert.AreEqual(2,coldWorld.Get(patient.id).healingBlockedUntil);
                world.Paused=true;coldWorld.Paused=true;world.Tick(5);coldWorld.Tick(5);Assert.AreEqual(hp,patient.hp);Assert.AreEqual(.5f,world.Time,.00001f);
                world.Paused=false;coldWorld.Paused=false;
                Step(simulation,25);Step(cold,25);Assert.Greater(patient.hp,hp);
                Assert.AreEqual(State(world),State(coldWorld));Assert.AreEqual(ReplayDigest.Compute(world,simulation),ReplayDigest.Compute(coldWorld,cold));
                Assert.Throws<IOException>(()=>MatchSaveStore.Read(path,"0.18.0"));
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test] public void StrictColdReplaySeeksThroughBlockedHealingOverrideAndFullHealthWithoutLosingReceipts()
        {
            var world=Prepared(out var healer,out var patient);patient.healingBlockedUntil=1.2f;
            var simulation=new MatchSimulation(world,Array.Empty<int>());var identity=ReplayIdentity.Create(world.Rules,"0.19.0","healing076-test-guid","test-unity");
            string directory=Path.Combine(Path.GetTempPath(),"frontier-healing076-replay-"+Guid.NewGuid().ToString("N"));
            try
            {
                ReplayDocument document;string final;
                using(var recorder=new ReplayRecorder(simulation,identity,segmentId:"healing-cold-replay",anchorInterval:13,startKind:"controlled-healing076-fixture"))
                {
                    Assert.IsTrue(recorder.Issue(Heal(healer,patient)).accepted);Step(simulation,32);
                    Assert.IsTrue(recorder.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{healer.id},order=OrderKind.Stop}).accepted);Step(simulation,7);
                    Assert.IsTrue(recorder.Issue(Heal(healer,patient)).accepted);Step(simulation,130);
                    Assert.AreEqual(world.MaxHp(patient),patient.hp,.001f);
                    document=recorder.Finish("declared_controlled_boundary");final=ReplayDigest.Compute(world,simulation);
                }
                Assert.IsFalse(document.incomplete);Assert.IsTrue(document.events.Any(e=>e.hasHealing));
                var path=ReplayStore.PathFor(directory,document.segmentId);ReplayStore.Write(path,document,world.Rules,identity);
                var loaded=ReplayStore.Read(path,world.Rules,identity);Assert.AreEqual(JsonUtility.ToJson(document),JsonUtility.ToJson(loaded));
                using(var replay=new ReplaySession(world.Rules,loaded,identity))
                {
                    Assert.IsTrue(replay.Seek(replay.EndStep),replay.Error);Assert.IsTrue(replay.Verified,replay.Error);Assert.AreEqual(final,ReplayDigest.Compute(replay.World,replay.Simulation));
                    foreach(int step in new[]{5,25,38,75,replay.EndStep})Assert.IsTrue(replay.Seek(step),replay.Error);
                    Assert.IsTrue(replay.Verified);Assert.AreEqual(final,ReplayDigest.Compute(replay.World,replay.Simulation));
                }
                var forged=SnapshotCodec.Clone(loaded);forged.events.First(e=>e.hasHealing).healing.actual=-1;
                Assert.Throws<ArgumentException>(()=>ReplayValidation.Validate(world.Rules,forged,identity));
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        [Test] public void SnapshotRejectsNonHealerCapabilityInvalidPatientAndNonFiniteHealingClocks()
        {
            var world=Prepared(out var healer,out var patient);Assert.IsTrue(world.Issue(Heal(healer,patient)).accepted);
            string original=State(world);
            foreach(int mutation in Enumerable.Range(0,5))
            {
                var snapshot=world.CaptureState();var h=snapshot.entities.Single(e=>e.state.id==healer.id).state;
                if(mutation==0)h.healingBlockedUntil=float.NaN;
                if(mutation==1)h.healingReviewAt=-1;
                if(mutation==2)h.healingTarget=world.Entities.First(e=>e.owner==1&&e.def.id=="town").id;
                if(mutation==3)snapshot.entities.Single(e=>e.state.id==patient.id).state.autoHeal=true;
                if(mutation==4)snapshot.schema=4;
                Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,snapshot),"Malformed state "+mutation);Assert.AreEqual(original,State(world));
            }
        }
        [TestCase(0,false)][TestCase(2,false)][TestCase(0,true)]
        public void ResignationImmediatelyClearsMedicalLinksAndTerminalReplayAndProjectionRemainRestorable(int departing,bool terminal)
        {
            var world=Prepared(out var healer,out var patient);var simulation=new MatchSimulation(world,Array.Empty<int>());
            var identity=ReplayIdentity.Create(world.Rules,"0.19.0","healing076-resign-fixture","test-unity");
            using(var recorder=new ReplayRecorder(simulation,identity,segmentId:"healing-resign"))
            {
                Assert.IsTrue(recorder.Issue(Heal(healer,patient)).accepted);Step(simulation,2);Assert.Greater(healer.healingTarget,0);
                Assert.IsTrue(recorder.Issue(new GameCommand{player=departing,kind=CommandKind.Resign}).accepted);
                if(terminal)Assert.IsTrue(recorder.Issue(new GameCommand{player=2,kind=CommandKind.Resign}).accepted);
                Assert.AreEqual(terminal,world.IsMatchEnded);Assert.AreEqual(0,healer.healingTarget);Assert.IsEmpty(healer.orders);
                if(departing==0)Assert.IsFalse(healer.autoHeal);
                string state=State(world);var cold=World.Restore(world.Rules,world.CaptureState());Assert.AreEqual(state,State(cold));
                var replica=new LanReplica(world.Rules);replica.Install(Wire("full",LanProjection.Full(world,Match,Epoch,1,simulation.StepCount)));
                Assert.AreEqual(State(world,false),State(replica.World,false));
                var document=recorder.Finish("resignation_boundary");
                using(var replay=new ReplaySession(world.Rules,document,identity))
                {Assert.IsTrue(replay.Seek(replay.EndStep),replay.Error);Assert.IsTrue(replay.Verified,replay.Error);Assert.AreEqual(state,State(replay.World));}
            }
        }
        [Test] public void DeltaTransmitsOnlyChangedHealingBehaviorAndCombatClockEvenWithoutHealthChange()
        {
            var world=Prepared(out var healer,out var patient);healer.autoHeal=false;
            var first=LanProjection.Full(world,Match,Epoch,1,0);var replica=new LanReplica(world.Rules);replica.Install(Wire("full",first));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.ToggleAutoHeal,actors=new[]{healer.id}}).accepted);
            healer.healingBlockedUntil=2;float hp=patient.hp;
            var next=LanProjection.Full(world,Match,Epoch,2,0);var delta=Wire("delta",LanProjection.Delta(first,next,world.Events));
            Assert.IsTrue(delta.changed.Any(e=>e.state.id==healer.id));Assert.IsTrue(replica.Apply(delta),replica.Error);
            Assert.IsTrue(replica.World.Get(healer.id).autoHeal);Assert.AreEqual(2,replica.World.Get(healer.id).healingBlockedUntil);Assert.AreEqual(hp,replica.World.Get(patient.id).hp);
            Assert.AreEqual(State(world,false),State(replica.World,false));
        }
    }
}
