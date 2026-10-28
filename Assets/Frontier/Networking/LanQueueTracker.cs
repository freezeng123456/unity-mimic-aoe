using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Frontier
{
    [Serializable]
    public sealed class LanQueueIdentity
    {
        public int entity;
        public long[] jobs = Array.Empty<long>();
    }

    // Production identity belongs to a LAN match. It must not change the simulation,
    // save or replay schemas, nor infer identity from two equal-looking orders.
    public sealed class LanQueueTracker
    {
        private sealed class JobComparer : IEqualityComparer<ProductionJob>
        {
            public bool Equals(ProductionJob left, ProductionJob right) => ReferenceEquals(left, right);
            public int GetHashCode(ProductionJob job) => RuntimeHelpers.GetHashCode(job);
        }

        private readonly Dictionary<ProductionJob, long> identities = new Dictionary<ProductionJob, long>(new JobComparer());
        private World trackedWorld;
        private long nextIdentity;
        public int TrackedJobCount => identities.Count;

        public LanQueueIdentity[] Capture(World world)
        {
            Synchronize(world);
            var rows = new List<LanQueueIdentity>();
            foreach (var entity in world.Entities)
            {
                if (entity.queue.Count == 0) continue;
                var jobs = new long[entity.queue.Count];
                for (int index = 0; index < jobs.Length; index++) jobs[index] = identities[entity.queue[index]];
                rows.Add(new LanQueueIdentity { entity = entity.id, jobs = jobs });
            }
            return rows.ToArray();
        }

        public long QueueId(World world, int entity, int index)
        {
            Synchronize(world);
            var producer = world.Get(entity);
            return producer == null || index < 0 || index >= producer.queue.Count ? 0 : identities[producer.queue[index]];
        }

        public GameCommand ResolveCancel(World world, GameCommand command, long expectedQueueId)
        {
            if (command == null || command.kind != CommandKind.CancelQueue || command.actors == null || command.actors.Length != 1)
                throw new InvalidOperationException("A queue cancellation must select one producer");
            if (expectedQueueId <= 0) throw new InvalidOperationException("Production entry identity is missing");
            Synchronize(world);
            var producer = world.Get(command.actors[0]);
            if (producer == null || !producer.Alive || producer.owner != command.player)
                throw new InvalidOperationException("Select your production building");
            for (int index = 0; index < producer.queue.Count; index++)
            {
                if (identities[producer.queue[index]] != expectedQueueId) continue;
                return new GameCommand
                {
                    player = command.player, kind = command.kind, actors = (int[])command.actors.Clone(),
                    order = command.order, point = command.point, target = command.target, index = index,
                    count = command.count, item = command.item, choice = command.choice, append = command.append
                };
            }
            throw new InvalidOperationException("That production entry has already completed or been cancelled");
        }

        private void Synchronize(World world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.Entities.Count > LanQueueIndex.MaximumEntities)
                throw new InvalidOperationException("Too many production entities for LAN publication");
            if (!ReferenceEquals(trackedWorld, world))
            {
                identities.Clear();
                trackedWorld = world;
            }
            var current = new HashSet<ProductionJob>(new JobComparer());
            foreach (var entity in world.Entities)
            {
                if (entity.queue.Count > LanQueueIndex.MaximumJobsPerQueue)
                    throw new InvalidOperationException("Production queue exceeds the LAN limit");
                foreach (var job in entity.queue)
                {
                    if (job == null || !current.Add(job))
                        throw new InvalidOperationException("Production entries must have unique object identities");
                }
            }
            var removed = new List<ProductionJob>();
            foreach (var pair in identities) if (!current.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var job in removed) identities.Remove(job);
            foreach (var entity in world.Entities)
                foreach (var job in entity.queue)
                    if (!identities.ContainsKey(job)) identities.Add(job, checked(++nextIdentity));
        }
    }

    // A replica reads host IDs without assigning its own identities to deserialized jobs.
    public sealed class LanQueueIndex
    {
        public const int MaximumEntities = 20000;
        public const int MaximumJobsPerQueue = 6;
        private Dictionary<int, long[]> rows = new Dictionary<int, long[]>();

        public void Replace(LanQueueIdentity[] identities)
        {
            rows = Parse(identities);
        }

        public long QueueId(int entity, int index)
        {
            return rows.TryGetValue(entity, out var jobs) && index >= 0 && index < jobs.Length ? jobs[index] : 0;
        }

        public static void Validate(WorldSnapshot snapshot, LanQueueIdentity[] identities)
        {
            if (snapshot == null || snapshot.entities == null) throw new ArgumentException("World snapshot is missing");
            if (snapshot.entities.Length > MaximumEntities) throw new ArgumentException("Too many production entities");
            var parsed = Parse(identities);
            int expected = 0;
            var entities = new HashSet<int>();
            foreach (var entity in snapshot.entities)
            {
                if (entity == null || entity.state == null || !entities.Add(entity.state.id) || entity.queue == null)
                    throw new ArgumentException("Invalid production entity snapshot");
                if (entity.queue.Length == 0) continue;
                expected++;
                if (!parsed.TryGetValue(entity.state.id, out var jobs) || jobs.Length != entity.queue.Length)
                    throw new ArgumentException("Production identity mapping does not match the world queue");
            }
            if (parsed.Count != expected) throw new ArgumentException("Production identity mapping contains an unknown queue");
        }

        private static Dictionary<int, long[]> Parse(LanQueueIdentity[] identities)
        {
            if (identities == null || identities.Length > MaximumEntities)
                throw new ArgumentException("Invalid production identity mapping");
            var result = new Dictionary<int, long[]>();
            var jobIds = new HashSet<long>();
            foreach (var identity in identities)
            {
                if (identity == null || identity.entity <= 0 || identity.jobs == null || identity.jobs.Length == 0
                    || identity.jobs.Length > MaximumJobsPerQueue || result.ContainsKey(identity.entity))
                    throw new ArgumentException("Invalid production queue identity");
                foreach (long job in identity.jobs)
                    if (job <= 0 || !jobIds.Add(job)) throw new ArgumentException("Duplicate or invalid production entry identity");
                result.Add(identity.entity, (long[])identity.jobs.Clone());
            }
            return result;
        }
    }
}
