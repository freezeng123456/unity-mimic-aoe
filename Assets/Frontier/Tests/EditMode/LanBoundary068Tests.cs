using System;
using System.Linq;
using NUnit.Framework;

namespace Frontier.Tests
{
    public sealed class LanBoundary068Tests
    {
        [Test] public void PauseAtTheNextBoundaryStopsCatchupAndResumeDoesNotBankPausedTime()
        {
            var world=new World(Catalog.Load(),314159,new[]{"coast","river","france","ottoman"},new[]{0,1,0,1});
            var simulation=new MatchSimulation(world);
            var authority=new LanAuthority(world,"match","epoch",world.Issue,(p,r)=>world.Paused=p);
            authority.BindPeer("host",0,1);
            simulation.BeforeStep+=()=>authority.Drain(simulation.StepCount,1);
            authority.Submit(new LanCommandRequest{matchId="match",epoch="epoch",peerId="host",sequence=1,generation=1,operation=LanOperation.Pause});
            Assert.AreEqual(0,simulation.AdvanceFrame(.8f));Assert.IsTrue(world.Paused);Assert.AreEqual(0,simulation.StepCount);
            Assert.AreEqual(0,simulation.AdvanceFrame(2));
            authority.Submit(new LanCommandRequest{matchId="match",epoch="epoch",peerId="host",sequence=2,generation=1,operation=LanOperation.Resume});
            Assert.IsTrue(authority.Drain(0,1).Single().accepted);
            Assert.AreEqual(1,simulation.AdvanceFrame(.1f));Assert.AreEqual(1,simulation.StepCount);
        }
        [Test] public void WorkQueuedAfterOneSettledStepAppliesBeforeTheNextStepInTheSameFrame()
        {
            var world=new World(Catalog.Load(),314159,new[]{"coast","river","france","ottoman"},new[]{0,1,0,1});
            var simulation=new MatchSimulation(world,new[]{1,3});int issuedAt=-1;
            var authority=new LanAuthority(world,"match","epoch",c=>{issuedAt=simulation.StepCount;return world.Issue(c);},(p,r)=>world.Paused=p);
            authority.BindPeer("remote",2,1);
            simulation.BeforeStep+=()=>authority.Drain(simulation.StepCount,1);
            simulation.StepSettled+=()=>
            {
                if(simulation.StepCount==1)authority.Submit(new LanCommandRequest{matchId="match",epoch="epoch",peerId="remote",sequence=1,generation=1,operation=LanOperation.Command,
                    command=new GameCommand{player=2,kind=CommandKind.Train,actors=new[]{world.Entities.Single(e=>e.owner==2&&e.def.id=="town").id},item=world.WorkerDefinition(2).id}});
            };
            Assert.AreEqual(2,simulation.AdvanceFrame(.21f));Assert.AreEqual(1,issuedAt);Assert.AreEqual(0,authority.PendingCount);
            CollectionAssert.AreEquivalent(new[]{1,3},simulation.CaptureState().controllers.Select(c=>c.player));
            Assert.AreEqual(1,world.Entities.Single(e=>e.owner==2&&e.def.id=="town").queue.Count);
        }
    }
}
