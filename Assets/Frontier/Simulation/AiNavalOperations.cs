using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    [Serializable] public sealed class AiNavalOperationsSnapshot
    {
        public int ship,phase,landingIndex,landingAttempts,landingPlayer;
        public float startedAt,retryAt;
        public int[] passengers=Array.Empty<int>();
        public bool hasBridgeCandidate,hasBridgeMission;
        public AiBridgeCandidate084 bridgeCandidate;public AiBridgeMission084 bridgeMission;
        public AiBridgeReview084 bridgeReview;public float bridgeRetryAt;
    }
    // This plan knows authored terrain, not hidden enemy entities. Purchases,
    // boarding and landings use precisely the public human commands.
    public sealed class AiNavalOperations
    {
        private readonly World world;private readonly int player;
        private readonly AiResourceContinuity072 continuity;
        private int shipId,phase,landingIndex,landingAttempts,landingPlayer;
        private float startedAt,retryAt;
        private readonly List<int> passengers=new List<int>();
        private AiBridgeTransport084 bridge;
        public void BindBridgeArmy(AiArmyPlan072 army,AiRevolution071 revolution,AiMedicalSupport076 medical)
        {bridge=new AiBridgeTransport084(world,player,army,revolution,medical);army.BindTransport(OwnsBridgeMember);}
        public bool OwnsBridgeMember(int id)=>bridge!=null&&bridge.Owns(id);
        public void ValidateBridgeState()=>bridge?.ValidateOwnership();
        public AiNavalOperations(World world,int player,AiResourceContinuity072 continuity=null)
        {this.world=world;this.player=player;this.continuity=continuity;landingPlayer=world.FirstEnemyPlayer(player);}
        public AiNavalOperationsSnapshot CaptureState()=>new AiNavalOperationsSnapshot
        {ship=shipId,phase=phase,landingIndex=landingIndex,landingAttempts=landingAttempts,landingPlayer=landingPlayer,startedAt=startedAt,retryAt=retryAt,passengers=passengers.ToArray(),hasBridgeCandidate=bridge?.Candidate!=null,hasBridgeMission=bridge?.Mission!=null,bridgeCandidate=bridge?.Candidate==null?null:SnapshotCodec.Clone(bridge.Candidate),bridgeMission=bridge?.Mission==null?null:SnapshotCodec.Clone(bridge.Mission),bridgeReview=bridge?.LastReview??AiBridgeReview084.None,bridgeRetryAt=bridge?.RetryAt??0};
        // Unity inline serialization replaces null DTOs with default instances.
        // Markers distinguish absence; only truly empty unmarked payloads may
        // be ignored, so a corrupt active plan cannot silently become idle.
        static bool Empty(Array values)=>values==null||values.Length==0;
        static bool Zero(Vector2 point)=>point.x==0&&point.y==0;
        static bool EmptyBridgeCandidate(AiBridgeCandidate084 value)=>value==null||
            (value.schema==0||value.schema==1)&&value.ship==0&&value.navigationVersion==0&&value.waterVersion==0&&value.pair==0&&value.cursor==0&&
            Zero(value.destination)&&Zero(value.shipOrigin)&&Zero(value.boardingShore)&&Zero(value.boardingWater)&&Zero(value.landingShore)&&Zero(value.landingWater)&&
            Empty(value.members)&&Empty(value.origins)&&Empty(value.landingSlots)&&Empty(value.directIntents)&&Empty(value.continuationIntents)&&
            Empty(value.direct)&&Empty(value.boarding)&&Empty(value.continuation)&&value.shipApproach==0&&value.sea==0;
        static bool EmptyBridgeMission(AiBridgeMission084 value)=>value==null||
            (value.schema==0||value.schema==1)&&value.ship==0&&(value.phase==0||value.phase==1)&&value.attempts==0&&value.originalCount==0&&
            Zero(value.destination)&&Zero(value.boardingShore)&&Zero(value.boardingWater)&&Zero(value.landingShore)&&Zero(value.landingWater)&&
            Empty(value.members)&&Empty(value.landed)&&value.startedAt==0&&value.retryAt==0&&value.directSeconds==0&&value.transportSeconds==0&&!value.invalidLanding;
        public void RestoreState(AiNavalOperationsSnapshot state)
        {
            AiSaveValidation.Require(state!=null&&state.ship>=0&&state.phase>=0&&state.phase<=2&&state.landingIndex>=0&&state.landingAttempts>=0&&state.landingAttempts<=6&&
                state.landingPlayer>=0&&state.landingPlayer<world.Players.Length&&AiSaveValidation.Clock(state.startedAt)&&AiSaveValidation.Clock(state.retryAt)&&state.passengers!=null,"naval operations state");
            AiSaveValidation.Require(state.landingPlayer==player||world.AreEnemies(player,state.landingPlayer),"naval landing faction");
            var seen=new HashSet<int>();foreach(int id in state.passengers)AiSaveValidation.Require(id>0&&seen.Add(id),"naval passengers");
            AiSaveValidation.Require((state.hasBridgeCandidate?state.bridgeCandidate!=null:EmptyBridgeCandidate(state.bridgeCandidate))&&
                (state.hasBridgeMission?state.bridgeMission!=null:EmptyBridgeMission(state.bridgeMission)),"bridge plan presence");
            AiSaveValidation.Require(bridge!=null||!state.hasBridgeCandidate&&!state.hasBridgeMission&&state.bridgeReview==AiBridgeReview084.None&&state.bridgeRetryAt==0,"unbound bridge plan");
            shipId=state.ship;phase=state.phase;landingIndex=state.landingIndex;landingAttempts=state.landingAttempts;landingPlayer=state.landingPlayer;
            startedAt=state.startedAt;retryAt=state.retryAt;passengers.Clear();passengers.AddRange(state.passengers);
            bridge?.Restore(state.hasBridgeCandidate?state.bridgeCandidate:null,state.hasBridgeMission?state.bridgeMission:null,state.bridgeReview,state.bridgeRetryAt);
        }
        public bool NeedsTransport=>world.Layout.RequiresTransport&&world.Players[player].age>=2&&world.Rules.Entity("transport_ship")!=null;
        public bool Tick(List<EntityState> own,List<EntityState> visibleEnemies,bool underAttack)
        {
            if(world.Paused||world.IsMatchEnded||!world.IsPlayerActive(player))return false;
            if(!world.Layout.RequiresTransport)return world.Layout.HasWater&&world.Players[player].age>=2&&bridge!=null&&bridge.Tick(own,visibleEnemies,underAttack);
            if(!NeedsTransport)return false;
            var ship=world.Get(shipId);
            if(world.NavalTransportReason(player,ship)!="")
            {
                ship=own.Find(e=>e.Alive&&e.def.transportShip&&e.garrisonedIn==0);
                shipId=ship?.id??0;phase=0;passengers.Clear();landingAttempts=0;
            }
            if(ship==null||world.Time<retryAt)return false;
            if(ship.orders.Count>0&&ship.orders.Peek().kind==OrderKind.DockRepair)return true;
            if(ship.occupants.Count>0)
            {
                if(ship.orders.Count>0&&ship.orders.Peek().kind==OrderKind.Disembark)
                {
                    var order=ship.orders.Peek();
                    if(order.destinationCursor>=6&&(ship.status.StartsWith("Landing occupied",StringComparison.Ordinal)||ship.status.StartsWith("Shore blocked",StringComparison.Ordinal)))
                    {landingIndex++;landingAttempts++;Send(new GameCommand{kind=CommandKind.Order,actors=new[]{ship.id},order=OrderKind.Stop});retryAt=world.Time+3;}
                    return true;
                }
                bool pending=own.Exists(e=>e.Alive&&e.garrisonedIn==0&&e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Embark&&e.orders.Peek().target==ship.id);
                if(pending&&world.Time-startedAt<45&&!underAttack)return true;
                // A bounded muster timeout releases late arrivals rather than
                // reserving soldiers or a full ship forever.
                foreach(var unit in own)if(unit.Alive&&unit.garrisonedIn==0&&unit.orders.Count>0&&unit.orders.Peek().kind==OrderKind.Embark&&unit.orders.Peek().target==ship.id)
                    Send(new GameCommand{kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Stop});
                landingPlayer=underAttack||ship.hp<world.MaxHp(ship)*.45f?player:world.FirstEnemyPlayer(player);
                var shores=world.Layout.LandingSites[landingPlayer];
                if(shores.Length==0)return true;
                if(landingAttempts>=6){landingAttempts=0;retryAt=world.Time+30;return true;}
                for(int probe=0;probe<Math.Min(3,shores.Length);probe++)
                {
                    if(landingAttempts>=6)break;
                    Vector2 shore=shores[landingIndex%shores.Length];
                    if(Threatened(shore,visibleEnemies)){landingIndex++;landingAttempts++;continue;}
                    var result=Send(new GameCommand{kind=CommandKind.Disembark,actors=new[]{ship.id},point=shore});
                    if(!result.accepted){landingIndex++;landingAttempts++;continue;}
                    passengers.Clear();passengers.AddRange(ship.occupants);phase=2;return true;
                }
                retryAt=world.Time+5;return true;
            }
            if(phase==2)
            {
                Vector2 destination=world.OpeningTownPosition(landingPlayer);
                foreach(int id in passengers)
                {
                    var soldier=world.Get(id);if(soldier==null||!soldier.Alive||soldier.garrisonedIn!=0||soldier.owner!=player)continue;
                    // AttackMove to a public starting region discovers real
                    // targets through normal fog; it does not select hidden IDs.
                    Send(new GameCommand{kind=CommandKind.Order,actors=new[]{id},order=OrderKind.AttackMove,point=destination});
                }
                world.Emit("NavalLandingCompleted",player,ship.id,"Transport expedition landed",passengers.Count,0,ship.position);
                phase=0;passengers.Clear();landingAttempts=0;retryAt=world.Time+8;return true;
            }
            if(ship.hp<world.MaxHp(ship)*.45f&&AiResourceContinuity072.MayRepair(world,player,ship))
            {
                var dock=own.Find(e=>e.Alive&&e.Complete&&e.def.id=="dock"&&world.Fog.At(player,e.position)==VisibilityState.Visible&&!Threatened(e.position,visibleEnemies));
                if(dock!=null&&Send(new GameCommand{kind=CommandKind.DockRepair,actors=new[]{ship.id},target=dock.id}).accepted)return true;
            }
            if(underAttack)
            {
                foreach(var unit in own)if(unit.Alive&&unit.garrisonedIn==0&&unit.orders.Count>0&&unit.orders.Peek().kind==OrderKind.Embark&&unit.orders.Peek().target==ship.id)
                    Send(new GameCommand{kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Stop});
                if(ship.orders.Count>0&&ship.orders.Peek().kind==OrderKind.TransportBerth)Send(new GameCommand{kind=CommandKind.Order,actors=new[]{ship.id},order=OrderKind.Stop});
                phase=0;retryAt=world.Time+5;return true;
            }
            if(ship.orders.Count>0&&ship.orders.Peek().kind==OrderKind.TransportBerth)
            {
                if(world.Time-startedAt<60)return true;
                foreach(var unit in own)if(unit.Alive&&unit.garrisonedIn==0&&unit.orders.Count>0&&unit.orders.Peek().kind==OrderKind.Embark&&unit.orders.Peek().target==ship.id)
                    Send(new GameCommand{kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Stop});
                Send(new GameCommand{kind=CommandKind.Order,actors=new[]{ship.id},order=OrderKind.Stop});phase=0;retryAt=world.Time+8;return true;
            }
            Vector2 home=world.OpeningTownPosition(player);var chosen=new List<int>();int slots=0;
            foreach(var soldier in own)
            {
                if(!soldier.Alive||!soldier.Complete||soldier.garrisonedIn!=0||!soldier.Military||soldier.def.waterborne||(soldier.def.tags&Tags.Explorer)!=0)continue;
                if(!OnHomeLand(soldier,home))continue;
                int cost=world.TransportSlotCost(soldier);if(slots+cost>ship.def.transportCapacity)continue;
                chosen.Add(soldier.id);slots+=cost;if(slots>=ship.def.transportCapacity)break;
            }
            if(chosen.Count<4)return true;
            var boarding=world.Layout.LandingSites[player];
            for(int i=0;i<Math.Min(3,boarding.Length);i++)
            {
                var shore=boarding[(landingIndex+i)%boarding.Length];if(Threatened(shore,visibleEnemies))continue;
                if(!Send(new GameCommand{kind=CommandKind.Embark,actors=chosen.ToArray(),target=ship.id,point=shore}).accepted)continue;
                phase=1;startedAt=world.Time;landingAttempts=0;passengers.Clear();passengers.AddRange(chosen);return true;
            }
            retryAt=world.Time+5;return true;
        }
        private bool OnHomeLand(EntityState unit,Vector2 home)
        {
            Vector2 anchor=world.Navigation.NearestWalkable(home,unit.def.radius);
            if(!world.Navigation.IsWalkable(unit.position,unit.def.radius)||!world.Navigation.IsWalkable(anchor,unit.def.radius))return false;
            if(world.Navigation.ClearLine(unit.position,anchor,unit.def.radius))return true;
            var path=world.Navigation.FindPath(unit.position,anchor,unit.def.radius);return path.Count>0&&(path[path.Count-1]-anchor).sqrMagnitude<.01f;
        }
        private bool Threatened(Vector2 point,List<EntityState> enemies)
        {
            foreach(var enemy in enemies)if(world.IsVisible(player,enemy)&&enemy.Military&&enemy.def.damage>0&&
                (enemy.position-point).sqrMagnitude<Mathf.Pow(Mathf.Max(10,enemy.def.range+6),2))return true;
            return false;
        }
        private CommandResult Send(GameCommand command)
        {command.player=player;var result=world.Issue(command);if(result.accepted)continuity?.InvalidateSourceSurvey();return result;}
    }
}
