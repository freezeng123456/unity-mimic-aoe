using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    public sealed class ProductionDeathPresentationTests : NativeInputFixture
    {
        private EntityState barracks;
        private PlayerState Player=>game.World.Players[0];
        private Text ResourceText()=>session.GetComponentsInChildren<Text>().First(t=>t.text.StartsWith("FOOD  "));
        private IEnumerator Prepare()
        {
            // Only runtime setup changes: retain the shipped unit/building definitions.
            game.World.Players[0].age=2;game.World.Players[0].resources=new Cost(1000,1000,1000);
            barracks=SpawnHere("barracks",0,new Vector2(60,60),true);game.Controls.Select(barracks);yield return Ready();
            var result=game.World.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{barracks.id},item="musketeer",count=5});Assert.IsTrue(result.accepted);
            Assert.AreEqual(5,barracks.queue.Single().count);Assert.AreEqual(5,Player.reservedPopulation);yield return Ready();
        }

        [UnityTest] public IEnumerator BarracksDeathImmediatelyClearsProductionAndReservedPopulation()
        {
            yield return Prepare();var capBefore=Player.populationCap;var paidBalance=Player.resources;
            game.World.ApplyDamage(barracks,100000,DamageKind.Siege,1);
            Assert.IsFalse(barracks.Alive);Assert.IsEmpty(barracks.queue);Assert.AreEqual(0,Player.reservedPopulation);
            Assert.AreEqual(capBefore,Player.populationCap,"Destroying a zero-capacity barracks must preserve the housing cap while clearing its reservation");
            Assert.AreEqual(paidBalance.food,Player.resources.food,"Enemy destruction does not refund the paid order");
            Assert.AreEqual(paidBalance.coin,Player.resources.coin);
        }

        [UnityTest] public IEnumerator BarracksDeathRefreshesHudWithoutReservedTrainCount()
        {
            yield return Prepare();var text=ResourceText();var before=text.text;StringAssert.Contains("POP  "+Player.usedPopulation+"+5/"+Player.populationCap,before);
            game.World.ApplyDamage(barracks,100000,DamageKind.Siege,1);
            for(int i=0;i<8;i++)
            {
                yield return null;
                StringAssert.Contains("POP  "+Player.usedPopulation+"+0/"+Player.populationCap,text.text,"Actual HUD text must clear the reservation on frame "+i);
                Assert.AreEqual(0,Player.reservedPopulation);Assert.IsEmpty(barracks.queue);
            }
        }
    }
}
