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
                // A low pitched troop canopy leaves broad open side bays,
                // unlike the opaque fighting casemate on the monitor.
                foreach(float side in new[]{-1f,1f})
                {
                    foreach(float z in new[]{-1.25f,.05f})deck.Cylinder(new Vector3(side*.55f,.90f,z),.042f,1.10f,DarkWood,8);
                    var aft=new Vector3(side*.61f,1.45f,-1.37f);var fore=new Vector3(side*.61f,1.45f,.17f);
                    var aftRidge=new Vector3(0,1.72f,-1.37f);var foreRidge=new Vector3(0,1.72f,.17f);
                    deck.Quad(aft,fore,foreRidge,aftRidge,Canvas);deck.Quad(aftRidge,foreRidge,fore,aft,Canvas);
                    // Team cloth is a shallow valance, not a solid cabin wall.
                    var drop=Vector3.down*.14f;
                    deck.Quad(aft+drop,aft,fore,fore+drop,cloth);deck.Quad(fore+drop,fore,aft,aft+drop,cloth);
                }
                deck.Beam(new Vector3(0,1.72f,-1.37f),new Vector3(0,1.72f,.17f),.045f,.045f,PaleWood);
                deck.Box(new Vector3(0,.58f,-1.25f),new Vector3(1.05f,.35f,.09f),Wood);
                // A visible hatch and stowed boarding gangway distinguish the troop carrier.
                deck.Box(new Vector3(0,.43f,.55f),new Vector3(.73f,.12f,.94f),DarkWood);
                for(int i=0;i<6;i++)deck.Box(new Vector3(0,.51f,.18f+i*.13f),new Vector3(.7f,.045f,.045f),PaleWood);
                foreach(float x in new[]{-.67f,.67f})deck.Box(new Vector3(x,.57f,-.08f),new Vector3(.18f,.12f,1.45f),PaleWood);
                Barrel(deck,new Vector3(-.50f,.39f,1.22f),.17f,.39f);Barrel(deck,new Vector3(.48f,.39f,1.22f),.17f,.39f);
                Crew(deck,new Vector3(-.32f,.40f,1.0f),team,true);Crew(deck,new Vector3(.27f,.40f,-.74f),team,true);
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
                    // Thick folded canvas reads as two low rigging masses at
                    // strategy zoom, while every vertex remains under the flag.
                    var a=new Vector3(-.64f,1.84f,z-.04f);var b=new Vector3(.64f,1.84f,z-.04f);
                    var c=new Vector3(.48f,1.38f,z+.26f);var d=new Vector3(-.48f,1.38f,z+.26f);
                    var fold=Vector3.forward*.08f;
                    deck.Quad(a,b,c,d,Canvas);deck.Quad(d+fold,c+fold,b+fold,a+fold,Canvas);
                    deck.Quad(a+fold,b+fold,b,a,Canvas);deck.Quad(d,c,c+fold,d+fold,Canvas);
                    deck.Quad(a+fold,a,d,d+fold,Canvas);deck.Quad(b,b+fold,c+fold,c,Canvas);
                    deck.Beam(new Vector3(-.56f,1.61f,z+.10f),new Vector3(.56f,1.61f,z+.10f),.035f,.035f,Rope);
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
