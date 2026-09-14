using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public class WorkerRecoveryTests
    {
        World world;
        EntityState worker, town, enemy, food;

        [SetUp] public void SetUp()
        {
            var rules = JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            world = new World(rules);
            town = world.Entities.First(e => e.owner == 0 && e.def.id == "town");
            worker = world.Entities.First(e => e.owner == 0 && e.Worker);
            food = world.Entities.First(e => e.owner < 0 && e.def.resource == ResourceKind.Food);
            world.Fog.Reveal(0, town.position, 20);
            world.Fog.Reveal(0, food.position, 20);
        }

        void Steps(int count) { for (int i = 0; i < count; i++) world.Tick(.1f); }

        [Test] public void SafeUngarrisonEventuallyResumesGatherIncome()
        {
            Assert.IsTrue(world.Issue(new GameCommand { player = 0, kind = CommandKind.Order, actors = new[] { worker.id }, order = OrderKind.Garrison, target = town.id, point = town.position }).accepted);
            Steps(30);
            Assert.AreEqual(town.id, worker.garrisonedIn);
            float beforeGathered = world.Players[0].gathered.food;
            int beforeEvents = world.Events.Count(e => e.kind == "Gather" && e.entity == worker.id);
            var ai = new SkirmishAI(world, 0);
            for (int i = 0; i < 300; i++) { ai.Tick(.1f); world.Tick(.1f); }
            world.FlushEconomyLog();
            Assert.Greater(world.Players[0].gathered.food, beforeGathered, "A released villager must produce real Gather income after AI reassigns it");
            Assert.Greater(world.Events.Count(e => e.kind == "Gather" && e.entity == worker.id), beforeEvents);
        }

        [Test] public void AIDoesNotUngarrisonWhileVisibleEnemyRemainsNearby()
        {
            enemy = world.Spawn("musketeer", 1, new Vector2(30, 28));
            world.Fog.Reveal(0, enemy.position, 20);
            Assert.IsTrue(world.Issue(new GameCommand { player = 0, kind = CommandKind.Order, actors = new[] { worker.id }, order = OrderKind.Garrison, target = town.id, point = town.position }).accepted);
            Steps(30);
            Assert.AreEqual(town.id, worker.garrisonedIn);
            var ai = new SkirmishAI(world, 0);
            ai.Tick(.1f);
            Assert.AreEqual(town.id, worker.garrisonedIn, "AI must retain shelter while a visible enemy remains in the home threat radius");
            Assert.IsFalse(world.Events.Any(e => e.kind == "Command" && e.message.StartsWith("Ungarrison")));
        }

        [Test] public void DestroyedTownReleasesWorkerAndUpdatesPopulationAndNavigation()
        {
            Assert.IsTrue(world.Issue(new GameCommand { player = 0, kind = CommandKind.Order, actors = new[] { worker.id }, order = OrderKind.Garrison, target = town.id, point = town.position }).accepted);
            Steps(30);
            Assert.AreEqual(town.id, worker.garrisonedIn);
            int cap = world.Players[0].populationCap;
            Assert.IsFalse(world.Navigation.IsWalkable(town.position, .4f));
            Assert.IsTrue(world.Issue(new GameCommand { player = 0, kind = CommandKind.Demolish, actors = new[] { town.id } }).accepted);
            Assert.IsTrue(worker.Alive);
            Assert.AreEqual(0, worker.garrisonedIn);
            Assert.AreEqual(cap - town.def.capacity, world.Players[0].populationCap);
            Assert.IsTrue(world.Navigation.IsWalkable(town.position, .4f));
            Assert.AreEqual(1, world.Events.Count(e => e.kind == "Death" && e.entity == town.id));
        }
    }
}
