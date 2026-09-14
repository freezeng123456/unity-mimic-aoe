using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class EconomyEvidenceTests
    {
        private World world;
        private EntityState town, worker, barracks;
        private PlayerState Player => world.Players[0];
        // Independent wire DTOs let these assertions compile against the old event schema.
        [Serializable] private class EvidenceEvent
        { public int schemaVersion, entity, target; public bool hasEconomy, hasPopulation; public Economy economy; public Population population; }
        [Serializable] private class Economy
        {
            public Cost requestedDelta, actualDelta, balanceBefore, balanceAfter;
            public bool hasRepair; public int healthTarget; public float hpBefore, hpAfter;
        }
        [Serializable] private class Population { public int used, reserved, capacity; }

        [SetUp] public void SetUp()
        {
            world = new World(Catalog.Load(), 935, "coast", "coast", 3);
            town = world.Entities.First(e => e.owner == 0 && e.def.id == "town");
            barracks = world.Spawn("barracks", 0, new Vector2(45, 40));
            worker = WorkerAtEdge(Vector2.right);
            Player.age = 2;
            Player.resources = new Cost(1000, 1000, 1000);
            world.Fog.Reveal(0, barracks.position, 12);
        }
        private EntityState WorkerAtEdge(Vector2 direction) => world.Spawn("villager", 0,
            barracks.position + direction * (barracks.def.width * .5f + world.Rules.Entity("villager").radius + .75f));
        private EvidenceEvent Read(SimEvent e) => JsonUtility.FromJson<EvidenceEvent>(JsonUtility.ToJson(e));
        private SimEvent[] Payments => world.Events.Where(e => e.kind == "Payment" && e.player == 0).ToArray();
        private SimEvent[] Refunds => world.Events.Where(e => e.kind == "Refund" && e.player == 0).ToArray();
        private void Steps(int n) { for (int i = 0; i < n; i++) world.Tick(.1f); }
        private void OrderRepair(EntityState actor)
        {
            Assert.Greater(world.EdgeDistance(actor, barracks), 0, "Worker must be outside the footprint");
            Assert.LessOrEqual(world.EdgeDistance(actor, barracks), 1.5f);
            var result = world.Issue(new GameCommand { player = 0, kind = CommandKind.Order,
                actors = new[] { actor.id }, order = OrderKind.Repair, target = barracks.id });
            Assert.IsTrue(result.accepted, result.reason);
        }
        private void AssertBalance(EvidenceEvent e)
        {
            Assert.AreEqual(2, e.schemaVersion);
            Assert.True(e.hasEconomy);
            Assert.NotNull(e.economy, "Financial evidence must survive JSON serialization");
            var t = e.economy;
            Assert.AreEqual(t.balanceAfter.food - t.balanceBefore.food, t.actualDelta.food, .0001f);
            Assert.AreEqual(t.balanceAfter.wood - t.balanceBefore.wood, t.actualDelta.wood, .0001f);
            Assert.AreEqual(t.balanceAfter.coin - t.balanceBefore.coin, t.actualDelta.coin, .0001f);
        }
        private void Train(EntityState producer, string unit, int count = 1)
        {
            var result = world.Issue(new GameCommand { player = 0, kind = CommandKind.Train,
                actors = new[] { producer.id }, item = unit, count = count });
            Assert.IsTrue(result.accepted, result.reason);
        }

        [Test] public void TinyFinalRepairRecordsRealWoodDebitAndHealthChange()
        {
            barracks.hp = world.MaxHp(barracks) - .05f;
            float beforeHp = barracks.hp; var beforeBalance = Player.resources;
            OrderRepair(worker); Steps(1);
            Assert.AreEqual(1, Payments.Length);
            var e = Read(Payments.Single()); AssertBalance(e);
            Assert.True(e.economy.hasRepair); Assert.AreEqual(worker.id, e.entity);
            Assert.AreEqual(barracks.id, e.target); Assert.AreEqual(barracks.id, e.economy.healthTarget);
            Assert.AreEqual(beforeHp, e.economy.hpBefore); Assert.AreEqual(world.MaxHp(barracks), e.economy.hpAfter);
            Assert.AreEqual(beforeBalance.wood, e.economy.balanceBefore.wood);
            Assert.AreEqual(Player.resources.wood, e.economy.balanceAfter.wood);
            Assert.Less(e.economy.requestedDelta.wood, 0); Assert.Less(e.economy.actualDelta.wood, 0);
            Assert.AreEqual(0, e.economy.requestedDelta.food); Assert.AreEqual(0, e.economy.requestedDelta.coin);
            float expected = -world.EntityCost(0, barracks.def).wood * ((world.MaxHp(barracks) - beforeHp) / world.MaxHp(barracks) * .5f);
            Assert.AreEqual(expected, e.economy.requestedDelta.wood, .0000001f);
            Steps(3); Assert.AreEqual(1, Payments.Length, "Full health cannot trigger a further debit");
        }

        [Test] public void TwoWorkersRecordSequentialClampedFinalRepairsOnceEach()
        {
            barracks.hp = world.MaxHp(barracks) - 2;
            var second = WorkerAtEdge(Vector2.left);
            OrderRepair(worker); OrderRepair(second); Steps(2);
            Assert.AreEqual(2, Payments.Length, "Exactly two workers finish the missing 2 HP");
            var first = Read(Payments[0]); var last = Read(Payments[1]);
            AssertBalance(first); AssertBalance(last);
            Assert.AreEqual(worker.id, first.entity); Assert.AreEqual(second.id, last.entity);
            Assert.True(first.economy.hasRepair && last.economy.hasRepair);
            Assert.AreEqual(barracks.id, first.target); Assert.AreEqual(barracks.id, last.target);
            Assert.AreEqual(1.8f, first.economy.hpAfter - first.economy.hpBefore, .0002f);
            Assert.AreEqual(.2f, last.economy.hpAfter - last.economy.hpBefore, .0002f);
            Assert.AreEqual(first.economy.hpAfter, last.economy.hpBefore);
            Assert.AreEqual(first.economy.balanceAfter.wood, last.economy.balanceBefore.wood);
            Assert.AreEqual(world.MaxHp(barracks), last.economy.hpAfter);
        }

        [Test] public void FullHealthRepairEmitsNoPayment()
        { OrderRepair(worker); var before = Player.resources; Steps(2); Assert.IsEmpty(Payments); Assert.AreEqual(before, Player.resources); }

        [Test] public void PausingAnAcceptedRepairFreezesTimeHealthAndBalance()
        {
            barracks.hp -= 1; OrderRepair(worker); world.Paused = true;
            var before = Player.resources; var hp = barracks.hp; var time = world.Time;
            Steps(3); Assert.AreEqual(time, world.Time); Assert.AreEqual(hp, barracks.hp);
            Assert.AreEqual(before, Player.resources); Assert.IsEmpty(Payments);
        }

        [Test] public void UnaffordableRepairDoesNotHealOrDebit()
        {
            barracks.hp -= 20; Player.resources = new Cost(); var hp = barracks.hp;
            OrderRepair(worker); Steps(2); Assert.IsEmpty(Payments); Assert.AreEqual(hp, barracks.hp);
            Assert.AreEqual(new Cost(), Player.resources); Assert.AreEqual("Repair needs resources", worker.status);
        }

        [Test] public void TrainingAndCancelKeepPaymentReservationAndRefundBoundaries()
        {
            var opening = Player.resources; Train(town, "villager");
            var paid = Read(Payments.Single()); AssertBalance(paid);
            Assert.AreEqual(-100, paid.economy.requestedDelta.food);
            Assert.False(paid.economy.hasRepair); Assert.True(paid.hasPopulation); Assert.NotNull(paid.population);
            Assert.AreEqual(0, paid.population.reserved, "Payment precedes the reservation");
            var queued = world.Events.SingleOrDefault(e => e.kind == "ProductionQueued" && e.entity == town.id);
            Assert.NotNull(queued, "The successful queue boundary needs its own snapshot");
            var queuedEvidence = Read(queued); Assert.True(queuedEvidence.hasPopulation); Assert.NotNull(queuedEvidence.population);
            Assert.AreEqual(1, queuedEvidence.population.reserved);
            var cancel = new GameCommand { player = 0, kind = CommandKind.CancelQueue, actors = new[] { town.id }, index = 0 };
            Assert.True(world.Issue(cancel).accepted); var refund = Read(Refunds.Single()); AssertBalance(refund);
            Assert.AreEqual(100, refund.economy.requestedDelta.food); Assert.AreEqual(0, refund.population.reserved);
            Assert.AreEqual(opening, Player.resources); Assert.False(world.Issue(cancel).accepted);
            Assert.AreEqual(1, Refunds.Length); Assert.AreEqual(opening, Player.resources);
            Assert.AreEqual(1, queuedEvidence.population.reserved, "Snapshots must not reference mutable live state");
        }

        [Test] public void RejectedTrainingEmitsNeitherPaymentNorQueue()
        {
            Player.resources = new Cost();
            Assert.False(world.Issue(new GameCommand { player = 0, kind = CommandKind.Train, actors = new[] { town.id }, item = "villager" }).accepted);
            Assert.IsEmpty(Payments); Assert.IsEmpty(town.queue); Assert.AreEqual(0, Player.reservedPopulation);
            Assert.False(world.Events.Any(e => e.kind == "ProductionQueued"));
        }

        [Test] public void DeathEvidenceRecordsReleasedReservationWithoutRefund()
        {
            Train(barracks, "musketeer", 5); Assert.AreEqual(5, Player.reservedPopulation);
            var balance = Player.resources; var cap = Player.populationCap; var used = Player.usedPopulation;
            world.ApplyDamage(barracks, 100000, DamageKind.Siege, 1);
            var e = Read(world.Events.Last(x => x.kind == "Death" && x.entity == barracks.id));
            Assert.AreEqual(2, e.schemaVersion); Assert.True(e.hasPopulation); Assert.NotNull(e.population);
            Assert.AreEqual(0, e.population.reserved); Assert.AreEqual(used, e.population.used);
            Assert.AreEqual(cap, e.population.capacity); Assert.IsEmpty(barracks.queue);
            Assert.AreEqual(balance, Player.resources); Assert.IsEmpty(Refunds);
        }

        [Test] public void HousingDeathSnapshotUsesReducedCapacity()
        {
            var cap = Player.populationCap; world.ApplyDamage(town, 100000, DamageKind.Siege, 1);
            var e = Read(world.Events.Last(x => x.kind == "Death" && x.entity == town.id));
            Assert.AreEqual(2, e.schemaVersion); Assert.True(e.hasPopulation); Assert.NotNull(e.population); Assert.Less(e.population.capacity, cap);
            Assert.AreEqual(Player.populationCap, e.population.capacity);
        }
    }
}
