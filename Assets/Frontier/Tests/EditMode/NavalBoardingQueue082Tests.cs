using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Isolated physical boarding fixtures. Ships and bodies are explicit fixture
    // inputs; existing NavalTransport065Tests retain the paid production chain.
    public sealed class NavalBoardingQueue082Tests
    {
        static readonly Vector2 Shore=new Vector2(55,64);
        static World Arena(out EntityState ship)
        {
            var w=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.River);
            foreach(var e in w.Entities.Where(e=>e.def.id!="town").ToArray())w.Remove(e);
            ship=w.Spawn("transport_ship",0,new Vector2(64,20));
            Assert.IsTrue(w.WaterNavigation.IsWalkable(ship.position,ship.def.radius));
            Assert.IsTrue(w.TryTransportShore(ship,Shore,.4f,out var shore,out var berth));
            Assert.AreEqual(Shore,shore);Assert.Greater(Vector2.Distance(ship.position,berth),20);
            return w;
        }
        static string State(World w)=>JsonUtility.ToJson(w.CaptureState());
        static World Cold(World w)=>World.Restore(w.Rules,SnapshotCodec.Clone(w.CaptureState()));
        static void Embark(World w,EntityState ship,params EntityState[] group)
        {
            foreach(var e in group)Assert.IsTrue(w.Navigation.IsWalkable(e.position,e.def.radius));
            var result=w.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=group.Select(e=>e.id).ToArray(),target=ship.id,point=Shore});
            Assert.IsTrue(result.accepted,result.reason);Assert.AreEqual(Shore,ship.orders.Peek().point);
            foreach(var e in group)Assert.AreEqual(0,e.garrisonedIn,"The public order must not board remotely");
        }
        static float SegmentDistance(Vector2 a,Vector2 b,Vector2 p)
        {var d=b-a;float t=d.sqrMagnitude>1e-12f?Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude):0;return Vector2.Distance(a+d*t,p);}
        static void Step(World w,World cold,EntityState ship,params EntityState[] group)
        {
            var before=group.Select(e=>e.position).ToArray();var aboard=group.Select(e=>e.garrisonedIn).ToArray();
            var berth=ship.orders.Count>0?ship.orders.Peek():null;
            w.Tick(.1f);cold.Tick(.1f);Assert.AreEqual(State(w),State(cold),"Queue head and saved waiting slots must survive cold restore exactly");
            for(int i=0;i<group.Length;i++)
            {
                var e=group[i];Assert.IsTrue(e.Alive);
                if(aboard[i]!=0)continue;
                if(e.garrisonedIn!=0)
                {
                    Assert.AreEqual(ship.id,e.garrisonedIn);Assert.IsNotNull(berth);
                    Assert.LessOrEqual(Vector2.Distance(before[i],Shore),w.Speed(e)*.1f+.35f+.0001f,"Entry must be reachable within the original .35m threshold and this tick's speed");
                    Assert.LessOrEqual(Vector2.Distance(ship.position,berth.resolvedPoint),.251f,"The actual vessel must be berthed before entry");
                    continue;
                }
                Assert.IsTrue(w.Navigation.IsWalkable(e.position,e.def.radius));Assert.IsTrue(w.Navigation.ClearLine(before[i],e.position,e.def.radius));
                Assert.LessOrEqual(Vector2.Distance(before[i],e.position),w.Speed(e)*.1f+.0001f);
                for(int j=0;j<group.Length;j++)
                {
                    if(i==j||aboard[j]!=0)continue;
                    // Earlier actors expose their committed position (or are
                    // already cargo); later actors still expose their old body,
                    // even if they subsequently board during this same tick.
                    var other=group[j];if(other.id<e.id&&other.garrisonedIn!=0)continue;
                    var obstacle=other.id<e.id?other.position:before[j];
                    Assert.GreaterOrEqual(SegmentDistance(before[i],e.position,obstacle),e.def.radius+other.def.radius-.00001f,"No swept body penetration while waiting or stepping aside");
                }
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void AlreadyNearTheSameBoardingPointStepAsideAndBoardWithoutBodyOverlapAfterColdRestore(bool reverseSpawn)
        {
            var w=Arena(out var ship);var north=Shore+Vector2.up*.42f;var south=Shore-Vector2.up*.43f;
            var first=w.Spawn("musketeer",0,reverseSpawn?south:north);var second=w.Spawn("musketeer",0,reverseSpawn?north:south);
            Assert.Greater(Vector2.Distance(first.position,second.position),first.def.radius+second.def.radius);
            Assert.Greater(Vector2.Distance(first.position,Shore),.35f);Assert.Greater(Vector2.Distance(second.position,Shore),.35f);
            Embark(w,ship,second,first);var head=reverseSpawn?second:first;var follower=reverseSpawn?first:second;
            var mirror=Cold(w);Step(w,mirror,ship,first,second);
            Assert.AreEqual(head.id,ship.orders.Peek().target,"The closest body gets one saved boarding turn independent of selection order");
            Assert.IsTrue(follower.orders.Peek().hasResolvedPoint,"An already near-shore follower must have a real retreat destination");
            Assert.Greater(Vector2.Distance(follower.orders.Peek().resolvedPoint,Shore),1.5f);
            mirror=Cold(w);float peakRetreat=Vector2.Distance(follower.position,Shore);
            for(int i=1;i<900&&(first.garrisonedIn==0||second.garrisonedIn==0);i++)
            {
                Step(w,mirror,ship,first,second);
                if(follower.garrisonedIn==0)peakRetreat=Mathf.Max(peakRetreat,Vector2.Distance(follower.position,Shore));
                if(head.garrisonedIn==0)Assert.AreEqual(head.id,ship.orders.Peek().target,"A moving follower must not preempt the saved queue head");
            }
            Assert.Greater(peakRetreat,1,"The follower must actually walk away from the shared aperture");
            Assert.AreEqual(ship.id,first.garrisonedIn);Assert.AreEqual(ship.id,second.garrisonedIn);
            foreach(var e in new[]{first,second})Assert.AreEqual(1,w.Events.Count(v=>v.kind=="Embarked"&&v.entity==e.id&&v.target==ship.id));
        }
        [Test] public void StoppingTheSavedHeadAdvancesTheQueueAndDoesNotBoardTheCancelledBodyAfterColdRestore()
        {
            var w=Arena(out var ship);var first=w.Spawn("musketeer",0,new Vector2(49,62));var second=w.Spawn("musketeer",0,new Vector2(48,66));
            Embark(w,ship,first,second);var mirror=Cold(w);Step(w,mirror,ship,first,second);
            Assert.AreEqual(first.id,ship.orders.Peek().target);Assert.AreEqual(0,first.garrisonedIn);
            var stop=new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{first.id},order=OrderKind.Stop};Assert.IsTrue(w.Issue(stop).accepted);
            var parked=first.position;mirror=Cold(w);Step(w,mirror,ship,first,second);
            Assert.AreEqual(second.id,ship.orders.Peek().target,"A cancelled head must release its saved turn");
            for(int i=1;i<900&&second.garrisonedIn==0;i++)Step(w,mirror,ship,first,second);
            Assert.AreEqual(ship.id,second.garrisonedIn);Assert.AreEqual(0,first.garrisonedIn);Assert.AreEqual(parked,first.position);
            Assert.AreEqual(0,w.Events.Count(v=>v.kind=="Embarked"&&v.entity==first.id));
            Assert.AreEqual(1,w.Events.Count(v=>v.kind=="Embarked"&&v.entity==second.id));
        }
    }
}
