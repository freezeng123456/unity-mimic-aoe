using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class AiConstructionTests
    {
        World world;EntityState builder,foundation;
        void PaidFoundation(string definition)
        {
            world=new World(Catalog.Load(),4701,"coast","coast",5);world.Players[0].age=3;
            builder=world.Entities.First(e=>e.owner==0&&e.Worker);
            var cost=world.EntityCost(0,world.Rules.Entity(definition));world.Players[0].resources=cost;
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{builder.id},item=definition,point=new Vector2(36,28)});
            Assert.IsTrue(result.accepted,result.reason);
            foundation=world.Entities.Single(e=>e.owner==0&&e.def.id==definition);
            Assert.IsFalse(foundation.Complete);Assert.AreEqual(cost.wood,world.Players[0].spent.wood,.001f);
            Assert.AreEqual(0,world.Players[0].resources.wood,.001f);
        }
        List<EntityState> Workers()=>world.Entities.Where(e=>e.Alive&&e.owner==0&&e.Worker).ToList();
        int Builders()=>Workers().Count(e=>e.garrisonedIn==0&&e.orders.Any(o=>o.kind==OrderKind.Build&&o.target==foundation.id));

        [TestCase("farm","berry")]
        [TestCase("estate","mine")]
        public void WorkersGatherFromAvailableSourceWhilePaidEconomicBuildingIsUnfinished(string building,string resource)
        {
            PaidFoundation(building);
            foreach(var e in world.Entities.Where(e=>e.def.kind==EntityKind.Resource).ToArray())world.Remove(e);
            var ready=world.Spawn(resource,-1,new Vector2(45,28));world.Fog.Reveal(0,ready.position,25);
            var ai=new AiWorkforce(world,0);
            ai.Assign(Workers(),building=="farm"?new Cost(1000,0,0):new Cost(0,0,1000),new List<EntityState>());
            var collectors=Workers().Where(e=>e.id!=builder.id).ToArray();
            Assert.IsTrue(collectors.All(e=>e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Gather&&e.orders.Peek().target==ready.id),"An unfinished economy building must not attract gatherers away from a working source");
            Assert.AreEqual(1,Builders(),"The real construction worker must keep working");
            for(int i=0;i<300;i++)world.Tick(.1f);world.FlushEconomyLog();
            Assert.IsTrue(world.Events.Any(e=>e.kind=="Gather"&&collectors.Any(v=>v.id==e.entity)&&e.target==ready.id&&e.amount>0));
        }

        [Test] public void LostBuilderIsReplacedAtOriginalPaidFoundationAndConstructionFinishes()
        {
            PaidFoundation("farm");for(int i=0;i<100;i++)world.Tick(.1f);
            Assert.Greater(foundation.construction,0);Assert.IsFalse(foundation.Complete);
            world.Remove(builder,1);Assert.AreEqual(0,Builders());var paid=world.Players[0].spent;
            int payments=world.Events.Count(e=>e.kind=="Payment");new SkirmishAI(world,0).Tick(.1f);
            Assert.AreEqual(1,Builders(),"A surviving real villager must resume the existing unfinished foundation");
            Assert.AreEqual(payments,world.Events.Count(e=>e.kind=="Payment"),"Resuming construction cannot pay for a new foundation");
            Assert.AreEqual(1,world.Entities.Count(e=>e.owner==0&&e.def.id=="farm"));
            for(int i=0;i<1000&&!foundation.Complete;i++)world.Tick(.1f);
            Assert.IsTrue(foundation.Complete,"Accepted Build must lead to real construction completion");
            Assert.AreEqual(paid.wood,world.Players[0].spent.wood,.001f);
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="BuildingComplete"&&e.entity==foundation.id));
        }

        [Test] public void ActiveConstructionKeepsItsBuilderWithoutDuplicatePaymentOrReassignment()
        {
            PaidFoundation("farm");int payments=world.Events.Count(e=>e.kind=="Payment");
            new SkirmishAI(world,0).Tick(.1f);
            Assert.AreEqual(1,Builders());Assert.AreEqual(OrderKind.Build,builder.orders.Peek().kind);
            Assert.AreEqual(foundation.id,builder.orders.Peek().target);
            Assert.AreEqual(payments,world.Events.Count(e=>e.kind=="Payment"));
            Assert.IsFalse(world.Events.Any(e=>e.kind=="Command"&&e.target==foundation.id&&e.message.StartsWith("Build")),"Existing construction must not be interrupted with redundant orders");
        }

        [Test] public void VisibleRaidPreventsSendingReplacementBuilderIntoDanger()
        {
            PaidFoundation("farm");world.Remove(builder,1);
            var raider=world.Spawn("musketeer",1,new Vector2(40,28));world.Fog.Tick(world.Entities);Assert.IsTrue(world.IsVisible(0,raider));
            new SkirmishAI(world,0).Tick(.1f);
            Assert.AreEqual(0,Builders());Assert.IsFalse(foundation.Complete);
            Assert.IsFalse(world.Events.Any(e=>e.kind=="Command"&&e.target==foundation.id&&e.message.StartsWith("Build")));
        }
    }
}
