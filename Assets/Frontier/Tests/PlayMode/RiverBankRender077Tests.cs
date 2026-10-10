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
    // Actual GameCamera / WorldView / URP offscreen study. Only ground mesh data
    // differ within a pair. "Historical" means reconstructed ground formula in
    // the NEW build, not an original old-build/native capture or a paid match.
    public sealed class RiverBankRender077Tests
    {
        const int Width=1920,Height=1080,Layer=29,PixelThreshold=3;
        GameObject session;GameController game;
        [UnitySetUp] public IEnumerator CreateActualWorldView()
        {
            // Disable automatic Start/Update before the first frame. Build one
            // actual normal World once, avoiding a restart report/personal files.
            session=new GameObject("Bank077 actual GameCamera render fixture");game=session.AddComponent<GameController>();game.enabled=false;
            game.SelectedMap=MapTopology.Winding;game.MatchSeed=42;game.PlayerCivilization="coast";game.OpponentCivilization="ottoman";game.StartMatch();
            game.Controls.RestoreCamera(game.World.OpeningTownPosition(0),game.GameCamera.orthographicSize);game.Controls.enabled=false;
            yield return null;Canvas.ForceUpdateCanvases();yield return null;
        }
        [UnityTearDown] public IEnumerator DestroyActualWorldView(){if(session!=null)UnityEngine.Object.Destroy(session);yield return null;}
        string fingerprint,inventoryHash,revision,output;
        readonly List<PairReceipt> pairs=new List<PairReceipt>();
        [Serializable] sealed class SourceEntry {public string path,sha256;}
        [Serializable] sealed class Inventory
        {
            public string schema="river-bank-source-inventory-077",revision,sourceFingerprint,runnerExpectedFingerprint;
            public string sourceState="Exact working-tree Assets/Packages/ProjectSettings/Tools inventory; excludes hidden path components, __pycache__, and only Unity temporary InitTestScene GUID unity/meta fixtures. No clean-commit claim.";
            public string fingerprintAlgorithm="SHA256 compact ordinal-key JSON path-to-SHA256 map, ASCII escaped; same verification_evidence.py algorithm with explicitly listed temporary test-scene exclusions.";
            public SourceEntry[] files;
        }
        [Serializable] sealed class CaptureReceipt
        {
            public string schema="river-bank-gamecamera-render-077";
            public string purpose="Controlled actual GameCamera/WorldView/URP render study. Default AgeI Winding world is not ticked; fog is fixture-revealed and scene behaviours/shader time are frozen within a pair. No paid construction, native screenshot, commands, combat, performance or visual-quality acceptance.";
            public string baselineMeaning="Reconstructed historical .42m/3m ground height and original RGBA formula on this new-build ground mesh only; all other new-build meshes/model placements/materials remain identical. Not an old-build landscape capture.";
            public string file,sha256,rgbaSha256,recordedAtUtc,role,view,appVersion,buildGuid,unityVersion,graphicsDevice,renderPipeline,sourceFingerprint,sourceInventorySha256,referenceRevision,matchId,worldSha256,groundMeshSha256,mapId;
            public int width,height,frame,seed,simulationSteps,entityCount;
            public float worldTime,unityTime,cameraOrthographicSize,cameraAspect,defaultCameraZoom;
            public Vector2 nativeCameraFocus;
            public Vector3 cameraPosition,cameraEuler;
            public bool gameControllerDisabled,worldViewFrozen,onlyGroundMeshVaries;
        }
        [Serializable] sealed class PairReceipt
        {
            public string view,controlFile,historicalFile,currentFile,controlPngSha256,historicalPngSha256,currentPngSha256,worldSha256,currentGroundMeshSha256,historicalGroundMeshSha256;
            public int changedPixelsAboveThreshold,repeatControlChangedPixels,pixelDeltaThreshold;
            public float cameraZoom,meanAbsoluteRgbDelta,repeatControlMeanAbsoluteRgbDelta;
            public Vector2 focus;
            public bool expectedVisibleDifference,meshRestoredExactly,worldUnchanged;
        }
        [Serializable] sealed class RunReceipt
        {
            public string schema="river-bank-render-study-077",status,recordedAtUtc,appVersion,sourceFingerprint,sourceInventorySha256,referenceRevision;
            public string scope="Five controlled camera pairs plus repeat-current controls; historical ground reconstructed inside the new build. Not native/paid gameplay, old-build evidence, FPS or visual approval.";
            public bool completed,meshRestoredExactly;
            public float defaultZoom;
            public PairReceipt[] pairs;
        }
        struct ViewSpec
        {
            public string name;public Vector2 focus;public float zoom;public bool expectDifference;
            public ViewSpec(string name,Vector2 focus,float zoom,bool difference){this.name=name;this.focus=focus;this.zoom=zoom;expectDifference=difference;}
        }

        [UnityTest] public IEnumerator OrdinaryCameraPairsCompareReconstructedHistoricalGroundWithNewBankProfile()
        {
            // Re-run the 077 bank-profile/palette comparison in the 078 runtime;
            // this is not historical077 evidence or the 078 grid-density pair.
            Assert.AreEqual("0.21.0",Application.version,"Integrate the 078 version identity before this retained study");
            Assert.IsTrue(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset,"Study requires the project's actual URP asset");
            output=Environment.GetEnvironmentVariable("FRONTIER_BANK_STUDY_DIR");
            if(string.IsNullOrWhiteSpace(output))output=Path.Combine(Application.temporaryCachePath,"FrontierRiverBankRender077-"+Guid.NewGuid().ToString("N"));
            output=Path.GetFullPath(output);Directory.CreateDirectory(output);
            Assert.IsFalse(Directory.EnumerateFileSystemEntries(output).Any(),"Use a fresh empty study directory; preserve previous captures");
            RecordSource(output);pairs.Clear();
            var camera=game.GameCamera;float defaultZoom=camera.orthographicSize;
            Assert.AreEqual(26,defaultZoom,.0001f,"Read the actual ordinary default zoom, not the old full-map exporter size56");
            var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
            float oldSize=camera.orthographicSize,oldAspect=camera.aspect,oldScale=Time.timeScale;
            int oldMask=camera.cullingMask;bool oldCameraEnabled=camera.enabled,oldControls=game.Controls.enabled,oldAudioPause=AudioListener.pause;
            RenderTexture rt=null;Light sun=null;Mesh ground=null;Vector3[] currentVertices=null,currentNormals=null;Vector4[] currentTangents=null;Color[] currentColors=null;Bounds currentBounds=default;
            string currentMeshHash=null;bool completed=false,restored=false;
            var savedLayers=new Dictionary<GameObject,int>();
            try
            {
                Assert.AreEqual(MapTopology.Winding,game.World.MapId);Assert.IsFalse(game.Autoplay);
                var layout=game.World.Layout;var channel=layout.Channels[0];float maximum=channel.Centers.Max(p=>p.x);
                var bend=channel.Centers.First(p=>p.x==maximum);
                Assert.That(bend.x,Is.InRange(78,80));Assert.That(bend.y,Is.InRange(64,76));
                game.Controls.RestoreCamera(bend,defaultZoom);game.Controls.enabled=false;
                camera.enabled=false;camera.aspect=Width/(float)Height;camera.cullingMask=1<<Layer;
                AudioListener.pause=true;Time.timeScale=0;
                sun=new GameObject("Bank077 controlled directional sun").AddComponent<Light>();sun.transform.SetParent(session.transform,false);
                sun.type=LightType.Directional;sun.intensity=1.55f;sun.color=new Color(1,.96f,.87f);sun.shadows=LightShadows.Soft;sun.cullingMask=1<<Layer;sun.transform.rotation=Quaternion.Euler(48,-28,0);
                rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);Assert.IsTrue(rt.Create());
                yield return Settle(savedLayers);
                var matches=session.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Continuous sculpted land and river bed").ToArray();
                Assert.AreEqual(1,matches.Length,"Use the real WorldView ground, not a second terrain fixture");ground=matches[0].sharedMesh;
                Assert.AreEqual(257*257,ground.vertexCount);Assert.AreEqual(1,ground.subMeshCount);
                currentVertices=ground.vertices;currentColors=ground.colors;currentNormals=ground.normals;currentTangents=ground.tangents;currentBounds=ground.bounds;
                Assert.AreEqual(currentVertices.Length,currentColors.Length);currentMeshHash=MeshHash(ground);
                var historicalVertices=(Vector3[])currentVertices.Clone();var historicalColors=new Color[currentColors.Length];int changedGroundVertices=0;
                for(int i=0;i<historicalVertices.Length;i++)
                {
                    var point=new Vector2(historicalVertices[i].x,historicalVertices[i].z);
                    Assert.AreEqual(layout.BankHeight(point),currentVertices[i].y,.000001f);
                    var expected=layout.BankLayers(point);Assert.AreEqual(expected,currentColors[i]);
                    historicalVertices[i].y=HistoricalHeight(layout,point);historicalColors[i]=HistoricalLayers(layout,point);
                    if(Mathf.Abs(historicalColors[i].g-currentColors[i].g)>.05f)changedGroundVertices++;
                }
                Assert.Greater(changedGroundVertices,50,"Visible study must not compare the same dormant formula twice");
                var site=layout.DockSites[0][0];var harborLook=site+game.World.DockWaterDirection(site)*3;
                var specs=new[]{new ViewSpec("01-bend-default26",bend,defaultZoom,true),new ViewSpec("02-bend-close16",bend,16,true),
                    new ViewSpec("03-winding-overview-default26",new Vector2(64,64),defaultZoom,true),
                    new ViewSpec("04-protected-harbor-pad-default26",harborLook,defaultZoom,false),
                    new ViewSpec("05-protected-bridge-default26",layout.Bridges[0].Center,defaultZoom,false)};
                foreach(var view in specs)
                {
                    // Uses the normal input controller's focus/zoom and 50deg
                    // camera transform, never a synthetic full-map size56 view.
                    game.Controls.RestoreCamera(view.focus,view.zoom);game.Controls.enabled=false;
                    Assert.That(camera.orthographicSize,Is.InRange(12,47));yield return Settle(savedLayers);
                    var behaviours=session.GetComponentsInChildren<MonoBehaviour>(true);
                    var enabled=behaviours.Select(b=>b.enabled).ToArray();
                    for(int i=0;i<behaviours.Length;i++)if(!(behaviours[i] is UniversalAdditionalCameraData))behaviours[i].enabled=false;
                    try
                    {
                        Assert.AreEqual(currentMeshHash,MeshHash(ground));string worldHash=WorldHash();
                        Color32[] control=null,historical=null,current=null;
                        yield return ReadFrame(rt,p=>control=p);
                        var controlReceipt=WriteImage(view,"control-current",control,ground,defaultZoom);
                        ground.vertices=historicalVertices;ground.colors=historicalColors;ground.RecalculateNormals();ground.RecalculateTangents();ground.RecalculateBounds();
                        string historicalHash=MeshHash(ground);Assert.AreNotEqual(currentMeshHash,historicalHash);
                        yield return ReadFrame(rt,p=>historical=p);
                        var historicalReceipt=WriteImage(view,"baseline-reconstructed-historical-ground",historical,ground,defaultZoom);
                        RestoreMesh(ground,currentVertices,currentColors,currentNormals,currentTangents,currentBounds);
                        Assert.AreEqual(currentMeshHash,MeshHash(ground));
                        yield return ReadFrame(rt,p=>current=p);
                        var currentReceipt=WriteImage(view,"current-bank077",current,ground,defaultZoom);
                        float delta,repeatDelta;int changed=ChangedPixels(historical,current,out delta),repeat=ChangedPixels(control,current,out repeatDelta);
                        Assert.LessOrEqual(repeat,Mathf.Max(64,Width*Height/1000),"Repeated current frame drift confounds a ground-only comparison");
                        if(view.expectDifference)Assert.Greater(changed,repeat*4+200,"Expected real bank pixel signal must exceed repeat-render noise");
                        Assert.AreEqual(worldHash,WorldHash(),"No World/commands/time change is permitted within a ground-only pair");
                        Assert.AreEqual(fingerprint,SourceFingerprint(SourceEntries()),"Source changed during study");
                        var pair=new PairReceipt{view=view.name,focus=view.focus,cameraZoom=view.zoom,expectedVisibleDifference=view.expectDifference,
                            controlFile=controlReceipt.file,historicalFile=historicalReceipt.file,currentFile=currentReceipt.file,
                            controlPngSha256=controlReceipt.sha256,historicalPngSha256=historicalReceipt.sha256,currentPngSha256=currentReceipt.sha256,
                            changedPixelsAboveThreshold=changed,repeatControlChangedPixels=repeat,pixelDeltaThreshold=PixelThreshold,
                            meanAbsoluteRgbDelta=delta,repeatControlMeanAbsoluteRgbDelta=repeatDelta,worldSha256=worldHash,
                            currentGroundMeshSha256=currentMeshHash,historicalGroundMeshSha256=historicalHash,meshRestoredExactly=true,worldUnchanged=true};
                        pairs.Add(pair);File.WriteAllText(Path.Combine(output,view.name+"-pair.json"),JsonUtility.ToJson(pair,true),new UTF8Encoding(false));
                    }
                    finally
                    {
                        RestoreMesh(ground,currentVertices,currentColors,currentNormals,currentTangents,currentBounds);
                        for(int i=0;i<behaviours.Length;i++)if(behaviours[i]!=null)behaviours[i].enabled=enabled[i];
                    }
                }
                Assert.AreEqual(5,pairs.Count);completed=true;
            }
            finally
            {
                if(ground!=null&&currentVertices!=null)
                {
                    RestoreMesh(ground,currentVertices,currentColors,currentNormals,currentTangents,currentBounds);
                    restored=MeshHash(ground)==currentMeshHash;
                }
                foreach(var layer in savedLayers)if(layer.Key!=null)layer.Key.layer=layer.Value;
                camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.orthographicSize=oldSize;camera.aspect=oldAspect;camera.cullingMask=oldMask;camera.enabled=oldCameraEnabled;
                game.Controls.enabled=oldControls;Time.timeScale=oldScale;AudioListener.pause=oldAudioPause;
                if(rt!=null){rt.Release();UnityEngine.Object.Destroy(rt);}if(sun!=null)UnityEngine.Object.Destroy(sun.gameObject);
                File.WriteAllText(Path.Combine(output,"study-run077.json"),JsonUtility.ToJson(new RunReceipt{status=completed&&restored?"captured-ground-pairs-not-visual-approval":"incomplete-preserved",completed=completed,meshRestoredExactly=restored,
                    recordedAtUtc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,sourceFingerprint=fingerprint,sourceInventorySha256=inventoryHash,referenceRevision=revision,defaultZoom=defaultZoom,pairs=pairs.ToArray()},true),new UTF8Encoding(false));
            }
            Assert.IsTrue(restored,"New ground mesh must be exactly restored even after historical comparison");
        }

        IEnumerator Settle(Dictionary<GameObject,int> saved)
        {
            game.World.Fog.Reveal(0,new Vector2(64,64),180);
            for(int i=0;i<12;i++)yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            foreach(var t in session.GetComponentsInChildren<Transform>(true))
            {if(!saved.ContainsKey(t.gameObject))saved.Add(t.gameObject,t.gameObject.layer);t.gameObject.layer=Layer;}
            yield return null;
        }
        IEnumerator ReadFrame(RenderTexture rt,Action<Color32[]> receive)
        {
            for(int i=0;i<2;i++){RenderPipeline.SubmitRenderRequest(game.GameCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;}
            var old=RenderTexture.active;var image=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply();var pixels=image.GetPixels32();
                int low=255,high=0;foreach(var p in pixels){int l=(p.r+p.g+p.b)/3;low=Math.Min(low,l);high=Math.Max(high,l);}
                Assert.Greater(high-low,20,"Actual URP frame must contain visible terrain rather than a uniform clear target");receive(pixels);
            }
            finally{RenderTexture.active=old;UnityEngine.Object.Destroy(image);}
        }
        CaptureReceipt WriteImage(ViewSpec view,string role,Color32[] pixels,Mesh ground,float defaultZoom)
        {
            Assert.AreEqual(Width*Height,pixels.Length);Assert.AreEqual(fingerprint,SourceFingerprint(SourceEntries()));
            var texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false);byte[] png;
            try{texture.SetPixels32(pixels);texture.Apply();png=texture.EncodeToPNG();}finally{UnityEngine.Object.Destroy(texture);}
            string file=view.name+"-"+role+".png";File.WriteAllBytes(Path.Combine(output,file),png);
            var camera=game.GameCamera;var w=game.World;
            var receipt=new CaptureReceipt{file=file,sha256=Hash(png),rgbaSha256=PixelHash(pixels),recordedAtUtc=DateTime.UtcNow.ToString("o"),role=role,view=view.name,
                appVersion=Application.version,buildGuid=Application.buildGUID,unityVersion=Application.unityVersion,graphicsDevice=SystemInfo.graphicsDeviceName,
                renderPipeline=GraphicsSettings.currentRenderPipeline.GetType().FullName,sourceFingerprint=fingerprint,sourceInventorySha256=inventoryHash,referenceRevision=revision,
                matchId=game.MatchId,worldSha256=WorldHash(),groundMeshSha256=MeshHash(ground),mapId=w.MapId,seed=w.Seed,width=Width,height=Height,frame=Time.frameCount,
                simulationSteps=game.Simulation.StepCount,entityCount=w.Entities.Count,worldTime=w.Time,unityTime=Time.time,defaultCameraZoom=defaultZoom,
                nativeCameraFocus=game.Controls.CameraFocus,cameraPosition=camera.transform.position,cameraEuler=camera.transform.eulerAngles,cameraOrthographicSize=camera.orthographicSize,cameraAspect=camera.aspect,
                gameControllerDisabled=!game.enabled,worldViewFrozen=!game.View.enabled,onlyGroundMeshVaries=true};
            File.WriteAllText(Path.Combine(output,Path.GetFileNameWithoutExtension(file)+".json"),JsonUtility.ToJson(receipt,true),new UTF8Encoding(false));return receipt;
        }
        static void RestoreMesh(Mesh mesh,Vector3[] vertices,Color[] colors,Vector3[] normals,Vector4[] tangents,Bounds bounds)
        {mesh.vertices=vertices;mesh.colors=colors;mesh.normals=normals;mesh.tangents=tangents;mesh.bounds=bounds;}
        static float HistoricalHeight(MapLayout layout,Vector2 point)
        {
            float d=layout.SignedBankDistance(point);
            if(d<=0)return Mathf.Lerp(layout.WaterHeight-.02f,-.9f,Mathf.SmoothStep(0,1,-d/4));
            return (layout.WaterHeight-.02f)*(1-Mathf.SmoothStep(0,1,d/3));
        }
        static Color HistoricalLayers(MapLayout layout,Vector2 point)
        {
            float d=layout.SignedBankDistance(point),noise=Mathf.PerlinNoise(point.x*.13f+3,point.y*.11f+8)-.5f;
            float damp=1-Mathf.SmoothStep(0,1,(d-.25f-noise*.25f)/2.6f);
            float shoulder=1-Mathf.SmoothStep(0,1,(d-1.5f-noise*.65f)/4);float sand=(1-damp)*shoulder;
            return new Color(damp,sand,1-damp-sand,1);
        }
        static int ChangedPixels(Color32[] a,Color32[] b,out float mean)
        {
            Assert.AreEqual(a.Length,b.Length);int count=0;long sum=0;
            for(int i=0;i<a.Length;i++)
            {
                int r=Math.Abs(a[i].r-b[i].r),g=Math.Abs(a[i].g-b[i].g),blue=Math.Abs(a[i].b-b[i].b);
                if(Math.Max(r,Math.Max(g,blue))>PixelThreshold)count++;sum+=r+g+blue;
            }
            mean=(float)(sum/(double)(a.Length*3));return count;
        }
        static string PixelHash(Color32[] pixels)
        {
            var bytes=new byte[pixels.Length*4];for(int i=0;i<pixels.Length;i++){int j=i*4;bytes[j]=pixels[i].r;bytes[j+1]=pixels[i].g;bytes[j+2]=pixels[i].b;bytes[j+3]=pixels[i].a;}return Hash(bytes);
        }
        string WorldHash()=>Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(game.World.CaptureState())));
        static string MeshHash(Mesh mesh)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(mesh.vertexCount);
                foreach(var p in mesh.vertices){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
                foreach(var c in mesh.colors){writer.Write(c.r);writer.Write(c.g);writer.Write(c.b);writer.Write(c.a);}
                foreach(var n in mesh.normals){writer.Write(n.x);writer.Write(n.y);writer.Write(n.z);}
                foreach(var t in mesh.tangents){writer.Write(t.x);writer.Write(t.y);writer.Write(t.z);writer.Write(t.w);}
                foreach(var uv in mesh.uv){writer.Write(uv.x);writer.Write(uv.y);}foreach(int index in mesh.triangles)writer.Write(index);
                var b=mesh.bounds;writer.Write(b.center.x);writer.Write(b.center.y);writer.Write(b.center.z);writer.Write(b.size.x);writer.Write(b.size.y);writer.Write(b.size.z);
                writer.Flush();return Hash(stream.ToArray());
            }
        }
        void RecordSource(string target)
        {
            var entries=SourceEntries();fingerprint=SourceFingerprint(entries);revision=ReadRevision(Path.GetFullPath(Path.Combine(Application.dataPath,"..")));
            string expected=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT");
            if(!string.IsNullOrWhiteSpace(expected))Assert.AreEqual(expected,fingerprint,"Runner/source identity mismatch before render study");
            var bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Inventory{revision=revision,sourceFingerprint=fingerprint,runnerExpectedFingerprint=expected,files=entries},true));
            inventoryHash=Hash(bytes);File.WriteAllBytes(Path.Combine(target,"source-inventory-bank077.json"),bytes);
        }
        static SourceEntry[] SourceEntries()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));var entries=new List<SourceEntry>();
            foreach(string directory in new[]{"Assets","Packages","ProjectSettings","Tools"})
            {
                var folder=Path.Combine(root,directory);if(!Directory.Exists(folder))continue;
                foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))
                {
                    string relative=file.Substring(root.Length+1).Replace('\\','/');if(relative.Split('/').Any(p=>p.StartsWith(".",StringComparison.Ordinal)||p=="__pycache__"))continue;
                    if(System.Text.RegularExpressions.Regex.IsMatch(relative,@"^Assets/InitTestScene[0-9a-f-]{36}\.unity(\.meta)?$"))continue;
                    entries.Add(new SourceEntry{path=relative,sha256=Hash(File.ReadAllBytes(file))});
                }
            }
            return entries.OrderBy(e=>e.path,StringComparer.Ordinal).ToArray();
        }
        static string SourceFingerprint(SourceEntry[] entries)
        {
            var text=new StringBuilder("{");for(int i=0;i<entries.Length;i++){if(i>0)text.Append(',');Quote(text,entries[i].path);text.Append(':');Quote(text,entries[i].sha256);}text.Append('}');return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        static void Quote(StringBuilder text,string value)
        {
            text.Append('"');foreach(char c in value)
            {
                switch(c)
                {
                    case '"':text.Append("\\\"");break;case '\\':text.Append("\\\\");break;case '\b':text.Append("\\b");break;case '\f':text.Append("\\f");break;
                    case '\n':text.Append("\\n");break;case '\r':text.Append("\\r");break;case '\t':text.Append("\\t");break;
                    default:if(c<32||c>126)text.Append("\\u").Append(((int)c).ToString("x4"));else text.Append(c);break;
                }
            }text.Append('"');
        }
        static string ReadRevision(string root)
        {
            try
            {
                string git=Path.Combine(root,".git");if(File.Exists(git)){var pointer=File.ReadAllText(git).Trim();if(!pointer.StartsWith("gitdir: ",StringComparison.Ordinal))return "unavailable";git=Path.GetFullPath(Path.Combine(root,pointer.Substring(8)));}
                string head=File.ReadAllText(Path.Combine(git,"HEAD")).Trim();if(!head.StartsWith("ref: ",StringComparison.Ordinal))return head;
                string reference=head.Substring(5),common=git;var commonFile=Path.Combine(git,"commondir");if(File.Exists(commonFile))common=Path.GetFullPath(Path.Combine(git,File.ReadAllText(commonFile).Trim()));
                foreach(string directory in new[]{git,common})
                {
                    var loose=Path.Combine(directory,reference);if(File.Exists(loose))return File.ReadAllText(loose).Trim();var packed=Path.Combine(directory,"packed-refs");
                    if(File.Exists(packed))foreach(var line in File.ReadLines(packed)){var fields=line.Split(' ');if(fields.Length==2&&fields[1]==reference)return fields[0];}
                }
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
            return "unavailable";
        }
        static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
