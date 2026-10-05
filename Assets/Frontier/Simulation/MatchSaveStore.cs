using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Frontier
{
    [Serializable] public sealed class SavedMatch
    {
        public string matchId,appVersion;public WorldSnapshot world;public MatchSimulationSnapshot simulation;
        public Vector3 cameraPosition;public float cameraSize=26;
        public Vector2 cameraFocus;public int[] selection;public ControlGroupSnapshot[] groups;
        public bool muted,environmentSound=true,music=true,autoplay;public float simulationSpeed=1;
    }
    [Serializable] public sealed class SaveEnvelope
    {public int schema=1;public string payload,sha256,encoding,payloadBase64;}
    public static class MatchSaveStore
    {
        public const long MaxBytes=64*1024*1024;
        public const long MaxPayloadBytes=256*1024*1024;
        static readonly UTF8Encoding StrictUtf8=new UTF8Encoding(false,true);
        static readonly uint[] CrcTable=CreateCrcTable();
        public static string SlotPath(string directory,int slot)
        {if(slot<1||slot>3)throw new ArgumentOutOfRangeException(nameof(slot));return Path.Combine(directory,"slot-"+slot+".json");}
        public static void Write(string path,SavedMatch save)
        {WriteWithLimits(path,save,MaxBytes,MaxPayloadBytes);}
        internal static void WriteWithLimits(string path,SavedMatch save,long storedLimit,long payloadLimit)
        {
            if(save?.world==null||save.simulation==null)throw new ArgumentException("No match to save");
            string payload=JsonUtility.ToJson(save);
            var envelope=EncodePayload(payload,payloadLimit);
            string json=JsonUtility.ToJson(envelope);
            byte[] bytes=StrictUtf8.GetBytes(json);if(bytes.LongLength>storedLimit)throw new IOException("This match exceeds the save size limit");
            string directory=Path.GetDirectoryName(Path.GetFullPath(path));Directory.CreateDirectory(directory);
            string temporary=path+".tmp-"+Guid.NewGuid().ToString("N");
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                // Replace is atomic on the supported local filesystem. If it fails,
                // retain the old slot and report the error instead of deleting it.
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        public static SavedMatch Read(string path,string expectedVersion=null)
        {
            SavedMatch saved;
            try
            {
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(stream.Length>MaxBytes)throw new IOException("Save file exceeds the size limit");
                    var envelope=JsonUtility.FromJson<SaveEnvelope>(ReadUtf8(stream,MaxBytes,out _,out _,out _));
                    saved=JsonUtility.FromJson<SavedMatch>(DecodePayload(envelope,MaxPayloadBytes));
                }
            }
            catch(FileNotFoundException){throw new FileNotFoundException("Empty save slot");}
            catch(Exception error)when(error is ArgumentException||error is FormatException)
            {throw new IOException("Save is incomplete or damaged",error);}
            if(saved?.world==null||saved.simulation==null||string.IsNullOrEmpty(saved.matchId))throw new IOException("Save has no match state");
            if(expectedVersion!=null&&(string.IsNullOrEmpty(saved.appVersion)||saved.appVersion!=expectedVersion))throw new IOException("Save requires game version "+(saved.appVersion??"unknown")+"; current version is "+expectedVersion);
            if(!SnapshotCodec.Finite(saved.cameraSize)||saved.cameraSize<8||saved.cameraSize>100||!SnapshotCodec.Finite(saved.cameraPosition.x)||!SnapshotCodec.Finite(saved.cameraPosition.y)||!SnapshotCodec.Finite(saved.cameraPosition.z))throw new IOException("Save has invalid camera settings");
            if(!SnapshotCodec.Finite(saved.cameraFocus)||!SnapshotCodec.Finite(saved.simulationSpeed)||saved.simulationSpeed<1||saved.simulationSpeed>16)throw new IOException("Save has invalid session settings");
            return saved;
        }

        // Limits are parameters only for bounded codec tests; public writes and
        // reads always use the fixed limits above.
        internal static SaveEnvelope EncodePayload(string payload,long limit)
        {
            try
            {
                if(StrictUtf8.GetByteCount(payload)>limit)throw new IOException("This match exceeds the uncompressed save size limit");
                using(var compressed=new BoundedMemoryStream(MaxBytes/4*3))
                using(var hash=SHA256.Create())
                {
                    using(var gzip=new GZipStream(compressed,System.IO.Compression.CompressionLevel.Optimal,true))
                    {
                        var encoder=StrictUtf8.GetEncoder();var characters=new char[4096];var bytes=new byte[StrictUtf8.GetMaxByteCount(characters.Length)];
                        for(int offset=0;offset<payload.Length;)
                        {
                            int count=Math.Min(characters.Length,payload.Length-offset);payload.CopyTo(offset,characters,0,count);offset+=count;
                            int written=encoder.GetBytes(characters,0,count,bytes,0,offset==payload.Length);
                            hash.TransformBlock(bytes,0,written,bytes,0);gzip.Write(bytes,0,written);
                        }
                        hash.TransformFinalBlock(Array.Empty<byte>(),0,0);
                    }
                    return new SaveEnvelope{schema=2,encoding="gzip-base64",payloadBase64=Convert.ToBase64String(compressed.GetBuffer(),0,(int)compressed.Length),sha256=Digest(hash.Hash)};
                }
            }
            catch(ArgumentException error){throw new IOException("Save contains invalid UTF-8 text",error);}
        }
        internal static string DecodePayload(SaveEnvelope envelope,long limit)
        {
            if(envelope==null)throw new IOException("Save is incomplete or damaged");
            if(envelope.schema==1)
            {
                if(string.IsNullOrEmpty(envelope.payload)||!string.IsNullOrEmpty(envelope.encoding)||!string.IsNullOrEmpty(envelope.payloadBase64)||StrictUtf8.GetByteCount(envelope.payload)>limit||envelope.sha256!=SnapshotCodec.Hash(envelope.payload))
                    throw new IOException("Save is incomplete or damaged");
                return envelope.payload;
            }
            if(envelope.schema!=2||envelope.encoding!="gzip-base64"||!string.IsNullOrEmpty(envelope.payload))throw new IOException("Save is incomplete or damaged");
            using(var encoded=new Base64ReadStream(envelope.payloadBase64))
            {
                // New saves contain one gzip member with no optional header.
                // Footer checks also reject truncation on runtimes whose gzip
                // inflater otherwise returns the entire payload without a footer.
                if(encoded.Length<18||encoded.ByteAt(0)!=0x1f||encoded.ByteAt(1)!=0x8b||encoded.ByteAt(2)!=8||encoded.ByteAt(3)!=0)
                    throw new IOException("Save has an invalid gzip header");
                string payload;string digest;uint crc;long length;
                encoded.BeginDeflate();
                // Decode only the raw deflate body. GZipStream can concatenate
                // members, including an empty first member whose final CRC and
                // length would still match the combined payload.
                using(var deflate=new DeflateStream(encoded,CompressionMode.Decompress,true))payload=ReadUtf8(deflate,limit,out digest,out crc,out length);
                if(encoded.Position!=encoded.Length-8||encoded.UInt32At(encoded.Length-8)!=crc||encoded.UInt32At(encoded.Length-4)!=(uint)length||envelope.sha256!=digest)
                    throw new IOException("Save is incomplete or damaged");
                return payload;
            }
        }
        static string ReadUtf8(Stream stream,long limit,out string digest,out uint crc,out long length)
        {
            var decoder=StrictUtf8.GetDecoder();var bytes=new byte[8192];var characters=new char[StrictUtf8.GetMaxCharCount(bytes.Length)];var text=new StringBuilder();
            length=0;uint checksum=uint.MaxValue;
            using(var hash=SHA256.Create())
            {
                int count;
                while((count=stream.Read(bytes,0,(int)Math.Min(bytes.Length,limit-length+1)))>0)
                {
                    length+=count;if(length>limit)throw new IOException("Save exceeds the decoded size limit");
                    hash.TransformBlock(bytes,0,count,bytes,0);
                    for(int i=0;i<count;i++)checksum=CrcTable[(checksum^bytes[i])&255]^(checksum>>8);
                    text.Append(characters,0,decoder.GetChars(bytes,0,count,characters,0,false));
                }
                text.Append(characters,0,decoder.GetChars(Array.Empty<byte>(),0,0,characters,0,true));
                hash.TransformFinalBlock(Array.Empty<byte>(),0,0);digest=Digest(hash.Hash);
            }
            crc=checksum^uint.MaxValue;return text.ToString();
        }
        static string Digest(byte[] bytes)=>BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
        static uint[] CreateCrcTable()
        {
            var table=new uint[256];for(uint i=0;i<table.Length;i++){uint value=i;for(int bit=0;bit<8;bit++)value=(value&1)!=0?0xedb88320u^(value>>1):value>>1;table[i]=value;}return table;
        }
        sealed class BoundedMemoryStream:MemoryStream
        {
            readonly long limit;public BoundedMemoryStream(long limit){this.limit=limit;}
            public override void Write(byte[] buffer,int offset,int count)
            {if(Position+count>limit)throw new IOException("This match exceeds the save size limit");base.Write(buffer,offset,count);}
            public override void WriteByte(byte value)
            {if(Position>=limit)throw new IOException("This match exceeds the save size limit");base.WriteByte(value);}
        }
        sealed class Base64ReadStream:Stream
        {
            readonly string text;readonly long length;long position;
            public Base64ReadStream(string text)
            {
                if(string.IsNullOrEmpty(text)||text.Length%4!=0)throw new IOException("Save has invalid Base64 data");
                int padding=text.EndsWith("==",StringComparison.Ordinal)?2:text.EndsWith("=",StringComparison.Ordinal)?1:0;
                for(int i=0;i<text.Length-padding;i++)if(Value(text[i])<0)throw new IOException("Save has invalid Base64 data");
                if(padding==1&&(Value(text[text.Length-2])&3)!=0||padding==2&&(Value(text[text.Length-3])&15)!=0)throw new IOException("Save has noncanonical Base64 data");
                this.text=text;length=text.Length/4L*3-padding;
            }
            static int Value(char c)=>c>='A'&&c<='Z'?c-'A':c>='a'&&c<='z'?c-'a'+26:c>='0'&&c<='9'?c-'0'+52:c=='+'?62:c=='/'?63:-1;
            public byte ByteAt(long index)
            {
                int offset=(int)(index/3)*4;int shift=(int)(index%3);
                if(shift==0)return (byte)(Value(text[offset])<<2|Value(text[offset+1])>>4);
                if(shift==1)return (byte)(Value(text[offset+1])<<4|Value(text[offset+2])>>2);
                return (byte)(Value(text[offset+2])<<6|Value(text[offset+3]));
            }
            public uint UInt32At(long index)=>(uint)(ByteAt(index)|ByteAt(index+1)<<8|ByteAt(index+2)<<16|ByteAt(index+3)<<24);
            public void BeginDeflate(){position=10;}
            public override int Read(byte[] buffer,int offset,int count)
            {
                // Withhold the last body byte from bulk reads. An inflater that
                // finishes before that byte leaves it unread, exposing trailing
                // garbage even when its earlier input buffer read ahead. A valid
                // deflate body must finish without requesting EOF as more input.
                if(count==0)return 0;
                long end=length-8;if(position>=end)throw new IOException("Save has an incomplete deflate body");
                count=position<end-1?(int)Math.Min(count,end-1-position):Math.Min(count,1);
                for(int i=0;i<count;i++)buffer[offset+i]=ByteAt(position++);return count;
            }
            public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
            public override long Length=>length;public override long Position{get=>position;set=>throw new NotSupportedException();}
            public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
            public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        }
    }
}
