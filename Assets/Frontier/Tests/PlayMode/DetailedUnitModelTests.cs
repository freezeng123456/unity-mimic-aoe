using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class DetailedUnitModelTests
    {
        GameObject stage;
        [SetUp] public void Setup(){stage=new GameObject("Detailed unit surface acceptance");}
        [TearDown] public void Cleanup(){if(stage!=null)Object.DestroyImmediate(stage);}
        UnitRig Build(string id)=>ProceduralArt.BuildEntity(new EntityState{id=1,owner=0,construction=1,def=Catalog.Load().Entity(id)},stage.transform).GetComponent<UnitRig>();
        static MeshRenderer[] Visible(Transform root)=>root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();

        [UnityTest] public IEnumerator EveryUnitKeepsVisibleSurfaceBatchesAndNoBatchCrossesItsMovingJoints()
        {
            foreach(string id in new[]{"villager","musketeer","pike","longbow","coureur","hussar","cuirassier","spahi","janissary","abus","cannon","culverin","mortar","caravan"})
            {
                var rig=Build(id);yield return null;
                var rendered=Visible(rig.transform);Assert.IsNotEmpty(rendered,id);
                Assert.That(rendered.Length,Is.LessThanOrEqualTo(24),"Detailed geometry must remain batched per independent rig group: "+id);
                foreach(var renderer in rendered)
                {
                    Assert.AreEqual("Batched rig surface",renderer.name,id);
                    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.AreEqual(1,mesh.subMeshCount,id+" must not reintroduce a draw per button or material");
                    Assert.AreEqual(mesh.vertexCount,mesh.uv.Length);Assert.AreEqual(mesh.vertexCount,mesh.normals.Length);
                    foreach(var v in mesh.vertices)Assert.IsFalse(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.sqrMagnitude),id);
                }
                if(rig.RightHand!=null)
                {
                    var hand=rig.RightHand.Find("Batched rig surface");Assert.IsNotNull(hand,id);
                    var before=hand.localToWorldMatrix.MultiplyPoint3x4(Vector3.zero);
                    UnitAnimation.Apply(rig,new AnimationPose{state=UnitPoseState.PreparingAttack,actionPhase=.6f,melee=id=="pike"||id=="cuirassier"||id=="spahi"});
                    Assert.Less(Vector3.Distance(hand.position,rig.RightHand.position),.0001f,id+" hand render must move with its real joint");
                    Assert.Greater(Vector3.Distance(before,hand.position),.01f,id);
                }
            }
        }

        [UnityTest] public IEnumerator HiddenToolsScimitarAndPaidFreightRemainAbsentFromTheActualRenderedBatches()
        {
            var janissary=Build("janissary");var worker=Build("coureur");var cart=Build("caravan");yield return null;
            UnitAnimation.Apply(janissary,new AnimationPose{state=UnitPoseState.PreparingAttack,melee=false,actionPhase=.5f});
            Assert.IsEmpty(Visible(janissary.MeleeWeapon));Assert.IsNotEmpty(Visible(janissary.Weapon));
            UnitAnimation.Apply(janissary,new AnimationPose{state=UnitPoseState.PreparingAttack,melee=true,actionPhase=.5f});
            Assert.IsNotEmpty(Visible(janissary.MeleeWeapon));Assert.IsEmpty(Visible(janissary.Weapon));
            worker.WorkerEquipment.Show(WorkerTool.Pick);
            Assert.IsNotEmpty(Visible(worker.WeaponPivot.Find("Pick")));Assert.IsEmpty(Visible(worker.WeaponPivot.Find("Axe")));
            cart.Cargo.gameObject.SetActive(false);Assert.IsEmpty(Visible(cart.Cargo));cart.Cargo.gameObject.SetActive(true);Assert.IsNotEmpty(Visible(cart.Cargo));
        }

        [Test] public void ProfiledCannonsHaveOpenBoresAndRaisedReinforceBandsOnTheirActualSurface()
        {
            foreach(string id in new[]{"cannon","culverin","mortar"})
            {
                var rig=Build(id);var mesh=rig.Barrel.GetComponent<MeshFilter>().sharedMesh;float top=mesh.bounds.max.y;
                var lip=mesh.vertices.Where(v=>Mathf.Abs(v.y-top)<.0001f).Select(v=>new Vector2(v.x,v.z).magnitude).ToArray();
                Assert.Greater(lip.Min(),.08f,id+" muzzle must have an actual open center");Assert.Greater(lip.Max(),lip.Min()*1.5f,id+" raised lip and inner bore must both exist");
                Assert.Greater(mesh.vertices.Select(v=>Mathf.Round(new Vector2(v.x,v.z).magnitude*10000)).Distinct().Count(),5,id+" profiled tube must have distinct reinforce and chase radii");
                Assert.IsNotNull(rig.Barrel.Find("Batched rig surface"));
            }
        }
    }
}
