using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Frontier.Tests
{
    public sealed class ImportedCharacterTests
    {
        [UnityTest] public IEnumerator ImportedAxesAndActualArmSurfacesFollowTheGameplayJoint()
        {
            var holder=new GameObject("Imported character validation");
            try
            {
                var prefab=Resources.Load<GameObject>("Models/FrontierInfantry");Assert.IsNotNull(prefab);
                var imported=Object.Instantiate(prefab,holder.transform);
                foreach(var side in new[]{"Left","Right"})
                {
                    float x=side=="Left"?-.3f:.3f;
                    foreach(var point in new[]{(" shoulder",1.25f),(" elbow",.89f),(" hand",.55f)})
                    {
                        var joint=imported.GetComponentsInChildren<Transform>().Single(t=>t.name==side+point.Item1);
                        Assert.Less(Vector3.Distance(holder.transform.InverseTransformPoint(joint.position),new Vector3(x,point.Item2,.05f)),.001f,"Source FBX joints must preserve Unity axes and scale: "+joint.name);
                    }
                }
                Object.Destroy(imported);yield return null;
                var e=new EntityState{id=1,owner=0,def=Catalog.Load().Entity("musketeer"),hp=100};var model=ProceduralArt.BuildEntity(e,holder.transform);yield return null;
                var rig=model.GetComponent<UnitRig>();var handMesh=rig.RightHand.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name.Contains("Hand"));
                Assert.Less(Vector3.Distance(handMesh.bounds.center,rig.RightHand.position),.09f,"Rest hand surface must be at the same position as its actual grip joint");
                UnitAnimation.Apply(rig,new AnimationPose{state=UnitPoseState.PreparingAttack,actionPhase=.75f});
                Assert.Less(Vector3.Distance(handMesh.bounds.center,rig.RightHand.position),.09f,"Aiming hand surface must move with the real grip");
                var skin=model.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name.Contains("Skin"));var hair=model.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name.Contains("Hair"));
                Assert.Greater(model.InverseTransformPoint(skin.bounds.center).z,model.InverseTransformPoint(hair.bounds.center).z,"Face must be ahead of the hair at the nape");
                var renderers=model.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                Assert.That(bounds.size.y,Is.InRange(1.8f,2.05f));
            }
            finally{Object.Destroy(holder);}
            yield return null;
        }
    }
}
