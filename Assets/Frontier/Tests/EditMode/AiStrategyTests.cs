using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Frontier.Tests
{
    public sealed class AiStrategyTests
    {
        [Test] public void LateReplacementTroopsRegroupInsteadOfAttackingAlone()
        {
            var w=new World(Catalog.Load(),42,"river","coast",5);w.Time=720;w.Players[0].age=2;w.Players[0].resources=new Cost();
            var a=w.Spawn("musketeer",0,new Vector2(33,33));var b=w.Spawn("musketeer",0,new Vector2(35,33));
            new SkirmishAI(w,0).Tick(.1f);
            foreach(var unit in new[]{a,b})
            {
                Assert.IsNotEmpty(unit.orders);var order=unit.orders.Peek();
                Assert.Less(Vector2.Distance(order.point,new Vector2(24,28)),30,"Elapsed time alone must not order two replacements to the enemy base");
            }
        }
        [Test] public void ExpeditionLosingMostOfItsArmyReturnsForReinforcement()
        {
            var w=new World(Catalog.Load(),42,"river","coast",5);w.Players[0].age=2;w.Players[0].resources=new Cost();
            var troops=Enumerable.Range(0,6).Select(i=>w.Spawn("musketeer",0,new Vector2(32+i,37))).ToArray();var ai=new SkirmishAI(w,0);ai.Tick(.1f);
            Assert.Greater(Vector2.Distance(troops[0].orders.Peek().point,new Vector2(24,28)),60);
            foreach(var unit in troops.Take(4))w.Remove(unit,1);w.Time+=8;ai.Tick(2);
            foreach(var unit in troops.Skip(4))Assert.Less(Vector2.Distance(unit.orders.Peek().point,new Vector2(24,28)),30);
        }
        [Test] public void AiSpendsActualSingleCannonCostWhenBatchIsUnaffordable()
        {
            var w=new World(Catalog.Load(),42,"coast","coast",5);var p=w.Players[0];p.age=3;p.resources=new Cost(0,100,250);
            var foundry=w.Spawn("foundry",0,new Vector2(38,37));new SkirmishAI(w,0).Tick(.1f);
            Assert.AreEqual(1,foundry.queue.Count);Assert.AreEqual("cannon",foundry.queue[0].item);Assert.AreEqual(1,foundry.queue[0].count);
            Assert.AreEqual(100,p.spent.wood);Assert.AreEqual(250,p.spent.coin);Assert.AreEqual(0,p.resources.coin);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Payment"&&e.entity==foundry.id));Assert.AreEqual(0,w.Entities.Count(e=>e.owner==0&&e.def.id=="cannon"),"AI must queue paid production, never spawn immediately");
        }
        [Test] public void CannonBudgetPreventsOtherMilitaryQueuesConsumingTheSaving()
        {
            var w=new World(Catalog.Load(),42,"coast","coast",5);var p=w.Players[0];p.age=3;p.resources=new Cost(500,80,200);
            var barracks=w.Spawn("barracks",0,new Vector2(38,37));var foundry=w.Spawn("foundry",0,new Vector2(45,37));new SkirmishAI(w,0).Tick(.1f);
            Assert.AreEqual(0,foundry.queue.Count);Assert.AreEqual(0,barracks.queue.Count);Assert.AreEqual(200,p.resources.coin);Assert.AreEqual(80,p.resources.wood);
        }
        [Test] public void HiddenEnemyIsNeverIssuedAsLiveAttackTarget()
        {
            var w=new World(Catalog.Load());var hidden=w.Spawn("musketeer",1,new Vector2(95,72));
            for(int i=0;i<8;i++)w.Spawn("musketeer",0,new Vector2(35+i*2,40));Assert.IsFalse(w.IsVisible(0,hidden));
            new SkirmishAI(w,0).Tick(.1f);
            Assert.IsFalse(w.Events.Any(e=>e.kind=="Command"&&e.player==0&&e.target==hidden.id));
            Assert.IsFalse(w.Entities.Where(e=>e.owner==0).SelectMany(e=>e.orders).Any(o=>o.kind==OrderKind.Attack&&o.target==hidden.id));
        }
        [Test] public void RiverMilitaryCannotSpendTheNextSettlersCoinSaving()
        {
            var w=new World(Catalog.Load(),42,"river","coast",5);var p=w.Players[0];p.age=2;
            var barracks=w.Spawn("barracks",0,new Vector2(38,37));w.Spawn("bank",0,new Vector2(45,37));w.Spawn("market",0,new Vector2(45,45));
            p.resources=new Cost(500,0,75);var ai=new SkirmishAI(w,0);ai.Tick(.1f);
            Assert.AreEqual(75,p.resources.coin,"Saving below 100C must survive an affordable 25C musketeer request");Assert.IsEmpty(barracks.queue);
            p.resources.coin=100;ai.Tick(2);
            var town=w.Entities.First(e=>e.owner==0&&e.def.id=="town");Assert.AreEqual("villager",town.queue[0].item);Assert.AreEqual(0,p.resources.coin);
        }
        [Test] public void VisibleBowRaidPrioritizesCavalryOverAdditionalSettlersAndBanks()
        {
            var w=new World(Catalog.Load(),42,"river","coast",5);var p=w.Players[0];p.age=2;
            for(int i=0;i<10;i++)w.Spawn("villager",0,new Vector2(30+i,25));
            w.Spawn("barracks",0,new Vector2(38,37));var stable=w.Spawn("stable",0,new Vector2(45,37));w.Spawn("market",0,new Vector2(45,45));
            var enemy=w.Spawn("longbow",1,new Vector2(44,27));w.Fog.Reveal(0,enemy.position,8);p.resources=new Cost(200,0,100);
            new SkirmishAI(w,0).Tick(.1f);
            Assert.AreEqual("hussar",stable.queue[0].item);Assert.AreEqual(20,p.resources.coin);
            Assert.IsEmpty(w.Entities.First(e=>e.owner==0&&e.def.id=="town").queue,"Enough surviving workers: defend before recruiting more");
            Assert.IsFalse(w.Entities.Any(e=>e.owner==0&&e.def.id=="bank"));
        }
    }
}
