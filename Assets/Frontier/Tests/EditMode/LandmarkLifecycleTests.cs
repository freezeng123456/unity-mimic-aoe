using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class LandmarkLifecycleTests
    {
        World world;
        [SetUp]public void Setup()=>world=new World(Catalog.Load(),314159,"coast","river",5);
        void Tick(float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.1f);i++)world.Tick(.1f);}
        EntityState Worker(int player=0)=>world.Entities.First(e=>e.Alive&&e.owner==player&&e.Worker);
        EntityState Explorer(int player)=>world.Entities.First(e=>e.Alive&&e.owner==player&&(e.def.tags&Tags.Explorer)!=0);
        EntityState Socket()=>world.Get(world.TradeSockets[0].entity);
        EntityState BuildPost()
        {
            // Mechanism fixture: move the existing worker to an authored socket.
            // Resources remain the real configured opening and ordinary payments.
            var worker=Worker();var socket=Socket();worker.position=socket.position+Vector2.left*4;world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{worker.id},item="trade_post",point=socket.position}).accepted);
            return world.TradingPostAt(socket.id);
        }
        void WithdrawBuilder()
        {
            var worker=Worker();Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Stop}).accepted);
            worker.position=new Vector2(29,25);Tick(.5f);
        }
        [Test]public void UnfinishedTradingPostDoesNotProvideVision()
        {
            var post=BuildPost();Assert.IsFalse(post.Complete);WithdrawBuilder();
            var probe=post.position+Vector2.up*14;
            Assert.AreNotEqual(VisibilityState.Visible,world.Fog.At(0,probe),"A paid foundation must not supply the completed Trading Post's vision");
            Assert.AreEqual(0,world.Events.Count(e=>e.kind=="TradeDelivery"&&e.entity==post.id));
        }
        [Test]public void CancellationRebuildAndEnemyDestructionHaveRealPaymentsIncomeAndVisionLifecycle()
        {
            var p=world.Players[0];var first=BuildPost();WithdrawBuilder();Tick(60);
            Assert.IsFalse(first.Complete);Assert.IsFalse(world.Events.Any(e=>e.kind=="TradeDelivery"&&e.entity==first.id));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.CancelBuilding,actors=new[]{first.id}}).accepted);
            Assert.IsFalse(first.Alive);Assert.IsNull(world.TradingPostAt(Socket().id));
            Assert.AreEqual(150,p.refunded.wood,.001f);Assert.AreEqual(250,p.resources.wood,.001f);
            var second=BuildPost();Assert.AreNotEqual(first.id,second.id);Assert.AreEqual(50,p.resources.wood,.001f);Assert.AreEqual(400,p.spent.wood,.001f);
            Tick(63);Assert.IsTrue(second.Complete);WithdrawBuilder();Tick(70);
            Assert.IsTrue(world.Events.Any(e=>e.kind=="TradeDelivery"&&e.entity==second.id&&e.message=="XP"));
            var probe=second.position+Vector2.up*14;Assert.AreEqual(VisibilityState.Visible,world.Fog.At(0,probe));
            var caravan=world.CaravanPosition;int deliveries=world.Events.Count(e=>e.kind=="TradeDelivery");float time=world.Time;
            world.Paused=true;Tick(90);Assert.AreEqual(time,world.Time);Assert.AreEqual(caravan,world.CaravanPosition);Assert.AreEqual(deliveries,world.Events.Count(e=>e.kind=="TradeDelivery"));world.Paused=false;
            // An existing opposing explorer destroys the post through normal
            // attack authority and real damage; no HP or combat bonus is applied.
            var attacker=Explorer(1);attacker.position=second.position+Vector2.right*4;world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{attacker.id},order=OrderKind.Attack,target=second.id}).accepted);
            for(int i=0;i<3200&&second.Alive;i++)world.Tick(.1f);
            Assert.IsFalse(second.Alive,"Ordinary enemy damage must destroy the completed post");Assert.IsTrue(world.Events.Any(e=>e.kind=="Damage"&&e.entity==attacker.id&&e.target==second.id));
            Assert.IsNull(world.TradingPostAt(Socket().id));Assert.IsTrue(world.Navigation.IsWalkable(Socket().position,.4f));
            deliveries=world.Events.Count(e=>e.kind=="TradeDelivery");float xp=p.xp;Tick(90);
            Assert.AreEqual(deliveries,world.Events.Count(e=>e.kind=="TradeDelivery"));Assert.AreEqual(xp,p.xp);
            Assert.AreNotEqual(VisibilityState.Visible,world.Fog.At(0,probe));
            Assert.IsFalse(world.Events.Any(e=>e.kind=="TradeDelivery"&&e.entity==first.id),"Cancelled foundations never earn later income");
            Assert.AreEqual(2,world.Events.Count(e=>e.kind=="Payment"&&e.player==0&&e.message.StartsWith("Foundation Trading Post:")));
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="Refund"&&e.player==0&&e.entity==first.id));
        }
        [Test]public void AllPaidResourceModesAndReturnToXpCreditSeparateLedgers()
        {
            // Exact funding/age fixture isolates the paid Cargo Ledgers lifecycle;
            // it is not evidence of a normal economy reaching age II.
            var p=world.Players[0];p.age=2;p.resources=new Cost(0,400,150);
            var post=BuildPost();Tick(63);Assert.IsTrue(post.Complete);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.TradeMode,actors=new[]{post.id},index=1}).accepted);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{post.id},item="cargo_ledgers"}).accepted);Tick(46);
            Assert.AreEqual(400,p.spent.wood);Assert.AreEqual(150,p.spent.coin);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{post.id},item="cargo_ledgers"}).accepted);
            foreach(int mode in new[]{1,2,3,0})
            {
                var before=p.resources;var trade=p.tradeIncome;int cursor=world.Events.Count;
                Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.TradeMode,actors=new[]{post.id},index=mode}).accepted);Tick(70);
                var paid=world.Events.Skip(cursor).Where(e=>e.kind=="TradeDelivery"&&e.entity==post.id).ToArray();Assert.IsNotEmpty(paid);
                string resource=mode==0?"XP":((ResourceKind)mode).ToString();Assert.IsTrue(paid.All(e=>e.message==resource));
                float amount=mode==0?50:mode==1?75:mode==2?62.5f:50;Assert.IsTrue(paid.All(e=>Mathf.Abs(e.amount-amount)<.001f));
                float total=paid.Sum(e=>e.amount);
                Assert.AreEqual(mode==1?total:0,p.resources.food-before.food,.001f);Assert.AreEqual(mode==2?total:0,p.resources.wood-before.wood,.001f);Assert.AreEqual(mode==3?total:0,p.resources.coin-before.coin,.001f);
                Assert.AreEqual(mode==1?total:0,p.tradeIncome.food-trade.food,.001f);Assert.AreEqual(mode==2?total:0,p.tradeIncome.wood-trade.wood,.001f);Assert.AreEqual(mode==3?total:0,p.tradeIncome.coin-trade.coin,.001f);
                // XP progress can roll into shipment tokens; the positive XP
                // ledger records each award independently of that rollover.
                float awarded=world.Events.Skip(cursor).Where(e=>e.kind=="Experience"&&e.entity==post.id&&e.message=="Trade route delivery").Sum(e=>e.amount);
                Assert.AreEqual(mode==0?total:0,awarded,.001f);Assert.AreEqual(0,p.gathered.food+p.gathered.wood+p.gathered.coin);Assert.AreEqual(0,p.passiveIncome.food+p.passiveIncome.wood+p.passiveIncome.coin);
            }
        }
        [TestCase(0)][TestCase(1)]public void NaturalFailureThenFreshWorldCannotRetainTradeTreasureOrDiscoveryState(int loser)
        {
            // Scoped end-state fixture exercises real elimination and a fresh
            // world, without presenting its teleports as a played whole match.
            var post=BuildPost();Tick(63);Tick(70);Assert.IsTrue(world.Events.Any(e=>e.kind=="TradeDelivery"));
            var cache=world.Get(world.Treasures.First(t=>t.definition.guards==0).entity);var explorer=Explorer(0);explorer.position=cache.position+Vector2.left;world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{explorer.id},order=OrderKind.Claim,target=cache.id}).accepted);Tick(3.5f);Assert.AreEqual(1,world.Players[0].treasuresClaimed);
            foreach(var e in world.Entities.Where(e=>e.Alive&&e.owner==loser&&e.def.kind!=EntityKind.Resource).ToArray())world.Remove(e,1-loser);
            Tick(.1f);Assert.AreEqual(1-loser,world.Winner);int cursor=world.Events.Count;Tick(70);Assert.AreEqual(cursor,world.Events.Count,"Ended worlds cannot continue paying trade");
            var fresh=new World(Catalog.Load(),271828,"coast","river",5);
            Assert.AreEqual(-1,fresh.Winner);Assert.AreEqual(0,fresh.Time);Assert.AreEqual(0,fresh.Players[0].treasuresClaimed);Assert.AreEqual(0,fresh.Players[1].treasuresClaimed);
            Assert.IsTrue(fresh.Treasures.All(t=>fresh.Get(t.entity).Alive&&t.guards.All(id=>fresh.Get(id).Alive)));
            Assert.IsTrue(fresh.TradeSockets.All(t=>fresh.TradingPostAt(t.entity)==null));Assert.AreEqual(Catalog.Load().landmarks.route[0],fresh.CaravanPosition);
            Assert.IsFalse(fresh.Events.Any(e=>e.kind=="TradeDelivery"||e.kind=="TradeMode"||e.kind=="TreasureClaimed"||e.kind=="MatchEnded"));
            Assert.AreEqual(0,fresh.Players[0].tradeIncome.food+fresh.Players[0].tradeIncome.wood+fresh.Players[0].tradeIncome.coin);
            Assert.AreNotEqual(VisibilityState.Visible,fresh.Fog.At(0,post.position));
        }
    }
}
