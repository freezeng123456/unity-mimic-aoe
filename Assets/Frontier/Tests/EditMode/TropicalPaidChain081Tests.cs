using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Both normal AgeI openings use public paid commands and natural fog.
    // Interfering neutral guardians are fought by the original explorer with
    // public Move/Attack before boarding; normal bounty XP, no treasure Claim.
    // The occupied-shore branch is explicitly a cold-cloned mechanics fixture:
    // its spawned blocker bodies never enter or advance the normal paid World.
    public sealed class TropicalPaidChain081Tests
    {
        sealed class Fixture
        {
            public readonly int P; public readonly World W; public World Mirror; public bool ClearingGuardians;
            public readonly EntityState[] Workers;
            public readonly EntityState Explorer,Town;
            public readonly string RulesHash;
            public Fixture(int player)
            {
                P=player;W=new World(Catalog.Load(),42,"coast","coast",2,MapTopology.Tropical);
                Assert.AreEqual(new Cost(300,300,100),W.Players[P].resources);Assert.AreEqual(1,W.Players[P].age);
                Workers=W.Entities.Where(e=>e.Alive&&e.owner==P&&e.Worker).ToArray();Assert.AreEqual(6,Workers.Length);
                Explorer=W.Entities.Single(e=>e.Alive&&e.owner==P&&(e.def.tags&Tags.Explorer)!=0);
                Town=W.Entities.Single(e=>e.Alive&&e.owner==P&&e.def.id=="town");RulesHash=SnapshotCodec.RulesHash(W.Rules);
                foreach(var actor in W.Entities.Where(e=>e.Alive&&e.def.kind==EntityKind.Unit&&(e.owner==1-P||e.id==Explorer.id)))
                    Accepted(W.Issue(new GameCommand{player=actor.owner,kind=CommandKind.Order,actors=new[]{actor.id},order=OrderKind.Hold}));
            }
            public void Order(EntityState actor,OrderKind kind,int target=0,Vector2 point=default(Vector2))=>
                Accepted(W.Issue(new GameCommand{player=P,kind=CommandKind.Order,actors=new[]{actor.id},order=kind,target=target,point=point}));
            public void Step()
            {
                Assert.Less(W.Time,1400,"Fixed total normal-chain budget");Assert.IsFalse(W.IsMatchEnded);
                var before=W.Entities.Where(e=>e.Alive&&e.owner==P&&e.def.kind==EntityKind.Unit)
                    .ToDictionary(e=>e.id,e=>(position:e.position,container:e.garrisonedIn));int cursor=W.Events.Count;W.Tick(.1f);if(Mirror!=null)Mirror.Tick(.1f);
                Assert.IsFalse(W.IsMatchEnded);
                foreach(var ev in W.Events.Skip(cursor).Where(e=>e.kind=="Fire"||e.kind=="Damage"))
                    Assert.IsTrue(ClearingGuardians&&(World.Guardian(W.Get(ev.entity))||World.Guardian(W.Get(ev.target))),"Only explicit neutral-guardian phase permits combat t="+W.Time.ToString("R")+" phase="+(ClearingGuardians?"neutral-clear":"paid-chain")+" event="+ev.kind+" source="+ev.entity+"/"+W.Get(ev.entity)?.def.id+" target="+ev.target+"/"+W.Get(ev.target)?.def.id);
                Assert.AreEqual(0,W.Players[P].losses,"No normal paid actor loss");
                foreach(var actor in W.Entities.Where(e=>e.Alive&&e.owner==P&&e.def.kind==EntityKind.Unit))
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
            {for(int i=0;i<Mathf.CeilToInt(seconds*10)&&!done();i++)Step();Assert.IsTrue(done(),reason+" t="+W.Time+" funds="+W.Players[P].resources);}
            public void StopGatherers()
            {foreach(var actor in W.Entities.Where(e=>e.Alive&&e.owner==P&&e.def.kind==EntityKind.Unit&&e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Gather).ToArray())Order(actor,OrderKind.Stop);}
            public void Ensure(Cost need)
            {
                foreach(var resource in new[]{ResourceKind.Food,ResourceKind.Wood,ResourceKind.Coin})
                {
                    float Amount(Cost c)=>resource==ResourceKind.Food?c.food:resource==ResourceKind.Wood?c.wood:c.coin;
                    int cursor=W.Events.Count;float gathered=Amount(W.Players[P].gathered);
                    for(int step=0;step<4000&&Amount(W.Players[P].resources)+.001f<Amount(need);step++)
                    {
                        foreach(var worker in Workers)
                        {
                            if(worker.orders.Count>0&&worker.orders.Peek().kind==OrderKind.Gather&&W.Get(worker.orders.Peek().target)?.def.resource==resource)continue;
                            var source=W.Entities.Where(e=>e.def.resource==resource&&W.GatherableResource(P,e)&&W.CanGather(worker,e))
                                .OrderBy(e=>(e.position-worker.position).sqrMagnitude).ThenBy(e=>e.id).FirstOrDefault();
                            Assert.IsNotNull(source,"A physically visible normal resource is required: "+resource);Order(worker,OrderKind.Gather,source.id);
                        }
                        Step();
                    }
                    Assert.GreaterOrEqual(Amount(W.Players[P].resources)+.001f,Amount(need),"Fixed400s gather phase: "+resource);StopGatherers();
                    if(Amount(W.Players[P].gathered)>gathered)Assert.IsTrue(W.Events.Skip(cursor).Any(e=>e.kind=="Gather"&&e.player==P&&e.amount>0));
                }
                Assert.IsTrue(W.CanAfford(P,need));
            }
            public void Paid(GameCommand command,Cost cost)
            {
                Ensure(cost);var before=W.Players[P].resources;var spent=W.Players[P].spent;int cursor=W.Events.Count;
                Accepted(W.Issue(command));EqualCost(new Cost(before.food-cost.food,before.wood-cost.wood,before.coin-cost.coin),W.Players[P].resources);
                EqualCost(spent+cost,W.Players[P].spent);var payment=W.Events.Skip(cursor).Single(e=>e.kind=="Payment"&&e.player==P);
                Assert.IsTrue(payment.hasEconomy);EqualCost(cost*-1,payment.economy.actualDelta);EqualCost(before,payment.economy.balanceBefore);
            }
            public EntityState Build(string id)
            {
                var definition=W.Rules.Entity(id);Ensure(W.EntityCost(P,definition));var builder=Workers[0];Vector2 site=Vector2.zero;bool found=false;
                if(id=="dock")foreach(var candidate in W.Layout.DockSites[P].OrderBy(p=>(p-Town.position).sqrMagnitude))
                {
                    var survey=W.Navigation.NearestWalkable(candidate-W.Layout.WaterDirection(candidate)*4,builder.def.radius);Order(builder,OrderKind.Move,point:survey);
                    Until(()=>W.Fog.At(P,candidate)==VisibilityState.Visible,90,"Physical dock survey");
                    if(W.CanBuild(P,id,candidate)!="")continue;site=candidate;found=true;break;
                }
                else for(int y=-17;y<=17&&!found;y++)for(int x=-17;x<=17&&!found;x++)
                {var candidate=W.BuildPoint(P,id,Town.position+new Vector2(x,y));if(W.CanBuild(P,id,candidate)==""){site=candidate;found=true;}}
                Assert.IsTrue(found,"No legal visible paid "+id);int cursor=W.Events.Count;
                Paid(new GameCommand{player=P,kind=CommandKind.Build,actors=new[]{builder.id},item=id,point=site},W.EntityCost(P,definition));
                var building=W.Entities.Single(e=>e.Alive&&e.owner==P&&e.def.id==id);Assert.IsFalse(building.Complete);
                Until(()=>building.Complete,130,"Actual worker construction "+id);
                Assert.IsTrue(W.Events.Skip(cursor).Any(e=>e.kind=="BuildingComplete"&&e.entity==building.id));return building;
            }
            public EntityState Train(EntityState producer,string id)
            {
                var definition=W.Rules.Entity(id);Ensure(W.UnitCost(P,definition));int used=W.Players[P].usedPopulation,reserved=W.Players[P].reservedPopulation,cursor=W.Events.Count;
                Paid(new GameCommand{player=P,kind=CommandKind.Train,actors=new[]{producer.id},item=id,count=1},W.UnitCost(P,definition));
                Assert.AreEqual(used,W.Players[P].usedPopulation);Assert.AreEqual(reserved+definition.population,W.Players[P].reservedPopulation);
                Assert.IsTrue(producer.queue.Any(j=>j.kind=="Train"&&j.item==id&&j.count==1));
                Until(()=>W.Events.Skip(cursor).Any(e=>e.kind=="UnitTrained"&&e.entity==producer.id&&e.message==id),definition.time+3,"Paid train "+id);
                var ev=W.Events.Skip(cursor).Single(e=>e.kind=="UnitTrained"&&e.entity==producer.id&&e.message==id);var actor=W.Get(ev.target);
                Assert.IsTrue(actor.Alive&&actor.owner==P&&actor.def.id==id);Assert.AreEqual(used+definition.population,W.Players[P].usedPopulation);
                Assert.AreEqual(reserved,W.Players[P].reservedPopulation);Order(actor,OrderKind.Hold);return actor;
            }
        }
        static void Accepted(CommandResult result)=>Assert.IsTrue(result.accepted,result.reason);
        static void EqualCost(Cost expected,Cost actual)
        {Assert.AreEqual(expected.food,actual.food,.002f);Assert.AreEqual(expected.wood,actual.wood,.002f);Assert.AreEqual(expected.coin,actual.coin,.002f);}
        static string Describe(EntityState actor)=>$"{actor.id}/{actor.def.id} {actor.position} {actor.status} cargo={actor.garrisonedIn}";
        static string State(World w)=>JsonUtility.ToJson(w.CaptureState());
        static void Cargo(World w,int shipId,int[] ids,bool aboard)
        {
            var ship=w.Get(shipId);Assert.IsTrue(ship.Alive);Assert.AreEqual(aboard?5:0,w.TransportLoad(ship));
            if(aboard)CollectionAssert.AreEquivalent(ids,ship.occupants);else Assert.IsEmpty(ship.occupants);
            Assert.AreEqual(ship.occupants.Count,ship.occupants.Distinct().Count());
            foreach(int id in ids){var e=w.Get(id);Assert.IsTrue(e.Alive);Assert.AreEqual(aboard?ship.id:0,e.garrisonedIn);}
        }
        static void ClearInterferingGuardians(Fixture f,Vector2 home,int other)
        {
            var w=f.W;var points=new[]{home,TropicalBayContent081.Landing(other,1),TropicalBayContent081.HeadlandMine(other).Position,TropicalBayContent081.HeadlandWood(other,0).Position};
            var guards=w.Entities.Where(e=>e.Alive&&World.Guardian(e)&&points.Any(p=>Vector2.Distance(p,e.origin)<=w.Rules.landmarks.guardLeash+2)).OrderBy(e=>(e.origin-f.Explorer.position).sqrMagnitude).ThenBy(e=>e.id).ToArray();
            Assert.Greater(guards.Length,0,"Named shores are tactically guarded, not an unopposed fixture");int cursor=w.Events.Count,before=w.Players[f.P].guardiansDefeated,treasures=w.Players[f.P].treasuresClaimed;float start=w.Time;f.ClearingGuardians=true;
            foreach(var guard in guards)
            {
                if(!guard.Alive)continue;f.Order(f.Explorer,OrderKind.Move,point:guard.position);f.Until(()=>w.Fog.At(f.P,guard.position)==VisibilityState.Visible,60,"Original explorer physically reveals interfering guardian");
                f.Order(f.Explorer,OrderKind.Attack,guard.id);f.Until(()=>!guard.Alive,45,"Public explorer attack clears guardian "+guard.id);Assert.IsTrue(f.Explorer.Alive);
            }
            f.Order(f.Explorer,OrderKind.Hold);f.ClearingGuardians=false;Assert.LessOrEqual(w.Time-start,180,"Fixed neutral-clear phase");Assert.IsTrue(guards.All(e=>!e.Alive));
            Assert.GreaterOrEqual(w.Players[f.P].guardiansDefeated-before,guards.Length);Assert.AreEqual(treasures,w.Players[f.P].treasuresClaimed,"No treasure Claim or reward grant");
            Assert.IsTrue(w.Events.Skip(cursor).Any(e=>e.kind=="Fire"&&e.entity==f.Explorer.id));Assert.IsTrue(w.Events.Skip(cursor).Any(e=>e.kind=="Damage"&&World.Guardian(w.Get(e.target))));
            TestContext.WriteLine($"Tropical081 neutral-clear player={f.P} originalExplorer={f.Explorer.id} guardians={string.Join(",",guards.Select(e=>e.id))} elapsed={w.Time-start:R} remainingHP={f.Explorer.hp:R}; natural vision/public Attack, normal guardian bounty XP, no treasure claim");
        }
        static void OccupiedClone(World normal,int player,int shipId,int[] ids,Vector2 forward,Vector2 alternate)
        {
            string frozen=State(normal);var w=World.Restore(normal.Rules,SnapshotCodec.Clone(normal.CaptureState()));var ship=w.Get(shipId);
            Accepted(w.Issue(new GameCommand{player=player,kind=CommandKind.Disembark,actors=new[]{shipId},point=forward}));
            var shore=ship.orders.Peek().point;int blockers=0;
            foreach(var point in w.Navigation.FormationSlots(shore,256,.5f))
                if((point-shore).sqrMagnitude<=40&&w.Navigation.IsWalkable(point,.4f)){w.Spawn("villager",player,point);blockers++;}
            Assert.Greater(blockers,50,"Explicit mechanical crowd, not modified nav/water");int population=w.Players[player].usedPopulation;var funds=w.Players[player].resources;
            int cursor=w.Events.Count;
            for(int i=0;i<1600&&!(ship.orders.Peek().destinationCursor>=6&&ship.status=="Landing occupied; choose another shore");i++)w.Tick(.1f);
            Assert.AreEqual("Landing occupied; choose another shore",ship.status);Assert.GreaterOrEqual(ship.orders.Peek().destinationCursor,6);Cargo(w,shipId,ids,true);
            Assert.IsFalse(w.Events.Skip(cursor).Any(e=>e.kind=="Disembarked"&&e.target==shipId),"No partial release into an occupied shore");
            Accepted(w.Issue(new GameCommand{player=player,kind=CommandKind.Order,actors=new[]{shipId},order=OrderKind.Stop}));w.Tick(.1f);Assert.IsEmpty(ship.orders);Cargo(w,shipId,ids,true);
            Accepted(w.Issue(new GameCommand{player=player,kind=CommandKind.Disembark,actors=new[]{shipId},point=alternate}));
            for(int i=0;i<1500&&ids.Any(id=>w.Get(id).garrisonedIn!=0);i++)w.Tick(.1f);
            Cargo(w,shipId,ids,false);Assert.AreEqual(4,w.Events.Skip(cursor).Count(e=>e.kind=="Disembarked"&&e.target==shipId));
            Assert.AreEqual(population,w.Players[player].usedPopulation);EqualCost(funds,w.Players[player].resources);
            Assert.AreEqual(frozen,State(normal),"Crowd branch must leave normal paid chain wholly unchanged");
            TestContext.WriteLine($"Tropical081 occupied clone player={player} blockers={blockers} forward={shore} alternate={alternate}; retained all four, Stop, alternate unload; no paid-crowd/native claim");
        }
        static void GatherHeadland(Fixture f,EntityState worker,MapResourceAnchor anchor)
        {
            var w=f.W;var source=w.Entities.Single(e=>e.Alive&&e.def.id==anchor.Definition&&e.position==anchor.Position);
            Assert.IsTrue(w.CanGather(worker,source),"Delivered worker must see and use the named headland resource");
            var funds=w.Players[f.P].resources;var gathered=w.Players[f.P].gathered;float remaining=source.remaining;int cursor=w.Events.Count;
            f.Order(worker,OrderKind.Gather,source.id);f.Until(()=>w.Events.Skip(cursor).Any(e=>e.kind=="Gather"&&e.entity==worker.id&&e.target==source.id&&e.amount>0),90,"Real headland gather "+anchor.Definition);
            float delta=remaining-source.remaining;Assert.Greater(delta,0);var credited=source.def.resource==ResourceKind.Coin?new Cost(0,0,delta):new Cost(0,delta,0);
            EqualCost(funds+credited,w.Players[f.P].resources);EqualCost(gathered+credited,w.Players[f.P].gathered);f.Order(worker,OrderKind.Stop);
            TestContext.WriteLine($"Tropical081 actual headland player={f.P} worker={worker.id} resource={source.id}/{source.def.id} position={source.position} credited={delta:R} t={w.Time:R}");
        }
        [TestCase(0)] [TestCase(1)] public void NormalPaidMixedCargoUsesBothNamedBanksAndColdSavesThenGathersHeadlandCoinAndWood(int playerId)
        {
            Assert.AreEqual("0.24.0",Application.version);var f=new Fixture(playerId);var w=f.W;var player=w.Players[playerId];
            var home=TropicalBayContent081.Landing(playerId,0);int other=1-playerId;var forward=TropicalBayContent081.Landing(other,1);
            ClearInterferingGuardians(f,home,other); // Before resource spending can send workers to the guarded safe forward mine.
            f.Paid(new GameCommand{player=playerId,kind=CommandKind.AdvanceAge,actors=new[]{f.Town.id},choice="coast_homestead_2"},w.Rules.Age(2).cost);
            Assert.AreEqual(1,player.age);Assert.IsTrue(f.Town.queue.Any(j=>j.kind=="Age"));f.Until(()=>player.age==2,93,"Paid ageII");
            Assert.IsTrue(w.Events.Any(e=>e.kind=="AgeAdvanced"&&e.player==playerId));var dock=f.Build("dock");var fisher=f.Train(dock,"fishing_boat");
            var fish=w.Entities.Where(e=>e.def.id=="fish"&&w.CanGather(fisher,e)).OrderBy(e=>(e.position-fisher.position).sqrMagnitude).FirstOrDefault();Assert.IsNotNull(fish);
            f.Order(fisher,OrderKind.Gather,fish.id);int cursor=w.Events.Count;f.Until(()=>w.Events.Skip(cursor).Any(e=>e.kind=="Gather"&&e.entity==fisher.id&&e.target==fish.id&&e.amount>0),120,"Actual fishing credits food");
            f.StopGatherers();f.Order(fisher,OrderKind.Gather,fish.id);float food=player.resources.food,gathered=player.gathered.food,remaining=fish.remaining;f.Step();
            float delta=remaining-fish.remaining;Assert.Greater(delta,0);Assert.AreEqual(delta,player.resources.food-food,.002f);Assert.AreEqual(delta,player.gathered.food-gathered,.002f);f.Order(fisher,OrderKind.Hold);
            var ship=f.Train(dock,"transport_ship");var barracks=f.Build("barracks");var infantry=f.Train(barracks,"musketeer");var stable=f.Build("stable");var cavalry=f.Train(stable,"hussar");
            var passengers=new[]{infantry,cavalry,f.Workers[0],f.Explorer};var ids=passengers.Select(e=>e.id).ToArray();
            f.StopGatherers();int population=player.usedPopulation;var funds=player.resources;cursor=w.Events.Count;
            Accepted(w.Issue(new GameCommand{player=playerId,kind=CommandKind.Embark,actors=ids,target=ship.id,point=home}));Assert.IsTrue(passengers.All(e=>e.garrisonedIn==0));f.Step();
            Assert.IsTrue(passengers.Any(e=>e.garrisonedIn==0),"Cold boarding snapshot is genuinely in progress");f.Mirror=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));
            f.Until(()=>passengers.All(e=>e.garrisonedIn==ship.id),150,"Original and paid mixed passengers physically board");Assert.AreEqual(State(w),State(f.Mirror),"Cold boarding continuation exact");f.Mirror=null;
            Cargo(w,ship.id,ids,true);Assert.AreEqual(4,w.Events.Skip(cursor).Count(e=>e.kind=="Embarked"&&e.target==ship.id));OccupiedClone(w,playerId,ship.id,ids,forward,home);
            var start=ship.position;float sailed=0;cursor=w.Events.Count;Accepted(w.Issue(new GameCommand{player=playerId,kind=CommandKind.Disembark,actors=new[]{ship.id},point=forward}));
            Assert.AreEqual(OrderKind.Disembark,ship.orders.Peek().kind);Assert.AreEqual(forward,ship.orders.Peek().point);Assert.IsTrue(ship.orders.Peek().hasResolvedPoint);f.Step();
            Assert.IsTrue(passengers.All(e=>e.garrisonedIn==ship.id),"Cold sailing snapshot retains all cargo");f.Mirror=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));
            for(int step=0;step<1500&&passengers.Any(e=>e.garrisonedIn!=0);step++)
            {var before=ship.position;f.Step();sailed+=Vector2.Distance(before,ship.position);foreach(var e in passengers.Where(e=>e.garrisonedIn==ship.id))Assert.AreEqual(ship.position,e.position);}
            Cargo(w,ship.id,ids,false);Assert.AreEqual(State(w),State(f.Mirror),"Cold sailing/unload continuation exact, no duplicate funds/resources/cargo");f.Mirror=null;
            Assert.Greater(sailed,8);Assert.Greater(Vector2.Distance(start,ship.position),8);Assert.AreEqual(4,w.Events.Skip(cursor).Count(e=>e.kind=="Disembarked"&&e.target==ship.id));Assert.AreEqual(population,player.usedPopulation);EqualCost(funds,player.resources);
            foreach(var e in passengers)
            {
                Assert.IsFalse(w.IsWater(e.position)||w.IsBridge(e.position)||w.IsBridgeApproach(e.position));Assert.IsTrue(w.Navigation.IsWalkable(e.position,e.def.radius));Assert.IsEmpty(e.orders);Assert.Less(Vector2.Distance(e.position,forward),6.01f);
                var normal=w.Layout.WaterDirection(e.position);var localNormal=w.Layout.WaterDirection(forward);
                Assert.Greater(other==0?normal.x:-normal.x,.25f,"Actual authored destination bank, not a remote river-normal comparison");Assert.Greater(Vector2.Dot(normal,localNormal),.5f,"Unloaded body must remain on the forward shore local bank");
                TestContext.WriteLine($"Tropical081 landed player={playerId} actor={e.id}/{e.def.id} position={e.position} destinationBank={other} normal={normal} forwardNormal={localNormal} localDot={Vector2.Dot(normal,localNormal):R} distance={Vector2.Distance(e.position,forward):R}");
                foreach(var otherActor in passengers.Where(a=>a.id!=e.id))Assert.GreaterOrEqual(Vector2.Distance(e.position,otherActor.position),e.def.radius+otherActor.def.radius+.15f-.001f);
            }
            f.Order(infantry,OrderKind.Hold);f.Order(cavalry,OrderKind.Hold);f.Order(f.Explorer,OrderKind.Hold);
            GatherHeadland(f,f.Workers[0],TropicalBayContent081.HeadlandMine(other));GatherHeadland(f,f.Workers[0],TropicalBayContent081.HeadlandWood(other,0));
            Assert.AreEqual(2,player.age);Assert.AreEqual(f.RulesHash,SnapshotCodec.RulesHash(w.Rules));Assert.AreEqual(population,player.usedPopulation);
            TestContext.WriteLine($"TropicalPaid081 player={playerId} t={w.Time:R} dock={dock.position} near={home} forwardBank={other} forward={forward} sailed={sailed:R} delivered={string.Join(",",ids)} source={Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT")}; normal opening/fog + public guardian combat, public paid ageII/dock/fisher/transport/musketeer/hussar, cold boarding+sailing, real headland coin+wood; no native/FPS claim");
        }
    }
}
