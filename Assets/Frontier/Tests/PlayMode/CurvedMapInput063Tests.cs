using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class CurvedMapInput063Tests : NativeInputFixture
    {
        [UnityTest] public IEnumerator MenuStartsEveryAuthoredMapThroughRealButtons()
        {
            game.ReturnToMenu();yield return Ready();
            for(int i=1;i<=MapTopology.All.Length;i++)
            {
                yield return Click(ButtonCenter(FindButton("MAP:")));
                string id=MapTopology.All[i%MapTopology.All.Length];Assert.AreEqual(id,game.SelectedMap);
                yield return Click(ButtonCenter(FindButton("BEGIN SKIRMISH")));yield return Ready();
                Assert.AreEqual(id,game.World.MapId);Assert.AreEqual(id,game.World.Layout.Id);
                Assert.AreEqual(id==MapTopology.Frontier||id==MapTopology.River,session.transform.Find("Authored Frontier Terrain/Ground").GetComponent<MeshRenderer>().enabled);
                game.ReturnToMenu();yield return Ready();
            }
        }

        [UnityTest] public IEnumerator CurvedBanksAndSlantedBridgesProjectToTheirRenderedSurface()
        {
            game.SelectedMap=MapTopology.Winding;game.StartMatch();game.enabled=false;
            var w=game.World;var bridge=w.Layout.Bridges[0];game.Controls.Focus(bridge.Center);w.Fog.Reveal(0,bridge.Center,40);yield return Ready();
            foreach(float across in new[]{-bridge.HalfLength-4,0,bridge.HalfLength+4})
            {
                var point=bridge.Point(across,0);
                Assert.Less(Vector2.Distance(point,game.Controls.GroundPoint(ScreenPoint(point,w.SurfaceHeight(point)))),.025f,"Rotated deck and ramp clicks must follow the real surface");
            }
            var soldier=SpawnHere("pike",0,bridge.Center);yield return Ready();
            yield return Click(ScreenPoint(soldier.position,w.SurfaceHeight(soldier.position)+1));Assert.AreEqual(soldier.id,game.Controls.FirstSelected?.id);
            var under=bridge.Point(0,2);var boat=SpawnHere("fishing_boat",0,under);game.Controls.Selected.Clear();game.Controls.Selected.Add(boat.id);
            Assert.Less(Vector2.Distance(under,game.Controls.GroundPoint(ScreenPoint(under,game.World.Layout.WaterHeight))),.025f,"Boat click must resolve below the bridge");
            game.Controls.Selected.Clear();var shore=w.Layout.DockSites[0][0]+w.Layout.WaterDirection(w.Layout.DockSites[0][0])*1.5f;
            Assert.IsFalse(w.IsWater(shore));game.Controls.Focus(shore);yield return Ready();
            Assert.Less(Vector2.Distance(shore,game.Controls.GroundPoint(ScreenPoint(shore,w.Layout.BankHeight(shore)))),.025f,"Carved bank must not use the legacy flat plane");
        }

        [UnityTest] public IEnumerator RightClickDamagesChosenCurvedBankDockThenRetargetsChosenBoat()
        {
            game.SelectedMap=MapTopology.Winding;game.StartMatch();game.enabled=false;var w=game.World;
            foreach(var e in w.Entities.Where(e=>e.def.id!="town").ToArray())w.Remove(e);
            var site=w.Layout.DockSites[1][0];var direction=w.DockWaterDirection(site);Assert.Greater(direction.sqrMagnitude,.9f);
            var dock=SpawnHere("dock",1,site);dock.hp=10000;
            var gun=SpawnHere("gunboat",0,w.WaterNavigation.NearestWalkable(site+direction*7,.9f));var side=new Vector2(-direction.y,direction.x);
            var enemy=SpawnHere("fishing_boat",1,w.WaterNavigation.NearestWalkable(gun.position+side*6,.65f));enemy.hp=10000;enemy.hold=true;enemy.facing=Vector2.down;
            Assert.IsTrue(w.WaterNavigation.IsWalkable(gun.position,gun.def.radius));
            game.Controls.Focus((site+gun.position)*.5f);w.Fog.Reveal(0,gun.position,40);yield return Ready();
            yield return Click(ScreenPoint(gun.position,w.SurfaceHeight(gun.position,true)+1));Assert.AreEqual(gun.id,game.Controls.FirstSelected?.id);
            yield return Click(ScreenPoint(dock.position,1),MouseButton.Right);Assert.AreEqual(dock.id,gun.orders.Peek().target);
            for(int i=0;i<250&&!w.Events.Any(e=>e.kind=="Damage"&&e.entity==gun.id&&e.target==dock.id);i++)w.Tick(.1f);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Damage"&&e.entity==gun.id&&e.target==dock.id));
            yield return Ready();
            var enemyModel=session.transform.Find("Simulation Entity Views/Entity "+enemy.id).GetComponentInChildren<BoatPresentationMotion>().transform;
            var bow=enemyModel.TransformPoint(new Vector3(0,.41f,1.58f));var bowScreen=(Vector2)game.GameCamera.WorldToScreenPoint(bow);AssertWorldPoint(bowScreen);
            yield return Click(bowScreen,MouseButton.Right);Assert.AreEqual(enemy.id,gun.orders.Peek().target,"Right-clicking the visible bow must lock this boat instead of issuing a water movement order");
            int start=w.Events.Count;
            for(int i=0;i<250&&!w.Events.Skip(start).Any(e=>e.kind=="Damage"&&e.entity==gun.id&&e.target==enemy.id);i++)w.Tick(.1f);
            Assert.IsTrue(w.Events.Skip(start).Any(e=>e.kind=="Damage"&&e.entity==gun.id&&e.target==enemy.id));
            Assert.IsFalse(w.Events.Skip(start).Any(e=>e.kind=="Fire"&&e.entity==gun.id&&e.target==dock.id));
        }

        [UnityTest]public IEnumerator VisibleBoatBowSelectsAfterTurningAndWakeNeverExpandsItsPickBounds()
        {
            game.SelectedMap=MapTopology.Winding;game.StartMatch();game.enabled=false;var w=game.World;
            foreach(var e in w.Entities.Where(e=>e.def.id!="town").ToArray())w.Remove(e);
            var boat=SpawnHere("gunboat",0,w.Layout.PatrolPoints[0]);boat.hold=true;boat.facing=Vector2.down;
            game.Controls.Focus(boat.position);game.GameCamera.orthographicSize=16;
            w.Fog.Reveal(0,boat.position,30);yield return Ready();
            var motion=session.transform.Find("Simulation Entity Views/Entity "+boat.id).GetComponentInChildren<BoatPresentationMotion>();var model=motion.transform;
            Vector2 BowScreen()=>(Vector2)game.GameCamera.WorldToScreenPoint(model.TransformPoint(new Vector3(0,.41f,1.85f)));
            var bow=BowScreen();AssertWorldPoint(bow);
            var oldCentre=(Vector2)game.GameCamera.WorldToScreenPoint(new Vector3(boat.position.x,w.SurfaceHeight(boat.position,true)+1,boat.position.y));
            float oldThreshold=Mathf.Max(13,Screen.height/game.GameCamera.orthographicSize*.35f);
            Assert.Greater(Vector2.Distance(bow,oldCentre),oldThreshold,"The fixture must click real hull geometry beyond the former centre-only hit radius");
            yield return Click(bow);Assert.AreEqual(boat.id,game.Controls.FirstSelected?.id);
            Assert.IsTrue(game.View.TryGetPickBounds(boat.id,out var before));
            motion.Wake.enabled=true;Assert.IsTrue(game.View.TryGetPickBounds(boat.id,out var withWake));
            Assert.Less(Vector3.Distance(before.min,withWake.min),.0001f);Assert.Less(Vector3.Distance(before.max,withWake.max),.0001f);
            motion.Wake.enabled=false;
            boat.facing=Vector2.right;for(int i=0;i<24;i++)yield return null;
            Assert.Greater(Vector3.Dot(model.forward,Vector3.right),.999f);
            Assert.IsTrue(game.View.TryGetPickBounds(boat.id,out var after));
            Assert.Greater(after.size.x,before.size.x+1);Assert.Less(after.size.z,before.size.z-1);
            game.Controls.ResetSelection();var turnedBow=BowScreen();AssertWorldPoint(turnedBow);
            yield return Click(turnedBow);Assert.AreEqual(boat.id,game.Controls.FirstSelected?.id,"A rotated boat must use current hull bounds, not the stale pre-turn rectangle");
        }
    }
}
