using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class DestinationLifecycleTests
    {
        [TestCase(false)][TestCase(true)]
        public void PendingIntentReturnsToOriginalPointAfterFoundationIsRemoved(bool cancel)
        {
            var w=new World(Catalog.Load(),5102,"coast","coast",5);
            foreach(var e in w.Entities.Where(World.Guardian).ToArray())w.Remove(e);
            w.Players[0].resources=new Cost(1000,2000,1000);
            var first=new Vector2(52,60);var unit=w.Spawn("pike",0,new Vector2(36,60));
            var builder=w.Spawn("villager",0,first+Vector2.up*3);w.Fog.Tick(w.Entities);
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Move,point=first}).accepted);
            var intent=unit.orders.Peek();
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{builder.id},item="house",point=first}).accepted);
            var house=w.Get(builder.orders.Peek().target);w.Tick(.1f);
            Assert.IsTrue(intent.hasResolvedPoint);Assert.AreEqual(first,intent.point);
            int version=w.Navigation.Version;
            if(cancel)Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.CancelBuilding,actors=new[]{house.id}}).accepted);
            else w.ApplyDamage(house,100000,DamageKind.Siege,1);
            Assert.Greater(w.Navigation.Version,version);Assert.IsTrue(w.Navigation.IsWalkable(first,unit.def.radius));
            for(int i=0;i<200;i++)
            {
                var before=unit.position;w.Tick(.1f);
                Assert.LessOrEqual(Vector2.Distance(before,unit.position),w.Speed(unit)*.1f+.001f);
                Assert.IsTrue(w.Navigation.IsWalkable(unit.position,unit.def.radius));
                Assert.AreEqual(first,intent.point);
            }
            Assert.IsFalse(intent.hasResolvedPoint);Assert.AreEqual(0,unit.orders.Count);
            Assert.Less(Vector2.Distance(unit.position,first),.3f);
        }
    }
}
