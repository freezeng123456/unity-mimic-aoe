using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class CombatEventTests
    {
        [Test] public void ActualProjectileLogsTheImpactPointAndItsVisibilityAtThatTime()
        {
            var w=new World(Catalog.Load());var shooter=w.Spawn("musketeer",0,new Vector2(32,33));
            var target=w.Spawn("pike",1,new Vector2(42,33));w.Fog.Tick(w.Entities);
            Assert.IsTrue(w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{target.id},order=OrderKind.Hold}).accepted);
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{shooter.id},order=OrderKind.Attack,target=target.id}).accepted);
            for(int i=0;i<60&&!w.Events.Any(e=>e.kind=="Impact"&&e.entity==shooter.id);i++)w.Tick(.1f);
            var impact=w.Events.First(e=>e.kind=="Impact"&&e.entity==shooter.id);
            var damage=w.Events.First(e=>e.kind=="Damage"&&e.target==target.id&&e.entity==shooter.id);
            Assert.AreEqual(target.id,impact.target);Assert.Less(Vector2.Distance(impact.position,target.position),.01f);
            Assert.AreEqual(target.id,damage.target);Assert.Less(Vector2.Distance(damage.position,target.position),.01f);
            Assert.AreEqual(impact.visibilityMask,damage.visibilityMask);
            Assert.Greater(Vector2.Distance(impact.position,shooter.position),5);
            Assert.AreEqual(1,impact.visibilityMask&1);
            w.Fog.Tick(new System.Collections.Generic.List<EntityState>());
            Assert.AreEqual(1,impact.visibilityMask&1,"Past event visibility must not be recomputed after vision changes");
        }
    }
}
