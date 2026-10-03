using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public enum EntityKind { Unit, Building, Resource }
    public enum ResourceKind { None, Food, Wood, Coin }
    public enum DamageKind { Melee, Ranged, Siege }
    [Flags] public enum Tags { None=0, Infantry=1, HeavyInfantry=2, LightInfantry=4, Cavalry=8, HeavyCavalry=16, LightCavalry=32, Artillery=64, Villager=128, Building=256, Explorer=512, Guardian=1024 }
    public enum OrderKind { Move, Attack, AttackMove, Gather, Build, Repair, Garrison, Stop, Hold, Claim, Manufacture }
    public enum CommandKind { Order, Build, Train, CancelQueue, CancelBuilding, Research, AdvanceAge, Ship, Rally, Ungarrison, ToggleMode, Demolish, Resign, TradeMode, TradeRoute, Manufacture, ToggleShield }
    [Serializable] public struct Cost
    {
        public float food, wood, coin;
        public Cost(float f,float w,float c) { food=f;wood=w;coin=c; }
        public static Cost operator *(Cost c,float n) => new Cost(c.food*n,c.wood*n,c.coin*n);
        public static Cost operator +(Cost a,Cost b) => new Cost(a.food+b.food,a.wood+b.wood,a.coin+b.coin);
        public override string ToString() => $"{food:0} F / {wood:0} W / {coin:0} C";
    }
    [Serializable] public class TargetBonus { public Tags tags; public float multiplier=1; }
    [Serializable] public class EntityDef
    {
        public string id, name, description, producer, civilization="";
        public string[] excludedCivilizations=Array.Empty<string>();
        public bool civilian,repairArtillery,shieldAbility;
        public EntityKind kind; public Tags tags; public Cost cost;
        public float hp=100, radius=.4f, width=1, depth=1, speed=4, vision=18;
        public float damage=0, meleeDamage=0, siegeDamage=0, range=1, minRange=0, period=2, windup=.35f, projectileSpeed=30, splash=0;
        public DamageKind damageKind; public float meleeResist=0, rangedResist=0, siegeResist=0;
        public float time=20, buildXp=0, trainXp=0, killXp=0; public int population=1, capacity=0, age=1, batch=1, garrison=0;
        // Capacity and firepower are separate: non-contributing occupants can still take shelter.
        public int garrisonAttackLimit=0; public Tags garrisonAttackTags=Tags.Villager;
        public bool deploy=false; public TargetBonus[] bonuses=Array.Empty<TargetBonus>();
        public ResourceKind resource; public float amount=0, gatherRate=0;
    }
    [Serializable] public class TechDef
    {
        public string id,name,description,building,requires="",civilization=""; public int age=1; public Cost cost; public float time=30;
        public Tags tags; public float attack=0,health=0,gather=0,speed=0; public ResourceKind resource;
        public string[] units=Array.Empty<string>(); public int bankLimit; public float bankIncome;
        public float caravanProfit,manufactureSpeed,buildSpeed,trainSpeed;
    }
    [Serializable] public class CardDef
    {
        public string id,name,description,civilization="",unit="",tech=""; public int age=1,count=0; public bool repeatable=false; public Cost crates; public float travel=35;
    }
    [Serializable] public class AgeDef { public int age; public string name; public Cost cost; public float time; public Cost economicReward; public string militaryUnit; public int militaryCount; }
    [Serializable] public class CivDef { public string id,name,description; public bool houseVillager; public bool coinVillager; public bool caravanTrade,manufacturing; public int bankLimit=0; public float bankRate=0; public int villagerLimit=99; public Cost startingResources=new Cost(300,300,100); public Cost houseCost=new Cost(0,135,0); }
    [Serializable] public class AgeChoiceDef { public string id,civilization,name,description,tech,unit; public int age,count; public Cost crates; }
    [Serializable] public class ManufactureRecipeDef { public string id,name,description,requires; public Cost cost; public float time=40; public int prefabs=1; }
    [Serializable] public class AiDifficultyDef
    { public string id,name,description; public float decisionSeconds,armySeconds; }
    [Serializable] public class TradeSocketDef { public Vector2 position; public int waypoint; }
    [Serializable] public class TreasureDef { public string entity; public Vector2 position; public int guards; public Cost crates; public float xp; }
    [Serializable] public class LandmarkRules
    {
        public float caravanSpeed=4,tradeXp=40,tradeUpgradeMultiplier=1.25f,claimTime=3,guardLeash=10;
        public Cost tradeResources=new Cost(60,50,40);
        public Vector2[] route=Array.Empty<Vector2>(); public TradeSocketDef[] sockets=Array.Empty<TradeSocketDef>();
        public TreasureDef[] treasures=Array.Empty<TreasureDef>();
    }
    [Serializable] public class RuleCatalog
    {
        public EntityDef[] entities; public TechDef[] techs; public CardDef[] cards; public AgeDef[] ages; public CivDef[] civs;
        public AgeChoiceDef[] ageChoices=Array.Empty<AgeChoiceDef>();
        public ManufactureRecipeDef[] manufactureRecipes=Array.Empty<ManufactureRecipeDef>();
        public LandmarkRules landmarks;
        public AiDifficultyDef[] aiDifficulties=Array.Empty<AiDifficultyDef>();
        public AiDifficultyDef Difficulty(string id) => Array.Find(aiDifficulties??Array.Empty<AiDifficultyDef>(),x=>x!=null&&x.id==id);
        public int deckCapacity=8,populationLimit=200; public float initialXp=150,xpIncrease=75,constructionRefund=.75f;
        public EntityDef Entity(string id) => Array.Find(entities,x=>x.id==id);
        public TechDef Tech(string id) => Array.Find(techs,x=>x.id==id);
        public CardDef Card(string id) => Array.Find(cards,x=>x.id==id);
        public CivDef Civ(string id) => Array.Find(civs,x=>x.id==id);
        public AgeDef Age(int n) => Array.Find(ages,x=>x.age==n);
    }
    public class UnitOrder
    {
        public OrderKind kind; public Vector2 point; public int target; public float elapsed;
        // Preserve the command's original point while navigation resolves a taken endpoint.
        public Vector2 resolvedPoint;public bool hasResolvedPoint;
        public int destinationVersion,destinationCursor;public float destinationRetryAt;
        public UnitOrder(OrderKind k,Vector2 p,int t=0) {kind=k;point=p;target=t;}
    }
    public class ProductionJob
    {
        public string item,kind; public int count=1,population; public Cost paid; public float elapsed,duration; public string choice="";
    }
    public class RewardJob { public Cost crates; public string unit=""; public int count; public string waiting=""; }
    public class ShipmentJob { public string card; public float elapsed; public string waiting=""; }
    public enum WorkKind { None,Gather,Build,Repair,Manufacture }
    public struct WorkActivity
    {
        public WorkKind kind;public int target;public ResourceKind resource;public float time;
    }
    public class EntityState
    {
        public int id,owner; public EntityDef def; public Vector2 position,previousPosition,origin,facing=Vector2.up,rally;
        public ResourceKind tradeMode,rallyResource;
        public float hp,construction=1,remaining,attackReadyAt,fireAt=-1,deployUntil,scanAt,stuckFor,pathRetryAt;
        public Vector2 progressGoal,detour;public float progressDistance=float.MaxValue,progressAt,detourUntil;
        public Vector2 pathGoal; public bool isDeployed,shotMelee; public float nextEconomyLog; public Cost paidConstruction;
        public int attackTarget,garrisonedIn,rallyTarget,pathVersion=-1; public bool hold,dead,forceMelee;
        public int routeTown,routeStation,routeLeg;
        public float routeDistance,shieldReadyAt;
        public Cost cargoPaid;
        public bool shielded;
        public readonly Queue<UnitOrder> orders=new Queue<UnitOrder>();
        public readonly List<Vector2> path=new List<Vector2>(); public bool pathAvoidsAllies;
        public int pathIndex;
        public readonly List<ProductionJob> queue=new List<ProductionJob>();
        public readonly List<int> occupants=new List<int>(); public string status="Idle";
        // Last work actually performed, for read-only presentation. It is not an
        // order, economy grant, repeating timer, or mutable shared definition.
        public WorkActivity work;
        public bool IsWorkingAt(float time)
        {
            if(!Alive||(!Worker&&!def.repairArtillery)||garrisonedIn!=0||work.kind==WorkKind.None||Mathf.Abs(work.time-time)>.0001f||orders.Count==0)return false;
            var order=orders.Peek();
            return order.target==work.target&&(work.kind==WorkKind.Gather&&order.kind==OrderKind.Gather
                ||work.kind==WorkKind.Build&&order.kind==OrderKind.Build||work.kind==WorkKind.Repair&&order.kind==OrderKind.Repair||work.kind==WorkKind.Manufacture&&order.kind==OrderKind.Manufacture);
        }
        public bool Alive => !dead && hp>0;
        public bool Complete => construction>=1;
        public bool Worker => (def.tags & Tags.Villager)!=0;
        public bool Military => def.kind==EntityKind.Unit && !Worker && !def.civilian;
    }
    public class PlayerState
    {
        public int id,age=1,usedPopulation,reservedPopulation,populationCap,shipmentTokens,xpLevels;
        public int prefabCharges;
        public string civilization; public Cost resources; public float xp; public bool resigned;
        public readonly HashSet<string> technologies=new HashSet<string>();
        public readonly HashSet<string> sentCards=new HashSet<string>(); public readonly List<string> deck=new List<string>();
        public readonly List<RewardJob> rewards=new List<RewardJob>();
        public readonly List<ShipmentJob> shipments=new List<ShipmentJob>();
        public readonly Dictionary<int,Vector2> enemyMemory=new Dictionary<int,Vector2>();
        public Cost gathered,spent,refunded,passiveIncome,tradeIncome; public int kills,losses,trained,shipsSent,treasuresClaimed,guardiansDefeated; public readonly List<string> ageHistory=new List<string>();
    }
    public class GameCommand
    {
        public int player; public CommandKind kind; public int[] actors=Array.Empty<int>(); public OrderKind order;
        public Vector2 point; public int target,index,count=1; public string item="",choice=""; public bool append;
    }
    public struct CommandResult
    {
        public bool accepted; public string reason;
        public static CommandResult Ok(string why="Accepted") => new CommandResult{accepted=true,reason=why};
        public static CommandResult Fail(string why) => new CommandResult{accepted=false,reason=why};
    }
    [Serializable] public class SimEvent
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        // Explicit presence flags: Unity JSON deserialization can create default nested objects.
        public bool hasEconomy, hasPopulation;
        public EconomyTransaction economy;
        public PopulationSnapshot population;
        public float time; public string kind,message; public int player,entity,target,visibilityMask; public float amount; public Vector2 position,facing;
    }
    public class ProjectileState
    {
        public float baseDamage,siegeDamage; public DamageKind baseKind;
        public int source,target,owner; public Vector2 start,end; public float firedAt,hitAt,damage,splash; public DamageKind kind;
    }
}
