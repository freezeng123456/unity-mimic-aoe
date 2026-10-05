using System;

namespace Frontier
{
    // Save DTOs contain owned arrays and values, never live controllers or entity references.
    [Serializable] public sealed class AiAgePlanSnapshot
    {
        public int targetAge;
        public bool saving;
        public float retryAt,resumeAt,progressAt,bestCoverage;
    }
    [Serializable] public sealed class AiWorkforceSnapshot
    { public float rebalanceAt; }
    [Serializable] public sealed class AiSearchSnapshot
    {
        public bool visitedStart;
        public bool[] processed;
        public int current,pass,revision,enemyPlayer=-1,schema;
        public float selectedAt;
    }
    [Serializable] public sealed class AiWaterEconomySnapshot
    { public int surveyor;public float patrolAt;public AiNavalOperationsSnapshot navalOperations; }
    [Serializable] public sealed class AiCivilizationEconomySnapshot
    { public float manufactureAt; }
    [Serializable] public sealed class AiSnapshot
    {
        public int player,expeditionSize,rotation,buildSerial;
        public float thinkAt,armyAt;
        public bool expedition,searchingLastReview;
        public IntPointSnapshot[] memory;
        public AiAgePlanSnapshot agePlan;
        public AiWorkforceSnapshot workforce;
        public AiSearchSnapshot search;
        public AiWaterEconomySnapshot waterEconomy;
        public AiCivilizationEconomySnapshot civilizationEconomy;
    }
    [Serializable] public sealed class MatchSimulationSnapshot
    {
        public string difficultyId;
        public int stepCount;
        public float accumulator;
        public AiSnapshot[] controllers;
    }
    internal static class AiSaveValidation
    {
        public static void Require(bool valid,string message)
        { if(!valid)throw new ArgumentException("Invalid saved AI: "+message); }
        public static bool Clock(float value)=>SnapshotCodec.Finite(value)&&value>=0;
    }
}
