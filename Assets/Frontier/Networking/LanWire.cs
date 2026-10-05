using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Frontier
{
    [Serializable] public sealed class LanEnvelope
    {
        public int schema;
        public string kind="",payload="",error="",matchId="",epoch="";
        public long nonce,stateRevision;
    }
    // Serialization/validation runs on the caller's main thread. The TCP worker
    // owns only bounded encoded bytes, never a live game or a Unity object.
    public static class LanWire
    {
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        public static byte[] Encode<T>(string kind,T payload,string matchId="",string epoch="",long nonce=0,long revision=0,string error="")
        {
            var envelope=new LanEnvelope{schema=LanProtocol.Version,kind=kind,payload=payload==null?"":JsonUtility.ToJson(payload),matchId=matchId,epoch=epoch,nonce=nonce,stateRevision=revision,error=error};
            var raw=Utf8.GetBytes(JsonUtility.ToJson(envelope));
            if(raw.Length>LanProtocol.MaxExpandedBytes)throw new InvalidDataException("Network state exceeds expanded size limit");
            using(var output=new MemoryStream())
            {
                output.Write(new byte[]{70,82,76,78,1},0,5);
                output.Write(BitConverter.GetBytes(raw.Length),0,4);
                using(var sha=SHA256.Create()){var hash=sha.ComputeHash(raw);output.Write(hash,0,hash.Length);}
                using(var gzip=new GZipStream(output,System.IO.Compression.CompressionLevel.Fastest,true))gzip.Write(raw,0,raw.Length);
                if(output.Length>LanProtocol.MaxWireBytes)throw new InvalidDataException("Network state exceeds frame size limit");
                return output.ToArray();
            }
        }
        public static LanEnvelope Decode(byte[] bytes)
        {
            if(bytes==null||bytes.Length<59||bytes.Length>LanProtocol.MaxWireBytes||bytes[0]!=70||bytes[1]!=82||bytes[2]!=76||bytes[3]!=78||bytes[4]!=1)
                throw new InvalidDataException("Invalid network frame");
            int length=BitConverter.ToInt32(bytes,5);
            if(length<1||length>LanProtocol.MaxExpandedBytes)throw new InvalidDataException("Network expansion limit exceeded");
            byte[] raw;
            using(var input=new MemoryStream(bytes,41,bytes.Length-41,false))
            using(var gzip=new GZipStream(input,CompressionMode.Decompress))
            using(var output=new MemoryStream(Math.Min(length,262144)))
            {
                var buffer=new byte[16384];int count;
                while((count=gzip.Read(buffer,0,buffer.Length))>0)
                {if(output.Length+count>length)throw new InvalidDataException("Unexpected network expansion");output.Write(buffer,0,count);}
                raw=output.ToArray();
            }
            if(raw.Length!=length)throw new InvalidDataException("Truncated network state");
            using(var sha=SHA256.Create()){var hash=sha.ComputeHash(raw);for(int i=0;i<32;i++)if(hash[i]!=bytes[9+i])throw new InvalidDataException("Network frame digest mismatch");}
            string json=Utf8.GetString(raw);ReplayJson.Validate<LanEnvelope>(json);
            var result=JsonUtility.FromJson<LanEnvelope>(json);
            if(result.schema!=LanProtocol.Version||string.IsNullOrEmpty(result.kind)||result.kind.Length>32||result.matchId.Length>128||result.epoch.Length>128||result.error.Length>512)
                throw new InvalidDataException("Unsupported network envelope");
            return result;
        }
        public static T Body<T>(LanEnvelope envelope)
        {
            ReplayJson.Validate<T>(envelope.payload);return JsonUtility.FromJson<T>(envelope.payload);
        }
    }
}
