using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Frontier
{
    public enum ReplayFileStatus { RecordedSegment,IncompleteRecording,Unreadable,Incompatible }
    public sealed class ReplayCatalogEntry
    {
        public string FilePath {get;internal set;}
        public string FileName=>Path.GetFileName(FilePath);
        public string SegmentId {get;internal set;}="";
        public ReplayFileStatus Status {get;internal set;}=ReplayFileStatus.Unreadable;
        public string Error {get;internal set;}="";
        public bool CanOpen=>Status==ReplayFileStatus.RecordedSegment||Status==ReplayFileStatus.IncompleteRecording;
        public bool MatchEnded {get;internal set;}
        public bool Incomplete {get;internal set;}
        public string IncompleteReason {get;internal set;}="";
        public string FinishReason {get;internal set;}="";
        public string MapId {get;internal set;}="";
        public string MatchId {get;internal set;}="";
        public int PlayerCount {get;internal set;}
        public float DurationSeconds {get;internal set;}
        public int StartStep {get;internal set;}
        public int EndStep {get;internal set;}
        public int RecordCount {get;internal set;}
        public long StoredBytes {get;internal set;}
        public DateTime LastWriteUtc {get;internal set;}
    }
    // Replay files have their own directory and are never match slots, account
    // unlocks or preferences. These byte limits bound stored UTF-8 JSON and the
    // decompressed UTF-8 document, not total managed-memory consumption.
    public static class ReplayStore
    {
        public const long MaxBytes=64*1024*1024,MaxPayloadBytes=256*1024*1024;
        public const int MaxCatalogFiles=256;
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        static readonly object CatalogCacheLock=new object();
        static readonly Dictionary<string,ReplayCatalogEntry> ValidatedCatalogMetadata=new Dictionary<string,ReplayCatalogEntry>();
        static readonly Queue<string> CatalogCacheOrder=new Queue<string>();
        static long catalogValidationPasses;
        public static string DefaultDirectory=>Path.Combine(Application.persistentDataPath,"Replays");
        public static string PathFor(string directory,string segmentId)
        {
            if(string.IsNullOrEmpty(directory))throw new ArgumentException("Replay directory is missing",nameof(directory));
            if(string.IsNullOrEmpty(segmentId)||segmentId.Length>96||!segmentId.All(c=>c>='A'&&c<='Z'||c>='a'&&c<='z'||c>='0'&&c<='9'||c=='-'||c=='_'))
                throw new ArgumentException("Replay segment ID is not a safe file name",nameof(segmentId));
            return Path.Combine(directory,"replay-"+segmentId+".replay.json");
        }
        public static void Write(string path,ReplayDocument document,RuleCatalog rules,ReplayIdentity expectedIdentity)
        {WriteWithLimits(path,document,rules,expectedIdentity,MaxBytes,MaxPayloadBytes);}
        internal static void WriteWithLimits(string path,ReplayDocument document,RuleCatalog rules,ReplayIdentity expectedIdentity,long storedLimit,long payloadLimit)
        {
            RequireIdentity(rules,expectedIdentity);
            if(storedLimit<=0||storedLimit>MaxBytes||payloadLimit<=0||payloadLimit>MaxPayloadBytes)throw new ArgumentOutOfRangeException("Replay size limit");
            Validate(rules,document,expectedIdentity);
            string payload=JsonUtility.ToJson(document);
            try{ReplayJson.Validate<ReplayDocument>(payload);}
            catch(FormatException error){throw new IOException("Replay payload is not strict JSON: "+error.Message,error);}
            var envelope=MatchSaveStore.EncodePayload(payload,payloadLimit);
            byte[] bytes=Utf8.GetBytes(JsonUtility.ToJson(envelope));
            if(bytes.LongLength>storedLimit)throw new IOException("Replay exceeds the stored file size limit");
            string fullPath=Path.GetFullPath(path);
            // Corrupt or incompatible inputs remain read-only. A user can save
            // another named recording; this API never silently repairs/deletes it.
            if(File.Exists(fullPath))Read(fullPath,rules,expectedIdentity);
            string directory=Path.GetDirectoryName(fullPath);Directory.CreateDirectory(directory);
            string temporary=fullPath+".tmp-"+Guid.NewGuid().ToString("N");
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                // Replacement failure preserves the old file. No delete-then-
                // move fallback can turn a failed save into data loss.
                if(File.Exists(fullPath))File.Replace(temporary,fullPath,null);else File.Move(temporary,fullPath);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
            RememberCatalogMetadata(CatalogContext(rules,expectedIdentity),BytesHash(bytes),document);
        }
        public static ReplayDocument Read(string path,RuleCatalog rules,ReplayIdentity expectedIdentity)
        {
            RequireIdentity(rules,expectedIdentity);
            try
            {
                string json=ReadBounded(path,out string storedHash);
                var document=ReadDocument(json,rules,expectedIdentity);
                RememberCatalogMetadata(CatalogContext(rules,expectedIdentity),storedHash,document);return document;
            }
            catch(Exception error)when(error is ArgumentException||error is FormatException)
            {throw new IOException("Replay is unreadable: "+error.Message,error);}
        }
        // Catalog cache hits never enter this path: opening a file always runs
        // the original envelope, document, anchor and ledger validation.
        static ReplayDocument ReadDocument(string json,RuleCatalog rules,ReplayIdentity expectedIdentity)
        {
            try
            {
                // All envelope values get type/duplicate/unknown-key validation;
                // schema and digest must be present even for the plain legacy
                // container. No legacy ReplayDocument or MatchSave is accepted.
                ReplayJson.Validate<SaveEnvelope>(json,new[]{"schema","sha256"});
                var envelope=JsonUtility.FromJson<SaveEnvelope>(json);
                if(envelope==null||envelope.schema!=1&&envelope.schema!=2)throw new ReplayIncompatibleException("Unsupported replay storage schema");
                if(envelope?.sha256==null||envelope.sha256.Length!=64||!envelope.sha256.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f'))throw new IOException("Replay envelope digest is invalid");
                string payload=MatchSaveStore.DecodePayload(envelope,MaxPayloadBytes);
                ReplayJson.Validate<ReplayDocument>(payload);
                var document=JsonUtility.FromJson<ReplayDocument>(payload);Validate(rules,document,expectedIdentity);return document;
            }
            catch(Exception error)when(error is ArgumentException||error is FormatException)
            {throw new IOException("Replay is unreadable: "+error.Message,error);}
        }
        public static IReadOnlyList<ReplayCatalogEntry> Catalog(string directory,RuleCatalog rules,ReplayIdentity expectedIdentity)
        {
            RequireIdentity(rules,expectedIdentity);
            if(string.IsNullOrEmpty(directory))throw new ArgumentException("Replay directory is missing",nameof(directory));
            if(!Directory.Exists(directory))return Array.Empty<ReplayCatalogEntry>();
            var entries=new List<ReplayCatalogEntry>();string context=CatalogContext(rules,expectedIdentity);
            foreach(string file in Directory.EnumerateFiles(directory,"*.replay.json",SearchOption.TopDirectoryOnly))
            {
                if(entries.Count>=MaxCatalogFiles)throw new IOException("Replay catalog exceeds "+MaxCatalogFiles+" files; existing files are preserved");
                var entry=new ReplayCatalogEntry{FilePath=file,Status=ReplayFileStatus.Unreadable};
                try
                {
                    var info=new FileInfo(file);entry.StoredBytes=info.Length;entry.LastWriteUtc=info.LastWriteTimeUtc;
                    string json=ReadBounded(file,out string storedHash);
                    if(!TryCatalogMetadata(context,storedHash,out var metadata))
                    {
                        Interlocked.Increment(ref catalogValidationPasses);
                        var document=ReadDocument(json,rules,expectedIdentity);metadata=CatalogMetadata(document);
                        RememberCatalogMetadata(context,storedHash,document);
                    }
                    entry=metadata;entry.FilePath=file;entry.StoredBytes=info.Length;entry.LastWriteUtc=info.LastWriteTimeUtc;
                }
                catch(Exception error)when(error is IOException||error is UnauthorizedAccessException||error is ArgumentException||error is FormatException)
                {entry.Error=error.Message;if(error is ReplayIncompatibleException)entry.Status=ReplayFileStatus.Incompatible;}
                entries.Add(entry);
            }
            entries.Sort((a,b)=>{int date=b.LastWriteUtc.CompareTo(a.LastWriteUtc);return date!=0?date:string.CompareOrdinal(a.FileName,b.FileName);});
            return entries.AsReadOnly();
        }
        static string CatalogContext(RuleCatalog rules,ReplayIdentity identity)
        {return SnapshotCodec.Hash(JsonUtility.ToJson(identity)+"|"+SnapshotCodec.RulesHash(rules));}
        static ReplayCatalogEntry CatalogMetadata(ReplayDocument document)
        {
            var first=document.anchors[0];var last=document.anchors[document.anchors.Length-1];
            return new ReplayCatalogEntry{SegmentId=document.segmentId,Status=document.incomplete?ReplayFileStatus.IncompleteRecording:ReplayFileStatus.RecordedSegment,
                StartStep=first.completedStep,EndStep=last.completedStep,RecordCount=document.records.Length,MatchEnded=last.world.winner!=-1,
                MapId=first.world.mapId,PlayerCount=first.world.players.Length,MatchId=document.matchId,DurationSeconds=last.world.time-first.world.time,
                FinishReason=document.finishReason,Incomplete=document.incomplete,IncompleteReason=document.incompleteReason};
        }
        static ReplayCatalogEntry CopyCatalogMetadata(ReplayCatalogEntry entry)
        {
            return new ReplayCatalogEntry{SegmentId=entry.SegmentId,Status=entry.Status,StartStep=entry.StartStep,EndStep=entry.EndStep,RecordCount=entry.RecordCount,
                MatchEnded=entry.MatchEnded,MapId=entry.MapId,PlayerCount=entry.PlayerCount,MatchId=entry.MatchId,DurationSeconds=entry.DurationSeconds,
                FinishReason=entry.FinishReason,Incomplete=entry.Incomplete,IncompleteReason=entry.IncompleteReason};
        }
        static bool TryCatalogMetadata(string context,string storedHash,out ReplayCatalogEntry entry)
        {
            lock(CatalogCacheLock)
            {
                if(ValidatedCatalogMetadata.TryGetValue(context+":"+storedHash,out var cached))
                {entry=CopyCatalogMetadata(cached);return true;}
            }
            entry=null;return false;
        }
        static void RememberCatalogMetadata(string context,string storedHash,ReplayDocument document)
        {
            string key=context+":"+storedHash;
            lock(CatalogCacheLock)
            {
                if(ValidatedCatalogMetadata.ContainsKey(key))return;
                // Trusted only inside this process, after complete validation.
                // Every listing still hashes the actual bounded file bytes;
                // timestamps/lengths or mutable caller-owned entries are never
                // evidence. No sidecar or persisted cache can assert validity.
                while(ValidatedCatalogMetadata.Count>=MaxCatalogFiles)ValidatedCatalogMetadata.Remove(CatalogCacheOrder.Dequeue());
                ValidatedCatalogMetadata.Add(key,CatalogMetadata(document));CatalogCacheOrder.Enqueue(key);
            }
        }
        static void RequireIdentity(RuleCatalog rules,ReplayIdentity expected)
        {
            if(rules==null||expected==null)throw new ArgumentException("Replay rules and expected build identity are required");
            if(string.IsNullOrEmpty(expected.appVersion)||string.IsNullOrEmpty(expected.buildIdentity)||string.IsNullOrEmpty(expected.unityVersion)||string.IsNullOrEmpty(expected.rulesHash))
                throw new ArgumentException("Expected replay build identity is incomplete");
        }
        static void Validate(RuleCatalog rules,ReplayDocument document,ReplayIdentity expected)
        {
            if(document!=null&&document.schema!=ReplayDocument.CurrentSchema)throw new ReplayIncompatibleException("Unsupported replay document schema");
            var identity=document?.identity;
            if(identity!=null&&(identity.appVersion!=expected.appVersion||identity.buildIdentity!=expected.buildIdentity||identity.unityVersion!=expected.unityVersion||identity.rulesHash!=expected.rulesHash||identity.mapGenerationVersion!=expected.mapGenerationVersion||identity.guidedCatalogRevision!=expected.guidedCatalogRevision||identity.scenarioFormatVersion!=expected.scenarioFormatVersion||identity.scenarioRuntimeVersion!=expected.scenarioRuntimeVersion))
                throw new ReplayIncompatibleException("Replay requires its original game version, build, Unity version, rules and catalog revisions");
            try{ReplayValidation.Validate(rules,document,expected);}
            catch(ArgumentException error){throw new IOException("Replay validation failed: "+error.Message,error);}
        }
        sealed class ReplayIncompatibleException:IOException
        {public ReplayIncompatibleException(string message):base(message){}}
        static string BytesHash(byte[] bytes)
        {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        static string ReadBounded(string path,out string storedHash)
        {
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length<=0||stream.Length>MaxBytes)throw new IOException("Replay file size is outside the allowed range");
                byte[] bytes=new byte[(int)stream.Length];int used=0;
                while(used<bytes.Length){int count=stream.Read(bytes,used,bytes.Length-used);if(count==0)throw new IOException("Replay file was truncated while reading");used+=count;}
                if(stream.ReadByte()!=-1)throw new IOException("Replay file grew while reading");storedHash=BytesHash(bytes);return Utf8.GetString(bytes);
            }
        }
    }
}
