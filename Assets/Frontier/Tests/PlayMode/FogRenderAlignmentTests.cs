using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Isolated URP capture of the production WorldView fog surface. This tests
    // world-to-UV alignment only; it is not a native-player acceptance test.
    public sealed class FogRenderAlignmentTests
    {
        const int Layer=30;
        GameObject session,stage,ground;GameController game;Camera camera;RenderTexture rt;Material white;
        EntityState[] army;

        [UnityTest] public IEnumerator WorldFogSurfaceAlignsWithArmyCoordinates()
        {
            var output=Environment.GetEnvironmentVariable("FRONTIER_FOG_STUDY_DIR");
            if(string.IsNullOrEmpty(output))output=Path.Combine(Application.temporaryCachePath,"FrontierFogStudy038");
            Directory.CreateDirectory(output);
            try
            {
            session=new GameObject("Fog alignment isolated match");game=session.AddComponent<GameController>();yield return null;
            game.StartMatch();game.enabled=false;game.World.Paused=true;
            var worldView=session.GetComponentInChildren<WorldView>(true);Assert.IsNotNull(worldView);
            var surface=worldView.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Three-state visibility surface");
            surface.gameObject.layer=Layer;
            game.GameCamera.enabled=false;
            army=new[]{
                game.World.Spawn("musketeer",0,new Vector2(96,24),true),
                game.World.Spawn("cannon",0,new Vector2(32,100),true)
            };
            var villager=game.World.Spawn("villager",0,new Vector2(90,70),true);
            game.World.Fog.Tick(game.World.Entities);
            ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Fog alignment white ground";ground.layer=Layer;ground.transform.position=new Vector3(64,-.02f,64);ground.transform.localScale=new Vector3(12.8f,1,12.8f);
            white=new Material(Shader.Find("Universal Render Pipeline/Unlit"));white.color=Color.white;ground.GetComponent<Renderer>().sharedMaterial=white;
            stage=new GameObject("Fog alignment camera stage");camera=new GameObject("Fog alignment camera").AddComponent<Camera>();camera.transform.SetParent(stage.transform);camera.enabled=false;camera.orthographic=true;camera.orthographicSize=72;camera.aspect=1;camera.cullingMask=1<<Layer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.transform.position=new Vector3(64,80,64);camera.transform.rotation=Quaternion.Euler(90,0,0);
            rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);rt.Create();
                // Let the production WorldView execute its normal LateUpdate once,
                // while the simulation remains paused, so its original fog mesh,
                // texture and unit surfaces are the captured state.
                game.enabled=true;yield return null;game.enabled=false;yield return null;
                yield return new WaitForSecondsRealtime(.4f);yield return Capture(Path.Combine(output,"fog-alignment-before.png"));
                var first=Sample(UnitViewport(army[0]));var second=Sample(UnitViewport(army[1]));var villagerClear=Sample(UnitViewport(villager));var villagerVision=Sample(camera.WorldToViewportPoint(new Vector3(90,0,82)));var villagerOutside=Sample(camera.WorldToViewportPoint(new Vector3(114,0,70)));var unexplored=Sample(camera.WorldToViewportPoint(new Vector3(5,0,5)));
                var removed=army[0];game.World.Remove(removed);game.World.Fog.Tick(game.World.Entities);yield return new WaitForSecondsRealtime(.4f);yield return Capture(Path.Combine(output,"fog-alignment-after-remove.png"));
                var dark=Sample(UnitViewport(removed));var stillClear=Sample(UnitViewport(army[1]));
                File.WriteAllText(Path.Combine(output,"fog-alignment.json"),JsonUtility.ToJson(new Evidence{resolution=512,armyA=first,armyB=second,villager=villagerClear,villagerVision=villagerVision,villagerOutside=villagerOutside,unexplored=unexplored,removedLocation=dark,remainingArmy=stillClear,armyAState=game.World.Fog.At(0,removed.position).ToString(),armyBState=game.World.Fog.At(0,army[1].position).ToString(),villagerState=game.World.Fog.At(0,villager.position).ToString()},true));
                Assert.Greater(first,0.82f,"Army foot must be clear through the production fog surface");Assert.Greater(second,0.82f,"Asymmetric second army foot must be clear through the production fog surface");Assert.Greater(villagerClear,.82f,"Villager foot must be clear through the production fog surface");Assert.Greater(villagerVision,.82f,"Villager vision area must be clear");
                Assert.That(villagerOutside,Is.EqualTo(ExpectedFogBrightness(252)).Within(.025f),"Outside villager vision must have the unexplored fog opacity");
                Assert.That(unexplored,Is.EqualTo(ExpectedFogBrightness(252)).Within(.025f),"Unexplored corner must have the unexplored fog opacity");
                Assert.That(dark,Is.EqualTo(ExpectedFogBrightness(170)).Within(.025f),"Removed army location must have explored fog opacity");Assert.Greater(stillClear,.82f,"Remaining army foot must stay clear");
            }
            finally
            {
                if(rt!=null){rt.Release();UnityEngine.Object.Destroy(rt);}if(camera!=null)UnityEngine.Object.Destroy(camera.gameObject);if(stage!=null)UnityEngine.Object.Destroy(stage);if(ground!=null)UnityEngine.Object.Destroy(ground);if(white!=null)UnityEngine.Object.Destroy(white);UnityEngine.Object.Destroy(session);
            }
            yield return null;
        }
        [Serializable] class Evidence{public int resolution;public float armyA,armyB,villager,villagerVision,villagerOutside,unexplored,removedLocation,remainingArmy;public string armyAState,armyBState,villagerState;}
        static float ExpectedFogBrightness(byte alpha)
        {
            // The RGB24/RGBA32 texture is sampled as sRGB, blended in the
            // project's linear color space, then encoded into the capture.
            // A raw <.12 threshold incorrectly ignored this conversion.
            Color fog=new Color(12f/255,21f/255,25f/255).linear;
            return Color.Lerp(Color.white,fog,alpha/255f).gamma.grayscale;
        }
        Vector3 UnitViewport(EntityState e)=>camera.WorldToViewportPoint(new Vector3(e.position.x,0,e.position.y));
        float Sample(Vector3 uv){var x=Mathf.RoundToInt(uv.x*rt.width);var y=Mathf.RoundToInt(uv.y*rt.height);var old=RenderTexture.active;var tex=new Texture2D(3,3,TextureFormat.RGBA32,false);try{RenderTexture.active=rt;tex.ReadPixels(new Rect(x-1,y-1,3,3),0,0);tex.Apply();return tex.GetPixels().Average(c=>c.grayscale);}finally{RenderTexture.active=old;UnityEngine.Object.Destroy(tex);}}
        IEnumerator Capture(string path){RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});yield return null;var old=RenderTexture.active;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);try{RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}finally{RenderTexture.active=old;UnityEngine.Object.Destroy(tex);}}
    }
}
