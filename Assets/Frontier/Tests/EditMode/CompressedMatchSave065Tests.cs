using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class CompressedMatchSave065Tests
    {
        string directory,path;
        [SetUp] public void SetUp()
        {directory=Path.Combine(Path.GetTempPath(),"Frontier-compressed-save-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);path=MatchSaveStore.SlotPath(directory,1);}
        [TearDown] public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static object Codec(string name,params object[] arguments)
        {
            try{return typeof(MatchSaveStore).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,arguments);}
            catch(TargetInvocationException error){ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        }
        static SavedMatch Match(World world,MatchSimulation simulation=null)
        {return new SavedMatch{matchId="compression-fixture",appVersion="0.8.0",world=world.CaptureState(),simulation=(simulation??new MatchSimulation(world)).CaptureState(),cameraPosition=new Vector3(50,30,20),cameraFocus=new Vector2(52,24),cameraSize=18};}
        static World World()=>new World(Catalog.Load(),314159,"france","river",5,"river");
        void Envelope(SaveEnvelope envelope)=>File.WriteAllText(path,JsonUtility.ToJson(envelope),new UTF8Encoding(false,true));
        static byte[] Gzip(byte[] raw)
        {
            // Mono may emit no bytes when its compressor receives no input;
            // use an actual complete empty gzip member for concatenation tests.
            if(raw.Length==0)return new byte[]{0x1f,0x8b,8,0,0,0,0,0,0,3,3,0,0,0,0,0,0,0,0,0};
            using(var output=new MemoryStream()){using(var gzip=new GZipStream(output,System.IO.Compression.CompressionLevel.Optimal,true))gzip.Write(raw,0,raw.Length);return output.ToArray();}
        }
        static string Hash(byte[] raw)
        {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(raw)).Replace("-","").ToLowerInvariant();}

        [Test] public void CompressedFullMatchRetainsCargoGroupsEventsAndColdSimulationContinuation()
        {
            // Controlled transport fixture: real embark commands and ticks, no
            // claim that these spawned actors came from a normal paid opening.
            var world=World();var ship=world.Spawn("transport_ship",0,new Vector2(64,12));
            Assert.IsTrue(world.TryTransportShore(ship,new Vector2(55,12),.4f,out var shore,out _));
            var passengers=new[]{world.Spawn("musketeer",0,shore+new Vector2(-2,-1)),world.Spawn("musketeer",0,shore+new Vector2(-2,1))};
            world.Fog.Tick(world.Entities);
            foreach(var passenger in passengers)
            {
                Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=new[]{passenger.id},target=ship.id,point=shore}).accepted);
                for(int i=0;i<900&&passenger.garrisonedIn!=ship.id;i++)world.Tick(.1f);
                Assert.AreEqual(ship.id,passenger.garrisonedIn,"Each fixture passenger must physically board before the next approaches");
            }
            Assert.IsTrue(passengers.All(e=>e.garrisonedIn==ship.id),"The fixture must have real boarded cargo before saving");
            var simulation=new MatchSimulation(world,1);simulation.AdvanceFrame(.05f);
            var save=Match(world,simulation);save.selection=new[]{ship.id};save.groups=new[]{new ControlGroupSnapshot{number=4,actors=passengers.Select(e=>e.id).ToArray()}};
            MatchSaveStore.Write(path,save);var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path));
            Assert.AreEqual(2,envelope.schema);Assert.AreEqual("gzip-base64",envelope.encoding);Assert.IsTrue(string.IsNullOrEmpty(envelope.payload));
            var loaded=MatchSaveStore.Read(path,"0.8.0");Assert.AreEqual(JsonUtility.ToJson(save),JsonUtility.ToJson(loaded));
            CollectionAssert.AreEqual(passengers.Select(e=>e.id),loaded.groups.Single().actors);Assert.AreEqual(world.Events.Count,loaded.world.events.Length);
            var cold=Frontier.World.Restore(world.Rules,loaded.world);var resumed=MatchSimulation.Restore(cold,loaded.simulation);
            for(int i=0;i<50;i++){simulation.Step();resumed.Step();}
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(cold.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(simulation.CaptureState()),JsonUtility.ToJson(resumed.CaptureState()));
            Assert.IsTrue(passengers.All(e=>cold.Get(e.id).garrisonedIn==ship.id));
        }
        [Test] public void LegacySchemaOneRetainsTheOriginalMatchAndDoesNotRewriteTheFile()
        {
            var save=Match(World());string payload=JsonUtility.ToJson(save);Envelope(new SaveEnvelope{payload=payload,sha256=SnapshotCodec.Hash(payload)});
            byte[] before=File.ReadAllBytes(path);Assert.AreEqual(payload,JsonUtility.ToJson(MatchSaveStore.Read(path,"0.8.0")));CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
        }
        [Test] public void DamagedGzipHashAmbiguousPayloadAndNoncanonicalBase64AreRejected()
        {
            var save=Match(World());MatchSaveStore.Write(path,save);string original=File.ReadAllText(path);
            var valid=JsonUtility.FromJson<SaveEnvelope>(original);byte[] bytes=Convert.FromBase64String(valid.payloadBase64),empty=Gzip(Array.Empty<byte>());
            var crc=(byte[])bytes.Clone();crc[crc.Length-8]^=0x80;var size=(byte[])bytes.Clone();size[size.Length-4]^=1;
            var garbageInsideBody=bytes.Take(bytes.Length-8).Concat(new byte[]{0,1,2}).Concat(bytes.Skip(bytes.Length-8)).ToArray();
            var incompleteBody=bytes.Take(bytes.Length-9).Concat(bytes.Skip(bytes.Length-8)).ToArray();
            var invalid=new[]{bytes.Skip(4).ToArray(),bytes.Take(bytes.Length-4).ToArray(),crc,size,bytes.Concat(new byte[]{0}).ToArray(),bytes.Concat(bytes).ToArray(),empty.Concat(bytes).ToArray(),bytes.Concat(empty).ToArray(),garbageInsideBody,incompleteBody};
            for(int i=0;i<invalid.Length;i++)
            {var envelope=JsonUtility.FromJson<SaveEnvelope>(original);envelope.payloadBase64=Convert.ToBase64String(invalid[i]);Envelope(envelope);Assert.Throws<IOException>(()=>MatchSaveStore.Read(path),"Corruption case "+i);}
            var wrongHash=JsonUtility.FromJson<SaveEnvelope>(original);wrongHash.sha256=new string('0',64);Envelope(wrongHash);Assert.Throws<IOException>(()=>MatchSaveStore.Read(path));
            var ambiguous=JsonUtility.FromJson<SaveEnvelope>(original);ambiguous.payload=" ";Envelope(ambiguous);Assert.Throws<IOException>(()=>MatchSaveStore.Read(path));
            var whitespace=JsonUtility.FromJson<SaveEnvelope>(original);whitespace.payloadBase64+="\n";Envelope(whitespace);Assert.Throws<IOException>(()=>MatchSaveStore.Read(path));
            // Preserve an otherwise valid gzip and raw hash, changing only bits
            // that Base64 decoders ordinarily discard in the padded last quartet.
            SaveEnvelope padded=null;
            for(int i=1;i<=8;i++){var candidate=(SaveEnvelope)Codec("EncodePayload",new string('x',i),1024L);if(candidate.payloadBase64.EndsWith("=")){padded=candidate;break;}}
            Assert.IsNotNull(padded);const string alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
            int index=padded.payloadBase64.TrimEnd('=').Length-1;char[] encoded=padded.payloadBase64.ToCharArray();encoded[index]=alphabet[alphabet.IndexOf(encoded[index])+1];
            string canonical=padded.payloadBase64;padded.payloadBase64=new string(encoded);
            CollectionAssert.AreEqual(Convert.FromBase64String(canonical),Convert.FromBase64String(padded.payloadBase64));
            Assert.Throws<IOException>(()=>Codec("DecodePayload",padded,1024L));
        }
        [Test] public void InvalidUtf8IsRejectedEvenWhenTheRawHashMatches()
        {
            byte[] invalid={0xc3,0x28};Envelope(new SaveEnvelope{schema=2,encoding="gzip-base64",payloadBase64=Convert.ToBase64String(Gzip(invalid)),sha256=Hash(invalid)});
            Assert.Throws<IOException>(()=>MatchSaveStore.Read(path));
            File.WriteAllBytes(path,new byte[]{0x7b,0xff,0x7d});Assert.Throws<IOException>(()=>MatchSaveStore.Read(path));
        }
        [Test] public void ByteLimitsRejectOversizedInflationAndPreserveAnExistingSlotOnWriteFailure()
        {
            string text=new string('x',4095)+"😀汉字"+new string('x',4096);long bytes=new UTF8Encoding(false,true).GetByteCount(text);
            var envelope=(SaveEnvelope)Codec("EncodePayload",text,bytes);
            Assert.AreEqual(text,Codec("DecodePayload",envelope,bytes));Assert.Throws<IOException>(()=>Codec("DecodePayload",envelope,bytes-1));
            Assert.Throws<IOException>(()=>Codec("EncodePayload",text,bytes-1));
            var save=Match(World());MatchSaveStore.Write(path,save);byte[] previous=File.ReadAllBytes(path);
            Assert.Throws<IOException>(()=>Codec("WriteWithLimits",path,save,MatchSaveStore.MaxBytes,1L));
            CollectionAssert.AreEqual(previous,File.ReadAllBytes(path));Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp-*"));
            Assert.Throws<IOException>(()=>Codec("WriteWithLimits",path,save,1L,MatchSaveStore.MaxPayloadBytes));
            CollectionAssert.AreEqual(previous,File.ReadAllBytes(path));Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp-*"));
            using(var oversized=new FileStream(Path.Combine(directory,"oversized.json"),FileMode.Create))oversized.SetLength(MatchSaveStore.MaxBytes+1);
            Assert.Throws<IOException>(()=>MatchSaveStore.Read(Path.Combine(directory,"oversized.json")));
        }
        [Test] public void RawEventPayloadLargerThan64MiBCanBeSavedWithoutRaisingTheFileLimit()
        {
            // One bounded large fixture reproduces the size failure without a
            // 1,700-second AI simulation or duplicating the full event workload.
            var save=Match(World());save.world.events=new[]{new SimEvent{kind="CompressionFixture",player=0,message=new string('x',66*1024*1024)}};
            string raw=JsonUtility.ToJson(save);Assert.Greater(Encoding.UTF8.GetByteCount(raw),MatchSaveStore.MaxBytes);raw=null;
            MatchSaveStore.Write(path,save);Assert.Less(new FileInfo(path).Length,MatchSaveStore.MaxBytes);
            var loaded=MatchSaveStore.Read(path,"0.8.0");Assert.AreEqual(66*1024*1024,loaded.world.events.Single().message.Length);
            Assert.AreEqual('x',loaded.world.events.Single().message[0]);Assert.AreEqual('x',loaded.world.events.Single().message[66*1024*1024-1]);
            Assert.AreEqual(save.simulation.stepCount,loaded.simulation.stepCount);Assert.AreEqual(save.world.entities.Length,loaded.world.entities.Length);
        }
    }
}
