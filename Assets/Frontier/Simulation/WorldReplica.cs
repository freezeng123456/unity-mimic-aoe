using System;
using System.Reflection;

namespace Frontier
{
    public partial class World
    {
        static readonly FieldInfo[] ReplicaEntityFields=typeof(EntityState).GetFields(BindingFlags.Public|BindingFlags.Instance);
        static readonly FieldInfo[] ReplicaPlayerFields=typeof(PlayerState).GetFields(BindingFlags.Public|BindingFlags.Instance);
        static void CopyScalars(object from,object to,FieldInfo[] fields)
        {foreach(var field in fields)if(!field.IsInitOnly&&!field.IsNotSerialized)field.SetValue(to,field.GetValue(from));}
        // Only the replica coordinator calls this after validating an entire
        // projection. No Spawn/Remove/Issue/Tick runs, so no duplicate payment,
        // XP, casualties, production or implicit event is generated.
        internal void ApplyReplica(WorldSnapshot s)
        {
            LanProjection.Validate(Rules,s);
            if(s.seed!=Seed||s.mapId!=MapId||s.maxAge!=MaxAge||s.players.Length!=Players.Length||s.hasScenarioDocument!=HasScenario||s.scenarioHash!=ScenarioHash)throw new ArgumentException("Replica world identity changed");
            for(int i=0;i<Players.Length;i++)if(s.players[i].state.civilization!=Players[i].civilization||s.teams[i]!=Players[i].team)throw new ArgumentException("Replica roster changed");
            for(int i=0;i<Players.Length;i++)
            {
                var saved=SnapshotCodec.Clone(s.players[i]);var p=Players[i];CopyScalars(saved.state,p,ReplicaPlayerFields);
                p.technologies.Clear();p.technologies.UnionWith(saved.technologies);p.sentCards.Clear();p.sentCards.UnionWith(saved.sentCards);
                p.deck.Clear();p.deck.AddRange(saved.deck);p.ageHistory.Clear();p.ageHistory.AddRange(saved.ageHistory);
                p.rewards.Clear();p.rewards.AddRange(saved.rewards);p.shipments.Clear();p.shipments.AddRange(saved.shipments);p.enemyMemory.Clear();foreach(var item in saved.enemyMemory)p.enemyMemory.Add(item.id,item.point);
            }
            var old=new System.Collections.Generic.Dictionary<int,EntityState>(byId);Entities.Clear();byId.Clear();
            foreach(var source in s.entities)
            {
                var saved=SnapshotCodec.Clone(source);
                if(!old.TryGetValue(saved.state.id,out var e))e=new EntityState();CopyScalars(saved.state,e,ReplicaEntityFields);e.def=Rules.Entity(saved.definition);
                e.orders.Clear();foreach(var order in saved.orders)e.orders.Enqueue(order);e.path.Clear();e.path.AddRange(saved.path);e.queue.Clear();e.queue.AddRange(saved.queue);e.occupants.Clear();e.occupants.AddRange(saved.occupants);
                Entities.Add(e);byId.Add(e.id,e);
            }
            Navigation.RestoreState(s.landNavigation);WaterNavigation.RestoreState(s.waterNavigation);Fog.RestoreState(s.visibility);
            Time=s.time;Paused=s.paused;Winner=s.winner;nextId=s.nextId;fogAt=s.fogAt;nextEvacuation=s.nextEvacuation;
            Projectiles.Clear();foreach(var projectile in s.projectiles)Projectiles.Add(SnapshotCodec.Clone(projectile));TerrainObstacles.Clear();TerrainObstacles.AddRange(s.terrainObstacles);
            TradeSockets.Clear();foreach(var item in s.sockets)TradeSockets.Add(new TradeSocketState{entity=item.entity,waypoint=item.waypoint});
            Treasures.Clear();foreach(var item in s.treasures){var t=new TreasureState{entity=item.entity,definition=Rules.landmarks.treasures[item.definition]};t.guards.AddRange(item.guards);Treasures.Add(t);}
            CaravanPosition=s.caravanPosition;caravanWaypoint=s.caravanWaypoint;caravanDirection=s.caravanDirection;
            for(int i=0;i<Players.Length;i++){discoveredLandmarks[i].Clear();discoveredLandmarks[i].UnionWith(s.discoveries[i].entities);}
            pendingEvacuations.Clear();foreach(int id in s.pendingEvacuations)pendingEvacuations.Add(Get(id));
            pendingFineRecoveries.Clear();pendingFineRecoveryQueue.Clear();
            foreach(int id in s.fineRecoveryQueue)
            {
                var e=Get(id);var request=new FineRecoveryRequest{unit=e,order=e.orders.Count>0?e.orders.Peek():null};
                pendingFineRecoveries.Add(id,request);pendingFineRecoveryQueue.Enqueue(request);
            }
            gatherLog.Clear();gatherLogTarget.Clear();incomeKinds.Clear();foreach(var item in s.incomes){gatherLog[item.source]=item.amount;gatherLogTarget[item.source]=item.target;incomeKinds[item.source]=item.kind;}
            RestoreNeutralAllies(s.neutralAllies);RestoreMapObjective(s.mapObjective);RebuildSpatial();
            if(HasScenario)RestoreScenarioState(s.scenarioState);
        }
    }
}
