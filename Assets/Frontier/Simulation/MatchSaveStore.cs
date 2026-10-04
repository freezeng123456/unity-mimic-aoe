using System;
using System.IO;
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
    {public int schema=1;public string payload,sha256;}
    public static class MatchSaveStore
    {
        public const long MaxBytes=64*1024*1024;
        public static string SlotPath(string directory,int slot)
        {if(slot<1||slot>3)throw new ArgumentOutOfRangeException(nameof(slot));return Path.Combine(directory,"slot-"+slot+".json");}
        public static void Write(string path,SavedMatch save)
        {
            if(save?.world==null||save.simulation==null)throw new ArgumentException("No match to save");
            string payload=JsonUtility.ToJson(save),json=JsonUtility.ToJson(new SaveEnvelope{payload=payload,sha256=SnapshotCodec.Hash(payload)});
            byte[] bytes=Encoding.UTF8.GetBytes(json);if(bytes.LongLength>MaxBytes)throw new IOException("This match exceeds the save size limit");
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
            var info=new FileInfo(path);if(!info.Exists)throw new FileNotFoundException("Empty save slot");if(info.Length>MaxBytes)throw new IOException("Save file exceeds the size limit");
            var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path,Encoding.UTF8));
            if(envelope==null||envelope.schema!=1||string.IsNullOrEmpty(envelope.payload)||envelope.sha256!=SnapshotCodec.Hash(envelope.payload))throw new IOException("Save is incomplete or damaged");
            var saved=JsonUtility.FromJson<SavedMatch>(envelope.payload);
            if(saved?.world==null||saved.simulation==null||string.IsNullOrEmpty(saved.matchId))throw new IOException("Save has no match state");
            if(expectedVersion!=null&&(string.IsNullOrEmpty(saved.appVersion)||saved.appVersion!=expectedVersion))throw new IOException("Save requires game version "+(saved.appVersion??"unknown")+"; current version is "+expectedVersion);
            if(!SnapshotCodec.Finite(saved.cameraSize)||saved.cameraSize<8||saved.cameraSize>100||!SnapshotCodec.Finite(saved.cameraPosition.x)||!SnapshotCodec.Finite(saved.cameraPosition.y)||!SnapshotCodec.Finite(saved.cameraPosition.z))throw new IOException("Save has invalid camera settings");
            if(!SnapshotCodec.Finite(saved.cameraFocus)||!SnapshotCodec.Finite(saved.simulationSpeed)||saved.simulationSpeed<1||saved.simulationSpeed>16)throw new IOException("Save has invalid session settings");
            return saved;
        }
    }
}
