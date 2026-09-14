using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class LandmarkTests
    {
        World world;
        [SetUp]public void Setup(){world=new World(Catalog.Load(),314159,"coast","river",5);}
        void Tick(float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.1f);i++)world.Tick(.1f);}
        EntityState Explorer(int player)=>world.Entities.First(e=>e.owner==player&&(e.def.tags&Tags.Explorer)!=0);
        GameCommand Claim(EntityState explorer,EntityState treasure)=>new GameCommand{player=explorer.owner,kind=CommandKind.Order,actors=new[]{explorer.id},order=OrderKind.Claim,target=treasure.id};

        [TestCase(314159)][TestCase(271828)]public void AuthoredSitesAndCachesAreReachableForBothOpenings(int seed)
        {
            world=new World(Catalog.Load(),seed);
            for(int player=0;player<2;player++)
            {
                world.Players[player].resources=new Cost(1000,1000,1000);world.Fog.Reveal(player,new Vector2(64,64),128);
                var explorer=Explorer(player);
                foreach(var site in world.TradeSockets){var socket=world.Get(site.entity);Assert.AreEqual("",world.CanBuild(player,"trade_post",socket.position),"Site "+socket.position);Assert.IsNotEmpty(world.Navigation.FindPath(explorer.position,socket.position,.4f));}
                foreach(var cache in world.Treasures)Assert.IsNotEmpty(world.Navigation.FindPath(explorer.position,world.Get(cache.entity).position,.4f));
            }
        }

        [Test]public void IdlePathfinderDoesNotChargeDistantVisibleGuardians()
        {
            var explorer=Explorer(0);var start=explorer.position;Assert.IsTrue(world.Entities.Any(e=>World.Guardian(e)&&world.IsVisible(0,e)));
            Tick(5);Assert.AreEqual(start,explorer.position);Assert.IsFalse(world.Events.Any(e=>e.kind=="Fire"&&e.entity==explorer.id));
        }
        [Test]public void GuardedOrHiddenTreasureCannotBeClaimedAndWorkersCannotClaim()
        {
            var state=world.Treasures.First(t=>t.definition.guards>0);var cache=world.Get(state.entity);var explorer=Explorer(0);
            world.Fog.Reveal(0,cache.position,15);Assert.IsFalse(world.Issue(Claim(explorer,cache)).accepted);
            foreach(int id in state.guards)world.Remove(world.Get(id),0);
            var worker=world.Entities.First(e=>e.owner==0&&e.Worker);Assert.IsFalse(world.Issue(Claim(worker,cache)).accepted);
            var hidden=world.Get(world.Treasures.Last().entity);Assert.IsFalse(world.IsVisible(0,hidden));Assert.IsFalse(world.Issue(Claim(explorer,hidden)).accepted);
            Assert.AreEqual(0,world.Players[0].treasuresClaimed);
        }
        [Test]public void ContestedClaimCreatesOneRewardThatRequiresActualGathering()
        {
            var cache=world.Get(world.Treasures.First(t=>t.definition.guards==0).entity);var a=Explorer(0);var b=Explorer(1);
            a.position=cache.position+Vector2.left*1.5f;b.position=cache.position+Vector2.right*1.5f;world.Fog.Tick(world.Entities);
            var opening=world.Players[0].resources;Assert.IsTrue(world.Issue(Claim(a,cache)).accepted);Assert.IsTrue(world.Issue(Claim(b,cache)).accepted);
            Tick(3.5f);Assert.IsFalse(cache.Alive);Assert.AreEqual(1,world.Players.Sum(p=>p.treasuresClaimed));
            Assert.AreEqual(opening.wood,world.Players[0].resources.wood,"Finding supplies does not credit resources before a settler gathers them");
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="TreasureClaimed"));
            var crate=world.Entities.Last(e=>e.Alive&&e.def.id=="crate_wood");Assert.AreEqual(75,crate.remaining);Assert.AreEqual(0,crate.owner);
            // Isolate the crate-gather step from the rival explorer after the contest.
            world.Remove(b);var worker=world.Entities.First(e=>e.owner==0&&e.Worker);worker.position=crate.position+Vector2.down;world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Gather,target=crate.id}).accepted);
            Tick(10);Assert.Greater(world.Players[0].resources.wood,opening.wood);Assert.AreEqual(1,world.Events.Count(e=>e.kind=="TreasureClaimed"));
        }
        [Test]public void PausingAndCancellingClaimCannotKeepPartialRewardProgress()
        {
            var cache=world.Get(world.Treasures.First(t=>t.definition.guards==0).entity);var explorer=Explorer(0);explorer.position=cache.position+Vector2.left;world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(Claim(explorer,cache)).accepted);Tick(1);var position=world.CaravanPosition;world.Paused=true;Tick(10);
            Assert.AreEqual(position,world.CaravanPosition);Assert.IsTrue(cache.Alive);world.Paused=false;
            world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{explorer.id},order=OrderKind.Stop});Assert.IsTrue(world.Issue(Claim(explorer,cache)).accepted);
            Tick(2);Assert.IsTrue(cache.Alive);Tick(1.2f);Assert.IsFalse(cache.Alive);
            var fresh=new World(Catalog.Load());Assert.AreEqual(0,fresh.Players[0].treasuresClaimed);Assert.IsTrue(fresh.Treasures.All(t=>fresh.Get(t.entity).Alive));
        }
        [Test]public void GuardianUsesCombatAndItsCampLeashWithoutOwningPlayerPopulation()
        {
            var guardian=world.Entities.First(World.Guardian);var explorer=Explorer(0);explorer.position=guardian.position+Vector2.left*1.2f;world.Fog.Tick(world.Entities);
            int population=world.Players[0].usedPopulation;Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{explorer.id},order=OrderKind.Attack,target=guardian.id}).accepted);
            for(int i=0;i<500&&guardian.Alive;i++)world.Tick(.1f);
            Assert.IsFalse(guardian.Alive);Assert.IsTrue(world.Events.Any(e=>e.kind=="Damage"&&e.entity==guardian.id));Assert.AreEqual(population,world.Players[0].usedPopulation);
            Assert.AreEqual(1,world.Players[0].guardiansDefeated);Assert.AreEqual(0,world.Players[0].kills);
        }
        [Test]public void TradingPostRequiresSocketAndPaysOnlyAfterCompletionAndCaravanVisit()
        {
            var socket=world.Get(world.TradeSockets[0].entity);var worker=world.Entities.First(e=>e.owner==0&&e.Worker);world.Fog.Reveal(0,socket.position,12);
            Assert.AreNotEqual("",world.CanBuild(0,"trade_post",new Vector2(36,35)));Assert.AreEqual("",world.CanBuild(0,"trade_post",socket.position));
            float wood=world.Players[0].resources.wood;Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{worker.id},item="trade_post",point=socket.position+Vector2.right}).accepted);
            var post=world.TradingPostAt(socket.id);Assert.AreEqual(socket.position,post.position);Assert.AreEqual(wood-200,world.Players[0].resources.wood);
            world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Stop});Tick(12);Assert.IsFalse(world.Events.Any(e=>e.kind=="TradeDelivery"));
            worker.position=post.position+Vector2.left*3;world.Fog.Tick(world.Entities);Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Build,target=post.id}).accepted);
            Tick(62);Assert.IsTrue(post.Complete);Tick(60);Assert.IsTrue(world.Events.Any(e=>e.kind=="TradeDelivery"&&e.entity==post.id&&e.message=="XP"));
            int count=world.Events.Count(e=>e.kind=="TradeDelivery");world.Remove(post,1);Tick(65);Assert.AreEqual(count,world.Events.Count(e=>e.kind=="TradeDelivery"));
            Assert.IsNull(world.TradingPostAt(socket.id));Assert.IsTrue(world.Navigation.IsWalkable(socket.position,.4f));
        }
        [Test]public void TradeModesNeedPaidResearchAndCreditAnIndependentLedger()
        {
            var socket=world.Get(world.TradeSockets[0].entity);var post=world.Spawn("trade_post",0,socket.position);var p=world.Players[0];p.age=2;p.resources=new Cost(0,200,150);
            var mode=new GameCommand{player=0,kind=CommandKind.TradeMode,actors=new[]{post.id},index=3};Assert.IsFalse(world.Issue(mode).accepted);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{post.id},item="cargo_ledgers"}).accepted);Assert.AreEqual(0,p.resources.coin);Tick(46);
            Assert.IsTrue(world.Issue(mode).accepted);Tick(60);var paid=world.Events.Where(e=>e.kind=="TradeDelivery"&&e.entity==post.id&&e.message=="Coin").ToArray();
            Assert.IsNotEmpty(paid);Assert.IsTrue(paid.All(e=>Mathf.Abs(e.amount-50)<.01f));Assert.AreEqual(paid.Sum(e=>e.amount),p.tradeIncome.coin,.01f);Assert.AreEqual(0,p.gathered.coin);Assert.AreEqual(0,p.passiveIncome.coin);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{post.id},item="cargo_ledgers"}).accepted);
        }
    }
}
