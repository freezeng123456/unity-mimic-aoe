using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class DepletedRallyTests
    {
        World world; EntityState town;
        [SetUp] public void SetUp()
        {
            world=new World(Catalog.Load(),4601,"coast","coast",3);
            world.Players[0].resources=new Cost(5000,5000,5000);
            town=world.Spawn("town",0,new Vector2(76,25));
            world.Fog.Tick(world.Entities);
        }
        EntityState FoodBox()
        {
            var box=world.Spawn("crate_food",0,new Vector2(82,28));box.remaining=1.2f;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{town.id},target=box.id,point=box.position}).accepted);
            return box;
        }
        int BeginPaidTraining()
        {
            int previous=world.Entities.Max(e=>e.id);float food=world.Players[0].resources.food;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item="villager",count=1}).accepted);
            Assert.AreEqual(100,food-world.Players[0].resources.food,.001f,"The fixture must pay for a real training job");
            return previous;
        }
        EntityState AwaitBirth(int previous)
        {
            for(int i=0;i<260&&!world.Entities.Any(e=>e.owner==0&&e.Worker&&e.id>previous);i++)world.Tick(.1f);
            var born=world.Entities.Where(e=>e.owner==0&&e.Worker&&e.id>previous).ToArray();
            Assert.AreEqual(1,born.Length,"Exactly one new paid villager must finish training");
            Assert.IsNotEmpty(born[0].orders);return born[0];
        }
        EntityState DepleteDuringTraining(EntityState box)
        {
            var collector=world.Spawn("villager",0,new Vector2(83,28));
            int previous=BeginPaidTraining();
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{collector.id},order=OrderKind.Gather,target=box.id}).accepted);
            var born=AwaitBirth(previous);
            Assert.IsFalse(box.Alive,"The original box must really be exhausted before birth");
            var depletion=world.Events.Single(e=>e.kind=="ResourceDepleted"&&e.entity==box.id);
            var birth=world.Events.Single(e=>e.kind=="Spawn"&&e.entity==born.id);
            Assert.Less(depletion.time,birth.time);
            Assert.IsTrue(world.Events.Any(e=>e.kind=="Gather"&&e.entity==collector.id&&e.target==box.id&&e.amount>0));
            return born;
        }

        [Test] public void FoodBoxExhaustedDuringPaidTrainingAssignsNearestFoodAndActuallyGathers()
        {
            var box=FoodBox();var berry=world.Spawn("berry",-1,new Vector2(85,30));
            world.Spawn("berry",-1,new Vector2(83,39));
            var born=DepleteDuringTraining(box);
            Assert.AreEqual(OrderKind.Gather,born.orders.Peek().kind);Assert.AreEqual(berry.id,born.orders.Peek().target);
            for(int i=0;i<250;i++)world.Tick(.1f);world.FlushEconomyLog();
            Assert.IsTrue(world.Events.Any(e=>e.kind=="Gather"&&e.entity==born.id&&e.target==berry.id&&e.amount>0),"The new villager must reach the replacement and credit real food");
            Assert.AreEqual(box.id,town.rallyTarget,"Replacement must preserve the player's original resource area");
        }

        [Test] public void ReplacementSkipsNearerWoodEnemyFarmAndUnfinishedFarm()
        {
            var box=FoodBox();var wood=world.Spawn("tree",-1,new Vector2(82,30));
            var enemy=world.Spawn("farm",1,new Vector2(89,28));
            var unfinished=world.Spawn("farm",0,new Vector2(76,35),false);
            var berry=world.Spawn("berry",-1,new Vector2(83,40));
            Assert.IsTrue(world.IsVisible(0,enemy));Assert.IsFalse(world.GatherableResource(0,enemy));
            Assert.IsFalse(world.GatherableResource(0,unfinished));Assert.IsTrue(world.GatherableResource(0,berry));
            var born=DepleteDuringTraining(box);
            Assert.AreEqual(OrderKind.Gather,born.orders.Peek().kind);Assert.AreEqual(berry.id,born.orders.Peek().target);
            Assert.IsFalse(new[]{wood.id,enemy.id,unfinished.id}.Contains(born.orders.Peek().target));
        }

        [Test] public void TerrainRallyClearsResourceIntentEvenWithVisibleReplacement()
        {
            var box=FoodBox();world.Spawn("berry",-1,new Vector2(85,30));world.Remove(box);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{town.id},target=0,point=new Vector2(82,28)}).accepted);
            var born=AwaitBirth(BeginPaidTraining());
            Assert.AreEqual(OrderKind.Move,born.orders.Peek().kind);Assert.AreEqual(0,born.orders.Peek().target);
            Assert.IsFalse(world.Events.Any(e=>e.kind=="RallyAssigned"&&e.entity==born.id&&e.target!=0));
        }

        [Test] public void DepletedResourceWithoutEligibleReplacementKeepsRememberedMove()
        {
            var box=FoodBox();var remembered=town.rally;
            var born=DepleteDuringTraining(box);
            Assert.AreEqual(OrderKind.Move,born.orders.Peek().kind);Assert.AreEqual(0,born.orders.Peek().target);
            Assert.Less(Vector2.Distance(born.orders.Peek().point,remembered),6);
            Assert.IsTrue(world.Events.Any(e=>e.kind=="RallyFallback"&&e.entity==town.id));
        }
    }
}
