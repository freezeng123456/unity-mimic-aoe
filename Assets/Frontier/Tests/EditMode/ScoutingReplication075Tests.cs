using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Controlled local fixtures, not native GUI or multi-host network evidence.
    // Wallets, detector placements and removal of neutral guards are captured in
    // the initial snapshot. Accepted gameplay thereafter uses the real wire,
    // authority, recorder and fixed-step scheduler; there are no mid-record grants.
    public sealed class ScoutingReplication075Tests
    {
        const string Match="scouting075-match",Epoch="scouting075-epoch";
        [Serializable] sealed class Events {public SimEvent[] values;}
        static string State(World world,bool events=true)=>JsonUtility.ToJson(world.CaptureState(events));
        // Save/replay JSON can initialize absent nested metadata DTOs. Compare
        // every event field after the same JSON round trip on both sides; keep
        // hasEconomy/hasPopulation and all actual payload values intact.
        static string EventState(IEnumerable<SimEvent> events)=>JsonUtility.ToJson(SnapshotCodec.Clone(new Events{values=events.ToArray()}));
        static EntityState Own(World world,int player,string definition)=>world.Entities.First(e=>e.Alive&&e.owner==player&&e.def.id==definition);
        static T ThroughWire<T>(string kind,T value)
        {
            var envelope=LanWire.Decode(LanWire.Encode(kind,value,Match,Epoch));
            Assert.AreEqual(kind,envelope.kind);Assert.AreEqual(Match,envelope.matchId);Assert.AreEqual(Epoch,envelope.epoch);
            return LanWire.Body<T>(envelope);
        }
        static World Prepared(bool fourPlayers=true,string opponent="coast")
        {
            var rules=SnapshotCodec.Clone(Catalog.Load());
            var world=fourPlayers
                ?new World(rules,314159,new[]{"france","coast","river","ottoman"},new[]{0,1,0,1},5,MapTopology.Winding)
                :new World(rules,314159,"france",opponent,5,MapTopology.Frontier);
            foreach(var player in world.Players){player.resources=new Cost(10000,10000,10000);player.populationCap=200;}
            foreach(var guardian in world.Entities.Where(World.Guardian).ToArray())world.Remove(guardian);
            world.Fog.Tick(world.Entities);return world;
        }
        static GameCommand Train(World world,int player,string definition)=>new GameCommand{player=player,kind=CommandKind.Train,
            actors=new[]{Own(world,player,world.Rules.Entity(definition).producer).id},item=definition};
        static GameCommand Order(EntityState actor,OrderKind order,Vector2 point=default,bool append=false)=>new GameCommand{
            player=actor.owner,kind=CommandKind.Order,actors=new[]{actor.id},order=order,point=point,append=append};
        static GameCommand Stealth(EntityState actor)=>new GameCommand{player=actor.owner,kind=CommandKind.ToggleStealth,actors=new[]{actor.id}};
        static void Step(MatchSimulation simulation,int count=1)
        {
            for(int i=0;i<count;i++)
            {
                Assert.IsFalse(simulation.World.Paused,"A paused world cannot satisfy a bounded wait");
                Assert.IsFalse(simulation.World.IsMatchEnded,"An early terminal world cannot satisfy a bounded wait");
                Assert.IsTrue(simulation.Step(),"Fixed step stopped at "+simulation.StepCount);
            }
        }

        sealed class Checkpoint
        {
            public readonly int step;
            readonly string world,simulation,events,digest;
            public Checkpoint(MatchSimulation value)
            {
                step=value.StepCount;world=State(value.World);simulation=JsonUtility.ToJson(value.CaptureState());
                events=EventState(value.World.Events);digest=ReplayDigest.Compute(value.World,value);
            }
            public void Check(MatchSimulation value)
            {
                Assert.AreEqual(step,value.StepCount);Assert.AreEqual(world,State(value.World),"Full world at "+step);
                Assert.AreEqual(simulation,JsonUtility.ToJson(value.CaptureState()),"Full scheduler at "+step);
                Assert.AreEqual(events,EventState(value.World.Events),"Ordered complete event ledger at "+step);
                Assert.AreEqual(digest,ReplayDigest.Compute(value.World,value),"Replay state/event digest at "+step);
            }
        }

        sealed class Session:IDisposable
        {
            public readonly World world;
            public readonly MatchSimulation simulation;
            public readonly ReplayIdentity identity;
            public readonly ReplayRecorder recorder;
            public readonly LanAuthority authority;
            public readonly LanQueueTracker queues=new LanQueueTracker();
            public readonly LanReplica replica;
            public readonly string initialWorld,initialSimulation;
            public int applied;
            readonly Dictionary<string,long> sequences=new Dictionary<string,long>();
            readonly int initialEventCount;
            LanFullState previous;
            long revision=1;
            public Session(World value)
            {
                world=value;simulation=new MatchSimulation(world,Array.Empty<int>());
                identity=ReplayIdentity.Create(world.Rules,"075-controlled-test","scouting075-local-fixture","test-engine");
                initialWorld=State(world,false);initialSimulation=JsonUtility.ToJson(simulation.CaptureState());initialEventCount=world.Events.Count;
                recorder=new ReplayRecorder(simulation,identity,segmentId:Guid.NewGuid().ToString("N"),anchorInterval:37,startKind:"controlled-scouting075-fixture");
                authority=new LanAuthority(world,Match,Epoch,c=>{applied++;return recorder.Issue(c);},(paused,reason)=>recorder.Pause(paused,reason));
                authority.BindPeer("host",0,1);if(world.Players.Length>2)authority.BindPeer("remote",2,1);
                previous=LanProjection.Full(world,Match,Epoch,revision,simulation.StepCount,queues.Capture(world));
                replica=new LanReplica(world.Rules);replica.Install(ThroughWire("full",previous));Assert.IsEmpty(replica.World.Events);
            }
            public LanCommandRequest Request(GameCommand command,string peer="host")
            {
                sequences.TryGetValue(peer,out long sequence);sequences[peer]=++sequence;
                return ThroughWire("command",new LanCommandRequest{matchId=Match,epoch=Epoch,peerId=peer,generation=1,
                    sequence=sequence,operation=LanOperation.Command,command=command});
            }
            public LanCommandAck Send(GameCommand command,string peer="host",bool retransmit=false)
            {
                var request=Request(command,peer);var pending=authority.Submit(request);Assert.IsFalse(pending.terminal,pending.reason);
                if(retransmit)Assert.AreEqual(pending.hostSequence,authority.Submit(ThroughWire("command",request)).hostSequence);
                var ack=ThroughWire("ack",authority.Drain(simulation.StepCount,++revision).Single());
                Assert.IsTrue(ack.terminal);Assert.IsTrue(ack.accepted,ack.reason);Assert.IsFalse(ack.protocolRejected);
                Assert.AreEqual(simulation.StepCount,ack.appliedStep);Assert.AreEqual(request.sequence,ack.sequence);
                if(retransmit)
                {
                    Assert.AreEqual(ack.hostSequence,authority.Submit(ThroughWire("command",request)).hostSequence);
                    Assert.IsEmpty(authority.Drain(simulation.StepCount,revision));
                }
                return ack;
            }
            public void Publish()
            {
                var next=LanProjection.Full(world,Match,Epoch,++revision,simulation.StepCount,queues.Capture(world));
                var delta=ThroughWire("delta",LanProjection.Delta(previous,next,world.Events));
                Assert.IsTrue(replica.Apply(delta),replica.Error);
                Assert.AreEqual(State(world,false),State(replica.World,false));
                Assert.AreEqual(next.digest,LanProjection.Digest(replica.World.CaptureState(false),replica.QueueSnapshot));
                Assert.AreEqual(EventState(world.Events.Skip(initialEventCount)),EventState(replica.World.Events));
                Assert.AreEqual(next.eventCursor,replica.EventCursor);Assert.AreEqual(simulation.StepCount,replica.CompletedStep);
                foreach(var row in next.queues)for(int index=0;index<row.jobs.Length;index++)Assert.AreEqual(row.jobs[index],replica.Queues.QueueId(row.entity,index));
                string before=State(replica.World);Assert.IsFalse(replica.Apply(delta));Assert.AreEqual(before,State(replica.World),"Duplicate delta is inert");
                previous=next;
            }
            public void Dispose()=>recorder.Dispose();
        }

        static MatchSimulation ColdSimulation(MatchSimulation original)
        {
            var coldWorld=World.Restore(original.World.Rules,JsonUtility.FromJson<WorldSnapshot>(State(original.World)));
            var cold=MatchSimulation.Restore(coldWorld,JsonUtility.FromJson<MatchSimulationSnapshot>(JsonUtility.ToJson(original.CaptureState())));
            new Checkpoint(original).Check(cold);return cold;
        }
        static ReplayDocument CheckColdReplay(Session session,params Checkpoint[] expected)
        {
            var document=session.recorder.Finish("controlled_test_complete");
            Assert.IsFalse(document.incomplete);Assert.AreEqual("controlled-scouting075-fixture",document.startKind);
            Assert.AreEqual(session.initialWorld,JsonUtility.ToJson(document.anchors[0].world));
            Assert.AreEqual(session.initialSimulation,JsonUtility.ToJson(document.anchors[0].simulation));
            string directory=Path.Combine(Path.GetTempPath(),"frontier-scouting-replication075-"+Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);string path=ReplayStore.PathFor(directory,document.segmentId);
                ReplayStore.Write(path,document,session.world.Rules,session.identity);Assert.Greater(new FileInfo(path).Length,0);
                var cold=ReplayStore.Read(path,session.world.Rules,session.identity);
                Assert.AreEqual(JsonUtility.ToJson(document),JsonUtility.ToJson(cold));
                ReplayValidation.ValidateIdentity(session.world.Rules,cold.identity,session.identity);
                string eventHash=SnapshotCodec.Hash("frontier-replay-events-v1"),inputHash=SnapshotCodec.Hash("frontier-replay-inputs-v1");int eventIndex=0,inputIndex=0;
                foreach(var anchor in cold.anchors)
                {
                    while(eventIndex<anchor.eventCount)eventHash=SnapshotCodec.Hash(eventHash+"\n"+JsonUtility.ToJson(cold.events[eventIndex++]));
                    while(inputIndex<anchor.recordIndex)inputHash=SnapshotCodec.Hash(inputHash+"\n"+JsonUtility.ToJson(cold.records[inputIndex++]));
                    Assert.AreEqual(eventHash,anchor.eventHash);Assert.AreEqual(inputHash,anchor.inputHash);
                    var hydrated=SnapshotCodec.Clone(anchor.world);hydrated.events=cold.events.Take(anchor.eventCount).ToArray();
                    hydrated=SnapshotCodec.Clone(hydrated); // Match the public cold-save DTO hydration, without dropping any state fields.
                    var world=World.Restore(session.world.Rules,hydrated);var simulation=MatchSimulation.Restore(world,SnapshotCodec.Clone(anchor.simulation));
                    Assert.AreEqual(JsonUtility.ToJson(hydrated),State(world));Assert.AreEqual(JsonUtility.ToJson(anchor.simulation),JsonUtility.ToJson(simulation.CaptureState()));
                    Assert.AreEqual(anchor.stateDigest,ReplayDigest.Compute(world,simulation));
                }
                using(var replay=new ReplaySession(session.world.Rules,cold,session.identity))
                {
                    int guard=0;while(replay.CurrentStep<replay.EndStep&&guard++<=replay.EndStep+1)Assert.Greater(replay.Advance(11),0,replay.Error);
                    Assert.IsEmpty(replay.Error);Assert.IsTrue(replay.Verified);Assert.AreEqual(replay.EndStep,replay.CurrentStep);
                    expected.Last().Check(replay.Simulation);
                    // Alternate backward/forward seeks, including non-anchor checkpoints.
                    foreach(var checkpoint in expected.Reverse())
                    {
                        Assert.IsTrue(replay.BeginSeek(checkpoint.step),replay.Error);guard=0;
                        while(replay.IsSeeking&&guard++<=replay.EndStep+1)Assert.Greater(replay.AdvanceSeek(3),0,replay.Error);
                        Assert.IsFalse(replay.IsSeeking);Assert.IsEmpty(replay.Error);checkpoint.Check(replay.Simulation);
                    }
                    Assert.IsTrue(replay.Seek(replay.EndStep),replay.Error);Assert.IsTrue(replay.Verified);expected.Last().Check(replay.Simulation);
                }
                return cold;
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }

        [TestCase(0,"native_scout","host",25,1)]
        [TestCase(2,"envoy","remote",0,0)]
        public void ActualPaidUnitProducerCommandCompletesOnceThroughAuthorityReplicaAndColdReplay(int player,string definition,string peer,int wood,int population)
        {
            using(var session=new Session(Prepared()))
            {
                var world=session.world;var producer=Own(world,player,world.Rules.Entity(definition).producer);var owner=world.Players[player];
                var wallet=owner.resources;var spent=owner.spent;int used=owner.usedPopulation;
                session.Send(Train(world,player,definition),peer,true);Assert.AreEqual(1,session.applied);
                Assert.AreEqual(wallet.food-50,owner.resources.food);Assert.AreEqual(wallet.wood-wood,owner.resources.wood);
                Assert.AreEqual(spent.food+50,owner.spent.food);Assert.AreEqual(spent.wood+wood,owner.spent.wood);
                Assert.AreEqual(population,owner.reservedPopulation);Assert.AreEqual(used,owner.usedPopulation);
                var job=producer.queue.Single();Assert.AreEqual(definition,job.item);Assert.AreEqual("Train",job.kind);
                Assert.AreEqual(new Cost(50,wood,0),job.paid);Assert.AreEqual(world.Rules.Entity(definition).time,job.duration);
                Assert.AreEqual(1,world.Events.Count(e=>e.kind=="Payment"&&e.entity==producer.id));
                var payment=world.Events.Last(e=>e.kind=="Payment"&&e.entity==producer.id);Assert.IsTrue(payment.hasEconomy);
                Assert.AreEqual(-50,payment.economy.actualDelta.food);Assert.AreEqual(-wood,payment.economy.actualDelta.wood);
                Step(session.simulation,3);var pending=new Checkpoint(session.simulation);session.Publish();
                long queueId=session.queues.QueueId(world,producer.id,0);Assert.Greater(queueId,0);Assert.AreEqual(queueId,session.replica.Queues.QueueId(producer.id,0));
                var cold=ColdSimulation(session.simulation);
                for(int i=0;i<220;i++){Step(session.simulation);Step(cold);}
                new Checkpoint(session.simulation).Check(cold);Assert.IsEmpty(producer.queue);Assert.AreEqual(0,owner.reservedPopulation);
                var trained=world.Entities.Single(e=>e.Alive&&e.owner==player&&e.def.id==definition);Assert.AreEqual(used+population,owner.usedPopulation);
                Assert.IsTrue(world.Events.Any(e=>e.kind=="UnitTrained"&&e.entity==producer.id&&e.target==trained.id&&e.message==definition));
                Assert.AreEqual(1,world.Events.Count(e=>e.kind=="Payment"&&e.entity==producer.id));session.Publish();
                Assert.AreEqual(0,session.replica.Queues.QueueId(producer.id,0));
                var document=CheckColdReplay(session,pending,new Checkpoint(session.simulation));
                Assert.AreEqual(1,document.records.Count(r=>r.hasCommand&&r.command.kind==CommandKind.Train&&r.command.item==definition&&r.accepted));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void ScoutWireOrderHandsOffToActualAppendedMoveAtTheCorrectLegBoundary(bool legAlreadyStarted)
        {
            using(var session=new Session(Prepared()))
            {
                session.Send(Train(session.world,0,"native_scout"));Step(session.simulation,220);var scout=Own(session.world,0,"native_scout");
                Vector2 birth=scout.position;session.Send(Order(scout,OrderKind.Scout));
                if(legAlreadyStarted)
                {
                    for(int i=0;i<40&&(scout.position-birth).sqrMagnitude<.01f;i++)Step(session.simulation);
                    Assert.Greater(Vector2.Distance(birth,scout.position),.1f,"The Scout order must physically start its chosen leg");
                    Assert.IsTrue(scout.orders.Peek().hasResolvedPoint);
                }
                else Assert.IsFalse(scout.orders.Peek().hasResolvedPoint);
                Vector2 goal=VacantMoveGoal(session.world,scout,birth);session.Send(Order(scout,OrderKind.Move,goal,true));
                Assert.AreEqual(2,scout.orders.Count);Assert.AreEqual(OrderKind.Scout,scout.orders.Peek().kind);
                Assert.AreEqual(OrderKind.Move,scout.orders.Last().kind);var queued=new Checkpoint(session.simulation);session.Publish();
                var lastLeg=scout.orders.Peek().resolvedPoint;int waited=0;
                for(;waited<1200&&scout.orders.Count>0&&scout.orders.Peek().kind==OrderKind.Scout;waited++)
                {
                    if(scout.orders.Peek().hasResolvedPoint)lastLeg=scout.orders.Peek().resolvedPoint;
                    Step(session.simulation);Assert.IsTrue(scout.Alive);Assert.AreEqual(0,scout.garrisonedIn);
                }
                Assert.Less(waited,1200,"Scout must yield to an appended order within the bounded physical leg");
                Assert.AreEqual(1,scout.orders.Count);Assert.AreEqual(OrderKind.Move,scout.orders.Peek().kind);
                if(legAlreadyStarted)Assert.LessOrEqual(Vector2.Distance(scout.position,lastLeg),.151f,"An active leg hands off only after reaching its actual endpoint");
                else {Assert.AreEqual(1,waited);Assert.Less(Vector2.Distance(birth,scout.position),.001f,"Before its first review Scout yields without inventing a leg");}
                var handedOff=new Checkpoint(session.simulation);session.Publish();
                for(waited=0;waited<600&&scout.orders.Count>0;waited++)Step(session.simulation);
                Assert.Less(waited,600);Assert.IsEmpty(scout.orders);Assert.Less(Vector2.Distance(scout.position,goal),.8f);session.Publish();
                var document=CheckColdReplay(session,queued,handedOff,new Checkpoint(session.simulation));
                Assert.IsTrue(document.records.Any(r=>r.hasCommand&&r.accepted&&r.command.order==OrderKind.Scout&&r.command.actors.Contains(scout.id)));
                Assert.IsTrue(document.records.Any(r=>r.hasCommand&&r.accepted&&r.command.order==OrderKind.Move&&r.command.append&&r.command.actors.Contains(scout.id)));
            }
        }
        static Vector2 VacantMoveGoal(World world,EntityState actor,Vector2 origin)
        {
            for(int ring=1;ring<=3;ring++)for(int direction=0;direction<8;direction++)
            {
                float angle=direction*Mathf.PI/4;var point=world.Navigation.NearestWalkable(origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(ring*4),actor.def.radius);
                if(!world.Navigation.IsWalkable(point,actor.def.radius)||world.Entities.Any(e=>e.Alive&&e.def.kind==EntityKind.Unit&&Vector2.Distance(e.position,point)<e.def.radius+actor.def.radius+1))continue;
                var path=world.Navigation.FindPath(actor.position,point,actor.def.radius);
                if(path.Count>1&&(path[path.Count-1]-point).sqrMagnitude<.0001f)return point;
            }
            Assert.Fail("Controlled fixture has no vacant reachable handoff destination");return Vector2.zero;
        }

        [Test] public void UnauthorizedLanActorsCannotChangePaidQueuesScoutOrdersOrStealth()
        {
            var world=Prepared();var scout=world.Spawn("native_scout",0,world.Navigation.NearestWalkable(Own(world,0,"town").position+new Vector2(12,0),.4f));
            var envoy=world.Spawn("envoy",2,world.Navigation.NearestWalkable(Own(world,2,"town").position+new Vector2(12,0),.4f));world.Fog.Tick(world.Entities);
            using(var session=new Session(world))
            {
                session.Send(Train(world,0,"native_scout"));session.Send(Order(scout,OrderKind.Scout));session.Send(Stealth(scout));
                Assert.IsTrue(scout.stealth);Assert.AreEqual(OrderKind.Scout,scout.orders.Peek().kind);Assert.AreEqual(1,Own(world,0,"town").queue.Count);
                foreach(var command in new[]{
                    new GameCommand{player=2,kind=CommandKind.Order,actors=new[]{envoy.id,scout.id},order=OrderKind.Scout},
                    new GameCommand{player=2,kind=CommandKind.ToggleStealth,actors=new[]{scout.id}},
                    new GameCommand{player=2,kind=CommandKind.Train,actors=new[]{Own(world,0,"town").id},item="native_scout"},
                    Stealth(scout)})
                {
                    var before=new Checkpoint(session.simulation);int applied=session.applied,records=session.recorder.Capture().records.Length;
                    var rejected=session.authority.Submit(session.Request(command,"remote"));
                    Assert.IsTrue(rejected.terminal);Assert.IsTrue(rejected.protocolRejected);Assert.IsFalse(rejected.accepted);
                    Assert.AreEqual(0,session.authority.PendingCount);Assert.AreEqual(applied,session.applied);
                    Assert.AreEqual(records,session.recorder.Capture().records.Length);before.Check(session.simulation);
                }
                session.Publish();Assert.IsTrue(session.replica.World.Get(scout.id).stealth);
                Assert.AreEqual(OrderKind.Scout,session.replica.World.Get(scout.id).orders.Peek().kind);
                CheckColdReplay(session,new Checkpoint(session.simulation));
            }
        }

        static Vector2 OpenDetectorLane(World world,float length,string detector)
        {
            var definition=world.Rules.Entity(detector);
            for(int y=12;y<=112;y+=4)for(int x=12;x<=112-length;x+=4)
            {
                var origin=new Vector2(x,y);var target=origin+Vector2.right*length;
                if(!world.Navigation.IsWalkable(origin,.5f)||!world.Navigation.IsWalkable(target,.5f)||!world.Navigation.ClearLine(origin,target,.5f))continue;
                if(definition.kind==EntityKind.Building&&(!world.Navigation.IsAreaFree(origin,definition.width,definition.depth)||
                    new[]{new Vector2(-definition.width*.5f,-definition.depth*.5f),new Vector2(definition.width*.5f,-definition.depth*.5f),new Vector2(-definition.width*.5f,definition.depth*.5f),new Vector2(definition.width*.5f,definition.depth*.5f)}.Any(offset=>world.IsWater(origin+offset))))continue;
                if(world.Entities.Any(e=>e.Alive&&e.def.kind!=EntityKind.Resource&&(Vector2.Distance(e.position,origin)<20||Vector2.Distance(e.position,target)<20)))continue;
                return origin;
            }
            Assert.Fail("No isolated walkable detector fixture lane");return Vector2.zero;
        }
        [TestCase("explorer",8,true)] [TestCase("envoy",8,true)] [TestCase("native_scout",8,true)]
        [TestCase("town",8,true)] [TestCase("tower",8,true)] [TestCase("native_scout",8.01f,false)] [TestCase("musketeer",7.99f,false)]
        public void ActualStealthCommandAndReplicaRespectOnlyConfiguredDetectorsAndTheirEightUnitBoundary(string detector,float distance,bool expected)
        {
            var world=Prepared(false,detector=="native_scout"?"france":detector=="envoy"?"river":"coast");
            CollectionAssert.AreEquivalent(new[]{"explorer","envoy","native_scout","town","tower"},world.Rules.entities.Where(d=>d.detectionRange>0&&d.id!="guardian").Select(d=>d.id));
            var point=OpenDetectorLane(world,distance,detector);var observer=world.Spawn(detector,1,point);observer.hold=true;
            var scout=world.Spawn("native_scout",0,point+Vector2.right*distance);world.Fog.Tick(world.Entities);
            Assert.AreEqual(VisibilityState.Visible,world.Fog.At(1,scout.position),"Geometry is visible before testing stealth filtering");Assert.IsTrue(world.IsVisible(1,scout));
            using(var session=new Session(world))
            {
                session.Send(Stealth(scout),retransmit:true);Assert.IsTrue(scout.stealth);Assert.AreEqual(1,session.applied);
                Assert.AreEqual(expected,world.IsVisible(1,scout));Assert.IsTrue(world.IsVisible(0,scout));Assert.AreEqual(3,world.Speed(scout),.001f);
                session.Publish();Assert.AreEqual(expected,session.replica.World.IsVisible(1,session.replica.World.Get(scout.id)));
                Step(session.simulation);session.Publish();Assert.IsTrue(scout.stealth);Assert.AreEqual(expected,world.IsVisible(1,scout));
                var document=CheckColdReplay(session,new Checkpoint(session.simulation));
                Assert.AreEqual(1,document.records.Count(r=>r.hasCommand&&r.command.kind==CommandKind.ToggleStealth&&r.accepted));
            }
        }

        [TestCase(7.99f,true)] [TestCase(8,true)] [TestCase(8.01f,false)]
        public void NeutralGuardianUsesItsOwnEightUnitDetectionAndRealChaseWithoutSharingPlayerDetection(float distance,bool detects)
        {
            var world=Prepared(false);Assert.AreEqual(8,world.Rules.Entity("guardian").detectionRange);
            var point=OpenDetectorLane(world,distance,"guardian");var guardian=world.Spawn("guardian",-1,point);
            var scout=world.Spawn("native_scout",0,point+Vector2.right*distance);world.Fog.Tick(world.Entities);world.Fog.Reveal(1,scout.position,2);
            using(var session=new Session(world))
            {
                session.Send(Stealth(scout));Assert.AreEqual(VisibilityState.Visible,world.Fog.At(1,scout.position));
                Assert.IsFalse(world.IsVisible(1,scout),"Neutral personal detection is not an allied player detector");
                var start=guardian.position;Step(session.simulation);Assert.IsTrue(scout.stealth);Assert.IsTrue(scout.Alive);
                Assert.AreEqual(detects?scout.id:0,guardian.attackTarget);
                if(detects)Assert.Greater(Vector2.Distance(start,guardian.position),.01f,"A real neutral scan must begin pursuit");
                else Assert.Less(Vector2.Distance(start,guardian.position),.0001f,"Nine-unit vision does not bypass the eight-unit stealth detector");
                session.Publish();CheckColdReplay(session,new Checkpoint(session.simulation));
            }
        }
    }
}
