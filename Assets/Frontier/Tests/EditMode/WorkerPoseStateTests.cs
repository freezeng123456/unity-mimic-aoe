using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Controlled lifecycle fixtures: actual work must precede target invalidation.
    public sealed class WorkerPoseStateTests
    {
        World w; EntityState worker;
        [SetUp] public void SetUp()
        {
            w=new World(Catalog.Load(),5091,"coast","coast",5);
            foreach(var e in w.Entities.Where(e=>(e.def.tags&Tags.Guardian)!=0).ToArray())w.Remove(e);
            w.Players[0].resources=new Cost(1000,1000,1000);
            worker=w.Spawn("villager",0,new Vector2(33,28));w.Fog.Tick(w.Entities);
        }
        EntityState BeginConstruction()
        {
            var r=w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{worker.id},item="house",point=new Vector2(36,28)});
            Assert.IsTrue(r.accepted,r.reason);var b=w.Get(worker.orders.Peek().target);
            w.Tick(.1f);Assert.Greater(b.construction,0);Assert.Greater(w.Players[0].spent.wood,0);
            Assert.AreEqual(UnitPoseState.Build,UnitAnimation.Evaluate(worker,w.Time).state);return b;
        }
        [Test] public void DestroyedConstructionTargetClearsWorkPoseOnNextSimulationStep()
        {
            var b=BeginConstruction();w.Remove(b);w.Tick(.1f);
            Assert.AreEqual(0,worker.orders.Count);
            Assert.AreEqual(UnitPoseState.Idle,UnitAnimation.Evaluate(worker,w.Time).state,"A removed foundation cannot drive another work frame");
            Assert.AreNotEqual("Constructing",worker.status);
        }
        [Test] public void CompletedConstructionStopsWorkPoseWhenOrderFinishes()
        {
            var b=BeginConstruction();b.construction=.9999f;w.Tick(.1f);
            Assert.IsTrue(b.Complete);w.Tick(.1f);Assert.AreEqual(0,worker.orders.Count);
            Assert.AreEqual(UnitPoseState.Idle,UnitAnimation.Evaluate(worker,w.Time).state);
            Assert.AreNotEqual("Constructing",worker.status);
        }
        [Test] public void FullyRepairedBuildingStopsWorkPoseWhenOrderFinishes()
        {
            var b=w.Spawn("house",0,new Vector2(36,28));b.hp=w.MaxHp(b)-.5f;
            var r=w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Repair,target=b.id});
            Assert.IsTrue(r.accepted,r.reason);float wood=w.Players[0].resources.wood;
            w.Tick(.1f);Assert.AreEqual(w.MaxHp(b),b.hp);Assert.Less(w.Players[0].resources.wood,wood);
            Assert.AreEqual(UnitPoseState.Repair,UnitAnimation.Evaluate(worker,w.Time).state);
            w.Tick(.1f);Assert.AreEqual(0,worker.orders.Count);
            Assert.AreEqual(UnitPoseState.Idle,UnitAnimation.Evaluate(worker,w.Time).state);
            Assert.AreNotEqual("Repairing",worker.status);
        }
    }
}
