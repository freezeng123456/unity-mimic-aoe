using UnityEngine;

namespace Frontier
{
    internal static class EntityPickGeometry
    {
        public static Bounds WorldBounds(Transform model)
        {
            var renderers=model.GetComponentsInChildren<Renderer>();
            var bounds=new Bounds(model.position,Vector3.zero);bool found=false;
            foreach(var renderer in renderers)
            {
                if(!renderer.enabled||renderer.name=="Non-solid trailing boat wake")continue;
                if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }
        public static bool RayHits(Bounds bounds,Ray ray)=>bounds.IntersectRay(ray);
    }
}
