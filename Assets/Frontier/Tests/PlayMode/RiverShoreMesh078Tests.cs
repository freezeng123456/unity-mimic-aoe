using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Tests
{
    // A rendered triangle is a plane, not a second call to BankHeight. The
    // initial probe accepts either regular topology and reports, never awards
    // visual quality. Refinement acceptance is a separate, measured contract.
    public sealed class RiverShoreMesh078Tests
    {
        const float OcclusionTolerance=.002f;
        // Frozen from the ACTUAL 0.20.0 ProbeCoarse2 Unity output, JSON SHA
        // a08e8a2a863b11f3fc7b7e3b544d62c0fe15ffa9ccd87cd8a6d5683e4e0d14d7,
        // source bb276a1a312d6ecfbf08cc7d29bb5205ce3c9f36884263005b84b96f806f0070.
        // These pre-candidate identities are independent evidence, not values
        // minted by a refined implementation and called its own baseline.
        const string OriginalCoarseMesh="ba50da44c5dfa029da5d3d434f92f9620cc17dc2e13ce7fd8752e64b6bde1d71";
        const string OriginalWaterMesh="db35369358b0f77e5fa2de73d1a905e02a6caf1398177b3c3d5e96f03e83b319";
        const string OriginalAuthority="fedab0863f4e557d052866d2bc6fe47e4e4a8ef2a1dfffc1caaacb09289c6e86";
        const string OriginalBridges="6018ce7f67af844a8d176422a3d2b2f88a96beaecffc3b71c6b56886f16caca4";
        GameObject stage;World world;Transform terrain;Mesh ground,water;
        [SetUp] public void SetUp()
        {
            stage=new GameObject("Actual shoreline mesh078 contract");
            world=new World(Catalog.Load(),42,"coast","ottoman",5,MapTopology.Winding);
            terrain=WaterPresentation.BuildTerrain(stage.transform,world);
            ground=terrain.Find("Continuous sculpted land and river bed").GetComponent<MeshFilter>().sharedMesh;
            water=terrain.Find("Continuous navigable river").GetComponent<MeshFilter>().sharedMesh;
        }
        [TearDown] public void TearDown(){if(stage!=null)UnityEngine.Object.DestroyImmediate(stage);}

        [Serializable] sealed class Sample
        {
            public int segment,row;public float fraction,x,z,signedBankDistance,queryHeight,waterHeight,morphologyInfluence;
            public float coarseGroundHeight,candidateGroundHeight,coarseClearance,candidateClearance,coarseInterpolationError,candidateInterpolationError;
            public bool excluded,protectedProfile,focusEastOuter,authorityWater,exactQueryAtOrBelowWater,coarseTriangleOnlyOcclusion,candidateTriangleOnlyOcclusion;
            public string exclusion;
        }
        [Serializable] sealed class Summary
        {
            public string group,role;public int samples,authorityWater,authorityDry,exactQueryAtOrBelowWater,exactQueryAboveWater,triangleOnlyOcclusions,groundAboveWater;
            public float maximumPositiveClearance,maximumTriangleOnlyClearance,maximumInterpolationError;
            public Vector2 worstTriangleOnlyPosition;
        }
        [Serializable] sealed class SourceEntry {public string path,sha256;}
        [Serializable] sealed class Receipt
        {
            public string schema="river-shore-actual-mesh-probe078",status="diagnostic-not-visual-acceptance";
            public string purpose="Actual generated water-boundary endpoints and quarter-segment samples; actual rendered regular ground triangle barycentrics compared with a reconstructed077 1m grid using the SAME077 bank query. No historical old-build/native or performance claim.";
            public string classification="authorityWater = signedBankDistance<=0; exactQueryAtOrBelowWater = BankHeight<=waterHeight; triangleOnlyOcclusion = exact query below/equal water AND triangle>water+0.002m. Exact-query-high/authority-dry samples remain separately reported, not silently counted as an interpolation repair.";
            public string recordedAtUtc,appVersion,unityVersion,runnerExpectedFingerprint,sourceFingerprint,groundMeshSha256,coarseMeshSha256,waterMeshSha256,authorityGridSha256,bridgeGeometrySha256;
            public int seed,candidateGridCount,candidateVertexCount,candidateTriangleCount,coarseVertexCount,coarseTriangleCount,totalBoundarySamples,excludedBoundarySamples;
            public float candidateCellSize,occlusionTolerance;
            public Vector2 independentlySelectedEasternBend;
            public Summary[] summaries;public Sample[] samples;public SourceEntry[] sourceFiles;
        }

        [Test] public void ActualGroundWaterBoundaryProbeRecordsQueryAndBarycentricClearance()
        {
            int count=RegularGridCount(ground);Assert.That(count,Is.EqualTo(129).Or.EqualTo(257));
            CheckRegularTopology(ground,count);
            var coarse=ReconstructCoarseGround(world.Layout);
            try
            {
                CheckRegularTopology(coarse,129);
                if(count==129)Assert.AreEqual(MeshHash(coarse),MeshHash(ground),"Initial actual077 ground must match the reconstructed coarse control exactly");
                var channel=world.Layout.Channels.Single();float max=channel.Centers.Max(p=>p.x);
                var bend=channel.Centers.First(p=>p.x==max);var samples=MeasureBoundary(coarse,count,bend);
                var groups=new[]{"all-unclamped","unprotected","protected","focus-east-outer-unprotected"};var summaries=new List<Summary>();
                foreach(string group in groups)foreach(bool candidate in new[]{false,true})summaries.Add(Summarize(samples,group,candidate));
                Assert.Greater(samples.Count(s=>!s.excluded),2500,"Keep broad channel coverage, not just a favorable bend crop");
                Assert.Greater(samples.Count(s=>!s.excluded&&s.focusEastOuter&&!s.protectedProfile),50,"Known east/outer bend must be sampled");
                var entries=SourceEntries();string fp=SourceFingerprint(entries),expected=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT");
                if(!string.IsNullOrWhiteSpace(expected))Assert.AreEqual(expected,fp,"Runner/source identity at actual geometry probe");
                var receipt=new Receipt{recordedAtUtc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,unityVersion=Application.unityVersion,
                    seed=world.Seed,candidateGridCount=count,candidateCellSize=128f/(count-1),candidateVertexCount=ground.vertexCount,candidateTriangleCount=ground.triangles.Length/3,
                    coarseVertexCount=coarse.vertexCount,coarseTriangleCount=coarse.triangles.Length/3,totalBoundarySamples=samples.Count,excludedBoundarySamples=samples.Count(s=>s.excluded),
                    occlusionTolerance=OcclusionTolerance,independentlySelectedEasternBend=bend,groundMeshSha256=MeshHash(ground),coarseMeshSha256=MeshHash(coarse),
                    waterMeshSha256=MeshHash(water),authorityGridSha256=AuthorityHash(world),bridgeGeometrySha256=BridgeHash(terrain),
                    sourceFingerprint=fp,runnerExpectedFingerprint=expected,sourceFiles=entries,samples=samples.ToArray(),summaries=summaries.ToArray()};
                string output=Environment.GetEnvironmentVariable("FRONTIER_SHORE_STUDY_DIR");
                if(string.IsNullOrWhiteSpace(output))output=Path.Combine(Application.temporaryCachePath,"FrontierShoreMesh078-"+Guid.NewGuid().ToString("N"));
                output=Path.GetFullPath(output);Directory.CreateDirectory(output);Assert.IsFalse(Directory.EnumerateFileSystemEntries(output).Any(),"Use fresh study output; preserve prior probe data");
                File.WriteAllText(Path.Combine(output,"shore-mesh-probe078.json"),JsonUtility.ToJson(receipt,true),new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(output,"shore-boundary-samples078.csv"),Csv(samples),new UTF8Encoding(false));
                foreach(var summary in summaries)TestContext.WriteLine(JsonUtility.ToJson(summary));
                TestContext.WriteLine("Actual mesh078 diagnostic: "+output+" source="+fp+" count="+count+" water="+receipt.waterMeshSha256+" authority="+receipt.authorityGridSha256+" bridges="+receipt.bridgeGeometrySha256);
            }
            finally{UnityEngine.Object.DestroyImmediate(coarse);}
        }

        List<Sample> MeasureBoundary(Mesh coarse,int count,Vector2 bend)
        {
            var channel=world.Layout.Channels.Single();
            Assert.That(bend.x,Is.InRange(78,80));Assert.That(bend.y,Is.InRange(64,76));
            var points=water.vertices;Assert.AreEqual(channel.Centers.Length*5,points.Length);
            var samples=new List<Sample>();var currentVertices=ground.vertices;var coarseVertices=coarse.vertices;
            for(int i=0;i<channel.Centers.Length-1;i++)foreach(int row in new[]{0,4})for(int quarter=0;quarter<=4;quarter++)
            {
                float t=quarter*.25f;Vector3 a=points[i*5+row],b=points[(i+1)*5+row],point=Vector3.Lerp(a,b,t);
                var p=new Vector2(point.x,point.z);bool border=OnBorder(a)||OnBorder(b),degenerate=(new Vector2(b.x-a.x,b.z-a.z)).sqrMagnitude<.00000001f;
                float exact=world.Layout.BankHeight(p),h0=PlaneHeight(coarseVertices,129,p),h1=PlaneHeight(currentVertices,count,p);
                bool wet=exact<=point.y,excluded=border||degenerate;
                samples.Add(new Sample{segment=i,row=row,fraction=t,x=p.x,z=p.y,waterHeight=point.y,
                    signedBankDistance=world.Layout.SignedBankDistance(p),queryHeight=exact,morphologyInfluence=world.Layout.BankMorphologyInfluence(p),
                    protectedProfile=world.Layout.BankMorphologyInfluence(p)<=.000001f,focusEastOuter=p.x>bend.x&&Mathf.Abs(p.y-bend.y)<=10,
                    authorityWater=world.Layout.IsWater(p),exactQueryAtOrBelowWater=wet,excluded=excluded,exclusion=border?"clamped-map-border":degenerate?"collapsed-boundary-segment":"",
                    coarseGroundHeight=h0,candidateGroundHeight=h1,coarseClearance=h0-point.y,candidateClearance=h1-point.y,
                    coarseInterpolationError=h0-exact,candidateInterpolationError=h1-exact,
                    coarseTriangleOnlyOcclusion=!excluded&&wet&&h0-point.y>OcclusionTolerance,candidateTriangleOnlyOcclusion=!excluded&&wet&&h1-point.y>OcclusionTolerance});
            }
            return samples;
        }

        [Test] public void RefinedBoundaryRemovesMeasuredCoarseTriangleOcclusionsWithoutMovingAuthorityWaterOrBridges()
        {
            Assert.AreEqual("0.25.0",Application.version);Assert.AreEqual(257*257,ground.vertexCount);
            CheckRegularTopology(ground,257);var coarse=ReconstructCoarseGround(world.Layout);
            try
            {
                Assert.AreEqual(OriginalCoarseMesh,MeshHash(coarse),"The pre-candidate077 control must remain exact");
                Assert.AreEqual(OriginalWaterMesh,MeshHash(water),"No water strip displacement/width/normal/material-data disguise");
                Assert.AreEqual(OriginalAuthority,AuthorityHash(world),"Exact pre-candidate .5m query grid and both nav masks");
                Assert.AreEqual(OriginalBridges,BridgeHash(terrain),"All actual bridge transforms/mesh attributes/material names");
                var channel=world.Layout.Channels.Single();float max=channel.Centers.Max(p=>p.x);
                var bend=channel.Centers.First(p=>p.x==max);var samples=MeasureBoundary(coarse,257,bend);
                var oldAll=Summarize(samples,"all-unclamped",false);var newAll=Summarize(samples,"all-unclamped",true);
                var oldFocus=Summarize(samples,"focus-east-outer-unprotected",false);var newFocus=Summarize(samples,"focus-east-outer-unprotected",true);
                var protectedCurrent=Summarize(samples,"protected",true);
                foreach(var summary in new[]{oldAll,newAll,oldFocus,newFocus,protectedCurrent})TestContext.WriteLine(JsonUtility.ToJson(summary));
                Assert.AreEqual(3400,samples.Count);Assert.AreEqual(390,samples.Count(p=>p.excluded));
                Assert.AreEqual(3010,oldAll.samples);Assert.AreEqual(2783,oldAll.authorityWater);Assert.AreEqual(227,oldAll.authorityDry);
                Assert.AreEqual(0,oldAll.exactQueryAboveWater,"The measured issue must be triangle interpolation, not exact query-dry ground");
                Assert.AreEqual(54,oldAll.triangleOnlyOcclusions,"Actual initial coarse control was genuinely defective at54 samples");
                Assert.AreEqual(.020181715488433838f,oldAll.maximumTriangleOnlyClearance,.000001f);
                Assert.AreEqual(133,oldFocus.samples);Assert.AreEqual(18,oldFocus.triangleOnlyOcclusions);
                Assert.AreEqual(.009694486856460571f,oldFocus.maximumTriangleOnlyClearance,.000001f);
                // Absolute bound, not merely a relative improvement. Any residual
                // or new cluster above2mm fails, including away from the crop.
                Assert.AreEqual(0,newAll.triangleOnlyOcclusions,"No remaining/new >2mm interpolation occlusion in any unclamped sample");
                Assert.LessOrEqual(newAll.maximumPositiveClearance,OcclusionTolerance);
                Assert.AreEqual(0,newFocus.triangleOnlyOcclusions,"Known east/outer contact notches require their own gate");
                Assert.AreEqual(1167,protectedCurrent.samples);Assert.AreEqual(0,protectedCurrent.triangleOnlyOcclusions);
            }
            finally{UnityEngine.Object.DestroyImmediate(coarse);}
        }

        [Test] public void RefinedWindingMeshRetainsEveryCoarseVertexAndAllSharedQueryValues()
        {
            Assert.AreEqual("0.25.0",Application.version);Assert.AreEqual(257*257,ground.vertexCount);Assert.AreEqual(IndexFormat.UInt32,ground.indexFormat);
            Assert.IsEmpty(terrain.GetComponentsInChildren<Collider>());CheckRegularTopology(ground,257);CheckVertices(ground,world.Layout,257);
            var coarse=ReconstructCoarseGround(world.Layout);
            try
            {
                var refined=ground.vertices;var refinedColors=ground.colors;var refinedUv=ground.uv;
                var old=coarse.vertices;var oldColors=coarse.colors;var oldUv=coarse.uv;
                int protectedCount=0;
                for(int z=0;z<129;z++)for(int x=0;x<129;x++)
                {int before=x+z*129,after=x*2+z*2*257;Assert.AreEqual(old[before],refined[after]);Assert.AreEqual(oldColors[before],refinedColors[after]);Assert.AreEqual(oldUv[before],refinedUv[after]);}
                for(int i=0;i<refined.Length;i++)
                {
                    var p=new Vector2(refined[i].x,refined[i].z);if(world.Layout.BankMorphologyInfluence(p)>.000001f)continue;
                    Assert.AreEqual(HistoricalHeight(world.Layout,p),refined[i].y,.000001f,"Protected exact vertex height "+i);
                    AssertColor(HistoricalLayers(world.Layout,p),refinedColors[i],"Protected exact vertex layers "+i);protectedCount++;
                }
                Assert.Greater(protectedCount,1000);Assert.AreEqual(131072,ground.triangles.Length/3);
            }
            finally{UnityEngine.Object.DestroyImmediate(coarse);}
        }

        [Test] public void ActualWaterStripIsExactlyTheFrozen077FormulaAndHasNoAuthorityCollider()
        {
            var expected=ReconstructWater077(world.Layout.Channels.Single(),world.Layout.WaterHeight);
            try
            {
                Assert.AreEqual(MeshHash(expected),MeshHash(water),"All water vertices/colors/UV/triangles/normals/tangents/bounds must retain077 bytes");
                Assert.AreSame(WaterPresentation.WaterMaterial(),terrain.Find("Continuous navigable river").GetComponent<MeshRenderer>().sharedMaterial);
                Assert.AreEqual(ShadowCastingMode.Off,terrain.Find("Continuous navigable river").GetComponent<MeshRenderer>().shadowCastingMode);
                Assert.IsEmpty(terrain.GetComponentsInChildren<Collider>());
                for(int i=0;i<world.Layout.Bridges.Length;i++)
                {
                    var span=world.Layout.Bridges[i];var t=terrain.Find("Raised crossing "+i+" on sculpted banks");Assert.IsNotNull(t);
                    Assert.AreEqual(new Vector3(span.Center.x,0,span.Center.y),t.localPosition);
                    Assert.Less(Quaternion.Angle(Quaternion.Euler(0,-Mathf.Atan2(span.Direction.y,span.Direction.x)*Mathf.Rad2Deg,0),t.localRotation),.0001f);
                    Assert.AreEqual(Vector3.one,t.localScale);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(expected);}
        }

        [TestCase(MapTopology.Tropical)] [TestCase(MapTopology.Tributaries)] [TestCase(MapTopology.Estuary)]
        [TestCase(MapTopology.GeneratedRiver)] [TestCase(MapTopology.GeneratedDelta)] [TestCase(MapTopology.GeneratedStrait)]
        public void NonWindingGroundRemainsExactlyTheFrozenOneMeterFormula(string map)
        {
            var other=new World(Catalog.Load(),42,"coast","ottoman",5,map);var root=WaterPresentation.BuildTerrain(stage.transform,other);
            var mesh=root.Find("Continuous sculpted land and river bed").GetComponent<MeshFilter>().sharedMesh;
            bool confluence=Application.version=="0.25.0"&&map==MapTopology.Tributaries;
            var expected=ReconstructCoarseGround(other.Layout,confluence);
            try
            {Assert.AreEqual(129*129,mesh.vertexCount);Assert.AreEqual(MeshHash(expected),MeshHash(mesh),confluence?"082 Tributaries must use the measured center-error diagonal policy":"No presentation-density or geometry change to "+map);CheckRegularTopology(mesh,129,confluence,other.Layout);CheckVertices(mesh,other.Layout,129);}
            finally{UnityEngine.Object.DestroyImmediate(expected);}
        }

        static bool OnBorder(Vector3 p)=>p.x<=0||p.x>=128||p.z<=0||p.z>=128;
        static int RegularGridCount(Mesh mesh)
        {int count=Mathf.RoundToInt(Mathf.Sqrt(mesh.vertexCount));Assert.AreEqual(count*count,mesh.vertexCount);return count;}
        static void CheckRegularTopology(Mesh mesh,int count,bool confluence=false,MapLayout layout=null)
        {
            Assert.AreEqual(1,mesh.subMeshCount);var v=mesh.vertices;var t=mesh.triangles;
            Assert.AreEqual((count-1)*(count-1)*6,t.Length);float cell=128f/(count-1);int triangle=0;
            for(int z=0;z<count;z++)for(int x=0;x<count;x++)
            {var p=v[x+z*count];Assert.AreEqual(x*cell,p.x);Assert.AreEqual(z*cell,p.z);Assert.IsFalse(float.IsNaN(p.y)||float.IsInfinity(p.y));}
            for(int z=0;z<count-1;z++)for(int x=0;x<count-1;x++)
            {
                int a=x+z*count,b=a+count,c=b+1,d=a+1;
                bool reverse=confluence&&Mathf.Abs((v[b].y+v[d].y)*.5f-layout.BankHeight(new Vector2(x+.5f,z+.5f)))+.000001f<Mathf.Abs((v[a].y+v[c].y)*.5f-layout.BankHeight(new Vector2(x+.5f,z+.5f)));
                foreach(int expected in (reverse?new[]{a,b,d,b,c,d}:new[]{a,b,c,a,c,d})){Assert.AreEqual(expected,t[triangle++],confluence?"Measured center-error diagonal topology":"Actual fixed-diagonal topology");Assert.That(expected,Is.InRange(0,v.Length-1));}
                Assert.Greater(Vector3.Cross(v[b]-v[a],v[c]-v[a]).sqrMagnitude,.0000001f);
                Assert.Greater(Vector3.Cross(v[c]-v[a],v[d]-v[a]).sqrMagnitude,.0000001f);
            }
        }
        static void CheckVertices(Mesh mesh,MapLayout layout,int count)
        {
            var v=mesh.vertices;var color=mesh.colors;var uv=mesh.uv;Assert.AreEqual(v.Length,color.Length);Assert.AreEqual(v.Length,uv.Length);
            for(int i=0;i<v.Length;i++)
            {var p=new Vector2(v[i].x,v[i].z);Assert.AreEqual(layout.BankHeight(p),v[i].y,.000001f,"Public bank height "+i);AssertColor(layout.BankLayers(p),color[i],"Public layers "+i);Assert.AreEqual(p/128,uv[i]);}
        }
        // Actual selected triangle's xz barycentrics. Topology is checked in full
        // before use, so an index/diagonal mutation is not masked by this oracle.
        static float PlaneHeight(Vector3[] vertices,int count,Vector2 point)
        {
            float cell=128f/(count-1),px=Mathf.Clamp(point.x/cell,0,count-1),pz=Mathf.Clamp(point.y/cell,0,count-1);
            int x=Mathf.Min(count-2,Mathf.FloorToInt(px)),z=Mathf.Min(count-2,Mathf.FloorToInt(pz)),a=x+z*count,b=a+count,c=b+1,d=a+1;
            float tx=px-x,tz=pz-z;
            var p0=vertices[a];var p1=vertices[tz>=tx?b:c];var p2=vertices[tz>=tx?c:d];
            float denominator=(p1.z-p2.z)*(p0.x-p2.x)+(p2.x-p1.x)*(p0.z-p2.z);
            Assert.Greater(Mathf.Abs(denominator),.000001f);
            float w0=((p1.z-p2.z)*(point.x-p2.x)+(p2.x-p1.x)*(point.y-p2.z))/denominator;
            float w1=((p2.z-p0.z)*(point.x-p2.x)+(p0.x-p2.x)*(point.y-p2.z))/denominator;
            return w0*p0.y+w1*p1.y+(1-w0-w1)*p2.y;
        }
        static Summary Summarize(IEnumerable<Sample> input,string group,bool candidate)
        {
            var values=input.Where(s=>!s.excluded&&(group!="protected"||s.protectedProfile)&&(group!="unprotected"||!s.protectedProfile)&&
                (group!="focus-east-outer-unprotected"||s.focusEastOuter&&!s.protectedProfile)).ToArray();
            var result=new Summary{group=group,role=candidate?"actual-candidate":"reconstructed077-coarse1m",samples=values.Length,
                authorityWater=values.Count(s=>s.authorityWater),authorityDry=values.Count(s=>!s.authorityWater),exactQueryAtOrBelowWater=values.Count(s=>s.exactQueryAtOrBelowWater),exactQueryAboveWater=values.Count(s=>!s.exactQueryAtOrBelowWater)};
            foreach(var sample in values)
            {
                float clearance=candidate?sample.candidateClearance:sample.coarseClearance,error=candidate?sample.candidateInterpolationError:sample.coarseInterpolationError;
                bool occlusion=candidate?sample.candidateTriangleOnlyOcclusion:sample.coarseTriangleOnlyOcclusion;
                if(clearance>OcclusionTolerance)result.groundAboveWater++;result.maximumPositiveClearance=Mathf.Max(result.maximumPositiveClearance,clearance);result.maximumInterpolationError=Mathf.Max(result.maximumInterpolationError,error);
                if(!occlusion)continue;result.triangleOnlyOcclusions++;
                if(clearance>result.maximumTriangleOnlyClearance){result.maximumTriangleOnlyClearance=clearance;result.worstTriangleOnlyPosition=new Vector2(sample.x,sample.z);}
            }
            return result;
        }
        static Mesh ReconstructCoarseGround(MapLayout layout,bool confluence=false)
        {
            const int count=129;var v=new Vector3[count*count];var uv=new Vector2[v.Length];var color=new Color[v.Length];var triangles=new int[128*128*6];int n=0;
            for(int z=0;z<count;z++)for(int x=0;x<count;x++){int i=x+z*count;var p=new Vector2(x,z);v[i]=new Vector3(x,layout.BankHeight(p),z);uv[i]=p/128;color[i]=layout.BankLayers(p);}
            for(int z=0;z<128;z++)for(int x=0;x<128;x++){int a=x+z*count,b=a+count,c=b+1,d=a+1;var p=new Vector2(x+.5f,z+.5f);bool reverse=confluence&&Mathf.Abs((v[b].y+v[d].y)*.5f-layout.BankHeight(p))+.000001f<Mathf.Abs((v[a].y+v[c].y)*.5f-layout.BankHeight(p));foreach(int i in (reverse?new[]{a,b,d,b,c,d}:new[]{a,b,c,a,c,d}))triangles[n++]=i;}
            var mesh=new Mesh{name="Reconstructed077 one meter ground control",indexFormat=IndexFormat.UInt32};mesh.vertices=v;mesh.uv=uv;mesh.colors=color;mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        static Mesh ReconstructWater077(RiverChannel channel,float waterHeight)
        {
            // Frozen077 literal strip formula. No production BuildChannel call.
            const int rows=5;int samples=channel.Centers.Length;var v=new Vector3[samples*rows];var uv=new Vector2[v.Length];var colors=new Color[v.Length];var triangles=new List<int>();float arc=0;
            for(int i=0;i<samples;i++)
            {
                var center=channel.Centers[i];if(i>0)arc+=Vector2.Distance(center,channel.Centers[i-1]);
                var tangent=(channel.Centers[Mathf.Min(samples-1,i+1)]-channel.Centers[Mathf.Max(0,i-1)]).normalized;var across=new Vector2(tangent.y,-tangent.x);
                for(int row=0;row<rows;row++){float fraction=row/(float)(rows-1),offset=(fraction*2-1)*channel.HalfWidths[i];var p=center+across*offset;int n=i*rows+row;v[n]=new Vector3(Mathf.Clamp(p.x,0,128),waterHeight,Mathf.Clamp(p.y,0,128));uv[n]=new Vector2(fraction,arc/12);colors[n]=new Color(Mathf.Sqrt(1-Mathf.Abs(fraction*2-1)),0,0,1);}
            }
            for(int i=0;i<samples-1;i++)for(int row=0;row<rows-1;row++)
            {int a=i*rows+row,b=a+rows,c=b+1,d=a+1;if(Vector3.Cross(v[b]-v[a],v[c]-v[a]).sqrMagnitude>.0000001f)triangles.AddRange(new[]{a,b,c});if(Vector3.Cross(v[c]-v[a],v[d]-v[a]).sqrMagnitude>.0000001f)triangles.AddRange(new[]{a,c,d});}
            var mesh=new Mesh{name="Frozen077 water strip"};mesh.vertices=v;mesh.uv=uv;mesh.colors=colors;mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        static float HistoricalHeight(MapLayout layout,Vector2 p)
        {float d=layout.SignedBankDistance(p);if(d<=0)return Mathf.Lerp(layout.WaterHeight-.02f,-.9f,Mathf.SmoothStep(0,1,-d/4));return (layout.WaterHeight-.02f)*(1-Mathf.SmoothStep(0,1,d/3));}
        static Color HistoricalLayers(MapLayout layout,Vector2 p)
        {float d=layout.SignedBankDistance(p),noise=Mathf.PerlinNoise(p.x*.13f+3,p.y*.11f+8)-.5f;float damp=1-Mathf.SmoothStep(0,1,(d-.25f-noise*.25f)/2.6f),shoulder=1-Mathf.SmoothStep(0,1,(d-1.5f-noise*.65f)/4);float sand=(1-damp)*shoulder;return new Color(damp,sand,1-damp-sand,1);}
        static void AssertColor(Color a,Color b,string reason)
        {Assert.AreEqual(a.r,b.r,.000001f,reason);Assert.AreEqual(a.g,b.g,.000001f,reason);Assert.AreEqual(a.b,b.b,.000001f,reason);Assert.AreEqual(a.a,b.a,.000001f,reason);}
        static string Csv(IEnumerable<Sample> values)
        {
            var s=new StringBuilder("segment,row,fraction,x,z,excluded,exclusion,protectedProfile,focusEastOuter,authorityWater,signedBankDistance,queryHeight,waterHeight,morphologyInfluence,exactQueryAtOrBelowWater,coarseGroundHeight,candidateGroundHeight,coarseClearance,candidateClearance,coarseInterpolationError,candidateInterpolationError,coarseTriangleOnlyOcclusion,candidateTriangleOnlyOcclusion\n");
            foreach(var p in values){object[] fields={p.segment,p.row,p.fraction,p.x,p.z,p.excluded,p.exclusion,p.protectedProfile,p.focusEastOuter,p.authorityWater,p.signedBankDistance,p.queryHeight,p.waterHeight,p.morphologyInfluence,p.exactQueryAtOrBelowWater,p.coarseGroundHeight,p.candidateGroundHeight,p.coarseClearance,p.candidateClearance,p.coarseInterpolationError,p.candidateInterpolationError,p.coarseTriangleOnlyOcclusion,p.candidateTriangleOnlyOcclusion};s.AppendLine(string.Join(",",fields.Select(v=>v is float?((float)v).ToString("R",CultureInfo.InvariantCulture):Convert.ToString(v,CultureInfo.InvariantCulture))));}return s.ToString();
        }
        static string MeshHash(Mesh mesh)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s))
            {
                w.Write(mesh.vertexCount);foreach(var v in mesh.vertices){w.Write(v.x);w.Write(v.y);w.Write(v.z);}foreach(var c in mesh.colors){w.Write(c.r);w.Write(c.g);w.Write(c.b);w.Write(c.a);}
                foreach(var n in mesh.normals){w.Write(n.x);w.Write(n.y);w.Write(n.z);}foreach(var t in mesh.tangents){w.Write(t.x);w.Write(t.y);w.Write(t.z);w.Write(t.w);}foreach(var uv in mesh.uv){w.Write(uv.x);w.Write(uv.y);}foreach(int i in mesh.triangles)w.Write(i);
                var b=mesh.bounds;w.Write(b.center.x);w.Write(b.center.y);w.Write(b.center.z);w.Write(b.size.x);w.Write(b.size.y);w.Write(b.size.z);w.Flush();return Hash(s.ToArray());
            }
        }
        static string AuthorityHash(World world)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s))
            {
                var layout=world.Layout;w.Write(layout.WaterHeight);
                for(int z=0;z<257;z++)for(int x=0;x<257;x++){var p=new Vector2(x*.5f,z*.5f);w.Write(layout.SignedBankDistance(p));w.Write(layout.BankHeight(p));w.Write(layout.GroundHeight(p));w.Write(layout.IsWater(p));w.Write(layout.IsBridge(p));w.Write(layout.IsBridgeApproach(p));w.Write(layout.IsBridgeSideBarrierCell(p));var c=layout.BankLayers(p);w.Write(c.r);w.Write(c.g);w.Write(c.b);w.Write(c.a);}
                foreach(var n in new[]{world.Navigation.CaptureState(),world.WaterNavigation.CaptureState()}){w.Write(n.width);w.Write(n.height);foreach(bool v in n.terrain)w.Write(v);foreach(bool v in n.blocked)w.Write(v);}
                foreach(var channel in layout.Channels){w.Write(channel.Centers.Length);for(int i=0;i<channel.Centers.Length;i++){w.Write(channel.Centers[i].x);w.Write(channel.Centers[i].y);w.Write(channel.HalfWidths[i]);}}
                foreach(var sites in layout.DockSites)foreach(var p in sites){w.Write(p.x);w.Write(p.y);}foreach(var p in layout.FishPoints){w.Write(p.x);w.Write(p.y);}w.Flush();return Hash(s.ToArray());
            }
        }
        static string BridgeHash(Transform terrain)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s))
            {
                foreach(var bridge in terrain.Cast<Transform>().Where(t=>t.name.StartsWith("Raised crossing ",StringComparison.Ordinal)))foreach(var t in bridge.GetComponentsInChildren<Transform>(true))
                {w.Write(t.name);var p=t.localPosition;var r=t.localRotation;var scale=t.localScale;w.Write(p.x);w.Write(p.y);w.Write(p.z);w.Write(r.x);w.Write(r.y);w.Write(r.z);w.Write(r.w);w.Write(scale.x);w.Write(scale.y);w.Write(scale.z);var filter=t.GetComponent<MeshFilter>();if(filter!=null)w.Write(MeshHash(filter.sharedMesh));var renderer=t.GetComponent<MeshRenderer>();if(renderer!=null)foreach(var material in renderer.sharedMaterials)w.Write(material.name);}
                w.Flush();return Hash(s.ToArray());
            }
        }
        static SourceEntry[] SourceEntries()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));var entries=new List<SourceEntry>();
            foreach(string directory in new[]{"Assets","Packages","ProjectSettings","Tools"})foreach(string path in Directory.GetFiles(Path.Combine(root,directory),"*",SearchOption.AllDirectories))
            {string relative=path.Substring(root.Length+1).Replace('\\','/');if(relative.Split('/').Any(p=>p.StartsWith(".",StringComparison.Ordinal)||p=="__pycache__"))continue;if(System.Text.RegularExpressions.Regex.IsMatch(relative,@"^Assets/InitTestScene[0-9a-f-]{36}\.unity(\.meta)?$"))continue;entries.Add(new SourceEntry{path=relative,sha256=Hash(File.ReadAllBytes(path))});}
            return entries.OrderBy(e=>e.path,StringComparer.Ordinal).ToArray();
        }
        static string SourceFingerprint(SourceEntry[] entries)
        {
            var text=new StringBuilder("{");for(int i=0;i<entries.Length;i++){if(i>0)text.Append(',');Quote(text,entries[i].path);text.Append(':');Quote(text,entries[i].sha256);}text.Append('}');return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }
        static void Quote(StringBuilder text,string value)
        {
            text.Append('"');foreach(char c in value){switch(c){case '"':text.Append("\\\"");break;case '\\':text.Append("\\\\");break;case '\b':text.Append("\\b");break;case '\f':text.Append("\\f");break;case '\n':text.Append("\\n");break;case '\r':text.Append("\\r");break;case '\t':text.Append("\\t");break;default:if(c<32||c>126)text.Append("\\u").Append(((int)c).ToString("x4"));else text.Append(c);break;}}text.Append('"');
        }
        static string Hash(byte[] bytes){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
