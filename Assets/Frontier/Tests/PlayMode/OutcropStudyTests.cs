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

namespace Frontier.Tests
{
    // Native URP image study and resource lifecycle checks. Not player acceptance.
    public sealed class OutcropStudyTests
    {
        const int Layer=30;Camera camera;RenderTexture rt;
        [UnityTest] public IEnumerator RenderBaselineAndCandidateAtIdenticalCameras()
        {
            var output=Environment.GetEnvironmentVariable("FRONTIER_OUTCROP_STUDY_DIR");
            if(string.IsNullOrEmpty(output))output=Path.Combine(Application.temporaryCachePath,"FrontierOutcropStudy039");
            Directory.CreateDirectory(output);GameObject stage=null;Material earth=null,stone=null,outline=null;
            var owned=new List<Mesh>();var ambient=RenderSettings.ambientLight;var ambientMode=RenderSettings.ambientMode;
            try
            {
                stage=new GameObject("Original outcrop study");
                camera=new GameObject("Study camera").AddComponent<Camera>();camera.transform.SetParent(stage.transform);
                camera.enabled=false;camera.orthographic=true;camera.aspect=2560f/1440;camera.cullingMask=1<<Layer;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.1f,.08f);
                var sun=new GameObject("Study sunlight").AddComponent<Light>();sun.transform.SetParent(stage.transform);
                sun.type=LightType.Directional;sun.intensity=1.8f;sun.color=new Color(1,.92f,.75f);sun.cullingMask=1<<Layer;sun.transform.rotation=Quaternion.Euler(50,-35,0);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.57f,.63f,.65f);
                earth=new Material(Shader.Find("Universal Render Pipeline/Lit"));earth.color=new Color(.35f,.38f,.22f);
                stone=new Material(Shader.Find("Universal Render Pipeline/Lit"));stone.color=new Color(.38f,.39f,.33f);stone.SetFloat("_Smoothness",0);
                outline=new Material(Shader.Find("Universal Render Pipeline/Unlit"));outline.color=new Color(.85f,.68f,.28f);
                var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.SetParent(stage.transform);ground.layer=Layer;
                ground.transform.position=new Vector3(0,-.02f,0);ground.transform.localScale=new Vector3(10,1,8);ground.GetComponent<Renderer>().sharedMaterial=earth;
                UnityEngine.Object.Destroy(ground.GetComponent<Collider>());
                rt=new RenderTexture(2560,1440,24,RenderTextureFormat.ARGB32);rt.Create();
                foreach(bool baseline in new[]{true,false})
                {
                    var sheet=new GameObject(baseline?"Old sphere outcrops":"Original faceted outcrops");sheet.transform.SetParent(stage.transform);
                    var info=new List<MeshInfo>();
                    for(int i=0;i<4;i++)
                    {
                        GameObject rock;
                        if(baseline){rock=GameObject.CreatePrimitive(PrimitiveType.Sphere);UnityEngine.Object.Destroy(rock.GetComponent<Collider>());rock.transform.localScale=new Vector3(1.8f*2.2f,2.8f,1.8f*2.1f);}
                        else{rock=new GameObject("Original outcrop "+i);var mesh=OutcropArt.CreateMesh(1.5f,i);owned.Add(mesh);rock.AddComponent<MeshFilter>().sharedMesh=mesh;rock.AddComponent<MeshRenderer>();info.Add(new MeshInfo{variation=i,vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,bounds=mesh.bounds});}
                        rock.transform.SetParent(sheet.transform);rock.layer=Layer;rock.transform.localPosition=new Vector3((i-1.5f)*4.3f,baseline?1.1f:0,0);rock.GetComponent<Renderer>().sharedMaterial=stone;
                        var marker=new GameObject("Actual occupied grid boundary");marker.transform.SetParent(sheet.transform);marker.layer=Layer;
                        var line=marker.AddComponent<LineRenderer>();line.sharedMaterial=outline;line.widthMultiplier=.025f;line.loop=true;line.positionCount=4;
                        float x=(i-1.5f)*4.3f;line.SetPositions(new[]{new Vector3(x-1.5f,.015f,-1.5f),new Vector3(x+1.5f,.015f,-1.5f),new Vector3(x+1.5f,.015f,1.5f),new Vector3(x-1.5f,.015f,1.5f)});
                    }
                    if(!baseline)File.WriteAllText(Path.Combine(output,"outcrop-mesh.json"),JsonUtility.ToJson(new MeshEvidence{variations=info.ToArray()},true));
                    camera.orthographicSize=26;camera.transform.rotation=Quaternion.Euler(50,0,0);camera.transform.position=-camera.transform.forward*65;
                    yield return Capture(Path.Combine(output,baseline?"baseline-normal.png":"candidate-normal.png"));
                    camera.orthographicSize=7.5f;camera.transform.rotation=Quaternion.Euler(50,0,0);camera.transform.position=new Vector3(0,1,0)-camera.transform.forward*40;
                    yield return Capture(Path.Combine(output,baseline?"baseline-close.png":"candidate-close.png"));
                    UnityEngine.Object.Destroy(sheet);yield return null;
                }
            }
            finally
            {
                if(rt!=null){rt.Release();UnityEngine.Object.Destroy(rt);}foreach(var mesh in owned)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
                if(stage!=null)UnityEngine.Object.Destroy(stage);if(earth!=null)UnityEngine.Object.Destroy(earth);if(stone!=null)UnityEngine.Object.Destroy(stone);if(outline!=null)UnityEngine.Object.Destroy(outline);
                RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=ambientMode;
            }
            yield return null;
        }
        [UnityTest] public IEnumerator RestartReleasesOwnedMeshesAndResetsRockVisibility()
        {
            GameObject session=null;Mesh[] latest=Array.Empty<Mesh>();
            try
            {
                session=new GameObject("Outcrop lifecycle match");var game=session.AddComponent<GameController>();yield return null;
                game.StartMatch();game.World.Paused=true;game.enabled=false;
                for(int round=0;round<3;round++)
                {
                    yield return new WaitForSecondsRealtime(.4f);
                    var rocks=game.View.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.gameObject.name=="Navigation outcrop").ToArray();
                    Assert.AreEqual(game.World.TerrainObstacles.Count,rocks.Length);Assert.Greater(rocks.Length,0);
                    latest=rocks.Select(f=>f.sharedMesh).ToArray();Assert.IsTrue(latest.All(m=>m!=null&&m.name.StartsWith("Frontier Outcrop ")));
                    Assert.IsTrue(rocks.All(r=>r.GetComponent<Collider>()==null),"Navigation owns blocking; art adds no competing collider");
                    var point=rocks[0].transform.position;Assert.IsFalse(rocks[0].gameObject.activeSelf,"New match does not reveal outcrops");
                    game.World.Fog.Reveal(0,new Vector2(point.x,point.z),6);yield return new WaitForSecondsRealtime(.4f);Assert.IsTrue(rocks[0].gameObject.activeSelf);
                    game.StartMatch();game.World.Paused=true;game.enabled=false;yield return null;yield return null;
                    Assert.IsTrue(latest.All(m=>m==null),"Restart destroys every prior owned mesh");
                }
                latest=game.View.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.gameObject.name=="Navigation outcrop").Select(f=>f.sharedMesh).ToArray();
            }
            finally{if(session!=null)UnityEngine.Object.Destroy(session);}
            yield return null;yield return null;Assert.IsTrue(latest.All(m=>m==null),"WorldView destruction releases its last mesh generation");
        }
        [Serializable]class MeshEvidence{public MeshInfo[] variations;}
        [Serializable]class MeshInfo{public int variation,vertices,triangles;public Bounds bounds;}
        IEnumerator Capture(string path)
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;
            var old=RenderTexture.active;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();Assert.Greater(tex.GetPixels().Count(c=>c.grayscale>.25f),100,"URP buffer contains rendered scene");File.WriteAllBytes(path,tex.EncodeToPNG());}
            finally{RenderTexture.active=old;UnityEngine.Object.Destroy(tex);}
        }
    }
}
