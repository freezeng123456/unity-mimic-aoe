using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class ArmyControl072Tests : NativeInputFixture
    {
        [UnityTest] public IEnumerator HumanChosenBuildingSurvivesOpponentReviewsAndReceivesDamage()
        {
            var world=game.World;
            CollectionAssert.AreEqual(new[]{1},game.Simulation.CaptureState().controllers.Select(c=>c.player));
            foreach(var guardian in world.Entities.Where(World.Guardian).ToArray())world.Remove(guardian);
            var cavalry=SpawnHere("hussar",0,new Vector2(72,25));
            var target=SpawnHere("house",1,new Vector2(90,25));target.hp=100000;
            var bystander=SpawnHere("house",1,new Vector2(88,35));bystander.hp=100000;
            SpawnHere("villager",0,new Vector2(96,30));world.Fog.Tick(world.Entities);yield return Ready();
            yield return Click(ScreenPoint(cavalry.position,1));Assert.AreEqual(cavalry.id,game.Controls.FirstSelected?.id);
            yield return Click(ScreenPoint(target.position),MouseButton.Right);
            Assert.AreEqual(OrderKind.Attack,cavalry.orders.Single().kind);Assert.AreEqual(target.id,cavalry.orders.Single().target);
            float hp=target.hp;
            for(int i=0;i<450;i++)
            {
                Assert.IsTrue(game.Simulation.Step());
                Assert.IsTrue(target.Alive);Assert.IsTrue(world.IsVisible(0,target));
                Assert.AreEqual(OrderKind.Attack,cavalry.orders.Single().kind,"Enemy AI reviews must preserve the human order");
                Assert.AreEqual(target.id,cavalry.orders.Single().target);
            }
            Assert.Less(target.hp,hp);Assert.IsTrue(world.Events.Any(e=>e.kind=="Damage"&&e.entity==cavalry.id&&e.target==target.id));
            Assert.IsFalse(world.Events.Any(e=>e.kind=="Fire"&&e.entity==cavalry.id&&e.target==bystander.id));
            Assert.IsTrue(world.Events.Any(e=>e.kind=="Payment"&&e.player==1),"The opposing economy must actually continue");
        }

        [UnityTest] public IEnumerator AutoplayRejectsPointerAndDirectOrdersBeforeTheyCanBeOverwritten()
        {
            game.ReturnToMenu();game.Autoplay=true;game.StartMatch();game.enabled=false;game.Controls.Focus(site);
            CollectionAssert.AreEquivalent(new[]{0,1},game.Simulation.CaptureState().controllers.Select(c=>c.player));
            var unit=SpawnHere("hussar",0,new Vector2(72,25));var target=SpawnHere("house",1,new Vector2(90,25));
            worldVisibility();yield return new WaitForSecondsRealtime(.3f);yield return Ready();
            yield return Click(ScreenPoint(unit.position,1));
            Assert.AreEqual(unit.id,game.Controls.FirstSelected?.id,"Observation can still select objects");
            int commandEvents=game.World.Events.Count;var resources=game.World.Players[0].resources;
            yield return Click(ScreenPoint(target.position),MouseButton.Right);
            Assert.IsFalse(game.CanSubmitOrders);Assert.AreEqual(0,unit.orders.Count);
            var result=game.Command(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Attack,target=target.id});
            Assert.IsFalse(result.accepted);StringAssert.Contains("observation",result.reason);
            Assert.AreEqual(commandEvents,game.World.Events.Count);Assert.AreEqual(resources,game.World.Players[0].resources);
            Assert.IsTrue(game.Simulation.Step(),"Observation must keep both AIs running");
            Assert.IsTrue(game.World.Events.Any(e=>e.kind=="Payment"&&e.player==0));
            void worldVisibility(){SpawnHere("villager",0,new Vector2(96,30));game.World.Fog.Tick(game.World.Entities);}
        }

        [UnityTest] public IEnumerator HumanChosenPersonIsPursuedAndDamagedWhileOpponentManagesItsWorkers()
        {
            var world=game.World;
            foreach(var guardian in world.Entities.Where(World.Guardian).ToArray())world.Remove(guardian);
            var cavalry=SpawnHere("hussar",0,new Vector2(72,25));
            var person=SpawnHere("villager",1,new Vector2(83,25));person.hp=10000;
            world.Fog.Tick(world.Entities);yield return Ready();
            yield return Click(ScreenPoint(cavalry.position,1));yield return Click(ScreenPoint(person.position,1),MouseButton.Right);
            var start=person.position;float hp=person.hp;var chase=new System.Text.StringBuilder();
            for(int i=0;i<80;i++)
            {
                Assert.IsTrue(game.Simulation.Step());Assert.IsTrue(world.IsVisible(0,person));
                Assert.AreEqual(OrderKind.Attack,cavalry.orders.Single().kind);Assert.AreEqual(person.id,cavalry.orders.Single().target);
                chase.AppendLine(world.Time+" edge="+world.EdgeDistance(cavalry,person)+" requested="+world.WorkPoint(person,cavalry)+" cached="+cavalry.pathGoal+" index="+cavalry.pathIndex+" retry="+cavalry.pathRetryAt+" fire="+cavalry.fireAt+" ready="+cavalry.attackReadyAt+" status="+cavalry.status);
            }
            TestContext.WriteLine(chase.ToString());foreach(var ev in world.Events.Where(e=>e.entity==cavalry.id))TestContext.WriteLine(JsonUtility.ToJson(ev));
            Assert.Greater(Vector2.Distance(start,person.position),2,"The opponent must really move the chosen person");
            Assert.Less(person.hp,hp,"Actual chase at "+world.Time+"s: cavalry="+cavalry.position+" status="+cavalry.status+" path="+string.Join(";",cavalry.path)+" person="+person.position+" status="+person.status+" orders="+string.Join(";",person.orders.Select(o=>o.kind+":"+o.target+":"+o.point)));Assert.IsTrue(world.Events.Any(e=>e.kind=="Damage"&&e.entity==cavalry.id&&e.target==person.id));
        }

        [UnityTest] public IEnumerator AuthoredObservationKeepsBothAIsAndEditorPlaytestKeepsHumanControl()
        {
            game.ReturnToMenu();var document=RiverContent070.Build(game.Rules,RiverContent070.Skirmish);
            Assert.IsTrue(game.SelectAuthoredScenario(document));game.Autoplay=true;game.SimulationSpeed=16;game.StartMatch();game.enabled=false;
            Assert.IsTrue(game.Autoplay);Assert.AreEqual(16,game.SimulationSpeed);Assert.IsFalse(game.CanSubmitOrders);
            CollectionAssert.AreEquivalent(new[]{0,1},game.Simulation.CaptureState().controllers.Select(c=>c.player));
            Assert.IsTrue(game.Simulation.Step());
            foreach(int player in new[]{0,1})Assert.IsTrue(game.World.Events.Any(e=>e.kind=="Payment"&&e.player==player));
            game.ReturnToMenu();Assert.IsTrue(game.OpenScenarioDocument(document));game.Autoplay=true;
            Assert.IsTrue(game.PlaytestScenario());Assert.IsFalse(game.Autoplay);Assert.AreEqual(1,game.SimulationSpeed);
            CollectionAssert.AreEqual(new[]{1},game.Simulation.CaptureState().controllers.Select(c=>c.player));Assert.IsTrue(game.CanSubmitOrders);
            yield return Ready();
        }
    }
}
