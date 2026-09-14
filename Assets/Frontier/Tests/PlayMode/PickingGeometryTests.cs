using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class PickingGeometryTests : NativeInputFixture
    {
        private Vector2 Roof(EntityState b)
        {
            // Actual back ridge on the ProceduralArt gable, above/outside the
            // projected ground footprint, not an arbitrary empty point overhead.
            float w=b.def.width*.44f*1.12f,d=b.def.depth*.44f*1.12f;
            return ScreenPoint(b.position+Vector2.up*(d*.95f),2.15f+w*.7f-.03f);
        }
        [UnityTest] public IEnumerator RoofAndGroundSelectSameBuilding()
        {
            var b=SpawnHere("barracks",0);yield return Ready();var roof=Roof(b);AssertWorldPoint(roof);
            Assert.Greater(game.Controls.GroundPoint(roof).y-b.position.y,b.def.depth*.5f,"Roof test must be outside old ground footprint");
            yield return Click(roof);Assert.AreEqual(b.id,game.Controls.FirstSelected?.id);
            game.Controls.ResetSelection();yield return Click(ScreenPoint(b.position));Assert.AreEqual(b.id,game.Controls.FirstSelected?.id);
        }
        [UnityTest] public IEnumerator TreeCrownIsSelectableAtNearAndFarZoom()
        {
            var tree=SpawnHere("tree");yield return Ready();
            foreach(float zoom in new[]{12f,47f})
            {
                game.Controls.ResetSelection();game.GameCamera.orthographicSize=zoom;
                var crown=ScreenPoint(tree.position,4.1f);AssertWorldPoint(crown);
                yield return Click(crown);Assert.AreEqual(tree.id,game.Controls.FirstSelected?.id,"Tree crown at zoom "+zoom);
            }
        }
        [UnityTest] public IEnumerator RightClickCrownOrdersActualWorkerGather()
        {
            var tree=SpawnHere("tree");var worker=SpawnHere("villager",0,site+Vector2.right*5);
            game.Controls.Select(worker);yield return Ready();var crown=ScreenPoint(tree.position,4.1f);AssertWorldPoint(crown);
            yield return Click(crown,MouseButton.Right);Assert.AreEqual(OrderKind.Gather,worker.orders.Peek().kind);Assert.AreEqual(tree.id,worker.orders.Peek().target);
        }
        [UnityTest] public IEnumerator MineEdgeIsSelectableAtBothZooms()
        {
            var mine=SpawnHere("mine");yield return Ready();
            foreach(float zoom in new[]{12f,47f})
            {
                game.Controls.ResetSelection();game.GameCamera.orthographicSize=zoom;
                // Inside the ore sphere centred at (.7,.45,0), radii (.85,.52,.75).
                var p=ScreenPoint(mine.position+new Vector2(1.35f,.1f),.6f);AssertWorldPoint(p);
                yield return Click(p);Assert.AreEqual(mine.id,game.Controls.FirstSelected?.id);
            }
        }
        [UnityTest] public IEnumerator FriendlyUnitUnderBuildingVolumeRemainsSelectable()
        {
            var building=SpawnHere("barracks",0);var worker=SpawnHere("villager",0,site+Vector2.right*.5f);
            yield return Ready();var p=ScreenPoint(worker.position,1);AssertWorldPoint(p);
            yield return Click(p);Assert.AreEqual(worker.id,game.Controls.FirstSelected?.id);
        }
        [UnityTest] public IEnumerator PreviouslyVisibleResourceCannotLeakFromExploredFog()
        {
            var tree=SpawnHere("tree");yield return Ready();var p=ScreenPoint(tree.position,4.1f);
            yield return Click(p);Assert.AreEqual(tree.id,game.Controls.FirstSelected?.id,"Positive visibility control");
            game.World.Fog.Tick(game.World.Entities);Assert.AreEqual(VisibilityState.Explored,game.World.Fog.At(0,site));
            game.Controls.ResetSelection();yield return Click(p);Assert.IsNull(game.Controls.FirstSelected);
        }
        [UnityTest] public IEnumerator DeadResourceAndReusedModelDoNotRetainOldLocation()
        {
            var old=SpawnHere("tree");yield return Ready();var oldPoint=ScreenPoint(old.position,4.1f);
            yield return Click(oldPoint);Assert.AreEqual(old.id,game.Controls.FirstSelected?.id);
            game.World.Remove(old);game.Controls.ResetSelection();yield return Ready();
            var next=SpawnHere("tree",-1,site+Vector2.right*8);yield return Ready();
            yield return Click(oldPoint);Assert.IsNull(game.Controls.FirstSelected);
            yield return Click(ScreenPoint(next.position,4.1f));Assert.AreEqual(next.id,game.Controls.FirstSelected?.id);
        }
        [UnityTest] public IEnumerator RealGarrisonEntryRemovesOldUnitPickTarget()
        {
            var town=SpawnHere("town",0);var worker=SpawnHere("villager",0,site+Vector2.right*(town.def.width*.5f+1));
            yield return Ready();var p=ScreenPoint(worker.position,1);yield return Click(p);Assert.AreEqual(worker.id,game.Controls.FirstSelected?.id);
            Assert.IsTrue(game.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Garrison,target=town.id}).accepted);
            game.World.Tick(.1f);Assert.AreEqual(town.id,worker.garrisonedIn);
            game.Controls.ResetSelection();yield return Click(p);Assert.AreNotEqual(worker.id,game.Controls.FirstSelected?.id);
        }
        [UnityTest] public IEnumerator HudPanelBlocksEntityProjectedDirectlyBeneathIt()
        {
            game.Controls.Focus(new Vector2(76,60));var point=new Vector2(Screen.width*.35f,Screen.height*.13f);
            var ray=game.GameCamera.ScreenPointToRay(point);var plane=new Plane(Vector3.up,new Vector3(0,4.1f,0));
            Assert.IsTrue(plane.Raycast(ray,out float distance));var p=ray.GetPoint(distance);
            var tree=SpawnHere("tree",-1,new Vector2(p.x,p.z));yield return Ready();
            Assert.Less(Vector2.Distance(ScreenPoint(tree.position,4.1f),point),.1f);
            Assert.IsTrue(game.Hud.BlocksWorldInput(point),"Real raycastable HUD panel required");
            yield return Click(point);Assert.IsNull(game.Controls.FirstSelected);
        }
        [UnityTest] public IEnumerator ConstructionBoundsGrowWithModel()
        {
            var building=SpawnHere("barracks",0,complete:false);yield return Ready();var roof=Roof(building);
            yield return Click(roof);Assert.IsNull(game.Controls.FirstSelected,"Full roof is absent from small foundation");
            building.construction=1;yield return Ready();yield return Click(roof);Assert.AreEqual(building.id,game.Controls.FirstSelected?.id);
        }
    }
}
