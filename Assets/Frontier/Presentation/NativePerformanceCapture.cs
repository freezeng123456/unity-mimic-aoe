using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using Unity.Profiling;

namespace Frontier
{
    [Serializable] public struct PerformanceFrame
    {
        public int frame;
        public double realtime;
        public float worldTime, frameMs, simulationMs;
        public int simulationSteps,poseApplications,offscreenPoseSkips,cameraRelevantUnits;
        public float lastTickNavigationMs,worldViewMs,fogUploadMs,mainThreadSampleMs,renderThreadSampleMs;
        public long gcAllocatedInFrameBytes,drawCalls,triangles;
        public bool allocationCounterAvailable,mainThreadCounterAvailable,renderThreadCounterAvailable;
        public bool focused, paused, eligibleInterval, focusLostInInterval, pauseChangedInInterval;
    }
    [Serializable] public struct PerformanceMemory
    {
        public double realtime;
        public float worldTime;
        public long managedBytes, unityAllocatedBytes;
        public int livingUnits, playerUnits0, playerUnits1, neutralUnits, viewportVisibleUnitCenters, livingBuildings, totalEntities,eventCount;
    }
    [Serializable] public sealed class PerformanceCaptureReport
    {
        public int schemaVersion = 2;
        public string scope = "Native rendered-frame intervals including capture overhead; focused and unpaused subset is reported separately. No hardware portability claim.";
        public string measurementDetails = PerformanceCounters.Scope;
        public string appVersion, buildGuid, unityVersion, matchId, currentMatchId, platform, cpu, gpu, resolution, utcStart, utcEnd, stopReason;
        public bool autoplay, benchmark, allFramesFocused, singleSession, truncated, focusedAtStart, pausedAtStart;
        public float simulationSpeed, worldTimeStart, worldTimeEnd, meanFrameMs, p95FrameMs, p99FrameMs;
        public double realtimeStart, realtimeEnd;
        public int totalFrames, includedFrames, excludedFrames, gc0, gc1, gc2;
        public int simulationSteps,allocationCounterSamples,mainThreadCounterSamples,renderThreadCounterSamples;
        public double meanAllocatedInFrameBytes;
        public float meanSimulationFrameMs,meanWorldViewMs,meanMainThreadSampleMs,meanRenderThreadSampleMs;
        public PerformanceFrame[] frames;
        public PerformanceMemory[] memory;
    }

    // Optional observation only. F8 never changes resources, commands or speed.
    [DefaultExecutionOrder(1000)]
    public sealed class NativePerformanceCapture : MonoBehaviour
    {
        public const int MaximumSeconds = 600, MaximumFrames = 72000;
        private GameController game;
        private World capturedWorld;
        private PerformanceCaptureReport report;
        private PerformanceFrame[] frames;
        private readonly List<PerformanceMemory> memory = new List<PerformanceMemory>(602);
        private int count, initialGc0, initialGc1, initialGc2;
        private double nextMemory;
        private bool previousFocused, previousPaused, focusLostInInterval;
        private int eventCursor;
        private int previousSimulationStep;
        private readonly PerformanceCounters counters=new PerformanceCounters();
        public bool Recording => report != null;
        public string LastReportPath { get; private set; }
        public void Initialize(GameController controller) { game = controller; }

        public bool Begin()
        {
            if (Recording || game == null || game.World == null || game.InMenu || Application.isBatchMode) return false;
            capturedWorld = game.World;
            frames = new PerformanceFrame[MaximumFrames]; count = 0; memory.Clear();
            counters.Begin();previousSimulationStep=game.Simulation?.StepCount??0;
            initialGc0 = GC.CollectionCount(0); initialGc1 = GC.CollectionCount(1); initialGc2 = GC.CollectionCount(2);
            report = new PerformanceCaptureReport
            {
                appVersion = Application.version, buildGuid = Application.buildGUID, unityVersion = Application.unityVersion,
                matchId = game.MatchId, platform = Application.platform.ToString(), cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName, resolution = Screen.width + "x" + Screen.height,
                utcStart = DateTime.UtcNow.ToString("O"), realtimeStart = Time.realtimeSinceStartupAsDouble,
                worldTimeStart = capturedWorld.Time, autoplay = game.Autoplay, benchmark = game.BenchmarkMode,
                simulationSpeed = game.Autoplay ? game.SimulationSpeed : 1,
                focusedAtStart = Application.isFocused, pausedAtStart = game.IsPaused
            };
            previousFocused = Application.isFocused; previousPaused = game.IsPaused;
            focusLostInInterval = false; eventCursor = capturedWorld.Events.Count;
            AddMemory(); nextMemory = report.realtimeStart + 1;
            game.Notify("Performance capture started. F8 stops; maximum 10 minutes.");
            return true;
        }

        private void LateUpdate()
        {
            bool pressed = Application.isFocused && Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame;
            if (pressed) { if (Recording) { Finish("f8"); return; } Begin(); return; }
            if (!Recording) return;
            if (game == null || game.World != capturedWorld || game.MatchId != report.matchId) { Finish("match_changed"); return; }
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - report.realtimeStart >= MaximumSeconds) { Finish("wall_clock_limit"); return; }
            if (count >= frames.Length) { report.truncated = true; Finish("frame_limit"); return; }
            bool pauseChanged = false;
            while (eventCursor < capturedWorld.Events.Count) if (capturedWorld.Events[eventCursor++].kind == "PauseChanged") pauseChanged = true;
            bool focused = Application.isFocused, paused = game.IsPaused;
            var sample=counters.Read();int step=game.Simulation?.StepCount??previousSimulationStep;int steps=Mathf.Max(0,step-previousSimulationStep);previousSimulationStep=step;
            frames[count++] = new PerformanceFrame
            {
                frame = Time.frameCount, realtime = now, worldTime = capturedWorld.Time,
                frameMs = Time.unscaledDeltaTime * 1000, simulationMs = steps==0||paused?0:game.Simulation.LastFrameMs,simulationSteps=steps,
                lastTickNavigationMs=steps==0?0:capturedWorld.LastNavigationMs,
                worldViewMs=game.View?.LastFrameMs??0,fogUploadMs=game.View?.LastFogMs??0,
                poseApplications=game.View?.LastPoseApplications??0,offscreenPoseSkips=game.View?.LastOffscreenPoseSkips??0,cameraRelevantUnits=game.View?.LastCameraRelevantUnits??0,
                gcAllocatedInFrameBytes=sample.gcAllocatedBytes,mainThreadSampleMs=sample.mainThreadMs,renderThreadSampleMs=sample.renderThreadMs,drawCalls=sample.drawCalls,triangles=sample.triangles,
                allocationCounterAvailable=sample.gcAllocatedBytes>=0,mainThreadCounterAvailable=sample.mainThreadMs>=0,renderThreadCounterAvailable=sample.renderThreadMs>=0,
                focused = focused, paused = paused, focusLostInInterval = focusLostInInterval, pauseChangedInInterval = pauseChanged,
                eligibleInterval = focused && previousFocused && !paused && !previousPaused && !focusLostInInterval && !pauseChanged
            };
            previousFocused = focused; previousPaused = paused; focusLostInInterval = false;
            if (now >= nextMemory) { AddMemory(); nextMemory = now + 1; }
            if (capturedWorld.Winner >= 0) Finish("match_finished");
        }

        private void AddMemory()
        {
            int units = 0, owned0 = 0, owned1 = 0, neutral = 0, viewportCenters = 0, buildings = 0;
            foreach (var e in capturedWorld.Entities) if (e.Alive)
            {
                if (e.def.kind == EntityKind.Unit)
                {
                    units++; if (e.owner == 0) owned0++; else if (e.owner == 1) owned1++; else neutral++;
                    // Center inside the camera viewport and allowed by player fog;
                    // this count does not promise unoccluded models or HUD-free pixels.
                    if (game != null && game.GameCamera != null && (e.owner == 0 || capturedWorld.Fog.At(0, e.position) == VisibilityState.Visible))
                    {
                        var screen = game.GameCamera.WorldToViewportPoint(new Vector3(e.position.x, 1, e.position.y));
                        if (screen.z > 0 && screen.x >= 0 && screen.x <= 1 && screen.y >= 0 && screen.y <= 1) viewportCenters++;
                    }
                }
                else if (e.def.kind == EntityKind.Building) buildings++;
            }
            memory.Add(new PerformanceMemory
            {
                realtime = Time.realtimeSinceStartupAsDouble, worldTime = capturedWorld.Time,
                managedBytes = GC.GetTotalMemory(false), unityAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                livingUnits = units, playerUnits0 = owned0, playerUnits1 = owned1, neutralUnits = neutral,
                viewportVisibleUnitCenters = viewportCenters,
                livingBuildings = buildings, totalEntities = capturedWorld.Entities.Count,eventCount=capturedWorld.Events.Count
            });
        }

        public void Finish(string reason = "requested")
        {
            if (!Recording) return;
            var stopped = report; report = null;
            try
            {
                AddMemory(); stopped.utcEnd = DateTime.UtcNow.ToString("O"); stopped.realtimeEnd = Time.realtimeSinceStartupAsDouble;
                stopped.worldTimeEnd = capturedWorld.Time; stopped.currentMatchId = game != null ? game.MatchId : "";
                stopped.singleSession = stopped.currentMatchId == stopped.matchId;
                stopped.stopReason = reason; stopped.gc0 = GC.CollectionCount(0) - initialGc0;
                stopped.gc1 = GC.CollectionCount(1) - initialGc1; stopped.gc2 = GC.CollectionCount(2) - initialGc2;
                stopped.totalFrames = count; stopped.frames = new PerformanceFrame[count]; Array.Copy(frames, stopped.frames, count);
                stopped.memory = memory.ToArray(); Summarize(stopped);
                string dir = Path.Combine(Application.persistentDataPath, "PerformanceCaptures"); Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "Frontier-Performance-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(path + ".partial", JsonUtility.ToJson(stopped, false)); File.Move(path + ".partial", path);
                LastReportPath = path; Debug.Log("FRONTIER_PERFORMANCE_CAPTURE_SAVED " + path);
                if (game != null) game.Notify("Performance sample saved to PerformanceCaptures.");
            }
            catch (Exception ex) { Debug.LogError("FRONTIER_PERFORMANCE_CAPTURE_FAILED " + ex.GetType().Name + ": " + ex.Message); }
            finally { counters.Dispose();capturedWorld = null; frames = null; memory.Clear(); count = 0; }
        }

        public static void Summarize(PerformanceCaptureReport value)
        {
            var included = new List<float>(); value.allFramesFocused = value.totalFrames > 0 && value.focusedAtStart;
            double simulation=0,view=0,allocation=0,main=0,render=0;
            value.simulationSteps=value.allocationCounterSamples=value.mainThreadCounterSamples=value.renderThreadCounterSamples=0;
            foreach (var frame in value.frames)
            {
                value.allFramesFocused &= frame.focused && !frame.focusLostInInterval;
                if (frame.eligibleInterval && frame.focused && !frame.paused && frame.frameMs > 0 && !float.IsNaN(frame.frameMs) && !float.IsInfinity(frame.frameMs))
                {
                    included.Add(frame.frameMs);simulation+=frame.simulationSteps==0?0:frame.simulationMs;view+=frame.worldViewMs;value.simulationSteps+=frame.simulationSteps;
                    if(frame.allocationCounterAvailable&&frame.gcAllocatedInFrameBytes>=0){allocation+=frame.gcAllocatedInFrameBytes;value.allocationCounterSamples++;}
                    if(frame.mainThreadCounterAvailable&&frame.mainThreadSampleMs>=0){main+=frame.mainThreadSampleMs;value.mainThreadCounterSamples++;}
                    if(frame.renderThreadCounterAvailable&&frame.renderThreadSampleMs>=0){render+=frame.renderThreadSampleMs;value.renderThreadCounterSamples++;}
                }
            }
            included.Sort(); value.includedFrames = included.Count; value.excludedFrames = value.totalFrames - included.Count;
            double sum = 0; foreach (float ms in included) sum += ms;
            value.meanFrameMs = included.Count == 0 ? 0 : (float)(sum / included.Count);
            value.p95FrameMs = included.Count == 0 ? 0 : included[(int)((included.Count - 1) * .95)];
            value.p99FrameMs = included.Count == 0 ? 0 : included[(int)((included.Count - 1) * .99)];
            value.meanSimulationFrameMs=included.Count==0?0:(float)(simulation/included.Count);value.meanWorldViewMs=included.Count==0?0:(float)(view/included.Count);
            value.meanAllocatedInFrameBytes=value.allocationCounterSamples==0?-1:allocation/value.allocationCounterSamples;
            value.meanMainThreadSampleMs=value.mainThreadCounterSamples==0?-1:(float)(main/value.mainThreadCounterSamples);
            value.meanRenderThreadSampleMs=value.renderThreadCounterSamples==0?-1:(float)(render/value.renderThreadCounterSamples);
        }

        private void OnDisable() { if (Recording) Finish("disabled"); }
        private void OnApplicationFocus(bool focused) { if (!focused && Recording) focusLostInInterval = true; }
        private void OnApplicationQuit() { if (Recording) Finish("quit"); }
    }

    // Optional counters are native recorder buffers, disposed when observation
    // ends. Reading them never collects the managed heap or changes a match.
    internal sealed class PerformanceCounters : IDisposable
    {
        internal const string Scope="No forced GC: managed memory uses GC.GetTotalMemory(false). Profiler counters are the latest completed samples at observation time and may lag the reported rendered interval; unavailable values are -1. Current simulationMs covers the observed AdvanceFrame call, simulationSteps records actual steps since the previous observation. lastTickNavigationMs covers only the last World.Tick, not all catch-up steps. WorldView timing includes its presentation work, not render-thread/GPU time. Camera-relevant animation includes a 15% viewport border and selected units. CollectionCount deltas are not allocation bytes or proof of a leak.";
        ProfilerRecorder allocation,main,render,draws,triangles;
        internal struct Sample{public long gcAllocatedBytes,drawCalls,triangles;public float mainThreadMs,renderThreadMs;}
        public void Begin()
        {
            Dispose();allocation=Start(ProfilerCategory.Memory,"GC Allocated In Frame");main=Start(ProfilerCategory.Internal,"Main Thread");render=Start(ProfilerCategory.Internal,"Render Thread");draws=Start(ProfilerCategory.Render,"Draw Calls Count");triangles=Start(ProfilerCategory.Render,"Triangles Count");
        }
        static ProfilerRecorder Start(ProfilerCategory category,string name)
        {try{return ProfilerRecorder.StartNew(category,name,1);}catch{return default;}}
        static long Read(ProfilerRecorder recorder)=>recorder.Valid&&recorder.Count>0?recorder.LastValue:-1;
        static float Milliseconds(ProfilerRecorder recorder){long value=Read(recorder);return value<0?-1:value*.000001f;}
        public Sample Read()=>new Sample{gcAllocatedBytes=Read(allocation),mainThreadMs=Milliseconds(main),renderThreadMs=Milliseconds(render),drawCalls=Read(draws),triangles=Read(triangles)};
        public void Dispose(){allocation.Dispose();main.Dispose();render.Dispose();draws.Dispose();triangles.Dispose();allocation=main=render=draws=triangles=default;}
    }
}
