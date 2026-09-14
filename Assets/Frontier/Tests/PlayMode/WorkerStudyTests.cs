using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Frontier.Tests
{
    // Matched native URP offscreen study; not a player-input or final-art acceptance test.
    public sealed class WorkerStudyTests
    {
        const int Layer=29;
        RuleCatalog rules; Camera camera; GameObject stage; Font font; Material neutral,flat,wire;
        readonly List<Mesh> diagnosticMeshes=new List<Mesh>();
        sealed class Sample { public string label; public World world; public EntityState worker,target; }
        [UnityTest] public IEnumerator RenderControlledWorkerStudy()
        {
            var dir=Environment.GetEnvironmentVariable("FRONTIER_WORKER_STUDY_DIR");
            if(string.IsNullOrEmpty(dir))dir=Path.Combine(Application.temporaryCachePath,"FrontierWorkerStudy050");
            Directory.CreateDirectory(dir);rules=Catalog.Load();font=Resources.Load<Font>("Fonts/NotoSans-Regular");Assert.IsNotNull(font);
            stage=new GameObject("Isolated worker study");camera=new GameObject("Study camera").AddComponent<Camera>();camera.transform.SetParent(stage.transform);
            camera.enabled=false;camera.orthographic=true;camera.orthographicSize=2.35f;camera.aspect=3;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.09f,.11f);camera.cullingMask=1<<Layer;
            camera.transform.position=new Vector3(0,2.1f,8);camera.transform.LookAt(new Vector3(0,.95f,0));
            var light=new GameObject("Study light").AddComponent<Light>();light.transform.SetParent(stage.transform);light.type=LightType.Directional;
            light.intensity=1.5f;light.shadows=LightShadows.None;light.cullingMask=1<<Layer;light.transform.rotation=Quaternion.Euler(48,-28,0);
            neutral=new Material(Shader.Find("Universal Render Pipeline/Lit"));neutral.SetColor("_BaseColor",new Color(.63f,.63f,.63f));
            flat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));flat.SetColor("_BaseColor",Color.white);
            wire=new Material(flat);wire.SetColor("_BaseColor",new Color(.65f,.84f,.94f));
            var rt=new RenderTexture(2400,800,24,RenderTextureFormat.ARGB32);rt.Create();
            try
            {
                var samples=new[]{Gather("tree","Wood / axe"),Gather("mine","Coin / pick"),Gather("berry","Food / basket"),BuildHouse(),Repair()};
                foreach(string mode in new[]{"shaded","neutral","silhouette","wireframe"})yield return Sheet(samples,rt,Path.Combine(dir,"close-"+mode+".png"),mode);
                for(int phase=1;phase<=2;phase++)
                {
                    foreach(var s in samples){s.world.Tick(.1f);s.world.Tick(.1f);AssertFresh(s);}
                    yield return Sheet(samples,rt,Path.Combine(dir,"close-shaded-phase-"+phase+".png"),"shaded");
                }
                var wide=new RenderTexture(2560,1440,24,RenderTextureFormat.ARGB32);wide.Create();
                try
                {
                    camera.aspect=2560f/1440;camera.orthographicSize=26;camera.transform.rotation=Quaternion.Euler(50,0,0);camera.transform.position=-camera.transform.forward*65;
                    yield return Sheet(samples,wide,Path.Combine(dir,"strategy-scale.png"),"strategy");
                }
                finally{wide.Release();Object.Destroy(wide);}
                File.WriteAllText(Path.Combine(dir,"scope.txt"),"Native Unity URP offscreen controlled work study; five independent Worlds seed5091. Actual resource deltas, paid construction and paid healing; no guardians. Same camera/light/time across baseline/candidate. No real-time shadows. Close 2400x800 ortho2.35; strategy2560x1440 ortho26 pitch50. Neutral preserves original normals. Silhouette unlit. Wire MeshTopology.Lines from original vertices/triangles. Three actual time samples, no fabricated animation clock. Not native player input, full match or final art acceptance.");
            }
            finally
            {
                rt.Release();Object.Destroy(rt);Object.Destroy(stage);Object.Destroy(neutral);Object.Destroy(flat);Object.Destroy(wire);
                foreach(var m in diagnosticMeshes)Object.Destroy(m);diagnosticMeshes.Clear();
            }
        }
        Sample New(string label)
        {
            var w=new World(rules,5091,"coast","coast",5);
            foreach(var e in w.Entities.Where(e=>(e.def.tags&Tags.Guardian)!=0).ToArray())w.Remove(e);
            w.Players[0].resources=new Cost(1000,1000,1000);
            var worker=w.Spawn("villager",0,new Vector2(33,28));w.Fog.Tick(w.Entities);
            return new Sample{label=label,world=w,worker=worker};
        }
        void Issue(Sample s,OrderKind order)
        {
            s.world.Fog.Tick(s.world.Entities);
            var r=s.world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{s.worker.id},order=order,target=s.target.id});Assert.IsTrue(r.accepted,r.reason);
        }
        void Step(Sample s){for(int i=0;i<5;i++)s.world.Tick(.1f);AssertFresh(s);}
        void AssertFresh(Sample s)
        {
            Assert.AreEqual(s.target.id,s.worker.work.target,s.label);Assert.AreEqual(s.world.Time,s.worker.work.time,s.label);
            Assert.AreNotEqual(WorkKind.None,s.worker.work.kind);Assert.IsTrue(s.worker.Alive);
        }
        Sample Gather(string id,string label)
        {
            var s=New(label);s.target=s.world.Spawn(id,-1,new Vector2(36,32));s.worker.position=s.target.position-Vector2.right*(s.target.def.radius+s.worker.def.radius+.7f);
            var before=s.world.Players[0].gathered;Issue(s,OrderKind.Gather);Step(s);
            Assert.Greater(s.world.Players[0].gathered.food+s.world.Players[0].gathered.wood+s.world.Players[0].gathered.coin,before.food+before.wood+before.coin);
            return s;
        }
        Sample BuildHouse()
        {
            var s=New("Build / hammer");float wood=s.world.Players[0].resources.wood;
            var r=s.world.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{s.worker.id},item="house",point=new Vector2(36,28)});Assert.IsTrue(r.accepted,r.reason);
            s.target=s.world.Get(s.worker.orders.Peek().target);Step(s);
            Assert.Greater(s.target.construction,0);Assert.IsFalse(s.target.Complete);Assert.Less(s.world.Players[0].resources.wood,wood);return s;
        }
        Sample Repair()
        {
            var s=New("Repair / hammer");s.target=s.world.Spawn("house",0,new Vector2(36,28));s.target.hp-=100;
            float hp=s.target.hp,wood=s.world.Players[0].resources.wood;Issue(s,OrderKind.Repair);Step(s);
            Assert.Greater(s.target.hp,hp);Assert.Less(s.world.Players[0].resources.wood,wood);return s;
        }
        IEnumerator Sheet(Sample[] samples,RenderTexture rt,string path,string mode)
        {
            var sheet=new GameObject("Work samples "+mode);sheet.transform.SetParent(stage.transform);
            try
            {
                bool strategy=mode=="strategy";
                if(strategy)ProceduralArt.Part(sheet.transform,"Ground",PrimitiveType.Cube,new Vector3(0,-.15f,0),new Vector3(100,.2f,70),new Color(.32f,.34f,.2f));
                for(int i=0;i<samples.Length;i++)
                {
                    var s=samples[i];var m=ProceduralArt.BuildEntity(s.worker,sheet.transform);m.position=new Vector3((i-2)*(strategy?9:2.6f),0,0);m.rotation=Quaternion.Euler(0,25,0);
                    var method=typeof(UnitAnimation).GetMethod("Evaluate");var args=new object[]{s.worker,s.world.Time,-1000f,-1000f,-1000f,s.target};
                    var pose=(AnimationPose)method.Invoke(null,args.Take(method.GetParameters().Length).ToArray());UnitAnimation.Apply(m.GetComponent<UnitRig>(),pose);
                    if(!strategy)Label(sheet.transform,s.label,m.position+new Vector3(0,-.3f,0));
                }
                if(mode=="neutral"||mode=="silhouette")foreach(var r in sheet.GetComponentsInChildren<Renderer>())if(r.GetComponent<TextMesh>()==null)r.sharedMaterial=mode=="neutral"?neutral:flat;
                if(mode=="wireframe")Wire(sheet);
                foreach(var t in sheet.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
                foreach(var r in sheet.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
                yield return null;yield return Capture(rt,path);
            }
            finally{Object.Destroy(sheet);}
            yield return null;
        }
        void Wire(GameObject sheet)
        {
            foreach(var f in sheet.GetComponentsInChildren<MeshFilter>())
            {
                var r=f.GetComponent<Renderer>();if(r==null||f.sharedMesh==null)continue;r.enabled=false;
                var original=f.sharedMesh;var triangles=original.triangles;var indices=new int[triangles.Length*2];
                for(int i=0;i<triangles.Length;i+=3)for(int j=0;j<3;j++){indices[(i+j)*2]=triangles[i+j];indices[(i+j)*2+1]=triangles[i+(j+1)%3];}
                var mesh=new Mesh{name="Original mesh edges",vertices=original.vertices};mesh.SetIndices(indices,MeshTopology.Lines,0);diagnosticMeshes.Add(mesh);
                var go=new GameObject("Wire edges");go.transform.SetParent(f.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=wire;
            }
        }
        void Label(Transform parent,string title,Vector3 position)
        {
            var obj=new GameObject(title);obj.transform.SetParent(parent,false);obj.transform.position=position;obj.transform.rotation=camera.transform.rotation;
            var text=obj.AddComponent<TextMesh>();text.font=font;text.fontSize=40;text.characterSize=.025f;text.text=title;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
            obj.GetComponent<Renderer>().sharedMaterial=font.material;
        }
        IEnumerator Capture(RenderTexture target,string path)
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});yield return null;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});yield return null;
            var previous=RenderTexture.active;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;Object.Destroy(pixels);}
        }
    }
}
