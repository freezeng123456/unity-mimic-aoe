using System;
using UnityEngine;
namespace Frontier
{
    public sealed class UnitRig:MonoBehaviour
    {
        [NonSerialized] public Transform LeftShoulder,RightShoulder,LeftElbow,RightElbow,LeftHand,RightHand,LeftLeg,RightLeg,WeaponPivot,Weapon;
        public WorkerEquipment WorkerEquipment;
        public Transform BowStringUpper,BowStringLower;
        public Transform Barrel,Carriage,LeftWheel,RightWheel,LeftHorseLeg,RightHorseLeg,RearLeftHorseLeg,RearRightHorseLeg;
        public Transform LeftKnee,RightKnee,HorseHead,HorseTail;
        public Transform Shield,Cargo,RepairWeapon;
        public Transform WorkerFirearm;
        public Transform SupplyCollectorWeapon;
        public Transform MeleeWeapon;
        public Transform[] CartWheels=Array.Empty<Transform>();
        public float RiderSeatHeight=1.15f,ArtilleryTravelTilt=12,ArtilleryRecoil=.32f;
        Transform[] cached;Vector3[] initialPosition;Quaternion[] initialRotation;
        AnimationPose lastPose;Quaternion basketOrientation;bool poseReady;
        public int PoseApplicationCount { get; private set; }
        public bool FoldingPavise { get; private set; }
        public void CaptureInitialPose(){var bones=new System.Collections.Generic.List<Transform>{LeftShoulder,RightShoulder,LeftElbow,RightElbow,LeftHand,RightHand,LeftLeg,RightLeg,LeftKnee,RightKnee,WeaponPivot,Weapon,BowStringUpper,BowStringLower,Barrel,Carriage,LeftWheel,RightWheel,LeftHorseLeg,RightHorseLeg,RearLeftHorseLeg,RearRightHorseLeg,HorseHead,HorseTail,Shield};bones.AddRange(CartWheels);cached=bones.ToArray();initialPosition=new Vector3[cached.Length];initialRotation=new Quaternion[cached.Length];for(int i=0;i<cached.Length;i++)if(cached[i]!=null){initialPosition[i]=cached[i].localPosition;initialRotation[i]=cached[i].localRotation;}FoldingPavise=Shield!=null&&Shield.name=="Folding pavise";poseReady=false;}
        public void RestoreInitialPose(){poseReady=false;if(cached==null)return;for(int i=0;i<cached.Length;i++)if(cached[i]!=null){if(!cached[i].localPosition.Equals(initialPosition[i]))cached[i].localPosition=initialPosition[i];if(!cached[i].localRotation.Equals(initialRotation[i]))cached[i].localRotation=initialRotation[i];}}
        internal bool BeginPose(AnimationPose pose)
        {
            // Fixed-step simulation can present the same pose several times. A
            // basket also depends on world orientation while its carrier turns.
            var orientation=pose.tool==WorkerTool.Basket?transform.rotation:Quaternion.identity;
            if(poseReady&&UnitAnimation.SameRigPose(lastPose,pose)&&basketOrientation.Equals(orientation))return false;
            RestoreInitialPose();lastPose=pose;basketOrientation=orientation;poseReady=true;PoseApplicationCount++;return true;
        }
    }
}
