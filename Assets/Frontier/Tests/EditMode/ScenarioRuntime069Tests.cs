using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ScenarioRuntime069Tests
    {
        static ScenarioDocument Blank(int players=2)
        {
            var rules=Catalog.Load();var doc=new ScenarioDocument{id="runtime-fixture",name="Authored runtime fixture",rulesHash=SnapshotCodec.RulesHash(rules),starts=new ScenarioStart[players]};
            var towns=new[]{new Vector2(24,28),new Vector2(104,100),new Vector2(24,100),new Vector2(104,28)};
            for(int i=0;i<players;i++)doc.starts[i]=new ScenarioStart{player=i,team=i,civilization="coast",town=towns[i],explorer=towns[i]+new Vector2(7,4),workers=new[]{towns[i]+new Vector2(5,-3)},initialResources=new Cost(300,300,100)};
            return doc;
        }
        static World Play(ScenarioDocument doc)=>new World(Catalog.Load(),doc,doc.starts.Select(s=>s.civilization).ToArray(),doc.starts.Select(s=>s.team).ToArray(),5);
        static string State(World world)=>JsonUtility.ToJson(world.CaptureState(false));
        static ScenarioDocument Triggered(int players=2)
        {
            var doc=Blank(players);
            doc.triggers=new[]{new ScenarioTrigger{id="reinforcements",conditions=new[]{new ScenarioCondition{kind="time",time=.2f}},actions=new[]{
                new ScenarioAction{kind="grant_resource",player=0,resource=ResourceKind.Wood,amount=25},
                new ScenarioAction{kind="spawn",player=0,definition="musketeer",count=2,point=new Vector2(40,30),objectId="reinforcement-group"},
                new ScenarioAction{kind="order_move",player=0,objectId="reinforcement-group",point=new Vector2(48,38)}}}};
            return doc;
        }
        [TestCase("frontier",2)][TestCase("river",2)][TestCase("winding",2)][TestCase("tropical",2)][TestCase("tributaries",2)][TestCase("estuary",2)]
        [TestCase("generated_river",2)][TestCase("generated_delta",2)][TestCase("generated_strait",2)]
        [TestCase("frontier",4)][TestCase("river",4)][TestCase("winding",4)][TestCase("tropical",4)][TestCase("tributaries",4)][TestCase("estuary",4)]
        [TestCase("generated_river",4)][TestCase("generated_delta",4)][TestCase("generated_strait",4)]
        public void ActualTemplateExpansionPlaysAtEveryAuthoredPlacement(string map,int players)
        {
            var rules=Catalog.Load();var doc=ScenarioEditorSession.FromMap(rules,map,314159,players);
            Assert.IsEmpty(ScenarioCompiler.Validate(doc,rules,players),map);
            var world=Play(doc);Assert.IsTrue(world.HasScenario);Assert.AreEqual(doc.resources.Length,world.Entities.Count(e=>e.def.kind==EntityKind.Resource&&!World.Treasure(e)&&e.def.id!="trade_socket"));
            foreach(var resource in doc.resources)Assert.AreEqual(resource.position,world.ScenarioEntity(resource.id).position,"No silent resource relocation");
            for(int player=0;player<players;player++)Assert.AreEqual(doc.starts[player].town,world.OpeningTownPosition(player));
            Assert.DoesNotThrow(()=>World.ValidateSnapshot(rules,world.CaptureState()));
        }
        [Test]
        public void TangentUnitBodiesPreserveCoordinatesWhilePhysicalIntrusionIsRejected()
        {
            var doc=Blank();float diameter=Catalog.Load().Entity("musketeer").radius*2;
            doc.objects=new[]{
                new ScenarioPlacement{id="first-unit",definition="musketeer",position=new Vector2(40,40),owner=0},
                new ScenarioPlacement{id="second-unit",definition="musketeer",position=new Vector2(40,40+diameter),owner=0}};
            var world=Play(doc);
            Assert.AreEqual(doc.objects[0].position,world.ScenarioEntity("first-unit").position);
            Assert.AreEqual(doc.objects[1].position,world.ScenarioEntity("second-unit").position);
            Assert.IsEmpty(world.ScenarioPlacementReason("musketeer",new Vector2(40,40+diameter*2)),"Exact physical tangency is legal");
            Assert.IsNotEmpty(world.ScenarioPlacementReason("musketeer",new Vector2(40,40+diameter*2-.01f)),"Body intrusion remains illegal");
            doc.objects[1].position=new Vector2(40,40+diameter-.01f);
            Assert.IsNotEmpty(ScenarioCompiler.Validate(doc,Catalog.Load(),doc.starts.Length));
            Assert.Throws<ArgumentException>(()=>Play(doc));
        }
        [Test]
        public void ResourceClustersAndGatheringUnitsKeepTheirAuthoredPositionsAndAmounts()
        {
            var doc=Blank();Vector2 treePoint=new Vector2(40,40),deerPoint=new Vector2(40,41.8f);
            doc.resources=new[]{new ScenarioPlacement{id="cluster-tree",definition="tree",position=treePoint,remaining=321},new ScenarioPlacement{id="cluster-deer",definition="deer",position=deerPoint,remaining=123}};
            doc.objects=new[]{new ScenarioPlacement{id="gathering-unit",definition="villager",owner=0,position=new Vector2(40.1f,41)}};
            var world=Play(doc);var worker=world.ScenarioEntity("gathering-unit");var tree=world.ScenarioEntity("cluster-tree");var deer=world.ScenarioEntity("cluster-deer");
            Assert.AreEqual(treePoint,tree.position);Assert.AreEqual(deerPoint,deer.position);Assert.AreEqual(doc.objects[0].position,worker.position);
            Assert.AreEqual(321,tree.remaining);Assert.AreEqual(123,deer.remaining);world.Fog.Reveal(0,treePoint,8);
            var command=new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Gather,actors=new[]{worker.id},target=tree.id};
            Assert.IsTrue(world.Issue(command).accepted);float wood=world.Players[0].resources.wood;
            for(int i=0;i<30;i++)world.Tick(.1f);
            Assert.Greater(world.Players[0].resources.wood,wood);Assert.Less(tree.remaining,321);
            Assert.DoesNotThrow(()=>World.Restore(world.Rules,world.CaptureState()));
        }
        [Test]
        public void RotatedDockAndFishingUseTheAuthoredRiverAndDetectARealWarehouseCornerPeak()
        {
            var doc=Blank();doc.starts[0].age=2;
            doc.rivers=new[]{new ScenarioRiver{id="diagonal-channel",smooth=false,points=new[]{new Vector2(-32,-2),new Vector2(128,158)},halfWidths=new[]{5f,5f}}};
            Vector2 quay=new Vector2(60.3f,76.3f),towardsWater=new Vector2(-1,1).normalized;
            doc.resources=new[]{new ScenarioPlacement{id="authored-fish",definition="fish",position=new Vector2(58,88),remaining=222}};
            doc.objects=new[]{new ScenarioPlacement{id="rotated-dock",definition="dock",position=quay,owner=0},
                new ScenarioPlacement{id="authored-boat",definition="fishing_boat",position=quay+towardsWater*8,owner=0}};
            var world=Play(doc);var dock=world.ScenarioEntity("rotated-dock");var boat=world.ScenarioEntity("authored-boat");var fish=world.ScenarioEntity("authored-fish");
            Assert.Less(Vector2.Distance(towardsWater,world.DockWaterDirection(quay)),.08f,"A sampled nearest-bank direction still follows the diagonal river");
            Assert.AreEqual(quay,dock.position);Assert.IsTrue(world.FindSpawnPositions(dock,1,boat.def,out var spawnPoints));
            Assert.IsTrue(world.WaterNavigation.IsWalkable(spawnPoints[0],boat.def.radius));
            Assert.IsTrue(world.CanGather(boat,fish));
            var gather=world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Gather,actors=new[]{boat.id},target=fish.id});Assert.IsTrue(gather.accepted,gather.reason);
            float food=world.Players[0].resources.food;for(int i=0;i<150;i++)world.Tick(.1f);
            Assert.Greater(world.Players[0].resources.food,food);Assert.Less(fish.remaining,222);
            Assert.DoesNotThrow(()=>World.Restore(world.Rules,world.CaptureState()));

            var peak=ScenarioCodec.Clone(doc);peak.objects=Array.Empty<ScenarioPlacement>();
            for(int i=0;i<peak.heightsCm.Length;i++)peak.heightsCm[i]=-10;peak.heightsCm[58+76*129]=42;
            var edited=Play(peak);edited.Fog.Reveal(0,quay,12);
            Assert.LessOrEqual(edited.Layout.FoundationHeightRange(quay,4,4),.5f,"The old axis-aligned square omits the rotated corner vertex");
            Assert.Greater(edited.Layout.FoundationHeightRange(quay,4,4,edited.Layout.WaterDirection(quay)),.5f,"The real rotated warehouse includes that edited corner");
            Assert.AreEqual("Dock needs a level warehouse foundation",edited.ScenarioPlacementReason("dock",quay));
            Assert.AreEqual("Building requires a level foundation",edited.CanBuild(0,"dock",quay));
            peak.objects=new[]{doc.objects[0]};Assert.IsNotEmpty(ScenarioCompiler.Validate(peak,Catalog.Load(),peak.starts.Length));
        }
        [Test]
        public void CompleteFoundationDetectsAnEditedPeakBetweenCoarseSamplePoints()
        {
            var doc=Blank();doc.heightsCm[71+71*129]=40;
            var world=Play(doc);var town=Catalog.Load().Entity("town");
            var site=new Vector2(70,70);
            Assert.AreEqual(.4f,world.Layout.FoundationHeightRange(site,town.width,town.depth),.0001f);
            Assert.IsTrue(world.Navigation.IsAreaFree(site,town.width,town.depth),"The height bump is below the cliff navigation threshold");
            Assert.IsNotEmpty(world.ScenarioPlacementReason("town",site),"Complete building foundation rejects the bump");
            doc.heightsCm[71+71*129]=0;doc.heightsCm[25+29*129]=40;
            Assert.IsNotEmpty(ScenarioCompiler.Validate(doc,Catalog.Load(),doc.starts.Length),"Opening towns use the same complete foundation check");
        }
        [Test]
        public void SingleFactionDraftsStayPortableButReadyUsesTheActualAuthoredOrLanTeams()
        {
            var rules=Catalog.Load();var doc=Blank(4);foreach(var start in doc.starts)start.team=0;
            Assert.IsEmpty(ScenarioCodec.Validate(doc,rules),"Editing and preserving an unfinished team roster remains allowed");
            var restored=ScenarioCodec.Decode(ScenarioCodec.Encode(doc,rules),rules);
            Assert.AreEqual(ScenarioCodec.ContentHash(doc),ScenarioCodec.ContentHash(restored));
            StringAssert.Contains("at least two factions",ScenarioCompiler.Validate(doc,rules,4));
            Assert.Throws<ArgumentException>(()=>Play(doc));
            var lanTeams=new[]{0,1,0,1};Assert.IsEmpty(ScenarioCompiler.Validate(doc,rules,4,lanTeams));
            var lan=new World(rules,doc,doc.starts.Select(s=>s.civilization).ToArray(),lanTeams,5);
            Assert.IsTrue(lan.AreAllies(0,2));Assert.IsTrue(lan.AreEnemies(0,1));Assert.DoesNotThrow(()=>World.Restore(rules,lan.CaptureState()));
            doc.starts[3].team=1;Assert.IsEmpty(ScenarioCompiler.Validate(doc,rules,4));Assert.DoesNotThrow(()=>Play(doc));
        }
        [Test]
        public void FrozenDocumentHeightSurfaceRosterAndResourcesComeFromTheAuthoredData()
        {
            var doc=Blank(3);doc.starts[2].civilization="france";doc.starts[2].age=3;doc.starts[2].initialResources=new Cost(17,29,41);
            doc.resources=new[]{new ScenarioPlacement{id="little-mine",definition="mine",position=new Vector2(48,48),remaining=37}};
            for(int y=68;y<=82;y++)for(int x=68;x<=82;x++)doc.heightsCm[x+y*129]=400;
            doc.surfaces[76+76*128]=3;
            var world=Play(doc);string hash=world.ScenarioHash;
            Assert.AreEqual(4,world.SurfaceHeight(new Vector2(75,75)),.001f);Assert.IsFalse(world.Navigation.IsWalkable(new Vector2(76,76)));
            Assert.IsFalse(world.Navigation.IsWalkable(new Vector2(68,75)),"Height cliff blocks the actual navigation");
            Assert.AreEqual(3,world.Players[2].age);Assert.AreEqual(29,world.Players[2].resources.wood);Assert.AreEqual(37,world.ScenarioEntity("little-mine").remaining);
            doc.heightsCm[75+75*129]=0;world.ScenarioDocument.heightsCm[75+75*129]=0;
            Assert.AreEqual(4,world.SurfaceHeight(new Vector2(75,75)),.001f);Assert.AreEqual(hash,world.ScenarioHash);
            Assert.DoesNotThrow(()=>World.Restore(world.Rules,world.CaptureState()));
        }
        [Test]
        public void FreehandRiverAndSlopeShareWaterNavigationAndBuildingFoundationChecks()
        {
            var doc=Blank();doc.rivers=new[]{new ScenarioRiver{id="freehand",smooth=false,points=new[]{new Vector2(64,-8),new Vector2(75,48),new Vector2(54,90),new Vector2(64,136)},halfWidths=new[]{5f,7,5,5}}};
            for(int y=41;y<=51;y++)for(int x=38;x<=46;x++)doc.heightsCm[x+y*129]=(x-38)*20;
            var world=Play(doc);
            Assert.IsTrue(world.IsWater(new Vector2(75,48)));Assert.IsTrue(world.WaterNavigation.IsWalkable(new Vector2(75,48),.4f));
            Assert.IsFalse(world.Navigation.IsWalkable(new Vector2(75,48)));Assert.IsFalse(world.WaterNavigation.IsWalkable(new Vector2(30,30)));
            Assert.Greater(world.Layout.FoundationHeightRange(new Vector2(42,46),4,4),.25f);
            Assert.AreEqual("Building requires a level foundation",world.ScenarioPlacementReason("house",new Vector2(42,46)));
        }
        [Test]
        public void TriggerGroupCommandsAndRealGrantSurviveSnapshotWithoutExecutingAgain()
        {
            var world=Play(Triggered());world.Tick(.1f);Assert.AreEqual(300,world.Players[0].resources.wood);world.Tick(.1f);
            Assert.AreEqual(325,world.Players[0].resources.wood);Assert.AreEqual(2,world.ScenarioState.bindings.Single(b=>b.objectId=="reinforcement-group").entityIds.Length);
            Assert.IsTrue(world.ScenarioEntity("reinforcement-group").orders.Any(o=>o.kind==OrderKind.Move));
            var grant=world.Events.Single(e=>e.kind=="ScenarioResourceGranted");Assert.IsTrue(grant.hasEconomy);Assert.AreEqual(25,grant.economy.actualDelta.wood);
            var restored=World.Restore(world.Rules,world.CaptureState());Assert.AreEqual(State(world),State(restored));
            for(int i=0;i<20;i++){world.Tick(.1f);restored.Tick(.1f);}
            Assert.AreEqual(State(world),State(restored));Assert.AreEqual(325,restored.Players[0].resources.wood);Assert.AreEqual(1,restored.ScenarioState.triggerStates[0].fires);
        }
        [Test]
        public void BlockedSpawnPlansDoNotPartiallyGrantAndRetryBeforeTheGroupCanFit()
        {
            var doc=Triggered();for(int y=20;y<45;y++)for(int x=29;x<53;x++)doc.surfaces[x+y*128]=3;
            // Move the authored scout out of the deliberately blocked spawn area.
            doc.starts[0].explorer=new Vector2(20,34);
            doc.starts[0].workers=new[]{new Vector2(20,24)};
            var world=Play(doc);for(int i=0;i<15;i++)world.Tick(.1f);
            Assert.AreEqual(300,world.Players[0].resources.wood);Assert.AreEqual(0,world.ScenarioState.triggerStates[0].fires);
            Assert.IsNotEmpty(world.ScenarioState.triggerStates[0].lastError);Assert.IsNull(world.ScenarioEntity("reinforcement-group"));
        }
        [Test]
        public void EnterRegionObjectiveWaitsForActualArrivalAndAllowsScenarioWinWithEnemiesStillAlive()
        {
            var doc=Blank();doc.regions=new[]{new ScenarioRegion{id="destination",center=new Vector2(42,40),size=new Vector2(4,4)}};
            doc.objectives=new[]{new ScenarioObjective{id="arrival",title="Reach the marker",kind="reach_region",regionId="destination",objectId="start-0-explorer",amount=1,player=0}};
            var world=Play(doc);world.Tick(.1f);Assert.AreEqual(-1,world.Winner);
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{world.ScenarioEntity("start-0-explorer").id},order=OrderKind.Move,point=new Vector2(42,40)});Assert.IsTrue(result.accepted,result.reason);
            for(int i=0;i<250&&!world.IsMatchEnded;i++)world.Tick(.1f);
            Assert.AreEqual(0,world.Winner);Assert.IsTrue(world.Entities.Any(e=>e.owner==1&&e.Alive));Assert.IsTrue(world.ScenarioState.scenarioVictory);
            Assert.AreEqual("completed",world.ScenarioObjectiveStates[0].status);Assert.DoesNotThrow(()=>World.Restore(world.Rules,world.CaptureState()));
        }
        [Test]
        public void MalformedHashBindingsTerrainAndAbsentPresenceAreRejectedBeforeOldReplicaChanges()
        {
            var world=Play(Triggered(4));var tracker=new LanQueueTracker();var full=LanProjection.Full(world,"custom-match","custom-epoch",1,0,tracker.Capture(world));
            var replica=new LanReplica(world.Rules);replica.Install(full);string before=State(replica.World);
            var bad=SnapshotCodec.Clone(full);bad.revision=2;bad.world.scenarioHash=new string('0',64);
            Assert.Throws<ArgumentException>(()=>replica.Install(bad));Assert.AreEqual(before,State(replica.World));
            var snapshot=world.CaptureState();snapshot.scenarioState.bindings[0].entityIds[0]=987654;Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,snapshot));
            snapshot=world.CaptureState();int firstId=snapshot.scenarioState.bindings[0].entityIds[0];
            snapshot.scenarioState.bindings[0].entityIds[0]=snapshot.scenarioState.bindings[1].entityIds[0];snapshot.scenarioState.bindings[1].entityIds[0]=firstId;
            Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,snapshot),"Existing actors cannot be rebound to a different authored stable ID");
            snapshot=world.CaptureState();snapshot.landNavigation.terrain[0]=!snapshot.landNavigation.terrain[0];Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,snapshot));
            snapshot=world.CaptureState();snapshot.hasScenarioDocument=false;Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,snapshot));
        }
        [Test]
        public void CustomLanFullDeltaAndReconnectKeepTheDocumentAndDoNotRerunTriggers()
        {
            var host=Play(Triggered(4));var tracker=new LanQueueTracker();var first=LanProjection.Full(host,"custom-match","custom-epoch",1,0,tracker.Capture(host));
            var replica=new LanReplica(host.Rules);replica.Install(LanWire.Body<LanFullState>(LanWire.Decode(LanWire.Encode("full",first))));
            host.Tick(.1f);host.Tick(.1f);var next=LanProjection.Full(host,"custom-match","custom-epoch",2,2,tracker.Capture(host));
            var delta=LanProjection.Delta(first,next,host.Events);Assert.IsNull(delta.globals.scenarioDocument);Assert.IsFalse(delta.hasScenarioDocument);
            Assert.IsTrue(replica.Apply(LanWire.Body<LanStateDelta>(LanWire.Decode(LanWire.Encode("delta",delta)))),replica.Error);
            Assert.AreEqual(State(host),State(replica.World));Assert.AreEqual(325,replica.World.Players[0].resources.wood);
            Assert.IsFalse(replica.Apply(delta));Assert.AreEqual(325,replica.World.Players[0].resources.wood);
            replica.Install(LanProjection.Full(host,"custom-match","custom-epoch",3,2,tracker.Capture(host)));
            Assert.AreEqual(325,replica.World.Players[0].resources.wood);Assert.AreEqual(1,replica.World.ScenarioState.triggerStates[0].fires);
        }
        [Test]
        public void RecordedCustomScenarioReexecutesTheSameTriggersAndSeekDoesNotDoubleGrant()
        {
            var host=Play(Triggered());var simulation=new MatchSimulation(host,Array.Empty<int>());
            var identity=ReplayIdentity.Create(host.Rules,"scenario-test","scenario-build","scenario-unity");
            using(var recorder=new ReplayRecorder(simulation,identity,anchorInterval:5,startKind:"scenario_playtest"))
            {
                for(int i=0;i<20;i++)Assert.IsTrue(simulation.Step());
                var document=recorder.Finish("scenario_test");using(var replay=new ReplaySession(host.Rules,document,identity))
                {
                    while(replay.CurrentStep<replay.EndStep&&replay.Error=="")replay.Advance(50);
                    Assert.IsEmpty(replay.Error);Assert.IsTrue(replay.Verified);Assert.AreEqual(State(host),State(replay.World));
                    replay.BeginSeek(3);while(replay.IsSeeking)replay.AdvanceSeek(50);
                    Assert.IsEmpty(replay.Error);Assert.AreEqual(325,replay.World.Players[0].resources.wood);Assert.AreEqual(1,replay.World.ScenarioState.triggerStates[0].fires);
                }
            }
        }
        [TestCase(2)][TestCase(3)]
        public void ExplicitLegacyWorldSchemaStillRestoresBuiltInMaps(int schema)
        {
            var world=new World(Catalog.Load());var snapshot=world.CaptureState();snapshot.schema=schema;
            Assert.DoesNotThrow(()=>World.Restore(world.Rules,snapshot));
        }
        [Test]
        public void DisablingUnitEliminationStillHonorsActualPlayerResignation()
        {
            var doc=Blank();doc.enableElimination=false;var world=Play(doc);
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Resign});Assert.IsTrue(result.accepted);
            Assert.AreEqual(1,world.Winner);Assert.IsFalse(world.ScenarioState.scenarioVictory);
            Assert.DoesNotThrow(()=>World.Restore(world.Rules,world.CaptureState()));
        }
    }
}
