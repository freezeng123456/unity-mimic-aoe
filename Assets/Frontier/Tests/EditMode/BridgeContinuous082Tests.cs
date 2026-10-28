using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class BridgeContinuous082Tests
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        sealed class Frame
        {
            public World world;public BridgeSpan span;public EntityState actor;public int direction;
            public Vector2 At(float across,float side)=>span.Point(across*direction,side*direction);
            public bool Point(Vector2 p)=>(bool)typeof(World).GetMethod("BridgeTrafficPointFree",Flags).Invoke(world,new object[]{p});
            public bool Segment(Vector2 a,Vector2 b)=>(bool)typeof(World).GetMethod("BridgeTrafficSegmentFree",Flags).Invoke(world,new object[]{a,b});
        }
        static Frame Geometry(int direction,float originAcross=-25,float originSide=-1)
        {
            var f=new Frame{world=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.River),direction=direction};
            foreach(var e in f.world.Entities.Where(e=>e.def.id!="town").ToArray())f.world.Remove(e);
            f.span=f.world.Layout.Bridges[0];f.actor=f.world.Spawn("pike",0,f.At(originAcross,originSide));Vector2 goal=f.At(25,-1);
            Assert.IsTrue(f.world.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{f.actor.id},point=goal}).accepted);
            typeof(World).GetMethod("PrepareBridgeTraffic",Flags).Invoke(f.world,new object[]{f.actor,goal,new List<Vector2>{f.actor.position,goal}});
            Assert.IsEmpty(f.actor.path);return f;
        }
        static void Pure(Frame f,Action checks)
        {
            string before=JsonUtility.ToJson(f.world.CaptureState());checks();
            Assert.AreEqual(before,JsonUtility.ToJson(f.world.CaptureState()),"Continuous geometry queries cannot change serialized World");Assert.IsEmpty(f.actor.path);
        }
        [TestCase(1)] [TestCase(-1)] public void WiderEntryUpperEdgeAndItsOpenCornerStayOutsideTheLaneDomain(int direction)
        {
            var f=Geometry(direction);float edge=-f.span.HalfLength-f.span.RampLength,width=f.span.HalfWidth+6;
            Pure(f,()=>
            {
                var a=f.At(edge,7);var b=f.At(edge,9);Assert.IsTrue(f.Point(a)&&f.Point(b));Assert.IsTrue(f.Segment(a,b));
                a=f.At(edge-.5f,width+.5f);b=f.At(edge+.5f,width-.5f);Assert.IsTrue(f.Point(a)&&f.Point(b));Assert.IsTrue(f.Segment(a,b),"A single touch of the excluded entry upper corner is empty");
            });
        }
        [TestCase(1)] [TestCase(-1)] public void AClosedFarEntryCornerTouchRemainsARealConstrainedPoint(int direction)
        {
            var f=Geometry(direction);float edge=-f.span.HalfLength-f.span.RampLength-6,width=f.span.HalfWidth+6;
            Pure(f,()=>
            {
                var a=f.At(edge-.5f,width-.5f);var b=f.At(edge+.5f,width+.5f);Assert.IsTrue(f.Point(a)&&f.Point(b));
                Assert.IsFalse(f.Segment(a,b),"The isolated closed corner is in the opposing half even though both endpoints are outside");
            });
        }
        [TestCase(1)] [TestCase(-1)] public void ThinEntryIntersectionsCannotBeSkippedBetweenQuarterMeterSamples(int direction)
        {
            var f=Geometry(direction);float edge=-f.span.HalfLength-f.span.RampLength-6,width=f.span.HalfWidth+6;
            Pure(f,()=>
            {
                var a=f.At(edge-.7f,width-.5f);var b=f.At(edge+.3f,width+.21425f);Assert.IsTrue(f.Point(a)&&f.Point(b));
                Assert.IsFalse(f.Segment(a,b),"A roughly1e-5 parameter interval still contains actual wrong-half points");
            });
        }
        [TestCase(1)] [TestCase(-1)] public void CorrectionConeRetainsTheRealOriginAndAllowsLateralRepair(int direction)
        {
            var f=Geometry(direction,-17,1);
            Pure(f,()=>
            {
                Assert.IsTrue(f.Point(f.actor.position));Assert.IsTrue(f.Segment(f.actor.position,f.actor.position),"Same canonical origin must give cone excess0");
                Assert.IsTrue(f.Segment(f.actor.position,f.At(-17,.5f)));Assert.IsFalse(f.Segment(f.actor.position,f.At(-16.5f,1)),"Forward wrong-half travel does not satisfy lateral correction");
            });
        }
        [TestCase(1)] [TestCase(-1)] public void TheOwnHalfPlaneRemainsValidOutsideTheCorrectionCone(int direction)
        {
            var f=Geometry(direction,-17,1);
            Pure(f,()=>Assert.IsTrue(f.Segment(f.At(-17,-1),f.At(5,-1)),"The constant own-half allowance must be clipped before testing cone excess"));
        }
        [Serializable] sealed class RecordedReceipt
        {
            public string sourceFingerprint;public int actualSourceActor=369;public Vector2 origin,rawEnd,subdividedEnd;
            public bool navigationClear,rawSegmentFree,subdividedSegmentFree,worldUnchanged;
            public string scope="Pure query over actual52 e369 geometry constants; no path injection,World.Tick orfine-producer attribution";
        }
        [Test] public void Actual52LongSegmentIsRejectedBeforeAnEightMeterSubdivisionChangesSamplingPhase()
        {
            var w=new World(Catalog.Load(),314159,"coast","coast",5,MapTopology.Tributaries);
            foreach(var e in w.Entities.Where(e=>e.def.id!="town").ToArray())w.Remove(e);
            var route=new List<Vector2>{new Vector2(79.15125274658203f,19.172439575195312f),new Vector2(73.76750183105469f,22.114959716796875f),new Vector2(68.38375091552734f,25.057479858398438f),new Vector2(63,28),new Vector2(57.5f,30.75f),new Vector2(52,33.5f),new Vector2(46.5f,36.25f),new Vector2(41,39),new Vector2(35,40),new Vector2(34,46),new Vector2(29.774999618530273f,51.32500076293945f),new Vector2(25.549999237060547f,56.650001525878906f),new Vector2(21.325000762939453f,61.97500228881836f),new Vector2(17.100000381469727f,67.30000305175781f)};
            var actor=w.Spawn("dragoon",0,route[0]);Vector2 goal=route.Last();Assert.AreEqual(.65f,actor.def.radius);
            Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Move,actors=new[]{actor.id},point=goal}).accepted);Assert.IsEmpty(actor.path);
            string before=JsonUtility.ToJson(w.CaptureState());typeof(World).GetMethod("PrepareBridgeTraffic",Flags).Invoke(w,new object[]{actor,goal,route});
            var segment=typeof(World).GetMethod("BridgeTrafficSegmentFree",Flags);
            var receipt=new RecordedReceipt{sourceFingerprint=Environment.GetEnvironmentVariable("FRONTIER_SOURCE_FINGERPRINT"),origin=route[0],rawEnd=route[3],subdividedEnd=route[1],navigationClear=w.Navigation.ClearLine(route[0],route[3],actor.def.radius),rawSegmentFree=(bool)segment.Invoke(w,new object[]{route[0],route[3]}),subdividedSegmentFree=(bool)segment.Invoke(w,new object[]{route[0],route[1]})};
            receipt.worldUnchanged=before==JsonUtility.ToJson(w.CaptureState());
            string output=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");if(string.IsNullOrEmpty(output))output=Path.Combine(Directory.GetCurrentDirectory(),"Builds","BridgeTraffic082");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"bridge-continuous-actual52-e369.json"),JsonUtility.ToJson(receipt,true));
            Assert.IsTrue(receipt.navigationClear);Assert.IsFalse(receipt.rawSegmentFree,"Continuous detection must reject the original long line, which old quarter sampling missed");Assert.IsFalse(receipt.subdividedSegmentFree);Assert.IsTrue(receipt.worldUnchanged);Assert.IsEmpty(actor.path);
        }
    }
}
