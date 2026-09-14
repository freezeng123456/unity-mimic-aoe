using UnityEngine;

namespace Frontier
{
    public static class CameraPanInput
    {
        public static Vector2 EdgeDirection(Vector2 pointer,Vector2 viewport,bool focused)
        {
            if(!focused||viewport.x<=0||viewport.y<=0||pointer.x<0||pointer.y<0||pointer.x>=viewport.x||pointer.y>=viewport.y)return Vector2.zero;
            // Pointer and viewport are both Unity input pixels. Scaling the band
            // with the viewport keeps it usable on Retina and resized windows.
            float band=Mathf.Min(Mathf.Clamp(Mathf.Min(viewport.x,viewport.y)*.03f,16,64),Mathf.Min(viewport.x,viewport.y)*.5f);
            Vector2 pan=Vector2.zero;
            if(pointer.x<band)pan.x--;else if(pointer.x>=viewport.x-band)pan.x++;
            if(pointer.y>=viewport.y-band)pan.y++;else if(pointer.y<band)pan.y--;
            return pan.normalized;
        }
    }
}
