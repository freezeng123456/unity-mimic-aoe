using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public class ClassicCivilizationSystemsTests
    {
        private World world;
        private EntityState town;
        private void Setup(string civilization)
        {
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            world=new World(rules,civ0:civilization,civ1:"coast",maxAge:5);
            foreach(var e in world.Entities.ToArray())world.Remove(e);
            town=world.Spawn("town",0,new Vector2(20,20));
            world.Spawn("town",1,new Vector2(110,110));
            world.Players[0].resources=new Cost();
            world.Fog.Tick(world.Entities);
        }
        private void Steps(float seconds)
        {for(int i=0;i<Mathf.CeilToInt(seconds*10);i++)world.Tick(.1f);}
        private int AutomaticCount=>world.Events.Count(e=>e.player==0&&e.kind=="AutomaticWorkerProduced");
        private CommandResult Toggle()=>world.Issue(new GameCommand{player=0,kind=CommandKind.ToggleRecruitment,actors=new[]{town.id}});

        [Test] public void FrenchOpeningAndPaidTrainingUseCoureurs()
        {
            world=new World(Catalog.Load(),civ0:"france",civ1:"coast",maxAge:5);
            Assert.AreEqual(6,world.Entities.Count(e=>e.owner==0&&e.Worker&&e.def.id=="coureur"));
            Assert.IsFalse(world.Entities.Any(e=>e.owner==0&&e.def.id=="villager"));
            town=world.Entities.First(e=>e.owner==0&&e.def.id=="town");
            var before=world.Players[0].resources;
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item="villager"}).accepted);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item="coureur"}).accepted);
            Assert.AreEqual(before.food-120,world.Players[0].resources.food,.001f);
            Steps(world.Rules.Entity("coureur").time+1);
            Assert.AreEqual(7,world.Entities.Count(e=>e.owner==0&&e.Worker));
            Assert.AreEqual(1,world.Players[0].trained);
        }
        [Test] public void FrenchWorkerProducesActualExtraGatheringAndSharesLimitAcrossWorkerIds()
        {
            Setup("france");var worker=world.Spawn("coureur",0,new Vector2(40,40));
            var wood=world.Spawn("tree",-1,new Vector2(41,40));world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Gather,target=wood.id}).accepted);
            Steps(10);
            Assert.AreEqual(wood.def.gatherRate*1.25f*10,world.Players[0].resources.wood,.02f);
            world.Players[0].resources=new Cost(10000,0,0);
            for(int i=1;i<79;i++)world.Spawn(i%2==0?"coureur":"villager",0,new Vector2(60+i%8,60+i/8));
            world.Players[0].populationCap=200;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item="coureur"}).accepted);
            Assert.AreEqual(80,world.WorkerCountIncludingProduction(0));
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item="coureur"}).accepted);
        }
        [Test] public void DutchMineBonusDoesNotIncreasePlantationIncome()
        {
            Setup("river");var worker=world.Spawn("villager",0,new Vector2(40,40));
            var mine=world.Spawn("mine",-1,new Vector2(42,40));var plantation=world.Spawn("estate",0,new Vector2(70,70));
            Assert.AreEqual(mine.def.gatherRate*1.15f,world.GatherRate(worker,mine),.001f);
            Assert.AreEqual(plantation.def.gatherRate,world.GatherRate(worker,plantation),.001f);
        }
        [Test] public void AutomaticSettlerIsRealFreeUnitWithRallyAndSeparateLedger()
        {
            Setup("ottoman");town.rally=new Vector2(36,20);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item="villager"}).accepted);
            Steps(world.AutoRecruitSeconds(0)+.2f);
            var ev=world.Events.Single(e=>e.player==0&&e.kind=="AutomaticWorkerProduced");var unit=world.Get(ev.target);
            Assert.IsNotNull(unit);Assert.IsTrue(unit.Worker);Assert.AreEqual(1,world.Players[0].usedPopulation);
            Assert.AreEqual(OrderKind.Move,unit.orders.Peek().kind);Assert.AreEqual(town.rally,unit.orders.Peek().point);
            Assert.AreEqual(new Cost(),world.Players[0].resources);Assert.AreEqual(new Cost(),world.Players[0].spent);
            Assert.AreEqual(0,world.Players[0].trained);Assert.IsFalse(world.Events.Any(e=>e.player==0&&e.kind=="Payment"));
            Assert.IsTrue(ev.hasPopulation);Assert.AreEqual(1,ev.population.used);
        }
        [Test] public void AutoRecruitmentPausesWithTownQueuePlayerToggleAndWorldPause()
        {
            Setup("ottoman");Steps(8);float elapsed=town.autoRecruitElapsed;
            Assert.IsTrue(Toggle().accepted);Steps(60);Assert.AreEqual(elapsed,town.autoRecruitElapsed);Assert.AreEqual(0,AutomaticCount);
            Assert.IsTrue(Toggle().accepted);world.Paused=true;Steps(60);Assert.AreEqual(elapsed,town.autoRecruitElapsed);world.Paused=false;
            world.Players[0].resources=new Cost(10000,10000,10000);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{town.id},choice=world.AgeChoices(0,2)[0].id}).accepted);
            Steps(5);Assert.AreEqual(elapsed,town.autoRecruitElapsed);Assert.AreEqual("Town center queue busy",world.AutoRecruitReason(town));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{town.id},index=0}).accepted);
            Steps(world.AutoRecruitSeconds(0)-elapsed+1);Assert.AreEqual(1,AutomaticCount);
        }
        [Test] public void IncompleteDestroyedAndForeignTownsCannotRecruitOrBeToggled()
        {
            Setup("ottoman");town.construction=.5f;Steps(60);Assert.AreEqual(0,AutomaticCount);Assert.IsFalse(Toggle().accepted);
            town.construction=1;var enemy=world.Entities.First(e=>e.owner==1&&e.def.id=="town");
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.ToggleRecruitment,actors=new[]{enemy.id}}).accepted);
            world.Spawn("villager",0,new Vector2(40,40));world.Remove(town);Steps(60);Assert.AreEqual(0,AutomaticCount);
        }
        [Test] public void HousingAndSettlerCapsBlockWithoutRecruitmentDebt()
        {
            Setup("ottoman");
            for(int i=0;i<20;i++)world.Spawn("villager",0,new Vector2(50+i%5*2,30+i/5*2));
            Steps(120);Assert.AreEqual(0,AutomaticCount);Assert.AreEqual(0,town.autoRecruitElapsed);StringAssert.Contains("housing",world.AutoRecruitReason(town));
            world.Spawn("house",0,new Vector2(40,60));Steps(world.AutoRecruitSeconds(0)+1);Assert.AreEqual(1,AutomaticCount);
            for(int i=21;i<25;i++)world.Spawn("villager",0,new Vector2(50,i));
            float elapsed=town.autoRecruitElapsed;Steps(120);Assert.AreEqual(1,AutomaticCount);Assert.AreEqual(elapsed,town.autoRecruitElapsed);
            StringAssert.Contains("limit",world.AutoRecruitReason(town));
        }
        [Test] public void BlockedExitHoldsOneReadyRecruitAndDoesNotBurstAfterUnblocking()
        {
            Setup("ottoman");world.Navigation.SetBlocked(town.position,15,true);
            Steps(180);Assert.AreEqual(0,AutomaticCount);Assert.AreEqual(world.AutoRecruitSeconds(0),town.autoRecruitElapsed);
            StringAssert.Contains("exit blocked",world.AutoRecruitReason(town));
            world.Navigation.SetBlocked(town.position,15,false);Steps(.1f);Assert.AreEqual(1,AutomaticCount);
            Steps(1);Assert.AreEqual(1,AutomaticCount);Assert.Less(town.autoRecruitElapsed,2);
        }
        [Test] public void MultipleTownsCannotExceedTheSharedSettlerCap()
        {
            Setup("ottoman");var second=world.Spawn("town",0,new Vector2(70,20));
            for(int i=0;i<24;i++)world.Spawn("villager",0,new Vector2(40+i%5*2,60+i/5*2));
            town.autoRecruitElapsed=world.AutoRecruitSeconds(0);second.autoRecruitElapsed=world.AutoRecruitSeconds(0);
            Steps(.1f);Assert.AreEqual(25,world.WorkerCountIncludingProduction(0));Assert.AreEqual(1,AutomaticCount);
        }
        [Test] public void MosqueResearchPaysCompletesAndChangesRecruitmentWithoutDuplicating()
        {
            Setup("ottoman");world.Players[0].age=2;world.Players[0].resources=new Cost(10000,10000,10000);
            var mosque=world.Spawn("mosque",0,new Vector2(60,20));
            var cap=world.Rules.techs.First(t=>t.building=="mosque"&&t.villagerLimit>0&&t.age<=2&&string.IsNullOrEmpty(t.requires));
            var speed=world.Rules.techs.First(t=>t.building=="mosque"&&t.autoVillagerSpeed>0&&t.age<=2&&string.IsNullOrEmpty(t.requires));
            int initialLimit=world.WorkerLimit(0);float initialSeconds=world.AutoRecruitSeconds(0);var balance=world.Players[0].resources;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{mosque.id},item=cap.id}).accepted);
            Assert.AreEqual(balance.wood-cap.cost.wood,world.Players[0].resources.wood,.01f);Steps(cap.time+1);
            Assert.AreEqual(initialLimit+cap.villagerLimit,world.WorkerLimit(0));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{mosque.id},item=speed.id}).accepted);Steps(speed.time+1);
            Assert.Less(world.AutoRecruitSeconds(0),initialSeconds);
            float finalSeconds=world.AutoRecruitSeconds(0);world.ApplyTechnology(0,speed.id);world.ApplyTechnology(0,cap.id);
            Assert.AreEqual(finalSeconds,world.AutoRecruitSeconds(0));Assert.AreEqual(initialLimit+cap.villagerLimit,world.WorkerLimit(0));
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{mosque.id},item=speed.id}).accepted);
        }
        [Test] public void DutchEarlySkirmishersAndClassicRosterRestrictionsAreReal()
        {
            Setup("river");world.Players[0].age=2;world.Players[0].resources=new Cost(5000,5000,5000);
            var barracks=world.Spawn("barracks",0,new Vector2(60,20));
            Assert.AreEqual(2,world.UnitAge(0,world.Rules.Entity("skirmisher")));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{barracks.id},item="skirmisher"}).accepted);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{barracks.id},item="musketeer"}).accepted);
            Assert.IsFalse(DeckLibrary.CardAllowed(world.Rules,"river",new CardDef{unit="dragoon"}));
        }
        [Test] public void CuirassierMeleeAreaRecomputesVictimsAndExcludesFriendsShelteredUnitsAndResources()
        {
            Setup("france");var def=world.Rules.Entity("cuirassier");def.period=20;def.windup=.1f;def.damage=40;def.meleeDamage=40;def.meleeSplash=3;
            def.meleeBonuses=new[]{new TargetBonus{tags=Tags.Infantry,multiplier=3}};
            var attacker=world.Spawn("cuirassier",0,new Vector2(40,40));var primary=world.Spawn("pike",1,new Vector2(41.5f,40));
            var secondary=world.Spawn("villager",1,new Vector2(42,41));secondary.def.meleeResist=.4f;
            var friend=world.Spawn("villager",0,new Vector2(41.5f,41));var sheltered=world.Spawn("villager",1,new Vector2(42,40));sheltered.garrisonedIn=town.id;
            var far=world.Spawn("villager",1,new Vector2(47,40));var tree=world.Spawn("tree",-1,new Vector2(42,42));
            var building=world.Spawn("house",1,new Vector2(43,42));float buildingHp=building.hp;
            foreach(var victim in new[]{primary,secondary,friend,sheltered,far}){victim.hp=500;victim.def.damage=0;victim.def.meleeDamage=0;victim.hold=true;}
            float treeHp=tree.hp;world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{attacker.id},order=OrderKind.Attack,target=primary.id}).accepted);Steps(.7f);
            Assert.AreEqual(120,world.Events.Single(e=>e.kind=="Damage"&&e.entity==attacker.id&&e.target==primary.id).amount,.01f);
            Assert.AreEqual(12,world.Events.Single(e=>e.kind=="Damage"&&e.entity==attacker.id&&e.target==secondary.id).amount,.01f);
            Assert.AreEqual(500,friend.hp);Assert.AreEqual(500,sheltered.hp);Assert.AreEqual(500,far.hp);Assert.AreEqual(treeHp,tree.hp);
            Assert.AreEqual(buildingHp,building.hp,"Melee cleave affects units; buildings require the separate siege attack");
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="MeleeAreaHit"&&e.entity==attacker.id));
        }
        [TestCase("janissary",2f)]
        [TestCase("musketeer",3f)]
        public void BayonetCavalryBonusNeverLeaksIntoRangedShots(string id,float bonus)
        {
            Setup(id=="janissary"?"ottoman":"coast");
            var attacker=world.Spawn(id,0,new Vector2(40,40));attacker.def.period=20;attacker.def.windup=.1f;attacker.def.projectileSpeed=100;
            var victim=world.Spawn("hussar",1,new Vector2(49,40));victim.def.damage=0;victim.def.meleeDamage=0;victim.def.rangedResist=0;victim.def.meleeResist=0;victim.hold=true;victim.hp=2000;
            world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{attacker.id},order=OrderKind.Attack,target=victim.id}).accepted);Steps(.8f);
            Assert.AreEqual(attacker.def.damage,world.Events.Single(e=>e.kind=="Damage"&&e.entity==attacker.id&&e.target==victim.id).amount,.01f);
            victim.position=attacker.position+Vector2.right*1.1f;attacker.attackReadyAt=world.Time;world.Events.Clear();world.Fog.Tick(world.Entities);Steps(.5f);
            Assert.AreEqual(attacker.def.meleeDamage*bonus,world.Events.Single(e=>e.kind=="Damage"&&e.entity==attacker.id&&e.target==victim.id).amount,.01f);
        }
    }
}
