using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    // Actual InputSystem -> RtsInput -> public commands, actual World.Tick.
    // Spawned command fixtures; not paid-economy, native OS or400 evidence.
    public sealed class MovementFeedback084PlayModeTests : NativeInputFixture
    {
        [Serializable] sealed class Receipt
        {
            public string sourceFingerprint,scope,status="running",error;
            public string initialSummary,waitingSummary,movingSummary,changedSummary;
            public bool worldReadOnly,fogReadOnly,coldEqual,ownerPrivacy,unknownBridgeHidden,rampOnlyHidden;
            public float zeroWaitDisplacement,subsequentAdvance;
        }
        static string State(World world)=>JsonUtility.ToJson(world.CaptureState());
        static string Point(Vector2 point)=>"("+point.x.ToString("0",CultureInfo.InvariantCulture)+","+point.y.ToString("0",CultureInfo.InvariantCulture)+")";
        static void Save(string name,Receipt receipt)
        {
            string folder=Environment.GetEnvironmentVariable("FRONTIER084_EVIDENCE");
            if(string.IsNullOrEmpty(folder))folder=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");
            if(string.IsNullOrEmpty(folder))folder=Path.Combine(Directory.GetCurrentDirectory(),"Builds","Movement084");
            Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,name+".json"),JsonUtility.ToJson(receipt,true));
        }
        IEnumerator River()
        {
            game.SelectedMap=MapTopology.River;game.SelectedObjective="elimination";game.StartMatch();game.enabled=false;
            foreach(var entity in game.World.Entities.Where(e=>e.def.id!="town").ToArray())game.World.Remove(entity);
            yield return Ready();
        }
        IEnumerator GroundClick(Vector2 point,MouseButton button)
        {
            game.Controls.Focus(point);yield return Ready();
            Vector2 pixel=ScreenPoint(point,game.World.SurfaceHeight(point));AssertWorldPoint(pixel);yield return Click(pixel,button);
        }
        IEnumerator SelectByClick(EntityState actor)
        {
            game.Controls.Focus(actor.position);yield return Ready();
            Vector2 pixel=ScreenPoint(actor.position,game.World.SurfaceHeight(actor.position)+1);AssertWorldPoint(pixel);yield return Click(pixel);
            Assert.AreEqual(actor.id,game.Controls.FirstSelected?.id,"The production input controller must select the actor");
        }
        void AssertReadOnly(Receipt receipt)
        {
            var world=game.World;string before=State(world);int[] fog=world.Fog.CaptureState();
            for(int n=0;n<20;n++)game.Hud.RefreshOrderSummary079(game.Controls.CaptureBuildPlacementPreview079());
            receipt.worldReadOnly=before==State(world);receipt.fogReadOnly=fog.SequenceEqual(world.Fog.CaptureState());
            Assert.IsTrue(receipt.worldReadOnly,"HUD reads must not change orders, routes, navigation, events, economy or fog");
            Assert.IsTrue(receipt.fogReadOnly,"Own route feedback must not reveal any map cell");
        }

        [UnityTest] public IEnumerator ActualGoalChangesRespectBridgeKnowledgeTargetModesAndOwnerPrivacy()
        {
            var receipt=new Receipt{sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),scope="Actual right-click ramp goal, cross-river replacement, A+left-click replacement, unexplored/explored bridge knowledge and foreign selection; read-only HUD"};
            try
            {
                yield return River();var world=game.World;var span=world.Layout.Bridges[0];
                var actor=SpawnHere("hussar",0,new Vector2(44,38));
                world.Fog.RestoreState(new int[world.Fog.CaptureState().Length]);world.Fog.Tick(world.Entities);
                Assert.AreEqual(VisibilityState.Unexplored,world.Fog.At(0,span.Center));
                yield return SelectByClick(actor);
                world.Fog.Reveal(0,span.Center,1);world.Fog.Tick(world.Entities);
                Assert.AreEqual(VisibilityState.Explored,world.Fog.At(0,span.Center));
                Vector2 rampGoal=span.Point(-span.HalfLength-span.RampLength+2,-2);
                yield return GroundClick(rampGoal,MouseButton.Right);Assert.AreEqual(OrderKind.Move,actor.orders.Peek().kind);
                Assert.Less(Vector2.Distance(rampGoal,actor.orders.Peek().point),.15f);world.Tick(.1f);yield return Ready();
                StringAssert.Contains("MOVE TO "+Point(actor.orders.Peek().point),game.Hud.OrderSummary);
                receipt.rampOnlyHidden=!game.Hud.OrderSummary.Contains(" AHEAD");Assert.IsTrue(receipt.rampOnlyHidden,"A route ending on an incoming ramp never enters the bridge deck");

                world.Fog.RestoreState(new int[world.Fog.CaptureState().Length]);world.Fog.Tick(world.Entities);
                Assert.AreEqual(VisibilityState.Unexplored,world.Fog.At(0,span.Center));
                Vector2 crossingGoal=span.Point(20,-2);yield return GroundClick(crossingGoal,MouseButton.Right);
                Assert.AreEqual(1,actor.orders.Count);Assert.Less(Vector2.Distance(crossingGoal,actor.orders.Peek().point),.15f);
                Assert.AreEqual(0,actor.path.Count,"Changing the public destination invalidates the previous route");
                world.Tick(.1f);yield return Ready();Assert.IsNotEmpty(actor.path);Assert.IsFalse(span.Approach(actor.position));
                Assert.AreEqual(VisibilityState.Unexplored,world.Fog.At(0,span.Center));
                receipt.unknownBridgeHidden=!game.Hud.OrderSummary.Contains("BRIDGE");Assert.IsTrue(receipt.unknownBridgeHidden,"Owned intent must not disclose an unexplored bridge");
                world.Fog.Reveal(0,span.Center,1);world.Fog.Tick(world.Entities);
                Assert.AreEqual(VisibilityState.Explored,world.Fog.At(0,span.Center),"The future crossing is explored rather than currently visible");
                yield return Ready();receipt.initialSummary=game.Hud.OrderSummary;
                StringAssert.Contains("MOVE TO "+Point(actor.orders.Peek().point),receipt.initialSummary);StringAssert.Contains("BRIDGE 1 AHEAD",receipt.initialSummary);
                AssertReadOnly(receipt);

                yield return Press(Key.A);Assert.IsTrue(game.Controls.AttackMode);StringAssert.Contains("CHOOSE DESTINATION",game.Hud.OrderSummary);
                Vector2 replacement=new Vector2(40,58);yield return GroundClick(replacement,MouseButton.Left);
                Assert.IsFalse(game.Controls.AttackMode);Assert.AreEqual(1,actor.orders.Count);Assert.AreEqual(OrderKind.AttackMove,actor.orders.Peek().kind);
                Assert.Less(Vector2.Distance(replacement,actor.orders.Peek().point),.15f);world.Tick(.1f);yield return Ready();
                receipt.changedSummary=game.Hud.OrderSummary;StringAssert.Contains("ATTACK MOVE TO "+Point(actor.orders.Peek().point),receipt.changedSummary);
                StringAssert.DoesNotContain(Point(crossingGoal),receipt.changedSummary);StringAssert.DoesNotContain("BRIDGE 1 AHEAD",receipt.changedSummary);
                game.Controls.BeginBuild("house");yield return Ready();
                Assert.AreEqual(game.Controls.CaptureBuildPlacementPreview079().Label,game.Hud.OrderSummary,"Placement feedback keeps priority over movement feedback");
                game.Controls.CancelTargetMode();game.Autoplay=true;yield return Ready();StringAssert.Contains("AI OBSERVATION",game.Hud.OrderSummary);game.Autoplay=false;

                // World.Spawn deliberately does not reveal enemies to the local
                // observer, unlike this fixture's convenience SpawnHere helper.
                var hidden=world.Spawn("hussar",1,new Vector2(108,108));
                Assert.IsFalse(world.IsVisible(0,hidden));game.Controls.Select(hidden);yield return Ready();Assert.IsNull(game.Controls.FirstSelected);
                StringAssert.DoesNotContain(" TO ",game.Hud.OrderSummary);
                var visible=world.Spawn("hussar",1,actor.position+new Vector2(0,10));
                Assert.IsTrue(world.IsVisible(0,visible));
                Assert.IsTrue(world.Issue(new GameCommand{player=1,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{visible.id},point=new Vector2(111,111)}).accepted);
                game.Controls.Select(visible);yield return Ready();Assert.AreEqual(visible.id,game.Controls.FirstSelected?.id);
                StringAssert.DoesNotContain(" TO ",game.Hud.OrderSummary);StringAssert.DoesNotContain("111",game.Hud.OrderSummary);
                receipt.ownerPrivacy=true;AssertReadOnly(receipt);receipt.status="passed";
            }
            finally{game.Autoplay=false;Save("movement-feedback084-goals-privacy",receipt);}
        }

        [UnityTest] public IEnumerator ActualPhysicalFriendlyWaitClearsAfterTheLeaderOpensSpaceAndColdStateMatches()
        {
            var receipt=new Receipt{sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),scope="Actual follower-first public bridge convoy: .001 warm-up, native follower Move, .1 zero wait and .01 real opening; full-state cold and hard physical sweeps"};
            try
            {
                yield return River();var world=game.World;var span=world.Layout.Bridges[0];Vector2 forward=span.Direction;
                Vector2 leaderStart=span.Point(-4,-2);float radii=game.Rules.Entity("great_bombard").radius+game.Rules.Entity("hussar").radius;
                var follower=SpawnHere("hussar",0,leaderStart-forward*(radii+.02f));var leader=SpawnHere("great_bombard",0,leaderStart);
                Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Hold,actors=new[]{follower.id}}).accepted);
                Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{leader.id},point=span.Point(20,-2)}).accepted);
                world.Tick(.001f);Assert.IsNotEmpty(leader.path);yield return SelectByClick(follower);
                Vector2 goal=span.Point(22,-2);yield return GroundClick(goal,MouseButton.Right);
                Assert.AreEqual(OrderKind.Move,follower.orders.Peek().kind);Assert.Less(Vector2.Distance(goal,follower.orders.Peek().point),.15f);
                var cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));Assert.AreEqual(State(world),State(cold));
                Vector2 before=follower.position,leaderBefore=leader.position;world.Tick(.1f);cold.Tick(.1f);
                receipt.zeroWaitDisplacement=Vector2.Distance(before,follower.position);Assert.LessOrEqual(receipt.zeroWaitDisplacement,.00002f,"The earlier follower must wait for the real, later-processed leader");
                receipt.coldEqual=State(world)==State(cold);Assert.IsTrue(receipt.coldEqual);Assert.AreEqual(1,follower.orders.Count);
                Assert.GreaterOrEqual(Vector2.Distance(follower.position,leaderBefore),radii-.00001f);
                Assert.IsTrue(world.Navigation.ClearLine(leaderBefore,leader.position,leader.def.radius));
                yield return Ready();receipt.waitingSummary=game.Hud.OrderSummary;
                StringAssert.Contains("MOVE TO "+Point(follower.orders.Peek().point),receipt.waitingSummary);StringAssert.Contains("BRIDGE 1",receipt.waitingSummary);
                StringAssert.Contains("YIELDING: FRIENDLY TRAFFIC",receipt.waitingSummary);StringAssert.DoesNotContain("MOVING",receipt.waitingSummary);AssertReadOnly(receipt);

                // A cold reconstruction carries previousPosition and the current
                // route. HUD observation adds no history needed for continuation.
                cold=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));before=follower.position;leaderBefore=leader.position;
                world.Tick(.01f);cold.Tick(.01f);receipt.subsequentAdvance=Vector2.Dot(follower.position-before,forward);
                Assert.Greater(receipt.subsequentAdvance,.001f,"The leader's real move must open the next Tick's gap");
                Assert.LessOrEqual(Vector2.Distance(follower.position,before),world.Speed(follower)*.01f+.0001f);
                Assert.IsTrue(world.Navigation.ClearLine(before,follower.position,follower.def.radius));
                Assert.GreaterOrEqual(SegmentDistance(before,follower.position,leaderBefore),radii-.00001f);
                receipt.coldEqual&=State(world)==State(cold);Assert.IsTrue(receipt.coldEqual);yield return Ready();receipt.movingSummary=game.Hud.OrderSummary;
                StringAssert.Contains("MOVING",receipt.movingSummary);StringAssert.DoesNotContain("YIELDING",receipt.movingSummary);AssertReadOnly(receipt);receipt.status="passed";
            }
            finally{Save("movement-feedback084-physical-wait-cold",receipt);}
        }
        static float SegmentDistance(Vector2 start,Vector2 end,Vector2 point)
        {Vector2 delta=end-start;float at=delta.sqrMagnitude>1e-12f?Mathf.Clamp01(Vector2.Dot(point-start,delta)/delta.sqrMagnitude):0;return Vector2.Distance(start+delta*at,point);}
    }
}
