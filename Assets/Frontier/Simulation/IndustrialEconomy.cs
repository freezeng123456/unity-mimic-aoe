using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        public const int FactoryLimit=2;

        public bool ClassicIndustryAvailable(int player)
        {
            var p=P(player);if(p==null)return false;
            return p.civilization=="coast"||p.civilization=="river"||p.civilization=="france"||p.civilization=="ottoman";
        }

        private int LivingIndustry(int player,bool wagons)
        {
            int count=0;foreach(var e in Entities)
                if(e.Alive&&e.owner==player&&(e.def.id=="factory"||wagons&&e.def.id=="factory_wagon"))count++;
            return count;
        }

        // In-flight wagons reserve the same two entitlements as living wagons
        // and foundations. Arrival never bypasses a later occupied limit.
        public string IndustrialShipmentReason(int player,CardDef card,bool arriving=false)
        {
            if(card==null||card.unit!="factory_wagon")return "";
            if(!ClassicIndustryAvailable(player))return "Factory shipments require a classic civilization";
            if(card.count!=1)return "Factory shipments must contain one wagon";
            int used=LivingIndustry(player,true);
            foreach(var shipment in Players[player].shipments)
            {
                var pending=Rules.Card(shipment.card);
                if(pending?.unit=="factory_wagon")used+=pending.count;
            }
            // During delivery this shipment is already in the pending list.
            return used+(arriving?0:card.count)>FactoryLimit?"Factory and wagon limit reached":"";
        }

        public string IndustrialBuildingReason(int player,string building)
        {
            if(building!="factory")return "";
            if(!ClassicIndustryAvailable(player))return "Factories require a classic civilization";
            if(LivingIndustry(player,false)>=FactoryLimit)return "Factory limit reached";
            foreach(var e in Entities)if(e.Alive&&e.owner==player&&e.def.id=="factory_wagon"&&e.garrisonedIn==0)return "";
            return "A delivered factory wagon is required";
        }

        public string IndustrialDeploymentReason(int player,Vector2 point,int wagonId=0)
        {
            var definition=Rules.Entity("factory");if(definition==null)return "Unknown factory";
            foreach(var wagon in Entities)
            {
                if(!wagon.Alive||wagon.owner!=player||wagon.id==wagonId||wagon.def.id!="factory_wagon")continue;
                foreach(var order in wagon.orders)
                    // CanBuild checks width/depth + 1 through inclusive
                    // floor/ceil grid bounds. Two integer 4m foundations at
                    // 5m separation would therefore block each other's check.
                    if(order.kind==OrderKind.DeployFactory&&BuildingFootprintsOverlap(definition,point,definition,order.point,2))
                        return "Another factory wagon is deploying to this site";
            }
            return "";
        }

        public bool TryHandleIndustrialCommand(GameCommand command,out CommandResult result)
        {
            result=default;
            if(command.kind==CommandKind.FactoryMode)
            {
                if(!ValidFactoryMode(command.item)){result=CommandResult.Fail("Choose food, wood, coin or artillery");return true;}
                var factories=new List<EntityState>();
                if(command.actors!=null)foreach(int id in command.actors)
                {
                    var factory=Get(id);
                    if(factory!=null&&factory.Alive&&factory.owner==command.player&&factory.Complete&&factory.def.id=="factory"&&!factories.Contains(factory))factories.Add(factory);
                }
                if(factories.Count==0){result=CommandResult.Fail("Select your completed factory");return true;}
                foreach(var factory in factories)
                {
                    if(factory.factoryMode==command.item)continue;
                    // Finish the old resource bucket before its mode label changes.
                    FlushGather(factory.id);
                    factory.factoryMode=command.item;factory.factoryElapsed=0;
                    factory.status="Factory: "+command.item;
                    Emit("FactoryModeChanged",command.player,factory.id,command.item);
                }
                result=CommandResult.Ok("Factory production: "+command.item);return true;
            }
            if(command.kind!=CommandKind.Build||command.item!="factory")return false;
            EntityState wagon=null;
            if(command.actors!=null)foreach(int id in command.actors)
            {
                var selected=Get(id);
                if(selected!=null&&selected.Alive&&selected.owner==command.player&&selected.def.id=="factory_wagon"&&selected.garrisonedIn==0){wagon=selected;break;}
            }
            if(wagon==null){result=CommandResult.Fail("Select a delivered factory wagon");return true;}
            command.point=BuildPoint(command.player,command.item,command.point);
            string reason=CanBuild(command.player,command.item,command.point);
            if(reason!=""){result=CommandResult.Fail(reason);return true;}
            reason=IndustrialDeploymentReason(command.player,command.point,wagon.id);
            if(reason!=""){result=CommandResult.Fail(reason);return true;}
            var definition=Rules.Entity("factory");Vector2 direction=(wagon.position-command.point).normalized;
            if(direction.sqrMagnitude<.5f)direction=Vector2.right;
            float reach=Mathf.Max(definition.width,definition.depth)*.5f+wagon.def.radius+1.5f;
            Vector2 approach=Navigation.NearestWalkable(command.point+direction*reach,wagon.def.radius);
            if(!Navigation.IsWalkable(approach,wagon.def.radius)||BuildingFootprintDistance(definition,command.point,approach)<=wagon.def.radius+.25f)
            {result=CommandResult.Fail("No safe factory wagon approach");return true;}
            if(!command.append){wagon.orders.Clear();InvalidateCommandPath(wagon);}
            wagon.hold=false;wagon.fireAt=-1;wagon.attackTarget=0;
            wagon.orders.Enqueue(new UnitOrder(OrderKind.DeployFactory,command.point){resolvedPoint=approach,hasResolvedPoint=true});
            wagon.status="Moving";
            Emit("FactoryDeploymentOrdered",command.player,wagon.id,"Moving to factory site",0,0,command.point);
            result=CommandResult.Ok("Factory wagon moving to construction site");return true;
        }

        // A deployment is a mobile order, so rivers and occupied corridors use
        // the same navigation and body sweeps as every other land actor.
        public void TickFactoryDeployment(EntityState wagon,UnitOrder order,float dt)
        {
            if(wagon==null||!wagon.Alive||wagon.def.id!="factory_wagon"||!ClassicIndustryAvailable(wagon.owner))
            {if(wagon!=null&&wagon.orders.Count>0)wagon.orders.Dequeue();return;}
            if(!ResolveFactoryApproach(wagon,order)){wagon.path.Clear();wagon.status="Factory deployment waiting for a safe approach";return;}
            MoveTowardsTarget(wagon,order.resolvedPoint,dt);
            if(Vector2.Distance(wagon.position,order.resolvedPoint)>.15f){wagon.status="Moving";return;}
            if(Time<order.elapsed)return;
            string reason=CanBuild(wagon.owner,"factory",order.point);
            if(reason=="")reason=IndustrialDeploymentReason(wagon.owner,order.point,wagon.id);
            if(reason!=""){wagon.status="Factory deployment waiting: "+reason;order.elapsed=Time+.5f;return;}
            // Only arrival consumes the wagon. A rejected or stopped deployment
            // leaves the real shipment entitlement available for another site.
            var foundation=Spawn("factory",wagon.owner,order.point,false);
            foundation.status="Factory under construction";Remove(wagon);
            Emit("FactoryFoundationStarted",foundation.owner,foundation.id,"Factory wagon consumed",1,wagon.id);
        }

        private bool SafeFactoryApproach(EntityState wagon,Vector2 site,Vector2 point)
        {
            var definition=Rules.Entity("factory");
            if(!Navigation.IsWalkable(point,wagon.def.radius))return false;
            float edge=BuildingFootprintDistance(definition,site,point)-wagon.def.radius;
            if(edge<.15f||edge>1.55f)return false;
            // The future foundation's conservative cells must also leave the
            // wagon's full body outside the wall when it is spawned.
            foreach(var cell in BuildingFootprintCells(definition,site))
            {
                Vector2 distance=new Vector2(Mathf.Max(0,Mathf.Abs(point.x-cell.x)-.5f),Mathf.Max(0,Mathf.Abs(point.y-cell.y)-.5f));
                if(distance.sqrMagnitude<(wagon.def.radius+.05f)*(wagon.def.radius+.05f))return false;
            }
            return true;
        }

        private bool ResolveFactoryApproach(EntityState wagon,UnitOrder order)
        {
            // Reuse ordinary destination occupancy, including reserved Move
            // endpoints, while constraining candidates to the construction ring.
            GatherDestinationSpaces(wagon,order.point);
            if(order.hasResolvedPoint&&SafeFactoryApproach(wagon,order.point,order.resolvedPoint)&&
                DestinationAvailable(wagon,order.resolvedPoint)&&DestinationSpaceFree(wagon,order.resolvedPoint))return true;
            bool revised=order.destinationVersion!=Navigation.Version;
            if(!revised&&Time<order.destinationRetryAt)return false;
            order.destinationVersion=Navigation.Version;order.destinationRetryAt=Time+1.2f;
            if(revised)order.destinationCursor=0;
            var definition=Rules.Entity("factory");
            float reach=Mathf.Max(definition.width,definition.depth)*.5f+wagon.def.radius+1.5f;
            var candidates=new List<DestinationCandidate>();
            for(int k=0;k<32;k++)
            {
                float angle=k*Mathf.PI*2/32;Vector2 point=order.point+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*reach;
                if(!SafeFactoryApproach(wagon,order.point,point)||!DestinationAvailable(wagon,point)||!DestinationSpaceFree(wagon,point))continue;
                candidates.Add(new DestinationCandidate{point=point,score=(point-wagon.position).sqrMagnitude});
            }
            candidates.Sort(CompareDestinationCandidates);
            for(int probe=0;probe<Mathf.Min(12,candidates.Count);probe++)
            {
                int index=order.destinationCursor%candidates.Count;order.destinationCursor=(index+1)%candidates.Count;
                var candidate=candidates[index].point;
                if(!Navigation.ClearLine(wagon.position,candidate,wagon.def.radius))
                {
                    var route=Navigation.FindPath(wagon.position,candidate,wagon.def.radius);
                    if(route.Count<2||(route[route.Count-1]-candidate).sqrMagnitude>.0001f)continue;
                }
                bool changed=!order.hasResolvedPoint||(order.resolvedPoint-candidate).sqrMagnitude>.0001f;
                order.resolvedPoint=candidate;order.hasResolvedPoint=true;ResetDestinationPath(wagon);
                if(changed)Emit("FactoryApproachAdjusted",wagon.owner,wagon.id,"Factory wagon changed construction side",0,0,candidate);
                return true;
            }
            return false;
        }

        public static bool ValidFactoryMode(string mode)=>mode=="food"||mode=="wood"||mode=="coin"||mode=="artillery";
        private static ResourceKind FactoryResource(string mode)=>mode=="food"?ResourceKind.Food:mode=="wood"?ResourceKind.Wood:mode=="coin"?ResourceKind.Coin:ResourceKind.None;

        public EntityDef FactoryArtilleryDefinition(int player)
        {
            if(!ClassicIndustryAvailable(player))return null;
            string civilization=Players[player].civilization;
            return Rules.Entity(civilization=="coast"?"rocket":civilization=="ottoman"?"great_bombard":"heavy_cannon");
        }

        public float FactoryResourceRate(int player,ResourceKind resource)
        {
            if(!ClassicIndustryAvailable(player)||resource==ResourceKind.None)return 0;
            float factor=1;
            foreach(string id in Players[player].technologies)
            {
                var tech=Rules.Tech(id);
                if(tech!=null&&(tech.resource==ResourceKind.None||tech.resource==resource))factor+=tech.factoryIncome;
            }
            return (Rules.Entity("factory")?.gatherRate??0)*Mathf.Clamp(factor,.1f,4);
        }

        public float FactoryArtillerySeconds(int player)
        {
            var artillery=FactoryArtilleryDefinition(player);
            return artillery==null?0:artillery.time/CivilizationFactor(player,t=>t.factoryArtillerySpeed);
        }

        public void TickIndustrialEconomy(float dt)
        {
            if(dt<=0||float.IsNaN(dt)||float.IsInfinity(dt)||Paused||Winner>=0)return;
            int originalCount=Entities.Count;
            for(int i=0;i<originalCount;i++)
            {
                var factory=Entities[i];
                if(!factory.Alive||factory.def.id!="factory"||factory.garrisonedIn!=0||!ClassicIndustryAvailable(factory.owner))continue;
                if(!factory.Complete)
                {
                    float delta=dt*CivilizationFactor(factory.owner,t=>t.buildSpeed)/Mathf.Max(1,factory.def.time);
                    factory.construction=Mathf.Min(1,factory.construction+delta);
                    factory.hp=Mathf.Min(MaxHp(factory),factory.hp+delta*MaxHp(factory)*.95f);
                    factory.status="Factory under construction";
                    if(factory.Complete)
                    {
                        RecomputePopulationCap(factory.owner);AwardXp(factory.owner,factory.def.buildXp,factory.id,"Building completed");
                        Emit("BuildingComplete",factory.owner,factory.id,factory.def.name+" ready");factory.status="Factory: "+factory.factoryMode;
                    }
                    // Completion at this tick's end never earns a full tick of
                    // resources or artillery progress before construction ends.
                    continue;
                }
                if(factory.queue.Count>0){factory.status="Factory research in progress";continue;}
                var resource=FactoryResource(factory.factoryMode);
                if(resource!=ResourceKind.None)
                {
                    Credit(factory.owner,resource,FactoryResourceRate(factory.owner,resource)*dt,factory.id,factory.id,"FactoryIncome");
                    factory.status="Producing "+factory.factoryMode;continue;
                }
                if(factory.factoryMode!="artillery"){factory.status="Choose a factory production mode";continue;}
                var artillery=FactoryArtilleryDefinition(factory.owner);if(artillery==null)continue;
                float duration=Mathf.Max(1,FactoryArtillerySeconds(factory.owner));
                factory.factoryElapsed=Mathf.Min(duration,factory.factoryElapsed+dt);
                if(factory.factoryElapsed<duration){factory.status="Producing "+artillery.name;continue;}
                var owner=Players[factory.owner];
                string limit=UnitLimitReason(factory.owner,artillery,1);
                if(limit!=""){factory.status=limit;continue;}
                if(owner.usedPopulation+owner.reservedPopulation+artillery.population>Mathf.Min(owner.populationCap,Rules.populationLimit))
                {factory.status="Factory artillery waiting for housing";continue;}
                if(!FindSpawnPositions(factory,1,artillery.radius,out var positions))
                {factory.status="Factory artillery exit blocked";continue;}
                var produced=Spawn(artillery.id,factory.owner,positions[0]);
                owner.trained++;AwardXp(factory.owner,artillery.trainXp,factory.id,"Factory artillery produced");
                AssignProducerRally(factory,new List<EntityState>{produced});factory.factoryElapsed=0;
                factory.status="Producing "+artillery.name;
                Emit("FactoryArtilleryProduced",factory.owner,factory.id,artillery.id,1,produced.id);
            }
        }
    }
}
