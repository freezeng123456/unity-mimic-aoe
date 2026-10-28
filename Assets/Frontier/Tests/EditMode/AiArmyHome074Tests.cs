using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Controlled anchors isolate Home selection. They are not production or
    // normal-match timing evidence; paid replacement construction is covered
    // by AiArmyPlan072Tests.HomeFollowsAReallyPaidAndCompletedReplacementTown.
    public sealed class AiArmyHome074Tests
    {
        static World Arena()
        {
            var world=new World(SnapshotCodec.Clone(Catalog.Load()),7401,"coast","coast",5,"frontier");
            foreach(var e in world.Entities.Where(e=>e.Alive&&e.owner==0).ToArray())world.Remove(e);
            world.Players[0].age=4;
            return world;
        }
        static AiArmyPlan072 Plan(World world)=>new AiArmyPlan072(world,0,AiDifficultyProfile.From(world.Rules,"standard"));

        [Test] public void CompleteTownWinsOverOlderFoundationAndSafeRescueThenLowestTownIdBreaksTies()
        {
            var w=Arena();var opening=w.OpeningTownPosition(0);
            var foundation=w.Spawn("town",0,opening+new Vector2(6,0),false);
            var firstComplete=w.Spawn("town",0,opening+new Vector2(26,0));
            var laterComplete=w.Spawn("town",0,opening+new Vector2(40,0));
            w.Spawn("villager",0,opening+new Vector2(0,3));
            var raider=w.Spawn("musketeer",1,firstComplete.position+new Vector2(8,0));
            w.Fog.Tick(w.Entities);Assert.IsTrue(w.IsVisible(0,raider));
            w.Entities.Reverse();var plan=Plan(w);
            Assert.AreEqual(firstComplete.position,plan.Home,"A visible raid does not replace the Town anchor with a rescue worker, and list order cannot override the lower Town ID");
            firstComplete.construction=.5f;
            Assert.AreEqual(laterComplete.position,plan.Home,"A remaining complete Town takes priority over both foundations");
            w.Remove(laterComplete);
            Assert.AreEqual(foundation.position,plan.Home,"Without a complete Town, the oldest eligible foundation remains the home");
        }

        [TestCase("dead")][TestCase("zero_hp")][TestCase("garrisoned")][TestCase("foreign")][TestCase("waterborne")]
        public void IneligibleCompleteTownCannotReplaceOwnedLandFoundation(string state)
        {
            var w=Arena();var opening=w.OpeningTownPosition(0);
            var foundation=w.Spawn("town",0,opening+new Vector2(5,0),false);
            var excluded=w.Spawn("town",state=="foreign"?1:0,opening+new Vector2(24,0));
            if(state=="dead")w.Remove(excluded);
            else if(state=="zero_hp")excluded.hp=0;
            else if(state=="garrisoned")excluded.garrisonedIn=999;
            else if(state=="waterborne")
            {
                // Declared entity flags exercise the anchor eligibility rule;
                // ordinary Towns are not waterborne or garrisoned passengers.
                excluded.def=SnapshotCodec.Clone(excluded.def);excluded.def.waterborne=true;
            }
            Assert.AreEqual(foundation.position,Plan(w).Home);
        }

        [TestCase("villager")][TestCase("farm")][TestCase("factory")][TestCase("estate")]
        public void WithNoTownClosestSafeOwnedEconomicAnchorIsUsed(string definition)
        {
            var w=Arena();var opening=w.OpeningTownPosition(0);
            var unsafeWorker=w.Spawn("villager",0,opening+new Vector2(3,0));
            var chosen=w.Spawn(definition,0,opening+new Vector2(28,0));
            w.Spawn("villager",0,opening+new Vector2(48,0));
            w.Spawn("villager",1,opening+new Vector2(1,0));
            var raider=w.Spawn("hussar",1,unsafeWorker.position+Vector2.right);
            w.Fog.Tick(w.Entities);Assert.IsTrue(w.IsVisible(0,raider));
            Assert.AreEqual(chosen.position,Plan(w).Home,"A foreign worker and the nearer worker under a visible raid cannot anchor recovery");
        }

        [Test] public void HiddenAndDeadWeaponsDoNotDisqualifyTheNearestRescueWorker()
        {
            var w=Arena();var opening=w.OpeningTownPosition(0);
            var near=w.Spawn("villager",0,opening+new Vector2(3,0));
            var safe=w.Spawn("villager",0,opening+new Vector2(0,45));
            var cannon=w.Spawn("cannon",1,near.position+new Vector2(24,0));
            w.Fog.Tick(w.Entities);Assert.IsFalse(w.IsVisible(0,cannon),"The declared weapon lies beyond either worker's current vision");
            var plan=Plan(w);Assert.AreEqual(near.position,plan.Home,"Hidden weapons are not inputs to recovery safety");
            w.Fog.Reveal(0,cannon.position,2);Assert.IsTrue(w.IsVisible(0,cannon));
            Assert.AreEqual(safe.position,plan.Home,"The same live visible weapon now excludes the closer anchor");
            w.Remove(cannon);
            Assert.AreEqual(near.position,plan.Home,"A dead visible weapon no longer excludes the worker");
        }

        [TestCase(false)][TestCase(true)]
        public void OnlyACompletedVisibleArmedBuildingDisqualifiesNearbyRescue(bool complete)
        {
            var w=Arena();var opening=w.OpeningTownPosition(0);
            var near=w.Spawn("villager",0,opening+new Vector2(7,0));
            var safe=w.Spawn("villager",0,opening+new Vector2(0,40));
            var tower=w.Spawn("tower",1,near.position+new Vector2(12,0),complete);
            w.Fog.Tick(w.Entities);Assert.IsTrue(w.IsVisible(0,tower));
            Assert.AreEqual(complete?safe.position:near.position,Plan(w).Home);
        }

        [Test] public void EqualDistanceRescueAnchorsKeepTheirCurrentEntityOrder()
        {
            var w=Arena();var opening=w.OpeningTownPosition(0);
            var first=w.Spawn("villager",0,opening+new Vector2(6,0));
            var second=w.Spawn("villager",0,opening+new Vector2(0,6));
            var plan=Plan(w);Assert.AreEqual(first.position,plan.Home);
            w.Entities.Reverse();Assert.AreEqual(second.position,plan.Home,"Rescue ties retain the existing first-seen rule, independently of the Town ID rule");
        }

        [Test] public void WithoutAnEligibleTownOrEconomicAssetHomeReturnsTheOpening()
        {
            var w=Arena();var opening=w.OpeningTownPosition(0);
            w.Spawn("musketeer",0,opening+new Vector2(3,0));
            w.Spawn("barracks",0,opening+new Vector2(20,0));
            Assert.AreEqual(opening,Plan(w).Home,"Military units and producers other than the listed economic anchors cannot invent a recovery home");
        }

        [TestCase(false)][TestCase(true)]
        public void ReadingHomePreservesWorldAndAgreesAfterColdRestore(bool hasTown)
        {
            var w=Arena();var opening=w.OpeningTownPosition(0);
            var anchor=w.Spawn(hasTown?"town":"farm",0,opening+new Vector2(20,0));
            w.Spawn("villager",0,opening+new Vector2(0,35));w.Fog.Tick(w.Entities);
            var plan=Plan(w);string before=JsonUtility.ToJson(w.CaptureState());
            for(int i=0;i<8;i++)Assert.AreEqual(anchor.position,plan.Home);
            Assert.AreEqual(before,JsonUtility.ToJson(w.CaptureState()),"Reading the anchor cannot mutate wallets, orders, fog, navigation or events");
            var cold=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));var restored=Plan(cold);
            restored.RestoreState(SnapshotCodec.Clone(plan.CaptureState()));
            Assert.AreEqual(plan.Home,restored.Home);
            Assert.AreEqual(JsonUtility.ToJson(plan.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
            Assert.AreEqual(before,JsonUtility.ToJson(cold.CaptureState()));
        }
    }
}
