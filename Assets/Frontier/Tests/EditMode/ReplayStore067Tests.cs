using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ReplayStore067Tests
    {
        string directory,path;RuleCatalog rules;ReplayIdentity identity;
        [SetUp] public void SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"Frontier-replay-store-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            path=ReplayStore.PathFor(directory,"codec-fixture");rules=Catalog.Load();identity=ReplayIdentity.Create(rules,"0.10.0","codec-test-build","codec-test-unity");
        }
        [TearDown] public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        ReplayDocument Fixture(bool abort=false,bool invalidCommand=false)
        {
            var world=new World(rules,271828,"coast","river",3,"river");var simulation=new MatchSimulation(world,1);
            using(var recorder=new ReplayRecorder(simulation,identity,segmentId:"codec-fixture",matchId:"codec-match"))
            {
                var town=world.Entities.First(e=>e.owner==0&&e.def.id=="town");
                Assert.IsTrue(recorder.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item=rules.Civ("coast").workerUnit}).accepted);
                if(invalidCommand)Assert.IsFalse(recorder.Issue(new GameCommand{player=0,kind=(CommandKind)999,actors=null,item=null,choice=null}).accepted);
                for(int i=0;i<40;i++)Assert.IsTrue(simulation.Step());
                if(abort)recorder.Abort("controlled codec interruption");return recorder.Finish();
            }
        }
        void Envelope(SaveEnvelope value)=>File.WriteAllText(path,JsonUtility.ToJson(value),new UTF8Encoding(false,true));
        void Plain(string payload)=>Envelope(new SaveEnvelope{schema=1,payload=payload,sha256=SnapshotCodec.Hash(payload)});
        ReplayDocument Read()=>ReplayStore.Read(path,rules,identity);
        static object LimitedWrite(params object[] args)
        {
            try{return typeof(ReplayStore).GetMethod("WriteWithLimits",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}
            catch(TargetInvocationException error){ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        }
        static byte[] Gzip(byte[] raw,System.IO.Compression.CompressionLevel level=System.IO.Compression.CompressionLevel.Optimal)
        {
            if(raw.Length==0)return new byte[]{0x1f,0x8b,8,0,0,0,0,0,0,3,3,0,0,0,0,0,0,0,0,0};
            using(var output=new MemoryStream()){using(var gzip=new GZipStream(output,level,true))gzip.Write(raw,0,raw.Length);return output.ToArray();}
        }
        static string Hash(byte[] bytes)
        {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}

        [Test] public void CompressedColdReadRetainsPaidCommandsAndCanSeekToTheExactFinalDigest()
        {
            var document=Fixture();ReplayStore.Write(path,document,rules,identity);
            var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path));Assert.AreEqual(2,envelope.schema);Assert.AreEqual("gzip-base64",envelope.encoding);
            var loaded=Read();Assert.AreEqual(JsonUtility.ToJson(document),JsonUtility.ToJson(loaded));
            Assert.IsTrue(loaded.records.Any(r=>r.kind==ReplayOperation.Command&&r.accepted&&r.command.kind==CommandKind.Train));
            using(var session=new ReplaySession(rules,loaded,identity))
            {
                Assert.IsTrue(session.Seek(session.EndStep),session.Error);Assert.IsTrue(session.Verified,session.Error);
                Assert.AreEqual(loaded.anchors.Last().stateDigest,ReplayDigest.Compute(session.World,session.Simulation,session.Guided));
                Assert.IsTrue(session.Seek(session.StartStep),session.Error);Assert.IsTrue(session.Seek(session.EndStep),session.Error);
            }
            var entry=ReplayStore.Catalog(directory,rules,identity).Single();Assert.AreEqual(ReplayFileStatus.RecordedSegment,entry.Status);
            Assert.AreEqual(40,entry.EndStep);Assert.AreEqual("codec-match",entry.MatchId);Assert.AreEqual("river",entry.MapId);Assert.AreEqual(2,entry.PlayerCount);
            Assert.IsFalse(entry.MatchEnded,"A closed segment from an active match is not a whole-match result");Assert.IsFalse(entry.Incomplete);Assert.Greater(entry.DurationSeconds,0);
        }
        [Test] public void RejectedUnknownCommandEnumsAndNullInputFlagsSurviveStrictStorage()
        {
            var document=Fixture(invalidCommand:true);ReplayStore.Write(path,document,rules,identity);var loaded=Read();
            var invalid=loaded.records.Single(r=>r.hasCommand&&(int)r.command.kind==999);
            Assert.IsFalse(invalid.accepted);Assert.IsTrue(invalid.commandActorsNull);Assert.IsTrue(invalid.commandItemNull);Assert.IsTrue(invalid.commandChoiceNull);
            using(var session=new ReplaySession(rules,loaded,identity))Assert.IsTrue(session.Seek(session.EndStep),session.Error);
        }
        [Test] public void ManualCaptureExtendsTheSameDurableSegmentAndOldCapturedObjectRemainsOwned()
        {
            var world=new World(rules,271828,"coast","river",3,"river");var simulation=new MatchSimulation(world,1);
            using(var recorder=new ReplayRecorder(simulation,identity,segmentId:"codec-fixture",matchId:"codec-match"))
            {
                for(int i=0;i<5;i++)simulation.Step();var first=recorder.Capture();string immutable=JsonUtility.ToJson(first);
                ReplayStore.Write(path,first,rules,identity);
                for(int i=0;i<15;i++)simulation.Step();var later=recorder.Capture();ReplayStore.Write(path,later,rules,identity);
                Assert.AreEqual(20,Read().anchors.Last().completedStep);Assert.AreEqual(immutable,JsonUtility.ToJson(first));
                Assert.AreEqual("manual_save",Read().finishReason);Assert.IsFalse(Read().incomplete);Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp-*"));
            }
        }
        [Test] public void PlainContainerIsCompatibleOnlyWithTheCurrentReplayDocumentAndNeverRewritten()
        {
            var document=Fixture();string payload=JsonUtility.ToJson(document);Plain(payload);byte[] before=File.ReadAllBytes(path);
            Assert.AreEqual(payload,JsonUtility.ToJson(Read()));CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
            var old=SnapshotCodec.Clone(document);old.schema=0;Plain(JsonUtility.ToJson(old));Assert.Catch<IOException>(()=>Read());
            Plain(JsonUtility.ToJson(new SavedMatch{matchId="old-match",appVersion="0.9.0"}));Assert.Throws<IOException>(()=>Read());
        }
        [Test] public void TruncatedGzipWrongCrcWrongSizeConcatenationAndTrailingBodyAreRejectedWithoutMutation()
        {
            ReplayStore.Write(path,Fixture(),rules,identity);string original=File.ReadAllText(path);var valid=JsonUtility.FromJson<SaveEnvelope>(original);
            byte[] bytes=Convert.FromBase64String(valid.payloadBase64),empty=Gzip(Array.Empty<byte>());
            var crc=(byte[])bytes.Clone();crc[crc.Length-8]^=0x80;var size=(byte[])bytes.Clone();size[size.Length-4]^=1;
            var garbage=bytes.Take(bytes.Length-8).Concat(new byte[]{0,1,2}).Concat(bytes.Skip(bytes.Length-8)).ToArray();
            var incomplete=bytes.Take(bytes.Length-9).Concat(bytes.Skip(bytes.Length-8)).ToArray();
            var cases=new[]{bytes.Skip(4).ToArray(),bytes.Take(bytes.Length-4).ToArray(),crc,size,bytes.Concat(new byte[]{0}).ToArray(),bytes.Concat(bytes).ToArray(),empty.Concat(bytes).ToArray(),bytes.Concat(empty).ToArray(),garbage,incomplete};
            for(int i=0;i<cases.Length;i++)
            {
                var envelope=JsonUtility.FromJson<SaveEnvelope>(original);envelope.payloadBase64=Convert.ToBase64String(cases[i]);Envelope(envelope);byte[] before=File.ReadAllBytes(path);
                Assert.Throws<IOException>(()=>Read(),"gzip corruption case "+i);CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
            }
            var wrongHash=JsonUtility.FromJson<SaveEnvelope>(original);wrongHash.sha256=new string('0',64);Envelope(wrongHash);Assert.Throws<IOException>(()=>Read());
            var ambiguous=JsonUtility.FromJson<SaveEnvelope>(original);ambiguous.payload=" ";Envelope(ambiguous);Assert.Throws<IOException>(()=>Read());
            var whitespace=JsonUtility.FromJson<SaveEnvelope>(original);whitespace.payloadBase64+="\n";Envelope(whitespace);Assert.Throws<IOException>(()=>Read());
        }
        [Test] public void NoncanonicalBase64PaddingBitsAreRejectedEvenWhenDecodedBytesMatch()
        {
            string raw=JsonUtility.ToJson(Fixture());byte[] compressed=null;
            // JSON trailing whitespace keeps the document valid while providing
            // a padded gzip length for the exact discarded-bit regression.
            for(int i=0;i<16;i++){compressed=Gzip(Encoding.UTF8.GetBytes(raw+new string(' ',i)),System.IO.Compression.CompressionLevel.NoCompression);if(compressed.Length%3!=0){raw+=new string(' ',i);break;}}
            string canonical=Convert.ToBase64String(compressed);Assert.IsTrue(canonical.EndsWith("="));
            Envelope(new SaveEnvelope{schema=2,encoding="gzip-base64",payloadBase64=canonical,sha256=SnapshotCodec.Hash(raw)});Assert.DoesNotThrow(()=>Read());
            const string alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";char[] changed=canonical.ToCharArray();int index=canonical.TrimEnd('=').Length-1;
            changed[index]=alphabet[alphabet.IndexOf(changed[index])+1];string invalid=new string(changed);
            CollectionAssert.AreEqual(Convert.FromBase64String(canonical),Convert.FromBase64String(invalid));
            Envelope(new SaveEnvelope{schema=2,encoding="gzip-base64",payloadBase64=invalid,sha256=SnapshotCodec.Hash(raw)});Assert.Throws<IOException>(()=>Read());
        }
        [Test] public void StrictJsonRejectsDuplicatesUnknownFieldsMissingFieldsWrongTypesAndNonfiniteFloats()
        {
            string raw=JsonUtility.ToJson(Fixture());
            var cases=new[]{
                "{\"schema\":1,"+raw.Substring(1),
                "{\"\\u0073chema\":1,"+raw.Substring(1),
                raw.Replace("\"schema\":1,\"segmentId\"","\"segmentId\""),
                "{\"unexpected\":0,"+raw.Substring(1),
                raw.Replace("\"incomplete\":false","\"incomplete\":\"false\""),
                raw.Replace("\"completedStep\":0","\"completedStep\":0.5"),
                raw.Replace("\"point\":{","\"point\":{\"x\":1e999,"),
                raw.Replace("\"segmentId\":\"codec-fixture\"","\"segmentId\":\"\\ud800\""),
                raw.Replace("\"schema\":1","\"schema\":1.0"),
                raw.Replace("\"rulesHash\":","\"unknownRulesHash\":"),
                raw+" {}","[]"};
            for(int i=0;i<cases.Length;i++)
            {Assert.AreNotEqual(raw,cases[i],"Fixture mutation must be effective: "+i);Plain(cases[i]);Assert.Throws<IOException>(()=>Read(),"JSON corruption case "+i);}
            ReplayStore.Write(Path.Combine(directory,"valid.replay.json"),Fixture(),rules,identity);
            string envelope=File.ReadAllText(Path.Combine(directory,"valid.replay.json"));File.WriteAllText(path,"{\"schema\":2,"+envelope.Substring(1));Assert.Throws<IOException>(()=>Read());
        }
        [Test] public void InvalidUtf8AndBrokenEscapedSurrogatesAreRejectedWhilePairedUnicodeRoundTrips()
        {
            byte[] invalid={0xc3,0x28};Envelope(new SaveEnvelope{schema=2,encoding="gzip-base64",payloadBase64=Convert.ToBase64String(Gzip(invalid)),sha256=Hash(invalid)});Assert.Throws<IOException>(()=>Read());
            File.WriteAllBytes(path,new byte[]{0x7b,0xff,0x7d});Assert.Throws<IOException>(()=>Read());
            var valid=Fixture();valid.finishReason="manual_save 汉字 😀";string raw=JsonUtility.ToJson(valid);Plain(raw.Replace("😀","\\ud83d\\ude00"));
            Assert.AreEqual(valid.finishReason,Read().finishReason);
        }
        [TestCase(0)][TestCase(3)] public void UnsupportedStorageSchemasAreRetainedAndCatalogedAsIncompatible(int schema)
        {
            string raw=JsonUtility.ToJson(Fixture());Envelope(new SaveEnvelope{schema=schema,payload=raw,sha256=SnapshotCodec.Hash(raw)});byte[] before=File.ReadAllBytes(path);
            Assert.Catch<IOException>(()=>Read());var entry=ReplayStore.Catalog(directory,rules,identity).Single();Assert.AreEqual(ReplayFileStatus.Incompatible,entry.Status);
            CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
        }
        [TestCase("application")][TestCase("build")][TestCase("unity")][TestCase("rules")][TestCase("map")][TestCase("guided")]
        public void EveryIdentityMismatchIsRejectedAndCatalogedWithoutDeletingTheFile(string field)
        {
            ReplayStore.Write(path,Fixture(),rules,identity);var changed=SnapshotCodec.Clone(identity);
            if(field=="application")changed.appVersion="0.9.0";else if(field=="build")changed.buildIdentity="other-build";else if(field=="unity")changed.unityVersion="other-unity";
            else if(field=="rules")changed.rulesHash=new string('0',64);else if(field=="map")changed.mapGenerationVersion++;else changed.guidedCatalogRevision++;
            byte[] before=File.ReadAllBytes(path);Assert.Catch<IOException>(()=>ReplayStore.Read(path,rules,changed));
            var entry=ReplayStore.Catalog(directory,rules,changed).Single();Assert.AreEqual(ReplayFileStatus.Incompatible,entry.Status);Assert.IsFalse(entry.CanOpen);Assert.IsNotEmpty(entry.Error);
            CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
        }
        [Test] public void MaliciousOrderingNullArraysAndTamperedDigestsAreRejected()
        {
            var source=Fixture();
            var badOrdinal=SnapshotCodec.Clone(source);badOrdinal.records[0].ordinal++;
            var badStep=SnapshotCodec.Clone(source);badStep.records[0].completedStep=badStep.anchors.Last().completedStep+1;
            var badDigest=SnapshotCodec.Clone(source);badDigest.anchors.Last().stateDigest=new string('0',64);
            foreach(var invalid in new[]{badOrdinal,badStep,badDigest}){Plain(JsonUtility.ToJson(invalid));Assert.Throws<IOException>(()=>Read());}
            Plain(Regex.Replace(JsonUtility.ToJson(source),"\"actors\":\\[[^\\]]*\\]","\"actors\":null"));Assert.Throws<IOException>(()=>Read());
        }
        [Test] public void FailedWritesAndUnreadableTargetsPreserveTheOldBytesAndLeaveNoTemporaryFiles()
        {
            var document=Fixture();ReplayStore.Write(path,document,rules,identity);byte[] before=File.ReadAllBytes(path);
            Assert.Throws<IOException>(()=>LimitedWrite(path,document,rules,identity,ReplayStore.MaxBytes,1L));
            Assert.Throws<IOException>(()=>LimitedWrite(path,document,rules,identity,1L,ReplayStore.MaxPayloadBytes));
            CollectionAssert.AreEqual(before,File.ReadAllBytes(path));Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp-*"));
            File.WriteAllText(path,"broken replay");before=File.ReadAllBytes(path);Assert.Throws<IOException>(()=>ReplayStore.Write(path,document,rules,identity));
            CollectionAssert.AreEqual(before,File.ReadAllBytes(path));Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp-*"));
            using(var oversized=new FileStream(path,FileMode.Create))oversized.SetLength(ReplayStore.MaxBytes+1);Assert.Throws<IOException>(()=>Read());
        }
        [Test] public void CatalogRetainsIncompletePrefixAndPerFileErrorsWithoutTouchingOtherPlayerData()
        {
            var document=Fixture(abort:true);ReplayStore.Write(path,document,rules,identity);File.WriteAllText(Path.Combine(directory,"broken.replay.json"),"not JSON");
            File.WriteAllText(Path.Combine(directory,"player-settings.json"),"untouched");
            var entries=ReplayStore.Catalog(directory,rules,identity);Assert.AreEqual(2,entries.Count);
            var good=entries.Single(e=>e.CanOpen);Assert.AreEqual(ReplayFileStatus.IncompleteRecording,good.Status);Assert.IsTrue(good.Incomplete);Assert.IsNotEmpty(good.IncompleteReason);Assert.IsFalse(good.MatchEnded);
            var bad=entries.Single(e=>!e.CanOpen);Assert.AreEqual(ReplayFileStatus.Unreadable,bad.Status);Assert.IsNotEmpty(bad.Error);Assert.IsTrue(File.Exists(bad.FilePath));
            Assert.AreEqual("untouched",File.ReadAllText(Path.Combine(directory,"player-settings.json")));
            using(var session=new ReplaySession(rules,Read(),identity))Assert.IsTrue(session.Seek(session.EndStep),session.Error);
        }
        [Test] public void FileNamesRejectTraversalAndMissingCatalogDoesNotCreateDirectories()
        {
            foreach(string id in new[]{"../save","a/b","a\\b","","name.json","a:b"})Assert.Throws<ArgumentException>(()=>ReplayStore.PathFor(directory,id));
            string missing=Path.Combine(directory,"missing");Assert.IsEmpty(ReplayStore.Catalog(missing,rules,identity));Assert.IsFalse(Directory.Exists(missing));
            Assert.AreNotEqual(Path.Combine(Application.persistentDataPath,"Saves"),ReplayStore.DefaultDirectory);
        }
    }
}
