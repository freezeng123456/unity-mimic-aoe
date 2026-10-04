using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class CurvedRiverPresentation063Tests
    {
        GameObject stage;
        [SetUp]public void Setup(){stage=new GameObject("Curved river terrain validation 063");}
        [TearDown]public void Cleanup(){if(stage!=null)Object.DestroyImmediate(stage);}
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);

        [TestCase(MapTopology.Winding)] [TestCase(MapTopology.Tropical)]
        [TestCase(MapTopology.Tributaries)] [TestCase(MapTopology.Estuary)]
        public void SculptedGroundRetainsSubmergedBedAndWaterMeshesFollowAllChannels(string map)
        {
            var world=new World(Catalog.Load(),45,"coast","france",5,map);
            var root=WaterPresentation.BuildTerrain(stage.transform,world);Assert.IsNotNull(root);
            Assert.IsEmpty(root.GetComponentsInChildren<Collider>());
            var ground=root.Find("Continuous sculpted land and river bed").GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(129*129,ground.vertexCount);Assert.LessOrEqual(ground.bounds.max.y,.001f);
            Assert.AreEqual(1,ground.subMeshCount,"Shore layers must blend on one surface rather than making hard cell-sized material boundaries");
            Assert.AreEqual(ground.vertexCount,ground.colors.Length);
            Assert.IsTrue(ground.colors.Any(c=>c.r>.1f&&c.r<.9f&&c.g>.05f),"Mud and dry sand overlap continuously instead of ending in a grid staircase");
            foreach(var weights in ground.colors)
            {
                Assert.AreEqual(1,weights.r+weights.g+weights.b,.0001f);
                Assert.That(weights.r,Is.InRange(0,1));Assert.That(weights.g,Is.InRange(0,1));Assert.That(weights.b,Is.InRange(0,1));
            }
            var groundMaterial=root.Find("Continuous sculpted land and river bed").GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreEqual("Frontier/River Ground",groundMaterial.shader.name);Assert.IsTrue(groundMaterial.shader.isSupported);
            Assert.Less(ground.bounds.min.y,-.89f,"The bed remains below vessels even under elevated bridges");
            Assert.AreEqual(-.4f,world.Layout.WaterHeight,.0001f,"The valley exposes real banks above its lowered river level");
            foreach(var p in ground.vertices)Assert.AreEqual(world.Layout.BankHeight(new Vector2(p.x,p.z)),p.y,.0001f);
            var surfaces=root.Cast<Transform>().Where(t=>t.name=="Continuous navigable river"||t.name.StartsWith("Navigable tributary")).ToArray();
            Assert.AreEqual(1,surfaces.Length,"Adjoining channels must have one union surface instead of coplanar overlapping renderers");
            foreach(var surface in surfaces)
            {
                var mesh=surface.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(mesh.vertexCount,mesh.uv.Length);Assert.AreEqual(mesh.vertexCount,mesh.colors.Length);
                foreach(var p in mesh.vertices)
                {
                    Assert.IsTrue(Finite(p.x)&&Finite(p.y)&&Finite(p.z));
                    Assert.That(p.x,Is.InRange(0,128));Assert.That(p.z,Is.InRange(0,128));
                    Assert.AreEqual(world.SurfaceHeight(new Vector2(p.x,p.z),true),p.y,.0001f);
                }
                var points=mesh.vertices;var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                    Assert.Greater(Vector3.Cross(points[triangles[i+1]]-points[triangles[i]],points[triangles[i+2]]-points[triangles[i]]).sqrMagnitude,.00000009f,"Boundary clipping must not introduce zero-area faces");
            }
            var crossings=root.Cast<Transform>().Where(t=>t.name.StartsWith("Raised crossing")).ToArray();
            Assert.AreEqual(world.Layout.Bridges.Length,crossings.Length);
            for(int i=0;i<crossings.Length;i++)
            {
                var span=world.Layout.Bridges[i];var crossing=crossings[i];
                Assert.AreEqual(span.Center.x,crossing.position.x,.0001f);Assert.AreEqual(span.Center.y,crossing.position.z,.0001f);
                Assert.Less(Vector2.Distance(span.Direction,new Vector2(crossing.right.x,crossing.right.z)),.0001f);
                var mesh=crossing.GetComponentInChildren<MeshFilter>().sharedMesh;
                int aligned=0;
                foreach(var local in mesh.vertices)
                {
                    var p=crossing.TransformPoint(local);var point=new Vector2(p.x,p.z);
                    if(local.y>.05f&&Mathf.Abs(local.y-world.SurfaceHeight(point))<.004f)aligned++;
                    if(Mathf.Abs(local.x)<span.HalfLength-.1f)
                        Assert.IsFalse(local.y>world.Layout.WaterHeight&&local.y<world.Layout.WaterHeight+WaterPresentation.MaximumBoatHeight,"Underpass must retain boat clearance");
                }
                Assert.Greater(aligned,120,"Rotated deck boards track navigation heights at the ends and approaches");
            }
        }

        [TestCase("fishing_boat")] [TestCase("gunboat")]
        public void BoatMotionHasVisibleGearAndKeepsSimulationStateAndHullEnvelope(string id)
        {
            var definition=Catalog.Load().Entity(id);
            var entity=new EntityState{id=918,def=definition,position=new Vector2(70,60),owner=0,hp=definition.hp,construction=1};
            var model=ProceduralArt.BuildEntity(entity,stage.transform,"coast",3);var motion=model.GetComponent<BoatPresentationMotion>();
            Assert.IsNotNull(motion);Assert.IsNotNull(motion.SternOar);Assert.IsNotNull(motion.Rudder);Assert.IsNotNull(motion.Wake);
            var oldRotation=motion.SternOar.localRotation;var entityPosition=entity.position;float radius=definition.radius;
            motion.Apply(new AnimationPose{state=UnitPoseState.Move,moving=true,locomotionPhase=.7f},12);
            Assert.IsTrue(motion.Wake.enabled);Assert.Greater(Quaternion.Angle(oldRotation,motion.SternOar.localRotation),2);
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>().Where(r=>r!=motion.Wake))
            {Assert.LessOrEqual(renderer.bounds.max.y,WaterPresentation.MaximumBoatHeight);Assert.LessOrEqual(renderer.bounds.size.x,1.6f);}
            motion.Apply(new AnimationPose{state=UnitPoseState.Idle},12);Assert.IsFalse(motion.Wake.enabled);
            Assert.Less(Quaternion.Angle(oldRotation,motion.SternOar.localRotation),.001f);
            if(id=="fishing_boat")
            {
                Assert.IsNotNull(motion.FishingGear);var start=motion.FishingGear.localPosition;
                motion.Apply(new AnimationPose{state=UnitPoseState.Gather,actionPhase=.25f},13);
                Assert.Greater(Vector3.Distance(start,motion.FishingGear.localPosition),.08f);
            }
            Assert.AreEqual(entityPosition,entity.position);Assert.AreEqual(radius,definition.radius);
        }

        [Test]public void TropicalBanksHaveBoundedPalmBatchesThatRespectFogAndNewConstruction()
        {
            var world=new World(Catalog.Load(),71,"coast","france",5,MapTopology.Tropical);
            int entityCount=world.Entities.Count,treeCount=world.Entities.Count(e=>e.def.id=="tree"),landVersion=world.Navigation.Version;
            var tropical=WaterPresentation.BuildTerrain(stage.transform,world);
            var palms=tropical.Cast<Transform>().Where(t=>t.name.StartsWith("Tropical palm and broadleaf cluster")).ToArray();
            Assert.That(palms.Length,Is.InRange(1,8));
            Assert.AreEqual(entityCount,world.Entities.Count);Assert.AreEqual(treeCount,world.Entities.Count(e=>e.def.id=="tree"));
            Assert.AreEqual(landVersion,world.Navigation.Version,"Decorative foliage cannot add gameplay obstacles or resource instances");
            foreach(var palm in palms)
            {
                var mesh=palm.GetComponent<MeshFilter>().sharedMesh;Assert.LessOrEqual(mesh.vertexCount,1000);
                Assert.AreEqual(3,mesh.subMeshCount);Assert.LessOrEqual(palm.GetComponent<MeshRenderer>().sharedMaterials.Length,8);
                Assert.Greater(mesh.bounds.size.y,2.7f);Assert.IsEmpty(palm.GetComponentsInChildren<Collider>());
                Vector2 point=new Vector2(palm.position.x,palm.position.z);
                Assert.IsFalse(world.IsWater(point));Assert.IsFalse(world.IsBridgeApproach(point));
                if(world.Fog.At(0,point)==VisibilityState.Unexplored)Assert.IsFalse(palm.GetComponent<MeshRenderer>().enabled,"A high canopy must not reveal an unexplored bank over the flat fog plane");
            }
            var scenery=tropical.GetComponent<RiverSceneryVisibility>();Assert.AreEqual(palms.Length,scenery.ClusterCount);
            var first=palms[0];Vector2 center=new Vector2(first.position.x,first.position.z);
            world.Fog.Reveal(0,center,5);scenery.Refresh();Assert.IsTrue(first.GetComponent<MeshRenderer>().enabled);
            var building=world.Spawn("house",0,center);scenery.Refresh(true);
            Assert.IsFalse(first.GetComponent<MeshRenderer>().enabled,"A later player building must not intersect a decorative tree cluster");
            building.hp=0;scenery.Refresh(true);Assert.IsTrue(first.GetComponent<MeshRenderer>().enabled);
            var temperate=new World(Catalog.Load(),71,"coast","france",5,MapTopology.Winding);
            var valley=WaterPresentation.BuildTerrain(stage.transform,temperate);
            Assert.IsFalse(valley.Cast<Transform>().Any(t=>t.name.StartsWith("Tropical palm")),"The warm river bay owns its distinct palm silhouette");
        }

        [Test]public void TributaryConfluenceHasOneWaterLayerAndNoFakeInternalShallows()
        {
            var world=new World(Catalog.Load(),25,"coast","france",5,MapTopology.Tributaries);
            var root=WaterPresentation.BuildTerrain(stage.transform,world);
            var surface=root.Find("Continuous navigable river").GetComponent<MeshFilter>().sharedMesh;
            Assert.LessOrEqual(surface.vertexCount,20000,"Union triangulation shares grid and shoreline vertices instead of triplicating every water face");
            Assert.AreEqual(surface.vertexCount,surface.uv2.Length);
            Vector2 junction=world.Layout.Channels[1].Centers.Last(),sample=junction+new Vector2(.137f,.219f);
            var vertices=surface.vertices;var triangles=surface.triangles;int covering=0;bool deepAtJoin=false;
            float Side(Vector2 a,Vector2 b,Vector2 p)=>(b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x);
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 aa=vertices[triangles[i]],bb=vertices[triangles[i+1]],cc=vertices[triangles[i+2]];
                Vector2 a=new Vector2(aa.x,aa.z),b=new Vector2(bb.x,bb.z),c=new Vector2(cc.x,cc.z);
                float sa=Side(a,b,sample),sb=Side(b,c,sample),sc=Side(c,a,sample);
                if(sa>.000001f&&sb>.000001f&&sc>.000001f||sa<-.000001f&&sb<-.000001f&&sc<-.000001f)covering++;
            }
            for(int i=0;i<vertices.Length;i++)
                if((new Vector2(vertices[i].x,vertices[i].z)-junction).sqrMagnitude<1&&surface.colors[i].r>.95f)deepAtJoin=true;
            Assert.AreEqual(1,covering,"The mouth must have exactly one rendered water face, preventing coplanar z-fighting");
            Assert.IsTrue(deepAtJoin,"The end of the tributary is deep water within the union, not a white painted bank");
        }

        [UnityTest]public IEnumerator GroundCrossingsAndAnimatedBoatMeshesReleaseWhenRemoved()
        {
            var world=new World(Catalog.Load(),23,"coast","coast",5,MapTopology.Tributaries);
            var terrain=WaterPresentation.BuildTerrain(stage.transform,world);
            var entity=new EntityState{id=43,def=world.Rules.Entity("fishing_boat"),owner=0,hp=100,construction=1};
            var boat=ProceduralArt.BuildEntity(entity,stage.transform,"coast",3);
            var meshes=terrain.GetComponentsInChildren<MeshFilter>(true).Concat(boat.GetComponentsInChildren<MeshFilter>(true)).Select(f=>f.sharedMesh).ToArray();
            foreach(var filter in terrain.GetComponentsInChildren<MeshFilter>(true))Assert.AreSame(filter.sharedMesh,filter.GetComponent<ProceduralMeshLifetime>().OwnedMesh);
            Object.Destroy(terrain.gameObject);Object.Destroy(boat.gameObject);yield return null;yield return null;
            foreach(var mesh in meshes)Assert.IsTrue(mesh==null,"Deleting/replacing a map must also release ground, current, reeds, deck and gear native meshes");
        }
    }
}
