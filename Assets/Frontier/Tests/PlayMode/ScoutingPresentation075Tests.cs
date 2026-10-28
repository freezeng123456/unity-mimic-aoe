using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class ScoutingPresentation075Tests
    {
        GameObject stage;
        RuleCatalog rules;
        [SetUp] public void Setup(){stage=new GameObject("Original scouting presentation acceptance");rules=Catalog.Load();}
        [TearDown] public void Cleanup(){if(stage!=null)Object.DestroyImmediate(stage);}
        EntityState Entity(string id)=>new EntityState{id=37,owner=0,def=rules.Entity(id),hp=100,construction=1,fireAt=-1};
        UnitRig Build(EntityState e)=>ProceduralArt.BuildEntity(e,stage.transform).GetComponent<UnitRig>();
        static MeshRenderer[] Visible(Transform root)=>root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
        static Transform Named(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name==name);

        static void AssertBaked(Transform source,Transform joint)
        {
            Assert.IsNotNull(source,"Required silhouette geometry is missing");
            var sourceMesh=source.GetComponent<MeshFilter>().sharedMesh;
            var batch=joint.Find("Batched rig surface");Assert.IsNotNull(batch);
            Assert.IsTrue(batch.GetComponent<MeshRenderer>().enabled);
            var batchMesh=batch.GetComponent<MeshFilter>().sharedMesh;
            var matrix=batch.worldToLocalMatrix*source.localToWorldMatrix;
            foreach(var vertex in sourceMesh.vertices.Take(8))
            {
                var point=matrix.MultiplyPoint3x4(vertex);
                Assert.IsTrue(batchMesh.vertices.Any(v=>(v-point).sqrMagnitude<.00000001f),source.name+" must be present in the visible batch");
            }
        }

        [UnityTest] public IEnumerator NewRolesKeepActualSurfacesBatchedAtTheirIndependentJoints()
        {
            foreach(string id in new[]{"envoy","native_scout"})
            {
                var rig=Build(Entity(id));yield return null;
                var surfaces=Visible(rig.transform);Assert.IsNotEmpty(surfaces,id);
                Assert.LessOrEqual(surfaces.Length,24,id+" must not restore one draw per accessory");
                Assert.IsNotNull(rig.ScoutBody.Find("Batched rig surface"));
                foreach(var renderer in surfaces)
                {
                    Assert.AreEqual("Batched rig surface",renderer.name,id);
                    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;Assert.AreEqual(1,mesh.subMeshCount);
                    Assert.AreEqual(mesh.vertexCount,mesh.normals.Length);Assert.AreEqual(mesh.vertexCount,mesh.uv.Length);
                    foreach(var v in mesh.vertices)Assert.IsFalse(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.sqrMagnitude),id);
                }
                foreach(var joint in new[]{rig.LeftShoulder,rig.RightShoulder,rig.LeftElbow,rig.RightElbow,rig.LeftHand,rig.RightHand,rig.LeftLeg,rig.RightLeg,rig.LeftKnee,rig.RightKnee})
                {
                    Assert.IsNotNull(joint.Find("Batched rig surface"),id+" moving surface must follow its own joint");
                    Assert.AreEqual(Vector3.zero,joint.Find("Batched rig surface").localPosition);
                }
            }
        }

        [Test] public void EnvoyRendersTallNarrowHatSplitCoatAndDocumentSatchelWithEmptyFists()
        {
            var e=Entity("envoy");var rig=Build(e);
            Assert.IsTrue(rig.Unarmed);Assert.IsNull(rig.Weapon);Assert.AreEqual("Construction tool grip",rig.WeaponPivot.name);
            Assert.IsNotNull(rig.WorkerEquipment);Assert.AreEqual(WorkerTool.None,rig.WorkerEquipment.CurrentTool);
            foreach(string name in new[]{"Envoy narrow hat brim","Envoy tall hat crown","Left split coat tail","Right split coat tail","Envoy pale document satchel","Visible paper folio"})
                AssertBaked(Named(rig.transform,name),rig.ScoutBody);
            var hat=Named(rig.transform,"Envoy tall hat crown");Assert.Greater(hat.GetComponent<MeshFilter>().sharedMesh.bounds.size.y,.30f);
            var left=Named(rig.transform,"Left split coat tail");var right=Named(rig.transform,"Right split coat tail");
            Assert.Less(left.GetComponent<Renderer>().bounds.max.x,right.GetComponent<Renderer>().bounds.min.x,"The coat tails have a real open split");
            foreach(string name in new[]{"Musket","Gun barrel","Coureur fur cap","Scout rear quiver"})Assert.IsNull(Named(rig.transform,name));
            AssertBaked(rig.RightHand.Find("Empty hand"),rig.RightHand);
            e.fireAt=2.2f;var preparation=UnitAnimation.Evaluate(e,2);Assert.IsTrue(preparation.melee);
            UnitAnimation.Apply(rig,preparation);var guard=rig.RightHand.position;
            e.fireAt=-1;var fired=UnitAnimation.Evaluate(e,2.11f,2);UnitAnimation.Apply(rig,fired);
            Assert.Greater(Vector3.Distance(guard,rig.RightHand.position),.10f,"The actual empty fist extends for the strike");
            Assert.IsNull(rig.Weapon,"Punch animation must not create a weapon");
            var hammer=Named(rig.transform,"Hammer");Assert.IsNotNull(hammer);Assert.IsFalse(hammer.gameObject.activeInHierarchy);
            Assert.IsNull(Named(rig.transform,"Axe"));Assert.IsNull(Named(rig.transform,"Pick"));Assert.IsNull(Named(rig.transform,"Basket"));
            UnitAnimation.Apply(rig,new AnimationPose{state=UnitPoseState.Build,tool=WorkerTool.Hammer,actionPhase=.5f});
            Assert.IsTrue(hammer.gameObject.activeInHierarchy);AssertBaked(Named(rig.transform,"Hammer head"),hammer);
            UnitAnimation.Apply(rig,new AnimationPose{state=UnitPoseState.Idle});Assert.IsFalse(hammer.gameObject.activeInHierarchy);
        }

        [Test] public void NativeScoutRendersLowWrapAsymmetricMantleAndQuiverWithoutCoureurOrMusketEquipment()
        {
            var rig=Build(Entity("native_scout"));Assert.IsFalse(rig.Unarmed);
            foreach(string name in new[]{"Scout low cloth headwrap","Scout asymmetric short mantle","Scout diagonal quiver strap","Scout quiver leather case"})
                AssertBaked(Named(rig.transform,name),rig.ScoutBody);
            Assert.IsNull(Named(rig.transform,"Coureur fur cap"));Assert.IsNull(Named(rig.transform,"Musket"));Assert.IsNull(Named(rig.transform,"Gun barrel"));
            var wrap=Named(rig.transform,"Scout low cloth headwrap");Assert.Less(wrap.GetComponent<MeshFilter>().sharedMesh.bounds.size.y,.15f);
            var mantle=Named(rig.transform,"Scout asymmetric short mantle").GetComponent<MeshFilter>().sharedMesh.vertices;
            Assert.Less(mantle.Where(v=>v.x<0).Min(v=>v.y),mantle.Where(v=>v.x>0).Min(v=>v.y)-.10f);
            var envoy=Build(Entity("envoy"));
            Assert.Greater(Visible(envoy.transform).Max(r=>r.bounds.max.y),Visible(rig.transform).Max(r=>r.bounds.max.y)+.20f,"Tall hat and low wrap remain distinct in rendered geometry");
        }

        [Test] public void CompactBowDrawConnectsBothActualTipsToTheDrawingHandAndRestoresWithoutDrift()
        {
            var e=Entity("native_scout");var rig=Build(e);rig.transform.rotation=Quaternion.Euler(0,37,0);
            Assert.IsTrue(rig.WeaponPivot.IsChildOf(rig.LeftHand));Assert.IsTrue(rig.Weapon.IsChildOf(rig.WeaponPivot));
            var stave=rig.Weapon.Find("Curved bow stave");AssertBaked(stave,rig.Weapon);
            Assert.That(stave.GetComponent<MeshFilter>().sharedMesh.bounds.size.y,Is.InRange(1.2f,1.4f));
            var initialStringScale=rig.BowStringUpper.localScale;
            e.fireAt=2.2f;var pose=UnitAnimation.Evaluate(e,2);Assert.IsTrue(pose.bow);Assert.IsFalse(pose.melee);
            pose.actionPhase=.75f;UnitAnimation.Apply(rig,pose);
            foreach(var segment in new[]{rig.BowStringUpper,rig.BowStringLower})
            {
                AssertBaked(segment,segment);
                Assert.Less(Vector3.Distance(segment.TransformPoint(Vector3.down*.5f),rig.RightHand.position),.001f);
                var tip=segment==rig.BowStringUpper?rig.BowUpperTip:rig.BowLowerTip;
                Assert.Less(Vector3.Distance(segment.TransformPoint(Vector3.up*.5f),rig.Weapon.TransformPoint(tip)),.001f);
            }
            var drawn=rig.RightHand.position;var stringAt=rig.BowStringUpper.localPosition;int count=rig.PoseApplicationCount;
            for(int i=0;i<100;i++)UnitAnimation.Apply(rig,pose);
            Assert.AreEqual(count,rig.PoseApplicationCount);Assert.Less(Vector3.Distance(drawn,rig.RightHand.position),.00001f);
            UnitAnimation.Apply(rig,new AnimationPose{state=UnitPoseState.Idle});UnitAnimation.Apply(rig,pose);
            Assert.Less(Vector3.Distance(drawn,rig.RightHand.position),.00001f);Assert.Less(Vector3.Distance(stringAt,rig.BowStringUpper.localPosition),.00001f);
            rig.RestoreInitialPose();Assert.AreEqual(initialStringScale,rig.BowStringUpper.localScale,"Pool reset must restore drawn string length too");
            pose.actionPhase=.10f;UnitAnimation.Apply(rig,pose);Assert.Greater(Vector3.Distance(drawn,rig.RightHand.position),.10f);
            var longbow=Build(Entity("longbow"));
            Assert.AreEqual(new Vector3(0,.88f,-.24f),longbow.BowUpperTip);Assert.AreEqual(new Vector3(0,-.88f,-.24f),longbow.BowLowerTip);
        }

        [Test] public void StealthPoseReadsTheRealFlagAndResetsTorsoLegsAndCachedPose()
        {
            var e=Entity("native_scout");var rig=Build(e);e.status="Stealth";
            var torso=rig.ScoutBody.localPosition;var knee=rig.LeftKnee.localRotation;
            var ordinary=UnitAnimation.Evaluate(e,3);Assert.IsFalse(ordinary.stealth);UnitAnimation.Apply(rig,ordinary);
            Assert.AreEqual(torso,rig.ScoutBody.localPosition,"A status string must not invent stealth");
            e.stealth=true;e.revealedUntil=7;var before=JsonUtility.ToJson(e);var hidden=UnitAnimation.Evaluate(e,3);Assert.IsTrue(hidden.stealth);
            UnitAnimation.Apply(rig,hidden);Assert.Less(rig.ScoutBody.localPosition.y,torso.y-.10f);Assert.Greater(Quaternion.Angle(knee,rig.LeftKnee.localRotation),20);
            var lowered=rig.ScoutBody.localPosition;int count=rig.PoseApplicationCount;
            for(int i=0;i<100;i++)UnitAnimation.Apply(rig,hidden);
            Assert.AreEqual(count,rig.PoseApplicationCount);Assert.AreEqual(lowered,rig.ScoutBody.localPosition);Assert.AreEqual(before,JsonUtility.ToJson(e));
            e.stealth=false;UnitAnimation.Apply(rig,UnitAnimation.Evaluate(e,3));
            Assert.AreEqual(torso,rig.ScoutBody.localPosition);Assert.Less(Quaternion.Angle(knee,rig.LeftKnee.localRotation),.001f);
            e.stealth=true;UnitAnimation.Apply(rig,UnitAnimation.Evaluate(e,3));rig.RestoreInitialPose();
            Assert.AreEqual(torso,rig.ScoutBody.localPosition);Assert.Less(Quaternion.Angle(knee,rig.LeftKnee.localRotation),.001f,"A pooled rig must shed its previous stealth pose");
        }

        [Test] public void ExistingEngineerStillRendersRepairToolsAndApronWithoutChangingSimulationState()
        {
            var e=Entity("field_engineer");var rig=Build(e);var cannon=Entity("cannon");cannon.id=91;
            Assert.IsNotNull(Named(rig.transform,"Engineer tool frame"));AssertBaked(Named(rig.transform,"Guild leather apron"),rig.transform);
            e.orders.Enqueue(new UnitOrder(OrderKind.Repair,cannon.position,cannon.id));e.work=new WorkActivity{kind=WorkKind.Repair,target=cannon.id,time=2};
            var before=JsonUtility.ToJson(e);var pose=UnitAnimation.Evaluate(e,2,workTarget:cannon);Assert.AreEqual(WorkerTool.Hammer,pose.tool);
            UnitAnimation.Apply(rig,pose);Assert.IsNotEmpty(Visible(rig.WeaponPivot.Find("Hammer")));Assert.IsEmpty(Visible(rig.RepairWeapon));
            Assert.AreEqual(before,JsonUtility.ToJson(e));Assert.AreEqual(OrderKind.Repair,e.orders.Peek().kind);
        }

        [UnityTest] public IEnumerator BowFireNeverCreatesMuzzleFlashWhileMusketAndImpactFeedbackStillWork()
        {
            var feedback=stage.AddComponent<CombatFeedback>();var world=new World(rules);
            var scout=world.Spawn("native_scout",0,new Vector2(28,28));var archer=world.Spawn("longbow",0,new Vector2(29,28));
            var envoy=world.Spawn("envoy",0,new Vector2(30,28));var musket=world.Spawn("musketeer",0,new Vector2(31,28));
            world.Fog.Reveal(0,new Vector2(30,28),6);feedback.Bind(world,0,stage.transform);
            world.Emit("Fire",0,scout.id,"Ranged");world.Emit("Fire",0,archer.id,"Ranged");world.Emit("Fire",0,envoy.id,"Melee");
            var before=JsonUtility.ToJson(world.CaptureState());int eventCount=world.Events.Count;feedback.Tick();Assert.AreEqual(0,feedback.ActiveCount);
            Assert.AreEqual(eventCount,world.Events.Count);Assert.AreEqual(before,JsonUtility.ToJson(world.CaptureState()),"Visual feedback must not alter simulation state");
            world.Emit("Fire",0,musket.id,"Ranged");feedback.Tick();Assert.AreEqual(1,feedback.ActiveCount,"Musket muzzle flash remains enabled");
            world.Emit("Impact",0,scout.id,"Projectile impact",7,musket.id);feedback.Tick();Assert.AreEqual(2,feedback.ActiveCount,"An arrow impact remains observable");
            feedback.Dispose();yield return null;
        }
    }
}
