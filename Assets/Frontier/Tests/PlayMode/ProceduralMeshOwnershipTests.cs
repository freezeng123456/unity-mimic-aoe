using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class ProceduralMeshOwnershipTests
    {
        [UnityTest] public IEnumerator NeverActivatedLeavesReleaseThroughTheirModelOwnerAndKeepImportedAssets()
        {
            var stage=new GameObject("Surviving pool stage");var root=new GameObject("Runtime mesh owner regression");root.transform.SetParent(stage.transform,false);
            var imported=Resources.Load<GameObject>("Models/FrontierInfantry").GetComponentsInChildren<MeshFilter>().First().sharedMesh;
            var hidden=new GameObject("Never active scaffold");hidden.transform.SetParent(root.transform,false);hidden.SetActive(false);
            var mesh=new Mesh{name="Never-activated generated scaffold"};mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.triangles=new[]{0,1,2};
            hidden.AddComponent<MeshFilter>().sharedMesh=mesh;hidden.AddComponent<ProceduralMeshLifetime>().OwnedMesh=mesh;
            var asset=new GameObject("Shared imported mesh");asset.transform.SetParent(hidden.transform,false);asset.AddComponent<MeshFilter>().sharedMesh=imported;
            yield return null;
            Assert.IsNotNull(root.GetComponent<ProceduralMeshRootOwner>());
            root.SetActive(false);yield return null;Assert.IsTrue(mesh!=null,"Pool deactivation must preserve generated surfaces");
            root.SetActive(true);Object.Destroy(root);yield return null;yield return null;
            Assert.IsTrue(stage!=null,"The parent pool remains alive while the entity model is released");
            Assert.IsTrue(mesh==null,"Never-activated scaffold mesh must be released with its model, not deferred until stage teardown");
            Object.Destroy(stage);
            Assert.IsTrue(imported!=null,"Unregistered imported FBX asset must remain loaded");
        }

        [UnityTest] public IEnumerator NeverActivatedLeafDestroyedEarlyIsStillReleasedByItsActiveAncestor()
        {
            var stage=new GameObject("Surviving stage after hidden-child removal");var root=new GameObject("Hidden temporary mesh owner regression");root.transform.SetParent(stage.transform,false);
            var hidden=new GameObject("Hidden abandoned geometry");hidden.transform.SetParent(root.transform,false);hidden.SetActive(false);
            var mesh=new Mesh{name="Generated mesh from removed hidden child"};mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.triangles=new[]{0,1,2};
            hidden.AddComponent<ProceduralMeshLifetime>().OwnedMesh=mesh;
            Object.Destroy(hidden);yield return null;yield return null;
            Object.Destroy(root);yield return null;yield return null;
            Assert.IsTrue(stage!=null);
            Assert.IsTrue(mesh==null,"Destroying a never-active leaf must not orphan its registered mesh when its model is later destroyed");
            Object.Destroy(stage);
        }
    }
}
