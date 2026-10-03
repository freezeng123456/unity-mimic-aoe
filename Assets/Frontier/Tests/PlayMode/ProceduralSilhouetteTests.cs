using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class ProceduralSilhouetteTests
    {
        private GameObject stage;
        private RuleCatalog rules;
        [SetUp] public void Setup(){stage=new GameObject("Original silhouette geometry validation");rules=Catalog.Load();}
        [TearDown] public void Cleanup(){if(stage!=null)Object.DestroyImmediate(stage);}
        private Transform Build(string id,int owner=0)
        {var def=rules.Entity(id);Assert.IsNotNull(def,id);return ProceduralArt.BuildEntity(new EntityState{id=1,owner=owner,def=def,hp=def.hp,construction=1},stage.transform);}

        [TestCase("town")][TestCase("house")][TestCase("barracks")][TestCase("stable")]
        [TestCase("foundry")][TestCase("market")][TestCase("tower")][TestCase("farm")]
        [TestCase("bank")][TestCase("estate")][TestCase("trade_post")]
        public void EntireBuildingIncludingYardAwningAndBannerFitsSimulationFootprint(string id)
        {
            var model=Build(id);var def=rules.Entity(id);int vertices=0;
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))foreach(var vertex in filter.sharedMesh.vertices)
            {
                var p=model.InverseTransformPoint(filter.transform.TransformPoint(vertex));vertices++;
                Assert.LessOrEqual(Mathf.Abs(p.x),def.width*.5f+.002f,id+" / "+filter.name+" crosses width");
                Assert.LessOrEqual(Mathf.Abs(p.z),def.depth*.5f+.002f,id+" / "+filter.name+" crosses depth");
                Assert.IsFalse(float.IsNaN(p.y)||float.IsInfinity(p.y));
            }
            Assert.Greater(vertices,50,"Actual rendered geometry must be present");
        }

        [TestCase("villager")][TestCase("pike")][TestCase("longbow")][TestCase("skirmisher")]
        public void FigureHasShoulderWaistShapeAndBroadTeamClothOnBothTeams(string id)
        {
            foreach(int owner in new[]{0,1})
            {
                var model=Build(id,owner);var coat=model.Find("Tailored coat").GetComponent<MeshFilter>().sharedMesh;
                float low=coat.vertices.Min(v=>v.y),high=coat.vertices.Max(v=>v.y);
                float waist=coat.vertices.Where(v=>Mathf.Abs(v.y-low)<.001f).Max(v=>Mathf.Abs(v.x));
                float shoulder=coat.vertices.Where(v=>Mathf.Abs(v.y-high)<.001f).Max(v=>Mathf.Abs(v.x));
                Assert.Greater(shoulder,waist*1.4f,"The actual coat mesh must taper, rather than a renamed cube");
                var sash=model.Find("Waist sash").GetComponent<MeshRenderer>();
                var expected=owner==0?ProceduralArt.Blue:ProceduralArt.Red;var actual=sash.sharedMaterial.color;
                for(int channel=0;channel<4;channel++)
                {
                    // URP MainColor is written/read through native Material storage.
                    // Record the roundtrip rather than rely on Color's rounded string.
                    float delta=actual[channel]-expected[channel];
                    if(delta!=0)Debug.Log($"FRONTIER_TEAM_COLOR unit={id} owner={owner} channel={channel} expected={expected[channel]:R} material={actual[channel]:R} delta={delta:R}");
                    Assert.That(actual[channel],Is.EqualTo(expected[channel]).Within(1e-5f),"Stored URP team color / "+id+" / owner "+owner+" / RGBA channel "+channel);
                }
                Assert.Greater(sash.bounds.size.x,.55f);Assert.Greater(sash.bounds.size.y,.14f);Assert.Greater(sash.bounds.size.z,.39f);
                var rig=model.GetComponent<UnitRig>();Assert.IsTrue(rig.LeftKnee.IsChildOf(rig.LeftLeg));
            }
        }

        [TestCase("hussar")][TestCase("dragoon")][TestCase("carabineer")]
        public void MountedFigureKeepsSeatedKneesAndActualGripThroughRepeatedGait(string id)
        {
            var rig=Build(id).GetComponent<UnitRig>();var pose=new AnimationPose{state=UnitPoseState.Move,moving=true,locomotionPhase=.8f,melee=id=="hussar"};
            UnitAnimation.Apply(rig,pose);
            float hip=rig.transform.InverseTransformPoint(rig.LeftLeg.position).z,knee=rig.transform.InverseTransformPoint(rig.LeftKnee.position).z;
            Assert.Greater(knee-hip,.25f,"The rider's actual thigh must sit forward of the hip");
            Assert.Greater(Vector3.Dot(rig.LeftKnee.up,rig.transform.up),.97f,"Boots must hang down from bent knees");
            Assert.Less(Vector3.Distance(rig.RightHand.position,rig.WeaponPivot.position),.001f);
            Assert.IsNotNull(rig.HorseHead);Assert.IsNotNull(rig.HorseTail);
            var hand=rig.RightHand.position;var leg=rig.LeftLeg.localRotation;var head=rig.HorseHead.localRotation;
            for(int i=0;i<100;i++)UnitAnimation.Apply(rig,pose);
            Assert.Less(Vector3.Distance(hand,rig.RightHand.position),.00001f);Assert.Less(Quaternion.Angle(leg,rig.LeftLeg.localRotation),.001f);Assert.Less(Quaternion.Angle(head,rig.HorseHead.localRotation),.001f);
            pose.locomotionPhase=2.0f;UnitAnimation.Apply(rig,pose);Assert.Greater(Quaternion.Angle(head,rig.HorseHead.localRotation),.1f);
            UnitAnimation.Apply(rig,new AnimationPose{state=UnitPoseState.PreparingAttack,melee=id=="hussar",actionPhase=.8f});
            Assert.Less(Vector3.Distance(rig.RightHand.position,rig.WeaponPivot.position),.001f);
            if(id!="hussar")Assert.Greater(Vector3.Dot(rig.transform.InverseTransformDirection(rig.Weapon.Find("Gun barrel").up),Vector3.forward),.999f);
        }

        [Test] public void CannonAndMortarDifferInActualTubeLengthDiameterAndDeployedElevation()
        {
            var cannon=Build("cannon").GetComponent<UnitRig>();var mortar=Build("mortar").GetComponent<UnitRig>();
            var longTube=cannon.Barrel.GetComponent<MeshFilter>().sharedMesh.bounds;var shortTube=mortar.Barrel.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.Greater(longTube.size.y,shortTube.size.y*2);Assert.Greater(shortTube.size.x,longTube.size.x*1.5f);
            foreach(var rig in new[]{cannon,mortar})
            {
                UnitAnimation.Apply(rig,new AnimationPose{state=UnitPoseState.Idle,deployed=true});var rest=rig.Barrel.localPosition;var angle=rig.Barrel.localRotation;
                var pose=new AnimationPose{state=UnitPoseState.Fire,deployed=true,actionPhase=.5f};UnitAnimation.Apply(rig,pose);var recoil=rig.Barrel.localPosition;
                Assert.Less(Vector3.Dot(recoil-rest,angle*Vector3.up),-.10f,"Recoil must follow each tube's actual axis");
                for(int i=0;i<100;i++)UnitAnimation.Apply(rig,pose);Assert.Less(Vector3.Distance(recoil,rig.Barrel.localPosition),.00001f);
                UnitAnimation.Apply(rig,new AnimationPose{state=UnitPoseState.Idle,deployed=true});Assert.Less(Vector3.Distance(rest,rig.Barrel.localPosition),.00001f);
            }
            Assert.Less(Vector3.Dot(cannon.Barrel.up,cannon.transform.up),.3f);Assert.Greater(Vector3.Dot(mortar.Barrel.up,mortar.transform.up),.75f);
        }

        [UnityTest] public IEnumerator AuthoredMeshesSurvivePoolDeactivateAndAreReleasedWithoutDestroyingImportedFbx()
        {
            foreach(string id in new[]{"villager","hussar","mortar","bank","farm","crate_food","crate_wood","crate_coin","musketeer"})Build(id);
            yield return null;
            var owned=stage.GetComponentsInChildren<ProceduralMeshLifetime>(true).Select(o=>o.OwnedMesh).ToArray();Assert.Greater(owned.Length,30);
            var imported=stage.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.GetComponent<ProceduralMeshLifetime>()==null&&f.name.Contains("Skin")).Select(f=>f.sharedMesh).ToArray();Assert.IsNotEmpty(imported);
            stage.SetActive(false);yield return null;foreach(var mesh in owned)Assert.IsTrue(mesh!=null,"Pool deactivation must retain authored meshes");
            stage.SetActive(true);yield return null;Object.Destroy(stage);yield return null;yield return null;
            foreach(var mesh in owned)Assert.IsTrue(mesh==null,"Each unique generated mesh must be released with its model: "+(mesh!=null?mesh.name:"released"));
            foreach(var mesh in imported)Assert.IsTrue(mesh!=null,"Shared FBX asset meshes must survive model teardown");
            stage=null;
        }
    }
}
