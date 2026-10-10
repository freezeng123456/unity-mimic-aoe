using UnityEngine;

namespace Frontier
{
    // Advisory only: submitting Build still runs the authoritative command checks.
    // One snapped point and CanBuild reason drive both the outline and HUD text.
    public readonly struct BuildPlacementPreview079
    {
        public readonly bool Active;
        public readonly string BuildingId,Name,Reason;
        public readonly Vector2 Point;
        public bool Valid=>Active&&Reason=="";
        public string Label=>!Active?"":"PLACING  "+Name+"\n"+(Valid?"READY · valid footprint":Reason);
        private BuildPlacementPreview079(string id,string name,Vector2 point,string reason)
        {Active=true;BuildingId=id;Name=name;Point=point;Reason=reason;}
        public static BuildPlacementPreview079 Evaluate(World world,int player,string id,Vector2 hover)
        {
            if(world==null||string.IsNullOrEmpty(id)||!SnapshotCodec.Finite(hover))return default;
            var definition=world?.Rules.Entity(id);
            if(world==null||player<0||player>=world.Players.Length||definition==null||definition.kind!=EntityKind.Building)return default;
            var point=world.BuildPoint(player,id,hover);
            return new BuildPlacementPreview079(id,definition.name,point,world.CanBuild(player,id,point));
        }
    }
}
