using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        private const int NavalShoreAttempts=6;
        public int TransportSlotCost(EntityState passenger)=>Math.Max(1,passenger.def.population);
        public int TransportLoad(EntityState ship)
        {
            int slots=0;if(ship==null)return slots;
            foreach(int id in ship.occupants)
            {var passenger=Get(id);if(passenger!=null&&passenger.Alive&&passenger.garrisonedIn==ship.id)slots+=TransportSlotCost(passenger);}
            return slots;
        }
        private int TransportReservations(EntityState ship,HashSet<int> excluded=null)
        {
            int slots=0;
            foreach(var passenger in Entities)
            {
                if(!passenger.Alive||passenger.garrisonedIn!=0||excluded!=null&&excluded.Contains(passenger.id))continue;
                foreach(var order in passenger.orders)if(order.kind==OrderKind.Embark&&order.target==ship.id)
                {slots+=TransportSlotCost(passenger);break;}
            }
            return slots;
        }
        public string NavalTransportReason(int player,EntityState ship)
        {
            if(ship==null||!ship.Alive||ship.owner!=player||ship.def.kind!=EntityKind.Unit||!ship.def.waterborne||!ship.def.transportShip||ship.def.transportCapacity<=0)
                return "Choose your transport ship";
            return ship.garrisonedIn!=0?"A transport cannot itself be cargo":"";
        }
        private bool NavalPathExists(GridNavigation navigation,Vector2 start,Vector2 end,float radius)
        {
            if(!navigation.IsWalkable(start,radius)||!navigation.IsWalkable(end,radius))return false;
            if(navigation.ClearLine(start,end,radius))return true;
            var path=navigation.FindPath(start,end,radius);
            return path.Count>0&&(path[path.Count-1]-end).sqrMagnitude<.01f;
        }
        private struct ShoreChoice {public Vector2 land,water;public float score;}
        // Terrain is public. This does not reveal occupants, resources or enemy orders.
        // Exact endpoints must be navigable: FindPath's nearest-cell projection alone
        // must never connect a passenger to a different island.
        public bool TryTransportShore(EntityState ship,Vector2 requested,float landRadius,out Vector2 land,out Vector2 water)
        {
            land=water=Vector2.zero;
            if(ship==null||!ship.Alive||!ship.def.waterborne||!SnapshotCodec.Finite(requested)||landRadius<=0||!SnapshotCodec.Finite(landRadius))return false;
            var choices=new List<ShoreChoice>();var ignoredShip=new HashSet<int>{ship.id};
            for(int y=-12;y<=12;y++)for(int x=-12;x<=12;x++)
            {
                if(x*x+y*y>144)continue;var candidate=requested+new Vector2(x,y);
                if(IsWater(candidate)||IsBridge(candidate)||IsBridgeApproach(candidate)||!Navigation.IsWalkable(candidate,landRadius))continue;
                Vector2 direction=Layout.WaterDirection(candidate);if(direction.sqrMagnitude<.5f)continue;
                float maximum=ship.def.radius+landRadius+3.5f;
                for(float distance=1;distance<=maximum;distance+=.5f)
                {
                    Vector2 berth=candidate+direction*distance;
                    if(!IsWater(berth)||IsBridge(berth)||IsBridgeApproach(berth)||!WaterNavigation.IsWalkable(berth,ship.def.radius)||PositionOccupied(berth,ship.def.radius,ignoredShip,false,true))continue;
                    choices.Add(new ShoreChoice{land=candidate,water=berth,score=(candidate-requested).sqrMagnitude+distance*.15f});break;
                }
            }
            choices.Sort((a,b)=>{int order=a.score.CompareTo(b.score);if(order!=0)return order;order=a.land.x.CompareTo(b.land.x);return order!=0?order:a.land.y.CompareTo(b.land.y);});
            foreach(var choice in choices)
            {
                // Avoid endpoint searches through disconnected seas. This runs on a
                // command/retry, never for every passenger on every simulation tick.
                if(!NavalPathExists(WaterNavigation,ship.position,choice.water,ship.def.radius))continue;
                if(Navigation.IsWalkable(requested,landRadius)&&!NavalPathExists(Navigation,requested,choice.land,landRadius))continue;
                land=choice.land;water=choice.water;return true;
            }
            return false;
        }
        private void SetNavalOrder(EntityState unit,UnitOrder order,bool append=false)
        {
            FlushGather(unit.id);CancelCaravan(unit,"Naval order");
            if(!append){unit.orders.Clear();InvalidateCommandPath(unit);}
            unit.fireAt=-1;unit.attackTarget=0;unit.hold=false;unit.work=default;
            if(unit.shielded){unit.shielded=false;unit.shieldReadyAt=Time+2;}
            unit.orders.Enqueue(order);unit.status="Moving";
        }
        public bool TryHandleNavalTransportCommand(GameCommand command,out CommandResult result)
        {
            result=default;
            bool embark=command.kind==CommandKind.Embark||command.kind==CommandKind.Order&&command.order==OrderKind.Embark;
            bool disembark=command.kind==CommandKind.Disembark||command.kind==CommandKind.Order&&command.order==OrderKind.Disembark;
            bool repair=command.kind==CommandKind.DockRepair||command.kind==CommandKind.Order&&command.order==OrderKind.DockRepair;
            if(!embark&&!disembark&&!repair)return false;
            if(command.append){result=CommandResult.Fail("Naval operations replace current orders");return true;}
            if(embark){result=OrderEmbark(command);return true;}
            if(repair){result=OrderDockRepair(command);return true;}
            if(!SnapshotCodec.Finite(command.point)){result=CommandResult.Fail("Choose a shoreline");return true;}
            var selected=new List<EntityState>();
            if(command.actors!=null)foreach(int id in command.actors)
            {
                var ship=Get(id);string reason=NavalTransportReason(command.player,ship);
                if(reason!=""){result=CommandResult.Fail(reason);return true;}
                if(!selected.Contains(ship))selected.Add(ship);
            }
            if(selected.Count==0){result=CommandResult.Fail("Select a transport ship");return true;}
            var orders=new List<UnitOrder>();
            foreach(var ship in selected)
            {
                if(ship.occupants.Count==0){result=CommandResult.Fail("The transport is empty");return true;}
                float radius=LargestCargoRadius(ship);
                if(!TryTransportShore(ship,command.point,radius,out var shore,out var berth))
                {result=CommandResult.Fail("No reachable landing shore");return true;}
                orders.Add(new UnitOrder(OrderKind.Disembark,shore){resolvedPoint=berth,hasResolvedPoint=true,destinationVersion=Navigation.Version});
            }
            for(int i=0;i<selected.Count;i++)
            {SetNavalOrder(selected[i],orders[i]);Emit("DisembarkOrdered",command.player,selected[i].id,"Sailing to a landing shore",0,0,orders[i].point);}
            result=CommandResult.Ok("Transports sailing to shore");return true;
        }
        private CommandResult OrderEmbark(GameCommand command)
        {
            var ship=Get(command.target);string reason=NavalTransportReason(command.player,ship);if(reason!="")return CommandResult.Fail(reason);
            if(ship.orders.Count>0&&ship.orders.Peek().kind==OrderKind.Disembark)return CommandResult.Fail("Finish or stop the landing operation first");
            var passengers=new List<EntityState>();var selected=new HashSet<int>();int slots=0;float radius=.4f;
            if(command.actors!=null)foreach(int id in command.actors)
            {
                var passenger=Get(id);
                if(passenger==null||!passenger.Alive||passenger.owner!=command.player||passenger.def.kind!=EntityKind.Unit||passenger.def.waterborne||passenger.garrisonedIn!=0)
                    return CommandResult.Fail("Only your unembarked land units can board");
                if(!selected.Add(id))continue;passengers.Add(passenger);slots+=TransportSlotCost(passenger);radius=Mathf.Max(radius,passenger.def.radius);
            }
            if(passengers.Count==0)return CommandResult.Fail("Select land units to board");
            if(TransportLoad(ship)+TransportReservations(ship,selected)+slots>ship.def.transportCapacity)return CommandResult.Fail("Transport population capacity reached");
            Vector2 shore,berth;UnitOrder current=ship.orders.Count>0?ship.orders.Peek():null;
            if(current!=null&&current.kind==OrderKind.TransportBerth)
            {shore=current.point;berth=current.resolvedPoint;}
            else
            {
                Vector2 requested=command.point;
                if(requested==Vector2.zero)
                {
                    if(!TryAutomaticBoardingShore(ship,passengers,radius,out shore,out berth))return CommandResult.Fail("No common reachable boarding shore");
                }
                else if(!TryTransportShore(ship,requested,radius,out shore,out berth))return CommandResult.Fail("No reachable boarding shore");
            }
            foreach(var passenger in passengers)
                if(!NavalPathExists(Navigation,passenger.position,shore,passenger.def.radius))return CommandResult.Fail("A selected unit cannot reach this shore");
            if(current==null||current.kind!=OrderKind.TransportBerth)
                SetNavalOrder(ship,new UnitOrder(OrderKind.TransportBerth,shore){resolvedPoint=berth,hasResolvedPoint=true,destinationVersion=Navigation.Version});
            foreach(var passenger in passengers)SetNavalOrder(passenger,new UnitOrder(OrderKind.Embark,shore,ship.id));
            Emit("EmbarkOrdered",command.player,ship.id,"Land units walking to the boarding shore",slots,0,shore);
            return CommandResult.Ok("Units walking to transport shore");
        }
        private bool TryAutomaticBoardingShore(EntityState ship,List<EntityState> passengers,float radius,out Vector2 shore,out Vector2 berth)
        {
            shore=berth=Vector2.zero;var candidates=new List<Vector2>{passengers[0].position};
            for(int player=0;player<Layout.LandingSites.Length;player++)
            {
                candidates.AddRange(Layout.LandingSites[player]);
                foreach(var dock in Layout.DockSites[player])
                {
                    candidates.Add(dock);Vector2 direction=Layout.WaterDirection(dock),side=new Vector2(-direction.y,direction.x);
                    candidates.Add(dock+side*10);candidates.Add(dock-side*10);
                }
            }
            Vector2 first=passengers[0].position;
            candidates.Sort((a,b)=>{int result=(a-first).sqrMagnitude.CompareTo((b-first).sqrMagnitude);if(result!=0)return result;result=a.x.CompareTo(b.x);return result!=0?result:a.y.CompareTo(b.y);});
            foreach(var candidate in candidates)
            {
                if(!TryTransportShore(ship,candidate,radius,out var land,out var water))continue;
                bool reachable=true;foreach(var passenger in passengers)if(!NavalPathExists(Navigation,passenger.position,land,passenger.def.radius)){reachable=false;break;}
                if(!reachable)continue;shore=land;berth=water;return true;
            }
            return false;
        }
        private float LargestCargoRadius(EntityState ship)
        {
            float radius=.4f;foreach(int id in ship.occupants){var passenger=Get(id);if(passenger!=null&&passenger.Alive)radius=Mathf.Max(radius,passenger.def.radius);}
            foreach(var passenger in Entities)if(passenger.Alive&&passenger.garrisonedIn==0)
                foreach(var order in passenger.orders)if(order.kind==OrderKind.Embark&&order.target==ship.id){radius=Mathf.Max(radius,passenger.def.radius);break;}
            return radius;
        }
        public bool TickNavalTransportOrder(EntityState actor,UnitOrder order,float dt)
        {
            if(order==null)return false;
            if(order.kind==OrderKind.DockRepair){TickDockRepair(actor,order,dt);return true;}
            if(order.kind==OrderKind.Embark){TickEmbark(actor,order,dt);return true;}
            if(order.kind!=OrderKind.TransportBerth&&order.kind!=OrderKind.Disembark)return false;
            if(NavalTransportReason(actor.owner,actor)!=""){actor.orders.Dequeue();actor.status="Transport lost";return true;}
            if(order.kind==OrderKind.TransportBerth&&TransportReservations(actor)==0)
            {actor.orders.Dequeue();actor.path.Clear();actor.status="Idle";return true;}
            if(order.hasResolvedPoint&&(actor.position-order.resolvedPoint).sqrMagnitude>.25f*.25f&&Time-actor.recoveryProgressAt>3&&Time>=order.destinationRetryAt&&!DestinationAvailable(actor,order.resolvedPoint))
                order.hasResolvedPoint=false;
            if(!order.hasResolvedPoint||!WaterNavigation.IsWalkable(order.resolvedPoint,actor.def.radius))
            {
                if(Time<order.destinationRetryAt)return true;
                order.destinationRetryAt=Time+1;
                if(order.destinationCursor>=NavalShoreAttempts)
                {order.destinationRetryAt=float.MaxValue;actor.status="Shore blocked; choose another landing";return true;}
                order.destinationCursor++;
                if(!TryTransportShore(actor,order.point,LargestCargoRadius(actor),out var shore,out var berth))
                {actor.status="Shore blocked; waiting";return true;}
                order.point=shore;order.resolvedPoint=berth;order.hasResolvedPoint=true;InvalidateCommandPath(actor);
                UpdateBoardingShore(actor,shore);
            }
            MoveTowardsTarget(actor,order.resolvedPoint,dt);
            if((actor.position-order.resolvedPoint).sqrMagnitude>.25f*.25f){actor.status="Moving";return true;}
            actor.path.Clear();actor.status=order.kind==OrderKind.TransportBerth?"Waiting for passengers":"Unloading";
            if(order.kind==OrderKind.Disembark&&Time>=order.destinationRetryAt)
            {
                if(TryUnloadTransport(actor,order.point)){actor.orders.Dequeue();actor.status="Idle";return true;}
                if(order.destinationCursor<NavalShoreAttempts){order.destinationCursor++;order.destinationRetryAt=Time+1;}
                else order.destinationRetryAt=float.MaxValue;
                actor.status="Landing occupied; choose another shore";
            }
            return true;
        }
        private void UpdateBoardingShore(EntityState ship,Vector2 shore)
        {
            foreach(var passenger in Entities)if(passenger.Alive&&passenger.garrisonedIn==0)
                foreach(var order in passenger.orders)if(order.kind==OrderKind.Embark&&order.target==ship.id)
                {order.point=shore;InvalidateCommandPath(passenger);}
        }
        private void TickEmbark(EntityState passenger,UnitOrder order,float dt)
        {
            var ship=Get(order.target);
            if(passenger.def.waterborne||NavalTransportReason(passenger.owner,ship)!=""||ship.orders.Count==0||ship.orders.Peek().kind!=OrderKind.TransportBerth)
            {passenger.orders.Dequeue();passenger.path.Clear();passenger.status="Boarding cancelled";return;}
            var berth=ship.orders.Peek();order.point=berth.point;
            MoveTowardsTarget(passenger,order.point,dt);
            if((passenger.position-order.point).sqrMagnitude>.35f*.35f){passenger.status="Moving";return;}
            passenger.path.Clear();passenger.status="Waiting to board";
            if(!berth.hasResolvedPoint||(ship.position-berth.resolvedPoint).sqrMagnitude>.25f*.25f||
                Vector2.Distance(passenger.position,ship.position)>ship.def.radius+passenger.def.radius+3.85f)return;
            if(TransportLoad(ship)+TransportSlotCost(passenger)>ship.def.transportCapacity)return;
            FlushGather(passenger.id);passenger.garrisonedIn=ship.id;ship.occupants.Add(passenger.id);
            passenger.orders.Clear();InvalidateCommandPath(passenger);passenger.fireAt=-1;passenger.attackTarget=0;passenger.work=default;
            passenger.position=passenger.previousPosition=ship.position;passenger.status="Embarked";
            Emit("Embarked",passenger.owner,passenger.id,"Aboard transport",TransportSlotCost(passenger),ship.id,berth.point);
        }
        private bool TryUnloadTransport(EntityState ship,Vector2 shore)
        {
            var group=new List<EntityState>();
            foreach(int id in ship.occupants){var passenger=Get(id);if(passenger!=null&&passenger.Alive&&passenger.garrisonedIn==ship.id)group.Add(passenger);}
            if(group.Count==0){ship.occupants.Clear();return true;}
            group.Sort((a,b)=>a.def.radius==b.def.radius?a.id.CompareTo(b.id):b.def.radius.CompareTo(a.def.radius));
            var points=new List<Vector2>();
            foreach(var passenger in group)
            {
                Vector2 found=Vector2.zero;bool available=false;
                foreach(var candidate in Navigation.FormationSlots(shore,256,passenger.def.radius*2+.35f))
                {
                    if((candidate-shore).sqrMagnitude>36||Vector2.Distance(candidate,ship.position)>ship.def.radius+passenger.def.radius+6||
                        IsWater(candidate)||IsBridge(candidate)||IsBridgeApproach(candidate)||!Navigation.IsWalkable(candidate,passenger.def.radius)||
                        Fog.At(ship.owner,candidate)!=VisibilityState.Visible||PositionOccupied(candidate,passenger.def.radius,null,true))continue;
                    if(!Navigation.ClearLine(shore,candidate,passenger.def.radius))continue;
                    bool overlap=false;
                    for(int i=0;i<points.Count;i++)if((candidate-points[i]).sqrMagnitude<Mathf.Pow(passenger.def.radius+group[i].def.radius+.15f,2)){overlap=true;break;}
                    if(overlap)continue;found=candidate;available=true;break;
                }
                if(!available)return false;points.Add(found);
            }
            for(int i=0;i<group.Count;i++)
            {
                var passenger=group[i];passenger.garrisonedIn=0;passenger.position=passenger.previousPosition=points[i];
                passenger.orders.Clear();InvalidateCommandPath(passenger);passenger.fireAt=-1;passenger.attackTarget=0;passenger.work=default;passenger.status="Idle";
                ship.occupants.Remove(passenger.id);Emit("Disembarked",passenger.owner,passenger.id,"Landed on navigable shore",TransportSlotCost(passenger),ship.id,points[i]);
            }
            return true;
        }
        public void TickTransportCargo()
        {
            foreach(var ship in Entities)if(ship.Alive&&ship.def.transportShip)
                foreach(int id in ship.occupants)
                {var passenger=Get(id);if(passenger!=null&&passenger.Alive&&passenger.garrisonedIn==ship.id)passenger.position=passenger.previousPosition=ship.position;}
        }
        public bool HandleTransportRemoval(EntityState removed,int killerOwner)
        {
            if(removed.garrisonedIn!=0)
            {var host=Get(removed.garrisonedIn);if(host!=null&&host.def.transportShip)host.occupants.Remove(removed.id);removed.garrisonedIn=0;}
            if(!removed.def.transportShip)return false;
            var cargo=removed.occupants.ToArray();removed.occupants.Clear();
            foreach(int id in cargo)
            {var passenger=Get(id);if(passenger==null||!passenger.Alive||passenger.garrisonedIn!=removed.id)continue;passenger.garrisonedIn=0;Remove(passenger,killerOwner);}
            return true;
        }
        public string NavalRepairReason(int player,EntityState ship,EntityState dock)
        {
            if(ship==null||!ship.Alive||ship.owner!=player||ship.def.kind!=EntityKind.Unit||!ship.def.waterborne||ship.garrisonedIn!=0)return "Select your ship";
            return dock==null||!dock.Alive||dock.owner!=player||dock.def.id!="dock"||!dock.Complete?"Choose your completed dock":"";
        }
        public float NavalRepairRate(EntityState ship)
        {
            float factor=1;foreach(var id in Players[ship.owner].technologies)
            {var technology=Rules.Tech(id);if(technology!=null&&Applies(technology,ship))factor+=technology.navalRepair;}
            return 18*Mathf.Max(0,factor);
        }
        private CommandResult OrderDockRepair(GameCommand command)
        {
            var dock=Get(command.target);var ships=new List<EntityState>();var orders=new List<UnitOrder>();
            if(command.actors!=null)foreach(int id in command.actors)
            {
                var ship=Get(id);string reason=NavalRepairReason(command.player,ship,dock);if(reason!="")return CommandResult.Fail(reason);
                if(ships.Contains(ship))continue;
                if(!TryDockRepairBerth(ship,dock,ships,orders,out var berth))return CommandResult.Fail("Dock repair berth blocked");
                ships.Add(ship);orders.Add(new UnitOrder(OrderKind.DockRepair,berth,dock.id){resolvedPoint=berth,hasResolvedPoint=true});
            }
            if(ships.Count==0)return CommandResult.Fail("Select ships to repair");
            for(int i=0;i<ships.Count;i++)SetNavalOrder(ships[i],orders[i]);return CommandResult.Ok("Ships sailing to dock repairs");
        }
        private bool TryDockRepairBerth(EntityState ship,EntityState dock,List<EntityState> selected,List<UnitOrder> reserved,out Vector2 berth)
        {
            berth=Vector2.zero;Vector2 direction=DockWaterDirection(dock.position);if(direction==Vector2.zero)return false;
            Vector2 side=new Vector2(-direction.y,direction.x);var ignored=new HashSet<int>{ship.id};
            for(int row=0;row<3;row++)for(int slot=0;slot<7;slot++)
            {
                int offset=slot==0?0:(slot+1)/2*(slot%2==1?1:-1);
                Vector2 point=dock.position+direction*(DockQuayLength+ship.def.radius+1.5f+row*1.25f)+side*(offset*(ship.def.radius*2+.4f));
                if(PositionOccupied(point,ship.def.radius,ignored,true,true)||!WaterNavigation.IsWalkable(point,ship.def.radius))continue;
                bool taken=false;
                foreach(var other in Entities)
                    if(other.id!=ship.id&&other.Alive&&other.def.waterborne&&other.orders.Count>0&&other.orders.Peek().kind==OrderKind.DockRepair&&
                        (other.orders.Peek().point-point).sqrMagnitude<Mathf.Pow(ship.def.radius+other.def.radius+.2f,2)){taken=true;break;}
                if(selected!=null)for(int i=0;i<selected.Count&&!taken;i++)if((reserved[i].point-point).sqrMagnitude<Mathf.Pow(ship.def.radius+selected[i].def.radius+.2f,2))taken=true;
                if(taken||!NavalPathExists(WaterNavigation,ship.position,point,ship.def.radius))continue;berth=point;return true;
            }
            return false;
        }
        private void TickDockRepair(EntityState ship,UnitOrder order,float dt)
        {
            var dock=Get(order.target);
            if(NavalRepairReason(ship.owner,ship,dock)!=""){ship.orders.Dequeue();ship.path.Clear();ship.status="Dock repair cancelled";return;}
            if(!WaterNavigation.IsWalkable(order.point,ship.def.radius)||PositionOccupied(order.point,ship.def.radius,new HashSet<int>{ship.id},false,true))
            {
                if(Time<order.destinationRetryAt)return;order.destinationRetryAt=Time+1;
                if(!TryDockRepairBerth(ship,dock,null,null,out var safe)){ship.status="Dock repair berth occupied";return;}
                order.point=order.resolvedPoint=safe;InvalidateCommandPath(ship);
            }
            MoveTowardsTarget(ship,order.point,dt);
            if((ship.position-order.point).sqrMagnitude>.3f*.3f){ship.status="Moving";return;}
            ship.path.Clear();float maximum=MaxHp(ship),heal=Mathf.Min(NavalRepairRate(ship)*dt,maximum-ship.hp);
            if(heal<=.001f){ship.orders.Dequeue();ship.status="Idle";return;}
            var value=ship.def.repairCost;if(value.food<=0&&value.wood<=0&&value.coin<=0)value=EntityCost(ship.owner,ship.def);
            Cost cost=new Cost(0,value.wood,value.coin)*(heal/maximum*.5f);
            if(!CanAfford(ship.owner,cost)){ship.status="Dock repair needs resources";return;}
            float before=ship.hp;var payment=Pay(ship.owner,cost,ship.id,"Dock repair "+ship.id,dock.id);ship.hp+=heal;ship.status="Dock repairing";
            ship.work=new WorkActivity{kind=WorkKind.Repair,target=ship.id,time=Time};
            payment.economy.hasRepair=true;payment.economy.healthTarget=ship.id;payment.economy.hpBefore=before;payment.economy.hpAfter=ship.hp;
        }
    }
}
