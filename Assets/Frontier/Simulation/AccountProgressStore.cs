using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Frontier
{
    [Serializable] public sealed class GuidedCompletionRecord
    {
        public string id,lastAttemptId;
        public int completions;
        public float bestSeconds;
        public string[] attempts=Array.Empty<string>();
    }
    [Serializable] sealed class GuidedAccountData
    {public int schema=1,catalogRevision=GuidedPlayCatalog.Revision;public GuidedCompletionRecord[] records=Array.Empty<GuidedCompletionRecord>();}
    [Serializable] sealed class GuidedAccountEnvelope
    {public int schema=1;public string payload,sha256;}
    // Local account progress is separate from match slots. Invalid/future files
    // are kept intact and become read-only until the user explicitly repairs them.
    public sealed class AccountProgressStore
    {
        const int MaxBytes=1024*1024,MaxAttempts=1000;
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        GuidedAccountData data=new GuidedAccountData();
        readonly string path;
        bool blocked;
        public bool MemoryOnly {get;}
        public bool CanWrite=>!blocked;
        public string LastError {get;private set;}="";
        public string FilePath=>path;
        public int CompletedCount=>data.records.Length;
        AccountProgressStore(string file,bool memory){path=file;MemoryOnly=memory;}
        public static AccountProgressStore Load(string directory=null,bool memoryOnly=false)
        {
            bool memory=memoryOnly||(directory==null&&(Application.isBatchMode||Application.isEditor));
            string file=memory?null:Path.Combine(directory??Path.Combine(Application.persistentDataPath,"Account"),"guided-progress.json");
            var result=new AccountProgressStore(file,memory);if(memory||!File.Exists(file))return result;
            try
            {
                string json=ReadBounded(file);GuidedProgressJson.Validate(json,new[]{"schema","payload","sha256"},new[]{"schema","payload","sha256"});
                var envelope=JsonUtility.FromJson<GuidedAccountEnvelope>(json);
                if(envelope==null||envelope.schema!=1||envelope.payload==null||envelope.sha256==null||envelope.sha256.Length!=64||SnapshotCodec.Hash(envelope.payload)!=envelope.sha256)throw new IOException("Unsupported or damaged account envelope");
                GuidedProgressJson.Validate(envelope.payload,new[]{"schema","catalogRevision","records","id","lastAttemptId","completions","bestSeconds","attempts"},new[]{"schema","catalogRevision","records"},new[]{"id","lastAttemptId","completions","bestSeconds","attempts"});
                var saved=JsonUtility.FromJson<GuidedAccountData>(envelope.payload);Validate(saved);result.data=saved;
            }
            catch(Exception error)when(error is IOException||error is ArgumentException||error is UnauthorizedAccessException||error is FormatException)
            {result.blocked=true;result.LastError="Account progress is unreadable; original file preserved: "+error.GetType().Name;}
            return result;
        }
        public bool IsCompleted(string id)=>GuidedPlayCatalog.Find(id)!=null&&data.records.Any(r=>r.id==id);
        public bool IsUnlocked(string id)
        {
            var d=GuidedPlayCatalog.Find(id);return d!=null&&(string.IsNullOrEmpty(d.UnlockAfterId)||IsCompleted(d.UnlockAfterId));
        }
        public float BestSeconds(string id)=>data.records.FirstOrDefault(r=>r.id==id)?.bestSeconds??-1;
        public int CompletionCount(string id)=>data.records.FirstOrDefault(r=>r.id==id)?.completions??0;
        public bool RecordCompletion(GuidedPlaySession session)
        {
            if(session==null||session.Status!=GuidedPlayStatus.Completed){LastError="Only completed scenarios can be recorded";return false;}
            if(blocked){LastError="Account file is read-only because loading failed; original file preserved";return false;}
            try
            {
                // Revalidate all saved witnesses against the actual completed
                // world. A merely edited status enum never earns an unlock.
                using(var verified=GuidedPlaySession.Restore(session.World,session.Simulation,session.CaptureState()))
                {
                    if(verified.Status!=GuidedPlayStatus.Completed)throw new ArgumentException("Completion lacks scenario witnesses");
                }
                var candidate=SnapshotCodec.Clone(data);var old=candidate.records.FirstOrDefault(r=>r.id==session.Definition.Id);
                if(candidate.records.Any(r=>r.attempts.Contains(session.AttemptId)))
                {
                    if(old==null||!old.attempts.Contains(session.AttemptId))throw new ArgumentException("Attempt belongs to a different scenario");
                    LastError="";return true;
                }
                if(!IsUnlocked(session.Definition.Id)){LastError="Complete the previous campaign chapter first";return false;}
                if(old==null)
                {
                    old=new GuidedCompletionRecord{id=session.Definition.Id,bestSeconds=session.CompletedSeconds};
                    var records=new List<GuidedCompletionRecord>(candidate.records){old};candidate.records=records.ToArray();
                }
                if(old.attempts.Length>=MaxAttempts){LastError="Completion history limit reached; original progress preserved";return false;}
                old.attempts=old.attempts.Concat(new[]{session.AttemptId}).ToArray();old.completions=old.attempts.Length;
                old.lastAttemptId=session.AttemptId;old.bestSeconds=Math.Min(old.bestSeconds,session.CompletedSeconds);
                Array.Sort(candidate.records,(a,b)=>string.CompareOrdinal(a.id,b.id));Validate(candidate);
                if(!MemoryOnly)WriteAtomic(path,candidate);
                data=candidate;LastError="";return true;
            }
            catch(Exception error)when(error is IOException||error is ArgumentException||error is UnauthorizedAccessException||error is FormatException)
            {LastError="Completion could not be stored; existing progress preserved: "+error.GetType().Name;return false;}
        }
        static void Validate(GuidedAccountData saved)
        {
            if(saved==null||saved.schema!=1||saved.catalogRevision!=GuidedPlayCatalog.Revision||saved.records==null||saved.records.Length>GuidedPlayCatalog.Definitions.Count)throw new IOException("Unsupported account version or collection");
            var ids=new HashSet<string>();var attempts=new HashSet<string>();
            foreach(var record in saved.records)
            {
                if(record==null||GuidedPlayCatalog.Find(record.id)==null||!ids.Add(record.id)||record.attempts==null||record.attempts.Length==0||record.attempts.Length>MaxAttempts||record.completions!=record.attempts.Length||!SnapshotCodec.Finite(record.bestSeconds)||record.bestSeconds<0||record.bestSeconds>GuidedPlayCatalog.Find(record.id).TimeLimit||record.lastAttemptId!=record.attempts[record.attempts.Length-1])throw new IOException("Invalid or unknown completion record");
                foreach(string attempt in record.attempts)
                    if(string.IsNullOrEmpty(attempt)||attempt.Length>96||!attempt.All(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-')||!attempts.Add(attempt))throw new IOException("Invalid or repeated attempt ID");
            }
            foreach(var record in saved.records)
            {
                var previous=GuidedPlayCatalog.Find(record.id).UnlockAfterId;
                if(!string.IsNullOrEmpty(previous)&&!ids.Contains(previous))throw new IOException("Campaign completion lacks preceding chapter");
            }
        }
        static string ReadBounded(string file)
        {
            using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var bytes=new MemoryStream())
            {
                if(stream.Length>MaxBytes)throw new IOException("Account file exceeds limit");
                var buffer=new byte[4096];int read;while((read=stream.Read(buffer,0,buffer.Length))>0)
                {if(bytes.Length+read>MaxBytes)throw new IOException("Account file exceeds limit");bytes.Write(buffer,0,read);}
                return Utf8.GetString(bytes.ToArray());
            }
        }
        static void WriteAtomic(string file,GuidedAccountData saved)
        {
            string payload=JsonUtility.ToJson(saved);var bytes=Utf8.GetBytes(JsonUtility.ToJson(new GuidedAccountEnvelope{payload=payload,sha256=SnapshotCodec.Hash(payload)}));
            if(bytes.Length>MaxBytes)throw new IOException("Account history exceeds limit");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));string temporary=file+".tmp-"+Guid.NewGuid().ToString("N");
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
    // JsonUtility's permissive field defaults are useful in runtime snapshots,
    // but account files require a single complete JSON value, known fields and
    // unique keys before typed deserialization.
    static class GuidedProgressJson
    {
        public static void Validate(string text,string[] allowed,string[] rootRequired=null,string[] nestedRequired=null)
        {var parser=new Parser(text,new HashSet<string>(allowed),rootRequired,nestedRequired);parser.Value(0);parser.Space();if(parser.Position!=text.Length)throw new FormatException("Trailing account JSON");}
        sealed class Parser
        {
            readonly string text;readonly HashSet<string> allowed;readonly string[] rootRequired,nestedRequired;int cursor;
            public int Position=>cursor;
            public Parser(string value,HashSet<string> fields,string[] root,string[] nested){text=value??throw new FormatException("Missing JSON");allowed=fields;rootRequired=root;nestedRequired=nested;}
            public void Space(){while(cursor<text.Length&&(text[cursor]==' '||text[cursor]=='\n'||text[cursor]=='\r'||text[cursor]=='\t'))cursor++;}
            bool Take(char c){Space();if(cursor<text.Length&&text[cursor]==c){cursor++;return true;}return false;}
            void Need(char c){if(!Take(c))throw new FormatException("Invalid account JSON punctuation");}
            public void Value(int depth)
            {
                if(depth>20)throw new FormatException("Account JSON nesting");Space();if(cursor>=text.Length)throw new FormatException("Incomplete account JSON");
                if(Take('{'))
                {
                    var keys=new HashSet<string>();if(Take('}')){CheckKeys(keys,depth);return;}
                    do{string key=String();if(!keys.Add(key)||!allowed.Contains(key))throw new FormatException("Repeated or unknown account field");Need(':');Value(depth+1);}while(Take(','));Need('}');CheckKeys(keys,depth);return;
                }
                if(Take('[')){if(Take(']'))return;int count=0;do{if(++count>10000)throw new FormatException("Account JSON array limit");Value(depth+1);}while(Take(','));Need(']');return;}
                if(text[cursor]=='"'){String();return;}
                foreach(string literal in new[]{"true","false","null"})if(text.Substring(cursor).StartsWith(literal,StringComparison.Ordinal)){cursor+=literal.Length;return;}
                Number();
            }
            void CheckKeys(HashSet<string> keys,int depth)
            {var required=depth==0?rootRequired:nestedRequired;if(required!=null&&required.Any(key=>!keys.Contains(key)))throw new FormatException("Missing account field");}
            string String()
            {
                Need('"');var result=new StringBuilder();
                while(cursor<text.Length)
                {
                    char c=text[cursor++];if(c=='"')return result.ToString();if(c<' ')throw new FormatException("Account JSON control character");
                    if(c=='\\')
                    {
                        if(cursor>=text.Length)throw new FormatException("Account JSON escape");c=text[cursor++];
                        if(c=='u')
                        {
                            if(cursor+4>text.Length||!ushort.TryParse(text.Substring(cursor,4),System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out ushort code))throw new FormatException("Account JSON Unicode escape");cursor+=4;c=(char)code;
                        }
                        else if(c=='"'||c=='\\'||c=='/'){}else if(c=='b')c='\b';else if(c=='f')c='\f';else if(c=='n')c='\n';else if(c=='r')c='\r';else if(c=='t')c='\t';else throw new FormatException("Account JSON escape");
                    }
                    result.Append(c);
                }
                throw new FormatException("Unterminated account JSON string");
            }
            void Number()
            {
                int start=cursor;if(cursor<text.Length&&text[cursor]=='-')cursor++;
                if(cursor>=text.Length||!Digit(text[cursor]))throw new FormatException("Account JSON number");
                if(text[cursor]=='0')cursor++;else while(cursor<text.Length&&Digit(text[cursor]))cursor++;
                if(cursor<text.Length&&text[cursor]=='.'){cursor++;int first=cursor;while(cursor<text.Length&&Digit(text[cursor]))cursor++;if(first==cursor)throw new FormatException("Account JSON fraction");}
                if(cursor<text.Length&&(text[cursor]=='e'||text[cursor]=='E')){cursor++;if(cursor<text.Length&&(text[cursor]=='+'||text[cursor]=='-'))cursor++;int first=cursor;while(cursor<text.Length&&Digit(text[cursor]))cursor++;if(first==cursor)throw new FormatException("Account JSON exponent");}
                if(start==cursor)throw new FormatException("Account JSON value");
            }
            static bool Digit(char c)=>c>='0'&&c<='9';
        }
    }
}
