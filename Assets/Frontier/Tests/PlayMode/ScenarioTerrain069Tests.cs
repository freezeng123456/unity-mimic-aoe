using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class ScenarioTerrain069Tests
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        GameController game;
        Camera camera;
        string directory;

        [UnitySetUp] public IEnumerator SetUp()
        {
            Assert.IsTrue(Application.isEditor || Application.isBatchMode);
            directory = Path.Combine(Path.GetTempPath(), "Frontier-terrain-069-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("Isolated authored terrain 069");
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
            root.GetComponent<NativePerformanceCapture>().enabled = false;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>()) canvas.gameObject.SetActive(false);
            var ownedCamera = new GameObject("Owned authored terrain camera");
            ownedCamera.transform.SetParent(root.transform, false);
            camera = ownedCamera.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.aspect = 1;
            camera.orthographicSize = 35;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 300;
            SetProperty(game, "GameCamera", camera);
            SetProperty(game, "InMenu", false);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (game != null) SetProperty(game, "World", null);
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        ScenarioDocument Document(int heightCm = 2000)
        {
            return new ScenarioDocument
            {
                id = "presentation-fixture", name = "Authored terrain fixture",
                rulesHash = SnapshotCodec.RulesHash(game.Rules),
                heightsCm = Enumerable.Repeat(heightCm, 129 * 129).ToArray(),
                starts = new[]
                {
                    new ScenarioStart { player = 0, team = 0, civilization = "coast", town = new Vector2(20, 20),
                        explorer = new Vector2(28, 20), workers = new[] { new Vector2(26, 16) } },
                    new ScenarioStart { player = 1, team = 1, civilization = "river", town = new Vector2(108, 108),
                        explorer = new Vector2(100, 108), workers = new[] { new Vector2(102, 104) } }
                }
            };
        }

        World Bind(ScenarioDocument document)
        {
            Assert.AreEqual("", ScenarioCodec.Validate(document, game.Rules));
            var world = new World(game.Rules, document, new[] { "coast", "river" }, new[] { 0, 1 });
            SetProperty(game, "World", world);
            SetProperty(game, "MatchId", "isolated-terrain-069");
            game.Controls.Selected.Clear();
            game.View.ResetWorld();
            return world;
        }

        Transform Terrain => root.transform.Find("Authored Frontier Terrain");
        Mesh Ground => Terrain.GetComponentsInChildren<MeshFilter>().Single(
            f => f.name == "Continuous sculpted land and river bed").sharedMesh;
        Mesh Fog => Terrain.Find("Three-state visibility surface").GetComponent<MeshFilter>().sharedMesh;
        Vector2 Screen(Vector3 point) => camera.WorldToScreenPoint(point);
        void Aim(Vector3 point, float angle = 58)
        {
            camera.transform.rotation = Quaternion.Euler(angle, 0, 0);
            camera.transform.position = point - camera.transform.forward * 150;
        }
        static void Near(Vector2 actual, Vector2 expected, string reason)
        { Assert.Less(Vector2.Distance(actual, expected), .015f, reason + ": " + actual); }

        [UnityTest] public IEnumerator DryAuthoredHeightsSurfacesAndFogShareTheRealGridAndReleaseItOnReset()
        {
            var document = Document(10000);
            document.heightsCm[1 + 129] = -2000;
            document.heightsCm[2 + 129] = 3250;
            document.surfaces[42 + 70 * 128] = 1;
            document.surfaces[44 + 70 * 128] = 2;
            document.surfaces[46 + 70 * 128] = 3;
            var world = Bind(document);
            Assert.IsFalse(world.Layout.HasWater);
            Assert.IsFalse(Terrain.Find("Ground").GetComponent<MeshRenderer>().enabled);
            var ground = Ground; var fog = Fog;
            Assert.AreEqual(129 * 129, ground.vertexCount);
            Assert.AreEqual(4, ground.subMeshCount);
            Assert.AreEqual(6, ground.GetTriangles(1).Length);
            Assert.AreEqual(6, ground.GetTriangles(2).Length);
            Assert.AreEqual(6, ground.GetTriangles(3).Length);
            Assert.AreEqual(129 * 129, fog.vertexCount, "Fog must cover the authored mountains, rather than remain a flat y=0 quad");
            var vertices = ground.vertices; var fogVertices = fog.vertices; var fogUv = fog.uv;
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.AreEqual(document.heightsCm[i] * .01f, vertices[i].y, .0001f, "Authored height at vertex " + i);
                Assert.AreEqual(vertices[i].y + .08f, fogVertices[i].y, .0001f, "Fog height at vertex " + i);
                Assert.AreEqual(new Vector2(vertices[i].x, vertices[i].z) / 128, fogUv[i]);
            }
            var materials = Terrain.GetComponentsInChildren<MeshFilter>().Single(
                f => f.sharedMesh == ground).GetComponent<MeshRenderer>().sharedMaterials;
            Assert.AreEqual(4, materials.Length);
            Assert.AreNotSame(materials[1], materials[2], "Sand and mud must have distinct visible material tints");
            world.Fog.Reveal(0, new Vector2(64, 64), 40);
            Invoke(game.View, "UpdateFog", world);
            Assert.AreEqual(new Color32(166, 148, 98, 255), (Color32)game.View.Minimap.GetPixel(42, 70));
            Assert.AreEqual(new Color32(99, 82, 59, 255), (Color32)game.View.Minimap.GetPixel(44, 70));
            Assert.AreEqual(new Color32(108, 115, 116, 255), (Color32)game.View.Minimap.GetPixel(46, 70));
            Assert.AreEqual(new Color32(87, 103, 57, 255), (Color32)game.View.Minimap.GetPixel(48, 70));

            SetProperty(game, "World", new World(game.Rules, 314159, "coast", "river"));
            game.View.ResetWorld();
            Assert.AreEqual(4, Fog.vertexCount, "A built-in map restores its original quad");
            Assert.IsNull(Terrain.Find("Three-state visibility surface").GetComponent<ProceduralMeshLifetime>().OwnedMesh);
            yield return null;
            Assert.IsTrue(fog == null, "Replacing the scenario releases its owned fog mesh");
            Assert.IsTrue(ground == null, "Replacing the scenario releases its owned terrain mesh");
        }

        [UnityTest] public IEnumerator GroundPickingCoversBothAuthoredHeightExtremesAndFirstSteepFace()
        {
            foreach (int heightCm in new[] { -2000, 10000 })
            {
                var world = Bind(Document(heightCm));
                var target = new Vector2(66.25f, 63.75f);
                Aim(new Vector3(target.x, world.Layout.BaseGroundHeight(target), target.y));
                Near(game.Controls.GroundPoint(Screen(new Vector3(target.x, heightCm * .01f, target.y))), target,
                    "Ground point at the authored " + heightCm + "cm limit");
                yield return null;
            }

            // A sharp ridge is only one grid cell wide. Its sloping face must be
            // the first intersection even though the y=0 projection is behind it.
            var ridge = Document(0);
            for (int z = 0; z < 129; z++) ridge.heightsCm[64 + z * 129] = 10000;
            Bind(ridge);
            var facePoint = new Vector3(63.6f, 60, 64);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(.35f, -1, .2f));
            camera.transform.position = facePoint - camera.transform.forward * 150;
            Near(game.Controls.GroundPoint(Screen(facePoint)), new Vector2(facePoint.x, facePoint.z),
                "The narrow rendered ridge face must beat the terrain farther behind it");
            yield return null;
        }

        [UnityTest] public IEnumerator NormalFocusKeeps100MetreAuthoredGroundVisibleAndRetainsTheBuiltInCameraHeight()
        {
            var world = Bind(Document(10000));
            var target = new Vector2(64, 64);
            game.Controls.Focus(target);
            Assert.AreEqual(150, camera.transform.position.y, .0001f,
                "Normal playtest focus must place the camera above the authored plateau");
            var point = new Vector3(target.x, world.SurfaceHeight(target), target.y);
            var viewport = camera.WorldToViewportPoint(point);
            Assert.Greater(viewport.z, 0);
            Assert.That(viewport.x, Is.InRange(0, 1));
            Assert.That(viewport.y, Is.InRange(0, 1));
            Near(game.Controls.GroundPoint(Screen(point)), target, "Ground picking after the real Focus route");
            yield return null;

            SetProperty(game, "World", new World(game.Rules, 314159, "coast", "river"));
            game.View.ResetWorld();
            game.Controls.Focus(target);
            Assert.AreEqual(50, camera.transform.position.y, .0001f, "Built-in maps retain their original camera baseline");
            Near(game.Controls.GroundPoint(Screen(new Vector3(64, 0, 64))), target, "Built-in ground picking remains unchanged");
            yield return null;
        }

        [UnityTest] public IEnumerator ElevatedResourceUsesItsAuthoredHeightForVisibleGeometryAndPicking()
        {
            var document = Document(10000);
            document.resources = new[] { new ScenarioPlacement { id = "hill-tree", definition = "tree", position = new Vector2(64, 64) } };
            var world = Bind(document);
            var resource = world.ScenarioEntity("hill-tree");
            Assert.IsNotNull(resource);
            world.Fog.Reveal(0, resource.position, 12);
            Aim(new Vector3(64, 103, 64), 80);
            Invoke(game.View, "LateUpdate");
            var visual = root.transform.Find("Simulation Entity Views/Entity " + resource.id);
            Assert.IsNotNull(visual);
            Assert.AreEqual(100, visual.position.y, .0001f);
            Assert.IsTrue(game.View.TryGetPickBounds(resource.id, out var bounds));
            Assert.Greater(bounds.max.y, 102, "The resource crown is above its elevated ground");
            Assert.GreaterOrEqual(bounds.min.y, 99.9f);
            var selected = (EntityState)Invoke(game.Controls, "Pick", Screen(bounds.center), false, false, false);
            Assert.AreEqual(resource.id, selected?.id, "The actual visible resource envelope can be selected at 100m elevation");
            yield return null;
        }

        [UnityTest] public IEnumerator RiverBedBridgeRampEndpointsAndBoatPickingUseTheirSeparateAuthoredHeights()
        {
            var document = Document();
            document.waterHeightCm = 1000;
            for (int z = 0; z < 129; z++) for (int x = 0; x < 129; x++)
                document.heightsCm[x + z * 129] = Mathf.RoundToInt(Mathf.Lerp(2000, 4000, Mathf.Clamp01((x - 40) / 48f)));
            document.rivers = new[] { new ScenarioRiver { id = "stream", smooth = false,
                points = new[] { new Vector2(64, -16), new Vector2(64, 144) }, halfWidths = new[] { 6f, 6f } } };
            document.bridges = new[] { new ScenarioBridge { id = "crossing", center = new Vector2(64, 64),
                direction = Vector2.right, halfLength = 9, halfWidth = 5, rampLength = 9, height = 50 } };
            document.objects = new[] { new ScenarioPlacement { id = "fixture-ship", definition = "transport_ship", owner = 0, position = new Vector2(64, 54) } };
            var world = Bind(document); var span = world.Layout.Bridges.Single();
            Assert.AreEqual(22.5f, span.WestHeight, .001f);
            Assert.AreEqual(37.5f, span.EastHeight, .001f);
            Assert.AreEqual(50, world.SurfaceHeight(span.Center));
            Assert.AreEqual(10, world.SurfaceHeight(span.Center, true));
            Assert.Less(Ground.vertices[64 + 64 * 129].y, 10, "A real submerged bed remains beneath the elevated crossing");
            Assert.AreEqual(10.08f, Fog.vertices[64 + 64 * 129].y, .0001f, "Ground fog follows the visible water under the separately fogged deck");
            var water = Terrain.GetComponentsInChildren<MeshFilter>().Single(f => f.name == "Continuous navigable river").sharedMesh;
            Assert.IsTrue(water.vertices.All(v => Mathf.Abs(v.y - 10) < .0001f));
            var bridge = Terrain.GetComponentsInChildren<MeshFilter>().Single(f => f.name == "Batched valley crossing with clear water channel");
            var bridgeVertices = bridge.sharedMesh.vertices.Select(bridge.transform.TransformPoint).ToArray();
            foreach (float sign in new[] { -1f, 1f })
            {
                var mouth = span.Point(sign * (span.HalfLength + span.RampLength), 0);
                float height = world.Layout.BaseGroundHeight(mouth);
                Assert.AreEqual(height, world.SurfaceHeight(mouth), .001f);
                Assert.IsTrue(bridgeVertices.Any(v => Mathf.Abs(v.x - mouth.x) < .05f && Mathf.Abs(v.y - height) < .15f),
                    "The visible ramp reaches its authored bank height on side " + sign);
            }
            var target = new Vector3(64, 10, 64); Aim(target, 85);
            var screen = Screen(target);
            var landPoint = game.Controls.GroundPoint(screen);
            Assert.IsTrue(span.Approach(landPoint));
            Assert.Greater(Vector2.Distance(landPoint, new Vector2(64, 64)), 2,
                "Land selection resolves the closer deck rather than the water behind it");
            var ship = world.ScenarioEntity("fixture-ship");
            game.Controls.Selected.Add(ship.id);
            Near(game.Controls.GroundPoint(screen), new Vector2(64, 64), "Boat selection targets water beneath the bridge");
            yield return null;
        }

        static void SetField(object target, string name, object value)
        { target.GetType().GetField(name, PrivateInstance).SetValue(target, value); }
        static void SetProperty(object target, string name, object value)
        { target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value }); }
        static object Invoke(object target, string name, params object[] arguments)
        { return target.GetType().GetMethod(name, PrivateInstance).Invoke(target, arguments); }
    }
}
