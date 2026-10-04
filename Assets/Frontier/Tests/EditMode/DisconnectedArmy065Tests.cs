using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class DisconnectedArmy065Tests
    {
        [Test] public void HumanAcrossStraitOrderWaitsForTransportAndCanBeReplacedByAReachableMove()
        {
            var world=new World(SnapshotCodec.Clone(Catalog.Load()),314159,"coast","coast",5,MapTopology.GeneratedStrait);
            Vector2 home=world.OpeningTownPosition(0),enemy=world.OpeningTownPosition(1);
            var definition=world.Rules.Entity("pike");
            // A controlled army isolates manual path commands; no AI or economic
            // opening success is inferred from these fixture units.
            var positions=world.Navigation.FormationSlots(home+new Vector2(8,9),128,1.6f)
                .Where(p=>world.Layout.InStartingRegion(0,p)&&world.Navigation.IsWalkable(p,definition.radius)
                    &&!world.Entities.Any(e=>e.Alive&&e.garrisonedIn==0&&(e.position-p).sqrMagnitude<Mathf.Pow(e.def.radius+definition.radius+.3f,2)))
                .Take(4).ToArray();
            Assert.AreEqual(4,positions.Length);
            var army=positions.Select(p=>world.Spawn("pike",0,p)).ToArray();
            var ids=army.Select(e=>e.id).ToArray();
            Assert.AreNotEqual(world.Layout.LandRegion(home),world.Layout.LandRegion(enemy));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=ids,order=OrderKind.Move,point=enemy}).accepted);
            for(int i=0;i<200;i++)world.Tick(.1f);
            for(int i=0;i<army.Length;i++)
            {
                Assert.Less(Vector2.Distance(positions[i],army[i].position),.001f,"A manual command cannot manufacture a land crossing");
                Assert.AreEqual(OrderKind.Move,army[i].orders.Peek().kind,"Keep the player's order available to stop or replace");
                Assert.AreEqual("Transport required",army[i].status);
            }
            var attempts=typeof(World).GetField("fineRecoveryAttempts",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.AreEqual(0,(int)attempts.GetValue(world),"Disconnected islands must not trigger expensive ally-corridor searches");
            Vector2 destination=world.Navigation.FormationSlots(home+new Vector2(15,12),128,1.6f)
                .First(p=>world.Layout.InStartingRegion(0,p)&&world.Navigation.IsWalkable(p,definition.radius)
                    &&positions.All(start=>(start-p).sqrMagnitude>100)
                    &&world.Navigation.FindPath(army[0].position,p,definition.radius).Count>1);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=ids,order=OrderKind.Move,point=destination}).accepted);
            for(int i=0;i<150;i++)world.Tick(.1f);
            for(int i=0;i<army.Length;i++)Assert.Greater(Vector2.Distance(positions[i],army[i].position),2,"A valid replacement command must still move each soldier");
        }
    }
}
