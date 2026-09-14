using UnityEngine;

namespace Frontier
{
    internal static class EntityPickGeometry
    {
        public static Bounds WorldBounds(Transform model)
        {
            var renderers=model.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0)return new Bounds(model.position,Vector3.zero);
            var bounds=renderers[0].bounds;
            for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
        public static bool RayHits(Bounds bounds,Ray ray)=>bounds.IntersectRay(ray);
    }
}
