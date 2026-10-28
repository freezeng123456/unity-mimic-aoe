using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Real URP rendering at the native strategy camera's scale. These isolated
    // diagnostic sheets supplement, but do not replace, an ordinary player view.
    public sealed class FrontierArtStudy055Tests
    {
        private const int Layer=30;
        private GameObject stage;
        private Camera camera;
        private RuleCatalog rules;
        private Font font;
        private int nextId;
        [Serializable] private sealed class CaptureReceipt
        {
            public string purpose="Isolated original-art readability diagnostic; not normal-player acceptance";
            public string file,sha256,unityVersion;
            public int width=2560,height=1440,frame;
            public float cameraOrthographicSize,cameraPitch=50;
            public float unitYaw=155,staticObjectYaw=0;
            public Vector3 cameraPosition;
            public string[] entities;
        }
        [UnityTest] public IEnumerator RenderAllUnitsBuildingsAndResourcesAtStrategyDistance()
        {
            string output=Environment.GetEnvironmentVariable("FRONTIER_ART_STUDY_DIR");
            if(string.IsNullOrEmpty(output))output=Path.Combine(Application.temporaryCachePath,"FrontierArtStudy055");
            Directory.CreateDirectory(output);rules=Catalog.Load();font=Resources.Load<Font>("Fonts/NotoSans-Regular");
            stage=new GameObject("Original art 055 isolated URP study");
            camera=new GameObject("Native-scale study camera").AddComponent<Camera>();camera.transform.SetParent(stage.transform,false);camera.enabled=false;camera.orthographic=true;camera.orthographicSize=26;
            camera.transform.position=new Vector3(0,50,-42);camera.transform.rotation=Quaternion.Euler(50,0,0);camera.aspect=2560f/1440;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.20f,.25f,.19f);camera.cullingMask=1<<Layer;camera.nearClipPlane=.1f;camera.farClipPlane=150;
            var light=new GameObject("Study key light").AddComponent<Light>();light.transform.SetParent(stage.transform,false);light.type=LightType.Directional;light.intensity=1.6f;light.cullingMask=1<<Layer;light.transform.rotation=Quaternion.Euler(48,-28,0);
            var rt=new RenderTexture(2560,1440,24,RenderTextureFormat.ARGB32);rt.Create();
            try
            {
                string[] units={"villager","explorer","pike","longbow","musketeer","skirmisher","hussar","dragoon","carabineer","cannon","mortar","guardian"};
                var sheet=Sheet("All original units");
                for(int i=0;i<units.Length;i++)
                {
                    var point=new Vector3((i%6-2.5f)*9,0,(i/6-.5f)*13);
                    for(int n=0;n<3;n++)Model(sheet.transform,units[i],point+Vector3.right*(n-1)*1.65f,n==2?1:0);
                    Label(sheet.transform,units[i],point+new Vector3(0,0,-2.7f));
                }
                SetLayer(sheet);yield return null;yield return Capture(rt,output,"units-strategy-055",units);
                camera.orthographicSize=16;yield return Capture(rt,output,"units-study-055",units);camera.orthographicSize=26;
                UnityEngine.Object.Destroy(sheet);yield return null;
                string[] buildings={"town","house","barracks","stable","foundry","market","tower","farm","bank","estate","trade_post"};
                sheet=Sheet("All original facilities");
                for(int i=0;i<buildings.Length;i++)
                {var point=new Vector3((i%4-1.5f)*12,0,(i/4-1)*11);Model(sheet.transform,buildings[i],point,i%2);Label(sheet.transform,buildings[i],point+new Vector3(0,0,-3));}
                SetLayer(sheet);yield return null;yield return Capture(rt,output,"buildings-strategy-055",buildings);
                UnityEngine.Object.Destroy(sheet);yield return null;
                string[] resources={"villager","tree","mine","deer","berry","crate_food","crate_wood","crate_coin"};
                sheet=Sheet("Worker and resources at the same scale");
                for(int i=0;i<resources.Length;i++)
                {var point=new Vector3((i%4-1.5f)*9,0,(i/4-.5f)*10);Model(sheet.transform,resources[i],point,resources[i]=="villager"?0:-1);Label(sheet.transform,resources[i],point+new Vector3(0,0,-2.8f));}
                SetLayer(sheet);yield return null;yield return Capture(rt,output,"resources-strategy-055",resources);
                camera.orthographicSize=12;yield return Capture(rt,output,"resources-study-055",resources);
            }
            finally{rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(stage);}
            yield return null;
        }
        private GameObject Sheet(string name)
        {var sheet=new GameObject(name);sheet.transform.SetParent(stage.transform,false);ProceduralArt.Part(sheet.transform,"Study ground",PrimitiveType.Cube,new Vector3(0,-.13f,0),new Vector3(100,.20f,80),new Color(.36f,.39f,.23f));return sheet;}
        private void Model(Transform sheet,string id,Vector3 point,int owner)
        {
            var def=rules.Entity(id);Assert.IsNotNull(def,id);
            var e=new EntityState{id=++nextId,owner=owner,def=def,hp=def.hp,construction=1,fireAt=-1,status="Idle",isDeployed=(def.tags&Tags.Artillery)!=0};
            var model=ProceduralArt.BuildEntity(e,sheet);model.localPosition=point;
            // World.Spawn leaves static facing at zero; the normal view uses identity.
            // Infantry face the study camera so their grips remain visible.
            model.localRotation=def.kind==EntityKind.Unit?Quaternion.Euler(0,155,0):Quaternion.identity;
            if(def.kind==EntityKind.Unit)UnitAnimation.Apply(model.GetComponent<UnitRig>(),UnitAnimation.Evaluate(e,10));
        }
        private void Label(Transform parent,string title,Vector3 point)
        {
            var obj=new GameObject("Study label "+title);obj.transform.SetParent(parent,false);obj.transform.localPosition=point;obj.transform.rotation=camera.transform.rotation;
            var text=obj.AddComponent<TextMesh>();text.font=font;text.fontSize=40;text.characterSize=.15f;text.text=title;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.94f,.92f,.75f);obj.GetComponent<Renderer>().sharedMaterial=font.material;
        }
        private static void SetLayer(GameObject sheet){foreach(var t in sheet.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;}
        private IEnumerator Capture(RenderTexture rt,string output,string name,string[] ids)
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
            var previous=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();var pixels=image.GetPixels32();var first=pixels[0];
                Assert.Greater(pixels.Where((p,i)=>i%17==0&&!p.Equals(first)).Count(),500,"URP must render actual geometry");
                byte[] png=image.EncodeToPNG();string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(png)).Replace("-","").ToLowerInvariant();
                File.WriteAllBytes(Path.Combine(output,name+".png"),png);
                var receipt=new CaptureReceipt{file=name+".png",sha256=hash,unityVersion=Application.unityVersion,frame=Time.frameCount,cameraOrthographicSize=camera.orthographicSize,cameraPosition=camera.transform.position,entities=ids};
                File.WriteAllText(Path.Combine(output,name+".json"),JsonUtility.ToJson(receipt,true));
            }
            finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(image);}
        }
    }
}
