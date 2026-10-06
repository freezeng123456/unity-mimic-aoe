using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Solvers use accepted player orders and the fixed scheduler. Direct deaths
    // below are explicit failure fixtures, never substitutes for a winning run.
    public sealed class RiverContent070Tests
    {
        RuleCatalog rules;string directory;
        [SetUp] public void SetUp()
        {rules=Catalog.Load();directory=Path.Combine(Path.GetTempPath(),"frontier-river070-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);}
        [TearDown] public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static World Create(RuleCatalog r,ScenarioDocument d)=>new World(r,d,d.starts.Select(s=>s.civilization).ToArray(),d.starts.Select(s=>s.team).ToArray(),5);
        static Vector2 Region(GuidedPlaySession s,string id)=>s.World.ScenarioDocument.regions.Single(r=>r.id==id).center;
        static Vector2 ChannelPoint(RiverChannel channel,float z)=>channel.Centers.OrderBy(p=>Mathf.Abs(p.y-z)).First();
        static int[] Alive(GuidedPlaySession s,int[] ids)=>ids.Where(id=>s.World.Get(id)?.Alive==true&&s.World.Get(id).garrisonedIn==0).ToArray();
        static string Diagnostic(GuidedPlaySession s)=>" stage="+s.Stage?.Id+" t="+s.World.Time+" status="+s.Status+" reason="+s.Reason+" units="+
            string.Join(";",s.World.Entities.Where(e=>e.owner==0&&e.Alive&&(e.def.waterborne||s.CaptureState().soldiers.Contains(e.id))).Select(e=>$"{e.id}:{e.def.id}:{e.status}@{e.position} hp={e.hp} cargo={e.garrisonedIn}"));
        static void Until(GuidedPlaySession s,Func<bool> condition,float seconds,string why)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds*10)&&!condition()&&s.Status==GuidedPlayStatus.Running;i++)s.Simulation.Step();
            Assert.IsTrue(condition(),why+Diagnostic(s));
        }
        static void Accepted(GuidedPlaySession s,GameCommand c,ReplayRecorder recorder=null)
        {var result=recorder==null?s.Issue(c):recorder.Issue(c);Assert.IsTrue(result.accepted,result.reason+Diagnostic(s));}
        static void Order(GuidedPlaySession s,int[] ids,OrderKind kind,Vector2 point,int target=0,ReplayRecorder recorder=null)
            =>Accepted(s,new GameCommand{player=0,kind=CommandKind.Order,actors=Alive(s,ids),order=kind,point=point,target=target},recorder);
        static void Attack(GuidedPlaySession s,int target,ReplayRecorder recorder=null)
        {
            var entity=s.World.Get(target);Assert.IsNotNull(entity);Assert.IsTrue(entity.Alive);
            var army=Alive(s,s.CaptureState().soldiers);Assert.GreaterOrEqual(army.Length,4,"Winning path must preserve a useful field army");
            // A building attack intentionally ignores intervening soldiers.
            // Clear the authored guards with visible, named attacks first.
            var guardIds=s.Definition.Id==RiverContent070.Scout?new[]{"guard-a","guard-b"}:new[]{"island-guard-a","island-guard-b"};
            foreach(string guardId in guardIds)
            {
                var guard=s.World.ScenarioEntity(guardId);Assert.IsNotNull(guard,guardId);if(!guard.Alive)continue;
                if(!s.World.IsVisible(0,guard))
                {
                    var approach=s.World.Navigation.NearestWalkable(guard.position+new Vector2(-8,-4),.4f);
                    Order(s,Alive(s,army),OrderKind.AttackMove,approach,recorder:recorder);
                    Until(s,()=>s.World.IsVisible(0,guard),120,"Actually reveal the guard before selecting it");
                }
                Order(s,Alive(s,army),OrderKind.Attack,guard.position,guard.id,recorder);
                Until(s,()=>!guard.Alive,120,"Focused ordinary combat must clear "+guardId+" before attacking the warehouse");
            }
            if(!s.World.IsVisible(0,entity))
            {
                var near=s.World.Navigation.NearestWalkable(entity.position+new Vector2(-8,-4),.4f);
                Order(s,army,OrderKind.AttackMove,near,recorder:recorder);
                Until(s,()=>s.World.IsVisible(0,entity),120,"Reveal the named target through actual movement");
            }
            Order(s,Alive(s,army),OrderKind.Attack,entity.position,target,recorder);
            Until(s,()=>!entity.Alive,180,"Actual specified-target combat must destroy the blockade");
        }
        static void DefendPort(GuidedPlaySession s,ReplayRecorder recorder=null)
        {
            var state=s.CaptureState();var centre=Region(s,"port-perimeter");
            var stations=state.soldiers.ToDictionary(id=>id,id=>s.World.Get(id).position);
            // Guarding keeps pikes beside the shooters instead of pursuing the
            // optional supply market before the cavalry arrive.
            Order(s,state.soldiers,OrderKind.Hold,centre,recorder:recorder);
            Order(s,new[]{state.scout},OrderKind.Move,centre+new Vector2(0,-5),recorder:recorder);
            var escort=s.World.ScenarioEntity("escort");
            Order(s,new[]{escort.id},OrderKind.AttackMove,ChannelPoint(s.World.Layout.Channels[0],82),recorder:recorder);
            int selected=0;bool regrouping=false;
            for(int decision=0;decision<220&&s.Status==GuidedPlayStatus.Running&&s.Stage?.Id=="hold";decision++)
            {
                var old=s.World.Get(selected);
                var threat=old?.Alive==true&&s.World.IsVisible(0,old)?old:
                    s.World.Entities.Where(e=>e.Alive&&e.owner==1&&e.def.kind==EntityKind.Unit&&!e.def.waterborne&&!e.Worker&&
                        s.World.IsVisible(0,e)&&(e.position-centre).sqrMagnitude<24*24)
                        .OrderBy(e=>e.def.id=="hussar"?0:1).ThenBy(e=>(e.position-centre).sqrMagnitude).FirstOrDefault();
                if(threat!=null&&threat.id!=selected)
                {
                    var actors=Alive(s,state.soldiers).ToList();var scout=s.World.Get(state.scout);
                    if(scout.Alive&&(scout.position-centre).sqrMagnitude<20*20)actors.Add(scout.id);
                    Order(s,actors.ToArray(),OrderKind.Attack,threat.position,threat.id,recorder);
                    selected=threat.id;regrouping=false;
                }
                else if(threat==null&&selected!=0)
                {
                    foreach(int id in Alive(s,state.soldiers))Order(s,new[]{id},OrderKind.Move,stations[id],recorder:recorder);
                    selected=0;regrouping=true;
                }
                else if(threat==null&&regrouping&&Alive(s,state.soldiers).All(id=>(s.World.Get(id).position-stations[id]).sqrMagnitude<1))
                {Order(s,state.soldiers,OrderKind.Hold,centre,recorder:recorder);regrouping=false;}
                // Commands are reconsidered once per simulation second. This
                // represents ordinary human target selection, not frame-perfect input.
                for(int step=0;step<10&&s.Status==GuidedPlayStatus.Running&&s.Stage?.Id=="hold";step++)s.Simulation.Step();
            }
            Assert.AreNotEqual("hold",s.Stage?.Id,"The unchanged waves and forty-second hold must be genuinely cleared"+Diagnostic(s));
            Assert.AreEqual(GuidedPlayStatus.Running,s.Status,Diagnostic(s));
            Assert.IsTrue(s.World.ScenarioEntity("forward-port").Alive,"The real forward dock must survive the hold");
            Assert.IsTrue(s.CaptureState().commands.Any(p=>p.command.order==OrderKind.Attack&&s.World.Get(p.command.target)?.def.kind==EntityKind.Unit),"Incoming units require ordinary directed focus fire");
        }
        static void FirstStage(GuidedPlaySession s,ReplayRecorder recorder=null)
        {
            var state=s.CaptureState();
            if(s.Definition.Id==RiverContent070.Scout)
            {
                Order(s,state.soldiers,OrderKind.AttackMove,Region(s,"survey-zone"),recorder:recorder);
                Order(s,new[]{state.scout},OrderKind.Move,Region(s,"survey-zone"),recorder:recorder);
                Until(s,()=>s.Stage?.Id!="survey",180,"The explorer must really cross a bridge and survey the far bank");
            }
            else if(s.Definition.Id==RiverContent070.Convoy)
            {
                Assert.AreEqual(6,state.soldiers.Length);
                var enemy=s.World.ScenarioEntity("river-raider");var escort=s.World.ScenarioEntity("escort");
                Order(s,new[]{escort.id},OrderKind.AttackMove,enemy.position,recorder:recorder);
                Accepted(s,new GameCommand{player=0,kind=CommandKind.Embark,actors=state.soldiers,target=state.transport,point=state.boardingPoint},recorder);
                Until(s,()=>s.Stage?.Id!="embark",150,"At least four of the six designated soldiers must actually board");
                Assert.GreaterOrEqual(state.soldiers.Count(id=>s.World.Get(id).Alive&&s.World.Get(id).garrisonedIn==state.transport),4);
                Assert.GreaterOrEqual(s.World.Events.Count(e=>e.kind=="Embarked"),4);
                // Finish the accepted six-person boarding order before moving
                // the transport; the four-person goal need not be the full load.
                Until(s,()=>state.soldiers.All(id=>s.World.Get(id).Alive&&s.World.Get(id).garrisonedIn==state.transport),30,"The requested six-person load must finish before sailing");
            }
            else
            {
                DefendPort(s,recorder);
            }
            Assert.AreEqual(GuidedPlayStatus.Running,s.Status,Diagnostic(s));
        }
        static void SolveRemaining(GuidedPlaySession s,ReplayRecorder recorder=null)
        {
            int guard=0;
            while(s.Status==GuidedPlayStatus.Running&&guard++<8)
            {
                string stage=s.Stage.Id;var state=s.CaptureState();
                if(stage=="survey"||stage=="embark"||stage=="hold"){FirstStage(s,recorder);continue;}
                if(stage=="blockade")Attack(s,state.firstTarget,recorder);
                else if(stage=="sail")
                {
                    Order(s,new[]{state.transport},OrderKind.Move,Region(s,"bay-water"),recorder:recorder);
                    Until(s,()=>s.Stage?.Id!=stage,160,"The loaded transport must sail through the channel");
                }
                else if(stage=="land")
                {
                    var boarded=state.soldiers.Where(id=>s.World.Get(id)?.Alive==true&&s.World.Get(id).garrisonedIn==state.transport).ToArray();
                    Assert.GreaterOrEqual(boarded.Length,4,"The designated army must actually be aboard at the landing boundary");
                    var island=Region(s,"island");var ship=s.World.Get(state.transport);
                    Assert.IsTrue(s.World.TryTransportShore(ship,island+new Vector2(-12,0),.4f,out var shore,out var berth),"A legal island west-bank landing must be reachable");
                    Connected(s.World.Navigation,shore,island,.4f,"The actual landing joins the island interior");
                    Assert.AreEqual(s.World.Layout.LandRegion(island),s.World.Layout.LandRegion(shore),"Landing must join the island's actual land region");
                    Assert.AreNotEqual(s.World.Layout.LandRegion(state.boardingPoint),s.World.Layout.LandRegion(shore),"Unloading onto the home mainland cannot stand in for island landing");
                    Accepted(s,new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{state.transport},point=shore},recorder);
                    Until(s,()=>boarded.All(id=>s.World.Get(id)?.Alive==true&&s.World.Get(id).garrisonedIn==0),140,"These actual passengers must disembark; troops left on the home shore cannot count");
                    Order(s,boarded,OrderKind.AttackMove,Region(s,"island"),recorder:recorder);
                    Until(s,()=>s.Stage?.Id!=stage,120,"Four designated soldiers, not the ship, must enter the island interior");
                }
                else if(stage=="withdraw")
                {
                    Order(s,state.soldiers,OrderKind.AttackMove,Region(s,"rear-camp"),recorder:recorder);
                    Until(s,()=>s.Status!=GuidedPlayStatus.Running,270,"The rear guard must really cross the river and reach its camp");
                }
                else Assert.Fail("Unsolved authored stage "+stage);
            }
            Assert.AreEqual(GuidedPlayStatus.Completed,s.Status,Diagnostic(s));
            Assert.IsTrue(s.World.Paused);Assert.AreEqual(0,s.World.Winner);
            Assert.IsTrue(s.Checklist.All(c=>c.Complete));
            Assert.IsTrue(s.World.ScenarioObjectiveStates.Where(o=>s.World.ScenarioDocument.objectives.Single(d=>d.id==o.id).required).All(o=>o.status=="completed"));
            Assert.IsTrue(s.World.CaptureState().scenarioState.triggerStates.All(t=>t.lastError==""),"Spawn and ordinary command failures cannot be hidden by victory");
            Assert.Greater(s.CaptureState().commands.Length,0);
        }
        GuidedPlaySession Cold(GuidedPlaySession s,int slot=1)
        {
            s.World.Paused=true;string path=MatchSaveStore.SlotPath(directory,slot);
            var before=s.World.CaptureState();var guided=s.CaptureState();
            MatchSaveStore.Write(path,new SavedMatch{matchId=s.AttemptId,appVersion="river070-tests",world=before,simulation=s.Simulation.CaptureState(),guided=guided,
                cameraPosition=new Vector3(24,50,-14),cameraFocus=new Vector2(24,28),cameraSize=26,simulationSpeed=1});
            var saved=MatchSaveStore.Read(path,"river070-tests");var w=World.Restore(rules,saved.world);var sim=MatchSimulation.Restore(w,saved.simulation);
            string frozen=JsonUtility.ToJson(w.CaptureState());var restored=GuidedPlaySession.Restore(w,sim,saved.guided);
            Assert.AreEqual(frozen,JsonUtility.ToJson(w.CaptureState()),"Cold adopting a chapter must not spawn, grant, evaluate, or append events");
            Assert.AreEqual(JsonUtility.ToJson(guided),JsonUtility.ToJson(restored.CaptureState()));
            Assert.AreEqual(s.AttemptId,restored.AttemptId);Assert.AreEqual(before.scenarioHash,w.ScenarioHash);
            CollectionAssert.AreEqual(before.entities.Select(e=>e.state.id),w.Entities.Select(e=>e.id));
            return restored;
        }

        [TestCase(RiverContent070.Skirmish)][TestCase(RiverContent070.Scout)][TestCase(RiverContent070.Convoy)][TestCase(RiverContent070.Retreat)]
        public void FactoriesProduceLegalIndependentFrozenDocumentsAndPortableByteRoundtrips(string id)
        {
            var d=RiverContent070.Build(rules,id);var again=RiverContent070.Build(rules,id);
            Assert.AreEqual("",ScenarioCompiler.Validate(d,rules,2));Assert.AreEqual(ScenarioCodec.ContentHash(d),ScenarioCodec.ContentHash(again));
            string portable=ScenarioCodec.Encode(d,rules);var decoded=ScenarioCodec.Decode(portable,rules);
            Assert.AreEqual(portable,ScenarioCodec.Encode(decoded,rules));
            var store=new ScenarioStore(Path.Combine(directory,"Scenarios"),rules);string file=store.Write(decoded);
            Assert.AreEqual(ScenarioCodec.ContentHash(d),ScenarioCodec.ContentHash(store.ReadFile(file)));
            var w=Create(rules,d);string hash=w.ScenarioHash;string owned=JsonUtility.ToJson(w.ScenarioDocument);
            d.name="External modification";d.starts[0].initialResources.food=0;d.heightsCm[0]++;
            var read=w.ScenarioDocument;read.name="Getter modification";read.resources[0].remaining=0;
            Assert.AreEqual(hash,w.ScenarioHash);Assert.AreEqual(owned,JsonUtility.ToJson(w.ScenarioDocument));
            Assert.AreEqual(ScenarioCodec.ContentHash(again),hash);
        }
        [Test] public void OrdinaryMapStartsWithPaidRulesAndPairedHomeAndForwardResourceBudgets()
        {
            var d=RiverContent070.Build(rules,RiverContent070.Skirmish);var w=Create(rules,d);
            Assert.IsTrue(d.enableElimination);Assert.IsEmpty(d.objectives);Assert.IsEmpty(d.triggers);Assert.IsEmpty(d.objects);
            Assert.AreEqual("elimination",w.MapObjective.mode);Assert.AreEqual(-1,w.Winner);
            for(int p=0;p<2;p++)
            {
                Assert.AreEqual(1,w.Players[p].age);Assert.AreEqual(300,w.Players[p].resources.food);Assert.AreEqual(300,w.Players[p].resources.wood);Assert.AreEqual(100,w.Players[p].resources.coin);
                Assert.AreEqual(6,w.Entities.Count(e=>e.owner==p&&e.Worker));
                Assert.IsFalse(w.Entities.Any(e=>e.owner==p&&(e.def.waterborne||e.def.id=="dock"||e.Military&&e.def.id!="explorer")));
            }
            var first=d.starts[0];var second=d.starts[1];
            Assert.AreEqual(new Vector2(128,128)-first.town,second.town);Assert.AreEqual(new Vector2(128,128)-first.explorer,second.explorer);
            for(int i=0;i<first.workers.Length;i++)Assert.AreEqual(new Vector2(128,128)-first.workers[i],second.workers[i]);
            foreach(string prefix in new[]{"home-mine-","home-food-","home-wood-","forward-mine-","forward-wood-"})
            {
                var a=d.resources.Where(x=>x.id.StartsWith(prefix+"0")).ToArray();var b=d.resources.Where(x=>x.id.StartsWith(prefix+"1")).ToArray();
                Assert.Greater(a.Length,0,prefix);Assert.AreEqual(a.Length,b.Length,prefix);
                for(int i=0;i<a.Length;i++){Assert.AreEqual(a[i].definition,b[i].definition);Assert.AreEqual(a[i].remaining,b[i].remaining);Assert.AreEqual(new Vector2(128,128)-a[i].position,b[i].position);}
            }
            Assert.Greater(d.resources.Single(x=>x.id=="forward-mine-0").remaining,d.resources.Single(x=>x.id=="home-mine-0").remaining);
            Assert.Greater(Vector2.Distance(first.town,d.resources.Single(x=>x.id=="forward-mine-0").position),35);
            Assert.AreEqual(4,d.tradeSockets.Length);Assert.AreEqual(2,d.neutralBindings.Length);
            Assert.AreEqual(4,w.TradeSockets.Count);Assert.AreEqual(2,w.NeutralAllies.Count);
            var sim=new MatchSimulation(w,Array.Empty<int>(),"standard");for(int i=0;i<100;i++)Assert.IsTrue(sim.Step());
            Assert.AreEqual(-1,w.Winner,"Ordinary geography cannot grant a scripted victory to slot zero");
        }
        static void Connected(GridNavigation nav,Vector2 from,Vector2 to,float radius,string why)
        {
            Assert.IsTrue(nav.IsWalkable(from,radius),why+" start");Assert.IsTrue(nav.IsWalkable(to,radius),why+" end");
            var path=nav.FindPath(from,to,radius);Assert.IsNotEmpty(path,why);Assert.Less((path[path.Count-1]-to).sqrMagnitude,.02f,why+" exact endpoint");
        }
        [Test] public void OrdinaryMapOffersBothBridgeRoutesAnUnbrokenShipChannelLegalPortsAndOppositeShoreLandings()
        {
            var w=Create(rules,RiverContent070.Build(rules,RiverContent070.Skirmish));Assert.AreEqual(2,w.Layout.Bridges.Length);
            foreach(var bridge in w.Layout.Bridges)
            {
                Vector2 west=bridge.Point(-(bridge.HalfLength+bridge.RampLength+4),0),east=bridge.Point(bridge.HalfLength+bridge.RampLength+4,0);
                Connected(w.Navigation,west,east,.4f,"Cross "+bridge.Center);
                Connected(w.Navigation,w.ScenarioEntity("start-0-explorer").position,west,.4f,"Western approach");
                Connected(w.Navigation,east,w.ScenarioEntity("start-1-explorer").position,.4f,"Eastern approach");
            }
            var channel=w.Layout.Channels[0];var a=ChannelPoint(channel,18);var b=ChannelPoint(channel,110);
            Connected(w.WaterNavigation,a,b,rules.Entity("frigate").radius,"Whole curved river including both bridge clearances");
            var centre=ChannelPoint(channel,67);
            var legalPorts=Enumerable.Range(6,116).SelectMany(x=>Enumerable.Range(6,116).Select(y=>new Vector2(x,y)))
                .Where(p=>Mathf.Abs(p.y-67)<10&&w.ScenarioPlacementReason("dock",p)=="").ToArray();
            Assert.IsTrue(legalPorts.Any(p=>p.x<centre.x));Assert.IsTrue(legalPorts.Any(p=>p.x>centre.x));
            // A geometry-only probe is not inserted into the world or granted to
            // a player. It tests the real transport endpoint planner on both banks.
            var probe=new EntityState{id=int.MaxValue,owner=0,def=rules.Entity("transport_ship"),position=centre,hp=rules.Entity("transport_ship").hp};
            string state=JsonUtility.ToJson(w.CaptureState());
            foreach(float side in new[]{-1f,1f})
            {
                var requested=centre+new Vector2(side*18,0);
                Assert.IsTrue(w.TryTransportShore(probe,requested,.4f,out var land,out var water),"Legal unloading bank "+side);
                Assert.IsFalse(w.IsWater(land));Assert.IsTrue(w.WaterNavigation.IsWalkable(water,probe.def.radius));
                Connected(w.Navigation,land,w.ScenarioEntity("start-"+(side<0?0:1)+"-explorer").position,.4f,"Landing joins the intended mainland");
            }
            Assert.AreEqual(state,JsonUtility.ToJson(w.CaptureState()),"Geometry inspection cannot grant a free naval opening");
        }
        [TestCase(RiverContent070.Scout)][TestCase(RiverContent070.Convoy)][TestCase(RiverContent070.Retreat)]
        public void NewChaptersAreWonByRealCommandsCombatCargoAndTimedWithdrawal(string id)
        {
            using(var s=GuidedPlaySession.Start(rules,id))
            {
                Assert.IsTrue(s.World.HasScenario);Assert.AreEqual(0,s.Simulation.CaptureState().controllers.Length);
                SolveRemaining(s);Assert.AreEqual(s.Definition.Stages.Count,s.CaptureState().stageEvents.Length);
                if(id==RiverContent070.Convoy)
                {
                    Assert.GreaterOrEqual(s.World.Events.Count(e=>e.kind=="Embarked"),4);Assert.GreaterOrEqual(s.World.Events.Count(e=>e.kind=="Disembarked"),4);
                    Assert.IsTrue(s.CaptureState().commands.Any(p=>p.command.kind==CommandKind.Embark));Assert.IsTrue(s.CaptureState().commands.Any(p=>p.command.kind==CommandKind.Disembark));
                }
                if(id==RiverContent070.Retreat)
                {
                    Assert.GreaterOrEqual(s.World.Time,40);foreach(string wave in new[]{"land-wave-one","land-wave-two","water-wave"})
                        Assert.AreEqual(1,s.World.CaptureState().scenarioState.triggerStates.Single(t=>t.id==wave).fires,wave);
                }
            }
        }
        [Test] public void EmptyTransportCannotCompleteTheEscortByLeavingTheDesignatedPartyOnItsHomeShore()
        {
            using(var s=GuidedPlaySession.Start(rules,RiverContent070.Convoy))
            {
                FirstStage(s);Assert.AreEqual("sail",s.Stage.Id);var state=s.CaptureState();var ship=s.World.Get(state.transport);
                var passengers=state.soldiers.Where(id=>s.World.Get(id).Alive&&s.World.Get(id).garrisonedIn==ship.id).ToArray();
                Assert.AreEqual(6,passengers.Length,"The exploit begins after a genuinely completed six-person boarding order");
                Assert.IsTrue(s.World.TryTransportShore(ship,state.boardingPoint,.4f,out var homeShore,out var homeBerth),"Home-bank unloading must be a real legal operation");
                var path=s.World.Navigation.FindPath(homeShore,state.boardingPoint,.4f);
                Assert.IsNotEmpty(path,"The chosen shore must join the party's original mainland");
                Assert.Less((path[path.Count-1]-state.boardingPoint).sqrMagnitude,.02f);
                Accepted(s,new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{ship.id},point=homeShore});
                Until(s,()=>passengers.All(id=>s.World.Get(id).Alive&&s.World.Get(id).garrisonedIn==0),90,"All designated soldiers must actually unload at home before the empty voyage");
                Assert.AreEqual(0,s.World.TransportLoad(ship));Assert.GreaterOrEqual(s.World.Events.Count(e=>e.kind=="Disembarked"),6);
                Order(s,passengers,OrderKind.Hold,homeShore);
                var destination=Region(s,"bay-water");Order(s,new[]{ship.id},OrderKind.Move,destination);
                Until(s,()=>(ship.position-destination).sqrMagnitude<4,100,"The empty ship must actually reach the escort's water marker");
                for(int step=0;step<20&&s.Status==GuidedPlayStatus.Running;step++)s.Simulation.Step();
                Assert.AreEqual(GuidedPlayStatus.Running,s.Status,Diagnostic(s));Assert.AreEqual("sail",s.Stage.Id,"Entering the bay without the designated cargo cannot complete escort");
                Assert.AreEqual("active",s.World.ScenarioObjectiveStates.Single(o=>o.id=="sail").status);
                Assert.AreEqual(0,s.World.ScenarioObjectiveStates.Single(o=>o.id=="sail").progress);
                Assert.AreEqual("locked",s.World.ScenarioObjectiveStates.Single(o=>o.id=="land").status);
                Assert.IsTrue(passengers.All(id=>s.World.Get(id).Alive&&s.World.Get(id).garrisonedIn==0));
                using(var cold=Cold(s,2))
                {
                    Assert.AreEqual("sail",cold.Stage.Id);Assert.AreEqual(0,cold.World.TransportLoad(cold.World.Get(state.transport)));
                    cold.World.Paused=false;for(int step=0;step<20&&cold.Status==GuidedPlayStatus.Running;step++)cold.Simulation.Step();
                    Assert.AreEqual(GuidedPlayStatus.Running,cold.Status,Diagnostic(cold));Assert.AreEqual("sail",cold.Stage.Id);
                    Assert.AreEqual("locked",cold.World.ScenarioObjectiveStates.Single(o=>o.id=="land").status);
                    Assert.AreEqual(6,cold.World.Events.Count(e=>e.kind=="Embarked"),"Cold restore must not regenerate the missing cargo");
                }
            }
        }
        [TestCase(RiverContent070.Scout)][TestCase(RiverContent070.Convoy)][TestCase(RiverContent070.Retreat)]
        public void StageBoundaryColdSlotContinuesWithoutDuplicatingBodiesSuppliesOrWaves(string id)
        {
            using(var first=GuidedPlaySession.Start(rules,id))
            {
                FirstStage(first);var prior=first.World.CaptureState();
                using(var cold=Cold(first))
                {
                    Assert.AreEqual(prior.entities.Length,cold.World.Entities.Count);Assert.AreEqual(prior.events.Length,cold.World.Events.Count);
                    Assert.AreEqual(JsonUtility.ToJson(prior.scenarioState),JsonUtility.ToJson(cold.World.CaptureState().scenarioState));
                    cold.World.Paused=false;SolveRemaining(cold);
                    foreach(var before in prior.scenarioState.triggerStates.Where(t=>t.fires>0))
                        Assert.AreEqual(before.fires,cold.World.CaptureState().scenarioState.triggerStates.Single(t=>t.id==before.id).fires,"Already fired "+before.id);
                    foreach(var entity in prior.entities)Assert.IsNotNull(cold.World.Get(entity.state.id),"Bound body must keep its ID");
                    Assert.AreEqual(1,cold.World.Events.Count(e=>e.kind=="GuidedScenarioSetup"));
                }
            }
        }
        [Test] public void RearGuardCanChooseActualBoardSailAndUnloadWithdrawalAfterHoldingTheSamePort()
        {
            using(var s=GuidedPlaySession.Start(rules,RiverContent070.Retreat))
            {
                FirstStage(s);Assert.AreEqual("withdraw",s.Stage.Id);var state=s.CaptureState();var ship=s.World.Get(state.transport);
                var boardingRequest=ChannelPoint(s.World.Layout.Channels[0],68)+new Vector2(18,0);
                Assert.IsTrue(s.World.TryTransportShore(ship,boardingRequest,.4f,out var boarding,out var berth),"Forward east-bank boarding must be legal");
                var party=Alive(s,state.soldiers);Assert.GreaterOrEqual(party.Length,4);
                Order(s,party,OrderKind.Move,boarding);
                Until(s,()=>party.All(id=>(s.World.Get(id).position-boarding).sqrMagnitude<64),90,"The surviving guard must walk to the actual embarkation shore");
                Accepted(s,new GameCommand{player=0,kind=CommandKind.Embark,actors=party,target=ship.id,point=boarding});
                Until(s,()=>party.All(id=>s.World.Get(id).garrisonedIn==ship.id),90,"The actual rear guard must board the ship");
                Assert.GreaterOrEqual(s.World.TransportLoad(ship),4);
                var destination=ChannelPoint(s.World.Layout.Channels[0],24)+new Vector2(-18,0);
                Assert.IsTrue(s.World.TryTransportShore(ship,destination,.4f,out var landing,out var water),"Rear western mainland must have a connected landing");
                var beforeSailing=ship.position;
                Order(s,new[]{ship.id},OrderKind.Move,water);
                var acceptedMove=ship.orders.Peek();var assigned=acceptedMove.point;
                Connected(s.World.WaterNavigation,beforeSailing,assigned,ship.def.radius,"Full route to the actually assigned naval Move slot");
                string voyage="requested shore berth="+water+" assigned Move slot="+assigned;
                Until(s,()=>ship.orders.Count==0&&(ship.position-(acceptedMove.hasResolvedPoint?acceptedMove.resolvedPoint:assigned)).sqrMagnitude<.01f,90,
                    "The loaded ship must finish its actual assigned route: "+voyage);
                Assert.Greater(beforeSailing.y,s.World.Layout.Bridges[0].Center.y,"Voyage starts upstream of the south crossing");
                Assert.Less(ship.position.y,s.World.Layout.Bridges[0].Center.y,"Actual loaded ship must cross to the downstream reach: "+voyage);
                Assert.GreaterOrEqual(s.World.TransportLoad(ship),4,"The assigned-slot arrival must still carry the designated survivors");
                Accepted(s,new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{ship.id},point=landing});
                Until(s,()=>party.All(id=>s.World.Get(id).Alive&&s.World.Get(id).garrisonedIn==0),90,"These actual survivors must unload on the other mainland");
                Order(s,party,OrderKind.Move,Region(s,"rear-camp"));
                Until(s,()=>s.Status!=GuidedPlayStatus.Running,90,"Disembarked rear guard must reach camp before victory");
                Assert.AreEqual(GuidedPlayStatus.Completed,s.Status,Diagnostic(s));Assert.AreEqual(0,s.World.Winner);
                Assert.GreaterOrEqual(s.World.Events.Count(e=>e.kind=="Embarked"),4);Assert.GreaterOrEqual(s.World.Events.Count(e=>e.kind=="Disembarked"),4);
                Assert.IsTrue(s.World.ScenarioEntity("forward-port").Alive);
            }
        }
        [TestCase(RiverContent070.Scout)][TestCase(RiverContent070.Convoy)][TestCase(RiverContent070.Retreat)]
        public void GenuineResignationFailsAndRetryStartsANewUnrewardedAttempt(string id)
        {
            using(var s=GuidedPlaySession.Start(rules,id))
            {
                string hash=s.World.ScenarioHash;string attempt=s.AttemptId;Accepted(s,new GameCommand{player=0,kind=CommandKind.Resign});
                Assert.AreEqual(GuidedPlayStatus.Failed,s.Status);Assert.IsFalse(AccountProgressStore.Load(memoryOnly:true).RecordCompletion(s));
                using(var retry=s.Retry())
                {
                    Assert.AreNotEqual(attempt,retry.AttemptId);Assert.AreEqual(hash,retry.World.ScenarioHash);Assert.AreEqual(0,retry.CaptureState().stageIndex);
                    Assert.AreEqual(GuidedPlayStatus.Running,retry.Status);Assert.AreEqual(0,retry.World.Time);Assert.AreEqual(6,retry.CaptureState().soldiers.Length);
                    Assert.IsTrue(retry.World.CaptureState().scenarioState.triggerStates.All(t=>t.fires==0));
                }
            }
        }
        [TestCase(RiverContent070.Convoy,"transport")][TestCase(RiverContent070.Retreat,"forward-port")]
        public void LossOfTheNamedProtectedAssetFailsEvenWhenOtherStartingUnitsSurvive(string id,string asset)
        {
            using(var s=GuidedPlaySession.Start(rules,id))
            {
                s.World.ScenarioEntity(asset).hp=0; // Explicit loss fixture.
                Until(s,()=>s.Status==GuidedPlayStatus.Failed,1,"Protected asset destruction must be an actual failure");
                Assert.IsTrue(s.CaptureState().soldiers.Any(n=>s.World.Get(n).Alive));Assert.AreEqual(1,s.World.Winner);
            }
        }
        [TestCase(RiverContent070.Convoy)][TestCase(RiverContent070.Retreat)]
        public void LosingThreeBoundTroopsFailsRatherThanAllowingWorkersOrReplacementUnitsToCount(string id)
        {
            using(var s=GuidedPlaySession.Start(rules,id))
            {
                foreach(int n in s.CaptureState().soldiers.Take(3))s.World.Get(n).hp=0; // Explicit group-loss fixture.
                Until(s,()=>s.Status==GuidedPlayStatus.Failed,1,"Fewer than four designated survivors must fail");
                Assert.AreEqual(6,s.World.Entities.Count(e=>e.owner==0&&e.Worker&&e.Alive));
            }
        }
        [Test] public void NewArcUnlocksSequentiallyOncePerActualAttemptAndPreservesTheRevisionOneLegacyAccount()
        {
            var store=AccountProgressStore.Load(Path.Combine(directory,"Account"));
            using(var old=GuidedPlaySession.Start(rules,"lesson_selection","legacy-revision-one"))
            {
                old.ObserveSelection(old.SuggestedActors);Order(old,old.SuggestedActors,OrderKind.Move,old.ObjectivePoint);
                Until(old,()=>old.Status==GuidedPlayStatus.Completed,80,"Real legacy lesson completion");Assert.IsTrue(store.RecordCompletion(old),store.LastError);
            }
            byte[] oldBytes=File.ReadAllBytes(store.FilePath);store=AccountProgressStore.Load(Path.Combine(directory,"Account"));
            Assert.IsTrue(store.CanWrite);Assert.AreEqual(1,GuidedPlayCatalog.Revision);Assert.AreEqual(1,store.CompletionCount("lesson_selection"));
            CollectionAssert.AreEqual(oldBytes,File.ReadAllBytes(store.FilePath));Assert.IsTrue(store.IsUnlocked(RiverContent070.Scout));
            Assert.IsFalse(store.IsUnlocked(RiverContent070.Convoy));Assert.IsFalse(store.IsUnlocked(RiverContent070.Retreat));Assert.IsFalse(store.IsUnlocked("campaign_bridge"));
            foreach(string id in new[]{RiverContent070.Scout,RiverContent070.Convoy,RiverContent070.Retreat})
            using(var s=GuidedPlaySession.Start(rules,id))
            {
                Assert.IsTrue(store.IsUnlocked(id));SolveRemaining(s);Assert.IsTrue(store.RecordCompletion(s),store.LastError);Assert.IsTrue(store.RecordCompletion(s),store.LastError);
                Assert.AreEqual(1,store.CompletionCount(id));using(var cold=Cold(s))Assert.IsTrue(store.RecordCompletion(cold),store.LastError);
                Assert.AreEqual(1,store.CompletionCount(id));store=AccountProgressStore.Load(Path.Combine(directory,"Account"));Assert.IsTrue(store.CanWrite);
            }
            Assert.AreEqual(1,store.CompletionCount("lesson_selection"));Assert.IsFalse(store.IsUnlocked("campaign_bridge"));
            Assert.IsTrue(store.IsUnlocked(RiverContent070.Retreat));
        }
        [Test] public void AlteredFrozenChapterOrHashCannotEarnAnUnlockEvenWithRecomputedPortableHashes()
        {
            using(var s=GuidedPlaySession.Start(rules,RiverContent070.Scout))
            {
                SolveRemaining(s);var store=AccountProgressStore.Load(memoryOnly:true);var guided=s.CaptureState();
                var wrong=SnapshotCodec.Clone(guided);wrong.scenarioHash=new string('0',64);
                Assert.Throws<ArgumentException>(()=>GuidedPlaySession.Restore(s.World,s.Simulation,wrong));Assert.IsFalse(store.IsUnlocked(RiverContent070.Convoy));
                var snapshot=s.World.CaptureState();snapshot.scenarioDocument.description+=" Altered chapter";
                snapshot.scenarioHash=ScenarioCodec.ContentHash(snapshot.scenarioDocument);snapshot.scenarioState.documentHash=snapshot.scenarioHash;
                var altered=World.Restore(rules,snapshot);var sim=MatchSimulation.Restore(altered,s.Simulation.CaptureState());
                guided.scenarioHash=altered.ScenarioHash;string before=JsonUtility.ToJson(altered.CaptureState());
                Assert.Throws<ArgumentException>(()=>GuidedPlaySession.Restore(altered,sim,guided));Assert.AreEqual(before,JsonUtility.ToJson(altered.CaptureState()));
                Assert.IsFalse(store.IsUnlocked(RiverContent070.Convoy));Assert.AreEqual(0,store.CompletedCount);
            }
        }
        [Test] public void ActualAuthoredConvoyRecordingRebuildsCargoTriggersObjectivesAndGuidedResultForward()
        {
            var identity=ReplayIdentity.Create(rules,"river070-tests","synthetic-070","synthetic-unity-070");
            using(var s=GuidedPlaySession.Start(rules,RiverContent070.Convoy))
            using(var recorder=new ReplayRecorder(s.Simulation,identity,s,"authored-convoy070",100))
            {
                SolveRemaining(s,recorder);string world=JsonUtility.ToJson(s.World.CaptureState());string guided=JsonUtility.ToJson(s.CaptureState());
                string digest=ReplayDigest.Compute(s.World,s.Simulation,s);var document=recorder.Finish("guided_completed");
                string path=ReplayStore.PathFor(directory,document.segmentId);ReplayStore.Write(path,document,rules,identity);var cold=ReplayStore.Read(path,rules,identity);
                byte[] immutable=File.ReadAllBytes(path);Assert.IsTrue(cold.anchors[0].world.hasScenarioDocument);
                using(var replay=new ReplaySession(rules,cold,identity))
                {
                    int guard=0;while(replay.CurrentStep<replay.EndStep&&replay.Error==""&&guard++<20000)Assert.Greater(replay.Advance(23),0);
                    Assert.IsEmpty(replay.Error);Assert.IsTrue(replay.Verified);Assert.AreEqual(document.anchors[document.anchors.Length-1].completedStep,replay.CurrentStep);
                    Assert.AreEqual(world,JsonUtility.ToJson(replay.World.CaptureState()));Assert.AreEqual(guided,JsonUtility.ToJson(replay.Guided.CaptureState()));
                    Assert.AreEqual(digest,ReplayDigest.Compute(replay.World,replay.Simulation,replay.Guided));
                    Assert.GreaterOrEqual(replay.World.Events.Count(e=>e.kind=="Embarked"),4);Assert.GreaterOrEqual(replay.World.Events.Count(e=>e.kind=="Disembarked"),4);
                }
                CollectionAssert.AreEqual(immutable,File.ReadAllBytes(path));
            }
        }
    }
}
