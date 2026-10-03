using System;

namespace Frontier
{
    [Serializable] public sealed class EconomyTransaction
    {
        // Negative is spending, positive is a refund. Actual preserves float rounding.
        public Cost requestedDelta, actualDelta, balanceBefore, balanceAfter;
        public bool hasRepair;
        public int healthTarget;
        public float hpBefore, hpAfter;
    }

    [Serializable] public sealed class PopulationSnapshot
    {
        public int used, reserved, capacity;
        public static PopulationSnapshot From(PlayerState player) => new PopulationSnapshot
        { used = player.usedPopulation, reserved = player.reservedPopulation, capacity = player.populationCap };
    }

    public partial class World
    {
        private SimEvent Pay(int player, Cost cost, int entity, string reason, int target = 0)
        {
            var p = P(player); var before = p.resources;
            p.resources = Sub(p.resources, cost); p.spent += cost;
            return RecordTransaction("Payment", p, entity, target, reason + ": " + cost, cost * -1, before);
        }

        private SimEvent Refund(int player, Cost cost, int entity, string reason)
        {
            var p = P(player); var before = p.resources;
            p.resources += cost; p.refunded += cost;
            return RecordTransaction("Refund", p, entity, 0, reason + ": " + cost, cost, before);
        }

        private SimEvent RecordTransaction(string kind, PlayerState player, int entity, int target,
            string message, Cost requested, Cost before)
        {
            var e = Emit(kind, player.id, entity, message, 0, target);
            e.hasEconomy = true;
            e.economy = new EconomyTransaction
            {
                requestedDelta = requested, actualDelta = Sub(player.resources, before),
                balanceBefore = before, balanceAfter = player.resources
            };
            return e;
        }

        private static bool RecordsPopulation(string kind) => kind == "Payment" || kind == "Refund" ||
            kind == "ProductionQueued" || kind == "Spawn" || kind == "Death" || kind == "BuildingComplete" || kind == "AutomaticWorkerProduced";
    }
}
