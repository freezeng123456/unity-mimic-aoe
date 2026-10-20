using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ConfluenceContent082Tests
    {
        static World Opening()=>new World(Catalog.Load(),42,"coast","coast",5,MapTopology.Tributaries);
        static List<Vector2> Exact(GridNavigation nav,Vector2 from,Vector2 to,float radius)
        {
            Assert.IsTrue(nav.IsWalkable(from,radius),"start "+from+" radius "+radius);
            Assert.IsTrue(nav.IsWalkable(to,radius),"goal "+to+" radius "+radius);
            var path=nav.ClearLine(from,to,radius)?new List<Vector2>{to}:nav.FindPath(from,to,radius);
            Assert.Greater(path.Count,0);Assert.Less(Vector2.Distance(path.Last(),to),.001f);
            var previous=from;
            foreach(var p in path){Assert.IsTrue(nav.IsWalkable(p,radius));Assert.IsTrue(nav.ClearLine(previous,p,radius));previous=p;}
            return path;
        }
        [Test] public void EntireInitialWorldRetainsTheActualOldResourcesEventsEconomyFogAndNavigation()
        {
            Assert.AreEqual("0.25.0",Application.version);var w=Opening();
            // Actual GameplayBaseline1, installed old0.24 instrumented source
            // d1c1ba2c...; this hashes the full snapshot, not selected budgets.
            Assert.AreEqual("402e03c1a484d71b3c359234e1374b60b6f16eb3aa770b4dd392c3f5a58eeced",SnapshotCodec.Hash(JsonUtility.ToJson(w.CaptureState())));
            Assert.AreEqual(135,w.Entities.Count);Assert.AreEqual(0,w.Time);Assert.IsFalse(w.Layout.RequiresTransport);
            Assert.AreEqual("ad70350d6ed009c853117f4d4beafaa4c9d5d6bbd55e80da72dff02c07e4243b",SnapshotCodec.RulesHash(w.Rules));
            var mine=ConfluenceContent082.NorthMine;Assert.AreEqual(mine.Position,w.Get(103).position);Assert.AreEqual(mine.Definition,w.Get(103).def.id);Assert.AreEqual(1200,w.Get(103).remaining);
            for(int i=0;i<4;i++){var wood=ConfluenceContent082.NorthWood(i);Assert.AreEqual(wood.Position,w.Get(109+i).position);Assert.AreEqual(wood.Definition,w.Get(109+i).def.id);Assert.AreEqual(1500,w.Get(109+i).remaining);}
        }
        [Test] public void BothNamedRequestsKeepExactLargestPassengerExitsAndExistingResourceAndTradeConnections()
        {
            var w=Opening();var largest=w.Rules.entities.Where(d=>d.kind==EntityKind.Unit&&!d.waterborne&&(d.tags&Tags.Artillery)!=0).OrderByDescending(d=>d.radius).First();
            Assert.AreEqual("great_bombard",largest.id);Assert.AreEqual(1.05f,largest.radius,.00001f);
            var worker=w.Rules.Entity("villager");var ship=w.Spawn("transport_ship",0,w.Layout.FishPoints[0]);
            foreach(var request in new[]{ConfluenceContent082.SouthLanding,ConfluenceContent082.NorthLanding})
            {
                Assert.IsTrue(w.TryTransportShore(ship,request,largest.radius,out var land,out var berth));Assert.AreEqual(request,land);
                Exact(w.WaterNavigation,ship.position,berth,ship.def.radius);
                for(int player=0;player<2;player++)
                {
                    CollectionAssert.AreEqual(new[]{ConfluenceContent082.SouthLanding,ConfluenceContent082.NorthLanding},w.Layout.LandingSites[player]);
                    var town=w.Entities.Single(e=>e.owner==player&&e.def.id=="town");
                    foreach(var d in new[]{worker,largest}){Assert.IsTrue(w.FindSpawnPositions(town,1,d,out var points));Exact(w.Navigation,points[0],land,d.radius);}
                }
                Assert.IsFalse(w.IsWater(land)||w.IsBridge(land)||w.IsBridgeApproach(land));
            }
            foreach(var d in new[]{worker,largest})
            {
                Exact(w.Navigation,ConfluenceContent082.NorthLanding,ConfluenceContent082.NorthMine.Position,d.radius);
                for(int i=0;i<4;i++)Exact(w.Navigation,ConfluenceContent082.NorthLanding,ConfluenceContent082.NorthWood(i).Position,d.radius);
                Exact(w.Navigation,ConfluenceContent082.SouthLanding,w.Get(119).position,d.radius);
            }
            Assert.AreEqual(new Vector2(30,55),w.Get(119).position);Assert.AreEqual(0,w.Time);
        }
        [Test] public void ActualLandDetourUsesBothMainRiverBridgesRatherThanAnInventedUpstreamShortcut()
        {
            var w=Opening();var from=ConfluenceContent082.SouthLanding;var to=ConfluenceContent082.NorthLanding;
            foreach(string id in new[]{"villager","cannon","great_bombard"})
            {
                var d=w.Rules.Entity(id);var path=Exact(w.Navigation,from,to,d.radius);float length=0;var previous=from;var bridges=new HashSet<int>();
                foreach(var p in path)
                {
                    float distance=Vector2.Distance(previous,p);length+=distance;
                    int steps=Mathf.CeilToInt(distance/.25f);
                    for(int i=0;i<=steps;i++)for(int bridge=0;bridge<w.Layout.Bridges.Length;bridge++)if(w.Layout.Bridges[bridge].Contains(Vector2.Lerp(previous,p,(float)i/Mathf.Max(1,steps))))bridges.Add(bridge);
                    previous=p;
                }
                Assert.Greater(length,170);Assert.Less(length,220);CollectionAssert.AreEquivalent(new[]{0,1},bridges);
                TestContext.WriteLine($"Confluence082 exact geometric detour {id} {length:R}m; direct={Vector2.Distance(from,to):R}m; not actual travel time or a pure speed benchmark");
            }
        }
        [Test] public void SurveyedDockPreservesEveryMainAndBranchFishRouteForAllCurrentVesselSizes()
        {
            var w=Opening();var p=ConfluenceContent082.Dock;
            Assert.AreEqual("Requires age 2",w.CanBuild(0,"dock",p));Assert.AreEqual("",w.ScenarioPlacementReason("dock",p));
            var dock=w.Spawn("dock",0,p); // Explicit geometry fixture, not paid construction.
            foreach(string id in new[]{"fishing_boat","transport_ship","frigate"})
            {
                var d=w.Rules.Entity(id);Assert.IsTrue(w.FindSpawnPositions(dock,1,d,out var exits));var exit=exits[0];
                Assert.IsTrue(w.WaterNavigation.IsWalkable(exit,d.radius));
                foreach(var fish in w.Layout.FishPoints)Exact(w.WaterNavigation,exit,fish,d.radius);
                for(int i=0;i<w.Layout.FishPoints.Length;i++)for(int j=0;j<w.Layout.FishPoints.Length;j++)if(i!=j)Exact(w.WaterNavigation,w.Layout.FishPoints[i],w.Layout.FishPoints[j],d.radius);
            }
        }
        [Test] public void ConfluenceAnchorsAreGatedAwayFromOtherAuthoredAndGeneratedLayouts()
        {
            Assert.IsTrue(ConfluenceContent082.Applies(Opening().Layout));
            foreach(string id in MapTopology.All.Where(id=>id!=MapTopology.Tributaries))Assert.IsFalse(ConfluenceContent082.Applies(MapLayout.Create(id,42)));
            Assert.Throws<ArgumentOutOfRangeException>(()=>ConfluenceContent082.NorthWood(4));
            Assert.Throws<ArgumentOutOfRangeException>(()=>ConfluenceContent082.Landing(-1));
        }
    }
}
