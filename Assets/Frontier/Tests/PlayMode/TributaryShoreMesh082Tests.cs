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
    // Same old/current diagnostic. No channel-row assumption, recalled mesh,
    // renderer acceptance or improvement predicate is used for the union.
    public sealed class TributaryShoreMesh082Tests
    {
        const float Tolerance=.002f,GradientLimit=.4f;
        [Serializable] sealed class Source {public string path,sha256;}
        sealed class Edge {public int a,b,count,forward,firstTriangle;}
        [Serializable] sealed class Boundary
        {
            public int ordinal,a,b,firstTriangle;public Vector3 start,end;public float length;
            public bool mapFrameEdge,collapsed;public string exclusion;
        }
        [Serializable] sealed class Sample
        {
            public int edge,a,b,quarter,groundTriangle;public float fraction,x,z,waterHeight,signedDistance,queryHeight,triangleHeight,clearance,interpolationError;
            public bool excluded,mapFrameEndpoint,authorityWater,queryAtOrBelowWater,triangleOnlyOcclusion,bridgeApproach,nearDock;public string exclusion;
        }
        [Serializable] sealed class Summary
        {
            public string group;public int samples,authorityWater,authorityDry,queryAboveWater,triangleOnlyOcclusions,groundAboveWater;
            public float maximumPositiveClearance,maximumTriangleOnlyClearance,maximumAbsoluteInterpolationError;public Vector2 worstPosition;
        }
        [Serializable] sealed class Receipt
        {
            public string schema="tributary082-indexed-union-boundary-quarter-ground-probe-v2",status;
            public string scope="Actual union-water indexed edges occurring once, endpoints and quarters against actual indexed 1m ground. Only entire same-map-frame edges and zero-length edges excluded. Query-high/dry cases remain separate; no visual, native, FPS, paid-chain or relative-improvement claim.";
            public string recordedUtc,appVersion,role,unityVersion,sourceBefore,sourceAfter,groundMeshSha256,waterMeshSha256,bridgesSha256,waterBridgeNavigationMasksSha256,bankQueryGridSha256,worldSha256,boundaryEdgesSha256;
            public string meshBinaryFormat="Little-endian BinaryWriter: string FRONTIER082-MESH-v1; vertexCount,indexFormat,subMeshCount; each vertex/normal/tangent/color array length and values; eight UV lengths+Vector4 values; each submesh topology,indexCount,indices; bounds center+size.";
            public bool sourceStable,worldStable;public int seed,channelCount,gridCount,groundVertices,groundTriangles,waterVertices,waterTriangles,uniqueIndexedEdges,internalEdges,nonManifoldEdges,internalSameDirectionEdges,degenerateWaterTriangles,boundaryEdges,boundaryComponents,boundaryDegreeNotTwo,boundarySamples,excludedSamples;
            public float cellSize,occlusionTolerance=Tolerance,queryAxisGradientLimit=GradientLimit,maximumMeasuredQueryAxisGradient,maximumGroundTriangleGradient,maximumOriginalCenterError,maximumSelectedCenterError;
            public int forwardDiagonalCells,reverseDiagonalCells;public double totalOriginalCenterError,totalSelectedCenterError;public string triangulationPolicy;
            public Source[] sourceFiles;public Boundary[] boundaries;public Sample[] samples;public Summary[] summaries;
        }
        [Test] public void ActualUnionBoundaryEdgesMeasureIndexedGroundClearance()
        {
            string version=Environment.GetEnvironmentVariable("FRONTIER_CONFLUENCE_EXPECTED_VERSION"),role=Environment.GetEnvironmentVariable("FRONTIER_CONFLUENCE_CAPTURE_ROLE");
            if(string.IsNullOrWhiteSpace(version))version="0.25.0";if(string.IsNullOrWhiteSpace(role))role="candidate-confluence-0.25";
            Assert.That(version,Is.EqualTo("0.24.0").Or.EqualTo("0.25.0"));Assert.AreEqual(version=="0.24.0"?"baseline-confluence-0.24":"candidate-confluence-0.25",role);Assert.AreEqual(version,Application.version);
            var sources=Inventory();string before=Fingerprint(sources),expected=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT");Assert.IsFalse(string.IsNullOrWhiteSpace(expected));Assert.AreEqual(expected,before);
            string output=Environment.GetEnvironmentVariable("FRONTIER_CONFLUENCE_SHORE_OUTPUT");if(string.IsNullOrWhiteSpace(output))output=Path.Combine(Application.temporaryCachePath,"FrontierConfluenceShore082-"+Guid.NewGuid().ToString("N"));
            output=Path.GetFullPath(output);Directory.CreateDirectory(output);Assert.IsFalse(Directory.EnumerateFileSystemEntries(output).Any(),"Fresh output; never replace an old passing or failing diagnostic");
            var world=new World(Catalog.Load(),42,"coast","ottoman",5,MapTopology.Tributaries);string original=JsonUtility.ToJson(world.CaptureState());var stage=new GameObject("082 actual union shoreline diagnostic");
            try
            {
                Assert.AreEqual(2,world.Layout.Channels.Length);Assert.IsFalse(world.Layout.Generated);Assert.IsFalse(world.Layout.HasScenario);
                var terrain=WaterPresentation.BuildTerrain(stage.transform,world);var gf=terrain.Find("Continuous sculpted land and river bed").GetComponent<MeshFilter>();var wf=terrain.Find("Continuous navigable river").GetComponent<MeshFilter>();
                Assert.AreEqual(Matrix4x4.identity,gf.transform.localToWorldMatrix);Assert.AreEqual(Matrix4x4.identity,wf.transform.localToWorldMatrix,"Probe coordinates are actual unchanged map-space transforms");
                var ground=gf.sharedMesh;var water=wf.sharedMesh;var gv=ground.vertices;var gi=ground.triangles;var wv=water.vertices;var wi=water.triangles;
                int count=Mathf.RoundToInt(Mathf.Sqrt(gv.Length));Assert.AreEqual(129,count,"No presumed mesh refinement: old/current probe is the actual 1m ground contract");Assert.AreEqual(count*count,gv.Length);Assert.AreEqual((count-1)*(count-1)*6,gi.Length);
                int cursor=0,reverseCells=0;float maximumOriginalError=0,maximumSelectedError=0;double totalOriginalError=0,totalSelectedError=0;
                for(int z=0;z<count;z++)for(int x=0;x<count;x++){int i=x+z*count;Assert.AreEqual(x,gv[i].x);Assert.AreEqual(z,gv[i].z);Assert.AreEqual(world.Layout.BankHeight(new Vector2(x,z)),gv[i].y,.000001f);}
                for(int z=0;z<count-1;z++)for(int x=0;x<count-1;x++)
                {
                    int a=x+z*count,b=a+count,c=b+1,d=a+1;float query=world.Layout.BankHeight(new Vector2(x+.5f,z+.5f));
                    float originalError=Mathf.Abs((gv[a].y+gv[c].y)*.5f-query),otherError=Mathf.Abs((gv[b].y+gv[d].y)*.5f-query);
                    bool reverse=version=="0.25.0"&&otherError+.000001f<originalError;int[] literals=reverse?new[]{a,b,d,b,c,d}:new[]{a,b,c,a,c,d};
                    foreach(int literal in literals)Assert.AreEqual(literal,gi[cursor++],"Actual cell must use the exact version-bound center-error choice, never a third triangle or guessed plane");
                    float selectedError=reverse?otherError:originalError;Assert.LessOrEqual(selectedError,originalError+.000001f);if(reverse)reverseCells++;
                    maximumOriginalError=Mathf.Max(maximumOriginalError,originalError);maximumSelectedError=Mathf.Max(maximumSelectedError,selectedError);totalOriginalError+=originalError;totalSelectedError+=selectedError;
                }
                Assert.AreEqual(0,wi.Length%3);var edges=new Dictionary<ulong,Edge>();int degenerate=0;
                for(int i=0;i<wi.Length;i+=3)
                {
                    int a=wi[i],b=wi[i+1],c=wi[i+2];Assert.That(a,Is.InRange(0,wv.Length-1));Assert.That(b,Is.InRange(0,wv.Length-1));Assert.That(c,Is.InRange(0,wv.Length-1));
                    if(Vector3.Cross(wv[b]-wv[a],wv[c]-wv[a]).sqrMagnitude<=.0000001f)degenerate++;
                    AddEdge(edges,a,b,i/3);AddEdge(edges,b,c,i/3);AddEdge(edges,c,a,i/3);
                }
                var boundaries=edges.OrderBy(e=>e.Key).Where(e=>e.Value.count==1).Select((entry,n)=>
                {
                    var e=entry.Value;var a=wv[e.a];var b=wv[e.b];bool frame=SameFrame(a,b),collapsed=new Vector2(a.x-b.x,a.z-b.z).sqrMagnitude<.00000001f;
                    return new Boundary{ordinal=n,a=e.a,b=e.b,firstTriangle=e.firstTriangle,start=a,end=b,length=Vector3.Distance(a,b),mapFrameEdge=frame,collapsed=collapsed,exclusion=frame?"entire-same-map-frame-edge":collapsed?"collapsed-boundary-edge":""};
                }).ToArray();
                var samples=new List<Sample>();
                foreach(var e in boundaries)for(int quarter=0;quarter<=4;quarter++)
                {
                    float t=quarter*.25f;var v=Vector3.Lerp(e.start,e.end,t);var p=new Vector2(v.x,v.z);Assert.That(p.x,Is.InRange(-.0001f,128.0001f));Assert.That(p.y,Is.InRange(-.0001f,128.0001f));
                    int triangle;float query=world.Layout.BankHeight(p),plane=Plane(gv,gi,count,p,out triangle),clearance=plane-v.y;bool wet=query<=v.y,excluded=e.mapFrameEdge||e.collapsed;
                    samples.Add(new Sample{edge=e.ordinal,a=e.a,b=e.b,quarter=quarter,groundTriangle=triangle,fraction=t,x=p.x,z=p.y,waterHeight=v.y,signedDistance=world.Layout.SignedBankDistance(p),queryHeight=query,triangleHeight=plane,clearance=clearance,interpolationError=plane-query,
                        excluded=excluded,exclusion=e.exclusion,mapFrameEndpoint=OnFrame(v),authorityWater=world.Layout.IsWater(p),queryAtOrBelowWater=wet,triangleOnlyOcclusion=!excluded&&wet&&clearance>Tolerance,
                        bridgeApproach=world.Layout.IsBridgeApproach(p),nearDock=world.Layout.DockSites.SelectMany(s=>s).Any(s=>Vector2.Distance(p,s)<=8)});
                }
                var summaries=new[]{Summarize(samples,"all-unclamped"),Summarize(samples.Where(s=>s.x>=64&&s.x<=88&&s.z>=50&&s.z<=78),"junction-main-union-x64-88-z50-78"),Summarize(samples.Where(s=>s.x>=44&&s.x<=70&&s.z>=53&&s.z<=76),"branch-mouth-x44-70-z53-76"),Summarize(samples.Where(s=>s.x>=48&&s.x<=62&&s.z>=68&&s.z<=76),"north-pocket-survey-x48-62-z68-76"),Summarize(samples.Where(s=>s.x>=46&&s.x<=62&&s.z>=48&&s.z<=58),"south-stage-survey-x46-62-z48-58"),Summarize(samples.Where(s=>s.bridgeApproach||s.nearDock),"authored-bridge-or-dock-context")};
                float gradient=0;const float step=.25f;
                for(int z=0;z<512;z++)for(int x=0;x<512;x++){var p=new Vector2(x*step,z*step);float h=world.Layout.BankHeight(p);gradient=Mathf.Max(gradient,Mathf.Abs(world.Layout.BankHeight(p+Vector2.right*step)-h)/step,Mathf.Abs(world.Layout.BankHeight(p+Vector2.up*step)-h)/step);}
                float triangleGradient=0;for(int i=0;i<gi.Length;i+=3){var n=Vector3.Cross(gv[gi[i+1]]-gv[gi[i]],gv[gi[i+2]]-gv[gi[i]]);Assert.Greater(Mathf.Abs(n.y),.000001f);triangleGradient=Mathf.Max(triangleGradient,Mathf.Sqrt(n.x*n.x+n.z*n.z)/Mathf.Abs(n.y));}
                int components,degreeNotTwo;BoundaryGraph(boundaries,out components,out degreeNotTwo);
                byte[] groundBytes=MeshBytes(ground),waterBytes=MeshBytes(water);string after=Fingerprint(Inventory());bool worldStable=original==JsonUtility.ToJson(world.CaptureState());int nonManifold=edges.Values.Count(e=>e.count>2),sameDirection=edges.Values.Count(e=>e.count==2&&e.forward!=1);
                var receipt=new Receipt{recordedUtc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,role=role,unityVersion=Application.unityVersion,sourceBefore=before,sourceAfter=after,sourceStable=before==after,worldStable=worldStable,seed=42,channelCount=world.Layout.Channels.Length,gridCount=count,cellSize=1,groundVertices=gv.Length,groundTriangles=gi.Length/3,waterVertices=wv.Length,waterTriangles=wi.Length/3,
                    uniqueIndexedEdges=edges.Count,internalEdges=edges.Values.Count(e=>e.count==2),nonManifoldEdges=nonManifold,internalSameDirectionEdges=sameDirection,degenerateWaterTriangles=degenerate,boundaryEdges=boundaries.Length,boundaryComponents=components,boundaryDegreeNotTwo=degreeNotTwo,boundarySamples=samples.Count,excludedSamples=samples.Count(s=>s.excluded),
                    groundMeshSha256=Hash(groundBytes),waterMeshSha256=Hash(waterBytes),bridgesSha256=BridgeHash(terrain),waterBridgeNavigationMasksSha256=GridHash(world,false),bankQueryGridSha256=GridHash(world,true),worldSha256=Hash(Encoding.UTF8.GetBytes(original)),boundaryEdgesSha256=Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new BoundaryList{edges=boundaries}))),maximumMeasuredQueryAxisGradient=gradient,maximumGroundTriangleGradient=triangleGradient,maximumOriginalCenterError=maximumOriginalError,maximumSelectedCenterError=maximumSelectedError,totalOriginalCenterError=totalOriginalError,totalSelectedCenterError=totalSelectedError,forwardDiagonalCells=(count-1)*(count-1)-reverseCells,reverseDiagonalCells=reverseCells,triangulationPolicy=version=="0.24.0"?"historical-A-C":"actual-center-error-reverse-only-if-improves-by-more-than1e-6",sourceFiles=sources,boundaries=boundaries,samples=samples.ToArray(),summaries=summaries,
                    status=before==after&&worldStable&&nonManifold==0&&sameDirection==0&&degenerate==0&&summaries[0].samples>0&&summaries[0].groundAboveWater==0&&gradient<=GradientLimit?"measured-thresholds-passed-not-visual-acceptance":"measured-thresholds-failed-originals-retained"};
                // Commit all actual bytes BEFORE threshold assertions, including an old-map failure.
                File.WriteAllBytes(Path.Combine(output,"tributary-ground082.meshbytes"),groundBytes);File.WriteAllBytes(Path.Combine(output,"tributary-water082.meshbytes"),waterBytes);
                File.WriteAllText(Path.Combine(output,"tributary-shore-probe082.json"),JsonUtility.ToJson(receipt,true),new UTF8Encoding(false));
                var csv=new StringBuilder("edge,a,b,quarter,fraction,x,z,groundTriangle,signedDistance,queryHeight,waterHeight,triangleHeight,clearance,interpolationError,excluded,exclusion,mapFrameEndpoint,authorityWater,queryAtOrBelowWater,triangleOnlyOcclusion\n");
                foreach(var s in samples)csv.AppendLine(string.Join(",",new[]{s.edge.ToString(),s.a.ToString(),s.b.ToString(),s.quarter.ToString(),F(s.fraction),F(s.x),F(s.z),s.groundTriangle.ToString(),F(s.signedDistance),F(s.queryHeight),F(s.waterHeight),F(s.triangleHeight),F(s.clearance),F(s.interpolationError),s.excluded.ToString(),s.exclusion,s.mapFrameEndpoint.ToString(),s.authorityWater.ToString(),s.queryAtOrBelowWater.ToString(),s.triangleOnlyOcclusion.ToString()}));
                File.WriteAllText(Path.Combine(output,"tributary-shore-samples082.csv"),csv.ToString(),new UTF8Encoding(false));
                foreach(var summary in summaries)TestContext.WriteLine(JsonUtility.ToJson(summary));TestContext.WriteLine("Confluence082 actual indexed union probe: "+output+" source="+before+" edges="+boundaries.Length+" axisGradient="+F(gradient));
                Assert.AreEqual(before,after);Assert.IsTrue(worldStable,"Terrain diagnostic must leave authoritative world state unchanged");Assert.Zero(nonManifold);Assert.Zero(sameDirection);Assert.Zero(degenerate);Assert.Greater(summaries[0].samples,0);
                Assert.Zero(summaries[0].groundAboveWater,"Actual indexed ground must remain <=2mm above the actual union-water boundary; inspect retained query-high versus triangle-only causes");Assert.LessOrEqual(gradient,GradientLimit,"Retain original quarter-metre axis-query gradient bound .4; actual triangle gradient is reported separately");
            }
            finally{UnityEngine.Object.DestroyImmediate(stage);}
        }
        [Serializable] sealed class BoundaryList {public Boundary[] edges;}
        static void AddEdge(Dictionary<ulong,Edge> edges,int a,int b,int triangle)
        {int low=Mathf.Min(a,b),high=Mathf.Max(a,b);ulong key=((ulong)(uint)low<<32)|(uint)high;if(!edges.TryGetValue(key,out Edge e)){e=new Edge{a=low,b=high,firstTriangle=triangle};edges.Add(key,e);}e.count++;if(a==low)e.forward++;}
        static bool OnFrame(Vector3 p)=>p.x<=.0001f||p.x>=127.9999f||p.z<=.0001f||p.z>=127.9999f;
        static bool SameFrame(Vector3 a,Vector3 b)=>(Mathf.Abs(a.x)<.0001f&&Mathf.Abs(b.x)<.0001f)||(Mathf.Abs(a.x-128)<.0001f&&Mathf.Abs(b.x-128)<.0001f)||(Mathf.Abs(a.z)<.0001f&&Mathf.Abs(b.z)<.0001f)||(Mathf.Abs(a.z-128)<.0001f&&Mathf.Abs(b.z-128)<.0001f);
        static string F(float x)=>x.ToString("R",CultureInfo.InvariantCulture);
        static Summary Summarize(IEnumerable<Sample> source,string group)
        {
            var samples=source.Where(s=>!s.excluded).ToArray();var r=new Summary{group=group,samples=samples.Length,authorityWater=samples.Count(s=>s.authorityWater),authorityDry=samples.Count(s=>!s.authorityWater),queryAboveWater=samples.Count(s=>!s.queryAtOrBelowWater),triangleOnlyOcclusions=samples.Count(s=>s.triangleOnlyOcclusion),groundAboveWater=samples.Count(s=>s.clearance>Tolerance)};
            foreach(var s in samples){if(s.clearance>r.maximumPositiveClearance){r.maximumPositiveClearance=s.clearance;r.worstPosition=new Vector2(s.x,s.z);}r.maximumAbsoluteInterpolationError=Mathf.Max(r.maximumAbsoluteInterpolationError,Mathf.Abs(s.interpolationError));if(s.triangleOnlyOcclusion)r.maximumTriangleOnlyClearance=Mathf.Max(r.maximumTriangleOnlyClearance,s.clearance);}return r;
        }
        static float Plane(Vector3[] vertices,int[] indices,int count,Vector2 p,out int triangle)
        {
            float px=Mathf.Clamp(p.x,0,count-1),pz=Mathf.Clamp(p.y,0,count-1);int x=Mathf.Min(count-2,Mathf.FloorToInt(px)),z=Mathf.Min(count-2,Mathf.FloorToInt(pz)),first=(x+z*(count-1))*2;
            // Select containment from BOTH actual indexed triangles. A reverse B-D
            // cell must never be sampled with the old assumed A-C diagonal.
            for(int candidate=first;candidate<=first+1;candidate++)
            {
                var a=vertices[indices[candidate*3]];var b=vertices[indices[candidate*3+1]];var c=vertices[indices[candidate*3+2]];float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);Assert.Greater(Mathf.Abs(denominator),.000001f);
                float wa=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.y-c.z))/denominator,wb=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.y-c.z))/denominator,wc=1-wa-wb;
                if(wa>=-.00001f&&wb>=-.00001f&&wc>=-.00001f){triangle=candidate;return wa*a.y+wb*b.y+wc*c.y;}
            }
            Assert.Fail("Actual indexed cell has no containing triangle at"+p);triangle=-1;return float.NaN;
        }
        static void BoundaryGraph(Boundary[] edges,out int components,out int degreeNotTwo)
        {
            var graph=new Dictionary<int,List<int>>();foreach(var edge in edges){if(!graph.ContainsKey(edge.a))graph[edge.a]=new List<int>();if(!graph.ContainsKey(edge.b))graph[edge.b]=new List<int>();graph[edge.a].Add(edge.b);graph[edge.b].Add(edge.a);}degreeNotTwo=graph.Values.Count(v=>v.Count!=2);components=0;var visited=new HashSet<int>();
            foreach(int start in graph.Keys)if(visited.Add(start)){components++;var queue=new Queue<int>();queue.Enqueue(start);while(queue.Count>0){int current=queue.Dequeue();foreach(int next in graph[current])if(visited.Add(next))queue.Enqueue(next);}}
        }
        static string GridHash(World world,bool heights)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){for(int z=0;z<=256;z++)for(int x=0;x<=256;x++){var p=new Vector2(x*.5f,z*.5f);w.Write(p.x);w.Write(p.y);if(heights){w.Write(world.Layout.BankHeight(p));var c=world.Layout.BankLayers(p);w.Write(c.r);w.Write(c.g);w.Write(c.b);w.Write(c.a);}else{w.Write(world.Layout.IsWater(p));w.Write(world.Layout.IsBridge(p));w.Write(world.Layout.IsBridgeApproach(p));w.Write(world.Layout.IsBridgeSideBarrierCell(p));w.Write(world.Navigation.IsWalkable(p,0));w.Write(world.WaterNavigation.IsWalkable(p,0));}}w.Flush();return Hash(stream.ToArray());}
        }
        static string BridgeHash(Transform terrain)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){foreach(Transform child in terrain)if(child.GetComponentInChildren<MeshFilter>()!=null&&child.name.StartsWith("Raised crossing ",StringComparison.Ordinal))foreach(var f in child.GetComponentsInChildren<MeshFilter>(true)){w.Write(child.name);w.Write(Hash(MeshBytes(f.sharedMesh)));for(int i=0;i<16;i++)w.Write(f.transform.localToWorldMatrix[i]);foreach(var m in f.GetComponent<MeshRenderer>().sharedMaterials)w.Write(m.name);}w.Flush();return Hash(stream.ToArray());}
        }
        static byte[] MeshBytes(Mesh mesh)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){w.Write("FRONTIER082-MESH-v1");w.Write(mesh.vertexCount);w.Write((int)mesh.indexFormat);w.Write(mesh.subMeshCount);var vertices=mesh.vertices;w.Write(vertices.Length);foreach(var v in vertices){w.Write(v.x);w.Write(v.y);w.Write(v.z);}var normals=mesh.normals;w.Write(normals.Length);foreach(var n in normals){w.Write(n.x);w.Write(n.y);w.Write(n.z);}var tangents=mesh.tangents;w.Write(tangents.Length);foreach(var t in tangents){w.Write(t.x);w.Write(t.y);w.Write(t.z);w.Write(t.w);}var colors=mesh.colors;w.Write(colors.Length);foreach(var c in colors){w.Write(c.r);w.Write(c.g);w.Write(c.b);w.Write(c.a);}for(int i=0;i<8;i++){var uv=new List<Vector4>();mesh.GetUVs(i,uv);w.Write(uv.Count);foreach(var t in uv){w.Write(t.x);w.Write(t.y);w.Write(t.z);w.Write(t.w);}}for(int i=0;i<mesh.subMeshCount;i++){w.Write((int)mesh.GetTopology(i));var t=mesh.GetIndices(i);w.Write(t.Length);foreach(int index in t)w.Write(index);}var b=mesh.bounds;foreach(float f in new[]{b.center.x,b.center.y,b.center.z,b.size.x,b.size.y,b.size.z})w.Write(f);w.Flush();return stream.ToArray();}
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
