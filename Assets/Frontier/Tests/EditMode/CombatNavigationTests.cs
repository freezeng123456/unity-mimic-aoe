using NUnit.Framework;
using UnityEngine;
using Frontier;

public class CombatNavigationTests
{
    [Test] public void BlockRelease(){var n=new GridNavigation(16,16);n.SetBlocked(new Vector2(8,8),2,true);Assert.IsFalse(n.IsWalkable(new Vector2(8,8)));n.SetBlocked(new Vector2(8,8),2,false);Assert.IsTrue(n.IsWalkable(new Vector2(8,8)));}
    [Test] public void FullWallIsUnreachable(){var n=new GridNavigation(16,16);n.SetBlocked(new Vector2(8,8),.5f,true);for(int y=0;y<16;y++)n.SetBlocked(new Vector2(8,y),.5f,true);Assert.IsEmpty(n.FindPath(new Vector2(2,8),new Vector2(14,8),.4f));}
    [Test] public void NarrowPassageSupportsActors(){var n=new GridNavigation(20,12);for(int y=0;y<12;y++)if(y<5||y>6)n.SetBlocked(new Vector2(10,y),.4f,true);Assert.IsNotEmpty(n.FindPath(new Vector2(3,5),new Vector2(16,5),.4f));}
    [Test] public void SlotsRespectSpacing(){var n=new GridNavigation();var s=n.FormationSlots(new Vector2(64,64),50,1.2f);Assert.AreEqual(50,s.Count);for(int i=0;i<s.Count;i++)for(int j=i+1;j<s.Count;j++)Assert.GreaterOrEqual(Vector2.Distance(s[i],s[j]),1.19f);}
    [Test] public void BoundariesAreBlocked(){var n=new GridNavigation();Assert.IsFalse(n.IsWalkable(new Vector2(-1,1)));Assert.IsFalse(n.IsWalkable(new Vector2(128,1)));}
    [Test] public void DetourPreservesFractionalEndpointsAndClearSegments()
    {
        var n=new GridNavigation(32,32);for(int y=0;y<32;y++)if(y<14||y>18)n.SetBlocked(new Vector2(16,y),.4f,true);
        var start=new Vector2(15.05f,10.15f);var end=new Vector2(22.35f,25.45f);var path=n.FindPath(start,end,.4f);
        Assert.Greater(path.Count,2);Assert.AreEqual(start,path[0]);Assert.AreEqual(end,path[path.Count-1]);
        for(int i=1;i<path.Count;i++)for(int k=0;k<=100;k++)Assert.IsTrue(n.IsWalkable(Vector2.Lerp(path[i-1],path[i],k/100f),.4f),"Every interpolated path segment must clear the wall");
    }
}
