using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class AiSaveStateTests
    {
        static T ColdCopy<T>(T value)=>JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        static MatchSimulation ColdRestore(MatchSimulation source)
        {
            var savedWorld=ColdCopy(source.World.CaptureState());
            var savedSimulation=ColdCopy(source.CaptureState());
            return MatchSimulation.Restore(World.Restore(source.World.Rules,savedWorld),savedSimulation);
        }
        static void SameState(MatchSimulation first,MatchSimulation second)
        {
            Assert.AreEqual(JsonUtility.ToJson(first.CaptureState()),JsonUtility.ToJson(second.CaptureState()),"Controller plans and fractional time must continue identically");
            Assert.AreEqual(JsonUtility.ToJson(first.World.CaptureState()),JsonUtility.ToJson(second.World.CaptureState()),"Resources, orders, fog, production and actual AI/world events must continue identically");
        }

        [TestCase("frontier","coast","river","standard")]
        [TestCase("winding","highland","wayfarer","hard")]
        [TestCase("tropical","ottoman","france","easy")]
        public void ColdRestoreContinuesRealAiAndWorldAtTheOriginalCadence(string map,string first,string second,string difficulty)
        {
            var simulation=new MatchSimulation(new World(Catalog.Load(),6407,first,second,5,map),new[]{0,1},difficulty);
            for(int i=0;i<600;i++)simulation.Step();
            simulation.AdvanceFrame(.05f);
            var restored=ColdRestore(simulation);SameState(simulation,restored);
            Assert.Greater(simulation.World.Events.Count(e=>e.kind=="Payment"),0,"Warm-up must include ordinary paid AI decisions");
            float[] frames={.025f,.075f,.15f,.05f};
            for(int i=0;i<320;i++)
            {
                Assert.AreEqual(simulation.AdvanceFrame(frames[i%frames.Length]),restored.AdvanceFrame(frames[i%frames.Length]));
            }
            SameState(simulation,restored);
        }

        [Test] public void FractionalStepSurvivesPausedSaveWithoutAddingAHumanController()
        {
            var simulation=new MatchSimulation(new World(Catalog.Load()),1);
            simulation.AdvanceFrame(.05f);simulation.World.Paused=true;
            var restored=ColdRestore(simulation);int events=restored.World.Events.Count;
            Assert.AreEqual(0,restored.AdvanceFrame(60));Assert.AreEqual(events,restored.World.Events.Count);Assert.AreEqual(0,restored.StepCount);
            restored.World.Paused=false;simulation.World.Paused=false;
            Assert.AreEqual(1,restored.AdvanceFrame(.05f));Assert.AreEqual(1,simulation.AdvanceFrame(.05f));
            SameState(simulation,restored);
            Assert.IsFalse(restored.World.Events.Any(e=>e.player==0&&(e.kind=="Payment"||e.kind=="Command")),"Loading a human slot must never silently attach an AI");
        }

        [Test] public void SavedControllerOrderCannotChangeNativeDispatchOrder()
        {
            var simulation=new MatchSimulation(new World(Catalog.Load()),0,1);
            var snapshot=ColdCopy(simulation.CaptureState());Array.Reverse(snapshot.controllers);
            var restored=MatchSimulation.Restore(World.Restore(simulation.World.Rules,ColdCopy(simulation.World.CaptureState())),snapshot);
            simulation.Step();restored.Step();SameState(simulation,restored);
            Assert.AreEqual(1,restored.World.Events.First(e=>e.kind=="Payment").player);
        }

        [Test] public void LiveExpeditionRetainsItsMusterAndDoesNotRallyAgainAfterLoading()
        {
            // A staged force exercises an expedition already launched through an ordinary AI review.
            var world=new World(Catalog.Load(),6407,"coast","coast",3);world.Players[0].age=2;world.Players[0].resources=new Cost();
            for(int i=0;i<6;i++)world.Spawn("musketeer",0,new Vector2(33+i%3,37+i/3));
            var simulation=new MatchSimulation(world,0);simulation.Step();
            var state=simulation.CaptureState().controllers[0];Assert.IsTrue(state.expedition);Assert.GreaterOrEqual(state.expeditionSize,6);
            var restored=ColdRestore(simulation);
            for(int i=0;i<180;i++){simulation.Step();restored.Step();}
            SameState(simulation,restored);
            Assert.IsTrue(restored.CaptureState().controllers[0].expedition);
        }

        [Test] public void SearchResumesADeferredPassWithoutForgettingVisitedRegions()
        {
            // An empty observed enemy base and distant survivor isolate real patrol state.
            var world=new World(Catalog.Load(),6407,"coast","coast",3);
            foreach(var enemy in world.Entities.Where(e=>e.owner==1).ToArray())world.Remove(enemy);
            world.Spawn("villager",1,new Vector2(116,16));
            var soldiers=new List<EntityState>{world.Spawn("musketeer",0,new Vector2(104,104))};world.Tick(.5f);
            var search=new AiSearchPlan(world,0,new Vector2(104,100));search.ObserveStart(soldiers);
            Assert.IsTrue(search.TryDestination(soldiers,out _));
            for(int i=0;i<910;i++)world.Tick(.1f);
            Assert.IsTrue(search.TryDestination(soldiers,out var destination));
            var saved=ColdCopy(search.CaptureState());Assert.IsTrue(saved.visitedStart);Assert.IsTrue(saved.processed.Any(value=>value));
            Assert.IsTrue(world.Events.Any(e=>e.kind=="AISearchDeferred"));
            var loadedWorld=World.Restore(world.Rules,ColdCopy(world.CaptureState()));
            var loaded=new AiSearchPlan(loadedWorld,0,new Vector2(104,100));loaded.RestoreState(saved);
            var loadedSoldiers=soldiers.Select(e=>loadedWorld.Get(e.id)).ToList();int events=loadedWorld.Events.Count;
            Assert.IsTrue(loaded.TryDestination(loadedSoldiers,out var loadedDestination));Assert.AreEqual(destination,loadedDestination);
            Assert.AreEqual(events,loadedWorld.Events.Count,"A selected patrol must not be selected again just because it was loaded");
            for(int i=0;i<910;i++){world.Tick(.1f);loadedWorld.Tick(.1f);}
            Assert.IsTrue(search.TryDestination(soldiers,out destination));Assert.IsTrue(loaded.TryDestination(loadedSoldiers,out loadedDestination));
            Assert.AreEqual(destination,loadedDestination);Assert.AreEqual(JsonUtility.ToJson(search.CaptureState()),JsonUtility.ToJson(loaded.CaptureState()));
        }

        [Test] public void InterruptedAgeSavingKeepsItsRemainingDefenseDelay()
        {
            var world=new World(Catalog.Load());world.Players[0].resources=new Cost();
            var town=world.Entities.First(e=>e.owner==0&&e.def.id=="town");var plan=new AiAgePlan(world,0);
            plan.Tick(town,12,8,0,false);plan.Tick(town,12,3,1,false);
            for(int i=0;i<100;i++)world.Tick(.1f);
            var loadedWorld=World.Restore(world.Rules,ColdCopy(world.CaptureState()));
            var loaded=new AiAgePlan(loadedWorld,0);loaded.RestoreState(ColdCopy(plan.CaptureState()));
            var loadedTown=loadedWorld.Get(town.id);
            for(int i=0;i<90;i++)
            {
                world.Tick(.1f);loadedWorld.Tick(.1f);plan.Tick(town,12,3,0,false);loaded.Tick(loadedTown,12,3,0,false);
                Assert.IsFalse(plan.Saving);Assert.IsFalse(loaded.Saving);
            }
            for(int i=0;i<20;i++){world.Tick(.1f);loadedWorld.Tick(.1f);plan.Tick(town,12,3,0,false);loaded.Tick(loadedTown,12,3,0,false);}
            Assert.IsTrue(plan.Saving);Assert.IsTrue(loaded.Saving);
            Assert.AreEqual(JsonUtility.ToJson(plan.CaptureState()),JsonUtility.ToJson(loaded.CaptureState()));
        }

        [Test] public void CancelledManufactureCannotBypassTheExistingAiCooldownOnLoad()
        {
            var world=new World(Catalog.Load(),6407,"highland","coast",5);var player=world.Players[0];player.age=2;player.resources=new Cost(2000,2000,2000);
            player.technologies.Add("highland_standard_jigs");var workshop=world.Spawn("workshop",0,new Vector2(30,29));
            var own=world.Entities.Where(e=>e.Alive&&e.owner==0).ToList();var workers=own.Where(e=>e.Worker).ToList();
            var worker=workers.First();
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Manufacture,target=workshop.id}).accepted);
            for(int i=0;i<200&&world.AssignedArtisans(workshop)==0;i++)world.Tick(.1f);
            Assert.Greater(world.AssignedArtisans(workshop),0);
            var economy=new AiCivilizationEconomy(world,0);economy.Tick(own,workers,new List<EntityState>(),new Cost(),false,false);
            Assert.AreEqual("Manufacture",workshop.queue.Single().kind);var saved=ColdCopy(economy.CaptureState());
            Assert.Greater(saved.manufactureAt,world.Time);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{workshop.id},index=0}).accepted);
            var loadedWorld=World.Restore(world.Rules,ColdCopy(world.CaptureState()));var loaded=new AiCivilizationEconomy(loadedWorld,0);loaded.RestoreState(saved);
            var loadedOwn=loadedWorld.Entities.Where(e=>e.Alive&&e.owner==0).ToList();var loadedWorkers=loadedOwn.Where(e=>e.Worker).ToList();
            economy.Tick(own,workers,new List<EntityState>(),new Cost(),false,false);loaded.Tick(loadedOwn,loadedWorkers,new List<EntityState>(),new Cost(),false,false);
            Assert.IsEmpty(workshop.queue);Assert.IsEmpty(loadedWorld.Get(workshop.id).queue,"Loading must not spend wood on a replacement manufacture immediately");
        }

        [Test] public void CapturedAiDataCannotMutateTheRunningPlans()
        {
            var simulation=new MatchSimulation(new World(Catalog.Load()),0,1);simulation.Step();
            string original=JsonUtility.ToJson(simulation.CaptureState());var saved=simulation.CaptureState();
            saved.controllers[0].search.processed[0]=!saved.controllers[0].search.processed[0];
            saved.controllers[0].agePlan.retryAt=900;saved.controllers[0].workforce.rebalanceAt=900;
            saved.controllers[0].waterEconomy.patrolAt=900;saved.controllers[0].civilizationEconomy.manufactureAt=900;
            Assert.AreEqual(original,JsonUtility.ToJson(simulation.CaptureState()));
        }

        [Test] public void ANewSightingAfterForgettingAMiddleEntryContinuesIdenticallyAfterColdRestore()
        {
            var world=new World(Catalog.Load(),6407,"coast","coast",3);
            // Isolate ordinary army decisions without workers or an explorer
            // independently revealing additional enemies during the fixture.
            foreach(var entity in world.Entities.Where(e=>e.owner==0&&e.def.id!="town").ToArray())world.Remove(entity);
            world.Players[0].age=2;world.Players[0].resources=new Cost();
            for(int i=0;i<6;i++)world.Spawn("musketeer",0,new Vector2(33+i%3,37+i/3));
            var first=world.Spawn("house",1,new Vector2(92,60));
            var forgotten=world.Spawn("house",1,new Vector2(84,44));
            var retained=world.Spawn("house",1,new Vector2(64,28));
            foreach(var enemy in new[]{first,forgotten,retained})world.Fog.Reveal(0,enemy.position,2);
            var simulation=new MatchSimulation(world,0);Assert.IsTrue(simulation.Step());
            CollectionAssert.AreEqual(new[]{first.id,forgotten.id,retained.id},simulation.CaptureState().controllers[0].memory.Select(m=>m.id).ToArray());

            // The normal observation/army review removes the middle sighting;
            // no controller dictionary or saved DTO is edited by the fixture.
            world.Remove(forgotten);
            int reviewSteps=Mathf.CeilToInt((simulation.Difficulty.ArmySeconds+simulation.Difficulty.DecisionSeconds+2)/MatchSimulation.StepSeconds);
            for(int i=0;i<reviewSteps&&simulation.CaptureState().controllers[0].memory.Any(m=>m.id==forgotten.id);i++)
            {world.Fog.Reveal(0,forgotten.position,2);Assert.IsTrue(simulation.Step());}
            CollectionAssert.AreEqual(new[]{first.id,retained.id},simulation.CaptureState().controllers[0].memory.Select(m=>m.id).ToArray());
            var restored=ColdRestore(simulation);SameState(simulation,restored);

            // This new target ties the retained target's distance from home.
            // Memory order therefore affects an actual army destination too.
            var newcomer=world.Spawn("house",1,new Vector2(24,68));
            var loadedNewcomer=restored.World.Spawn("house",1,newcomer.position);Assert.AreEqual(newcomer.id,loadedNewcomer.id);
            int sightingSteps=Mathf.CeilToInt((simulation.Difficulty.DecisionSeconds+1)/MatchSimulation.StepSeconds);
            for(int i=0;i<sightingSteps&&!simulation.CaptureState().controllers[0].memory.Any(m=>m.id==newcomer.id);i++)
            {
                world.Fog.Reveal(0,newcomer.position,2);restored.World.Fog.Reveal(0,newcomer.position,2);
                Assert.IsTrue(simulation.Step());Assert.IsTrue(restored.Step());
            }
            CollectionAssert.AreEqual(new[]{first.id,retained.id,newcomer.id},simulation.CaptureState().controllers[0].memory.Select(m=>m.id).ToArray());
            SameState(simulation,restored);
            for(int i=0;i<reviewSteps;i++)
            {
                world.Fog.Reveal(0,newcomer.position,2);restored.World.Fog.Reveal(0,newcomer.position,2);
                Assert.IsTrue(simulation.Step());Assert.IsTrue(restored.Step());
            }
            SameState(simulation,restored);
        }

        [TestCase("duplicate")]
        [TestCase("player")]
        [TestCase("accumulator")]
        [TestCase("clock")]
        [TestCase("search")]
        [TestCase("mask")]
        [TestCase("difficulty")]
        [TestCase("memory")]
        public void DamagedAiStateIsRejectedBeforeItCanRun(string problem)
        {
            var world=new World(Catalog.Load());var saved=new MatchSimulation(world,1).CaptureState();var ai=saved.controllers[0];
            switch(problem)
            {
                case "duplicate":saved.controllers=new[]{ai,ColdCopy(ai)};break;
                case "player":ai.player=world.Players.Length;break;
                case "accumulator":saved.accumulator=float.NaN;break;
                case "clock":ai.waterEconomy.patrolAt=float.PositiveInfinity;break;
                case "search":ai.search.current=ai.search.processed.Length;break;
                case "mask":ai.search.processed=new bool[1];break;
                case "difficulty":saved.difficultyId="unknown";break;
                case "memory":ai.memory=new[]{new IntPointSnapshot{id=12,point=Vector2.zero},new IntPointSnapshot{id=12,point=Vector2.one}};break;
            }
            string before=JsonUtility.ToJson(world.CaptureState());
            Assert.Throws<ArgumentException>(()=>MatchSimulation.Restore(world,saved));Assert.AreEqual(before,JsonUtility.ToJson(world.CaptureState()),"A rejected load must not alter the current match");
        }
    }
}
