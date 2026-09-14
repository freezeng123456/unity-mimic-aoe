using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class GarrisonExitTests
    {
        private World world;private EntityState town;private EntityState[] workers;
        [SetUp] public void Setup()
        {
            world=new World(Catalog.Load(),42,"coast","coast",3);
            town=world.Spawn("town",0,new Vector2(76,25));
            workers=Enumerable.Range(0,10).Select(i=>world.Spawn("villager",0,town.position+new Vector2(4+i%2*1.2f,-3+i/2*1.2f))).ToArray();
            world.Fog.Tick(world.Entities);
        }
        private void Steps(int count){for(int i=0;i<count;i++)world.Tick(.1f);}
        private EntityState[] Enter(int count)
        {
            var group=workers.Take(count).ToArray();
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=group.Select(e=>e.id).ToArray(),order=OrderKind.Garrison,target=town.id}).accepted);
            Steps(160);Assert.AreEqual(count,town.occupants.Count,"Fixture must actually enter every worker");return group;
        }
        private void AssertSafe(EntityState[] group,bool settled)
        {
            foreach(var e in group){Assert.AreEqual(0,e.garrisonedIn);Assert.IsTrue(world.Navigation.IsWalkable(e.position,e.def.radius));if(settled)Assert.IsEmpty(e.orders,"Unit "+e.id+" remains "+e.status+" at "+e.position);}
            for(int i=0;i<group.Length;i++)for(int j=i+1;j<group.Length;j++)
                Assert.GreaterOrEqual(Vector2.Distance(group[i].position,group[j].position),group[i].def.radius+group[j].def.radius-.05f,"Overlapping released units");
        }
        [Test] public void SixReleasedWorkersFinishTheirRallyMoves()
        {
            var group=Enter(6);Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Ungarrison,actors=new[]{town.id}}).accepted);
            Steps(450);AssertSafe(group,true);
        }
        [Test] public void DestroyedTownReleasesTenWorkersWithoutStacking()
        {
            var group=Enter(10);world.Remove(town);AssertSafe(group,true);
            Steps(450);AssertSafe(group,true);
        }
        [Test] public void ObstructedPreferredExitUsesAvailableSpace()
        {
            var group=Enter(6);
            for(int x=80;x<=85;x++)for(int y=23;y<=33;y++)world.Navigation.SetBlocked(new Vector2(x,y),0,true);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Ungarrison,actors=new[]{town.id}}).accepted);
            AssertSafe(group,false);Steps(450);AssertSafe(group,true);
        }
        [Test] public void FullyBlockedReleaseRetainsUnitsUntilSpaceIsAvailable()
        {
            var group=Enter(6);int population=world.Players[0].usedPopulation;
            for(int x=61;x<=91;x++)for(int y=10;y<=40;y++)world.Navigation.SetBlocked(new Vector2(x,y),0,true);
            var blocked=world.Issue(new GameCommand{player=0,kind=CommandKind.Ungarrison,actors=new[]{town.id}});
            Assert.IsFalse(blocked.accepted);Assert.AreEqual(6,town.occupants.Count);Assert.IsTrue(group.All(e=>e.garrisonedIn==town.id));Assert.AreEqual(population,world.Players[0].usedPopulation);
            // Reopen only the normal east-side arrival area, leaving the building occupied.
            for(int x=80;x<=88;x++)for(int y=19;y<=34;y++)world.Navigation.SetBlocked(new Vector2(x,y),0,false);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Ungarrison,actors=new[]{town.id}}).accepted);
            AssertSafe(group,false);Assert.AreEqual(population,world.Players[0].usedPopulation);
        }
        [Test] public void ConsecutiveBatchesSettleWithoutSharingOccupiedRallySlots()
        {
            var b=world.Spawn("barracks",0,new Vector2(76,38));b.rally=new Vector2(87,35);
            world.Players[0].age=2;world.Players[0].resources=new Cost(4000,4000,4000);
            for(int batch=0;batch<2;batch++)
            {
                Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{b.id},item="musketeer",count=5}).accepted);
                Steps(750);var produced=world.Entities.Where(e=>e.owner==0&&e.def.id=="musketeer").ToArray();
                Assert.AreEqual((batch+1)*5,produced.Length);AssertSafe(produced,true);
            }
        }
        [Test] public void DestroyedTownUsesFreedFootprintWhenExteriorIsBlocked()
        {
            var group=Enter(10);
            for(int x=61;x<=91;x++)for(int y=10;y<=40;y++)world.Navigation.SetBlocked(new Vector2(x,y),0,true);
            world.Remove(town);
            Assert.IsTrue(group.Any(e=>e.garrisonedIn==0),"The newly freed footprint should allow evacuation");
            Assert.IsTrue(group.Where(e=>e.garrisonedIn==0).All(e=>world.Navigation.IsWalkable(e.position,e.def.radius)));
            Assert.IsTrue(group.All(e=>e.Alive),"No occupant may be destroyed just because exits are blocked");
        }
        [Test] public void PendingEvacuationRetriesAfterSpaceReopensWithoutPopulationDuplication()
        {
            var group=workers.ToList();
            for(int i=0;i<40;i++)
            {float a=i*Mathf.PI*2/40;group.Add(world.Spawn("villager",0,town.position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(6+i%2)));}
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=group.Select(e=>e.id).ToArray(),order=OrderKind.Garrison,target=town.id}).accepted);
            Steps(800);Assert.AreEqual(50,town.occupants.Count);int population=world.Players[0].usedPopulation;
            for(int x=61;x<=91;x++)for(int y=10;y<=40;y++)world.Navigation.SetBlocked(new Vector2(x,y),0,true);
            world.Remove(town);Assert.IsTrue(group.Any(e=>e.garrisonedIn==town.id));
            Assert.IsTrue(world.Events.Any(e=>e.kind=="EvacuationWaiting"));
            for(int x=61;x<=91;x++)for(int y=10;y<=40;y++)world.Navigation.SetBlocked(new Vector2(x,y),0,false);
            Steps(50);AssertSafe(group.ToArray(),true);Assert.IsEmpty(town.occupants);
            Assert.AreEqual(population,world.Players[0].usedPopulation);
        }
    }
}
