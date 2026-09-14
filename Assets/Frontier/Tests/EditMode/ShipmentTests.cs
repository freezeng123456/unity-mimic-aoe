using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ShipmentTests
    {
        World world;
        [SetUp] public void SetUp(){world=new World(Catalog.Load(),314159,"coast","coast",3);}
        [Test] public void OneTimeSettlersCardConsumesOnlyOneToken(){var p=world.Players[0];p.deck.Clear();p.deck.Add("settlers");p.shipmentTokens=2;Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item="settlers"}).accepted);Assert.AreEqual(1,p.shipmentTokens);Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item="settlers"}).accepted);Assert.AreEqual(1,p.shipmentTokens);}
        [Test] public void ShipmentWaitsWithoutRecipientAndArrivesOnceAfterTownRebuilt(){var p=world.Players[0];p.deck.Clear();p.deck.Add("settlers");p.shipmentTokens=1;foreach(var e in world.Entities)if(e.owner==0&&(e.def.id=="town"||e.def.id=="tower"))world.Remove(e);int before=world.Entities.Count;Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item="settlers"}).accepted);world.Tick(40);Assert.AreEqual(1,p.shipments.Count);Assert.AreEqual(before,world.Entities.Count);world.Spawn("town",0,new Vector2(20,20));world.Tick(1);Assert.AreEqual(0,p.shipments.Count);int after=world.Entities.Count;world.Tick(1);Assert.AreEqual(after,world.Entities.Count);}
        [Test] public void UnitShipmentWaitsForPopulationThenArrives(){var p=world.Players[0];p.deck.Clear();p.deck.Add("pikes");p.age=2;p.shipmentTokens=1;p.populationCap=p.usedPopulation;Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item="pikes"}).accepted);world.Tick(40);Assert.AreEqual(1,p.shipments.Count);world.Spawn("house",0,new Vector2(30,30));world.Tick(1);Assert.AreEqual(0,p.shipments.Count);}
        [Test] public void CrateShipmentCreatesCrateWithoutDirectCredit(){var p=world.Players[0];p.deck.Clear();p.deck.Add("food_crates");p.shipmentTokens=1;float food=p.resources.food;int crates=0;foreach(var e in world.Entities)if(e.def.id=="crate_food")crates++;Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item="food_crates"}).accepted);world.Tick(36);Assert.AreEqual(food,p.resources.food);int after=0;foreach(var e in world.Entities)if(e.def.id=="crate_food"&&e.Alive&&e.remaining>0)after++;Assert.Greater(after,crates);}
        static bool present(World w,string id){foreach(var s in w.Players[0].shipments)if(s.card==id)return true;return false;}
    }
}
