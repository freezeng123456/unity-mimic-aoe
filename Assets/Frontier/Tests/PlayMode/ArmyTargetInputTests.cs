using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Actual viewport selection and right-click events through RtsInput. Spawned
    // fixtures isolate combat commands; these do not certify native OS clicks.
    public sealed class ArmyTargetInputTests : NativeInputFixture
    {
        void Isolate()
        {
            foreach(var e in game.World.Entities.Where(World.Guardian).ToArray())game.World.Remove(e);
        }
        EntityState Dummy(string kind,Vector2 point)
        {
            var e=SpawnHere(kind,1,point);
            e.def=JsonUtility.FromJson<EntityDef>(JsonUtility.ToJson(e.def));
            e.def.damage=e.def.meleeDamage=e.def.siegeDamage=0;e.hp=100000;
            Assert.IsTrue(game.World.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{e.id},order=OrderKind.Hold}).accepted||e.def.kind==EntityKind.Building);
            return e;
        }
        IEnumerator Box(EntityState[] army)
        {
            var points=army.Select(e=>ScreenPoint(e.position,1)).ToArray();
            var low=new Vector2(points.Min(p=>p.x)-12,points.Min(p=>p.y)-12);
            var high=new Vector2(points.Max(p=>p.x)+12,points.Max(p=>p.y)+12);
            AssertWorldPoint(low);AssertWorldPoint(high);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=low}.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=high}.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=high});yield return Ready();
            CollectionAssert.AreEquivalent(army.Select(e=>e.id),game.Controls.Selected,"The real selection rectangle must capture the entire mixed army");
        }
        IEnumerator RightClick(EntityState target)
        {
            var p=ScreenPoint(target.position,target.def.kind==EntityKind.Unit?1:0);AssertWorldPoint(p);yield return Click(p,MouseButton.Right);
        }
        [UnityTest] public IEnumerator BoxedMixedArmyDamagesChosenBuildingThenChosenPerson()
        {
            Isolate();var w=game.World;
            var kinds=new[]{"cannon","musketeer","janissary","abus","cuirassier","halberdier"};
            var army=kinds.Select((kind,i)=>SpawnHere(kind,0,new Vector2(65+i%3*3,23+i/3*5))).ToArray();
            var building=Dummy("barracks",new Vector2(88,25));
            var person=Dummy("pike",new Vector2(87,38));
            SpawnHere("villager",0,new Vector2(95,34));w.Fog.Tick(w.Entities);yield return Ready();
            yield return Box(army);yield return RightClick(building);
            foreach(var e in army){Assert.AreEqual(OrderKind.Attack,e.orders.Single().kind);Assert.AreEqual(building.id,e.orders.Single().target);}
            for(int i=0;i<600&&!army.All(a=>w.Events.Any(e=>e.kind=="Damage"&&e.entity==a.id&&e.target==building.id));i++)w.Tick(.1f);
            foreach(var actor in army)
            {
                Assert.IsTrue(w.Events.Any(e=>e.kind=="Fire"&&e.entity==actor.id&&e.target==building.id),actor.def.id+" must fire at the selected building");
                Assert.IsTrue(w.Events.Any(e=>e.kind=="Damage"&&e.entity==actor.id&&e.target==building.id),actor.def.id+" must damage the selected building");
                Assert.IsFalse(w.Events.Any(e=>e.kind=="Fire"&&e.entity==actor.id&&e.target==person.id),"Nearby defender must not replace the explicit building target");
            }
            int switched=w.Events.Count;float hp=person.hp;yield return RightClick(person);
            foreach(var e in army)Assert.AreEqual(person.id,e.orders.Single().target);
            for(int i=0;i<600&&!army.All(a=>w.Events.Skip(switched).Any(e=>e.kind=="Damage"&&e.entity==a.id&&e.target==person.id));i++)w.Tick(.1f);
            foreach(var actor in army)
            {
                Assert.IsTrue(w.Events.Skip(switched).Any(e=>e.kind=="Fire"&&e.entity==actor.id&&e.target==person.id),actor.def.id+" must fire at the new selected person");
                Assert.IsTrue(w.Events.Skip(switched).Any(e=>e.kind=="Damage"&&e.entity==actor.id&&e.target==person.id),actor.def.id+" must damage the new selected person");
                Assert.IsFalse(w.Events.Skip(switched).Any(e=>e.kind=="Fire"&&e.entity==actor.id&&e.target==building.id),"A new shot at the abandoned target is forbidden; earlier projectiles may still impact");
            }
            Assert.Less(person.hp,hp);
        }
        [UnityTest] public IEnumerator RightClickRedirectsCannonDuringDeploymentAndShotPreparation()
        {
            Isolate();var w=game.World;var cannon=SpawnHere("cannon",0,new Vector2(71,25));var building=Dummy("barracks",new Vector2(87,25));
            SpawnHere("villager",0,new Vector2(94,25));w.Fog.Tick(w.Entities);yield return Ready();
            yield return Click(ScreenPoint(cannon.position,1));Assert.AreEqual(cannon.id,game.Controls.FirstSelected?.id);
            yield return RightClick(building);w.Tick(.1f);Assert.AreEqual("Deploying cannon",cannon.status);
            var retreat=ScreenPoint(new Vector2(67,32));AssertWorldPoint(retreat);yield return Click(retreat,MouseButton.Right);
            var before=cannon.position;w.Tick(.1f);
            Assert.Greater(Vector2.Distance(before,cannon.position),.02f);Assert.IsFalse(cannon.isDeployed);Assert.AreEqual(0,cannon.deployUntil);
            yield return RightClick(building);
            for(int i=0;i<100&&cannon.fireAt<0;i++)w.Tick(.1f);
            Assert.Greater(cannon.fireAt,w.Time);float deadline=cannon.attackReadyAt;int switched=w.Events.Count;
            yield return Click(retreat,MouseButton.Right);Assert.Less(cannon.fireAt,0);Assert.AreEqual(deadline,cannon.attackReadyAt,"Cancellation must not reset the reload deadline");
            before=cannon.position;w.Tick(.1f);Assert.Greater(Vector2.Distance(before,cannon.position),.02f);
            Assert.IsFalse(w.Events.Skip(switched).Any(e=>e.kind=="Fire"&&e.entity==cannon.id));
        }
        [UnityTest] public IEnumerator RightClickCavalryPursuesMovingPersonAndActuallyHits()
        {
            Isolate();var w=game.World;var cavalry=SpawnHere("hussar",0,new Vector2(70,25));var person=Dummy("pike",new Vector2(79,25));
            SpawnHere("villager",0,new Vector2(96,32));w.Fog.Tick(w.Entities);yield return Ready();
            yield return Click(ScreenPoint(cavalry.position,1));Assert.AreEqual(cavalry.id,game.Controls.FirstSelected?.id);
            yield return RightClick(person);Assert.AreEqual(person.id,cavalry.orders.Single().target);
            Assert.IsTrue(w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{person.id},order=OrderKind.Move,point=new Vector2(107,25)}).accepted);
            var initial=person.position;float hp=person.hp;
            for(int i=0;i<150&&person.hp==hp;i++)w.Tick(.1f);
            Assert.Greater(Vector2.Distance(initial,person.position),8,"Target must move substantially after the click");
            Assert.Less(person.hp,hp);Assert.IsTrue(w.Events.Any(e=>e.kind=="Fire"&&e.entity==cavalry.id&&e.target==person.id));
        }
    }
}
