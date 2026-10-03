using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class CivilizationExpansionArtTests
    {
        private GameObject root;private RuleCatalog rules;
        [SetUp] public void Setup(){root=new GameObject("Expansion art validation");rules=Catalog.Load();}
        [TearDown] public void Cleanup(){Object.DestroyImmediate(root);}
        private EntityState Entity(string id)=>new EntityState{id=17,owner=0,def=rules.Entity(id),hp=100};
        private UnitRig Rig(EntityState entity)=>ProceduralArt.BuildEntity(entity,root.transform).GetComponent<UnitRig>();
        [Test] public void FreightWheelsMoveAndPaidCargoAppearsOnlyAfterLoading()
        {
            var e=Entity("caravan");var rig=Rig(e);e.position=Vector2.one;e.previousPosition=Vector2.zero;
            var pose=UnitAnimation.Evaluate(e,1);var initial=rig.CartWheels[0].localRotation;UnitAnimation.Apply(rig,pose);
            Assert.IsFalse(rig.Cargo.gameObject.activeSelf);Assert.Greater(Quaternion.Angle(initial,rig.CartWheels[0].localRotation),1);
            e.cargoPaid=new Cost(60,0,0);pose=UnitAnimation.Evaluate(e,2);UnitAnimation.Apply(rig,pose);Assert.IsTrue(rig.Cargo.gameObject.activeSelf);
            var rotation=rig.CartWheels[0].localRotation;for(int i=0;i<20;i++)UnitAnimation.Apply(rig,pose);Assert.Less(Quaternion.Angle(rotation,rig.CartWheels[0].localRotation),.001f);
        }
        [Test] public void PaviseVisiblyMovesToFrontWhenBracedAndRestoresFoldedPosition()
        {
            var e=Entity("shield_guard");var rig=Rig(e);UnitAnimation.Apply(rig,UnitAnimation.Evaluate(e,1));var folded=rig.Shield.localPosition;
            e.shielded=true;UnitAnimation.Apply(rig,UnitAnimation.Evaluate(e,2));Assert.Greater(Vector3.Distance(folded,rig.Shield.localPosition),.4f);Assert.Greater(rig.Shield.localPosition.z,.4f);
            e.shielded=false;UnitAnimation.Apply(rig,UnitAnimation.Evaluate(e,3));Assert.Less(Vector3.Distance(folded,rig.Shield.localPosition),.001f);
        }
        [Test] public void FieldEngineerRepairsWithHammerInsteadOfFiringItsWeapon()
        {
            var e=Entity("field_engineer");e.orders.Enqueue(new UnitOrder(OrderKind.Repair,Vector2.zero,4));e.work=new WorkActivity{kind=WorkKind.Repair,target=4,time=3};var rig=Rig(e);
            var pose=UnitAnimation.Evaluate(e,3);Assert.AreEqual(UnitPoseState.Repair,pose.state);UnitAnimation.Apply(rig,pose);
            Assert.AreEqual(WorkerTool.Hammer,rig.WorkerEquipment.CurrentTool);Assert.IsFalse(rig.RepairWeapon.gameObject.activeSelf);
            e.work=new WorkActivity();UnitAnimation.Apply(rig,UnitAnimation.Evaluate(e,4));Assert.IsTrue(rig.RepairWeapon.gameObject.activeSelf);Assert.AreEqual(WorkerTool.None,rig.WorkerEquipment.CurrentTool);
        }
        [Test] public void CounterGunHasLongerNarrowerBarrelThanFieldCannon()
        {
            var cannon=Rig(Entity("cannon"));var culverin=Rig(Entity("culverin"));
            var standard=cannon.Barrel.GetComponent<MeshFilter>().sharedMesh.bounds;var counter=culverin.Barrel.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.Greater(counter.size.y,standard.size.y*1.4f);Assert.Less(counter.size.x,standard.size.x*.8f);
        }
        [TestCase("waystation", "Open caravan courtyard")][TestCase("workshop", "Workshop loading crane")]
        public void NewBuildingsRetainTheirDistinctStructuralSilhouettes(string id,string landmark)
        {
            var model=ProceduralArt.BuildEntity(Entity(id),root.transform);var detail=model.GetComponentsInChildren<Transform>().Single(t=>t.name==landmark);
            Assert.Greater(detail.GetComponentsInChildren<Renderer>().Length,0);Assert.Greater(model.GetComponentsInChildren<Renderer>().Length,15);
            foreach(var mesh in model.GetComponentsInChildren<MeshFilter>())Assert.Greater(mesh.sharedMesh.vertexCount,0);
        }
    }
}
