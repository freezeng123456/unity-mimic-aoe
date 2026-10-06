using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Controlled colonial military fixtures, not normal age-I route evidence.
    // New reinforcements are genuinely paid, produced and moved by commands.
    public sealed class AiArmyPlan072Tests
    {
        sealed class Arena
        {
            public World world;public AiArmyPlan072 plan;public EntityState barracks;
            AiSearchPlan search;AiWaterEconomy water;AiRevolution071 revolution;AiCivilizationEconomy civilization;
            public Arena(string map="frontier",int openingSoldiers=6,int age=2,bool artillery=false,string objective="")
            {
                world=new World(Catalog.Load(),4702,"coast","coast",5,map);world.Players[0].age=age;
                if(objective!="")
                {var result=world.ConfigureMapObjective(objective);Assert.IsTrue(result.accepted,result.reason);}
                foreach(var e in world.Entities.Where(World.Guardian).ToArray())world.Remove(e);
                var home=world.OpeningTownPosition(0);
                // An existing barracks and opening force are declared fixture inputs.
                barracks=world.Spawn("barracks",0,FindBuilding("barracks",home));
                var d=world.Rules.Entity("musketeer");world.Players[0].resources=world.UnitCost(0,d)*6;
                Initialize();Review();Assert.IsTrue(plan.CaptureState().hasStage,"The real map must provide a legal reachable stage");
                var slots=world.Navigation.FormationSlots(plan.RallyPoint,openingSoldiers,artillery?world.Rules.Entity("cannon").radius*4+.3f:2.1f);
                Assert.AreEqual(openingSoldiers,slots.Count);
                var soldiers=slots.Select((p,i)=>world.Spawn(artillery&&i==slots.Count-1?"cannon":"musketeer",0,p)).ToArray();
                if(soldiers.Length>0)Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=soldiers.Select(e=>e.id).ToArray(),order=OrderKind.Hold}).accepted);
                world.Fog.Tick(world.Entities);Step(9);
            }
            public Vector2 FindBuilding(string definition,Vector2 home)
            {
                var cost=world.EntityCost(0,world.Rules.Entity(definition));world.Players[0].resources=cost;
                for(int ring=10;ring<34;ring+=3)for(int i=0;i<20;i++)
                {
                    float angle=i*Mathf.PI*2/20;var p=home+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*ring;
                    p=new Vector2(Mathf.Round(p.x),Mathf.Round(p.y));
                    if(world.CanBuild(0,definition,p)=="")return p;
                }
                Assert.Fail("No valid fixture foundation for "+definition);return Vector2.zero;
            }
            public void Initialize()
            {
                var profile=AiDifficultyProfile.From(world.Rules,"standard");plan=new AiArmyPlan072(world,0,profile);
                search=new AiSearchPlan(world,0,world.OpeningTownPosition(1));water=new AiWaterEconomy(world,0);
                revolution=new AiRevolution071(world,0,profile);civilization=new AiCivilizationEconomy(world,0);
            }
            public void Review()
            {
                var own=world.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
                var army=own.Where(e=>e.Military&&!e.def.waterborne).ToList();
                var visible=world.Entities.Where(e=>e.Alive&&world.AreEnemies(0,e.owner)&&world.IsVisible(0,e)).ToList();
                plan.Review(own,army,visible,world.OpeningTownPosition(1),true,search,water,revolution,civilization);
            }
            public void Step(float seconds)
            {for(int i=0;i<Mathf.CeilToInt(seconds/.1f);i++){Review();world.Tick(.1f);}}
        }

        [Test] public void FirstCohortUsesNormalGroupCommandAndProducersRallyToActualSafeStage()
        {
            var a=new Arena();var w=a.world;
            Assert.AreEqual(AiArmyPhase072.March,a.plan.Phase);Assert.AreEqual(6,a.plan.WaveMembers.Length);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="RallySet"&&e.entity==a.barracks.id));
            Assert.Less(Vector2.Distance(a.barracks.rally,a.plan.RallyPoint),.01f);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Command"&&e.player==0&&e.message.StartsWith("AttackMove")&&e.amount==6),"The actual first wave must share a formation command");
            Assert.IsTrue(w.Navigation.IsWalkable(a.plan.RallyPoint,.4f));
            foreach(int id in a.plan.WaveMembers)Assert.AreEqual(OrderKind.AttackMove,w.Get(id).orders.Peek().kind);
        }

        [Test] public void PaidNewSoldiersStayHomeUntilARealReinforcementCohortAssembles()
        {
            var a=new Arena();var w=a.world;var oldIds=new HashSet<int>(w.Entities.Select(e=>e.id));
            var cost=w.UnitCost(0,w.Rules.Entity("musketeer"))*3;
            var before=w.Players[0].resources;
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{a.barracks.id},item="musketeer",count=3}).accepted);
            Assert.AreEqual(before.food-cost.food,w.Players[0].resources.food,.001f);
            a.Step(40);var first=w.Entities.Where(e=>e.Alive&&e.owner==0&&e.def.id=="musketeer"&&!oldIds.Contains(e.id)).ToArray();
            Assert.AreEqual(3,first.Length);Assert.IsFalse(first.Any(e=>a.plan.WaveMembers.Contains(e.id)));
            Assert.IsFalse(w.Events.Any(e=>e.kind=="AIArmyReinforcement"));
            Assert.IsTrue(first.All(e=>Vector2.Distance(e.position,a.plan.RallyPoint)<12),"New paid troops must actually stay near the stage");
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{a.barracks.id},item="musketeer",count=3}).accepted);
            a.Step(40);
            var joined=w.Events.Where(e=>e.kind=="AIArmyReinforcement").ToArray();Assert.AreEqual(1,joined.Length);
            Assert.AreEqual(4,joined[0].amount,"Two locally available soldiers remain as guards; four really reinforce together");
            Assert.AreEqual(2,a.plan.GuardMembers.Length);Assert.AreEqual(10,a.plan.WaveMembers.Length);
            Assert.AreEqual(6,w.Events.Where(e=>e.kind=="Spawn"&&e.player==0&&!oldIds.Contains(e.entity)&&w.Get(e.entity)?.def.id=="musketeer").Count());
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Command"&&e.player==0&&e.message.StartsWith("AttackMove")&&e.amount>=4&&e.time>=joined[0].time));
        }

        [Test] public void VisibleWorkerDoesNotRecallWaveButVisibleMilitaryAtRemoteEconomicLineDoes()
        {
            var a=new Arena(objective:"escort_caravan");var w=a.world;var home=w.OpeningTownPosition(0);
            var visitor=w.Spawn("villager",1,home+new Vector2(16,0));w.Fog.Tick(w.Entities);Assert.IsTrue(w.IsVisible(0,visitor));
            a.Step(9);Assert.AreEqual(AiArmyPhase072.March,a.plan.Phase,"A civilian visitor is not a military raid");
            // Configure the real objective at Time==0 in Arena. Let ordinary
            // time reach the next review before declaring this visible raid, so
            // it cannot kill its worker or be killed before the decision tested.
            while(w.Time<a.plan.CaptureState().reviewAt+.01f)w.Tick(.1f);
            var line=home+new Vector2(44,0);var worker=w.Spawn("villager",0,w.Navigation.NearestWalkable(line));
            var raider=w.Spawn("pike",1,w.Navigation.NearestWalkable(worker.position+new Vector2(4,0)));
            Assert.IsTrue(w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{raider.id},order=OrderKind.Hold}).accepted);
            w.Fog.Tick(w.Entities);Assert.IsTrue(w.IsVisible(0,raider));Assert.Greater(Vector2.Distance(home,raider.position),34);
            Assert.IsTrue(w.TryAiObjectiveDestination(0,out var objective));
            Assert.Greater(Vector2.Distance(objective,raider.position),18,"The active public objective must differ from the actual raid");
            a.Review();
            Assert.AreEqual(AiArmyPhase072.Defend,a.plan.Phase,"An actually visible raider threatens the owned worker line");
            Assert.IsTrue(w.Events.Any(e=>e.kind=="AIArmyPhase"&&e.message.Contains("Visible military")));
            var soldier=w.Get(a.plan.WaveMembers[0]);
            Assert.Less(Vector2.Distance(soldier.orders.Peek().point,raider.position),12,"Objective cannot replace the real military defense destination");
            a.Step(45);Assert.IsTrue(w.Events.Any(e=>e.kind=="Damage"&&e.player==0&&e.target==raider.id),"The dispatched defense must really reach and shoot the visible raider");
        }

        [Test] public void HomeFollowsAReallyPaidAndCompletedReplacementTown()
        {
            var a=new Arena(openingSoldiers:0);var w=a.world;var town=w.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="town");
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Demolish,actors=new[]{town.id}}).accepted);
            var point=a.FindBuilding("town",w.OpeningTownPosition(0));var worker=w.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker);
            var cost=w.EntityCost(0,w.Rules.Entity("town"));var before=w.Players[0].spent;
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{worker.id},item="town",point=point}).accepted);
            var replacement=w.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id=="town");
            for(int i=0;i<2400&&!replacement.Complete;i++)w.Tick(.1f);
            Assert.IsTrue(replacement.Complete,"Actual construction, not an inserted completed replacement");
            Assert.AreEqual(before.wood+cost.wood,w.Players[0].spent.wood,.001f);
            Assert.Less(Vector2.Distance(a.plan.Home,replacement.position),.01f);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="BuildingComplete"&&e.entity==replacement.id));
        }

        [TestCase("Muster")][TestCase("March")][TestCase("Defend")]
        public void ColdRestorePreservesCohortAndTheActualSubsequentWorld(string phase)
        {
            var a=new Arena(openingSoldiers:phase=="Muster"?3:6);var w=a.world;
            if(phase=="Defend")
            {
                float next=a.plan.CaptureState().reviewAt;while(w.Time<next+.01f)w.Tick(.1f);
                var enemy=w.Spawn("pike",1,w.OpeningTownPosition(0)+new Vector2(18,0));
                Assert.IsTrue(w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{enemy.id},order=OrderKind.Hold}).accepted);
                w.Fog.Tick(w.Entities);a.Review();
            }
            Assert.AreEqual(phase,a.plan.Phase.ToString());
            var state=JsonUtility.FromJson<AiArmyPlan072Snapshot>(JsonUtility.ToJson(a.plan.CaptureState()));
            var cold=World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));
            var b=new AiArmyPlan072(cold,0,AiDifficultyProfile.From(cold.Rules,"standard"));b.RestoreState(state);
            var search=new AiSearchPlan(cold,0,cold.OpeningTownPosition(1));var water=new AiWaterEconomy(cold,0);
            var rev=new AiRevolution071(cold,0,AiDifficultyProfile.From(cold.Rules,"standard"));var civ=new AiCivilizationEconomy(cold,0);
            for(int i=0;i<100;i++)
            {
                a.Review();var own=cold.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
                b.Review(own,own.Where(e=>e.Military&&!e.def.waterborne).ToList(),cold.Entities.Where(e=>e.Alive&&cold.AreEnemies(0,e.owner)&&cold.IsVisible(0,e)).ToList(),cold.OpeningTownPosition(1),true,search,water,rev,civ);
                w.Tick(.1f);cold.Tick(.1f);
            }
            Assert.AreEqual(JsonUtility.ToJson(a.plan.CaptureState()),JsonUtility.ToJson(b.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(w.CaptureState()),JsonUtility.ToJson(cold.CaptureState()),"Cold world must preserve IDs, real movement, attacks, population and event sequence");
        }

        [Test] public void LegacyMissingPlanMustersAndInvalidOwnershipDoesNotRestore()
        {
            var a=new Arena();var fresh=new AiArmyPlan072(a.world,0,AiDifficultyProfile.From(a.world.Rules,"standard"));
            fresh.RestoreState(null);Assert.AreEqual(AiArmyPhase072.Muster,fresh.Phase);Assert.IsEmpty(fresh.WaveMembers);
            var state=a.plan.CaptureState();state.wave=new[]{a.world.Entities.First(e=>e.owner==1).id};
            Assert.Throws<ArgumentException>(()=>fresh.RestoreState(state));
            state=a.plan.CaptureState();state.guards=new[]{state.wave[0]};Assert.Throws<ArgumentException>(()=>fresh.RestoreState(state));
        }

        [Test] public void VisibleTowerSafetyUsesRealWeaponEnvelopeAndAvoidsAnExposedStage()
        {
            var a=new Arena(openingSoldiers:3);var w=a.world;var oldStage=a.plan.RallyPoint;
            while(w.Time<a.plan.CaptureState().reviewAt+.01f)w.Tick(.1f);
            var towerDefinition=w.Rules.Entity("tower");var towerPoint=Vector2.zero;bool found=false;
            foreach(var offset in new[]{new Vector2(21,0),new Vector2(21,4),new Vector2(21,-4)})
            {
                var point=(Vector2)Vector2Int.RoundToInt(oldStage+offset);
                if(!w.Navigation.IsAreaFree(point,towerDefinition.width+1,towerDefinition.depth+1))continue;
                if(w.Entities.Any(e=>e.Alive&&e.garrisonedIn==0&&(e.def.kind==EntityKind.Building?
                    w.BuildingFootprintsOverlap(towerDefinition,point,e.def,e.position,1):
                    w.BuildingFootprintContains(towerDefinition,point,e.position,e.def.radius))))continue;
                towerPoint=point;found=true;break;
            }
            Assert.IsTrue(found,"The controlled tower must occupy a legal unoccupied land footprint");
            var tower=w.Spawn("tower",1,towerPoint);
            // An opening scout observes the static weapon. A scout by itself is
            // not an economic asset whose presence recalls the entire army.
            var scout=w.Spawn("explorer",0,w.Navigation.NearestWalkable(tower.position+new Vector2(0,23)));
            Assert.Greater(w.EdgeDistance(tower,scout),tower.def.range,"The real observing scout stays outside the tower's fire");
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{scout.id},order=OrderKind.Hold}).accepted);
            w.Fog.Tick(w.Entities);Assert.IsTrue(w.IsVisible(0,tower));
            var noAssets=new List<EntityState>();Assert.IsFalse(a.plan.HasImmediateThreat(noAssets,new List<EntityState>{tower}));
            var worker=w.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker);
            var inRange=w.Spawn("villager",0,w.Navigation.NearestWalkable(tower.position+new Vector2(4,0)));
            Assert.IsTrue(a.plan.HasImmediateThreat(new List<EntityState>{inRange},new List<EntityState>{tower}));
            Assert.IsFalse(a.plan.HasImmediateThreat(new List<EntityState>{worker},new List<EntityState>{tower}),"A visible but out-of-range tower cannot recall defenders just because it is near an old home");
            int first=w.Events.Count;a.Review();
            var snapshot=a.plan.CaptureState();
            Assert.AreEqual(AiArmyPhase072.Defend,a.plan.Phase,"The same review still dispatches against the real threat");
            Assert.IsTrue(snapshot.hasStage,"This open fixture must provide another reachable safe stage");
            Assert.GreaterOrEqual(w.BuildingFootprintDistance(tower.def,tower.position,snapshot.stage),tower.def.range+6,"Never gather a formation in the observed tower firing envelope");
            Assert.Greater(Vector2.Distance(oldStage,snapshot.stage),1);
            Assert.Less(Vector2.Distance(a.barracks.rally,snapshot.stage),.01f,"Future real production is redirected during defense");
            Assert.IsTrue(w.Events.Skip(first).Any(e=>e.kind=="RallySet"&&e.entity==a.barracks.id&&(e.position-snapshot.stage).sqrMagnitude<.01f));
        }

        [Test] public void FullSimulationColdRecoveryRetainsArmyDecisionsAndActualPaidEconomy()
        {
            var a=new Arena();var live=new MatchSimulation(a.world,new[]{0},"standard");
            for(int i=0;i<50;i++)Assert.IsTrue(live.Step());
            var state=SnapshotCodec.Clone(live.CaptureState());Assert.IsNotNull(state.controllers.Single().armyPlan);
            Assert.Greater(state.controllers.Single().armyPlan.wave.Length,0,"The full controller must capture its actual committed cohort");
            var cold=World.Restore(a.world.Rules,SnapshotCodec.Clone(a.world.CaptureState()));
            var restored=MatchSimulation.Restore(cold,state);
            for(int i=0;i<200;i++)Assert.AreEqual(live.Step(),restored.Step());
            Assert.AreEqual(JsonUtility.ToJson(live.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(a.world.CaptureState()),JsonUtility.ToJson(cold.CaptureState()),"Whole simulation recovery must reproduce real group commands, payments, production, movement and attacks");
        }

        [Test] public void NearbySoldierBehindLongWallMustReallyGoAroundBeforeWaveDeparts()
        {
            var a=new Arena(openingSoldiers:5);var w=a.world;var stage=a.plan.RallyPoint;
            for(int y=-15;y<=15;y++)w.Navigation.SetTerrainBlocked(stage+new Vector2(5,y),0,true);
            var late=w.Spawn("musketeer",0,stage+new Vector2(8,0));w.Fog.Tick(w.Entities);
            Assert.Less(Vector2.Distance(late.position,stage),10);var route=w.Navigation.FindPath(late.position,stage,late.def.radius);
            Assert.IsNotEmpty(route);Assert.Greater(route.Zip(route.Skip(1),(x,y)=>Vector2.Distance(x,y)).Sum(),14);
            a.Step(8);Assert.AreEqual(AiArmyPhase072.Muster,a.plan.Phase,"Euclidean proximity cannot manufacture a sixth assembled soldier");
            for(int i=0;i<600&&a.plan.Phase==AiArmyPhase072.Muster;i++)
            {a.Review();w.Tick(.1f);Assert.IsTrue(w.Navigation.IsWalkable(late.position,late.def.radius));}
            Assert.AreEqual(AiArmyPhase072.March,a.plan.Phase,"The ordinary movement command must really deliver the late soldier around the wall");
            Assert.Less(Vector2.Distance(late.position,stage),10);
        }

        [Test] public void MixedRealWaveCrossesCurvedRiverOnlyOnBridgeAndActuallyReachesEnemy()
        {
            var a=new Arena(MapTopology.Winding,10,3,true);var w=a.world;
            var cohort=w.Entities.Where(e=>e.Alive&&e.owner==0&&e.Military&&(e.def.tags&Tags.Explorer)==0).ToArray();
            var crossed=new HashSet<int>();
            for(int i=0;i<2000&&!w.IsMatchEnded;i++)
            {
                a.Review();w.Tick(.1f);
                foreach(var unit in cohort.Where(e=>e.Alive))
                {
                    Assert.IsTrue(w.Navigation.IsWalkable(unit.position,unit.def.radius),unit.def.id+" leaves legal ground");
                    if(w.IsWater(unit.position)){Assert.IsTrue(w.IsBridge(unit.position),"Land soldier cannot walk across open curved water");crossed.Add(unit.id);}
                }
                if(crossed.Count==cohort.Length&&cohort.All(e=>w.Events.Any(v=>v.kind=="Damage"&&v.entity==e.id&&v.player==0)))break;
            }
            Assert.AreEqual(cohort.Length,crossed.Count,"Every real infantry/cannon body must cross a bridge, not simply receive an order");
            foreach(var unit in cohort)Assert.IsTrue(w.Events.Any(e=>e.kind=="Damage"&&e.entity==unit.id&&e.player==0),unit.def.id+" must actually reach and attack after crossing");
        }
    }
}
