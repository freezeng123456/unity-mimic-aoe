using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class NeutralAlliesTests
    {
        static World Fixture(int index,out NeutralAllianceState alliance,out EntityState contact)
        {
            var rules=SnapshotCodec.Clone(Catalog.Load());Assert.AreEqual(3,rules.neutralAllies.Length,"Three authored original settlements must be configured");
            foreach(var unit in rules.entities){unit.damage=0;unit.meleeDamage=0;unit.siegeDamage=0;}
            foreach(var d in rules.neutralAllies){d.allianceSeconds=.5f;d.lossSeconds=.5f;rules.Entity(d.unit).time=.5f;rules.Tech(d.tech).time=.5f;}
            var world=new World(rules,maxAge:5);
            // Controlled fixture: completed post/resources isolate the actual
            // paid service commands. This is not normal-opening play evidence.
            foreach(var e in world.Entities.Where(e=>e.def.kind==EntityKind.Unit).ToArray())world.Remove(e);
            world.Players[0].age=5;world.Players[0].resources=new Cost(5000,5000,5000);
            alliance=world.NeutralAllies[index];var site=world.Get(alliance.site);world.Spawn("trade_post",0,site.position);
            contact=world.Spawn("villager",0,world.Navigation.NearestWalkable(site.position+new Vector2(-5,0),.4f));
            world.Fog.Tick(world.Entities);return world;
        }
        static void Tick(World world,int steps=8){for(int i=0;i<steps;i++)world.Tick(.1f);}
        static CommandResult Service(World world,NeutralAllianceState state,CommandKind kind,int player=0)
            =>world.Issue(new GameCommand{player=player,kind=kind,target=state.site});
        static void Alliance(World world,NeutralAllianceState state)
        {var result=Service(world,state,CommandKind.NeutralAlliance);Assert.IsTrue(result.accepted,result.reason);Tick(world);Assert.AreEqual(0,state.owner);}

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void EachOriginalAllyRequiresPaidAllianceThenPaidTroopsAndUniqueResearch(int index)
        {
            var world=Fixture(index,out var state,out _);var definition=world.NeutralDefinition(state.id);var player=world.Players[0];
            Assert.IsFalse(Service(world,state,CommandKind.NeutralRecruit).accepted);
            Cost before=player.resources;Alliance(world,state);
            Assert.AreEqual(before.coin-definition.allianceCost.coin,player.resources.coin,.001f);
            Assert.AreEqual(0,world.Entities.Count(e=>e.Alive&&e.def.id==definition.unit),"Alliance completion never grants a free unit");
            Assert.IsFalse(Service(world,state,CommandKind.NeutralAlliance).accepted);
            Assert.IsTrue(Service(world,state,CommandKind.NeutralRecruit).accepted);Assert.Greater(player.reservedPopulation,0);
            var cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));
            Tick(world);Tick(cold);
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(cold.CaptureState()),"Paid recruit and actual birth continue identically after cold JSON recovery");
            var recruited=world.Entities.Single(e=>e.Alive&&e.def.id==definition.unit);Assert.AreEqual(0,recruited.owner);Assert.AreEqual(0,player.reservedPopulation);
            Assert.IsTrue(Service(world,state,CommandKind.NeutralResearch).accepted);Assert.IsFalse(Service(world,state,CommandKind.NeutralResearch).accepted);Tick(world);
            Assert.IsTrue(player.technologies.Contains(definition.tech));Assert.AreEqual(1,world.Events.Count(e=>e.kind=="Technology"&&e.message.StartsWith(definition.tech+" ")));
            Assert.IsFalse(Service(world,state,CommandKind.NeutralResearch).accepted);
            Assert.AreEqual(3,world.Events.Count(e=>e.kind=="Payment"&&e.entity==state.site),"Alliance, soldier and technology have three real economy transactions");
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="NeutralRecruitComplete"));
        }
        [Test] public void FogDistanceHousingAndAffordabilityCannotBeBypassedBySettlementId()
        {
            var world=Fixture(0,out var state,out var contact);world.Remove(contact);
            Assert.IsFalse(Service(world,state,CommandKind.NeutralAlliance).accepted,"Owning a visible post is insufficient without contact");
            var site=world.Get(state.site);contact=world.Spawn("villager",0,site.position+new Vector2(-5,0));
            world.Fog.RestoreState(new int[32768]);Assert.IsFalse(Service(world,state,CommandKind.NeutralAlliance).accepted,"Current visibility is mandatory even when the settlement ID is known");world.Fog.Tick(world.Entities);
            world.Players[0].resources=new Cost();Assert.IsFalse(Service(world,state,CommandKind.NeutralAlliance).accepted);
            world.Players[0].resources=new Cost(5000,5000,5000);world.Fog.Tick(world.Entities);Alliance(world,state);
            world.Players[0].populationCap=world.Players[0].usedPopulation;
            Assert.IsFalse(Service(world,state,CommandKind.NeutralRecruit).accepted);
            Assert.IsFalse(Service(world,state,CommandKind.NeutralResearch,1).accepted,"Opponent cannot purchase from an owned post by knowing its ID");
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void AlliedTechnologyChangesActualUnitStatsOnceAndRetainsHealthFraction(int index)
        {
            var world=new World(Catalog.Load(),maxAge:5);var definition=world.Rules.neutralAllies[index];var unit=world.Spawn(definition.unit,0,new Vector2(28,30));
            float health=world.MaxHp(unit),speed=world.Speed(unit),attack=world.Attack(unit);unit.hp=health*.4f;
            world.ApplyTechnology(0,definition.tech);
            Assert.IsTrue(world.MaxHp(unit)>health||world.Speed(unit)>speed||world.Attack(unit)>attack,"Each technology must alter real simulation statistics");
            Assert.AreEqual(.4f,unit.hp/world.MaxHp(unit),.0001f);float afterHealth=world.MaxHp(unit),afterSpeed=world.Speed(unit),afterAttack=world.Attack(unit);
            world.ApplyTechnology(0,definition.tech);
            Assert.AreEqual(afterHealth,world.MaxHp(unit));Assert.AreEqual(afterSpeed,world.Speed(unit));Assert.AreEqual(afterAttack,world.Attack(unit));
        }
        [Test] public void HostileOccupationPausesWorkThenLosesControlAndReleasesOnlyReservedPopulation()
        {
            var world=Fixture(0,out var state,out var contact);Alliance(world,state);var definition=world.NeutralDefinition(state.id);
            Assert.IsTrue(Service(world,state,CommandKind.NeutralRecruit).accepted);Cost paidBalance=world.Players[0].resources;
            var site=world.Get(state.site);var enemy=world.Spawn("musketeer",1,site.position+new Vector2(5,0));
            Tick(world,2);Assert.IsTrue(state.contested);Assert.AreEqual(0,state.jobs[0].elapsed);Assert.AreEqual(0,state.lossProgress,"Friendly contact contests the hostile occupation");
            world.Remove(contact);var cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));Tick(world);Tick(cold);
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(cold.CaptureState()));
            Assert.AreEqual(-1,state.owner);Assert.AreEqual(0,state.jobs.Count);Assert.AreEqual(0,world.Players[0].reservedPopulation);
            Assert.AreEqual(paidBalance.coin,world.Players[0].resources.coin,.001f);Assert.AreEqual(0,world.Entities.Count(e=>e.Alive&&e.def.id==definition.unit));
            world.Remove(enemy);world.Spawn("villager",0,site.position+new Vector2(-5,0));world.Fog.Tick(world.Entities);Alliance(world,state);
            Assert.AreEqual(0,world.Entities.Count(e=>e.Alive&&e.def.id==definition.unit),"Reacquiring control cannot replay a forfeited job");
        }
        [Test] public void DestroyedPostEndsPendingTreatyAndColdSaveCannotRepeatCompletion()
        {
            var world=Fixture(1,out var state,out _);Assert.IsTrue(Service(world,state,CommandKind.NeutralAlliance).accepted);Tick(world,2);
            world.Remove(world.Get(state.post));Tick(world);Assert.AreEqual(-1,state.claimant);Assert.AreEqual(-1,state.owner);
            var cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));Tick(cold);
            Assert.IsFalse(cold.Events.Any(e=>e.kind=="NeutralAllianceComplete"));
            var invalid=world.CaptureState();invalid.neutralAllies[0].site=state.site;
            Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,invalid),"Duplicate settlement state must not duplicate its services");
        }
    }
}
