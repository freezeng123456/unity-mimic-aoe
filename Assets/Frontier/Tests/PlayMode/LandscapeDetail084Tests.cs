using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class LandscapeDetail084Tests
    {
        GameObject stage;
        [SetUp] public void SetUp(){stage=new GameObject("084 read-only bank dressing fixture");}
        [TearDown] public void TearDown(){if(stage!=null)Object.DestroyImmediate(stage);}

        [TestCase(MapTopology.Winding)] [TestCase(MapTopology.Tropical)]
        [TestCase(MapTopology.Tributaries)] [TestCase(MapTopology.Estuary)]
        public void SeededBankBatchesLeaveTheWorldUntouchedAndStayWithinTheDeclaredBudget(string map)
        {
            var world=new World(Catalog.Load(),42,"coast","coast",5,map);
            string initial=JsonUtility.ToJson(world.CaptureState());var random=Random.state;
            var first=WaterPresentation.BuildTerrain(stage.transform,world);
            var second=WaterPresentation.BuildTerrain(stage.transform,world);
            Assert.AreEqual(initial,JsonUtility.ToJson(world.CaptureState()),"Rendering must not change resources, fog, heights, navigation or orders");
            Assert.AreEqual(random,Random.state,"Dressing must not consume Unity's global random sequence");
            var a=first.Find("Seeded riverbank detail 084");var b=second.Find("Seeded riverbank detail 084");
            Assert.IsNotNull(a,"The ordinary opening must contain a visible-sized bank cluster, not only a shader change");Assert.IsNotNull(b);
            var batches=a.GetComponentsInChildren<MeshFilter>(true);var repeat=b.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(batches.Length,Is.InRange(1,12));Assert.AreEqual(batches.Length,repeat.Length);
            Assert.AreEqual(batches.Length,a.GetComponent<RiverSceneryVisibility>().ClusterCount);
            Assert.IsEmpty(a.GetComponentsInChildren<Collider>(true));
            int vertices=0;
            for(int i=0;i<batches.Length;i++)
            {
                var mesh=batches[i].sharedMesh;vertices+=mesh.vertexCount;
                Assert.LessOrEqual(mesh.vertexCount,900);Assert.LessOrEqual(mesh.subMeshCount,4);
                Assert.LessOrEqual(batches[i].GetComponent<MeshRenderer>().sharedMaterials.Length,4);
                Assert.AreSame(mesh,batches[i].GetComponent<ProceduralMeshLifetime>().OwnedMesh);
                Assert.IsNotNull(batches[i].GetComponent<LODGroup>());
                Assert.AreEqual(batches[i].transform.localPosition,repeat[i].transform.localPosition);
                CollectionAssert.AreEqual(mesh.vertices,repeat[i].sharedMesh.vertices);
                CollectionAssert.AreEqual(mesh.triangles,repeat[i].sharedMesh.triangles);
                var point=new Vector2(batches[i].transform.position.x,batches[i].transform.position.z);
                Assert.AreEqual(world.Layout.BankHeight(point),batches[i].transform.position.y,.000001f);
                Assert.IsFalse(world.IsWater(point));Assert.IsFalse(world.IsBridgeApproach(point));Assert.IsFalse(world.Layout.IsBridgeSideBarrier(point));
                Assert.LessOrEqual(mesh.bounds.max.y,1.35f,"Bank detail stays low and distinct from resource trees");
                Assert.AreEqual(world.Fog.At(0,point)==VisibilityState.Visible,batches[i].GetComponent<MeshRenderer>().enabled);
            }
            Assert.LessOrEqual(vertices,10800);
            var blockA=new MaterialPropertyBlock();var blockB=new MaterialPropertyBlock();
            first.Find("Continuous sculpted land and river bed").GetComponent<MeshRenderer>().GetPropertyBlock(blockA);
            second.Find("Continuous sculpted land and river bed").GetComponent<MeshRenderer>().GetPropertyBlock(blockB);
            Assert.AreEqual(blockA.GetVector("_DetailOrigin"),blockB.GetVector("_DetailOrigin"));
            TestContext.WriteLine("Actual "+map+": bank clusters="+batches.Length+", total vertices="+vertices+"; same seed reproduces geometry; full opening World JSON and Unity Random unchanged. This is a fixture/budget check, not visual acceptance.");
        }

        [UnityTest] public IEnumerator NewBankDetailUsesFogBuildingDeathAndOwnedMeshCleanup()
        {
            var world=new World(Catalog.Load(),42,"coast","coast",5,MapTopology.Winding);
            var terrain=WaterPresentation.BuildTerrain(stage.transform,world);
            var root=terrain.Find("Seeded riverbank detail 084");Assert.IsNotNull(root);
            var meshes=root.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).ToArray();Assert.IsNotEmpty(meshes);
            var cluster=root.GetComponentInChildren<MeshRenderer>(true);var visibility=root.GetComponent<RiverSceneryVisibility>();
            var point=new Vector2(cluster.transform.position.x,cluster.transform.position.z);
            world.Fog.Reveal(0,point,6);visibility.Refresh(true);Assert.IsTrue(cluster.enabled);
            var house=world.Spawn("house",0,point);Assert.AreEqual(point,house.position);visibility.Refresh(true);
            Assert.IsFalse(cluster.enabled,"A later building hides the complete cluster, rather than clipping through its grass or driftwood");
            world.ApplyDamage(house,100000,DamageKind.Siege,1);Assert.IsFalse(house.Alive);visibility.Refresh(true);Assert.IsTrue(cluster.enabled);
            Object.Destroy(terrain.gameObject);yield return null;yield return null;
            Assert.IsTrue(meshes.All(mesh=>mesh==null),"Replacing a map must release every new bank mesh");
            TestContext.WriteLine("Controlled Spawn/damage fixture: new bank cluster revealed, occupied, restored after death, then owned meshes released. This does not claim paid construction or a natural match.");
        }
    }
}
