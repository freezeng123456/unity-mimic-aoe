using System;
using UnityEngine;

namespace Frontier
{
    public sealed class ReplaySession:IDisposable
    {
        public World World {get;private set;}
        public MatchSimulation Simulation {get;private set;}
        public GuidedPlaySession Guided {get;private set;}
        public string Error {get;private set;}="";
        public int StartStep=>document.anchors[0].completedStep;
        public int EndStep=>document.anchors[document.anchors.Length-1].completedStep;
        public int CurrentStep=>Simulation?.StepCount??StartStep;
        public bool Verified {get;private set;}
        public bool IsSeeking {get;private set;}
        public int SeekTargetStep {get;private set;}
        public int SeekAnchorStep {get;private set;}
        public int LastSeekSimulatedSteps {get;private set;}
        readonly RuleCatalog rules;
        readonly ReplayDocument document;
        ReplayEventChain chain;
        int recordIndex,nextAnchor;
        bool disposed;
        public ReplaySession(RuleCatalog rules,ReplayDocument document,ReplayIdentity expectedIdentity)
        {
            ReplayValidation.Validate(rules,document,expectedIdentity);
            this.rules=rules;this.document=ReplayRecorder.CloneDocument(document);
            RestoreAnchor(0);ProcessBoundary();
        }
        void Fail(string reason)
        {if(Error=="")Error="Replay mismatch: "+reason;IsSeeking=false;Verified=false;}
        void RestoreAnchor(int index)
        {
            Guided?.Dispose();Guided=null;chain?.Dispose();chain=null;
            var anchor=document.anchors[index];var history=new SimEvent[anchor.eventCount];
            for(int i=0;i<history.Length;i++)history[i]=ReplayEventCopy.Copy(document.events[i]);
            World=World.RestoreReplay(rules,anchor.world,history);Simulation=MatchSimulation.Restore(World,anchor.simulation);
            if(anchor.hasGuided)Guided=GuidedPlaySession.Restore(World,Simulation,anchor.guided);
            chain=new ReplayEventChain(anchor.eventCount,anchor.eventHash);recordIndex=anchor.recordIndex;nextAnchor=index;
            Verified=false;CheckAnchors();
        }
        bool SyncEvents()
        {
            if(World.Events.Count<chain.Count){Fail("event history truncated");return false;}
            while(chain.Count<World.Events.Count)
            {
                int index=chain.Count;if(index>=document.events.Length){Fail("unexpected event at "+index);return false;}
                var actual=ReplayEventCopy.Copy(World.Events[index]);
                if(JsonUtility.ToJson(actual)!=JsonUtility.ToJson(document.events[index])){Fail("event "+index+" at completed step "+CurrentStep);return false;}
                chain.Append(actual);
            }
            return true;
        }
        void CheckAnchors()
        {
            while(Error==""&&nextAnchor<document.anchors.Length)
            {
                var anchor=document.anchors[nextAnchor];
                if(anchor.completedStep>CurrentStep||anchor.completedStep==CurrentStep&&anchor.recordIndex>recordIndex)return;
                if(anchor.completedStep!=CurrentStep||anchor.recordIndex!=recordIndex){Fail("missed anchor boundary");return;}
                if(!SyncEvents())return;
                if(anchor.eventCount!=chain.Count||anchor.eventHash!=chain.Hash){Fail("anchor event prefix at "+CurrentStep);return;}
                var world=World.CaptureState(false);var simulation=ReplayDigest.SimulationState(Simulation);var guided=Guided?.CaptureState();
                if(anchor.stateDigest!=ReplayDigest.FromState(world,simulation,Guided!=null,guided,chain.Hash)){Fail("state digest at completed step "+CurrentStep);return;}
                nextAnchor++;
            }
        }
        void ProcessBoundary()
        {
            if(Error!="")return;CheckAnchors();
            while(Error==""&&recordIndex<document.records.Length&&document.records[recordIndex].completedStep==CurrentStep)
            {
                var record=document.records[recordIndex];
                switch(record.kind)
                {
                    case ReplayOperation.Command:
                        var command=ReplayRecorder.Command(record);var result=Guided==null?World.Issue(command):Guided.Issue(command);
                        if(result.accepted!=record.accepted||(result.reason??"")!=record.reason){Fail("command outcome at step "+CurrentStep+" ordinal "+record.ordinal);return;}
                        break;
                    case ReplayOperation.Pause:
                        World.Paused=record.paused;World.Emit("PauseChanged",0,0,record.reason,record.paused?1:0);break;
                    case ReplayOperation.GuidedSelection:
                        if(Guided==null){Fail("selection outside guided play");return;}Guided.ObserveSelection(record.selection);break;
                    case ReplayOperation.FlushEconomy:World.FlushEconomyLog();break;
                    default:Fail("unknown operation");return;
                }
                if(!SyncEvents())return;
                if(record.eventCount!=chain.Count||record.eventHash!=chain.Hash){Fail("operation event prefix at step "+CurrentStep+" ordinal "+record.ordinal);return;}
                recordIndex++;CheckAnchors();
            }
            if(Error!="")return;
            if(recordIndex<document.records.Length&&document.records[recordIndex].completedStep<CurrentStep){Fail("missed operation boundary");return;}
            if(CurrentStep==EndStep)
            {
                CheckAnchors();
                if(recordIndex!=document.records.Length||nextAnchor!=document.anchors.Length)Fail("unfinished final boundary");
                else Verified=true;
            }
        }
        public int Advance(int maxSteps)
        {
            if(disposed)throw new ObjectDisposedException(nameof(ReplaySession));
            if(maxSteps<0)throw new ArgumentOutOfRangeException(nameof(maxSteps));
            if(IsSeeking)return AdvanceSeek(maxSteps);
            return AdvanceTo(EndStep,maxSteps);
        }
        int AdvanceTo(int target,int maxSteps)
        {
            int advanced=0;ProcessBoundary();
            while(Error==""&&CurrentStep<target&&advanced<maxSteps)
            {
                if(!Simulation.Step()){Fail("simulation stopped before recorded final step");break;}
                advanced++;if(!SyncEvents())break;ProcessBoundary();
            }
            return advanced;
        }
        public bool BeginSeek(int completedStep)
        {
            if(disposed)throw new ObjectDisposedException(nameof(ReplaySession));
            if(completedStep<StartStep||completedStep>EndStep)throw new ArgumentOutOfRangeException(nameof(completedStep));
            Error="";Verified=false;SeekTargetStep=completedStep;LastSeekSimulatedSteps=0;
            int index=0;for(int i=1;i<document.anchors.Length&&document.anchors[i].completedStep<=completedStep;i++)index=i;
            SeekAnchorStep=document.anchors[index].completedStep;
            try{RestoreAnchor(index);ProcessBoundary();}
            catch(ArgumentException e){Fail(e.Message);return false;}
            IsSeeking=Error==""&&CurrentStep<completedStep;
            return Error=="";
        }
        public int AdvanceSeek(int maxSteps)
        {
            if(disposed)throw new ObjectDisposedException(nameof(ReplaySession));
            if(maxSteps<0)throw new ArgumentOutOfRangeException(nameof(maxSteps));
            if(!IsSeeking||Error!="")return 0;
            int advanced=AdvanceTo(SeekTargetStep,maxSteps);LastSeekSimulatedSteps+=advanced;
            if(CurrentStep>=SeekTargetStep||Error!="")IsSeeking=false;
            return advanced;
        }
        public bool Seek(int completedStep)
        {
            if(!BeginSeek(completedStep))return false;
            while(IsSeeking&&Error=="")AdvanceSeek(1000);
            return Error=="";
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;IsSeeking=false;Guided?.Dispose();chain?.Dispose();
        }
    }
}
