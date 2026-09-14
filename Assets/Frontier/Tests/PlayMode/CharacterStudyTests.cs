using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Native URP offscreen diagnostics. These captures do not certify player input or final art.
    public sealed class CharacterStudyTests
    {
        const int Layer=30;
        RuleCatalog rules;Camera camera;GameObject stage;Font font;
        [UnityTest] public IEnumerator RenderUnitStudyAndPoseSheet()
        {
            var output=Environment.GetEnvironmentVariable("FRONTIER_ART_STUDY_DIR");
            if(string.IsNullOrEmpty(output))output=Path.Combine(Application.temporaryCachePath,"FrontierCharacterStudy024");
            Directory.CreateDirectory(output);rules=Catalog.Load();font=Resources.Load<Font>("Fonts/NotoSans-Regular");Assert.IsNotNull(font);
            stage=new GameObject("Isolated original character study");
            camera=new GameObject("Study camera").AddComponent<Camera>();camera.transform.SetParent(stage.transform);
            camera.enabled=false;camera.orthographic=true;camera.orthographicSize=7.8f;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.09f,.12f,.15f);camera.cullingMask=1<<Layer;camera.nearClipPlane=.1f;camera.farClipPlane=100;
            camera.transform.position=new Vector3(0,17,27);camera.transform.LookAt(new Vector3(0,1,0));
            var light=new GameObject("Study light").AddComponent<Light>();light.transform.SetParent(stage.transform);light.type=LightType.Directional;light.intensity=1.6f;light.cullingMask=1<<Layer;light.transform.rotation=Quaternion.Euler(48,-28,0);
            var rt=new RenderTexture(1800,1100,24,RenderTextureFormat.ARGB32);rt.Create();camera.aspect=(float)rt.width/rt.height;
            try
            {
                // Both versions use the same light/camera/positions and default authored rest pose.
                foreach(bool baseline in new[]{true,false})
                {
                    var sheet=new GameObject(baseline?"Baseline 0.2.3":"Candidate 0.2.4");sheet.transform.SetParent(stage.transform);
                    string[] ids={"villager","pike","musketeer","longbow","hussar","dragoon","carabineer","cannon"};
                    for(int i=0;i<ids.Length;i++)
                    {
                        var e=Entity(ids[i],i+1);var model=baseline?BaselineArt023.BuildEntity(e,sheet.transform):ProceduralArt.BuildEntity(e,sheet.transform);
                        model.position=new Vector3((i%4-1.5f)*4.6f,0,i<4?3:-3);
                        model.rotation=Quaternion.Euler(0,25,0);Label(sheet.transform,ids[i],model.position+new Vector3(0,-.65f,-2));
                    }
                    SetLayer(sheet);yield return null;
                    yield return Capture(rt,Path.Combine(output,baseline?"baseline-023.png":"candidate-024.png"));
                    var neutral=ProceduralArt.Mat(new Color(.62f,.62f,.62f));
                    foreach(var renderer in sheet.GetComponentsInChildren<Renderer>())if(renderer.GetComponent<TextMesh>()==null)renderer.sharedMaterial=neutral;
                    yield return Capture(rt,Path.Combine(output,baseline?"baseline-023-neutral.png":"candidate-024-neutral.png"));
                    UnityEngine.Object.Destroy(sheet);yield return null;
                }
                // Five independent sheets; every state is a distinct model at a fixed timer sample.
                camera.orthographicSize=6.6f;camera.transform.position=new Vector3(0,8,18);camera.transform.LookAt(new Vector3(0,1,0));
                foreach(var id in new[]{"pike","musketeer","longbow","hussar","cannon"})
                {
                    var sheet=new GameObject(id+" animation samples");sheet.transform.SetParent(stage.transform);
                    for(int i=0;i<4;i++)
                    {
                        var e=Entity(id,10+i);float time=10;float fired=-1000;e.isDeployed=id=="cannon";
                        if(i==1){e.fireAt=time+e.def.windup*.25f;e.attackReadyAt=time+e.def.period;e.status="Preparing attack";}
                        if(i==2){e.fireAt=-1;fired=time-.09f;e.attackReadyAt=time+e.def.period-e.def.windup-.09f;e.status="Reloading";}
                        if(i==3){e.fireAt=-1;e.attackReadyAt=time+(e.def.period-e.def.windup)*.4f;e.status="Reloading";}
                        var model=ProceduralArt.BuildEntity(e,sheet.transform);model.position=new Vector3((i-1.5f)*4.6f,0,0);model.rotation=Quaternion.Euler(0,50,0);
                        UnitAnimation.Apply(model.GetComponent<UnitRig>(),UnitAnimation.Evaluate(e,time,fired));
                        Label(sheet.transform,id+" / "+new[]{"idle","prepare","fire","recover"}[i],model.position+new Vector3(0,-.65f,-2));
                    }
                    SetLayer(sheet);yield return null;yield return Capture(rt,Path.Combine(output,id+"-poses.png"));
                    UnityEngine.Object.Destroy(sheet);yield return null;
                }
                // Close views compare the body and grip on the same current rig,
                // with identical camera, lighting, scale and animation timer.
                camera.orthographicSize=1.75f;camera.transform.position=new Vector3(0,2.1f,8);camera.transform.LookAt(new Vector3(0,.9f,0));
                foreach(var state in new[]{UnitPoseState.Idle,UnitPoseState.PreparingAttack,UnitPoseState.Fire,UnitPoseState.Reloading})
                {
                    var sheet=new GameObject("Infantry close comparison");sheet.transform.SetParent(stage.transform);
                    for(int i=0;i<2;i++)
                    {
                        var e=Entity("musketeer",30+i);var model=i==0?BaselineArt023.BuildEntity(e,sheet.transform):ProceduralArt.BuildEntity(e,sheet.transform);
                        model.position=new Vector3(i==0?-1.3f:1.3f,0,0);model.rotation=Quaternion.Euler(0,25,0);
                        UnitAnimation.Apply(model.GetComponent<UnitRig>(),new AnimationPose{state=state,reloading=state==UnitPoseState.Reloading,actionPhase=.75f});
                        Label(sheet.transform,i==0?"0.2.3 body":"0.2.4 body",model.position+new Vector3(0,-.22f,0));
                    }
                    SetLayer(sheet);yield return null;yield return Capture(rt,Path.Combine(output,"musketeer-close-"+state+".png"));
                    var neutral=ProceduralArt.Mat(new Color(.62f,.62f,.62f));
                    foreach(var renderer in sheet.GetComponentsInChildren<Renderer>())if(renderer.GetComponent<TextMesh>()==null)renderer.sharedMaterial=neutral;
                    yield return Capture(rt,Path.Combine(output,"musketeer-close-"+state+"-neutral.png"));
                    UnityEngine.Object.Destroy(sheet);yield return null;
                }
                // Actual default strategy-camera pitch and orthographic scale.
                // This isolated formation diagnoses size/readability, not gameplay.
                var strategyRt=new RenderTexture(2560,1440,24,RenderTextureFormat.ARGB32);strategyRt.Create();
                try
                {
                    camera.aspect=2560f/1440;camera.orthographicSize=26;camera.transform.rotation=Quaternion.Euler(50,0,0);camera.transform.position=new Vector3(0,0,0)-camera.transform.forward*65;
                    foreach(bool baseline in new[]{true,false})
                    {
                        var sheet=new GameObject("Default strategy camera formation");sheet.transform.SetParent(stage.transform);
                        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.SetParent(sheet.transform,false);ground.transform.position=new Vector3(0,-.12f,0);ground.transform.localScale=new Vector3(100,.2f,80);ground.GetComponent<Renderer>().sharedMaterial=ProceduralArt.Mat(new Color(.32f,.34f,.2f));
                        string[] ids={"pike","musketeer","longbow"};
                        for(int group=0;group<3;group++)for(int i=0;i<24;i++)
                        {
                            var e=Entity(ids[group],100+group*24+i);e.owner=group==2?1:0;
                            var model=baseline?BaselineArt023.BuildEntity(e,sheet.transform):ProceduralArt.BuildEntity(e,sheet.transform);
                            model.position=new Vector3((group-1)*13+(i%6-2.5f)*1.45f,0,(i/6-1.5f)*1.5f);model.rotation=Quaternion.Euler(0,155,0);
                        }
                        SetLayer(sheet);yield return null;yield return Capture(strategyRt,Path.Combine(output,baseline?"strategy-scale-023.png":"strategy-scale-024.png"));
                        UnityEngine.Object.Destroy(sheet);yield return null;
                    }
                }
                finally{strategyRt.Release();UnityEngine.Object.Destroy(strategyRt);}
            }
            finally{rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(stage);}
            yield return null;
        }
        EntityState Entity(string id,int n){var def=rules.Entity(id);Assert.IsNotNull(def,id);return new EntityState{id=n,owner=0,def=def,construction=1,hp=100,fireAt=-1,status="Idle"};}
        void Label(Transform parent,string title,Vector3 position)
        {
            var obj=new GameObject(title);obj.transform.SetParent(parent,false);obj.transform.position=position;obj.transform.rotation=camera.transform.rotation;
            var text=obj.AddComponent<TextMesh>();text.font=font;text.fontSize=40;text.characterSize=.035f;text.text=title;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.85f,.9f,.92f);
            obj.GetComponent<Renderer>().sharedMaterial=font.material;
        }
        static void SetLayer(GameObject root){foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;}
        IEnumerator Capture(RenderTexture rt,string path)
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
            var previous=RenderTexture.active;var pixels=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();
                var samples=pixels.GetPixels32();int varied=0;var first=samples[0];for(int i=0;i<samples.Length;i+=13)if(!samples[i].Equals(first))varied++;
                Assert.Greater(varied,100,"URP must render visible geometry, not an empty buffer");File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(pixels);}
        }
    }
}
