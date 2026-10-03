using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // A model's active ancestor covers mesh leaves which were hidden from birth.
    // It contains only explicit ProceduralMeshLifetime registrations, never a
    // scene-wide MeshFilter scan, so shared imported FBX meshes stay untouched.
    public sealed class ProceduralMeshRootOwner : MonoBehaviour
    {
        readonly HashSet<ProceduralMeshLifetime> owners=new HashSet<ProceduralMeshLifetime>();
        internal void Register(ProceduralMeshLifetime owner){owners.Add(owner);}
        internal void Unregister(ProceduralMeshLifetime owner){owners.Remove(owner);}
        void OnDestroy()
        {
            var release=new List<ProceduralMeshLifetime>(owners);owners.Clear();
            foreach(var owner in release)
            {
                // The managed owner may remain after its never-active native
                // component was destroyed; its explicit mesh reference is still
                // ours to release. Unity's overloaded null would skip it.
                if(!ReferenceEquals(owner,null))owner.ReleaseFromRoot();
            }
        }
    }
}
