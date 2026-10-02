using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

namespace Frontier
{
    [Serializable] public struct PerformanceFrame
    {
        public int frame;
        public double realtime;
        public float worldTime, frameMs, simulationMs;
        public bool focused, paused, eligibleInterval, focusLostInInterval, pauseChangedInInterval;
    }
    [Serializable] public struct PerformanceMemory
    {
        public double realtime;
        public float worldTime;
        public long managedBytes, unityAllocatedBytes;
        public int livingUnits, playerUnits0, playerUnits1, neutralUnits, viewportVisibleUnitCenters, livingBuildings, totalEntities;
    }
    [Serializable] public sealed class PerformanceCaptureReport
    {
        public int schemaVersion = 1;
        public string scope = "Native rendered-frame intervals including capture overhead; focused and unpaused subset is reported separately. No hardware portability claim.";
        public string appVersion, buildGuid, unityVersion, matchId, currentMatchId, platform, cpu, gpu, resolution, utcStart, utcEnd, stopReason;
        public bool autoplay, benchmark, allFramesFocused, singleSession, truncated, focusedAtStart, pausedAtStart;
        public float simulationSpeed, worldTimeStart, worldTimeEnd, meanFrameMs, p95FrameMs, p99FrameMs;
        public double realtimeStart, realtimeEnd;
        public int totalFrames, includedFrames, excludedFrames, gc0, gc1, gc2;
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
        public bool Recording => report != null;
        public string LastReportPath { get; private set; }
        public void Initialize(GameController controller) { game = controller; }

        public bool Begin()
        {
            if (Recording || game == null || game.World == null || game.InMenu || Application.isBatchMode) return false;
            capturedWorld = game.World;
            frames = new PerformanceFrame[MaximumFrames]; count = 0; memory.Clear();
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
            frames[count++] = new PerformanceFrame
            {
                frame = Time.frameCount, realtime = now, worldTime = capturedWorld.Time,
                frameMs = Time.unscaledDeltaTime * 1000, simulationMs = game.IsPaused ? 0 : game.LastSimulationFrameMs,
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
                livingBuildings = buildings, totalEntities = capturedWorld.Entities.Count
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
            finally { capturedWorld = null; frames = null; memory.Clear(); count = 0; }
        }

        public static void Summarize(PerformanceCaptureReport value)
        {
            var included = new List<float>(); value.allFramesFocused = value.totalFrames > 0 && value.focusedAtStart;
            foreach (var frame in value.frames)
            {
                value.allFramesFocused &= frame.focused && !frame.focusLostInInterval;
                if (frame.eligibleInterval && frame.focused && !frame.paused && frame.frameMs > 0 && !float.IsNaN(frame.frameMs) && !float.IsInfinity(frame.frameMs)) included.Add(frame.frameMs);
            }
            included.Sort(); value.includedFrames = included.Count; value.excludedFrames = value.totalFrames - included.Count;
            double sum = 0; foreach (float ms in included) sum += ms;
            value.meanFrameMs = included.Count == 0 ? 0 : (float)(sum / included.Count);
            value.p95FrameMs = included.Count == 0 ? 0 : included[(int)((included.Count - 1) * .95)];
            value.p99FrameMs = included.Count == 0 ? 0 : included[(int)((included.Count - 1) * .99)];
        }

        private void OnDisable() { if (Recording) Finish("disabled"); }
        private void OnApplicationFocus(bool focused) { if (!focused && Recording) focusLostInInterval = true; }
        private void OnApplicationQuit() { if (Recording) Finish("quit"); }
    }
}
