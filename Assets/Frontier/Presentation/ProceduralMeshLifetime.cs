using UnityEngine;

namespace Frontier
{
    // Only runtime-authored meshes are owned here; imported/shared meshes and
    // materials stay with their asset/cache. Pooling keeps the owner alive.
    public sealed class ProceduralMeshLifetime : MonoBehaviour
    {
        public Mesh OwnedMesh { get; set; }
        private void OnDestroy()
        {
            if(OwnedMesh==null)return;
            if(Application.isPlaying)Destroy(OwnedMesh);else DestroyImmediate(OwnedMesh);
            OwnedMesh=null;
        }
    }
}
