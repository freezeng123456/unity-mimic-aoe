using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Actual WorldView, ordinary camera and fog surface; a controlled native
    // scout supplies local vision. No whole-map reveal and no fabricated fog.
    public sealed class RoadFog088RenderTests
    {
        const int Width=1920,Height=1080,Layer=29;
        const string Scope="Actual Winding WorldView/GameCamera/URP; same fixed road position and camera, unknown then locally seen by a controlled spawned scout then explored after its removal. One original frame plus same-state road-hidden and road-and-ground-hidden controls per state. Controlled spawn, camera and lighting; not native input, normal production, a full match or all fog boundaries.";
        [Serializable] sealed class SourceEntry {public string path,sha256;}
        [Serializable] sealed class Inventory {public string sourceFingerprint,runnerExpectedFingerprint;public SourceEntry[] files;}
        [Serializable] sealed class Frame {public string state,originalPngSha256,withoutRoadPngSha256,withoutGroundPngSha256;public float meanRoadDelta,maxRoadDelta,maxRoadChannelDelta,meanGroundDelta,maxGroundChannelDelta;public int changedPixels;}
        [Serializable] sealed class Receipt
        {
            public string scope=Scope,status,sourceFingerprint,sourceAfter,unityVersion,gpu;
            public Vector2 roadPoint;public Vector3 cameraPosition,cameraEuler;public float zoom,aspect;public int roiX,roiY,roiWidth,roiHeight;
            public bool sameCamera,sourceStable,unexploredWasNatural,scoutGeneratedVisible,removedScoutGeneratedExplored;public Frame[] frames;
        }
        [UnityTest] public IEnumerator UnknownAndExploredRoadAreActuallyAttenuatedByFog()
        {
            Assert.IsTrue(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset);
            Assert.IsNull(Camera.main,"Use an empty PlayMode scene");
            string output=Environment.GetEnvironmentVariable("FRONTIER088_ROAD_FOG_OUTPUT");Assert.IsFalse(string.IsNullOrWhiteSpace(output));
            output=Path.GetFullPath(output);Directory.CreateDirectory(output);Assert.IsFalse(Directory.EnumerateFileSystemEntries(output).Any());
            var sources=Sources();string fingerprint=Fingerprint(sources),expected=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT");
            Assert.IsFalse(string.IsNullOrWhiteSpace(expected));Assert.AreEqual(expected,fingerprint);
            File.WriteAllText(Path.Combine(output,"source-inventory088.json"),JsonUtility.ToJson(new Inventory{sourceFingerprint=fingerprint,runnerExpectedFingerprint=expected,files=sources},true));
            var session=new GameObject("088 actual road fog study");var cameraObject=new GameObject("Strategy Camera");cameraObject.transform.SetParent(session.transform,false);cameraObject.tag="MainCamera";
            var camera=cameraObject.AddComponent<Camera>();cameraObject.AddComponent<UniversalAdditionalCameraData>().renderShadows=true;
            var game=session.AddComponent<GameController>();game.enabled=false;game.Controls.enabled=false;
            float oldScale=Time.timeScale;bool oldAudio=AudioListener.pause;var oldAmbientMode=RenderSettings.ambientMode;var oldAmbient=RenderSettings.ambientLight;var oldSun=RenderSettings.sun;
            RenderTexture rt=null;Light sun=null;var layers=new Dictionary<GameObject,int>();MonoBehaviour[] behaviours=null;bool[] behaviourEnabled=null;Canvas[] canvases=null;bool[] canvasEnabled=null;
            try
            {
                Time.timeScale=0;AudioListener.pause=true;game.SelectedMap=MapTopology.Winding;game.MatchSeed=42;game.PlayerCivilization="coast";game.OpponentCivilization="ottoman";game.StartMatch();
                game.Controls.enabled=false;var w=game.World;Assert.IsNotNull(w);w.Fog.Tick(w.Entities);
                Vector2 point=FindHiddenRoad(w);Assert.AreEqual(VisibilityState.Unexplored,w.Fog.At(0,point));
                game.Controls.RestoreCamera(point,16);game.Controls.enabled=false;
                for(int i=0;i<12;i++)yield return null;
                var road=session.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Caravan road").GetComponent<Renderer>();Assert.IsNotNull(road);Assert.IsTrue(road.enabled);
                var ground=session.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Continuous sculpted land and river bed").GetComponent<Renderer>();Assert.IsNotNull(ground);Assert.IsTrue(ground.enabled);
                var update=typeof(WorldView).GetMethod("UpdateFog",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(update);
                canvases=session.GetComponentsInChildren<Canvas>(true);canvasEnabled=canvases.Select(c=>c.enabled).ToArray();foreach(var c in canvases)c.enabled=false;
                foreach(var t in session.GetComponentsInChildren<Transform>(true)){layers[t.gameObject]=t.gameObject.layer;t.gameObject.layer=Layer;}
                camera.enabled=false;camera.aspect=Width/(float)Height;camera.cullingMask=1<<Layer;
                sun=new GameObject("088 fixed road fog light").AddComponent<Light>();sun.transform.SetParent(session.transform,false);sun.gameObject.layer=Layer;
                sun.type=LightType.Directional;sun.intensity=1.55f;sun.color=new Color(1,.96f,.87f);sun.shadows=LightShadows.Soft;sun.cullingMask=1<<Layer;sun.transform.rotation=Quaternion.Euler(50,-35,0);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.57f,.63f,.65f);RenderSettings.sun=sun;
                rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);Assert.IsTrue(rt.Create());
                behaviours=session.GetComponentsInChildren<MonoBehaviour>(true);behaviourEnabled=behaviours.Select(b=>b.enabled).ToArray();
                for(int i=0;i<behaviours.Length;i++)if(!(behaviours[i] is UniversalAdditionalCameraData))behaviours[i].enabled=false;
                // The editor window and 1920x1080 capture can have different
                // dimensions. Map normalized viewport coordinates to the target.
                Vector3 projected=camera.WorldToViewportPoint(new Vector3(point.x,w.SurfaceHeight(point)+.025f,point.y));
                Assert.Greater(projected.z,0);
                int roiX=Mathf.RoundToInt(projected.x*Width)-60,roiY=Mathf.RoundToInt(projected.y*Height)-60;const int size=120;
                Assert.That(roiX,Is.InRange(0,Width-size));Assert.That(roiY,Is.InRange(0,Height-size));
                var frames=new List<Frame>();EntityState scout=null;
                foreach(string state in new[]{"Unexplored","Visible","Explored"})
                {
                    if(state=="Visible")
                    {
                        // A real in-world scout and Fog.Tick provide local vision;
                        // no Reveal of the full map is used.
                        scout=w.Spawn("native_scout",0,point+new Vector2(0,5));w.Fog.Tick(w.Entities);
                    }
                    if(state=="Explored"){w.Remove(scout);w.Fog.Tick(w.Entities);}
                    var visibility=(VisibilityState)Enum.Parse(typeof(VisibilityState),state);
                    for(int y=-3;y<=3;y++)for(int x=-3;x<=3;x++)Assert.AreEqual(visibility,w.Fog.At(0,point+new Vector2(x,y)),"Entire comparison area must have the intended natural visibility state");
                    update.Invoke(game.View,new object[]{w});
                    if(state=="Visible")
                    {
                        yield return CaptureBoundary(game,camera,rt,road,w,output);
                        game.Controls.RestoreCamera(point,16);game.Controls.enabled=false;
                    }
                    // Only one renderer changes between each same-state capture.
                    // Ground-disabled comparison starts with road already disabled.
                    Color32[] original=null,without=null,withoutGround=null;
                    road.enabled=true;ground.enabled=true;yield return ReadFrame(camera,rt,p=>original=p);
                    road.enabled=false;yield return ReadFrame(camera,rt,p=>without=p);
                    ground.enabled=false;yield return ReadFrame(camera,rt,p=>withoutGround=p);
                    ground.enabled=true;road.enabled=true;
                    float sum=0,max=0,maxRoadChannel=0,groundSum=0,maxGroundChannel=0;int changed=0;
                    for(int y=roiY;y<roiY+size;y++)for(int x=roiX;x<roiX+size;x++)
                    {
                        int i=y*Width+x;float dr=Math.Abs(original[i].r-without[i].r),dg=Math.Abs(original[i].g-without[i].g),db=Math.Abs(original[i].b-without[i].b);
                        float gr=Math.Abs(without[i].r-withoutGround[i].r),gg=Math.Abs(without[i].g-withoutGround[i].g),gb=Math.Abs(without[i].b-withoutGround[i].b);
                        float delta=(dr+dg+db)/3f;sum+=delta;max=Mathf.Max(max,delta);maxRoadChannel=Mathf.Max(maxRoadChannel,dr,dg,db);
                        groundSum+=(gr+gg+gb)/3f;maxGroundChannel=Mathf.Max(maxGroundChannel,gr,gg,gb);if(delta>2)changed++;
                    }
                    byte[] a=Encode(original),b=Encode(without),c=Encode(withoutGround);
                    File.WriteAllBytes(Path.Combine(output,state+"-road-original.png"),a);File.WriteAllBytes(Path.Combine(output,state+"-road-hidden-control.png"),b);
                    File.WriteAllBytes(Path.Combine(output,state+"-road-and-ground-hidden-control.png"),c);
                    frames.Add(new Frame{state=state,originalPngSha256=Hash(a),withoutRoadPngSha256=Hash(b),withoutGroundPngSha256=Hash(c),meanRoadDelta=sum/(size*size),maxRoadDelta=max,maxRoadChannelDelta=maxRoadChannel,meanGroundDelta=groundSum/(size*size),maxGroundChannelDelta=maxGroundChannel,changedPixels=changed});
                }
                string after=Fingerprint(Sources());Assert.AreEqual(fingerprint,after);
                var receipt=new Receipt{status="frames-recorded-pending-assertions",sourceFingerprint=fingerprint,sourceAfter=after,sourceStable=true,unityVersion=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,roadPoint=point,cameraPosition=camera.transform.position,cameraEuler=camera.transform.eulerAngles,zoom=camera.orthographicSize,aspect=camera.aspect,roiX=roiX,roiY=roiY,roiWidth=size,roiHeight=size,sameCamera=true,unexploredWasNatural=true,scoutGeneratedVisible=true,removedScoutGeneratedExplored=true,frames=frames.ToArray()};
                string receiptPath=Path.Combine(output,"road-fog-receipt088.json");File.WriteAllText(receiptPath,JsonUtility.ToJson(receipt,true));
                float unknown=frames[0].meanRoadDelta,visible=frames[1].meanRoadDelta,explored=frames[2].meanRoadDelta;
                Assert.Greater(visible,.4f,"The ROI must actually contain the visible road and detect its contribution");
                Assert.Greater(frames[1].meanGroundDelta,1f,"The ROI must actually contain visible ground and detect its contribution");
                // Allow at most one 8-bit level for graphics quantization; a
                // mean-only test can miss a readable thin line or small patch.
                Assert.LessOrEqual(frames[0].maxRoadChannelDelta,1f,"No pixel in the unexplored ROI may reveal the road");
                Assert.LessOrEqual(frames[0].maxGroundChannelDelta,1f,"No pixel in the unexplored ROI may reveal the ground");
                Assert.Less(explored,visible*.75f,"Explored fog must visibly attenuate the road");Assert.Greater(explored,unknown,"Explored and unexplored should remain distinguishable");
                receipt.status="three-actual-visibility-states-passed-not-full-fog-proof";File.WriteAllText(receiptPath,JsonUtility.ToJson(receipt,true));
            }
            finally
            {
                if(behaviours!=null)for(int i=0;i<behaviours.Length;i++)if(behaviours[i]!=null)behaviours[i].enabled=behaviourEnabled[i];
                if(canvases!=null)for(int i=0;i<canvases.Length;i++)if(canvases[i]!=null)canvases[i].enabled=canvasEnabled[i];
                foreach(var pair in layers)if(pair.Key!=null)pair.Key.layer=pair.Value;
                if(rt!=null){rt.Release();UnityEngine.Object.Destroy(rt);}if(sun!=null)UnityEngine.Object.Destroy(sun.gameObject);
                RenderSettings.ambientMode=oldAmbientMode;RenderSettings.ambientLight=oldAmbient;RenderSettings.sun=oldSun;Time.timeScale=oldScale;AudioListener.pause=oldAudio;UnityEngine.Object.Destroy(session);
            }
            yield return null;
        }
        [Serializable] sealed class BoundaryReceipt
        {
            public string scope="Real road crossing a locally revealed frontier; actual fogged pair and fog-disabled comparison pair. Test requires unknown and visible samples over road pixels and checks maximum channel difference in unknown pixels.";
            public Vector2 point;public int roiX,roiY,roiSize,unknownRoadPixels,visibleRoadPixels,softVisibleRoadPixels,clearVisibleRoadPixels;public float maxUnknownChannel;public string originalSha256,roadHiddenSha256,fogHiddenSha256,bothHiddenSha256;
        }
        static IEnumerator CaptureBoundary(GameController game,Camera camera,RenderTexture rt,Renderer road,World w,string output)
        {
            var boundary=FindRoadBoundary(w);game.Controls.RestoreCamera(boundary,16);game.Controls.enabled=false;
            var fog=game.View.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Three-state visibility surface").GetComponent<Renderer>();Assert.IsNotNull(fog);Assert.IsTrue(fog.enabled);
            var viewport=camera.WorldToViewportPoint(new Vector3(boundary.x,w.SurfaceHeight(boundary)+.08f,boundary.y));Assert.Greater(viewport.z,0);
            const int size=120;int x0=Mathf.RoundToInt(viewport.x*Width)-size/2,y0=Mathf.RoundToInt(viewport.y*Height)-size/2;
            Assert.That(x0,Is.InRange(0,Width-size));Assert.That(y0,Is.InRange(0,Height-size));
            Color32[] actual=null,roadHidden=null,fogHidden=null,bothHidden=null;
            try
            {
                road.enabled=true;fog.enabled=true;yield return ReadFrame(camera,rt,p=>actual=p);
                road.enabled=false;yield return ReadFrame(camera,rt,p=>roadHidden=p);
                fog.enabled=false;road.enabled=true;yield return ReadFrame(camera,rt,p=>fogHidden=p);
                road.enabled=false;yield return ReadFrame(camera,rt,p=>bothHidden=p);
            }
            finally{road.enabled=true;fog.enabled=true;}
            int unknown=0,visible=0,softVisible=0,clearVisible=0;float maxUnknown=0;
            for(int y=y0;y<y0+size;y++)for(int x=x0;x<x0+size;x++)
            {
                int i=y*Width+x;int roadSignal=Math.Max(Math.Abs(fogHidden[i].r-bothHidden[i].r),Math.Max(Math.Abs(fogHidden[i].g-bothHidden[i].g),Math.Abs(fogHidden[i].b-bothHidden[i].b)));if(roadSignal<=3)continue;
                // The fog mesh is surface height plus .08 m; iterating the ray
                // handles the local terrain slope without assuming a flat map.
                Ray ray=camera.ViewportPointToRay(new Vector3((x+.5f)/Width,(y+.5f)/Height,0));float t=(w.SurfaceHeight(boundary)+.08f-ray.origin.y)/ray.direction.y;Vector3 hit=ray.origin+ray.direction*t;
                for(int step=0;step<4;step++){t=(w.SurfaceHeight(new Vector2(hit.x,hit.z))+.08f-ray.origin.y)/ray.direction.y;hit=ray.origin+ray.direction*t;}
                Vector2 p=new Vector2(hit.x,hit.z);var v=w.Fog.At(0,p);
                bool margin=true;foreach(var off in new[]{new Vector2(.08f,0),new Vector2(-.08f,0),new Vector2(0,.08f),new Vector2(0,-.08f)})if(w.Fog.At(0,p+off)!=v){margin=false;break;}
                if(!margin)continue;
                if(v==VisibilityState.Visible)
                {
                    visible++;
                    float shown=Math.Max(Math.Abs(actual[i].r-roadHidden[i].r),Math.Max(Math.Abs(actual[i].g-roadHidden[i].g),Math.Abs(actual[i].b-roadHidden[i].b)));
                    if(shown>=2&&shown<roadSignal*.80f)softVisible++;
                    if(shown>=roadSignal*.90f)clearVisible++;
                }
                if(v==VisibilityState.Unexplored)
                {
                    unknown++;float d=Math.Max(Math.Abs(actual[i].r-roadHidden[i].r),Math.Max(Math.Abs(actual[i].g-roadHidden[i].g),Math.Abs(actual[i].b-roadHidden[i].b)));maxUnknown=Mathf.Max(maxUnknown,d);
                }
            }
            byte[] a=Encode(actual),b=Encode(roadHidden),c=Encode(fogHidden),dbytes=Encode(bothHidden);
            File.WriteAllBytes(Path.Combine(output,"Boundary-road-original.png"),a);File.WriteAllBytes(Path.Combine(output,"Boundary-road-hidden-control.png"),b);
            File.WriteAllBytes(Path.Combine(output,"Boundary-fog-hidden-control.png"),c);File.WriteAllBytes(Path.Combine(output,"Boundary-fog-and-road-hidden-control.png"),dbytes);
            var receipt=new BoundaryReceipt{point=boundary,roiX=x0,roiY=y0,roiSize=size,unknownRoadPixels=unknown,visibleRoadPixels=visible,softVisibleRoadPixels=softVisible,clearVisibleRoadPixels=clearVisible,maxUnknownChannel=maxUnknown,originalSha256=Hash(a),roadHiddenSha256=Hash(b),fogHiddenSha256=Hash(c),bothHiddenSha256=Hash(dbytes)};
            File.WriteAllText(Path.Combine(output,"road-fog-boundary-receipt088.json"),JsonUtility.ToJson(receipt,true));
            Assert.Greater(unknown,5,"Require genuinely unknown road pixels at the local vision frontier");Assert.Greater(visible,5,"Require actually visible road pixels at the same frontier");
            Assert.LessOrEqual(maxUnknown,1,"No unexplored road pixel near a known visibility boundary may leak");
            Assert.Greater(softVisible,5,"Require partially obscured pixels on the visible side, not only an opaque hard edge");
            Assert.Greater(clearVisible,5,"Keep clearly visible road pixels away from the local boundary");
        }
        static Vector2 FindRoadBoundary(World w)
        {
            var route=w.TradeRoute;
            for(int i=1;i<route.Length;i++)for(int step=1;step<500;step++)
            {
                var p=Vector2.Lerp(route[i-1],route[i],step/500f);
                if(p.x<12||p.x>116||p.y<12||p.y>116||w.IsWater(p)||w.IsBridgeApproach(p)||w.Fog.At(0,p)!=VisibilityState.Unexplored)continue;
                foreach(var offset in new[]{Vector2.right,Vector2.left,Vector2.up,Vector2.down})if(w.Fog.At(0,p+offset)==VisibilityState.Visible)return p;
            }
            Assert.Fail("The locally revealed road must cross an unexplored boundary away from water and bridge approaches");return Vector2.zero;
        }
        static Vector2 FindHiddenRoad(World w)
        {
            var route=w.TradeRoute;
            for(int i=1;i<route.Length;i++)
            {
                if(Vector2.Distance(route[i-1],route[i])<10)continue;
                for(int sample=3;sample<=7;sample++)
                {
                    var p=Vector2.Lerp(route[i-1],route[i],sample/10f);if(p.x<20||p.x>108||p.y<20||p.y>108||w.IsWater(p)||w.IsBridgeApproach(p))continue;
                    bool unknown=true;for(int y=-5;y<=5&&unknown;y++)for(int x=-5;x<=5;x++)if(w.Fog.At(0,p+new Vector2(x,y))!=VisibilityState.Unexplored){unknown=false;break;}
                    if(unknown)return p;
                }
            }
            Assert.Fail("Need a naturally unexplored straight road section, away from the bridge and map border");return Vector2.zero;
        }
        static IEnumerator ReadFrame(Camera camera,RenderTexture rt,Action<Color32[]> receive)
        {
            for(int i=0;i<2;i++){RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;}
            var prior=RenderTexture.active;var image=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply();receive(image.GetPixels32());}
            finally{RenderTexture.active=prior;UnityEngine.Object.Destroy(image);}
        }
        static byte[] Encode(Color32[] pixels){var image=new Texture2D(Width,Height,TextureFormat.RGBA32,false);try{image.SetPixels32(pixels);image.Apply();return image.EncodeToPNG();}finally{UnityEngine.Object.Destroy(image);}}
        static SourceEntry[] Sources()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));var files=new List<SourceEntry>();
            foreach(string name in new[]{"Assets","Packages","ProjectSettings","Tools"})foreach(string file in Directory.GetFiles(Path.Combine(root,name),"*",SearchOption.AllDirectories))
            {string path=file.Substring(root.Length+1).Replace('\\','/');if(path.Split('/').Any(p=>p.StartsWith(".",StringComparison.Ordinal)||p=="__pycache__")||System.Text.RegularExpressions.Regex.IsMatch(path,@"^Assets/InitTestScene[0-9a-f-]{36}\.unity(\.meta)?$"))continue;files.Add(new SourceEntry{path=path,sha256=Hash(File.ReadAllBytes(file))});}
            return files.OrderBy(f=>f.path,StringComparer.Ordinal).ToArray();
        }
        static string Fingerprint(SourceEntry[] files){var s=new StringBuilder("{");for(int i=0;i<files.Length;i++){if(i>0)s.Append(',');Quote(s,files[i].path);s.Append(':');Quote(s,files[i].sha256);}s.Append('}');return TextHash(s.ToString());}
        static void Quote(StringBuilder s,string text){s.Append('"');foreach(char c in text){if(c=='"')s.Append("\\\"");else if(c=='\\')s.Append("\\\\");else if(c<32||c>126)s.Append("\\u").Append(((int)c).ToString("x4"));else s.Append(c);}s.Append('"');}
        static string TextHash(string value)=>Hash(Encoding.UTF8.GetBytes(value));
        static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
