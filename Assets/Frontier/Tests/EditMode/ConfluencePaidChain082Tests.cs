using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Normal P0 AgeI economy, source-bound public paid commands and natural fog.
    // Interfering neutral guardians are fought by the original explorer with
    // public Move/Attack before gathering and port construction; normal bounty XP, no treasure Claim.
    // The occupied-shore branch is explicitly a cold-cloned mechanics fixture:
    // its spawned blocker bodies never enter or advance the normal paid World.
    public sealed class ConfluencePaidChain082Tests
    {
        sealed class Fixture
        {
            public readonly int P; public readonly World W; public World Mirror; public bool ClearingGuardians;
            public readonly EntityState[] Workers;
            public readonly EntityState Explorer,Town;
            public readonly string RulesHash; int placementDiagnostic;
            [Serializable] sealed class PlacementBody
            {public int id,owner,container;public string definition,kind,orders;public Vector2 position;public float radius;public bool physicalOverlap,coarseCellOverlap,buildingOverlap,legalSocketAnchor,placementBlocker;}
            [Serializable] sealed class PlacementRecord
            {public int schema=1,fixtureRevision=5,player;public float time;public string sourceFingerprint,stage,building,canBuild,worldSnapshotSha256;public Vector2 site;public Vector2[] footprintCells;public PlacementBody[] nearbyBodies,legalSocketAnchors,truePlacementBlockers;}
            bool LegalSocketAnchor(EntityDef definition,Vector2 site,EntityState body,Vector2 position)=>
                definition.id=="trade_post"&&body.id==119&&body.def.id=="trade_socket"&&W.SocketAt(site)?.id==body.id&&(position-site).sqrMagnitude<.01f;
            bool Occupies(EntityDef definition,Vector2 site,EntityState body,Vector2 position)
            {
                // Exact production CanBuild exception for this actual socket,
                // not a resource/unit/building blanket exclusion.
                if(LegalSocketAnchor(definition,site,body,position))return false;
                if(W.BuildingFootprintContains(definition,site,position,body.def.radius))return true;
                return W.BuildingFootprintCells(definition,site).Any(cell=>new Vector2(Mathf.Max(0,Mathf.Abs(position.x-cell.x)-.5f),Mathf.Max(0,Mathf.Abs(position.y-cell.y)-.5f)).sqrMagnitude<body.def.radius*body.def.radius+.00001f);
            }
            void PlacementEvidence(string stage,EntityDef definition,Vector2 site)
            {
                var cells=W.BuildingFootprintCells(definition,site).ToArray();
                var bodies=W.Entities.Where(e=>e.Alive&&e.garrisonedIn==0&&((e.position-site).sqrMagnitude<=144||Occupies(definition,site,e,e.position))).OrderBy(e=>e.id).Select(e=>new PlacementBody{id=e.id,owner=e.owner,container=e.garrisonedIn,definition=e.def.id,kind=e.def.kind.ToString(),position=e.position,radius=e.def.radius,legalSocketAnchor=LegalSocketAnchor(definition,site,e,e.position),buildingOverlap=e.def.kind==EntityKind.Building&&W.BuildingFootprintsOverlap(definition,site,e.def,e.position,1),placementBlocker=!LegalSocketAnchor(definition,site,e,e.position)&&(e.def.kind==EntityKind.Building?W.BuildingFootprintsOverlap(definition,site,e.def,e.position,1):Occupies(definition,site,e,e.position)),physicalOverlap=W.BuildingFootprintContains(definition,site,e.position,e.def.radius),coarseCellOverlap=cells.Any(cell=>new Vector2(Mathf.Max(0,Mathf.Abs(e.position.x-cell.x)-.5f),Mathf.Max(0,Mathf.Abs(e.position.y-cell.y)-.5f)).sqrMagnitude<e.def.radius*e.def.radius+.00001f),orders=string.Join(";",e.orders.Select(o=>o.kind+":"+o.target+":"+o.point))}).ToArray();
                var record=new PlacementRecord{player=P,time=W.Time,sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),stage=stage,building=definition.id,canBuild=W.CanBuild(P,definition.id,site),worldSnapshotSha256=SnapshotCodec.Hash(State(W)),site=site,footprintCells=cells,nearbyBodies=bodies,legalSocketAnchors=bodies.Where(b=>b.legalSocketAnchor).ToArray(),truePlacementBlockers=bodies.Where(b=>b.placementBlocker).ToArray()};
                string directory=Environment.GetEnvironmentVariable("FRONTIER082_OUTPUT");if(string.IsNullOrEmpty(directory))directory=Path.Combine(Path.GetTempPath(),"FrontierConfluencePaid082-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,(++placementDiagnostic).ToString("D2")+"-paid-fixedsite-"+definition.id+"-"+stage+".json");File.WriteAllText(path,JsonUtility.ToJson(record,true));
                TestContext.WriteLine("Confluence082 fixed-site diagnostic "+path+" reason="+record.canBuild+" occupying="+string.Join(",",bodies.Where(b=>b.placementBlocker).Select(b=>b.id+"/"+b.definition+"@"+b.position)));
            }
            Vector2 ClearStagingPoint(EntityState actor,EntityDef definition,Vector2 site,Vector2 center)
            {
                foreach(var candidate in W.Navigation.FormationSlots(center,128,Mathf.Max(1.8f,actor.def.radius*4+.3f)))
                {
                    if(Occupies(definition,site,actor,candidate)||!W.Navigation.IsWalkable(candidate,actor.def.radius)||W.IsWater(candidate)||W.IsBridgeApproach(candidate))continue;
                    if(W.Entities.Any(e=>e.Alive&&e.garrisonedIn==0&&e.id!=actor.id&&Vector2.Distance(e.position,candidate)<e.def.radius+actor.def.radius+.25f))continue;
                    var route=W.Navigation.FindPath(actor.position,candidate,actor.def.radius);if(route.Count>0&&Vector2.Distance(route.Last(),candidate)<.05f)return candidate;
                }
                Assert.Fail("No exact reachable public staging point outside fixed foundation for "+Describe(actor));return Vector2.zero;
            }
            void SurveyAndClearFixedSite(EntityDef definition,EntityState builder,Vector2 site)
            {
                float start=W.Time;PlacementEvidence("before-survey",definition,site);
                var direction=definition.id=="dock"?W.Layout.WaterDirection(site):Vector2.right;
                var approach=ClearStagingPoint(builder,definition,site,site-direction*4);Order(builder,OrderKind.Move,point:approach);
                Until(()=>builder.orders.Count==0&&W.Fog.At(P,site)==VisibilityState.Visible,90,"Physical fixed-site survey and actual builder staging "+definition.id);
                Assert.IsFalse(Occupies(definition,site,builder,builder.position),"Builder physically waits outside both exact and coarse foundation");PlacementEvidence("before-clear",definition,site);
                var blockers=W.Entities.Where(e=>e.Alive&&e.garrisonedIn==0&&e.def.kind!=EntityKind.Building&&Occupies(definition,site,e,e.position)).OrderBy(e=>e.id).ToArray();
                foreach(var actor in blockers)
                {
                    Assert.IsTrue(actor.owner==P&&actor.def.kind==EntityKind.Unit&&!actor.def.waterborne,"Fixed-site blocker is not an owned movable land unit: "+Describe(actor));
                    var target=ClearStagingPoint(actor,definition,site,Town.position+(site-Town.position).normalized*7);
                    var before=actor.position;Order(actor,OrderKind.Move,point:target);
                    Until(()=>actor.orders.Count==0&&!Occupies(definition,site,actor,actor.position),Mathf.Max(0,90-(W.Time-start)),"Public Move physically clears fixed-site blocker "+actor.id);Order(actor,OrderKind.Hold);
                    TestContext.WriteLine($"Confluence082 public fixed-site clearance actor={actor.id}/{actor.def.id} from={before} requested={target} actual={actor.position} elapsed={W.Time-start:R}; fixed site/resources/entity identities unchanged");
                }
                Assert.LessOrEqual(W.Time-start,90.001f,"Original fixed-site survey budget includes all public clearance");PlacementEvidence("after-clear",definition,site);
                Assert.AreEqual("",W.CanBuild(P,definition.id,site),"Fixed paid site must really pass every unchanged gate after actual public staging");
            }
            public Fixture(int player)
            {
                P=player;W=new World(Catalog.Load(),42,"coast","coast",3,MapTopology.Tributaries);
                Assert.AreEqual(new Cost(300,300,100),W.Players[P].resources);Assert.AreEqual(1,W.Players[P].age);
                Workers=W.Entities.Where(e=>e.Alive&&e.owner==P&&e.Worker).ToArray();Assert.AreEqual(6,Workers.Length);
                Explorer=W.Entities.Single(e=>e.Alive&&e.owner==P&&(e.def.tags&Tags.Explorer)!=0);
                Town=W.Entities.Single(e=>e.Alive&&e.owner==P&&e.def.id=="town");RulesHash=SnapshotCodec.RulesHash(W.Rules);
                foreach(var actor in W.Entities.Where(e=>e.Alive&&e.def.kind==EntityKind.Unit&&e.id==Explorer.id))
                    Accepted(W.Issue(new GameCommand{player=actor.owner,kind=CommandKind.Order,actors=new[]{actor.id},order=OrderKind.Hold}));
            }
            public void Order(EntityState actor,OrderKind kind,int target=0,Vector2 point=default(Vector2))=>
                Accepted(W.Issue(new GameCommand{player=P,kind=CommandKind.Order,actors=new[]{actor.id},order=kind,target=target,point=point}));
            public void Step()
            {
                Assert.Less(W.Time,1800.0001f,"Fixed total normal-chain budget");Assert.IsFalse(W.IsMatchEnded);
                float dt=Mathf.Min(.1f,1800f-W.Time);Assert.Greater(dt,0,"Fixed total normal-chain budget exhausted before the requested event");
                var before=W.Entities.Where(e=>e.Alive&&e.owner==P&&e.def.kind==EntityKind.Unit)
                    .ToDictionary(e=>e.id,e=>(position:e.position,container:e.garrisonedIn));int cursor=W.Events.Count;W.Tick(dt);if(Mirror!=null)Mirror.Tick(dt);
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
                    float moved=Vector2.Distance(old.position,actor.position);Assert.LessOrEqual(moved,W.Speed(actor)*dt+.0001f,Describe(actor));
                    Assert.LessOrEqual(Mathf.Abs(W.SurfaceHeight(old.position,actor.def.waterborne)-W.SurfaceHeight(actor.position,actor.def.waterborne)),moved*.4f+.0002f,Describe(actor));
                }
                Assert.IsTrue(Workers.All(e=>e.Alive)&&Explorer.Alive,"Original workers/explorer survive the chain");
            }
            public void Until(Func<bool> done,float seconds,string reason)
            {for(int i=0;i<Mathf.CeilToInt(seconds*10)&&!done();i++)Step();if(!done())TestContext.WriteLine(reason+" diagnostics "+string.Join(" | ",W.Entities.Where(e=>e.Alive&&e.owner==P&&e.def.kind==EntityKind.Unit).Select(Describe)));Assert.IsTrue(done(),reason+" t="+W.Time+" funds="+W.Players[P].resources);}
            public void StopGatherers()
            {foreach(var actor in W.Entities.Where(e=>e.Alive&&e.owner==P&&e.def.kind==EntityKind.Unit&&e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Gather).ToArray())Order(actor,OrderKind.Stop);}
            public void Ensure(Cost need)
            {
                foreach(var resource in new[]{ResourceKind.Food,ResourceKind.Wood,ResourceKind.Coin})
                {
                    float Amount(Cost c)=>resource==ResourceKind.Food?c.food:resource==ResourceKind.Wood?c.wood:c.coin;
                    int cursor=W.Events.Count;float gathered=Amount(W.Players[P].gathered);
                    for(int step=0;step<4500&&Amount(W.Players[P].resources)+.001f<Amount(need);step++)
                    {
                        foreach(var worker in Workers)
                        {
                            if(worker.orders.Count>0&&worker.orders.Peek().kind==OrderKind.Gather&&W.Get(worker.orders.Peek().target)?.def.resource==resource)continue;
                            var source=W.Entities.Where(e=>e.def.resource==resource&&W.GatherableResource(P,e)&&W.CanGather(worker,e))
                                // The original deer47 is a real visible food
                                // choice after the public southern survey. It
                                // replaces the nearest food source; it is not
                                // injected income or a late cleanup shortcut.
                                .OrderBy(e=>resource==ResourceKind.Food&&e.id==47?0:1)
                                .ThenBy(e=>(e.position-worker.position).sqrMagnitude).ThenBy(e=>e.id).FirstOrDefault();
                            Assert.IsNotNull(source,"A physically visible normal resource is required: "+resource);Order(worker,OrderKind.Gather,source.id);
                        }
                        Step();
                    }
                    Assert.GreaterOrEqual(Amount(W.Players[P].resources)+.001f,Amount(need),"Fixed450s gather phase: "+resource);StopGatherers();
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
            public EntityState Build(string id,Vector2? fixedSite=null)
            {
                var definition=W.Rules.Entity(id);Ensure(W.EntityCost(P,definition));var builder=Workers[0];Vector2 site=Vector2.zero;bool found=false;
                if(fixedSite.HasValue)
                {
                    site=fixedSite.Value;SurveyAndClearFixedSite(definition,builder,site);found=true;
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
        static void ClearInterferingGuardians(Fixture f)
        {
            var w=f.W;var tradeApproach=w.Get(126);Assert.IsNotNull(tradeApproach);Assert.AreEqual("treasure_coin",tradeApproach.def.id);Assert.AreEqual(new Vector2(37,61),tradeApproach.position);
            // Original southern gather/trade corridor: changing the port must
            // not silently drop its still-present guardian camp from public clearing.
            var points=new[]{ConfluenceContent082.Dock,ConfluenceContent082.SouthLanding,ConfluenceContent082.NorthLanding,ConfluenceContent082.NorthMine.Position,ConfluenceContent082.NorthWood(3).Position,new Vector2(49,42),tradeApproach.position};
            var touched=w.Entities.Where(e=>e.Alive&&World.Guardian(e)&&points.Any(p=>Vector2.Distance(p,e.origin)<=w.Rules.landmarks.guardLeash+2)).Select(e=>e.id).ToHashSet();
            var guardIds=w.Treasures.Where(t=>t.guards.Any(touched.Contains)).SelectMany(t=>t.guards).Concat(touched).ToHashSet();
            var guards=w.Entities.Where(e=>e.Alive&&World.Guardian(e)&&guardIds.Contains(e.id)).OrderBy(e=>(e.origin-f.Explorer.position).sqrMagnitude).ThenBy(e=>e.id).ToArray();
            Assert.Greater(guards.Length,0,"Original port/economy approaches are tactically guarded");int cursor=w.Events.Count,before=w.Players[f.P].guardiansDefeated,treasures=w.Players[f.P].treasuresClaimed;float start=w.Time;f.ClearingGuardians=true;
            foreach(var guard in guards)
            {
                if(!guard.Alive)continue;f.Order(f.Explorer,OrderKind.Move,point:guard.position);f.Until(()=>w.Fog.At(f.P,guard.position)==VisibilityState.Visible,60,"Original explorer physically reveals interfering guardian");
                f.Order(f.Explorer,OrderKind.Attack,guard.id);f.Until(()=>!guard.Alive,45,"Public explorer attack clears guardian "+guard.id);Assert.IsTrue(f.Explorer.Alive);
            }
            f.Order(f.Explorer,OrderKind.Hold);f.ClearingGuardians=false;Assert.LessOrEqual(w.Time-start,180,"Fixed neutral-clear phase");Assert.IsTrue(guards.All(e=>!e.Alive));
            Assert.GreaterOrEqual(w.Players[f.P].guardiansDefeated-before,guards.Length);Assert.AreEqual(treasures,w.Players[f.P].treasuresClaimed,"No treasure Claim or reward grant");
            Assert.IsTrue(w.Events.Skip(cursor).Any(e=>e.kind=="Fire"&&e.entity==f.Explorer.id));Assert.IsTrue(w.Events.Skip(cursor).Any(e=>e.kind=="Damage"&&World.Guardian(w.Get(e.target))));
            TestContext.WriteLine($"Confluence082 neutral-clear player={f.P} originalExplorer={f.Explorer.id} guardians={string.Join(",",guards.Select(e=>e.id))} elapsed={w.Time-start:R} remainingHP={f.Explorer.hp:R}; natural vision/public Attack, normal guardian bounty XP, no treasure claim");
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
            Cargo(w,shipId,ids,false);Assert.AreEqual(2,w.Events.Skip(cursor).Count(e=>e.kind=="Disembarked"&&e.target==shipId));
            Assert.AreEqual(population,w.Players[player].usedPopulation);EqualCost(funds,w.Players[player].resources);
            Assert.AreEqual(frozen,State(normal),"Crowd branch must leave normal paid chain wholly unchanged");
            TestContext.WriteLine($"Confluence082 occupied clone player={player} blockers={blockers} forward={shore} alternate={alternate}; retained both, Stop, alternate unload; no paid-crowd/native claim");
        }
        static void GatherExistingNorthernResource(Fixture f,EntityState worker,MapResourceAnchor anchor)
        {
            var w=f.W;var source=w.Entities.Single(e=>e.Alive&&e.def.id==anchor.Definition&&e.position==anchor.Position);
            Assert.IsTrue(w.CanGather(worker,source),"Delivered worker must see and use the named existing northern pocket resource");
            var funds=w.Players[f.P].resources;var gathered=w.Players[f.P].gathered;float remaining=source.remaining;int cursor=w.Events.Count;
            f.Order(worker,OrderKind.Gather,source.id);f.Until(()=>w.Events.Skip(cursor).Any(e=>e.kind=="Gather"&&e.entity==worker.id&&e.target==source.id&&e.amount>0),90,"Real existing northern pocket gather "+anchor.Definition);
            float delta=remaining-source.remaining;Assert.Greater(delta,0);var credited=source.def.resource==ResourceKind.Coin?new Cost(0,0,delta):new Cost(0,delta,0);
            EqualCost(funds+credited,w.Players[f.P].resources);EqualCost(gathered+credited,w.Players[f.P].gathered);f.Order(worker,OrderKind.Stop);
            TestContext.WriteLine($"Confluence082 actual existing northern pocket player={f.P} worker={worker.id} resource={source.id}/{source.def.id} position={source.position} credited={delta:R} t={w.Time:R}");
        }
        static void Advance(Fixture f,int age)
        {
            var w=f.W;int before=w.Players[f.P].age,cursor=w.Events.Count;
            f.Paid(new GameCommand{player=f.P,kind=CommandKind.AdvanceAge,actors=new[]{f.Town.id},choice="coast_homestead_"+age},w.Rules.Age(age).cost);
            Assert.AreEqual(before,w.Players[f.P].age);Assert.IsTrue(f.Town.queue.Any(j=>j.kind=="Age"));f.Until(()=>w.Players[f.P].age==age,w.Rules.Age(age).time+3,"Actual paid age"+age);
            Assert.IsTrue(w.Events.Skip(cursor).Any(e=>e.kind=="AgeAdvanced"&&e.player==f.P));
        }
        static void NormallyGatherStationBlocker(Fixture f,EntityState socket)
        {
            var w=f.W;var p=w.Players[f.P];var source=w.Get(47);Assert.IsNotNull(source);Assert.AreEqual("deer",source.def.id);Assert.AreEqual(new Vector2(33,53),source.position);
            Assert.LessOrEqual(source.remaining,1400);Assert.GreaterOrEqual(source.remaining,0);f.StopGatherers();w.FlushEconomyLog();
            if(!source.Alive){Assert.AreEqual(0,source.remaining);Assert.IsTrue(w.Events.Any(e=>e.kind=="ResourceDepleted"&&e.entity==source.id));TestContext.WriteLine("Confluence082 station original deer47 was already naturally depleted by preceding normal economy");return;}
            float surveyStart=w.Time;f.Order(f.Workers[0],OrderKind.Move,point:socket.position);f.Until(()=>w.Fog.At(f.P,source.position)==VisibilityState.Visible,90,"Original worker physically surveys station resource obstruction");
            foreach(var worker in f.Workers){Assert.IsTrue(w.CanGather(worker,source));f.Order(worker,OrderKind.Gather,source.id);}
            var initialFunds=p.resources;var initialGathered=p.gathered;var spent=p.spent;int population=p.usedPopulation,cursor=w.Events.Count,ticks=0;float initialRemaining=source.remaining,start=w.Time;
            for(;ticks<4500&&source.Alive;ticks++)
            {
                var funds=p.resources;var gathered=p.gathered;float remaining=source.remaining;f.Step();float credited=remaining-source.remaining;Assert.GreaterOrEqual(credited,0);
                // Check each actual credit rather than comparing thousands of
                // accumulated float additions as if they were bitwise decimals.
                EqualCost(funds+new Cost(credited,0,0),p.resources);EqualCost(gathered+new Cost(credited,0,0),p.gathered);
            }
            Assert.IsFalse(source.Alive,"Original450s resource-gather cap, no manual depletion/removal");Assert.AreEqual(0,source.remaining);f.StopGatherers();w.FlushEconomyLog();
            var receipts=w.Events.Skip(cursor).Where(e=>e.kind=="Gather").ToArray();Assert.IsNotEmpty(receipts);Assert.IsTrue(receipts.All(e=>e.player==f.P&&e.target==source.id&&f.Workers.Any(a=>a.id==e.entity)&&e.amount>0));
            Assert.AreEqual(1,w.Events.Skip(cursor).Count(e=>e.kind=="ResourceDepleted"&&e.entity==source.id));Assert.IsFalse(w.Events.Skip(cursor).Any(e=>e.kind=="Payment"));EqualCost(spent,p.spent);Assert.AreEqual(population,p.usedPopulation);
            Assert.Greater(p.resources.food-initialFunds.food,0);Assert.Greater(p.gathered.food-initialGathered.food,0);Assert.AreEqual(initialFunds.wood,p.resources.wood,.002f);Assert.AreEqual(initialFunds.coin,p.resources.coin,.002f);Assert.AreEqual(initialGathered.wood,p.gathered.wood,.002f);Assert.AreEqual(initialGathered.coin,p.gathered.coin,.002f);
            TestContext.WriteLine($"Confluence082 normal station preparation resource={source.id}/{source.def.id} position={source.position} initialRemaining={initialRemaining:R} remaining={source.remaining:R} foodCredited={p.resources.food-initialFunds.food:R} gatheredCredited={p.gathered.food-initialGathered.food:R} receiptSum={receipts.Sum(e=>(double)e.amount):R} survey={start-surveyStart:R} harvest={w.Time-start:R} ticks={ticks}; all six original workers/public Gather, per-tick0.002 credit gates, actual ResourceDepleted, no source relocation/manual deletion/grant; preparation investment counted in total1800s");
        }
        static void PublicTradeRoundTrip(Fixture f)
        {
            var w=f.W;var socket=w.TradeSockets.Single(s=>s.entity==119);Assert.AreEqual(new Vector2(30,55),w.Get(socket.entity).position);
            NormallyGatherStationBlocker(f,w.Get(socket.entity));f.StopGatherers();var post=f.Build("trade_post",w.Get(socket.entity).position);f.Order(f.Workers[0],OrderKind.Hold);Assert.AreEqual(ResourceKind.None,post.tradeMode);
            var funds=w.Players[f.P].resources;var income=w.Players[f.P].tradeIncome;float xp=w.Players[f.P].xp,start=w.Time;var directions=new HashSet<int>();int deliveries=0;
            for(int step=0;step<1800&&directions.Count<2;step++)
            {
                Vector2 previous=w.CaravanPosition;int cursor=w.Events.Count;f.Step();Assert.LessOrEqual(Vector2.Distance(previous,w.CaravanPosition),w.Rules.landmarks.caravanSpeed*.1f+.0001f);
                foreach(var ev in w.Events.Skip(cursor).Where(e=>e.kind=="TradeDelivery"&&e.entity==post.id&&e.target==socket.entity))
                {
                    var incoming=(w.TradeRoute[socket.waypoint]-previous).normalized;var forward=(w.TradeRoute[socket.waypoint]-w.TradeRoute[socket.waypoint-1]).normalized;
                    var backward=(w.TradeRoute[socket.waypoint]-w.TradeRoute[socket.waypoint+1]).normalized;int direction=Vector2.Dot(incoming,forward)>Vector2.Dot(incoming,backward)?1:-1;
                    Assert.Greater(Mathf.Max(Vector2.Dot(incoming,forward),Vector2.Dot(incoming,backward)),.99f);directions.Add(direction);deliveries++;Assert.AreEqual("XP",ev.message);Assert.AreEqual(w.Rules.landmarks.tradeXp,ev.amount,.001f);
                    TestContext.WriteLine($"Confluence082 public route trade post={post.id} socket={socket.entity} direction={direction} time={w.Time:R} amount={ev.amount:R}");
                }
            }
            CollectionAssert.AreEquivalent(new[]{-1,1},directions);Assert.AreEqual(deliveries*w.Rules.landmarks.tradeXp,w.Players[f.P].xp-xp,.002f);EqualCost(funds,w.Players[f.P].resources);EqualCost(income,w.Players[f.P].tradeIncome);
            TestContext.WriteLine($"Confluence082 normal paid200W trade station receives both directions elapsed={w.Time-start:R}; existing public route caravan, not a newly trained player caravan");
        }
        static float WalkClone(World normal,int[] ids,Vector2 destination)
        {
            string frozen=State(normal);var w=World.Restore(normal.Rules,SnapshotCodec.Clone(normal.CaptureState()));float start=w.Time;int cursor=w.Events.Count;var distances=ids.ToDictionary(id=>id,id=>0f);var decks=new HashSet<int>();var initialHp=ids.ToDictionary(id=>id,id=>w.Get(id).hp);
            var cannons=ids.Where(id=>w.Get(id)?.def.id=="cannon").ToArray();
            var workers=ids.Where(id=>w.Get(id)?.Worker==true).ToArray();
            bool intercepted=false;
            if(cannons.Length>0)Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.AttackMove,actors=cannons,point=destination}));
            // The cannon is the escort: let it establish a real lead before
            // the faster villager receives its public move order.
            if(cannons.Length>0&&workers.Length>0)
                for(int lead=0;lead<600&&cannons.Any(id=>w.Get(id).orders.Count>0);lead++)
                {
                    var previous=w.Get(cannons[0]).position;int beforeEvents=w.Events.Count;w.Tick(.1f);var moved=Vector2.Distance(previous,w.Get(cannons[0]).position);distances[cannons[0]]+=moved;
                    if(w.Players[0].losses!=0)
                    {
                        intercepted=true;TestContext.WriteLine("WalkClone escort lead intercepted events="+string.Join(" | ",w.Events.Skip(beforeEvents).Select(e=>e.kind+":"+e.entity+":"+e.target+":"+e.amount)));break;
                    }
                    Assert.IsFalse(w.IsMatchEnded);
                }
            if(intercepted){TestContext.WriteLine("Confluence082 land escort ended at the first natural opponent contact before the full destination; ferry remains the safe paid option");Assert.AreEqual(frozen,State(normal));return w.Time-start;}
            if(workers.Length>0)Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=workers,point=destination}));
            if(cannons.Length==0&&workers.Length==0)Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=ids,point=destination}));
            for(int step=0;step<1800&&ids.Any(id=>w.Get(id).orders.Count>0);step++)
            {
                var positions=ids.ToDictionary(id=>id,id=>w.Get(id).position);int beforeEvents=w.Events.Count;w.Tick(.1f);Assert.IsFalse(w.IsMatchEnded);
                if(w.Players[0].losses!=0)
                {
                    intercepted=true;TestContext.WriteLine("WalkClone natural interception events="+string.Join(" | ",w.Events.Skip(beforeEvents).Select(e=>e.kind+":"+e.entity+":"+e.target+":"+e.amount+"/"+w.Get(e.entity)?.def.id+"->"+w.Get(e.target)?.def.id)));break;
                }
                foreach(int id in ids)
                {
                    var e=w.Get(id);Assert.IsTrue(e.Alive&&e.garrisonedIn==0);Vector2 previous=positions[id];float moved=Vector2.Distance(previous,e.position);distances[id]+=moved;
                    Assert.IsTrue(w.Navigation.IsWalkable(e.position,e.def.radius)&&(!w.IsWater(e.position)||w.IsBridge(e.position)));Assert.IsTrue(w.Navigation.ClearLine(previous,e.position,e.def.radius));
                    Assert.LessOrEqual(moved,w.Speed(e)*.1f+.0001f);Assert.LessOrEqual(Mathf.Abs(w.SurfaceHeight(previous)-w.SurfaceHeight(e.position)),moved*.4f+.0002f);
                    int samples=Mathf.Max(1,Mathf.CeilToInt(moved/.25f));for(int s=0;s<=samples;s++)for(int b=0;b<w.Layout.Bridges.Length;b++)if(w.Layout.Bridges[b].Contains(Vector2.Lerp(previous,e.position,(float)s/samples)))decks.Add(b);
                }
            }
            if(intercepted)
            {
                var casualties=ids.Where(id=>!w.Get(id).Alive).ToArray();Assert.Greater(casualties.Length,0,"Natural interception must identify a paid convoy casualty");
                TestContext.WriteLine($"Confluence082 natural land clone intercepted before destination actors={string.Join(",",casualties)} bridgeDecks={string.Join(",",decks.OrderBy(i=>i))}; retained as an observed tactical risk, not counted as a ferry failure");
                Assert.AreEqual(frozen,State(normal));return w.Time-start;
            }
            foreach(int id in ids){var e=w.Get(id);Assert.IsTrue(e.Alive);Assert.IsEmpty(e.orders);Assert.Less(Vector2.Distance(e.position,destination),6.01f);TestContext.WriteLine($"Confluence082 natural land clone actor={id}/{e.def.id} length={distances[id]:R} hpBefore={initialHp[id]:R} hpAfter={e.hp:R} destination={e.position}");}
            Assert.IsTrue(decks.Contains(0)&&decks.Contains(1),"Actual detour traverses both main-river bridges; not assumed upstream-branch shortcut");
            foreach(var ev in w.Events.Skip(cursor).Where(e=>e.kind=="Fire"||e.kind=="Damage"))TestContext.WriteLine($"Confluence082 natural land threat time={ev.time:R} event={ev.kind} source={ev.entity}/{w.Get(ev.entity)?.def.id} target={ev.target}/{w.Get(ev.target)?.def.id} amount={ev.amount:R}");
            Assert.AreEqual(frozen,State(normal),"Walking branch never mutates or advances original paid World");TestContext.WriteLine($"Confluence082 land actual elapsed={w.Time-start:R} bridgeDecks={string.Join(",",decks.OrderBy(i=>i))} commonPaidSnapshot={SnapshotCodec.Hash(frozen)}; no guardian/enemy Hold/removal/spawn; enemy exposure is not a controlled pure movement benchmark");return w.Time-start;
        }
        [Test] public void NormalPaidWorkerAndFieldCannonFerryUsesExistingResourcesAndRecordsActualLandTradeoffs()
        {
            Assert.AreEqual("0.25.0",Application.version);var f=new Fixture(0);var w=f.W;var player=w.Players[0];var south=ConfluenceContent082.SouthLanding;var north=ConfluenceContent082.NorthLanding;
            ClearInterferingGuardians(f);Advance(f,2);var dock=f.Build("dock",ConfluenceContent082.Dock);var fisher=f.Train(dock,"fishing_boat");
            var fish=w.Get(114);Assert.IsTrue(fish!=null&&fish.def.id=="fish","Preserved original main-channel fish");
            if(!w.CanGather(fisher,fish)){f.Order(fisher,OrderKind.Move,point:fish.position);f.Until(()=>w.CanGather(fisher,fish),90,"Public fisher movement physically scouts main-channel fish, no fog reveal");}
            f.Order(fisher,OrderKind.Gather,fish.id);int cursor=w.Events.Count;f.Until(()=>w.Events.Skip(cursor).Any(e=>e.kind=="Gather"&&e.entity==fisher.id&&e.target==fish.id&&e.amount>0),120,"Actual paid fishing credits food");
            f.StopGatherers();f.Order(fisher,OrderKind.Gather,fish.id);float food=player.resources.food,gathered=player.gathered.food,remaining=fish.remaining;f.Step();float delta=remaining-fish.remaining;
            Assert.Greater(delta,0);Assert.AreEqual(delta,player.resources.food-food,.002f);Assert.AreEqual(delta,player.gathered.food-gathered,.002f);f.Order(fisher,OrderKind.Hold);
            var ship=f.Train(dock,"transport_ship");Advance(f,3);var foundry=f.Build("foundry");var cannon=f.Train(foundry,"cannon");PublicTradeRoundTrip(f);
            var passengers=new[]{f.Workers[0],cannon};var ids=passengers.Select(e=>e.id).ToArray();f.StopGatherers();
            Assert.IsTrue(w.TryTransportShore(ship,south,cannon.def.radius,out var actualSouth,out var southBerth));Assert.AreEqual(south,actualSouth,"Paid dock must not redirect southern loading request");
            Assert.IsTrue(w.TryTransportShore(ship,north,cannon.def.radius,out var actualNorth,out _));Assert.AreEqual(north,actualNorth);
            // Issue separate public moves so the first unit does not occupy
            // the exact landing anchor and strand the larger cannon in a
            // formation slot. Both orders remain ordinary player movement.
            foreach(var passenger in passengers)
            {
                f.Order(passenger,OrderKind.Move,point:south);
                f.Until(()=>passenger.orders.Count==0||Vector2.Distance(passenger.position,south)<6.01f,150,"Paid passenger physically stage south "+passenger.def.id);
                if(passenger.orders.Count>0)f.Order(passenger,OrderKind.Hold);
                Assert.Less(Vector2.Distance(passenger.position,south),6.01f);
                Assert.IsTrue(passenger.Alive&&passenger.garrisonedIn==0);
            }
            f.Order(ship,OrderKind.Move,point:southBerth);f.Until(()=>ship.orders.Count==0,150,"Paid transport physically prepositions at south berth");f.Order(ship,OrderKind.Hold);
            string common=State(w);float investmentTime=w.Time;var investmentSpent=player.spent;var investmentGathered=player.gathered;var investmentFunds=player.resources;float landSeconds=WalkClone(w,ids,north);Assert.AreEqual(common,State(w));
            TestContext.WriteLine($"Confluence082 first-use investment elapsed={investmentTime:R} spent={JsonUtility.ToJson(investmentSpent)} gathered={JsonUtility.ToJson(investmentGathered)} funds={JsonUtility.ToJson(investmentFunds)}; includes paidAgeII/III+Dock/fisher/transport/foundry/cannon/tradePost, genuine age reward crates; no sunk-cost-free first-use shortcut claim");
            float ferryStart=w.Time;int population=player.usedPopulation;var funds=player.resources;cursor=w.Events.Count;
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=ids,target=ship.id,point=south}));Assert.IsTrue(passengers.All(e=>e.garrisonedIn==0));f.Step();
            Assert.IsTrue(passengers.Any(e=>e.garrisonedIn==0),"Genuine in-progress boarding cold save");f.Mirror=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));f.Until(()=>passengers.All(e=>e.garrisonedIn==ship.id),150,"Both physically board");Assert.AreEqual(State(w),State(f.Mirror));f.Mirror=null;
            Cargo(w,ship.id,ids,true);Assert.AreEqual(2,w.Events.Skip(cursor).Count(e=>e.kind=="Embarked"&&e.target==ship.id));OccupiedClone(w,0,ship.id,ids,north,south);
            float sailingStart=w.Time;var sailStart=ship.position;float sailed=0;cursor=w.Events.Count;Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{ship.id},point=north}));
            Assert.AreEqual(north,ship.orders.Peek().point);Assert.IsTrue(ship.orders.Peek().hasResolvedPoint);f.Step();Assert.IsTrue(passengers.All(e=>e.garrisonedIn==ship.id));f.Mirror=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));
            for(int step=0;step<1500&&passengers.Any(e=>e.garrisonedIn!=0);step++){var before=ship.position;f.Step();sailed+=Vector2.Distance(before,ship.position);foreach(var e in passengers.Where(e=>e.garrisonedIn==ship.id))Assert.AreEqual(ship.position,e.position);}
            Cargo(w,ship.id,ids,false);Assert.AreEqual(State(w),State(f.Mirror),"Cold sail/unload no duplicate funds/resources/cargo");f.Mirror=null;Assert.Greater(sailed,8);Assert.Greater(Vector2.Distance(sailStart,ship.position),8);
            Assert.AreEqual(2,w.Events.Skip(cursor).Count(e=>e.kind=="Disembarked"&&e.target==ship.id));Assert.AreEqual(population,player.usedPopulation);EqualCost(funds,player.resources);
            foreach(var e in passengers)
            {
                Assert.IsFalse(w.IsWater(e.position)||w.IsBridge(e.position)||w.IsBridgeApproach(e.position));Assert.IsTrue(w.Navigation.IsWalkable(e.position,e.def.radius));Assert.IsEmpty(e.orders);Assert.Less(Vector2.Distance(e.position,north),6.01f);
                Assert.Greater(Vector2.Dot(w.Layout.WaterDirection(e.position),w.Layout.WaterDirection(north)),.5f);Assert.Greater(Vector2.Dot(e.position-south,north-south),(north-south).sqrMagnitude*.5f);
                foreach(var other in passengers.Where(a=>a.id!=e.id))Assert.GreaterOrEqual(Vector2.Distance(e.position,other.position),e.def.radius+other.def.radius+.15f-.001f);
            }
            float ferrySeconds=w.Time-ferryStart;TestContext.WriteLine($"Confluence082 same paid snapshot actual ferry elapsed={ferrySeconds:R} boarding={sailingStart-ferryStart:R} sailUnload={w.Time-sailingStart:R} shipLength={sailed:R} land={landSeconds:R} signedSavings={landSeconds-ferrySeconds:R}; same starting state, same northern destination region, natural unequal threat exposure, existing-boat marginal time only; not parity/FPS/native");
            f.Order(cannon,OrderKind.Hold);GatherExistingNorthernResource(f,f.Workers[0],ConfluenceContent082.NorthMine);GatherExistingNorthernResource(f,f.Workers[0],ConfluenceContent082.NorthWood(3));Assert.AreEqual(3,player.age);Assert.AreEqual(f.RulesHash,SnapshotCodec.RulesHash(w.Rules));Assert.AreEqual(population,player.usedPopulation);Assert.AreEqual(0,player.losses);
            TestContext.WriteLine($"ConfluencePaid082 t={w.Time:R} worker={ids[0]} cannon={ids[1]} dock={dock.position} source={Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT")}; no original resource relocation, economic grants, normal actor spawn, enemy Hold, native/fullmatch/FPS claim");
        }
    }
}
