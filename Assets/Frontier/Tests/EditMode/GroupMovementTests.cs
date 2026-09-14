using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Frontier.Tests
{
    public sealed class GroupMovementTests
    {
        [Test]public void FiftyMixedUnitsCrossOpenGroundToDistinctDestinations()
        {
            var world=new World(Catalog.Load());var units=new List<EntityState>();
            // This fixture measures travel and arrival, not post-arrival combat pursuit.
            foreach(var guard in world.Entities.Where(World.Guardian).ToArray())world.Remove(guard);
            string[] kinds={"pike","musketeer","hussar","dragoon","cannon"};
            for(int i=0;i<50;i++)units.Add(world.Spawn(kinds[i%5],0,new Vector2(35+i%10*2,45+i/10*2)));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=units.Select(e=>e.id).ToArray(),order=OrderKind.Move,point=new Vector2(43,65)}).accepted);
            var destinations=units.Select(e=>e.orders.Peek().point).ToArray();Assert.AreEqual(50,destinations.Distinct().Count());
            for(int i=0;i<900;i++)world.Tick(.1f);
            int arrived=units.Select((e,i)=>(e.position-destinations[i]).magnitude).Count(d=>d<2);
            Assert.AreEqual(50,arrived,"All mixed units should complete their own destination on open ground: "+string.Join("; ",units.Select((e,i)=>$"{e.id} {e.def.id} at {e.position} goal {destinations[i]} status {e.status} orders={e.orders.Count}").Where((text,i)=>(units[i].position-destinations[i]).magnitude>=2)));
        }
        [Test]public void GroupResumesAfterDynamicWallIsRemoved()
        {
            var world=new World(Catalog.Load());var units=new List<EntityState>();
            // This fixture measures travel and arrival, not post-arrival combat pursuit.
            foreach(var guard in world.Entities.Where(World.Guardian).ToArray())world.Remove(guard);
            // Close the complete map cross-section; the existing map cannot provide a flank.
            for(int y=0;y<128;y++)world.Navigation.SetBlocked(new Vector2(60,y),.45f,true);
            for(int i=0;i<10;i++)units.Add(world.Spawn("musketeer",0,new Vector2(40+i%5*2,48+i/5*2)));
            world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=units.Select(e=>e.id).ToArray(),order=OrderKind.Move,point=new Vector2(80,50)});
            for(int i=0;i<70;i++)world.Tick(.1f);Assert.IsTrue(units.All(e=>e.position.x<60));
            for(int y=0;y<128;y++)world.Navigation.SetBlocked(new Vector2(60,y),.45f,false);
            for(int i=0;i<1200;i++)world.Tick(.1f);
            Assert.IsTrue(units.All(e=>e.position.x>70),"Navigation revision must cause failed paths to retry after obstruction removal");
        }
    }
}
