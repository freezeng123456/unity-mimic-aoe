using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class SeededMap065Tests
    {
        private static IEnumerable<TestCaseData> Seeds()
        {
            foreach(string template in MapTopology.Generated)for(int seed=0;seed<30;seed++)
                yield return new TestCaseData(template,seed).SetName($"SeededMap_{template}_{seed:00}_GeographyOpeningHarborAndLanding");
        }
        private static Vector2 Flip(Vector2 point)=>new Vector2(128-point.x,128-point.y);
        private static GridNavigation Land(MapLayout layout)
        {var navigation=new GridNavigation();navigation.ConfigureTerrain(p=>layout.IsWater(p)&&!layout.IsBridge(p)||layout.IsBridgeSideBarrierCell(p));return navigation;}
        private static GridNavigation Water(MapLayout layout)
        {var navigation=new GridNavigation();navigation.ConfigureTerrain(p=>!layout.IsWater(p));return navigation;}
        private static void ExactPath(GridNavigation navigation,Vector2 start,Vector2 goal,float radius,string context)
        {
            Assert.IsTrue(navigation.IsWalkable(start,radius),context+" start");Assert.IsTrue(navigation.IsWalkable(goal,radius),context+" goal");
            var path=navigation.FindPath(start,goal,radius);Assert.IsNotEmpty(path,context);
            Assert.Less((path[path.Count-1]-goal).sqrMagnitude,.00001f,context+" endpoint must not project to another shore");
            for(int i=1;i<path.Count;i++)Assert.IsTrue(navigation.ClearLine(path[i-1],path[i],radius),context+" swept leg "+i);
        }

        [TestCaseSource(nameof(Seeds))]
        public void SeededGeographyHasSafeSymmetricOpeningAndRealNavalConnections(string template,int seed)
        {
            var layout=MapLayout.Create(template,seed);Assert.IsTrue(layout.Generated);Assert.AreEqual(seed,layout.Seed);Assert.AreEqual(2,layout.Starts.Length);
            Assert.AreEqual(template==MapTopology.GeneratedStrait,layout.RequiresTransport);
            var land=Land(layout);var water=Water(layout);
            for(int player=0;player<2;player++)
            {
                Assert.IsTrue(land.IsAreaFree(layout.Starts[player].Town,5,5),"Complete town footprint");
                Assert.AreEqual(43,layout.Starts[player].Resources.Length);
                Assert.AreEqual(2,layout.DockSites[player].Length);Assert.AreEqual(2,layout.LandingSites[player].Length);
                Assert.Less(Vector2.Distance(layout.DockSites[player][0],layout.Starts[player].Town),45,"The first harbor is a nearby opening opportunity");
                foreach(var site in layout.DockSites[player])
                {
                    Assert.IsTrue(layout.InStartingRegion(player,site),"A harbor must be on its player's mainland");
                    Vector2 berth=site+layout.WaterDirection(site)*6.4f;
                    ExactPath(water,berth,layout.PatrolPoints[0],1.2f,"Harbor boat access");
                }
                foreach(var beach in layout.LandingSites[player])
                {
                    Assert.IsTrue(layout.InStartingRegion(player,beach));Assert.IsTrue(land.IsWalkable(beach,1.05f),"Largest land body at landing");
                    Assert.IsFalse(layout.BridgeFootprintOverlaps(beach,8,8));
                    foreach(var dock in layout.DockSites[player])Assert.GreaterOrEqual(Vector2.Distance(beach,dock),9,"Beach must remain outside the quay district");
                    ExactPath(water,beach+layout.WaterDirection(beach)*7,layout.PatrolPoints[0],1.2f,"Landing berth access");
                }
            }
            foreach(var fish in layout.FishPoints)
            {Assert.IsTrue(layout.IsWater(fish));Assert.IsFalse(layout.IsBridge(fish));ExactPath(water,fish,layout.PatrolPoints[0],1.2f,"Fishing access");}
            ExactPath(water,layout.PatrolPoints[0],layout.PatrolPoints[1],1.2f,"Sea/channel network");
            for(int i=1;i<layout.TradeRoute.Length;i++)Assert.IsTrue(land.ClearLine(layout.TradeRoute[i-1],layout.TradeRoute[i],.5f),"Trade route must not wade or enter a ramp side: "+i);
            foreach(var site in layout.NeutralSites.Concat(layout.ObjectiveSites))
            {Assert.IsTrue(land.IsAreaFree(site,4,4),"A public candidate needs a full footprint at "+site);Assert.IsFalse(layout.BridgeFootprintOverlaps(site,4,4));}
            foreach(var site in layout.NeutralSites)Assert.LessOrEqual(layout.TradeRoute.Min(p=>Vector2.Distance(p,site)),6,"Trade delivery is physically near its route waypoint");
            foreach(var bridge in layout.Bridges)
            {
                Vector2 from=bridge.Point(-bridge.HalfLength-bridge.RampLength-3,0),to=bridge.Point(bridge.HalfLength+bridge.RampLength+3,0);
                Assert.IsTrue(land.ClearLine(from,to,1.05f),"Actual bridge deck and both aprons must accept a large army body");
                const int steps=250;Vector2 previous=from;
                for(int i=1;i<=steps;i++)
                {
                    Vector2 point=Vector2.Lerp(from,to,(float)i/steps);
                    Assert.IsTrue(!layout.IsWater(point)||layout.IsBridge(point),"Bridge cannot leave a gap in the river");
                    Assert.LessOrEqual(Mathf.Abs(layout.GroundHeight(point)-layout.GroundHeight(previous)),Vector2.Distance(point,previous)*.4f+.002f,"Deck/shore height must be continuous");previous=point;
                }
            }
            if(layout.RequiresTransport)
            {
                Assert.IsEmpty(layout.Bridges);Assert.AreNotEqual(layout.LandRegion(layout.Starts[0].Town),layout.LandRegion(layout.Starts[1].Town));
                Assert.IsEmpty(land.FindPath(layout.Starts[0].Explorer,layout.Starts[1].Explorer,1.05f),"Mainlands must truly require naval transport");
                Assert.AreEqual(2,layout.IslandLandingSites.Length);int island=layout.LandRegion(layout.TradeRoute[4]);
                Assert.AreNotEqual(island,layout.LandRegion(layout.Starts[0].Town));Assert.AreNotEqual(island,layout.LandRegion(layout.Starts[1].Town));
                foreach(var beach in layout.IslandLandingSites)
                {Assert.AreEqual(island,layout.LandRegion(beach));Assert.IsTrue(land.IsWalkable(beach,1.05f));ExactPath(water,beach+layout.WaterDirection(beach)*7,layout.PatrolPoints[0],1.2f,"Island access");}
            }
            else ExactPath(land,layout.Starts[0].Explorer,layout.Starts[1].Explorer,1.05f,"Both bridge-linked mainlands");

            // These are unmodified opening resources and actual civilization
            // bodies. Age/visibility are controlled solely for placement queries;
            // physical paid build/transport gameplay has separate integration tests.
            var world=new World(Catalog.Load(),seed,"coast","coast",5,template);
            AssertSafeOpeningCamps(world);
            int block=world.Rules.Civ("coast").startingWorkers+2+43;
            var opening=world.Entities.Take(block*2).ToArray();Assert.AreEqual(block*2,opening.Length);
            for(int i=0;i<block;i++)
            {
                var first=opening[i];var second=opening[block+i];Assert.AreEqual(first.def.id,second.def.id);
                Assert.Less(Vector2.Distance(Flip(first.position),second.position),.025f,"Actual opening placement must preserve paired fairness for "+first.def.id);
                Assert.AreEqual(first.remaining,second.remaining);Assert.AreEqual(first.owner<0?first.owner:1-first.owner,second.owner);
            }
            for(int player=0;player<2;player++)
            {
                Assert.AreEqual(layout.Starts[player].Town,opening[player*block].position,"Opening metadata is the actual town");
                var explorer=opening[player*block+world.Rules.Civ("coast").startingWorkers+1];
                foreach(var resource in opening.Skip(player*block+world.Rules.Civ("coast").startingWorkers+2).Take(43))
                {
                    Assert.IsTrue(layout.InStartingRegion(player,resource.position));Assert.Less(Vector2.Distance(resource.position,layout.Starts[player].Town),60,"Resources must not scatter beyond their opening district");
                    ExactPath(world.Navigation,explorer.position,resource.position,.4f,"Actual local resource access: "+resource.def.id);
                }
                world.Players[player].age=2;
                foreach(var site in layout.DockSites[player])
                {world.Fog.Reveal(player,site,18);Assert.AreEqual("",world.CanBuild(player,"dock",site),"Initial actors and resources cannot obstruct a default harbor");}
                foreach(var beach in layout.LandingSites[player])
                {ExactPath(world.Navigation,explorer.position,beach,1.05f,"Army reaching its public embarkation beach");Assert.IsTrue(world.Navigation.IsWalkable(beach,1.05f));}
            }
            foreach(var actor in opening)
                if(actor.def.kind!=EntityKind.Building)
                {Assert.IsFalse(world.IsWater(actor.position));Assert.IsFalse(world.IsBridgeApproach(actor.position));Assert.IsTrue(world.Navigation.IsWalkable(actor.position,actor.def.radius),"Actual body at "+actor.position);}
            for(int i=0;i<opening.Length;i++)for(int j=i+1;j<opening.Length;j++)
            {
                var a=opening[i];var b=opening[j];float gap;
                if(a.def.kind==EntityKind.Building)gap=world.BuildingFootprintDistance(a.def,a.position,b.position)-b.def.radius;
                else if(b.def.kind==EntityKind.Building)gap=world.BuildingFootprintDistance(b.def,b.position,a.position)-a.def.radius;
                else gap=Vector2.Distance(a.position,b.position)-a.def.radius-b.def.radius;
                Assert.GreaterOrEqual(gap,.115f,"Opening physical clearance: "+a.id+" / "+b.id);
            }
        }

        private static float SegmentDistance(Vector2 point,Vector2 from,Vector2 to)
        {var delta=to-from;float t=Mathf.Clamp01(Vector2.Dot(point-from,delta)/Mathf.Max(.000001f,delta.sqrMagnitude));return Vector2.Distance(point,from+delta*t);}
        private static void AssertSafeOpeningCamps(World world)
        {
            Assert.AreEqual(world.Rules.landmarks.treasures.Length,world.Treasures.Count,"Camp placement must retain every treasure definition");
            var guards=world.Entities.Where(World.Guardian).ToArray();
            Assert.AreEqual(world.Rules.landmarks.treasures.Sum(c=>c.guards),guards.Length,"Moving camps must retain all guardians");
            for(int i=0;i<world.Treasures.Count;i++)
                Assert.AreEqual(world.Layout.ObjectiveSites[i],world.Get(world.Treasures[i].entity).position,"A camp must not be projected back into its opening district");
            foreach(var guard in guards)
            {
                Assert.IsTrue(world.Navigation.IsWalkable(guard.position,guard.def.radius));
                foreach(var worker in world.Entities.Where(e=>e.owner>=0&&(e.Worker||e.def.id=="explorer")))
                    Assert.Greater(Vector2.Distance(guard.origin,worker.position),Mathf.Max(guard.def.vision,world.Rules.landmarks.guardLeash)+worker.def.radius+1,"A stationary opening body must remain outside camp aggro: "+guard.id+" / "+worker.id);
            }
            for(int player=0;player<2;player++)
            {
                var worker=world.Entities.First(e=>e.owner==player&&e.Worker);
                foreach(var shore in world.Layout.DockSites[player].Concat(world.Layout.LandingSites[player]))
                {
                    var path=world.Navigation.FindPath(worker.position,shore,worker.def.radius);Assert.IsNotEmpty(path,"Normal opening harbor/beach access");
                    Assert.Less((path[path.Count-1]-shore).sqrMagnitude,.00001f,"Harbor access must reach the requested shore");
                    foreach(var guard in guards)for(int i=1;i<path.Count;i++)
                        Assert.Greater(SegmentDistance(guard.origin,path[i-1],path[i]),Mathf.Max(guard.def.vision,world.Rules.landmarks.guardLeash)+worker.def.radius+1,"An ordinary harbor corridor must not pull a camp: "+guard.id);
                }
            }
        }

        [TestCase(MapTopology.GeneratedRiver,161803)]
        [TestCase(MapTopology.GeneratedDelta,271828)]
        [TestCase(MapTopology.GeneratedStrait,314159)]
        public void IdleOpeningWorkersSurviveExistingCampRulesForTwoHundredSeconds(string template,int seed)
        {
            var world=new World(Catalog.Load(),seed,"coast","coast",5,template);AssertSafeOpeningCamps(world);
            var opening=world.Entities.Where(e=>e.owner>=0&&(e.Worker||e.def.id=="explorer")).ToArray();
            var hp=opening.Select(e=>e.hp).ToArray();var positions=opening.Select(e=>e.position).ToArray();
            var simulation=new MatchSimulation(world,Array.Empty<int>());
            for(int tick=0;tick<2000;tick++)Assert.IsTrue(simulation.Step(),"Normal idle match must continue");
            for(int i=0;i<opening.Length;i++)
            {
                Assert.IsTrue(opening[i].Alive,"Neutral camps must not clear an untouched opening: "+opening[i].id);
                Assert.AreEqual(hp[i],opening[i].hp,"An opening actor must not need a hidden health grant");
                Assert.AreEqual(positions[i],opening[i].position,"This regression must leave opening actors in place");
            }
            Assert.IsFalse(world.Events.Any(e=>e.kind=="Damage"&&opening.Any(actor=>actor.id==e.target)),"No initial actor may have been damaged and healed");
        }

        [TestCase(MapTopology.GeneratedRiver)] [TestCase(MapTopology.GeneratedDelta)] [TestCase(MapTopology.GeneratedStrait)]
        public void ThirtySeedsChangeActualTerrainBridgesShoreAndResources(string template)
        {
            var terrain=new HashSet<string>();var shores=new HashSet<string>();var resources=new HashSet<string>();var bridges=new HashSet<string>();
            for(int seed=0;seed<30;seed++)
            {
                var layout=MapLayout.Create(template,seed);terrain.Add(Signature(layout,0));shores.Add(Signature(layout,1));resources.Add(Signature(layout,2));bridges.Add(Signature(layout,3));
            }
            Assert.AreEqual(30,terrain.Count,"Generator must not select a fixed geography or merely decorate it");
            Assert.GreaterOrEqual(shores.Count,25,"Raster water/shore boundaries must vary");Assert.AreEqual(30,resources.Count,"Seeded opening anchors must vary");
            if(template!=MapTopology.GeneratedStrait)Assert.AreEqual(30,bridges.Count,"Bridge positions/orientations are generated with their channel");
        }

        [TestCase(MapTopology.GeneratedRiver,int.MinValue)] [TestCase(MapTopology.GeneratedDelta,-97)] [TestCase(MapTopology.GeneratedStrait,int.MaxValue)]
        public void SameSeedRebuildsIdenticalGeographyAfterCacheEviction(string template,int seed)
        {
            var first=MapLayout.Create(template,seed);string signature=Signature(first,4);
            for(int other=100;other<113;other++)MapLayout.Create(template,other);
            var rebuilt=MapLayout.Create(template,seed);Assert.AreNotSame(first,rebuilt,"This must exercise generation, not return the cached object");
            Assert.AreEqual(signature,Signature(rebuilt,4));
            var expected=new World(Catalog.Load(),seed,"coast","coast",5,template);var restored=World.Restore(Catalog.Load(),JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(expected.CaptureState())));
            Assert.AreEqual(seed,restored.Layout.Seed);Assert.AreEqual(signature,Signature(restored.Layout,4));
            Assert.AreEqual(JsonUtility.ToJson(expected.CaptureState()),JsonUtility.ToJson(restored.CaptureState()),"Cold restoration must retain actual generated terrain and opening state");
        }

        [TestCaseSource(typeof(MapTopology),nameof(MapTopology.Authored))]
        public void SeedOverloadPreservesAllSixAuthoredGeographies(string template)
        {
            var authored=MapLayout.Create(template);Assert.AreSame(authored,MapLayout.Create(template,int.MinValue));Assert.AreSame(authored,MapLayout.Create(template,int.MaxValue));
            Assert.IsFalse(authored.Generated);Assert.AreEqual(0,authored.Seed);Assert.AreEqual(new Vector2(24,28),authored.Starts[0].Town);Assert.AreEqual(new Vector2(104,100),authored.Starts[1].Town);
        }

        private static string Signature(MapLayout layout,int part)
        {
            using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory))
            {
                void Point(Vector2 p){writer.Write(p.x);writer.Write(p.y);}
                if(part==0||part==4)foreach(var channel in layout.Channels)
                {writer.Write(channel.Centers.Length);for(int i=0;i<channel.Centers.Length;i++){Point(channel.Centers[i]);writer.Write(channel.HalfWidths[i]);}}
                if(part==1||part==4)for(int y=1;y<128;y+=2)for(int x=1;x<128;x+=2)writer.Write(layout.IsWater(new Vector2(x,y)));
                if(part==2||part==4)foreach(var start in layout.Starts)
                {Point(start.Town);Point(start.Explorer);foreach(var resource in start.Resources){writer.Write(resource.Definition);Point(resource.Position);writer.Write(resource.Owned);}}
                if(part==3||part==4)foreach(var span in layout.Bridges)
                {Point(span.Center);Point(span.Direction);writer.Write(span.HalfLength);writer.Write(span.HalfWidth);writer.Write(span.RampLength);writer.Write(span.Height);}
                if(part==4)
                {foreach(var points in layout.DockSites)foreach(var p in points)Point(p);foreach(var points in layout.LandingSites)foreach(var p in points)Point(p);foreach(var p in layout.IslandLandingSites)Point(p);foreach(var p in layout.NeutralSites)Point(p);foreach(var p in layout.ObjectiveSites)Point(p);foreach(var p in layout.FishPoints)Point(p);foreach(var p in layout.TradeRoute)Point(p);}
                writer.Flush();using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(memory.ToArray()));
            }
        }
    }
}
