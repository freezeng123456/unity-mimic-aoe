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
    // A small actual-WorldView image study. No fake screenshot, force-revealed
    // fog, new units, stat/HP/position changes, injected damage or native claim.
    public sealed class ClassicTactics084RenderTests
    {
        const int Width=1920,Height=1080,Layer=29;
        const string Scope="Controlled Editor GameController/WorldView/GameCamera/URP originals of the four new tactical starts after accepted public deployment and attack commands cause actual combat. Scenario-authored initial forces; ordinary fog and original unit rules. HUD excluded; no native input/window, normal Age-I payment, completed tactics, full match, balance, performance or final-art acceptance.";
        GameObject session;GameController game;string output,fingerprint,inventoryHash;
        readonly List<CaptureReceipt> captures=new List<CaptureReceipt>();
        [Serializable] sealed class SourceEntry {public string path,sha256;}
        [Serializable] sealed class Inventory {public string schema="tactics-source-inventory084",sourceFingerprint,runnerExpectedFingerprint;public SourceEntry[] files;}
        [Serializable] sealed class Subject
        {public int id,owner;public string definition;public float hp,maxHp;public Vector2 position;public Vector3 renderedPosition;public bool alive,visible;}
        [Serializable] sealed class CaptureReceipt
        {
            public string schema="classic-tactics-urp-original084",scope=Scope,file,sha256,worldSha256,simulationSha256,guidedSha256,documentSha256,scenarioFileSha256,rulesSha256,sourceFingerprint,sourceInventorySha256;
            public string capturedUtc,appVersion,unityVersion,buildGuid,graphicsDevice,renderPipeline,definitionId,sourceMapId,runtimeMapId,matchId,stage,status,playerCivilization,opponentCivilization;
            public int width,height,seed,startingAge,playerAge,opponentAge,simulationSteps,playerFireEvents,playerDamageEvents,enemyFireEvents,activeProjectiles,unitGeometryChangedPixels,visibleWaterViewportSamples;
            public float simulationTime,cameraZoom,cameraAspect,unityTime;
            public Vector2 cameraFocus;public Vector3 cameraPosition,cameraEuler;
            public bool controllerDisabled,hudExcluded,manualFogReveal=false,worldUnchangedDuringRender;
            public Subject[] featured;public GuidedPlayCommandProof[] acceptedCommands;
        }
        [Serializable] sealed class RunReceipt
        {public string schema="classic-tactics-urp-study084",scope=Scope,status,sourceFingerprint,sourceInventorySha256;public bool completed;public CaptureReceipt[] captures;}

        [UnityTest] public IEnumerator FourControlledEncountersRenderActualFirstCombatThroughUrpWithUnrevealedFog()
        {
            Assert.IsTrue(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset);
            output=Environment.GetEnvironmentVariable("FRONTIER084_TACTICS_VISUAL");
            if(string.IsNullOrWhiteSpace(output))output=Path.Combine(Application.temporaryCachePath,"Frontier-tactics-visual084-"+Guid.NewGuid().ToString("N"));
            output=Path.GetFullPath(output);Directory.CreateDirectory(output);
            Assert.IsFalse(Directory.EnumerateFileSystemEntries(output).Any(),"Use a new empty directory; preserve every earlier original");
            TestContext.WriteLine("Actual tactical URP originals: "+output);
            RecordSource();captures.Clear();bool completed=false;
            float oldScale=Time.timeScale;bool oldAudioPause=AudioListener.pause;int oldFrameRate=Application.targetFrameRate,oldVsync=QualitySettings.vSyncCount;
            var existingCamera=Camera.main;Vector3 originalPosition=existingCamera!=null?existingCamera.transform.position:Vector3.zero;Quaternion originalRotation=existingCamera!=null?existingCamera.transform.rotation:Quaternion.identity;
            bool originalOrtho=existingCamera!=null&&existingCamera.orthographic;float originalZoom=existingCamera!=null?existingCamera.orthographicSize:26,originalNear=existingCamera!=null?existingCamera.nearClipPlane:.2f,originalFar=existingCamera!=null?existingCamera.farClipPlane:260;
            var originalClear=existingCamera!=null?existingCamera.clearFlags:CameraClearFlags.SolidColor;var originalBackground=existingCamera!=null?existingCamera.backgroundColor:Color.black;
            try
            {
                // AddComponent runs Awake, then disable before the first frame:
                // no Start/Update, automatic match, reports or wall-time ticking.
                session=new GameObject("084 actual tactical image study");game=session.AddComponent<GameController>();game.enabled=false;game.Controls.enabled=false;
                Assert.IsFalse(game.Settings.Persistent);Assert.IsTrue(game.Progress.MemoryOnly);
                foreach(string name in new[]{"saveDirectoryOverride","reportDirectoryOverride","replayDirectoryOverride"})
                    typeof(GameController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,Path.Combine(output,"IsolatedUserData",name));
                AudioListener.pause=true;Time.timeScale=0;
                foreach(string id in new[]{ClassicCivilizationTactics084.British,ClassicCivilizationTactics084.Dutch,ClassicCivilizationTactics084.French,ClassicCivilizationTactics084.Ottoman})
                {
                    Assert.IsTrue(game.StartGuided(id),game.Notice);game.Controls.enabled=false;
                    Assert.AreEqual(0,game.World.Time);Assert.IsFalse(game.Autoplay);Assert.IsEmpty(game.Simulation.CaptureState().controllers);
                    Assert.AreEqual(ClassicCivilizationTactics084.ArmyCount(id),game.Guided.CaptureState().soldiers.Length);
                    Deploy();FightToFirstActualExchange();
                    yield return Capture(id);
                }
                Assert.AreEqual(4,captures.Count);completed=true;
            }
            finally
            {
                Time.timeScale=oldScale;AudioListener.pause=oldAudioPause;Application.targetFrameRate=oldFrameRate;QualitySettings.vSyncCount=oldVsync;
                if(existingCamera!=null){existingCamera.transform.SetPositionAndRotation(originalPosition,originalRotation);existingCamera.orthographic=originalOrtho;existingCamera.orthographicSize=originalZoom;existingCamera.nearClipPlane=originalNear;existingCamera.farClipPlane=originalFar;existingCamera.clearFlags=originalClear;existingCamera.backgroundColor=originalBackground;}
                Write(output,"capture-run084.json",new RunReceipt{status=completed?"captured-four-actual-editor-originals-not-visual-approval":"incomplete-preserved",completed=completed,sourceFingerprint=fingerprint,sourceInventorySha256=inventoryHash,captures=captures.ToArray()});
                if(!completed&&game?.World!=null)
                {
                    try{Write(output,"last-world.json",game.World.CaptureState());Write(output,"last-simulation.json",game.Simulation.CaptureState());Write(output,"last-guided.json",game.Guided.CaptureState());}
                    catch(Exception e){Debug.LogWarning("Tactical image failure archive: "+e.Message);}
                }
                if(session!=null)UnityEngine.Object.Destroy(session);
            }
            yield return null;
        }
        int[] Army()=>game.Guided.CaptureState().soldiers.Where(id=>game.World.Get(id)?.Alive==true).ToArray();
        void Issue(GameCommand c){var result=game.Command(c);Assert.IsTrue(result.accepted,result.reason+"; "+game.Guided.Definition.Id+" t="+game.World.Time);}
        void Order(int[] actors,OrderKind kind,Vector2 point,int target=0)
        {if(actors.Length>0)Issue(new GameCommand{kind=CommandKind.Order,player=0,actors=actors,order=kind,point=point,target=target});}
        void Step(){Assert.AreEqual(GuidedPlayStatus.Running,game.Guided.Status,game.Guided.Reason);Assert.IsTrue(game.Simulation.Step());}
        void Deploy()
        {
            string id=game.Guided.Definition.Id;var center=ClassicCivilizationTactics084.DeploymentPoint(id);var stations=new Dictionary<int,Vector2>();
            foreach(int soldier in Army())
            {
                var e=game.World.Get(soldier);var peers=Army().Where(a=>game.World.Get(a).def.id==e.def.id).ToArray();int index=Array.IndexOf(peers,soldier);
                float row=id==ClassicCivilizationTactics084.British?(e.def.id=="pike"?4:-4):id==ClassicCivilizationTactics084.Ottoman?(e.def.id=="janissary"?4:-4):0;
                stations[soldier]=center+new Vector2((index-(peers.Length-1)*.5f)*1.8f,row);Order(new[]{soldier},OrderKind.Move,stations[soldier]);
            }
            if(id==ClassicCivilizationTactics084.Ottoman)Issue(new GameCommand{kind=CommandKind.ToggleMode,player=0,actors=Army().Where(a=>game.World.Get(a).def.id=="janissary").ToArray()});
            for(int i=0;i<90&&stations.Any(s=>Vector2.Distance(game.World.Get(s.Key).position,s.Value)>1);i++)Step();
            Assert.IsTrue(stations.All(s=>Vector2.Distance(game.World.Get(s.Key).position,s.Value)<=1),"Wait for actual deployment, never reposition a unit for an image");
            foreach(int soldier in Army())Order(new[]{soldier},OrderKind.Hold,game.World.Get(soldier).position);
        }
        void FightToFirstActualExchange()
        {
            var w=game.World;int start=w.Events.Count;
            bool Exchange()=>w.Events.Skip(start).Any(e=>e.kind=="Fire"&&e.player==0)&&w.Events.Skip(start).Any(e=>e.kind=="Damage"&&e.player==0&&e.amount>0)&&w.Events.Skip(start).Any(e=>e.kind=="Fire"&&e.player==1);
            for(int tick=0;tick<600&&!Exchange();tick++)
            {
                if(tick%10==0)
                {
                    var visible=w.ScenarioEntities("wave-one").Concat(w.ScenarioEntities("wave-two")).Select(w.Get).Where(e=>w.IsVisible(0,e)).ToArray();
                    foreach(var group in Army().GroupBy(a=>w.Get(a).def.id))
                    {
                        // Keep British front pikes in their accepted Hold line.
                        if(game.Guided.Definition.Id==ClassicCivilizationTactics084.British&&group.Key=="pike")continue;
                        var target=visible.OrderBy(e=>Vector2.Distance(e.position,w.Get(group.First()).position)).FirstOrDefault();if(target==null)continue;
                        var actors=group.ToArray();if(actors.All(a=>w.Get(a).orders.Count>0&&w.Get(a).orders.Peek().kind==OrderKind.Attack&&w.Get(a).orders.Peek().target==target.id))continue;
                        Order(actors,OrderKind.Attack,target.position,target.id);
                    }
                }
                Step();
            }
            Assert.IsTrue(Exchange(),"A battle image needs genuine opposing Fire and actual player Damage, not attack orders alone");
        }
        IEnumerator Capture(string id)
        {
            string dir=Path.Combine(output,id);Directory.CreateDirectory(dir);
            var w=game.World;var camera=game.GameCamera;var featured=Army().Select(w.Get).Concat(w.ScenarioEntities("wave-one").Concat(w.ScenarioEntities("wave-two")).Select(w.Get).Where(e=>w.IsVisible(0,e))).Distinct().ToArray();
            var focus=Vector2.zero;foreach(var e in featured)focus+=e.position;focus/=featured.Length;
            game.Controls.RestoreCamera(focus,16);game.Controls.enabled=false;
            var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;float oldZoom=camera.orthographicSize,oldAspect=camera.aspect;int oldMask=camera.cullingMask;bool oldCamera=camera.enabled;
            var canvases=session.GetComponentsInChildren<Canvas>(true);var canvasEnabled=canvases.Select(c=>c.enabled).ToArray();
            foreach(var canvas in canvases)canvas.enabled=false;
            camera.enabled=false;camera.aspect=Width/(float)Height;camera.cullingMask=1<<Layer;
            var sun=new GameObject("084 controlled directional image light").AddComponent<Light>();sun.transform.SetParent(session.transform,false);
            sun.type=LightType.Directional;sun.intensity=1.55f;sun.color=new Color(1,.96f,.87f);sun.shadows=LightShadows.Soft;sun.cullingMask=1<<Layer;sun.transform.rotation=Quaternion.Euler(48,-28,0);
            var rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);Assert.IsTrue(rt.Create());
            var layers=new Dictionary<GameObject,int>();MonoBehaviour[] behaviours=null;bool[] enabled=null;Renderer[] subjects=null;bool[] subjectEnabled=null;
            try
            {
                // Actual WorldView consumes the real events, visibility and poses.
                // No World tick/reveal happens while waiting for presentation.
                for(int i=0;i<12;i++)yield return null;yield return new WaitForSecondsRealtime(.4f);
                foreach(var t in session.GetComponentsInChildren<Transform>(true)){layers[t.gameObject]=t.gameObject.layer;t.gameObject.layer=Layer;}
                foreach(var e in featured)
                {
                    var root=session.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Entity "+e.id);Assert.IsTrue(root.gameObject.activeInHierarchy);
                    var p=camera.WorldToViewportPoint(root.position+Vector3.up*.7f);Assert.That(p.x,Is.InRange(.02f,.98f),e.def.id);Assert.That(p.y,Is.InRange(.02f,.98f),e.def.id);
                }
                behaviours=session.GetComponentsInChildren<MonoBehaviour>(true);enabled=behaviours.Select(b=>b.enabled).ToArray();
                for(int i=0;i<behaviours.Length;i++)if(!(behaviours[i] is UniversalAdditionalCameraData))behaviours[i].enabled=false;
                string world=JsonUtility.ToJson(w.CaptureState()),simulation=JsonUtility.ToJson(game.Simulation.CaptureState()),guided=JsonUtility.ToJson(game.Guided.CaptureState());
                File.WriteAllText(Path.Combine(dir,"world.json"),world,new UTF8Encoding(false));File.WriteAllText(Path.Combine(dir,"simulation.json"),simulation,new UTF8Encoding(false));File.WriteAllText(Path.Combine(dir,"guided.json"),guided,new UTF8Encoding(false));Write(dir,"scenario.json",w.ScenarioDocument);
                subjects=featured.SelectMany(e=>session.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Entity "+e.id).GetComponentsInChildren<MeshRenderer>(true)).Distinct().Cast<Renderer>().ToArray();
                subjectEnabled=subjects.Select(r=>r.enabled).ToArray();Assert.IsTrue(subjectEnabled.Any(e=>e));
                Color32[] control=null,actual=null;
                for(int i=0;i<subjects.Length;i++)subjects[i].enabled=false;
                yield return ReadFrame(rt,p=>control=p);
                for(int i=0;i<subjects.Length;i++)subjects[i].enabled=subjectEnabled[i];
                yield return ReadFrame(rt,p=>actual=p);
                int changed=0;for(int i=0;i<actual.Length;i++)if(Math.Max(Math.Abs(actual[i].r-control[i].r),Math.Max(Math.Abs(actual[i].g-control[i].g),Math.Abs(actual[i].b-control[i].b)))>3)changed++;
                Assert.Greater(changed,100,"Real unit geometry must affect the actual frame");Assert.AreEqual(world,JsonUtility.ToJson(w.CaptureState()),"Rendering must leave the World, fog and orders unchanged");
                Assert.AreEqual(simulation,JsonUtility.ToJson(game.Simulation.CaptureState()));Assert.AreEqual(guided,JsonUtility.ToJson(game.Guided.CaptureState()));
                Assert.AreEqual(fingerprint,Fingerprint(SourceEntries()),"Source changed during image study");
                var texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false);byte[] png;try{texture.SetPixels32(actual);texture.Apply();png=texture.EncodeToPNG();}finally{UnityEngine.Object.Destroy(texture);}
                File.WriteAllBytes(Path.Combine(dir,"battle-original.png"),png);
                int visibleWater=0;for(int z=1;z<128;z+=2)for(int x=1;x<128;x+=2){var p=new Vector2(x,z);if(!w.IsWater(p)||w.Fog.At(0,p)!=VisibilityState.Visible)continue;var v=camera.WorldToViewportPoint(new Vector3(x,w.Layout.WaterHeight,z));if(v.z>0&&v.x>0&&v.x<1&&v.y>0&&v.y<1)visibleWater++;}
                var receipt=new CaptureReceipt{file="battle-original.png",sha256=Hash(png),worldSha256=TextHash(world),simulationSha256=TextHash(simulation),guidedSha256=TextHash(guided),documentSha256=ScenarioCodec.ContentHash(w.ScenarioDocument),scenarioFileSha256=Hash(File.ReadAllBytes(Path.Combine(dir,"scenario.json"))),rulesSha256=SnapshotCodec.RulesHash(w.Rules),sourceFingerprint=fingerprint,sourceInventorySha256=inventoryHash,capturedUtc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,unityVersion=Application.unityVersion,buildGuid=Application.buildGUID,graphicsDevice=SystemInfo.graphicsDeviceName,renderPipeline=GraphicsSettings.currentRenderPipeline.GetType().FullName,definitionId=id,sourceMapId=ClassicCivilizationTactics084.SourceMapId(id),runtimeMapId=w.MapId,matchId=game.MatchId,playerCivilization=w.Players[0].civilization,opponentCivilization=w.Players[1].civilization,startingAge=game.Guided.Definition.StartingAge,playerAge=w.Players[0].age,opponentAge=w.Players[1].age,stage=game.Guided.Stage?.Id??"",status=game.Guided.Status.ToString(),width=Width,height=Height,seed=w.Seed,simulationSteps=game.Simulation.StepCount,simulationTime=w.Time,playerFireEvents=w.Events.Count(e=>e.kind=="Fire"&&e.player==0),playerDamageEvents=w.Events.Count(e=>e.kind=="Damage"&&e.player==0&&e.amount>0),enemyFireEvents=w.Events.Count(e=>e.kind=="Fire"&&e.player==1),activeProjectiles=w.Projectiles.Count,unitGeometryChangedPixels=changed,visibleWaterViewportSamples=visibleWater,cameraZoom=camera.orthographicSize,cameraAspect=camera.aspect,cameraFocus=game.Controls.CameraFocus,cameraPosition=camera.transform.position,cameraEuler=camera.transform.eulerAngles,unityTime=Time.time,controllerDisabled=!game.enabled,hudExcluded=canvases.All(c=>!c.enabled),worldUnchangedDuringRender=true,acceptedCommands=game.Guided.CaptureState().commands,featured=featured.Select(e=>new Subject{id=e.id,owner=e.owner,definition=e.def.id,hp=e.hp,maxHp=w.MaxHp(e),position=e.position,alive=e.Alive,visible=w.IsVisible(0,e),renderedPosition=session.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Entity "+e.id).position}).ToArray()};
                captures.Add(receipt);Write(dir,"battle-original.json",receipt);
            }
            finally
            {
                if(subjects!=null&&subjectEnabled!=null)for(int i=0;i<subjects.Length;i++)if(subjects[i]!=null)subjects[i].enabled=subjectEnabled[i];
                if(behaviours!=null&&enabled!=null)for(int i=0;i<behaviours.Length;i++)if(behaviours[i]!=null)behaviours[i].enabled=enabled[i];
                foreach(var layer in layers)if(layer.Key!=null)layer.Key.layer=layer.Value;
                for(int i=0;i<canvases.Length;i++)if(canvases[i]!=null)canvases[i].enabled=canvasEnabled[i];
                camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.orthographicSize=oldZoom;camera.aspect=oldAspect;camera.cullingMask=oldMask;camera.enabled=oldCamera;
                rt.Release();UnityEngine.Object.Destroy(rt);if(sun!=null)UnityEngine.Object.Destroy(sun.gameObject);
            }
        }
        IEnumerator ReadFrame(RenderTexture rt,Action<Color32[]> receive)
        {
            for(int i=0;i<2;i++){RenderPipeline.SubmitRenderRequest(game.GameCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;}
            var old=RenderTexture.active;var image=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply();var p=image.GetPixels32();Assert.Greater(p.Max(c=>(int)c.r)-p.Min(c=>(int)c.r),20,"Require a nonempty actual URP frame");receive(p);}
            finally{RenderTexture.active=old;UnityEngine.Object.Destroy(image);}
        }
        void RecordSource()
        {
            var files=SourceEntries();fingerprint=Fingerprint(files);string expected=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT");Assert.IsFalse(string.IsNullOrWhiteSpace(expected),"Root runner must bind the actual source");Assert.AreEqual(expected,fingerprint);
            byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Inventory{sourceFingerprint=fingerprint,runnerExpectedFingerprint=expected,files=files},true));inventoryHash=Hash(bytes);File.WriteAllBytes(Path.Combine(output,"source-inventory084.json"),bytes);
        }
        static SourceEntry[] SourceEntries()
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
        static void Write(string dir,string name,object value)=>File.WriteAllText(Path.Combine(dir,name),JsonUtility.ToJson(value,true),new UTF8Encoding(false));
        static string TextHash(string value)=>Hash(Encoding.UTF8.GetBytes(value));
        static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
