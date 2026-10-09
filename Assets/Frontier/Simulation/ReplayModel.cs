using System;
using UnityEngine;

namespace Frontier
{
    [Serializable] public sealed class ReplayIdentity
    {
        public string appVersion="",buildIdentity="",unityVersion="",rulesHash="";
        public int mapGenerationVersion=MapGeneration.Version,guidedCatalogRevision=GuidedPlayCatalog.Revision;
        public int scenarioFormatVersion=ScenarioDocument.CurrentSchema,scenarioRuntimeVersion=ScenarioRuntime.Version;
        public static ReplayIdentity Create(RuleCatalog rules,string appVersion,string buildIdentity,string unityVersion)
        {
            if(rules==null||string.IsNullOrWhiteSpace(appVersion)||string.IsNullOrWhiteSpace(buildIdentity)||string.IsNullOrWhiteSpace(unityVersion))
                throw new ArgumentException("Replay requires explicit application, build and Unity identities");
            return new ReplayIdentity{appVersion=appVersion,buildIdentity=buildIdentity,unityVersion=unityVersion,rulesHash=SnapshotCodec.RulesHash(rules)};
        }
    }
    public enum ReplayOperation {Command,Pause,GuidedSelection,FlushEconomy}
    [Serializable] public sealed class ReplayRecord
    {
        public int completedStep,ordinal;
        public ReplayOperation kind;
        public bool hasCommand,commandActorsNull,commandItemNull,commandChoiceNull;
        public GameCommand command=new GameCommand();
        public int[] selection=Array.Empty<int>();
        public bool paused,accepted;
        public string reason="",eventHash="";
        public int eventCount;
    }
    [Serializable] public sealed class ReplayAnchor
    {
        public int completedStep,recordIndex,eventCount;
        // History is stored once in ReplayDocument.events. Hydrate its prefix only
        // when restoring an anchor; never duplicate it in sparse seek snapshots.
        public WorldSnapshot world;
        public MatchSimulationSnapshot simulation;
        public bool hasGuided;
        public GuidedPlaySnapshot guided=new GuidedPlaySnapshot();
        public string eventHash="",inputHash="",stateDigest="";
    }
    [Serializable] public sealed class ReplayDocument
    {
        public const int CurrentSchema=1;
        public int schema=CurrentSchema;
        public string segmentId="",matchId="",startKind="normal",finishReason="segment_end";
        public ReplayIdentity identity;
        public bool incomplete;
        public string incompleteReason="";
        public int recordLimit=ReplayRecorder.MaxRecords,eventLimit=ReplayRecorder.MaxEvents,anchorInterval=300;
        public ReplayAnchor[] anchors=Array.Empty<ReplayAnchor>();
        public ReplayRecord[] records=Array.Empty<ReplayRecord>();
        public SimEvent[] events=Array.Empty<SimEvent>();
    }
    public static class ReplayValidation
    {
        internal static void Require(bool valid,string reason)
        {if(!valid)throw new ArgumentException("Invalid replay: "+reason);}
        internal static bool Hash(string value)
        {
            if(value==null||value.Length!=64)return false;
            foreach(char c in value)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))return false;
            return true;
        }
        public static void ValidateIdentity(RuleCatalog rules,ReplayIdentity actual,ReplayIdentity expected)
        {
            Require(rules!=null&&actual!=null&&expected!=null,"missing rules/build identity");
            Require(!string.IsNullOrWhiteSpace(actual.appVersion)&&!string.IsNullOrWhiteSpace(actual.buildIdentity)&&!string.IsNullOrWhiteSpace(actual.unityVersion),"empty build identity");
            Require(actual.appVersion==expected.appVersion&&actual.buildIdentity==expected.buildIdentity&&actual.unityVersion==expected.unityVersion,"recording requires its original application/build/Unity version");
            string hash=SnapshotCodec.RulesHash(rules);
            Require(actual.rulesHash==hash&&expected.rulesHash==hash,"rules changed");
            Require(actual.mapGenerationVersion==MapGeneration.Version&&expected.mapGenerationVersion==MapGeneration.Version,"map generation changed");
            Require(actual.guidedCatalogRevision==GuidedPlayCatalog.Revision&&expected.guidedCatalogRevision==GuidedPlayCatalog.Revision,"guided catalog changed");
            Require(actual.scenarioFormatVersion==ScenarioDocument.CurrentSchema&&expected.scenarioFormatVersion==ScenarioDocument.CurrentSchema&&actual.scenarioRuntimeVersion==ScenarioRuntime.Version&&expected.scenarioRuntimeVersion==ScenarioRuntime.Version,"scenario format or execution changed");
        }
        public static void Validate(RuleCatalog rules,ReplayDocument document,ReplayIdentity expectedIdentity)
        {
            Require(document!=null&&document.schema==ReplayDocument.CurrentSchema,"unsupported schema");
            ValidateIdentity(rules,document.identity,expectedIdentity);
            Require(!string.IsNullOrWhiteSpace(document.segmentId)&&document.segmentId.Length<=128&&document.matchId!=null&&document.matchId.Length<=128,"segment/match identity");
            Require(!string.IsNullOrWhiteSpace(document.startKind)&&document.startKind.Length<=128&&!string.IsNullOrWhiteSpace(document.finishReason)&&document.finishReason.Length<=512,"segment labels");
            Require(document.incompleteReason!=null&&document.incompleteReason.Length<=512&&document.incomplete==!string.IsNullOrEmpty(document.incompleteReason),"incomplete status");
            Require(document.recordLimit>0&&document.recordLimit<=ReplayRecorder.MaxRecords&&document.eventLimit>0&&document.eventLimit<=ReplayRecorder.MaxEvents&&document.anchorInterval>=1&&document.anchorInterval<=3000,"recording limits");
            Require(document.records!=null&&document.records.Length<=document.recordLimit&&document.events!=null&&document.events.Length<=document.eventLimit&&document.anchors!=null&&document.anchors.Length>0&&document.anchors.Length<=ReplayRecorder.MaxAnchors,"collections");
            var first=document.anchors[0];var last=document.anchors[document.anchors.Length-1];
            Require(first!=null&&last!=null&&first.recordIndex==0&&last.recordIndex==document.records.Length,"initial/final boundary");
            int start=first.completedStep,end=last.completedStep;
            Require(start>=0&&end>=start,"step range");
            int step=start,ordinal=-1,eventCount=first.eventCount;
            for(int i=0;i<document.records.Length;i++)
            {
                var r=document.records[i];Require(r!=null&&r.completedStep>=step&&r.completedStep<=end&&r.completedStep>=start,"nonmonotonic command stream");
                int nextOrdinal=r.completedStep==step?ordinal+1:0;
                Require(r.ordinal==nextOrdinal,"nonmonotonic boundary ordinal");step=r.completedStep;ordinal=r.ordinal;
                Require(Enum.IsDefined(typeof(ReplayOperation),r.kind)&&r.selection!=null&&r.selection.Length<=100000&&r.reason!=null&&r.reason.Length<=4096,"operation shape");
                Require(r.command!=null&&r.command.actors!=null&&r.command.actors.Length<=100000&&r.command.item!=null&&r.command.item.Length<=4096&&r.command.choice!=null&&r.command.choice.Length<=4096&&SnapshotCodec.Finite(r.command.point),"command payload");
                Require(r.kind==ReplayOperation.Command||!r.hasCommand&&!r.commandActorsNull&&!r.commandItemNull&&!r.commandChoiceNull,"unexpected command payload");
                Require(r.kind==ReplayOperation.Command&&r.hasCommand||!r.commandActorsNull&&!r.commandItemNull&&!r.commandChoiceNull,"null command flags");
                Require(r.kind==ReplayOperation.Command||r.accepted,"non-command operation outcome");
                Require(r.kind==ReplayOperation.GuidedSelection||r.selection.Length==0,"unexpected selection payload");
                Require(r.eventCount>=eventCount&&r.eventCount<=document.events.Length&&Hash(r.eventHash),"operation event boundary");eventCount=r.eventCount;
            }
            int previousStep=-1,previousRecord=-1,previousEvents=-1;
            foreach(var a in document.anchors)
            {
                Require(a!=null&&a.completedStep>=start&&a.completedStep<=end&&a.recordIndex>=0&&a.recordIndex<=document.records.Length,"anchor range");
                Require(a.completedStep>previousStep||a.completedStep==previousStep&&a.recordIndex>previousRecord,"nonmonotonic anchors");
                Require(a.recordIndex>=previousRecord&&a.eventCount>=previousEvents&&a.eventCount<=document.events.Length&&Hash(a.eventHash)&&Hash(a.inputHash)&&Hash(a.stateDigest),"anchor prefix");
                Require(a.world!=null&&a.world.players!=null&&a.world.events!=null&&a.world.events.Length==0&&a.simulation!=null&&a.simulation.stepCount==a.completedStep&&a.simulation.accumulator==0,"anchor projection");
                Require(a.world.seed==first.world.seed&&a.world.mapId==first.world.mapId&&a.world.maxAge==first.world.maxAge&&a.world.players.Length==first.world.players.Length,"segment world changed");
                Require(a.hasGuided==first.hasGuided&&(!a.hasGuided||a.guided!=null&&a.guided.attemptId==first.guided.attemptId&&a.guided.definitionId==first.guided.definitionId),"segment guided identity changed");
                if(a.recordIndex>0)Require(document.records[a.recordIndex-1].completedStep<=a.completedStep&&document.records[a.recordIndex-1].eventCount<=a.eventCount,"anchor precedes its input");
                if(a.recordIndex<document.records.Length)Require(document.records[a.recordIndex].completedStep>=a.completedStep,"anchor skips input");
                World.ValidateSnapshot(rules,a.world);
                Require(a.stateDigest==ReplayDigest.FromState(a.world,a.simulation,a.hasGuided,a.guided,a.eventHash),"anchor digest mismatch");
                previousStep=a.completedStep;previousRecord=a.recordIndex;previousEvents=a.eventCount;
            }
            Require(last.eventCount==document.events.Length,"final event boundary");
            using(var inputs=new ReplayInputChain())
            {
                int index=0;
                foreach(var anchor in document.anchors)
                {
                    while(index<anchor.recordIndex)inputs.Append(document.records[index++]);
                    Require(inputs.Hash==anchor.inputHash,"input ledger digest mismatch");
                }
            }
            // Check every advertised event prefix in one linear pass. History is
            // never re-hashed separately for each seek point or operation.
            var points=new System.Collections.Generic.SortedDictionary<int,string>();
            void Prefix(int count,string hash){Require(count>=0&&count<=document.events.Length,"event prefix range");if(points.TryGetValue(count,out var old))Require(old==hash,"conflicting event prefix");else points.Add(count,hash);}
            foreach(var a in document.anchors)Prefix(a.eventCount,a.eventHash);
            foreach(var r in document.records)Prefix(r.eventCount,r.eventHash);
            using(var chain=new ReplayEventChain())
            {
                int index=0;
                foreach(var point in points)
                {
                    while(index<point.Key)
                    {
                        var e=document.events[index++];Require(e!=null&&e.player>=-2&&e.player<first.world.players.Length&&(e.visibilityMask&~((1<<first.world.players.Length)-1))==0&&e.schemaVersion==SimEvent.CurrentSchemaVersion&&e.kind!=null&&e.message!=null&&SnapshotCodec.Finite(e.time)&&e.time>=0&&SnapshotCodec.Finite(e.amount)&&SnapshotCodec.Finite(e.position)&&SnapshotCodec.Finite(e.facing),"event shape");
                        Require(!e.hasEconomy||e.economy!=null&&SnapshotCodec.Finite(e.economy.requestedDelta)&&SnapshotCodec.Finite(e.economy.actualDelta)&&SnapshotCodec.Finite(e.economy.balanceBefore)&&SnapshotCodec.Finite(e.economy.balanceAfter)&&SnapshotCodec.Finite(e.economy.hpBefore)&&SnapshotCodec.Finite(e.economy.hpAfter),"event transaction");
                        Require(!e.hasPopulation||e.population!=null&&e.population.used>=0&&e.population.reserved>=0&&e.population.capacity>=0,"event population");
                        Require(e.kind!="Heal"||e.hasHealing,"missing healing receipt");
                        Require(!e.hasHealing||e.kind=="Heal"&&e.healing!=null&&e.healing.source==e.entity&&e.healing.target==e.target&&e.healing.source>0&&e.healing.target>0&&e.healing.source!=e.healing.target&&SnapshotCodec.Finite(e.healing.hpBefore)&&SnapshotCodec.Finite(e.healing.hpAfter)&&SnapshotCodec.Finite(e.healing.maxHp)&&SnapshotCodec.Finite(e.healing.actual)&&SnapshotCodec.Finite(e.healing.rate)&&e.healing.hpBefore>0&&e.healing.hpAfter>e.healing.hpBefore&&e.healing.maxHp>=e.healing.hpAfter&&e.healing.actual>0&&e.healing.rate>0&&Mathf.Abs((e.healing.hpAfter-e.healing.hpBefore)-e.healing.actual)<.001f&&Mathf.Abs(e.amount-e.healing.actual)<.001f,"event healing receipt");
                        chain.Append(e);
                    }
                    Require(chain.Hash==point.Value,"event ledger digest mismatch");
                }
            }
        }
    }
}
