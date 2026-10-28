using System;
using System.Linq;
using NUnit.Framework;
namespace Frontier.Tests
{
    public sealed class ContentExpansionRulesTests
    {
        [Test] public void SixCivilizationsHaveSubstantialLegalPoolsAndDistinctCompletePresets()
        {
            var r=Catalog.Load();CollectionAssert.AreEqual(new[]{"coast","river","france","ottoman","wayfarer","highland"},r.civs.Select(c=>c.id));
            foreach(var civ in r.civs)
            {
                var available=DeckLibrary.Available(r,civ.id);Assert.That(available.Count,Is.InRange(43,53),civ.id);
                CollectionAssert.IsSubsetOf(new[]{"naval_transport","naval_frigate","naval_monitor"},available.Select(c=>c.id),civ.id+" needs the shared harbor support cards");
                var presets=new[]{"economy","military","technology"}.Select(name=>DeckLibrary.Preset(r,civ.id,name)).ToArray();
                foreach(var deck in presets)
                {
                    Assert.AreEqual(25,deck.Count,civ.id);Assert.AreEqual(25,deck.Distinct().Count());
                    Assert.AreEqual("",DeckLibrary.Validate(r,civ.id,deck));
                    Assert.IsTrue(deck.Select(r.Card).Any(c=>c.age==1),civ.id+" needs an opening shipment");
                    Assert.IsTrue(deck.Select(r.Card).Any(c=>c.age==5),civ.id+" needs a late-game shipment");
                }
                for(int i=0;i<presets.Length;i++)for(int j=i+1;j<presets.Length;j++)
                    Assert.IsFalse(presets[i].OrderBy(x=>x).SequenceEqual(presets[j].OrderBy(x=>x)),civ.id+" presets need different contents");
            }
        }
        [Test] public void ExpandedCardRewardsAreDefinedAndAvailableInTheirRequiredEra()
        {
            var r=Catalog.Load();Assert.AreEqual(r.cards.Length,r.cards.Select(c=>c.id).Distinct().Count());
            foreach(var card in r.cards)
            {
                Assert.That(card.age,Is.InRange(1,5),card.id);
                bool reward=card.crates.food+card.crates.wood+card.crates.coin>0;
                if(!string.IsNullOrEmpty(card.unit))
                {
                    var u=r.Entity(card.unit);Assert.NotNull(u,card.id);Assert.LessOrEqual(RequiredAge(r,card.civilization,u),card.age,card.id);Assert.Greater(card.count,0,card.id);reward=true;
                    if(!string.IsNullOrEmpty(card.civilization))Assert.IsTrue(DeckLibrary.EntityAllowed(r,card.civilization,u.id),card.id);
                }
                if(!string.IsNullOrEmpty(card.tech))
                {
                    var t=r.Tech(card.tech);Assert.NotNull(t,card.id);Assert.LessOrEqual(t.age,card.age,card.id);reward=true;
                    if(!string.IsNullOrEmpty(t.civilization))Assert.AreEqual(t.civilization,card.civilization,card.id);
                }
                Assert.IsTrue(reward,card.id+" must deliver an actual reward");
            }
        }
        [Test] public void ReplacedMilitaryUnitsCannotReturnThroughSharedCards()
        {
            var r=Catalog.Load();
            foreach(var id in new[]{"reserve_pike","hussars","reserve_hussar","skirmishers","reserve_skirmisher"})
            {
                Assert.IsFalse(DeckLibrary.Available(r,"wayfarer").Any(c=>c.id==id),id);
                Assert.IsNotEmpty(DeckLibrary.Validate(r,"wayfarer",new[]{id}),id);
                Assert.AreEqual(id!="skirmishers"&&id!="reserve_skirmisher",DeckLibrary.CardAllowed(r,"coast",r.Card(id)),id+" respects British longbow replacement");
                Assert.IsTrue(DeckLibrary.CardAllowed(r,"river",r.Card(id)),id+" still belongs in existing river pool");
            }
            Assert.IsFalse(DeckLibrary.CardAllowed(r,"highland",r.Card("reserve_pike")));
            Assert.IsFalse(DeckLibrary.CardAllowed(r,"coast",r.Card("highland_culverin_battery")));
        }
        [Test] public void EveryAdvancementOffersThreeFactionSpecificPermanentChoices()
        {
            var r=Catalog.Load();Assert.AreEqual(72,r.ageChoices.Length);
            Assert.AreEqual(72,r.ageChoices.Select(x=>x.id).Distinct().Count());
            foreach(var civ in r.civs)for(int age=2;age<=5;age++)
            {
                var choices=r.ageChoices.Where(x=>x.civilization==civ.id&&x.age==age).ToArray();Assert.AreEqual(3,choices.Length,civ.id+age);
                foreach(var choice in choices)
                {
                    var tech=r.Tech(choice.tech);Assert.NotNull(tech,choice.id);Assert.AreEqual(civ.id,tech.civilization);Assert.AreEqual(age,tech.age);
                    Assert.IsTrue(string.IsNullOrEmpty(tech.building),choice.id+" choice cannot also be bought at a building");
                    Assert.IsFalse(r.cards.Any(c=>c.tech==choice.tech),choice.id+" choice cannot also be granted by a card");
                    if(!string.IsNullOrEmpty(choice.unit))
                    {
                        Assert.IsTrue(DeckLibrary.EntityAllowed(r,civ.id,choice.unit),choice.id);
                        Assert.LessOrEqual(RequiredAge(r,civ.id,r.Entity(choice.unit)),age);Assert.Greater(choice.count,0);
                    }
                    else Assert.Greater(choice.crates.food+choice.crates.wood+choice.crates.coin,0,choice.id);
                }
            }
        }
        [Test] public void SpecialistMilitaryUnitsHaveLegalProducersAndSequentialUpgradeLadders()
        {
            var r=Catalog.Load();
            foreach(var id in new[]{"escort","outrider","convoy_gunner","field_engineer","culverin","shield_guard"})
            {
                var unit=r.Entity(id);Assert.NotNull(unit);Assert.IsTrue(DeckLibrary.EntityAllowed(r,unit.civilization,unit.producer));
                var producer=r.Entity(unit.producer);Assert.AreEqual(EntityKind.Building,producer.kind);Assert.LessOrEqual(producer.age,unit.age);
                string previous="";int age=3;
                foreach(var rank in new[]{"veteran","guard","imperial"})
                {
                    var tech=r.Tech(rank+"_"+id);Assert.NotNull(tech);Assert.AreEqual(unit.civilization,tech.civilization);
                    Assert.AreEqual(age++,tech.age);Assert.AreEqual(previous,tech.requires);CollectionAssert.AreEqual(new[]{id},tech.units);
                    Assert.Greater(tech.attack,0);Assert.Greater(tech.health,0);previous=tech.id;
                }
            }
        }
        [Test] public void FreightAndEngineeringHaveDifferentRolesAndExplicitCosts()
        {
            var r=Catalog.Load();var wagon=r.Entity("caravan");Assert.IsTrue(wagon.civilian);Assert.AreEqual(Tags.None,wagon.tags);
            Assert.AreEqual(0,wagon.damage);Assert.AreEqual(0,wagon.meleeDamage);Assert.AreEqual(0,wagon.siegeDamage);
            Assert.Greater(wagon.population,0);Assert.Greater(wagon.cost.wood+wagon.cost.coin,0);Assert.AreEqual("waystation",wagon.producer);
            var engineer=r.Entity("field_engineer");Assert.IsTrue(engineer.repairArtillery);Assert.AreEqual(Tags.None,engineer.tags&Tags.Villager);
            Assert.IsTrue(r.Entity("shield_guard").shieldAbility);
            var culverin=r.Entity("culverin");Assert.AreEqual("workshop",culverin.producer);Assert.AreEqual(0,culverin.splash);
            Assert.IsTrue(culverin.bonuses.Any(b=>b.tags==Tags.Artillery&&b.multiplier>=3));
            Assert.IsFalse(culverin.bonuses.Any(b=>(b.tags&Tags.Infantry)!=0));
        }
        [Test] public void ManufactureRecipeHasAnExplicitPricePrerequisiteAndFiniteOutput()
        {
            var r=Catalog.Load();Assert.IsTrue(r.Civ("highland").manufacturing);Assert.IsTrue(r.Civ("wayfarer").caravanTrade);
            Assert.AreEqual(1,r.manufactureRecipes.Length);var recipe=r.manufactureRecipes[0];
            Assert.Greater(recipe.cost.food+recipe.cost.wood+recipe.cost.coin,0);Assert.Greater(recipe.time,0);Assert.AreEqual(1,recipe.prefabs);
            var unlock=r.Tech(recipe.requires);Assert.NotNull(unlock);Assert.AreEqual("highland",unlock.civilization);Assert.AreEqual("workshop",unlock.building);
        }
        [Test] public void NewFactionPresetsAlwaysIncludeTheirCoreEconomyAndSpecialists()
        {
            var r=Catalog.Load();foreach(var preset in new[]{"economy","military","technology"})
            {
                var wayfarer=DeckLibrary.Preset(r,"wayfarer",preset);
                CollectionAssert.IsSubsetOf(new[]{"wayfarer_cargo_wagons","card_wayfarer_cargo_bonds","wayfarer_escort_patrol"},wayfarer);
                var highland=DeckLibrary.Preset(r,"highland",preset);
                CollectionAssert.IsSubsetOf(new[]{"card_highland_standard_jigs","highland_engineer_team","highland_culverin_battery"},highland);
            }
        }
        private static int RequiredAge(RuleCatalog rules,string civilization,EntityDef unit)
        {
            var civ=string.IsNullOrEmpty(civilization)?null:rules.Civ(civilization);
            var age=civ?.unitAges?.FirstOrDefault(x=>x.unit==unit.id);
            return age==null?unit.age:age.age;
        }
    }
}
