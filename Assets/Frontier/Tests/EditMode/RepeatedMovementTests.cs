using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class RepeatedMovementTests
    {
        World Create()
        {var w=new World(Catalog.Load(),5301,"coast","coast",5);foreach(var e in w.Entities.Where(World.Guardian).ToArray())w.Remove(e);return w;}
        void Order(World w,EntityState unit,OrderKind kind,Vector2 point,EntityState target=null)
        {var r=w.Issue(new GameCommand{player=unit.owner,kind=CommandKind.Order,actors=new[]{unit.id},order=kind,point=point,target=target?.id??0});Assert.IsTrue(r.accepted,r.reason);}
        [TestCase(OrderKind.Move)][TestCase(OrderKind.AttackMove)]
        public void RepeatingGroundDestinationDoesNotInterruptOpenGroundTravel(OrderKind kind)
        {
            var once=Create();var repeated=Create();var start=new Vector2(40,30);var goal=new Vector2(58,30);
            var a=once.Spawn("musketeer",0,start);var b=repeated.Spawn("musketeer",0,start);
            Order(once,a,kind,goal);Order(repeated,b,kind,goal);
            int pauses=0;
            for(int i=0;i<30;i++)
            {
                once.Tick(.1f);Order(repeated,b,kind,goal);var before=b.position;repeated.Tick(.1f);
                if(Vector2.Distance(b.position,before)<.01f)pauses++;
                Assert.IsTrue(repeated.Navigation.IsWalkable(b.position,b.def.radius));
                Assert.LessOrEqual(Vector2.Distance(b.position,before),repeated.Speed(b)*.1f+.001f);
            }
            float reference=Vector2.Distance(start,a.position),actual=Vector2.Distance(start,b.position);
            Assert.Greater(reference,10,"Fixture must permit continuous open-ground movement");
            Assert.GreaterOrEqual(actual,reference-.5f,$"Repeated order progress={actual}, single order={reference}, stopped ticks={pauses}/30");
            Assert.AreEqual(0,pauses,"Repeated destination confirmation must not cause stop-and-go travel");
        }
        [Test]public void SmallDestinationCorrectionMovesOnNextSimulationStep()
        {
            var w=Create();var gun=w.Spawn("musketeer",0,new Vector2(40,30));var first=new Vector2(58,30);
            Order(w,gun,OrderKind.Move,first);w.Tick(.1f);Assert.Greater(gun.pathRetryAt,w.Time);
            var before=gun.position;var corrected=first+Vector2.up*.75f;
            Order(w,gun,OrderKind.Move,corrected);Assert.AreEqual(corrected,gun.orders.Peek().point);w.Tick(.1f);
            Assert.Greater(Vector2.Distance(before,gun.position),.3f,"A nearby corrected destination must not inherit the previous route retry delay");
            Assert.Greater(gun.position.y,before.y);
        }
        [Test]public void RepeatedAttackOnMovingVisibleEnemyDoesNotThrottlePursuit()
        {
            var once=Create();var repeated=Create();var start=new Vector2(40,30);
            var a=once.Spawn("musketeer",0,start);var ta=once.Spawn("pike",1,new Vector2(55.5f,30));
            var b=repeated.Spawn("musketeer",0,start);var tb=repeated.Spawn("pike",1,new Vector2(55.5f,30));
            once.Spawn("villager",0,new Vector2(65,38));repeated.Spawn("villager",0,new Vector2(65,38));
            foreach(var pair in new[]{(once,a,ta),(repeated,b,tb)})
            {pair.Item1.Fog.Tick(pair.Item1.Entities);Order(pair.Item1,pair.Item3,OrderKind.Move,new Vector2(78,30));Order(pair.Item1,pair.Item2,OrderKind.Attack,pair.Item3.position,pair.Item3);}
            for(int i=0;i<15;i++)
            {
                once.Tick(.1f);Assert.IsTrue(repeated.IsVisible(0,tb),"A hidden target cannot be used for this response test");
                Order(repeated,b,OrderKind.Attack,tb.position,tb);var before=b.position;repeated.Tick(.1f);
                Assert.LessOrEqual(Vector2.Distance(before,b.position),repeated.Speed(b)*.1f+.001f);
            }
            Assert.Greater(ta.position.x,60,"Target must be moving through its actual order");Assert.Greater(a.position.x,45);
            Assert.GreaterOrEqual(b.position.x,a.position.x-.5f,$"Repeated pursuit={b.position}, single pursuit={a.position}");
        }
    }
}
