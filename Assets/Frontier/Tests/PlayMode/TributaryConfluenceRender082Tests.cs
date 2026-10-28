using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    // Import this SAME file before the old-Tributaries capture and keep it unchanged
    // for the new-Tributaries capture. No replacement meshes or new-art oracle.
    public sealed class TributaryConfluenceRender082Tests
    {
        const int Width=1920,Height=1080,Layer=29,Threshold=3;
        const bool AnchorsFrozen=true; // Literal contexts from closed old GameplayBaseline1 and post-Dock DockBaseline1 probes.
        static readonly Vector2[] DockContexts={new Vector2(62.0f,68.0f),new Vector2(44.0f,56.0f)};
        static readonly Vector2[] ShipContexts={new Vector2(64.37175750732422f,61.43972396850586f),new Vector2(77.11881256103516f,74.0f),new Vector2(44.70127487182617f,62.94050979614258f),new Vector2(73.02684783935547f,56.0f)};
        static readonly Vector2[] ShipFacings={new Vector2(0.999990701675415f,-0.004307857248932123f),new Vector2(0.4337988793849945f,-0.901009738445282f),new Vector2(0.8969408869743347f,-0.44215044379234314f),new Vector2(-0.6322618126869202f,-0.7747548222541809f)};
        static readonly Vector2[] ViewFocus={new Vector2(62,67),new Vector2(62,58)}; // literal old/current framing, actual coverage still asserted
        GameObject session;GameController game;Camera camera;
        readonly List<EntityState> actors=new List<EntityState>();
        readonly List<Frame> frames=new List<Frame>();
        Vector2[] centralCandidates;
        [Serializable] sealed class Source {public string path,sha256;}
        [Serializable] sealed class Sources {public string fingerprint;public Source[] files;}
        [Serializable] sealed class Actor {public int id;public string definition;public Vector2 position,facing;public float radius;}
        [Serializable] sealed class MeshItem
        {
            public string path,meshSha256,propertyBlockSha256;public string[] materialSha256;public float[] matrix;
            public int vertices,triangles;public bool active,rendererEnabled,receiveShadows;
            public int shadowCasting;public Bounds localBounds;
        }
        [Serializable] sealed class SceneItems {public MeshItem[] meshes;}
        [Serializable] sealed class Frame
        {
            public string name,file,pngSha256,rgbaSha256,repeatFile,repeatPngSha256,repeatRgbaSha256;
            public string sourceFingerprint,worldSha256,modelsSha256,scenerySha256,cameraSha256;
            public Vector2 focus;public float zoom,repeatMeanRgbDelta;public int repeatChangedPixels,threshold=Threshold;
            public Vector3 cameraPosition,cameraEuler;public float[] projection,worldToCamera;public Vector3[] groundCoverageProbes;
        }
        [Serializable] sealed class Run
        {
            public string schema="confluence082-actual-editor-urp-capture",status="incomplete-preserved",role;
            public string scope="Actual imported-source Confluence WorldView/GameCamera/URP with initial real resources and controlled spawned Dock/fleet context at six literal old actual discovered centers/facings (fixed from actual old gameplay plus post-Dock public FindSpawnPositions discovery). Revealed fog, fixed simulation time0; no paid chain/native/FPS/fullmatch acceptance. Old0.24 baseline is instrumented with this same frozen test, not the unmodified081 release. All original resource positions/budgets are intended preserved; explicitly changed bank/profile/scenery remains a scene difference, not an identical-scene promise.";
            public string startedUtc,finishedUtc,appVersion,buildGuid,unityVersion,gpu,pipeline,sourceBefore,sourceAfter,fixtureSha256,waterSourceSha256,fleetSourceSha256,anchorSourceSha256,profileSourceSha256;
            public string worldSha256,modelsSha256,scenerySha256,lightingSha256,rulesSha256,sourceInventoryFile="source-inventory082.json";
            public int seed,width=Width,height=Height,simulationSteps;public float worldTime,defaultZoom,sunIntensity;
            public Vector3 sunEuler;public Color sunColor,ambient;public bool completed,sourceStable,worldStable,sceneStable,linearColorSpace;
            public Actor[] actors;public Frame[] frames;public Vector2[] centralDockCandidates;public float dockPairNormalDot;
            public string contextSelection="Literal old gameplay-query centers and controlled tangent headings; sequential public legality and exact Spawns are checked in this run; no dynamic selection or fallback",contextDiscoveryProbeSha256="04e19765f6099e244e291b53260bbe7389d0fc866e85130741e444df92166911",postDockDiscoveryProbeSha256="eec502f118e41d042514468c77b99cba778ef2b2a09ef8bd32df903afce17f2e";
        }
        [UnitySetUp] public IEnumerator SetUp()
        {
            Assert.IsTrue(AnchorsFrozen,"Literal old contexts are frozen before any active production change");
            Assert.IsNull(Camera.main,"Use an empty PlayMode test scene, not another session camera");
            session=new GameObject("082 actual source-bound confluence render fixture");
            var cameraObject=new GameObject("Strategy Camera");cameraObject.transform.SetParent(session.transform,false);cameraObject.tag="MainCamera";
            camera=cameraObject.AddComponent<Camera>();cameraObject.AddComponent<UniversalAdditionalCameraData>().renderShadows=true;
            game=session.AddComponent<GameController>();game.enabled=false;Assert.AreSame(camera,game.GameCamera);
            game.SelectedMap=MapTopology.Tributaries;game.MatchSeed=42;game.PlayerCivilization="coast";game.OpponentCivilization="ottoman";game.StartMatch();
            game.Controls.enabled=false;game.Controls.Selected.Clear();game.Options.friendEnemyPatterns=false;
            game.World.Fog.Reveal(0,new Vector2(64,64),180);
            // All actual discovered centers/facings are frozen as literals in this SAME source.
            // No current-resource ranking, first-success choice or Spawn relocation.
            centralCandidates=DockContexts;
            Assert.Less(Vector2.Dot(game.World.DockWaterDirection(DockContexts[0]),game.World.DockWaterDirection(DockContexts[1])),-.8f);
            for(int i=0;i<2;i++)
            {
                var point=DockContexts[i];Assert.AreEqual("",game.World.ScenarioPlacementReason("dock",point),"Frozen context Dock must remain publicly legal");
                var actor=game.World.Spawn("dock",0,point);Assert.AreEqual(point,actor.position,"No context relocation");actor.facing=Vector2.up;actors.Add(actor);
            }
            for(int i=0;i<4;i++)
            {
                string definition=i%2==0?"transport_ship":"frigate";var point=ShipContexts[i];
                Assert.AreEqual("",game.World.ScenarioPlacementReason(definition,point),"Frozen actual vessel center must remain publicly legal");Assert.IsFalse(game.World.IsBridgeApproach(point));
                Assert.IsTrue(actors.Where(e=>e.def.waterborne).All(e=>(e.position-point).sqrMagnitude>=36),"Fixed waterborne contexts remain >=6m apart");
                var actor=game.World.Spawn(definition,0,point);Assert.AreEqual(point,actor.position,"No context relocation");actor.facing=ShipFacings[i];actor.hold=true;actors.Add(actor);
                Assert.IsTrue(game.World.WaterNavigation.IsWalkable(actor.position,actor.def.radius));
            }
            game.Controls.RestoreCamera(ViewFocus[0],26);
            for(int i=0;i<12;i++)yield return null;yield return new WaitForSecondsRealtime(.4f);
        }
        [UnityTearDown] public IEnumerator TearDown(){if(session!=null)UnityEngine.Object.Destroy(session);yield return null;actors.Clear();frames.Clear();}

        [UnityTest] public IEnumerator ActualSourceBoundHarborsAndFleetAtOrdinaryCameraScales()
        {
            Assert.IsTrue(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset);Assert.AreEqual(ColorSpace.Linear,QualitySettings.activeColorSpace);
            string expectedVersion=Environment.GetEnvironmentVariable("FRONTIER_CONFLUENCE_EXPECTED_VERSION"),role=Environment.GetEnvironmentVariable("FRONTIER_CONFLUENCE_CAPTURE_ROLE");
            if(string.IsNullOrWhiteSpace(expectedVersion))expectedVersion="0.25.0";if(string.IsNullOrWhiteSpace(role))role="candidate-confluence-0.25";
            Assert.That(expectedVersion,Is.EqualTo("0.24.0").Or.EqualTo("0.25.0"));Assert.AreEqual(expectedVersion=="0.24.0"?"baseline-confluence-0.24":"candidate-confluence-0.25",role,"Exact recorded role/version association");
            Assert.AreEqual(expectedVersion,Application.version,"Current full/CI defaults to exact0.25; old0.24 baseline requires explicit version and matching role");
            string output=Environment.GetEnvironmentVariable("FRONTIER_CONFLUENCE_OUTPUT"),expected=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT");
            if(string.IsNullOrWhiteSpace(output))output=Path.Combine(Application.temporaryCachePath,"FrontierConfluence082-"+Guid.NewGuid().ToString("N"));
            Assert.IsFalse(string.IsNullOrWhiteSpace(expected),"Keep the existing runner's strict source binding");output=Path.GetFullPath(output);
            Directory.CreateDirectory(output);Assert.IsFalse(Directory.EnumerateFileSystemEntries(output).Any(),"Every run uses a fresh directory");
            var sources=Inventory();string before=Fingerprint(sources);Assert.AreEqual(expected,before);Write(output,"source-inventory082.json",new Sources{fingerprint=before,files=sources});
            var run=new Run{startedUtc=DateTime.UtcNow.ToString("o"),role=role,appVersion=Application.version,buildGuid=Application.buildGUID,
                unityVersion=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,pipeline=GraphicsSettings.currentRenderPipeline.GetType().FullName,sourceBefore=before,seed=game.World.Seed,
                fixtureSha256=Find(sources,"Assets/Frontier/Tests/PlayMode/TributaryConfluenceRender082Tests.cs"),waterSourceSha256=Find(sources,"Assets/Frontier/Presentation/WaterPresentation.cs"),fleetSourceSha256=Find(sources,"Assets/Frontier/Presentation/FleetPresentation.cs"),
                anchorSourceSha256=FindOptional(sources,"Assets/Frontier/Simulation/ConfluenceContent082.cs"),profileSourceSha256=FindOptional(sources,"Assets/Frontier/Simulation/ConfluenceBankProfile082.cs"),
                defaultZoom=camera.orthographicSize,worldTime=game.World.Time,simulationSteps=game.Simulation.StepCount,rulesSha256=SnapshotCodec.RulesHash(game.World.Rules),linearColorSpace=QualitySettings.activeColorSpace==ColorSpace.Linear,
                actors=actors.Select(e=>new Actor{id=e.id,definition=e.def.id,position=e.position,facing=e.facing,radius=e.def.radius}).ToArray(),centralDockCandidates=centralCandidates,
                dockPairNormalDot=Vector2.Dot(game.World.DockWaterDirection(actors[0].position),game.World.DockWaterDirection(actors[1].position))};
            Assert.That(run.role,Is.EqualTo("baseline-confluence-0.24").Or.EqualTo("candidate-confluence-0.25"));Assert.AreEqual(42,run.seed);Assert.AreEqual(MapTopology.Tributaries,game.World.MapId);Assert.AreEqual(26,run.defaultZoom);Assert.AreEqual(0,run.worldTime);Assert.AreEqual(0,run.simulationSteps);
            var layers=session.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.gameObject,t=>t.gameObject.layer);
            var behaviours=session.GetComponentsInChildren<MonoBehaviour>(true);var enabled=behaviours.Select(b=>b.enabled).ToArray();
            var canvases=session.GetComponentsInChildren<Canvas>(true);var canvasEnabled=canvases.Select(c=>c.enabled).ToArray();
            var lights=UnityEngine.Object.FindObjectsByType<Light>();var lightEnabled=lights.Select(l=>l.enabled).ToArray();
            var oldAmbientMode=RenderSettings.ambientMode;var oldAmbient=RenderSettings.ambientLight;bool oldFog=RenderSettings.fog,oldAudio=AudioListener.pause,oldCamera=camera.enabled;var oldSun=RenderSettings.sun;
            float oldScale=Time.timeScale;int oldMask=camera.cullingMask;float oldAspect=camera.aspect;Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;
            Light sun=null;RenderTexture target=null;string world=null,models=null,scenery=null;Func<MeshFilter,bool> isModel=null;
            try
            {
                foreach(var item in layers)item.Key.layer=Layer;
                for(int i=0;i<behaviours.Length;i++)if(!(behaviours[i] is UniversalAdditionalCameraData))behaviours[i].enabled=false;
                foreach(var c in canvases)c.enabled=false;foreach(var l in lights)l.enabled=false;
                Time.timeScale=0;AudioListener.pause=true;camera.enabled=false;camera.aspect=Width/(float)Height;camera.cullingMask=1<<Layer;
                var additional=camera.GetComponent<UniversalAdditionalCameraData>();additional.renderPostProcessing=false;additional.antialiasing=AntialiasingMode.None;
                sun=new GameObject("082 fixed controlled directional light").AddComponent<Light>();sun.transform.SetParent(session.transform,false);
                sun.type=LightType.Directional;sun.intensity=1.55f;sun.color=new Color(1,.96f,.87f);sun.shadows=LightShadows.Soft;sun.shadowStrength=.7f;sun.shadowBias=.03f;sun.shadowNormalBias=.2f;sun.cullingMask=1<<Layer;sun.transform.rotation=Quaternion.Euler(50,-35,0);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.57f,.63f,.65f);RenderSettings.fog=false;RenderSettings.sun=sun;
                run.sunEuler=sun.transform.eulerAngles;run.sunIntensity=sun.intensity;run.sunColor=sun.color;run.ambient=RenderSettings.ambientLight;
                run.lightingSha256=Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new LightData{rotation=run.sunEuler,color=run.sunColor,ambient=run.ambient,intensity=sun.intensity,strength=sun.shadowStrength,bias=sun.shadowBias,normalBias=sun.shadowNormalBias,shadowDistance=QualitySettings.shadowDistance,ambientMode=(int)RenderSettings.ambientMode,shadows=(int)sun.shadows,fog=RenderSettings.fog})));
                target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);Assert.IsTrue(target.Create());
                var roots=actors.Select(e=>session.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Entity "+e.id).GetChild(0)).ToArray();
                isModel=f=>roots.Any(t=>f.transform==t||f.transform.IsChildOf(t));
                models=SceneHash(session.GetComponentsInChildren<MeshFilter>(true).Where(isModel),output,"model-inventory082.json");
                scenery=SceneHash(session.GetComponentsInChildren<MeshFilter>(true).Where(f=>!isModel(f)),output,"scenery-inventory082.json");
                world=JsonHash(game.World.CaptureState());Write(output,"world-snapshot082.json",game.World.CaptureState());
                run.modelsSha256=models;run.scenerySha256=scenery;run.worldSha256=world;
                foreach(var dock in actors.Where(e=>e.def.id=="dock"))foreach(float zoom in new[]{26f,16f})
                {
                    int bank=actors.IndexOf(dock);var focus=ViewFocus[bank];
                    game.Controls.RestoreCamera(focus,zoom);Assert.IsFalse(game.Controls.enabled);yield return null;
                    var frame=new Frame{name=(bank==0?"north-mouth":"south-stage")+"-zoom"+zoom.ToString("0",CultureInfo.InvariantCulture),focus=game.Controls.CameraFocus,zoom=camera.orthographicSize,
                        cameraPosition=camera.transform.position,cameraEuler=camera.transform.eulerAngles,projection=Matrix(camera.projectionMatrix),worldToCamera=Matrix(camera.worldToCameraMatrix),groundCoverageProbes=GroundCoverage(camera),sourceFingerprint=before,worldSha256=world,modelsSha256=models,scenerySha256=scenery};
                    frame.cameraSha256=JsonHash(new CameraData{position=frame.cameraPosition,euler=frame.cameraEuler,projection=frame.projection,view=frame.worldToCamera,aspect=camera.aspect,near=camera.nearClipPlane,far=camera.farClipPlane,zoom=zoom,background=camera.backgroundColor});
                    foreach(var e in actors)
                    {var p=camera.WorldToViewportPoint(new Vector3(e.position.x,game.World.SurfaceHeight(e.position,e.def.waterborne)+1,e.position.y));Assert.That(p.x,Is.InRange(.01f,.99f));Assert.That(p.y,Is.InRange(.01f,.99f));Assert.Greater(p.z,0);}
                    Color32[] original=null,repeat=null;yield return ReadFrame(target,p=>original=p);yield return ReadFrame(target,p=>repeat=p);
                    frame.file=frame.name+"-original.png";frame.repeatFile=frame.name+"-repeat.png";
                    frame.pngSha256=SaveImage(output,frame.file,original);frame.repeatPngSha256=SaveImage(output,frame.repeatFile,repeat);frame.rgbaSha256=PixelHash(original);frame.repeatRgbaSha256=PixelHash(repeat);
                    frame.repeatChangedPixels=Delta(original,repeat,out frame.repeatMeanRgbDelta);frames.Add(frame);Write(output,frame.name+"-frame.json",frame);
                    Assert.LessOrEqual(frame.repeatChangedPixels,Mathf.Max(64,Width*Height/1000),"Repeat drift is recorded; not an art-quality verdict");
                    Assert.AreEqual(world,JsonHash(game.World.CaptureState()));Assert.AreEqual(models,SceneHash(session.GetComponentsInChildren<MeshFilter>(true).Where(isModel)));
                    Assert.AreEqual(scenery,SceneHash(session.GetComponentsInChildren<MeshFilter>(true).Where(f=>!isModel(f))));Assert.AreEqual(0,game.World.Time);Assert.AreEqual(0,game.Simulation.StepCount);
                }
                Assert.AreEqual(4,frames.Count);run.completed=true;
            }
            finally
            {
                run.sourceAfter=Fingerprint(Inventory());run.sourceStable=before==run.sourceAfter;run.worldStable=world!=null&&world==JsonHash(game.World.CaptureState());
                run.sceneStable=isModel!=null&&models==SceneHash(session.GetComponentsInChildren<MeshFilter>(true).Where(isModel))&&scenery==SceneHash(session.GetComponentsInChildren<MeshFilter>(true).Where(f=>!isModel(f)));run.frames=frames.ToArray();run.finishedUtc=DateTime.UtcNow.ToString("o");run.status=run.completed&&run.sourceStable&&run.worldStable&&run.sceneStable?"captured-confluence-originals-with-repeat-controls":"incomplete-preserved";
                Write(output,"capture-run082.json",run);TestContext.WriteLine("Confluence082 actual originals: "+output+" source="+before+" role="+run.role+" version="+run.appVersion);
                for(int i=0;i<behaviours.Length;i++)if(behaviours[i]!=null)behaviours[i].enabled=enabled[i];for(int i=0;i<canvases.Length;i++)if(canvases[i]!=null)canvases[i].enabled=canvasEnabled[i];
                foreach(var item in layers)if(item.Key!=null)item.Key.layer=item.Value;for(int i=0;i<lights.Length;i++)if(lights[i]!=null)lights[i].enabled=lightEnabled[i];
                RenderSettings.ambientMode=oldAmbientMode;RenderSettings.ambientLight=oldAmbient;RenderSettings.fog=oldFog;RenderSettings.sun=oldSun;AudioListener.pause=oldAudio;Time.timeScale=oldScale;
                camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.aspect=oldAspect;camera.cullingMask=oldMask;camera.enabled=oldCamera;camera.orthographicSize=run.defaultZoom;
                if(target!=null){target.Release();UnityEngine.Object.Destroy(target);}if(sun!=null)UnityEngine.Object.Destroy(sun.gameObject);
            }
            Assert.IsTrue(run.completed&&run.sourceStable&&run.worldStable&&run.sceneStable);
        }
        [Serializable] sealed class LightData {public Vector3 rotation;public Color color,ambient;public float intensity,strength,bias,normalBias,shadowDistance;public int ambientMode,shadows;public bool fog;}
        [Serializable] sealed class CameraData {public Vector3 position,euler;public float[] projection,view;public float aspect,near,far,zoom;public Color background;}
        static Vector3[] GroundCoverage(Camera c)
        {
            var probes=new List<Vector3>();foreach(float height in new[]{-.9f,3.2f})foreach(float y in new[]{0f,1f})foreach(float x in new[]{0f,1f})
            {
                var ray=c.ViewportPointToRay(new Vector3(x,y,0));var plane=new Plane(Vector3.up,new Vector3(0,height,0));
                Assert.IsTrue(plane.Raycast(ray,out float distance),"Actual camera corner must meet the authored terrain-height envelope");var p=ray.GetPoint(distance);
                Assert.That(p.x,Is.InRange(1f,127f),"Whole ordinary camera footprint must stay inside the map");Assert.That(p.z,Is.InRange(1f,127f),"Whole ordinary camera footprint must stay inside the map");probes.Add(p);
            }
            return probes.ToArray();
        }
        IEnumerator ReadFrame(RenderTexture target,Action<Color32[]> receive)
        {
            for(int i=0;i<2;i++){RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});yield return null;}
            var previous=RenderTexture.active;var texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,Width,Height),0,0);texture.Apply();var p=texture.GetPixels32();Assert.Greater(p.Max(c=>(int)c.r)-p.Min(c=>(int)c.r),20,"Require a nonempty actual scene");receive(p);}
            finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(texture);}
        }
        static string SaveImage(string output,string name,Color32[] pixels)
        {var t=new Texture2D(Width,Height,TextureFormat.RGBA32,false);try{t.SetPixels32(pixels);t.Apply();byte[] bytes=t.EncodeToPNG();File.WriteAllBytes(Path.Combine(output,name),bytes);return Hash(bytes);}finally{UnityEngine.Object.Destroy(t);}}
        static int Delta(Color32[] a,Color32[] b,out float mean)
        {int count=0;long sum=0;Assert.AreEqual(a.Length,b.Length);for(int i=0;i<a.Length;i++){int r=Math.Abs(a[i].r-b[i].r),g=Math.Abs(a[i].g-b[i].g),blue=Math.Abs(a[i].b-b[i].b);if(Math.Max(r,Math.Max(g,blue))>Threshold)count++;sum+=r+g+blue;}mean=(float)(sum/(double)(a.Length*3));return count;}
        static string PixelHash(Color32[] p){var b=new byte[p.Length*4];for(int i=0;i<p.Length;i++){int j=i*4;b[j]=p[i].r;b[j+1]=p[i].g;b[j+2]=p[i].b;b[j+3]=p[i].a;}return Hash(b);}
        static float[] Matrix(Matrix4x4 m){var a=new float[16];for(int i=0;i<16;i++)a[i]=m[i];return a;}
        static string SceneHash(IEnumerable<MeshFilter> filters,string output=null,string file=null)
        {
            var materials=new Dictionary<Material,string>();var textures=new Dictionary<Texture,string>();var meshes=new Dictionary<Mesh,string>();var items=new List<MeshItem>();
            foreach(var f in filters.OrderBy(f=>Hierarchy(f.transform),StringComparer.Ordinal))
            {
                var r=f.GetComponent<MeshRenderer>();Assert.IsNotNull(r);var mesh=f.sharedMesh;Assert.IsNotNull(mesh);
                if(!meshes.TryGetValue(mesh,out var mh)){mh=MeshHash(mesh);meshes.Add(mesh,mh);}
                var hashes=r.sharedMaterials.Select(m=>{if(!materials.TryGetValue(m,out var h)){h=MaterialHash(m,textures);materials.Add(m,h);}return h;}).ToArray();
                items.Add(new MeshItem{path=Hierarchy(f.transform),meshSha256=mh,materialSha256=hashes,matrix=Matrix(f.transform.localToWorldMatrix),vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,
                    propertyBlockSha256=PropertyHash(r,textures),active=f.gameObject.activeInHierarchy,rendererEnabled=r.enabled,receiveShadows=r.receiveShadows,shadowCasting=(int)r.shadowCastingMode,localBounds=mesh.bounds});
            }
            var data=new SceneItems{meshes=items.ToArray()};if(output!=null)Write(output,file,data);return JsonHash(data);
        }
        static string Hierarchy(Transform t){var parts=new List<string>();while(t!=null&&t.name!="082 actual source-bound confluence render fixture"){parts.Add(t.name+"#"+t.GetSiblingIndex());t=t.parent;}Assert.IsNotNull(t,"Only fixture-owned scene descendants are inventoried");parts.Reverse();return "fixture/"+string.Join("/",parts);}
        static string MeshHash(Mesh m)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s))
            {
                w.Write(m.vertexCount);w.Write((int)m.indexFormat);w.Write(m.subMeshCount);
                foreach(var v in m.vertices){w.Write(v.x);w.Write(v.y);w.Write(v.z);}foreach(var n in m.normals){w.Write(n.x);w.Write(n.y);w.Write(n.z);}foreach(var c in m.colors){w.Write(c.r);w.Write(c.g);w.Write(c.b);w.Write(c.a);}
                foreach(var t in m.tangents){w.Write(t.x);w.Write(t.y);w.Write(t.z);w.Write(t.w);}for(int channel=0;channel<8;channel++){var uv=new List<Vector4>();m.GetUVs(channel,uv);w.Write(uv.Count);foreach(var v in uv){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}}
                for(int i=0;i<m.subMeshCount;i++){w.Write((int)m.GetTopology(i));var a=m.GetIndices(i);w.Write(a.Length);foreach(int n in a)w.Write(n);}foreach(float n in new[]{m.bounds.center.x,m.bounds.center.y,m.bounds.center.z,m.bounds.size.x,m.bounds.size.y,m.bounds.size.z})w.Write(n);
                w.Flush();return Hash(s.ToArray());
            }
        }
        static string MaterialHash(Material m,Dictionary<Texture,string> textures)
        {
            Assert.IsNotNull(m);var text=new StringBuilder(m.name+"|"+m.shader.name+"|"+m.renderQueue+"|"+m.enableInstancing);foreach(var k in m.shaderKeywords.OrderBy(k=>k,StringComparer.Ordinal))text.Append('|').Append(k);
            for(int i=0;i<m.shader.GetPropertyCount();i++)
            {
                string p=m.shader.GetPropertyName(i);var type=m.shader.GetPropertyType(i);text.Append('|').Append(p).Append(':').Append(type);
                if(type==ShaderPropertyType.Texture)
                {var t=m.GetTexture(p);if(t!=null){if(!textures.TryGetValue(t,out var h)){h=TextureHash(t);textures.Add(t,h);}text.Append(h);}else text.Append("null");var scale=m.GetTextureScale(p);var offset=m.GetTextureOffset(p);Append(text,new Vector4(scale.x,scale.y,offset.x,offset.y));}
                else if(type==ShaderPropertyType.Color||type==ShaderPropertyType.Vector)Append(text,m.GetVector(p));else text.Append(m.GetFloat(p).ToString("R",CultureInfo.InvariantCulture));
            }
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        static string PropertyHash(MeshRenderer r,Dictionary<Texture,string> textures)
        {
            var text=new StringBuilder();var block=new MaterialPropertyBlock();
            for(int slot=-1;slot<r.sharedMaterials.Length;slot++)
            {
                if(slot<0)r.GetPropertyBlock(block);else r.GetPropertyBlock(block,slot);text.Append('|').Append(slot).Append(':').Append(block.isEmpty);if(block.isEmpty)continue;
                foreach(var m in r.sharedMaterials)for(int i=0;i<m.shader.GetPropertyCount();i++)
                {
                    string p=m.shader.GetPropertyName(i);var type=m.shader.GetPropertyType(i);text.Append('|').Append(p);
                    if(type==ShaderPropertyType.Texture){var t=block.GetTexture(p);if(t==null)text.Append("null");else{if(!textures.TryGetValue(t,out var h)){h=TextureHash(t);textures.Add(t,h);}text.Append(h);}}
                    else if(type==ShaderPropertyType.Color||type==ShaderPropertyType.Vector)Append(text,block.GetVector(p));else text.Append(block.GetFloat(p).ToString("R",CultureInfo.InvariantCulture));
                }
            }
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        static void Append(StringBuilder s,Vector4 v){foreach(float f in new[]{v.x,v.y,v.z,v.w})s.Append(',').Append(f.ToString("R",CultureInfo.InvariantCulture));}
        static string TextureHash(Texture t)
        {
            // Non-readable authored textures are copied through the actual GPU;
            // instance IDs are intentionally excluded from cross-run identity.
            var rt=RenderTexture.GetTemporary(t.width,t.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);var old=RenderTexture.active;var copy=new Texture2D(t.width,t.height,TextureFormat.RGBA32,false,true);
            try{Graphics.Blit(t,rt);RenderTexture.active=rt;copy.ReadPixels(new Rect(0,0,t.width,t.height),0,0);copy.Apply();return JsonHash(new TextureData{name=t.name,width=t.width,height=t.height,filter=(int)t.filterMode,wrap=(int)t.wrapMode,rgbaSha256=PixelHash(copy.GetPixels32())});}
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(copy);}
        }
        [Serializable] sealed class TextureData {public string name,rgbaSha256;public int width,height,filter,wrap;}
        static Source[] Inventory()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));var list=new List<Source>();foreach(string d in new[]{"Assets","Packages","ProjectSettings","Tools"})foreach(string f in Directory.GetFiles(Path.Combine(root,d),"*",SearchOption.AllDirectories))
            {string p=f.Substring(root.Length+1).Replace('\\','/');if(p.Split('/').Any(c=>c.StartsWith(".",StringComparison.Ordinal)||c=="__pycache__")||System.Text.RegularExpressions.Regex.IsMatch(p,@"^Assets/InitTestScene[0-9a-f-]{36}\.unity(\.meta)?$"))continue;list.Add(new Source{path=p,sha256=Hash(File.ReadAllBytes(f))});}
            return list.OrderBy(e=>e.path,StringComparer.Ordinal).ToArray();
        }
        static string Find(Source[] s,string p)=>s.Single(e=>e.path==p).sha256;
        static string FindOptional(Source[] s,string p)=>s.FirstOrDefault(e=>e.path==p)?.sha256;
        static string Fingerprint(Source[] s){var b=new StringBuilder("{");for(int i=0;i<s.Length;i++){if(i>0)b.Append(',');Quote(b,s[i].path);b.Append(':');Quote(b,s[i].sha256);}return Hash(Encoding.UTF8.GetBytes(b.Append('}').ToString()));}
        static void Quote(StringBuilder b,string s){b.Append('"');foreach(char c in s){if(c=='"')b.Append("\\\"");else if(c=='\\')b.Append("\\\\");else if(c<32||c>126)b.Append("\\u").Append(((int)c).ToString("x4"));else b.Append(c);}b.Append('"');}
        static string JsonHash(object o)=>Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(o)));
        static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
        static void Write(string output,string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonUtility.ToJson(value,true),new UTF8Encoding(false));
    }
}
