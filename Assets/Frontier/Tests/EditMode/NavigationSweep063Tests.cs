using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class NavigationSweep063Tests
    {
        [Test] public void ClearLineRejectsCornerGrazeBetweenFormerSamplingPositions()
        {
            var navigation=new GridNavigation();navigation.SetTerrainBlocked(new Vector2(64,64),0,true);
            Vector2 direction=new Vector2(1,-1).normalized,corner=new Vector2(63.5f,63.5f);
            Vector2 closest=corner-new Vector2(1,1).normalized*.3999f,start=closest-direction*4,end=closest+direction*4.08f;
            Assert.IsTrue(navigation.IsWalkable(start,.4f));Assert.IsTrue(navigation.IsWalkable(end,.4f));
            // The prior every-third-meter policy misses this short collision.
            int steps=Mathf.CeilToInt(Vector2.Distance(start,end)*3);
            for(int i=0;i<=steps;i++)Assert.IsTrue(navigation.IsWalkable(Vector2.Lerp(start,end,(float)i/steps),.4f));
            Assert.IsFalse(navigation.IsWalkable(closest,.4f));
            Assert.IsFalse(navigation.ClearLine(start,end,.4f));Assert.IsFalse(navigation.ClearLine(end,start,.4f));
        }
        [Test] public void ClearLineKeepsARealClearanceBeyondTheRoundedCellCorner()
        {
            var navigation=new GridNavigation();navigation.SetTerrainBlocked(new Vector2(64,64),0,true);
            Vector2 direction=new Vector2(1,-1).normalized,closest=new Vector2(63.5f,63.5f)-new Vector2(1,1).normalized*.41f;
            Assert.IsTrue(navigation.ClearLine(closest-direction*4,closest+direction*4.08f,.4f));
            Assert.IsTrue(navigation.ClearLine(closest+direction*4.08f,closest-direction*4,.4f));
        }
        [Test] public void SmoothedPathsDetourAroundAGrazedCornerAndRemainLegalThroughout()
        {
            var navigation=new GridNavigation();navigation.SetTerrainBlocked(new Vector2(64,64),0,true);
            Vector2 direction=new Vector2(1,-1).normalized,closest=new Vector2(63.5f,63.5f)-new Vector2(1,1).normalized*.3999f;
            var path=navigation.FindPath(closest-direction*4,closest+direction*4.08f,.4f);Assert.GreaterOrEqual(path.Count,3);
            for(int i=0;i<path.Count-1;i++)
            {
                Assert.IsTrue(navigation.ClearLine(path[i],path[i+1],.4f));
                int steps=Mathf.CeilToInt(Vector2.Distance(path[i],path[i+1])*40);
                for(int s=0;s<=steps;s++)Assert.IsTrue(navigation.IsWalkable(Vector2.Lerp(path[i],path[i+1],(float)s/steps),.4f));
            }
        }
        [Test] public void DynamicPathSegmentsRejectAGrazedAllyDiscBetweenSamplePositions()
        {
            var navigation=new GridNavigation();Vector2 start=new Vector2(20,20),end=new Vector2(28.08f,20),disc=new Vector2(24.1f,20.3999f);
            bool PointFree(Vector2 point)=>(point-disc).sqrMagnitude>=.4f*.4f;
            bool SegmentFree(Vector2 a,Vector2 b)=>SegmentDistance(a,b,disc)>=.4f;
            int samples=Mathf.CeilToInt(Vector2.Distance(start,end)*3);
            for(int i=0;i<=samples;i++)Assert.IsTrue(PointFree(Vector2.Lerp(start,end,(float)i/samples)),"Prior point sampling misses this ally graze");
            Assert.IsFalse(SegmentFree(start,end));
            var route=navigation.FindPath(start,end,.4f,PointFree,SegmentFree);Assert.GreaterOrEqual(route.Count,3);
            Assert.AreEqual(end,route[route.Count-1]);
            for(int i=1;i<route.Count;i++){Assert.IsTrue(navigation.ClearLine(route[i-1],route[i],.4f));Assert.IsTrue(SegmentFree(route[i-1],route[i]));}
        }
        [Test] public void DynamicEndpointConnectionsEscapeAnIsolatedNearestIntegerNode()
        {
            var navigation=new GridNavigation();Vector2 start=new Vector2(20.25f,20.25f),goal=new Vector2(25,20.1f);
            var discs=new[]{new Vector2(21,20),new Vector2(19,20),new Vector2(20,21),new Vector2(20,19)};
            bool PointFree(Vector2 point)=>discs.All(disc=>Vector2.Distance(point,disc)>=.4f);
            bool SegmentFree(Vector2 a,Vector2 b)=>discs.All(disc=>SegmentDistance(a,b,disc)>=.4f);
            Assert.IsTrue(PointFree(start));Assert.IsFalse(SegmentFree(start,goal));
            Assert.IsTrue(SegmentFree(start,new Vector2(21,21)),"The fractional start has a usable connection beyond the isolated nearest node");
            var route=navigation.FindPath(start,goal,.4f,PointFree,SegmentFree);Assert.GreaterOrEqual(route.Count,3);
            Assert.AreEqual(start,route[0]);Assert.AreEqual(goal,route[route.Count-1]);
            for(int i=1;i<route.Count;i++){Assert.IsTrue(navigation.ClearLine(route[i-1],route[i],.4f));Assert.IsTrue(SegmentFree(route[i-1],route[i]));}
        }
        [Test] public void CannonLeavesItsExtraSafetyBufferBesideACurvedBankWithoutBodyCollision()
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.Tributaries);
            foreach(var entity in world.Entities.Where(e=>e.def.id!="town").ToArray())world.Remove(entity);
            var cannon=world.Spawn("cannon",0,new Vector2(44.72f,77.51f));
            var pike=world.Spawn("pike",0,new Vector2(43.61f,77.19f));
            var hussar=world.Spawn("hussar",0,new Vector2(43.5f,80.5f));var parked=new[]{pike,hussar};
            Vector2 pikeStart=pike.position,hussarStart=hussar.position,goal=new Vector2(40.2f,83.8f);
            float physical=cannon.def.radius+pike.def.radius,distance=Vector2.Distance(cannon.position,pike.position);
            Assert.Greater(distance,physical);Assert.Less(distance,physical+.1f);
            world.Tick(.001f); // Populate the spatial index after spawning the fixture.
            var command=world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{cannon.id},point=goal});Assert.IsTrue(command.accepted,command.reason);
            // Exercise the recovery planner at the exact recorded fractional start.
            var recovery=typeof(World).GetMethod("TryAllyRecovery",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.IsTrue((bool)recovery.Invoke(world,new object[]{cannon,goal}),"A body-clear exit must survive the preferred safety buffer and connect to the grid");
            for(int i=1;i<cannon.path.Count;i++)
            {
                Assert.IsTrue(world.Navigation.ClearLine(cannon.path[i-1],cannon.path[i],cannon.def.radius));
                foreach(var ally in parked)Assert.GreaterOrEqual(SegmentDistance(cannon.path[i-1],cannon.path[i],ally.position),cannon.def.radius+ally.def.radius);
            }
            for(int step=0;step<600&&cannon.orders.Count>0;step++)
            {
                world.Tick(.1f);Assert.IsTrue(world.Navigation.ClearLine(cannon.previousPosition,cannon.position,cannon.def.radius));
                foreach(var ally in parked)Assert.GreaterOrEqual(SegmentDistance(cannon.previousPosition,cannon.position,ally.position),cannon.def.radius+ally.def.radius);
            }
            Assert.IsEmpty(cannon.orders);Assert.Less(Vector2.Distance(cannon.position,goal),.3f);
            Assert.AreEqual(pikeStart,pike.position);Assert.AreEqual(hussarStart,hussar.position);
        }
        [Test] public void ArrivalRemainsMovingUntilItsBodyIsSeparateFromAMovingAlly()
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.Frontier);
            foreach(var entity in world.Entities.Where(e=>e.def.id!="town").ToArray())world.Remove(entity);
            var arriving=world.Spawn("hussar",0,new Vector2(50,50));
            var leaving=world.Spawn("hussar",0,new Vector2(50.9f,50));
            var guard=world.Spawn("pike",0,new Vector2(45,50));
            var guardPosition=guard.position;float arrivingHp=arriving.hp,leavingHp=leaving.hp,guardHp=guard.hp;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{leaving.id},point=new Vector2(60,50)}).accepted);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{arriving.id},point=arriving.position}).accepted);
            world.Tick(.001f);
            Assert.Less(Vector2.Distance(arriving.position,leaving.position),arriving.def.radius+leaving.def.radius);
            Assert.IsNotEmpty(arriving.orders,"Arriving must not turn a moving contact into an overlapping stationary obstacle");
            Assert.AreEqual("Moving",arriving.status);
            for(int step=0;step<100&&arriving.orders.Count>0;step++)world.Tick(.1f);
            Assert.IsEmpty(arriving.orders);Assert.AreEqual("Idle",arriving.status);
            Assert.GreaterOrEqual(Vector2.Distance(arriving.position,leaving.position),arriving.def.radius+leaving.def.radius);
            Assert.AreEqual(new Vector2(50,50),arriving.position);Assert.AreEqual(guardPosition,guard.position);
            Assert.AreEqual(arrivingHp,arriving.hp);Assert.AreEqual(leavingHp,leaving.hp);Assert.AreEqual(guardHp,guard.hp);
        }
        [TestCase(false)] [TestCase(true)]
        public void DeferredFineRecoveryIsFairAndDropsChangedOrders(bool cancelEarlierRequest)
        {
            var world=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.Frontier);
            foreach(var entity in world.Entities.Where(e=>e.def.id!="town").ToArray())world.Remove(entity);
            world.Navigation.ConfigureTerrain(point=>point.y==54||point.y==59);
            var low=world.Spawn("pike",0,new Vector2(48,56.25f));var high=world.Spawn("pike",0,new Vector2(46,56.25f));
            var guards=new[]{world.Spawn("hussar",0,new Vector2(55,55.08f)),world.Spawn("hussar",0,new Vector2(55,57.42f))};
            var lowGoal=new Vector2(61,56.9f);var highGoal=new Vector2(63,56.9f);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{low.id},point=lowGoal}).accepted);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{high.id},point=highGoal}).accepted);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(World).GetMethod("RebuildSpatial",flags).Invoke(world,null);
            var recover=typeof(World).GetMethod("TryAllyRecovery",flags);
            int Attempts()=>(int)typeof(World).GetField("fineRecoveryAttempts",flags).GetValue(world);
            int CoarseAttempts()=>(int)typeof(World).GetField("allyRecoveryCoarseAttempts",flags).GetValue(world);
            bool Deferred()=>(bool)typeof(World).GetField("fineRecoveryDeferred",flags).GetValue(world);
            low.progressGoal=lowGoal;high.progressGoal=highGoal;
            low.progressAt=high.progressAt=low.recoveryProgressAt=high.recoveryProgressAt=-3;
            low.recoveryProgressPosition=low.position;high.recoveryProgressPosition=high.position;
            Assert.IsTrue((bool)recover.Invoke(world,new object[]{low,lowGoal}));Assert.AreEqual(1,Attempts());
            Assert.IsFalse((bool)recover.Invoke(world,new object[]{high,highGoal}));Assert.IsTrue(Deferred());
            Assert.IsFalse((bool)recover.Invoke(world,new object[]{low,lowGoal}));Assert.IsTrue(Deferred());Assert.AreEqual(1,Attempts());
            int coarse=CoarseAttempts();
            Assert.IsFalse((bool)recover.Invoke(world,new object[]{low,lowGoal}));Assert.IsTrue(Deferred());
            Assert.AreEqual(coarse,CoarseAttempts(),"A queued actor must wait before rebuilding or repeating its failed coarse search");
            Vector2 lowStart=low.position;
            if(cancelEarlierRequest)Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Stop,actors=new[]{high.id}}).accepted);
            world.Tick(.1f);Assert.AreEqual(2,Attempts(),"Only one fine query may execute in a Tick");
            Assert.AreEqual(coarse,CoarseAttempts(),"A reserved actor must rebuild current collision data and proceed directly to fine recovery");
            if(cancelEarlierRequest)
            {
                Assert.IsEmpty(high.orders);Assert.AreEqual("Moving",low.status,"A changed command must release its queued reservation");
            }
            else
            {
                Assert.IsTrue(high.pathAvoidsAllies,"The earlier high-ID request must run before a later low-ID request");
                Assert.AreEqual(lowStart,low.position);Assert.AreEqual(-3,low.recoveryProgressAt);Assert.AreEqual(0,low.detourUntil);
                world.Tick(.1f);Assert.AreEqual(3,Attempts());Assert.AreEqual("Moving",low.status);
                Assert.AreEqual(coarse,CoarseAttempts());
            }
            foreach(var actor in new[]{low,high})
            {
                Assert.IsTrue(world.Navigation.ClearLine(actor.previousPosition,actor.position,actor.def.radius));
                foreach(var guard in guards)Assert.GreaterOrEqual(SegmentDistance(actor.previousPosition,actor.position,guard.position),actor.def.radius+guard.def.radius);
            }
        }
        private static float SegmentDistance(Vector2 start,Vector2 end,Vector2 point)
        {
            Vector2 segment=end-start;float t=segment.sqrMagnitude>.000001f?Mathf.Clamp01(Vector2.Dot(point-start,segment)/segment.sqrMagnitude):0;
            return Vector2.Distance(start+segment*t,point);
        }
    }
}
