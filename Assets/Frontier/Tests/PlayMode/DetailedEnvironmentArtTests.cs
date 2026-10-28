using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class DetailedEnvironmentArtTests
    {
        private GameObject root;
        private RuleCatalog rules;
        [SetUp] public void Setup() { root = new GameObject("Detailed environment validation"); rules = Catalog.Load(); }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); }

        private Transform Build(string id, int number = 17)
        {
            var model = new GameObject(id).transform; model.SetParent(root.transform, false);
            Assert.IsTrue(DetailedEnvironment.TryBuild(model, new EntityState { id = number, def = rules.Entity(id), owner = -1, hp = 100 }));
            return model;
        }
        private static Bounds GeometryBounds(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(); Assert.IsNotEmpty(renderers);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        private static Vector3[] Vertices(Transform model) => model.GetComponentsInChildren<MeshFilter>().SelectMany(f => f.sharedMesh.vertices).ToArray();

        [TestCase("tree")][TestCase("mine")][TestCase("berry")][TestCase("deer")]
        [TestCase("crate_food")][TestCase("crate_wood")][TestCase("crate_coin")]
        [TestCase("trade_socket")][TestCase("treasure_coin")][TestCase("treasure_wood")][TestCase("treasure_xp")]
        public void ResourceMeshesHaveFiniteSurfaceGeometryAndAreBatchedWithoutPhysics(string id)
        {
            var model = Build(id);
            Assert.IsEmpty(model.GetComponentsInChildren<Collider>(), "Art must not alter navigation or picking by adding physics.");
            Assert.IsEmpty(model.GetComponentsInChildren<UnitRig>());
            var filters = model.GetComponentsInChildren<MeshFilter>();
            Assert.That(filters.Length, Is.InRange(2, 6), "Static surface details share material batches instead of individual renderers.");
            Assert.LessOrEqual(filters.Sum(f => f.sharedMesh.vertexCount), 7500, "One resource must stay inexpensive enough for a whole map.");
            foreach (var f in filters)
            {
                var mesh = f.sharedMesh;
                Assert.AreSame(mesh, f.GetComponent<ProceduralMeshLifetime>().OwnedMesh, "Native procedural meshes require disposal ownership.");
                var vertices = mesh.vertices; var indices = mesh.triangles;
                Assert.IsNotEmpty(vertices); Assert.IsNotEmpty(indices);
                foreach (var p in vertices)
                    Assert.IsTrue(!float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z));
                for (int i = 0; i < indices.Length; i += 3)
                    Assert.Greater(Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]], vertices[indices[i + 2]] - vertices[indices[i]]).sqrMagnitude,
                        1e-17f, id + "/" + f.name + " has a collapsed triangle");
            }
        }

        [Test]
        public void TreeFormsRemainWithinTheirEstablishedCanopyAndSelectionEnvelope()
        {
            var meshes = new Vector3[3][];
            for (int i = 0; i < 3; i++)
            {
                var model = Build("tree", i); var bounds = GeometryBounds(model); meshes[i] = Vertices(model);
                Assert.IsTrue(bounds.Contains(new Vector3(0, 4.1f, 0)), "Existing near/far tree-crown picking location must survive every form.");
                Assert.LessOrEqual(bounds.max.y, 5.45f); Assert.Greater(bounds.max.y, 4.8f);
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)), 1.86f);
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)), 1.16f);
                Assert.IsNotNull(model.Find("Trunk")); Assert.IsNotNull(model.Find("Crown"));
            }
            Assert.IsFalse(meshes[0].SequenceEqual(meshes[1]));
            Assert.IsFalse(meshes[1].SequenceEqual(meshes[2]));
        }

        [Test]
        public void ResourceVariationIsRepeatableWithoutChangingGameplayOrGlobalRandomState()
        {
            var entity = new EntityState { id = 42, def = rules.Entity("tree"), position = new Vector2(11, 23), remaining = 813, hp = 7, owner = -1 };
            var originalRandom = Random.state;
            var first = new GameObject("first").transform; first.SetParent(root.transform, false);
            Assert.IsTrue(DetailedEnvironment.TryBuild(first, entity));
            Assert.AreEqual(originalRandom, Random.state);
            Assert.AreEqual(new Vector2(11, 23), entity.position); Assert.AreEqual(813, entity.remaining); Assert.AreEqual(7, entity.hp);
            CollectionAssert.AreEqual(Vertices(first), Vertices(Build("tree", 42)));
            Assert.IsFalse(Vertices(first).SequenceEqual(Vertices(Build("tree", 45))), "Trees of the same form also vary their branches.");
        }

        [Test]
        public void MineRetainsVisibleEdgeSelectionAndOreRendersAboveActualStoneFaces()
        {
            var model = Build("mine"); var bounds = GeometryBounds(model);
            Assert.IsTrue(bounds.Contains(new Vector3(1.35f, .6f, .1f)), "Existing near/far mine edge selection must remain valid.");
            Assert.Less(bounds.size.x, 3.2f); Assert.Less(bounds.size.z, 2.3f);
            Assert.IsTrue(model.GetComponentsInChildren<MeshFilter>().Any(f => f.name == "Ore silver quartz seams" && f.sharedMesh.triangles.Length > 0));
        }

        [TestCase("tree", "Forest trunk and branching limbs")]
        [TestCase("crate_wood", "Crate individual weathered boards")]
        public void SolidBatchesHaveOutwardWindingInsteadOfInsideOutFaces(string id, string batch)
        {
            var mesh = Build(id).GetComponentsInChildren<MeshFilter>().Single(f => f.name == batch).sharedMesh;
            var p = mesh.vertices; var t = mesh.triangles; double volume = 0;
            for (int i = 0; i < t.Length; i += 3) volume += Vector3.Dot(p[t[i]], Vector3.Cross(p[t[i + 1]], p[t[i + 2]])) / 6.0;
            Assert.Greater(volume, .01, "Backface culling must expose the exterior of closed tubes and boards.");
        }

        [TestCase("treasure_coin")][TestCase("treasure_wood")][TestCase("treasure_xp")]
        public void ExpeditionCachesKeepTheirOldPickingEnvelopeAndExposeDistinctRewards(string id)
        {
            var model = Build(id); var bounds = GeometryBounds(model);
            Assert.IsTrue(bounds.Contains(new Vector3(0, 1, 0)), "The established treasure click at one metre must remain inside visible geometry bounds.");
            Assert.LessOrEqual(Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)), .655f);
            Assert.LessOrEqual(Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)), .455f);
            Assert.LessOrEqual(bounds.max.y, 2.25f); Assert.GreaterOrEqual(bounds.max.y, 2.1f);
            string reward = id == "treasure_coin" ? "Treasure exposed coins and gold" : id == "treasure_wood" ? "Treasure exposed timber end grain" : "Treasure parchment maps and book pages";
            var cargo = model.GetComponentsInChildren<MeshFilter>().Single(f => f.name == reward).sharedMesh;
            Assert.IsTrue(cargo.vertices.Any(v => v.y > .88f && v.y < 1.30f), "Actual cargo vertices must rise above the rim; the high pennant cannot satisfy this check.");
        }

        [Test]
        public void TradingSiteRetainsOpenConstructionSpaceAndOriginalBoundaryCorners()
        {
            var model = Build("trade_socket"); var bounds = GeometryBounds(model);
            Assert.That(bounds.size.x, Is.InRange(3.59f, 3.79f)); Assert.That(bounds.size.z, Is.InRange(3.59f, 3.79f));
            Assert.LessOrEqual(bounds.max.y, .72f);
            foreach (var vertex in Vertices(model))
                Assert.IsTrue(Mathf.Abs(vertex.x) > 1.5f || Mathf.Abs(vertex.z) > 1.5f, "The site marker must leave the central construction space visibly empty.");
        }

        [TestCase("town")][TestCase("villager")]
        public void UnownedResourceAndGameplayTypesStayWithExistingBuilders(string id)
        {
            Assert.IsFalse(DetailedEnvironment.TryBuild(root.transform, new EntityState { id = 4, def = rules.Entity(id) }));
            Assert.AreEqual(0, root.transform.childCount);
        }
    }
}
