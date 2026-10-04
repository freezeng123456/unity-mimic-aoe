using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class WaterPresentation061Tests
    {
        GameObject stage;
        RuleCatalog rules;
        [SetUp] public void Setup(){stage=new GameObject("River art validation 061");rules=Catalog.Load();}
        [TearDown] public void Cleanup(){if(stage!=null)Object.DestroyImmediate(stage);}
        Transform Model(string id)
        {
            var definition=rules.Entity(id);Assert.IsNotNull(definition,"Water rules must include "+id);
            return ProceduralArt.BuildEntity(new EntityState{id=123,def=definition,owner=id=="fish"?-1:0,hp=definition.hp,construction=1},stage.transform,"france",3);
        }
        static Bounds Bounds(Transform root)
        {
            var renderers=root.GetComponentsInChildren<MeshRenderer>();Assert.IsNotEmpty(renderers);
            var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);return bounds;
        }
        [TestCase("dock",4,9500)][TestCase("fishing_boat",2,6500)][TestCase("gunboat",3,6500)][TestCase("fish",2,1400)]
        public void RiverEntitiesUseOwnedBatchedLitGeometryWithoutPhysics(string id,int rendererBudget,int vertexBudget)
        {
            var model=Model(id);Assert.IsEmpty(model.GetComponentsInChildren<Collider>(true));
            var filters=model.GetComponentsInChildren<MeshFilter>(true);Assert.That(filters.Length,Is.InRange(1,rendererBudget));
            Assert.LessOrEqual(filters.Sum(f=>f.sharedMesh.vertexCount),vertexBudget);
            foreach(var filter in filters)
            {
                var mesh=filter.sharedMesh;Assert.AreSame(mesh,filter.GetComponent<ProceduralMeshLifetime>().OwnedMesh);
                Assert.AreEqual(mesh.vertexCount,mesh.uv.Length);Assert.AreEqual(mesh.vertexCount,mesh.normals.Length);Assert.AreEqual(mesh.vertexCount,mesh.tangents.Length);
                foreach(var point in mesh.vertices)Assert.IsTrue(Finite(point.x)&&Finite(point.y)&&Finite(point.z),id+" must not introduce invalid bounds");
                var materialSlots=filter.GetComponent<MeshRenderer>().sharedMaterials;
                Assert.AreEqual(mesh.subMeshCount,materialSlots.Length);Assert.LessOrEqual(materialSlots.Length,8);
                foreach(var material in materialSlots){Assert.IsNotNull(material);Assert.IsNotNull(material.shader);Assert.IsTrue(material.shader.isSupported);}
            }
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        [TestCase("fishing_boat")][TestCase("gunboat")]
        public void LowRiverVesselsFitUnderBridgeAndAcceptExistingAnimationStates(string id)
        {
            var model=Model(id);var bounds=Bounds(model);var rig=model.GetComponent<UnitRig>();Assert.IsNotNull(rig);
            Assert.LessOrEqual(bounds.max.y,WaterPresentation.MaximumBoatHeight);Assert.Less(bounds.max.y+WaterPresentation.WaterLevel,WaterPresentation.BridgeDeckHeight-.28f);
            Assert.Greater(bounds.size.z,2.8f,"Vessel has a pointed hull instead of a humanoid placeholder");Assert.LessOrEqual(bounds.size.x,1.6f);
            Assert.Less(bounds.min.y,0,"Lower hull sits below the waterline");
            foreach(UnitPoseState state in System.Enum.GetValues(typeof(UnitPoseState)))
                Assert.DoesNotThrow(()=>UnitAnimation.Apply(rig,new AnimationPose{state=state,moving=state==UnitPoseState.Move,actionPhase=.5f,locomotionPhase=1,tool=WorkerTool.Basket}),id+" "+state);
            if(id=="gunboat")
            {
                Assert.IsNotNull(rig.Barrel);rig.RestoreInitialPose();var origin=rig.Barrel.localPosition;
                UnitAnimation.Apply(rig,new AnimationPose{state=UnitPoseState.Fire,actionPhase=.5f});
                Assert.Greater(Vector3.Distance(origin,rig.Barrel.localPosition),.09f,"Naval cannon must expose visible shot recoil");
            }
        }
        [Test]
        public void DockFacesWaterWithinItsFootprintAndBuildsInVisibleStages()
        {
            var model=Model("dock");var architecture=model.GetComponent<ArchitectureModel>();Assert.IsNotNull(architecture);
            Assert.AreEqual("dock",architecture.Civilization,"Dock uses one maritime style and must not enter civilization variant rebuilding");
            foreach(float rotation in new[]{0f,90f,180f,270f})
            {
                model.localRotation=Quaternion.Euler(0,rotation,0);var bounds=Bounds(model);
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(bounds.min.x),Mathf.Abs(bounds.max.x)),2.001f);
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(bounds.min.z),Mathf.Abs(bounds.max.z)),2.001f);
            }
            architecture.SetConstruction(.10f);Assert.AreEqual(1,model.GetComponentsInChildren<MeshRenderer>().Length);
            architecture.SetConstruction(.30f);Assert.AreEqual(2,model.GetComponentsInChildren<MeshRenderer>().Length);
            architecture.SetConstruction(.65f);Assert.AreEqual(3,model.GetComponentsInChildren<MeshRenderer>().Length);
            architecture.SetConstruction(1);Assert.AreEqual(4,model.GetComponentsInChildren<MeshRenderer>().Length);
            Assert.IsTrue(model.Find("Dock crane rope cargo and team sign").gameObject.activeSelf);
        }
        [Test]
        public void NonRiverDefinitionsDoNotCreateWaterArtOrAlterGameplayState()
        {
            var entity=new EntityState{id=27,def=rules.Entity("town"),position=new Vector2(20,30),hp=81,owner=0};
            var random=Random.state;Assert.IsFalse(WaterPresentation.TryBuildEntity(stage.transform,entity,Color.blue));
            Assert.AreEqual(0,stage.transform.childCount);Assert.AreEqual(new Vector2(20,30),entity.position);Assert.AreEqual(81,entity.hp);Assert.AreEqual(random,Random.state);
            var fish=new EntityState{id=28,def=rules.Entity("fish"),position=new Vector2(64,21),remaining=1337,hp=77,owner=-1};
            Assert.IsTrue(WaterPresentation.TryBuildEntity(stage.transform,fish,Color.gray));
            Assert.AreEqual(new Vector2(64,21),fish.position);Assert.AreEqual(1337,fish.remaining);Assert.AreEqual(77,fish.hp);Assert.AreEqual(random,Random.state);
        }
        [Test]
        public void RiverSurfaceAndCrossingsUseTheSameBoundariesAndHeightAsNavigation()
        {
            var world=new World(rules,314159,"coast","coast",5,MapTopology.River);
            var river=WaterPresentation.BuildTerrain(stage.transform,world);Assert.IsNotNull(river);
            Assert.IsEmpty(river.GetComponentsInChildren<Collider>());
            var water=river.Find("Continuous navigable river");Assert.IsNotNull(water);
            var waterBounds=water.GetComponent<MeshRenderer>().bounds;
            Assert.AreEqual(MapTopology.RiverMinX,waterBounds.min.x,.0001f);Assert.AreEqual(MapTopology.RiverMaxX,waterBounds.max.x,.0001f);
            Assert.AreEqual(0,waterBounds.min.z,.0001f);Assert.AreEqual(128,waterBounds.max.z,.0001f);Assert.AreEqual(world.SurfaceHeight(new Vector2(64,40),true),waterBounds.center.y,.0001f);
            Assert.AreEqual(2,river.Cast<Transform>().Count(t=>t.name.StartsWith("Raised crossing at ")));
            foreach(var crossing in river.Cast<Transform>().Where(t=>t.name.StartsWith("Raised crossing at ")))
            {
                var mesh=crossing.GetComponentInChildren<MeshFilter>().sharedMesh;Assert.Less(mesh.vertexCount,6000);
                var surfaceVertices=mesh.vertices.Where(p=>Mathf.Abs(p.y-world.SurfaceHeight(new Vector2(p.x,p.z)))<.002f&&p.y>0).ToArray();
                Assert.Greater(surfaceVertices.Length,150,"Deck boards must track both continuous ramp slopes and the raised navigation surface");
                Assert.IsFalse(mesh.vertices.Any(p=>p.x>MapTopology.RiverMinX+.1f&&p.x<MapTopology.RiverMaxX-.1f&&p.y>WaterLevel()+MaximumBoat()&&p.y<2.85f),"No beams may cut into the declared vessel clearance");
            }
            foreach(var filter in river.GetComponentsInChildren<MeshFilter>())Assert.AreSame(filter.sharedMesh,filter.GetComponent<ProceduralMeshLifetime>().OwnedMesh);
        }
        static float WaterLevel()=>WaterPresentation.WaterLevel;
        static float MaximumBoat()=>WaterPresentation.MaximumBoatHeight;
        [Test]
        public void LandMapDoesNotAcquireARiverOrNewTerrainObstacles()
        {
            var world=new World(rules);int obstacles=world.TerrainObstacles.Count;
            Assert.IsNull(WaterPresentation.BuildTerrain(stage.transform,world));Assert.AreEqual(0,stage.transform.childCount);Assert.AreEqual(obstacles,world.TerrainObstacles.Count);
        }
        [UnityTest] public IEnumerator HiddenDockStagesAndVesselMeshesReleaseWithTheirModelRoots()
        {
            var dock=Model("dock");dock.GetComponent<ArchitectureModel>().SetConstruction(.01f);var boat=Model("gunboat");
            var meshes=dock.GetComponentsInChildren<MeshFilter>(true).Concat(boat.GetComponentsInChildren<MeshFilter>(true)).Select(f=>f.sharedMesh).ToArray();
            Object.Destroy(dock.gameObject);Object.Destroy(boat.gameObject);yield return null;yield return null;
            foreach(var mesh in meshes)Assert.IsTrue(mesh==null,"Native mesh lifetime must cover inactive construction sections and vessel cannon children");
            Assert.IsNotNull(stage,"Destroying water entities must not require disposing their whole world");
        }
    }
}
