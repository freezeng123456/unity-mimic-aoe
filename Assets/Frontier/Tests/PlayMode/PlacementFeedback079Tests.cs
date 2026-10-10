using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class PlacementFeedback079Tests : NativeInputFixture
    {
        // Funded/revealed placement fixtures, not normal-opening economy evidence.
        Vector2 dockSite;
        IEnumerator Winding()
        {
            game.SelectedMap=MapTopology.Winding;game.StartMatch();
            var w=game.World;w.Players[0].age=2;w.Players[0].resources=new Cost(5000,5000,5000);
            w.Fog.Reveal(0,new Vector2(64,64),180);
            bool found=false;
            for(int y=12;y<=116&&!found;y++)for(int x=12;x<=116&&!found;x++)
            {
                var point=new Vector2(x,y);
                if(Mathf.Abs(w.DockWaterDirection(point).y)<.25f||w.CanBuild(0,"dock",point)!="")continue;
                dockSite=point;found=true;
            }
            Assert.IsTrue(found,"Winding must offer a legal oblique harbor");
            game.Controls.RestoreCamera(dockSite,26);yield return Ready();
        }
        IEnumerator Hover(Vector2 point)
        {
            game.Controls.RestoreCamera(point,26);
            var pixel=ScreenPoint(point,game.World.SurfaceHeight(point));AssertWorldPoint(pixel);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pixel});yield return Ready();
        }
        LineRenderer Outline(string name)=>session.GetComponentsInChildren<LineRenderer>(true).Single(l=>l.name==name);
        void AssertFeedback()
        {
            var state=game.Controls.CaptureBuildPlacementPreview079();
            Assert.IsTrue(state.Active);Assert.AreEqual(game.World.CanBuild(0,"dock",state.Point),state.Reason);
            Assert.AreEqual(state.Label,game.Hud.OrderSummary,"HUD must show the current authoritative reason before a click");
            Assert.IsTrue(Outline("Building footprint").gameObject.activeSelf);
            Assert.IsTrue(Outline("Dock quay footprint").gameObject.activeSelf);
            // LineRenderer's gradient stores color channels at 8-bit precision.
            var expected=state.Valid?new Color(.5f,1,.35f):new Color(1,.22f,.12f);
            Assert.AreEqual((Color32)expected,(Color32)Outline("Building footprint").startColor);
        }
        [UnityTest] public IEnumerator ObliqueDockReadyTextAndEightCornersShareSnappedPointWithoutMutatingWorld()
        {
            yield return Winding();game.Controls.BeginBuild("dock");
            Assert.IsFalse(BuildPlacementPreview079.Evaluate(game.World,0,"dock",new Vector2(float.NaN,0)).Active);
            Assert.IsFalse(BuildPlacementPreview079.Evaluate(game.World,0,null,dockSite).Active);
            string before=JsonUtility.ToJson(game.World.CaptureState());
            yield return Hover(dockSite+new Vector2(.12f,.12f));AssertFeedback();
            var state=game.Controls.CaptureBuildPlacementPreview079();Assert.IsTrue(state.Valid,state.Reason);
            Assert.AreEqual(dockSite,state.Point);StringAssert.Contains("READY",game.Hud.OrderSummary);
            var corners=game.World.BuildingFootprintCorners(game.Rules.Entity("dock"),state.Point);Assert.AreEqual(8,corners.Length);
            for(int i=0;i<8;i++)
            {
                var actual=Outline(i<4?"Building footprint":"Dock quay footprint").GetPosition(i%4);
                Assert.Less(Vector2.Distance(corners[i],new Vector2(actual.x,actual.z)),.0001f);
                Assert.AreEqual((i<4?game.World.SurfaceHeight(corners[i]):game.World.Layout.WaterHeight)+.2f,actual.y,.0001f);
            }
            for(int i=0;i<20;i++)game.Controls.CaptureBuildPlacementPreview079();
            Assert.AreEqual(before,JsonUtility.ToJson(game.World.CaptureState()),"Hover/read must not spend, order, change fog/navigation or emit simulation events");
        }
        [UnityTest] public IEnumerator AgeFundsVisionTerrainAndOccupancyReasonsAppearBeforeClickAndTargetSwitchClearsThem()
        {
            yield return Winding();game.Controls.BeginBuild("dock");yield return Hover(dockSite);
            var w=game.World;w.Players[0].age=1;yield return Ready();AssertFeedback();StringAssert.Contains("Requires age",game.Hud.OrderSummary);
            w.Players[0].age=2;w.Players[0].resources=default;yield return Ready();AssertFeedback();StringAssert.Contains("Need ",game.Hud.OrderSummary);
            w.Players[0].resources=new Cost(5000,5000,5000);
            w.Fog.RestoreState(new int[w.Fog.CaptureState().Length]);yield return Ready();AssertFeedback();StringAssert.Contains("Build inside current vision",game.Hud.OrderSummary);
            w.Fog.Reveal(0,new Vector2(64,64),180);
            foreach(var point in new[]{new Vector2(24,72),w.Layout.Bridges[0].Center})
            {yield return Hover(point);AssertFeedback();Assert.IsFalse(game.Controls.CaptureBuildPlacementPreview079().Valid,"Inland / bridge must not accept a harbor");}
            yield return Hover(dockSite);var occupant=SpawnHere("villager",0,dockSite);yield return Ready();AssertFeedback();StringAssert.Contains("occupied",game.Hud.OrderSummary);
            w.Remove(occupant);game.Controls.BeginEmbark();yield return Ready();
            Assert.IsFalse(game.Controls.CaptureBuildPlacementPreview079().Active);StringAssert.DoesNotContain("PLACING",game.Hud.OrderSummary);
            Assert.IsFalse(Outline("Building footprint").gameObject.activeSelf);Assert.IsFalse(Outline("Dock quay footprint").gameObject.activeSelf);
            game.Controls.BeginBuild("dock");yield return Ready();game.Controls.CancelTargetMode();yield return Ready();
            Assert.IsFalse(game.Controls.CaptureBuildPlacementPreview079().Active);StringAssert.DoesNotContain("PLACING",game.Hud.OrderSummary);
        }
        [UnityTest] public IEnumerator PointerBuildPaysForTheSamePreviewedRotatedFoundation()
        {
            yield return Winding();var w=game.World;
            var worker=SpawnHere("villager",0,dockSite-w.DockWaterDirection(dockSite)*3.3f);
            game.Controls.Select(worker);game.Controls.BeginBuild("dock");yield return Hover(dockSite+new Vector2(.12f,.12f));
            AssertFeedback();var state=game.Controls.CaptureBuildPlacementPreview079();Assert.IsTrue(state.Valid,state.Reason);
            float before=w.Players[0].resources.wood;yield return new WaitForSecondsRealtime(.25f);
            yield return Click(ScreenPoint(dockSite+new Vector2(.12f,.12f),w.SurfaceHeight(dockSite+new Vector2(.12f,.12f))));
            var dock=w.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id=="dock");Assert.AreEqual(state.Point,dock.position);
            Assert.AreEqual(before-w.UnitCost(0,dock.def).wood,w.Players[0].resources.wood,.001f);
            Assert.AreEqual(OrderKind.Build,worker.orders.Peek().kind);Assert.AreEqual(dock.id,worker.orders.Peek().target);
            Assert.IsFalse(game.Controls.CaptureBuildPlacementPreview079().Active);yield return Ready();StringAssert.DoesNotContain("PLACING",game.Hud.OrderSummary);
        }
    }
}
