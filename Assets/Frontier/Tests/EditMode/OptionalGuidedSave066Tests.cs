using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class OptionalGuidedSave066Tests
    {
        const string Version="optional-guided-fixture";
        string directory,path;
        [SetUp] public void SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"Frontier-optional-guided-"+Guid.NewGuid().ToString("N"));
            Assert.IsFalse(Directory.Exists(directory));Directory.CreateDirectory(directory);path=MatchSaveStore.SlotPath(directory,1);
        }
        [TearDown] public void TearDown(){if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);}
        static SavedMatch Plain()
        {
            var world=new World(Catalog.Load(),314159,"coast","river",3);var simulation=new MatchSimulation(world,1);
            for(int i=0;i<3;i++)simulation.Step();world.Paused=true;
            return new SavedMatch{matchId="plain-optional-fixture",appVersion=Version,world=world.CaptureState(),simulation=simulation.CaptureState(),
                cameraPosition=new Vector3(24,50,-14),cameraFocus=new Vector2(24,28),cameraSize=26,selection=Array.Empty<int>(),groups=Array.Empty<ControlGroupSnapshot>()};
        }
        static SavedMatch Guided(GuidedPlaySession session)=>new SavedMatch
        {
            matchId=session.AttemptId,appVersion=Version,world=session.World.CaptureState(),simulation=session.Simulation.CaptureState(),guided=session.CaptureState(),
            cameraPosition=new Vector3(24,50,-14),cameraFocus=new Vector2(24,28),cameraSize=26,selection=session.SuggestedActors,groups=Array.Empty<ControlGroupSnapshot>()
        };
        static object Codec(string name,params object[] arguments)
        {
            try{return typeof(MatchSaveStore).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,arguments);}
            catch(TargetInvocationException error){ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        }
        string Payload()=> (string)Codec("DecodePayload",JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path)),MatchSaveStore.MaxPayloadBytes);
        void AuthenticatedPayload(string payload,bool legacy=false)
        {
            // Recompute a genuine checksum/compressed envelope. The failure
            // must come from optional-state consistency, not a stale digest.
            var envelope=legacy?new SaveEnvelope{schema=1,payload=payload,sha256=SnapshotCodec.Hash(payload)}:
                (SaveEnvelope)Codec("EncodePayload",payload,MatchSaveStore.MaxPayloadBytes);
            Assert.AreEqual(SnapshotCodec.Hash(payload),envelope.sha256);
            Assert.AreEqual(payload,(string)Codec("DecodePayload",envelope,MatchSaveStore.MaxPayloadBytes));
            File.WriteAllText(path,JsonUtility.ToJson(envelope),new UTF8Encoding(false,true));
        }
        [Test] public void OrdinaryMatchReadsAsNullGuidedAndResavesByteIdentically()
        {
            var save=Plain();save.hasGuided=true; // Write derives presence from the actual optional session.
            MatchSaveStore.Write(path,save);Assert.IsFalse(save.hasGuided);byte[] first=File.ReadAllBytes(path);
            var loaded=MatchSaveStore.Read(path,Version);Assert.IsFalse(loaded.hasGuided);Assert.IsNull(loaded.guided);
            Assert.AreEqual(JsonUtility.ToJson(save.world),JsonUtility.ToJson(loaded.world));
            Assert.AreEqual(JsonUtility.ToJson(save.simulation),JsonUtility.ToJson(loaded.simulation));
            MatchSaveStore.Write(path,loaded);CollectionAssert.AreEqual(first,File.ReadAllBytes(path));
            Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp-*"));
        }
        [Test] public void ActualLessonStageAndAttemptSurviveTheOptionalFieldAndColdRestore()
        {
            using(var original=GuidedPlaySession.Start(Catalog.Load(),"lesson_selection"))
            {
                original.ObserveSelection(original.SuggestedActors);Assert.AreEqual(GuidedPlayGoal.Move,original.Stage.Goal);
                original.World.Paused=true;var save=Guided(original);Assert.IsFalse(save.hasGuided);
                MatchSaveStore.Write(path,save);Assert.IsTrue(save.hasGuided);byte[] first=File.ReadAllBytes(path);
                var loaded=MatchSaveStore.Read(path,Version);Assert.IsTrue(loaded.hasGuided);Assert.IsNotNull(loaded.guided);
                Assert.AreEqual(original.AttemptId,loaded.guided.attemptId);Assert.AreEqual(JsonUtility.ToJson(save.guided),JsonUtility.ToJson(loaded.guided));
                var world=World.Restore(original.World.Rules,loaded.world);var simulation=MatchSimulation.Restore(world,loaded.simulation);
                using(var restored=GuidedPlaySession.Restore(world,simulation,loaded.guided))
                {
                    Assert.AreEqual(original.AttemptId,restored.AttemptId);Assert.AreEqual(GuidedPlayGoal.Move,restored.Stage.Goal);
                    Assert.IsTrue(restored.Checklist[0].Complete);Assert.AreEqual(GuidedPlayStatus.Running,restored.Status);
                    Assert.AreEqual(JsonUtility.ToJson(original.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
                }
                MatchSaveStore.Write(path,loaded);CollectionAssert.AreEqual(first,File.ReadAllBytes(path));
            }
        }
        [Test] public void ValidChecksumCannotHideARealLessonBehindFalsePresence()
        {
            using(var session=GuidedPlaySession.Start(Catalog.Load(),"lesson_selection"))
            {
                MatchSaveStore.Write(path,Guided(session));string payload=Payload();
                StringAssert.Contains("\"hasGuided\":true",payload);
                payload=payload.Replace("\"hasGuided\":true","\"hasGuided\":false");AuthenticatedPayload(payload);
                byte[] before=File.ReadAllBytes(path);var error=Assert.Throws<IOException>(()=>MatchSaveStore.Read(path,Version));
                StringAssert.Contains("inconsistent challenge marker",error.Message);CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
            }
        }
        [Test] public void ValidChecksumCannotClaimAChallengeWhenItsActualStateIsEmpty()
        {
            MatchSaveStore.Write(path,Plain());string payload=Payload();StringAssert.Contains("\"hasGuided\":false",payload);
            payload=payload.Replace("\"hasGuided\":false","\"hasGuided\":true");AuthenticatedPayload(payload);
            byte[] before=File.ReadAllBytes(path);var error=Assert.Throws<IOException>(()=>MatchSaveStore.Read(path,Version));
            StringAssert.Contains("no challenge state",error.Message);CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
        }
        [Test] public void LegacyOrdinaryPayloadWithoutPresenceMarkerStillReadsAsNullWithoutRewritingIt()
        {
            var save=Plain();MatchSaveStore.Write(path,save);string payload=Payload();
            StringAssert.Contains("\"hasGuided\":false,",payload);payload=payload.Replace("\"hasGuided\":false,","");
            StringAssert.DoesNotContain("\"hasGuided\"",payload);AuthenticatedPayload(payload,true);byte[] before=File.ReadAllBytes(path);
            var loaded=MatchSaveStore.Read(path,Version);Assert.IsFalse(loaded.hasGuided);Assert.IsNull(loaded.guided);
            Assert.AreEqual(save.matchId,loaded.matchId);Assert.AreEqual(JsonUtility.ToJson(save.world),JsonUtility.ToJson(loaded.world));
            Assert.AreEqual(JsonUtility.ToJson(save.simulation),JsonUtility.ToJson(loaded.simulation));CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
        }
    }
}
