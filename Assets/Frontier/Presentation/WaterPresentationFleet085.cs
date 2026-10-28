using UnityEngine;
using Surface=Frontier.ArchitecturalMaterials.Surface;

namespace Frontier
{
    // River silhouettes stay under their existing pennants and bridge limits.
    // All fixed fittings join the original batches; motion and gun pivots remain owned by their existing rigs.
    public static partial class WaterPresentation
    {
        static Material HullShadow085=>ArchitecturalMaterials.Get(Surface.Wood,new Color(.19f,.14f,.095f));
        static Material NavalWale085=>ArchitecturalMaterials.Get(Surface.Wood,new Color(.17f,.24f,.23f));
        static Material WorkingHull085=>ArchitecturalMaterials.Get(Surface.Wood,new Color(.56f,.48f,.32f));
        static Material WorkingCanvas085=>ArchitecturalMaterials.Get(Surface.Cloth,new Color(.72f,.66f,.49f));

        static Material HullStrake085(string role,int ring,Material paint)
        {
            if(ring==3)return paint;
            if(role=="frigate")return ring==0?HullShadow085:ring==1?Wood:NavalWale085;
            if(role=="transport_ship")return ring==0?DarkWood:ring==1?Wood:PaleWood;
            if(role=="fishing_boat")return ring==0?DarkWood:ring==1?Wood:WorkingHull085;
            return ring%2==0?Wood:DarkWood;
        }

        static void BuildFishingCanopy085(ArchitecturalGeometry cargo)
        {
            // One low tent fold replaces the old flat sheet; the four original
            // posts, open working deck, animated basket and sculling oar remain unchanged.
            var leftA=new Vector3(-.52f,1.43f,-.90f);var leftB=new Vector3(-.52f,1.43f,.29f);
            var rightA=new Vector3(.52f,1.49f,-.90f);var rightB=new Vector3(.52f,1.49f,.29f);
            var ridgeA=new Vector3(-.04f,1.61f,-.90f);var ridgeB=new Vector3(-.04f,1.61f,.29f);
            cargo.Quad(leftA,leftB,ridgeB,ridgeA,Canvas);
            cargo.Quad(ridgeA,ridgeB,rightB,rightA,Canvas);
            cargo.Quad(ridgeA,ridgeB,leftB,leftA,Canvas);
            cargo.Quad(rightA,rightB,ridgeB,ridgeA,Canvas);
            cargo.Beam(ridgeA,ridgeB,.045f,.045f,DarkWood);
            cargo.Beam(leftA,leftB,.045f,.055f,PaleWood);
            cargo.Beam(rightA,rightB,.045f,.055f,PaleWood);
        }

        static void BuildTransportCanopy085(ArchitecturalGeometry deck,Material cloth)
        {
            // The existing open troop/cargo bays stay hollow. A warm awning is
            // distinct from both the frigate's two square rigs and the fishing tent.
            foreach(float side in new[]{-1f,1f})
            {
                foreach(float z in new[]{-1.25f,.05f})deck.Cylinder(new Vector3(side*.55f,.90f,z),.042f,1.10f,DarkWood,8);
                var aft=new Vector3(side*.61f,1.45f,-1.37f);var fore=new Vector3(side*.61f,1.45f,.17f);
                var aftRidge=new Vector3(0,1.72f,-1.37f);var foreRidge=new Vector3(0,1.72f,.17f);
                deck.Quad(aft,fore,foreRidge,aftRidge,WorkingCanvas085);deck.Quad(aftRidge,foreRidge,fore,aft,WorkingCanvas085);
                var drop=Vector3.down*.14f;
                deck.Quad(aft+drop,aft,fore,fore+drop,cloth);deck.Quad(fore+drop,fore,aft,aft+drop,cloth);
                deck.Beam(aft,fore,.065f,.065f,DarkWood);
            }
            deck.Beam(new Vector3(0,1.72f,-1.37f),new Vector3(0,1.72f,.17f),.065f,.065f,DarkWood);
        }

        static void BuildFrigateRig085(ArchitecturalGeometry deck,Material cloth)
        {
            foreach(float z in new[]{-.9f,.84f})
            {
                // Original mast stations and height; broad shallow canvas folds
                // curve forward, rather than reading as two rigid cream boxes.
                deck.Cylinder(new Vector3(0,1.11f,z),.055f,1.55f,DarkWood,10);
                var top=new[]{new Vector3(-.64f,1.84f,z-.04f),new Vector3(0,1.84f,z+.015f),new Vector3(.64f,1.84f,z-.04f)};
                var foot=new[]{new Vector3(-.48f,1.38f,z+.26f),new Vector3(0,1.43f,z+.38f),new Vector3(.48f,1.38f,z+.26f)};
                var fold=Vector3.forward*.08f;
                for(int i=0;i<2;i++)
                {
                    var a=top[i];var b=top[i+1];var c=foot[i+1];var d=foot[i];
                    var lowerA=Vector3.Lerp(d,a,.22f);var lowerB=Vector3.Lerp(c,b,.22f);
                    deck.Quad(a,b,lowerB,lowerA,Canvas);deck.Quad(lowerA,lowerB,c,d,cloth);
                    deck.Quad(d+fold,c+fold,b+fold,a+fold,Canvas);
                    deck.Quad(a+fold,b+fold,b,a,Canvas);deck.Quad(d,c,c+fold,d+fold,Canvas);
                }
                deck.Quad(top[0]+fold,top[0],foot[0],foot[0]+fold,Canvas);
                deck.Quad(top[2],top[2]+fold,foot[2]+fold,foot[2],Canvas);
                deck.Beam(new Vector3(-.64f,1.85f,z-.04f),new Vector3(.64f,1.85f,z-.04f),.055f,.055f,DarkWood);
                deck.Beam(new Vector3(-.56f,1.61f,z+.10f),new Vector3(.56f,1.61f,z+.10f),.035f,.035f,Rope);
                foreach(float side in new[]{-1f,1f})deck.Beam(new Vector3(0,1.88f,z),new Vector3(side*.86f,.47f,z+.45f),.022f,.022f,Rope);
            }
        }

        static void RefineFleetRole085(ArchitecturalGeometry deck,string role,Material cloth)
        {
            if(role=="frigate")
            {
                // Six genuine gaps surround the six existing cannon tubes; no
                // opaque black panel is drawn across a gun muzzle or recoil pivot.
                foreach(float side in new[]{-1f,1f})
                {
                    float x=side*.92f;
                    deck.Box(new Vector3(x,.36f,.29f),new Vector3(.10f,.07f,2.16f),DarkWood);
                    deck.Box(new Vector3(x,.86f,.29f),new Vector3(.10f,.11f,2.16f),PaleWood);
                    foreach(float z in new[]{-.69f,-.035f,.615f,1.27f})
                        deck.Box(new Vector3(x,.64f,z),new Vector3(.10f,.38f,.20f),DarkWood);
                    deck.Box(new Vector3(side*.975f,.49f,.29f),new Vector3(.035f,.09f,2.16f),cloth);
                }
                // One clear quarterdeck rim, kept inside the existing aft house.
                foreach(float side in new[]{-1f,1f})deck.Box(new Vector3(side*.57f,.735f,-.94f),new Vector3(.065f,.10f,1.0f),PaleWood);
                deck.Box(new Vector3(0,.735f,-1.40f),new Vector3(1.18f,.10f,.065f),PaleWood);
            }
            else if(role=="transport_ship")
            {
                // A single low triangular rig ahead of the open cargo canopy,
                // all below the existing 2.02m pennant and within the original bow.
                deck.Cylinder(new Vector3(0,1.11f,1.0f),.045f,1.40f,DarkWood,10);
                var a=new Vector3(0,1.75f,1.0f);var b=new Vector3(.07f,.99f,1.0f);var c=new Vector3(.07f,1.40f,1.62f);
                var aft=new Vector3(-.07f,0,0);
                deck.Triangle(a,b,c,WorkingCanvas085);deck.Triangle(c+aft,b+aft,a+aft,WorkingCanvas085);
                deck.Beam(a,c,.045f,.045f,PaleWood);deck.Beam(b,c,.035f,.035f,DarkWood);
                // The existing hatch remains visible between the canopy and rig.
                foreach(float x in new[]{-.39f,.39f})deck.Box(new Vector3(x,.50f,.55f),new Vector3(.065f,.16f,1.0f),PaleWood);
                foreach(float z in new[]{.10f,1.0f})deck.Box(new Vector3(0,.50f,z),new Vector3(.82f,.16f,.065f),DarkWood);
            }
        }
    }
}
