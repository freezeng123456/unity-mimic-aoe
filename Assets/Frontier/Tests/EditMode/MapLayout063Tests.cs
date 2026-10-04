using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class MapLayout063Tests
    {
        private static readonly string[] NewMaps={MapTopology.Winding,MapTopology.Tropical,MapTopology.Tributaries,MapTopology.Estuary};
        [Test] public void MapRegistryHasDistinctNamesAndRejectsUnknownLayouts()
        {
            Assert.AreEqual(6,MapTopology.All.Length);Assert.AreEqual(6,MapTopology.All.Distinct().Count());
            foreach(var map in MapTopology.All){Assert.IsTrue(MapTopology.IsValid(map));Assert.IsNotEmpty(MapTopology.Name(map));Assert.AreEqual(MapTopology.IsRiverMap(map),MapLayout.Create(map).HasWater);}
            Assert.IsFalse(MapTopology.IsValid(null));Assert.IsFalse(MapTopology.IsValid("unknown"));Assert.Throws<ArgumentException>(()=>MapLayout.Create("unknown"));
        }
        [Test] public void LegacyBoundsBridgeRampsAndBankDirectionsRemainExact()
        {
            var layout=MapLayout.Create(MapTopology.River);
            Assert.AreEqual(MapTopology.WaterHeight,layout.WaterHeight);
            foreach(float z in new[]{20f,40,88,105})foreach(float x in new[]{49.5f,53.5f,57.5f,64,70.5f,74.5f,78.5f})
            {
                var point=new Vector2(x,z);Assert.AreEqual(MapTopology.IsWater(point),layout.IsWater(point));Assert.AreEqual(MapTopology.IsBridge(point),layout.IsBridge(point));
                Assert.AreEqual(MapTopology.IsBridgeApproach(point),layout.IsBridgeApproach(point));Assert.AreEqual(MapTopology.GroundHeight(point),layout.GroundHeight(point));Assert.AreEqual(0,layout.BankHeight(point));
            }
            Assert.AreEqual(Vector2.right,layout.WaterDirection(new Vector2(55,28)));Assert.AreEqual(Vector2.left,layout.WaterDirection(new Vector2(73,100)));
        }
        [TestCaseSource(nameof(NewMaps))] public void RiversAreCurvedDenselySampledAndHaveSafeBedAndSpawns(string map)
        {
            var layout=MapLayout.Create(map);var main=layout.Channels[0];
            Assert.AreEqual(-.4f,layout.WaterHeight,.00001f);
            Assert.Greater(main.Centers.Max(p=>p.x)-main.Centers.Min(p=>p.x),20,"A map variant must actually alter its river's shape");
            foreach(var channel in layout.Channels)
            {
                Assert.AreEqual(channel.Centers.Length,channel.HalfWidths.Length);
                for(int i=0;i<channel.Centers.Length-1;i++)Assert.LessOrEqual(Vector2.Distance(channel.Centers[i],channel.Centers[i+1]),1.5f);
                Assert.IsTrue(channel.HalfWidths.All(width=>width>=4));
            }
            foreach(var point in layout.FishPoints){Assert.IsTrue(layout.IsWater(point),$"{map} fish {point}");Assert.Less(layout.BankHeight(point),layout.WaterHeight);Assert.LessOrEqual(layout.GroundHeight(point),0,"Fish must stay beyond bridges");}
            foreach(var town in new[]{new Vector2(24,28),new Vector2(104,100)})Assert.Greater(layout.SignedBankDistance(town),8,"Starting towns need a complete land footprint");
            Assert.IsFalse(layout.IsWater(new Vector2(-1,64)));Assert.IsFalse(layout.IsWater(new Vector2(64,129)));
        }
        [TestCaseSource(nameof(NewMaps))] public void BridgeCoordinatesDeckRampsAndFootprintsUseOneOrientedDefinition(string map)
        {
            var layout=MapLayout.Create(map);Assert.GreaterOrEqual(layout.Bridges.Length,2);
            foreach(var bridge in layout.Bridges)
            {
                Assert.AreEqual(1,bridge.Direction.magnitude,.0001f);Assert.IsTrue(layout.IsWater(bridge.Center));Assert.IsTrue(layout.IsBridge(bridge.Center));
                foreach(var local in new[]{new Vector2(-bridge.HalfLength,0),new Vector2(2,1),new Vector2(bridge.HalfLength+bridge.RampLength,0)})
                {Vector2 restored=bridge.Local(bridge.Point(local.x,local.y));Assert.AreEqual(local.x,restored.x,.0001f);Assert.AreEqual(local.y,restored.y,.0001f);}
                Assert.AreEqual(bridge.Height,layout.GroundHeight(bridge.Center),.001f);Assert.Less(layout.BankHeight(bridge.Center),-.1f,"The bed under a bridge stays below the water");
                foreach(int direction in new[]{-1,1})
                {
                    Vector2 mouth=bridge.Point(direction*(bridge.HalfLength+bridge.RampLength),0);
                    Assert.IsFalse(layout.IsWater(mouth),"Ramp mouth must start on land");Assert.AreEqual(0,bridge.GroundHeight(mouth),.001f);
                    Assert.IsTrue(layout.BridgeFootprintOverlaps(mouth,4,4));
                    Assert.IsTrue(layout.IsBridgeSideBarrier(bridge.Point(direction*(bridge.HalfLength+bridge.RampLength*.5f),bridge.HalfWidth+.4f)));
                }
            }
            Assert.IsFalse(layout.BridgeFootprintOverlaps(new Vector2(24,28),4,4));
        }
        [TestCaseSource(nameof(NewMaps))] public void LandAndWaterNavigationConnectEachNewMapWithoutDomainLeaks(string map)
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,map);
            Vector2 from=world.Navigation.NearestWalkable(new Vector2(33,28),.9f),to=world.Navigation.NearestWalkable(new Vector2(95,100),.9f);
            var land=world.Navigation.FindPath(from,to,.9f);Assert.IsNotEmpty(land,$"{map} armies must be able to cross");Assert.Less(Vector2.Distance(to,land.Last()),.001f);
            Check(land,world.Navigation,.9f,false);
            var water=world.WaterNavigation.FindPath(world.Layout.PatrolPoints[0],world.Layout.PatrolPoints[1],.9f);
            Assert.IsNotEmpty(water,$"{map} gunboats must pass under both crossings");Check(water,world.WaterNavigation,.9f,true);
            foreach(var fish in world.Entities.Where(e=>e.def.id=="fish"))Assert.IsTrue(world.WaterNavigation.IsWalkable(fish.position,fish.def.radius),$"Fish at {fish.position} has no ship access");
            foreach(var span in world.Layout.Bridges)
            {
                Vector2 start=span.Point(-span.HalfLength-span.RampLength-3,0),end=span.Point(span.HalfLength+span.RampLength+3,0);
                var crossing=world.Navigation.FindPath(start,end,.9f);Assert.IsNotEmpty(crossing,$"{map} bridge at {span.Center} must connect both banks");Check(crossing,world.Navigation,.9f,false);
            }
            void Check(System.Collections.Generic.List<Vector2> path,GridNavigation navigation,float radius,bool boat)
            {
                for(int i=0;i<path.Count-1;i++)
                {
                    int steps=Mathf.CeilToInt(Vector2.Distance(path[i],path[i+1])*4);
                    for(int s=0;s<=steps;s++)
                    {
                        Vector2 point=Vector2.Lerp(path[i],path[i+1],steps==0?0:(float)s/steps);
                        Assert.IsTrue(navigation.IsWalkable(point,radius),$"{map} path invalid at {point}");
                        Assert.IsTrue(boat?world.IsWater(point):!world.IsWater(point)||world.IsBridge(point),$"{map} navigation domain leak at {point}");
                    }
                }
            }
        }
        [TestCaseSource(nameof(NewMaps))] public void AuthoredTradeRouteNeverCutsAcrossWaterOrTheSideOfARamp(string map)
        {
            var layout=MapLayout.Create(map);Assert.GreaterOrEqual(layout.TradeRoute.Length,7);
            for(int i=0;i<layout.TradeRoute.Length-1;i++)
            {
                int steps=Mathf.CeilToInt(Vector2.Distance(layout.TradeRoute[i],layout.TradeRoute[i+1])*4);
                for(int s=0;s<=steps;s++)
                {
                    Vector2 point=Vector2.Lerp(layout.TradeRoute[i],layout.TradeRoute[i+1],steps==0?0:(float)s/steps);
                    Assert.IsTrue(!layout.IsWater(point)||layout.IsBridge(point),$"{map} trade caravan would wade at {point}");
                    Assert.IsFalse(layout.IsBridgeSideBarrier(point),$"{map} trade caravan hits the bridge railing at {point}");
                }
            }
        }
        [TestCaseSource(nameof(NewMaps))] public void BothBanksHaveAuthoredHarborSitesAndDirectionsIntoWater(string map)
        {
            var layout=MapLayout.Create(map);Assert.AreEqual(2,layout.DockSites.Length);
            foreach(var side in layout.DockSites)
            {
                Assert.GreaterOrEqual(side.Length,2,$"{map} should offer two harbors per player");
                foreach(var site in side)
                {Assert.IsFalse(layout.IsWater(site));Assert.IsFalse(layout.BridgeFootprintOverlaps(site,4,4));Assert.Greater(layout.WaterDirection(site).sqrMagnitude,.99f);Assert.IsTrue(layout.IsWater(site+layout.WaterDirection(site)*5),$"{map} harbor must face usable water at {site}");}
            }
        }
        [TestCase(MapTopology.Winding,73.52f,32.01f,73.12f,32.01f)]
        [TestCase(MapTopology.Tropical,69.61f,34.98f,70.01f,35.02f)]
        [TestCase(MapTopology.Tropical,73.73f,98.17f,74.09f,98.01f)]
        [TestCase(MapTopology.Estuary,47.70f,65.07f,48.04f,64.86f)]
        public void SlantedBridgeHeightDiscontinuitiesCannotBeCrossedThroughPartialRailingCells(string map,float fromX,float fromY,float toX,float toY)
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,map);
            Vector2 from=new Vector2(fromX,fromY),to=new Vector2(toX,toY);
            Assert.Greater(Mathf.Abs(world.SurfaceHeight(to)-world.SurfaceHeight(from)),Vector2.Distance(from,to)*.4f,"The probe must really straddle a raised ramp side");
            Assert.IsFalse(world.Navigation.ClearLine(from,to,.4f),"Grid cells intersecting a physical railing must block traversal even when their center lies inside the ramp");
        }
        [Test] public void ShiftedConfluenceCrossingKeepsItsRaisedRampSidesSealed()
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.Tributaries);var span=world.Layout.Bridges[0];
            float across=span.HalfLength+span.RampLength*.5f;
            Vector2 from=span.Point(across,span.HalfWidth+.1f),to=span.Point(across,span.HalfWidth-.3f);
            Assert.Greater(Mathf.Abs(world.SurfaceHeight(to)-world.SurfaceHeight(from)),Vector2.Distance(from,to)*.4f);
            Assert.IsFalse(world.Navigation.ClearLine(from,to,.4f),"Confluence bridge relocations must keep the physical ramp side impassable");
        }
        [Test] public void ConfluenceBankPocketIsConnectedForFieldCannonsAfterMovingTheSouthernRamp()
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.Tributaries);
            var goal=new Vector2(53.4f,54.1f);float radius=world.Rules.Entity("cannon").radius;
            Assert.IsTrue(world.Navigation.IsWalkable(goal,radius));
            var path=world.Navigation.FindPath(new Vector2(120.6f,69.6f),goal,radius);
            Assert.IsNotEmpty(path,"Bridge railings and the tributary shore must not enclose an isolated artillery pocket");
            Assert.Less(Vector2.Distance(path.Last(),goal),.001f);
            for(int i=1;i<path.Count;i++)Assert.IsTrue(world.Navigation.ClearLine(path[i-1],path[i],radius));
        }
    }
}
