using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class BridgeFollowingEligibility084Tests
    {
        [Test] public void FreshPublicBridgeMovesDistinguishThroughFlowExactDeckGoalsAndPendingLaneCorrectionWithoutMutation()
        {
            var method=typeof(World).GetMethod("SteeringThroughCorridor084",BindingFlags.Instance|BindingFlags.NonPublic);Assert.NotNull(method);
            foreach(int sign in new[]{-1,1})foreach(string scenario in new[]{"through","exact-deck-goal","wrong-half"})
            {
                var world=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.River);
                foreach(var e in world.Entities.Where(e=>e.def.id!="town").ToArray())world.Remove(e);
                var span=world.Layout.Bridges[0];float side=scenario=="wrong-half"?sign*2:-sign*2;
                var actor=world.Spawn("hussar",0,span.Point(-sign*4,side));
                Vector2 goal=span.Point(sign*(scenario=="exact-deck-goal"?4:20),side);
                var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{actor.id},point=goal});Assert.IsTrue(result.accepted,result.reason);
                Assert.AreEqual(goal,actor.orders.Peek().point);Assert.IsEmpty(actor.path);Assert.AreEqual(0,actor.orders.Peek().bridgeTransitIndex,"New public intention has no inherited transit");
                Assert.IsTrue(world.Navigation.ClearLine(actor.position,goal,actor.def.radius),"Fixture must isolate through-flow eligibility, not static route failure");
                string before=JsonUtility.ToJson(world.CaptureState());bool eligible=(bool)method.Invoke(world,new object[]{actor,span,(float)sign});
                Assert.AreEqual(scenario=="through",eligible,"direction="+sign+" scenario="+scenario);
                Assert.AreEqual(before,JsonUtility.ToJson(world.CaptureState()),"Eligibility sampling must not plan, commit, alter fog or mutate World");
            }
        }
    }
}
