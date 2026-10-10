using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Controlled capacity fixtures, not a normal economic-opening acceptance.
    // Existing definitions, slot costs and public naval commands remain unchanged.
    public sealed class TransportCargo079Tests
    {
        sealed class Fixture
        {
            public readonly World World;
            public readonly EntityState Ship;
            public readonly EntityState[] Passengers;
            public Fixture()
            {
                World=new World(SnapshotCodec.Clone(Catalog.Load()),42,"coast","coast",5,MapTopology.River);
                foreach(var entity in World.Entities.Where(e=>e.def.id!="town"&&e.def.id!="trade_socket").ToArray())World.Remove(entity);
                Ship=World.Spawn("transport_ship",0,new Vector2(64,25));
                string[] definitions={"cannon","hussar","musketeer","explorer"};
                Passengers=definitions.Select((id,index)=>World.Spawn(id,0,new Vector2(49,19+index*4))).ToArray();
                World.Fog.Tick(World.Entities);
            }
            public void Reserve(bool repeatActor=false)
            {
                var ids=Passengers.Select(e=>e.id).ToList();if(repeatActor)ids.Add(Passengers[0].id);
                var result=World.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=ids.ToArray(),target=Ship.id,point=new Vector2(55,25)});
                Assert.IsTrue(result.accepted,result.reason);
            }
            public TransportCargoSummary Summary()
            {Assert.IsTrue(World.TryGetTransportCargoSummary(0,Ship,out var summary));return summary;}
        }
        static void Counts(TransportCargoSummary summary,int aboardSlots,int aboardUnits,int incomingSlots,int incomingUnits)
        {
            Assert.AreEqual(aboardSlots,summary.AboardSlots);Assert.AreEqual(aboardUnits,summary.AboardUnits);
            Assert.AreEqual(incomingSlots,summary.IncomingSlots);Assert.AreEqual(incomingUnits,summary.IncomingUnits);
            Assert.AreEqual(14,summary.Capacity);
        }

        [Test] public void MixedReservationsUsePopulationSlotsAndDeduplicateRepeatedActorsAndQueuedOrders()
        {
            var f=new Fixture();Counts(f.Summary(),0,0,0,0);f.Reserve(true);
            Assert.AreEqual(4,f.World.TransportSlotCost(f.Passengers[0]));Assert.AreEqual(2,f.World.TransportSlotCost(f.Passengers[1]));
            Assert.AreEqual(1,f.World.TransportSlotCost(f.Passengers[2]));Assert.AreEqual(0,f.Passengers[3].def.population);
            Assert.AreEqual(1,f.World.TransportSlotCost(f.Passengers[3]));Counts(f.Summary(),0,0,8,4);
            // A stale repeated queue entry must not display a second reservation.
            var passenger=f.Passengers[0];passenger.orders.Enqueue(new UnitOrder(OrderKind.Embark,passenger.orders.Peek().point,f.Ship.id));
            Counts(f.Summary(),0,0,8,4);
            Assert.AreEqual("Cargo 0 + 8 incoming /14 slots · 0 aboard · 4 incoming",FrontierHud.FormatTransportCargoSummary079(f.Summary()));
        }

        [TestCase("stop")][TestCase("death")][TestCase("replace")]
        public void ReservationDisappearsWhenItsPassengerStopsDiesOrReplacesItsOrder(string action)
        {
            var f=new Fixture();f.Reserve();var actor=f.Passengers[0];Counts(f.Summary(),0,0,8,4);
            if(action=="death")f.World.Remove(actor);
            else
            {
                var result=f.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{actor.id},
                    order=action=="stop"?OrderKind.Stop:OrderKind.Move,point=new Vector2(45,20)});
                Assert.IsTrue(result.accepted,result.reason);
            }
            Counts(f.Summary(),0,0,4,3);
        }

        [Test] public void PhysicallyBoardedPassengersTransferSlotsRatherThanCountingTwice()
        {
            var f=new Fixture();f.Reserve();int used=f.World.Players[0].usedPopulation;var resources=f.World.Players[0].resources;
            bool sawAboard=false;
            for(int step=0;step<900&&f.Passengers.Any(e=>e.garrisonedIn!=f.Ship.id);step++)
            {
                f.World.Tick(.1f);var summary=f.Summary();
                Assert.AreEqual(8,summary.AboardSlots+summary.IncomingSlots);Assert.AreEqual(4,summary.AboardUnits+summary.IncomingUnits);
                sawAboard|=summary.AboardUnits>0;
            }
            Assert.IsTrue(sawAboard);Assert.IsTrue(f.Passengers.All(e=>e.garrisonedIn==f.Ship.id));
            Counts(f.Summary(),8,4,0,0);Assert.AreEqual(f.World.TransportLoad(f.Ship),f.Summary().AboardSlots);
            Assert.AreEqual(used,f.World.Players[0].usedPopulation);Assert.AreEqual(resources,f.World.Players[0].resources);
            Assert.AreEqual(4,f.World.Events.Count(e=>e.kind=="Embarked"&&e.target==f.Ship.id));
        }

        [Test] public void StoppingTheShipReleasesPendingBoardingThroughTheExistingTick()
        {
            var f=new Fixture();f.Reserve();
            var result=f.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{f.Ship.id},order=OrderKind.Stop});
            Assert.IsTrue(result.accepted,result.reason);f.World.Tick(.1f);
            Counts(f.Summary(),0,0,0,0);Assert.IsTrue(f.Passengers.All(e=>e.Alive&&e.garrisonedIn==0));
        }

        [Test] public void QueriesAndFormattingLeaveTheEntireWorldSnapshotUnchanged()
        {
            var f=new Fixture();f.Reserve();string before=JsonUtility.ToJson(f.World.CaptureState());
            int landVersion=f.World.Navigation.Version,waterVersion=f.World.WaterNavigation.Version;
            for(int i=0;i<10;i++)
            {Counts(f.Summary(),0,0,8,4);Assert.IsNotEmpty(FrontierHud.FormatTransportCargoSummary079(f.Summary()));}
            Assert.AreEqual(before,JsonUtility.ToJson(f.World.CaptureState()));
            Assert.AreEqual(landVersion,f.World.Navigation.Version);Assert.AreEqual(waterVersion,f.World.WaterNavigation.Version);
        }

        [Test] public void ForeignInvalidAndForgedShipsDiscloseNoCargoAndForeignStaleOrdersAreExcluded()
        {
            var f=new Fixture();f.Reserve();var enemyShip=f.World.Spawn("transport_ship",1,new Vector2(64,100));
            Assert.IsFalse(f.World.IsVisible(0,enemyShip));
            foreach(var ship in new[]{enemyShip,f.Passengers[0],null,new EntityState{id=f.Ship.id,owner=0,def=f.Ship.def,hp=f.Ship.hp}})
            {
                Assert.IsFalse(f.World.TryGetTransportCargoSummary(0,ship,out var hidden));
                Assert.AreEqual(0,hidden.AboardSlots+hidden.AboardUnits+hidden.IncomingSlots+hidden.IncomingUnits+hidden.Capacity);
            }
            Assert.IsFalse(f.World.TryGetTransportCargoSummary(-1,f.Ship,out _));
            Assert.IsFalse(f.World.TryGetTransportCargoSummary(f.World.Players.Length,f.Ship,out _));
            // This malformed enemy queue is not created by a legal Embark command;
            // an own-ship UI query must not enumerate it as incoming enemy cargo.
            var enemy=f.World.Spawn("musketeer",1,new Vector2(100,100));
            enemy.orders.Enqueue(new UnitOrder(OrderKind.Embark,new Vector2(55,25),f.Ship.id));
            Counts(f.Summary(),0,0,8,4);
            string before=JsonUtility.ToJson(f.World.CaptureState());f.World.TryGetTransportCargoSummary(0,enemyShip,out _);
            Assert.AreEqual(before,JsonUtility.ToJson(f.World.CaptureState()));
            f.World.Remove(f.Ship);Assert.IsFalse(f.World.TryGetTransportCargoSummary(0,f.Ship,out _));
        }
    }
}
