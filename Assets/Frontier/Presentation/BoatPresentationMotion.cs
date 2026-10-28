using UnityEngine;

namespace Frontier
{
    // View-only boat animation. The owner supplies an evaluated simulation pose;
    // this component never moves the unit root or expands its collision radius.
    public sealed class BoatPresentationMotion : MonoBehaviour
    {
        public Transform SternOar,FishingGear,Rudder;
        public MeshRenderer Wake;
        public float HullLength=1.64f;
        Quaternion oarRotation,gearRotation,rudderRotation;
        Vector3 gearPosition;
        MaterialPropertyBlock wakeProperties;
        static readonly int Phase=Shader.PropertyToID("_Phase");
        public void Capture()
        {
            if(SternOar!=null)oarRotation=SternOar.localRotation;
            if(FishingGear!=null){gearRotation=FishingGear.localRotation;gearPosition=FishingGear.localPosition;}
            if(Rudder!=null)rudderRotation=Rudder.localRotation;
            wakeProperties=new MaterialPropertyBlock();
        }
        public void Apply(AnimationPose pose,float simulationTime)
        {
            float stroke=Mathf.Sin(pose.locomotionPhase);
            if(SternOar!=null)SternOar.localRotation=oarRotation*Quaternion.Euler(pose.moving?stroke*10:0,pose.moving?Mathf.Cos(pose.locomotionPhase)*9:0,0);
            if(Rudder!=null)Rudder.localRotation=rudderRotation*Quaternion.Euler(0,pose.moving?stroke*8:0,0);
            if(FishingGear!=null)
            {
                float gather=pose.state==UnitPoseState.Gather?Mathf.Sin(pose.actionPhase*Mathf.PI*2):0;
                FishingGear.localRotation=gearRotation*Quaternion.Euler(gather*16,0,gather*4);
                FishingGear.localPosition=gearPosition+Vector3.up*gather*.09f;
            }
            if(Wake!=null)
            {
                bool show=pose.moving&&pose.state!=UnitPoseState.Death;
                Wake.enabled=show;
                if(show)
                {
                    wakeProperties.SetFloat(Phase,simulationTime);
                    Wake.SetPropertyBlock(wakeProperties);
                }
            }
        }
    }
}
