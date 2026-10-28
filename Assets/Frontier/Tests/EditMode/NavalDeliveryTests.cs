using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class NavalDeliveryTests
    {
        static World Opening(){var w=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.River);w.Players[0].age=4;w.Players[0].shipmentTokens=1;return w;}
        static EntityState Dock(World w)=>w.Spawn("dock",0,new Vector2(55,24));
        [TestCase("naval_transport","transport_ship")]
        [TestCase("naval_frigate","frigate")]
        [TestCase("naval_monitor","monitor")]
        public void NavyCardWaitsForHarborAndHousingThroughColdRestoreThenLaunchesOnce(string card,string definition)
        {
            var w=Opening();var p=w.Players[0];p.deck.Clear();p.deck.Add(card);
            Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item=card}).accepted);
            Assert.AreEqual(1,p.shipmentTokens);var dock=Dock(w);
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item=card}).accepted);
            w.Remove(dock);w.Tick(46);Assert.AreEqual(1,p.shipments.Count);Assert.IsFalse(w.Entities.Any(e=>e.Alive&&e.def.id==definition));
            w=World.Restore(w.Rules,JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(w.CaptureState())));p=w.Players[0];Dock(w);
            p.populationCap=p.usedPopulation;w.Tick(.1f);Assert.AreEqual(1,p.shipments.Count,"Navy delivery must wait for housing");
            w.Spawn("house",0,new Vector2(35,25));w.Tick(.1f);
            var ship=w.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id==definition);
            Assert.IsTrue(w.Layout.IsWater(ship.position));Assert.IsTrue(w.WaterNavigation.IsWalkable(ship.position,ship.def.radius));
            Assert.AreEqual(0,p.shipments.Count);Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item=card}).accepted);
            w.Tick(5);Assert.AreEqual(1,w.Entities.Count(e=>e.Alive&&e.owner==0&&e.def.id==definition));
        }
        [Test] public void NavalShipmentCanArriveAtSurvivingHarborAfterTownLoss()
        {
            var w=Opening();var p=w.Players[0];p.deck.Clear();p.deck.Add("naval_transport");Dock(w);
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item="naval_transport"}).accepted);
            foreach(var town in w.Entities.Where(e=>e.owner==0&&e.def.id=="town").ToArray())w.Remove(town);
            w.Spawn("house",0,new Vector2(35,25));w.Tick(46);
            Assert.AreEqual(0,p.shipments.Count);Assert.AreEqual(1,w.Entities.Count(e=>e.Alive&&e.owner==0&&e.def.transportShip));
        }
        [Test] public void ShipCannotRestoreThroughAOrdinaryBuildingShelter()
        {
            var w=Opening();var town=w.Entities.First(e=>e.owner==0&&e.def.id=="town");var ship=w.Spawn("transport_ship",0,new Vector2(64,22));
            town.occupants.Add(ship.id);ship.garrisonedIn=town.id;
            Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,w.CaptureState()),"A forged land shelter must not place a vessel on a land exit");
        }
        [Test] public void LoadedPassengerRelationshipsAreValidatedBeforeRestoringWorld()
        {
            var w=Opening();var ship=w.Spawn("transport_ship",0,new Vector2(64,22));var unit=w.Spawn("pike",0,new Vector2(54,22));
            ship.occupants.Add(unit.id);unit.garrisonedIn=ship.id;unit.position=ship.position;
            var valid=w.CaptureState();Assert.DoesNotThrow(()=>World.Restore(w.Rules,valid));
            var duplicate=SnapshotCodec.Clone(valid);duplicate.entities.Single(e=>e.state.id==ship.id).occupants=new[]{unit.id,unit.id};
            Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,duplicate));
            var missing=SnapshotCodec.Clone(valid);missing.entities.Single(e=>e.state.id==ship.id).occupants=Array.Empty<int>();
            Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,missing));
            var hostile=SnapshotCodec.Clone(valid);hostile.entities.Single(e=>e.state.id==unit.id).state.owner=1;
            Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,hostile));
            var seaCargo=SnapshotCodec.Clone(valid);seaCargo.entities.Single(e=>e.state.id==unit.id).definition="fishing_boat";
            Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,seaCargo));
        }
    }
}
