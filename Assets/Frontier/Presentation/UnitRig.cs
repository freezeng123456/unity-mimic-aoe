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
        public float RiderSeatHeight=1.15f,ArtilleryTravelTilt=12,ArtilleryRecoil=.32f;
        Transform[] cached;Vector3[] initialPosition;Quaternion[] initialRotation;
        public void CaptureInitialPose(){cached=new[]{LeftShoulder,RightShoulder,LeftElbow,RightElbow,LeftHand,RightHand,LeftLeg,RightLeg,LeftKnee,RightKnee,WeaponPivot,Weapon,BowStringUpper,BowStringLower,Barrel,Carriage,LeftWheel,RightWheel,LeftHorseLeg,RightHorseLeg,RearLeftHorseLeg,RearRightHorseLeg,HorseHead,HorseTail};initialPosition=new Vector3[cached.Length];initialRotation=new Quaternion[cached.Length];for(int i=0;i<cached.Length;i++)if(cached[i]!=null){initialPosition[i]=cached[i].localPosition;initialRotation[i]=cached[i].localRotation;}}
        public void RestoreInitialPose(){if(cached==null)return;for(int i=0;i<cached.Length;i++)if(cached[i]!=null){cached[i].localPosition=initialPosition[i];cached[i].localRotation=initialRotation[i];}}
    }
}
