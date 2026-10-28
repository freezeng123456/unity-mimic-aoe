using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class LanWire068Tests
    {
        private static byte[] Frame(byte[] raw, int? declaredLength = null)
        {
            using (var output = new MemoryStream())
            {
                output.Write(new byte[] { 70, 82, 76, 78, 1 }, 0, 5);
                var length = BitConverter.GetBytes(declaredLength ?? raw.Length);
                output.Write(length, 0, length.Length);
                using (var sha = SHA256.Create())
                {
                    var digest = sha.ComputeHash(raw);
                    output.Write(digest, 0, digest.Length);
                }
                using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Fastest, true)) gzip.Write(raw, 0, raw.Length);
                return output.ToArray();
            }
        }

        private static byte[] Envelope(string payload, int schema = 1)
            => Frame(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new LanEnvelope
            { schema = schema, kind = "ready", payload = payload, matchId = "match", epoch = "epoch", nonce = 9, stateRevision = 2 })));

        [Test]
        public void CompressedUtf8PreservesUnicodePayloadAndLongSequenceFields()
        {
            var ready = new LanReady { ready = true, civilization = "france", deck = new[] { "法兰西 / 港区", "économie", "船只🚢" } };
            var envelope = LanWire.Decode(LanWire.Encode("ready", ready, "match", "epoch", long.MaxValue - 1, long.MaxValue - 2));
            Assert.AreEqual("ready", envelope.kind);
            Assert.AreEqual(long.MaxValue - 1, envelope.nonce);
            Assert.AreEqual(long.MaxValue - 2, envelope.stateRevision);
            var decoded = LanWire.Body<LanReady>(envelope);
            Assert.IsTrue(decoded.ready);
            Assert.AreEqual(ready.civilization, decoded.civilization);
            CollectionAssert.AreEqual(ready.deck, decoded.deck);
        }

        [Test]
        public void InvalidFramesAndExpansionBoundsFailBeforeUnboundedDecoding()
        {
            Assert.Throws<InvalidDataException>(() => LanWire.Decode(null));
            Assert.Throws<InvalidDataException>(() => LanWire.Decode(new byte[58]));
            Assert.Throws<InvalidDataException>(() => LanWire.Decode(new byte[LanProtocol.MaxWireBytes + 1]));
            var wrongMagic = LanWire.Encode("ready", new LanReady());
            wrongMagic[0] = 0;
            Assert.Throws<InvalidDataException>(() => LanWire.Decode(wrongMagic));
            var raw = Encoding.UTF8.GetBytes(new string('x', 10000));
            Assert.Throws<InvalidDataException>(() => LanWire.Decode(Frame(raw, 0)));
            Assert.Throws<InvalidDataException>(() => LanWire.Decode(Frame(raw, LanProtocol.MaxExpandedBytes + 1)));
            Assert.Throws<InvalidDataException>(() => LanWire.Decode(Frame(raw, 1)), "A small declared output size must stop expansion");
            Assert.Throws<InvalidDataException>(() => LanWire.Decode(Frame(raw, raw.Length + 1)), "The actual uncompressed length must match its declaration");
        }

        [Test]
        public void ValidCompressionCannotHideChangedDigestOrInvalidUtf8()
        {
            var bytes = LanWire.Encode("ready", new LanReady());
            bytes[9] ^= 1;
            Assert.Throws<InvalidDataException>(() => LanWire.Decode(bytes));
            var invalidUtf8 = Frame(new byte[] { 0xff, 0xfe, 0x80 });
            Assert.Throws<DecoderFallbackException>(() => LanWire.Decode(invalidUtf8));
            var badVersion = Envelope("{\"ready\":false,\"civilization\":\"\",\"deck\":[]}", LanProtocol.Version + 1);
            Assert.Throws<InvalidDataException>(() => LanWire.Decode(badVersion));
        }

        [Test]
        public void DtoShapeValidationRejectsUnknownMissingDuplicateAndCoercedFields()
        {
            foreach (string payload in new[]
            {
                "{\"ready\":false,\"civilization\":\"france\"}",
                "{\"ready\":false,\"civilization\":\"france\",\"deck\":[],\"surprise\":1}",
                "{\"ready\":false,\"ready\":true,\"civilization\":\"france\",\"deck\":[]}",
                "{\"ready\":\"true\",\"civilization\":\"france\",\"deck\":[]}",
                "{\"ready\":false,\"civilization\":\"france\",\"deck\":null}",
                "{\"ready\":false,\"civilization\":\"france\",\"deck\":[]} {}"
            })
            {
                var envelope = LanWire.Decode(Envelope(payload));
                Assert.Throws<FormatException>(() => LanWire.Body<LanReady>(envelope), payload);
            }
            var unknownEnvelope = Encoding.UTF8.GetBytes("{\"schema\":1,\"kind\":\"ready\",\"payload\":\"{}\",\"error\":\"\",\"matchId\":\"m\",\"epoch\":\"e\",\"nonce\":0,\"stateRevision\":0,\"extra\":true}");
            Assert.Throws<FormatException>(() => LanWire.Decode(Frame(unknownEnvelope)));
        }
    }
}
