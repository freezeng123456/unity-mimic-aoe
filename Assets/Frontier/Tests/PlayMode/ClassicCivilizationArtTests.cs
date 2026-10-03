using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ClassicCivilizationArtTests
    {
        private GameObject root;private RuleCatalog rules;
        [SetUp] public void Setup(){root=new GameObject("Classic civilization art validation");rules=Catalog.Load();}
        [TearDown] public void Cleanup(){Object.DestroyImmediate(root);}
        private EntityState Entity(string id)=>new EntityState{id=19,owner=0,def=rules.Entity(id),hp=100};
        private UnitRig Rig(EntityState entity)=>ProceduralArt.BuildEntity(entity,root.transform).GetComponent<UnitRig>();
        [Test] public void CoureurChangesFromGatheringToolToFirearmForRealAttackPose()
        {
            var e=Entity("coureur");var rig=Rig(e);var tree=Entity("tree");e.orders.Enqueue(new UnitOrder(OrderKind.Gather,Vector2.zero,tree.id));e.work=new WorkActivity{kind=WorkKind.Gather,target=tree.id,time=2};
            UnitAnimation.Apply(rig,UnitAnimation.Evaluate(e,2,workTarget:tree));Assert.AreEqual(WorkerTool.Axe,rig.WorkerEquipment.CurrentTool);Assert.IsFalse(rig.WorkerFirearm.gameObject.activeSelf);
            e.work=new WorkActivity();e.orders.Clear();e.fireAt=3.2f;e.shotMelee=false;var attack=UnitAnimation.Evaluate(e,3);UnitAnimation.Apply(rig,attack);
            Assert.AreEqual(UnitPoseState.PreparingAttack,attack.state);Assert.AreEqual(WorkerTool.None,rig.WorkerEquipment.CurrentTool);Assert.IsTrue(rig.WorkerFirearm.gameObject.activeSelf);
            Assert.Less(Vector3.Distance(rig.WeaponPivot.position,rig.RightHand.position),.001f);Assert.IsNotNull(rig.transform.Find("Coureur fur cap"));
        }
        [Test] public void JanissarySwitchesBetweenMusketAndScimitarWithoutLosingItsHandGrip()
        {
            var e=Entity("janissary");var rig=Rig(e);e.fireAt=2.2f;UnitAnimation.Apply(rig,UnitAnimation.Evaluate(e,2));Assert.IsTrue(rig.Weapon.gameObject.activeSelf);Assert.IsFalse(rig.MeleeWeapon.gameObject.activeSelf);
            e.forceMelee=true;UnitAnimation.Apply(rig,UnitAnimation.Evaluate(e,2));Assert.IsFalse(rig.Weapon.gameObject.activeSelf);Assert.IsTrue(rig.MeleeWeapon.gameObject.activeSelf);
            Assert.Less(Vector3.Distance(rig.MeleeWeapon.position,rig.RightHand.position),.001f);Assert.IsNotNull(rig.transform.Find("Janissary tall felt cap"));
        }
        [Test] public void AbusIsAnimatedInfantryWithHeavyHandCannonAndNoArtilleryCarriage()
        {
            var e=Entity("abus");var rig=Rig(e);Assert.IsNull(rig.Barrel);Assert.IsNull(rig.Carriage);Assert.IsNull(rig.LeftWheel);Assert.IsNotNull(rig.LeftLeg);
            var barrel=rig.Weapon.Find("Heavy hand cannon barrel");Assert.IsNotNull(barrel);Assert.Greater(barrel.GetComponent<MeshFilter>().sharedMesh.bounds.size.x,.25f);
            e.fireAt=2.2f;UnitAnimation.Apply(rig,UnitAnimation.Evaluate(e,2));Assert.Less(Vector3.Distance(rig.WeaponPivot.position,rig.RightHand.position),.001f);
            Assert.Greater(Vector3.Dot(rig.transform.InverseTransformDirection(barrel.up),Vector3.forward),.99f);
        }
        [Test] public void CuirassierHasSteelArmorAndSweepsItsSwordWithoutPoseAccumulation()
        {
            var e=Entity("cuirassier");var rig=Rig(e);Assert.IsNotNull(rig.LeftHorseLeg);Assert.IsNotNull(rig.transform.Find("French steel breastplate"));Assert.IsNotNull(rig.Weapon.Find("Straight steel blade"));
            var pose=new AnimationPose{state=UnitPoseState.Fire,melee=true,sweepingMelee=true,actionPhase=.25f};UnitAnimation.Apply(rig,pose);var first=rig.WeaponPivot.rotation;
            pose.actionPhase=.75f;UnitAnimation.Apply(rig,pose);Assert.Greater(Quaternion.Angle(first,rig.WeaponPivot.rotation),30);var final=rig.WeaponPivot.rotation;
            for(int i=0;i<25;i++)UnitAnimation.Apply(rig,pose);Assert.Less(Quaternion.Angle(final,rig.WeaponPivot.rotation),.001f);
        }
        [Test] public void SpahiHasMountedLamellarAndShieldSilhouetteDistinctFromCuirassier()
        {
            var rig=Rig(Entity("spahi"));Assert.IsNotNull(rig.LeftHorseLeg);Assert.IsNotNull(rig.Shield);Assert.IsNotNull(rig.transform.Find("Spahi helmet finial"));
            Assert.AreEqual(16,rig.GetComponentsInChildren<Transform>().Count(t=>t.name=="Spahi lamellar plate"));Assert.IsNull(rig.transform.Find("French steel breastplate"));
        }
        [Test] public void DutchHalberdKeepsWideAxeHeadAndPolearmThrustPose()
        {
            var e=Entity("halberdier");var rig=Rig(e);var blade=rig.WeaponPivot.Find("Dutch halberd axe blade");Assert.IsNotNull(blade);Assert.Greater(blade.GetComponent<MeshFilter>().sharedMesh.bounds.size.x,.4f);
            e.fireAt=2.2f;var pose=UnitAnimation.Evaluate(e,2);Assert.IsTrue(pose.polearm);UnitAnimation.Apply(rig,pose);
            Assert.Greater(Vector3.Dot(rig.transform.InverseTransformDirection(rig.Weapon.up),Vector3.forward),.99f);
        }
        [Test] public void MosqueHasDomedHallAndTallerMinaretInsideItsDistinctModel()
        {
            var model=ProceduralArt.BuildEntity(Entity("mosque"),root.transform);var dome=model.GetComponentsInChildren<Transform>().Single(t=>t.name=="Mosque central dome");
            Assert.Greater(dome.GetComponent<MeshFilter>().sharedMesh.bounds.size.y,.5f);var cap=model.GetComponentsInChildren<Transform>().Single(t=>t.name=="Pointed minaret cap");
            Assert.Greater(cap.GetComponent<Renderer>().bounds.max.y,dome.GetComponent<Renderer>().bounds.max.y);
            Assert.That(model.GetComponentsInChildren<Renderer>().Length,Is.InRange(4,16),"Detailed sections are material-batched");
            Assert.Greater(model.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertexCount),1500,"Dome, arcade and minaret require real geometry");
        }
    }
}
