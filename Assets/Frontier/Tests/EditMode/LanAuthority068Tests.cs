using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class LanAuthority068Tests
    {
        World world;MatchSimulation simulation;ReplayRecorder recorder;LanAuthority authority;int applied;
        [SetUp] public void SetUp()
        {
            applied=0;
            var rules=Catalog.Load();world=new World(rules,314159,new[]{"coast","river","france","ottoman"},new[]{0,1,0,1},5,MapTopology.GeneratedRiver);
            simulation=new MatchSimulation(world);
            recorder=new ReplayRecorder(simulation,ReplayIdentity.Create(rules,"0.11.0","LAN-test-build","LAN-test-engine"),segmentId:"LAN-authority-fixture");
            authority=Authority();
        }
        [TearDown] public void TearDown()=>recorder?.Dispose();
        LanAuthority Authority(Func<GameCommand,CommandResult> command=null,Func<GameCommand,long,CommandResult> cancel=null)
        {
            var value=new LanAuthority(world,"match-a","epoch-a",command??(c=>{applied++;return recorder.Issue(c);}),
                (paused,reason)=>recorder.Pause(paused,reason),cancel);
            value.BindPeer("host",0,1);value.BindPeer("remote",2,1);return value;
        }
        EntityState Town(int player)=>world.Entities.Single(e=>e.owner==player&&e.def.id=="town");
        EntityState Worker(int player)=>world.Entities.First(e=>e.owner==player&&e.Worker&&e.Alive);
        GameCommand Train(int player)=>new GameCommand{player=player,kind=CommandKind.Train,actors=new[]{Town(player).id},item=world.WorkerDefinition(player).id};
        GameCommand Move(int player)=>new GameCommand{player=player,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{Worker(player).id},point=Worker(player).position+Vector2.right*3};
        static GameCommand Clone(GameCommand c)=>c==null?null:new GameCommand{player=c.player,kind=c.kind,actors=c.actors==null?null:(int[])c.actors.Clone(),order=c.order,point=c.point,target=c.target,index=c.index,count=c.count,item=c.item,choice=c.choice,append=c.append};
        static LanCommandRequest Request(long sequence,GameCommand command,LanOperation operation=LanOperation.Command,string peer="host",int generation=1,long queueId=0)
            =>new LanCommandRequest{matchId="match-a",epoch="epoch-a",peerId=peer,generation=generation,sequence=sequence,operation=operation,command=command,expectedQueueId=queueId};

        [Test] public void PaidTrainIsQueuedOnceAndCachedAckSurvivesAConnectionGenerationChange()
        {
            float before=world.Players[2].resources.food,other=world.Players[0].resources.food;
            var request=Request(1,Train(2),peer:"remote");var pending=authority.Submit(request);Assert.IsFalse(pending.terminal);
            var repeated=authority.Submit(Request(1,Clone(request.command),peer:"remote"));Assert.AreEqual(pending.hostSequence,repeated.hostSequence);Assert.AreEqual(1,authority.PendingCount);
            authority.BindPeer("remote",2,2);var terminal=authority.Drain(17,8).Single();
            Assert.IsTrue(terminal.accepted,terminal.reason);Assert.IsTrue(terminal.terminal);Assert.AreEqual(2,terminal.generation);
            Assert.AreEqual(17,terminal.appliedStep);Assert.AreEqual(0,terminal.ordinal);Assert.AreEqual(8,terminal.stateRevision);Assert.AreEqual(1,applied);
            Assert.AreEqual(1,Town(2).queue.Count);Assert.Less(world.Players[2].resources.food,before);Assert.AreEqual(other,world.Players[0].resources.food);
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="ProductionQueued"&&e.player==2));
            request.generation=2;terminal.reason="caller mutation";var cached=authority.Submit(request);
            Assert.IsTrue(cached.accepted);Assert.AreEqual(8,cached.stateRevision);Assert.AreEqual(17,cached.appliedStep);Assert.AreEqual(pending.hostSequence,cached.hostSequence);
            Assert.AreNotEqual("caller mutation",cached.reason);Assert.IsEmpty(authority.Drain(18,9));Assert.AreEqual(1,applied);
            Assert.AreEqual(1,recorder.Capture().records.Count(r=>r.hasCommand&&r.command.kind==CommandKind.Train));
        }

        [Test] public void ReceivedPendingCommandSurvivesRetirementAndAReplacementSocketWithoutBeingIssuedTwice()
        {
            var request=Request(1,Move(2),peer:"remote");var pending=authority.Submit(request);
            authority.RetirePeer("remote",1);Assert.IsTrue(authority.Submit(request).protocolRejected);
            var terminal=authority.Drain(0,1).Single();Assert.IsTrue(terminal.accepted,terminal.reason);Assert.AreEqual(1,applied);
            authority.BindPeer("remote",2,2);request.generation=2;Assert.AreEqual(terminal.hostSequence,authority.Submit(request).hostSequence);
            Assert.IsEmpty(authority.Drain(0,2));Assert.AreEqual(1,applied);Assert.AreEqual(pending.hostSequence,terminal.hostSequence);
        }

        [Test] public void CapturedCommandAndDigestOwnEveryMutablePayloadBeforeTheDelegateCanModifyIt()
        {
            GameCommand actual=null;authority=Authority(c=>{applied++;actual=Clone(c);c.actors[0]=999999;return CommandResult.Ok();});
            var original=Move(0);original.target=19;original.index=7;original.count=3;original.item="item";original.choice="choice";original.append=true;
            var stable=Clone(original);authority.Submit(Request(1,original));original.actors[0]=Worker(2).id;original.point=Vector2.zero;original.item="changed";original.count=1;
            Assert.IsTrue(authority.Drain(3,4).Single().accepted);Assert.AreEqual(JsonUtility.ToJson(stable),JsonUtility.ToJson(actual));
            Assert.IsTrue(authority.Submit(Request(1,stable)).accepted);Assert.AreEqual(1,applied);
        }

        [TestCase("player")][TestCase("kind")][TestCase("actors")][TestCase("order")][TestCase("pointX")][TestCase("pointY")]
        [TestCase("target")][TestCase("index")][TestCase("count")][TestCase("item")][TestCase("choice")][TestCase("append")][TestCase("operation")]
        public void ReusingASequenceWithAnyDifferentCommandFieldIsAProtocolRejection(string field)
        {
            var command=Move(0);authority.Submit(Request(1,command));var changed=Request(1,Clone(command));
            switch(field)
            {
                case "player":changed.command.player=2;break;case "kind":changed.command.kind=CommandKind.Rally;break;
                case "actors":changed.command.actors=new[]{world.Entities.First(e=>e.owner==0&&e.Worker&&e.id!=command.actors[0]).id};break;
                case "order":changed.command.order=OrderKind.AttackMove;break;case "pointX":changed.command.point.x+=1;break;case "pointY":changed.command.point.y+=1;break;
                case "target":changed.command.target=Town(0).id;break;case "index":changed.command.index=1;break;case "count":changed.command.count=2;break;
                case "item":changed.command.item="x";break;case "choice":changed.command.choice="x";break;case "append":changed.command.append=true;break;
                case "operation":changed.operation=LanOperation.Pause;break;
            }
            var rejected=authority.Submit(changed);Assert.IsTrue(rejected.terminal);Assert.IsTrue(rejected.protocolRejected);Assert.IsFalse(rejected.accepted);
            Assert.AreEqual(1,authority.PendingCount);Assert.IsTrue(authority.Drain(0,1).Single().accepted);Assert.AreEqual(1,applied);
        }

        [Test] public void NullAndEmptyInputFieldsRemainDistinctInThePayloadHash()
        {
            var command=new GameCommand{player=0,kind=CommandKind.Resign,actors=null,item=null,choice=null};authority.Submit(Request(1,command));
            var changed=Clone(command);changed.actors=Array.Empty<int>();Assert.IsTrue(authority.Submit(Request(1,changed)).protocolRejected);
            changed=Clone(command);changed.item="";Assert.IsTrue(authority.Submit(Request(1,changed)).protocolRejected);
            changed=Clone(command);changed.choice="";Assert.IsTrue(authority.Submit(Request(1,changed)).protocolRejected);Assert.AreEqual(1,authority.PendingCount);
        }

        [Test] public void AllActorsMustBelongToTheBoundHumanEvenWhenOneActorWouldHaveBeenFilteredByWorld()
        {
            var command=Move(0);command.actors=new[]{Worker(0).id,Worker(2).id};
            var rejected=authority.Submit(Request(1,command));Assert.IsTrue(rejected.protocolRejected);Assert.IsTrue(rejected.terminal);
            Assert.AreEqual(0,authority.PendingCount);Assert.IsEmpty(Worker(0).orders);Assert.AreEqual(0,applied);
            command.actors=new[]{Worker(0).id};Assert.IsTrue(authority.Submit(Request(1,command)).protocolRejected,"A rejected sequence cannot be repurposed with a now-valid payload");
            command.actors=new[]{Worker(1).id};Assert.IsTrue(authority.Submit(Request(2,command)).protocolRejected);Assert.AreEqual(0,applied);
        }

        [Test] public void OwnedDeadActorsReachTheRecorderAndPreserveWorldRejectionAsATerminalCachedResult()
        {
            var command=Move(0);world.Remove(world.Get(command.actors[0]));authority.Submit(Request(1,command));
            var result=authority.Drain(0,2).Single();Assert.IsFalse(result.accepted);Assert.IsFalse(result.protocolRejected);Assert.AreEqual(1,applied);
            Assert.AreEqual(result.reason,authority.Submit(Request(1,Clone(command))).reason);Assert.IsEmpty(authority.Drain(0,3));Assert.AreEqual(1,applied);
            Assert.IsTrue(recorder.Capture().records.Any(r=>r.hasCommand&&!r.accepted));
        }

        [Test] public void OwnershipIsRecheckedAtDrainSoAChangedActorIsNeverIssuedToTheWrongSlot()
        {
            var command=Move(0);authority.Submit(Request(1,command));world.Get(command.actors[0]).owner=2;
            var result=authority.Drain(0,1).Single();Assert.IsTrue(result.protocolRejected);Assert.AreEqual(0,applied);Assert.IsEmpty(world.Get(command.actors[0]).orders);
        }

        [Test] public void PauseAndResumePumpWhileTheWorldIsPausedAndSystemResignSharesTheSameOrdering()
        {
            var first=authority.Submit(Request(1,null,LanOperation.Pause));var second=authority.Submit(Request(2,Move(0)));
            var results=authority.Drain(0,1);Assert.IsTrue(results[0].accepted);Assert.IsFalse(results[1].accepted);Assert.IsFalse(results[1].protocolRejected);Assert.IsTrue(world.Paused);
            Assert.IsFalse(simulation.Step());Assert.AreEqual(1,applied);Assert.Less(first.hostSequence,second.hostSequence);
            var resume=authority.SubmitSystem(LanOperation.Resume);var resign=authority.SubmitSystem(LanOperation.Command,new GameCommand{player=2,kind=CommandKind.Resign});
            results=authority.Drain(0,2);Assert.IsTrue(results.All(a=>a.accepted),string.Join(",",results.Select(a=>a.reason)));
            Assert.AreEqual(2,results[0].ordinal);Assert.AreEqual(3,results[1].ordinal);Assert.IsFalse(world.Paused);Assert.IsTrue(world.Players[2].resigned);
            Assert.IsTrue(world.IsPlayerActive(0));Assert.IsFalse(world.IsMatchEnded);Assert.AreEqual(2,applied);
            Assert.AreEqual("server",results[0].peerId);Assert.AreEqual(0,results[0].sequence);Assert.AreEqual(resume.hostSequence,results[0].hostSequence);Assert.AreEqual(resign.hostSequence,results[1].hostSequence);
            Assert.AreEqual(2,recorder.Capture().records.Count(r=>r.kind==ReplayOperation.Pause));
        }

        [Test] public void PauseRequestFromRemoteCanCarryTheDeserializerDefaultInactiveCommand()
        {
            authority.Submit(Request(1,new GameCommand(),LanOperation.Pause,"remote"));Assert.IsTrue(authority.Drain(0,1).Single().accepted);Assert.IsTrue(world.Paused);
            authority.Submit(Request(2,null,LanOperation.Resume,"remote"));Assert.IsTrue(authority.Drain(0,2).Single().accepted);Assert.IsFalse(world.Paused);Assert.AreEqual(0,applied);
        }

        [TestCase("operation")][TestCase("kind")][TestCase("order")][TestCase("nan")][TestCase("infinity")][TestCase("coordinate")]
        [TestCase("actors")][TestCase("actorId")][TestCase("target")][TestCase("index")][TestCase("count")][TestCase("string")][TestCase("utf8")]
        public void InvalidBoundedPayloadsRejectBeforeIssueAndTheirSequencesRemainTerminal(string field)
        {
            var request=Request(1,Move(0));
            switch(field)
            {
                case "operation":request.operation=(LanOperation)99;break;case "kind":request.command.kind=(CommandKind)99;break;case "order":request.command.order=(OrderKind)99;break;
                case "nan":request.command.point.x=float.NaN;break;case "infinity":request.command.point.y=float.PositiveInfinity;break;
                case "coordinate":request.command.point.x=LanAuthority.MaxCoordinateMagnitude+10;break;
                case "actors":request.command.actors=new int[LanProtocol.MaxActors+1];break;case "actorId":request.command.actors[0]=-1;break;
                case "target":request.command.target=-1;break;case "index":request.command.index=LanAuthority.MaxCommandIndex+1;break;
                case "count":request.command.count=0;break;case "string":request.command.item=new string('x',LanProtocol.MaxString+1);break;
                case "utf8":request.command.choice="\ud800";break;
            }
            var result=authority.Submit(request);Assert.IsTrue(result.terminal);Assert.IsTrue(result.protocolRejected);Assert.AreEqual(0,applied);Assert.AreEqual(0,authority.PendingCount);
            Assert.AreEqual(result.hostSequence,authority.Submit(request).hostSequence);Assert.IsTrue(authority.Submit(Request(1,Move(0))).protocolRejected);
            Assert.IsEmpty(authority.Drain(0,1));Assert.AreEqual(0,applied);
        }

        [Test] public void MatchEpochPeerSlotGenerationAndSequenceAuthenticationNeverIssuesACommand()
        {
            Assert.IsTrue(authority.Submit(null).protocolRejected);
            foreach(string field in new[]{"match","epoch","peer","generation","sequence","player"})
            {
                var request=Request(1,Move(0));
                switch(field){case "match":request.matchId="other";break;case "epoch":request.epoch="other";break;case "peer":request.peerId="unknown";break;case "generation":request.generation=2;break;case "sequence":request.sequence=0;break;case "player":request.command.player=2;break;}
                Assert.IsTrue(authority.Submit(request).protocolRejected,field);
            }
            Assert.IsEmpty(authority.Drain(0,1));Assert.AreEqual(0,applied);
            Assert.Throws<ArgumentException>(()=>authority.BindPeer("other-host",0,1));Assert.Throws<ArgumentOutOfRangeException>(()=>authority.BindPeer("ai",1,1));
            Assert.Throws<ArgumentException>(()=>authority.BindPeer("server",2,1));Assert.Throws<ArgumentException>(()=>authority.BindPeer("host",2,1));
            authority.BindPeer("host",0,2);Assert.Throws<ArgumentException>(()=>authority.BindPeer("host",0,1));
            var valid=authority.Submit(Request(2,Move(0),generation:2));Assert.IsFalse(valid.terminal);Assert.IsTrue(authority.Drain(0,2).Single().accepted);
        }

        [Test] public void PendingAndTerminalWindowsAreBoundedAndEvictedOrSkippedOldSequencesCannotExecuteAgain()
        {
            authority=Authority(c=>{applied++;return CommandResult.Fail("controlled game rejection");});
            for(int i=1;i<=LanProtocol.MaxPeerPending;i++)Assert.IsFalse(authority.Submit(Request(i,Move(0))).terminal);
            var rejected=authority.Submit(Request(LanProtocol.MaxPeerPending+1,Move(0)));Assert.IsTrue(rejected.protocolRejected);
            Assert.AreEqual(LanProtocol.MaxPeerPending,authority.PendingCount);authority.Drain(0,1);Assert.AreEqual(LanProtocol.MaxPeerPending,applied);
            for(int i=LanProtocol.MaxPeerPending+2;i<=LanProtocol.ResultWindow+10;i++){authority.Submit(Request(i,Move(0)));authority.Drain(0,i);}
            int issued=applied;var expired=authority.Submit(Request(1,Move(0)));Assert.IsTrue(expired.protocolRejected);StringAssert.Contains("expired",expired.reason);
            Assert.IsEmpty(authority.Drain(0,LanProtocol.ResultWindow+20));Assert.AreEqual(issued,applied);
            var skipped=authority.Submit(Request(LanProtocol.ResultWindow+100,Move(0)));Assert.IsFalse(skipped.terminal);
            expired=authority.Submit(Request(LanProtocol.ResultWindow+50,Move(0)));Assert.IsTrue(expired.protocolRejected);StringAssert.Contains("expired",expired.reason);
            Assert.AreEqual(1,authority.PendingCount);
        }

        [Test] public void HostQueueLimitAlsoCoversTrustedSystemWork()
        {
            for(int i=0;i<LanProtocol.MaxHostPending;i++)Assert.IsFalse(authority.SubmitSystem(LanOperation.Pause).terminal);
            Assert.AreEqual(LanProtocol.MaxHostPending,authority.PendingCount);Assert.IsTrue(authority.SubmitSystem(LanOperation.Resume).protocolRejected);
            var result=authority.Submit(Request(1,Move(0)));Assert.IsTrue(result.protocolRejected);Assert.AreEqual(0,applied);
        }

        [Test] public void StableQueueIdIsHashedAndTheCancellationCallbackCorrectsOnlyTheAppliedClone()
        {
            int cancellations=0;authority=Authority(cancel:(command,id)=>
            {
                cancellations++;if(id!=37)return CommandResult.Fail("Queue entry expired");
                command.index=0;applied++;return recorder.Issue(command);
            });
            authority.Submit(Request(1,Train(0)));Assert.IsTrue(authority.Drain(0,1).Single().accepted);Assert.AreEqual(1,Town(0).queue.Count);
            var cancel=new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{Town(0).id},index=7};
            Assert.IsTrue(authority.Submit(Request(2,cancel,queueId:0)).protocolRejected);
            authority.Submit(Request(3,cancel,queueId:37));Assert.IsTrue(authority.Submit(Request(3,Clone(cancel),queueId:38)).protocolRejected);
            var result=authority.Drain(0,2).Single();Assert.IsTrue(result.accepted,result.reason);Assert.IsEmpty(Town(0).queue);Assert.AreEqual(7,cancel.index);
            Assert.IsTrue(authority.Submit(Request(3,Clone(cancel),queueId:37)).accepted);Assert.AreEqual(1,cancellations);Assert.AreEqual(2,applied);
            Assert.AreEqual(0,recorder.Capture().records.Single(r=>r.hasCommand&&r.command.kind==CommandKind.CancelQueue).command.index);
            Assert.IsTrue(authority.Submit(Request(4,Move(0),queueId:37)).protocolRejected);
        }

        [Test] public void AStaleQueueCallbackResultIsCachedAndCannotCancelAnotherOrderAfterRetransmission()
        {
            int cancellations=0;authority=Authority(cancel:(command,id)=>{cancellations++;return CommandResult.Fail("Queue entry expired");});
            authority.Submit(Request(1,Train(0)));authority.Drain(0,1);float paid=world.Players[0].resources.food;
            var cancel=new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{Town(0).id}};
            authority.Submit(Request(2,cancel,queueId:17));var result=authority.Drain(0,2).Single();Assert.IsFalse(result.accepted);Assert.AreEqual("Queue entry expired",result.reason);
            Assert.IsFalse(authority.Submit(Request(2,Clone(cancel),queueId:17)).accepted);Assert.IsEmpty(authority.Drain(0,3));
            Assert.AreEqual(1,cancellations);Assert.AreEqual(paid,world.Players[0].resources.food);Assert.AreEqual(1,Town(0).queue.Count);
        }

        [Test] public void ExecutionFailureIsTerminalAndOwningThreadAndMonotonicBoundaryAreEnforced()
        {
            int owner=Thread.CurrentThread.ManagedThreadId;
            authority=Authority(c=>{Assert.AreEqual(owner,Thread.CurrentThread.ManagedThreadId);applied++;throw new InvalidOperationException("controlled failure");});
            var request=Request(1,Move(0));authority.Submit(request);var result=authority.Drain(10,2).Single();Assert.IsFalse(result.accepted);StringAssert.Contains("controlled failure",result.reason);
            Assert.IsTrue(authority.Submit(request).terminal);Assert.IsEmpty(authority.Drain(10,3));Assert.AreEqual(1,applied);
            Assert.Throws<ArgumentOutOfRangeException>(()=>authority.Drain(9,3));Assert.Throws<ArgumentOutOfRangeException>(()=>authority.Drain(10,2));
            var crossThread=Request(2,Move(0));Assert.Throws<InvalidOperationException>(()=>Task.Run(()=>authority.Submit(crossThread)).GetAwaiter().GetResult());
        }
    }
}
