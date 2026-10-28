using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class TeamMatch066Tests
    {
        static readonly string[] Maps=MapTopology.All;
        static readonly string[] Civilizations={"coast","france","river","ottoman"};
        static World Four(string map="frontier",int[] teams=null,RuleCatalog rules=null)=>new World(rules??Catalog.Load(),314159,Civilizations,teams??new[]{0,1,0,1},5,map);
        static string State(World world)=>JsonUtility.ToJson(world.CaptureState());
        static void Steps(World world,int count){for(int i=0;i<count&&!world.IsMatchEnded;i++)world.Tick(.1f);}
        static void RemoveActors(World world,int player)
        {foreach(var e in world.Entities.Where(e=>e.Alive&&e.owner==player&&e.def.kind!=EntityKind.Resource).ToArray())world.Remove(e);}
        static World CombatFixture()
        {
            var rules=SnapshotCodec.Clone(Catalog.Load());rules.Entity("villager").damage=0;
            var world=Four(rules:rules);
            foreach(var e in world.Entities.Where(e=>World.Guardian(e)||e.owner>=0&&e.def.kind==EntityKind.Unit).ToArray())world.Remove(e);
            return world;
        }
        static GameCommand Attack(int player,EntityState actor,EntityState target)=>new GameCommand{player=player,kind=CommandKind.Order,order=OrderKind.Attack,actors=new[]{actor.id},target=target.id,point=target.position};

        [TestCaseSource(nameof(Maps))]
        public void FourSlotsHaveDistinctUsableOpeningsIndependentEconomiesAndActualLegalPorts(string map)
        {
            var world=Four(map);Assert.AreEqual(4,world.Layout.Starts.Length);Assert.AreEqual(4,world.Layout.DockSites.Length);Assert.AreEqual(4,world.Layout.LandingSites.Length);
            for(int player=0;player<4;player++)
            {
                var p=world.Players[player];Assert.AreEqual(player,p.id);Assert.AreEqual(Civilizations[player],p.civilization);
                Assert.AreEqual(JsonUtility.ToJson(world.Rules.Civ(p.civilization).startingResources),JsonUtility.ToJson(p.resources));
                Assert.AreEqual(world.Rules.Civ(p.civilization).startingWorkers,world.Entities.Count(e=>e.Alive&&e.owner==player&&e.Worker));
                Assert.AreEqual(1,world.Entities.Count(e=>e.Alive&&e.owner==player&&e.def.id=="town"));
                Assert.AreEqual(1,world.Entities.Count(e=>e.Alive&&e.owner==player&&e.def.id=="explorer"));
                Assert.AreEqual(2,world.Entities.Count(e=>e.Alive&&e.owner==player&&e.def.id.StartsWith("crate_",StringComparison.Ordinal)));
                var town=world.Entities.Single(e=>e.Alive&&e.owner==player&&e.def.id=="town");Assert.AreEqual(world.OpeningTownPosition(player),town.position);
                foreach(var cell in world.BuildingFootprintCells(town.def,town.position))Assert.IsFalse(world.IsWater(cell),map+" town footprint "+player);
                foreach(var actor in world.Entities.Where(e=>e.Alive&&e.owner==player&&e.def.kind==EntityKind.Unit))
                    Assert.IsTrue(world.Navigation.IsWalkable(actor.position,actor.def.radius),map+" trapped starting actor "+actor.id);
                for(int other=0;other<player;other++)Assert.GreaterOrEqual(Vector2.Distance(town.position,world.OpeningTownPosition(other)),42);
                if(!world.Layout.HasWater)continue;
                Assert.IsNotEmpty(world.Layout.DockSites[player]);Assert.IsNotEmpty(world.Layout.LandingSites[player]);
                var worker=world.Entities.First(e=>e.Alive&&e.owner==player&&e.Worker);p.age=2;p.resources=new Cost(2000,2000,2000);
                foreach(var shore in world.Layout.DockSites[player])
                {
                    foreach(var start in world.Layout.Starts)Assert.GreaterOrEqual(Vector2.Distance(shore,start.Town),14,"Harbor must reserve the town/warehouse/quay separation yard");
                    world.Fog.Reveal(player,shore,12);Assert.AreEqual("",world.DockPlacementReason(shore));
                    Assert.AreEqual("",world.CanBuild(player,"dock",shore),map+" player "+player+" harbor "+shore);
                    var end=world.Navigation.NearestWalkable(shore,worker.def.radius);var path=world.Navigation.FindPath(worker.position,end,worker.def.radius);
                    Assert.IsNotEmpty(path);Assert.Less(Vector2.Distance(path.Last(),end),.01f,"Own worker must reach its actual harbor");
                }
            }
            foreach(var e in world.Entities.Where(e=>e.Alive&&!e.def.waterborne&&e.def.kind!=EntityKind.Building))
            {Assert.IsFalse(world.IsWater(e.position),map+" starts on water: "+e.id);Assert.IsTrue(world.Navigation.IsWalkable(e.position,e.def.radius),map+" invalid spawn "+e.id);}
            world.Tick(.1f);Assert.IsFalse(world.IsMatchEnded);
        }

        [TestCaseSource(nameof(Maps))]
        public void LegacyConstructorAndEquivalentTwoSlotRosterHaveIdenticalOpeningState(string map)
        {
            var rules=Catalog.Load();var old=new World(rules,314159,"france","ottoman",5,map);
            var roster=new World(rules,314159,new[]{"france","ottoman"},new[]{0,1},5,map);
            Assert.AreEqual(State(old),State(roster));
        }

        [TestCaseSource(nameof(Maps))]
        public void ThreeSlotFfaUsesAllNineActualMapLayouts(string map)
        {
            var world=new World(Catalog.Load(),314159,new[]{"coast","france","river"},new[]{0,1,2},5,map);
            Assert.AreEqual(3,world.Layout.Starts.Length);Assert.AreEqual(3,world.Layout.DockSites.Length);Assert.AreEqual(3,world.Layout.LandingSites.Length);
            for(int player=0;player<3;player++)
            {Assert.IsTrue(world.IsPlayerActive(player));Assert.IsTrue(world.AreEnemies(player,world.FirstEnemyPlayer(player)));Assert.AreEqual(1,world.Entities.Count(e=>e.Alive&&e.owner==player&&e.def.id=="town"));}
            Assert.AreEqual(State(world),State(World.Restore(world.Rules,world.CaptureState())));
        }

        [Test] public void ThreeContinuousFfaSlotsAreLegalAndInvalidRostersAreRejected()
        {
            var rules=Catalog.Load();var world=new World(rules,27,new[]{"coast","france","ottoman"},new[]{0,1,2});Assert.AreEqual(3,world.Players.Length);
            Assert.Throws<ArgumentException>(()=>new World(rules,27,new[]{"coast"},new[]{0}));
            Assert.Throws<ArgumentException>(()=>new World(rules,27,new[]{"coast","coast","coast","coast","coast"},new[]{0,1,2,3,0}));
            Assert.Throws<ArgumentException>(()=>new World(rules,27,Civilizations,new[]{0,0,0,0}));
            Assert.Throws<ArgumentException>(()=>new World(rules,27,Civilizations,new[]{0,1}));
            Assert.Throws<ArgumentException>(()=>new World(rules,27,Civilizations,new[]{0,1,2,4}));
            Assert.Throws<ArgumentException>(()=>new World(rules,27,new[]{"coast","unknown"},new[]{0,1}));
        }

        [Test] public void TeamVisionIsSharedWhileControlAndPaidEconomyRemainOwnedByEachSlot()
        {
            var world=Four();var ally=world.Entities.First(e=>e.owner==2&&e.Worker);var enemy=world.Entities.First(e=>e.owner==1&&e.Worker);
            Assert.IsTrue(world.IsVisible(0,ally));Assert.IsFalse(world.IsVisible(0,enemy));
            int commands=world.Events.Count;Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{ally.id},order=OrderKind.Move,point=ally.position+Vector2.right*5}).accepted);
            Assert.AreEqual(commands,world.Events.Count);Assert.IsEmpty(ally.orders);
            var before=world.Players.Select(p=>JsonUtility.ToJson(p.resources)).ToArray();var town=world.Entities.Single(e=>e.owner==2&&e.def.id=="town");
            Assert.IsTrue(world.Issue(new GameCommand{player=2,kind=CommandKind.Train,actors=new[]{town.id},item=world.WorkerDefinition(2).id}).accepted);
            for(int player=0;player<4;player++)if(player!=2)Assert.AreEqual(before[player],JsonUtility.ToJson(world.Players[player].resources));
            Assert.AreNotEqual(before[2],JsonUtility.ToJson(world.Players[2].resources));
            world.Fog.Reveal(0,new Vector2(64,64),4);Assert.AreEqual(world.Fog.At(0,new Vector2(64,64)),world.Fog.At(2,new Vector2(64,64)));
        }

        [Test] public void AlliedManualAutoWindupAndDirectDamageNeverCauseFriendlyFire()
        {
            var world=CombatFixture();var gun=world.Spawn("cannon",0,new Vector2(48,64));var ally=world.Spawn("villager",2,new Vector2(58,64));world.Fog.Tick(world.Entities);
            Assert.IsFalse(world.Issue(Attack(0,gun,ally)).accepted);float hp=ally.hp;
            world.ApplyDamage(ally,1000,DamageKind.Siege,0,gun.id);Steps(world,40);Assert.AreEqual(hp,ally.hp);Assert.IsFalse(world.Events.Any(e=>e.kind=="Fire"&&e.target==ally.id));
            var enemy=world.Spawn("villager",1,new Vector2(58,66));enemy.hp=10000;world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(Attack(0,gun,enemy)).accepted);
            for(int i=0;i<100&&gun.fireAt<0;i++)world.Tick(.1f);Assert.Greater(gun.fireAt,world.Time,"Fixture stops during an actual prepared attack");
            enemy.owner=2;hp=enemy.hp;Steps(world,10);Assert.AreEqual(hp,enemy.hp);Assert.IsFalse(world.Projectiles.Any(p=>p.source==gun.id));
        }

        [Test] public void InFlightOwnershipChangeAndActualAreaImpactRecheckHostilityAtExecution()
        {
            var world=CombatFixture();var gun=world.Spawn("cannon",0,new Vector2(48,64));var target=world.Spawn("villager",1,new Vector2(59,64));target.hp=10000;world.Fog.Tick(world.Entities);
            Assert.IsTrue(world.Issue(Attack(0,gun,target)).accepted);
            for(int i=0;i<100&&!world.Projectiles.Any(p=>p.source==gun.id);i++)world.Tick(.1f);Assert.IsTrue(world.Projectiles.Any(p=>p.source==gun.id));
            target.owner=2;float hp=target.hp;Steps(world,20);Assert.AreEqual(hp,target.hp);Assert.IsFalse(world.Events.Any(e=>e.kind=="Impact"&&e.target==target.id));
            target.owner=1;var ally=world.Spawn("villager",2,new Vector2(59,65.6f));var hostile=world.Spawn("villager",3,new Vector2(60.6f,64));hostile.hp=10000;
            float allyHp=ally.hp,hostileHp=hostile.hp;int impacts=world.Events.Count(e=>e.kind=="Impact"&&e.target==target.id);world.Fog.Tick(world.Entities);
            Assert.Greater(gun.def.splash,0);Assert.IsTrue(world.Issue(Attack(0,gun,target)).accepted);
            for(int i=0;i<150&&world.Events.Count(e=>e.kind=="Impact"&&e.target==target.id)==impacts;i++)world.Tick(.1f);
            Assert.Greater(world.Events.Count(e=>e.kind=="Impact"&&e.target==target.id),impacts);Assert.AreEqual(allyHp,ally.hp);Assert.Less(hostile.hp,hostileHp,"Actual cannon splash must still damage another hostile slot");
        }

        [Test] public void PlayerEliminationAndResignationPreserveTheTeamUntilItsLastActiveMemberFalls()
        {
            var world=Four();RemoveActors(world,0);world.Tick(.1f);Assert.IsTrue(world.Players[0].eliminated);Assert.IsFalse(world.IsMatchEnded);Assert.AreEqual(1,world.FirstEnemyPlayer(2));
            var result=world.Issue(new GameCommand{player=1,kind=CommandKind.Resign});Assert.IsTrue(result.accepted);Assert.IsFalse(world.IsMatchEnded);
            Assert.IsFalse(world.Issue(new GameCommand{player=1,kind=CommandKind.Train}).accepted);
            RemoveActors(world,3);world.Tick(.1f);Assert.AreEqual(2,world.Winner);Assert.IsTrue(world.IsPlayerWinner(0));Assert.IsTrue(world.IsPlayerWinner(2));Assert.IsFalse(world.IsPlayerWinner(1));
            var cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));Assert.AreEqual(State(world),State(cold));Assert.IsTrue(cold.IsMatchEnded);
        }

        [Test] public void FfaUsesActualRemainingRivalsAndSimultaneousExtinctionIsADraw()
        {
            var world=Four(teams:new[]{0,1,2,3});RemoveActors(world,1);world.Tick(.1f);Assert.AreEqual(2,world.FirstEnemyPlayer(0));Assert.IsFalse(world.IsMatchEnded);
            RemoveActors(world,0);RemoveActors(world,2);world.Tick(.1f);Assert.AreEqual(3,world.Winner);Assert.IsTrue(world.IsPlayerWinner(3));Assert.IsFalse(world.IsPlayerWinner(2));
            var draw=Four(teams:new[]{0,1,2,3});for(int player=0;player<4;player++)RemoveActors(draw,player);draw.Tick(.1f);Assert.AreEqual(-2,draw.Winner);Assert.IsTrue(draw.IsMatchEnded);
            Assert.IsFalse(new MatchSimulation(draw).Step());Assert.AreEqual(State(draw),State(World.Restore(draw.Rules,draw.CaptureState())));
        }

        [Test] public void ObjectiveUsesAlliedOccupantsAndActualFfaContendersIncludingSlotThree()
        {
            World ObjectiveWorld(int[] teams)
            {
                var rules=SnapshotCodec.Clone(Catalog.Load());foreach(var unit in rules.entities){unit.damage=0;unit.meleeDamage=0;}
                var def=Array.Find(rules.mapObjectives,d=>d.id=="defend_bridge");def.holdSeconds=1;def.failureSeconds=.6f;
                var result=Four("river",teams,rules);foreach(var unit in result.Entities.Where(e=>e.def.kind==EntityKind.Unit).ToArray())result.Remove(unit);
                Assert.IsTrue(result.ConfigureMapObjective("defend_bridge").accepted);return result;
            }
            var allied=ObjectiveWorld(new[]{0,1,0,1});var ally=allied.Spawn("musketeer",2,allied.MapObjective.center);ally.hold=true;Steps(allied,12);
            Assert.AreEqual(0,allied.Winner);Assert.IsTrue(allied.IsPlayerWinner(2));Assert.AreEqual("won",allied.MapObjective.status);
            Assert.IsTrue(allied.Events.Where(e=>e.kind.StartsWith("MapObjective",StringComparison.Ordinal)).All(e=>e.visibilityMask==15));
            var ffa=ObjectiveWorld(new[]{0,1,2,3});var first=ffa.Spawn("musketeer",1,ffa.MapObjective.center+new Vector2(0,2));var third=ffa.Spawn("musketeer",3,ffa.MapObjective.center-new Vector2(0,2));first.hold=true;third.hold=true;
            Steps(ffa,4);Assert.IsTrue(ffa.MapObjective.contested);Assert.AreEqual(0,ffa.MapObjective.enemyProgress);ffa.Remove(first);Steps(ffa,2);
            Assert.AreEqual(3,ffa.MapObjective.enemyPlayer);var cold=World.Restore(ffa.Rules,ffa.CaptureState());Steps(ffa,8);Steps(cold,8);
            Assert.AreEqual(3,ffa.Winner);Assert.AreEqual(State(ffa),State(cold));
        }

        [Test] public void SameRulesLegacyTwoRivalSnapshotRestoresItsMissingTeamFields()
        {
            var world=new World(Catalog.Load());world.Tick(.1f);var legacy=world.CaptureState();legacy.schema=2;legacy.teams=null;legacy.discoveries=null;
            legacy.players[1].state.team=0;legacy.mapObjective.enemyPlayer=0;
            var restored=World.Restore(world.Rules,legacy);Assert.AreEqual(0,restored.Players[0].team);Assert.AreEqual(1,restored.Players[1].team);Assert.AreEqual(State(world),State(restored));
            world.Tick(.1f);restored.Tick(.1f);Assert.AreEqual(State(world),State(restored));
        }

        [TestCase("frontier")][TestCase("generated_strait")]
        public void CompleteCompressedColdSaveAndFurtherAiFramesRetainAllFourSlots(string map)
        {
            var world=Four(map);var simulation=new MatchSimulation(world,new[]{0,1,2,3},"standard");for(int i=0;i<250;i++)Assert.IsTrue(simulation.Step());simulation.AdvanceFrame(.05f);
            foreach(int player in new[]{2,3})
            {Assert.IsTrue(world.Events.Any(e=>e.player==player&&e.kind=="Command"));Assert.IsTrue(world.Events.Any(e=>e.player==player&&e.kind=="Payment"));}
            var directory=Path.Combine(Path.GetTempPath(),"Frontier-team-save-"+Guid.NewGuid().ToString("N"));var path=MatchSaveStore.SlotPath(directory,1);
            try
            {
                var saved=new SavedMatch{matchId="team-cold-fixture",appVersion="0.9.0",world=world.CaptureState(),simulation=simulation.CaptureState()};MatchSaveStore.Write(path,saved);
                var read=MatchSaveStore.Read(path,"0.9.0");var cold=World.Restore(world.Rules,read.world);var resumed=MatchSimulation.Restore(cold,read.simulation);
                Assert.AreEqual(State(world),State(cold));Assert.AreEqual(JsonUtility.ToJson(simulation.CaptureState()),JsonUtility.ToJson(resumed.CaptureState()));
                float[] frames={.025f,.075f,.15f,.05f};for(int i=0;i<240;i++)Assert.AreEqual(simulation.AdvanceFrame(frames[i%4]),resumed.AdvanceFrame(frames[i%4]));
                Assert.AreEqual(State(world),State(cold));Assert.AreEqual(JsonUtility.ToJson(simulation.CaptureState()),JsonUtility.ToJson(resumed.CaptureState()));
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }

        [TestCase("teams")][TestCase("singleFaction")][TestCase("id")][TestCase("playerTeam")][TestCase("fogRoster")][TestCase("alliedFog")][TestCase("discoveryRoster")][TestCase("owner")][TestCase("winner")][TestCase("draw")][TestCase("objectivePlayer")]
        public void MalformedRosterVisibilityVictoryAndObjectiveStateAreRejectedWithoutChangingTheLiveWorld(string problem)
        {
            var world=Four();string before=State(world);var state=world.CaptureState();
            switch(problem)
            {
                case "teams":state.teams=new[]{0,1};break;
                case "singleFaction":for(int i=0;i<4;i++){state.teams[i]=0;state.players[i].state.team=0;}break;
                case "id":state.players[3].state.id=2;break;
                case "playerTeam":state.players[3].state.team=2;break;
                case "fogRoster":state.visibility=new int[32768];break;
                case "alliedFog":state.visibility[2*16384]=state.visibility[0]==0?2:0;break;
                case "discoveryRoster":state.discoveries=new LandmarkDiscoverySnapshot[2];break;
                case "owner":state.entities[0].state.owner=4;break;
                case "winner":state.winner=2;break;
                case "draw":state.winner=-2;break;
                case "objectivePlayer":state.mapObjective.player=4;break;
            }
            Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,state));Assert.AreEqual(before,State(world));
        }

        [Test] public void AiRestoreRejectsAlliedSearchAndTransportTargetsAndInvalidControllerSlots()
        {
            var world=Four("generated_strait");var simulation=new MatchSimulation(world,2,3);var saved=simulation.CaptureState();saved.controllers[0].search.enemyPlayer=1;
            Assert.Throws<ArgumentException>(()=>MatchSimulation.Restore(world,saved));
            saved=simulation.CaptureState();saved.controllers[0].waterEconomy.navalOperations.landingPlayer=1;Assert.Throws<ArgumentException>(()=>MatchSimulation.Restore(world,saved));
            saved=simulation.CaptureState();saved.controllers[0].player=4;Assert.Throws<ArgumentException>(()=>MatchSimulation.Restore(world,saved));
        }

        [Test] public void CompletedStepCallbackRunsAfterTheFixedTickAndIsNeverRestoredAsAnOldSubscription()
        {
            var world=Four();var simulation=new MatchSimulation(world);int calls=0;
            simulation.StepCompleted+=()=>{calls++;Assert.AreEqual(calls,simulation.StepCount);Assert.AreEqual(calls*.1f,world.Time,.001f);};
            Assert.IsTrue(simulation.Step());world.Paused=true;Assert.IsFalse(simulation.Step());Assert.AreEqual(1,calls);world.Paused=false;
            var cold=World.Restore(world.Rules,world.CaptureState());var resumed=MatchSimulation.Restore(cold,simulation.CaptureState());int restoredCalls=0;resumed.StepCompleted+=()=>restoredCalls++;
            Assert.IsTrue(resumed.Step());Assert.AreEqual(1,restoredCalls);Assert.AreEqual(1,calls);Assert.AreEqual(2,resumed.StepCount);
        }
    }
}
