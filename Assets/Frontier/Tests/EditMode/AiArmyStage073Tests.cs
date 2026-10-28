using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Declared terrain/forces isolate staging from production and normal-game
    // balance. After setup only ordinary planner commands and World.Tick move
    // the bodies; a soldier count never substitutes for actual arrival.
    public sealed class AiArmyStage073Tests
    {
        sealed class Arena
        {
            public readonly World world;
            public readonly AiArmyPlan072 plan;
            public readonly EntityState[] soldiers;
            public readonly EntityState target,barracks;
            public readonly Vector2 home,desired;
            readonly AiSearchPlan search;
            readonly AiWaterEconomy water;
            readonly AiRevolution071 revolution;
            readonly AiCivilizationEconomy civilization;

            public Arena(string geometry,int count=6,int age=2)
            {
                world=new World(Catalog.Load(),7304,"coast","coast",5,"frontier");world.Players[0].age=age;
                foreach(var e in world.Entities.Where(e=>e.Alive&&e.def.id!="town").ToArray())world.Remove(e);
                foreach(var obstacle in world.TerrainObstacles)world.Navigation.SetBlocked(new Vector2(obstacle.x,obstacle.y),obstacle.z,false);
                world.TerrainObstacles.Clear();home=world.OpeningTownPosition(0);
                desired=home+Vector2.ClampMagnitude(new Vector2(64,64)-home,12);
                if(geometry=="compact")
                {
                    // Seven grid rows support a5x5center and actual bodies;
                    // every7x7footprint necessarily touches a blocked row.
                    float row=Mathf.Round(desired.y);
                    world.Navigation.ConfigureTerrain(p=>Mathf.Abs(p.y-row)>3&&
                        !(Mathf.Abs(p.x-home.x)<=3&&p.y>=home.y-3&&p.y<=row+3));
                }
                else if(geometry=="blocked")
                {
                    // A closed ring around the Town leaves soldiers on legal
                    // ground. Outside spacious centers remain genuinely
                    // visible but belong to a disconnected physical component.
                    world.Navigation.ConfigureTerrain(p=>Mathf.Max(Mathf.Abs(p.x-home.x),Mathf.Abs(p.y-home.y))==8);
                }
                else
                {
                    world.Navigation.ConfigureTerrain(_=>false);
                    foreach(var point in OriginalCandidates(desired))world.Navigation.SetTerrainBlocked(point,0,true);
                }
                world.Fog.Reveal(0,new Vector2(64,64),180);
                world.Fog.Reveal(1,new Vector2(64,64),180);
                if(geometry=="expanded")
                {
                    var point=home+new Vector2(-12,12);world.Players[0].resources=world.EntityCost(0,world.Rules.Entity("barracks"));
                    Assert.AreEqual("",world.CanBuild(0,"barracks",point),"The declared producer needs a legal existing footprint");
                    barracks=world.Spawn("barracks",0,point);
                }
                var start=geometry=="blocked"?home+new Vector2(-5,0):new Vector2(home.x-12,Mathf.Round(desired.y));
                var slots=world.Navigation.FormationSlots(start,count,1.3f);Assert.AreEqual(count,slots.Count);
                soldiers=slots.Select(p=>world.Spawn("musketeer",0,p)).ToArray();
                foreach(var soldier in soldiers)Assert.IsTrue(world.Navigation.IsWalkable(soldier.position,soldier.def.radius));
                Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=soldiers.Select(e=>e.id).ToArray(),order=OrderKind.Hold}).accepted);
                if(geometry!="blocked")
                {
                    var point=new Vector2(home.x+52,Mathf.Round(desired.y));world.Players[1].resources=world.EntityCost(1,world.Rules.Entity("house"));
                    Assert.AreEqual("",world.CanBuild(1,"house",point),"The passive enemy target has a legal footprint");
                    target=world.Spawn("house",1,point);
                }
                world.Players[0].resources=new Cost();world.Players[1].resources=new Cost();
                var profile=AiDifficultyProfile.From(world.Rules,"standard");plan=new AiArmyPlan072(world,0,profile);
                search=new AiSearchPlan(world,0,world.OpeningTownPosition(1));water=new AiWaterEconomy(world,0);
                revolution=new AiRevolution071(world,0,profile);civilization=new AiCivilizationEconomy(world,0);
            }

            Arena(Arena source)
            {
                world=World.Restore(source.world.Rules,SnapshotCodec.Clone(source.world.CaptureState()));
                home=source.home;desired=source.desired;soldiers=source.soldiers.Select(e=>world.Get(e.id)).ToArray();
                target=source.target==null?null:world.Get(source.target.id);barracks=source.barracks==null?null:world.Get(source.barracks.id);
                var profile=AiDifficultyProfile.From(world.Rules,"standard");plan=new AiArmyPlan072(world,0,profile);
                plan.RestoreState(SnapshotCodec.Clone(source.plan.CaptureState()));
                search=new AiSearchPlan(world,0,world.OpeningTownPosition(1));search.RestoreState(SnapshotCodec.Clone(source.search.CaptureState()));
                water=new AiWaterEconomy(world,0);water.RestoreState(SnapshotCodec.Clone(source.water.CaptureState()));
                revolution=new AiRevolution071(world,0,profile);civilization=new AiCivilizationEconomy(world,0);
                civilization.RestoreState(SnapshotCodec.Clone(source.civilization.CaptureState()));
            }
            public Arena Cold()=>new Arena(this);
            public void Review()
            {
                var own=world.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
                var army=own.Where(e=>e.Military&&!e.def.waterborne).ToList();
                var enemies=world.Entities.Where(e=>e.Alive&&world.AreEnemies(0,e.owner)&&world.IsVisible(0,e)).ToList();
                plan.Review(own,army,enemies,target?.position??world.OpeningTownPosition(1),true,search,water,revolution,civilization);
            }
            public void Tick(){Review();world.Tick(.1f);}
            public void SameState(Arena other)
            {
                Assert.AreEqual(JsonUtility.ToJson(plan.CaptureState()),JsonUtility.ToJson(other.plan.CaptureState()));
                Assert.AreEqual(JsonUtility.ToJson(search.CaptureState()),JsonUtility.ToJson(other.search.CaptureState()));
                Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(other.world.CaptureState()),"Cold recovery must preserve actual orders, bodies, damage and event sequence");
            }
        }

        static IEnumerable<Vector2> OriginalCandidates(Vector2 desired)
        {
            yield return (Vector2)Vector2Int.RoundToInt(desired);
            for(int distance=4;distance<=12;distance+=4)for(int direction=0;direction<8;direction++)
            {
                float angle=direction*Mathf.PI/4;
                yield return (Vector2)Vector2Int.RoundToInt(desired+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*distance);
            }
        }
        static void AssertActuallyAssembled(World world,EntityState soldier,Vector2 point)
        {
            Assert.IsTrue(soldier.Alive);Assert.AreEqual(0,soldier.garrisonedIn);
            Assert.LessOrEqual(Vector2.Distance(soldier.position,point),10.001f);
            Assert.IsTrue(world.Navigation.IsWalkable(soldier.position,soldier.def.radius));
            var path=world.Navigation.FindPath(soldier.position,point,soldier.def.radius);
            Assert.GreaterOrEqual(path.Count,2);Assert.LessOrEqual((path[path.Count-1]-point).sqrMagnitude,.0001f);
            float length=0;for(int i=1;i<path.Count;i++)length+=Vector2.Distance(path[i-1],path[i]);
            Assert.LessOrEqual(length,14.001f,"A nearby body behind a long barrier is not an assembled member");
        }

        [TestCase(2,6)][TestCase(3,10)][TestCase(4,14)]
        public void BlockedOriginalCentersUseExpandedStageAndReallyDepartAtTheRequiredCohortSize(int age,int count)
        {
            var a=new Arena("expanded",count,age);var w=a.world;
            var original=OriginalCandidates(a.desired).ToArray();Assert.AreEqual(25,original.Length);
            foreach(var point in original)Assert.IsFalse(w.Navigation.IsAreaFree(point,7,7),"Every original center is deliberately obstructed");
            a.Review();Assert.IsTrue(a.plan.CaptureState().hasStage,"A visible reachable outer center must recover normal muster");
            Assert.IsFalse(original.Contains(a.plan.RallyPoint));Assert.AreEqual(AiArmyPhase072.Muster,a.plan.Phase);
            Assert.IsEmpty(a.plan.WaveMembers,"Declaring an army is not a departure before it actually walks to the stage");
            Assert.Less((a.barracks.rally-a.plan.RallyPoint).sqrMagnitude,.0001f);
            for(int i=0;i<1800&&a.plan.Phase!=AiArmyPhase072.March;i++)
            {
                a.Review();
                if(a.plan.Phase==AiArmyPhase072.March)
                    foreach(int id in a.plan.WaveMembers)AssertActuallyAssembled(w,w.Get(id),a.plan.RallyPoint);
                w.Tick(.1f);
                foreach(var soldier in a.soldiers.Where(e=>e.Alive))Assert.IsTrue(w.Navigation.IsWalkable(soldier.position,soldier.def.radius));
            }
            Assert.AreEqual(AiArmyPhase072.March,a.plan.Phase);Assert.AreEqual(count,a.plan.WaveMembers.Length);
            Assert.IsTrue(w.Events.Any(e=>e.player==0&&e.kind=="Command"&&e.message.StartsWith("AttackMove")&&e.amount==count),"Departure is an ordinary real group order");
            var wave=new HashSet<int>(a.plan.WaveMembers);
            for(int i=0;i<1800&&!w.Events.Any(e=>e.kind=="Damage"&&e.target==a.target.id&&wave.Contains(e.entity));i++)a.Tick();
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Damage"&&e.player==0&&e.target==a.target.id&&wave.Contains(e.entity)),"The recovered cohort must actually reach and damage the declared enemy building");
        }

        [Test] public void NarrowReachableCorridorUsesCompactCenterWithoutSkippingActualArrival()
        {
            var a=new Arena("compact");var w=a.world;
            a.Review();var saved=a.plan.CaptureState();Assert.IsTrue(saved.hasStage);
            Assert.IsFalse(w.Navigation.IsAreaFree(saved.stage,7,7));Assert.IsTrue(w.Navigation.IsAreaFree(saved.stage,5,5));
            Assert.AreEqual(AiArmyPhase072.Muster,a.plan.Phase);Assert.IsEmpty(saved.wave);
            for(int i=0;i<1200&&a.plan.Phase!=AiArmyPhase072.March;i++)
            {
                a.Review();if(a.plan.Phase==AiArmyPhase072.March)
                    foreach(int id in a.plan.WaveMembers)AssertActuallyAssembled(w,w.Get(id),a.plan.RallyPoint);
                w.Tick(.1f);
            }
            Assert.AreEqual(AiArmyPhase072.March,a.plan.Phase);Assert.AreEqual(6,a.plan.WaveMembers.Length);
            Assert.AreEqual(saved.stage,a.plan.RallyPoint,"A valid compact stage is stable across subsequent reviews");
        }

        [Test] public void VisibleButDisconnectedCentersKeepTheRealArmyWaitingAndAdvanceBoundedSearch()
        {
            var a=new Arena("blocked");a.Review();var first=a.plan.CaptureState();
            Assert.IsFalse(first.hasStage);Assert.Greater(first.stageSearchCursor,0,"Failed route work resumes at a stored candidate instead of rechecking the same prefix");
            for(int i=0;i<1000;i++)
            {
                a.Tick();Assert.IsFalse(a.plan.CaptureState().hasStage);Assert.AreEqual(AiArmyPhase072.Muster,a.plan.Phase);
                Assert.IsEmpty(a.plan.WaveMembers);
                foreach(var soldier in a.soldiers)Assert.IsTrue(a.world.Navigation.IsWalkable(soldier.position,soldier.def.radius));
            }
            Assert.IsFalse(a.world.Events.Any(e=>e.player==0&&e.kind=="Command"&&e.message.StartsWith("AttackMove")));
            Assert.IsFalse(a.world.Events.Any(e=>e.player==0&&e.kind=="Damage"));
        }

        [TestCase("expanded")][TestCase("blocked")]
        public void ColdRestorePreservesExpandedMusterOrDeferredSearchAndTheSubsequentWorld(string geometry)
        {
            var a=new Arena(geometry);a.Review();Assert.AreEqual(AiArmyPhase072.Muster,a.plan.Phase);
            if(geometry=="blocked")Assert.Greater(a.plan.CaptureState().stageSearchCursor,0);
            var cold=a.Cold();a.SameState(cold);
            for(int i=0;i<300;i++){a.Tick();cold.Tick();if(i%50==0)a.SameState(cold);}
            a.SameState(cold);
            Assert.AreEqual(geometry=="blocked"?AiArmyPhase072.Muster:AiArmyPhase072.March,a.plan.Phase);
        }

        [Test] public void InvalidDeferredCandidateCannotRestoreAndLegacyMissingCursorStartsAtZero()
        {
            var a=new Arena("expanded");a.Review();var saved=a.plan.CaptureState();
            var legacy=JsonUtility.ToJson(saved);StringAssert.Contains("\"stageSearchCursor\":0,",legacy);
            legacy=legacy.Replace("\"stageSearchCursor\":0,","");
            var restored=new AiArmyPlan072(a.world,0,AiDifficultyProfile.From(a.world.Rules,"standard"));
            restored.RestoreState(JsonUtility.FromJson<AiArmyPlan072Snapshot>(legacy));
            Assert.AreEqual(0,restored.CaptureState().stageSearchCursor);
            foreach(int invalid in new[]{-1,130})
            {saved=a.plan.CaptureState();saved.stageSearchCursor=invalid;Assert.Throws<ArgumentException>(()=>restored.RestoreState(saved));}
            saved=a.plan.CaptureState();Assert.IsTrue(saved.hasStage);saved.stageSearchCursor=1;
            Assert.Throws<ArgumentException>(()=>restored.RestoreState(saved),"A selected center cannot also claim a deferred search cursor");
        }
    }
}
