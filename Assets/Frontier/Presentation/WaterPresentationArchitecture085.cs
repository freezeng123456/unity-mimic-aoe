using UnityEngine;
using Surface=Frontier.ArchitecturalMaterials.Surface;

namespace Frontier
{
    public static partial class WaterPresentation
    {
        static Material DockRoof085=>ArchitecturalMaterials.Get(Surface.Slate,new Color(.30f,.39f,.37f));
        static Material DockInfill085=>ArchitecturalMaterials.Get(Surface.Plaster,new Color(.69f,.65f,.50f));
        static Material DockGlass085=>ArchitecturalMaterials.Get(Surface.Glass,new Color(.14f,.22f,.22f));

        static void BuildDockRoof085(ArchitecturalGeometry roof)
        {
            // Same eave rectangle as the original roof. The higher ridge still
            // sits below the unchanged hoist and keeps its working silhouette.
            const float left=-1.86f,right=-.045f,ridge=-.95f,eave=1.78f,top=2.54f,depth=1.46f;
            var covering=DockRoof085;var infill=DockInfill085;
            roof.Quad(new Vector3(left,eave,-depth),new Vector3(left,eave,depth),new Vector3(ridge,top,depth),new Vector3(ridge,top,-depth),covering);
            roof.Quad(new Vector3(ridge,top,-depth),new Vector3(ridge,top,depth),new Vector3(right,eave,depth),new Vector3(right,eave,-depth),covering);
            roof.Triangle(new Vector3(left,eave,-depth),new Vector3(ridge,top,-depth),new Vector3(right,eave,-depth),infill);
            roof.Triangle(new Vector3(right,eave,depth),new Vector3(ridge,top,depth),new Vector3(left,eave,depth),infill);
            foreach(float x in new[]{left+.055f,right-.055f})
            {
                roof.Box(new Vector3(x,eave-.015f,0),new Vector3(.11f,.18f,depth*2),DarkWood);
                roof.Box(new Vector3(x,eave+.075f,0),new Vector3(.12f,.055f,depth*2),covering);
            }
            roof.Box(new Vector3(ridge,top+.025f,0),new Vector3(.145f,.10f,depth*2),DarkWood);
            foreach(float side in new[]{-1f,1f})
            {
                float z=side*(depth-.035f);
                roof.Beam(new Vector3(left+.035f,eave+.02f,z),new Vector3(ridge,top+.015f,z),.095f,.075f,DarkWood);
                roof.Beam(new Vector3(ridge,top+.015f,z),new Vector3(right-.035f,eave+.02f,z),.095f,.075f,DarkWood);
                roof.Box(new Vector3(ridge,eave+.32f,z),new Vector3(.10f,.64f,.075f),DarkWood);
                roof.Box(new Vector3(ridge,eave+.015f,z),new Vector3(right-left-.035f,.12f,.075f),DarkWood);
            }
        }

        static void RefineDockArchitecture085(ArchitecturalGeometry foundation,ArchitecturalGeometry walls)
        {
            // Broad plaster bays and dark timber break up the former single
            // brown storehouse. The +X opening and its swinging leaf stay clear.
            foreach(float side in new[]{-1f,1f})
            {
                float z=side*1.295f;
                walls.Box(new Vector3(-.95f,.995f,z),new Vector3(1.30f,1.34f,.035f),DockInfill085);
                foreach(float x in new[]{-1.59f,-.31f})
                    walls.Box(new Vector3(x,1.0f,z+side*.035f),new Vector3(.085f,1.50f,.075f),DarkWood);
                foreach(float y in new[]{.36f,1.69f})
                    walls.Box(new Vector3(-.95f,y,z+side*.035f),new Vector3(1.40f,.095f,.075f),DarkWood);
                float face=z+side*.056f;
                walls.Box(new Vector3(-.95f,1.12f,face),new Vector3(.74f,.64f,.06f),DarkWood);
                walls.Box(new Vector3(-.95f,1.12f,face+side*.038f),new Vector3(.62f,.52f,.025f),DockGlass085);
                foreach(float x in new[]{-1.30f,-.60f})
                    walls.Box(new Vector3(x,1.12f,face+side*.065f),new Vector3(.065f,.66f,.07f),PaleWood);
                foreach(float y in new[]{.815f,1.425f})
                    walls.Box(new Vector3(-.95f,y,face+side*.065f),new Vector3(.77f,.065f,.07f),PaleWood);
                walls.Box(new Vector3(-.95f,1.12f,face+side*.075f),new Vector3(.055f,.54f,.045f),PaleWood);
                walls.Box(new Vector3(-.95f,1.12f,face+side*.075f),new Vector3(.64f,.045f,.045f),PaleWood);
                // A continuous fascia gives each finger pier a readable edge.
                foundation.Box(new Vector3(3.39f,.075f,side*1.41f),new Vector3(3.12f,.23f,.11f),DarkWood);
                foundation.Box(new Vector3(3.39f,.222f,side*1.41f),new Vector3(3.12f,.055f,.11f),PaleWood);
            }
            walls.Box(new Vector3(-1.765f,.995f,0),new Vector3(.035f,1.34f,2.28f),DockInfill085);
            foreach(float z in new[]{-1.11f,0,1.11f})
                walls.Box(new Vector3(-1.795f,1.0f,z),new Vector3(.075f,1.50f,.085f),DarkWood);
            foreach(float y in new[]{.36f,1.69f})
                walls.Box(new Vector3(-1.795f,y,0),new Vector3(.075f,.095f,2.35f),DarkWood);
            walls.Box(new Vector3(-.12f,1.78f,0),new Vector3(.16f,.15f,2.62f),DarkWood);
        }
    }
}
