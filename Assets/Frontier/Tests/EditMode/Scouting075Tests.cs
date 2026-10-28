using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class Scouting075Tests
    {
        sealed class Fixture
        {
            public readonly World World;public readonly EntityState Scout,Anchor;
            public Fixture()
            {
                World=new World(SnapshotCodec.Clone(Catalog.Load()),7501,"france","river",1);
                // Declared peaceful movement fixture. Production and payment
                // have separate integration coverage in ScoutingProduction075.
                foreach(var e in World.Entities.Where(e=>e.def.id!="town").ToArray())World.Remove(e);
                Anchor=World.Spawn("villager",0,new Vector2(20,18));
                Scout=World.Spawn("native_scout",0,new Vector2(40,26));World.Fog.Tick(World.Entities);
            }
            public void OnlyUnknown(params Vector2[] points)
            {
                var fog=Enumerable.Repeat((int)VisibilityState.Explored,128*128*World.Players.Length).ToArray();
                foreach(var point in points)fog[(int)point.x*128+(int)point.y]=(int)VisibilityState.Unexplored;
                World.Fog.RestoreState(fog);World.Fog.Tick(World.Entities);
            }
        }
        static void Accepted(CommandResult result)=>Assert.IsTrue(result.accepted,result.reason);
        static void Steps(World world,int count){for(int i=0;i<count;i++)world.Tick(.1f);}
        static GameCommand Order(EntityState actor,OrderKind kind,bool append=false,Vector2? point=null,int target=0)=>
            new GameCommand{player=actor.owner,kind=CommandKind.Order,actors=new[]{actor.id},order=kind,append=append,point=point??actor.position,target=target};
        static GameCommand Cloak(EntityState actor)=>new GameCommand{player=actor.owner,kind=CommandKind.ToggleStealth,actors=new[]{actor.id}};
        static string State(World world)=>JsonUtility.ToJson(world.CaptureState());

        [Test] public void AutoScoutPhysicallyMovesAndRevealsOnlyThroughActualVision()
        {
            var a=new Fixture();var w=a.World;var before=w.Fog.CaptureState();var start=a.Scout.position;var funds=w.Players[0].resources;
            Accepted(w.Issue(Order(a.Scout,OrderKind.Scout)));w.Tick(.1f);var order=a.Scout.orders.Peek();
            Assert.IsTrue(order.hasResolvedPoint);Assert.AreEqual(0,before[(int)order.resolvedPoint.x*128+(int)order.resolvedPoint.y]);
            Assert.Greater((a.Scout.position-start).sqrMagnitude,.01f);Assert.AreEqual("Scouting",a.Scout.status);
            Assert.IsTrue(UnitAnimation.Evaluate(a.Scout,w.Time).moving,"A real scout displacement must animate its actual gait");
            Assert.IsTrue(w.Navigation.IsWalkable(order.resolvedPoint,a.Scout.def.radius));
            int known=before.Take(128*128).Count(value=>value!=0);Steps(w,160);
            Assert.Greater(w.Fog.CaptureState().Take(128*128).Count(value=>value!=0),known);
            Assert.AreEqual(funds,w.Players[0].resources);Assert.IsTrue(a.Scout.Alive);
        }

        [Test] public void SafeFrontierWinsOverSafeEndpointWhoseOrdinaryRouteCrossesVisibleGuard()
        {
            var a=new Fixture();var w=a.World;var unsafeGoal=new Vector2(70,26);var safeGoal=new Vector2(42,58);
            var guard=w.Spawn("guardian",-1,new Vector2(55,33));a.OnlyUnknown(unsafeGoal,safeGoal);
            Assert.IsTrue(w.IsVisible(0,guard));Assert.Greater(Vector2.Distance(a.Scout.position,guard.position),8);
            Assert.Greater(Vector2.Distance(unsafeGoal,guard.position),8);Assert.IsTrue(w.Navigation.ClearLine(a.Scout.position,unsafeGoal,a.Scout.def.radius));
            Accepted(w.Issue(Order(a.Scout,OrderKind.Scout)));w.Tick(.1f);
            Assert.AreEqual(safeGoal,a.Scout.orders.Peek().resolvedPoint,"A safe endpoint cannot authorize crossing a visible weapon's route");
            for(int i=0;i<65;i++){w.Tick(.1f);Assert.GreaterOrEqual(Vector2.Distance(a.Scout.position,guard.position),8);}
            Assert.AreEqual(w.MaxHp(a.Scout),a.Scout.hp);
        }

        [Test] public void UnreachableFrontierWaitsAndClearingRealTerrainPermitsMovement()
        {
            var a=new Fixture();var w=a.World;var goal=new Vector2(70,26);a.OnlyUnknown(goal);
            w.Navigation.ConfigureTerrain(point=>Mathf.Abs(point.x-60)<.1f);var start=a.Scout.position;
            Accepted(w.Issue(Order(a.Scout,OrderKind.Scout)));Steps(w,25);
            Assert.AreEqual(start,a.Scout.position);Assert.IsFalse(a.Scout.orders.Peek().hasResolvedPoint);
            Assert.AreEqual(VisibilityState.Unexplored,w.Fog.At(0,goal));
            w.Navigation.ConfigureTerrain(point=>false);Steps(w,60);
            Assert.Greater((a.Scout.position-start).sqrMagnitude,25);Assert.AreEqual(VisibilityState.Visible,w.Fog.At(0,goal));
        }

        [Test] public void HiddenEnemyTruthDoesNotChangeFirstScoutLeg()
        {
            var a=new Fixture();var b=new Fixture();var hidden=b.World.Spawn("musketeer",1,new Vector2(94,70));b.World.Fog.Tick(b.World.Entities);
            Assert.IsFalse(b.World.IsVisible(0,hidden));Accepted(a.World.Issue(Order(a.Scout,OrderKind.Scout)));Accepted(b.World.Issue(Order(b.Scout,OrderKind.Scout)));
            a.World.Tick(.1f);b.World.Tick(.1f);
            Assert.AreEqual(a.Scout.orders.Peek().resolvedPoint,b.Scout.orders.Peek().resolvedPoint);Assert.AreEqual(a.Scout.position,b.Scout.position);
        }

        [Test] public void InactiveFactionWeaponDoesNotBlockTheSameOtherwiseUnsafeScoutFrontier()
        {
            var active=new Fixture();var inactive=new Fixture();var goal=new Vector2(70,26);
            foreach(var fixture in new[]{active,inactive})
            {
                var weapon=fixture.World.Spawn("musketeer",1,new Vector2(55,33));fixture.OnlyUnknown(goal);
                Assert.IsTrue(fixture.World.IsVisible(0,weapon));Assert.Greater(weapon.def.damage,0);
                Assert.Greater(Vector2.Distance(goal,weapon.position),weapon.def.range+4);
                Assert.IsTrue(fixture.World.Navigation.ClearLine(fixture.Scout.position,goal,fixture.Scout.def.radius));
            }
            // Declared inactive owner before the review. The first settled tick
            // may close this two-player fixture after its actual scout movement.
            inactive.World.Players[1].resigned=true;inactive.World.Players[1].eliminated=true;
            var start=inactive.Scout.position;
            Accepted(active.World.Issue(Order(active.Scout,OrderKind.Scout)));Accepted(inactive.World.Issue(Order(inactive.Scout,OrderKind.Scout)));
            active.World.Tick(.1f);inactive.World.Tick(.1f);
            Assert.IsFalse(active.Scout.orders.Peek().hasResolvedPoint,"The live visible weapon blocks the crossing");
            Assert.IsTrue(inactive.Scout.orders.Peek().hasResolvedPoint);Assert.AreEqual(goal,inactive.Scout.orders.Peek().resolvedPoint);
            Assert.Greater((inactive.Scout.position-start).sqrMagnitude,.01f,"An inactive owner's weapon cannot prevent a real scout leg");
        }

        [Test] public void NewlyVisibleThreatTriggersRealLocalRetreatWithoutAttacking()
        {
            var a=new Fixture();var w=a.World;Accepted(w.Issue(Order(a.Scout,OrderKind.Scout)));w.Tick(.1f);
            var enemy=w.Spawn("musketeer",1,a.Scout.position+Vector2.right*10);Accepted(w.Issue(Order(enemy,OrderKind.Hold)));w.Fog.Tick(w.Entities);
            float initial=Vector2.Distance(a.Scout.position,enemy.position);Steps(w,28);
            Assert.Greater(Vector2.Distance(a.Scout.position,enemy.position),initial+2);Assert.IsTrue(a.Scout.Alive);
            Assert.IsFalse(w.Events.Any(e=>e.kind=="AttackPrepared"&&e.entity==a.Scout.id));
        }

        [Test] public void QueuedMoveTakesOverAfterCurrentScoutLegAndActuallyArrives()
        {
            var a=new Fixture();var w=a.World;Accepted(w.Issue(Order(a.Scout,OrderKind.Scout)));w.Tick(.1f);var leg=a.Scout.orders.Peek().resolvedPoint;
            var destination=new Vector2(32,26);Accepted(w.Issue(Order(a.Scout,OrderKind.Move,true,destination)));
            Assert.AreEqual(2,a.Scout.orders.Count);Assert.AreEqual(leg,a.Scout.orders.Peek().resolvedPoint);
            for(int i=0;i<150&&a.Scout.orders.Peek().kind==OrderKind.Scout;i++)w.Tick(.1f);
            Assert.AreEqual(OrderKind.Move,a.Scout.orders.Peek().kind);Steps(w,180);
            Assert.IsEmpty(a.Scout.orders);Assert.Less(Vector2.Distance(destination,a.Scout.position),.1f);
        }

        [TestCase(OrderKind.Stop)][TestCase(OrderKind.Hold)]
        public void StopAndHoldOverrideScoutEvenWithShift(OrderKind kind)
        {
            var a=new Fixture();Accepted(a.World.Issue(Order(a.Scout,OrderKind.Scout)));a.World.Tick(.1f);
            Accepted(a.World.Issue(Order(a.Scout,kind,true)));var point=a.Scout.position;Steps(a.World,30);
            Assert.IsEmpty(a.Scout.orders);Assert.AreEqual(point,a.Scout.position);Assert.AreEqual(kind==OrderKind.Hold,a.Scout.hold);
        }

        [Test] public void ScoutCapabilityRejectsWorkersAndEntityTargetWithoutChangingState()
        {
            var a=new Fixture();var w=a.World;string before=State(w);
            Assert.IsFalse(w.Issue(Order(a.Anchor,OrderKind.Scout)).accepted);
            Assert.IsFalse(w.Issue(Order(a.Scout,OrderKind.Scout,target:a.Anchor.id)).accepted);Assert.AreEqual(before,State(w));
            var explorer=w.Spawn("explorer",0,new Vector2(38,26));Assert.IsTrue(w.CanScout(explorer));Assert.IsFalse(w.CanScout(a.Anchor));
        }

        [Test] public void ConcealmentGatesEnemyAttackAndEventMaskEvenOnVisibleGround()
        {
            var a=new Fixture();var w=a.World;var enemy=w.Spawn("musketeer",1,a.Scout.position+Vector2.right*14);w.Fog.Tick(w.Entities);
            Assert.AreEqual(VisibilityState.Visible,w.Fog.At(1,a.Scout.position));Assert.IsTrue(w.IsVisible(1,a.Scout));
            Accepted(w.Issue(Cloak(a.Scout)));Assert.IsTrue(w.IsVisible(0,a.Scout));Assert.IsFalse(w.IsVisible(1,a.Scout));
            Assert.AreEqual(0,w.Events.Last(e=>e.kind=="Stealth").visibilityMask&(1<<1));
            string before=State(w);Assert.IsFalse(w.Issue(Order(enemy,OrderKind.Attack,target:a.Scout.id)).accepted);Assert.AreEqual(before,State(w));
            Assert.AreEqual(a.Scout.def.speed*.6f,w.Speed(a.Scout),.001f);
        }

        [Test] public void DuplicateStealthActorTogglesExactlyOnce()
        {
            var a=new Fixture();var command=Cloak(a.Scout);command.actors=new[]{a.Scout.id,a.Scout.id};Accepted(a.World.Issue(command));
            Assert.IsTrue(a.Scout.stealth);Assert.AreEqual(1,a.World.Events.Count(e=>e.kind=="Stealth"&&e.entity==a.Scout.id));
        }

        [Test] public void OnlyActiveNearbyDesignatedDetectorRevealsStealthScout()
        {
            var a=new Fixture();var w=a.World;var soldier=w.Spawn("musketeer",1,a.Scout.position+Vector2.right*6);
            var detector=w.Spawn("explorer",1,a.Scout.position+Vector2.right*9);w.Fog.Tick(w.Entities);Accepted(w.Issue(Cloak(a.Scout)));
            Assert.AreEqual(0,soldier.def.detectionRange);Assert.IsFalse(w.IsVisible(1,a.Scout));
            detector.position=a.Scout.position+Vector2.right*8;w.Fog.Tick(w.Entities);Assert.IsTrue(w.IsVisible(1,a.Scout));
            w.Remove(detector);Assert.IsFalse(w.IsVisible(1,a.Scout));Assert.IsTrue(a.Scout.stealth,"Detection preserves the current movement/action");
            var tower=w.Spawn("tower",1,a.Scout.position+Vector2.up*7,false);w.Fog.Tick(w.Entities);Assert.IsFalse(w.IsVisible(1,a.Scout));
            tower.construction=1;Assert.IsTrue(w.IsVisible(1,a.Scout));
        }

        [Test] public void SharedAlliedVisionAndDetectionUseActualNearbyAlly()
        {
            var rules=SnapshotCodec.Clone(Catalog.Load());var w=new World(rules,7502,new[]{"france","river","france","coast"},new[]{0,0,1,1},1);
            foreach(var e in w.Entities.ToArray())w.Remove(e);
            var observer=w.Spawn("native_scout",0,new Vector2(40,26));var target=w.Spawn("native_scout",2,new Vector2(60,26));w.Spawn("villager",2,new Vector2(88,26));
            w.Fog.Tick(w.Entities);Accepted(w.Issue(Cloak(target)));Assert.IsFalse(w.IsVisible(0,target));Assert.IsTrue(w.IsVisible(1,observer));
            w.Spawn("envoy",1,new Vector2(54,26));w.Fog.Tick(w.Entities);Assert.IsTrue(w.IsVisible(0,target));
        }

        [Test] public void ManualAndQueuedAttackBreakStealthAtExecutionWithRecovery()
        {
            var a=new Fixture();var w=a.World;var target=w.Spawn("villager",1,new Vector2(60,26));w.Fog.Tick(w.Entities);
            Accepted(w.Issue(Order(a.Scout,OrderKind.Scout)));w.Tick(.1f);Accepted(w.Issue(Cloak(a.Scout)));
            Accepted(w.Issue(Order(a.Scout,OrderKind.Attack,true,target:target.id)));Assert.IsFalse(a.Scout.stealth);
            Assert.IsFalse(w.Issue(Cloak(a.Scout)).accepted);while(w.Time<a.Scout.stealthReadyAt)w.Tick(.1f);
            Assert.AreEqual(OrderKind.Scout,a.Scout.orders.Peek().kind);Accepted(w.Issue(Cloak(a.Scout)));
            for(int i=0;i<180&&a.Scout.orders.Peek().kind==OrderKind.Scout;i++)w.Tick(.1f);
            Assert.AreEqual(OrderKind.Attack,a.Scout.orders.Peek().kind);w.Tick(.1f);
            Assert.IsFalse(a.Scout.stealth);Assert.Greater(a.Scout.stealthReadyAt,w.Time);
        }

        [Test] public void AlreadyFiredProjectileStillHitsAndRevealsForSixSeconds()
        {
            var a=new Fixture();var w=a.World;var enemy=w.Spawn("musketeer",1,a.Scout.position+Vector2.right*10);w.Fog.Tick(w.Entities);
            Accepted(w.Issue(Order(a.Scout,OrderKind.Scout)));Accepted(w.Issue(Order(enemy,OrderKind.Attack,target:a.Scout.id)));
            for(int i=0;i<80&&!w.Projectiles.Any(p=>p.target==a.Scout.id);i++)w.Tick(.1f);
            Assert.IsTrue(w.Projectiles.Any(p=>p.target==a.Scout.id));float hp=a.Scout.hp;Accepted(w.Issue(Cloak(a.Scout)));Assert.IsFalse(w.IsVisible(1,a.Scout));
            for(int i=0;i<30&&a.Scout.hp==hp;i++)w.Tick(.1f);
            Assert.Less(a.Scout.hp,hp);Assert.IsTrue(a.Scout.stealth);Assert.AreEqual(w.Time+6,a.Scout.revealedUntil,.001f);Assert.IsTrue(w.IsVisible(1,a.Scout));
        }

        [Test] public void GarrisonAndDeathClearStealthAndLastPeerDeathCancelsConcealment()
        {
            var a=new Fixture();var w=a.World;Accepted(w.Issue(Cloak(a.Scout)));w.Remove(a.Anchor);w.Tick(.1f);
            Assert.IsFalse(a.Scout.stealth);StringAssert.Contains("last",w.StealthReason(0,a.Scout));
            w.Spawn("villager",0,new Vector2(20,18));Steps(w,31);Accepted(w.Issue(Cloak(a.Scout)));
            var town=w.Entities.First(e=>e.owner==0&&e.def.id=="town");Accepted(w.Issue(Order(a.Scout,OrderKind.Garrison,target:town.id)));
            for(int i=0;i<120&&a.Scout.garrisonedIn==0;i++)w.Tick(.1f);
            Assert.AreEqual(town.id,a.Scout.garrisonedIn);Assert.IsFalse(a.Scout.stealth);Assert.IsFalse(w.Issue(Cloak(a.Scout)).accepted);
            Assert.DoesNotThrow(()=>World.Restore(w.Rules,w.CaptureState()));w.Remove(a.Scout);Assert.IsFalse(a.Scout.stealth);
        }

        [Test] public void HiddenDirectDeathDoesNotLeakItsPositionThroughEventMask()
        {
            var a=new Fixture();var w=a.World;w.Fog.Reveal(1,a.Scout.position,2);Accepted(w.Issue(Cloak(a.Scout)));w.Remove(a.Scout);
            Assert.AreEqual(0,w.Events.Last(e=>e.kind=="Death"&&e.entity==a.Scout.id).visibilityMask&(1<<1));
            Assert.DoesNotThrow(()=>World.Restore(w.Rules,w.CaptureState()));
        }

        [TestCase(7f,true)][TestCase(8.5f,false)]
        public void NeutralGuardianUsesPersonalEightRangeDetection(float distance,bool detected)
        {
            var a=new Fixture();var w=a.World;var guardian=w.Spawn("guardian",-1,a.Scout.position+Vector2.right*distance);
            Assert.AreEqual(8,guardian.def.detectionRange);w.Fog.Tick(w.Entities);Accepted(w.Issue(Cloak(a.Scout)));Steps(w,35);
            Assert.AreEqual(detected,w.Events.Any(e=>e.kind=="AttackPrepared"&&e.entity==guardian.id&&e.target==a.Scout.id));
            if(!detected)Assert.AreEqual(w.MaxHp(a.Scout),a.Scout.hp);
        }

        [Test] public void PausedEndedAndForeignStealthCommandsDoNotMutateWorld()
        {
            var a=new Fixture();var w=a.World;var foreign=Cloak(a.Scout);foreign.player=1;string before=State(w);
            Assert.IsFalse(w.Issue(foreign).accepted);Assert.AreEqual(before,State(w));
            w.Paused=true;before=State(w);Assert.IsFalse(w.Issue(Cloak(a.Scout)).accepted);w.Tick(.1f);Assert.AreEqual(before,State(w));
            w.Paused=false;w.Winner=0;before=State(w);Assert.IsFalse(w.Issue(Cloak(a.Scout)).accepted);w.Tick(.1f);Assert.AreEqual(before,State(w));
        }

        [Test] public void ColdRestoreAndLanProjectionContinueIdenticalScoutOrdersFogAndEvents()
        {
            var a=new Fixture();var w=a.World;Accepted(w.Issue(Order(a.Scout,OrderKind.Scout)));Accepted(w.Issue(Cloak(a.Scout)));Steps(w,35);
            Accepted(w.Issue(Order(a.Scout,OrderKind.Move,true,new Vector2(32,26))));
            var saved=JsonUtility.FromJson<WorldSnapshot>(State(w));var restored=World.Restore(w.Rules,saved);
            Assert.AreEqual(State(w),State(restored));var replica=new LanReplica(w.Rules);replica.Install(LanProjection.Full(w,"scout075","epoch075",1,35));
            Assert.AreEqual(JsonUtility.ToJson(w.CaptureState(false)),JsonUtility.ToJson(replica.World.CaptureState(false)));
            for(int i=0;i<90;i++){w.Tick(.1f);restored.Tick(.1f);}Assert.AreEqual(State(w),State(restored));
            Assert.AreEqual(a.Scout.stealth,restored.Get(a.Scout.id).stealth);
        }

        [Test] public void RecordedScoutStealthAndQueuedOverrideReplayToIdenticalAuthoritativeState()
        {
            var a=new Fixture();var w=a.World;var simulation=new MatchSimulation(w,Array.Empty<int>());
            var identity=ReplayIdentity.Create(w.Rules,"scout075-test","controlled075","synthetic075");
            using(var recorder=new ReplayRecorder(simulation,identity,anchorInterval:25))
            {
                Accepted(recorder.Issue(Order(a.Scout,OrderKind.Scout)));Accepted(recorder.Issue(Cloak(a.Scout)));
                for(int i=0;i<30;i++)Assert.IsTrue(simulation.Step());Accepted(recorder.Issue(Order(a.Scout,OrderKind.Move,true,new Vector2(32,26))));
                for(int i=0;i<150;i++)Assert.IsTrue(simulation.Step());var document=recorder.Finish();
                using(var replay=new ReplaySession(w.Rules,document,identity))
                {
                    while(replay.CurrentStep<replay.EndStep&&replay.Error=="")Assert.Greater(replay.Advance(50),0);
                    Assert.IsEmpty(replay.Error);Assert.IsTrue(replay.Verified);Assert.AreEqual(State(w),State(replay.World));
                }
            }
        }

        [Test] public void AiUsesOnePaidDedicatedScoutAndReplacesLossWithoutMilitaryOvertraining()
        {
            var a=new Fixture();var w=a.World;w.Remove(a.Scout);
            for(int i=0;i<11;i++)w.Spawn("villager",0,new Vector2(30+i%4*1.4f,16+i/4*1.4f));
            w.Spawn("house",0,new Vector2(15,40));w.Spawn("house",0,new Vector2(20,40));w.Players[0].resources=new Cost(10000,10000,10000);w.Fog.Tick(w.Entities);
            var ai=new SkirmishAI(w,0);var before=w.Players[0].resources;
            for(int i=0;i<500&&!w.Entities.Any(e=>e.Alive&&e.owner==0&&e.Scout);i++){ai.Tick(.1f);w.Tick(.1f);}
            var first=w.Entities.FirstOrDefault(e=>e.Alive&&e.owner==0&&e.Scout);Assert.IsNotNull(first,"An available dedicated scout must be genuinely paid for and trained");
            Assert.IsFalse(first.Military);Assert.Less(w.Players[0].resources.food,before.food);
            for(int i=0;i<100;i++){ai.Tick(.1f);w.Tick(.1f);Assert.LessOrEqual(w.UnitCountIncludingProduction(0,"native_scout"),1);}
            Assert.IsTrue(first.orders.Any(o=>o.kind==OrderKind.Scout));w.Remove(first);
            for(int i=0;i<500&&!w.Entities.Any(e=>e.Alive&&e.owner==0&&e.Scout);i++){ai.Tick(.1f);w.Tick(.1f);}
            Assert.AreEqual(1,w.UnitCountIncludingProduction(0,"native_scout"));
            Assert.AreEqual(2,w.Events.Count(e=>e.kind=="UnitTrained"&&e.player==0&&w.Get(e.target)?.Scout==true));
        }
    }
}
