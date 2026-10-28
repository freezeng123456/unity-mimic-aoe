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
    // Diagnostic sheets render actual runtime geometry with URP. Entities are
    // constructed directly and poses are fixed samples; no simulation is run.
    public sealed class DetailedModelStudy060Tests
    {
        const int Layer=29,Width=3072,Height=2048;
        GameObject stage;Camera camera;Font font;RuleCatalog rules;int nextId;
        string fingerprint,inventoryHash,revision;
        readonly List<ModelReceipt> records=new List<ModelReceipt>();
        readonly List<Transform> models=new List<Transform>();
        readonly List<MeshRenderer> labels=new List<MeshRenderer>();

        [Serializable] sealed class SourceEntry {public string path,sha256;}
        [Serializable] sealed class Inventory
        {
            public string schema="detailed-model-study-source-060",revision,sourceFingerprint;
            public string fingerprintAlgorithm="Tools/verification_evidence.py: SHA256 of UTF-8 sorted compact JSON path-to-file-SHA256 map; ensure_ascii=true";
            public string sourceState="Working tree including uncommitted files, excluding temporary Unity InitTestScene GUID fixtures; revision is reference HEAD only, not a clean-commit claim";
            public SourceEntry[] files;
        }
        [Serializable] sealed class ModelReceipt
        {
            public string entityId,kind,civilization,definitionTags,poseState,visibleGeometrySha256;
            public int fixtureEntity,owner,visibleRenderers,visibleVertices,visibleTriangles,materialSlots;
            public Vector3 position,eulerAngles,scale,boundsCenter,boundsSize;
            public float hp,construction;
            public AnimationPose pose;
        }
        [Serializable] sealed class CaptureReceipt
        {
            public string schema="detailed-model-study-capture-060";
            public string purpose="Isolated original-model URP rendering, direct fixture entities and fixed pose samples. Not an ordinary game screenshot, simulation, paid production, full animation, input, frame-rate or visual-quality acceptance.";
            public string file,sha256,recordedAtUtc,appVersion,unityVersion,graphicsDevice,renderPipeline,study,sourceFingerprint,sourceInventorySha256,referenceRevision;
            public int width,height,frame,modelChangedPixelSamples;
            public Vector3 cameraPosition,cameraEuler,cameraTarget;
            public float cameraOrthographicSize,cameraAspect;
            public ModelReceipt[] models;
        }
        Vector3 target;

        [TearDown] public void Cleanup()
        {
            if(stage!=null)UnityEngine.Object.DestroyImmediate(stage);
            stage=null;models.Clear();records.Clear();labels.Clear();
        }

        [UnityTest] public IEnumerator RenderDetailedUnitCatalogPosesAndResourcesWithSourceReceipts()
        {
            string output=Environment.GetEnvironmentVariable("FRONTIER_MODEL_STUDY_DIR");
            if(string.IsNullOrWhiteSpace(output))output=Path.Combine(Application.temporaryCachePath,"FrontierDetailedModels060");
            Directory.CreateDirectory(output);rules=Catalog.Load();font=Resources.Load<Font>("Fonts/NotoSans-Regular");Assert.IsNotNull(font);
            RecordSource(output);stage=new GameObject("Detailed models 060 isolated URP study");
            camera=new GameObject("Detailed model study camera").AddComponent<Camera>();camera.transform.SetParent(stage.transform,false);
            camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.125f,.155f,.135f);
            camera.cullingMask=1<<Layer;camera.nearClipPlane=.1f;camera.farClipPlane=150;camera.aspect=Width/(float)Height;
            var light=new GameObject("Detailed model study sun").AddComponent<Light>();light.transform.SetParent(stage.transform,false);light.type=LightType.Directional;
            light.intensity=1.6f;light.cullingMask=1<<Layer;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(48,-28,0);
            var rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);rt.Create();
            try
            {
                Configure(4.7f,25,new Vector3(0,.95f,0));var sheet=Sheet("Infantry faces, tailoring and actual hand grips");
                var close=new[]{"musketeer","coureur","janissary","abus"};
                for(int row=0;row<2;row++)for(int column=0;column<4;column++)
                {
                    var point=new Vector3((column-1.5f)*3.1f,0,row==0?3.7f:-3.7f);
                    var pose=Pose(close[column],row==0?UnitPoseState.Idle:UnitPoseState.PreparingAttack);
                    Add(sheet.transform,close[column],point,pose,0,25);
                    Label(sheet.transform,close[column]+"\n"+ShortPose(pose.state),point+new Vector3(0,-.10f,-.85f),.070f);
                }
                yield return Capture(rt,output,"01-infantry-close-study","Four infantry types, idle and preparation samples; actual head, tailoring and weapon grips at close scale");
                yield return RemoveSheet(sheet);

                // Enumerate the catalog, so a new unit cannot silently disappear from this review.
                var units=rules.entities.Where(e=>e.kind==EntityKind.Unit).OrderBy(e=>e.id,StringComparer.Ordinal).ToArray();
                const int columns=6;int rows=(units.Length+columns-1)/columns;
                Configure(Mathf.Max(10.8f,rows*2.6f),38,new Vector3(0,1,0));sheet=Sheet("Every unit definition in the current catalog");
                for(int i=0;i<units.Length;i++)
                {
                    var point=new Vector3((i%columns-(columns-1)*.5f)*5.0f,0,((rows-1)*.5f-i/columns)*6.0f);
                    var pose=Pose(units[i].id,(units[i].tags&Tags.Artillery)!=0?UnitPoseState.Fire:UnitPoseState.Idle);
                    Add(sheet.transform,units[i].id,point,pose,i%2,25);
                    Label(sheet.transform,units[i].id+"\n"+(i%2==0?"blue":"red"),point+new Vector3(0,-.10f,-1.15f),.072f);
                }
                Assert.AreEqual(units.Length,records.Count);
                yield return Capture(rt,output,"02-complete-unit-catalog","Every Unit definition in Rules; owner colors alternate. Dedicated civilization units are displayed diagnostically, not claimed trainable by all civilizations.");
                yield return RemoveSheet(sheet);

                Configure(7.8f,32,new Vector3(0,1,0));sheet=Sheet("Mounted and artillery pose inspection");
                string[] ids={"cuirassier","cuirassier","spahi","spahi","hussar","dragoon","carabineer","outrider","cannon","cannon","culverin","mortar"};
                UnitPoseState[] states={UnitPoseState.Move,UnitPoseState.Fire,UnitPoseState.Move,UnitPoseState.Fire,UnitPoseState.Fire,UnitPoseState.PreparingAttack,UnitPoseState.Reloading,UnitPoseState.Move,UnitPoseState.Move,UnitPoseState.Fire,UnitPoseState.Fire,UnitPoseState.Fire};
                for(int i=0;i<ids.Length;i++)
                {
                    var point=new Vector3((i%4-1.5f)*5.1f,0,(1-i/4)*7.5f);var pose=Pose(ids[i],states[i]);
                    Add(sheet.transform,ids[i],point,pose,0,(i==9||i==11)?0:35);
                    Label(sheet.transform,ids[i]+"\n"+ShortPose(states[i]),point+new Vector3(0,-.10f,-1.30f),.085f);
                }
                yield return Capture(rt,output,"03-mounted-and-artillery-poses","Fixed walk, aim, recoil and reload poses expose saddle placement, reins, hand grips and open cannon muzzles; not a complete animation validation");
                yield return RemoveSheet(sheet);

                var resources=rules.entities.Where(e=>e.kind==EntityKind.Resource).OrderBy(e=>e.id,StringComparer.Ordinal).ToArray();
                int resourceRows=(resources.Length+3)/4;Configure(Mathf.Max(9.5f,resourceRows*3.15f),43,new Vector3(0,1.1f,0));sheet=Sheet("Environmental resource geometry");
                for(int i=0;i<resources.Length;i++)
                {
                    var point=new Vector3((i%4-1.5f)*5.8f,0,((resourceRows-1)*.5f-i/4)*9.0f);
                    Add(sheet.transform,resources[i].id,point,new AnimationPose{state=UnitPoseState.Idle},-1,-155);
                    Label(sheet.transform,resources[i].id.Replace("_","\n"),point+new Vector3(0,-.10f,-2.70f),.090f);
                }
                Assert.AreEqual(resources.Length,records.Count);
                yield return Capture(rt,output,"04-environment-resource-study","Every Resource definition at fixed scale and common light: vegetation, animals, deposits, crates, caches and trade-site model; no harvesting or simulation is performed");
                yield return RemoveSheet(sheet);
            }
            finally{rt.Release();UnityEngine.Object.Destroy(rt);}
            Assert.AreEqual(fingerprint,SourceFingerprint(SourceEntries()),"Source changed during model capture; regenerate receipts and all sheets from one working-tree state");
            Debug.Log("FRONTIER_MODEL_STUDY_OK "+output+" source="+fingerprint);
        }

        AnimationPose Pose(string id,UnitPoseState state)
        {
            var definition=rules.Entity(id);bool gun=(definition.tags&Tags.Artillery)!=0;
            bool melee=id=="pike"||id=="halberdier"||id=="escort"||id=="shield_guard"||id=="hussar"||id=="cuirassier"||id=="spahi"||id=="outrider";
            return new AnimationPose{state=state,moving=state==UnitPoseState.Move,melee=melee,bow=id=="longbow",polearm=id=="pike"||id=="halberdier"||id=="escort"||id=="shield_guard",deployed=gun&&state!=UnitPoseState.Move,
                reloading=state==UnitPoseState.Reloading,sweepingMelee=id=="cuirassier",locomotionPhase=.8f,actionPhase=state==UnitPoseState.Fire?.55f:.75f,loadedCargo=id=="caravan"};
        }
        void Add(Transform parent,string id,Vector3 point,AnimationPose pose,int owner,float yaw)
        {
            var definition=rules.Entity(id);Assert.IsNotNull(definition,id);
            var entity=new EntityState{id=++nextId,def=definition,owner=owner,hp=definition.hp,construction=1,fireAt=-1,status="Diagnostic static pose"};
            var model=ProceduralArt.BuildEntity(entity,parent);model.localPosition=point;model.localRotation=Quaternion.Euler(0,180+yaw,0);
            if(definition.kind==EntityKind.Unit){var rig=model.GetComponent<UnitRig>();Assert.IsNotNull(rig,id);UnitAnimation.Apply(rig,pose);}
            models.Add(model);
            records.Add(new ModelReceipt{entityId=id,fixtureEntity=entity.id,kind=definition.kind.ToString(),civilization=definition.civilization??"",definitionTags=definition.tags.ToString(),owner=owner,hp=entity.hp,construction=1,
                position=point,eulerAngles=model.localEulerAngles,scale=model.localScale,poseState=pose.state.ToString(),pose=pose});
        }
        GameObject Sheet(string name)
        {
            records.Clear();models.Clear();labels.Clear();var sheet=new GameObject(name);sheet.transform.SetParent(stage.transform,false);
            ProceduralArt.Part(sheet.transform,"Diagnostic ground",PrimitiveType.Cube,new Vector3(0,-.14f,0),new Vector3(100,.20f,100),new Color(.255f,.29f,.205f));return sheet;
        }
        IEnumerator RemoveSheet(GameObject sheet){UnityEngine.Object.Destroy(sheet);yield return null;yield return null;records.Clear();models.Clear();labels.Clear();}
        void Configure(float size,float pitch,Vector3 lookAt)
        {target=lookAt;camera.orthographicSize=size;camera.transform.rotation=Quaternion.Euler(pitch,0,0);camera.transform.position=lookAt+camera.transform.rotation*Vector3.back*60;}
        void Label(Transform parent,string title,Vector3 point,float size)
        {
            var obj=new GameObject("Study label "+title);obj.transform.SetParent(parent,false);obj.transform.localPosition=point;obj.transform.rotation=camera.transform.rotation;
            var text=obj.AddComponent<TextMesh>();text.font=font;text.fontSize=40;text.characterSize=size;text.text=title;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.94f,.91f,.78f);
            var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=font.material;labels.Add(renderer);
        }
        static string ShortPose(UnitPoseState state)
        {return state==UnitPoseState.PreparingAttack?"Aim":state==UnitPoseState.Reloading?"Reload":state==UnitPoseState.Move?"Stride":state==UnitPoseState.Fire?"Fire pose":state.ToString();}
        void ValidateLabels()
        {
            var rectangles=new List<Rect>();
            foreach(var label in labels)
            {
                var b=label.bounds;var low=Vector2.one*float.PositiveInfinity;var high=Vector2.one*float.NegativeInfinity;
                for(int corner=0;corner<8;corner++)
                {
                    var p=b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    var viewport=camera.WorldToViewportPoint(p);low=Vector2.Min(low,viewport);high=Vector2.Max(high,viewport);
                }
                Assert.GreaterOrEqual(low.x,.005f,label.name+" left label clipping");Assert.LessOrEqual(high.x,.995f,label.name+" right label clipping");
                Assert.GreaterOrEqual(low.y,.005f,label.name+" lower label clipping");Assert.LessOrEqual(high.y,.995f,label.name+" upper label clipping");
                var rectangle=Rect.MinMaxRect(low.x,low.y,high.x,high.y);
                foreach(var existing in rectangles)Assert.IsFalse(rectangle.Overlaps(existing),label.name+" must not overlap another label");
                rectangles.Add(rectangle);
            }
        }
        IEnumerator Capture(RenderTexture rt,string output,string name,string description)
        {
            foreach(var t in stage.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
            yield return null;yield return null;ValidateLabels();
            var foreground=models.SelectMany(m=>m.GetComponentsInChildren<MeshRenderer>()).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            Assert.IsNotEmpty(foreground,"The sheet must have actual enabled model surfaces");
            var previous=RenderTexture.active;var pixels=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            try
            {
                foreach(var renderer in foreground)renderer.enabled=false;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);pixels.Apply();var control=pixels.GetPixels32();
                foreach(var renderer in foreground)renderer.enabled=true;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);pixels.Apply();var actual=pixels.GetPixels32();int changed=0;
                for(int i=0;i<actual.Length;i+=17)if(!actual[i].Equals(control[i]))changed++;
                Assert.Greater(changed,1000,"Geometry must change real pixels relative to a ground/label-only control render");
                for(int i=0;i<models.Count;i++)
                {
                    RecordGeometry(models[i],records[i]);
                    var center=records[i].boundsCenter;var half=records[i].boundsSize*.5f;
                    for(int corner=0;corner<8;corner++)
                    {
                        var world=center+new Vector3((corner&1)==0?-half.x:half.x,(corner&2)==0?-half.y:half.y,(corner&4)==0?-half.z:half.z);
                        var point=camera.WorldToViewportPoint(world);
                        Assert.Greater(point.z,camera.nearClipPlane,records[i].entityId+" must be in front of the camera");
                        Assert.That(point.x,Is.InRange(.005f,.995f),records[i].entityId+" horizontal bounds must be inside the diagnostic frame");
                        Assert.That(point.y,Is.InRange(.005f,.995f),records[i].entityId+" vertical bounds must be inside the diagnostic frame");
                    }
                }
                Assert.AreEqual(fingerprint,SourceFingerprint(SourceEntries()),"Working tree changed before capture; do not bind this image to an earlier source state");
                var png=pixels.EncodeToPNG();File.WriteAllBytes(Path.Combine(output,name+".png"),png);
                var receipt=new CaptureReceipt{file=name+".png",sha256=Hash(png),recordedAtUtc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,unityVersion=Application.unityVersion,
                    graphicsDevice=SystemInfo.graphicsDeviceName,renderPipeline=GraphicsSettings.currentRenderPipeline?.GetType().FullName??"none",study=description,sourceFingerprint=fingerprint,sourceInventorySha256=inventoryHash,referenceRevision=revision,
                    width=Width,height=Height,frame=Time.frameCount,modelChangedPixelSamples=changed,cameraPosition=camera.transform.position,cameraEuler=camera.transform.eulerAngles,cameraTarget=target,cameraOrthographicSize=camera.orthographicSize,cameraAspect=camera.aspect,models=records.ToArray()};
                File.WriteAllText(Path.Combine(output,name+".json"),JsonUtility.ToJson(receipt,true),new UTF8Encoding(false));
            }
            finally{foreach(var renderer in foreground)if(renderer!=null)renderer.enabled=true;RenderTexture.active=previous;UnityEngine.Object.Destroy(pixels);}
        }
        static void RecordGeometry(Transform model,ModelReceipt record)
        {
            var renderers=model.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();Assert.IsNotEmpty(renderers,record.entityId);
            var bounds=renderers[0].bounds;record.visibleRenderers=renderers.Length;record.materialSlots=renderers.Sum(r=>r.sharedMaterials.Length);
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                foreach(var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);var filter=renderer.GetComponent<MeshFilter>();Assert.IsNotNull(filter,record.entityId);var mesh=filter.sharedMesh;Assert.IsNotNull(mesh,record.entityId);
                    var matrix=model.worldToLocalMatrix*filter.transform.localToWorldMatrix;record.visibleVertices+=mesh.vertexCount;record.visibleTriangles+=mesh.triangles.Length/3;
                    writer.Write(mesh.vertexCount);foreach(var vertex in mesh.vertices){var p=matrix.MultiplyPoint3x4(vertex);writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}foreach(int index in mesh.triangles)writer.Write(index);
                }
                writer.Flush();record.visibleGeometrySha256=Hash(stream.ToArray());
            }
            record.boundsCenter=bounds.center;record.boundsSize=bounds.size;
        }
        void RecordSource(string output)
        {
            var entries=SourceEntries();fingerprint=SourceFingerprint(entries);revision=ReadRevision(Path.GetFullPath(Path.Combine(Application.dataPath,"..")));
            var inventory=new Inventory{revision=revision,sourceFingerprint=fingerprint,files=entries};var bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(inventory,true));inventoryHash=Hash(bytes);
            File.WriteAllBytes(Path.Combine(output,"source-inventory-models-060.json"),bytes);
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
            var text=new StringBuilder("{");for(int i=0;i<entries.Length;i++){if(i>0)text.Append(',');Quote(text,entries[i].path);text.Append(':');Quote(text,entries[i].sha256);}text.Append('}');
            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
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
            // Read the local ref files directly. No shell/process/network invocation.
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
