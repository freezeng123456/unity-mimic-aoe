using UnityEngine;

namespace Frontier
{
    // Larger, readable architectural forms share the original construction
    // batches. No new entity, obstacle, animation or material ownership.
    public static partial class DetailedArchitecture
    {
        sealed partial class Builder
        {
            void RefineHallOpenings085(float halfWidth,float height,ref float opening,ref float windowHeight)
            {
                if(entity.def.id!="town"&&entity.def.id!="barracks")return;
                // Keep bespoke courtyard and highland joinery proportions.
                if(wayfarer||highland)return;
                opening=Mathf.Min(entity.def.id=="town"?.55f:.52f,halfWidth*.45f);
                windowHeight=Mathf.Min(entity.def.id=="town"?.82f:.74f,height*.36f);
            }

            void RefineArchitecture085()
            {
                if(entity.def.id=="town")
                {
                    if(wayfarer||highland)return;
                    float hallHeight=ottoman?2.55f:2.62f+age*.06f;
                    var centre=new Vector3(0,.23f+hallHeight+.045f,d*(ottoman?.20f:.12f));
                    ReadableEaves085(centre,w*(ottoman?.67f:.68f)+.065f,d*(ottoman?.58f:.69f)+.065f);
                }
                else if(entity.def.id=="barracks")
                {
                    ReadableEaves085(new Vector3(0,2.265f,d*.35f),w*.88f+.065f,d*.56f+.065f);
                    BarracksGate085();
                }
            }

            void ReadableEaves085(Vector3 centre,float halfWidth,float halfDepth)
            {
                // A single broad shadow line, rather than many tiny roof ribs.
                foreach(int side in new[]{-1,1})
                {
                    roof.Box(centre+new Vector3(side*(halfWidth-.035f),-.025f,0),new Vector3(.13f,.17f,halfDepth*2),wood);
                    roof.Box(centre+new Vector3(0,-.025f,side*(halfDepth-.035f)),new Vector3(halfWidth*2,.17f,.13f),wood);
                    roof.Box(centre+new Vector3(side*(halfWidth-.025f),.065f,0),new Vector3(.15f,.06f,halfDepth*2),roofMat);
                }
            }

            void CivicPortico085(Vector3 p,float halfWidth,float depth,float height)
            {
                // Same platform and entrance envelope as the original porch.
                ground.Box(p+new Vector3(0,.012f,-depth*.18f),new Vector3(halfWidth*2,.07f,depth),stone);
                foreach(int side in new[]{-1,1})foreach(int row in new[]{-1,1})
                {
                    var foot=p+new Vector3(side*halfWidth*.78f,.035f,row*depth*.25f-depth*.03f);
                    wall.Box(foot+Vector3.up*(height*.5f),new Vector3(.12f,height,.12f),trim);
                    wall.Box(foot+Vector3.up*.065f,new Vector3(.22f,.13f,.21f),stone);
                    wall.Box(foot+Vector3.up*(height-.045f),new Vector3(.23f,.12f,.22f),trim);
                }
                roof.Box(p+new Vector3(0,height+.005f,-depth*.13f),new Vector3(halfWidth*2.04f,.075f,depth*1.10f),wood);
                roof.Box(p+new Vector3(0,height+.105f,-depth*.13f),new Vector3(halfWidth*2.04f,.15f,depth*1.12f),trim);
                var ridge=p+new Vector3(0,height+.20f,-depth*.13f);
                GableRoof(ridge,halfWidth*1.03f,depth*.56f,.36f,roofMat);
                float front=ridge.z-depth*.56f-.015f;
                foreach(int side in new[]{-1,1})
                    roof.Beam(new Vector3(ridge.x+side*halfWidth*1.03f,ridge.y,front),new Vector3(ridge.x,ridge.y+.36f,front),.085f,.075f,trim);
                // One dark inset makes the portico read as a deep entrance.
                roof.Box(p+new Vector3(0,height+.102f,-depth*.70f),new Vector3(halfWidth*1.56f,.075f,.045f),wood);
            }

            void BarracksGate085()
            {
                float front=-d*.21f;
                foreach(int side in new[]{-1,1})
                {
                    wall.Box(new Vector3(side*.53f,1.16f,front-.085f),new Vector3(.14f,1.86f,.18f),stone);
                    wall.Box(new Vector3(side*.53f,.34f,front-.085f),new Vector3(.24f,.22f,.23f),trim);
                }
                roof.Box(new Vector3(0,2.16f,front-.085f),new Vector3(1.33f,.16f,.35f),trim);
                roof.Box(new Vector3(0,2.27f,front-.085f),new Vector3(1.38f,.07f,.38f),wood);
                if(ottoman||wayfarer)
                {
                    roof.Box(new Vector3(0,2.36f,front-.085f),new Vector3(1.38f,.11f,.38f),roofMat);
                    roof.Box(new Vector3(0,2.43f,front-.25f),new Vector3(1.22f,.055f,.08f),trim);
                }
                else
                {
                    var p=new Vector3(0,2.31f,front-.085f);
                    GableRoof(p,.69f,.23f,.32f,roofMat);
                    foreach(int side in new[]{-1,1})
                        roof.Beam(p+new Vector3(side*.69f,0,-.245f),p+new Vector3(0,.32f,-.245f),.085f,.075f,trim);
                }
                detail.Box(new Vector3(0,1.94f,front-.135f),new Vector3(.75f,.31f,.10f),dark);
                detail.Box(new Vector3(0,1.94f,front-.196f),new Vector3(.62f,.20f,.035f),team);
            }
        }
    }
}
