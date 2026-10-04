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
    }
    // This plan knows authored terrain, not hidden enemy entities. Purchases,
    // boarding and landings use precisely the public human commands.
    public sealed class AiNavalOperations
    {
        private readonly World world;private readonly int player;
        private int shipId,phase,landingIndex,landingAttempts,landingPlayer;
        private float startedAt,retryAt;
        private readonly List<int> passengers=new List<int>();
        public AiNavalOperations(World world,int player){this.world=world;this.player=player;landingPlayer=1-player;}
        public AiNavalOperationsSnapshot CaptureState()=>new AiNavalOperationsSnapshot
        {ship=shipId,phase=phase,landingIndex=landingIndex,landingAttempts=landingAttempts,landingPlayer=landingPlayer,startedAt=startedAt,retryAt=retryAt,passengers=passengers.ToArray()};
        public void RestoreState(AiNavalOperationsSnapshot state)
        {
            AiSaveValidation.Require(state!=null&&state.ship>=0&&state.phase>=0&&state.phase<=2&&state.landingIndex>=0&&state.landingAttempts>=0&&state.landingAttempts<=6&&
                state.landingPlayer>=0&&state.landingPlayer<=1&&AiSaveValidation.Clock(state.startedAt)&&AiSaveValidation.Clock(state.retryAt)&&state.passengers!=null,"naval operations state");
            var seen=new HashSet<int>();foreach(int id in state.passengers)AiSaveValidation.Require(id>0&&seen.Add(id),"naval passengers");
            shipId=state.ship;phase=state.phase;landingIndex=state.landingIndex;landingAttempts=state.landingAttempts;landingPlayer=state.landingPlayer;
            startedAt=state.startedAt;retryAt=state.retryAt;passengers.Clear();passengers.AddRange(state.passengers);
        }
        public bool NeedsTransport=>world.Layout.RequiresTransport&&world.Players[player].age>=2&&world.Rules.Entity("transport_ship")!=null;
        public bool Tick(List<EntityState> own,List<EntityState> visibleEnemies,bool underAttack)
        {
            if(!NeedsTransport||world.Paused||world.Winner>=0)return false;
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
                landingPlayer=underAttack||ship.hp<world.MaxHp(ship)*.45f?player:1-player;
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
            if(ship.hp<world.MaxHp(ship)*.45f)
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
        private CommandResult Send(GameCommand command){command.player=player;return world.Issue(command);}
    }
}
