using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // An actual old/current diagnostic, not a relative-improvement or visual
    // gate. Water-boundary geometry is measured against indexed ground planes.
    public sealed class TropicalShoreMesh081Tests
    {
        const float Tolerance=.002f;
        [Serializable] sealed class Source {public string path,sha256;}
        [Serializable] sealed class Sample
        {
            public int segment,row;public float fraction,x,z,waterHeight,signedDistance,queryHeight,triangleHeight,clearance,interpolationError;
            public bool excluded,authorityWater,queryAtOrBelowWater,triangleOnlyOcclusion,bridgeApproach,nearDock;
            public string exclusion;
        }
        [Serializable] sealed class Summary
        {
            public string group;public int samples,authorityWater,authorityDry,queryAboveWater,triangleOnlyOcclusions,groundAboveWater;
            public float maximumPositiveClearance,maximumTriangleOnlyClearance,maximumAbsoluteInterpolationError;public Vector2 worstPosition;
        }
        [Serializable] sealed class Receipt
        {
            public string schema="tropical081-actual-indexed-ground-water-edge-probe",status="diagnostic-not-visual-acceptance";
            public string scope="Actual generated Tropical ground and water vertices; indexed barycentrics at both actual boundary rows, endpoints/quarter samples. Query-high/dry cases reported separately. No reconstructed old map, native acceptance, FPS or chosen refinement claim.";
            public string recordedUtc,appVersion,role,unityVersion,sourceBefore,sourceAfter,groundMeshSha256,waterMeshSha256,bridgesSha256,waterBridgeNavigationMasksSha256,bankQueryGridSha256,worldSha256;
            public int seed,gridCount,groundVertices,groundTriangles,boundarySamples,excludedSamples;public float cellSize,occlusionTolerance=Tolerance,maximumMeasuredQueryGradient;
            public Source[] sourceFiles;public Sample[] samples;public Summary[] summaries;
        }
        [Test] public void ActualTropicalGroundTrianglesRecordBothWaterEdgesBeforeAnyRefinementDecision()
        {
            string version=Environment.GetEnvironmentVariable("FRONTIER_TROPICAL_EXPECTED_VERSION"),role=Environment.GetEnvironmentVariable("FRONTIER_TROPICAL_CAPTURE_ROLE");
            if(string.IsNullOrWhiteSpace(version))version="0.24.0";if(string.IsNullOrWhiteSpace(role))role="candidate-tropical-0.24";
            Assert.That(version,Is.EqualTo("0.23.0").Or.EqualTo("0.24.0"));Assert.AreEqual(version=="0.23.0"?"baseline-tropical-0.23":"candidate-tropical-0.24",role);Assert.AreEqual(version,Application.version);
            var sources=Inventory();string before=Fingerprint(sources),expected=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT");Assert.IsFalse(string.IsNullOrWhiteSpace(expected));Assert.AreEqual(expected,before);
            string output=Environment.GetEnvironmentVariable("FRONTIER_TROPICAL_SHORE_OUTPUT");if(string.IsNullOrWhiteSpace(output))output=Path.Combine(Application.temporaryCachePath,"FrontierTropicalShore081-"+Guid.NewGuid().ToString("N"));
            output=Path.GetFullPath(output);Directory.CreateDirectory(output);Assert.IsFalse(Directory.EnumerateFileSystemEntries(output).Any(),"Fresh actual probe output, never overwrite old evidence");
            var world=new World(Catalog.Load(),42,"coast","ottoman",5,MapTopology.Tropical);string original=JsonUtility.ToJson(world.CaptureState());var stage=new GameObject("081 actual Tropical shoreline mesh probe");
            try
            {
                var terrain=WaterPresentation.BuildTerrain(stage.transform,world);
                var ground=terrain.Find("Continuous sculpted land and river bed").GetComponent<MeshFilter>().sharedMesh;
                var water=terrain.Find("Continuous navigable river").GetComponent<MeshFilter>().sharedMesh;
                var vertices=ground.vertices;int count=Mathf.RoundToInt(Mathf.Sqrt(vertices.Length));Assert.That(count,Is.EqualTo(129).Or.EqualTo(257));Assert.AreEqual(count*count,vertices.Length);
                var indices=ground.triangles;Assert.AreEqual((count-1)*(count-1)*6,indices.Length);int index=0;
                for(int z=0;z<count;z++)for(int x=0;x<count;x++)
                {int i=x+z*count;float cell=128f/(count-1);Assert.AreEqual(x*cell,vertices[i].x);Assert.AreEqual(z*cell,vertices[i].z);Assert.AreEqual(world.Layout.BankHeight(new Vector2(vertices[i].x,vertices[i].z)),vertices[i].y,.000001f);}
                for(int z=0;z<count-1;z++)for(int x=0;x<count-1;x++)
                {int a=x+z*count,b=a+count,c=b+1,d=a+1;foreach(int literal in new[]{a,b,c,a,c,d})Assert.AreEqual(literal,indices[index++],"Check actual regular indexed diagonal before using its plane");}
                var channel=world.Layout.Channels.Single();var edge=water.vertices;Assert.AreEqual(channel.Centers.Length*5,edge.Length);var samples=new List<Sample>();
                for(int segment=0;segment<channel.Centers.Length-1;segment++)foreach(int row in new[]{0,4})for(int quarter=0;quarter<=4;quarter++)
                {
                    var a=edge[segment*5+row];var b=edge[(segment+1)*5+row];float t=quarter*.25f;var v=Vector3.Lerp(a,b,t);var p=new Vector2(v.x,v.z);
                    bool border=OnBorder(a)||OnBorder(b),collapsed=new Vector2(a.x-b.x,a.z-b.z).sqrMagnitude<.00000001f;
                    float query=world.Layout.BankHeight(p),plane=Plane(vertices,count,p),clearance=plane-v.y;bool wet=query<=v.y;
                    samples.Add(new Sample{segment=segment,row=row,fraction=t,x=p.x,z=p.y,waterHeight=v.y,signedDistance=world.Layout.SignedBankDistance(p),queryHeight=query,triangleHeight=plane,clearance=clearance,interpolationError=plane-query,
                        excluded=border||collapsed,exclusion=border?"clamped-map-border":collapsed?"collapsed-boundary-segment":"",authorityWater=world.Layout.IsWater(p),queryAtOrBelowWater=wet,triangleOnlyOcclusion=!border&&!collapsed&&wet&&clearance>Tolerance,
                        bridgeApproach=world.Layout.IsBridgeApproach(p),nearDock=world.Layout.DockSites.SelectMany(s=>s).Any(s=>Vector2.Distance(p,s)<=8)});
                }
                Assert.Greater(samples.Count(s=>!s.excluded),2000,"Measure the whole original channel, not just a favorable central crop");
                var summaries=new[]{Summarize(samples,"all-unclamped"),Summarize(samples.Where(s=>s.z>=50&&s.z<=82&&s.x<76),"west-bay"),Summarize(samples.Where(s=>s.z>=50&&s.z<=82&&s.x>=76),"east-bay"),Summarize(samples.Where(s=>s.bridgeApproach||s.nearDock),"bridge-or-dock-context")};
                float gradient=0;const float step=.25f;
                for(int z=0;z<512;z++)for(int x=0;x<512;x++)
                {var p=new Vector2(x*step,z*step);float h=world.Layout.BankHeight(p);gradient=Mathf.Max(gradient,Mathf.Abs(world.Layout.BankHeight(p+Vector2.right*step)-h)/step,Mathf.Abs(world.Layout.BankHeight(p+Vector2.up*step)-h)/step);}
                string after=Fingerprint(Inventory());Assert.AreEqual(before,after);Assert.AreEqual(original,JsonUtility.ToJson(world.CaptureState()),"Geometry diagnostic is world read-only");
                var receipt=new Receipt{recordedUtc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,role=role,unityVersion=Application.unityVersion,sourceBefore=before,sourceAfter=after,seed=42,gridCount=count,cellSize=128f/(count-1),groundVertices=vertices.Length,groundTriangles=indices.Length/3,
                    groundMeshSha256=MeshHash(ground),waterMeshSha256=MeshHash(water),bridgesSha256=BridgeHash(terrain),waterBridgeNavigationMasksSha256=GridHash(world,false),bankQueryGridSha256=GridHash(world,true),worldSha256=Hash(Encoding.UTF8.GetBytes(original)),maximumMeasuredQueryGradient=gradient,
                    boundarySamples=samples.Count,excludedSamples=samples.Count(s=>s.excluded),sourceFiles=sources,samples=samples.ToArray(),summaries=summaries};
                File.WriteAllText(Path.Combine(output,"tropical-shore-probe081.json"),JsonUtility.ToJson(receipt,true),new UTF8Encoding(false));
                var csv=new StringBuilder("segment,row,fraction,x,z,signedDistance,queryHeight,waterHeight,triangleHeight,clearance,interpolationError,excluded,authorityWater,queryAtOrBelowWater,triangleOnlyOcclusion\n");
                foreach(var s in samples)csv.AppendLine(string.Join(",",new[]{s.segment.ToString(),s.row.ToString(),F(s.fraction),F(s.x),F(s.z),F(s.signedDistance),F(s.queryHeight),F(s.waterHeight),F(s.triangleHeight),F(s.clearance),F(s.interpolationError),s.excluded.ToString(),s.authorityWater.ToString(),s.queryAtOrBelowWater.ToString(),s.triangleOnlyOcclusion.ToString()}));
                File.WriteAllText(Path.Combine(output,"tropical-shore-samples081.csv"),csv.ToString(),new UTF8Encoding(false));
                foreach(var summary in summaries)TestContext.WriteLine(JsonUtility.ToJson(summary));TestContext.WriteLine("Tropical081 actual triangle diagnostic: "+output+" source="+before+" grid="+count+" queryGradient="+F(gradient));
            }
            finally{UnityEngine.Object.DestroyImmediate(stage);}
        }
        static string F(float x)=>x.ToString("R",CultureInfo.InvariantCulture);
        static bool OnBorder(Vector3 p)=>p.x<=.0001f||p.x>=127.9999f||p.z<=.0001f||p.z>=127.9999f;
        static Summary Summarize(IEnumerable<Sample> source,string group)
        {
            var samples=source.Where(s=>!s.excluded).ToArray();var r=new Summary{group=group,samples=samples.Length,authorityWater=samples.Count(s=>s.authorityWater),authorityDry=samples.Count(s=>!s.authorityWater),queryAboveWater=samples.Count(s=>!s.queryAtOrBelowWater),triangleOnlyOcclusions=samples.Count(s=>s.triangleOnlyOcclusion),groundAboveWater=samples.Count(s=>s.clearance>Tolerance)};
            foreach(var s in samples){r.maximumPositiveClearance=Mathf.Max(r.maximumPositiveClearance,s.clearance);r.maximumAbsoluteInterpolationError=Mathf.Max(r.maximumAbsoluteInterpolationError,Mathf.Abs(s.interpolationError));if(s.triangleOnlyOcclusion&&s.clearance>r.maximumTriangleOnlyClearance){r.maximumTriangleOnlyClearance=s.clearance;r.worstPosition=new Vector2(s.x,s.z);}}return r;
        }
        static float Plane(Vector3[] v,int count,Vector2 p)
        {
            float cell=128f/(count-1),px=Mathf.Clamp(p.x/cell,0,count-1),pz=Mathf.Clamp(p.y/cell,0,count-1);int x=Mathf.Min(count-2,Mathf.FloorToInt(px)),z=Mathf.Min(count-2,Mathf.FloorToInt(pz));int a=x+z*count,b=a+count,c=b+1,d=a+1;float tx=px-x,tz=pz-z;
            var p0=v[a];var p1=v[tz>=tx?b:c];var p2=v[tz>=tx?c:d];float denominator=(p1.z-p2.z)*(p0.x-p2.x)+(p2.x-p1.x)*(p0.z-p2.z);Assert.Greater(Mathf.Abs(denominator),.000001f);
            float w0=((p1.z-p2.z)*(p.x-p2.x)+(p2.x-p1.x)*(p.y-p2.z))/denominator,w1=((p2.z-p0.z)*(p.x-p2.x)+(p0.x-p2.x)*(p.y-p2.z))/denominator;return w0*p0.y+w1*p1.y+(1-w0-w1)*p2.y;
        }
        static string GridHash(World world,bool heights)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {for(int z=0;z<=256;z++)for(int x=0;x<=256;x++){var p=new Vector2(x*.5f,z*.5f);w.Write(p.x);w.Write(p.y);if(heights){w.Write(world.Layout.BankHeight(p));var c=world.Layout.BankLayers(p);w.Write(c.r);w.Write(c.g);w.Write(c.b);w.Write(c.a);}else{w.Write(world.Layout.IsWater(p));w.Write(world.Layout.IsBridge(p));w.Write(world.Layout.IsBridgeApproach(p));w.Write(world.Layout.IsBridgeSideBarrierCell(p));w.Write(world.Navigation.IsWalkable(p,0));w.Write(world.WaterNavigation.IsWalkable(p,0));}}w.Flush();return Hash(stream.ToArray());}
        }
        static string BridgeHash(Transform terrain)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {foreach(Transform child in terrain)if(child.GetComponentInChildren<MeshFilter>()!=null&&child.name.StartsWith("Raised crossing ",StringComparison.Ordinal))foreach(var f in child.GetComponentsInChildren<MeshFilter>(true)){w.Write(child.name);w.Write(MeshHash(f.sharedMesh));for(int i=0;i<16;i++)w.Write(f.transform.localToWorldMatrix[i]);foreach(var m in f.GetComponent<MeshRenderer>().sharedMaterials)w.Write(m.name);}w.Flush();return Hash(stream.ToArray());}
        }
        static string MeshHash(Mesh mesh)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {w.Write(mesh.vertexCount);w.Write((int)mesh.indexFormat);w.Write(mesh.subMeshCount);foreach(var v in mesh.vertices){w.Write(v.x);w.Write(v.y);w.Write(v.z);}foreach(var n in mesh.normals){w.Write(n.x);w.Write(n.y);w.Write(n.z);}foreach(var t in mesh.tangents){w.Write(t.x);w.Write(t.y);w.Write(t.z);w.Write(t.w);}foreach(var c in mesh.colors){w.Write(c.r);w.Write(c.g);w.Write(c.b);w.Write(c.a);}for(int i=0;i<8;i++){var uv=new List<Vector4>();mesh.GetUVs(i,uv);w.Write(uv.Count);foreach(var t in uv){w.Write(t.x);w.Write(t.y);w.Write(t.z);w.Write(t.w);}}for(int i=0;i<mesh.subMeshCount;i++){w.Write((int)mesh.GetTopology(i));var t=mesh.GetIndices(i);w.Write(t.Length);foreach(int index in t)w.Write(index);}var b=mesh.bounds;foreach(float f in new[]{b.center.x,b.center.y,b.center.z,b.size.x,b.size.y,b.size.z})w.Write(f);w.Flush();return Hash(stream.ToArray());}
        }
        static Source[] Inventory()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));var files=new List<Source>();foreach(string d in new[]{"Assets","Packages","ProjectSettings","Tools"})foreach(string file in Directory.GetFiles(Path.Combine(root,d),"*",SearchOption.AllDirectories)){string p=file.Substring(root.Length+1).Replace('\\','/');if(p.Split('/').Any(s=>s.StartsWith(".",StringComparison.Ordinal)||s=="__pycache__")||System.Text.RegularExpressions.Regex.IsMatch(p,@"^Assets/InitTestScene[0-9a-f-]{36}\.unity(\.meta)?$"))continue;files.Add(new Source{path=p,sha256=Hash(File.ReadAllBytes(file))});}return files.OrderBy(s=>s.path,StringComparer.Ordinal).ToArray();
        }
        static string Fingerprint(Source[] files)
        {var s=new StringBuilder("{");for(int i=0;i<files.Length;i++){if(i>0)s.Append(',');Quote(s,files[i].path);s.Append(':');Quote(s,files[i].sha256);}return Hash(Encoding.UTF8.GetBytes(s.Append('}').ToString()));}
        static void Quote(StringBuilder s,string value){s.Append('"');foreach(char c in value){if(c=='"')s.Append("\\\"");else if(c=='\\')s.Append("\\\\");else if(c<32||c>126)s.Append("\\u").Append(((int)c).ToString("x4"));else s.Append(c);}s.Append('"');}
        static string Hash(byte[] value){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(value)).Replace("-","").ToLowerInvariant();}
    }
}
