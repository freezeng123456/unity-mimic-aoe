using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Actual old0.23 resource rows were captured before production edits by
    // ProbeBaseline1 (JSON SHA dab51f3758556926dcc1030b7f8bff45781183158841c3a4b2d3b357ed5edacd).
    // Geometry queries below are controlled fixtures, not paid/native claims.
    public sealed class TropicalContent081Tests
    {
        [Serializable] sealed class Row {public int id,owner;public string definition;public Vector2 position;public float remaining,radius;}
        [Serializable] sealed class Rows {public Row[] rows;}
        static World Opening()=>new World(Catalog.Load(),42,"coast","coast",3,MapTopology.Tropical);
        static Row Describe(EntityState e)=>new Row{id=e.id,owner=e.owner,definition=e.def.id,position=e.position,remaining=e.remaining,radius=e.def.radius};
        static void Check(World w,int id,string definition,Vector2 position)
        {var e=w.Get(id);Assert.IsNotNull(e);Assert.AreEqual(definition,e.def.id);Assert.AreEqual(position,e.position,"Old safe forward resource "+id);}
        static bool Exact(GridNavigation nav,Vector2 from,Vector2 to,float radius)
        {if(!nav.IsWalkable(from,radius)||!nav.IsWalkable(to,radius))return false;if(nav.ClearLine(from,to,radius))return true;var path=nav.FindPath(from,to,radius);return path.Count>0&&Vector2.Distance(path.Last(),to)<.001f;}
        [Test] public void BothHomeRecipesAndExistingSafeForwardResourcesAreExactlyTheActualOldOpening()
        {
            Assert.AreEqual("0.25.0",Application.version);var w=Opening();
            Assert.AreEqual("e7a6e32b927e15b27701b9441464f50d749d705ab36ad331fcaa3312ed25bf6d",SnapshotCodec.Hash(JsonUtility.ToJson(new Rows{rows=w.Entities.Where(e=>e.id>=1&&e.id<=36).Select(Describe).ToArray()})));
            Assert.AreEqual("b09870176fbae42cb0053e01b5b7ff9a193325697609360b18e1c00ef4906b1b",SnapshotCodec.Hash(JsonUtility.ToJson(new Rows{rows=w.Entities.Where(e=>e.id>=52&&e.id<=87).Select(Describe).ToArray()})));
            Check(w,37,"mine",new Vector2(44f,35f));
            Check(w,38,"tree",new Vector2(38f,49f));
            Check(w,39,"tree",new Vector2(39.0999985f,51f));
            Check(w,40,"tree",new Vector2(49.2000008f,53f));
            Check(w,41,"tree",new Vector2(41f,53.0999985f));
            Check(w,42,"tree",new Vector2(38.0999985f,53.0999985f));
            Check(w,43,"tree",new Vector2(46.2000008f,55.0999985f));
            Check(w,44,"tree",new Vector2(40f,55.2000008f));
            Check(w,45,"tree",new Vector2(43.0999985f,56.2000008f));
            Check(w,46,"tree",new Vector2(49.2000008f,55.2000008f));
            Check(w,47,"deer",new Vector2(33f,53f));
            Check(w,48,"deer",new Vector2(35f,52f));
            Check(w,49,"deer",new Vector2(36f,54f));
            Check(w,50,"deer",new Vector2(38f,56f));
            Check(w,51,"deer",new Vector2(41f,58f));
            Check(w,88,"mine",new Vector2(82f,93f));
            Check(w,89,"tree",new Vector2(87f,81f));
            Check(w,90,"tree",new Vector2(84.9000015f,80f));
            Check(w,91,"tree",new Vector2(83.8000031f,82f));
            Check(w,92,"tree",new Vector2(87f,77.9000015f));
            Check(w,93,"tree",new Vector2(88.9000015f,78.9000015f));
            Check(w,94,"tree",new Vector2(85.8000031f,82.9000015f));
            Check(w,95,"tree",new Vector2(88f,75.8000031f));
            Check(w,96,"tree",new Vector2(88.9000015f,73.8000031f));
            Check(w,97,"tree",new Vector2(90.8000031f,76.8000031f));
            Check(w,98,"deer",new Vector2(95f,75f));
            Check(w,99,"deer",new Vector2(93f,74f));
            Check(w,100,"deer",new Vector2(91f,73f));
            Check(w,101,"deer",new Vector2(89f,71f));
            Check(w,102,"deer",new Vector2(93f,77f));
            var p=w.Players[0];Assert.AreEqual(new Cost(300,300,100),p.resources);Assert.AreEqual(1,p.age);Assert.AreEqual(6,w.Entities.Count(e=>e.owner==0&&e.Worker));
            Assert.AreEqual("ad70350d6ed009c853117f4d4beafaa4c9d5d6bbd55e80da72dff02c07e4243b",SnapshotCodec.RulesHash(w.Rules));Assert.IsFalse(w.Layout.RequiresTransport);
        }
        [Test] public void AllFiniteResourceBudgetsAreRetainedAndOnlyTheCentralOutpostBudgetIsReassigned()
        {
            var w=Opening();
            Budget(w,"berry",4,4000f);
            Budget(w,"crate_food",2,600f);
            Budget(w,"crate_wood",2,600f);
            Budget(w,"deer",20,28000f);
            Budget(w,"fish",6,15000f);
            Budget(w,"mine",6,7200f);
            Budget(w,"trade_socket",3,0f);
            Budget(w,"treasure_coin",2,0f);
            Budget(w,"treasure_wood",2,0f);
            Budget(w,"treasure_xp",2,0f);
            Budget(w,"tree",62,93000f);
            Assert.AreEqual(6,w.Entities.Count(e=>e.def.id=="fish"));
            Check(w,113,"fish",new Vector2(53.9989433f,20f));
            Check(w,114,"fish",new Vector2(72.1708069f,56f));
            Check(w,115,"fish",new Vector2(73.6421814f,74f));
            Check(w,116,"fish",new Vector2(52.6133652f,108f));
            Check(w,117,"fish",new Vector2(79.89608f,63f));
            Check(w,118,"fish",new Vector2(68.3394928f,79f));
            for(int bank=0;bank<2;bank++)
            {
                bool OwnBank(EntityState e)=>bank==0?w.Layout.WaterDirection(e.position).x>0:w.Layout.WaterDirection(e.position).x<0;
                Assert.AreEqual(3,w.Entities.Count(e=>e.def.id=="mine"&&OwnBank(e)),"Three actual mines per bank, not requested-coordinate symmetry");
                Assert.AreEqual(31,w.Entities.Count(e=>e.def.id=="tree"&&OwnBank(e)),"31 actual trees per bank");
                var mine=TropicalBayContent081.HeadlandMine(bank);var actual=w.Entities.Single(e=>e.def.id==mine.Definition&&e.position==mine.Position);Assert.AreEqual(1200,actual.remaining);
                for(int i=0;i<TropicalBayContent081.WoodCount;i++)
                {var wood=TropicalBayContent081.HeadlandWood(bank,i);var tree=w.Entities.Single(e=>e.def.id==wood.Definition&&e.position==wood.Position);Assert.AreEqual(1500,tree.remaining);Assert.IsFalse(w.IsWater(tree.position));}
            }
            Assert.AreEqual(4,TropicalBayContent081.WoodCount);Assert.AreEqual(0,w.Time);
        }
        [TestCase(0)] [TestCase(1)] public void BothNamedShoreRequestsPreserveActualLargestPassengerAndVesselPaths(int bank)
        {
            var w=Opening();var layout=w.Layout;var worker=w.Entities.First(e=>e.owner==bank&&e.Worker);float radius=w.Rules.Entity("cannon").radius;
            var launch=layout.FishPoints.First(p=>w.WaterNavigation.IsWalkable(p,w.Rules.Entity("transport_ship").radius));var ship=w.Spawn("transport_ship",bank,launch);
            Assert.AreEqual(2,layout.LandingSites[bank].Length);Assert.Greater(Vector2.Distance(layout.LandingSites[bank][0],layout.LandingSites[bank][1]),20);
            for(int i=0;i<2;i++)
            {
                var request=TropicalBayContent081.Landing(bank,i);Assert.AreEqual(request,layout.LandingSites[bank][i]);Assert.IsTrue(w.TryTransportShore(ship,request,radius,out var land,out var water));Assert.AreEqual(request,land,"Actual baseline selected exact request remains legal");
                Assert.IsTrue(Exact(w.Navigation,worker.position,land,worker.def.radius));Assert.IsTrue(Exact(w.Navigation,worker.position,land,radius));Assert.IsTrue(Exact(w.WaterNavigation,launch,water,ship.def.radius));
                Assert.IsFalse(w.IsWater(land)||w.IsBridge(land)||w.IsBridgeApproach(land));Assert.GreaterOrEqual(layout.DockSites.SelectMany(a=>a).Min(p=>Vector2.Distance(p,land)),9);
                if(i==1)
                {
                    var mine=TropicalBayContent081.HeadlandMine(bank);Assert.IsTrue(Exact(w.Navigation,land,mine.Position,radius));Assert.IsTrue(Exact(w.Navigation,land,mine.Position,worker.def.radius));
                    var socket=w.TradeSockets.Select(s=>w.Get(s.entity)).OrderBy(e=>(e.position-land).sqrMagnitude).First();Assert.IsTrue(Exact(w.Navigation,land,socket.position,radius));
                    for(int wood=0;wood<4;wood++)Assert.IsTrue(Exact(w.Navigation,land,TropicalBayContent081.HeadlandWood(bank,wood).Position,worker.def.radius));
                    TestContext.WriteLine($"bank={bank} namedForward={land} berth={water} mine={mine.Position} nearestSocket={socket.position}; exact endpoint paths, not physical gather or equal-opportunity proof");
                }
            }
            Assert.IsFalse(layout.RequiresTransport);Assert.AreEqual(0,w.Time);
        }
        static void Budget(World w,string definition,int count,float amount)
        {var entities=w.Entities.Where(e=>e.def.id==definition).ToArray();Assert.AreEqual(count,entities.Length,definition);Assert.AreEqual(amount,entities.Sum(e=>e.remaining),.001f,definition);}
    }
}
