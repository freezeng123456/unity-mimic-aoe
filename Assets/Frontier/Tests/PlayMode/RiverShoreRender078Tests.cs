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
    // Real GameCamera / WorldView / URP study, not a native match. The coarse
    // control reconstructs ONLY the 1m ground grid in this candidate scene.
    public sealed class RiverShoreRender078Tests
    {
        const int Width=1920,Height=1080,Layer=29,Threshold=3,FineCount=257,CoarseCount=129;
        GameObject session;GameController game;
        string output,fingerprint,inventoryHash,revision;
        readonly List<PairReceipt> pairs=new List<PairReceipt>();
        [Serializable] sealed class SourceEntry {public string path,sha256;}
        [Serializable] sealed class Inventory
        {
            public string schema="river-shore-source-inventory-078",revision,sourceFingerprint,runnerExpectedFingerprint;
            public string algorithm="SHA256 of ordinal-key compact ASCII-escaped JSON path-to-SHA256 map for Assets/Packages/ProjectSettings/Tools; hidden components, __pycache__, and temporary Unity InitTestScene GUID assets excluded. Working-tree identity, not a clean-commit assertion.";
            public SourceEntry[] files;
        }
        [Serializable] sealed class ImageReceipt
        {
            public string schema="river-shore-actual-urp-frame-078",file,sha256,rgbaSha256,role,view,recordedAtUtc;
            public string meaning="Actual candidate GameCamera/WorldView/URP offscreen render. Reconstructed coarse control changes the ground mesh only, retaining candidate bank queries, palette, water, models and scenery. Not an original old-build/native image, paid harbor, movement, FPS or visual-quality acceptance.";
            public string appVersion,buildGuid,unityVersion,graphicsDevice,renderPipeline,sourceFingerprint,sourceInventorySha256,referenceRevision,matchId,mapId,worldSha256,groundMeshSha256,otherSceneSha256;
            public int width,height,frame,seed,simulationSteps,entityCount,groundVertices,groundTriangles;
            public float worldTime,unityTime,cameraZoom,cameraAspect,defaultZoom;
            public Vector2 cameraFocus;public Vector3 cameraPosition,cameraEuler;
            public bool gameDisabled,worldViewFrozen,fogFixtureRevealed,onlyGroundMeshVaries;
        }
        [Serializable] sealed class PairReceipt
        {
            public string view,coarseFile,currentFile,repeatFile,coarsePngSha256,currentPngSha256,repeatPngSha256;
            public string coarseMeshSha256,currentMeshSha256,worldSha256,otherSceneSha256;
            public Vector2 focus;public float cameraZoom,meanAbsoluteRgbDelta,repeatMeanAbsoluteRgbDelta;
            public int changedPixelsAboveThreshold,repeatChangedPixelsAboveThreshold,pixelDeltaThreshold;
            public bool expectsVisibleSignal,actualCandidateMeshRestored,worldUnchanged,otherSceneUnchanged;
        }
        [Serializable] sealed class RunReceipt
        {
            public string schema="river-shore-render-study-078",status,recordedAtUtc,appVersion,sourceFingerprint,sourceInventorySha256,referenceRevision;
            public string scope="Four ordinary-camera paired views and repeat-current controls. Coarse 1m ground is reconstructed from the actual candidate's aligned vertices inside this new scene; no old-build, native, paid-harbor, command, performance or visual approval claim.";
            public bool completed,actualCandidateMeshRestored,worldUnchanged,otherSceneUnchanged;
            public int coarseVertices=16641,currentVertices=66049,coarseTriangles=32768,currentTriangles=131072;
            public float defaultZoom;public PairReceipt[] pairs;
        }
        struct ViewSpec
        {
            public string name;public Vector2 focus;public float zoom;public bool signal;
            public ViewSpec(string n,Vector2 p,float z,bool s){name=n;focus=p;zoom=z;signal=s;}
        }

        [UnitySetUp] public IEnumerator CreateActualWorldView()
        {
            session=new GameObject("078 actual shoreline camera fixture");game=session.AddComponent<GameController>();game.enabled=false;
            game.SelectedMap=MapTopology.Winding;game.MatchSeed=42;game.PlayerCivilization="coast";game.OpponentCivilization="ottoman";game.StartMatch();
            game.Controls.RestoreCamera(game.World.OpeningTownPosition(0),game.GameCamera.orthographicSize);game.Controls.enabled=false;
            yield return null;Canvas.ForceUpdateCanvases();yield return null;
        }
        [UnityTearDown] public IEnumerator DestroyActualWorldView(){if(session!=null)UnityEngine.Object.Destroy(session);yield return null;}

        [UnityTest] public IEnumerator ActualCandidateAndReconstructedCoarseGroundRenderAtOrdinaryCameraScales()
        {
            Assert.AreEqual("0.25.0",Application.version,"Run this retained 078-geometry study with the reviewed 082 version identity");
            Assert.IsTrue(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset,"Use the actual project URP asset");
            output=Environment.GetEnvironmentVariable("FRONTIER_SHORE_RENDER_DIR");
            if(string.IsNullOrWhiteSpace(output))output=Path.Combine(Application.temporaryCachePath,"FrontierShore078-"+Guid.NewGuid().ToString("N"));
            output=Path.GetFullPath(output);Directory.CreateDirectory(output);Assert.IsFalse(Directory.EnumerateFileSystemEntries(output).Any(),"Preserve every previous capture in a separate directory");
            RecordSource();pairs.Clear();var camera=game.GameCamera;float defaultZoom=camera.orthographicSize;Assert.AreEqual(26,defaultZoom,.0001f);
            Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;
            float oldSize=camera.orthographicSize,oldAspect=camera.aspect,oldScale=Time.timeScale;
            int oldMask=camera.cullingMask;bool oldCameraEnabled=camera.enabled,oldControls=game.Controls.enabled,oldAudioPause=AudioListener.pause;
            var savedLayers=new Dictionary<GameObject,int>();RenderTexture rt=null;Light sun=null;Mesh coarse=null,candidate=null;MeshFilter ground=null;
            MonoBehaviour[] frozenBehaviours=null;bool[] frozenEnabled=null;
            string candidateHash=null,worldHash=null,sceneHash=null;bool completed=false,restored=false,worldRestored=false,sceneRestored=false;
            try
            {
                Assert.AreEqual(MapTopology.Winding,game.World.MapId);Assert.IsFalse(game.Autoplay);var layout=game.World.Layout;
                var channel=layout.Channels[0];float maximum=channel.Centers.Max(p=>p.x);var bend=channel.Centers.First(p=>p.x==maximum);
                Assert.That(bend.x,Is.InRange(78,80));Assert.That(bend.y,Is.InRange(64,76));
                camera.enabled=false;camera.aspect=Width/(float)Height;camera.cullingMask=1<<Layer;AudioListener.pause=true;Time.timeScale=0;
                sun=new GameObject("078 controlled directional sun").AddComponent<Light>();sun.transform.SetParent(session.transform,false);
                sun.type=LightType.Directional;sun.intensity=1.55f;sun.color=new Color(1,.96f,.87f);sun.shadows=LightShadows.Soft;sun.cullingMask=1<<Layer;sun.transform.rotation=Quaternion.Euler(48,-28,0);
                rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);Assert.IsTrue(rt.Create());
                game.World.Fog.Reveal(0,new Vector2(64,64),180);yield return Settle(savedLayers);
                // Freeze once after fog/model creation, before changing cameras.
                // Camera-dependent pose/visibility updates must not change the
                // scenery between views or confound a ground-only comparison.
                frozenBehaviours=session.GetComponentsInChildren<MonoBehaviour>(true);frozenEnabled=frozenBehaviours.Select(b=>b.enabled).ToArray();
                for(int i=0;i<frozenBehaviours.Length;i++)if(!(frozenBehaviours[i] is UniversalAdditionalCameraData))frozenBehaviours[i].enabled=false;
                var matches=session.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Continuous sculpted land and river bed").ToArray();
                Assert.AreEqual(1,matches.Length,"Only the actual WorldView ground may be swapped");ground=matches[0];candidate=ground.sharedMesh;
                coarse=ReconstructCoarse(candidate,layout);candidateHash=MeshHash(candidate);Assert.AreNotEqual(candidateHash,MeshHash(coarse));
                worldHash=WorldHash();sceneHash=SceneHash(ground);
                var site=layout.DockSites[0][0];var harborFocus=site+game.World.DockWaterDirection(site)*3;
                var views=new[]{new ViewSpec("01-east-bend-default26",bend,defaultZoom,true),new ViewSpec("02-east-bend-close16",bend,16,true),
                    new ViewSpec("03-harbor-pad-default26",harborFocus,defaultZoom,false),new ViewSpec("04-bridge-default26",layout.Bridges[0].Center,defaultZoom,false)};
                foreach(var view in views)
                {
                    game.Controls.RestoreCamera(view.focus,view.zoom);game.Controls.enabled=false;yield return Settle(savedLayers);
                    try
                    {
                        Assert.AreSame(candidate,ground.sharedMesh);Assert.AreEqual(candidateHash,MeshHash(candidate));Assert.AreEqual(worldHash,WorldHash());Assert.AreEqual(sceneHash,SceneHash(ground));
                        Color32[] current=null,baseline=null,repeat=null;
                        yield return ReadFrame(rt,p=>current=p);var currentReceipt=WriteImage(view,"current-fine-half-metre",current,candidate,sceneHash,defaultZoom);
                        ground.sharedMesh=coarse;yield return ReadFrame(rt,p=>baseline=p);var coarseReceipt=WriteImage(view,"control-reconstructed-coarse-one-metre",baseline,coarse,sceneHash,defaultZoom);
                        ground.sharedMesh=candidate;Assert.AreEqual(candidateHash,MeshHash(candidate));yield return ReadFrame(rt,p=>repeat=p);
                        var repeatReceipt=WriteImage(view,"repeat-current-fine-half-metre",repeat,candidate,sceneHash,defaultZoom);
                        float delta,repeatDelta;int changed=ChangedPixels(baseline,current,out delta),drift=ChangedPixels(current,repeat,out repeatDelta);
                        Assert.LessOrEqual(drift,Mathf.Max(64,Width*Height/1000),"Repeat-render noise confounds the paired signal");
                        if(view.signal)Assert.Greater(changed,drift*4+200,"Actual bend signal must exceed repeat noise; pixel change alone is not quality approval");
                        Assert.AreEqual(worldHash,WorldHash());Assert.AreEqual(sceneHash,SceneHash(ground));Assert.AreEqual(fingerprint,SourceFingerprint(SourceEntries()));
                        var pair=new PairReceipt{view=view.name,focus=view.focus,cameraZoom=view.zoom,expectsVisibleSignal=view.signal,
                            currentFile=currentReceipt.file,coarseFile=coarseReceipt.file,repeatFile=repeatReceipt.file,currentPngSha256=currentReceipt.sha256,coarsePngSha256=coarseReceipt.sha256,repeatPngSha256=repeatReceipt.sha256,
                            currentMeshSha256=candidateHash,coarseMeshSha256=MeshHash(coarse),worldSha256=worldHash,otherSceneSha256=sceneHash,
                            changedPixelsAboveThreshold=changed,repeatChangedPixelsAboveThreshold=drift,pixelDeltaThreshold=Threshold,meanAbsoluteRgbDelta=delta,repeatMeanAbsoluteRgbDelta=repeatDelta,
                            actualCandidateMeshRestored=true,worldUnchanged=true,otherSceneUnchanged=true};
                        pairs.Add(pair);WriteJson(view.name+"-pair.json",pair);
                    }
                    finally{ground.sharedMesh=candidate;}
                }
                Assert.AreEqual(4,pairs.Count);completed=true;
            }
            finally
            {
                if(ground!=null&&candidate!=null){ground.sharedMesh=candidate;restored=ReferenceEquals(ground.sharedMesh,candidate)&&MeshHash(candidate)==candidateHash;worldRestored=WorldHash()==worldHash;sceneRestored=SceneHash(ground)==sceneHash;}
                if(frozenBehaviours!=null)for(int i=0;i<frozenBehaviours.Length;i++)if(frozenBehaviours[i]!=null)frozenBehaviours[i].enabled=frozenEnabled[i];
                foreach(var layer in savedLayers)if(layer.Key!=null)layer.Key.layer=layer.Value;
                camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.orthographicSize=oldSize;camera.aspect=oldAspect;camera.cullingMask=oldMask;camera.enabled=oldCameraEnabled;
                game.Controls.enabled=oldControls;Time.timeScale=oldScale;AudioListener.pause=oldAudioPause;
                if(rt!=null){rt.Release();UnityEngine.Object.Destroy(rt);}if(sun!=null)UnityEngine.Object.Destroy(sun.gameObject);if(coarse!=null)UnityEngine.Object.Destroy(coarse);
                WriteJson("study-run078.json",new RunReceipt{status=completed&&restored&&worldRestored&&sceneRestored?"captured-ground-pairs-not-visual-approval":"incomplete-preserved",
                    completed=completed,actualCandidateMeshRestored=restored,worldUnchanged=worldRestored,otherSceneUnchanged=sceneRestored,recordedAtUtc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,
                    sourceFingerprint=fingerprint,sourceInventorySha256=inventoryHash,referenceRevision=revision,defaultZoom=defaultZoom,pairs=pairs.ToArray()});
            }
            Assert.IsTrue(restored&&worldRestored&&sceneRestored,"Exact actual mesh/world/other-scene restoration is required");
        }

        static Mesh ReconstructCoarse(Mesh actual,MapLayout layout)
        {
            Assert.AreEqual(FineCount*FineCount,actual.vertexCount);Assert.AreEqual(IndexFormat.UInt32,actual.indexFormat);Assert.AreEqual(1,actual.subMeshCount);
            var vertices=actual.vertices;var colors=actual.colors;var uv=actual.uv;var indices=actual.triangles;
            Assert.AreEqual(vertices.Length,colors.Length);Assert.AreEqual(vertices.Length,uv.Length);Assert.AreEqual(256*256*6,indices.Length);
            for(int z=0;z<FineCount;z++)for(int x=0;x<FineCount;x++)
            {
                int i=x+z*FineCount;var point=new Vector2(x*.5f,z*.5f);Assert.AreEqual(new Vector3(point.x,layout.BankHeight(point),point.y),vertices[i]);
                Assert.AreEqual(layout.BankLayers(point),colors[i]);Assert.AreEqual(point/128,uv[i]);
                if(x<256&&z<256)
                {int t=(x+z*256)*6,a=i,b=a+FineCount,c=b+1,d=a+1;Assert.AreEqual(a,indices[t]);Assert.AreEqual(b,indices[t+1]);Assert.AreEqual(c,indices[t+2]);Assert.AreEqual(a,indices[t+3]);Assert.AreEqual(c,indices[t+4]);Assert.AreEqual(d,indices[t+5]);}
            }
            var coarseVertices=new Vector3[CoarseCount*CoarseCount];var coarseColors=new Color[coarseVertices.Length];var coarseUv=new Vector2[coarseVertices.Length];var triangles=new int[128*128*6];
            for(int z=0;z<CoarseCount;z++)for(int x=0;x<CoarseCount;x++)
            {
                int i=x+z*CoarseCount,j=x*2+z*2*FineCount;coarseVertices[i]=vertices[j];coarseColors[i]=colors[j];coarseUv[i]=uv[j];
                if(x<128&&z<128){int t=(x+z*128)*6,a=i,b=a+CoarseCount,c=b+1,d=a+1;triangles[t]=a;triangles[t+1]=b;triangles[t+2]=c;triangles[t+3]=a;triangles[t+4]=c;triangles[t+5]=d;}
            }
            var mesh=new Mesh{name="078 reconstructed 1m ground control, candidate query and palette",indexFormat=IndexFormat.UInt32};
            mesh.vertices=coarseVertices;mesh.colors=coarseColors;mesh.uv=coarseUv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        IEnumerator Settle(Dictionary<GameObject,int> saved)
        {
            for(int i=0;i<12;i++)yield return null;yield return new WaitForSecondsRealtime(.4f);
            foreach(var t in session.GetComponentsInChildren<Transform>(true)){if(!saved.ContainsKey(t.gameObject))saved.Add(t.gameObject,t.gameObject.layer);t.gameObject.layer=Layer;}yield return null;
        }
        IEnumerator ReadFrame(RenderTexture rt,Action<Color32[]> receive)
        {
            for(int i=0;i<2;i++){RenderPipeline.SubmitRenderRequest(game.GameCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;}
            var previous=RenderTexture.active;var image=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply();var pixels=image.GetPixels32();int low=255,high=0;foreach(var p in pixels){int l=(p.r+p.g+p.b)/3;low=Math.Min(low,l);high=Math.Max(high,l);}Assert.Greater(high-low,20,"Require a visible actual frame, not an empty target");receive(pixels);}
            finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(image);}
        }
        ImageReceipt WriteImage(ViewSpec view,string role,Color32[] pixels,Mesh mesh,string otherScene,float defaultZoom)
        {
            Assert.AreEqual(Width*Height,pixels.Length);Assert.AreEqual(fingerprint,SourceFingerprint(SourceEntries()));
            var texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false);byte[] png;
            try{texture.SetPixels32(pixels);texture.Apply();png=texture.EncodeToPNG();}finally{UnityEngine.Object.Destroy(texture);}
            string file=view.name+"-"+role+".png";File.WriteAllBytes(Path.Combine(output,file),png);var camera=game.GameCamera;var w=game.World;
            var receipt=new ImageReceipt{file=file,sha256=Hash(png),rgbaSha256=PixelHash(pixels),role=role,view=view.name,recordedAtUtc=DateTime.UtcNow.ToString("o"),
                appVersion=Application.version,buildGuid=Application.buildGUID,unityVersion=Application.unityVersion,graphicsDevice=SystemInfo.graphicsDeviceName,renderPipeline=GraphicsSettings.currentRenderPipeline.GetType().FullName,
                sourceFingerprint=fingerprint,sourceInventorySha256=inventoryHash,referenceRevision=revision,matchId=game.MatchId,mapId=w.MapId,worldSha256=WorldHash(),groundMeshSha256=MeshHash(mesh),otherSceneSha256=otherScene,
                width=Width,height=Height,frame=Time.frameCount,seed=w.Seed,simulationSteps=game.Simulation.StepCount,entityCount=w.Entities.Count,groundVertices=mesh.vertexCount,groundTriangles=mesh.triangles.Length/3,
                worldTime=w.Time,unityTime=Time.time,cameraZoom=camera.orthographicSize,cameraAspect=camera.aspect,defaultZoom=defaultZoom,cameraFocus=game.Controls.CameraFocus,cameraPosition=camera.transform.position,cameraEuler=camera.transform.eulerAngles,
                gameDisabled=!game.enabled,worldViewFrozen=!game.View.enabled,fogFixtureRevealed=true,onlyGroundMeshVaries=true};WriteJson(Path.GetFileNameWithoutExtension(file)+".json",receipt);return receipt;
        }
        static int ChangedPixels(Color32[] a,Color32[] b,out float mean)
        {
            Assert.AreEqual(a.Length,b.Length);int count=0;long sum=0;for(int i=0;i<a.Length;i++){int r=Math.Abs(a[i].r-b[i].r),g=Math.Abs(a[i].g-b[i].g),blue=Math.Abs(a[i].b-b[i].b);if(Math.Max(r,Math.Max(g,blue))>Threshold)count++;sum+=r+g+blue;}mean=(float)(sum/(double)(a.Length*3));return count;
        }
        static string PixelHash(Color32[] pixels){var bytes=new byte[pixels.Length*4];for(int i=0;i<pixels.Length;i++){int j=i*4;bytes[j]=pixels[i].r;bytes[j+1]=pixels[i].g;bytes[j+2]=pixels[i].b;bytes[j+3]=pixels[i].a;}return Hash(bytes);}
        string WorldHash()=>Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(game.World.CaptureState())));
        string SceneHash(MeshFilter omittedGround)
        {
            // Mesh identity alone is insufficient: include actual bytes, model
            // transforms, renderer state and every material property as well.
            var text=new StringBuilder();var cache=new Dictionary<Mesh,string>();
            foreach(var filter in session.GetComponentsInChildren<MeshFilter>(true).OrderBy(f=>Hierarchy(f.transform),StringComparer.Ordinal))
            {
                text.Append(Hierarchy(filter.transform)).Append('|').Append(filter.gameObject.activeSelf).Append('|');var matrix=filter.transform.localToWorldMatrix;
                for(int i=0;i<16;i++)text.Append(matrix[i].ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                if(filter!=omittedGround&&filter.sharedMesh!=null){if(!cache.TryGetValue(filter.sharedMesh,out var hash)){hash=MeshHash(filter.sharedMesh);cache.Add(filter.sharedMesh,hash);}text.Append(hash);}
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null)continue;
                text.Append('|').Append(renderer.enabled).Append('|').Append(renderer.shadowCastingMode).Append('|').Append(renderer.receiveShadows);
                foreach(var material in renderer.sharedMaterials)text.Append('|').Append(MaterialHash(material));
                text.AppendLine();
            }
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        static string Hierarchy(Transform t){string path=t.name+"#"+t.GetSiblingIndex();while(t.parent!=null){t=t.parent;path=t.name+"#"+t.GetSiblingIndex()+"/"+path;}return path;}
        static string MaterialHash(Material m)
        {
            if(m==null)return "null";using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(m.GetEntityId().ToString());writer.Write(m.shader.name);writer.Write(m.renderQueue);foreach(string keyword in m.shaderKeywords.OrderBy(k=>k,StringComparer.Ordinal))writer.Write(keyword);
                for(int i=0;i<m.shader.GetPropertyCount();i++)
                {
                    string property=m.shader.GetPropertyName(i);writer.Write(property);var type=m.shader.GetPropertyType(i);writer.Write((int)type);
                    if(type==ShaderPropertyType.Texture){var texture=m.GetTexture(property);writer.Write(texture==null?"null":texture.GetEntityId().ToString());var scale=m.GetTextureScale(property);var offset=m.GetTextureOffset(property);writer.Write(scale.x);writer.Write(scale.y);writer.Write(offset.x);writer.Write(offset.y);}
                    else if(type==ShaderPropertyType.Color||type==ShaderPropertyType.Vector){var v=m.GetVector(property);writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);writer.Write(v.w);}
                    else writer.Write(m.GetFloat(property));
                }
                writer.Flush();return Hash(stream.ToArray());
            }
        }
        static string MeshHash(Mesh mesh)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(mesh.vertexCount);writer.Write((int)mesh.indexFormat);writer.Write(mesh.subMeshCount);
                foreach(var p in mesh.vertices){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}foreach(var c in mesh.colors){writer.Write(c.r);writer.Write(c.g);writer.Write(c.b);writer.Write(c.a);}
                foreach(var n in mesh.normals){writer.Write(n.x);writer.Write(n.y);writer.Write(n.z);}foreach(var t in mesh.tangents){writer.Write(t.x);writer.Write(t.y);writer.Write(t.z);writer.Write(t.w);}foreach(var uv in mesh.uv){writer.Write(uv.x);writer.Write(uv.y);}
                for(int i=0;i<mesh.subMeshCount;i++){writer.Write((int)mesh.GetTopology(i));var indices=mesh.GetIndices(i);writer.Write(indices.Length);foreach(int index in indices)writer.Write(index);}
                var b=mesh.bounds;writer.Write(b.center.x);writer.Write(b.center.y);writer.Write(b.center.z);writer.Write(b.size.x);writer.Write(b.size.y);writer.Write(b.size.z);writer.Flush();return Hash(stream.ToArray());
            }
        }
        void WriteJson(string file,object value)=>File.WriteAllText(Path.Combine(output,file),JsonUtility.ToJson(value,true),new UTF8Encoding(false));
        void RecordSource()
        {
            var entries=SourceEntries();fingerprint=SourceFingerprint(entries);revision=ReadRevision(Path.GetFullPath(Path.Combine(Application.dataPath,"..")));string expected=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT");
            if(!string.IsNullOrWhiteSpace(expected))Assert.AreEqual(expected,fingerprint,"Runner/source identity mismatch");
            var bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Inventory{revision=revision,sourceFingerprint=fingerprint,runnerExpectedFingerprint=expected,files=entries},true));inventoryHash=Hash(bytes);File.WriteAllBytes(Path.Combine(output,"source-inventory-shore078.json"),bytes);
        }
        static SourceEntry[] SourceEntries()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));var entries=new List<SourceEntry>();foreach(string directory in new[]{"Assets","Packages","ProjectSettings","Tools"})
            {
                string folder=Path.Combine(root,directory);if(!Directory.Exists(folder))continue;foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))
                {
                    string relative=file.Substring(root.Length+1).Replace('\\','/');if(relative.Split('/').Any(p=>p.StartsWith(".",StringComparison.Ordinal)||p=="__pycache__"))continue;
                    if(System.Text.RegularExpressions.Regex.IsMatch(relative,@"^Assets/InitTestScene[0-9a-f-]{36}\.unity(\.meta)?$"))continue;entries.Add(new SourceEntry{path=relative,sha256=Hash(File.ReadAllBytes(file))});
                }
            }
            return entries.OrderBy(e=>e.path,StringComparer.Ordinal).ToArray();
        }
        static string SourceFingerprint(SourceEntry[] entries){var text=new StringBuilder("{");for(int i=0;i<entries.Length;i++){if(i>0)text.Append(',');Quote(text,entries[i].path);text.Append(':');Quote(text,entries[i].sha256);}text.Append('}');return Hash(Encoding.UTF8.GetBytes(text.ToString()));}
        static void Quote(StringBuilder text,string value)
        {
            text.Append('"');foreach(char c in value){switch(c){case '"':text.Append("\\\"");break;case '\\':text.Append("\\\\");break;case '\b':text.Append("\\b");break;case '\f':text.Append("\\f");break;case '\n':text.Append("\\n");break;case '\r':text.Append("\\r");break;case '\t':text.Append("\\t");break;default:if(c<32||c>126)text.Append("\\u").Append(((int)c).ToString("x4"));else text.Append(c);break;}}text.Append('"');
        }
        static string ReadRevision(string root)
        {
            try
            {
                string git=Path.Combine(root,".git");if(File.Exists(git)){var p=File.ReadAllText(git).Trim();if(!p.StartsWith("gitdir: ",StringComparison.Ordinal))return "unavailable";git=Path.GetFullPath(Path.Combine(root,p.Substring(8)));}
                string head=File.ReadAllText(Path.Combine(git,"HEAD")).Trim();if(!head.StartsWith("ref: ",StringComparison.Ordinal))return head;string reference=head.Substring(5),common=git;string commonFile=Path.Combine(git,"commondir");if(File.Exists(commonFile))common=Path.GetFullPath(Path.Combine(git,File.ReadAllText(commonFile).Trim()));
                foreach(string directory in new[]{git,common}){string loose=Path.Combine(directory,reference);if(File.Exists(loose))return File.ReadAllText(loose).Trim();string packed=Path.Combine(directory,"packed-refs");if(File.Exists(packed))foreach(string line in File.ReadLines(packed)){var fields=line.Split(' ');if(fields.Length==2&&fields[1]==reference)return fields[0];}}
            }
            catch(IOException){}catch(UnauthorizedAccessException){}return "unavailable";
        }
        static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
