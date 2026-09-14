using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class UnitRigTests
    {
        GameObject root;RuleCatalog rules;
        [SetUp] public void Setup(){root=new GameObject("Actual procedural rig test");rules=Catalog.Load();}
        [TearDown] public void Cleanup(){Object.DestroyImmediate(root);}
        UnitRig Build(string id)
        {
            var entity=new EntityState{id=1,owner=0,def=rules.Entity(id),hp=100,construction=1};
            return ProceduralArt.BuildEntity(entity,root.transform).GetComponent<UnitRig>();
        }
        [TestCase("musketeer")][TestCase("pike")][TestCase("longbow")]
        public void AuthoredRigHoldsWeaponAndRestoresPoseWithoutAccumulation(string id)
        {
            var rig=Build(id);var pose=new AnimationPose{state=UnitPoseState.PreparingAttack,actionPhase=.75f,bow=id=="longbow",polearm=id=="pike",melee=id=="pike"};
            rig.transform.rotation=Quaternion.Euler(0,37,0);UnitAnimation.Apply(rig,pose);
            var hand=id=="longbow"?rig.LeftHand:rig.RightHand;
            Assert.IsTrue(rig.WeaponPivot.IsChildOf(hand));Assert.IsTrue(rig.Weapon.IsChildOf(rig.WeaponPivot));
            Assert.IsTrue(rig.RightHand.IsChildOf(rig.RightElbow));Assert.IsTrue(rig.RightElbow.IsChildOf(rig.RightShoulder));
            Assert.That(Vector3.Distance(hand.position,rig.WeaponPivot.position),Is.LessThan(.001f));
            var handAt=hand.position;var weaponAt=rig.WeaponPivot.rotation;
            for(int i=0;i<100;i++)UnitAnimation.Apply(rig,pose);
            Assert.That(Vector3.Distance(handAt,hand.position),Is.LessThan(.00001f));Assert.That(Quaternion.Angle(weaponAt,rig.WeaponPivot.rotation),Is.LessThan(.001f));
            UnitAnimation.Apply(rig,new AnimationPose{state=UnitPoseState.Idle});UnitAnimation.Apply(rig,pose);
            Assert.That(Vector3.Distance(handAt,hand.position),Is.LessThan(.00001f));
            if(id!="longbow")
            {
                var shaft=id=="pike"?rig.Weapon:rig.Weapon.Find("Gun barrel");
                var direction=rig.transform.InverseTransformDirection(shaft.up);
                Assert.That(Vector3.Dot(direction,Vector3.forward),Is.GreaterThan(.999f),"Actual shaft axis must face the simulation's forward direction");
            }
        }
        [Test] public void BothBowStringSegmentsConnectStaveTipsToActualDrawingHand()
        {
            var rig=Build("longbow");var pose=new AnimationPose{state=UnitPoseState.PreparingAttack,actionPhase=.75f,bow=true};
            UnitAnimation.Apply(rig,pose);
            foreach(var segment in new[]{rig.BowStringUpper,rig.BowStringLower})
            {
                var near=segment.TransformPoint(Vector3.down*.5f);var far=segment.TransformPoint(Vector3.up*.5f);
                Assert.That(Vector3.Distance(near,rig.RightHand.position),Is.LessThan(.001f),"Nock must follow drawing hand, not merely share a hierarchy name");
                var expected=rig.Weapon.TransformPoint(new Vector3(0,segment==rig.BowStringUpper?.88f:-.88f,-.24f));
                Assert.That(Vector3.Distance(far,expected),Is.LessThan(.001f));
            }
            var drawn=rig.RightHand.position;pose.actionPhase=.1f;UnitAnimation.Apply(rig,pose);
            Assert.That(Vector3.Distance(drawn,rig.RightHand.position),Is.GreaterThan(.1f),"Different preparation times must visibly draw the string");
        }
    }
}
