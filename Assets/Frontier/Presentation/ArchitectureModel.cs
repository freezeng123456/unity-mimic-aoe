using UnityEngine;

namespace Frontier
{
    // Presentation state only. Buildings retain their simulation footprint and
    // full foundation through construction instead of growing like scaled toys.
    public sealed class ArchitectureModel:MonoBehaviour
    {
        public string Civilization;
        public int Age;
        public Bounds LocalBounds;
        public Transform Walls,Roofs,Details,Scaffold;
        int stage=-1;
        public int ConstructionStage(float progress)=>progress>=1?4:progress>=.78f?3:progress>=.55f?2:progress>=.18f?1:0;
        public void SetConstruction(float progress)
        {
            int next=ConstructionStage(progress);
            if(next==stage)return;stage=next;
            if(Walls!=null)Walls.gameObject.SetActive(next>=1);
            if(Roofs!=null)Roofs.gameObject.SetActive(next>=2);
            if(Details!=null)Details.gameObject.SetActive(next>=3);
            if(Scaffold!=null)Scaffold.gameObject.SetActive(next<4);
        }
    }
}
