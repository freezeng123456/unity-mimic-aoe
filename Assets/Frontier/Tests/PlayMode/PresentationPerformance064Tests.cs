using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class PresentationPerformance064Tests
    {
        const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
        static EntityState Entity(RuleCatalog rules,string id)=>new EntityState{id=19,owner=0,def=rules.Entity(id),hp=100,construction=1};
        [Test] public void RepeatedCannonPoseSkipsBonesAndMovingToIdleRestoresTheActualWheelsAndBarrel()
        {
            var root=new GameObject("Cannon pose reuse regression");
            try
            {
                var rig=ProceduralArt.BuildEntity(Entity(Catalog.Load(),"cannon"),root.transform).GetComponent<UnitRig>();
                var idle=new AnimationPose{state=UnitPoseState.Idle,deployed=true};UnitAnimation.Apply(rig,idle);
                var barrel=rig.Barrel.localPosition;var tilt=rig.Barrel.localRotation;var wheel=rig.LeftWheel.localRotation;int applications=rig.PoseApplicationCount;
                for(int i=0;i<30;i++)UnitAnimation.Apply(rig,idle);
                Assert.AreEqual(applications,rig.PoseApplicationCount,"Identical render frames must not reset/rewrite the same skeleton");
                UnitAnimation.Apply(rig,new AnimationPose{state=UnitPoseState.Move,moving=true,locomotionPhase=1});
                Assert.Greater(Quaternion.Angle(wheel,rig.LeftWheel.localRotation),20);
                UnitAnimation.Apply(rig,idle);Assert.Less(Vector3.Distance(barrel,rig.Barrel.localPosition),.00001f);Assert.Less(Quaternion.Angle(tilt,rig.Barrel.localRotation),.001f);Assert.Less(Quaternion.Angle(wheel,rig.LeftWheel.localRotation),.001f);
                rig.RestoreInitialPose();UnitAnimation.Apply(rig,idle);Assert.AreEqual(applications+3,rig.PoseApplicationCount,"Explicit restoration invalidates pose reuse");
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test] public void ReusingIdleSkeletonDoesNotSwallowShieldOrWeaponModeChanges()
        {
            var root=new GameObject("Equipment pose reuse regression");
            try
            {
                var rules=Catalog.Load();var shield=ProceduralArt.BuildEntity(Entity(rules,"shield_guard"),root.transform).GetComponent<UnitRig>();
                var pose=new AnimationPose{state=UnitPoseState.Idle};UnitAnimation.Apply(shield,pose);var folded=shield.Shield.localPosition;
                pose.shielded=true;UnitAnimation.Apply(shield,pose);Assert.Greater(Vector3.Distance(folded,shield.Shield.localPosition),.4f);
                pose.shielded=false;UnitAnimation.Apply(shield,pose);Assert.Less(Vector3.Distance(folded,shield.Shield.localPosition),.00001f);
                var janissary=ProceduralArt.BuildEntity(Entity(rules,"janissary"),root.transform).GetComponent<UnitRig>();
                UnitAnimation.Apply(janissary,pose);Assert.IsTrue(janissary.Weapon.gameObject.activeSelf);Assert.IsFalse(janissary.MeleeWeapon.gameObject.activeSelf);
                pose.melee=true;UnitAnimation.Apply(janissary,pose);Assert.IsFalse(janissary.Weapon.gameObject.activeSelf);Assert.IsTrue(janissary.MeleeWeapon.gameObject.activeSelf);
            }
            finally{Object.DestroyImmediate(root);}
        }
        [UnityTest] public IEnumerator OffscreenBonesRefreshOnReentryWithoutChangingOrdersHealthPositionOrFog()
        {
            var root=new GameObject("Camera animation scheduling regression");
            try
            {
                var game=root.AddComponent<GameController>();yield return null;game.enabled=false;game.BenchmarkMode=true;game.StartMatch();game.View.enabled=false;
                var w=game.World;var near=w.Spawn("pike",0,new Vector2(27,25));var far=w.Spawn("pike",0,new Vector2(110,110));var hidden=w.Spawn("pike",1,new Vector2(115,115));
                near.status=far.status="Moving";near.previousPosition=near.position-Vector2.right;far.previousPosition=far.position-Vector2.right;
                near.orders.Enqueue(new UnitOrder(OrderKind.Move,new Vector2(30,30)));far.orders.Enqueue(new UnitOrder(OrderKind.Move,new Vector2(100,100)));
                var fog=w.Fog.At(0,hidden.position);Assert.IsFalse(w.IsVisible(0,hidden));var nearPosition=near.position;var farPosition=far.position;float hp=far.hp;int events=w.Events.Count;
                void Present()=>typeof(WorldView).GetMethod("LateUpdate",Hidden).Invoke(game.View,null);
                UnitRig Rig(EntityState e)=>root.transform.Find("Simulation Entity Views/Entity "+e.id).GetComponentInChildren<UnitRig>();
                game.Controls.Focus(near.position);w.Time=1;Present();var nearRig=Rig(near);var farRig=Rig(far);int nearCount=nearRig.PoseApplicationCount,farCount=farRig.PoseApplicationCount;
                w.Time=1.11f;Present();Assert.Greater(nearRig.PoseApplicationCount,nearCount);Assert.AreEqual(farCount,farRig.PoseApplicationCount);Assert.Greater(game.View.LastOffscreenPoseSkips,0);
                game.Controls.Focus(far.position);Present();Assert.Greater(farRig.PoseApplicationCount,farCount,"Re-entry must refresh immediately, without waiting for the offscreen timer");
                var reference=ProceduralArt.BuildEntity(far,root.transform).GetComponent<UnitRig>();UnitAnimation.Apply(reference,UnitAnimation.Evaluate(far,w.Time));Assert.Less(Quaternion.Angle(reference.LeftLeg.localRotation,farRig.LeftLeg.localRotation),.001f,"Re-entry presents the current pose, not the old offscreen phase");
                Assert.AreEqual(nearPosition,near.position);Assert.AreEqual(farPosition,far.position);Assert.AreEqual(hp,far.hp);Assert.AreEqual(1,near.orders.Count);Assert.AreEqual(1,far.orders.Count);Assert.AreEqual(events,w.Events.Count);Assert.AreEqual(fog,w.Fog.At(0,hidden.position));Assert.IsNull(root.transform.Find("Simulation Entity Views/Entity "+hidden.id),"A camera move must not reveal a fog-hidden opponent");
            }
            finally{Object.Destroy(root);}
            yield return null;
        }
        [UnityTest] public IEnumerator HoverProjectionInvalidatesForBoatSelectionCameraAndWorldAndRestoredControlsRemainValid()
        {
            var root=new GameObject("Hover and restored camera regression");
            try
            {
                var game=root.AddComponent<GameController>();yield return null;game.enabled=false;game.BenchmarkMode=true;game.SelectedMap=MapTopology.Winding;game.StartMatch();
                var w=game.World;var bridge=w.Layout.Bridges[0];game.Controls.RestoreCamera(bridge.Center,18);
                var boat=w.Spawn("fishing_boat",0,bridge.Point(0,2));var p=new Vector3(bridge.Center.x,bridge.Height,bridge.Center.y);Vector2 screen=game.GameCamera.WorldToScreenPoint(p);
                Vector2 Hover()=> (Vector2)typeof(RtsInput).GetMethod("HoverGroundPoint",Hidden).Invoke(game.Controls,new object[]{screen});
                var land=Hover();Assert.Less(Vector2.Distance(land,game.Controls.GroundPoint(screen)),.00001f);
                game.Controls.Selected.Add(boat.id);var water=Hover();Assert.Greater(Vector2.Distance(land,water),.3f);Assert.Less(Vector2.Distance(water,game.Controls.GroundPoint(screen)),.00001f);
                game.Controls.RestoreCamera(bridge.Center+Vector2.right*15,31);Assert.AreEqual(31,game.GameCamera.orthographicSize);Assert.Less(Vector2.Distance(Hover(),game.Controls.GroundPoint(screen)),.00001f);
                var own=w.Spawn("pike",0,new Vector2(28,25));var enemy=w.Spawn("pike",1,new Vector2(120,120));game.Controls.RestoreSelection(new[]{own.id,enemy.id,own.id,-123});Assert.AreEqual(1,game.Controls.Selected.Count);Assert.AreEqual(own.id,game.Controls.FirstSelected.id);
                game.Controls.SaveGroup(4);var snapshots=game.Controls.CaptureGroups();Assert.AreEqual(1,snapshots.Length);snapshots[0].actors[0]=-123;Assert.AreEqual(1,game.Controls.GroupCount(4),"Capture must return an independent copy");
                game.Controls.RestoreGroups(new[]{new ControlGroupSnapshot{number=4,actors=new[]{own.id,enemy.id,own.id,-123}},new ControlGroupSnapshot{number=12,actors=new[]{own.id}}});Assert.AreEqual(1,game.Controls.GroupCount(4));Assert.AreEqual(0,game.Controls.GroupCount(12));
                game.SelectedMap=MapTopology.Frontier;game.StartMatch();Assert.Less(Vector2.Distance(Hover(),game.Controls.GroundPoint(screen)),.00001f,"A replacement World invalidates the old terrain projection");
                game.Controls.RestoreCamera(new Vector2(20,90),17);game.Controls.Focus(new Vector2(21,91));Assert.AreEqual(17,game.GameCamera.orthographicSize,"The next pan must retain restored zoom");Assert.AreEqual(new Vector2(21,91),game.Controls.CameraFocus);
            }
            finally{Object.Destroy(root);}
            yield return null;
        }
    }
}
