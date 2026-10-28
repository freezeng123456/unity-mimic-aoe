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
    // Geometry checks and real URP diagnostic sheets. Passing these checks is
    // not a visual-quality approval and does not substitute for native gameplay.
    public sealed class ArchitectureReview060Tests
    {
        private static readonly string[] Civs={"coast","river","france","ottoman","wayfarer","highland"};
        private static readonly string[] Names={"British","Dutch","French","Ottoman","Wayfarer","Highland"};
        private static readonly string[] Buildings={"town","house","barracks","stable","foundry","market","tower","farm","bank","estate","trade_post","waystation","workshop","mosque"};
        private const int Layer=30;
        private GameObject stage;
        private Camera studyCamera;
        private Font font;
        private RuleCatalog rules;
        private int nextId;
        private string sourceFingerprint,sourceInventoryHash,sourceRevision;
        private readonly List<ModelReceipt> records=new List<ModelReceipt>();
        private readonly List<Transform> recordedModels=new List<Transform>();

        [Serializable] private sealed class SourceEntry {public string path,sha256;}
        [Serializable] private sealed class SourceInventory
        {
            public string schema="architecture-study-source-1",revision,sourceFingerprint;
            public string fingerprintAlgorithm="sha256 of ordinal path + tab + file sha256 + newline; UTF-8";
            public string sourceState="working tree including uncommitted files, excluding temporary Unity InitTestScene GUID fixtures; not a clean-commit claim";
            public SourceEntry[] files;
        }
        [Serializable] private sealed class ModelReceipt
        {
            public string id,requestedCivilization,effectiveCivilization,geometrySha256;
            public int owner,age,vertices,triangles,renderers,materialSlots;
            public Vector3 position,eulerAngles;
            public float construction=1;
        }
        [Serializable] private sealed class CaptureReceipt
        {
            public string schema="architecture-study-capture-1";
            public string purpose="Synthetic original-model URP study; not a normal match, paid-construction, input or performance proof";
            public string file,sha256,recordedAtUtc,sourceFingerprint,sourceInventorySha256,sourceRevision,appVersion,unityVersion,graphicsDevice,renderPipeline,study;
            public int width,height,frame,geometryChangedPixelSamples;
            public float cameraOrthographicSize,cameraAspect;
            public Vector3 cameraPosition,cameraEuler;
            public ModelReceipt[] models;
        }

        [TearDown] public void CleanStage()
        {if(stage!=null)UnityEngine.Object.DestroyImmediate(stage);stage=null;records.Clear();recordedModels.Clear();}

        [TestCase("coast")][TestCase("river")][TestCase("france")]
        [TestCase("ottoman")][TestCase("wayfarer")][TestCase("highland")]
        public void EveryBuildingAndAgeHasFiniteLitUvGeometryWithinItsSimulationFootprint(string civilization)
        {
            rules=Catalog.Load();stage=new GameObject("Architecture mesh contracts 060");
            foreach(string id in Buildings)for(int age=1;age<=5;age++)
            {
                var def=rules.Entity(id);var model=CreateModel(stage.transform,id,Vector3.zero,0,civilization,age);
                try
                {
                    string context=civilization+"/"+id+"/"+age;
                    var architecture=model.GetComponent<ArchitectureModel>();Assert.IsNotNull(architecture,context);
                    Assert.AreEqual(EffectiveCivilization(id,civilization),architecture.Civilization,context);
                    Assert.AreEqual(age,architecture.Age,context);
                    architecture.SetConstruction(1);
                    var renderers=model.GetComponentsInChildren<MeshRenderer>(true);
                    Assert.That(renderers.Length,Is.InRange(1,16),context+" uses grouped sections, not per-brick renderers");
                    Assert.Greater(architecture.LocalBounds.size.y,.1f,context+" cached model bounds");
                    var filters=model.GetComponentsInChildren<MeshFilter>(true);Assert.Greater(filters.Length,0,context);
                    foreach(var filter in filters)
                    {
                        var mesh=filter.sharedMesh;Assert.IsNotNull(mesh,context);
                        var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
                        Assert.Greater(vertices.Length,0,context);Assert.AreEqual(vertices.Length,normals.Length,context+" normals");Assert.AreEqual(vertices.Length,uv.Length,context+" UVs");
                        var matrix=model.worldToLocalMatrix*filter.transform.localToWorldMatrix;var validNormals=new bool[vertices.Length];
                        bool active=filter.gameObject.activeInHierarchy;
                        for(int i=0;i<vertices.Length;i++)
                        {
                            var p=matrix.MultiplyPoint3x4(vertices[i]);
                            if(!Finite(p)||!Finite(normals[i])||!Finite(uv[i].x)||!Finite(uv[i].y))Assert.Fail(context+" has nonfinite vertex, normal or UV at "+filter.name+"/"+i);
                            float normalLength=normals[i].sqrMagnitude;validNormals[i]=normalLength>=.90f&&normalLength<=1.10f;
                            // Hidden scaffolding can have its own construction envelope.
                            if(active&&(Mathf.Abs(p.x)>def.width*.5f+.08f||Mathf.Abs(p.z)>def.depth*.5f+.08f||p.y<-.10f))
                                Assert.Fail(context+" geometry outside simulation footprint at "+filter.name+"/"+i+": "+p);
                        }
                        var triangles=mesh.triangles;
                        for(int i=0;i<triangles.Length;i+=3)
                        {
                            int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                            if(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).sqrMagnitude<1e-12f)continue;
                            if(!validNormals[a]||!validNormals[b]||!validNormals[c])Assert.Fail(context+" lit triangle has zero/invalid vertex normal at "+filter.name+" triangle "+i/3);
                        }
                        var renderer=filter.GetComponent<MeshRenderer>();Assert.IsNotNull(renderer,context);Assert.AreEqual(mesh.subMeshCount,renderer.sharedMaterials.Length,context+" material slots");
                        foreach(var material in renderer.sharedMaterials){Assert.IsNotNull(material,context);Assert.IsNotNull(material.shader,context);Assert.IsTrue(material.shader.isSupported,context);}
                    }
                }
                finally {DestroyModelImmediately(model);}
            }
        }

        [TestCase("town")][TestCase("house")]
        public void SixCivilizationsHaveDifferentGeometricSilhouettesWithTheSameTeamColor(string id)
        {
            rules=Catalog.Load();stage=new GameObject("Civilization silhouette contracts 060");
            var masks=new Dictionary<string,bool[]>();var hashes=new Dictionary<string,string>();
            foreach(string civ in Civs)
            {
                var model=CreateModel(stage.transform,id,Vector3.zero,0,civ,3);
                try {masks[civ]=Silhouette(model);hashes[civ]=GeometryHash(model);}
                finally {DestroyModelImmediately(model);}
            }
            for(int a=0;a<Civs.Length;a++)for(int b=a+1;b<Civs.Length;b++)
            {
                Assert.AreNotEqual(hashes[Civs[a]],hashes[Civs[b]],id+" must change geometry, not just materials");
                int union=0,difference=0;var left=masks[Civs[a]];var right=masks[Civs[b]];
                for(int i=0;i<left.Length;i++){if(left[i]||right[i])union++;if(left[i]!=right[i])difference++;}
                Assert.Greater(union,0);Assert.GreaterOrEqual(difference,12,id+" geometric outline difference "+Civs[a]+" / "+Civs[b]);
                Assert.GreaterOrEqual(difference/(float)union,.025f,id+" same-team silhouette difference "+Civs[a]+" / "+Civs[b]);
            }
        }

        [TestCase("coast")][TestCase("river")][TestCase("france")]
        [TestCase("ottoman")][TestCase("wayfarer")][TestCase("highland")]
        public void AllFiveTownAndHouseAgesHaveGeometricProgression(string civilization)
        {
            rules=Catalog.Load();stage=new GameObject("Five-age geometry progression 060");
            foreach(string id in new[]{"town","house"})
            {
                var signatures=new HashSet<string>();
                for(int age=1;age<=5;age++)
                {
                    var model=CreateModel(stage.transform,id,Vector3.zero,0,civilization,age);
                    try {Assert.IsTrue(signatures.Add(GeometryHash(model)),civilization+" "+id+" age "+age+" repeats earlier geometry");}
                    finally {DestroyModelImmediately(model);}
                }
            }
        }

        [UnityTest] public IEnumerator DestroyedBuildingReleasesOwnedMeshesAndKeepsSharedMaterialsUsable()
        {
            rules=Catalog.Load();stage=new GameObject("Architecture ownership 060");
            var first=CreateModel(stage.transform,"town",Vector3.zero,0,"france",5);
            var owners=first.GetComponentsInChildren<ProceduralMeshLifetime>(true);Assert.Greater(owners.Length,0);
            var meshes=owners.Select(o=>o.OwnedMesh).Where(m=>m!=null).ToArray();
            var materials=first.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            UnityEngine.Object.Destroy(first.gameObject);yield return null;yield return null;
            foreach(var mesh in meshes)Assert.IsTrue(mesh==null,"Destroyed building leaked an owned mesh");
            foreach(var material in materials)Assert.IsTrue(material!=null,"Destroying one building must not destroy a shared material");
            var second=CreateModel(stage.transform,"town",Vector3.zero,0,"france",5);
            Assert.Greater(second.GetComponentsInChildren<MeshRenderer>().Length,0);
            foreach(var renderer in second.GetComponentsInChildren<MeshRenderer>())foreach(var material in renderer.sharedMaterials)Assert.IsNotNull(material);
        }

        [UnityTest] public IEnumerator RenderCivilizationsCatalogsAgesConstructionAndStrategyScene()
        {
            string output=Environment.GetEnvironmentVariable("FRONTIER_ARCHITECTURE_STUDY_DIR");
            if(string.IsNullOrWhiteSpace(output))output=Path.Combine(Application.temporaryCachePath,"FrontierArchitecture060");
            Directory.CreateDirectory(output);rules=Catalog.Load();font=Resources.Load<Font>("Fonts/NotoSans-Regular");Assert.IsNotNull(font);
            RecordSource(output);stage=new GameObject("Architecture 060 isolated URP study");
            studyCamera=new GameObject("Architecture study camera").AddComponent<Camera>();studyCamera.transform.SetParent(stage.transform,false);
            studyCamera.enabled=false;studyCamera.orthographic=true;studyCamera.clearFlags=CameraClearFlags.SolidColor;studyCamera.backgroundColor=new Color(.15f,.20f,.16f);
            studyCamera.cullingMask=1<<Layer;studyCamera.nearClipPlane=.1f;studyCamera.farClipPlane=180;
            var light=new GameObject("Architecture study sun").AddComponent<Light>();light.transform.SetParent(stage.transform,false);light.type=LightType.Directional;light.intensity=1.6f;
            light.cullingMask=1<<Layer;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(48,-28,0);
            var rt=new RenderTexture(3072,2048,24,RenderTextureFormat.ARGB32);rt.Create();
            try
            {
                ConfigureCamera(19);var sheet=Sheet("Six civilizations; identical blue ownership");
                for(int i=0;i<Civs.Length;i++)
                {
                    float x=(i-2.5f)*9;
                    CreateRecorded(sheet.transform,"town",new Vector3(x,0,6),0,Civs[i],3);Label(sheet.transform,Names[i]+" town / III",new Vector3(x,.05f,2.8f));
                    CreateRecorded(sheet.transform,"house",new Vector3(x,0,-6),0,Civs[i],3);Label(sheet.transform,Names[i]+" house / III",new Vector3(x,.05f,-8.4f));
                }
                yield return Capture(rt,output,"01-six-civilizations-town-house","Same team, same light, same camera; Age III geometry comparison");
                yield return RemoveSheet(sheet);
                for(int c=0;c<Civs.Length;c++)
                {
                    ConfigureCamera(19);sheet=Sheet(Names[c]+" complete building catalog");
                    for(int i=0;i<Buildings.Length;i++)
                    {
                        var point=new Vector3((i%4-1.5f)*11,0,(1.5f-i/4)*11);
                        CreateRecorded(sheet.transform,Buildings[i],point,0,Civs[c],3);
                        string effective=EffectiveCivilization(Buildings[i],Civs[c]);
                        Label(sheet.transform,Buildings[i]+(effective!=Civs[c]?" ["+effective+"]":""),point+new Vector3(0,.05f,-3.4f));
                    }
                    yield return Capture(rt,output,"0"+(c+2)+"-catalog-"+Civs[c],"Age III catalog fixture; bracketed dedicated buildings keep their own civilization and are not claimed buildable by every civilization");
                    yield return RemoveSheet(sheet);
                }
                foreach(string id in new[]{"town","house"})
                {
                    ConfigureCamera(28);sheet=Sheet(id+" five-age progression");
                    for(int c=0;c<Civs.Length;c++)for(int age=1;age<=5;age++)
                    {
                        var point=new Vector3((age-3)*11,0,(2.5f-c)*10);
                        CreateRecorded(sheet.transform,id,point,0,Civs[c],age);Label(sheet.transform,Names[c]+" / "+age,point+new Vector3(0,.05f,-3.25f));
                    }
                    yield return Capture(rt,output,id=="town"?"08-town-five-ages":"09-house-five-ages","Columns I-V; rows British, Dutch, French, Ottoman, Wayfarer, Highland; synthetic age comparison");
                    yield return RemoveSheet(sheet);
                }
                ConfigureCamera(19);sheet=Sheet("Construction state comparison");
                for(int c=0;c<Civs.Length;c++)for(int phase=0;phase<3;phase++)
                {
                    var point=new Vector3((c-2.5f)*9,0,(1-phase)*10);float amount=phase==0?.25f:phase==1?.65f:1;
                    var model=CreateRecorded(sheet.transform,"town",point,0,Civs[c],3);model.GetComponent<ArchitectureModel>().SetConstruction(amount);records[records.Count-1].construction=amount;
                    Label(sheet.transform,Names[c]+" / "+Mathf.RoundToInt(amount*100)+"%",point+new Vector3(0,.05f,-3.1f));
                }
                yield return Capture(rt,output,"10-construction-stages","Rows 25%, 65%, complete; construction presentation fixture");yield return RemoveSheet(sheet);
                ConfigureCamera(26);sheet=Sheet("Normal strategy distance settlement and army");
                CreateSettlement(sheet.transform,"france",0,new Vector3(-15,0,-1));CreateSettlement(sheet.transform,"ottoman",1,new Vector3(17,0,12));
                for(int i=0;i<18;i++)CreateRecorded(sheet.transform,i%3==0?"janissary":"musketeer",new Vector3((i%6-2.5f)*1.5f,0,(i/6-1)*2.0f-10),i%3==0?1:0,i%3==0?"ottoman":"france",3);
                foreach(float x in new[]{-5f,0,5}){CreateRecorded(sheet.transform,"cuirassier",new Vector3(x,0,-15),0,"france",3);CreateRecorded(sheet.transform,"cannon",new Vector3(x,0,-19),0,"france",3);}
                for(int i=0;i<10;i++)CreateRecorded(sheet.transform,"tree",new Vector3(-28+(i%3)*2,0,13+(i/3)*2),-1,"coast",1);
                for(int i=0;i<8;i++)CreateRecorded(sheet.transform,"tree",new Vector3(28+(i%2)*2,0,-12+(i/2)*2),-1,"coast",1);
                CreateRecorded(sheet.transform,"mine",new Vector3(-25,0,-12),-1,"coast",1);CreateRecorded(sheet.transform,"berry",new Vector3(26,0,2),-1,"coast",1);
                CreateRecorded(sheet.transform,"coureur",new Vector3(-23,0,-10),0,"france",3);CreateRecorded(sheet.transform,"villager",new Vector3(25,0,0),1,"ottoman",3);
                yield return Capture(rt,output,"11-strategy-distance-settlements","Orthographic size 26, matching game scale; staged models and units without gameplay HUD, fog, simulation or paid construction");
                yield return RemoveSheet(sheet);
            }
            finally{rt.Release();UnityEngine.Object.Destroy(rt);}
            yield return null;
        }

        private void CreateSettlement(Transform parent,string civ,int owner,Vector3 center)
        {
            CreateRecorded(parent,"town",center,owner,civ,3);
            var ids=new[]{"house","barracks","stable","market","foundry","farm"};
            var points=new[]{new Vector3(-7,0,-7),new Vector3(0,0,8),new Vector3(7,0,1),new Vector3(-7,0,1),new Vector3(7,0,8),new Vector3(-7,0,8)};
            for(int i=0;i<ids.Length;i++)CreateRecorded(parent,ids[i],center+points[i],owner,civ,3);
            if(civ=="ottoman")CreateRecorded(parent,"mosque",center+new Vector3(0,0,-7),owner,civ,3);
        }
        private Transform CreateModel(Transform parent,string id,Vector3 point,int owner,string civ,int age)
        {
            var def=rules.Entity(id);Assert.IsNotNull(def,id);
            var entity=new EntityState{id=++nextId,owner=owner,def=def,hp=def.hp,construction=1,fireAt=-1,status="Idle",isDeployed=(def.tags&Tags.Artillery)!=0};
            var model=ProceduralArt.BuildEntity(entity,parent,civ,age);model.localPosition=point;
            if(def.kind==EntityKind.Unit){model.localRotation=Quaternion.Euler(0,155,0);UnitAnimation.Apply(model.GetComponent<UnitRig>(),UnitAnimation.Evaluate(entity,10));}
            return model;
        }
        private Transform CreateRecorded(Transform parent,string id,Vector3 point,int owner,string civ,int age)
        {
            var model=CreateModel(parent,id,point,owner,civ,age);
            var renderers=model.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
            var meshes=renderers.Select(r=>r.GetComponent<MeshFilter>()).ToArray();
            records.Add(new ModelReceipt{id=id,requestedCivilization=civ,effectiveCivilization=model.GetComponent<ArchitectureModel>()?.Civilization??civ,owner=owner,age=age,position=point,eulerAngles=model.localEulerAngles,
                vertices=meshes.Sum(m=>m.sharedMesh.vertexCount),triangles=meshes.Sum(m=>m.sharedMesh.triangles.Length/3),renderers=renderers.Length,materialSlots=renderers.Sum(r=>r.sharedMaterials.Length),geometrySha256=GeometryHash(model)});
            recordedModels.Add(model);
            return model;
        }
        private GameObject Sheet(string name)
        {records.Clear();recordedModels.Clear();var sheet=new GameObject(name);sheet.transform.SetParent(stage.transform,false);ProceduralArt.Part(sheet.transform,"Diagnostic ground",PrimitiveType.Cube,new Vector3(0,-.13f,0),new Vector3(110,.20f,100),new Color(.32f,.36f,.23f));return sheet;}
        private IEnumerator RemoveSheet(GameObject sheet){UnityEngine.Object.Destroy(sheet);yield return null;yield return null;records.Clear();recordedModels.Clear();}
        private void ConfigureCamera(float size)
        {studyCamera.orthographicSize=size;studyCamera.aspect=1.5f;studyCamera.transform.rotation=Quaternion.Euler(50,0,0);studyCamera.transform.position=new Vector3(0,1,0)+studyCamera.transform.rotation*(Vector3.back*70);}
        private void Label(Transform parent,string title,Vector3 point)
        {
            var obj=new GameObject("Review label "+title);obj.transform.SetParent(parent,false);obj.transform.localPosition=point;obj.transform.rotation=studyCamera.transform.rotation;
            var text=obj.AddComponent<TextMesh>();text.font=font;text.fontSize=40;text.characterSize=.13f;text.text=title;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.94f,.93f,.81f);
            obj.GetComponent<Renderer>().sharedMaterial=font.material;
        }
        private IEnumerator Capture(RenderTexture rt,string output,string name,string description)
        {
            foreach(var transform in stage.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=Layer;
            var previous=RenderTexture.active;var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            var foreground=recordedModels.SelectMany(model=>model.GetComponentsInChildren<Renderer>()).Where(renderer=>renderer.enabled).ToArray();
            try
            {
                foreach(var renderer in foreground)
                {
                    var b=renderer.bounds;
                    for(int corner=0;corner<8;corner++)
                    {
                        var p=b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                        var screen=studyCamera.WorldToViewportPoint(p);
                        Assert.That(screen.x,Is.InRange(.01f,.99f),name+" clipped horizontal model: "+renderer.name);
                        Assert.That(screen.y,Is.InRange(.01f,.99f),name+" clipped vertical model: "+renderer.name);
                        Assert.Greater(screen.z,studyCamera.nearClipPlane,name+" model behind camera");
                    }
                }
                // A control render excludes every model but retains the ground and
                // labels, so an empty sheet cannot pass on background pixels alone.
                foreach(var renderer in foreground)renderer.enabled=false;
                RenderPipeline.SubmitRenderRequest(studyCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
                RenderPipeline.SubmitRenderRequest(studyCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
                RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();var control=texture.GetPixels32();
                foreach(var renderer in foreground)renderer.enabled=true;
                RenderPipeline.SubmitRenderRequest(studyCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
                RenderPipeline.SubmitRenderRequest(studyCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
                RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();var pixels=texture.GetPixels32();var first=pixels[0];int varied=0;
                for(int i=0;i<pixels.Length;i+=17)if(!pixels[i].Equals(first))varied++;
                Assert.Greater(varied,2000,"URP capture must contain real rendered pixels");
                int changed=0;for(int i=0;i<pixels.Length;i+=17)if(!pixels[i].Equals(control[i]))changed++;
                Assert.Greater(changed,1000,"Actual models must change pixels relative to the ground-and-label control render");
                var png=texture.EncodeToPNG();File.WriteAllBytes(Path.Combine(output,name+".png"),png);
                var receipt=new CaptureReceipt{file=name+".png",sha256=Hash(png),recordedAtUtc=DateTime.UtcNow.ToString("o"),sourceFingerprint=sourceFingerprint,sourceInventorySha256=sourceInventoryHash,sourceRevision=sourceRevision,
                    appVersion=Application.version,unityVersion=Application.unityVersion,graphicsDevice=SystemInfo.graphicsDeviceName,renderPipeline=GraphicsSettings.currentRenderPipeline?.GetType().FullName??"none",study=description,
                    width=rt.width,height=rt.height,frame=Time.frameCount,geometryChangedPixelSamples=changed,cameraOrthographicSize=studyCamera.orthographicSize,cameraAspect=studyCamera.aspect,cameraPosition=studyCamera.transform.position,cameraEuler=studyCamera.transform.eulerAngles,models=records.ToArray()};
                File.WriteAllText(Path.Combine(output,name+".json"),JsonUtility.ToJson(receipt,true),new UTF8Encoding(false));
            }
            finally{foreach(var renderer in foreground)if(renderer!=null)renderer.enabled=true;RenderTexture.active=previous;UnityEngine.Object.Destroy(texture);}
        }
        private void RecordSource(string output)
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));var entries=new List<SourceEntry>();
            foreach(string directory in new[]{"Assets","Packages","ProjectSettings","Tools"})
            {
                string folder=Path.Combine(root,directory);if(!Directory.Exists(folder))continue;
                foreach(string file in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))
                {
                    string path=file.Substring(root.Length+1).Replace('\\','/');if(path.Split('/').Any(p=>p.StartsWith(".",StringComparison.Ordinal)||p=="__pycache__"))continue;
                    if(System.Text.RegularExpressions.Regex.IsMatch(path,@"^Assets/InitTestScene[0-9a-f-]{36}\.unity(\.meta)?$"))continue;
                    entries.Add(new SourceEntry{path=path,sha256=Hash(File.ReadAllBytes(file))});
                }
            }
            entries=entries.OrderBy(e=>e.path,StringComparer.Ordinal).ToList();var text=new StringBuilder();foreach(var entry in entries)text.Append(entry.path).Append('\t').Append(entry.sha256).Append('\n');
            sourceFingerprint=Hash(Encoding.UTF8.GetBytes(text.ToString()));sourceRevision=ReadRevision(root);
            var inventory=new SourceInventory{revision=sourceRevision,sourceFingerprint=sourceFingerprint,files=entries.ToArray()};
            byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(inventory,true));sourceInventoryHash=Hash(bytes);File.WriteAllBytes(Path.Combine(output,"source-inventory-060.json"),bytes);
        }
        private static string ReadRevision(string root)
        {
            try
            {
                using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo{FileName="git",Arguments="rev-parse HEAD",WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true}))
                {if(process==null)return "unavailable";if(!process.WaitForExit(3000)){process.Kill();return "unavailable";}return process.ExitCode==0?process.StandardOutput.ReadToEnd().Trim():"unavailable";}
            }
            catch(Exception){return "unavailable";}
        }
        private static string EffectiveCivilization(string id,string civ)=>id=="bank"?"river":id=="mosque"?"ottoman":id=="waystation"?"wayfarer":id=="workshop"?"highland":civ;
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        private static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        private static string GeometryHash(Transform model)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                foreach(var filter in model.GetComponentsInChildren<MeshFilter>().Where(f=>f.gameObject.activeInHierarchy&&f.GetComponent<Renderer>().enabled))
                {
                    var mesh=filter.sharedMesh;var matrix=model.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    writer.Write(mesh.vertexCount);foreach(var vertex in mesh.vertices){var p=matrix.MultiplyPoint3x4(vertex);writer.Write(Mathf.RoundToInt(p.x*10000));writer.Write(Mathf.RoundToInt(p.y*10000));writer.Write(Mathf.RoundToInt(p.z*10000));}
                    foreach(int index in mesh.triangles)writer.Write(index);
                }
                writer.Flush();return Hash(stream.ToArray());
            }
        }
        private static bool[] Silhouette(Transform model)
        {
            const int size=96;var result=new bool[size*size*2];
            for(int view=0;view<2;view++)
            {
                var rotation=Quaternion.Inverse(Quaternion.Euler(35,view==0?30:120,0));
                foreach(var filter in model.GetComponentsInChildren<MeshFilter>().Where(f=>f.gameObject.activeInHierarchy&&f.GetComponent<Renderer>().enabled))
                {
                    var mesh=filter.sharedMesh;var matrix=model.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    var points=mesh.vertices.Select(v=>rotation*matrix.MultiplyPoint3x4(v)).Select(p=>new Vector2((p.x+5)*size/10f,(p.y+2)*size/10f)).ToArray();var triangles=mesh.triangles;
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        var a=points[triangles[i]];var b=points[triangles[i+1]];var c=points[triangles[i+2]];float area=Cross(b-a,c-a);if(Mathf.Abs(area)<1e-6f)continue;
                        int minX=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),0,size-1),maxX=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))),0,size-1);
                        int minY=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))),0,size-1),maxY=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))),0,size-1);
                        for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
                        {
                            var p=new Vector2(x+.5f,y+.5f);float ab=Cross(b-a,p-a),bc=Cross(c-b,p-b),ca=Cross(a-c,p-c);
                            if((ab>=0&&bc>=0&&ca>=0)||(ab<=0&&bc<=0&&ca<=0))result[view*size*size+y*size+x]=true;
                        }
                    }
                }
            }
            return result;
        }
        private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        private static void DestroyModelImmediately(Transform model)
        {
            var owners=model.GetComponentsInChildren<ProceduralMeshLifetime>(true);var meshes=owners.Select(o=>o.OwnedMesh).Where(m=>m!=null).Distinct().ToArray();
            foreach(var owner in owners)owner.OwnedMesh=null;UnityEngine.Object.DestroyImmediate(model.gameObject);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
}
