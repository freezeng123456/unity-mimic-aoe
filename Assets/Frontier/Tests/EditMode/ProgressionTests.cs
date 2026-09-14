using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Frontier.Tests
{
    public sealed class ProgressionTests
    {
        [Test] public void RiverSettlerUsesCoinAndOwnHousingCost()
        {
            var w=new World(Catalog.Load(),42,"river","coast",5);var town=w.Entities.First(e=>e.owner==0&&e.def.id=="town");var before=w.Players[0].resources;
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item="villager"}).accepted);
            Assert.AreEqual(before.coin-100,w.Players[0].resources.coin);Assert.AreEqual(before.food,w.Players[0].resources.food);
            Assert.AreEqual(100,w.EntityCost(0,w.Rules.Entity("house")).wood);Assert.AreEqual(135,w.EntityCost(1,w.Rules.Entity("house")).wood);
        }
        [Test] public void CountingHouseHasRealLifecycleAndDistinctIncomeLedger()
        {
            var w=new World(Catalog.Load(),42,"river","coast",5);var bank=w.Spawn("bank",0,new Vector2(39,37),false);float before=w.Players[0].resources.coin;
            w.Tick(1);Assert.AreEqual(before,w.Players[0].resources.coin);bank.construction=1;
            w.Tick(1);Assert.AreEqual(before+2.5f,w.Players[0].resources.coin,.001);Assert.AreEqual(2.5f,w.Players[0].passiveIncome.coin,.001);Assert.AreEqual(0,w.Players[0].gathered.coin);
            w.Remove(bank);before=w.Players[0].resources.coin;w.Tick(1);Assert.AreEqual(before,w.Players[0].resources.coin);w.FlushEconomyLog();Assert.IsTrue(w.Events.Any(e=>e.kind=="BankIncome"&&e.entity==bank.id));
        }
        [Test] public void BankCharterStacksOnceAndSettlerCapIncludesTrainingReservations()
        {
            var w=new World(Catalog.Load(),42,"river","coast",5);w.ApplyTechnology(0,"bank_charter");w.ApplyTechnology(0,"bank_charter");Assert.AreEqual(5,w.CountingHouseLimit(0));
            for(int i=0;i<43;i++)w.Spawn("villager",0,new Vector2(20+i%8*2,10+i/8*2));w.Players[0].populationCap=200;
            var town=w.Entities.First(e=>e.owner==0&&e.def.id=="town");
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item="villager"}).accepted);
            Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item="villager"}).accepted);Assert.AreEqual(50,w.UnitCountIncludingProduction(0,"villager"));
        }
        [Test] public void FactionUnitsAndDecksCannotCrossCivilizationBoundary()
        {
            var w=new World(Catalog.Load(),42,"river","coast",5);w.Players[0].age=3;w.Players[0].resources=new Cost(10000,10000,10000);var b=w.Spawn("barracks",0,new Vector2(38,37));
            Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{b.id},item="longbow"}).accepted);
            Assert.AreNotEqual("",w.ValidateDeck(0,new[]{"pikes"}));Assert.AreNotEqual("",w.ValidateDeck(0,new[]{"river_coin","river_coin"}));
            foreach(var civ in w.Rules.civs){var deck=DeckLibrary.Preset(w.Rules,civ.id,"military");Assert.AreEqual(25,deck.Count);Assert.AreEqual("",DeckLibrary.Validate(w.Rules,civ.id,deck));}
        }
        [Test] public void AllFiveAgesCompleteWithPaidOrdersAndNonemptyRewards()
        {
            var w=new World(Catalog.Load(),42,"coast","river",5);var p=w.Players[0];p.resources=new Cost(20000,20000,20000);var town=w.Entities.First(e=>e.owner==0&&e.def.id=="town");
            for(int age=2;age<=5;age++)
            {
                var a=w.Rules.Age(age);var before=p.resources;
                Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{town.id},choice="economic"}).accepted);
                Assert.AreEqual(before.food-a.cost.food,p.resources.food);for(int t=0;t<Mathf.CeilToInt(a.time*10)+2;t++)w.Tick(.1f);
                Assert.AreEqual(age,p.age);Assert.Greater(a.economicReward.food+a.economicReward.wood+a.economicReward.coin,0);
            }
            Assert.AreEqual(4,w.Events.Count(e=>e.kind=="AgeAdvanced"));Assert.AreEqual(4,w.Events.Count(e=>e.kind=="AgeRewardArrived"));
        }
        [Test] public void UnitSpecificUpgradeAffectsCurrentAndQueuedUnitsExactlyOnce()
        {
            var w=new World(Catalog.Load(),42,"coast","river",5);var p=w.Players[0];p.age=3;p.resources=new Cost(5000,5000,5000);var b=w.Spawn("barracks",0,new Vector2(38,37));
            var musket=w.Spawn("musketeer",0,new Vector2(46,30));var pike=w.Spawn("pike",0,new Vector2(48,30));musket.hp*=.5f;float pikeHp=w.MaxHp(pike);
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{b.id},item="musketeer"}).accepted);
            w.ApplyTechnology(0,"veteran_musketeer");w.ApplyTechnology(0,"veteran_musketeer");
            Assert.AreEqual(musket.def.hp*1.2f,w.MaxHp(musket),.01);Assert.AreEqual(w.MaxHp(musket)*.5f,musket.hp,.01);Assert.AreEqual(pikeHp,w.MaxHp(pike));
            for(int i=0;i<500;i++)w.Tick(.1f);
            var trained=w.Entities.Last(e=>e.owner==0&&e.def.id=="musketeer");Assert.AreNotEqual(musket.id,trained.id);Assert.AreEqual(w.MaxHp(musket),w.MaxHp(trained),.01);Assert.AreEqual(w.Attack(musket),w.Attack(trained),.01);
            Assert.IsTrue(w.ProductionReason(0,b,"imperial_musketeer",true).Length>0,"Cannot skip age or predecessor");
        }
    }
}
