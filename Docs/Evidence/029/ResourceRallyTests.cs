using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Contract tests for an explicit resource-aware production rally. They use
    // public GameCommand fields only so the suite also catches an implementation
    // that silently collapses an entity target into a location.
    public sealed class ResourceRallyTests
    {
        private World world;
        private EntityState fixtureTown;
        private PlayerState Player=>world.Players[0];

        [SetUp] public void SetUp()
        {
            world=new World(Catalog.Load(),314159,"coast","coast",3);
            Player.resources=new Cost(5000,5000,5000);
            fixtureTown=world.Spawn("town",0,new Vector2(76,25));
            world.Fog.Reveal(0,new Vector2(76,25),30);
        }

        private EntityState Resource(Vector2? at=null)
        {
            Vector2 point=at??new Vector2(76,30);var resource=world.Spawn("berry",-1,point);world.Fog.Reveal(0,point,30);return resource;
        }
        private EntityState Town()=>fixtureTown;
        private EntityState TrainVillager(EntityState producer)
        {
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{producer.id},item="villager",count=1}).accepted);
            int previous=world.Entities.Where(e=>e.owner==0&&e.Worker).Select(e=>e.id).DefaultIfEmpty(0).Max();
            for(int i=0;i<250&&!world.Entities.Any(e=>e.owner==0&&e.Worker&&e.id>previous);i++)world.Tick(.1f);
            return world.Entities.Where(e=>e.owner==0&&e.Worker&&e.id>previous).OrderByDescending(e=>e.id).FirstOrDefault();
        }

        [Test] public void ExplicitResourceRallyAssignsExactTargetAndCreditsGathering()
        {
            var town=Town();var resource=Resource();
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{town.id},target=resource.id,point=resource.position}).accepted);
            var villager=TrainVillager(town);
            Assert.IsNotNull(villager);Assert.IsTrue(villager.orders.Count>0);
            Assert.AreEqual(OrderKind.Gather,villager.orders.Peek().kind);Assert.AreEqual(resource.id,villager.orders.Peek().target);
            for(int i=0;i<200;i++)world.Tick(.1f);
            Assert.IsTrue(world.Events.Any(e=>e.kind=="Gather"&&e.entity==villager.id&&e.target==resource.id),"A resource rally must lead to real resource credit");
        }

        [Test] public void RallyChangedDuringPaidTrainingUsesLatestProducerIntent()
        {
            var town=Town();var first=Resource(new Vector2(76,30));var second=Resource(new Vector2(80,30));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{town.id},target=first.id,point=first.position}).accepted);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item="villager",count=1}).accepted);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{town.id},target=second.id,point=second.position}).accepted);
            int previous=world.Entities.Where(e=>e.owner==0&&e.Worker).Max(e=>e.id);for(int i=0;i<250&&!world.Entities.Any(e=>e.owner==0&&e.Worker&&e.id>previous);i++)world.Tick(.1f);
            var villager=world.Entities.FirstOrDefault(e=>e.owner==0&&e.Worker&&e.id>previous);
            Assert.IsNotNull(villager);Assert.AreEqual(OrderKind.Gather,villager.orders.Peek().kind);Assert.AreEqual(second.id,villager.orders.Peek().target);
        }

        [Test] public void TerrainRallyClearsPreviousResourceIntent()
        {
            var town=Town();var resource=Resource();var terrain=new Vector2(82,34);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{town.id},target=resource.id,point=resource.position}).accepted);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{town.id},target=0,point=terrain}).accepted);
            var villager=TrainVillager(town);Assert.IsNotNull(villager);Assert.AreEqual(OrderKind.Move,villager.orders.Peek().kind);Assert.AreEqual(0,villager.orders.Peek().target);
        }

        [Test] public void RemovedResourceRallyFallsBackWithoutHiddenGatherTarget()
        {
            var town=Town();var resource=Resource();
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{town.id},target=resource.id,point=new Vector2(84,34)}).accepted);world.Remove(resource);
            var villager=TrainVillager(town);Assert.IsNotNull(villager);Assert.IsFalse(villager.orders.Any(o=>o.kind==OrderKind.Gather&&o.target==resource.id));
            Assert.IsTrue(world.Events.Any(e=>e.kind=="RallyFallback"&&e.entity==town.id));Assert.IsTrue(villager.orders.Count==0||villager.orders.Peek().kind==OrderKind.Move);
        }

        [Test] public void MilitaryProductionAtResourceRallyReceivesDistributedMove()
        {
            Player.age=2;var barracks=world.Spawn("barracks",0,new Vector2(88,25));var resource=Resource(new Vector2(88,32));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{barracks.id},target=resource.id,point=resource.position}).accepted);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{barracks.id},item="musketeer",count=5}).accepted);
            for(int i=0;i<300&&!world.Entities.Any(e=>e.owner==0&&e.def.id=="musketeer");i++)world.Tick(.1f);
            var units=world.Entities.Where(e=>e.owner==0&&e.def.id=="musketeer").ToArray();Assert.AreEqual(5,units.Length);
            Assert.IsTrue(units.All(u=>u.orders.Count>0&&u.orders.Peek().kind==OrderKind.Move&&u.orders.Peek().target==0));Assert.AreEqual(5,units.Select(u=>u.orders.Peek().point).Distinct().Count());
        }

        [Test] public void InvalidEnemyTargetRejectsAndPreservesPreviousResourceRally()
        {
            var town=Town();var resource=Resource();var enemy=world.Spawn("farm",1,new Vector2(90,30));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{town.id},target=resource.id,point=resource.position}).accepted);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{town.id},target=enemy.id,point=enemy.position}).accepted);
            var villager=TrainVillager(town);Assert.IsNotNull(villager);Assert.AreEqual(OrderKind.Gather,villager.orders.Peek().kind);Assert.AreEqual(resource.id,villager.orders.Peek().target);
        }
        [Test] public void HiddenResourceCommandRejectsWithoutChangingExistingRally()
        {
            var resource=Resource();var hidden=world.Spawn("berry",-1,new Vector2(120,20));world.Fog.Tick(world.Entities);
            Assert.IsFalse(world.IsVisible(0,hidden),"Fixture requires a hidden target");
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{fixtureTown.id},target=resource.id,point=resource.position}).accepted);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{fixtureTown.id},target=hidden.id,point=hidden.position}).accepted);
            var villager=TrainVillager(fixtureTown);Assert.IsNotNull(villager);Assert.AreEqual(resource.id,villager.orders.Peek().target);
        }

        [Test] public void ResourceHiddenBeforeBirthFallsBackToRememberedPosition()
        {
            var remembered=new Vector2(76,65);var resource=Resource(remembered);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{fixtureTown.id},target=resource.id,point=remembered}).accepted);
            // Mutating this diagnostic target after observation tests that the
            // resolver cannot derive a new destination from hidden live state.
            resource.position=new Vector2(116,75);world.Fog.Tick(world.Entities);Assert.IsFalse(world.IsVisible(0,resource));
            var villager=TrainVillager(fixtureTown);Assert.IsNotNull(villager);
            Assert.AreEqual(OrderKind.Move,villager.orders.Peek().kind);Assert.AreEqual(0,villager.orders.Peek().target);
            Assert.Less(Vector2.Distance(villager.orders.Peek().point,remembered),6);
            Assert.IsTrue(world.Events.Any(e=>e.kind=="RallyFallback"&&e.entity==fixtureTown.id));
        }
    }
}
