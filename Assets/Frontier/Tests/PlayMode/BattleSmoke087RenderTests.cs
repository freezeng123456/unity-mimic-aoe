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
    // Controlled spawned units and a real accepted attack generate real simulation
    // Fire events. No fake events or synthetic composite images. This is a close
    // render study, not a paid match, native-input test, or visual approval.
    public sealed class BattleSmoke087RenderTests
    {
        const int Width=1920,Height=1080,Layer=29;
        const string Scope="Actual Editor WorldView/GameCamera/URP capture after controlled spawned units issue an accepted attack and the simulation emits Fire. Natural actor vision, no forced fog reveal. Controlled setup, close camera, HUD excluded; not paid production, native input, full match, performance or art approval. The control frame temporarily hides smoke renderers only.";
        [Serializable] sealed class SourceEntry { public string path,sha256; }
        [Serializable] sealed class Inventory { public string sourceFingerprint,runnerExpectedFingerprint;public SourceEntry[] files; }
        [Serializable] sealed class Receipt
        {
            public string scope=Scope,status,sourceFingerprint,sourceAfter,worldSha256,actualPngSha256,controlPngSha256,unit,shotMessage,unityVersion,gpu;
            public int sourceId,targetId,shotVisibilityMask,smokeRendererCount,smokeChangedPixels,width=Width,height=Height;
            public float shotTime,captureTime,elapsedAfterFire,zoom;
            public Vector2 shotPosition,shotFacing;public bool commandAccepted,worldStable,sourceStable;
        }
        [UnityTest] public IEnumerator MusketAndCannonActualFireRenderPowderSmoke()
        {
            Assert.IsTrue(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset);
            string output=Environment.GetEnvironmentVariable("FRONTIER087_BATTLE_EVIDENCE");
            Assert.IsFalse(string.IsNullOrWhiteSpace(output),"Set a fresh output directory");
            output=Path.GetFullPath(output);Directory.CreateDirectory(output);
            Assert.IsFalse(Directory.EnumerateFileSystemEntries(output).Any(),"Use a new empty evidence directory");
            var sources=Sources();string fingerprint=Fingerprint(sources),expected=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT");
            Assert.IsFalse(string.IsNullOrWhiteSpace(expected));Assert.AreEqual(expected,fingerprint,"Bind captures to the runner's actual source tree");
            File.WriteAllText(Path.Combine(output,"source-inventory087.json"),JsonUtility.ToJson(new Inventory{sourceFingerprint=fingerprint,runnerExpectedFingerprint=expected,files=sources},true));
            yield return Capture(output,"musketeer",fingerprint);
            yield return Capture(output,"cannon",fingerprint);
        }
        IEnumerator Capture(string output,string unit,string fingerprint)
        {
            string dir=Path.Combine(output,unit);Directory.CreateDirectory(dir);
            Assert.IsNull(Camera.main,"Run in an empty PlayMode scene");
            var session=new GameObject("087 real Fire smoke study "+unit);var cameraObject=new GameObject("Strategy Camera");cameraObject.transform.SetParent(session.transform,false);cameraObject.tag="MainCamera";
            var camera=cameraObject.AddComponent<Camera>();cameraObject.AddComponent<UniversalAdditionalCameraData>().renderShadows=true;
            var game=session.AddComponent<GameController>();game.enabled=false;game.Controls.enabled=false;
            float oldTimeScale=Time.timeScale;bool oldAudio=AudioListener.pause;
            RenderTexture rt=null;Light sun=null;var layers=new Dictionary<GameObject,int>();Canvas[] canvases=null;bool[] canvasEnabled=null;MonoBehaviour[] behaviours=null;bool[] behaviourEnabled=null;
            try
            {
                Time.timeScale=0;AudioListener.pause=true;
                foreach(string name in new[]{"saveDirectoryOverride","reportDirectoryOverride","replayDirectoryOverride"})
                    typeof(GameController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,Path.Combine(dir,"IsolatedUserData",name));
                game.MatchSeed=42;game.PlayerCivilization="coast";game.OpponentCivilization="ottoman";game.StartMatch();
                Assert.IsNotNull(game.World);Assert.AreSame(camera,game.GameCamera);var w=game.World;
                foreach(var guard in w.Entities.Where(World.Guardian).ToArray())w.Remove(guard);
                var actor=w.Spawn(unit,0,new Vector2(70,25));var target=w.Spawn("pike",1,new Vector2(80,25));
                w.Fog.Tick(w.Entities);Assert.IsTrue(w.IsVisible(0,actor));Assert.IsTrue(w.IsVisible(0,target));
                var hold=w.Issue(new GameCommand{kind=CommandKind.Order,player=1,actors=new[]{target.id},order=OrderKind.Hold,point=target.position});Assert.IsTrue(hold.accepted);
                var command=game.Command(new GameCommand{kind=CommandKind.Order,player=0,actors=new[]{actor.id},order=OrderKind.Attack,point=target.position,target=target.id});Assert.IsTrue(command.accepted,command.reason);
                int start=w.Events.Count;SimEvent shot=null;
                for(int i=0;i<150&&shot==null;i++){w.Tick(.1f);shot=w.Events.Skip(start).FirstOrDefault(e=>e.kind=="Fire"&&e.entity==actor.id&&e.target==target.id&&e.message!="Melee");}
                Assert.IsNotNull(shot,"The actual simulation must fire the requested ranged weapon");
                Assert.IsTrue(CombatFeedback.CanDisplay(w,0,shot),"Fire event must be visible at event time and capture time");
                w.Tick(.1f);float elapsed=w.Time-shot.time;Assert.That(elapsed,Is.InRange(.05f,.30f),"Smoke must be captured shortly after the actual shot");
                game.Controls.RestoreCamera(actor.position,12);game.Controls.enabled=false;
                for(int i=0;i<12;i++)yield return null;
                var feedback=session.GetComponentsInChildren<CombatFeedback>(true).Single();Assert.Greater(feedback.ActiveCount,0,"WorldView must consume the real fire event");
                var root=feedback.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Pooled combat feedback"&&t.gameObject.activeInHierarchy).OrderBy(t=>Vector2.Distance(new Vector2(t.position.x,t.position.z),shot.position)).FirstOrDefault();
                Assert.IsNotNull(root);Assert.Less(Vector2.Distance(new Vector2(root.position.x,root.position.z),shot.position),2,"Feedback must correspond to the firing actor");
                var smoke=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Powder smoke"&&r.gameObject.activeInHierarchy&&r.enabled).ToArray();Assert.AreEqual(3,smoke.Length,"Require the real active smoke renderers");
                canvases=session.GetComponentsInChildren<Canvas>(true);canvasEnabled=canvases.Select(c=>c.enabled).ToArray();foreach(var canvas in canvases)canvas.enabled=false;
                foreach(var t in session.GetComponentsInChildren<Transform>(true)){layers[t.gameObject]=t.gameObject.layer;t.gameObject.layer=Layer;}
                camera.enabled=false;camera.aspect=Width/(float)Height;camera.cullingMask=1<<Layer;
                sun=new GameObject("087 controlled directional light").AddComponent<Light>();sun.transform.SetParent(session.transform,false);sun.type=LightType.Directional;sun.intensity=1.55f;sun.color=new Color(1,.96f,.87f);sun.shadows=LightShadows.Soft;sun.cullingMask=1<<Layer;sun.transform.rotation=Quaternion.Euler(48,-28,0);
                rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);Assert.IsTrue(rt.Create());
                behaviours=session.GetComponentsInChildren<MonoBehaviour>(true);behaviourEnabled=behaviours.Select(b=>b.enabled).ToArray();
                for(int i=0;i<behaviours.Length;i++)if(!(behaviours[i] is UniversalAdditionalCameraData))behaviours[i].enabled=false;
                string before=JsonUtility.ToJson(w.CaptureState());Color32[] control=null,actual=null;
                foreach(var r in smoke)r.enabled=false;yield return ReadFrame(camera,rt,p=>control=p);foreach(var r in smoke)r.enabled=true;yield return ReadFrame(camera,rt,p=>actual=p);
                Assert.AreEqual(before,JsonUtility.ToJson(w.CaptureState()),"Rendering must not advance or mutate the simulated world");
                int changed=0;for(int i=0;i<actual.Length;i++)if(Math.Max(Math.Abs(actual[i].r-control[i].r),Math.Max(Math.Abs(actual[i].g-control[i].g),Math.Abs(actual[i].b-control[i].b)))>3)changed++;
                Assert.Greater(changed,3,"Smoke must contribute visible pixels to the real frame; inspect it for visual quality separately");
                byte[] real=Encode(actual),without=Encode(control);File.WriteAllBytes(Path.Combine(dir,"fire-original.png"),real);File.WriteAllBytes(Path.Combine(dir,"control-with-smoke-hidden.png"),without);
                string after=Fingerprint(Sources());Assert.AreEqual(fingerprint,after,"Source must remain stable for the entire capture");
                var receipt=new Receipt{status="actual-fire-captured-not-visual-approval",sourceFingerprint=fingerprint,sourceAfter=after,worldSha256=TextHash(before),actualPngSha256=Hash(real),controlPngSha256=Hash(without),unit=unit,shotMessage=shot.message,unityVersion=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,sourceId=actor.id,targetId=target.id,shotVisibilityMask=shot.visibilityMask,smokeRendererCount=smoke.Length,smokeChangedPixels=changed,shotTime=shot.time,captureTime=w.Time,elapsedAfterFire=elapsed,zoom=camera.orthographicSize,shotPosition=shot.position,shotFacing=shot.facing,commandAccepted=command.accepted,worldStable=true,sourceStable=true};
                File.WriteAllText(Path.Combine(dir,"fire-receipt087.json"),JsonUtility.ToJson(receipt,true));
            }
            finally
            {
                if(behaviours!=null)for(int i=0;i<behaviours.Length;i++)if(behaviours[i]!=null)behaviours[i].enabled=behaviourEnabled[i];
                if(canvases!=null)for(int i=0;i<canvases.Length;i++)if(canvases[i]!=null)canvases[i].enabled=canvasEnabled[i];
                foreach(var layer in layers)if(layer.Key!=null)layer.Key.layer=layer.Value;
                if(rt!=null){rt.Release();UnityEngine.Object.Destroy(rt);}if(sun!=null)UnityEngine.Object.Destroy(sun.gameObject);
                Time.timeScale=oldTimeScale;AudioListener.pause=oldAudio;UnityEngine.Object.Destroy(session);
            }
            yield return null;
        }
        static IEnumerator ReadFrame(Camera camera,RenderTexture rt,Action<Color32[]> receive)
        {
            for(int i=0;i<2;i++){RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;}
            var prior=RenderTexture.active;var image=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply();receive(image.GetPixels32());}
            finally{RenderTexture.active=prior;UnityEngine.Object.Destroy(image);}
        }
        static byte[] Encode(Color32[] pixels)
        {var image=new Texture2D(Width,Height,TextureFormat.RGBA32,false);try{image.SetPixels32(pixels);image.Apply();return image.EncodeToPNG();}finally{UnityEngine.Object.Destroy(image);}}
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
