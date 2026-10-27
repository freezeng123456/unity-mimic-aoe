using System;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    internal static class MapBaselineNeutral088
    {
        // Observed in NeutralArt120 actual source-bound Editor map receipts.
        internal const string CurrentRulesHash="be43d23923c334be31b757cbd916275c3882bb0146ebe5a03574fe438dfa5cc3";
        // Exact historical fixture catalog, byte-for-byte captured by the
        // individual tests. New empty DTO fields alter only its runtime hash.
        internal static World RestoreHistoricalFixture(string rulesJson,string worldJson)
        {
            const string legacy="b08413261c770b536bcc25f39187f237b5d83e753c93a55b4cfd6cc8b3ad0ba4";
            const string added=",\"additionalUnits\":[],\"additionalTechs\":[]";
            Assert.AreEqual(legacy,SnapshotCodec.Hash(rulesJson),"Pinned raw historical catalog");
            var rules=JsonUtility.FromJson<RuleCatalog>(rulesJson);string modern=JsonUtility.ToJson(rules);
            int count=0,offset=0;while((offset=modern.IndexOf(added,offset,StringComparison.Ordinal))>=0){count++;offset+=added.Length;}
            Assert.AreEqual(3,count,"Only three empty added service fields may be normalized");
            Assert.AreEqual(legacy,SnapshotCodec.Hash(modern.Replace(added,"")),"All original catalog fields and order remain exact");
            var snapshot=JsonUtility.FromJson<WorldSnapshot>(worldJson);
            Assert.AreEqual(legacy,snapshot.rulesHash,"The pinned snapshot belongs to the pinned catalog");
            snapshot.rulesHash=SnapshotCodec.RulesHash(rules);
            return World.Restore(rules,snapshot);
        }
        internal static void AssertExpandedRules(RuleCatalog rules)
        {
            Assert.AreEqual(CurrentRulesHash,SnapshotCodec.RulesHash(rules),"Current complete catalog identity");
            // The migration guard independently strips only the six additions and
            // checks every original catalog field against the pinned legacy hash.
            Assert.DoesNotThrow(()=>NeutralSaveMigration088.ReconstructPreviousRules(rules));
            string[] allies={"river_guild","highland_covenant","coastal_league"};
            string[] units={"ally_river_pike","ally_highland_rifle","ally_coastal_marine"};
            string[] techs={"ally_river_pike_drill","ally_highland_rifle_drill","ally_coastal_marine_drill"};
            Assert.AreEqual(allies.Length,rules.neutralAllies.Length);
            for(int i=0;i<allies.Length;i++)
            {
                var d=rules.neutralAllies[i];Assert.AreEqual(allies[i],d.id);
                Assert.IsNotNull(rules.Entity(units[i]));Assert.IsNotNull(rules.Tech(techs[i]));
                CollectionAssert.AreEqual(new[]{d.unit,units[i]},World.NeutralOptions(d,CommandKind.NeutralRecruit));
                CollectionAssert.AreEqual(new[]{d.tech,techs[i]},World.NeutralOptions(d,CommandKind.NeutralResearch));
            }
        }
    }
}
