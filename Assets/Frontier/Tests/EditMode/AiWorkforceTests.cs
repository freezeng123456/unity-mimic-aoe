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
            var w=Opening();var workers=w.Entities.Where(e=>e.owner==0&&e.Worker).ToList();
            var enemy=w.Spawn("musketeer",1,new Vector2(33,20));w.Fog.Reveal(0,enemy.position,5);
            w.Players[0].resources=new Cost(0,1000,1000);
            new AiWorkforce(w,0).Assign(workers,new Cost(800,0,0),new List<EntityState>{enemy});
            foreach(var worker in workers)
            {
                if(worker.orders.Count==0)continue;var target=w.Get(worker.orders.Peek().target);
                Assert.GreaterOrEqual(Vector2.Distance(target.position,enemy.position),Mathf.Max(12,enemy.def.range+4));
                Assert.IsTrue(w.IsVisible(0,target));
            }
        }
    }
}
