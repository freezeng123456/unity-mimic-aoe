using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Frontier.Editor
{
    public static class BuildTools
    {
        private const string ScenePath="Assets/Frontier/Scenes/FrontierSkirmish.unity";
        [MenuItem("Frontier/Prepare Native Project")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/Frontier/Scenes");Directory.CreateDirectory("Assets/Frontier/Settings");
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Frontier/Settings/FrontierRenderer.asset");
            if(renderer==null){renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,"Assets/Frontier/Settings/FrontierRenderer.asset");}
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Frontier/Settings/FrontierURP.asset");
            if(pipeline==null){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,"Assets/Frontier/Settings/FrontierURP.asset");}
            pipeline.msaaSampleCount=2;pipeline.shadowDistance=90;pipeline.shadowCascadeCount=2;pipeline.renderScale=1;pipeline.supportsHDR=false;
            // URP 17.6 exposes these setters only inside its assembly; persist editor settings explicitly.
            var pipelineSettings=new SerializedObject(pipeline);
            foreach(string property in new[]{"m_MainLightShadowsSupported","m_AdditionalLightShadowsSupported","m_SoftShadowsSupported","m_AnyShadowsSupported"})
            {var setting=pipelineSettings.FindProperty(property);if(setting==null)throw new InvalidOperationException("Missing URP setting: "+property);setting.boolValue=property!="m_AdditionalLightShadowsSupported";}
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            QualitySettings.shadows=UnityEngine.ShadowQuality.All;QualitySettings.shadowDistance=90;QualitySettings.vSyncCount=0;
            UnityEditor.PlayerSettings.companyName="Frontier Workshop";UnityEditor.PlayerSettings.productName="Frontier Reckoning";
            UnityEditor.PlayerSettings.defaultScreenWidth=2560;UnityEditor.PlayerSettings.defaultScreenHeight=1440;UnityEditor.PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            UnityEditor.PlayerSettings.runInBackground=true;UnityEditor.PlayerSettings.resizableWindow=true;UnityEditor.PlayerSettings.colorSpace=ColorSpace.Linear;
            UnityEditor.PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone,"com.frontierworkshop.reckoning");
            UnityEditor.PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            UnityEditor.PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone,1);
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset"));var input=settings.FindProperty("activeInputHandler");if(input!=null){input.intValue=1;settings.ApplyModifiedPropertiesWithoutUndo();}
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset"));var shaders=graphics.FindProperty("m_AlwaysIncludedShaders");
            if(shaders!=null)foreach(string name in new[]{"Frontier/Fog","Universal Render Pipeline/Lit","Universal Render Pipeline/Unlit"}){var shader=Shader.Find(name);bool found=false;for(int i=0;i<shaders.arraySize;i++)if(shaders.GetArrayElementAtIndex(i).objectReferenceValue==shader)found=true;if(!found&&shader!=null){int i=shaders.arraySize;shaders.InsertArrayElementAtIndex(i);shaders.GetArrayElementAtIndex(i).objectReferenceValue=shader;}}
            graphics.ApplyModifiedPropertiesWithoutUndo();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Strategy Camera");camera.tag="MainCamera";var cam=camera.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=26;camera.AddComponent<AudioListener>();camera.AddComponent<UniversalAdditionalCameraData>().renderShadows=true;camera.transform.SetPositionAndRotation(new Vector3(24,50,-14),Quaternion.Euler(50,0,0));
            var sun=new GameObject("Late afternoon sun");var light=sun.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.55f;light.color=new Color(1,.96f,.87f);light.shadows=LightShadows.Soft;light.shadowStrength=.7f;light.shadowBias=.03f;light.shadowNormalBias=.2f;sun.transform.rotation=Quaternion.Euler(50,-35,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.57f,.63f,.65f);RenderSettings.fog=false;
            new GameObject("Frontier Session").AddComponent<GameController>();
            EditorSceneManager.SaveScene(scene,ScenePath);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            EditorSettings.serializationMode=SerializationMode.ForceText;
            EditorUtility.SetDirty(pipeline);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("FRONTIER_PREPARED "+Application.unityVersion+" / "+pipeline.GetType().FullName);
        }
        public static void VerifyAndBuildMac(){RunSimulationDuel();PrepareAndBuildMac();}
        public static void PrepareAndBuildMac(){Prepare();BuildMac();}
        [MenuItem("Frontier/Build macOS Client")]
        public static void BuildMac()
        {
            if(!File.Exists(ScenePath))Prepare();
            string path=Environment.GetEnvironmentVariable("FRONTIER_BUILD_PATH");
            if(string.IsNullOrEmpty(path))path=Path.GetFullPath("Builds/macOS/Frontier Reckoning.app");Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            Debug.Log("FRONTIER_BUILD "+report.summary.result+" bytes="+report.summary.totalSize+" path="+path);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
        }
        public static void RunSimulationDuel()
        {
            var rules=Catalog.Load();var world=new World(rules);var simulation=new MatchSimulation(world,0,1);
            float next=60;var sw=System.Diagnostics.Stopwatch.StartNew();
            while(world.Time<2400&&world.Winner<0){simulation.Step();if(world.Time>=next){Debug.Log($"DUEL {world.Time:0}s age={world.Players[0].age}/{world.Players[1].age} pop={world.Players[0].usedPopulation}/{world.Players[1].usedPopulation} res0={world.Players[0].resources} alive={world.Entities.FindAll(e=>e.Alive).Count}");next+=120;}}
            world.FlushEconomyLog();string dir=Environment.GetEnvironmentVariable("FRONTIER_REPORT_PATH");if(string.IsNullOrEmpty(dir))dir="Builds/Verification";Directory.CreateDirectory(dir);
            using(var writer=new StreamWriter(Path.Combine(dir,"automated-duel-events.jsonl")))foreach(var e in world.Events)writer.WriteLine(JsonUtility.ToJson(e));
            File.WriteAllText(Path.Combine(dir,"automated-duel.json"),JsonUtility.ToJson(new DuelSummary{winner=world.Winner,seconds=world.Time,wallSeconds=(float)sw.Elapsed.TotalSeconds,age0=world.Players[0].age,age1=world.Players[1].age,kills0=world.Players[0].kills,kills1=world.Players[1].kills,shipments0=world.Players[0].shipsSent,shipments1=world.Players[1].shipsSent,events=world.Events.Count},true));
            Debug.Log("DUEL_RESULT winner="+world.Winner+" time="+world.Time+" elapsed="+sw.Elapsed.TotalSeconds);
        }
        [Serializable]private class DuelSummary{public int winner,age0,age1,kills0,kills1,shipments0,shipments1,events;public float seconds,wallSeconds;}
    }
}
