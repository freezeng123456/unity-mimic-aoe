using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public class EconomyTests
    {
        World w;
        [SetUp] public void SetUp(){w=new World(Catalog.Load(),314159,"coast","coast",3);}
        PlayerState P=>w.Players[0];
        EntityState E(string id,int owner=0,Vector2? at=null)=>w.Spawn(id,owner,at??new Vector2(40,40));
        [Test] public void GatherUsesInstanceRemainingNotDefinitionAmount(){var v=E("villager",0,new Vector2(40,40));var r=E("berry",-1,new Vector2(40.5f,40));w.Fog.Tick(w.Entities);float amount=r.remaining,defAmount=r.def.amount,food=P.resources.food;Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{v.id},order=OrderKind.Gather,target=r.id,point=r.position}).accepted);w.Tick(.1f);Assert.Less(r.remaining,amount);Assert.AreEqual(defAmount,r.def.amount);Assert.Greater(P.resources.food,food);}
        [Test] public void GatherFarAwayDoesNotCreditImmediately(){var v=E("villager",0,new Vector2(40,40));var r=E("berry",-1,new Vector2(60,60));w.Fog.Reveal(0,r.position,2);float food=P.resources.food;Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{v.id},order=OrderKind.Gather,target=r.id,point=r.position}).accepted);w.Tick(.1f);Assert.AreEqual(food,P.resources.food);}
        [Test] public void TrainBatchReservesAndCancelRefundsOnce(){var town=E("town",0,new Vector2(40,40));float food=P.resources.food;Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item="villager",count=1}).accepted);Assert.AreEqual(1,P.reservedPopulation);Assert.AreEqual(food-100,P.resources.food,0.01f);Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{town.id},index=0}).accepted);Assert.AreEqual(0,P.reservedPopulation);Assert.AreEqual(food,P.resources.food,0.01f);Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{town.id},index=0}).accepted);Assert.AreEqual(food,P.resources.food,0.01f);}
        [Test] public void PauseFreezesSimulationTime(){float t=w.Time;w.Paused=true;w.Tick(10);Assert.AreEqual(t,w.Time);}
        [Test] public void TechnologyApplicationIsIdempotent(){var u=E("musketeer");float hp=w.MaxHp(u),atk=w.Attack(u);w.ApplyTechnology(0,"masonry");w.ApplyTechnology(0,"masonry");Assert.AreEqual(hp,w.MaxHp(u),0.01f);Assert.AreEqual(atk,w.Attack(u),0.01f);}
    }
}
