using UnityEngine;
using Surface=Frontier.ArchitecturalMaterials.Surface;

namespace Frontier
{
    public static partial class WaterPresentation
    {
        static void BuildFleetVessel(Transform root,Color team,string role)
        {
            bool transport=role=="transport_ship",frigate=role=="frigate";
            var rig=root.gameObject.AddComponent<UnitRig>();var motion=root.gameObject.AddComponent<BoatPresentationMotion>();
            var hull=new ArchitecturalGeometry();var deck=new ArchitecturalGeometry();
            float width=transport?.92f:frigate?1.08f:1.03f,length=transport?2.18f:frigate?2.52f:2.35f;
            Hull(hull,width,length,team);
            var cloth=ArchitecturalMaterials.Get(Surface.Cloth,team);
            if(transport)
            {
                deck.Box(new Vector3(0,.58f,-.6f),new Vector3(1.12f,.65f,1.42f),Wood);
                deck.Box(new Vector3(0,.94f,-.6f),new Vector3(1.22f,.10f,1.54f),Canvas);
                foreach(float x in new[]{-.57f,.57f})for(int i=0;i<3;i++)deck.Box(new Vector3(x,.71f,-1.04f+i*.41f),new Vector3(.035f,.21f,.20f),Iron);
                // A visible hatch and stowed boarding gangway distinguish the troop carrier.
                deck.Box(new Vector3(0,.43f,.55f),new Vector3(.73f,.12f,.94f),DarkWood);
                for(int i=0;i<6;i++)deck.Box(new Vector3(0,.51f,.18f+i*.13f),new Vector3(.7f,.045f,.045f),PaleWood);
                foreach(float x in new[]{-.67f,.67f})deck.Box(new Vector3(x,.57f,-.08f),new Vector3(.18f,.12f,1.45f),PaleWood);
                Barrel(deck,new Vector3(-.50f,.39f,1.22f),.17f,.39f);Barrel(deck,new Vector3(.48f,.39f,1.22f),.17f,.39f);
                Crew(deck,new Vector3(-.32f,.40f,1.0f),team,true);Crew(deck,new Vector3(.27f,.99f,-.74f),team,true);
            }
            else if(frigate)
            {
                deck.Box(new Vector3(0,.51f,-.94f),new Vector3(1.20f,.35f,1.0f),Wood);
                for(int i=0;i<3;i++)foreach(float side in new[]{-1f,1f})
                {
                    float z=-.36f+i*.65f;deck.Box(new Vector3(side*.67f,.48f,z),new Vector3(.42f,.15f,.39f),DarkWood);
                    deck.Cylinder(new Vector3(side*.9f,.65f,z),.12f,.62f,Iron,12,Quaternion.Euler(0,0,90));
                    deck.Box(new Vector3(side*.98f,.38f,z),new Vector3(.09f,.29f,.45f),cloth);
                }
                // River masts are furled below bridge clearance, with two distinct rigging stations.
                foreach(float z in new[]{-.9f,.84f})
                {
                    deck.Cylinder(new Vector3(0,1.11f,z),.055f,1.55f,DarkWood,10);
                    deck.Cylinder(new Vector3(0,1.71f,z),.10f,1.35f,Canvas,10,Quaternion.Euler(0,0,90));
                    foreach(float side in new[]{-1f,1f})deck.Beam(new Vector3(0,1.88f,z),new Vector3(side*.86f,.47f,z+.45f),.022f,.022f,Rope);
                }
                Crew(deck,new Vector3(-.23f,.40f,-1.77f),team,true);Crew(deck,new Vector3(.25f,.70f,-.94f),team,true);
                var cannon=new GameObject("Frigate broadside recoil").transform;cannon.SetParent(root,false);cannon.localPosition=new Vector3(.65f,.64f,.2f);
                var iron=new ArchitecturalGeometry();iron.Cylinder(Vector3.zero,.12f,.61f,Iron,14,Quaternion.Euler(0,0,90));iron.Create(cannon,"Frigate recoil battery");rig.Barrel=cannon;rig.ArtilleryRecoil=.10f;
            }
            else
            {
                // Low armoured casemate and one large forward siege gun, without a frigate mast.
                deck.Box(new Vector3(0,.64f,-.62f),new Vector3(1.25f,.64f,1.50f),Iron);
                deck.Box(new Vector3(0,1.0f,-.62f),new Vector3(1.36f,.11f,1.63f),DarkWood);
                deck.Box(new Vector3(0,.56f,.72f),new Vector3(1.05f,.28f,.92f),Wood);
                var cannon=new GameObject("Monitor siege cannon recoil").transform;cannon.SetParent(root,false);cannon.localPosition=new Vector3(0,.85f,1.04f);cannon.localRotation=Quaternion.Euler(76,0,0);
                var tube=new ArchitecturalGeometry();tube.Lathe(Vector3.zero,new[]{new Vector2(.27f,-.58f),new Vector2(.30f,-.43f),new Vector2(.22f,.46f),new Vector2(.26f,.68f),new Vector2(.16f,.68f),new Vector2(.16f,.36f)},Iron,18);
                tube.Create(cannon,"Monitor large open siege bore");rig.Barrel=cannon;rig.ArtilleryRecoil=.18f;
                Barrel(deck,new Vector3(-.57f,.38f,-1.66f),.18f,.44f);Crew(deck,new Vector3(.35f,1.04f,-.87f),team,true);
            }
            float stern=-length*.79f;
            deck.Cylinder(new Vector3(.25f,1.18f,stern),.025f,1.72f,DarkWood,8);
            deck.Quad(new Vector3(.25f,2.02f,stern),new Vector3(.25f,1.7f,stern),new Vector3(.73f,1.73f,stern),new Vector3(.73f,1.99f,stern),cloth);
            deck.Quad(new Vector3(.73f,1.99f,stern),new Vector3(.73f,1.73f,stern),new Vector3(.25f,1.7f,stern),new Vector3(.25f,2.02f,stern),cloth);
            hull.Create(root,role+" original hull and deck");deck.Create(root,role+" role fittings and crew");BuildWake(root,motion,length);
            motion.HullLength=length;motion.Capture();
            rig.CaptureInitialPose();
        }
    }
}
