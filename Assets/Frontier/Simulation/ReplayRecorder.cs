using System;
using System.Collections.Generic;

namespace Frontier
{
    // External actions enter here once. AI and guided raids are reconstructed by
    // the ordinary fixed scheduler and must never be recorded a second time.
    public sealed class ReplayRecorder:IDisposable
    {
        public const int MaxRecords=200000,MaxEvents=1000000,MaxAnchors=10000;
        public World World=>Simulation.World;
        public string SegmentId=>segmentId;
        public string MatchId=>matchId;
        public int StartStep=>startStep;
        public int RecordedEndStep=>finished!=null?finished.anchors[finished.anchors.Length-1].completedStep:IsRecording?Simulation.StepCount:anchors[anchors.Count-1].completedStep;
        public MatchSimulation Simulation {get;}
        public GuidedPlaySession Guided {get;}
        public string Error {get;private set;}="";
        public bool IsRecording=>!sealedRecording&&Error=="";
        readonly ReplayIdentity identity;
        readonly string segmentId,matchId,startKind;
        readonly int anchorInterval,startStep;
        readonly List<ReplayRecord> records=new List<ReplayRecord>();
        readonly List<ReplayAnchor> anchors=new List<ReplayAnchor>();
        readonly List<SimEvent> events=new List<SimEvent>();
        ReplayEventChain chain=new ReplayEventChain();
        ReplayInputChain inputs=new ReplayInputChain();
        bool sealedRecording,disposed;
        ReplayDocument finished;
        int[] lastSelection=Array.Empty<int>();
        public ReplayRecorder(MatchSimulation simulation,ReplayIdentity identity,GuidedPlaySession guided=null,string segmentId=null,int anchorInterval=300,string matchId=null,string startKind="normal")
        {
            Simulation=simulation??throw new ArgumentNullException(nameof(simulation));Guided=guided;
            if(guided!=null&&(!ReferenceEquals(guided.World,World)||!ReferenceEquals(guided.Simulation,simulation)))throw new ArgumentException("Guided replay uses a different world/scheduler");
            ReplayValidation.ValidateIdentity(World.Rules,identity,identity);
            if(anchorInterval<1||anchorInterval>3000)throw new ArgumentOutOfRangeException(nameof(anchorInterval));
            this.identity=SnapshotCodec.Clone(identity);this.segmentId=segmentId??Guid.NewGuid().ToString("N");this.matchId=matchId??this.segmentId;this.startKind=startKind;
            if(string.IsNullOrWhiteSpace(this.segmentId)||this.segmentId.Length>128||this.matchId.Length>128||string.IsNullOrWhiteSpace(startKind)||startKind.Length>128)throw new ArgumentException("Invalid replay segment labels");
            this.anchorInterval=anchorInterval;startStep=Simulation.StepCount;
            if(World.Events.Count>MaxEvents)throw new ArgumentException("Loaded event history exceeds recording limit");
            ObserveEvents();anchors.Add(TakeAnchor());lastSelection=Guided?.CaptureState().selection??Array.Empty<int>();Simulation.StepSettled+=OnStepSettled;
        }
        public CommandResult Issue(GameCommand command)
        {
            bool capture=PrepareOperation();
            ReplayRecord record=null;
            if(capture)
            {
                if(command!=null&&(!SnapshotCodec.Finite(command.point)||(command.actors?.Length??0)>100000||(command.item?.Length??0)>4096||(command.choice?.Length??0)>4096))
                {Abort("Command payload cannot be represented by the bounded replay format");capture=false;}
                else
                {
                    record=Operation(ReplayOperation.Command);record.hasCommand=command!=null;
                    if(command!=null)
                    {
                        record.commandActorsNull=command.actors==null;record.commandItemNull=command.item==null;record.commandChoiceNull=command.choice==null;
                        record.command=CopyCommand(command);record.command.actors??=Array.Empty<int>();record.command.item??="";record.command.choice??="";
                    }
                }
            }
            var result=Guided==null?World.Issue(command):Guided.Issue(command);
            if(capture){record.accepted=result.accepted;record.reason=result.reason??"";CompleteOperation(record);}
            return result;
        }
        public void ObserveSelection(int[] selection)
        {
            if(Guided==null)return;
            var next=selection??Array.Empty<int>();var current=lastSelection;
            // HUD selection observation occurs every frame. Record only changes
            // in the normalized gameplay selection, never an idle frame stream.
            var normalized=new List<int>();foreach(int id in next){var e=World.Get(id);if(e!=null&&e.Alive&&e.owner==0&&e.garrisonedIn==0&&!normalized.Contains(id))normalized.Add(id);}
            bool same=current.Length==normalized.Count;for(int i=0;same&&i<current.Length;i++)same=current[i]==normalized[i];
            if(same||Guided.Status!=GuidedPlayStatus.Running)return;
            bool capture=PrepareOperation();var record=capture?Operation(ReplayOperation.GuidedSelection):null;
            if(capture)record.selection=normalized.ToArray();
            lastSelection=normalized.ToArray();Guided.ObserveSelection(lastSelection);if(capture){record.accepted=true;CompleteOperation(record);}
        }
        public void Pause(bool paused,string reason)
        {
            bool capture=PrepareOperation();var record=capture?Operation(ReplayOperation.Pause):null;
            if(capture){record.paused=paused;record.reason=reason??"";record.accepted=true;}
            World.Paused=paused;World.Emit("PauseChanged",0,0,reason??"",paused?1:0);
            if(capture)CompleteOperation(record);
        }
        public void FlushEconomy()
        {
            bool capture=PrepareOperation();var record=capture?Operation(ReplayOperation.FlushEconomy):null;
            World.FlushEconomyLog();if(capture){record.accepted=true;CompleteOperation(record);}
        }
        bool PrepareOperation()
        {
            if(!IsRecording)return false;
            if(records.Count>=MaxRecords){Abort("Replay command limit reached");return false;}
            return true;
        }
        ReplayRecord Operation(ReplayOperation kind)
        {
            int ordinal=records.Count>0&&records[records.Count-1].completedStep==Simulation.StepCount?records[records.Count-1].ordinal+1:0;
            return new ReplayRecord{completedStep=Simulation.StepCount,ordinal=ordinal,kind=kind};
        }
        void CompleteOperation(ReplayRecord record)
        {
            if(!ObserveEvents())return;
            record.eventCount=chain.Count;record.eventHash=chain.Hash;records.Add(record);inputs.Append(record);
        }
        bool ObserveEvents()
        {
            if(World.Events.Count<events.Count){FreezeLastAnchor("Live event history was truncated");return false;}
            if(World.Events.Count>MaxEvents){FreezeLastAnchor("Replay event limit reached");return false;}
            for(int i=events.Count;i<World.Events.Count;i++){var owned=ReplayEventCopy.Copy(World.Events[i]);events.Add(owned);chain.Append(owned);}
            return true;
        }
        void OnStepSettled()
        {
            if(!IsRecording)return;
            if(!ObserveEvents())return;
            if((Simulation.StepCount-startStep)%anchorInterval==0)
            {
                if(anchors.Count>=MaxAnchors-1){FreezeLastAnchor("Replay seek-anchor limit reached");return;}
                anchors.Add(TakeAnchor());
            }
        }
        ReplayAnchor TakeAnchor()
        {
            var world=World.CaptureState(false);var simulation=ReplayDigest.SimulationState(Simulation);var guided=Guided?.CaptureState()??new GuidedPlaySnapshot();
            return new ReplayAnchor{completedStep=Simulation.StepCount,recordIndex=records.Count,eventCount=chain.Count,world=world,simulation=simulation,hasGuided=Guided!=null,guided=guided,eventHash=chain.Hash,inputHash=inputs.Hash,
                stateDigest=ReplayDigest.FromState(world,simulation,Guided!=null,guided,chain.Hash)};
        }
        public void Abort(string reason)
        {
            if(!IsRecording)return;
            // Abort before an unrecordable operation: capture a valid current
            // boundary. Limits discovered after a tick use the previous anchor.
            if(World.Events.Count<=MaxEvents&&World.Events.Count>=events.Count)
            {
                ObserveEvents();var anchor=TakeAnchor();AddFinalAnchor(anchors,anchor);
            }
            FreezeLastAnchor(string.IsNullOrWhiteSpace(reason)?"Recording interrupted":reason);
        }
        void FreezeLastAnchor(string reason)
        {
            if(Error!="")return;
            Error=reason.Length>512?reason.Substring(0,512):reason;
            if(anchors.Count==0)throw new InvalidOperationException(Error);
            var anchor=anchors[anchors.Count-1];
            if(records.Count>anchor.recordIndex)records.RemoveRange(anchor.recordIndex,records.Count-anchor.recordIndex);
            if(events.Count>anchor.eventCount)events.RemoveRange(anchor.eventCount,events.Count-anchor.eventCount);
            chain.Dispose();chain=new ReplayEventChain(anchor.eventCount,anchor.eventHash);
            inputs.Dispose();inputs=new ReplayInputChain(anchor.inputHash);
            Simulation.StepSettled-=OnStepSettled;
        }
        static void AddFinalAnchor(List<ReplayAnchor> list,ReplayAnchor anchor)
        {
            var previous=list[list.Count-1];
            if(previous.completedStep==anchor.completedStep&&previous.recordIndex==anchor.recordIndex)list[list.Count-1]=anchor;
            else list.Add(anchor);
        }
        public ReplayDocument Capture(string reason="manual_save")
        {
            if(disposed)throw new ObjectDisposedException(nameof(ReplayRecorder));
            if(finished!=null)return CloneDocument(finished);
            if(string.IsNullOrWhiteSpace(reason)||reason.Length>512)throw new ArgumentException("Invalid replay termination label");
            var points=new List<ReplayAnchor>(anchors);
            if(IsRecording&&ObserveEvents())AddFinalAnchor(points,TakeAnchor());
            else points=new List<ReplayAnchor>(anchors);
            var document=new ReplayDocument{segmentId=segmentId,matchId=matchId,startKind=startKind,finishReason=reason,identity=identity,anchorInterval=anchorInterval,
                incomplete=Error!="",incompleteReason=Error,anchors=points.ToArray(),records=records.ToArray(),events=events.ToArray()};
            return CloneDocument(document);
        }
        public ReplayDocument Finish(string reason="segment_end")
        {
            if(finished==null){finished=Capture(reason);sealedRecording=true;Simulation.StepSettled-=OnStepSettled;}
            return CloneDocument(finished);
        }
        // Clone snapshots/commands once per explicit export. The event ledger is
        // copied as plain DTOs without JSON history cloning or nested aliasing.
        internal static ReplayDocument CloneDocument(ReplayDocument document)
        {
            var ledger=document.events;
            var projection=new ReplayDocument{schema=document.schema,segmentId=document.segmentId,matchId=document.matchId,startKind=document.startKind,finishReason=document.finishReason,
                identity=document.identity,incomplete=document.incomplete,incompleteReason=document.incompleteReason,recordLimit=document.recordLimit,eventLimit=document.eventLimit,
                anchorInterval=document.anchorInterval,anchors=document.anchors,records=document.records,events=Array.Empty<SimEvent>()};
            var copy=SnapshotCodec.Clone(projection);
            copy.events=new SimEvent[ledger.Length];for(int i=0;i<ledger.Length;i++)copy.events[i]=ReplayEventCopy.Copy(ledger[i]);
            return copy;
        }
        internal static GameCommand CopyCommand(GameCommand c)=>c==null?null:new GameCommand{player=c.player,kind=c.kind,actors=c.actors==null?null:(int[])c.actors.Clone(),order=c.order,point=c.point,target=c.target,index=c.index,count=c.count,item=c.item,choice=c.choice,append=c.append};
        internal static GameCommand Command(ReplayRecord record)
        {
            if(!record.hasCommand)return null;var c=CopyCommand(record.command);
            if(record.commandActorsNull)c.actors=null;if(record.commandItemNull)c.item=null;if(record.commandChoiceNull)c.choice=null;return c;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;sealedRecording=true;Simulation.StepSettled-=OnStepSettled;chain.Dispose();inputs.Dispose();
        }
    }
    internal static class ReplayEventCopy
    {
        public static SimEvent Copy(SimEvent e)
        {
            if(e==null)throw new ArgumentException("Missing replay event");
            var transaction=e.economy;var population=e.population;
            // Explicit presence flags carry absence, while initialized nested
            // DTOs keep Unity JSON round trips canonical across save/load.
            return new SimEvent{schemaVersion=e.schemaVersion,hasEconomy=e.hasEconomy,hasPopulation=e.hasPopulation,time=e.time,kind=e.kind,message=e.message,player=e.player,entity=e.entity,target=e.target,visibilityMask=e.visibilityMask,amount=e.amount,position=e.position,facing=e.facing,
                economy=transaction==null?new EconomyTransaction():new EconomyTransaction{requestedDelta=transaction.requestedDelta,actualDelta=transaction.actualDelta,balanceBefore=transaction.balanceBefore,balanceAfter=transaction.balanceAfter,hasRepair=transaction.hasRepair,healthTarget=transaction.healthTarget,hpBefore=transaction.hpBefore,hpAfter=transaction.hpAfter},
                population=population==null?new PopulationSnapshot():new PopulationSnapshot{used=population.used,reserved=population.reserved,capacity=population.capacity}};
        }
    }
}
