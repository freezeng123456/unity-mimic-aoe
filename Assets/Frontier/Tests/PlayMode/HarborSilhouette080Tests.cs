using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Proposed-current080 acceptance only: import AFTER the art change, not into
    // the old-art render baseline. Controlled model build, not paid/native play.
    public sealed class HarborSilhouette080Tests
    {
        const float Epsilon=.00005f;
        GameObject stage;World world;RuleCatalog rules;string worldBefore,rulesBefore;
        readonly List<EntityState> entities=new List<EntityState>();
        readonly List<string> entitiesBefore=new List<string>();
        struct Face {public Vector3 a,b,c;public Material material;}
        [SetUp] public void SetUp()
        {
            stage=new GameObject("080 actual maritime geometry contract");rules=Catalog.Load();
            world=new World(rules,42,"coast","ottoman",5,MapTopology.Winding);
            worldBefore=JsonUtility.ToJson(world.CaptureState());rulesBefore=JsonUtility.ToJson(rules);
        }
        [TearDown] public void TearDown()
        {
            try
            {
                Assert.AreEqual(worldBefore,JsonUtility.ToJson(world.CaptureState()),"Art/poses must not change world, fog, navigation, resources, orders or events");
                Assert.AreEqual(rulesBefore,JsonUtility.ToJson(rules),"Art must not change radii, rules or transport capacity");
                for(int i=0;i<entities.Count;i++)Assert.AreEqual(entitiesBefore[i],JsonUtility.ToJson(entities[i]),"Art must not mutate its supplied entity");
            }
            finally{if(stage!=null)UnityEngine.Object.DestroyImmediate(stage);entities.Clear();entitiesBefore.Clear();}
        }
        Transform Model(string id)
        {
            var d=rules.Entity(id);Assert.IsNotNull(d);
            var e=new EntityState{id=9000+entities.Count,def=d,owner=0,hp=d.hp,construction=1,position=new Vector2(48,64)};
            entities.Add(e);entitiesBefore.Add(JsonUtility.ToJson(e));
            return ProceduralArt.BuildEntity(e,stage.transform,"coast",2);
        }
        static IEnumerable<MeshFilter> SolidFilters(Transform root)
        {
            var wake=root.GetComponent<BoatPresentationMotion>()?.Wake;
            return root.GetComponentsInChildren<MeshFilter>(true).Where(f=>wake==null||f.GetComponent<MeshRenderer>()!=wake);
        }
        static Vector3 Local(Transform root,MeshFilter filter,Vector3 p)=>root.InverseTransformPoint(filter.transform.TransformPoint(p));
        static Vector3[] Vertices(Transform root)=>SolidFilters(root).SelectMany(f=>f.sharedMesh.vertices.Select(p=>Local(root,f,p))).ToArray();
        static Bounds BoundsOf(IEnumerable<Vector3> points)
        {
            var a=points.ToArray();Assert.IsNotEmpty(a);var b=new Bounds(a[0],Vector3.zero);foreach(var p in a)b.Encapsulate(p);return b;
        }
        static bool Near(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<=Epsilon;
        static bool Inside(Bounds b,Vector3 p)=>p.x>=b.min.x-Epsilon&&p.x<=b.max.x+Epsilon&&p.y>=b.min.y-Epsilon&&p.y<=b.max.y+Epsilon&&p.z>=b.min.z-Epsilon&&p.z<=b.max.z+Epsilon;
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        static IEnumerable<Face> Faces(Transform root,Material only=null)
        {
            foreach(var f in SolidFilters(root))
            {
                var m=f.sharedMesh;var v=m.vertices;var materials=f.GetComponent<MeshRenderer>().sharedMaterials;
                for(int s=0;s<m.subMeshCount;s++)
                {
                    if(only!=null&&materials[s]!=only)continue;Assert.AreEqual(MeshTopology.Triangles,m.GetTopology(s));
                    var t=m.GetTriangles(s);Assert.AreEqual(0,t.Length%3);
                    for(int i=0;i<t.Length;i+=3)yield return new Face{a=Local(root,f,v[t[i]]),b=Local(root,f,v[t[i+1]]),c=Local(root,f,v[t[i+2]]),material=materials[s]};
                }
            }
        }
        static float[] Crossings(IEnumerable<Face> faces,Ray ray,float limit)
        {
            // Two-sided Moller-Trumbore against real indexed triangle surfaces,
            // then merge shared edges/double-sided cloth; no physics or labels.
            var distances=new List<float>();
            foreach(var f in faces)
            {
                var e1=f.b-f.a;var e2=f.c-f.a;var p=Vector3.Cross(ray.direction,e2);float det=Vector3.Dot(e1,p);
                if(Mathf.Abs(det)<.000001f)continue;float inv=1/det;var offset=ray.origin-f.a;
                float u=Vector3.Dot(offset,p)*inv;if(u<-.00001f||u>1.00001f)continue;
                var q=Vector3.Cross(offset,e1);float v=Vector3.Dot(ray.direction,q)*inv;if(v<-.00001f||u+v>1.00001f)continue;
                float d=Vector3.Dot(e2,q)*inv;if(d>Epsilon&&d<=limit)distances.Add(d);
            }
            distances.Sort();var merged=new List<float>();foreach(float d in distances)if(merged.Count==0||d-merged[merged.Count-1]>.0001f)merged.Add(d);return merged.ToArray();
        }
        static void MeshContract(Transform model,int renderers,int vertexBudget)
        {
            Assert.IsEmpty(model.GetComponentsInChildren<Collider>(true));var filters=model.GetComponentsInChildren<MeshFilter>(true);
            Assert.AreEqual(renderers,filters.Length);Assert.AreEqual(renderers,model.GetComponentsInChildren<MeshRenderer>(true).Length);
            Assert.LessOrEqual(filters.Sum(f=>f.sharedMesh.vertexCount),vertexBudget);
            foreach(var f in filters)
            {
                var m=f.sharedMesh;Assert.AreSame(m,f.GetComponent<ProceduralMeshLifetime>().OwnedMesh);
                Assert.AreEqual(m.vertexCount,m.uv.Length);Assert.AreEqual(m.vertexCount,m.normals.Length);Assert.AreEqual(m.vertexCount,m.tangents.Length);
                Assert.IsTrue(m.vertices.All(p=>Finite(p.x)&&Finite(p.y)&&Finite(p.z)));Assert.IsTrue(m.normals.All(p=>Finite(p.x)&&Finite(p.y)&&Finite(p.z)));
                var materials=f.GetComponent<MeshRenderer>().sharedMaterials;Assert.AreEqual(m.subMeshCount,materials.Length);Assert.LessOrEqual(materials.Length,8);
                foreach(var material in materials){Assert.IsNotNull(material);Assert.IsNotNull(material.shader);Assert.IsTrue(material.shader.isSupported);}
            }
            TestContext.WriteLine("Actual080 mesh budget: renderers="+filters.Length+" vertices="+filters.Sum(f=>f.sharedMesh.vertexCount));
        }
        [Test] public void DockRealMeshBoundsAndShoreFootprintEncloseAllFourVisibleStages()
        {
            var model=Model("dock");MeshContract(model,4,11500);var a=model.GetComponent<ArchitectureModel>();Assert.IsNotNull(a);
            var actual=BoundsOf(Vertices(model));Assert.IsTrue(Near(actual.center,a.LocalBounds.center));Assert.IsTrue(Near(actual.size,a.LocalBounds.size));
            Assert.That(actual.max.y,Is.InRange(2.85f,2.90f));Assert.Less(actual.min.y,-1.04f,"Declared bounds include real submerged piles");
            var boom=Vertices(model).Where(p=>p.x>2.5f&&p.y>2.3f&&p.z<-.8f).ToArray();Assert.IsNotEmpty(boom);Assert.Greater(boom.Max(p=>p.x),3.30f);
            var foundation=model.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.transform).Single(t=>t!=a.Walls&&t!=a.Roofs&&t!=a.Details);
            foreach(float angle in new[]{0f,90f,180f,270f})
            {
                model.localRotation=Quaternion.Euler(0,angle,0);
                for(int i=0;i<5;i++)
                {
                    float progress=new[]{.10f,.30f,.65f,.85f,1f}[i];int visible=new[]{1,2,3,4,4}[i];a.SetConstruction(progress);
                    Assert.IsTrue(foundation.gameObject.activeInHierarchy);Assert.AreEqual(visible,model.GetComponentsInChildren<MeshRenderer>().Length);
                    foreach(var p in Vertices(model))
                    {
                        Assert.IsTrue(Inside(a.LocalBounds,p),"Every active or hidden construction section is in actual local bounds");
                        bool shore=p.x>=-2.001f&&p.x<=2.001f&&Mathf.Abs(p.z)<=2.001f;
                        bool quay=p.x>=2&&p.x<=World.DockQuayLength+.001f&&Mathf.Abs(p.z)<=World.DockQuayHalfWidth+.001f;
                        Assert.IsTrue(shore||quay,"Real rotated dock must remain inside the unchanged authoritative footprint");
                    }
                }
            }
            a.SetConstruction(1);TestContext.WriteLine("Actual080 Dock bounds="+actual);
        }
        [Test] public void TransportBothSideBaysAreSurfaceOpenAndCrewIsBelowTheShallowValance()
        {
            var model=Model("transport_ship");MeshContract(model,3,6500);Assert.AreEqual(14,rules.Entity("transport_ship").transportCapacity);
            var faces=Faces(model).ToArray();var canvas=ArchitecturalMaterials.Get(ArchitecturalMaterials.Surface.Cloth,new Color(.84f,.80f,.66f));
            var canvasFaces=Faces(model,canvas).ToArray();Assert.IsNotEmpty(canvasFaces);
            foreach(float z in new[]{-1.04f,-.25f})
            {
                foreach(float y in new[]{.80f,.98f,1.17f})Assert.IsEmpty(Crossings(faces,new Ray(new Vector3(-2,y,z),Vector3.right),4),"Port and starboard bays must be open in actual indexed surfaces, not just lack selected vertices");
                foreach(float x in new[]{-.30f,.30f})
                {
                    var hits=Crossings(canvasFaces,new Ray(new Vector3(x,1.12f,z),Vector3.up),1);
                    Assert.AreEqual(1,hits.Length,"A real two-sided pitched canvas roof is a positive surface-oracle control");
                    Assert.AreEqual(1.72f-Mathf.Abs(x)/.61f*.27f,1.12f+hits[0],.0002f);
                }
            }
            // Skin and hat are identified by existing shared materials/spatial
            // region, not named production children or new test-only hooks.
            var skin=ArchitecturalMaterials.Get(ArchitecturalMaterials.Surface.Cloth,new Color(.65f,.46f,.30f));
            var aftSkin=Faces(model,skin).SelectMany(f=>new[]{f.a,f.b,f.c}).Where(p=>p.z>-.90f&&p.z<-.35f&&p.x>.08f&&p.x<.5f).ToArray();Assert.IsNotEmpty(aftSkin);
            Assert.AreEqual(1.22f,aftSkin.Max(p=>p.y),.0002f);
            var hats=canvasFaces.SelectMany(f=>new[]{f.a,f.b,f.c}).Where(p=>p.y>1.16f&&p.y<1.30f&&p.z>-.90f&&p.z<-.58f&&p.x>.12f&&p.x<.43f).ToArray();Assert.IsNotEmpty(hats);
            Assert.AreEqual(1.255f,hats.Max(p=>p.y),.0002f);Assert.Less(hats.Max(p=>p.y),1.31f,"Crew stays below the actual valance bottom and clear of the roof");
            Assert.IsNull(model.GetComponent<UnitRig>().Barrel,"Transport remains a carrier, not a fighting-ship rig");
        }
        [Test] public void FrigateContainsTwoBroadClosedLowCanvasStationsAndTheOriginalBroadsideRig()
        {
            var model=Model("frigate");MeshContract(model,4,6500);var rig=model.GetComponent<UnitRig>();Assert.IsNotNull(rig.Barrel);Assert.AreEqual(.10f,rig.ArtilleryRecoil,Epsilon);
            var canvas=ArchitecturalMaterials.Get(ArchitecturalMaterials.Surface.Cloth,new Color(.84f,.80f,.66f));var faces=Faces(model,canvas).ToArray();Assert.IsNotEmpty(faces);
            var vertices=faces.SelectMany(f=>new[]{f.a,f.b,f.c}).ToArray();
            foreach(float station in new[]{-.9f,.84f})
            {
                foreach(float y in new[]{1.44f,1.60f,1.78f})foreach(float x in new[]{-.4f,0f,.4f})
                {
                    var hits=Crossings(faces,new Ray(new Vector3(x,y,station-.5f),Vector3.forward),.95f);
                    Assert.AreEqual(2,hits.Length,"Each broad folded mass needs actual front and back surfaces across width and height");
                    Assert.AreEqual(.08f,hits[1]-hits[0],.0002f);
                }
                foreach(var point in new[]{new Vector3(-.64f,1.84f,station-.04f),new Vector3(.64f,1.84f,station-.04f),new Vector3(-.48f,1.38f,station+.26f),new Vector3(.48f,1.38f,station+.34f)})Assert.IsTrue(vertices.Any(v=>Near(v,point)),"Real broad-canvas corner missing");
            }
        }
        static Vector3 RingPoint(int i,float y,float scale,float width,float length)
        {float a=i*Mathf.PI*2/16,cos=Mathf.Cos(a);return new Vector3(Mathf.Sin(a)*width*(cos>0?.76f:1)*scale,y,cos*length*scale);}
        static bool OriginalHullRings(MeshFilter filter,float width,float length)
        {
            var v=filter.sharedMesh.vertices;if(v.Length<256)return false;int cursor=0;
            for(int ring=0;ring<4;ring++)for(int i=0;i<16;i++)
            {
                float low=-.12f+ring*.125f,high=low+.119f,lo=.58f+ring*.105f,hi=lo+.105f;
                foreach(var p in new[]{RingPoint(i+1,low,lo,width,length),RingPoint(i+1,high,hi,width,length),RingPoint(i,high,hi,width,length),RingPoint(i,low,lo,width,length)})if(!Near(p,v[cursor++]))return false;
            }
            return true;
        }
        [TestCase("fishing_boat",6,.63f,1.64f,.65f)] [TestCase("gunboat",6,.76f,1.92f,.90f)]
        [TestCase("transport_ship",3,.92f,2.18f,1.05f)] [TestCase("frigate",4,1.08f,2.52f,1.25f)] [TestCase("monitor",4,1.03f,2.35f,1.20f)]
        public void EveryVesselPosePhaseRetainsOriginalHullRingsRulesAndBridgeClearance(string id,int renderers,float width,float length,float radius)
        {
            var model=Model(id);MeshContract(model,renderers,6500);var rig=model.GetComponent<UnitRig>();var motion=model.GetComponent<BoatPresentationMotion>();
            Assert.IsNotNull(rig);Assert.IsNotNull(motion);Assert.IsNotNull(motion.Wake);Assert.AreEqual(length,motion.HullLength,Epsilon);Assert.AreEqual(radius,rules.Entity(id).radius,Epsilon);
            var hull=SolidFilters(model).Single(f=>OriginalHullRings(f,width,length));var oldVertices=hull.sharedMesh.vertices;var oldTriangles=hull.sharedMesh.triangles;
            model.position=new Vector3(64,WaterPresentation.WaterLevel,64);float maximum=0;
            foreach(float heading in new[]{0f,45f,90f,180f})foreach(UnitPoseState state in Enum.GetValues(typeof(UnitPoseState)))foreach(float phase in new[]{0f,.25f,.5f,.75f,1f})
            {
                model.rotation=Quaternion.Euler(0,heading,0);rig.RestoreInitialPose();
                var pose=new AnimationPose{state=state,moving=state==UnitPoseState.Move,actionPhase=phase,locomotionPhase=phase*Mathf.PI*2,tool=WorkerTool.Basket,deployed=phase>=.5f,death=phase};
                UnitAnimation.Apply(rig,pose);motion.Apply(pose,phase);
                var worldVertices=SolidFilters(model).SelectMany(f=>f.sharedMesh.vertices.Select(p=>f.transform.TransformPoint(p))).ToArray();float high=worldVertices.Max(p=>p.y);maximum=Mathf.Max(maximum,high);
                Assert.LessOrEqual(high-WaterPresentation.WaterLevel,WaterPresentation.MaximumBoatHeight+Epsilon,id+" "+state+" phase="+phase);
                Assert.Less(high,WaterPresentation.BridgeDeckHeight-.28f,"Actual posed geometry must fit below the unchanged raised crossing");
                Assert.IsTrue(Near(model.position,new Vector3(64,WaterPresentation.WaterLevel,64)),"Boat animation never moves the unit root");
            }
            CollectionAssert.AreEqual(oldVertices,hull.sharedMesh.vertices);CollectionAssert.AreEqual(oldTriangles,hull.sharedMesh.triangles);
            Assert.IsTrue(Near(hull.transform.localPosition,Vector3.zero));Assert.Less(Quaternion.Angle(hull.transform.localRotation,Quaternion.identity),.0001f);
            TestContext.WriteLine("Actual080 "+id+": 220 pose/heading samples; maximum worldY="+maximum+"; unchanged hull ring vertices=256");
        }
        [UnityTest] public IEnumerator AllOwnedMarineMeshesReleaseIncludingHiddenConstructionAndWake()
        {
            var models=new[]{Model("dock"),Model("fishing_boat"),Model("gunboat"),Model("transport_ship"),Model("frigate"),Model("monitor")};
            models[0].GetComponent<ArchitectureModel>().SetConstruction(.10f);
            var meshes=models.SelectMany(m=>m.GetComponentsInChildren<MeshFilter>(true)).Select(f=>f.sharedMesh).ToArray();
            UnityEngine.Object.Destroy(stage);yield return null;yield return null;
            foreach(var mesh in meshes)Assert.IsTrue(mesh==null,"Owned lifetime must include inactive stages, wake and cannon child meshes");
        }
    }
}
