using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class MapOpening063Tests
    {
        private static readonly string[] NewMaps={"winding","tropical","tributaries","estuary"};

        [TestCaseSource(nameof(NewMaps))]
        public void EveryCivilizationOpeningKeepsLaborResourcesAndTreasureOnUsableLand(string map)
        {
            var rules=Catalog.Load();Assert.IsNotEmpty(rules.civs);
            foreach(var civilization in rules.civs)
            {
                // Both mirrored openings use each available civilization; no
                // grants, entity removals or opening-position overrides.
                var world=new World(rules,314159,civilization.id,civilization.id,5,map);
                foreach(var entity in world.Entities.Where(e=>e.Alive&&!e.def.waterborne))
                {
                    string label=$"{map}/{civilization.id}: {entity.def.id} #{entity.id} at {entity.position}";
                    Assert.IsFalse(world.IsWater(entity.position),label+" starts in water");
                    if(entity.def.kind==EntityKind.Building)
                    {
                        foreach(var cell in world.BuildingFootprintCells(entity.def,entity.position))
                        {
                            Assert.IsFalse(world.IsWater(cell),label+" warehouse footprint crosses the bank at "+cell);
                            Assert.IsFalse(world.IsBridgeApproach(cell),label+" occupies a bridge approach at "+cell);
                            Assert.IsFalse(world.Navigation.IsWalkable(cell,0),label+" does not block its occupied cell "+cell);
                        }
                        continue;
                    }
                    Assert.IsTrue(world.Navigation.IsWalkable(entity.position,entity.def.radius),label+" is trapped by the bank, railing or a starting building");
                    if(entity.def.kind!=EntityKind.Resource&&!World.Guardian(entity))continue;
                    // Check the circumference as well as the center: a cache,
                    // herd, mineral deposit or guard must not protrude over a
                    // curved shore or reserve an elevated crossing's approach.
                    for(int sample=0;sample<32;sample++)
                    {
                        float angle=sample*Mathf.PI*2/32;Vector2 edge=entity.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*entity.def.radius;
                        Assert.IsFalse(world.IsWater(edge),label+" radius reaches water at "+edge);
                        Assert.IsFalse(world.IsBridgeApproach(edge),label+" radius reaches a bridge approach at "+edge);
                    }
                }
                var resources=world.Entities.Where(e=>e.Alive&&!e.def.waterborne&&e.def.kind==EntityKind.Resource).ToArray();
                for(int first=0;first<resources.Length;first++)for(int second=first+1;second<resources.Length;second++)
                {
                    var a=resources[first];var b=resources[second];float distance=Vector2.Distance(a.position,b.position);
                    Assert.GreaterOrEqual(distance+.001f,a.def.radius+b.def.radius+.12f,
                        $"{map}/{civilization.id}: bank relocation overlaps {a.def.id} #{a.id} at {a.position} with {b.def.id} #{b.id} at {b.position}; center distance {distance:0.000}");
                }
                for(int player=0;player<2;player++)
                {
                    var workers=world.Entities.Where(e=>e.Alive&&e.owner==player&&e.Worker).ToArray();
                    Assert.AreEqual(civilization.startingWorkers,workers.Length,$"{map}/{civilization.id} starting labor for player {player}");
                    var explorer=world.Entities.Single(e=>e.Alive&&e.owner==player&&(e.def.tags&Tags.Explorer)!=0);
                    foreach(var treasure in world.Treasures)
                    {
                        var cache=world.Get(treasure.entity);var path=world.Navigation.FindPath(explorer.position,cache.position,explorer.def.radius);
                        Assert.IsNotEmpty(path,$"{map}/{civilization.id}: player {player} cannot reach cache {cache.position}");
                        Assert.Less(Vector2.Distance(path.Last(),cache.position),.001f,"A nearest-bank projection does not prove the cache is reachable");
                    }
                }
            }
        }

        [TestCaseSource(nameof(NewMaps))]
        public void TradeCaravanPhysicallyCompletesTwoReturnTripsWithoutWadingOrJumpingAcrossRamps(string map)
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,map);float speed=world.Rules.landmarks.caravanSpeed;
            Assert.Greater(speed,0);float length=0;
            for(int i=1;i<world.TradeRoute.Length;i++)length+=Vector2.Distance(world.TradeRoute[i-1],world.TradeRoute[i]);
            const float dt=.05f;int steps=Mathf.CeilToInt(length*4/speed/dt)+2;
            Vector2 previous=world.CaravanPosition;float previousHeight=world.SurfaceHeight(previous),travelled=0;
            bool climbed=false,crossed=false;
            for(int step=0;step<steps;step++)
            {
                world.Tick(dt);Vector2 position=world.CaravanPosition;float height=world.SurfaceHeight(position);
                float displacement=Vector2.Distance(previous,position);travelled+=displacement;
                Assert.LessOrEqual(displacement,speed*dt+.001f,$"{map}: caravan teleports at {position}");
                Assert.IsTrue(!world.IsWater(position)||world.IsBridge(position),$"{map}: actual caravan wades at t={world.Time:0.00}, {position}");
                Assert.IsTrue(world.Navigation.IsWalkable(position,.4f),$"{map}: caravan crosses blocked terrain at {position}");
                Assert.LessOrEqual(Mathf.Abs(height-previousHeight),.15f,$"{map}: caravan jumps off a ramp at t={world.Time:0.00}, {position}, height {previousHeight}->{height}");
                if(world.IsBridgeApproach(position)&&height>.15f&&height<2.9f)climbed=true;
                if(world.IsBridge(position)&&height>3)crossed=true;
                previous=position;previousHeight=height;
            }
            Assert.IsTrue(climbed,map+" must demonstrate actual ramp traversal");Assert.IsTrue(crossed,map+" must demonstrate an actual elevated bridge crossing");
            Assert.Greater(travelled,length*3.98f,map+" caravan must complete two outbound and two inbound trips");
            Assert.Less(Vector2.Distance(world.CaravanPosition,world.TradeRoute[0]),1,map+" return trip must reach its origin");
        }
    }
}
