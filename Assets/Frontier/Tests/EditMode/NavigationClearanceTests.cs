using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class NavigationClearanceTests
    {
        static GridNavigation Wall()
        {
            var grid=new GridNavigation(20,20);
            for(int y=0;y<20;y++)grid.SetBlocked(new Vector2(10,y),.45f,true);
            return grid;
        }
        static readonly Vector2 Start=new Vector2(3,5),End=new Vector2(16,14);
        [Test]public void OpeningAndClosingBarrierChangesReachabilityImmediately()
        {
            var grid=Wall();Assert.IsEmpty(grid.FindPath(Start,End,.4f));
            for(int y=8;y<=11;y++)grid.SetBlocked(new Vector2(10,y),.45f,false);
            Assert.IsNotEmpty(grid.FindPath(Start,End,.4f));
            for(int y=8;y<=11;y++)grid.SetBlocked(new Vector2(10,y),.45f,true);
            Assert.IsEmpty(grid.FindPath(Start,End,.4f));
        }
        [Test]public void InfantryClearanceCannotAuthorizeCannonThroughOneCellGap()
        {
            var grid=Wall();grid.SetBlocked(new Vector2(10,10),.45f,false);
            Assert.IsNotEmpty(grid.FindPath(Start,End,.4f));
            Assert.IsEmpty(grid.FindPath(Start,End,.75f));
            Assert.IsNotEmpty(grid.FindPath(Start,End,.4f));
            grid.SetBlocked(new Vector2(10,9),.45f,false);grid.SetBlocked(new Vector2(10,11),.45f,false);
            Assert.IsNotEmpty(grid.FindPath(Start,End,.75f));
        }
        [Test]public void MovingAllyFilterIsReevaluatedWithoutStaticObstacleChanges()
        {
            var grid=Wall();for(int y=8;y<=11;y++)grid.SetBlocked(new Vector2(10,y),.45f,false);
            bool occupied=true;
            System.Func<Vector2,bool> filter=p=>!occupied||p.x<9||p.x>11;
            Assert.IsEmpty(grid.FindPath(Start,End,.4f,filter));
            occupied=false;var route=grid.FindPath(Start,End,.4f,filter);Assert.IsNotEmpty(route);
            occupied=true;Assert.IsEmpty(grid.FindPath(Start,End,.4f,filter));
            Assert.IsNotEmpty(grid.FindPath(Start,End,.4f),"An ally filter must never alter the shared static graph");
        }
    }
}
