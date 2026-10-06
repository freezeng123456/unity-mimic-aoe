using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class AiWorkforceTests
    {
        [Test] public void NoSafeResourceLeavesDangerousGatheringForShelter()
        {
            var w=Opening();var worker=w.Entities.First(e=>e.owner==0&&e.Worker);
            var food=w.Entities.First(e=>e.def.id=="berry"&&w.IsVisible(0,e));
            foreach(var e in w.Entities.Where(e=>e.def.kind==EntityKind.Resource&&e.id!=food.id).ToArray())w.Remove(e);
            var workers=new List<EntityState>{worker};Gather(w,workers,food);
            var enemy=w.Spawn("longbow",1,food.position+Vector2.right*3);w.Fog.Reveal(0,enemy.position,3);
            new AiWorkforce(w,0).Assign(workers,new Cost(800,0,0),new List<EntityState>{enemy});
            Assert.AreEqual(OrderKind.Garrison,worker.orders.Peek().kind);
            for(int i=0;i<100&&worker.garrisonedIn==0;i++)w.Tick(.1f);
            Assert.IsTrue(worker.Alive);Assert.AreNotEqual(0,worker.garrisonedIn);
            Assert.AreEqual(0,w.Players[0].gathered.food,"Unsafe collection is interrupted before any resource is awarded");
        }
        private static World Opening()
        {
            var w=new World(Catalog.Load(),42,"river","coast",5);
            foreach(var e in w.Entities.Where(e=>e.def.id.StartsWith("crate_")||(e.def.tags&Tags.Guardian)!=0).ToArray())w.Remove(e);
            return w;
        }
        private static void Gather(World w,List<EntityState> workers,EntityState resource)
        {
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=workers.Select(e=>e.id).ToArray(),order=OrderKind.Gather,target=resource.id}).accepted);
        }
        [Test] public void CoinSurplusIsReassignedToFoodAndProducesActualIncome()
        {
            var w=Opening();var p=w.Players[0];p.resources=new Cost(0,500,900);
            var workers=w.Entities.Where(e=>e.owner==0&&e.Worker).ToList();
            var mine=w.Entities.First(e=>e.def.id=="mine"&&w.IsVisible(0,e));Gather(w,workers,mine);
            var ai=new AiWorkforce(w,0);var target=new Cost(600,100,300);Cost before=p.resources;
            ai.Assign(workers,target,new List<EntityState>());
            Assert.AreEqual(before.food,p.resources.food);Assert.AreEqual(before.coin,p.resources.coin,"A forecast must not create or spend resources");
            for(int i=0;i<360;i++){w.Tick(.1f);ai.Assign(workers,target,new List<EntityState>());}
            w.FlushEconomyLog();
            Assert.Greater(workers.Count(e=>e.orders.Count>0&&w.Get(e.orders.Peek().target)?.def.resource==ResourceKind.Food),workers.Count/2);
            Assert.Greater(p.gathered.food,30);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Gather"&&workers.Any(v=>v.id==e.entity)&&e.message.Contains("Food")));
        }
        [Test] public void GarrisonedWorkersDoNotInflateTheAvailableFoodQuota()
        {
            var w=Opening();var p=w.Players[0];var workers=w.Entities.Where(e=>e.owner==0&&e.Worker).ToList();
            var town=w.Entities.First(e=>e.owner==0&&e.def.id=="town");
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=workers.Take(4).Select(e=>e.id).ToArray(),order=OrderKind.Garrison,target=town.id}).accepted);
            for(int i=0;i<150;i++)w.Tick(.1f);
            Assert.AreEqual(4,workers.Count(e=>e.garrisonedIn==town.id));
            var available=workers.Where(e=>e.garrisonedIn==0).ToList();var mine=w.Entities.First(e=>e.def.id=="mine"&&w.IsVisible(0,e));Gather(w,available,mine);
            p.resources=new Cost();
            float foodRate=w.Entities.Where(e=>e.def.resource==ResourceKind.Food&&w.IsVisible(0,e)).Max(e=>w.GatherRate(available[0],e));
            float coinRate=w.GatherRate(available[0],mine);
            new AiWorkforce(w,0).Assign(workers,new Cost(foodRate*100,0,coinRate*100),new List<EntityState>());
            Assert.AreEqual(1,available.Count(e=>w.Get(e.orders.Peek().target)?.def.resource==ResourceKind.Food));
            Assert.AreEqual(1,available.Count(e=>w.Get(e.orders.Peek().target)?.def.resource==ResourceKind.Coin));
            Assert.AreEqual(4,town.occupants.Count);
        }
        [Test] public void ResourceChoiceRejectsAVisibleRaidButCannotInspectHiddenRaiders()
        {
            void AssertLegalResponse(World world,EntityState worker,EntityState raider,int commandAt)
            {
                Assert.AreEqual(1,worker.orders.Count,"The available worker must receive a legal response in this resource-and-shelter fixture");
                var order=worker.orders.Peek();var target=world.Get(order.target);
                Assert.IsNotNull(target);Assert.IsTrue(target.Alive);Assert.IsTrue(world.IsVisible(0,target));
                if(order.kind==OrderKind.Gather)
                {
                    Assert.IsTrue(world.GatherableResource(0,target));Assert.IsTrue(world.CanGather(worker,target));
                    Assert.GreaterOrEqual(Vector2.Distance(target.position,raider.position),Mathf.Max(12,raider.def.range+4));
                }
                else if(order.kind==OrderKind.Garrison)
                {
                    Assert.AreEqual(0,target.owner);Assert.AreEqual(EntityKind.Building,target.def.kind);Assert.IsTrue(target.Complete);
                    Assert.Greater(target.def.garrison,target.occupants.Count,"The real shelter must have available capacity");
                }
                else Assert.Fail("Expected a safe Gather or legal Garrison response, got "+order.kind);
                Assert.IsTrue(world.Events.Skip(commandAt).Any(e=>e.kind=="Command"&&e.player==0&&e.entity==worker.id&&
                    e.target==target.id&&e.message.StartsWith(order.kind+" ×")&&e.amount==1),"The response must come from the actual accepted worker command");
            }
            var w=Opening();var workers=w.Entities.Where(e=>e.owner==0&&e.Worker).ToList();
            var enemy=w.Spawn("musketeer",1,new Vector2(33,20));w.Fog.Reveal(0,enemy.position,5);
            Assert.IsTrue(w.IsVisible(0,enemy));
            w.Players[0].resources=new Cost(0,1000,1000);
            int visibleCommandAt=w.Events.Count;
            new AiWorkforce(w,0).Assign(workers,new Cost(800,0,0),new List<EntityState>{enemy});
            foreach(var worker in workers)AssertLegalResponse(w,worker,enemy,visibleCommandAt);

            // A separate declared single-wood-source fixture uses real unit
            // vision. Passing an actually hidden raider must not exclude the
            // visible tree inside its potential danger envelope.
            var hidden=Opening();var settler=hidden.Entities.First(e=>e.owner==0&&e.Worker);
            foreach(var resource in hidden.Entities.Where(e=>e.def.resource==ResourceKind.Wood).ToArray())hidden.Remove(resource);
            var tree=hidden.Spawn("tree",-1,new Vector2(42,28));var raider=hidden.Spawn("musketeer",1,new Vector2(55,28));
            hidden.Fog.Tick(hidden.Entities);Assert.IsTrue(hidden.IsVisible(0,tree));Assert.IsFalse(hidden.IsVisible(0,raider));
            Assert.Less(Vector2.Distance(tree.position,raider.position),Mathf.Max(12,raider.def.range+4));
            var workforce=new AiWorkforce(hidden,0);var oneWorker=new List<EntityState>{settler};var possibleRaid=new List<EntityState>{raider};
            int hiddenCommandAt=hidden.Events.Count;workforce.Assign(oneWorker,new Cost(0,1000,0),possibleRaid);
            Assert.AreEqual(OrderKind.Gather,settler.orders.Peek().kind);Assert.AreEqual(tree.id,settler.orders.Peek().target);
            Assert.IsTrue(hidden.CanGather(settler,tree));Assert.IsTrue(workforce.HasSafeSource(ResourceKind.Wood));
            Assert.IsTrue(hidden.Events.Skip(hiddenCommandAt).Any(e=>e.kind=="Command"&&e.player==0&&e.entity==settler.id&&
                e.target==tree.id&&e.message.StartsWith("Gather ×")&&e.amount==1));
            hidden.Fog.Reveal(0,raider.position,2);Assert.IsTrue(hidden.IsVisible(0,raider));
            int revealedCommandAt=hidden.Events.Count;workforce.Assign(oneWorker,new Cost(0,1000,0),possibleRaid);
            Assert.IsFalse(workforce.HasSafeSource(ResourceKind.Wood));
            Assert.IsFalse(settler.orders.Any(o=>o.kind==OrderKind.Gather&&o.target==tree.id));
            AssertLegalResponse(hidden,settler,raider,revealedCommandAt);
        }
    }
}
