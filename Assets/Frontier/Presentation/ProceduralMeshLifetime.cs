using UnityEngine;

namespace Frontier
{
    // Only explicitly assigned runtime-authored meshes are owned here. Imported
    // meshes and materials are never discovered or destroyed by this component.
    public sealed class ProceduralMeshLifetime : MonoBehaviour
    {
        Mesh ownedMesh;
        ProceduralMeshRootOwner rootOwner;
        public Mesh OwnedMesh
        {
            get=>ownedMesh;
            set
            {
                if(ownedMesh==value)return;
                if(rootOwner!=null)rootOwner.Unregister(this);
                rootOwner=null;ownedMesh=value;
                if(ownedMesh==null)return;
                // Unity does not send OnDestroy to a behaviour that has never
                // been active. Hidden tools and completed-building scaffolds
                // therefore register ownership before their first activation.
                var rig=GetComponentInParent<UnitRig>(true);
                Transform root=rig!=null?rig.transform:transform.parent??transform;
                while(!root.gameObject.activeInHierarchy&&root.parent!=null)root=root.parent;
                rootOwner=root.GetComponent<ProceduralMeshRootOwner>();
                if(rootOwner==null)rootOwner=root.gameObject.AddComponent<ProceduralMeshRootOwner>();
                rootOwner.Register(this);
            }
        }
        public void ReleaseOwnedMesh()
        {
            if(rootOwner!=null)rootOwner.Unregister(this);
            rootOwner=null;ReleaseMesh();
        }
        internal void ReleaseFromRoot()
        {
            rootOwner=null;ReleaseMesh();
        }
        void ReleaseMesh()
        {
            var mesh=ownedMesh;ownedMesh=null;if(mesh==null)return;
            if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);
        }
        void OnDestroy(){ReleaseOwnedMesh();}
    }
}
