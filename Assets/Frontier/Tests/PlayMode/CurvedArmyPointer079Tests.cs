using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Real InputSystem events through RtsInput;
    // spawned/fully-revealed command fixtures, not paid/native OS acceptance.
    public sealed class CurvedArmyPointer079Tests : NativeInputFixture
    {
        const float Dt=.1f;
        const int LegBudget=1200; // Proposed120sim-s per leg, not a measured result.
        readonly string[] kinds={"musketeer","musketeer","musketeer","musketeer","pike","pike","pike","pike","hussar","hussar","cannon","cannon"};

        [UnityTest] public IEnumerator SouthBridgePointerHoldReverseAndCross()=>MoveCase(0);
        [UnityTest] public IEnumerator NorthBridgePointerHoldReverseAndCross()=>MoveCase(1);
        [UnityTest] public IEnumerator SouthBridgePointerBuildingThenMovingPerson()=>AttackCase(0);
        [UnityTest] public IEnumerator NorthBridgePointerBuildingThenMovingPerson()=>AttackCase(1);

        IEnumerator Configure(int index,float zoom)
        {
            game.SelectedMap=MapTopology.Winding;game.MatchSeed=42;game.StartMatch();game.enabled=false;
            Assert.AreEqual(MapTopology.Winding,game.World.MapId);Assert.AreEqual(42,game.World.Seed);
            Assert.AreEqual(2,game.World.Layout.Bridges.Length);Assert.That(index,Is.InRange(0,1));
            // Keep own original town; replace the enemy town during setup with
            // a far-away inert anchor so idle arrivals do not auto-pursue the
            // original north-bank town while slower cannon are still travelling.
            // This controlled spawned arena is not opening/enemy-balance evidence.
            foreach(var entity in game.World.Entities.Where(e=>e.owner!=0||e.def.id!="town").ToArray())game.World.Remove(entity);
            var townDef=game.World.Rules.Entity("town");var enemyAnchor=new Vector2(114,14);
            Assert.IsTrue(game.World.Navigation.IsAreaFree(enemyAnchor,townDef.width,townDef.depth),"Inert elimination anchor needs actual legal land");
            var enemyTown=SpawnHere("town",1,enemyAnchor);enemyTown.hp=100000;Mute(enemyTown);
            game.Controls.RestoreCamera(game.World.Layout.Bridges[index].Center,zoom);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f)});
            float frozen=game.World.Time;
            yield return new WaitForSecondsRealtime(.25f);yield return Ready();
            Assert.AreEqual(frozen,game.World.Time,"Only manual World.Tick advances simulation; RtsInput stays active");
            Assert.IsTrue(game.CanSubmitOrders);Reveal();
        }
        void Reveal()=>game.World.Fog.Reveal(0,game.World.Layout.Bridges[0].Center,128);
        static void Mute(EntityState e)
        {
            e.def=JsonUtility.FromJson<EntityDef>(JsonUtility.ToJson(e.def));
            e.def.damage=e.def.meleeDamage=e.def.siegeDamage=0;
        }
        EntityState[] Army(BridgeSpan bridge)
        {
            var w=game.World;float spacing=Mathf.Max(1.8f,kinds.Max(k=>w.Rules.Entity(k).radius*4+.3f));
            var slots=w.Navigation.FormationSlots(bridge.Point(-bridge.HalfLength-bridge.RampLength-14,0),kinds.Length,spacing);
            Assert.AreEqual(12,slots.Count,"Authored west assembly must fit12legal spawn slots");
            var army=kinds.Select((kind,i)=>SpawnHere(kind,0,slots[i])).ToArray();
            foreach(var a in army)
            {
                Assert.IsTrue(w.Navigation.IsWalkable(a.position,a.def.radius));
                Assert.Less(bridge.Local(a.position).x,-bridge.HalfLength-bridge.RampLength-3,"Every initial body is beyond the near ramp mouth");
            }
            return army;
        }
        IEnumerator Box(EntityState[] army,float zoom,Trace trace)
        {
            game.Controls.RestoreCamera(army.Aggregate(Vector2.zero,(sum,a)=>sum+a.position)/army.Length,zoom);Reveal();yield return Ready();
            var pixels=army.Select(a=>ScreenPoint(a.position,game.World.SurfaceHeight(a.position)+1)).ToArray();
            var low=new Vector2(pixels.Min(p=>p.x)-12,pixels.Min(p=>p.y)-12);
            var high=new Vector2(pixels.Max(p=>p.x)+12,pixels.Max(p=>p.y)+12);
            AssertWorldPoint(low);AssertWorldPoint(high);float before=game.World.Time;
            trace.Pointer("box-start",low,Vector2.zero);trace.Pointer("box-end",high,Vector2.zero);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=low}.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=high}.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=high});yield return Ready();
            Assert.AreEqual(before,game.World.Time);CollectionAssert.AreEquivalent(army.Select(a=>a.id),game.Controls.Selected);
        }
        IEnumerator PointerMove(EntityState[] army,Vector2 requested,int side,Trace trace)
        {
            var w=game.World;game.Controls.RestoreCamera(requested,26);Reveal();yield return Ready();
            var pixel=ScreenPoint(requested,w.SurfaceHeight(requested));AssertWorldPoint(pixel);
            var ground=game.Controls.GroundPoint(pixel);Assert.Less(Vector2.Distance(ground,requested),.025f);
            trace.Pointer("right-move",pixel,ground);float before=w.Time;
            yield return Click(pixel,MouseButton.Right);Assert.AreEqual(before,w.Time);
            CollectionAssert.AreEquivalent(army.Select(a=>a.id),game.Controls.Selected);
            float spacing=Mathf.Max(1.8f,army.Max(a=>a.def.radius*4+.3f));
            var expected=w.Navigation.FormationSlots(ground,army.Length,spacing);
            Assert.AreEqual(army.Length,expected.Count);
            for(int i=0;i<game.Controls.Selected.Count;i++)
            {
                var a=w.Get(game.Controls.Selected[i]);Assert.AreEqual(1,a.orders.Count);var order=a.orders.Peek();
                Assert.AreEqual(OrderKind.Move,order.kind);Assert.AreEqual(0,order.target);
                Assert.Less(Vector2.Distance(expected[i],order.point),.0001f,"Actual assigned slot, not raw click");
                Assert.IsTrue(w.Navigation.IsWalkable(order.point,a.def.radius));
                Assert.Greater(side*trace.bridge.Local(order.point).x,trace.bridge.HalfLength+trace.bridge.RampLength+3);
                trace.Assigned(a,order);trace.State("move-command",a);
            }
        }
        IEnumerator PointerAttack(EntityState[] army,EntityState target,Trace trace,string label)
        {
            var w=game.World;game.Controls.RestoreCamera(target.position,16);Reveal();yield return Ready();
            Assert.IsTrue(w.IsVisible(0,target));float height=w.SurfaceHeight(target.position)+(target.def.kind==EntityKind.Unit?1:0);
            var pixel=ScreenPoint(target.position,height);AssertWorldPoint(pixel);trace.Pointer(label,pixel,target.position);float before=w.Time;
            yield return Click(pixel,MouseButton.Right);Assert.AreEqual(before,w.Time);
            CollectionAssert.AreEquivalent(army.Select(a=>a.id),game.Controls.Selected);
            foreach(var a in army)
            {Assert.AreEqual(1,a.orders.Count);Assert.AreEqual(OrderKind.Attack,a.orders.Peek().kind);Assert.AreEqual(target.id,a.orders.Peek().target);trace.State(label,a);}
        }
        void Tick(Trace trace,string phase)
        {
            var w=game.World;Assert.IsFalse(w.IsMatchEnded);Reveal();w.Tick(Dt);Reveal();
            foreach(var a in trace.actors)
            {
                Assert.IsTrue(a.Alive,$"{phase}: actor {a.id} alive");Assert.IsTrue(w.Navigation.IsWalkable(a.position,a.def.radius),$"{phase}: static body clearance {a.id}");
                Assert.IsTrue(w.Navigation.ClearLine(a.previousPosition,a.position,a.def.radius),$"{phase}: swept static clearance {a.id}");
                float distance=Vector2.Distance(a.previousPosition,a.position);
                Assert.LessOrEqual(distance,w.Speed(a)*Dt+.0001f,$"{phase}: speed budget {a.id}");
                Assert.LessOrEqual(Mathf.Abs(w.SurfaceHeight(a.position)-w.SurfaceHeight(a.previousPosition)),distance*.4f+.0002f,$"{phase}: continuous ramp height {a.id}");
                if(w.IsWater(a.position))Assert.IsTrue(w.IsBridge(a.position),$"{phase}: land body in open water {a.id}");
                if(w.IsWater(a.position)&&trace.bridge.Contains(a.position))trace.visited.Add(a.id);
                trace.State(phase,a);
            }
            foreach(var target in trace.targets)if(target.def.kind==EntityKind.Unit)
            {Assert.IsTrue(w.Navigation.IsWalkable(target.position,target.def.radius));Assert.IsTrue(w.Navigation.ClearLine(target.previousPosition,target.position,target.def.radius));Assert.IsFalse(w.IsWater(target.position),"Scripted person stays on actual land");trace.State(phase,target);}
        }
        IEnumerator Arrive(Trace trace,string phase,int side)
        {
            for(int step=0;step<LegBudget;step++)
            {
                Tick(trace,phase);
                if(trace.actors.All(a=>a.orders.Count==0&&Vector2.Distance(a.position,trace.effective[a.id])<.1f))break;
                if(step%20==19)yield return null;
            }
            foreach(var a in trace.actors)
            {
                Assert.IsEmpty(a.orders,$"{phase}: fixed120s budget exhausted; see untouched trace");
                Assert.Less(Vector2.Distance(a.position,trace.effective[a.id]),.1f,$"{phase}: actual resolved goal");
                Assert.Greater(side*trace.bridge.Local(a.position).x,trace.bridge.HalfLength+trace.bridge.RampLength+3,$"{phase}: complete ramp mouth crossed");
            }
        }
        IEnumerator MoveCase(int index)
        {
            yield return Configure(index,26);var w=game.World;var bridge=w.Layout.Bridges[index];var army=Army(bridge);
            using(var trace=new Trace(game,bridge,index,army))
            {
                yield return Box(army,26,trace);
                yield return PointerMove(army,bridge.Point(bridge.HalfLength+bridge.RampLength+14,0),1,trace);
                bool interrupted=false;
                for(int step=0;step<LegBudget;step++)
                {
                    Tick(trace,"outbound-before-hold");
                    interrupted=army.Any(a=>w.IsWater(a.position)&&bridge.Contains(a.position)&&Vector2.Distance(a.previousPosition,a.position)>.02f&&a.orders.Count>0);
                    if(interrupted)break;if(step%20==19)yield return null;
                }
                Assert.IsTrue(interrupted,"Hold must interrupt a genuinely moving body on this wet deck, not an already-arrived army");
                var positions=army.ToDictionary(a=>a.id,a=>a.position);var deadlines=army.ToDictionary(a=>a.id,a=>a.attackReadyAt);
                var hold=game.Settings.Bindings.KeyFor(PlayerAction.Hold);Assert.AreNotEqual(Key.None,hold);float before=w.Time;
                trace.Mark("hold-key "+hold);yield return Press(hold);Assert.AreEqual(before,w.Time);
                CollectionAssert.AreEquivalent(army.Select(a=>a.id),game.Controls.Selected);
                foreach(var a in army)
                {
                    Assert.IsTrue(a.hold);Assert.IsEmpty(a.orders);Assert.IsEmpty(a.path);Assert.AreEqual(0,a.pathIndex);Assert.AreEqual(-1,a.pathVersion);
                    Assert.AreEqual(w.Time,a.pathRetryAt);Assert.Less(a.fireAt,0);Assert.AreEqual(0,a.attackTarget);Assert.AreEqual(deadlines[a.id],a.attackReadyAt);
                    trace.State("hold-command",a);
                }
                for(int step=0;step<10;step++)
                {Tick(trace,"held-1s");foreach(var a in army)Assert.LessOrEqual(Vector2.Distance(positions[a.id],a.position),.0001f);}
                yield return PointerMove(army,bridge.Point(-bridge.HalfLength-bridge.RampLength-14,0),-1,trace);
                yield return Arrive(trace,"reverse-west",-1);
                trace.visited.Clear();yield return PointerMove(army,bridge.Point(bridge.HalfLength+bridge.RampLength+14,0),1,trace);
                yield return Arrive(trace,"full-cross-east",1);
                CollectionAssert.AreEquivalent(army.Select(a=>a.id),trace.visited,"All12bodies actually visit the requested wet bridge deck on the final full crossing");
                trace.passed=true;
            }
        }
        bool BattleSites(Trace trace,out Vector2 building,out Vector2 a,out Vector2 b)
        {
            var w=game.World;var span=trace.bridge;var def=w.Rules.Entity("barracks");float radius=w.Rules.Entity("pike").radius;
            foreach(float across in new[]{11f,9,7})foreach(float along in new[]{-8f,-12,-16})
            {
                building=span.Point(span.HalfLength+span.RampLength+across,along);
                a=building+span.Direction*6+new Vector2(-span.Direction.y,span.Direction.x)*4;
                b=building+span.Direction*6+new Vector2(-span.Direction.y,span.Direction.x)*16;
                if(!w.Navigation.IsAreaFree(building,def.width,def.depth)||span.FootprintOverlaps(building,def.width,def.depth))continue;
                var buildingPoint=building;var personPoint=a;
                if(trace.actors.Any(unit=>Vector2.Distance(unit.position,buildingPoint)<5||Vector2.Distance(unit.position,personPoint)<unit.def.radius+radius+.3f))continue;
                if(!w.Navigation.IsWalkable(a,radius)||!w.Navigation.IsWalkable(b,radius)||!w.Navigation.ClearLine(a,b,radius)||w.IsWater(a)||w.IsWater(b))continue;
                if(span.Local(a).x<=span.HalfLength+span.RampLength+3||span.Local(b).x<=span.HalfLength+span.RampLength+3)continue;
                trace.Mark($"battle-site across={across:R} along={along:R} building={building} movingA={a} movingB={b}");return true;
            }
            building=a=b=default;return false;
        }
        IEnumerator AttackCase(int index)
        {
            yield return Configure(index,26);var w=game.World;var bridge=w.Layout.Bridges[index];var army=Army(bridge);
            using(var trace=new Trace(game,bridge,index,army))
            {
                yield return Box(army,26,trace);
                yield return PointerMove(army,bridge.Point(bridge.HalfLength+bridge.RampLength+14,7),1,trace);
                yield return Arrive(trace,"attack-assembly-cross",1);
                CollectionAssert.AreEquivalent(army.Select(a=>a.id),trace.visited,"All12cross this bridge before combat targets are introduced");
                Assert.IsTrue(BattleSites(trace,out var site,out var movingA,out var movingB),"Deterministic actual API preflight must find a same-bank battle arena; no topology edits/teleports");
                var building=SpawnHere("barracks",1,site);building.hp=100000;Mute(building);
                var person=SpawnHere("pike",1,movingA);person.hp=100000;Mute(person);
                Assert.IsTrue(w.Navigation.ClearLine(movingA,movingB,person.def.radius),"Recheck actual path after the building obstacle exists");
                trace.targets.Add(building);trace.targets.Add(person);Reveal();yield return Ready();
                int attackStart=w.Events.Count;yield return PointerAttack(army,building,trace,"right-building");
                bool AllDamaged(int target,int start)=>army.All(actor=>w.Events.Skip(start).Any(ev=>ev.kind=="Damage"&&ev.entity==actor.id&&ev.target==target&&ev.amount>0));
                for(int step=0;step<LegBudget&&!AllDamaged(building.id,attackStart);step++)
                {Tick(trace,"attack-building");if(step%20==19)yield return null;}
                foreach(var actor in army)
                {
                    Assert.IsTrue(w.Events.Skip(attackStart).Any(ev=>ev.kind=="Fire"&&ev.entity==actor.id&&ev.target==building.id));
                    Assert.IsTrue(w.Events.Skip(attackStart).Any(ev=>ev.kind=="Damage"&&ev.entity==actor.id&&ev.target==building.id&&ev.amount>0));
                    Assert.IsFalse(w.Events.Skip(attackStart).Any(ev=>ev.kind=="Fire"&&ev.entity==actor.id&&ev.target==person.id),"Visible unselected person must not replace explicit building target");
                }
                for(int step=0;step<100&&!army.Any(actor=>actor.def.id=="cannon"&&actor.fireAt>w.Time&&actor.attackTarget==building.id);step++)Tick(trace,"wait-cannon-pending");
                Assert.IsTrue(army.Any(actor=>actor.def.id=="cannon"&&actor.fireAt>w.Time&&actor.attackTarget==building.id),"Retarget while an actual shot at this building is pending, not only while idle");
                foreach(var actor in army)
                {
                    Assert.IsTrue(actor.orders.Count>0&&actor.orders.Peek().kind==OrderKind.Attack&&actor.orders.Peek().target==building.id,"Explicit building target persists through the whole pre-retarget phase");
                    Assert.IsFalse(w.Events.Skip(attackStart).Any(ev=>ev.kind=="Fire"&&ev.entity==actor.id&&ev.target==person.id),"No unselected-person Fire during the cannon-preparation wait either");
                }
                var deadlines=army.ToDictionary(actor=>actor.id,actor=>actor.attackReadyAt);
                int switched=w.Events.Count;trace.Mark("retarget-start; live-old-projectiles="+w.Projectiles.Count(p=>p.target==building.id));
                yield return PointerAttack(army,person,trace,"right-moving-person");
                foreach(var actor in army)
                {Assert.Less(actor.fireAt,0);Assert.AreEqual(0,actor.attackTarget);Assert.AreEqual(deadlines[actor.id],actor.attackReadyAt,"Target cancellation preserves reload deadline");}
                float targetDistance=0;bool movingDamage=false;int leg=0;
                for(int step=0;step<LegBudget;step++)
                {
                    // Enemy controller only: public real Move legs, no direct state
                    // motion or friendly Issue/Submit calls masquerading as input.
                    if(person.orders.Count==0)
                    {
                        var result=w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{person.id},order=OrderKind.Move,point=(leg++%2==0)?movingB:movingA});
                        Assert.IsTrue(result.accepted,result.reason);trace.State("scripted-enemy-move",person);
                    }
                    var before=person.position;int eventStart=w.Events.Count;Tick(trace,"attack-moving-person");
                    float moved=Vector2.Distance(before,person.position);targetDistance+=moved;
                    if(moved>.001f&&w.Events.Skip(eventStart).Any(ev=>ev.kind=="Damage"&&ev.target==person.id&&ev.amount>0&&army.Any(actor=>actor.id==ev.entity)))movingDamage=true;
                    Assert.IsFalse(w.Events.Skip(switched).Any(ev=>ev.kind=="Fire"&&ev.target==building.id&&army.Any(actor=>actor.id==ev.entity)),"No new Fire at abandoned building; old projectiles may still Damage it");
                    if(AllDamaged(person.id,switched)&&targetDistance>=8&&movingDamage)break;if(step%20==19)yield return null;
                }
                foreach(var actor in army)
                {
                    Assert.IsTrue(w.Events.Skip(switched).Any(ev=>ev.kind=="Fire"&&ev.entity==actor.id&&ev.target==person.id));
                    Assert.IsTrue(w.Events.Skip(switched).Any(ev=>ev.kind=="Damage"&&ev.entity==actor.id&&ev.target==person.id&&ev.amount>0));
                }
                Assert.GreaterOrEqual(targetDistance,8);Assert.IsTrue(movingDamage,"At least one positive Damage occurs on a tick of genuine target movement");
                trace.Mark($"target cumulative distance={targetDistance:R}; movingDamage={movingDamage}");trace.passed=true;
            }
        }
        sealed class Trace : IDisposable
        {
            public readonly GameController game;public readonly BridgeSpan bridge;public readonly EntityState[] actors;
            public readonly List<EntityState> targets=new List<EntityState>();public readonly HashSet<int> visited=new HashSet<int>();
            public readonly Dictionary<int,Vector2> assigned=new Dictionary<int,Vector2>(),effective=new Dictionary<int,Vector2>();
            readonly Dictionary<int,UnitOrder> moveOrders=new Dictionary<int,UnitOrder>();
            public bool passed;readonly StreamWriter rows,commands;readonly string directory;readonly int bridgeIndex;
            static string F(float n)=>n.ToString("R",CultureInfo.InvariantCulture);
            static string C(string s)=>"\""+(s??"").Replace("\"","\"\"")+"\"";
            public Trace(GameController game,BridgeSpan bridge,int index,EntityState[] actors)
            {
                this.game=game;this.bridge=bridge;bridgeIndex=index;this.actors=actors;
                var root=Environment.GetEnvironmentVariable("FRONTIER079_OUTPUT");Assert.IsFalse(string.IsNullOrEmpty(root),"Fresh external output root is required");
                var full=Path.GetFullPath(root);var project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
                Assert.IsFalse(full==project||full.StartsWith(project+Path.DirectorySeparatorChar,StringComparison.Ordinal),"Trace must remain outside repository");
                directory=Path.Combine(full,TestContext.CurrentContext.Test.Name);Assert.IsFalse(Directory.Exists(directory),"Original evidence must never be overwritten");Directory.CreateDirectory(directory);
                rows=new StreamWriter(new FileStream(Path.Combine(directory,"ticks.csv"),FileMode.CreateNew));commands=new StreamWriter(new FileStream(Path.Combine(directory,"input-and-markers.log"),FileMode.CreateNew));
                rows.WriteLine("time,phase,id,kind,previousX,previousZ,x,z,radius,navVersion,assignedX,assignedZ,hasResolved,resolvedX,resolvedZ,effectiveX,effectiveZ,order,target,hold,pathIndex,pathCount,pathGoalX,pathGoalZ,status,attackReadyAt,fireAt,deployUntil,isDeployed,requestedBridgeLocalAcross,requestedBridgeLocalAlong");
                Mark($"appVersion={Application.version}; unity={Application.unityVersion}; buildGUIDActual={Application.buildGUID}; map={game.World.MapId}; seed={game.World.Seed}; bridgeIndex={index}; center={bridge.Center}; direction={bridge.Direction}; halfLength={F(bridge.HalfLength)}; halfWidth={F(bridge.HalfWidth)}; rampLength={F(bridge.RampLength)}; expectedRunnerFP={Environment.GetEnvironmentVariable("FRONTIER079_EXPECTED_FP")}; fixture=spawned/revealed/manual-World-ticks/real-InputSystem");
            }
            public void Assigned(EntityState actor,UnitOrder order){moveOrders[actor.id]=order;assigned[actor.id]=order.point;effective[actor.id]=order.point;}
            public void Mark(string message){commands.WriteLine(F(game.World.Time)+" "+message);commands.Flush();TestContext.WriteLine(message);}
            public void Pointer(string label,Vector2 screen,Vector2 world)=>Mark($"{label}; screen=({F(screen.x)},{F(screen.y)}); world=({F(world.x)},{F(world.y)}); selected="+string.Join(",",game.Controls.Selected));
            public void State(string phase,EntityState actor)
            {
                var order=actor.orders.Count>0?actor.orders.Peek():null;
                if(moveOrders.TryGetValue(actor.id,out var travel))effective[actor.id]=travel.hasResolvedPoint?travel.resolvedPoint:travel.point;
                // Retain the real UnitOrder reference after dequeue: a destination
                // adjustment and arrival can occur in the same simulation tick.
                var a=assigned.TryGetValue(actor.id,out var aa)?aa:Vector2.zero;var e=effective.TryGetValue(actor.id,out var ee)?ee:Vector2.zero;
                var r=order?.resolvedPoint??Vector2.zero;var local=bridge.Local(actor.position);
                rows.WriteLine(string.Join(",",F(game.World.Time),C(phase),actor.id,C(actor.def.id),F(actor.previousPosition.x),F(actor.previousPosition.y),F(actor.position.x),F(actor.position.y),F(actor.def.radius),game.World.Navigation.Version,F(a.x),F(a.y),order?.hasResolvedPoint??false,F(r.x),F(r.y),F(e.x),F(e.y),C(order?.kind.ToString()??"none"),order?.target??0,actor.hold,actor.pathIndex,actor.path.Count,F(actor.pathGoal.x),F(actor.pathGoal.y),C(actor.status),F(actor.attackReadyAt),F(actor.fireAt),F(actor.deployUntil),actor.isDeployed,F(local.x),F(local.y)));rows.Flush();
            }
            public void Dispose()
            {
                rows.Dispose();Mark($"finished; fixtureAssertionsReachedEnd={passed}; bridgeIndex={bridgeIndex}; visitedRequestedBridge="+string.Join(",",visited.OrderBy(id=>id)));
                File.WriteAllText(Path.Combine(directory,"events.json"),JsonUtility.ToJson(new EventEnvelope{events=game.World.Events.ToArray()},true));commands.Dispose();
            }
            [Serializable] sealed class EventEnvelope{public SimEvent[] events;}
        }
    }
}
