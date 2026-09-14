using System;

namespace Frontier
{
    // A detached match choice: editing shared rules or menu settings cannot retime a live AI.
    public sealed class AiDifficultyProfile
    {
        public readonly string Id,Name,Description;
        public readonly float DecisionSeconds,ArmySeconds;
        public AiDifficultyProfile(AiDifficultyDef definition)
        {
            string reason=InvalidReason(definition);
            if(reason!="")throw new ArgumentException(reason,nameof(definition));
            Id=definition.id;Name=definition.name;Description=definition.description;
            DecisionSeconds=definition.decisionSeconds;ArmySeconds=definition.armySeconds;
        }
        public static AiDifficultyProfile From(RuleCatalog rules,string id)
        {
            if(rules==null)throw new ArgumentNullException(nameof(rules));
            var definition=rules.Difficulty(id);
            if(definition==null)throw new ArgumentException("Unknown AI difficulty: "+id,nameof(id));
            return new AiDifficultyProfile(definition);
        }
        public static string InvalidReason(AiDifficultyDef definition)
        {
            if(definition==null||string.IsNullOrWhiteSpace(definition.id)||string.IsNullOrWhiteSpace(definition.name))return "Choose a valid AI difficulty.";
            bool valid(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&value>=MatchSimulation.StepSeconds;
            return valid(definition.decisionSeconds)&&valid(definition.armySeconds)?"":"AI difficulty intervals must be finite and at least one simulation step.";
        }
    }
}
