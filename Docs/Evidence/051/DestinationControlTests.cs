using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class DestinationControlTests
    {
        World w;
        [SetUp] public void SetUp()
        {
            w=new World(Catalog.Load(),5101,"coast","coast",5);
            foreach(var e in w.Entities.Where(World.Guardian).ToArray())w.Remove(e);
            w.Players[0].resources=new Cost(1000,2000,1000);
        }
        void Order(EntityState[] units,OrderKind kind,Vector2 point,bool append=false)
        {
            var r=w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=units.Select(e=>e.id).ToArray(),order=kind,point=point,append=append});Assert.IsTrue(r.accepted,r.reason);
        }
        EntityState PaidHouse(Vector2 point)
        {
            var worker=w.Spawn("villager",0,point+Vector2.up*3);w.Fog.Tick(w.Entities);float wood=w.Players[0].resources.wood;
            var r=w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{worker.id},item="house",point=point});Assert.IsTrue(r.accepted,r.reason);
            var b=w.Get(worker.orders.Peek().target);Assert.Less(w.Players[0].resources.wood,wood);return b;
        }
        void Step(int seconds,EntityState[] units)
        {
            for(int i=0;i<seconds*10;i++)
            {
                w.Tick(.1f);
                foreach(var e in units)Assert.IsTrue(w.Navigation.IsWalkable(e.position,e.def.radius),"Unit crossed an obstacle: "+e.id+" "+e.position);
            }
        }
        string State(EntityState e)=>$"{e.id} {e.def.id} position={e.position} orders={e.orders.Count} status={e.status} path={string.Join(",",e.path)} requested={(e.orders.Count>0?e.orders.Peek().point.ToString():"done")}";
        [TestCase(OrderKind.Move)][TestCase(OrderKind.AttackMove)]
        public void PaidBuildingCoveringDestinationAllowsArrivalBesideIt(OrderKind kind)
        {
            var unit=w.Spawn("pike",0,new Vector2(36,60));var units=new[]{unit};var point=new Vector2(52,60);
            Order(units,kind,point);w.Tick(.1f);var original=unit.orders.Peek().point;var house=PaidHouse(point);
            Assert.IsFalse(w.Navigation.IsWalkable(original,unit.def.radius));Step(45,units);
            Assert.Less(Vector2.Distance(unit.position,original),5,"Replacement destination must remain local");
            Assert.AreEqual(0,unit.orders.Count,State(unit));Assert.IsTrue(house.Alive);
        }
        [Test] public void BlockedFirstWaypointCompletesBeforeNextShiftWaypointRuns()
        {
            var unit=w.Spawn("pike",0,new Vector2(36,60));var units=new[]{unit};var first=new Vector2(52,60);var last=new Vector2(48,80);
            Order(units,OrderKind.Move,first);Order(units,OrderKind.Move,last,true);Assert.AreEqual(2,unit.orders.Count);PaidHouse(first);
            bool approached=false;
            for(int i=0;i<900;i++)
            {
                w.Tick(.1f);if(Vector2.Distance(unit.position,first)<5)approached=true;
                Assert.IsTrue(w.Navigation.IsWalkable(unit.position,unit.def.radius));
            }
            Assert.IsTrue(approached,"Do not silently discard the first queued intention");
            Assert.Less(Vector2.Distance(unit.position,last),.5f,State(unit));Assert.AreEqual(0,unit.orders.Count);
        }
        [Test] public void FiftyMixedUnitsKeepDistinctReachableGoalsAfterTwoFoundationsAppear()
        {
            var kinds=new[]{"pike","musketeer","hussar","dragoon","cannon"};var slots=w.Navigation.FormationSlots(new Vector2(40,60),50,2.8f);
            var units=slots.Select((p,i)=>w.Spawn(kinds[i%5],0,p)).ToArray();Order(units,OrderKind.Move,new Vector2(86,54));
            var original=units.ToDictionary(e=>e.id,e=>e.orders.Peek().point);
            var a=PaidHouse(original[units[1].id]);var b=PaidHouse(original[units[8].id]);
            int affected=units.Count(e=>!w.Navigation.IsWalkable(original[e.id],e.def.radius));Assert.GreaterOrEqual(affected,2);
            var builders=w.Entities.Where(e=>e.Worker&&e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Build&&(e.orders.Peek().target==a.id||e.orders.Peek().target==b.id)).ToArray();
            Assert.AreEqual(2,builders.Length);
            Order(builders,OrderKind.Move,new Vector2(85,75));
            Step(120,units);
            var missed=units.Where(e=>e.orders.Count>0).ToArray();
            Assert.AreEqual(0,units[4].orders.Count,State(units[4]));
            Assert.AreEqual(0,units[23].orders.Count,State(units[23]));
            Assert.IsTrue(missed.Contains(units[1])&&missed.Contains(units[8]),"Both physically covered destinations still fail without builders nearby");

            Assert.IsTrue(a.Alive&&b.Alive);
        }
    }
}
