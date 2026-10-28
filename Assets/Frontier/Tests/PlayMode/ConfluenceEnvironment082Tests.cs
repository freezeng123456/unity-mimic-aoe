using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class ConfluenceEnvironment082Tests
    {
        // Frozen ACTUAL old0.24 ShoreBaseline1, source543d575405da...
        // Original indexed union probe5ffa69763185... failed2mm at one shared endpoint;
        // these water/bridge/nav/history oracles predate candidate morphology.
        const string OriginalWater="fc1b4d9400d02671b044fe4d50aaf27accd562d6bc32c737f3ca892c451a126d";
        const string OriginalBridges="4cd7bb8e03316b530df87702935cd14debc41aa2a03acaf6066f2512a88722c9";
        const string OriginalMasks="1a8590c4d214cb5c61f1cecf2db1289654a2a704d2c78501df23adb4a3785df4";
        const string OriginalQuery="d3ae1a2cf774f4902bd7c56447c310bec323f7a727bee05351ac447e4e3f7aea";
        GameObject stage;World world;Transform terrain;Mesh ground,water;
        [SetUp] public void SetUp()
        {
            world=new World(Catalog.Load(),42,"coast","ottoman",5,MapTopology.Tributaries);
            stage=new GameObject("082 strict actual confluence environment");terrain=WaterPresentation.BuildTerrain(stage.transform,world);
            ground=terrain.Find("Continuous sculpted land and river bed").GetComponent<MeshFilter>().sharedMesh;
            water=terrain.Find("Continuous navigable river").GetComponent<MeshFilter>().sharedMesh;
        }
        [TearDown] public void TearDown(){if(stage!=null)UnityEngine.Object.DestroyImmediate(stage);}
        [Test] public void RealOneMeterGroundKeepsFrozenUnionWaterBridgesAndNavigation()
        {
            string before=JsonUtility.ToJson(world.CaptureState());Assert.AreEqual("0.25.0",Application.version);Assert.IsTrue(ConfluenceContent082.Applies(world.Layout));
            ValidateGrid();Assert.AreEqual(14368,water.vertexCount);Assert.AreEqual(26451,water.triangles.Length/3);
            Assert.AreEqual(OriginalWater,Hash(MeshBytes(water)));Assert.AreEqual(OriginalBridges,BridgeHash(terrain));Assert.AreEqual(OriginalMasks,GridHash(false,false));
            Assert.AreEqual(OriginalQuery,GridHash(true,true),"Independent historical formulas still reproduce the frozen actual pre-candidate query grid");
            Assert.AreNotEqual(OriginalQuery,GridHash(true,false),"Actual physical height/layer increment, not a rename or palette-only change");
            var v=ground.vertices;var colors=ground.colors;var uv=ground.uv;Assert.AreEqual(v.Length,colors.Length);Assert.AreEqual(v.Length,uv.Length);
            for(int z=0;z<129;z++)for(int x=0;x<129;x++)
            {int i=x+z*129;var p=new Vector2(x,z);Assert.AreEqual(new Vector3(x,world.Layout.BankHeight(p),z),v[i]);EqualColor(world.Layout.BankLayers(p),colors[i]);Assert.AreEqual(p/128,uv[i]);}
            Assert.AreEqual(before,JsonUtility.ToJson(world.CaptureState()));TestContext.WriteLine("Actual confluence retains1m/129grid,16641vertices/32768triangles; unionwater14368/26451 and bridges/navigation exact old hashes; world read-only");
        }
        [Test] public void AllActualUnionIndexedBoundaryPlanesMeetOriginalTwoMillimeters()
        {
            string before=JsonUtility.ToJson(world.CaptureState());ValidateGrid();var vertices=water.vertices;var indices=water.triangles;var g=ground.vertices;var gi=ground.triangles;
            var edges=new Dictionary<ulong,Edge>();int degenerates=0;
            for(int i=0;i<indices.Length;i+=3)
            {int a=indices[i],b=indices[i+1],c=indices[i+2];if(a==b||b==c||c==a||Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).sqrMagnitude<1e-12f)degenerates++;Add(edges,a,b);Add(edges,b,c);Add(edges,c,a);}
            Assert.AreEqual(40799,edges.Count);Assert.AreEqual(0,degenerates);Assert.AreEqual(0,edges.Values.Count(e=>e.count>2));Assert.AreEqual(0,edges.Values.Count(e=>e.count==2&&e.forward!=1));Assert.AreEqual(38554,edges.Values.Count(e=>e.count==2));
            int boundary=0,samples=0,excluded=0,queryHigh=0,occlusions=0,allAbove=0;float maxPositive=0;Vector2 worst=Vector2.zero;
            foreach(var edge in edges.Values.Where(e=>e.count==1))
            {
                boundary++;var a=vertices[edge.a];var b=vertices[edge.b];bool skip=SameFrame(a,b)||(new Vector2(a.x-b.x,a.z-b.z).sqrMagnitude<1e-12f);
                for(int quarter=0;quarter<=4;quarter++)
                {
                    samples++;if(skip){excluded++;continue;}var p=Vector3.Lerp(a,b,quarter*.25f);var xz=new Vector2(p.x,p.z);float query=world.Layout.BankHeight(xz),plane=Plane(g,gi,xz),clearance=plane-p.y;
                    if(query>p.y)queryHigh++;if(clearance>.002f){allAbove++;if(query<=p.y)occlusions++;}if(clearance>maxPositive){maxPositive=clearance;worst=xz;}
                }
            }
            TestContext.WriteLine("Actual union boundary="+boundary+" samples="+samples+" excluded whole-frame="+excluded+" queryHigh="+queryHigh+" triangleOnly="+occlusions+" allAbove="+allAbove+" maxPositive="+maxPositive.ToString("R")+" at"+worst);
            Assert.AreEqual(2245,boundary);Assert.AreEqual(11225,samples);Assert.AreEqual(355,excluded);Assert.AreEqual(10870,samples-excluded);
            Assert.AreEqual(0,queryHigh);Assert.AreEqual(0,occlusions);Assert.AreEqual(0,allAbove);Assert.LessOrEqual(maxPositive,.002f,"Original actual union indexed-plane gate retained, including the old failed endpoint");
            Assert.AreEqual(before,JsonUtility.ToJson(world.CaptureState()));
        }
        [Test] public void ActualProtectedPadsAndSampledWholeWaterBedKeepHistoricalProfiles()
        {
            string before=JsonUtility.ToJson(world.CaptureState());int wet=0,pads=0;
            Action<Vector2> historical=p=>{Assert.AreEqual(HistoricalHeight(world.Layout,p),world.Layout.BankHeight(p),.000001f,"Protected height at"+p);EqualColor(HistoricalLayers(world.Layout,p),world.Layout.BankLayers(p));pads++;};
            for(int z=0;z<=256;z++)for(int x=0;x<=256;x++)
            {var p=new Vector2(x*.5f,z*.5f);if(world.Layout.SignedBankDistance(p)<=0){historical(p);wet++;}}
            foreach(var start in world.Layout.Starts)for(int z=-12;z<=12;z++)for(int x=-12;x<=12;x++)if(x*x+z*z<=144)historical(start.Town+new Vector2(x,z));
            foreach(var span in world.Layout.Bridges)foreach(float across in new[]{-span.HalfLength-span.RampLength-2,-span.HalfLength,0,span.HalfLength,span.HalfLength+span.RampLength+2})foreach(float along in new[]{-span.HalfWidth-2,0,span.HalfWidth+2})historical(span.Point(across,along));
            var docks=world.Layout.DockSites.SelectMany(b=>b).Concat(new[]{ConfluenceContent082.Dock,ConfluenceContent082.NorthDockContext,ConfluenceContent082.SouthDockContext});
            foreach(var dock in docks)
            {
                for(int z=-8;z<=8;z++)for(int x=-8;x<=8;x++)if(x*x+z*z<=64)historical(dock+new Vector2(x,z));
                var forward=world.Layout.WaterDirection(dock);var side=new Vector2(-forward.y,forward.x);
                foreach(float x in new[]{-2f,0,2f,5f,5.15f,6.4f,7.65f})foreach(float y in new[]{-1.25f,0,1.25f})historical(dock+forward*x+side*y);
            }
            for(int landing=0;landing<2;landing++)for(int z=-6;z<=6;z++)for(int x=-6;x<=6;x++)if(x*x+z*z<=36)historical(ConfluenceContent082.Landing(landing)+new Vector2(x,z)*.5f);
            Assert.Greater(wet,1000);Assert.Greater(pads-wet,1000);Assert.AreEqual(before,JsonUtility.ToJson(world.CaptureState()));
            TestContext.WriteLine("Actual .5m-domain wet profile samples="+wet+" plus protected town/bridge/Dock/exit/landing points="+(pads-wet)+" retain old heights/layers; not a continuous-domain proof");
        }
        [Test] public void LocalMouthRimAndShoulderChangePhysicalQueriesWithinOriginalSlopeLimit()
        {
            string before=JsonUtility.ToJson(world.CaptureState());int lowered=0,raised=0,layerChanged=0,outside=0;float maxGradient=0,maxDryHeight=0;const float step=.25f;
            for(int z=0;z<=512;z++)for(int x=0;x<=512;x++)
            {
                var p=new Vector2(x*step,z*step);float height=world.Layout.BankHeight(p),old=HistoricalHeight(world.Layout,p);var color=world.Layout.BankLayers(p);var oldColor=HistoricalLayers(world.Layout,p);
                if(world.Layout.SignedBankDistance(p)>0){if(height-old<-.005f)lowered++;if(height-old>.005f)raised++;if(ColorDistance(color,oldColor)>.001f)layerChanged++;maxDryHeight=Mathf.Max(maxDryHeight,height);}
                if(!InLocalSupport(p)){Assert.AreEqual(old,height,.000001f,"Unrelated bank height at"+p);EqualColor(oldColor,color);outside++;}
                if(x<512)maxGradient=Mathf.Max(maxGradient,Mathf.Abs(world.Layout.BankHeight(p+Vector2.right*step)-height)/step);
                if(z<512)maxGradient=Mathf.Max(maxGradient,Mathf.Abs(world.Layout.BankHeight(p+Vector2.up*step)-height)/step);
            }
            var protectedOldDryVertex=new Vector2(61,79);Assert.AreEqual(HistoricalHeight(world.Layout,protectedOldDryVertex),world.Layout.BankHeight(protectedOldDryVertex),.000001f,"The old failed cell support is a real protected north-bridge pad and must NOT be height-dented");
            var oldFailedEndpoint=new Vector2(61.733238220214844f,79.73323822021484f);float originalPlane=OriginalHistoricalPlane(world.Layout,oldFailedEndpoint),actualPlane=Plane(ground.vertices,ground.triangles,oldFailedEndpoint);
            Assert.Greater(originalPlane-world.Layout.WaterHeight,.002f,"Independent frozen-old profile with old A-C diagonal still exposes the actual original failure");Assert.LessOrEqual(actualPlane-world.Layout.WaterHeight,.002f,"Actual center-error triangulation repairs the plane while protected queries remain historical");
            Assert.Greater(lowered,12);Assert.Greater(raised,12);Assert.Greater(maxDryHeight,.02f,"A real low shoulder, not only submillimeter tint/geometry");Assert.Greater(layerChanged,12);Assert.Greater(outside,200000);Assert.LessOrEqual(maxDryHeight,.09001f);Assert.LessOrEqual(maxGradient,.4f);
            Assert.AreEqual(before,JsonUtility.ToJson(world.CaptureState()));TestContext.WriteLine("Actual .25m query samples: broad-low="+lowered+" modest-raised="+raised+" layerChanged="+layerChanged+" outsideSupport="+outside+" maxDry="+maxDryHeight.ToString("R")+" maxAxisGradient="+maxGradient.ToString("R"));
        }
        [Test] public void OneLowLandmarkUsesFogConstructionDeathAndNoColliders()
        {
            var p=ConfluenceContent082.Landmark;Assert.AreEqual(new Vector2(44,85),p);
            var candidates=terrain.GetComponentsInChildren<MeshFilter>(true).Where(f=>Vector2.Distance(new Vector2(f.transform.localPosition.x,f.transform.localPosition.z),p)<.00001f).ToArray();Assert.AreEqual(1,candidates.Length);
            var landmark=candidates[0];var renderer=landmark.GetComponent<MeshRenderer>();var mesh=landmark.sharedMesh;
            Assert.AreEqual(1,landmark.GetComponentsInChildren<MeshRenderer>(true).Length);Assert.AreEqual(240,mesh.vertexCount);Assert.AreEqual(120,mesh.triangles.Length/3);Assert.AreEqual(2,mesh.subMeshCount);Assert.AreEqual(2,renderer.sharedMaterials.Length);
            Assert.AreSame(mesh,landmark.GetComponent<ProceduralMeshLifetime>().OwnedMesh);Assert.IsEmpty(landmark.GetComponentsInChildren<Collider>(true));Assert.LessOrEqual(mesh.bounds.max.y,.65f);
            Assert.AreEqual(new Vector3(p.x,world.Layout.BankHeight(p),p.y),landmark.transform.localPosition);Assert.IsNotNull(landmark.GetComponent<LODGroup>());
            var visibilities=terrain.GetComponents<RiverSceneryVisibility>();Assert.AreEqual(1,visibilities.Length);var visibility=visibilities[0];Assert.AreEqual(1,visibility.ClusterCount);
            Assert.AreNotEqual(VisibilityState.Visible,world.Fog.At(0,p));visibility.Refresh(true);Assert.IsFalse(renderer.enabled,"Natural landmark must not reveal a hidden mouth");
            world.Fog.Reveal(0,p,6);visibility.Refresh(true);Assert.IsTrue(renderer.enabled);
            var house=world.Spawn("house",0,p);Assert.AreEqual(p,house.position);visibility.Refresh(true);Assert.IsFalse(renderer.enabled,"Later actual building footprint hides the entire low landmark");
            world.ApplyDamage(house,100000,DamageKind.Siege,1);Assert.IsFalse(house.Alive,"Exercise ordinary lethal damage/removal rather than only editing renderer state");visibility.Refresh(true);Assert.IsTrue(renderer.enabled,"Still-revealed unoccupied landmark returns after building death");
            TestContext.WriteLine("Actual bounded landmark1renderer/1ownedmesh/240vertices/120triangles/2materials, localY<.65, fog/build/death return; controlled Spawn/damage fixture, not paid construction");
        }
        [UnityTest] public IEnumerator OwnedUnionTerrainAndLandmarkMeshesReleaseAfterDestruction()
        {
            var p=ConfluenceContent082.Landmark;var landmark=terrain.GetComponentsInChildren<MeshFilter>(true).Single(f=>Vector2.Distance(new Vector2(f.transform.localPosition.x,f.transform.localPosition.z),p)<.00001f);
            var meshes=terrain.GetComponentsInChildren<ProceduralMeshLifetime>(true).Select(o=>o.OwnedMesh).Where(m=>m!=null).Distinct().ToArray();Assert.Contains(landmark.sharedMesh,meshes);Assert.Greater(meshes.Length,4);
            UnityEngine.Object.Destroy(stage);stage=null;yield return null;yield return null;Assert.IsTrue(meshes.All(m=>m==null));TestContext.WriteLine("Actual confluence terrain/landmark owned meshes released="+meshes.Length);
        }
        void ValidateGrid()
        {
            Assert.AreEqual(16641,ground.vertexCount);var v=ground.vertices;var t=ground.triangles;Assert.AreEqual(98304,t.Length);int cursor=0,reverseCells=0;double originalTotal=0,selectedTotal=0;
            for(int z=0;z<128;z++)for(int x=0;x<128;x++)
            {
                int a=x+z*129,b=a+129,c=b+1,d=a+1;float query=world.Layout.BankHeight(new Vector2(x+.5f,z+.5f));
                float oldError=Mathf.Abs((v[a].y+v[c].y)*.5f-query),otherError=Mathf.Abs((v[b].y+v[d].y)*.5f-query);bool reverse=otherError+.000001f<oldError;
                foreach(int expected in reverse?new[]{a,b,d,b,c,d}:new[]{a,b,c,a,c,d})Assert.AreEqual(expected,t[cursor++],"Exact actual query-bound diagonal selection for every cell");
                float selected=reverse?otherError:oldError;Assert.LessOrEqual(selected,oldError+.000001f);originalTotal+=oldError;selectedTotal+=selected;if(reverse)reverseCells++;
            }
            Assert.Greater(reverseCells,0);Assert.Less(selectedTotal,originalTotal);TestContext.WriteLine("Actual unchanged16641/32768 ground: reverse="+reverseCells+" oldCenterErrorTotal="+originalTotal+" selectedTotal="+selectedTotal);
        }
        sealed class Edge {public int a,b,count,forward;}
        static void Add(Dictionary<ulong,Edge> edges,int a,int b)
        {int low=Mathf.Min(a,b),high=Mathf.Max(a,b);ulong key=((ulong)(uint)low<<32)|(uint)high;if(!edges.TryGetValue(key,out Edge edge)){edge=new Edge{a=low,b=high};edges.Add(key,edge);}edge.count++;if(a==low)edge.forward++;}
        static bool SameFrame(Vector3 a,Vector3 b)=>(Mathf.Abs(a.x)<.0001f&&Mathf.Abs(b.x)<.0001f)||(Mathf.Abs(a.x-128)<.0001f&&Mathf.Abs(b.x-128)<.0001f)||(Mathf.Abs(a.z)<.0001f&&Mathf.Abs(b.z)<.0001f)||(Mathf.Abs(a.z-128)<.0001f&&Mathf.Abs(b.z-128)<.0001f);
        static float Plane(Vector3[] v,int[] indices,Vector2 p)
        {
            int x=Mathf.Min(127,Mathf.FloorToInt(Mathf.Clamp(p.x,0,128))),z=Mathf.Min(127,Mathf.FloorToInt(Mathf.Clamp(p.y,0,128))),first=(x+z*128)*2;
            for(int triangle=first;triangle<=first+1;triangle++)
            {
                var a=v[indices[triangle*3]];var b=v[indices[triangle*3+1]];var c=v[indices[triangle*3+2]];float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);Assert.Greater(Mathf.Abs(d),.000001f);
                float wa=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.y-c.z))/d,wb=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.y-c.z))/d,wc=1-wa-wb;
                if(wa>=-.00001f&&wb>=-.00001f&&wc>=-.00001f)return wa*a.y+wb*b.y+wc*c.y;
            }
            Assert.Fail("No actual indexed triangle contains"+p);return float.NaN;
        }
        static float OriginalHistoricalPlane(MapLayout layout,Vector2 p)
        {
            int x=Mathf.FloorToInt(p.x),z=Mathf.FloorToInt(p.y);float tx=p.x-x,tz=p.y-z;
            var a=new Vector2(x,z);var b=new Vector2(x,z+1);var c=new Vector2(x+1,z+1);var d=new Vector2(x+1,z);
            return tz>=tx?(1-tz)*HistoricalHeight(layout,a)+(tz-tx)*HistoricalHeight(layout,b)+tx*HistoricalHeight(layout,c):(1-tx)*HistoricalHeight(layout,a)+tz*HistoricalHeight(layout,c)+(tx-tz)*HistoricalHeight(layout,d);
        }
        static bool Ellipse(Vector2 p,Vector2 center,Vector2 normal,float across,float along)
        {var delta=p-center;var tangent=new Vector2(-normal.y,normal.x);return Mathf.Pow(Vector2.Dot(delta,normal)/across,2)+Mathf.Pow(Vector2.Dot(delta,tangent)/along,2)<1;}
        static bool InLocalSupport(Vector2 p)=>Ellipse(p,new Vector2(50,66),Vector2.down,11,16)||Ellipse(p,new Vector2(53,54),Vector2.up,10,14)||Ellipse(p,new Vector2(38,82),Vector2.left,3,9)||Ellipse(p,new Vector2(42,83),Vector2.left,5,8);
        static float HistoricalHeight(MapLayout layout,Vector2 p)
        {float distance=layout.SignedBankDistance(p);return distance<=0?Mathf.Lerp(layout.WaterHeight-.02f,-.9f,Mathf.SmoothStep(0,1,-distance/4)):(layout.WaterHeight-.02f)*(1-Mathf.SmoothStep(0,1,distance/3));}
        static Color HistoricalLayers(MapLayout layout,Vector2 p)
        {float d=layout.SignedBankDistance(p),noise=Mathf.PerlinNoise(p.x*.13f+3,p.y*.11f+8)-.5f;float damp=1-Mathf.SmoothStep(0,1,(d-.25f-noise*.25f)/2.6f),shoulder=1-Mathf.SmoothStep(0,1,(d-1.5f-noise*.65f)/4),sand=(1-damp)*shoulder;return new Color(damp,sand,1-damp-sand,1);}
        static float ColorDistance(Color a,Color b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)+Mathf.Abs(a.a-b.a);
        static void EqualColor(Color expected,Color actual){Assert.AreEqual(expected.r,actual.r,.000001f);Assert.AreEqual(expected.g,actual.g,.000001f);Assert.AreEqual(expected.b,actual.b,.000001f);Assert.AreEqual(expected.a,actual.a,.000001f);}
        string GridHash(bool heights,bool historical)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {for(int z=0;z<=256;z++)for(int x=0;x<=256;x++){var p=new Vector2(x*.5f,z*.5f);w.Write(p.x);w.Write(p.y);if(heights){w.Write(historical?HistoricalHeight(world.Layout,p):world.Layout.BankHeight(p));var c=historical?HistoricalLayers(world.Layout,p):world.Layout.BankLayers(p);w.Write(c.r);w.Write(c.g);w.Write(c.b);w.Write(c.a);}else{w.Write(world.Layout.IsWater(p));w.Write(world.Layout.IsBridge(p));w.Write(world.Layout.IsBridgeApproach(p));w.Write(world.Layout.IsBridgeSideBarrierCell(p));w.Write(world.Navigation.IsWalkable(p,0));w.Write(world.WaterNavigation.IsWalkable(p,0));}}w.Flush();return Hash(stream.ToArray());}
        }
        static string BridgeHash(Transform terrain)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {foreach(Transform child in terrain)if(child.GetComponentInChildren<MeshFilter>()!=null&&child.name.StartsWith("Raised crossing ",StringComparison.Ordinal))foreach(var f in child.GetComponentsInChildren<MeshFilter>(true)){w.Write(child.name);w.Write(Hash(MeshBytes(f.sharedMesh)));for(int i=0;i<16;i++)w.Write(f.transform.localToWorldMatrix[i]);foreach(var m in f.GetComponent<MeshRenderer>().sharedMaterials)w.Write(m.name);}w.Flush();return Hash(stream.ToArray());}
        }
        static byte[] MeshBytes(Mesh mesh)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){w.Write("FRONTIER082-MESH-v1");w.Write(mesh.vertexCount);w.Write((int)mesh.indexFormat);w.Write(mesh.subMeshCount);var vertices=mesh.vertices;w.Write(vertices.Length);foreach(var v in vertices){w.Write(v.x);w.Write(v.y);w.Write(v.z);}var normals=mesh.normals;w.Write(normals.Length);foreach(var n in normals){w.Write(n.x);w.Write(n.y);w.Write(n.z);}var tangents=mesh.tangents;w.Write(tangents.Length);foreach(var t in tangents){w.Write(t.x);w.Write(t.y);w.Write(t.z);w.Write(t.w);}var colors=mesh.colors;w.Write(colors.Length);foreach(var c in colors){w.Write(c.r);w.Write(c.g);w.Write(c.b);w.Write(c.a);}for(int i=0;i<8;i++){var uv=new List<Vector4>();mesh.GetUVs(i,uv);w.Write(uv.Count);foreach(var t in uv){w.Write(t.x);w.Write(t.y);w.Write(t.z);w.Write(t.w);}}for(int i=0;i<mesh.subMeshCount;i++){w.Write((int)mesh.GetTopology(i));var t=mesh.GetIndices(i);w.Write(t.Length);foreach(int index in t)w.Write(index);}var b=mesh.bounds;foreach(float f in new[]{b.center.x,b.center.y,b.center.z,b.size.x,b.size.y,b.size.z})w.Write(f);w.Flush();return stream.ToArray();}
        }
        static string Hash(byte[] value){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(value)).Replace("-","").ToLowerInvariant();}
    }
}
