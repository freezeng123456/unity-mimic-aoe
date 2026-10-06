using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ReplayCatalogQuality071Tests
    {
        const BindingFlags PrivateStatic=BindingFlags.Static|BindingFlags.NonPublic;
        string directory,path,segment;RuleCatalog rules;ReplayIdentity identity;
        [SetUp] public void SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"frontier-replay-catalog071-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            segment=Guid.NewGuid().ToString("N");path=ReplayStore.PathFor(directory,segment);rules=Catalog.Load();
            identity=ReplayIdentity.Create(rules,"quality071","catalog-quality-build","catalog-quality-unity");ClearCache();
        }
        [TearDown] public void TearDown(){ClearCache();if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static FieldInfo Field(string name)=>typeof(ReplayStore).GetField(name,PrivateStatic);
        static long ValidationPasses=>(long)Field("catalogValidationPasses").GetValue(null);
        static void ClearCache()
        {
            lock(Field("CatalogCacheLock").GetValue(null))
            {
                ((Dictionary<string,ReplayCatalogEntry>)Field("ValidatedCatalogMetadata").GetValue(null)).Clear();
                ((Queue<string>)Field("CatalogCacheOrder").GetValue(null)).Clear();
            }
        }
        ReplayDocument Fixture(bool incomplete=false)
        {
            var document=new ScenarioDocument{id="catalog-quality071",name="Authored catalog fixture",enableElimination=false,rulesHash=SnapshotCodec.RulesHash(rules),
                starts=new[]{new ScenarioStart{player=0,team=0,civilization="coast",town=new Vector2(24,28),explorer=new Vector2(31,32),workers=new[]{new Vector2(29,25)}},
                    new ScenarioStart{player=1,team=1,civilization="france",town=new Vector2(104,100),explorer=new Vector2(97,96),workers=new[]{new Vector2(99,103)}}}};
            var world=new World(rules,document,new[]{"coast","france"},new[]{0,1},5);var simulation=new MatchSimulation(world,1);
            using(var recorder=new ReplayRecorder(simulation,identity,segmentId:segment,anchorInterval:3,matchId:"quality-match"))
            {
                for(int i=0;i<12;i++)Assert.IsTrue(simulation.Step());
                if(incomplete)recorder.Abort("Controlled quality interruption");return recorder.Finish();
            }
        }
        ReplayCatalogEntry Entry()=>ReplayStore.Catalog(directory,rules,identity).Single();
        void Plain(ReplayDocument document)
        {
            string payload=JsonUtility.ToJson(document);File.WriteAllText(path,JsonUtility.ToJson(new SaveEnvelope{schema=1,payload=payload,sha256=SnapshotCodec.Hash(payload)}),new UTF8Encoding(false,true));
        }
        [Test] public void NewlyWrittenAuthoredReplayListsWithoutRevalidationAndReturnsOwnedMetadata()
        {
            var document=Fixture();ReplayStore.Write(path,document,rules,identity);byte[] before=File.ReadAllBytes(path);long passes=ValidationPasses;
            document.matchId="mutated caller document";document.anchors[0].world.mapId="mutated caller map";
            var first=Entry();var second=Entry();Assert.AreEqual(passes,ValidationPasses);Assert.AreNotSame(first,second);
            Assert.AreEqual("quality-match",first.MatchId);Assert.AreEqual(MapTopology.Scenario,first.MapId);Assert.IsTrue(first.CanOpen);Assert.IsFalse(first.MatchEnded);
            typeof(ReplayCatalogEntry).GetProperty("SegmentId").GetSetMethod(true).Invoke(first,new object[]{"mutated caller entry"});
            Assert.AreEqual(segment,Entry().SegmentId);CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
            Assert.AreEqual(1,Directory.GetFiles(directory).Length,"Metadata cache must not create a sidecar or touch account/save data");
        }
        [Test] public void ColdAuthoredCatalogFullyValidatesOnceAndWarmRefreshAvoidsAllAnchorValidation()
        {
            ReplayStore.Write(path,Fixture(),rules,identity);ClearCache();long passes=ValidationPasses;var cold=Stopwatch.StartNew();var first=Entry();cold.Stop();
            Assert.AreEqual(passes+1,ValidationPasses);Assert.IsTrue(first.CanOpen);Assert.AreEqual(12,first.EndStep);passes=ValidationPasses;
            var warm=Stopwatch.StartNew();for(int i=0;i<5;i++)Assert.AreEqual(first.MatchId,Entry().MatchId);warm.Stop();
            Assert.AreEqual(passes,ValidationPasses,"Warm refresh must not repeat expensive anchor/terrain validation");
            TestContext.WriteLine("071 authored catalog: cold full validation "+cold.Elapsed.TotalMilliseconds.ToString("0.###")+"ms; 5 warm byte-hashed refreshes "+warm.Elapsed.TotalMilliseconds.ToString("0.###")+"ms");
        }
        [Test] public void SameLengthSameTimestampCorruptionCannotReuseValidatedMetadataAndIsPreserved()
        {
            ReplayStore.Write(path,Fixture(),rules,identity);
            // Establish the unchanged file identity before the baseline catalog
            // read. Whole seconds round-trip on the supported filesystems without
            // losing the sub-millisecond precision of an automatic write timestamp.
            DateTime stamp=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);File.SetLastWriteTimeUtc(path,stamp);
            Assert.AreEqual(stamp,File.GetLastWriteTimeUtc(path));var good=Entry();byte[] before=File.ReadAllBytes(path);
            var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path));envelope.sha256=new string('0',64);
            File.WriteAllText(path,JsonUtility.ToJson(envelope),new UTF8Encoding(false,true));File.SetLastWriteTimeUtc(path,stamp);byte[] corrupt=File.ReadAllBytes(path);
            Assert.AreEqual(before.Length,corrupt.Length);Assert.AreEqual(stamp,File.GetLastWriteTimeUtc(path));long passes=ValidationPasses;
            var bad=Entry();Assert.AreEqual(ReplayFileStatus.Unreadable,bad.Status);Assert.IsFalse(bad.CanOpen);Assert.IsNotEmpty(bad.Error);Assert.AreEqual(corrupt.Length,bad.StoredBytes);
            Assert.AreEqual(passes+1,ValidationPasses);Assert.Throws<IOException>(()=>ReplayStore.Read(path,rules,identity));CollectionAssert.AreEqual(corrupt,File.ReadAllBytes(path));
            File.WriteAllBytes(path,before);passes=ValidationPasses;Assert.AreEqual(good.SegmentId,Entry().SegmentId);Assert.AreEqual(passes,ValidationPasses);
        }
        [Test] public void ChangedBuildIdentityOrActualRulesCannotReuseAValidatedCatalogEntry()
        {
            ReplayStore.Write(path,Fixture(),rules,identity);byte[] before=File.ReadAllBytes(path);var changed=SnapshotCodec.Clone(identity);changed.buildIdentity="other-build";
            var incompatible=ReplayStore.Catalog(directory,rules,changed).Single();Assert.AreEqual(ReplayFileStatus.Incompatible,incompatible.Status);Assert.IsFalse(incompatible.CanOpen);
            float xp=rules.initialXp;rules.initialXp=xp+1;
            try{var changedRules=Entry();Assert.IsFalse(changedRules.CanOpen);Assert.IsNotEmpty(changedRules.Error);}
            finally{rules.initialXp=xp;}
            Assert.IsTrue(Entry().CanOpen);CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
        }
        [Test] public void DeepAnchorCorruptionStillFailsFullOpenAndCatalogWithoutDeletingTheFile()
        {
            var document=Fixture();ReplayStore.Write(path,document,rules,identity);Assert.IsTrue(Entry().CanOpen);
            document.anchors.Last().world.landNavigation.terrain[0]=!document.anchors.Last().world.landNavigation.terrain[0];Plain(document);byte[] corrupt=File.ReadAllBytes(path);
            Assert.Throws<IOException>(()=>ReplayStore.Read(path,rules,identity));var bad=Entry();Assert.IsFalse(bad.CanOpen);Assert.IsNotEmpty(bad.Error);
            CollectionAssert.AreEqual(corrupt,File.ReadAllBytes(path));Assert.AreEqual(1,Directory.GetFiles(directory).Length);
        }
        [Test] public void IncompleteCopiedRecordingsKeepPrefixStatusPerFileErrorsAndIndependentPaths()
        {
            ReplayStore.Write(path,Fixture(true),rules,identity);string copy=Path.Combine(directory,"copy.replay.json");File.Copy(path,copy);
            string broken=Path.Combine(directory,"broken.replay.json");File.WriteAllText(broken,"not JSON");long passes=ValidationPasses;
            var entries=ReplayStore.Catalog(directory,rules,identity);Assert.AreEqual(3,entries.Count);
            foreach(var entry in entries.Where(e=>e.CanOpen))
            {Assert.AreEqual(ReplayFileStatus.IncompleteRecording,entry.Status);Assert.IsTrue(entry.Incomplete);Assert.IsFalse(entry.MatchEnded);Assert.IsNotEmpty(entry.IncompleteReason);}
            CollectionAssert.AreEquivalent(new[]{path,copy},entries.Where(e=>e.CanOpen).Select(e=>e.FilePath));Assert.AreEqual(passes+1,ValidationPasses,"Only the corrupt file is new content; the copied recording shares exact validated bytes");
            var error=entries.Single(e=>!e.CanOpen);Assert.AreEqual(broken,error.FilePath);Assert.IsNotEmpty(error.Error);Assert.AreEqual("not JSON",File.ReadAllText(broken));
        }
        [Test] public void ConcurrentWarmCatalogReadersKeepOwnedEntriesAndUnchangedFileBytes()
        {
            ReplayStore.Write(path,Fixture(),rules,identity);byte[] before=File.ReadAllBytes(path);long passes=ValidationPasses;
            var readers=Enumerable.Range(0,4).Select(_=>Task.Run(()=>ReplayStore.Catalog(directory,rules,identity).Single())).ToArray();Task.WaitAll(readers);
            Assert.AreEqual(passes,ValidationPasses);foreach(var reader in readers){Assert.IsTrue(reader.Result.CanOpen);Assert.AreEqual(segment,reader.Result.SegmentId);}
            Assert.AreNotSame(readers[0].Result,readers[1].Result);CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
        }
    }
}
