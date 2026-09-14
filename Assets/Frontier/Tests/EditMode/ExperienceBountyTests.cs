using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ExperienceBountyTests
    {
        World world;
        PlayerState Player => world.Players[0];

        [SetUp] public void SetUp()
        {
            var rules = JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            world = new World(rules);
            foreach (var guardian in world.Entities.Where(World.Guardian).ToArray()) world.Remove(guardian);
        }

        void Steps(int count) { for (int i = 0; i < count; i++) world.Tick(.1f); }

        [Test] public void CompletedPaidUnitTrainingProducesPositiveExperience()
        {
            var town = world.Entities.First(e => e.owner == 0 && e.def.id == "town");
            Assert.IsTrue(world.Issue(new GameCommand { player = 0, kind = CommandKind.Train, actors = new[] { town.id }, item = "villager" }).accepted);
            Assert.AreEqual(0, Player.xp, "Paying for a queue entry does not grant experience");
            Steps(210);
            Assert.AreEqual(7, world.Entities.Count(e => e.owner == 0 && e.Worker && e.Alive));
            Assert.Greater(Player.xp, 0, "A completed paid unit must contribute to shipment progress");
            Assert.AreEqual(1, world.Events.Count(e => e.kind == "Experience" && e.player == 0 && e.message == "Unit trained"));
        }

        [Test] public void ActualEnemyUnitKillCanEarnOneShipmentWithoutRepeatedDeathCredit()
        {
            var attacker = world.Spawn("musketeer", 0, new Vector2(80, 24));
            var target = world.Spawn("pike", 1, new Vector2(83, 24));
            target.hp = 1;
            Player.xp = world.Rules.initialXp - 1;
            world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(new GameCommand { player = 0, kind = CommandKind.Order, actors = new[] { attacker.id }, order = OrderKind.Attack, target = target.id }).accepted);
            Steps(40);
            Assert.IsFalse(target.Alive);
            Assert.IsTrue(world.Events.Any(e => e.kind == "Damage" && e.entity == attacker.id && e.target == target.id));
            Assert.AreEqual(1, Player.shipmentTokens, "Defeating an ordinary enemy unit must award its positive bounty");
            var balance = Player.xp;
            world.Remove(target, 0);
            Steps(20);
            Assert.AreEqual(balance, Player.xp);
            Assert.AreEqual(1, world.Events.Count(e => e.kind == "Experience" && e.player == 0 && e.entity == target.id && e.message == "Combat bounty"));
        }

        [Test] public void BlockedBatchAwardsPerUnitOnlyAfterTheWholeBatchCanExit()
        {
            Player.age = 2;
            var barracks = world.Spawn("barracks", 0, new Vector2(80, 24));
            var definition = world.Rules.Entity("pike");
            Assert.IsTrue(world.Issue(new GameCommand { player = 0, kind = CommandKind.Train, actors = new[] { barracks.id }, item = "pike", count = 5 }).accepted);
            world.Navigation.SetBlocked(barracks.position, 15, true);
            Steps(250);
            Assert.AreEqual(1, barracks.queue.Count);
            Assert.AreEqual(0, Player.xp);
            Assert.AreEqual(0, Player.trained);
            world.Navigation.SetBlocked(barracks.position, 15, false);
            Steps(1);
            Assert.AreEqual(5, Player.trained);
            Assert.AreEqual(5 * definition.trainXp, Player.xp);
            Assert.AreEqual(5, world.Events.Count(e => e.kind == "Experience" && e.player == 0 && e.message == "Unit trained"));
            Steps(20);
            Assert.AreEqual(5 * definition.trainXp, Player.xp);
        }

        [Test] public void CancelledTrainingAndDestroyedProducerNeverAwardCompletionExperience()
        {
            var town = world.Entities.First(e => e.owner == 0 && e.def.id == "town");
            var train = new GameCommand { player = 0, kind = CommandKind.Train, actors = new[] { town.id }, item = "villager" };
            Assert.IsTrue(world.Issue(train).accepted);
            Steps(50);
            Assert.IsTrue(world.Issue(new GameCommand { player = 0, kind = CommandKind.CancelQueue, actors = new[] { town.id } }).accepted);
            Steps(210);
            Assert.AreEqual(0, Player.xp);
            Assert.IsTrue(world.Issue(train).accepted);
            world.Remove(town, 1);
            Steps(210);
            Assert.AreEqual(0, Player.xp);
            Assert.AreEqual(0, Player.trained);
            Assert.AreEqual(0, world.Events.Count(e => e.kind == "Experience" && e.player == 0 && e.message == "Unit trained"));
        }

        [Test] public void BuildingCompletionGiftAndEnemyKillUseSeparateBounties()
        {
            var definition = world.Rules.Entity("house");
            definition.buildXp = 17; definition.killXp = 29; definition.time = 1;
            var worker = world.Spawn("villager", 0, new Vector2(40, 40));
            var point = new Vector2(42, 40);
            world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(new GameCommand { player = 0, kind = CommandKind.Build, actors = new[] { worker.id }, item = "house", point = point }).accepted);
            var house = world.Entities.Last(e => e.owner == 0 && e.def.id == "house");
            Steps(100);
            Assert.IsTrue(house.Complete);
            Assert.AreEqual(8, world.Entities.Count(e => e.Alive && e.owner == 0 && e.Worker), "A completed Coast manor delivered one free worker");
            Assert.AreEqual(17, Player.xp, "The free manor worker is not paid training");
            Assert.AreEqual(0, Player.trained);
            world.Remove(house, 1);
            Assert.AreEqual(29, world.Players[1].xp);
            world.Remove(house, 1);
            Assert.AreEqual(29, world.Players[1].xp);
        }

        [Test] public void ShipmentAndAgeRewardUnitsNeverReceivePaidTrainingExperience()
        {
            Assert.AreEqual(0, Player.xp, "Starting units do not count as production");
            Player.deck.Clear(); Player.deck.Add("settlers"); Player.shipmentTokens = 1;
            Assert.IsTrue(world.Issue(new GameCommand { player = 0, kind = CommandKind.Ship, item = "settlers" }).accepted);
            Player.rewards.Add(new RewardJob { unit = "pike", count = 2 });
            Steps(370);
            Assert.AreEqual(0, Player.shipments.Count);
            Assert.AreEqual(0, Player.rewards.Count);
            Assert.AreEqual(8, world.Entities.Count(e => e.Alive && e.owner == 0 && e.Worker));
            Assert.AreEqual(2, world.Entities.Count(e => e.Alive && e.owner == 0 && e.def.id == "pike"));
            Assert.AreEqual(0, Player.xp);
            Assert.AreEqual(0, Player.trained);
        }

        [TestCase(0)] [TestCase(1)]
        public void BothPlayersReceiveVictimKillBountyRatherThanItsTrainingValue(int killer)
        {
            var definition = world.Rules.Entity("hussar");
            definition.trainXp = 3; definition.killXp = 19;
            var target = world.Spawn("hussar", 1 - killer, new Vector2(80, 24));
            world.Remove(target, killer);
            Assert.AreEqual(19, world.Players[killer].xp);
            Assert.AreEqual(0, world.Players[1 - killer].xp);
        }

        [TestCase("economic")] [TestCase("military")]
        public void RealAgeAdvancementAwardsItsOwnExperienceWithoutTrainingCreditForRewards(string choice)
        {
            var town = world.Entities.First(e => e.owner == 0 && e.def.id == "town");
            var age = world.Rules.Age(2);
            Player.resources = age.cost;
            int cratesBefore = world.Entities.Count(e => e.owner == 0 && e.def.id == "crate_food");
            Assert.IsTrue(world.Issue(new GameCommand { player = 0, kind = CommandKind.AdvanceAge, actors = new[] { town.id }, choice = choice }).accepted);
            Assert.AreEqual(0, Player.xp);
            Steps(Mathf.CeilToInt(age.time * 10) + 10);
            Assert.AreEqual(2, Player.age);
            Assert.AreEqual(0, Player.rewards.Count);
            Assert.AreEqual(100, Player.xp, "The age event grants XP, its delivered reward is not training");
            Assert.AreEqual(0, Player.trained);
            Assert.AreEqual(0, world.Events.Count(e => e.kind == "Experience" && e.player == 0 && e.message == "Unit trained"));
            Assert.AreEqual(1, world.Events.Count(e => e.kind == "AgeRewardArrived" && e.player == 0));
            if (choice == "military")
                Assert.AreEqual(age.militaryCount, world.Entities.Count(e => e.Alive && e.owner == 0 && e.def.id == age.militaryUnit));
            else
                Assert.AreEqual(cratesBefore + 1, world.Entities.Count(e => e.Alive && e.owner == 0 && e.def.id == "crate_food"));
        }

        [Test] public void FriendlyRemovalAndNaturalResourceRemovalDoNotGrantBounties()
        {
            var own = world.Spawn("pike", 0, new Vector2(80, 24));
            var resource = world.Spawn("deer", -1, new Vector2(84, 24));
            resource.def.killXp = 99;
            world.Remove(own, 0);
            world.Remove(resource, 0);
            Assert.AreEqual(0, Player.xp);
            Assert.AreEqual(0, Player.kills);
        }
    }
}
