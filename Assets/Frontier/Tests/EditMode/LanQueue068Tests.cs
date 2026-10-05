using System;
using System.Linq;
using NUnit.Framework;

namespace Frontier.Tests
{
    public sealed class LanQueue068Tests
    {
        private World world;
        private EntityState town;
        private LanQueueTracker tracker;

        [SetUp]
        public void SetUp()
        {
            world = new World(Catalog.Load(), 314159, "coast", "coast", 5);
            world.Players[0].resources = new Cost(5000, 5000, 5000);
            world.Players[0].populationCap = 200;
            town = world.Entities.First(entity => entity.owner == 0 && entity.def.id == "town");
            tracker = new LanQueueTracker();
        }

        private void Train(int count)
        {
            for (int index = 0; index < count; index++)
            {
                var result = world.Issue(new GameCommand { player = 0, kind = CommandKind.Train, actors = new[] { town.id }, item = "villager" });
                Assert.IsTrue(result.accepted, result.reason);
            }
        }

        private GameCommand Cancel(int index)
        {
            return new GameCommand { player = 0, kind = CommandKind.CancelQueue, actors = new[] { town.id }, index = index };
        }

        [Test]
        public void EqualOrdersReceiveDistinctIdsThatSurviveProgress()
        {
            Train(3);
            var ids = tracker.Capture(world).Single(row => row.entity == town.id).jobs;
            Assert.AreEqual(3, ids.Distinct().Count());
            Assert.IsTrue(ids.All(id => id > 0));
            world.TickEconomy(1);
            CollectionAssert.AreEqual(ids, tracker.Capture(world).Single(row => row.entity == town.id).jobs);
            Assert.AreEqual(ids[1], tracker.QueueId(world, town.id, 1));
        }

        [Test]
        public void CompletingHeadRebasesTheSelectedSecondJobWithoutCancellingTheThird()
        {
            Train(3);
            var second = town.queue[1];
            var third = town.queue[2];
            long selected = tracker.QueueId(world, town.id, 1);
            var request = Cancel(1);
            world.TickEconomy(town.queue[0].duration + 1);
            Assert.AreEqual(2, town.queue.Count);
            Assert.AreSame(second, town.queue[0]);
            var resolved = tracker.ResolveCancel(world, request, selected);
            Assert.AreEqual(0, resolved.index);
            Assert.AreEqual(1, request.index, "The submitted payload remains immutable for retry and deduplication");
            Assert.AreNotSame(request.actors, resolved.actors);
            Assert.IsTrue(world.Issue(resolved).accepted);
            Assert.AreEqual(1, town.queue.Count);
            Assert.AreSame(third, town.queue[0]);
        }

        [Test]
        public void CompletingSelectedHeadRejectsInsteadOfCancellingItsIdenticalSuccessor()
        {
            Train(2);
            var successor = town.queue[1];
            long selected = tracker.QueueId(world, town.id, 0);
            world.TickEconomy(town.queue[0].duration + 1);
            float food = world.Players[0].resources.food;
            int reserved = world.Players[0].reservedPopulation;
            Assert.Throws<InvalidOperationException>(() => tracker.ResolveCancel(world, Cancel(0), selected));
            Assert.AreEqual(1, town.queue.Count);
            Assert.AreSame(successor, town.queue[0]);
            Assert.AreEqual(food, world.Players[0].resources.food);
            Assert.AreEqual(reserved, world.Players[0].reservedPopulation);
        }

        [Test]
        public void EarlierCancellationRebasesALaterJobAndRepeatedCancellationCannotRefundTwice()
        {
            Train(3);
            long first = tracker.QueueId(world, town.id, 0);
            long third = tracker.QueueId(world, town.id, 2);
            Assert.IsTrue(world.Issue(tracker.ResolveCancel(world, Cancel(0), first)).accepted);
            var resolved = tracker.ResolveCancel(world, Cancel(2), third);
            Assert.AreEqual(1, resolved.index);
            Assert.IsTrue(world.Issue(resolved).accepted);
            float food = world.Players[0].resources.food;
            int reserved = world.Players[0].reservedPopulation;
            Assert.Throws<InvalidOperationException>(() => tracker.ResolveCancel(world, Cancel(2), third));
            Assert.AreEqual(food, world.Players[0].resources.food);
            Assert.AreEqual(reserved, world.Players[0].reservedPopulation);
            Assert.AreEqual(1, town.queue.Count);
        }

        [Test]
        public void PruningReleasesOldJobReferencesAndNeverReusesAnId()
        {
            Train(1);
            long old = tracker.QueueId(world, town.id, 0);
            Assert.AreEqual(1, tracker.TrackedJobCount);
            Assert.IsTrue(world.Issue(tracker.ResolveCancel(world, Cancel(0), old)).accepted);
            Assert.IsEmpty(tracker.Capture(world));
            Assert.AreEqual(0, tracker.TrackedJobCount);
            Train(1);
            Assert.Greater(tracker.QueueId(world, town.id, 0), old);
        }

        [Test]
        public void MovingToANewWorldCannotMistakeDeserializedJobsForOldObjects()
        {
            Train(1);
            long original = tracker.QueueId(world, town.id, 0);
            var restored = World.Restore(world.Rules, world.CaptureState(false));
            Assert.Greater(tracker.QueueId(restored, town.id, 0), original);
            Assert.Throws<InvalidOperationException>(() => tracker.ResolveCancel(restored, Cancel(0), original));
        }

        [Test]
        public void ClientLookupOwnsItsArraysAndMalformedReplacementPreservesOldMapping()
        {
            Train(2);
            var metadata = tracker.Capture(world);
            var index = new LanQueueIndex();
            index.Replace(metadata);
            long original = index.QueueId(town.id, 0);
            metadata[0].jobs[0] = 9000;
            Assert.AreEqual(original, index.QueueId(town.id, 0));
            var malformed = new[] { new LanQueueIdentity { entity = town.id, jobs = new[] { 77L, 77L } } };
            Assert.Throws<ArgumentException>(() => index.Replace(malformed));
            Assert.AreEqual(original, index.QueueId(town.id, 0));
            Assert.AreEqual(0, index.QueueId(town.id, 2));
            Assert.AreEqual(0, index.QueueId(999999, 0));
        }

        [Test]
        public void MetadataMustCoverExactlyEveryProjectedQueue()
        {
            Train(2);
            var snapshot = world.CaptureState(false);
            var metadata = tracker.Capture(world);
            Assert.DoesNotThrow(() => LanQueueIndex.Validate(snapshot, metadata));
            Assert.Throws<ArgumentException>(() => LanQueueIndex.Validate(snapshot, Array.Empty<LanQueueIdentity>()));
            metadata[0].jobs = new[] { metadata[0].jobs[0] };
            Assert.Throws<ArgumentException>(() => LanQueueIndex.Validate(snapshot, metadata));
            metadata = tracker.Capture(world);
            metadata[0].entity = 999999;
            Assert.Throws<ArgumentException>(() => LanQueueIndex.Validate(snapshot, metadata));
        }

        [Test]
        public void ClientMappingRejectsCrossProducerIdsAndUnboundedQueues()
        {
            var index = new LanQueueIndex();
            Assert.Throws<ArgumentException>(() => index.Replace(new[]
            {
                new LanQueueIdentity { entity = 1, jobs = new[] { 5L } },
                new LanQueueIdentity { entity = 2, jobs = new[] { 5L } }
            }));
            Assert.Throws<ArgumentException>(() => index.Replace(new[] { new LanQueueIdentity { entity = 1, jobs = Enumerable.Range(1, 7).Select(value => (long)value).ToArray() } }));
            Assert.Throws<ArgumentException>(() => index.Replace(new[] { new LanQueueIdentity { entity = 1, jobs = Array.Empty<long>() } }));
            Assert.Throws<ArgumentException>(() => index.Replace(new[] { new LanQueueIdentity { entity = 1, jobs = new[] { 0L } } }));
        }

        [Test]
        public void ForeignProducerAndMissingIdentityCannotCancelAJob()
        {
            Train(1);
            long selected = tracker.QueueId(world, town.id, 0);
            var foreign = Cancel(0);
            foreign.player = 1;
            Assert.Throws<InvalidOperationException>(() => tracker.ResolveCancel(world, foreign, selected));
            Assert.Throws<InvalidOperationException>(() => tracker.ResolveCancel(world, Cancel(0), 0));
            Assert.AreEqual(1, town.queue.Count);
        }
    }
}
