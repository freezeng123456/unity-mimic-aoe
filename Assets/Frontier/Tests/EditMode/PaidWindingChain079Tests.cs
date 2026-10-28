using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Normal opening economy, controlled peaceful opponent, public simulation commands.
    // No actor/fund/age grants, fog reveals, pose writes or topology changes.
    public sealed class PaidWindingChain079Tests
    {
        sealed class Fixture
        {
            public readonly World W=new World(Catalog.Load(),42,"coast","coast",2,MapTopology.Winding);
            public readonly EntityState[] Workers;
            public readonly EntityState Explorer,Town;
            public readonly string RulesHash;
            public Fixture()
            {
                Assert.AreEqual(new Cost(300,300,100),W.Players[0].resources);Assert.AreEqual(1,W.Players[0].age);
                Workers=W.Entities.Where(e=>e.Alive&&e.owner==0&&e.Worker).ToArray();Assert.AreEqual(6,Workers.Length);
                Explorer=W.Entities.Single(e=>e.Alive&&e.owner==0&&(e.def.tags&Tags.Explorer)!=0);
                Town=W.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id=="town");RulesHash=SnapshotCodec.RulesHash(W.Rules);
                foreach(var actor in W.Entities.Where(e=>e.Alive&&e.def.kind==EntityKind.Unit&&(e.owner==1||e.id==Explorer.id)))
                    Accepted(W.Issue(new GameCommand{player=actor.owner,kind=CommandKind.Order,actors=new[]{actor.id},order=OrderKind.Hold}));
            }
            public void Order(EntityState actor,OrderKind kind,int target=0,Vector2 point=default(Vector2))=>
                Accepted(W.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{actor.id},order=kind,target=target,point=point}));
            public void Step()
            {
                Assert.Less(W.Time,1400,"Fixed total normal-chain budget");Assert.IsFalse(W.IsMatchEnded);
                var before=W.Entities.Where(e=>e.Alive&&e.owner==0&&e.def.kind==EntityKind.Unit)
                    .ToDictionary(e=>e.id,e=>(position:e.position,container:e.garrisonedIn));int cursor=W.Events.Count;W.Tick(.1f);
                Assert.IsFalse(W.IsMatchEnded);Assert.IsFalse(W.Events.Skip(cursor).Any(e=>e.kind=="Fire"||e.kind=="Damage"),"Peaceful chain contaminated by combat");
                foreach(var actor in W.Entities.Where(e=>e.Alive&&e.owner==0&&e.def.kind==EntityKind.Unit))
                {
                    if(actor.garrisonedIn!=0)continue;var nav=W.NavigationFor(actor);
                    Assert.IsTrue(nav.IsWalkable(actor.position,actor.def.radius),Describe(actor));
                    Assert.IsTrue(actor.def.waterborne?W.IsWater(actor.position):!W.IsWater(actor.position)||W.IsBridge(actor.position),Describe(actor));
                    if(!before.TryGetValue(actor.id,out var old)||old.container!=0)continue;
                    Assert.IsTrue(nav.ClearLine(old.position,actor.position,actor.def.radius),Describe(actor));
                    float moved=Vector2.Distance(old.position,actor.position);Assert.LessOrEqual(moved,W.Speed(actor)*.1f+.0001f,Describe(actor));
                    Assert.LessOrEqual(Mathf.Abs(W.SurfaceHeight(old.position,actor.def.waterborne)-W.SurfaceHeight(actor.position,actor.def.waterborne)),moved*.4f+.0002f,Describe(actor));
                }
                Assert.IsTrue(Workers.All(e=>e.Alive)&&Explorer.Alive,"Original workers/explorer survive the chain");
            }
            public void Until(Func<bool> done,float seconds,string reason)
            {for(int i=0;i<Mathf.CeilToInt(seconds*10)&&!done();i++)Step();Assert.IsTrue(done(),reason+" t="+W.Time+" funds="+W.Players[0].resources);}
            public void StopGatherers()
            {foreach(var actor in W.Entities.Where(e=>e.Alive&&e.owner==0&&e.def.kind==EntityKind.Unit&&e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Gather).ToArray())Order(actor,OrderKind.Stop);}
            public void Ensure(Cost need)
            {
                foreach(var resource in new[]{ResourceKind.Food,ResourceKind.Wood,ResourceKind.Coin})
                {
                    float Amount(Cost c)=>resource==ResourceKind.Food?c.food:resource==ResourceKind.Wood?c.wood:c.coin;
                    int cursor=W.Events.Count;float gathered=Amount(W.Players[0].gathered);
                    for(int step=0;step<4000&&Amount(W.Players[0].resources)+.001f<Amount(need);step++)
                    {
                        foreach(var worker in Workers)
                        {
                            if(worker.orders.Count>0&&worker.orders.Peek().kind==OrderKind.Gather&&W.Get(worker.orders.Peek().target)?.def.resource==resource)continue;
                            var source=W.Entities.Where(e=>e.def.resource==resource&&W.GatherableResource(0,e)&&W.CanGather(worker,e))
                                .OrderBy(e=>(e.position-worker.position).sqrMagnitude).ThenBy(e=>e.id).FirstOrDefault();
                            Assert.IsNotNull(source,"A physically visible normal resource is required: "+resource);Order(worker,OrderKind.Gather,source.id);
                        }
                        Step();
                    }
                    Assert.GreaterOrEqual(Amount(W.Players[0].resources)+.001f,Amount(need),"Fixed400s gather phase: "+resource);StopGatherers();
                    if(Amount(W.Players[0].gathered)>gathered)Assert.IsTrue(W.Events.Skip(cursor).Any(e=>e.kind=="Gather"&&e.player==0&&e.amount>0));
                }
                Assert.IsTrue(W.CanAfford(0,need));
            }
            public void Paid(GameCommand command,Cost cost)
            {
                Ensure(cost);var before=W.Players[0].resources;var spent=W.Players[0].spent;int cursor=W.Events.Count;
                Accepted(W.Issue(command));EqualCost(new Cost(before.food-cost.food,before.wood-cost.wood,before.coin-cost.coin),W.Players[0].resources);
                EqualCost(spent+cost,W.Players[0].spent);var payment=W.Events.Skip(cursor).Single(e=>e.kind=="Payment"&&e.player==0);
                Assert.IsTrue(payment.hasEconomy);EqualCost(cost*-1,payment.economy.actualDelta);EqualCost(before,payment.economy.balanceBefore);
            }
            public EntityState Build(string id)
            {
                var definition=W.Rules.Entity(id);Ensure(W.EntityCost(0,definition));var builder=Workers[0];Vector2 site=Vector2.zero;bool found=false;
                if(id=="dock")foreach(var candidate in W.Layout.DockSites[0].OrderBy(p=>(p-Town.position).sqrMagnitude))
                {
                    var survey=W.Navigation.NearestWalkable(candidate-W.Layout.WaterDirection(candidate)*4,builder.def.radius);Order(builder,OrderKind.Move,point:survey);
                    Until(()=>W.Fog.At(0,candidate)==VisibilityState.Visible,90,"Physical dock survey");
                    if(W.CanBuild(0,id,candidate)!="")continue;site=candidate;found=true;break;
                }
                else for(int y=-17;y<=17&&!found;y++)for(int x=-17;x<=17&&!found;x++)
                {var candidate=W.BuildPoint(0,id,Town.position+new Vector2(x,y));if(W.CanBuild(0,id,candidate)==""){site=candidate;found=true;}}
                Assert.IsTrue(found,"No legal visible paid "+id);int cursor=W.Events.Count;
                Paid(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{builder.id},item=id,point=site},W.EntityCost(0,definition));
                var building=W.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id==id);Assert.IsFalse(building.Complete);
                Until(()=>building.Complete,130,"Actual worker construction "+id);
                Assert.IsTrue(W.Events.Skip(cursor).Any(e=>e.kind=="BuildingComplete"&&e.entity==building.id));return building;
            }
            public EntityState Train(EntityState producer,string id)
            {
                var definition=W.Rules.Entity(id);Ensure(W.UnitCost(0,definition));int used=W.Players[0].usedPopulation,reserved=W.Players[0].reservedPopulation,cursor=W.Events.Count;
                Paid(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{producer.id},item=id,count=1},W.UnitCost(0,definition));
                Assert.AreEqual(used,W.Players[0].usedPopulation);Assert.AreEqual(reserved+definition.population,W.Players[0].reservedPopulation);
                Assert.IsTrue(producer.queue.Any(j=>j.kind=="Train"&&j.item==id&&j.count==1));
                Until(()=>W.Events.Skip(cursor).Any(e=>e.kind=="UnitTrained"&&e.entity==producer.id&&e.message==id),definition.time+3,"Paid train "+id);
                var ev=W.Events.Skip(cursor).Single(e=>e.kind=="UnitTrained"&&e.entity==producer.id&&e.message==id);var actor=W.Get(ev.target);
                Assert.IsTrue(actor.Alive&&actor.owner==0&&actor.def.id==id);Assert.AreEqual(used+definition.population,W.Players[0].usedPopulation);
                Assert.AreEqual(reserved,W.Players[0].reservedPopulation);Order(actor,OrderKind.Hold);return actor;
            }
        }
        static void Accepted(CommandResult result)=>Assert.IsTrue(result.accepted,result.reason);
        static void EqualCost(Cost expected,Cost actual)
        {Assert.AreEqual(expected.food,actual.food,.002f);Assert.AreEqual(expected.wood,actual.wood,.002f);Assert.AreEqual(expected.coin,actual.coin,.002f);}
        static string Describe(EntityState actor)=>$"{actor.id}/{actor.def.id} {actor.position} {actor.status} cargo={actor.garrisonedIn}";
        static bool Path(World w,EntityState actor,Vector2 goal)
        {if(w.Navigation.ClearLine(actor.position,goal,actor.def.radius))return true;var path=w.Navigation.FindPath(actor.position,goal,actor.def.radius);return path.Count>0&&Vector2.Distance(path.Last(),goal)<.001f;}
        static Vector2 Shore(World w,EntityState ship,EntityState[] passengers,int bank,Vector2? home=null)
        {
            float radius=passengers.Max(e=>e.def.radius);var origin=home??passengers[0].position;
            foreach(var site in w.Layout.DockSites[bank].OrderBy(p=>(p-origin).sqrMagnitude))
            {
                var side=new Vector2(-w.Layout.WaterDirection(site).y,w.Layout.WaterDirection(site).x);
                foreach(var requested in new[]{site,site+side*10,site-side*10})
                {
                    if(!w.TryTransportShore(ship,requested,radius,out var land,out _))continue;
                    if(home.HasValue&&(Vector2.Distance(land,home.Value)<10||Vector2.Dot(w.Layout.WaterDirection(land),w.Layout.WaterDirection(home.Value))>-.5f))continue;
                    if(!home.HasValue&&!passengers.All(e=>Path(w,e,land)))continue;
                    if(w.Entities.Any(e=>e.Alive&&e.owner==1&&Vector2.Distance(e.position,land)<25))continue;
                    return land;
                }
            }
            Assert.Fail("No reachable quiet actual shore bank="+bank);return Vector2.zero;
        }
        [Test] public void NormalAgeIOpeningPaysForFishingAndMixedCargoCrossesTheWindingRiver()
        {
            var f=new Fixture();var w=f.W;var player=w.Players[0];
            f.Paid(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{f.Town.id},choice="coast_homestead_2"},w.Rules.Age(2).cost);
            Assert.AreEqual(1,player.age);Assert.IsTrue(f.Town.queue.Any(j=>j.kind=="Age"));f.Until(()=>player.age==2,93,"Paid ageII");
            Assert.IsTrue(w.Events.Any(e=>e.kind=="AgeAdvanced"&&e.player==0));var dock=f.Build("dock");var fisher=f.Train(dock,"fishing_boat");
            var fish=w.Entities.Where(e=>e.def.id=="fish"&&w.CanGather(fisher,e)).OrderBy(e=>(e.position-fisher.position).sqrMagnitude).FirstOrDefault();
            Assert.IsNotNull(fish,"Real launched fisher must discover a fish");f.Order(fisher,OrderKind.Gather,fish.id);int cursor=w.Events.Count;
            f.Until(()=>w.Events.Skip(cursor).Any(e=>e.kind=="Gather"&&e.entity==fisher.id&&e.target==fish.id&&e.amount>0),120,"Actual fishing credits food");
            f.StopGatherers();f.Order(fisher,OrderKind.Gather,fish.id);float food=player.resources.food,gathered=player.gathered.food,remaining=fish.remaining;f.Step();
            float delta=remaining-fish.remaining;Assert.Greater(delta,0);Assert.AreEqual(delta,player.resources.food-food,.002f);Assert.AreEqual(delta,player.gathered.food-gathered,.002f);f.Order(fisher,OrderKind.Hold);
            var ship=f.Train(dock,"transport_ship");var barracks=f.Build("barracks");var infantry=f.Train(barracks,"musketeer");var stable=f.Build("stable");var cavalry=f.Train(stable,"hussar");
            var passengers=new[]{infantry,cavalry,f.Workers[0],f.Explorer};var ids=passengers.Select(e=>e.id).ToArray();var home=Shore(w,ship,passengers,0);var landing=Shore(w,ship,passengers,1,home);
            f.StopGatherers();int population=player.usedPopulation;var funds=player.resources;cursor=w.Events.Count;
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=ids,target=ship.id,point=home}));Assert.IsTrue(passengers.All(e=>e.garrisonedIn==0));
            f.Until(()=>passengers.All(e=>e.garrisonedIn==ship.id),150,"Original and paid mixed passengers physically board");
            Assert.AreEqual(5,w.TransportLoad(ship));CollectionAssert.AreEquivalent(ids,ship.occupants);Assert.AreEqual(4,w.Events.Skip(cursor).Count(e=>e.kind=="Embarked"&&e.target==ship.id));
            var sailStart=ship.position;float sailed=0;cursor=w.Events.Count;Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{ship.id},point=landing}));
            Assert.AreEqual(OrderKind.Disembark,ship.orders.Peek().kind);Assert.IsTrue(ship.orders.Peek().hasResolvedPoint);
            for(int step=0;step<1500&&passengers.Any(e=>e.garrisonedIn!=0);step++)
            {var before=ship.position;f.Step();sailed+=Vector2.Distance(before,ship.position);foreach(var actor in passengers.Where(e=>e.garrisonedIn==ship.id))Assert.AreEqual(ship.position,actor.position);}
            Assert.IsTrue(passengers.All(e=>e.Alive&&e.garrisonedIn==0));Assert.IsEmpty(ship.occupants);Assert.Greater(sailed,8);Assert.Greater(Vector2.Distance(sailStart,ship.position),8);
            Assert.AreEqual(4,w.Events.Skip(cursor).Count(e=>e.kind=="Disembarked"&&e.target==ship.id));Assert.AreEqual(population,player.usedPopulation);EqualCost(funds,player.resources);
            foreach(var actor in passengers)
            {
                Assert.IsFalse(w.IsWater(actor.position));Assert.IsTrue(w.Navigation.IsWalkable(actor.position,actor.def.radius));Assert.IsEmpty(actor.orders);
                Assert.Less(Vector2.Distance(actor.position,landing),6.01f);Assert.Less(Vector2.Dot(w.Layout.WaterDirection(actor.position),w.Layout.WaterDirection(home)),-.5f);
                foreach(var other in passengers.Where(e=>e.id!=actor.id))Assert.GreaterOrEqual(Vector2.Distance(actor.position,other.position),actor.def.radius+other.def.radius+.15f-.001f);
            }
            Assert.AreEqual(2,player.age);Assert.AreEqual(f.RulesHash,SnapshotCodec.RulesHash(w.Rules));
            TestContext.WriteLine($"PaidWinding079 t={w.Time:R} home={home} landing={landing} sailed={sailed:R} slots={w.TransportLoad(ship)} delivered={string.Join(",",ids)}; normalAgeI, paidAgeII/fishing/transport/infantry/cavalry, quiet opponent, normal fog");
        }
    }
}
