using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ScoutingProduction075Tests
    {
        // Controlled peaceful economy fixture. Only resources/age are prepared;
        // all scout units under acceptance are produced by paid commands.
        static World Prepared(string civilization="river",int age=1)
        {
            var world=new World(SnapshotCodec.Clone(Catalog.Load()),314159,civilization,"coast",5);
            world.Players[0].resources=new Cost(10000,10000,10000);world.Players[0].age=age;
            return world;
        }
        static EntityState Own(World w,string id)=>w.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id==id);
        static string State(World w)=>JsonUtility.ToJson(w.CaptureState());
        static void Steps(World w,int count){for(int i=0;i<count;i++)w.Tick(.1f);}
        static CommandResult Train(World w,EntityState producer,string item)=>w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{producer.id},item=item});
        static void Accepted(CommandResult result)=>Assert.IsTrue(result.accepted,result.reason);
        static EntityState PaidScout(World w,string id)
        {
            var producer=Own(w,w.Rules.Entity(id).producer);int before=w.Entities.Count;
            Accepted(Train(w,producer,id));w.TickEconomy(w.Rules.Entity(id).time+.1f);
            return w.Entities.Skip(before).Single(e=>e.Alive&&e.owner==0&&e.def.id==id);
        }
        static void Order(World w,EntityState actor,OrderKind kind,Vector2 point=default,int target=0)
            =>Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{actor.id},order=kind,point=point,target=target}));

        [TestCase("river","envoy","explorer",0,0)]
        [TestCase("france","native_scout","town",1,25)]
        public void RealPaidQueueReservesOnlyConfiguredPopulationAndCancellationRefunds(string civ,string unit,string producerId,int population,int wood)
        {
            var w=Prepared(civ);var p=w.Players[0];var producer=Own(w,producerId);var before=p.resources;int used=p.usedPopulation;
            Assert.IsFalse(w.Entities.Any(e=>e.def.id==unit),"No free starting scout is added");
            Accepted(Train(w,producer,unit));Assert.AreEqual(before.food-50,p.resources.food);Assert.AreEqual(before.wood-wood,p.resources.wood);
            Assert.AreEqual(used,p.usedPopulation);Assert.AreEqual(population,p.reservedPopulation);Assert.AreEqual(1,w.UnitCountIncludingProduction(0,unit));
            var payment=w.Events.Last(e=>e.kind=="Payment"&&e.entity==producer.id);Assert.IsTrue(payment.hasEconomy);
            Assert.AreEqual(-50,payment.economy.actualDelta.food);Assert.AreEqual(-wood,payment.economy.actualDelta.wood);
            w.TickEconomy(2);Assert.AreEqual(2,producer.queue[0].elapsed,.001f);
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{producer.id},index=0}));
            Assert.AreEqual(before,p.resources);Assert.AreEqual(0,p.reservedPopulation);Assert.AreEqual(0,w.UnitCountIncludingProduction(0,unit));
            Assert.IsEmpty(producer.queue);Assert.IsTrue(w.Events.Any(e=>e.kind=="Refund"&&e.entity==producer.id));
        }

        [TestCase(1,3)][TestCase(2,5)][TestCase(3,8)][TestCase(4,13)][TestCase(5,18)]
        public void EnvoyLimitCountsRealLivingUnitsAndQueuesAcrossProducers(int age,int limit)
        {
            var w=Prepared("river",age);var first=Own(w,"explorer");
            // A second producer isolates the global cap from any one queue.
            var second=w.Spawn("explorer",0,new Vector2(44,24));
            for(int i=0;i<limit-2;i++)PaidScout(w,"envoy");
            Accepted(Train(w,first,"envoy"));Accepted(Train(w,second,"envoy"));
            Assert.AreEqual(limit,w.UnitCountIncludingProduction(0,"envoy"));var funds=w.Players[0].resources;
            var denied=Train(w,second,"envoy");Assert.IsFalse(denied.accepted);StringAssert.Contains("limit",denied.reason);
            Assert.AreEqual(funds,w.Players[0].resources);Assert.AreEqual(0,w.Players[0].reservedPopulation);
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{first.id},index=0}));
            Accepted(Train(w,second,"envoy"));Assert.AreEqual(limit,w.UnitCountIncludingProduction(0,"envoy"));
            w.Remove(second);Assert.IsEmpty(second.queue);Assert.AreEqual(limit-2,w.UnitCountIncludingProduction(0,"envoy"));
            Accepted(Train(w,first,"envoy"));
        }

        [TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        public void NativeScoutCapIncludesAllFourPaidReservationsAtEveryAge(int age)
        {
            var w=Prepared("france",age);var town=Own(w,"town");var p=w.Players[0];int used=p.usedPopulation;
            for(int i=0;i<4;i++)Accepted(Train(w,town,"native_scout"));
            var paid=p.resources;Assert.IsFalse(Train(w,town,"native_scout").accepted);Assert.AreEqual(paid,p.resources);
            Assert.AreEqual(4,p.reservedPopulation);Assert.AreEqual(4,w.UnitCountIncludingProduction(0,"native_scout"));
            w.TickEconomy(18);Assert.AreEqual(used+1,p.usedPopulation);Assert.AreEqual(3,p.reservedPopulation);
            Assert.IsFalse(Own(w,"native_scout").Military);Assert.IsTrue(Own(w,"native_scout").Scout);
            w.Remove(town);Assert.AreEqual(0,p.reservedPopulation);Assert.AreEqual(1,w.UnitCountIncludingProduction(0,"native_scout"));
            Assert.AreEqual(paid,p.resources,"Destroying a producer loses the paid queues without a refund");
        }

        [Test] public void ProducerCivilizationCompletionOwnershipHousingAndResearchCannotBeBypassed()
        {
            var w=Prepared();var explorer=Own(w,"explorer");var town=Own(w,"town");var worker=w.Entities.First(e=>e.owner==0&&e.Worker);var funds=w.Players[0].resources;
            Assert.IsFalse(Train(w,town,"envoy").accepted);Assert.IsFalse(Train(w,worker,"envoy").accepted);
            Assert.IsFalse(Train(w,w.Entities.First(e=>e.owner==1&&e.def.id=="explorer"),"envoy").accepted);
            Assert.IsFalse(Train(w,explorer,"native_scout").accepted);Assert.IsFalse(Train(w,town,"native_scout").accepted);
            explorer.construction=.5f;Assert.IsFalse(Train(w,explorer,"envoy").accepted);explorer.construction=1;
            Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{explorer.id},item=w.Rules.techs[0].id}).accepted);
            Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{explorer.id},choice="economic"}).accepted);
            Assert.AreEqual(funds,w.Players[0].resources);Assert.IsEmpty(explorer.queue);
            foreach(string civilization in new[]{"coast","france","ottoman","wayfarer","highland"})
            {var other=Prepared(civilization);Assert.IsFalse(Train(other,Own(other,"explorer"),"envoy").accepted,civilization);}
            var french=Prepared("france");french.Players[0].populationCap=french.Players[0].usedPopulation;
            var before=french.Players[0].resources;Assert.IsFalse(Train(french,Own(french,"town"),"native_scout").accepted);
            Assert.AreEqual(before,french.Players[0].resources);Assert.AreEqual(0,french.Players[0].reservedPopulation);
            w.Players[0].resources=new Cost(49,10000,10000);Assert.IsFalse(Train(w,explorer,"envoy").accepted);
        }

        [Test] public void ActualGarrisonPausesExplorerQueueAndColdLoadResumesItOnlyAfterExit()
        {
            var w=Prepared();var explorer=Own(w,"explorer");var town=Own(w,"town");Accepted(Train(w,explorer,"envoy"));
            Order(w,explorer,OrderKind.Garrison,target:town.id);
            for(int i=0;i<120&&explorer.garrisonedIn==0;i++)w.Tick(.1f);
            Assert.AreEqual(town.id,explorer.garrisonedIn);Assert.AreEqual(1,explorer.queue.Count);
            float elapsed=explorer.queue[0].elapsed;w.TickEconomy(30);Assert.AreEqual(elapsed,explorer.queue[0].elapsed);
            Assert.IsFalse(Train(w,explorer,"envoy").accepted);var cold=World.Restore(w.Rules,JsonUtility.FromJson<WorldSnapshot>(State(w)));
            Assert.AreEqual(State(w),State(cold));
            foreach(var world in new[]{w,cold})
            {
                Accepted(world.Issue(new GameCommand{player=0,kind=CommandKind.Ungarrison,actors=new[]{town.id}}));
                world.TickEconomy(30);Assert.AreEqual(0,world.Get(explorer.id).garrisonedIn);
                Assert.AreEqual(1,world.Entities.Count(e=>e.owner==0&&e.def.id=="envoy"));Assert.IsEmpty(world.Get(explorer.id).queue);
            }
            Assert.AreEqual(State(w),State(cold),"Queue, payment, population and actual ejection continue identically");
        }

        [Test] public void EnclosedMobileProducerRetainsPaidQueueInsteadOfSpawningThroughTheWall()
        {
            var w=Prepared();var explorer=Own(w,"explorer");Accepted(Train(w,explorer,"envoy"));var funds=w.Players[0].resources;
            var nav=w.Navigation.CaptureState();
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)if(x!=0||y!=0)w.Navigation.SetBlocked(explorer.position+new Vector2(x,y),0,true);
            Assert.IsTrue(w.Navigation.IsWalkable(explorer.position,explorer.def.radius));
            w.TickEconomy(20);Assert.AreEqual("Production exit blocked",explorer.status);Assert.AreEqual(1,explorer.queue.Count);
            Assert.AreEqual(1,w.UnitCountIncludingProduction(0,"envoy"));Assert.IsFalse(w.Entities.Any(e=>e.def.id=="envoy"));
            Assert.AreEqual(funds,w.Players[0].resources);w.Navigation.RestoreState(nav);w.TickEconomy(.1f);
            var envoy=Own(w,"envoy");Assert.IsEmpty(explorer.queue);Assert.IsTrue(w.Navigation.ClearLine(explorer.position,envoy.position,envoy.def.radius));
            Assert.AreEqual(funds,w.Players[0].resources);
        }

        [Test] public void MovingExplorerProducesAtItsActualLocationThenScoutPhysicallyFollowsItsRally()
        {
            var w=Prepared();var explorer=Own(w,"explorer");var origin=explorer.position;var rally=new Vector2(42,18);
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{explorer.id},point=rally}));
            Accepted(Train(w,explorer,"envoy"));Order(w,explorer,OrderKind.Move,new Vector2(44,27));Steps(w,20);
            Assert.Greater(Vector2.Distance(origin,explorer.position),2,"Producer follows an actual Move order during production");
            var birthOrigin=explorer.position;w.TickEconomy(15);var envoy=Own(w,"envoy");var birth=envoy.position;
            Assert.Less(Vector2.Distance(birth,birthOrigin),9);Assert.IsTrue(w.Navigation.ClearLine(birthOrigin,birth,envoy.def.radius));
            Assert.AreEqual(OrderKind.Move,envoy.orders.Peek().kind);var goal=envoy.orders.Peek().point;
            Assert.Greater(Vector2.Distance(birth,goal),2,"Rally assignment must not teleport the newborn");
            for(int i=0;i<400&&envoy.orders.Count>0;i++)w.Tick(.1f);
            Assert.Less(Vector2.Distance(envoy.position,goal),1);Assert.Greater(Vector2.Distance(birth,envoy.position),2);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="UnitTrained"&&e.entity==explorer.id&&e.target==envoy.id));
        }

        [Test] public void EnvoyBuildsOnlyPaidVisibleTradePostAndReallyPerformsConstructionWork()
        {
            var w=Prepared();var envoy=PaidScout(w,"envoy");
            // Peaceful construction fixture: remove treasure guards, not economic
            // prerequisites or map obstacles; the Envoy travels the actual map.
            foreach(var guard in w.Entities.Where(e=>(e.def.tags&Tags.Guardian)!=0).ToArray())w.Remove(guard);
            var site=w.Get(w.TradeSockets[0].entity);var destination=site.position+Vector2.down*6;
            Assert.AreNotEqual("",w.CanBuild(0,"trade_post",site.position),"Unseen distant socket must not permit a foundation");
            Order(w,envoy,OrderKind.Move,destination);
            for(int i=0;i<1800&&Vector2.Distance(envoy.position,destination)>1;i++)w.Tick(.1f);
            Assert.Less(Vector2.Distance(envoy.position,destination),1);Assert.IsTrue(w.IsVisible(0,site));
            Assert.IsTrue(w.CanConstruct(envoy,"trade_post"));Assert.IsFalse(w.CanConstruct(envoy,"house"));Assert.IsFalse(w.CanConstruct(envoy,"factory"));
            var funds=w.Players[0].resources;
            Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{envoy.id},item="house",point=envoy.position+Vector2.right*6}).accepted);
            Assert.AreEqual(funds,w.Players[0].resources);
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{envoy.id},item="trade_post",point=site.position}));
            var post=Own(w,"trade_post");Assert.AreEqual(funds.wood-200,w.Players[0].resources.wood);Assert.AreEqual(200,post.paidConstruction.wood);
            bool worked=false;for(int i=0;i<900&&!post.Complete;i++){w.Tick(.1f);worked|=envoy.IsWorkingAt(w.Time)&&envoy.work.kind==WorkKind.Build&&envoy.work.target==post.id;}
            Assert.IsTrue(worked,"New constructor capability must produce real work telemetry");Assert.IsTrue(post.Complete);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="BuildingComplete"&&e.entity==post.id));
            var berry=w.Entities.First(e=>e.def.id=="berry");w.Fog.Reveal(0,berry.position,4);
            Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{envoy.id},order=OrderKind.Gather,target=berry.id}).accepted);
            post.hp-=10;Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{envoy.id},order=OrderKind.Repair,target=post.id}).accepted);
            var french=Prepared("france");var native=PaidScout(french,"native_scout");Assert.IsFalse(french.CanConstruct(native,"trade_post"));
            foreach(var pair in new[]{(world:w,actor:envoy),(world:french,actor:native)})
            {
                var treasure=pair.world.Entities.First(e=>e.def.id.StartsWith("treasure_"));pair.world.Fog.Reveal(0,treasure.position,10);
                Assert.IsFalse(pair.world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{pair.actor.id},order=OrderKind.Claim,target=treasure.id}).accepted);
                Assert.IsFalse(pair.actor.Worker);Assert.IsFalse((pair.actor.def.tags&Tags.Explorer)!=0);
            }
        }

        [Test] public void StealthAndPendingPaidScoutProductionSurviveActualCompressedSaveAndContinueExactly()
        {
            var w=Prepared("france");var scout=PaidScout(w,"native_scout");Order(w,scout,OrderKind.Hold);
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.ToggleStealth,actors=new[]{scout.id}}));
            Assert.IsTrue(scout.stealth);Assert.AreEqual(3,w.Speed(scout),.001f);
            var town=Own(w,"town");Accepted(Train(w,town,"native_scout"));var sim=new MatchSimulation(w,Array.Empty<int>());
            for(int i=0;i<25;i++)Assert.IsTrue(sim.Step());Assert.Greater(town.queue[0].elapsed,0);
            string directory=Path.Combine(Path.GetTempPath(),"frontier-scout-075-"+Guid.NewGuid().ToString("N"));string path=MatchSaveStore.SlotPath(directory,3);
            try
            {
                MatchSaveStore.Write(path,new SavedMatch{matchId="scouting-075",appVersion="075-fixture",world=w.CaptureState(),simulation=sim.CaptureState()});
                var loaded=MatchSaveStore.Read(path,"075-fixture");var cold=World.Restore(w.Rules,loaded.world);var resumed=MatchSimulation.Restore(cold,loaded.simulation);
                Assert.AreEqual(State(w),State(cold));Assert.IsTrue(cold.Get(scout.id).stealth);
                Assert.AreEqual(scout.stealthReadyAt,cold.Get(scout.id).stealthReadyAt);Assert.AreEqual(scout.revealedUntil,cold.Get(scout.id).revealedUntil);
                for(int i=0;i<200;i++){Assert.IsTrue(sim.Step());Assert.IsTrue(resumed.Step());}
                Assert.AreEqual(State(w),State(cold));Assert.AreEqual(JsonUtility.ToJson(sim.CaptureState()),JsonUtility.ToJson(resumed.CaptureState()));
                Assert.AreEqual(2,w.Entities.Count(e=>e.owner==0&&e.def.id=="native_scout"));Assert.AreEqual(0,w.Players[0].reservedPopulation);
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }

        [Test] public void InvalidStealthAndUnitProducerPayloadsAreRejectedBeforeRestore()
        {
            var w=Prepared();var explorer=Own(w,"explorer");Accepted(Train(w,explorer,"envoy"));
            void Rejected(Action<WorldSnapshot> change)
            {var s=w.CaptureState();change(s);Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,s));}
            Rejected(s=>s.entities.Single(x=>x.state.id==explorer.id).state.stealth=true);
            Rejected(s=>s.entities.Single(x=>x.state.id==explorer.id).state.stealthReadyAt=float.NaN);
            Rejected(s=>s.entities.Single(x=>x.state.id==explorer.id).state.revealedUntil=-1);
            Rejected(s=>{var job=s.entities.Single(x=>x.state.id==explorer.id).queue[0];job.kind="Research";job.item=w.Rules.techs[0].id;});
            Rejected(s=>s.entities.Single(x=>x.state.id==explorer.id).queue[0].item="native_scout");
            Rejected(s=>s.entities.Single(x=>x.state.id==explorer.id).queue[0].population=1);
            Rejected(s=>s.entities.Single(x=>x.state.id==explorer.id).orders=new[]{new UnitOrder(OrderKind.Scout,explorer.position){target=explorer.id}});
            Rejected(s=>s.entities.Single(x=>x.state.id==explorer.id).orders=new[]{new UnitOrder(OrderKind.Scout,explorer.position){elapsed=.5f}});
        }
    }
}
