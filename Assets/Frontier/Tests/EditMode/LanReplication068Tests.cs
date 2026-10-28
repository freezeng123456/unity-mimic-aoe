using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class LanReplication068Tests
    {
        private static World Prepared(string map = "winding", bool teams = true)
        {
            var world = teams
                ? new World(Catalog.Load(), 314159, new[] { "coast", "river", "france", "ottoman" }, new[] { 0, 1, 0, 1 }, 5, map)
                : new World(Catalog.Load(), 314159, "coast", "coast", 5, map);
            world.Players[0].age = 3;
            world.Players[0].resources = new Cost(10000, 10000, 10000);
            world.Players[0].populationCap = 200;
            return world;
        }

        private static LanFullState Full(World world, LanQueueTracker tracker, long revision, int step = 0)
            => LanProjection.Full(world, "replication-match", "replication-epoch", revision, step, tracker.Capture(world));
        private static string State(World world) => JsonUtility.ToJson(world.CaptureState(false));
        private static EntityState Town(World world) => world.Entities.First(entity => entity.owner == 0 && entity.def.id == "town");
        private static EntityState Worker(World world) => world.Entities.First(entity => entity.owner == 0 && entity.Worker);
        private static void Issue(World world, GameCommand command)
        {
            var result = world.Issue(command);
            Assert.IsTrue(result.accepted, result.reason);
        }
        private static void Train(World world)
            => Issue(world, new GameCommand { player = 0, kind = CommandKind.Train, actors = new[] { Town(world).id }, item = "villager" });
        private static T ThroughWire<T>(string kind, T payload)
            => LanWire.Body<T>(LanWire.Decode(LanWire.Encode(kind, payload, "replication-match", "replication-epoch")));

        [Test]
        public void RealHostCommandsAndTicksRoundTripWithoutReplacingObservedObjectsOrRepeatingEffects()
        {
            var host = Prepared();
            var tracker = new LanQueueTracker();
            var first = Full(host, tracker, 1);
            var replica = new LanReplica(host.Rules);
            replica.Install(ThroughWire("full", first));
            var worldRef = replica.World;
            var playerRef = replica.World.Players[0];
            var townRef = replica.World.Get(Town(host).id);
            var workerRef = replica.World.Get(Worker(host).id);
            Assert.IsEmpty(replica.World.Events, "Initial history is silent");
            Train(host);
            var house = host.Spawn("house", 0, host.Navigation.NearestWalkable(Town(host).position + new Vector2(-12, 9), .4f));
            Issue(host, new GameCommand { player = 0, kind = CommandKind.Order, actors = new[] { Worker(host).id }, order = OrderKind.Move,
                point = host.Navigation.NearestWalkable(Worker(host).position + new Vector2(0, 8), .4f) });
            for (int step = 0; step < 5; step++) host.Tick(.1f);
            var next = Full(host, tracker, 2, 5);
            var delta = ThroughWire("delta", LanProjection.Delta(first, next, host.Events));
            Assert.IsTrue(replica.Apply(delta), replica.Error);
            Assert.AreEqual(State(host), State(replica.World), "The complete eventless world must match, including shared faction fog");
            Assert.AreSame(worldRef, replica.World);
            Assert.AreSame(playerRef, replica.World.Players[0]);
            Assert.AreSame(townRef, replica.World.Get(townRef.id));
            Assert.AreSame(workerRef, replica.World.Get(workerRef.id));
            Assert.IsNotNull(replica.World.Get(house.id));
            Assert.AreEqual(host.Players[0].spent.food, playerRef.spent.food);
            Assert.AreEqual(host.Players[0].xp, playerRef.xp);
            Assert.AreEqual(next.eventCursor - first.eventCursor, replica.World.Events.Count);
            Assert.AreEqual(host.Events.Skip((int)first.eventCursor).Count(e => e.kind == "Payment"), replica.World.Events.Count(e => e.kind == "Payment"));
            Assert.AreEqual(tracker.QueueId(host, townRef.id, 0), replica.Queues.QueueId(townRef.id, 0));
            string state = State(replica.World);
            string events = JsonUtility.ToJson(new Events { values = replica.World.Events.ToArray() });
            long cursor = replica.EventCursor;
            Assert.IsFalse(replica.Apply(delta));
            Assert.AreEqual(state, State(replica.World));
            Assert.AreEqual(events, JsonUtility.ToJson(new Events { values = replica.World.Events.ToArray() }));
            Assert.AreEqual(cursor, replica.EventCursor);
        }

        [Serializable] private sealed class Events { public SimEvent[] values; }

        [Test]
        public void InvalidBoundaryDigestMemoryQueueNavigationAndFogPreserveWorldAndCursor()
        {
            var host = Prepared();
            var tracker = new LanQueueTracker();
            var first = Full(host, tracker, 1);
            Train(host); Train(host);
            host.Tick(.1f);
            var next = Full(host, tracker, 2, 1);
            var valid = LanProjection.Delta(first, next, host.Events);
            foreach (string corruption in new[] { "base", "digest", "memory", "queues", "navigation", "fog" })
            {
                var replica = new LanReplica(host.Rules);
                replica.Install(first);
                var damaged = SnapshotCodec.Clone(valid);
                if (corruption == "base") damaged.baseRevision++;
                if (corruption == "digest") damaged.digest = new string('0', 64);
                if (corruption == "memory")
                {
                    var snapshot = SnapshotCodec.Clone(next.world);
                    snapshot.players[0].enemyMemory = new[]
                    {
                        new IntPointSnapshot { id = Town(host).id, point = Vector2.one },
                        new IntPointSnapshot { id = Town(host).id, point = Vector2.one }
                    };
                    var brokenFull = SnapshotCodec.Clone(next);
                    brokenFull.world = snapshot;
                    brokenFull.digest = LanProjection.Digest(snapshot, brokenFull.queues);
                    damaged = LanProjection.Delta(first, brokenFull, host.Events);
                }
                if (corruption == "queues")
                {
                    damaged.queues[0].jobs[1] = damaged.queues[0].jobs[0];
                    damaged.digest = LanProjection.Digest(next.world, damaged.queues);
                }
                if (corruption == "navigation")
                {
                    damaged.hasLandNavigation = true;
                    damaged.globals.landNavigation = SnapshotCodec.Clone(next.world.landNavigation);
                    damaged.globals.landNavigation.blocked = new bool[1];
                }
                if (corruption == "fog")
                {
                    damaged.hasVisibility = true;
                    damaged.globals.visibility = (int[])next.world.visibility.Clone();
                    damaged.globals.visibility[0] = 3;
                }
                string before = State(replica.World);
                var worldRef = replica.World;
                long queue = replica.Queues.QueueId(Town(host).id, 0);
                Assert.IsFalse(replica.Apply(damaged), corruption);
                Assert.IsTrue(replica.NeedsFullState, corruption);
                Assert.IsNotEmpty(replica.Error, corruption);
                Assert.AreSame(worldRef, replica.World, corruption);
                Assert.AreEqual(before, State(replica.World), corruption);
                Assert.AreEqual(first.eventCursor, replica.EventCursor, corruption);
                Assert.AreEqual(first.revision, replica.Revision, corruption);
                Assert.AreEqual(queue, replica.Queues.QueueId(Town(host).id, 0), corruption);
                Assert.IsEmpty(replica.World.Events, corruption);
            }
        }

        [Test]
        public void FullResynchronizationIsSilentPreservesObjectReferencesAndRejectsRollback()
        {
            var host = Prepared();
            var tracker = new LanQueueTracker();
            var first = Full(host, tracker, 1);
            var replica = new LanReplica(host.Rules);
            replica.Install(first);
            Train(host);
            host.Tick(.1f);
            var second = Full(host, tracker, 2, 1);
            Assert.IsTrue(replica.Apply(LanProjection.Delta(first, second, host.Events)), replica.Error);
            Assert.IsNotEmpty(replica.World.Events);
            var worldRef = replica.World;
            var townRef = replica.World.Get(Town(host).id);
            host.Tick(.1f);
            var third = Full(host, tracker, 3, 2);
            replica.Install(third);
            Assert.AreSame(worldRef, replica.World);
            Assert.AreSame(townRef, replica.World.Get(townRef.id));
            Assert.AreEqual(State(host), State(replica.World));
            Assert.IsEmpty(replica.World.Events);
            Assert.AreEqual(third.eventCursor, replica.EventCursor);
            string accepted = State(replica.World);
            Assert.Throws<ArgumentException>(() => replica.Install(first));
            Assert.AreEqual(accepted, State(replica.World));
            Assert.AreEqual(third.revision, replica.Revision);
            Assert.AreEqual(third.eventCursor, replica.EventCursor);
        }

        [Test]
        public void BaselineOwnsMutableOrdersAndJobsSeparatelyFromTheRenderedReplica()
        {
            var host = Prepared();
            var tracker = new LanQueueTracker();
            var first = Full(host, tracker, 1);
            var replica = new LanReplica(host.Rules);
            replica.Install(first);
            Train(host);
            Issue(host, new GameCommand { player = 0, kind = CommandKind.Order, actors = new[] { Worker(host).id }, order = OrderKind.Move,
                point = host.Navigation.NearestWalkable(Worker(host).position + new Vector2(0, 10), .4f) });
            var second = Full(host, tracker, 2);
            Assert.IsTrue(replica.Apply(LanProjection.Delta(first, second, host.Events)), replica.Error);
            var renderedWorker = replica.World.Get(Worker(host).id);
            Assert.IsNotEmpty(renderedWorker.orders);
            renderedWorker.orders.Peek().point += Vector2.one;
            replica.World.Get(Town(host).id).queue[0].elapsed = 7;
            host.Players[0].resources.coin += 1;
            var third = Full(host, tracker, 3);
            var delta = LanProjection.Delta(second, third, host.Events);
            Assert.IsFalse(delta.changed.Any(entity => entity.state.id == renderedWorker.id));
            Assert.IsFalse(delta.changed.Any(entity => entity.state.id == Town(host).id));
            Assert.IsTrue(replica.Apply(delta), "A local render-side mutation must not corrupt an unchanged host baseline: " + replica.Error);
            Assert.AreEqual(State(host), State(replica.World));
        }

        [Test]
        public void NavalCargoCombatFogObjectiveAndPaidShipQueueSurviveOneDelta()
        {
            var host = Prepared("river", false);
            var tracker = new LanQueueTracker();
            Assert.IsTrue(host.ConfigureMapObjective("defend_bridge").accepted);
            var first = Full(host, tracker, 1);
            var replica = new LanReplica(host.Rules);
            replica.Install(first);
            var ship = host.Spawn("transport_ship", 0, new Vector2(64, 24));
            Assert.IsTrue(host.TryTransportShore(ship, new Vector2(55, 24), .4f, out var shore, out _));
            var passenger = host.Spawn("villager", 0, host.Navigation.NearestWalkable(shore + new Vector2(-3, 0), .4f));
            Issue(host, new GameCommand { player = 0, kind = CommandKind.Embark, actors = new[] { passenger.id }, target = ship.id, point = shore });
            int steps = 0;
            for (; steps < 900 && passenger.garrisonedIn != ship.id; steps++) host.Tick(.1f);
            Assert.AreEqual(ship.id, passenger.garrisonedIn, "The host must board through its real navigation and transport rules");
            var dock = host.Spawn("dock", 0, new Vector2(55, 28));
            Issue(host, new GameCommand { player = 0, kind = CommandKind.Train, actors = new[] { dock.id }, item = "gunboat" });
            var gun = host.Spawn("gunboat", 0, new Vector2(64, 45));
            var enemy = host.Spawn("gunboat", 1, new Vector2(64, 51));
            host.Fog.Reveal(0, gun.position, 15);
            Issue(host, new GameCommand { player = 0, kind = CommandKind.Order, actors = new[] { gun.id }, order = OrderKind.Attack, target = enemy.id });
            for (int combat = 0; combat < 50 && host.Projectiles.Count == 0; combat++, steps++) host.Tick(.1f);
            Assert.IsNotEmpty(host.Projectiles, "A real combat projectile exercises the transient layer");
            var next = Full(host, tracker, 2, steps);
            var delta = ThroughWire("delta", LanProjection.Delta(first, next, host.Events));
            Assert.IsTrue(delta.hasLandNavigation && delta.hasWaterNavigation && delta.hasVisibility);
            Assert.IsTrue(replica.Apply(delta), replica.Error);
            Assert.AreEqual(State(host), State(replica.World));
            Assert.AreEqual(ship.id, replica.World.Get(passenger.id).garrisonedIn);
            CollectionAssert.AreEqual(ship.occupants, replica.World.Get(ship.id).occupants);
            Assert.AreEqual(host.Projectiles.Count, replica.World.Projectiles.Count);
            Assert.Greater(replica.World.MapObjective.elapsed, 0);
            Assert.AreEqual(tracker.QueueId(host, dock.id, 0), replica.Queues.QueueId(dock.id, 0));
        }

        [Test]
        public void PublishedPacketsCannotRetainAliasesIntoTheHostOrClientWorld()
        {
            var host = Prepared();
            var tracker = new LanQueueTracker();
            Train(host);
            var first = Full(host, tracker, 1);
            string frozen = JsonUtility.ToJson(first);
            var replica = new LanReplica(host.Rules);
            replica.Install(first);
            host.Tick(.1f);
            Assert.AreEqual(frozen, JsonUtility.ToJson(first));
            first.world.players[0].state.resources.food = -1;
            first.world.entities.First(entity => entity.state.id == Town(host).id).queue[0].elapsed = 99;
            first.queues[0].jobs[0] = 9999;
            Assert.GreaterOrEqual(replica.World.Players[0].resources.food, 0);
            Assert.AreEqual(0, replica.World.Get(Town(host).id).queue[0].elapsed);
            Assert.AreNotEqual(9999, replica.Queues.QueueId(Town(host).id, 0));
        }

        [Test]
        public void DeltaAndFullPublicationsOwnTheirQueueMetadataAndChangedEntities()
        {
            var host = Prepared();
            var tracker = new LanQueueTracker();
            Train(host);
            var externalQueues = tracker.Capture(host);
            var first = LanProjection.Full(host, "replication-match", "replication-epoch", 1, 0, externalQueues);
            string initial = JsonUtility.ToJson(first);
            externalQueues[0].jobs[0] = 9876;
            Assert.AreEqual(initial, JsonUtility.ToJson(first), "A full publication must own its supplied queue rows");
            host.Tick(.1f);
            var next = Full(host, tracker, 2, 1);
            string retained = JsonUtility.ToJson(next);
            var delta = LanProjection.Delta(first, next, host.Events);
            var changedTown = delta.changed.Single(entity => entity.state.id == Town(host).id);
            changedTown.state.hp -= 1;
            changedTown.queue[0].elapsed += 1;
            delta.queues[0].jobs[0] = 7654;
            Assert.AreEqual(retained, JsonUtility.ToJson(next), "Mutating an encoded delta must not corrupt the retained full baseline");
            delta = LanProjection.Delta(first, next, host.Events);
            string frozenDelta = JsonUtility.ToJson(delta);
            var retainedTown = next.world.entities.Single(entity => entity.state.id == Town(host).id);
            retainedTown.state.hp -= 1;
            retainedTown.queue[0].elapsed += 1;
            next.queues[0].jobs[0] = 5432;
            Assert.AreEqual(frozenDelta, JsonUtility.ToJson(delta), "A pending delta must not change when its source publication changes");
        }

        [Test]
        public void DeltaPreservesTheActualDeferredNavigationRecoveryFifo()
        {
            var host = Prepared("frontier", false);
            foreach (var entity in host.Entities.Where(entity => entity.def.id != "town").ToArray()) host.Remove(entity);
            host.Navigation.ConfigureTerrain(point => point.y == 54 || point.y == 59);
            var low = host.Spawn("pike", 0, new Vector2(48, 56.25f));
            var high = host.Spawn("pike", 0, new Vector2(46, 56.25f));
            var guards = new[] { host.Spawn("hussar", 0, new Vector2(55, 55.08f)), host.Spawn("hussar", 0, new Vector2(55, 57.42f)) };
            var enemy = host.Spawn("villager", 1, new Vector2(63, 56.9f));
            foreach (var actor in guards.Concat(new[] { low, high, enemy }))
                Issue(host, new GameCommand { player = actor.owner, kind = CommandKind.Order, order = OrderKind.Hold, actors = new[] { actor.id } });
            for (int index = 0; index < 31; index++) host.Tick(.1f);
            foreach (var actor in new[] { low, high })
                Issue(host, new GameCommand { player = 0, kind = CommandKind.Order, order = OrderKind.Stop, actors = new[] { actor.id } });
            host.Fog.Tick(host.Entities);
            for (int index = 0; index < 10 && (low.attackTarget != enemy.id || high.attackTarget != enemy.id); index++) host.Tick(.1f);
            foreach (var actor in new[] { low, high })
            {
                Assert.IsEmpty(actor.orders);
                Assert.AreEqual(enemy.id, actor.attackTarget);
                actor.recoveryProgressAt = host.Time - 3;
                actor.recoveryProgressPosition = actor.position;
            }
            var tracker = new LanQueueTracker();
            var first = Full(host, tracker, 1, 41);
            var replica = new LanReplica(host.Rules);
            replica.Install(first);
            var recover = typeof(World).GetMethod("TryAllyRecovery", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsTrue((bool)recover.Invoke(host, new object[] { low, low.pathGoal }));
            Assert.IsFalse((bool)recover.Invoke(host, new object[] { high, high.pathGoal }));
            Assert.IsFalse((bool)recover.Invoke(host, new object[] { low, low.pathGoal }));
            var next = Full(host, tracker, 2, 41);
            CollectionAssert.AreEqual(new[] { high.id, low.id }, next.world.fineRecoveryQueue);
            Assert.IsTrue(replica.Apply(LanProjection.Delta(first, next, host.Events)), replica.Error);
            CollectionAssert.AreEqual(next.world.fineRecoveryQueue, replica.World.CaptureState(false).fineRecoveryQueue);
            Assert.AreEqual(State(host), State(replica.World));
        }
    }
}
