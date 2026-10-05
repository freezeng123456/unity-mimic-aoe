using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Observation and file routing only. These fixtures do not certify native
    // foreground performance, a real LAN session or a natural match result.
    public sealed class NativePerformanceCapture069Tests
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        GameController game;
        NativePerformanceCapture capture;
        string directory;

        [UnitySetUp] public IEnumerator SetUp()
        {
            Assert.IsTrue(Application.isEditor || Application.isBatchMode,
                "Controller fixtures must not load a player's personal native storage");
            directory = Path.Combine(Path.GetTempPath(), "Frontier-performance-069-" + Guid.NewGuid().ToString("N"));
            Assert.IsFalse(Directory.Exists(directory));
            root = new GameObject("Isolated performance capture 069");
            game = root.AddComponent<GameController>();
            game.enabled = false;
            Assert.IsFalse(game.Settings.Persistent);
            Assert.IsTrue(game.Progress.MemoryOnly);
            SetField(game, "dataDirectoryOverride", directory);
            SetField(game, "saveDirectoryOverride", Path.Combine(directory, "Saves"));
            SetField(game, "reportDirectoryOverride", Path.Combine(directory, "Reports"));
            SetField(game, "replayDirectoryOverride", Path.Combine(directory, "Replays"));
            game.Controls.enabled = false;
            game.View.enabled = false;
            game.Ambience.enabled = false;
            game.Audio.PauseAll(true);
            foreach (var canvas in root.GetComponentsInChildren<Canvas>()) canvas.gameObject.SetActive(false);
            capture = root.GetComponent<NativePerformanceCapture>();
            Assert.IsNotNull(capture);
            capture.enabled = false;
            var cameraRoot = new GameObject("Owned performance observation camera");
            cameraRoot.transform.SetParent(root.transform, false);
            var camera = cameraRoot.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 30;
            camera.aspect = 1;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 260;
            camera.transform.position = new Vector3(64, 80, 64);
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            SetProperty(game, "GameCamera", camera);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (capture != null && capture.Recording) capture.Finish("fixture_cleanup");
            if (game != null) SetProperty(game, "World", null);
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        World CreateWorld()
        {
            var world = new World(game.Rules, 314159,
                new[] { "coast", "river", "france", "ottoman" }, new[] { 0, 1, 2, 3 }, 5, MapTopology.Frontier);
            foreach (var entity in world.Entities.Where(e => e.Alive && e.def.kind == EntityKind.Unit).ToArray())
                world.Remove(entity);
            world.Fog.RestoreState(new int[world.Fog.CaptureState().Length]);
            SetProperty(game, "World", world);
            SetProperty(game, "MatchId", "isolated-performance-069");
            return world;
        }

        void PrimeObservation(World world)
        {
            // Begin deliberately rejects batch mode. Exercise its exact report
            // constructor and the real Finish writer without bypassing that rule.
            SetField(capture, "capturedWorld", world);
            SetField(capture, "frames", Array.Empty<PerformanceFrame>());
            SetField(capture, "report", Invoke(capture, "CreateReport"));
        }

        [UnityTest] public IEnumerator FinishWritesOnlyToExplicitDataRootAndLabelsClientObserver()
        {
            var world = CreateWorld();
            SetField(game, "lanRole", GameSessionRole.LanClient);
            SetProperty(game, "LocalPlayerSlot", 2);
            SetProperty(game, "ObserverPlayer", 2);
            PrimeObservation(world);
            capture.Finish("test_finished");

            string expected = Path.Combine(directory, "PerformanceCaptures");
            Assert.AreEqual(Path.GetFullPath(expected), Path.GetDirectoryName(capture.LastReportPath));
            Assert.IsTrue(File.Exists(capture.LastReportPath));
            Assert.IsFalse(File.Exists(capture.LastReportPath + ".partial"));
            Assert.AreEqual(1, Directory.GetFiles(expected, "*.json").Length);
            Assert.IsFalse(Directory.Exists(game.CaptureDirectory), "Performance writing must not create F9 output as a side effect");
            var report = JsonUtility.FromJson<PerformanceCaptureReport>(File.ReadAllText(capture.LastReportPath));
            Assert.AreEqual(3, report.schemaVersion);
            Assert.AreEqual("LanClient", report.sessionRole);
            Assert.AreEqual(2, report.localPlayerSlot);
            Assert.AreEqual(2, report.observerPlayerStart);
            Assert.AreEqual(2, report.observerPlayerEnd);
            Assert.IsFalse(report.sampledObserverChanged);
            Assert.IsTrue(report.singleSession);
            Assert.AreEqual("test_finished", report.stopReason);
            Assert.AreEqual(1, report.memory.Length);
            Assert.AreEqual(2, report.memory[0].observerPlayer);
            Assert.IsFalse(capture.Recording);
            yield return null;
        }

        [UnityTest] public IEnumerator ViewportCountsUseCurrentObserverFogAndExcludeCargoAndOffscreenCenters()
        {
            var world = CreateWorld();
            var p1 = world.Spawn("villager", 0, new Vector2(44, 64));
            var p3 = world.Spawn("coureur", 2, new Vector2(84, 64));
            var enemy = world.Spawn("villager", 1, new Vector2(80, 67));
            var offscreen = world.Spawn("coureur", 2, new Vector2(124, 64));
            var cargo = world.Spawn("coureur", 2, new Vector2(82, 64));
            var town = world.Entities.First(e => e.Alive && e.owner == 2 && e.def.id == "town");
            cargo.garrisonedIn = town.id;
            town.occupants.Add(cargo.id);
            world.Fog.Reveal(2, p3.position, 8);
            Assert.IsTrue(world.IsVisible(2, enemy));
            Assert.IsFalse(world.IsVisible(0, enemy));
            Assert.IsFalse(world.IsVisible(2, p1));
            Assert.IsFalse(world.IsVisible(2, cargo));
            Assert.Greater(game.GameCamera.WorldToViewportPoint(new Vector3(offscreen.position.x, 1, offscreen.position.y)).x, 1);
            SetProperty(game, "ObserverPlayer", 2);
            PrimeObservation(world);
            Invoke(capture, "AddMemory");
            SetProperty(game, "ObserverPlayer", 0);
            Invoke(capture, "AddMemory");
            var samples = Field<List<PerformanceMemory>>(capture, "memory");
            Assert.AreEqual(2, samples.Count);
            Assert.AreEqual(2, samples[0].observerPlayer);
            Assert.AreEqual(2, samples[0].viewportVisibleUnitCenters,
                "P3 sees its own on-screen unit and its revealed enemy, excluding P1's hidden unit, cargo and off-screen centers");
            Assert.AreEqual(0, samples[1].observerPlayer);
            Assert.AreEqual(1, samples[1].viewportVisibleUnitCenters, "P1 sees its own on-screen unit only");
            Assert.AreEqual(5, samples[0].livingUnits, "Total living units include cargo and off-screen units");
            Assert.AreEqual(1, samples[0].playerUnits0);
            Assert.AreEqual(1, samples[0].playerUnits1);
            Assert.AreEqual(3, samples[0].playerUnits2);

            capture.Finish("observer_changed");
            var report = JsonUtility.FromJson<PerformanceCaptureReport>(File.ReadAllText(capture.LastReportPath));
            Assert.AreEqual("Single", report.sessionRole);
            Assert.AreEqual(2, report.observerPlayerStart);
            Assert.AreEqual(0, report.observerPlayerEnd);
            Assert.IsTrue(report.sampledObserverChanged);
            CollectionAssert.AreEqual(new[] { 2, 0, 0 }, report.memory.Select(m => m.observerPlayer).ToArray());
            CollectionAssert.AreEqual(new[] { 2, 1, 1 }, report.memory.Select(m => m.viewportVisibleUnitCenters).ToArray());
            yield return null;
        }

        [UnityTest] public IEnumerator ViewportCentersUseAuthoredSurfaceHeightRatherThanTheOldZeroPlane()
        {
            var document = new ScenarioDocument
            {
                id = "elevated-performance", name = "Elevated observation fixture",
                rulesHash = SnapshotCodec.RulesHash(game.Rules),
                heightsCm = Enumerable.Repeat(10000, 129 * 129).ToArray(),
                starts = new[]
                {
                    new ScenarioStart { player = 0, team = 0, civilization = "coast",
                        town = new Vector2(20, 20), explorer = new Vector2(28, 20), workers = new[] { new Vector2(64, 64) } },
                    new ScenarioStart { player = 1, team = 1, civilization = "river",
                        town = new Vector2(108, 108), explorer = new Vector2(100, 108) }
                }
            };
            var world = new World(game.Rules, document, new[] { "coast", "river" }, new[] { 0, 1 });
            var worker = world.ScenarioEntity("start-0-worker-0");
            Assert.IsNotNull(worker);
            Assert.IsTrue(world.IsVisible(0, worker));
            SetProperty(game, "World", world);
            SetProperty(game, "MatchId", "isolated-elevated-performance-069");
            PrimeObservation(world);
            Invoke(capture, "AddMemory");
            var samples = Field<List<PerformanceMemory>>(capture, "memory");
            Assert.Less(game.GameCamera.WorldToViewportPoint(new Vector3(64, 101, 64)).z, 0);
            Assert.AreEqual(0, samples[0].viewportVisibleUnitCenters,
                "Units above this camera are behind it; a y=1 proxy incorrectly counts the elevated center");
            game.GameCamera.transform.position = new Vector3(64, 180, 64);
            Invoke(capture, "AddMemory");
            Assert.AreEqual(1, samples[1].viewportVisibleUnitCenters, "Raising the camera brings the authored center into view");
            capture.Finish("elevated_observation");
            yield return null;
        }

        static T Field<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, PrivateInstance);
            Assert.IsNotNull(field, name);
            return (T)field.GetValue(target);
        }
        static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, PrivateInstance);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }
        static void SetProperty(object target, string name, object value)
        {
            var setter = target.GetType().GetProperty(name)?.GetSetMethod(true);
            Assert.IsNotNull(setter, name);
            setter.Invoke(target, new[] { value });
        }
        static object Invoke(object target, string name)
        {
            var method = target.GetType().GetMethod(name, PrivateInstance);
            Assert.IsNotNull(method, name);
            return method.Invoke(target, null);
        }
    }
}
