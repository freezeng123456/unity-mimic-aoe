using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Eight controlled EditMode instances: actual terrain/container mechanics.
    // Original Catalog definitions and original Winding geography are retained.
    // Age, funds, initial assembled land units and their wounds are fixture inputs.
    // After Seal(), only public commands and MatchSimulation.Step change gameplay;
    // no HP, position, garrison, navigation or order-list injection is performed.
    public sealed class Healing076RiverLifecycleTests
    {
        const string Version="0.19.0";
        const float Dt=MatchSimulation.StepSeconds;

        sealed class Fixture
        {
            public readonly World World;
            public MatchSimulation Simulation;
            public readonly string RulesHash;
            public Fixture()
            {
                World=new World(SnapshotCodec.Clone(Catalog.Load()),314159,"coast","coast",5,MapTopology.Winding);
                // Initial peaceful mechanics assembly: keep both original Towns,
                // resources, obstacles, bridge geometry and authored naval sites.
                foreach(var entity in World.Entities.Where(e=>e.def.kind==EntityKind.Unit).ToArray())World.Remove(entity);
                World.Players[0].age=2;World.Players[0].resources=new Cost(5000,5000,5000);
                RulesHash=SnapshotCodec.RulesHash(World.Rules);
                Assert.AreEqual(SnapshotCodec.RulesHash(Catalog.Load()),RulesHash,"No definition tuning in this fixture");
                Assert.AreEqual(2,World.Entities.Count(e=>e.Alive&&e.def.id=="town"));
            }
            Fixture(World world,MatchSimulation simulation)
            {World=world;Simulation=simulation;RulesHash=SnapshotCodec.RulesHash(world.Rules);}
            public EntityState Initial(string definition,Vector2 point,float healthFraction=1,bool automatic=false)
            {
                Assert.IsNull(Simulation,"Initial assembly ends before the first command/tick");
                var def=World.Rules.Entity(definition);Assert.IsNotNull(def);
                Assert.IsTrue(BodyLand(World,point,def.radius),"Illegal initial body: "+definition+" "+point);
                Assert.IsTrue(BodyFree(World,point,def.radius),"Initial body overlaps: "+definition+" "+point);
                var entity=World.Spawn(definition,0,point);
                entity.hp=World.MaxHp(entity)*healthFraction;
                if(entity.Healer)entity.autoHeal=automatic;
                Assert.Greater(entity.hp,0);Assert.LessOrEqual(entity.hp,World.MaxHp(entity));
                return entity;
            }
            public void Seal()
            {
                Assert.IsNull(Simulation);World.Fog.Tick(World.Entities);
                Simulation=new MatchSimulation(World,Array.Empty<int>());
                Assert.AreEqual(-1,World.Winner);
            }
            public static Fixture Cold(Fixture warm,int selected)
            {
                string directory=Path.Combine(Path.GetTempPath(),"frontier-healing-river076-"+Guid.NewGuid().ToString("N"));
                try
                {
                    string path=MatchSaveStore.SlotPath(directory,1);
                    MatchSaveStore.Write(path,new SavedMatch{
                        matchId="healing076-river-controlled",appVersion=Version,world=warm.World.CaptureState(),
                        simulation=warm.Simulation.CaptureState(),cameraSize=26,cameraFocus=Vector2.zero,
                        selection=new[]{selected},groups=Array.Empty<ControlGroupSnapshot>()});
                    Assert.IsTrue(File.Exists(path));Assert.Greater(new FileInfo(path).Length,0);
                    var saved=MatchSaveStore.Read(path,Version);Assert.AreEqual(5,saved.world.schema);
                    Assert.Throws<IOException>(()=>MatchSaveStore.Read(path,"0.18.0"));
                    var world=Frontier.World.Restore(warm.World.Rules,saved.world);
                    var cold=new Fixture(world,MatchSimulation.Restore(world,saved.simulation));
                    Assert.AreNotSame(warm.World,cold.World);Assert.AreNotSame(warm.Simulation,cold.Simulation);
                    Assert.AreNotSame(warm.World.Get(selected),cold.World.Get(selected));
                    Equal(warm,cold,"immediate actual disk cold restore");
                    TestContext.WriteLine($"Disk checkpoint: step={warm.Simulation.StepCount}; t={warm.World.Time:R}; schema={saved.world.schema}; bytes={new FileInfo(path).Length}; selected={selected}");
                    return cold;
                }
                finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
            }
        }
        sealed class MedicalGroup
        {
            public EntityState A,B,C,P,Q,Builder;
            public int[] Ids=>new[]{A.id,B.id,C.id,P.id,Q.id};
            public int[] Doctors=>new[]{A.id,B.id,C.id};
        }
        struct BeforeBody {public Vector2 position;public int container;}

        static string State(World world)=>JsonUtility.ToJson(world.CaptureState());
        static string OrderState(EntityState actor)=>JsonUtility.ToJson(new OrderList{orders=actor.orders.ToArray()});
        [Serializable] sealed class OrderList {public UnitOrder[] orders;}
        static void Equal(Fixture warm,Fixture cold,string boundary)
        {
            Assert.AreEqual(State(warm.World),State(cold.World),boundary+": world");
            Assert.AreEqual(JsonUtility.ToJson(warm.Simulation.CaptureState()),JsonUtility.ToJson(cold.Simulation.CaptureState()),boundary+": simulation");
            Assert.AreEqual(ReplayDigest.Compute(warm.World,warm.Simulation),ReplayDigest.Compute(cold.World,cold.Simulation),boundary+": event ledger digest");
        }
        static void Accepted(CommandResult result)=>Assert.IsTrue(result.accepted,result.reason);
        static void Order(Fixture f,int actor,OrderKind kind,int target=0,bool append=false,Vector2? point=null)
        {
            Assert.IsNotNull(f.Simulation);
            Accepted(f.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{actor},order=kind,
                target=target,append=append,point=point??f.World.Get(actor).position}));
        }
        static void OrderPair(Fixture warm,Fixture cold,int actor,OrderKind kind,int target=0,bool append=false,Vector2? point=null)
        {Order(warm,actor,kind,target,append,point);Order(cold,actor,kind,target,append,point);Equal(warm,cold,"paired command "+kind);}
        static void CommandPair(Fixture warm,Fixture cold,GameCommand command)
        {
            Accepted(warm.World.Issue(SnapshotCodec.Clone(command)));
            Accepted(cold.World.Issue(SnapshotCodec.Clone(command)));Equal(warm,cold,"paired command "+command.kind);
        }
        static SimEvent[] Heals(World world,int source=0,int target=0)=>world.Events.Where(e=>e.hasHealing&&
            (source==0||e.entity==source)&&(target==0||e.target==target)).ToArray();

        static void Step(Fixture f)
        {
            var world=f.World;int events=world.Events.Count;
            var before=world.Entities.Where(e=>e.Alive&&e.def.kind==EntityKind.Unit)
                .ToDictionary(e=>e.id,e=>new BeforeBody{position=e.position,container=e.garrisonedIn});
            Assert.IsTrue(f.Simulation.Step(),"Simulation stopped: "+Diagnostic(world));
            Assert.AreEqual(f.RulesHash,SnapshotCodec.RulesHash(world.Rules),"Definitions changed after fixture assembly");
            Assert.IsFalse(world.Events.Skip(events).Any(e=>e.kind=="AttackPrepared"||e.kind=="Fire"||e.kind=="Damage"),
                "Peaceful fixture was contaminated by actual combat: "+Diagnostic(world));
            foreach(var actor in world.Entities.Where(e=>e.Alive&&e.def.kind==EntityKind.Unit))
            {
                if(actor.garrisonedIn!=0)continue;
                var navigation=world.NavigationFor(actor);
                Assert.IsTrue(navigation.IsWalkable(actor.position,actor.def.radius),"Body left its domain: "+Describe(actor));
                Assert.IsTrue(actor.def.waterborne?world.IsWater(actor.position):!world.IsWater(actor.position)||world.IsBridge(actor.position),"Domain leak: "+Describe(actor));
                if(!before.TryGetValue(actor.id,out var previous)||previous.container!=0)continue; // Actual spawn/exit have their own assertions.
                float distance=Vector2.Distance(previous.position,actor.position);
                Assert.IsTrue(navigation.ClearLine(previous.position,actor.position,actor.def.radius),"Illegal swept body segment: "+Describe(actor));
                Assert.LessOrEqual(distance,world.Speed(actor)*Dt+.0001f,"Speed budget: "+Describe(actor));
                float height=Mathf.Abs(world.SurfaceHeight(actor.position,actor.def.waterborne)-world.SurfaceHeight(previous.position,actor.def.waterborne));
                Assert.LessOrEqual(height,distance*.4f+.0002f,"Bridge side height jump: "+Describe(actor));
            }
            foreach(var ev in world.Events.Skip(events).Where(e=>e.hasHealing))
            {
                var source=world.Get(ev.entity);var patient=world.Get(ev.target);var receipt=ev.healing;
                Assert.IsNotNull(receipt);Assert.IsTrue(world.CanHeal(source));Assert.IsTrue(patient.Alive);
                Assert.AreEqual(0,patient.garrisonedIn);Assert.IsTrue(patient.def.canReceiveHealing);
                Assert.AreEqual("Heal",ev.kind);Assert.AreEqual(ev.entity,receipt.source);Assert.AreEqual(ev.target,receipt.target);
                Assert.Greater(receipt.actual,0);Assert.AreEqual(receipt.actual,receipt.hpAfter-receipt.hpBefore,.0001f);
                Assert.AreEqual(receipt.actual,ev.amount,.0001f);Assert.AreEqual(world.HealRate(source),receipt.rate,.0001f);
                Assert.LessOrEqual(receipt.actual,receipt.rate*Dt+.0002f);Assert.LessOrEqual(receipt.hpAfter,receipt.maxHp);
                Assert.AreEqual(receipt.hpAfter,patient.hp,.0001f);
                Assert.LessOrEqual((source.position-source.previousPosition).sqrMagnitude,.000001f,"Moving source healed");
                Assert.LessOrEqual((patient.position-patient.previousPosition).sqrMagnitude,.000001f,"Moving patient healed");
                Assert.LessOrEqual(source.healingBlockedUntil,world.Time);Assert.LessOrEqual(patient.healingBlockedUntil,world.Time);
                Assert.Less(source.fireAt,0);Assert.Less(patient.fireAt,0);
                Assert.LessOrEqual(world.EdgeDistance(source,patient),world.HealRange(source)+.0001f);
                Assert.IsTrue(world.Navigation.ClearLine(source.position,patient.position,.05f),"Positive Heal crossed native terrain/railing");
            }
        }
        static void Until(Fixture f,Func<bool> done,float seconds,string reason,Action afterStep=null)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/Dt)&&!done();i++){Step(f);afterStep?.Invoke();}
            Assert.IsTrue(done(),reason+"; "+Diagnostic(f.World));
        }
        static void UntilPair(Fixture warm,Fixture cold,Func<Fixture,bool> done,float seconds,string reason,Action<Fixture> afterStep=null)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/Dt)&&!done(warm);i++)
            {Step(warm);Step(cold);afterStep?.Invoke(warm);afterStep?.Invoke(cold);Equal(warm,cold,reason+" tick "+warm.Simulation.StepCount);}
            Assert.IsTrue(done(warm),reason+"; "+Diagnostic(warm.World));Assert.IsTrue(done(cold));Equal(warm,cold,reason+" final");
        }
        static string Describe(EntityState e)=>$"{e.id}/{e.def.id} p={e.position} old={e.previousPosition} cargo={e.garrisonedIn} hp={e.hp:R} status={e.status} path={e.pathIndex}/{e.path.Count} orders={OrderState(e)}";
        static string Diagnostic(World w)=>$"t={w.Time:R}; winner={w.Winner}; "+string.Join("; ",w.Entities.Where(e=>e.Alive&&e.owner==0&&e.def.kind==EntityKind.Unit).Select(Describe));
        static bool BodyLand(World w,Vector2 p,float radius)=>p.x>=radius&&p.y>=radius&&p.x<=127-radius&&p.y<=127-radius&&
            !w.IsWater(p)&&w.Navigation.IsWalkable(p,radius);
        static bool BodyFree(World w,Vector2 p,float radius,HashSet<int> ignored=null)=>!w.Entities.Any(e=>e.Alive&&e.garrisonedIn==0&&
            !e.def.waterborne&&e.def.kind!=EntityKind.Building&&(ignored==null||!ignored.Contains(e.id))&&
            (e.position-p).sqrMagnitude<Mathf.Pow(radius+e.def.radius+.2f,2));
        static void AssertReleasedBody(World w,EntityState actor,float padding)
        {
            Assert.IsTrue(BodyLand(w,actor.position,actor.def.radius));
            foreach(var other in w.Entities.Where(e=>e.Alive&&e.id!=actor.id&&e.garrisonedIn==0&&!e.def.waterborne&&e.def.kind!=EntityKind.Building))
                Assert.GreaterOrEqual(Vector2.Distance(actor.position,other.position),actor.def.radius+other.def.radius+padding-.001f,
                    "Released body intersects another body: "+Describe(actor)+" / "+Describe(other));
        }
        static void AssertQuietForHold(World w,EntityState actor)
        {
            // Hold stops pursuit, not nearby fire. Make its quiet premise explicit.
            foreach(var enemy in w.Entities.Where(e=>e.Alive&&e.owner>=0&&w.AreEnemies(actor.owner,e.owner)&&e.def.kind!=EntityKind.Resource))
            {
                float ownRange=actor.def.damage>0?Mathf.Max(1.4f,actor.def.range):0;
                bool enemyArmed=enemy.def.damage>0&&(enemy.def.garrisonAttackLimit<=0||w.GarrisonFirepowerCount(enemy)>0);
                float enemyRange=enemyArmed?Mathf.Max(1.4f,enemy.def.range):0;
                Assert.Greater(w.EdgeDistance(actor,enemy),Mathf.Max(ownRange,enemyRange)+.2f,
                    "Post-exit Hold needs an actual quiet location: "+Describe(actor)+" / "+Describe(enemy));
            }
        }
        static Vector2 SafeLand(World w,Vector2 center,float radius,Func<Vector2,bool> extra=null,HashSet<int> ignored=null)
        {
            foreach(var p in w.Navigation.FormationSlots(center,512,radius*2+.4f))
                if(Vector2.Distance(center,p)<=12&&BodyLand(w,p,radius)&&BodyFree(w,p,radius,ignored)&&(extra==null||extra(p)))return p;
            Assert.Fail("No bounded body-safe land point near "+center);return Vector2.zero;
        }
        static float PathLength(World w,EntityState actor,Vector2 destination)
        {
            var path=w.NavigationFor(actor).FindPath(actor.position,destination,actor.def.radius);
            Assert.IsNotEmpty(path,"No physical route: "+Describe(actor)+" destination="+destination);
            Assert.Less(Vector2.Distance(path.Last(),destination),.001f,"Path projection changed the exact fixture destination");
            float length=Vector2.Distance(actor.position,path[0]);for(int i=1;i<path.Count;i++)length+=Vector2.Distance(path[i-1],path[i]);return length;
        }
        static bool PathVisits(World w,Vector2 start,List<Vector2> path,BridgeSpan bridge)
        {
            var previous=start;
            foreach(var next in path)
            {
                int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(previous,next)*4));
                for(int i=0;i<=n;i++){var p=Vector2.Lerp(previous,next,(float)i/n);if(bridge.Contains(p)&&w.IsWater(p))return true;}
                previous=next;
            }
            return false;
        }

        [TestCase(0)][TestCase(1)]
        public void ExplicitHealPhysicallyCrossesWindingBridgeAndSurvivesColdCheckpoint(int bridgeIndex)
        {
            var f=new Fixture();var w=f.World;var span=w.Layout.Bridges[bridgeIndex];
            var doctorDef=w.Rules.Entity("priest");var patientDef=w.Rules.Entity("explorer");float mouth=span.HalfLength+span.RampLength;
            var start=SafeLand(w,span.Point(-mouth-3,0),doctorDef.radius,p=>span.Local(p).x<-mouth-1);
            var end=SafeLand(w,span.Point(mouth+doctorDef.healRange+3,0),patientDef.radius,p=>span.Local(p).x>=mouth+doctorDef.healRange+2);
            var initialRoute=w.Navigation.FindPath(start,end,doctorDef.radius);Assert.IsNotEmpty(initialRoute);
            Assert.IsTrue(PathVisits(w,start,initialRoute,span),"Fixture route must use the specified bridge, not merely another crossing");
            var doctor=f.Initial("priest",start);var patient=f.Initial("explorer",end,.15f);f.Seal();
            Order(f,patient.id,OrderKind.Hold);Order(f,doctor.id,OrderKind.Heal,patient.id);
            bool visited=false;
            Until(f,()=>span.Contains(doctor.position)&&w.IsWater(doctor.position),120,"Doctor must physically enter specified bridge",()=>{
                visited|=span.Contains(doctor.position)&&w.IsWater(doctor.position);Assert.IsEmpty(Heals(w,doctor.id,patient.id),"No care before full medical crossing");});
            Assert.IsTrue(visited);Assert.IsEmpty(Heals(w));
            Assert.AreEqual(OrderKind.Heal,doctor.orders.Peek().kind);Assert.AreEqual(patient.id,doctor.orders.Peek().target);
            Assert.IsTrue(doctor.orders.Peek().hasResolvedPoint,"Checkpoint must preserve an actual medical approach");
            var cold=Fixture.Cold(f,doctor.id);var visitedBy=new Dictionary<Fixture,bool>{{f,true},{cold,true}};
            UntilPair(f,cold,x=>x.World.Get(patient.id).hp>=x.World.MaxHp(x.World.Get(patient.id)),160,"Cross-bridge care reaches full HP",x=>{
                var medic=x.World.Get(doctor.id);visitedBy[x]|=span.Contains(medic.position)&&x.World.IsWater(medic.position);
                if(Heals(x.World,doctor.id,patient.id).Length>0)Assert.Greater(span.Local(medic.position).x,mouth,"Doctor must pass the entire far ramp, not just heal from mid-deck");});
            Assert.IsTrue(visitedBy[f]&&visitedBy[cold]);Assert.Greater(Heals(w,doctor.id,patient.id).Length,0);
            Step(f);Step(cold);Equal(f,cold,"full patient order cleanup");Assert.IsEmpty(doctor.orders);Assert.IsFalse(doctor.autoHeal);
            TestContext.WriteLine($"Bridge {bridgeIndex}: center={span.Center}; start={start}; patient={end}; actualDoctor={doctor.position}; heals={Heals(w,doctor.id,patient.id).Length}");
        }

        static bool LineTouchesRailing(World w,Vector2 a,Vector2 b)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)*8));
            for(int i=0;i<=steps;i++)if(w.Layout.IsBridgeSideBarrierCell(Vector2.Lerp(a,b,(float)i/steps)))return true;
            return false;
        }
        static void RailingPair(World w,BridgeSpan span,out Vector2 inside,out Vector2 outside)
        {
            float doctorRadius=w.Rules.Entity("priest").radius,patientRadius=w.Rules.Entity("explorer").radius;
            foreach(int side in new[]{1,-1})
                for(float across=span.HalfLength+1;across<=span.HalfLength+span.RampLength-1;across+=.5f)
                    for(float inset=1.2f;inset<=2.7f;inset+=.5f)
                        for(float outset=2.8f;outset<=5.3f;outset+=.5f)
                        {
                            var a=span.Point(across,side*(span.HalfWidth-inset));var b=span.Point(across,side*(span.HalfWidth+outset));
                            if(!BodyLand(w,a,doctorRadius)||!BodyLand(w,b,patientRadius)||!BodyFree(w,a,doctorRadius)||!BodyFree(w,b,patientRadius)||
                                Vector2.Distance(a,b)>w.Rules.Entity("priest").healRange||w.Navigation.ClearLine(a,b,.05f)||!LineTouchesRailing(w,a,b))continue;
                            var path=w.Navigation.FindPath(a,b,doctorRadius);
                            if(path.Count==0||Vector2.Distance(path.Last(),b)>.001f)continue;
                            inside=a;outside=b;return;
                        }
            Assert.Fail("No complete-body, reachable, native-railing fixture for bridge "+span.Center);inside=outside=Vector2.zero;
        }
        [TestCase(0)][TestCase(1)]
        public void WindingRailingBlocksNearStationaryCareUntilOrdinaryDetour(int bridgeIndex)
        {
            var f=new Fixture();var w=f.World;var span=w.Layout.Bridges[bridgeIndex];RailingPair(w,span,out var inside,out var outside);
            var doctor=f.Initial("priest",inside,automatic:true);var patient=f.Initial("explorer",outside,.15f);f.Seal();Order(f,patient.id,OrderKind.Hold);
            Assert.LessOrEqual(w.EdgeDistance(doctor,patient),w.HealRange(doctor));Assert.IsFalse(w.Navigation.ClearLine(inside,outside,.05f));
            float hp=patient.hp;for(int i=0;i<30;i++)Step(f);
            Assert.AreEqual(inside,doctor.position);Assert.AreEqual(outside,patient.position);Assert.AreEqual(hp,patient.hp);Assert.IsEmpty(Heals(w));
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.ToggleAutoHeal,actors=new[]{doctor.id}}));Assert.IsFalse(doctor.autoHeal);
            Order(f,doctor.id,OrderKind.Heal,patient.id);bool visitedMouth=false;
            Until(f,()=>Heals(w,doctor.id,patient.id).Length>0,120,"Explicit care must take a real native railing detour",()=>{
                visitedMouth|=span.Local(doctor.position).x>span.HalfLength+span.RampLength;
                if(!w.Navigation.ClearLine(doctor.position,patient.position,.05f))Assert.AreEqual(hp,patient.hp,"Care leaked before the native medical line opened");});
            Assert.Greater(Vector2.Distance(inside,doctor.position),1);Assert.IsTrue(visitedMouth,"Body must reach the ordinary ramp mouth, not walk through the side rail");
            Assert.Greater(patient.hp,hp);Assert.IsTrue(w.Navigation.ClearLine(doctor.position,patient.position,.05f));
            TestContext.WriteLine($"Railing {bridgeIndex}: bodyPoints={inside}/{outside}; local={span.Local(inside)}/{span.Local(outside)}; firstHeal={Heals(w,doctor.id,patient.id).First().time:R}; actualDoctor={doctor.position}");
        }

        static Vector2[] Cluster(World w,Vector2 center,HashSet<int> ignored=null,Func<Vector2[],bool> extra=null)
        {
            var offsets=new[]{Vector2.zero,new Vector2(-2,2),new Vector2(2,2),new Vector2(3,0),new Vector2(0,5)};
            var definitions=new[]{"priest","priest","priest","explorer","explorer"};
            foreach(var anchor in w.Navigation.FormationSlots(center,512,1.5f))
            {
                if(Vector2.Distance(anchor,center)>8)continue;var points=offsets.Select(o=>anchor+o).ToArray();bool legal=true;
                for(int i=0;i<points.Length&&legal;i++)
                {
                    float radius=w.Rules.Entity(definitions[i]).radius;
                    if(!BodyLand(w,points[i],radius)||!BodyFree(w,points[i],radius,ignored)){legal=false;break;}
                    for(int j=0;j<i;j++)if(Vector2.Distance(points[i],points[j])<radius+w.Rules.Entity(definitions[j]).radius+.2f||
                        !w.Navigation.ClearLine(points[i],points[j],.05f)){legal=false;break;}
                }
                if(legal&&(extra==null||extra(points)))return points;
            }
            Assert.Fail("No complete-body medical cluster near "+center);return Array.Empty<Vector2>();
        }
        static MedicalGroup InitialGroup(Fixture f,bool builder)
        {
            var townPosition=f.World.OpeningTownPosition(0);
            var points=Cluster(f.World,townPosition+new Vector2(-9,7),extra:ps=>
                Vector2.Distance(ps[0],townPosition)>7&&Vector2.Distance(ps[3],townPosition)>7);
            var group=new MedicalGroup{A=f.Initial("priest",points[0],.15f),B=f.Initial("priest",points[1]),C=f.Initial("priest",points[2]),
                P=f.Initial("explorer",points[3],.15f),Q=f.Initial("explorer",points[4],.15f)};
            if(builder)group.Builder=f.Initial("villager",SafeLand(f.World,f.World.OpeningTownPosition(0)+new Vector2(-8,-7),f.World.Rules.Entity("villager").radius));
            f.Seal();Order(f,group.P.id,OrderKind.Hold);Order(f,group.Q.id,OrderKind.Hold);return group;
        }
        static int EstablishRelatedIntents(Fixture f,MedicalGroup g,bool enteringHealer,out Dictionary<int,string> expectedQueues)
        {
            int entrant=enteringHealer?g.A.id:g.P.id;
            var first=enteringHealer?g.B:g.A;var pending=enteringHealer?g.C:g.B;
            if(enteringHealer)Order(f,g.A.id,OrderKind.Heal,g.P.id);
            Order(f,first.id,OrderKind.Heal,entrant);Order(f,first.id,OrderKind.Heal,g.Q.id,append:true);
            var goal=SafeLand(f.World,g.Q.position+Vector2.left*8,first.def.radius,ignored:new HashSet<int>(g.Ids));
            Order(f,first.id,OrderKind.Move,append:true,point:goal);
            Order(f,pending.id,OrderKind.Heal,g.Q.id);Order(f,pending.id,OrderKind.Heal,entrant,append:true);
            Order(f,pending.id,OrderKind.Move,append:true,point:SafeLand(f.World,goal+Vector2.down*3,pending.def.radius,ignored:new HashSet<int>(g.Ids)));
            int before=f.World.Events.Count;Step(f);
            Assert.IsTrue(f.World.Events.Skip(before).Any(e=>e.hasHealing&&e.entity==first.id&&e.target==entrant),"Need a real active receipt before entering a container");
            Assert.AreEqual(entrant,first.healingTarget);
            Assert.IsTrue(pending.orders.Skip(1).Any(o=>o.kind==OrderKind.Heal&&o.target==entrant),"Need a genuine non-head queued relation");
            expectedQueues=new Dictionary<int,string>();
            foreach(var medic in new[]{first,pending})expectedQueues.Add(medic.id,JsonUtility.ToJson(new OrderList{
                orders=medic.orders.Where(o=>o.kind!=OrderKind.Heal||o.target!=entrant).ToArray()}));
            return entrant;
        }
        static void AssertEntryCleanup(Fixture f,int entrant,int container,Dictionary<int,string> expected,int entryEventStart,string eventKind)
        {
            var w=f.World;var actor=w.Get(entrant);var host=w.Get(container);
            Assert.AreEqual(container,actor.garrisonedIn);Assert.AreEqual(1,host.occupants.Count(id=>id==entrant));Assert.IsEmpty(actor.orders);
            Assert.AreEqual(0,actor.healingTarget);Assert.AreNotEqual(WorkKind.Heal,actor.work.kind);if(actor.Healer)Assert.IsFalse(w.CanHeal(actor));
            var entered=w.Events.Skip(entryEventStart).Single(e=>e.kind==eventKind&&e.entity==entrant&&e.target==container);
            foreach(var medic in w.Entities.Where(e=>e.Alive&&e.Healer))
            {Assert.AreNotEqual(entrant,medic.healingTarget);Assert.IsFalse(medic.orders.Any(o=>o.kind==OrderKind.Heal&&o.target==entrant));}
            foreach(var pair in expected)Assert.AreEqual(pair.Value,OrderState(w.Get(pair.Key)),"Unrelated Heal/Shift Move order data changed");
            Assert.IsFalse(w.Events.Where(e=>e.time==entered.time).Any(e=>e.hasHealing&&(e.entity==entrant||e.target==entrant)),"Container entry settles before same-tick care");
            Assert.Less(actor.hp,w.MaxHp(actor),"The active/queued cleanup case lost its wounded-patient discriminator");
            TestContext.WriteLine($"{eventKind}: id={entrant}; host={container}; step={f.Simulation.StepCount}; t={entered.time:R}; hp={actor.hp:R}; expectedQueues={expected.Count}");
        }
        static void StopExternalPair(Fixture warm,Fixture cold,MedicalGroup group,int entrant)
        {foreach(int id in group.Doctors)if(id!=entrant)OrderPair(warm,cold,id,OrderKind.Stop);}
        static void AssertNoEntrantCare(Fixture f,int entrant,int events,float hp)
        {
            Assert.AreEqual(hp,f.World.Get(entrant).hp);
            Assert.IsFalse(f.World.Events.Skip(events).Any(e=>e.hasHealing&&(e.entity==entrant||e.target==entrant)));
        }
        static void FreshCareAfterExit(Fixture warm,Fixture cold,MedicalGroup group,bool enteringHealer)
        {
            int healer=group.A.id,patient=group.P.id;var doctor=warm.World.Get(healer);var target=warm.World.Get(patient);
            Assert.IsTrue(warm.World.CanHeal(doctor));Assert.IsFalse(doctor.autoHeal);Assert.Less(target.hp,warm.World.MaxHp(target));
            var goal=SafeLand(warm.World,target.position+Vector2.left*3,doctor.def.radius,ignored:new HashSet<int>{healer});
            PathLength(warm.World,doctor,goal);OrderPair(warm,cold,healer,OrderKind.Move,point:goal);
            UntilPair(warm,cold,x=>x.World.Get(healer).orders.Count==0,240,"Returned healer must physically reach patient");
            int events=warm.World.Events.Count;float hp=target.hp;OrderPair(warm,cold,healer,OrderKind.Heal,patient);
            UntilPair(warm,cold,x=>x.World.Get(patient).hp>hp,10,"Fresh explicit order restores care after legal exit");
            Assert.IsTrue(warm.World.Events.Skip(events).Any(e=>e.hasHealing&&e.entity==healer&&e.target==patient));
            Assert.IsFalse(warm.World.Get(healer).autoHeal);Equal(warm,cold,"fresh post-exit positive healing");
        }

        [TestCase(false)][TestCase(true)]
        public void ActualGarrisonCleansOnlyContainerRelatedHealingAndColdExitNeedsFreshIntent(bool enteringHealer)
        {
            var f=new Fixture();var w=f.World;var g=InitialGroup(f,false);var town=w.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id=="town");
            int entrant=EstablishRelatedIntents(f,g,enteringHealer,out var expected);int population=w.Players[0].usedPopulation;
            int events=w.Events.Count;float started=w.Time;var initialPosition=w.Get(entrant).position;Order(f,entrant,OrderKind.Garrison,town.id);
            Until(f,()=>w.Get(entrant).garrisonedIn==town.id,12,"Actual short-route building entry");
            Assert.LessOrEqual(w.Time-started,12.001f);Assert.AreEqual(population,w.Players[0].usedPopulation);
            Assert.Greater(Vector2.Distance(initialPosition,w.Get(entrant).position),.1f,"Container fixture must actually walk toward the Town");
            AssertEntryCleanup(f,entrant,town.id,expected,events,"Garrison");
            var cold=Fixture.Cold(f,entrant);StopExternalPair(f,cold,g,entrant);
            float hp=w.Get(entrant).hp;int contained=w.Events.Count;
            for(int i=0;i<50;i++){Step(f);Step(cold);AssertNoEntrantCare(f,entrant,contained,hp);AssertNoEntrantCare(cold,entrant,contained,hp);Equal(f,cold,"contained medicine tick");}
            string before=State(w);Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{g.B.id},order=OrderKind.Heal,target=entrant}).accepted);
            Assert.AreEqual(before,State(w));Equal(f,cold,"invalid contained-patient request is atomic");
            CommandPair(f,cold,new GameCommand{player=0,kind=CommandKind.Ungarrison,actors=new[]{town.id}});
            UntilPair(f,cold,x=>x.World.Get(entrant).garrisonedIn==0&&x.World.Get(entrant).orders.Count==0,90,"Real Ungarrison and automatic rally arrival");
            Assert.IsFalse(town.occupants.Contains(entrant));Assert.AreEqual(population,w.Players[0].usedPopulation);
            AssertReleasedBody(w,w.Get(entrant),0);
            AssertQuietForHold(w,w.Get(entrant));
            OrderPair(f,cold,entrant,OrderKind.Hold); // Quiet public post-exit intent; never restart a stale Heal.
            int afterExit=w.Events.Count;for(int i=0;i<20;i++){Step(f);Step(cold);Equal(f,cold,"no stale post-garrison medical intent");}
            AssertNoEntrantCare(f,entrant,afterExit,hp);AssertNoEntrantCare(cold,entrant,afterExit,hp);
            FreshCareAfterExit(f,cold,g,enteringHealer);
        }

        static EntityState PaidTransport(Fixture f,EntityState builder,out EntityState dock)
        {
            var w=f.World;dock=null;
            // Same public paid production route as NavalTransport065Tests. No
            // assembled fixture ship, TickEconomy shortcut or private reflection.
            foreach(var candidate in w.Layout.DockSites[0])
            {
                var survey=w.Navigation.NearestWalkable(candidate-w.Layout.WaterDirection(candidate)*4,builder.def.radius);
                Order(f,builder.id,OrderKind.Move,point:survey);
                Until(f,()=>w.Fog.At(0,candidate)==VisibilityState.Visible,90,"Builder must physically survey harbor");
                if(w.CanBuild(0,"dock",candidate)!="")continue;
                var funds=w.Players[0].resources;var cost=w.EntityCost(0,w.Rules.Entity("dock"));
                Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{builder.id},item="dock",point=candidate}));
                Assert.AreEqual(funds.wood-cost.wood,w.Players[0].resources.wood,.001f);
                dock=w.Entities.Last(e=>e.Alive&&e.owner==0&&e.def.id=="dock");var foundation=dock;
                Assert.AreEqual(cost,dock.paidConstruction);
                Until(f,()=>foundation.Complete,130,"Paid foundation must finish through actual worker building");break;
            }
            Assert.IsNotNull(dock,"No actual paid harbor");var player=w.Players[0];var definition=w.Rules.Entity("transport_ship");
            var before=player.resources;int used=player.usedPopulation;var unitCost=w.UnitCost(0,definition);
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{dock.id},item=definition.id}));
            Assert.AreEqual(before.food-unitCost.food,player.resources.food,.001f);Assert.AreEqual(before.wood-unitCost.wood,player.resources.wood,.001f);
            Assert.AreEqual(before.coin-unitCost.coin,player.resources.coin,.001f);Assert.AreEqual(definition.population,player.reservedPopulation);
            Assert.AreEqual(unitCost,dock.queue.Single().paid);
            Until(f,()=>w.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.transportShip),definition.time+2,"Paid queue delivers physical water exit");
            var ship=w.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.transportShip);
            Assert.AreEqual(used+definition.population,player.usedPopulation);Assert.AreEqual(0,player.reservedPopulation);
            Assert.IsTrue(w.WaterNavigation.IsWalkable(ship.position,ship.def.radius));Assert.IsTrue(w.IsWater(ship.position));
            int dockId=dock.id;Assert.IsTrue(w.Events.Any(e=>e.kind=="Payment"&&e.entity==dockId&&e.message.StartsWith(ship.def.name)));
            Assert.IsTrue(w.Events.Any(e=>e.kind=="UnitTrained"&&e.entity==dockId&&e.target==ship.id));return ship;
        }
        static Vector2 Shore(World w,EntityState ship,int player,out Vector2 berth)
        {
            var candidates=w.Layout.LandingSites[player].ToList();
            foreach(var site in w.Layout.DockSites[player])
            {var direction=w.Layout.WaterDirection(site);var side=new Vector2(-direction.y,direction.x);candidates.Add(site+side*10);candidates.Add(site-side*10);}
            foreach(var candidate in candidates)if(w.TryTransportShore(ship,candidate,.9f,out var land,out berth))return land;
            Assert.Fail("No actual transport shore for player "+player);berth=Vector2.zero;return Vector2.zero;
        }
        static void MoveGroupToShore(Fixture f,MedicalGroup group,Vector2 shore)
        {
            var points=Cluster(f.World,shore-f.World.Layout.WaterDirection(shore)*7,new HashSet<int>(group.Ids),
                ps=>ps.All(p=>Vector2.Distance(p,shore)>3));
            // Sequential ordinary moves avoid reserving the same unoccupied goal.
            for(int i=0;i<group.Ids.Length;i++)
            {
                int id=group.Ids[i];PathLength(f.World,f.World.Get(id),points[i]);Order(f,id,OrderKind.Move,point:points[i]);
                Until(f,()=>f.World.Get(id).orders.Count==0,180,"Medical assembly walks to actual shore");
                Assert.Less(Vector2.Distance(f.World.Get(id).position,points[i]),.1f);
            }
            Order(f,group.P.id,OrderKind.Hold);Order(f,group.Q.id,OrderKind.Hold);
        }
        static void AssertCargo(Fixture f,int entrant,int ship,int expectedPopulation,float hp)
        {
            var actor=f.World.Get(entrant);var host=f.World.Get(ship);
            Assert.AreEqual(ship,actor.garrisonedIn);Assert.AreEqual(host.position,actor.position);Assert.AreEqual(host.position,actor.previousPosition);
            Assert.AreEqual(1,host.occupants.Count(id=>id==entrant));Assert.AreEqual(f.World.TransportSlotCost(actor),f.World.TransportLoad(host));
            Assert.AreEqual(expectedPopulation,f.World.Players[0].usedPopulation);Assert.AreEqual(hp,actor.hp);Assert.IsEmpty(actor.orders);
            if(actor.Healer)Assert.IsFalse(f.World.CanHeal(actor));
        }
        [TestCase(false)][TestCase(true)]
        public void PaidWindingTransportCleansHealingAndColdDisembarkRestoresEligibility(bool enteringHealer)
        {
            var f=new Fixture();var w=f.World;var g=InitialGroup(f,true);var ship=PaidTransport(f,g.Builder,out _);
            var shore=Shore(w,ship,0,out var berth);MoveGroupToShore(f,g,shore);
            // Ordinary Move chooses a water formation slot, not the exact naval
            // berth. Bind its actual destination; only real Embark is entitled
            // to resolve the final shore/berth pair with the passenger's radius.
            Order(f,ship.id,OrderKind.Move,point:berth);var sailOrder=ship.orders.Peek();var sailSlot=sailOrder.point;
            Until(f,()=>ship.orders.Count==0,90,"Paid vessel reaches its assigned water Move destination");
            var sailGoal=sailOrder.hasResolvedPoint?sailOrder.resolvedPoint:sailOrder.point;
            Assert.Less(Vector2.Distance(ship.position,sailGoal),.1f);
            Assert.IsTrue(w.WaterNavigation.IsWalkable(ship.position,ship.def.radius));
            TestContext.WriteLine($"Ordinary sail: requestedBerth={berth}; formationSlot={sailSlot}; finalGoal={sailGoal}; actualShip={ship.position}");
            int entrant=EstablishRelatedIntents(f,g,enteringHealer,out var expected);var actor=w.Get(entrant);
            Assert.Less(PathLength(w,actor,shore),w.Speed(actor)*10,"Prepared shoreline entry must be genuinely short");
            int population=w.Players[0].usedPopulation;var resources=w.Players[0].resources;float initialHp=actor.hp;int events=w.Events.Count;float started=w.Time;var start=actor.position;
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=new[]{entrant},target=ship.id,point=shore}));
            Assert.AreEqual(0,actor.garrisonedIn);Assert.AreEqual(OrderKind.Embark,actor.orders.Peek().kind);
            var actualShore=actor.orders.Peek().point;
            Assert.Less(Vector2.Distance(shore,actualShore),.001f,"Fixture must bind the actual common shore, not an assumed requested point");
            Assert.Greater(Vector2.Distance(actor.position,actualShore),w.Speed(actor)*Dt+.35f+.001f,
                "Need a genuinely distant first-tick boarding discriminator before asserting no instant entry");
            Assert.AreEqual(OrderKind.TransportBerth,ship.orders.Peek().kind);
            var boardingOrder=ship.orders.Peek();Assert.IsTrue(boardingOrder.hasResolvedPoint);
            Assert.IsTrue(w.WaterNavigation.IsWalkable(boardingOrder.resolvedPoint,ship.def.radius));
            TestContext.WriteLine($"Real Embark: requestedShore={shore}; actualShore={actualShore}; actualBerth={boardingOrder.resolvedPoint}; preEntryShip={ship.position}");
            Step(f);
            Assert.AreEqual(0,actor.garrisonedIn,"A distant shore command must walk, not instantly board");Assert.Greater(Vector2.Distance(start,actor.position),0);
            Until(f,()=>actor.garrisonedIn==ship.id,12,"Actual short-route boarding into paid transport");
            Assert.LessOrEqual(Vector2.Distance(ship.position,boardingOrder.resolvedPoint),.251f,"Actual entry requires the ship at its real resolved naval berth");
            Assert.LessOrEqual(w.Time-started,12.101f);AssertEntryCleanup(f,entrant,ship.id,expected,events,"Embarked");
            Assert.AreEqual(resources,w.Players[0].resources);Assert.GreaterOrEqual(actor.hp,initialHp);
            var cold=Fixture.Cold(f,entrant);StopExternalPair(f,cold,g,entrant);float containedHp=actor.hp;int containedEvents=w.Events.Count;
            for(int i=0;i<50;i++)
            {Step(f);Step(cold);AssertCargo(f,entrant,ship.id,population,containedHp);AssertCargo(cold,entrant,ship.id,population,containedHp);
                AssertNoEntrantCare(f,entrant,containedEvents,containedHp);Equal(f,cold,"real cargo care pause");}
            string before=State(w);Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{g.B.id},order=OrderKind.Heal,target=entrant}).accepted);
            Assert.AreEqual(before,State(w));Equal(f,cold,"cargo patient Heal rejected atomically");
            var landing=Shore(w,ship,1,out _);Assert.Greater(Vector2.Distance(shore,landing),20,"Landing must require actual sailing");
            var shipStart=ship.position;int unloadEvents=w.Events.Count;bool sailed=false;
            CommandPair(f,cold,new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{ship.id},point=landing});
            UntilPair(f,cold,x=>x.World.Get(entrant).garrisonedIn==0,180,"Paid ship sails and really disembarks",x=>{
                var vessel=x.World.Get(ship.id);sailed|=Vector2.Distance(vessel.position,shipStart)>10;
                if(x.World.Get(entrant).garrisonedIn!=0)AssertCargo(x,entrant,ship.id,population,containedHp);
                AssertNoEntrantCare(x,entrant,containedEvents,containedHp);});
            Assert.IsTrue(sailed);Assert.IsEmpty(ship.occupants);Assert.AreEqual(0,w.TransportLoad(ship));Assert.AreEqual(population,w.Players[0].usedPopulation);
            Assert.AreEqual(1,w.Events.Skip(unloadEvents).Count(e=>e.kind=="Disembarked"&&e.entity==entrant&&e.target==ship.id));
            AssertReleasedBody(w,actor,.15f);Assert.IsFalse(w.IsBridge(actor.position));Assert.IsFalse(w.IsBridgeApproach(actor.position));
            Assert.IsEmpty(actor.orders);Assert.AreEqual(containedHp,actor.hp);Assert.AreEqual(1,w.Entities.Count(e=>e.Alive&&e.id==entrant));
            AssertQuietForHold(w,actor);
            OrderPair(f,cold,entrant,OrderKind.Hold); // Explorer remains quiet near the opposing bank's ungarrisoned Town.
            int afterExit=w.Events.Count;for(int i=0;i<20;i++){Step(f);Step(cold);Equal(f,cold,"no stale post-ship medical intent");}
            AssertNoEntrantCare(f,entrant,afterExit,containedHp);FreshCareAfterExit(f,cold,g,enteringHealer);
            TestContext.WriteLine($"Paid transport: enteringHealer={enteringHealer}; ship={ship.id}; shore={shore}; landing={landing}; entrant={entrant}; population={population}; cold step={cold.Simulation.StepCount}");
        }
    }
}
