using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class CameraPanInputTests
    {
        [Test] public void EdgeBandIsThreePercentOfShortSideClampedToSixteenSixtyFour()
        {
            Assert.AreEqual(new Vector2(-1,0),CameraPanInput.EdgeDirection(new Vector2(10,360),new Vector2(1280,720),true));
            Assert.AreEqual(Vector2.zero,CameraPanInput.EdgeDirection(new Vector2(22,360),new Vector2(1280,720),true));
            Assert.AreEqual(new Vector2(1,0),CameraPanInput.EdgeDirection(new Vector2(1270,360),new Vector2(1280,720),true));
            Assert.AreEqual(new Vector2(-1,0),CameraPanInput.EdgeDirection(new Vector2(10,360),new Vector2(4000,2000),true));
            Assert.AreEqual(Vector2.zero,CameraPanInput.EdgeDirection(new Vector2(70,1000),new Vector2(4000,2000),true));
        }

        [Test] public void FourEdgesAreSymmetricAndCornerDirectionHasUnitLength()
        {
            var viewport=new Vector2(1280,720);Assert.AreEqual(new Vector2(-1,0),CameraPanInput.EdgeDirection(new Vector2(1,360),viewport,true));Assert.AreEqual(new Vector2(1,0),CameraPanInput.EdgeDirection(new Vector2(1279,360),viewport,true));Assert.AreEqual(new Vector2(0,-1),CameraPanInput.EdgeDirection(new Vector2(640,1),viewport,true));Assert.AreEqual(new Vector2(0,1),CameraPanInput.EdgeDirection(new Vector2(640,719),viewport,true));
            var corner=CameraPanInput.EdgeDirection(new Vector2(1,1),viewport,true);Assert.AreEqual(new Vector2(-1,-1).normalized,corner);Assert.AreEqual(1f,corner.magnitude,1e-5f);
        }

        [Test] public void NormalizedEdgeLocationsMatchAcrossDesktopResolutions()
        {
            var a=new Vector2(1280*.01f,720*.5f);var b=new Vector2(2560*.01f,1440*.5f);var da=CameraPanInput.EdgeDirection(a,new Vector2(1280,720),true);var db=CameraPanInput.EdgeDirection(b,new Vector2(2560,1440),true);Assert.AreEqual(da,db);Assert.AreNotEqual(Vector2.zero,da);
            a=new Vector2(1280*.99f,720*.5f);b=new Vector2(2560*.99f,1440*.5f);da=CameraPanInput.EdgeDirection(a,new Vector2(1280,720),true);db=CameraPanInput.EdgeDirection(b,new Vector2(2560,1440),true);Assert.AreEqual(da,db);Assert.AreNotEqual(Vector2.zero,da);
        }

        [Test] public void TriggerBandHonorsMinimumAndMaximumPixels()
        {
            Assert.AreEqual(new Vector2(-1,0),CameraPanInput.EdgeDirection(new Vector2(15,200),new Vector2(600,400),true));
            Assert.AreEqual(Vector2.zero,CameraPanInput.EdgeDirection(new Vector2(17,200),new Vector2(600,400),true));
            Assert.AreEqual(new Vector2(-1,0),CameraPanInput.EdgeDirection(new Vector2(63,1500),new Vector2(5000,3000),true));
            Assert.AreEqual(Vector2.zero,CameraPanInput.EdgeDirection(new Vector2(65,1500),new Vector2(5000,3000),true));
        }

        [Test] public void TenLogicalDesktopPointsInsideOldEightPixelBandStillTrigger()
        {
            var view=new Vector2(1280,720);var points=new[]{new Vector2(20,360),new Vector2(20,100),new Vector2(20,620),new Vector2(1260,360),new Vector2(1260,100),new Vector2(1260,620),new Vector2(640,20),new Vector2(300,20),new Vector2(980,20),new Vector2(640,700)};var expected=new[]{new Vector2(-1,0),new Vector2(-1,0),new Vector2(-1,0),new Vector2(1,0),new Vector2(1,0),new Vector2(1,0),new Vector2(0,-1),new Vector2(0,-1),new Vector2(0,-1),new Vector2(0,1)};
            for(int i=0;i<points.Length;i++)Assert.AreEqual(expected[i],CameraPanInput.EdgeDirection(points[i],view,true),"point "+points[i]);
        }

        [Test] public void OutsideUnfocusedAndZeroViewportNeverPan()
        {
            Assert.AreEqual(Vector2.zero,CameraPanInput.EdgeDirection(new Vector2(-1,10),new Vector2(1280,720),true));Assert.AreEqual(Vector2.zero,CameraPanInput.EdgeDirection(new Vector2(1280,10),new Vector2(1280,720),true));Assert.AreEqual(Vector2.zero,CameraPanInput.EdgeDirection(new Vector2(1,1),new Vector2(1280,720),false));Assert.AreEqual(Vector2.zero,CameraPanInput.EdgeDirection(new Vector2(1,1),Vector2.zero,true));Assert.AreEqual(Vector2.zero,CameraPanInput.EdgeDirection(new Vector2(1,1),new Vector2(-1,720),true));
        }
    }
}
