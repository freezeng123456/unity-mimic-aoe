using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Actual generated MeshFilter/sharedMesh integration, not a rendered-frame
    // or native-client claim. Retain the 077 shared-query/palette contracts in
    // the 078 runtime; historical 077 receipts remain bound to their old source.
    public sealed class RiverBankMesh077Tests
    {
        GameObject stage;
        World world;
        Transform terrain;
        Mesh ground;
        [SetUp] public void SetUp()
        {
            Assert.AreEqual("0.25.0",Application.version,"Apply the 082 version identity before retained bank-mesh acceptance");
            stage=new GameObject("Shared Winding bank mesh 077");
            world=new World(Catalog.Load(),44,"ottoman","coast",5,MapTopology.Winding);
            terrain=WaterPresentation.BuildTerrain(stage.transform,world);
            ground=terrain.Find("Continuous sculpted land and river bed").GetComponent<MeshFilter>().sharedMesh;
        }
        [TearDown] public void TearDown(){if(stage!=null)Object.DestroyImmediate(stage);}
        [Test] public void WindingSedimentPaletteHasItsOwnMaterialAndDoesNotLeakIntoOtherRiverMaps()
        {
            var material=terrain.Find("Continuous sculpted land and river bed").GetComponent<MeshRenderer>().sharedMaterial;
            AssertPalette(new Color(.74f,.67f,.48f),material.GetColor("_SandColor"),"Initial Winding");
            foreach(var map in new[]{MapTopology.Tributaries,MapTopology.Estuary,MapTopology.Tropical})
            {
                var other=WaterPresentation.BuildTerrain(stage.transform,new World(Catalog.Load(),44,"coast","ottoman",5,map));
                var otherMaterial=other.Find("Continuous sculpted land and river bed").GetComponent<MeshRenderer>().sharedMaterial;
                Assert.AreNotSame(material,otherMaterial,"Winding cannot recolor "+map+" via cached material");
                AssertPalette(map==MapTopology.Tropical?new Color(.66f,.57f,.38f):new Color(.62f,.555f,.37f),otherMaterial.GetColor("_SandColor"),map.ToString());
            }
            AssertPalette(new Color(.74f,.67f,.48f),material.GetColor("_SandColor"),"Winding after other-map construction");
        }
        static void AssertPalette(Color expected,Color actual,string label)
        {
            // Material Color properties can roundtrip with tiny float changes.
            // Keep strict per-channel bounds and print full precision; material
            // identity/other-map isolation and mesh/geographic checks stay exact.
            TestContext.WriteLine(label+" expected="+expected.r.ToString("R")+","+expected.g.ToString("R")+","+expected.b.ToString("R")+","+expected.a.ToString("R")+
                " actual="+actual.r.ToString("R")+","+actual.g.ToString("R")+","+actual.b.ToString("R")+","+actual.a.ToString("R"));
            Assert.AreEqual(expected.r,actual.r,.000001f,label+" red");
            Assert.AreEqual(expected.g,actual.g,.000001f,label+" green");
            Assert.AreEqual(expected.b,actual.b,.000001f,label+" blue");
            Assert.AreEqual(expected.a,actual.a,.000001f,label+" alpha");
        }
        [Test] public void ActualGroundMeshColorsAndHeightsEqualTheSharedMapQueries()
        {
            Assert.AreEqual(257*257,ground.vertexCount);Assert.AreEqual(1,ground.subMeshCount);
            Assert.IsEmpty(terrain.GetComponentsInChildren<Collider>());
            var points=ground.vertices;var colors=ground.colors;Assert.AreEqual(points.Length,colors.Length);
            int genuinelyChangedLayers=0;
            for(int i=0;i<points.Length;i++)
            {
                var point=new Vector2(points[i].x,points[i].z);var expected=world.Layout.BankLayers(point);var actual=colors[i];
                Assert.AreEqual(world.Layout.BankHeight(point),points[i].y,.000001f,"Shared height at vertex "+i);
                Assert.AreEqual(expected.r,actual.r,.000001f,"Shared mud at vertex "+i);
                Assert.AreEqual(expected.g,actual.g,.000001f,"Shared sand at vertex "+i);
                Assert.AreEqual(expected.b,actual.b,.000001f,"Shared turf at vertex "+i);
                Assert.AreEqual(expected.a,actual.a,.000001f);
                Assert.That(actual.r,Is.InRange(0,1));Assert.That(actual.g,Is.InRange(0,1));Assert.That(actual.b,Is.InRange(0,1));
                Assert.AreEqual(1,actual.r+actual.g+actual.b,.000001f);
                // Independent literal historical formula detects a disconnected
                // BuildGround call site that still writes the old shore ribbon.
                float d=world.Layout.SignedBankDistance(point),noise=Mathf.PerlinNoise(point.x*.13f+3,point.y*.11f+8)-.5f;
                float oldDamp=1-Mathf.SmoothStep(0,1,(d-.25f-noise*.25f)/2.6f);
                float oldShoulder=1-Mathf.SmoothStep(0,1,(d-1.5f-noise*.65f)/4);
                float oldSand=(1-oldDamp)*oldShoulder;
                if(Mathf.Abs(actual.g-oldSand)>.05f)genuinelyChangedLayers++;
            }
            Assert.Greater(genuinelyChangedLayers,50,"The actual mesh must carry meaningful new colors, not only normalized historical colors");
            TestContext.WriteLine("Actual shared-query ground mesh: vertices="+points.Length+" changedSandVertices="+genuinelyChangedLayers);
        }
        [Test] public void ActualEasternMeanderMeshHasBroaderSandOnItsLiteralWestInnerBank()
        {
            var channel=world.Layout.Channels[0];float maximum=channel.Centers.Max(p=>p.x);
            var center=channel.Centers.First(p=>p.x==maximum);
            Assert.That(center.x,Is.InRange(78,80));Assert.That(center.y,Is.InRange(64,76));
            // Frozen original controls (68,50)->(78,64)->(76,76) turn left
            // northward: west is the inner bank, east the outer bank. Neither
            // classification nor amplitude uses BankProfile/BankLayers.
            var points=ground.vertices;var colors=ground.colors;float west=0,east=0;int westCount=0,eastCount=0;
            for(int i=0;i<points.Length;i++)
            {
                var p=new Vector2(points[i].x,points[i].z);
                if(Mathf.Abs(p.y-center.y)>2||Mathf.Abs(p.x-center.x)>18)continue;
                float distance=world.Layout.SignedBankDistance(p);if(distance<3||distance>4.5f)continue;
                if(p.x<center.x){west+=colors[i].g;westCount++;}
                else{east+=colors[i].g;eastCount++;}
            }
            Assert.GreaterOrEqual(westCount,4);Assert.GreaterOrEqual(eastCount,4);
            float westMean=west/westCount,eastMean=east/eastCount;
            Assert.Greater(westMean,.55f,"Actual inner sediment beach must have useful sand amplitude");
            Assert.Less(eastMean,.35f,"Actual outer cut-bank shoulder must remain distinctly narrower");
            Assert.Greater(westMean-eastMean,.30f,"Independent actual-mesh inside/outside contrast");
            TestContext.WriteLine("Eastern meander actual mesh: westInnerCount="+westCount+" sand="+westMean+" eastOuterCount="+eastCount+" sand="+eastMean);
        }
    }
}
