using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Frontier
{
    // Reads parse fully before returning a new document. Writes never delete
    // the last good file as a fallback when atomic replacement is unavailable.
    public sealed class ScenarioStore
    {
        public const int MaxLibraryFiles=128;
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        readonly RuleCatalog rules;
        public string DirectoryPath {get;}
        public ScenarioStore(string directory,RuleCatalog rules)
        {
            if(string.IsNullOrWhiteSpace(directory))throw new ArgumentException("Scenario directory is required",nameof(directory));
            this.rules=rules??throw new ArgumentNullException(nameof(rules));DirectoryPath=Path.GetFullPath(directory);
        }
        string PathFor(string id)
        {if(!ScenarioCodec.IsIdentifier(id))throw new ArgumentException("Scenario ID must contain 1–64 lowercase ASCII letters, digits, underscores or hyphens");return Path.Combine(DirectoryPath,id+".json");}
        public ScenarioFileInfo[] List()
        {
            if(!Directory.Exists(DirectoryPath))return Array.Empty<ScenarioFileInfo>();
            var files=new List<string>();foreach(string file in Directory.EnumerateFiles(DirectoryPath,"*.json",SearchOption.TopDirectoryOnly))
            {if(files.Count>=MaxLibraryFiles)throw new IOException("Scenario library exceeds "+MaxLibraryFiles+" files; existing files are preserved");files.Add(file);}
            files.Sort(StringComparer.Ordinal);var result=new List<ScenarioFileInfo>();
            foreach(string file in files)
            {
                string id=Path.GetFileNameWithoutExtension(file);var row=new ScenarioFileInfo{id=id,name=id,contentHash="",path=file,reason=""};
                try
                {
                    if(!ScenarioCodec.IsIdentifier(id))throw new InvalidDataException("Invalid library filename");
                    var document=ReadFile(file);if(document.id!=id)throw new InvalidDataException("Scenario ID does not match its library filename");
                    row.name=document.name;row.contentHash=ScenarioCodec.ContentHash(document);
                }
                catch(Exception error)when(error is IOException||error is InvalidDataException||error is ArgumentException||error is UnauthorizedAccessException)
                {row.reason=error.Message;}
                result.Add(row);
            }
            return result.ToArray();
        }
        public ScenarioDocument Read(string id)
        {
            var document=ReadFile(PathFor(id));if(document.id!=id)throw new InvalidDataException("Scenario ID does not match its library filename");return document;
        }
        public ScenarioDocument ReadFile(string path)
        {
            string full=Path.GetFullPath(path);byte[] bytes;
            using(var stream=new FileStream(full,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length>ScenarioCodec.MaxBytes)throw new InvalidDataException("Scenario file exceeds the 2 MiB limit");
                bytes=new byte[(int)stream.Length];int offset=0;
                while(offset<bytes.Length){int count=stream.Read(bytes,offset,bytes.Length-offset);if(count==0)throw new InvalidDataException("Scenario file changed or was truncated during read");offset+=count;}
                if(stream.ReadByte()!=-1)throw new InvalidDataException("Scenario file grew during read");
            }
            try{return ScenarioCodec.Decode(Utf8.GetString(bytes),rules);}
            catch(DecoderFallbackException error){throw new InvalidDataException("Scenario file must use valid UTF-8",error);}
        }
        public string Write(ScenarioDocument document)
        {
            if(document==null)throw new ArgumentNullException(nameof(document));
            // Prepare and validate bytes before any directory/file mutation.
            string path=PathFor(document.id);string json=ScenarioCodec.Encode(document,rules);
            if(!File.Exists(path)&&Directory.Exists(DirectoryPath))
            {
                int count=0;foreach(string ignored in Directory.EnumerateFiles(DirectoryPath,"*.json",SearchOption.TopDirectoryOnly))
                    if(++count>=MaxLibraryFiles)throw new IOException("Scenario library is full; existing files are preserved");
            }
            return AtomicWrite(path,json);
        }
        public ScenarioDocument Import(string json)=>ScenarioCodec.Decode(json,rules);
        public string Export(string id)=>ScenarioCodec.Encode(Read(id),rules);
        public string ExportFile(string path,ScenarioDocument document)
        {string json=ScenarioCodec.Encode(document,rules);return AtomicWrite(Path.GetFullPath(path),json);}
        static string AtomicWrite(string path,string json)
        {
            byte[] bytes=Utf8.GetBytes(json);string directory=Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);string temporary=path+".tmp-"+Guid.NewGuid().ToString("N");
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
                return path;
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
