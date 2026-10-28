using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Finite command drivers for declared controlled starts. No test spawns,
    // resource grants, direct damage, position changes or goal injection.
    public sealed class ClassicTactics084Tests
    {
        GuidedPlaySession current;string last="";
        static readonly string[] Ids={ClassicCivilizationTactics084.British,ClassicCivilizationTactics084.Dutch,ClassicCivilizationTactics084.French,ClassicCivilizationTactics084.Ottoman};
        [TearDown] public void ArchiveFailureAndDispose()
        {
            if(current!=null&&TestContext.CurrentContext.Result.Outcome.Status==NUnit.Framework.Interfaces.TestStatus.Failed)
            {
                string root=Environment.GetEnvironmentVariable("FRONTIER084_TACTICS_EVIDENCE");if(string.IsNullOrEmpty(root))root=Path.Combine(Path.GetTempPath(),"Frontier-tactics084");
                string dir=Path.Combine(root,"Failures",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
                // Each raw part is independent: an archive error cannot discard
                // all of the actual failed world's diagnostics.
                Action<string,Func<string>> save=(name,read)=>{try{File.WriteAllText(Path.Combine(dir,name),read());}catch(Exception e){TestContext.Error.WriteLine(name+": "+e);}};
                save("world.json",()=>JsonUtility.ToJson(current.World.CaptureState(),true));save("guided.json",()=>JsonUtility.ToJson(current.CaptureState(),true));
                save("simulation.json",()=>JsonUtility.ToJson(current.Simulation.CaptureState(),true));save("last-command.txt",()=>last+"\n"+Diagnostic());
                TestContext.Error.WriteLine("Actual failure world: "+dir);
            }
            current?.Dispose();current=null;
        }
        string Diagnostic()=>current==null?"no session":"stage="+current.Stage?.Id+" status="+current.Status+" t="+current.World.Time+" reason="+current.Reason+"\n"+
            string.Join("\n",current.World.Entities.Where(e=>e.def.kind==EntityKind.Unit).Select(e=>$"{e.id}:{e.def.id}/P{e.owner} alive={e.Alive} hp={e.hp} pos={e.position} status={e.status}"));
        int[] Army(string type="")=>current.CaptureState().soldiers.Where(id=>current.World.Get(id)?.Alive==true&&(type==""||current.World.Get(id).def.id==type)).ToArray();
        Vector2 Centre=>ClassicCivilizationTactics084.DeploymentPoint(current.Definition.Id);
        void Issue(GameCommand command)
        {last=JsonUtility.ToJson(command);var result=current.Issue(command);Assert.IsTrue(result.accepted,result.reason+" "+Diagnostic());}
        void Order(int[] actors,OrderKind order,Vector2 point,int target=0)
        {if(actors.Length==0)return;Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=actors,order=order,point=point,target=target});}
        void Steps(int count){for(int i=0;i<count&&current.Status==GuidedPlayStatus.Running;i++)Assert.IsTrue(current.Simulation.Step(),Diagnostic());}
        void Cold()
        {
            var old=current;var ws=JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(old.World.CaptureState()));
            var ss=JsonUtility.FromJson<MatchSimulationSnapshot>(JsonUtility.ToJson(old.Simulation.CaptureState()));
            var gs=JsonUtility.FromJson<GuidedPlaySnapshot>(JsonUtility.ToJson(old.CaptureState()));
            var world=World.Restore(old.World.Rules,ws);var simulation=MatchSimulation.Restore(world,ss);
            string before=JsonUtility.ToJson(world.CaptureState());int count=world.Events.Count;
            current=GuidedPlaySession.Restore(world,simulation,gs);
            Assert.AreEqual(before,JsonUtility.ToJson(world.CaptureState()),"Cold adoption must not issue, spawn, tick or evaluate a goal");
            Assert.AreEqual(count,world.Events.Count);Assert.AreEqual(old.AttemptId,current.AttemptId);Assert.AreEqual(old.Status,current.Status);old.Dispose();
        }
        void Deploy()
        {
            string id=current.Definition.Id;var c=Centre;var stations=new Dictionary<int,Vector2>();
            foreach(int soldier in Army())
            {
                var e=current.World.Get(soldier);int[] group=Army(e.def.id);int index=Array.IndexOf(group,soldier);
                float dy=id==ClassicCivilizationTactics084.British?(e.def.id=="pike"?4:-4):id==ClassicCivilizationTactics084.Ottoman?(e.def.id=="janissary"?4:-4):0;
                // A single forward rider draws the incoming cavalry while the
                // other riders keep room to deliver coherent counter-fire.
                stations[soldier]=id==ClassicCivilizationTactics084.Dutch?
                    index==0?c+new Vector2(0,3):c+new Vector2((index-group.Length*.5f)*2,-4):
                    c+new Vector2((index-(group.Length-1)*.5f)*1.8f,dy);
                Order(new[]{soldier},OrderKind.Move,stations[soldier]);
            }
            if(id==ClassicCivilizationTactics084.Ottoman)Issue(new GameCommand{player=0,kind=CommandKind.ToggleMode,actors=Army("janissary")});
            for(int i=0;i<90&&stations.Any(x=>Vector2.Distance(current.World.Get(x.Key).position,x.Value)>1)&&current.Status==GuidedPlayStatus.Running;i++)Steps(1);
            Assert.AreEqual("trait",current.Stage?.Id,"Ordinary deployment must really finish "+Diagnostic());
            foreach(int soldier in Army())Order(new[]{soldier},OrderKind.Hold,current.World.Get(soldier).position);
        }
        int dutchFocus,dutchBait,dutchWaypoint,dutchShotFloor;
        bool dutchReleased,dutchRegrouping;
        // Public dry-land waypoints form an inner-field circuit. They stay well
        // above the original town at (24,28), away from the southern map edge.
        static readonly Vector2[] DutchCircuit={new Vector2(10,76),new Vector2(14,64),new Vector2(26,60),new Vector2(38,66),
            new Vector2(39,80),new Vector2(31,90),new Vector2(18,90),new Vector2(10,82)};
        void DutchStation(int rider,Vector2 point)
        {
            var e=current.World.Get(rider);var order=e.orders.Count>0?e.orders.Peek():null;
            if(Vector2.Distance(e.position,point)<=1)
            {if(!e.hold)Order(new[]{rider},OrderKind.Hold,e.position);}
            else if(order==null||order.kind!=OrderKind.Move||Vector2.Distance(order.point,point)>.1f)
                Order(new[]{rider},OrderKind.Move,point);
        }
        void DutchContinueCircuit()
        {
            var w=current.World;var rider=w.Get(dutchBait);var order=rider.orders.Count>0?rider.orders.Peek():null;
            if(order?.kind==OrderKind.Move&&Vector2.Distance(rider.position,order.point)>1.5f)return;
            Vector2 desired=DutchCircuit[dutchWaypoint%DutchCircuit.Length];dutchWaypoint++;
            var goal=w.Navigation.NearestWalkable(desired,rider.def.radius);
            Assert.That(goal.x,Is.InRange(8f,44f),"The actual rider endpoint must retain the field inset");
            Assert.That(goal.y,Is.InRange(56f,94f),"The circuit must not send a rider around the base or into a map corner");
            Assert.IsFalse(w.IsWater(goal)||w.IsBridgeApproach(goal));
            var path=w.Navigation.FindPath(rider.position,goal,rider.def.radius);Assert.IsNotEmpty(path,"A real land route must connect consecutive circuit points");
            Assert.IsTrue(path.All(p=>p.x>=7&&p.x<=46&&p.y>=54&&p.y<=96),"Validate the rider's actual route, not just a proposed centre");
            Order(new[]{dutchBait},OrderKind.Move,goal);
        }
        bool DutchDecision(EntityState[] enemies)
        {
            var w=current.World;var riders=Army("carabineer");
            if(!riders.Contains(dutchBait))
            {dutchBait=riders.OrderByDescending(id=>w.Get(id).hp).ThenBy(id=>id).First();dutchReleased=false;dutchShotFloor=w.Events.Count;dutchWaypoint=0;}
            if(enemies.Length==0)
            {
                if(!dutchRegrouping){dutchRegrouping=true;dutchReleased=false;dutchShotFloor=w.Events.Count;dutchWaypoint=0;}
                dutchFocus=0;
                // A declared public assembly point reacquires the next wave;
                // the driver never walks to a hidden enemy binding or position.
                DutchStation(dutchBait,Centre+new Vector2(0,3));int slot=0;
                foreach(int id in riders.Where(id=>id!=dutchBait))DutchStation(id,Centre+new Vector2((slot++-(riders.Length-2)*.5f)*2,-4));
                return true;
            }
            dutchRegrouping=false;
            if(!dutchReleased)
            {
                // Hold is an accepted public command. Wait for this forward
                // rider's real release before moving; later circuit legs begin
                // after the real impact and retain the strict post-hit 8m proof.
                var shot=w.Events.Skip(dutchShotFloor).LastOrDefault(e=>e.kind=="Fire"&&e.player==0&&e.entity==dutchBait&&e.message=="Ranged"&&enemies.Any(t=>t.id==e.target));
                if(shot==null)return true;
                dutchReleased=true;dutchFocus=shot.target;
            }
            DutchContinueCircuit();
            var focus=enemies.FirstOrDefault(e=>e.id==dutchFocus);
            if(focus==null)
            {
                // Keep one currently visible living target until it dies or
                // sight is genuinely lost. Do not spread 45-point shots over
                // four healthy cavalry by re-sorting around the original camp.
                focus=enemies.OrderBy(e=>e.hp).ThenBy(e=>Vector2.Distance(e.position,w.Get(dutchBait).position)).ThenBy(e=>e.id).First();
                dutchFocus=focus.id;
            }
            var shooters=riders.Where(id=>id!=dutchBait).ToArray();
            if(shooters.Length>0&&!shooters.All(id=>w.Get(id).orders.Count>0&&w.Get(id).orders.Peek().kind==OrderKind.Attack&&w.Get(id).orders.Peek().target==focus.id))
                Order(shooters,OrderKind.Attack,focus.position,focus.id);
            return true;
        }
        void FightDecision()
        {
            var w=current.World;string id=current.Definition.Id;
            var enemies=w.ScenarioEntities("wave-one").Concat(w.ScenarioEntities("wave-two")).Select(w.Get)
                .Where(e=>e?.Alive==true&&w.IsVisible(0,e)).OrderBy(e=>(e.position-Centre).sqrMagnitude).ToArray();
            if(id==ClassicCivilizationTactics084.Dutch&&DutchDecision(enemies))return;
            foreach(string type in Army().Select(x=>w.Get(x).def.id).Distinct())
            {
                int[] actors=Army(type);if(actors.Length==0)continue;
                // British pikes keep the forward line until cavalry arrive.
                if(id==ClassicCivilizationTactics084.British&&type=="pike"&&!enemies.Any(e=>e.def.id=="hussar"))continue;
                var candidates=enemies.AsEnumerable();if(type=="janissary")candidates=candidates.OrderBy(e=>e.def.id=="hussar"?0:1);
                if(type=="abus")candidates=candidates.OrderBy(e=>e.def.id=="musketeer"?0:1);
                var target=candidates.FirstOrDefault();if(target==null)continue;
                // Retain an identical target order; resetting it every review
                // could cancel the weapon windup and falsely stall combat.
                if(actors.All(a=>w.Get(a).orders.Count>0&&w.Get(a).orders.Peek().kind==OrderKind.Attack&&w.Get(a).orders.Peek().target==target.id))continue;
                // Far-ranging longbow shots need shared sight from the front.
                // Every selected enemy has passed the actual visibility guard.
                Order(actors,OrderKind.Attack,target.position,target.id);
            }
        }
        void AssertDutchEnemyEngagement099()
        {
            var w=current.World;var riders=w.ScenarioEntities(ClassicCivilizationTactics084.ArmyGroup);
            string[] groups={"wave-one","wave-two"},triggers={ClassicCivilizationTactics084.Wave1Trigger,ClassicCivilizationTactics084.Wave2Trigger};
            float[] times={ClassicCivilizationTactics084.Wave1Seconds,ClassicCivilizationTactics084.Wave2Seconds};
            for(int wave=0;wave<groups.Length;wave++)
            {
                int[] ids=w.ScenarioEntities(groups[wave]);Assert.AreEqual(4,ids.Length,"Each Dutch wave must contain its four real cavalry");
                Assert.IsTrue(w.ScenarioState.triggerStates.Any(t=>t.id==triggers[wave]&&t.fires==1&&t.lastError==""));
                var fired=w.Events.Single(e=>e.kind=="ScenarioTriggerFired"&&e.message==triggers[wave]+" · execution 1");
                Assert.That(Mathf.Abs(fired.time-times[wave]),Is.LessThanOrEqualTo(.11f));
                Assert.IsTrue(w.Events.Take(w.Events.IndexOf(fired)).Any(e=>e.kind=="Command"&&e.player==1&&e.entity==ids[0]&&
                    e.message=="AttackMove ×4"&&e.amount==4&&e.target==0&&e.time==fired.time),"The public AttackMove must actually be accepted for each wave");
                foreach(int id in ids)
                {
                    var enemy=w.Get(id);Assert.AreEqual(1,enemy.owner);Assert.AreEqual("hussar",enemy.def.id);Assert.IsFalse(enemy.Alive);
                    Assert.IsTrue(enemy.orders.Any(o=>o.kind==OrderKind.AttackMove)&&riders.Contains(enemy.attackTarget),"Each original enemy must retain its actual acquired Ruyter and attack-move intent");
                    var spawned=w.Events.Single(e=>e.kind=="Spawn"&&e.entity==id&&e.player==1);
                    var lost=w.Events.Single(e=>e.kind=="Death"&&e.entity==id&&e.player==1&&e.target==0);
                    Assert.GreaterOrEqual(Vector2.Distance(spawned.position,lost.position),8,"Each enemy must physically leave its spawn before being defeated");
                    Assert.IsTrue(w.Events.Any(e=>e.kind=="Damage"&&e.player==0&&e.target==id&&e.amount>0));
                }
                int pursuits=ids.Count(id=>{var enemy=w.Get(id);return Vector2.Distance(enemy.position,Centre)>=6&&
                    enemy.orders.Any(o=>o.kind==OrderKind.AttackMove&&Vector2.Distance(enemy.position,o.point)>=6);});
                Assert.Greater(pursuits,0,"Each wave must pursue a real acquired rider outside the public assembly goal, not merely walk to that goal");
                TestContext.WriteLine("ActualDutchEnemyEngagement099 wave="+groups[wave]+" acceptedAttackMove=4 acquired="+ids.Length+" outsideGoalPursuits="+pursuits);
            }
        }
        void Solve(bool cold)
        {
            Deploy();if(cold)Cold();bool waveSaved=false,traitSaved=false,shotSaved=false;
            for(int second=0;second<400&&current.Status==GuidedPlayStatus.Running;second++)
            {
                FightDecision();
                for(int tick=0;tick<10&&current.Status==GuidedPlayStatus.Running;tick++)
                {
                    Steps(1);
                    if(cold&&!shotSaved&&current.World.Projectiles.Any(p=>Army().Contains(p.source))){Cold();shotSaved=true;}
                    if(cold&&!waveSaved&&current.World.ScenarioEntities("wave-two").Length==ClassicCivilizationTactics084.Wave2Count(current.Definition.Id)){Cold();waveSaved=true;}
                    if(cold&&!traitSaved&&current.Stage?.Id=="clear"){Cold();traitSaved=true;}
                    // This one rider is continuously steered at five ordinary
                    // decisions per simulated second. Other countries retain
                    // their already-passing one-second command cadence.
                    if(current.Definition.Id==ClassicCivilizationTactics084.Dutch&&tick%2==1&&current.Status==GuidedPlayStatus.Running)FightDecision();
                }
            }
            Assert.AreEqual(GuidedPlayStatus.Completed,current.Status,Diagnostic());
            Assert.IsNull(current.Stage);Assert.AreEqual(0,current.World.Winner);Assert.IsTrue(current.World.ScenarioState.scenarioVictory);
            Assert.AreEqual(4+ClassicCivilizationTactics084.Wave2Count(current.Definition.Id),current.World.ScenarioEntities("wave-one").Concat(current.World.ScenarioEntities("wave-two")).Count(x=>!current.World.Get(x).Alive));
            if(current.Definition.Id==ClassicCivilizationTactics084.Dutch)AssertDutchEnemyEngagement099();
            else Assert.IsTrue(current.World.Events.Any(e=>e.kind=="Fire"&&e.player==1),"Enemy waves must actually fight");
            Assert.IsFalse(current.World.Events.Any(e=>e.kind=="GuidedScenarioGrant"||e.kind=="Payment"||e.kind=="ResearchComplete"));
            if(cold){Assert.IsTrue(waveSaved);Assert.IsTrue(traitSaved);if(current.Definition.Id!=ClassicCivilizationTactics084.French)Assert.IsTrue(shotSaved,"Cold coverage must include an actual projectile in flight");Cold();}
        }
        [TestCase(ClassicCivilizationTactics084.British)][TestCase(ClassicCivilizationTactics084.Dutch)]
        [TestCase(ClassicCivilizationTactics084.French)][TestCase(ClassicCivilizationTactics084.Ottoman)]
        public void ControlledStartPublicCommandsActualCombatBothWavesAndColdProofs(string id)
        {
            dutchFocus=dutchBait=dutchWaypoint=dutchShotFloor=0;dutchReleased=dutchRegrouping=false;var rules=Catalog.Load();current=GuidedPlaySession.Start(rules,id);
            Assert.AreEqual(GuidedPlayKind.TacticalEncounter084,current.Definition.Kind);Assert.AreEqual(0,current.World.Time);
            Assert.AreEqual(ClassicCivilizationTactics084.Civilization(id),current.World.Players[0].civilization);
            Assert.AreEqual(ClassicCivilizationTactics084.StartingAge(id),current.World.Players[0].age);
            Assert.AreEqual(new Cost(),current.World.Players[0].resources);Assert.IsEmpty(current.World.Players[0].technologies);
            Assert.AreEqual(ClassicCivilizationTactics084.ArmyCount(id),Army().Length);Assert.AreEqual(0,current.CaptureState().workers.Length);
            Assert.IsEmpty(current.Simulation.CaptureState().controllers);Assert.IsEmpty(current.World.ScenarioEntities("wave-two"));
            Assert.AreEqual(0,current.SuggestedTarget,"Do not reveal a hidden initial enemy ID");Assert.AreEqual(0,current.Checklist[0].Progress);
            Assert.AreEqual("",ScenarioCompiler.Validate(current.World.ScenarioDocument,rules,2));
            Cold();Solve(true);
            var saved=current.CaptureState();var ws=JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(current.World.CaptureState()));
            foreach(var ev in ws.events.Where(e=>e.kind=="TacticShot084"&&e.player==0))ev.kind="RemovedTacticShot084";
            var broken=World.Restore(rules,ws);var simulation=MatchSimulation.Restore(broken,current.Simulation.CaptureState());
            Assert.Throws<ArgumentException>(()=>GuidedPlaySession.Restore(broken,simulation,saved),"Completed trait needs its genuine firing geometry/combat chain");
            string evidence=Environment.GetEnvironmentVariable("FRONTIER084_TACTICS_EVIDENCE");
            if(!string.IsNullOrEmpty(evidence))
            {
                string directory=Path.Combine(evidence,"Completed",id);Directory.CreateDirectory(directory);
                foreach(string name in new[]{"world.json","guided.json","simulation.json"})Assert.IsFalse(File.Exists(Path.Combine(directory,name)),"Preserve the original completed state");
                File.WriteAllText(Path.Combine(directory,"world.json"),JsonUtility.ToJson(current.World.CaptureState(),true));
                File.WriteAllText(Path.Combine(directory,"guided.json"),JsonUtility.ToJson(current.CaptureState(),true));
                File.WriteAllText(Path.Combine(directory,"simulation.json"),JsonUtility.ToJson(current.Simulation.CaptureState(),true));
            }
            TestContext.WriteLine("ActualTactics084 id="+id+" status="+current.Status+" time="+current.World.Time+" survivors="+Army().Length+" enemyDeaths="+current.World.Events.Count(e=>e.kind=="Death"&&e.player==1)+" actualManeuvers="+current.World.Events.Count(e=>e.kind=="TacticManeuver084")+" actualAreaHits="+current.World.Events.Count(e=>e.kind=="MeleeAreaHit"&&e.player==0));
        }
        [Test] public void LosingTheRealBritishFrontLineFailsAndRetryRestoresIt()
        {
            current=GuidedPlaySession.Start(Catalog.Load(),ClassicCivilizationTactics084.British);
            foreach(int id in Army("longbow"))Order(new[]{id},OrderKind.Hold,current.World.Get(id).position);
            Order(Army("pike"),OrderKind.Move,Centre+new Vector2(0,22));
            for(int second=0;second<120&&current.Status==GuidedPlayStatus.Running;second++)Steps(10);
            Assert.AreEqual(GuidedPlayStatus.Failed,current.Status,Diagnostic());
            Assert.IsTrue(current.World.Events.Any(e=>e.kind=="Death"&&e.player==0&&current.World.Get(e.entity)?.def.id=="pike"&&e.target==1));
            Assert.IsTrue(current.World.Events.Any(e=>e.kind=="Damage"&&e.player==1&&e.amount>0));Cold();
            var next=current.Retry();current.Dispose();current=next;
            Assert.AreEqual(3,Army("pike").Length);Assert.AreEqual(4,Army("longbow").Length);Assert.AreEqual(0,current.World.Time);
        }
        [Test] public void PublicResignFailsColdRetryRestoresOnlyTheDeclaredNewEncounter()
        {
            foreach(string id in Ids)
            {
                current=GuidedPlaySession.Start(Catalog.Load(),id);string attempt=current.AttemptId;
                Issue(new GameCommand{player=0,kind=CommandKind.Resign});Assert.AreEqual(GuidedPlayStatus.Failed,current.Status);Cold();
                var next=current.Retry();current.Dispose();current=next;
                Assert.AreNotEqual(attempt,current.AttemptId);Assert.AreEqual(GuidedPlayStatus.Running,current.Status);
                Assert.AreEqual(0,current.World.Time);Assert.AreEqual(0,current.CaptureState().stageIndex);Assert.IsEmpty(current.World.ScenarioEntities("wave-two"));
                Assert.AreEqual(ClassicCivilizationTactics084.ArmyCount(id),Army().Length);current.Dispose();current=null;
            }
        }
    }
}
