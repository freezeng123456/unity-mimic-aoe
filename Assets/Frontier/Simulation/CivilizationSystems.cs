using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Project rules: the human UI and AI use the same paid, interruptible commands.
    public partial class World
    {
        public static readonly Cost CaravanLoadCost=new Cost(60,0,0);
        public bool EntityAvailable(int player,EntityDef definition)
        {
            var p=P(player);return p!=null&&definition!=null&&
                (string.IsNullOrEmpty(definition.civilization)||definition.civilization==p.civilization)&&
                (definition.excludedCivilizations==null||Array.IndexOf(definition.excludedCivilizations,p.civilization)<0);
        }
        public List<AgeChoiceDef> AgeChoices(int player,int age)
        {
            var choices=new List<AgeChoiceDef>();var p=P(player);if(p==null)return choices;
            foreach(var choice in Rules.ageChoices??Array.Empty<AgeChoiceDef>())
                if(choice.age==age&&(string.IsNullOrEmpty(choice.civilization)||choice.civilization==p.civilization))choices.Add(choice);
            if(choices.Count==0&&Rules.Age(age)!=null)
            {
                var a=Rules.Age(age);
                choices.Add(new AgeChoiceDef{id="economic",age=age,name="Supply Governor",description="Receive resource crates.",crates=a.economicReward});
                choices.Add(new AgeChoiceDef{id="military",age=age,name="Garrison Governor",description="Receive reinforcements.",unit=LegacyRewardUnit(player,a.militaryUnit),count=a.militaryCount});
            }
            return choices;
        }
        private string LegacyRewardUnit(int player,string unit)
        {
            if(string.IsNullOrEmpty(unit)||EntityAvailable(player,Rules.Entity(unit)))return unit;
            string civ=Players[player].civilization;
            if(civ=="wayfarer")return unit=="pike"?"escort":unit=="hussar"?"outrider":unit=="skirmisher"?"convoy_gunner":"";
            if(civ=="highland"&&unit=="pike")return "shield_guard";
            return "";
        }
        private AgeChoiceDef ResolveAgeChoice(int player,int age,string id)
        {
            foreach(var choice in AgeChoices(player,age))if(choice.id==id)return choice;
            // Keep old recorded commands and existing save-free fixtures valid.
            var a=Rules.Age(age);if(a==null)return null;
            if(id=="economic")return new AgeChoiceDef{id=id,age=age,crates=a.economicReward};
            if(id=="military")return new AgeChoiceDef{id=id,age=age,unit=LegacyRewardUnit(player,a.militaryUnit),count=a.militaryCount};
            return null;
        }
        private float CivilizationFactor(int player,Func<TechDef,float> field)
        {
            float result=1;foreach(var id in Players[player].technologies){var tech=Rules.Tech(id);if(tech!=null)result+=field(tech);}
            return Mathf.Clamp(result,.1f,4);
        }
        private EntityState CaravanHome(int player,EntityState station)
        {
            EntityState home=null;float best=float.MaxValue;
            foreach(var e in Entities)if(e.Alive&&e.Complete&&e.owner==player&&e.def.id=="town")
            {float d=(e.position-station.position).sqrMagnitude;if(d<best){home=e;best=d;}}
            return home;
        }
        public string CaravanRouteReason(int player,EntityState cart,EntityState station)
        {
            if(P(player)==null||!Rules.Civ(Players[player].civilization).caravanTrade)return "This civilization cannot operate caravans";
            if(cart==null||!cart.Alive||cart.owner!=player||cart.def.id!="caravan"||cart.garrisonedIn!=0)return "Select your active caravan";
            if(station==null||!station.Alive||!station.Complete||station.owner!=player||station.def.id!="waystation")return "Choose your completed waystation";
            var home=CaravanHome(player,station);if(home==null)return "A completed town center is required";
            if(Vector2.Distance(home.position,station.position)<12)return "Build the waystation at least 12 units from the town center";
            var start=WorkPoint(home,station);var end=WorkPoint(station,home);
            if(Navigation.FindPath(start,end,cart.def.radius).Count==0)return "No passable trade route";
            return "";
        }
        private CommandResult SetCaravanRoute(GameCommand c)
        {
            var station=Get(c.target);int n=0;string reason="Select caravans";
            foreach(int id in c.actors??Array.Empty<int>())
            {
                var cart=Get(id);reason=CaravanRouteReason(c.player,cart,station);if(reason!="")continue;
                var home=CaravanHome(c.player,station);
                if(cart.routeTown==home.id&&cart.routeStation==station.id&&cart.routeLeg>0){n++;continue;}
                CancelCaravan(cart,"Route changed");cart.orders.Clear();InvalidateCommandPath(cart);
                cart.routeTown=home.id;cart.routeStation=station.id;cart.routeLeg=1;
                // Endpoint distance, capped at 55, prevents detour and waypoint farming.
                cart.routeDistance=Mathf.Clamp(Vector2.Distance(home.position,station.position),12,55);
                cart.hold=false;Emit("CaravanRoute",c.player,cart.id,"Town "+home.id+" to waystation "+station.id,cart.routeDistance,station.id);n++;
            }
            return n>0?CommandResult.Ok("Caravan route assigned; each load costs 60 food"):CommandResult.Fail(reason);
        }
        private void CancelCaravan(EntityState cart,string reason)
        {
            if(cart.routeLeg==0)return;
            if(cart.cargoPaid.food>0)Emit("CaravanCargoLost",cart.owner,cart.id,reason+"; paid cargo is not refunded",cart.cargoPaid.food,cart.routeStation);
            cart.cargoPaid=new Cost();cart.routeTown=cart.routeStation=cart.routeLeg=0;cart.routeDistance=0;
        }
        private void TickCaravan(EntityState cart,float dt)
        {
            var home=Get(cart.routeTown);var station=Get(cart.routeStation);
            if(home==null||!home.Alive||!home.Complete||home.owner!=cart.owner||station==null||!station.Alive||!station.Complete||station.owner!=cart.owner)
            {CancelCaravan(cart,"Trade endpoint lost");cart.path.Clear();cart.status="Trade endpoint lost";return;}
            var destination=cart.routeLeg==2?station:home;
            if(EdgeDistance(cart,destination)>1.5f){MoveTowardsTarget(cart,WorkPoint(destination,cart),dt);return;}
            cart.path.Clear();cart.facing=(destination.position-cart.position).normalized;
            if(cart.routeLeg==2)
            {
                if(cart.cargoPaid.food<=0){CancelCaravan(cart,"No paid cargo");return;}
                float coin=(40+cart.routeDistance*1.6f)*CivilizationFactor(cart.owner,t=>t.caravanProfit);
                var p=Players[cart.owner];var before=p.resources;var income=new Cost(0,0,coin);p.resources+=income;p.tradeIncome+=income;
                var ev=RecordTransaction("CaravanDelivered",p,cart.id,station.id,"Paid cargo delivered",income,before);ev.amount=coin;
                cart.cargoPaid=new Cost();cart.routeLeg=3;cart.status="Returning for cargo";AwardXp(cart.owner,8,cart.id,"Caravan delivered");
                InvalidateCommandPath(cart);return;
            }
            if(!CanAfford(cart.owner,CaravanLoadCost)){cart.status="Caravan needs 60 food for cargo";return;}
            Pay(cart.owner,CaravanLoadCost,cart.id,"Caravan cargo",station.id);cart.cargoPaid=CaravanLoadCost;
            cart.routeLeg=2;cart.status="Cargo loaded";Emit("CaravanLoaded",cart.owner,cart.id,"60 food paid; deliver to earn coin",60,station.id);InvalidateCommandPath(cart);
        }
        public int AssignedArtisans(EntityState workshop)
        {
            if(workshop==null||!workshop.Alive||!workshop.Complete||workshop.def.id!="workshop")return 0;
            int count=0;foreach(var e in Entities)
                if(e.Alive&&e.owner==workshop.owner&&e.Worker&&e.garrisonedIn==0&&e.orders.Count>0&&
                   e.orders.Peek().kind==OrderKind.Manufacture&&e.orders.Peek().target==workshop.id&&EdgeDistance(e,workshop)<=1.5f)count++;
            return Math.Min(4,count);
        }
        public string ManufactureReason(int player,EntityState workshop,string recipeId)
        {
            if(P(player)==null||!Rules.Civ(Players[player].civilization).manufacturing)return "This civilization cannot manufacture";
            if(workshop==null||!workshop.Alive||!workshop.Complete||workshop.owner!=player||workshop.def.id!="workshop")return "Select a completed workshop";
            var recipe=Array.Find(Rules.manufactureRecipes??Array.Empty<ManufactureRecipeDef>(),x=>x.id==recipeId);
            if(recipe==null)return "Unknown manufacturing recipe";
            if(Players[player].age<2)return "Requires age II";
            if(!string.IsNullOrEmpty(recipe.requires)&&!Players[player].technologies.Contains(recipe.requires))return "Requires "+recipe.requires;
            if(workshop.queue.Count>=6)return "Production queue full";
            int stock=Players[player].prefabCharges;
            foreach(var b in Entities)if(b.Alive&&b.owner==player)foreach(var j in b.queue)if(j.kind=="Manufacture")stock+=j.count;
            if(stock+recipe.prefabs>8)return "At most 8 stored or queued prefabrication kits";
            return CanAfford(player,recipe.cost)?"":"Need "+recipe.cost;
        }
        private CommandResult StartManufacture(GameCommand c)
        {
            var b=c.actors!=null&&c.actors.Length>0?Get(c.actors[0]):null;var reason=ManufactureReason(c.player,b,c.item);
            if(reason!="")return CommandResult.Fail(reason);
            var recipe=Array.Find(Rules.manufactureRecipes,x=>x.id==c.item);Pay(c.player,recipe.cost,b.id,"Manufacture "+recipe.name);
            b.queue.Add(new ProductionJob{kind="Manufacture",item=recipe.id,paid=recipe.cost,duration=recipe.time,count=recipe.prefabs});
            Emit("ManufactureQueued",c.player,b.id,recipe.id,recipe.prefabs);return CommandResult.Ok("Order paid; assign settlers to operate the workshop");
        }
        private bool UsesPrefab(string id)=>id=="tower"||id=="barracks"||id=="stable"||id=="foundry"||id=="waystation"||id=="workshop";
        private void ApplyPrefab(EntityState building)
        {
            var p=Players[building.owner];if(p.prefabCharges<=0||!UsesPrefab(building.def.id))return;
            p.prefabCharges--;building.construction=.3f;building.hp=MaxHp(building)*(.05f+.95f*.3f);
            Emit("PrefabUsed",building.owner,building.id,"Paid foundation starts at 30%; kit is consumed",1);
        }
        private CommandResult ToggleShield(GameCommand c)
        {
            int n=0;foreach(int id in c.actors??Array.Empty<int>())
            {
                var e=Get(id);if(e==null||!e.Alive||e.owner!=c.player||!e.def.shieldAbility||e.garrisonedIn!=0||Time<e.shieldReadyAt)continue;
                e.shielded=!e.shielded;e.shieldReadyAt=Time+2;e.hold=e.shielded;e.orders.Clear();e.path.Clear();e.attackTarget=0;e.fireAt=-1;
                Emit("ShieldStance",c.player,e.id,e.shielded?"Deploying shield; ranged protection after 2s":"Lowering shield; mobile after 2s");n++;
            }
            return n>0?CommandResult.Ok("Shield stance changing"):CommandResult.Fail("Select ready shield guards");
        }
    }
}
