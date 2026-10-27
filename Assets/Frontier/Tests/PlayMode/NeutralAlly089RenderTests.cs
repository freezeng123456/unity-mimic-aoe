using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Controlled model study using actual catalog entities and ProceduralArt rigs.
    // Not normal recruitment, live battle, UI, native input, or final art approval.
    public sealed class NeutralAlly089RenderTests
    {
        const int Width=1920,Height=1080,Layer=29;
        const string Scope="Actual catalog entities and ProceduralArt rigs rendered through the Editor URP. Controlled empty stage, six unit definitions and two actual team colours. Not a paid game, native input, full battle, performance or visual acceptance.";
        [Serializable] sealed class SourceEntry { public string path,sha256; }
        [Serializable] sealed class Item { public string unit;public int owner;public float x,z;public bool weapon,teamColour; }
        [Serializable] sealed class Receipt
        {
            public string scope=Scope,status,sourceFingerprint,sourceAfter,worldSha256,pngSha256,unityVersion,graphicsDevice;
            public int width=Width,height=Height;public bool worldStable,sourceStable;public Item[] subjects;
        }
        [UnityTest] public IEnumerator SixAlliedUnitsInTwoTeamColoursRenderActualRigAtlas()
        {
            Assert.IsTrue(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset);
            string output=Environment.GetEnvironmentVariable("FRONTIER089_ALLY_EVIDENCE");
            Assert.IsFalse(string.IsNullOrWhiteSpace(output));output=Path.GetFullPath(output);
            Directory.CreateDirectory(output);Assert.IsFalse(Directory.EnumerateFileSystemEntries(output).Any(),"Use a fresh output directory");
            string expected=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),beforeFingerprint=Fingerprint(Sources());
            Assert.IsFalse(string.IsNullOrWhiteSpace(expected));Assert.AreEqual(expected,beforeFingerprint);
            var stage=new GameObject("Controlled neutral ally model study");Camera camera=null;RenderTexture rt=null;Texture2D image=null;
            var world=new World(Catalog.Load());var subjects=new List<Item>();
            string[,] ids={{"ally_river_guard","ally_river_pike"},{"ally_highland_guard","ally_highland_rifle"},{"ally_coastal_guard","ally_coastal_marine"}};
            string[] headwear={"River trader broad hat","Highland wool cap","Coastal iron helmet"};
            try
            {
                // Three factions left to right; the original and new unit occupy
                // near and far rows; blue and red owner colours appear in each cell.
                for(int faction=0;faction<3;faction++)for(int row=0;row<2;row++)for(int owner=0;owner<2;owner++)
                {
                    string id=ids[faction,row];Assert.IsNotNull(world.Rules.Entity(id),id);
                    Vector2 position=new Vector2(50+faction*5.4f+owner*1.65f,50+row*3.3f);
                    var entity=world.Spawn(id,owner,position);var model=ProceduralArt.BuildEntity(entity,stage.transform);
                    model.localPosition=new Vector3(position.x-50,0,position.y-50);model.localRotation=Quaternion.Euler(0,180,0);
                    var rig=model.GetComponent<UnitRig>();Assert.IsNotNull(rig);Assert.IsNotNull(rig.Weapon);
                    var parts=model.GetComponentsInChildren<Transform>(true);Assert.IsTrue(parts.Any(t=>t.name==headwear[faction]),id+" must have its faction silhouette");
                    string teamPart=faction==0?"River trader team band":faction==1?"Highland team band":"Coastal team ribbon";
                    Assert.IsTrue(parts.Any(t=>t.name==teamPart),id+" must retain team colour");
                    if(id=="ally_highland_guard"||id=="ally_river_pike"||id=="ally_coastal_marine")
                    {Assert.IsTrue(parts.Any(t=>t.name=="Pike shaft"),id+" must carry its actual pike");Assert.IsTrue(UnitAnimation.Evaluate(entity,world.Time).polearm);}
                    subjects.Add(new Item{unit=id,owner=owner,x=position.x,z=position.y,weapon=true,teamColour=true});
                }
                string worldBefore=JsonUtility.ToJson(world.CaptureState());
                var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Neutral controlled ground";floor.transform.SetParent(stage.transform,false);
                floor.transform.localPosition=new Vector3(6.2f,-.05f,1.7f);floor.transform.localScale=new Vector3(2.1f,1,1.3f);
                var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.34f,.42f,.25f)};
                floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
                var sun=new GameObject("Neutral study sunlight").AddComponent<Light>();sun.transform.SetParent(stage.transform,false);sun.type=LightType.Directional;sun.intensity=1.5f;sun.color=new Color(1,.96f,.88f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-30,0);
                var cameraObject=new GameObject("Neutral study camera");cameraObject.transform.SetParent(stage.transform,false);
                camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=6.0f;camera.aspect=Width/(float)Height;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.17f,.20f,.18f);camera.enabled=false;
                cameraObject.AddComponent<UniversalAdditionalCameraData>().renderShadows=true;
                var center=new Vector3(6.2f,1.0f,1.65f);camera.transform.position=center+new Vector3(0,12,-15);camera.transform.LookAt(center);
                foreach(var transform in stage.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=Layer;
                camera.cullingMask=1<<Layer;sun.cullingMask=1<<Layer;rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);Assert.IsTrue(rt.Create());
                for(int i=0;i<2;i++){RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;}
                var previous=RenderTexture.active;RenderTexture.active=rt;
                try {image=new Texture2D(Width,Height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply();}
                finally{RenderTexture.active=previous;}
                Assert.AreEqual(worldBefore,JsonUtility.ToJson(world.CaptureState()),"Rendering must not change simulation state");
                byte[] png=image.EncodeToPNG();File.WriteAllBytes(Path.Combine(output,"neutral-units-original.png"),png);
                string afterFingerprint=Fingerprint(Sources());Assert.AreEqual(beforeFingerprint,afterFingerprint);
                var receipt=new Receipt{status="actual-controlled-rigs-rendered-not-visual-approval",sourceFingerprint=beforeFingerprint,sourceAfter=afterFingerprint,worldSha256=TextHash(worldBefore),pngSha256=Hash(png),unityVersion=Application.unityVersion,graphicsDevice=SystemInfo.graphicsDeviceName,worldStable=true,sourceStable=true,subjects=subjects.ToArray()};
                File.WriteAllText(Path.Combine(output,"neutral-units-receipt089.json"),JsonUtility.ToJson(receipt,true));
                UnityEngine.Object.Destroy(floorMaterial);
            }
            finally
            {
                if(image!=null)UnityEngine.Object.Destroy(image);
                if(rt!=null){rt.Release();UnityEngine.Object.Destroy(rt);}
                UnityEngine.Object.Destroy(stage);
            }
            yield return null;
        }
        static SourceEntry[] Sources()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));var files=new List<SourceEntry>();
            foreach(string name in new[]{"Assets","Packages","ProjectSettings","Tools"})foreach(string file in Directory.GetFiles(Path.Combine(root,name),"*",SearchOption.AllDirectories))
            {string path=file.Substring(root.Length+1).Replace('\\','/');if(path.Split('/').Any(p=>p.StartsWith(".",StringComparison.Ordinal)||p=="__pycache__")||System.Text.RegularExpressions.Regex.IsMatch(path,@"^Assets/InitTestScene[0-9a-f-]{36}\.unity(\.meta)?$"))continue;files.Add(new SourceEntry{path=path,sha256=Hash(File.ReadAllBytes(file))});}
            return files.OrderBy(f=>f.path,StringComparer.Ordinal).ToArray();
        }
        static string Fingerprint(SourceEntry[] files)
        {var s=new StringBuilder("{");for(int i=0;i<files.Length;i++){if(i>0)s.Append(',');Quote(s,files[i].path);s.Append(':');Quote(s,files[i].sha256);}s.Append('}');return TextHash(s.ToString());}
        static void Quote(StringBuilder s,string text)
        {s.Append('"');foreach(char c in text){if(c=='"')s.Append("\\\"");else if(c=='\\')s.Append("\\\\");else if(c<32||c>126)s.Append("\\u").Append(((int)c).ToString("x4"));else s.Append(c);}s.Append('"');}
        static string TextHash(string value)=>Hash(Encoding.UTF8.GetBytes(value));
        static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
