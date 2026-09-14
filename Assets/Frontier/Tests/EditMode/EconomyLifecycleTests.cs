using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace Frontier.Tests
{
    public sealed class EconomyLifecycleTests
    {
        World world;
        [SetUp]public void Setup(){world=new World(Catalog.Load());}
        void Steps(int n){for(int i=0;i<n;i++)world.Tick(.1f);}
        [Test]public void DepletedNodeRetargetsNearbyLegalResource()
        {
            var worker=world.Spawn("villager",0,new Vector2(40,40));var first=world.Spawn("berry",-1,new Vector2(40.7f,40));first.remaining=.01f;
            var second=world.Spawn("berry",-1,new Vector2(42,40));world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Gather,target=first.id}).accepted);
            float food=world.Players[0].resources.food;Steps(100);
            Assert.IsFalse(first.Alive);Assert.Less(second.remaining,second.def.amount);Assert.Greater(world.Players[0].resources.food,food+.01f);
        }
        [Test]public void BuildingRefundAndObstacleAreReleasedOnce()
        {
            var worker=world.Entities.First(e=>e.owner==0&&e.Worker);var point=new Vector2(38,35);world.Fog.Reveal(0,point,10);
            float wood=world.Players[0].resources.wood;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{worker.id},item="house",point=point}).accepted);
            var house=world.Entities.Last();Assert.IsFalse(world.Navigation.IsWalkable(point,.4f));
            var cancel=new GameCommand{player=0,kind=CommandKind.CancelBuilding,actors=new[]{house.id}};
            Assert.IsTrue(world.Issue(cancel).accepted);Assert.IsFalse(world.Issue(cancel).accepted);
            Assert.AreEqual(wood-house.def.cost.wood*(1-world.Rules.constructionRefund),world.Players[0].resources.wood,.01f);
            Assert.IsTrue(world.Navigation.IsWalkable(point,.4f));
        }
        [Test]public void PendingAgeRewardSurvivesReceiverLossAndIsDeliveredOnce()
        {
            var p=world.Players[0];p.rewards.Add(new RewardJob{crates=new Cost(0,400,0)});
            var town=world.Entities.First(e=>e.owner==0&&e.def.id=="town");
            // Block all possible crate landing spots while leaving the rest of the match valid.
            world.Navigation.SetBlocked(town.position,15,true);Steps(1);Assert.AreEqual(1,p.rewards.Count);
            world.Remove(town);Steps(1);Assert.AreEqual(1,p.rewards.Count);
            world.Spawn("town",0,new Vector2(46,36));Steps(1);Assert.AreEqual(0,p.rewards.Count);
            Steps(5);Assert.AreEqual(1,world.Events.Count(e=>e.kind=="AgeRewardArrived"));
            Assert.AreEqual(300,p.resources.wood,"Crates are not direct credit");
        }
        [Test]public void HousingLossDoesNotDeleteExistingUnitsAndBlocksNewReservations()
        {
            var p=world.Players[0];var town=world.Entities.First(e=>e.owner==0&&e.def.id=="town");int population=p.usedPopulation;
            world.Remove(town);Assert.AreEqual(population,p.usedPopulation);Assert.AreEqual(0,p.populationCap);
            var barracks=world.Spawn("barracks",0,new Vector2(40,40));p.age=2;
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{barracks.id},item="pike"}).accepted);
            Assert.AreEqual(0,p.reservedPopulation);
        }
        [Test]public void TechnologyPreservesHealthRatioAndAppliesOnceToFutureUnits()
        {
            var barracks=world.Spawn("barracks",0,new Vector2(40,40));barracks.hp=world.MaxHp(barracks)*.5f;
            world.ApplyTechnology(0,"masonry");float upgraded=world.MaxHp(barracks);
            Assert.Greater(upgraded,barracks.def.hp);Assert.AreEqual(.5f,barracks.hp/upgraded,.001f);
            world.ApplyTechnology(0,"masonry");Assert.AreEqual(upgraded,world.MaxHp(barracks));
            var next=world.Spawn("barracks",0,new Vector2(50,40));Assert.AreEqual(upgraded,next.hp);
        }
    }
}
