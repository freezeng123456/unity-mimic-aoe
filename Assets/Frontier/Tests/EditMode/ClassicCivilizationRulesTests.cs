using System.Linq;
using NUnit.Framework;

namespace Frontier.Tests
{
    public sealed class ClassicCivilizationRulesTests
    {
        [Test] public void HistoricalNamesKeepLegacyIdsAndEconomicIdentity()
        {
            var r=Catalog.Load();
            Assert.AreEqual("British",r.Civ("coast").name);Assert.IsTrue(r.Civ("coast").houseVillager);
            Assert.AreEqual("Dutch",r.Civ("river").name);Assert.IsTrue(r.Civ("river").coinVillager);
            Assert.AreEqual(1.15f,r.Civ("river").mineGatherMultiplier,.0001f);
            Assert.AreEqual("Ruyter",r.Entity("carabineer").name);Assert.AreEqual(1,r.Entity("carabineer").population);
            Assert.AreEqual(2,r.Civ("river").unitAges.Single(x=>x.unit=="skirmisher").age);
        }
        [Test] public void CoureursReplaceSettlersAndHaveAnExplicitEconomicTradeoff()
        {
            var r=Catalog.Load();var civ=r.Civ("france");var worker=r.Entity(civ.workerUnit);
            Assert.AreEqual("coureur",worker.id);Assert.AreEqual(Tags.Villager,worker.tags&Tags.Villager);
            Assert.Greater(worker.cost.food,r.Entity("villager").cost.food);Assert.AreEqual(1.25f,worker.gatherMultiplier);
            Assert.Less(civ.villagerLimit,r.Civ("coast").villagerLimit);Assert.Greater(worker.hp,r.Entity("villager").hp);
            Assert.Greater(worker.range,1);Assert.Greater(worker.meleeDamage,0);
            Assert.IsFalse(DeckLibrary.EntityAllowed(r,"france","villager"));
            Assert.IsFalse(DeckLibrary.CardAllowed(r,"france",r.Card("reserve_villager")));
            Assert.IsTrue(DeckLibrary.CardAllowed(r,"france",r.Card("france_coureurs_1")));
        }
        [Test] public void CuirassiersHaveRealMeleeAreaDamageAndPopulationCost()
        {
            var r=Catalog.Load();var cuirassier=r.Entity("cuirassier");var hussar=r.Entity("hussar");
            Assert.AreEqual(DamageKind.Melee,cuirassier.damageKind);Assert.AreEqual(0,cuirassier.projectileSpeed);
            Assert.Greater(cuirassier.meleeSplash,0);Assert.AreEqual(0,cuirassier.splash);
            Assert.Greater(cuirassier.population,hussar.population);Assert.Greater(cuirassier.cost.food+cuirassier.cost.coin,hussar.cost.food+hussar.cost.coin);
            Assert.Greater(cuirassier.hp,hussar.hp);Assert.Less(cuirassier.speed,hussar.speed);
            Assert.IsTrue(DeckLibrary.EntityAllowed(r,"france","hussar"),"Cuirassier is an additional Age III commitment, not an Age II hussar replacement");
        }
        [Test] public void OttomanFreeRecruitmentHasSeparatePaidLimitAndSpeedLadders()
        {
            var r=Catalog.Load();var civ=r.Civ("ottoman");Assert.Greater(civ.autoVillagerSeconds,0);Assert.AreEqual(25,civ.villagerLimit);
            var caps=new[]{"mosque_galata","mosque_topkapi","mosque_tanzimat"}.Select(r.Tech).ToArray();
            Assert.AreEqual(99,civ.villagerLimit+caps.Sum(t=>t.villagerLimit));
            var speeds=new[]{"mosque_millet","mosque_koprulu","mosque_bazaar"}.Select(r.Tech).ToArray();
            for(int i=0;i<3;i++)
            {
                foreach(var tech in new[]{caps[i],speeds[i]})
                {
                    Assert.AreEqual("ottoman",tech.civilization);Assert.AreEqual("mosque",tech.building);Assert.Greater(tech.cost.wood,0);
                    Assert.AreEqual(i==0?"":(tech.villagerLimit>0?caps[i-1]:speeds[i-1]).id,tech.requires);
                }
                Assert.AreEqual(0,caps[i].autoVillagerSpeed);Assert.Greater(speeds[i].autoVillagerSpeed,0);Assert.AreEqual(0,speeds[i].villagerLimit);
            }
            Assert.AreEqual(2,r.Civ("ottoman").unitAges.Single(x=>x.unit=="foundry").age);
            Assert.AreEqual(2,r.Entity("abus").age);Assert.AreEqual(3,r.Entity("cannon").age);
        }
        [Test] public void OttomanInfantryAndShipmentOnlyCavalryHaveDifferentRoles()
        {
            var r=Catalog.Load();var janissary=r.Entity("janissary");var abus=r.Entity("abus");var spahi=r.Entity("spahi");
            Assert.AreEqual(Tags.HeavyInfantry,janissary.tags&Tags.HeavyInfantry);
            Assert.IsTrue(janissary.meleeBonuses.Any(b=>(b.tags&Tags.Cavalry)!=0&&b.multiplier>=2));
            Assert.IsFalse(janissary.bonuses.Any(b=>(b.tags&Tags.Cavalry)!=0),"Melee anti-cavalry must not leak into ranged fire");
            Assert.AreEqual(Tags.Infantry,abus.tags&Tags.Infantry);Assert.AreEqual(Tags.None,abus.tags&Tags.Artillery);
            Assert.AreEqual("foundry",abus.producer);Assert.IsTrue(abus.bonuses.Any(b=>(b.tags&Tags.HeavyInfantry)!=0));
            Assert.IsTrue(string.IsNullOrEmpty(spahi.producer));Assert.IsTrue(r.cards.Any(c=>c.civilization=="ottoman"&&c.unit=="spahi"));
            Assert.IsFalse(r.ageChoices.Any(c=>c.unit=="spahi"),"Spahi remain an actual Home City shipment choice");
        }
        [Test] public void ReplacementRestrictionsAlsoRemoveIncompatibleSharedCards()
        {
            var r=Catalog.Load();
            foreach(var item in new[]{("coast","skirmisher"),("river","musketeer"),("river","dragoon"),("ottoman","pike"),("ottoman","musketeer"),("ottoman","skirmisher"),("ottoman","dragoon")})
            {
                Assert.IsFalse(DeckLibrary.EntityAllowed(r,item.Item1,item.Item2));
                Assert.IsFalse(DeckLibrary.Available(r,item.Item1).Any(c=>c.unit==item.Item2));
                Assert.IsFalse(r.ageChoices.Any(c=>c.civilization==item.Item1&&c.unit==item.Item2));
            }
            Assert.IsTrue(DeckLibrary.EntityAllowed(r,"river","halberdier"));
            Assert.IsTrue(DeckLibrary.EntityAllowed(r,"ottoman","hussar"),"Classic Ottoman hussars remain; DE Deli is outside this baseline");
        }
        [Test] public void NewMilitaryUpgradeChainsAreSequentialAndUnitSpecific()
        {
            var r=Catalog.Load();foreach(var id in new[]{"cuirassier","janissary","abus","spahi","halberdier"})
            {
                string previous="";int age=3;
                foreach(var rank in new[]{"veteran","guard","imperial"})
                {
                    var t=r.Tech(rank+"_"+id);Assert.NotNull(t,id);Assert.AreEqual(age++,t.age);Assert.AreEqual(previous,t.requires);
                    CollectionAssert.AreEqual(new[]{id},t.units);Assert.AreEqual(r.Entity(id).civilization,t.civilization);
                    Assert.IsTrue(DeckLibrary.EntityAllowed(r,t.civilization,t.building));Assert.Greater(t.attack,0);Assert.Greater(t.health,0);previous=t.id;
                }
            }
            Assert.Greater(r.Tech("guard_cuirassier").attack,r.Tech("guard_hussar").attack);
            Assert.Greater(r.Tech("guard_carabineer").attack,r.Tech("guard_dragoon").attack);
        }
        [Test] public void FrenchAndOttomanPresetsCarryTheirMechanicsInAllThreeStyles()
        {
            var r=Catalog.Load();foreach(var preset in new[]{"economy","military","technology"})
            {
                CollectionAssert.IsSubsetOf(new[]{"france_coureurs_1","card_france_hunting_parties","france_cuirassiers_3"},DeckLibrary.Preset(r,"france",preset));
                CollectionAssert.IsSubsetOf(new[]{"ottoman_settlers_1","card_ottoman_millet_support","ottoman_janissaries_2","ottoman_abus_2"},DeckLibrary.Preset(r,"ottoman",preset));
            }
        }
    }
}
