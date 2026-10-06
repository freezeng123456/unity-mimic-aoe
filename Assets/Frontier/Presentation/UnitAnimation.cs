using UnityEngine;

namespace Frontier
{
    public enum UnitPoseState { Idle,Move,Gather,Build,Repair,PreparingAttack,Fire,Reloading,Deploying,Death }
    public struct AnimationPose
    {
        public UnitPoseState state;
        public WorkerTool tool;
        public bool moving,melee,bow,polearm,deployed,reloading;
        public bool shielded,loadedCargo,sweepingMelee;
        public float locomotionPhase,actionPhase,hurt,death;
    }
    // Read-only pose evaluation. Event times come from the incremental presentation
    // event cursor, while cooldown/assembly timers remain simulation-owned.
    public static class UnitAnimation
    {
        public static AnimationPose Evaluate(EntityState entity,float time,float firedAt=-1000,float hurtAt=-1000,float deathAt=-1000,EntityState workTarget=null)
        {
            var pose=new AnimationPose{state=UnitPoseState.Idle};if(entity==null)return pose;
            pose.melee=entity.shotMelee||entity.forceMelee||entity.def.damageKind==DamageKind.Melee;
            pose.bow=entity.def.id=="longbow";pose.polearm=entity.def.id=="pike"||entity.def.id=="escort"||entity.def.id=="shield_guard"||entity.def.id=="halberdier";pose.deployed=entity.isDeployed;
            pose.shielded=entity.shielded;pose.loadedCargo=entity.cargoPaid.food+entity.cargoPaid.wood+entity.cargoPaid.coin>0;
            pose.sweepingMelee=entity.def.meleeSplash>0;
            pose.hurt=Mathf.Clamp01(1-(time-hurtAt)/.2f);
            if(!entity.Alive){pose.state=UnitPoseState.Death;pose.death=Mathf.Clamp01((time-deathAt)/.65f);return pose;}
            bool moving=(entity.status=="Moving"||entity.def.id=="caravan")&&(entity.position-entity.previousPosition).sqrMagnitude>.0001f;
            pose.moving=moving;pose.locomotionPhase=time*Mathf.Max(.5f,entity.def.speed)*2.2f+entity.id*.6f;
            pose.reloading=entity.fireAt<0&&time<entity.attackReadyAt;
            if(time-firedAt>=0&&time-firedAt<.22f)
            {pose.state=UnitPoseState.Fire;pose.actionPhase=(time-firedAt)/.22f;}
            else if(entity.fireAt>=time&&entity.fireAt>=0)
            {pose.state=UnitPoseState.PreparingAttack;pose.actionPhase=1-Mathf.Clamp01((entity.fireAt-time)/Mathf.Max(.1f,entity.def.windup));}
            else if(entity.deployUntil>time&&!entity.isDeployed)
            {pose.state=UnitPoseState.Deploying;pose.actionPhase=1-Mathf.Clamp01((entity.deployUntil-time)/2);}
            else if(moving)pose.state=UnitPoseState.Move;
            else if(entity.IsWorkingAt(time))
            {
                pose.state=entity.work.kind==WorkKind.Gather?UnitPoseState.Gather:entity.work.kind==WorkKind.Build||entity.work.kind==WorkKind.Manufacture?UnitPoseState.Build:UnitPoseState.Repair;
                pose.tool=entity.work.kind!=WorkKind.Gather?WorkerTool.Hammer:workTarget?.def.id=="tree"?WorkerTool.Axe:workTarget?.def.id=="mine"?WorkerTool.Pick:WorkerTool.Basket;
                pose.actionPhase=Mathf.Repeat(time*(pose.tool==WorkerTool.Hammer?1.3f:pose.tool==WorkerTool.Pick?.85f:1.1f)+entity.id*.13f,1);
            }
            else if(pose.reloading)pose.state=UnitPoseState.Reloading;
            if(pose.reloading&&pose.state!=UnitPoseState.Fire&&pose.state!=UnitPoseState.PreparingAttack&&pose.tool==WorkerTool.None)
            {
                float began=entity.attackReadyAt-Mathf.Max(entity.def.period,entity.def.windup+.1f)+entity.def.windup;
                pose.actionPhase=Mathf.InverseLerp(began,entity.attackReadyAt,time);
            }
            if(entity.Worker&&(entity.def.id!="coureur"||pose.melee)&&(pose.state==UnitPoseState.PreparingAttack||pose.state==UnitPoseState.Fire||pose.state==UnitPoseState.Reloading))pose.tool=WorkerTool.Axe;
            return pose;
        }
        public static void Apply(UnitRig rig,AnimationPose pose)
        {
            if(rig==null||!rig.BeginPose(pose))return;rig.WorkerEquipment?.Show(pose.tool);
            SetActive(rig.RepairWeapon,pose.tool==WorkerTool.None);
            SetActive(rig.WorkerFirearm,pose.tool==WorkerTool.None);
            SetActive(rig.SupplyCollectorWeapon,pose.state!=UnitPoseState.Gather);
            if(rig.MeleeWeapon!=null){SetActive(rig.Weapon,!pose.melee);SetActive(rig.MeleeWeapon,pose.melee);}
            SetActive(rig.Cargo,pose.loadedCargo);
            if(rig.Shield!=null&&rig.FoldingPavise)
            {rig.Shield.localPosition=pose.shielded?new Vector3(0,.68f,.51f):new Vector3(-.48f,.90f,.06f);rig.Shield.localRotation=Quaternion.Euler(0,pose.shielded?0:65,0);}
            foreach(var wheel in rig.CartWheels)if(pose.moving)Rotate(wheel,0,pose.locomotionPhase*57,0);
            float stride=Mathf.Sin(pose.locomotionPhase),recoil=pose.state==UnitPoseState.Fire?Mathf.Sin(pose.actionPhase*Mathf.PI):0;
            bool mounted=rig.LeftHorseLeg!=null,gun=rig.Barrel!=null;
            if(gun)
            {
                if(rig.Barrel!=null){rig.Barrel.localPosition-=(rig.Barrel.localRotation*Vector3.up)*recoil*rig.ArtilleryRecoil;rig.Barrel.localRotation*=Quaternion.Euler(pose.deployed?0:pose.state==UnitPoseState.Deploying?rig.ArtilleryTravelTilt*(1-pose.actionPhase):rig.ArtilleryTravelTilt,0,0);}
                if(pose.moving){Rotate(rig.LeftWheel,0,pose.locomotionPhase*57,0);Rotate(rig.RightWheel,0,pose.locomotionPhase*57,0);}
                return;
            }
            if(pose.moving)
            {
                if(!mounted){Rotate(rig.LeftLeg,stride*28,0,0);Rotate(rig.RightLeg,-stride*28,0,0);}
                else
                {
                    Rotate(rig.LeftHorseLeg,stride*24,0,0);Rotate(rig.RearRightHorseLeg,stride*24,0,0);
                    Rotate(rig.RightHorseLeg,-stride*24,0,0);Rotate(rig.RearLeftHorseLeg,-stride*24,0,0);
                    Rotate(rig.HorseHead,stride*3,0,0);Rotate(rig.HorseTail,0,stride*12,0);
                }
            }
            if(pose.state==UnitPoseState.Gather||pose.state==UnitPoseState.Build||pose.state==UnitPoseState.Repair)
            {
                WorkerPose(rig,pose);
            }
            else if(pose.state==UnitPoseState.PreparingAttack||pose.state==UnitPoseState.Fire)
            {
                if(pose.polearm)
                {
                    float thrust=pose.state==UnitPoseState.Fire?.1f*Mathf.Sin(pose.actionPhase*Mathf.PI):0;
                    Arm(rig,false,new Vector3(.18f,1.07f,.3f+thrust));Arm(rig,true,new Vector3(.18f,1.07f,.51f+thrust));
                    WeaponRotation(rig,Quaternion.Euler(90,0,0));
                }
                else if(pose.melee)
                {
                    float swing=pose.state==UnitPoseState.Fire?pose.actionPhase:0;
                    Arm(rig,false,new Vector3(.24f+(pose.sweepingMelee?.3f*Mathf.Sin(swing*Mathf.PI):0),1.48f-.5f*swing,.3f));
                    WeaponRotation(rig,Quaternion.Euler(Mathf.Lerp(20,125,swing),pose.sweepingMelee?Mathf.Lerp(-40,40,swing):0,-20));
                }
                else if(pose.bow)
                {
                    float draw=pose.state==UnitPoseState.Fire?1-recoil:Mathf.Clamp01(pose.actionPhase);
                    Arm(rig,true,new Vector3(-.22f,1.28f,.65f));
                    WeaponRotation(rig,Quaternion.identity);
                    var grip=rig.transform.InverseTransformPoint(rig.LeftHand.position);
                    var nock=new Vector3(grip.x,grip.y,grip.z-.24f-.32f*draw);
                    Arm(rig,false,nock-new Vector3(0,RiderHeight(rig),0));
                }
                else
                {
                    Arm(rig,false,new Vector3(.12f,1.18f,.31f-recoil*.06f));Arm(rig,true,new Vector3(.12f,1.18f,.57f-recoil*.06f));
                    WeaponRotation(rig,Quaternion.identity);
                }
            }
            else if(pose.reloading)
            {
                float load=Mathf.Sin(pose.actionPhase*Mathf.PI*4);
                if(pose.melee){Arm(rig,false,new Vector3(.24f,1.05f,.3f));WeaponRotation(rig,Quaternion.Euler(pose.polearm?12:25,0,0));}
                else if(pose.bow){Arm(rig,true,new Vector3(-.22f,1.1f,.35f));Arm(rig,false,new Vector3(-.12f,1.15f,.14f));WeaponRotation(rig,Quaternion.Euler(0,0,-12));}
                else{Arm(rig,false,new Vector3(.2f,.96f,.35f));Arm(rig,true,new Vector3(.17f,1.36f+load*.12f,.4f));WeaponRotation(rig,Quaternion.Euler(-65,0,0));}
            }
            else if(mounted)
            {
                // Keep the seated leg pose and reins hand while the horse walks.
                Arm(rig,true,new Vector3(-.2f,1.04f,.45f));
                Arm(rig,false,new Vector3(.24f,1.10f,.26f));
                WeaponRotation(rig,pose.melee?Quaternion.Euler(15,0,-16):Quaternion.Euler(-28,0,0));
            }
            else
            {Rotate(rig.RightShoulder,-18,0,-6);Rotate(rig.LeftShoulder,-12,0,6);}
            if(rig.BowStringUpper!=null)
            {
                Vector3 nock=new Vector3(0,0,-.24f);
                if(pose.state==UnitPoseState.PreparingAttack||pose.state==UnitPoseState.Fire)nock=rig.Weapon.InverseTransformPoint(rig.RightHand.position);
                StringSegment(rig.BowStringUpper,new Vector3(0,.88f,-.24f),nock);
                StringSegment(rig.BowStringLower,new Vector3(0,-.88f,-.24f),nock);
            }
        }
        internal static bool SameRigPose(AnimationPose a,AnimationPose b)
        {
            return a.state==b.state&&a.tool==b.tool&&a.moving==b.moving&&a.melee==b.melee&&a.bow==b.bow&&a.polearm==b.polearm&&a.deployed==b.deployed&&a.reloading==b.reloading&&a.shielded==b.shielded&&a.loadedCargo==b.loadedCargo&&a.sweepingMelee==b.sweepingMelee
                &&(!a.moving||a.locomotionPhase==b.locomotionPhase)
                &&((!a.reloading&&(a.state==UnitPoseState.Idle||a.state==UnitPoseState.Move||a.state==UnitPoseState.Death))||a.actionPhase==b.actionPhase);
        }
        private static void SetActive(Transform part,bool active)
        {if(part!=null&&part.gameObject.activeSelf!=active)part.gameObject.SetActive(active);}
        private static void WorkerPose(UnitRig rig,AnimationPose pose)
        {
            float lift=.5f-.5f*Mathf.Cos(pose.actionPhase*Mathf.PI*2);
            if(pose.tool==WorkerTool.Basket)
            {
                Arm(rig,true,new Vector3(-.42f,.87f,.27f));
                Arm(rig,false,Vector3.Lerp(new Vector3(.38f,.9f,.57f),new Vector3(-.15f,.9f,.3f),lift));
                rig.WorkerEquipment?.KeepBasketUpright(rig.transform.rotation);
            }
            else if(pose.tool==WorkerTool.Hammer)
            {
                Arm(rig,false,new Vector3(.38f,1.05f+lift*.27f,.5f-lift*.08f));
                WeaponRotation(rig,Quaternion.Euler(Mathf.Lerp(100,10,lift),0,-8));
                Arm(rig,true,new Vector3(-.26f,1.02f,.57f));
            }
            else
            {
                bool pick=pose.tool==WorkerTool.Pick;
                Arm(rig,false,new Vector3(.4f,Mathf.Lerp(pick?.93f:1.14f,1.67f,lift),Mathf.Lerp(.51f,.16f,lift)));
                WeaponRotation(rig,Quaternion.Euler(Mathf.Lerp(pick?140:95,-25,lift),0,-12));
                Arm(rig,true,new Vector3(-.26f,pick?.88f:1.08f,.35f));
            }
        }
        private static float RiderHeight(UnitRig rig)=>rig.LeftHorseLeg!=null?rig.RiderSeatHeight:0;
        // Rigid two-link arm: solve actual geometry, then place the weapon in model space.
        // Target coordinates are authored in the standing figure's frame; riders inherit their seat offset.
        private static void Arm(UnitRig rig,bool left,Vector3 target)
        {
            var shoulder=left?rig.LeftShoulder:rig.RightShoulder;var elbow=left?rig.LeftElbow:rig.RightElbow;
            if(shoulder==null||elbow==null)return;target.y+=RiderHeight(rig);
            var start=rig.transform.InverseTransformPoint(shoulder.position);var delta=target-start;
            float distance=Mathf.Clamp(delta.magnitude,.021f,.699f);var direction=delta.normalized;
            float along=(.36f*.36f-.34f*.34f+distance*distance)/(2*distance);
            float height=Mathf.Sqrt(Mathf.Max(0,.36f*.36f-along*along));
            var bend=Vector3.ProjectOnPlane(new Vector3(left?-1:1,-.5f,-.5f),direction).normalized;
            var middle=start+direction*along+bend*height;var end=start+direction*distance;
            shoulder.rotation=rig.transform.rotation*Quaternion.FromToRotation(Vector3.down,middle-start);
            elbow.rotation=rig.transform.rotation*Quaternion.FromToRotation(Vector3.down,end-middle);
        }
        private static void WeaponRotation(UnitRig rig,Quaternion rotation){if(rig.WeaponPivot!=null)rig.WeaponPivot.rotation=rig.transform.rotation*rotation;}
        private static void StringSegment(Transform segment,Vector3 end,Vector3 nock)
        {if(segment==null)return;var delta=end-nock;segment.localPosition=(end+nock)*.5f;segment.localRotation=Quaternion.FromToRotation(Vector3.up,delta);segment.localScale=new Vector3(.014f,delta.magnitude,.014f);}
        private static void Rotate(Transform target,float x,float y,float z){if(target!=null)target.localRotation*=Quaternion.Euler(x,y,z);}
    }
}
