using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class WaterInput061Tests : NativeInputFixture
    {
        IEnumerator River(Vector2 focus)
        {
            game.SelectedMap=MapTopology.River;game.StartMatch();game.Controls.Focus(focus);
            foreach(var e in game.World.Entities.ToArray())if(e.def.kind!=EntityKind.Building)game.World.Remove(e);
            game.World.Fog.Reveal(0,focus,40);yield return Ready();
        }
        [UnityTest] public IEnumerator MapButtonCreatesRiverAndReturningToLandRemovesRiverArt()
        {
            game.ReturnToMenu();yield return Ready();
            yield return Click(ButtonCenter(FindButton("MAP:")));
            Assert.AreEqual(MapTopology.River,game.SelectedMap);
            yield return Click(ButtonCenter(FindButton("BEGIN SKIRMISH")));
            Assert.AreEqual(MapTopology.River,game.World.MapId);yield return Ready();
            Assert.IsTrue(session.GetComponentsInChildren<Transform>().Any(t=>t.name=="Continuous navigable river"));
            game.ReturnToMenu();yield return Ready();yield return Click(ButtonCenter(FindButton("MAP:")));
            yield return Click(ButtonCenter(FindButton("BEGIN SKIRMISH")));yield return Ready();
            Assert.AreEqual(MapTopology.Frontier,game.World.MapId);
            Assert.IsFalse(session.GetComponentsInChildren<Transform>().Any(t=>t.name=="Continuous navigable river"));
        }
        [UnityTest] public IEnumerator FishingBoatRightClickFishProducesGatheredFood()
        {
            yield return River(new Vector2(64,25));var w=game.World;
            var boat=SpawnHere("fishing_boat",0,new Vector2(62,25));var fish=SpawnHere("fish",-1,new Vector2(66,25));w.Fog.Tick(w.Entities);yield return Ready();
            yield return Click(ScreenPoint(boat.position,1.02f));Assert.AreEqual(boat.id,game.Controls.FirstSelected?.id);
            yield return Click(ScreenPoint(fish.position,.02f),MouseButton.Right);
            Assert.AreEqual(OrderKind.Gather,boat.orders.Peek().kind);Assert.AreEqual(fish.id,boat.orders.Peek().target);
            float before=w.Players[0].gathered.food;for(int i=0;i<200&&w.Players[0].gathered.food<=before;i++)w.Tick(.1f);
            Assert.Greater(w.Players[0].gathered.food,before);Assert.IsTrue(w.WaterNavigation.IsWalkable(boat.position,boat.def.radius));
        }
        [UnityTest] public IEnumerator GunboatRightClickDamagesDockThenSwitchesToEnemyBoat()
        {
            yield return River(new Vector2(64,25));var w=game.World;
            var gunboat=SpawnHere("gunboat",0,new Vector2(63,25));var dock=SpawnHere("dock",1,new Vector2(73,25));
            var enemy=SpawnHere("fishing_boat",1,new Vector2(66,32));dock.hp=enemy.hp=10000;
            w.Fog.Tick(w.Entities);yield return Ready();yield return Click(ScreenPoint(gunboat.position,1.02f));
            Assert.AreEqual(gunboat.id,game.Controls.FirstSelected?.id);
            yield return Click(ScreenPoint(dock.position,1),MouseButton.Right);Assert.AreEqual(dock.id,gunboat.orders.Peek().target);
            for(int i=0;i<150&&!w.Events.Any(e=>e.kind=="Damage"&&e.entity==gunboat.id&&e.target==dock.id);i++)w.Tick(.1f);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Damage"&&e.entity==gunboat.id&&e.target==dock.id));
            yield return Click(ScreenPoint(enemy.position,1.02f),MouseButton.Right);int switched=w.Events.Count;
            for(int i=0;i<150&&!w.Events.Skip(switched).Any(e=>e.kind=="Damage"&&e.entity==gunboat.id&&e.target==enemy.id);i++)w.Tick(.1f);
            Assert.IsTrue(w.Events.Skip(switched).Any(e=>e.kind=="Damage"&&e.entity==gunboat.id&&e.target==enemy.id));
            Assert.IsFalse(w.Events.Skip(switched).Any(e=>e.kind=="Fire"&&e.entity==gunboat.id&&e.target==dock.id));
        }
        [UnityTest] public IEnumerator BridgeMouseCoordinatesAndSelectionUseRaisedSurface()
        {
            yield return River(new Vector2(64,40));var w=game.World;
            foreach(var point in new[]{new Vector2(54,40),new Vector2(64,40),new Vector2(74,40)})
            {
                var screen=ScreenPoint(point,w.SurfaceHeight(point));
                Assert.Less(Vector2.Distance(point,game.Controls.GroundPoint(screen)),.02f,"Bridge deck and slopes must receive the intended command point");
            }
            var soldier=SpawnHere("pike",0,new Vector2(64,40));yield return Ready();
            yield return Click(ScreenPoint(soldier.position,MapTopology.BridgeHeight+1));Assert.AreEqual(soldier.id,game.Controls.FirstSelected?.id);
            var boat=SpawnHere("fishing_boat",0,new Vector2(62,43));game.Controls.Selected.Clear();game.Controls.Selected.Add(boat.id);
            var under=new Vector2(63,40);Assert.Less(Vector2.Distance(under,game.Controls.GroundPoint(ScreenPoint(under))),.02f,"Boat commands stay in the water channel beneath the bridge");
        }
    }
}
