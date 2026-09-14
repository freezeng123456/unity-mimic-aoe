using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class OutcropGeometryTests
    {
        [TestCase(.75f,0)] [TestCase(1.5f,1)] [TestCase(1.5f,7)] [TestCase(2.5f,2)]
        public void MeshHasClosedOutwardFacesAndExactGroundFootprint(float halfExtent,int variation)
        {
            var mesh=OutcropArt.CreateMesh(halfExtent,variation);
            try
            {
                var vertices=mesh.vertices;var normals=mesh.normals;var triangles=mesh.triangles;
                Assert.AreEqual(288,vertices.Length);Assert.AreEqual(96,triangles.Length/3);
                foreach(var v in vertices)
                {
                    foreach(var n in new[]{v.x,v.y,v.z})Assert.IsTrue(!float.IsNaN(n)&&!float.IsInfinity(n));
                    Assert.LessOrEqual(Mathf.Abs(v.x),halfExtent+.00001f);
                    Assert.LessOrEqual(Mathf.Abs(v.z),halfExtent+.00001f);Assert.GreaterOrEqual(v.y,0);
                }
                var counts=new Dictionary<string,int>();var directions=new Dictionary<string,int>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];
                    var cross=Vector3.Cross(b-a,c-a);Assert.Greater(cross.sqrMagnitude,1e-8f);
                    var normal=cross.normalized;
                    for(int j=0;j<3;j++)Assert.Greater(Vector3.Dot(normals[triangles[i+j]],normal),.9999f);
                    if(a.y==0&&b.y==0&&c.y==0)Assert.Less(normal.y,-.9999f,"Bottom closes downwards");
                    else if(i>=252)Assert.Greater(normal.y,0,"Crown faces upwards");
                    else Assert.Greater(Vector3.Dot(normal,new Vector3((a.x+b.x+c.x)/3,0,(a.z+b.z+c.z)/3)),0,"Side faces outwards");
                    for(int j=0;j<3;j++)
                    {
                        var from=Key(vertices[triangles[i+j]]);var to=Key(vertices[triangles[i+(j+1)%3]]);
                        int direction=string.CompareOrdinal(from,to)<0?1:-1;var key=direction>0?from+"|"+to:to+"|"+from;
                        if(!counts.ContainsKey(key)){counts[key]=0;directions[key]=0;}
                        counts[key]++;directions[key]+=direction;
                    }
                }
                foreach(var key in counts.Keys){Assert.AreEqual(2,counts[key],key);Assert.AreEqual(0,directions[key],"Welded edges pair in opposite directions: "+key);}
                foreach(float x in new[]{-halfExtent,halfExtent})foreach(float z in new[]{-halfExtent,halfExtent})
                    Assert.IsTrue(vertices.Any(v=>v==new Vector3(x,0,z)),"Bottom corner matches occupied cell boundary");
            }
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [Test] public void UnitySphereBaselineHasHalfUnitMeshRadius()
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                var bounds=go.GetComponent<MeshFilter>().sharedMesh.bounds;
                Assert.That(bounds.extents.x,Is.EqualTo(.5f).Within(.001f));Assert.That(bounds.extents.z,Is.EqualTo(.5f).Within(.001f));
                TestContext.WriteLine("Unity Sphere mesh bounds: "+bounds);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test] public void BlockedFootprintUsesOuterEdgesOfThreeByThreeOccupiedCells()
        {
            var grid=new GridNavigation();var point=new Vector2(64,35);
            var rect=GridNavigation.BlockedFootprint(point,1.8f);grid.SetBlocked(point,1.8f,true);
            Assert.AreEqual(new Rect(62.5f,33.5f,3,3),rect);
            foreach(var p in new[]{new Vector2(rect.xMin,35),new Vector2(rect.xMax,35),new Vector2(64,rect.yMin),new Vector2(64,rect.yMax)})
                Assert.IsFalse(grid.IsWalkable(p,.01f),"Occupied edge "+p);
            foreach(var p in new[]{new Vector2(rect.xMin-.03f,35),new Vector2(rect.xMax+.03f,35),new Vector2(64,rect.yMin-.03f),new Vector2(64,rect.yMax+.03f)})
                Assert.IsTrue(grid.IsWalkable(p,.01f),"Outside physical edge plus unit radius "+p);
        }
        [TestCase(.4f)] [TestCase(1.8f)] [TestCase(2.2f)]
        public void ExposedFootprintPreservesExistingCellBlocking(float radius)
        {
            var grid=new GridNavigation(32,32);var point=new Vector2(12.2f,13.7f);grid.SetBlocked(point,radius,true);
            int r=Mathf.FloorToInt(radius),cx=Mathf.RoundToInt(point.x),cy=Mathf.RoundToInt(point.y);
            for(int y=8;y<=20;y++)for(int x=6;x<=18;x++)
                Assert.AreEqual(!(Mathf.Abs(x-cx)<=r&&Mathf.Abs(y-cy)<=r),grid.IsWalkable(new Vector2(x,y),.001f),x+","+y);
            grid.SetBlocked(point,radius,false);
            for(int y=8;y<=20;y++)for(int x=6;x<=18;x++)Assert.IsTrue(grid.IsWalkable(new Vector2(x,y),.001f));
        }
        static string Key(Vector3 v)=>v.x.ToString("R",CultureInfo.InvariantCulture)+","+v.y.ToString("R",CultureInfo.InvariantCulture)+","+v.z.ToString("R",CultureInfo.InvariantCulture);
    }
}
