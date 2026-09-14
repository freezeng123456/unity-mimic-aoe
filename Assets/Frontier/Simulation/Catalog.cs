using UnityEngine;

namespace Frontier
{
    public static class Catalog
    {
        private static RuleCatalog cached;
        public static RuleCatalog Load()
        {
            if (cached != null) return cached;
            var asset = Resources.Load<TextAsset>("Rules");
            if (asset == null) { Debug.LogError("Rules.json is missing from Resources"); return cached = new RuleCatalog(); }
            cached = JsonUtility.FromJson<RuleCatalog>(asset.text) ?? new RuleCatalog();
            cached.entities ??= System.Array.Empty<EntityDef>(); cached.techs ??= System.Array.Empty<TechDef>();
            cached.cards ??= System.Array.Empty<CardDef>(); cached.ages ??= System.Array.Empty<AgeDef>(); cached.civs ??= System.Array.Empty<CivDef>();
            return cached;
        }
    }
}
