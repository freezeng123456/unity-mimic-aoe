using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public static class LanProjection
    {
        public static string Digest(WorldSnapshot state)=>SnapshotCodec.Hash(JsonUtility.ToJson(state));
        [Serializable] sealed class QueueRows {public LanQueueIdentity[] rows;}
        public static string Digest(WorldSnapshot state,LanQueueIdentity[] queues)=>SnapshotCodec.Hash(JsonUtility.ToJson(state)+"\n"+JsonUtility.ToJson(new QueueRows{rows=queues}));
        static LanQueueIdentity[] CopyQueues(LanQueueIdentity[] rows)=>Array.ConvertAll(rows,row=>new LanQueueIdentity{entity=row.entity,jobs=(long[])row.jobs.Clone()});
        public static void Validate(RuleCatalog rules,WorldSnapshot state)
        {
            World.ValidateSnapshot(rules,state);
            if(state.schema!=WorldSnapshot.CurrentSchema||state.events.Length!=0||state.entities.Length>20000)throw new ArgumentException("Invalid network projection");
            foreach(var nav in new[]{state.landNavigation,state.waterNavigation})
                if(nav==null||nav.width!=128||nav.height!=128||nav.version<1||nav.blocked?.Length!=16384||nav.terrain?.Length!=16384)throw new ArgumentException("Invalid network navigation");
            foreach(var player in state.players)
            {
                var ids=new HashSet<int>();foreach(var memory in player.enemyMemory)
                    if(memory==null||memory.id<1||!ids.Add(memory.id)||!SnapshotCodec.Finite(memory.point))throw new ArgumentException("Invalid network enemy memory");
            }
            var sources=new HashSet<int>();foreach(var income in state.incomes)
                if(income==null||!sources.Add(income.source)||!SnapshotCodec.Finite(income.amount))throw new ArgumentException("Invalid network income cache");
            foreach(var entity in state.entities)foreach(var point in entity.path)if(!SnapshotCodec.Finite(point))throw new ArgumentException("Invalid network path");
            foreach(var obstacle in state.terrainObstacles)if(!SnapshotCodec.Finite(obstacle.x)||!SnapshotCodec.Finite(obstacle.y)||!SnapshotCodec.Finite(obstacle.z))throw new ArgumentException("Invalid network terrain");
            foreach(var projectile in state.projectiles)
                if(!SnapshotCodec.Finite(projectile.start)||!SnapshotCodec.Finite(projectile.end)||!SnapshotCodec.Finite(projectile.firedAt)||!SnapshotCodec.Finite(projectile.baseDamage)||!SnapshotCodec.Finite(projectile.siegeDamage)||!SnapshotCodec.Finite(projectile.splash)||projectile.splash<0||!Enum.IsDefined(typeof(DamageKind),projectile.kind)||!Enum.IsDefined(typeof(DamageKind),projectile.baseKind))throw new ArgumentException("Invalid network projectile");
        }
        static bool Equal<T>(T a,T b)=>JsonUtility.ToJson(a)==JsonUtility.ToJson(b);
        public static LanFullState Full(World world,string matchId,string epoch,long revision,int step,LanQueueIdentity[] queues=null)
        {
            var state=world.CaptureState(false);
            queues??=Array.Empty<LanQueueIdentity>();LanQueueIndex.Validate(state,queues);queues=CopyQueues(queues);
            return new LanFullState{matchId=matchId,epoch=epoch,revision=revision,completedStep=step,eventCursor=world.Events.Count,world=state,queues=queues,digest=Digest(state,queues)};
        }
        public static LanStateDelta Delta(LanFullState previous,LanFullState next,IReadOnlyList<SimEvent> events)
        {
            if(previous.matchId!=next.matchId||previous.epoch!=next.epoch||next.revision<=previous.revision||next.eventCursor<previous.eventCursor)
                throw new ArgumentException("Invalid network delta boundaries");
            if(next.eventCursor-previous.eventCursor>4096)throw new InvalidOperationException("Network event gap requires a full state");
            var old=new Dictionary<int,EntitySnapshot>();foreach(var entity in previous.world.entities)old.Add(entity.state.id,entity);
            var changed=new List<EntitySnapshot>();var order=new int[next.world.entities.Length];
            for(int i=0;i<next.world.entities.Length;i++)
            {var entity=next.world.entities[i];order[i]=entity.state.id;if(!old.TryGetValue(entity.state.id,out var prior)||!Equal(prior,entity))changed.Add(SnapshotCodec.Clone(entity));old.Remove(entity.state.id);}
            var removed=new List<int>(old.Keys);removed.Sort();var globals=SnapshotCodec.Clone(next.world);globals.entities=Array.Empty<EntitySnapshot>();
            bool land=!Equal(previous.world.landNavigation,globals.landNavigation),water=!Equal(previous.world.waterNavigation,globals.waterNavigation);
            bool visibility=!ArraysEqual(previous.world.visibility,globals.visibility),terrain=!ArraysEqual(previous.world.terrainObstacles,globals.terrainObstacles);
            if(!land)globals.landNavigation=null;if(!water)globals.waterNavigation=null;
            if(!visibility)globals.visibility=Array.Empty<int>();if(!terrain)globals.terrainObstacles=Array.Empty<Vector3>();
            if(previous.world.hasScenarioDocument!=next.world.hasScenarioDocument||previous.world.scenarioHash!=next.world.scenarioHash)throw new InvalidOperationException("Scenario identity changed during the match");
            globals.scenarioDocument=null;
            var tail=new SimEvent[(int)(next.eventCursor-previous.eventCursor)];
            for(int i=0;i<tail.Length;i++)tail[i]=ReplayEventCopy.Copy(events[(int)previous.eventCursor+i]);
            return new LanStateDelta{matchId=next.matchId,epoch=next.epoch,baseRevision=previous.revision,revision=next.revision,completedStep=next.completedStep,
                eventStart=previous.eventCursor,eventCursor=next.eventCursor,digest=next.digest,globals=globals,changed=changed.ToArray(),removed=removed.ToArray(),entityOrder=order,
                hasLandNavigation=land,hasWaterNavigation=water,hasVisibility=visibility,hasTerrain=terrain,hasScenarioDocument=false,events=tail,queues=CopyQueues(next.queues)};
        }
        static bool ArraysEqual<T>(T[] a,T[] b)
        {if(a==null||b==null||a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!EqualityComparer<T>.Default.Equals(a[i],b[i]))return false;return true;}
    }
    public sealed class LanReplica
    {
        readonly RuleCatalog rules;
        WorldSnapshot baseline;
        public World World {get;private set;}
        public string MatchId {get;private set;}="";
        public string Epoch {get;private set;}="";
        public long Revision {get;private set;}
        public long EventCursor {get;private set;}
        public int CompletedStep {get;private set;}
        public bool NeedsFullState {get;private set;}=true;
        public string Error {get;private set;}="";
        public LanQueueIndex Queues {get;}=new LanQueueIndex();
        LanQueueIdentity[] queueSnapshot=Array.Empty<LanQueueIdentity>();
        static LanQueueIdentity[] CopyQueues(LanQueueIdentity[] rows)=>Array.ConvertAll(rows,row=>new LanQueueIdentity{entity=row.entity,jobs=(long[])row.jobs.Clone()});
        public LanQueueIdentity[] QueueSnapshot=>CopyQueues(queueSnapshot);
        public LanReplica(RuleCatalog rules){this.rules=rules??throw new ArgumentNullException(nameof(rules));}
        public void Install(LanFullState full)
        {
            if(full==null||string.IsNullOrEmpty(full.matchId)||string.IsNullOrEmpty(full.epoch)||full.revision<1||full.completedStep<0||full.eventCursor<0)
                throw new ArgumentException("Invalid full network state");
            if(World!=null&&MatchId==full.matchId&&Epoch==full.epoch&&(full.revision<Revision||full.completedStep<CompletedStep||full.eventCursor<EventCursor))throw new ArgumentException("Stale full network state");
            LanProjection.Validate(rules,full.world);var owned=SnapshotCodec.Clone(full.world);
            LanQueueIndex.Validate(owned,full.queues);
            if(LanProjection.Digest(owned,full.queues)!=full.digest)throw new ArgumentException("Full network digest mismatch");
            if(World==null||MatchId!=full.matchId||Epoch!=full.epoch)World=Frontier.World.Restore(rules,owned);
            else World.ApplyReplica(owned);
            Queues.Replace(full.queues);queueSnapshot=CopyQueues(full.queues);World.Events.Clear();baseline=owned;MatchId=full.matchId;Epoch=full.epoch;Revision=full.revision;CompletedStep=full.completedStep;EventCursor=full.eventCursor;NeedsFullState=false;Error="";
        }
        public bool Apply(LanStateDelta delta)
        {
            if(delta!=null&&delta.matchId==MatchId&&delta.epoch==Epoch&&delta.revision<=Revision)return false;
            try
            {
                if(NeedsFullState||baseline==null||delta==null||delta.matchId!=MatchId||delta.epoch!=Epoch||delta.baseRevision!=Revision||delta.revision<=Revision||delta.completedStep<CompletedStep||delta.eventStart!=EventCursor||delta.eventCursor<delta.eventStart)
                    throw new ArgumentException("Network baseline does not match");
                if(delta.globals==null||delta.globals.entities?.Length!=0||delta.changed==null||delta.removed==null||delta.entityOrder==null||delta.entityOrder.Length>20000||delta.events==null||delta.events.Length>4096||delta.eventCursor-delta.eventStart!=delta.events.Length)
                    throw new ArgumentException("Invalid network delta collections");
                var next=SnapshotCodec.Clone(delta.globals);var entities=new Dictionary<int,EntitySnapshot>();foreach(var e in baseline.entities)entities.Add(e.state.id,e);
                var changedIds=new HashSet<int>();foreach(var e in delta.changed){if(e?.state==null||!changedIds.Add(e.state.id))throw new ArgumentException("Duplicate changed entity");entities[e.state.id]=SnapshotCodec.Clone(e);}
                var removedIds=new HashSet<int>();foreach(int id in delta.removed){if(!removedIds.Add(id)||changedIds.Contains(id)||!entities.Remove(id))throw new ArgumentException("Invalid removed entity");}
                next.entities=new EntitySnapshot[delta.entityOrder.Length];var seen=new HashSet<int>();
                for(int i=0;i<next.entities.Length;i++)if(!seen.Add(delta.entityOrder[i])||!entities.TryGetValue(delta.entityOrder[i],out next.entities[i]))throw new ArgumentException("Invalid network entity order");
                if(seen.Count!=entities.Count)throw new ArgumentException("Missing network entity");
                if(!delta.hasLandNavigation)next.landNavigation=baseline.landNavigation;
                if(!delta.hasWaterNavigation)next.waterNavigation=baseline.waterNavigation;
                if(!delta.hasVisibility)next.visibility=baseline.visibility;
                if(!delta.hasTerrain)next.terrainObstacles=baseline.terrainObstacles;
                if(delta.hasScenarioDocument||next.hasScenarioDocument!=baseline.hasScenarioDocument||next.scenarioHash!=baseline.scenarioHash)throw new ArgumentException("Network scenario identity changed");
                next.scenarioDocument=baseline.scenarioDocument;
                LanProjection.Validate(rules,next);
                LanQueueIndex.Validate(next,delta.queues);
                if(LanProjection.Digest(next,delta.queues)!=delta.digest)throw new ArgumentException("Network delta digest mismatch");
                var events=new List<SimEvent>();foreach(var e in delta.events)
                {if(e==null||e.schemaVersion!=SimEvent.CurrentSchemaVersion||e.player< -2||e.player>=next.players.Length||(e.visibilityMask&~((1<<next.players.Length)-1))!=0||!SnapshotCodec.Finite(e.time)||!SnapshotCodec.Finite(e.position))throw new ArgumentException("Invalid network event");
                    if(e.kind=="Heal"&&!e.hasHealing)throw new ArgumentException("Missing network healing receipt");
                    if(e.hasHealing&&(e.kind!="Heal"||e.healing==null||e.healing.source!=e.entity||e.healing.target!=e.target||e.healing.source<1||e.healing.target<1||e.healing.source==e.healing.target||!SnapshotCodec.Finite(e.healing.hpBefore)||!SnapshotCodec.Finite(e.healing.hpAfter)||!SnapshotCodec.Finite(e.healing.maxHp)||!SnapshotCodec.Finite(e.healing.actual)||!SnapshotCodec.Finite(e.healing.rate)||e.healing.hpBefore<=0||e.healing.hpAfter<=e.healing.hpBefore||e.healing.maxHp<e.healing.hpAfter||e.healing.actual<=0||e.healing.rate<=0||Math.Abs((e.healing.hpAfter-e.healing.hpBefore)-e.healing.actual)>=.001f||!SnapshotCodec.Finite(e.amount)||Math.Abs(e.amount-e.healing.actual)>=.001f))throw new ArgumentException("Invalid network healing receipt");
                    events.Add(ReplayEventCopy.Copy(e));}
                // Validation completes before either the rendered world or its
                // event cursor can change. Repeated and rejected deltas play no audio.
                World.ApplyReplica(next);Queues.Replace(delta.queues);queueSnapshot=CopyQueues(delta.queues);World.Events.AddRange(events);
                if(World.Events.Count>8192)World.Events.RemoveRange(0,World.Events.Count-4096);
                baseline=next;Revision=delta.revision;CompletedStep=delta.completedStep;EventCursor=delta.eventCursor;Error="";return true;
            }
            catch(Exception error){NeedsFullState=true;Error=error.Message;return false;}
        }
    }
}
