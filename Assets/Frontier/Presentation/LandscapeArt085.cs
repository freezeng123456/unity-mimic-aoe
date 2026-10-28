using UnityEngine;

namespace Frontier
{
    // Original painted albedo, decoded once. This never reads or changes the
    // World, heights, navigation, visibility or procedural resource models.
    public static class LandscapeArt085
    {
        static Texture2D meadow;
        static Material ground;
        public static Texture2D Meadow
        {
            get
            {
                if(meadow!=null)return meadow;
                var asset=Resources.Load<TextAsset>("Meadow085");
                if(asset==null)return null;
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,true,false)
                {name="Original painted meadow 085 / fourteen metres",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
                if(!ImageConversion.LoadImage(texture,asset.bytes,true)){Object.Destroy(texture);return null;}
                meadow=texture;return meadow;
            }
        }
        public static Material GroundMaterial()
        {
            if(ground!=null)return ground;
            var original=LandscapePresentation.GroundMaterial();var texture=Meadow;
            if(texture==null)return original;
            var shader=Resources.Load<Shader>("RiverGround");if(shader==null)return original;
            ground=new Material(shader){name="Frontier / painted short meadow 085"};
            ground.SetTexture("_GrassMap",original.GetTexture("_BaseMap"));
            ground.SetTexture("_GrassNormals",original.GetTexture("_BumpMap"));
            ground.SetTexture("_SoilMap",original.GetTexture("_BaseMap"));
            ground.SetTexture("_SoilNormals",original.GetTexture("_BumpMap"));
            ground.SetColor("_TurfTint",new Color(.90f,.93f,.86f));
            ground.SetFloat("_MeadowOnly",1);ApplyRiver(ground);
            return ground;
        }
        public static void ApplyRiver(Material material)
        {
            var texture=Meadow;if(texture==null||!material.HasProperty("_MeadowDetail"))return;
            material.SetTexture("_MeadowDetail",texture);material.SetFloat("_PaintedStrength",.48f);
        }
    }
}
