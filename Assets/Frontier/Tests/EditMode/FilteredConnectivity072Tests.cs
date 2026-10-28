using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // A negative-only preflight must agree with the public, unchanged route search.
    // Positive answers never authorize a shortcut or replace actual swept paths.
    public sealed class FilteredConnectivity072Tests
    {
        static GridNavigation ThreatPassage()
        {
            var navigation=new GridNavigation(32,24);
            navigation.ConfigureTerrain(p=>p.x==16&&(p.y<10||p.y>14));
            return navigation;
        }
        static float SegmentDistance(Vector2 a,Vector2 b,Vector2 point)
        {
            Vector2 delta=b-a;
            float t=delta.sqrMagnitude>.000001f?Mathf.Clamp01(Vector2.Dot(point-a,delta)/delta.sqrMagnitude):0;
            return Vector2.Distance(a+delta*t,point);
        }
        static bool OutsideDiscs(Vector2 point,Vector2[] centers,float radius)
        {
            foreach(var center in centers)if((point-center).sqrMagnitude<radius*radius)return false;
            return true;
        }
        static bool OutsideDiscs(Vector2 a,Vector2 b,Vector2[] centers,float radius)
        {
            foreach(var center in centers)if(SegmentDistance(a,b,center)<radius)return false;
            return true;
        }
        static void AssertActualRoute(GridNavigation navigation,List<Vector2> path,Vector2 start,Vector2 end,
            float radius,Func<Vector2,bool> pointFree,Func<Vector2,Vector2,bool> segmentFree)
        {
            Assert.GreaterOrEqual(path.Count,2,"The original route must actually exist");
            Assert.Less(Vector2.Distance(navigation.NearestWalkable(start,radius),path[0]),.0001f);
            Assert.Less(Vector2.Distance(navigation.NearestWalkable(end,radius),path[path.Count-1]),.0001f);
            for(int i=1;i<path.Count;i++)
            {
                Vector2 a=path[i-1],b=path[i];
                Assert.IsTrue(navigation.ClearLine(a,b,radius),"Actual route must preserve continuous body/bank clearance");
                if(segmentFree!=null)Assert.IsTrue(segmentFree(a,b),"The original route must still respect its edge filter");
                int samples=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)*40));
                for(int n=0;n<=samples;n++)
                {
                    Vector2 point=Vector2.Lerp(a,b,n/(float)samples);
                    Assert.IsTrue(navigation.IsWalkable(point,radius),"Fine body samples must not cut a bank or wall");
                    if(pointFree!=null)Assert.IsTrue(pointFree(point),"Fine samples on this actual route must retain the declared threat clearance");
                }
            }
        }

        [TestCase(.35f,false)] [TestCase(.65f,true)]
        public void APhysicallyOpenPassageClosedByCurrentThreatRejectsOnlyAnActuallyEmptyFilteredRoute(float radius,bool continuousFilter)
        {
            var navigation=ThreatPassage();Vector2 start=new Vector2(6,12),end=new Vector2(25,12),threat=new Vector2(16,12);
            bool PointFree(Vector2 p)=>(p-threat).sqrMagnitude>=16;
            bool SegmentFree(Vector2 a,Vector2 b)=>SegmentDistance(a,b,threat)>=4;
            Func<Vector2,Vector2,bool> edge=continuousFilter?(Func<Vector2,Vector2,bool>)SegmentFree:null;
            Assert.IsTrue(PointFree(start));Assert.IsTrue(PointFree(end));
            Assert.GreaterOrEqual(navigation.FindPath(start,end,radius).Count,2,
                "Physical navigation has a real opening; the immutable visible threat closes it");
            var original=navigation.FindPath(start,end,radius,PointFree,edge);
            Assert.IsEmpty(original,"A point-only or continuous filter must really close this entire five-cell passage");
            var context=navigation.CreateFilteredConnectivity(radius,PointFree,edge);
            Assert.IsFalse(context.MayConnect(start,end),"This repeated sealed query should be rejected before another full search");
        }

        [Test] public void FractionalEndpointEscapesAnIsolatedNearestNodeAndCannotBeRejected()
        {
            var navigation=new GridNavigation(32,32);Vector2 start=new Vector2(20.25f,20.25f),end=new Vector2(25,20.1f);
            var discs=new[]{new Vector2(21,20),new Vector2(19,20),new Vector2(20,21),new Vector2(20,19)};
            bool PointFree(Vector2 p)=>OutsideDiscs(p,discs,.4f);
            bool SegmentFree(Vector2 a,Vector2 b)=>OutsideDiscs(a,b,discs,.4f);
            Assert.IsTrue(PointFree(start));Assert.IsFalse(SegmentFree(start,end));
            Assert.IsTrue(SegmentFree(start,new Vector2(21,21)),"The recorded fractional endpoint has a genuine farther swept connection");
            var original=navigation.FindPath(start,end,.4f,PointFree,SegmentFree);
            Assert.GreaterOrEqual(original.Count,3);
            AssertActualRoute(navigation,original,start,end,.4f,PointFree,SegmentFree);
            var context=navigation.CreateFilteredConnectivity(.4f,PointFree,SegmentFree);
            Assert.IsTrue(context.MayConnect(start,end),"Nearest-node isolation cannot erase the original endpoint alternatives");
        }

        [Test] public void DirectionDependentEdgesRetainTheRealWestboundDetourAndDoNotAuthorizeTheReverseRoute()
        {
            var navigation=new GridNavigation(32,24);
            navigation.ConfigureTerrain(p=>p.x==16&&p.y!=16);
            Vector2 start=new Vector2(25,6),end=new Vector2(6,6);
            bool PointFree(Vector2 p)=>true;
            bool WestOrVertical(Vector2 a,Vector2 b)=>b.x<=a.x+.000001f;
            Assert.IsFalse(navigation.ClearLine(start,end,.4f),"The valid route has to use a real gap around the wall");
            var original=navigation.FindPath(start,end,.4f,PointFree,WestOrVertical);
            Assert.GreaterOrEqual(original.Count,3);
            AssertActualRoute(navigation,original,start,end,.4f,PointFree,WestOrVertical);
            var context=navigation.CreateFilteredConnectivity(.4f,PointFree,WestOrVertical);
            Assert.IsTrue(context.MayConnect(start,end),"A weak connectivity proof must not erase a directed valid path");
            // MayConnect is only a conservative preflight. The public directed
            // search remains responsible for a reverse journey and rejects it.
            Assert.IsEmpty(navigation.FindPath(end,start,.4f,PointFree,WestOrVertical));
        }

        [Test] public void NavigationVersionChangeDisablesAnEarlierNegativeProofAndARealNewOpeningBecomesReachable()
        {
            var navigation=ThreatPassage();Vector2 start=new Vector2(6,12),end=new Vector2(25,12),threat=new Vector2(16,12);
            bool PointFree(Vector2 p)=>(p-threat).sqrMagnitude>=16;
            bool SegmentFree(Vector2 a,Vector2 b)=>SegmentDistance(a,b,threat)>=4;
            var previous=navigation.CreateFilteredConnectivity(.4f,PointFree,SegmentFree);
            Assert.IsFalse(previous.MayConnect(start,end));int version=navigation.Version;
            navigation.SetBlocked(new Vector2(3,3),0,true);
            Assert.Greater(navigation.Version,version);
            Assert.IsTrue(previous.MayConnect(start,end),"Even an unrelated navigation change invalidates this context's negative authority");
            Assert.IsEmpty(navigation.FindPath(start,end,.4f,PointFree,SegmentFree));
            var stillClosed=navigation.CreateFilteredConnectivity(.4f,PointFree,SegmentFree);
            Assert.IsFalse(stillClosed.MayConnect(start,end),"A new query can independently prove the unchanged blockade");
            for(int y=5;y<=7;y++)navigation.SetTerrainBlocked(new Vector2(16,y),0,false);
            Assert.IsTrue(stillClosed.MayConnect(start,end),"A freshly opened real route cannot be denied by a stale context");
            var original=navigation.FindPath(start,end,.4f,PointFree,SegmentFree);
            AssertActualRoute(navigation,original,start,end,.4f,PointFree,SegmentFree);
            Assert.IsTrue(navigation.CreateFilteredConnectivity(.4f,PointFree,SegmentFree).MayConnect(start,end));
        }

        [Test] public void SeparateRadiiAndIndependentFiltersPreserveTheActualSingleCellBridgeWithoutChangingNavigation()
        {
            var navigation=new GridNavigation(40,26);
            navigation.ConfigureTerrain(p=>p.x==20&&p.y!=12);
            Vector2 start=new Vector2(5,12),end=new Vector2(34,12);
            bool Open(Vector2 p)=>true;
            bool FilteredBridge(Vector2 p)=>Mathf.Abs(p.x-20)>=.6f;
            string before=JsonUtility.ToJson(navigation.CaptureState());int version=navigation.Version;
            var small=navigation.CreateFilteredConnectivity(.25f,Open);
            var large=navigation.CreateFilteredConnectivity(.65f,Open);
            var blocked=navigation.CreateFilteredConnectivity(.25f,FilteredBridge);
            Assert.IsEmpty(navigation.FindPath(start,end,.65f,Open));Assert.IsFalse(large.MayConnect(start,end));
            Assert.IsEmpty(navigation.FindPath(start,end,.25f,FilteredBridge));Assert.IsFalse(blocked.MayConnect(start,end));
            var original=navigation.FindPath(start,end,.25f,Open);
            AssertActualRoute(navigation,original,start,end,.25f,Open,null);
            Assert.IsTrue(small.MayConnect(start,end),"A larger body or another filter cannot poison the small body's context");
            Assert.IsFalse(large.MayConnect(start,end));Assert.IsFalse(blocked.MayConnect(start,end));
            Assert.IsTrue(small.MayConnect(start,end));
            Assert.AreEqual(version,navigation.Version);Assert.AreEqual(before,JsonUtility.ToJson(navigation.CaptureState()),
                "Queries must not rewrite the public obstacle snapshot");
        }

        [Test] public void SameVersionRestorationCannotReuseASealedWallProofAgainstARealNewDetour()
        {
            var navigation=new GridNavigation(20,20);
            for(int y=0;y<20;y++)navigation.SetBlocked(new Vector2(10,y),0,true);
            Vector2 start=new Vector2(3,5),end=new Vector2(16,14);
            bool Open(Vector2 p)=>true;
            Assert.IsEmpty(navigation.FindPath(start,end,.4f,Open));
            var previous=navigation.CreateFilteredConnectivity(.4f,Open);
            Assert.IsFalse(previous.MayConnect(start,end),"The original whole wall really separates both banks");
            var restored=navigation.CaptureState();int savedVersion=restored.version;
            Array.Clear(restored.blocked,0,restored.blocked.Length);
            restored.blocked[10+10*restored.width]=true;
            navigation.RestoreState(restored);
            Assert.AreEqual(savedVersion,navigation.Version,"Snapshot restoration preserves the saved public Version");
            Assert.IsFalse(navigation.ClearLine(start,end,.4f),"The remaining obstacle still prevents a trivial straight-line acceptance");
            var original=navigation.FindPath(start,end,.4f,Open);
            Assert.GreaterOrEqual(original.Count,3,"Removing the rest of the wall opens an actual body-clear detour");
            AssertActualRoute(navigation,original,start,end,.4f,Open,null);
            Assert.IsTrue(previous.MayConnect(start,end),"An old negative proof cannot survive a different restored map with the same public Version");
            Assert.IsTrue(navigation.CreateFilteredConnectivity(.4f,Open).MayConnect(start,end));
        }

        [Test] public void PositiveConnectivityStillRequiresTheOriginalContinuousDiscAvoidanceBetweenCoarseSamples()
        {
            var navigation=new GridNavigation(32,28);
            Vector2 start=new Vector2(8,12),end=new Vector2(16.08f,12),disc=new Vector2(12.1f,12.3999f);
            bool PointFree(Vector2 p)=>(p-disc).sqrMagnitude>=.4f*.4f;
            bool SegmentFree(Vector2 a,Vector2 b)=>SegmentDistance(a,b,disc)>=.4f;
            int samples=Mathf.CeilToInt(Vector2.Distance(start,end)*3);
            for(int n=0;n<=samples;n++)Assert.IsTrue(PointFree(Vector2.Lerp(start,end,n/(float)samples)),
                "This declared grazing disc is intentionally missed by the old point sampling");
            Assert.IsFalse(SegmentFree(start,end));
            var context=navigation.CreateFilteredConnectivity(.4f,PointFree,SegmentFree);
            Assert.IsTrue(context.MayConnect(start,end),"There is a valid detour; a positive proof cannot authorize the grazing shortcut");
            var original=navigation.FindPath(start,end,.4f,PointFree,SegmentFree);
            Assert.GreaterOrEqual(original.Count,3);AssertActualRoute(navigation,original,start,end,.4f,PointFree,SegmentFree);
        }

        [Test] public void PositiveConnectivityCannotReplaceTheActualBankCornerSweepWithAnUnsafeSampledLine()
        {
            var navigation=new GridNavigation(48,48);navigation.SetTerrainBlocked(new Vector2(24,24),0,true);
            Vector2 direction=new Vector2(1,-1).normalized;
            Vector2 closest=new Vector2(23.5f,23.5f)-new Vector2(1,1).normalized*.3999f;
            Vector2 start=closest-direction*4,end=closest+direction*4.08f;
            bool Open(Vector2 p)=>true;
            int samples=Mathf.CeilToInt(Vector2.Distance(start,end)*3);
            for(int n=0;n<=samples;n++)Assert.IsTrue(navigation.IsWalkable(Vector2.Lerp(start,end,n/(float)samples),.4f));
            Assert.IsFalse(navigation.IsWalkable(closest,.4f));Assert.IsFalse(navigation.ClearLine(start,end,.4f));
            var context=navigation.CreateFilteredConnectivity(.4f,Open);
            Assert.IsTrue(context.MayConnect(start,end),"Connectivity exists around the real bank cell, not through its rounded corner");
            var original=navigation.FindPath(start,end,.4f,Open);
            Assert.GreaterOrEqual(original.Count,3);AssertActualRoute(navigation,original,start,end,.4f,Open,null);
        }

        [Test] public void DeterministicSmallGridNegativeAnswersAlwaysAgreeWithOriginalFilteredRoutes()
        {
            var random=new System.Random(720631);int queries=0,rejections=0,realRoutes=0;
            foreach(int map in new[]{0,1,2})
            {
                var occupied=new bool[24*24];
                for(int y=2;y<22;y++)for(int x=2;x<22;x++)
                    if(!(x<=9&&y<=4))occupied[x+y*24]=random.NextDouble()<.12;
                var navigation=new GridNavigation(24,24);
                navigation.ConfigureTerrain(p=>occupied[(int)p.x+(int)p.y*24]||
                    map==0&&p.x==12&&(p.y<10||p.y>14)||map==1&&p.x==12&&p.y!=12);
                Vector2[] centers=map==0?new[]{new Vector2(12,12)}:map==1?new[]{new Vector2(12,8)}:
                    new[]{new Vector2(8.5f,10.5f),new Vector2(17.2f,15.4f)};
                float threatRadius=map==0?4:map==1?1.2f:1.15f;
                bool PointFree(Vector2 p)=>OutsideDiscs(p,centers,threatRadius);
                bool SegmentFree(Vector2 a,Vector2 b)=>OutsideDiscs(a,b,centers,threatRadius);
                string before=JsonUtility.ToJson(navigation.CaptureState());
                foreach(float radius in new[]{.25f,.4f,.65f})
                {
                    var context=navigation.CreateFilteredConnectivity(radius,PointFree,SegmentFree);
                    for(int q=0;q<4;q++)
                    {
                        Vector2 start=q==0?new Vector2(3,12):q==1?new Vector2(3,3):
                            new Vector2(2.1f+(float)random.NextDouble()*18,2.1f+(float)random.NextDouble()*18);
                        Vector2 end=q==0?new Vector2(20,12):q==1?new Vector2(7,3):
                            new Vector2(2.1f+(float)random.NextDouble()*18,2.1f+(float)random.NextDouble()*18);
                        bool mayConnect=context.MayConnect(start,end);
                        var original=navigation.FindPath(start,end,radius,PointFree,SegmentFree);queries++;
                        string description="seed720631 map"+map+" radius"+radius+" query"+q+" "+start+"→"+end;
                        if(!mayConnect){rejections++;Assert.IsEmpty(original,"Negative-only preflight produced a false rejection: "+description);}
                        if(original.Count>0)
                        {
                            realRoutes++;Assert.IsTrue(mayConnect,"A genuine original filtered route cannot be preflight-rejected: "+description);
                            AssertActualRoute(navigation,original,start,end,radius,PointFree,SegmentFree);
                        }
                    }
                }
                Assert.AreEqual(before,JsonUtility.ToJson(navigation.CaptureState()));
            }
            Assert.AreEqual(36,queries);Assert.Greater(rejections,0,"The fixed set must exercise actual negative proofs");
            Assert.Greater(realRoutes,0,"The fixed set must retain positive original paths as well");
        }
    }
}
