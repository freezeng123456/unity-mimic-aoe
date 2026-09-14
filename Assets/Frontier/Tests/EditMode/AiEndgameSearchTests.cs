using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // A staged endgame, not a standard-opening match: isolate a relocated
    // survivor while keeping real movement, fog, unit stats and victory rules.
    public sealed class AiEndgameSearchTests
    {
        sealed class Scenario
        {
            public World world;public EntityState survivor;public EntityState[] army;public MatchSimulation simulation;
        }
        Scenario Endgame(Vector2 retreat)
        {
            var w=new World(Catalog.Load(),4801,"coast","coast",3);
            var survivor=w.Entities.First(e=>e.owner==1&&e.Worker);
            foreach(var e in w.Entities.ToArray())
                if(e.def.kind==EntityKind.Resource||(e.def.tags&(Tags.Explorer|Tags.Guardian))!=0||e.owner==1&&e.id!=survivor.id)w.Remove(e);
            w.Players[0].age=2;w.Players[0].resources=new Cost();w.Players[1].resources=new Cost();
            Assert.IsTrue(w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{survivor.id},order=OrderKind.Move,point=retreat}).accepted);
            for(int i=0;i<500;i++)w.Tick(.1f);
            Assert.Less(Vector2.Distance(survivor.position,retreat),2,"The survivor must really move to its remote area");
            Assert.IsFalse(w.IsVisible(0,survivor));Assert.AreEqual(-1,w.Winner,"Destroying the first town cannot end the match");
            var army=Enumerable.Range(0,6).Select(i=>w.Spawn("musketeer",0,new Vector2(31+i%3*2,35+i/3*2))).ToArray();
            return new Scenario{world=w,survivor=survivor,army=army,simulation=new MatchSimulation(w,0)};
        }

        [TestCase(116,16)]
        [TestCase(16,116)]
        public void ArmySearchesBeyondEmptyEnemyBaseAndActuallyEliminatesRemoteSurvivor(float x,float y)
        {
            var s=Endgame(new Vector2(x,y));float began=s.world.Time;
            for(int i=0;i<9000&&s.world.Winner<0;i++)s.simulation.Step();
            Assert.AreEqual(0,s.world.Winner,"An empty enemy starting area must not trap the army forever while a remote survivor remains");
            Assert.IsFalse(s.survivor.Alive);Assert.IsTrue(s.world.Events.Any(e=>e.kind=="Damage"&&e.player==0&&e.target==s.survivor.id));
            Assert.AreEqual(1,s.world.Events.Count(e=>e.kind=="MatchEnded"));Assert.Greater(s.world.Time,began);
        }

        [Test] public void HiddenSurvivorPositionCannotChooseTheArmiesSearchOrders()
        {
            var a=Endgame(new Vector2(116,16));var b=Endgame(new Vector2(16,116));
            int compared=0;
            for(int i=0;i<1600;i++)
            {
                if(a.world.IsVisible(0,a.survivor)||b.world.IsVisible(0,b.survivor))break;
                a.simulation.Step();b.simulation.Step();
                if(a.world.IsVisible(0,a.survivor)||b.world.IsVisible(0,b.survivor))break;
                for(int j=0;j<a.army.Length;j++)
                {
                    Assert.Less(Vector2.Distance(a.army[j].position,b.army[j].position),.001f);
                    var first=a.army[j].orders.ToArray();var second=b.army[j].orders.ToArray();Assert.AreEqual(first.Length,second.Length);
                    for(int k=0;k<first.Length;k++){Assert.AreEqual(first[k].kind,second[k].kind);Assert.AreEqual(first[k].target,second[k].target);Assert.Less(Vector2.Distance(first[k].point,second[k].point),.001f);}
                }
                compared++;
            }
            Assert.Greater(compared,500,"Compare a substantial real march while both survivors remain unseen");
            Assert.IsTrue(a.world.Events.Any(e=>e.kind=="AISearchPoint")&&b.world.Events.Any(e=>e.kind=="AISearchPoint"),"Both armies must actually enter exploration beyond the empty enemy base");
        }
    }
}
