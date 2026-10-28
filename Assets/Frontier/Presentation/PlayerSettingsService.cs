using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using IOPath=System.IO.Path;

namespace Frontier
{
    // Preferences belong to the player, independently of session save files.
    public sealed class PlayerSettingsService
    {
        [Serializable] private sealed class StoredSettings {public int schemaVersion=1;public PlayerSettings settings;}
        public const int MaxFileBytes=64*1024;
        private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        private PlayerSettings current=PlayerSettings.Defaults();
        internal PlayerSettings Snapshot=>current;
        public PlayerSettings Current=>current.Clone();
        public int Revision {get;private set;}
        public event Action Changed;
        public string Path {get;private set;}="";
        public string LoadError {get;private set;}="";
        public string PreservedUnreadablePath {get;private set;}="";
        public bool Persistent=>Path.Length>0;
        public PlayerBindings Bindings {get;}
        public PlayerCaptions Captions {get;}
        private PlayerSettingsService(){Bindings=new PlayerBindings(this);Captions=new PlayerCaptions(this);}
        public static PlayerSettingsService Load(string directory=null)
        {
            var service=new PlayerSettingsService();
            if(directory==null&&(Application.isEditor||Application.isBatchMode))return service;
            try
            {
                string root=IOPath.GetFullPath(directory??IOPath.Combine(Application.persistentDataPath,"Preferences"));
                service.Path=IOPath.Combine(root,"player-settings.json");
                if(!File.Exists(service.Path))return service;
                byte[] bytes;
                using(var stream=new FileStream(service.Path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(stream.Length<=0||stream.Length>MaxFileBytes)throw new IOException("Settings file size is outside the allowed range");
                    bytes=new byte[(int)stream.Length];int used=0;
                    while(used<bytes.Length){int read=stream.Read(bytes,used,bytes.Length-used);if(read==0)throw new IOException("Settings file was truncated");used+=read;}
                    if(stream.ReadByte()!=-1)throw new IOException("Settings file changed while reading");
                }
                service.current=Read(Utf8.GetString(bytes));
            }
            catch(Exception error) when(error is IOException||error is UnauthorizedAccessException||error is ArgumentException||error is FormatException)
            {service.LoadError="Preferences could not be loaded: "+error.Message;service.current=PlayerSettings.Defaults();}
            return service;
        }
        public bool TryUpdate(PlayerSettings draft,out string error)
        {
            if(!Validate(draft,out error))return false;
            var next=draft.Clone();
            // Even a defaults-only Save explicitly repairs an unreadable file.
            if(string.IsNullOrEmpty(LoadError)&&JsonUtility.ToJson(next)==JsonUtility.ToJson(current))return true;
            try
            {
                if(Persistent)Write(next);
            }
            catch(Exception failure) when(failure is IOException||failure is UnauthorizedAccessException||failure is ArgumentException)
            {error="Preferences were not saved: "+failure.Message;return false;}
            current=next;LoadError="";Revision++;Changed?.Invoke();error="";return true;
        }
        public bool TryRebind(PlayerAction action,Key key,out string error)
        {
            if(action==PlayerAction.None||!Enum.IsDefined(typeof(PlayerAction),action)){error="Unknown player action";return false;}
            var draft=Current;foreach(var binding in draft.hotkeys)if(binding.action==action){binding.key=key;return TryUpdate(draft,out error);}
            error="Action has no binding";return false;
        }
        public string BindingLabel(PlayerAction action)
        {var key=Bindings.KeyFor(action);return key==Key.V&&action==PlayerAction.IdleWorker?"V / .":PlayerBindings.KeyLabel(key);}
        public static bool Validate(PlayerSettings value,out string error)
        {
            error="";
            if(value==null){error="Settings draft is missing";return false;}
            foreach(float volume in new[]{value.masterVolume,value.sfxVolume,value.musicVolume,value.environmentVolume})
                if(float.IsNaN(volume)||float.IsInfinity(volume)||volume<0||volume>1){error="Volumes must be between 0 and 1";return false;}
            if(float.IsNaN(value.uiScale)||float.IsInfinity(value.uiScale)||value.uiScale<.85f||value.uiScale>1.3f){error="UI scale must be between 0.85 and 1.30";return false;}
            int count=Enum.GetValues(typeof(PlayerAction)).Length-1;
            if(value.hotkeys==null||value.hotkeys.Length!=count){error="Every player action needs one hotkey";return false;}
            var actions=new HashSet<PlayerAction>();var keys=new HashSet<Key>();
            foreach(var binding in value.hotkeys)
            {
                if(binding==null||binding.action==PlayerAction.None||!Enum.IsDefined(typeof(PlayerAction),binding.action)||!actions.Add(binding.action)){error="Hotkeys contain a missing, duplicate or unknown action";return false;}
                if(PlayerBindings.Reserved(binding.key)){error="This key is reserved for UI, capture, modifiers or control groups";return false;}
                if(!keys.Add(binding.key)){error="The key is already assigned to another action";return false;}
            }
            return true;
        }
        private void Write(PlayerSettings settings)
        {
            byte[] bytes=Utf8.GetBytes(JsonUtility.ToJson(new StoredSettings{settings=settings},true));
            if(bytes.Length>MaxFileBytes)throw new IOException("Settings file is too large");
            string directory=IOPath.GetDirectoryName(Path);Directory.CreateDirectory(directory);
            string temporary=Path+".tmp-"+Guid.NewGuid().ToString("N");
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(Path))
                {
                    if(LoadError.Length>0)
                    {
                        // Save is explicit, but never destroy the unreadable input.
                        // A failed copy prevents replacement of the original.
                        string backup=Path+".unreadable-"+Guid.NewGuid().ToString("N");File.Copy(Path,backup,false);PreservedUnreadablePath=backup;
                    }
                    File.Replace(temporary,Path,null);
                }
                else File.Move(temporary,Path);
            }
            finally {if(File.Exists(temporary))File.Delete(temporary);}
        }
        private static PlayerSettings Read(string json)
        {
            var root=SettingsJson.Parse(json) as Dictionary<string,object>;
            if(root==null||Number(root,"schemaVersion")!=1)throw new FormatException("Unsupported preferences version");
            if(!root.TryGetValue("settings",out var raw)||!(raw is Dictionary<string,object> fields))throw new FormatException("Settings object is missing");
            var value=new PlayerSettings{masterVolume=(float)Number(fields,"masterVolume"),sfxVolume=(float)Number(fields,"sfxVolume"),musicVolume=(float)Number(fields,"musicVolume"),environmentVolume=(float)Number(fields,"environmentVolume"),uiScale=(float)Number(fields,"uiScale"),muted=Boolean(fields,"muted"),musicEnabled=Boolean(fields,"musicEnabled"),environmentEnabled=Boolean(fields,"environmentEnabled"),friendEnemyPatterns=Boolean(fields,"friendEnemyPatterns"),highContrastMinimap=Boolean(fields,"highContrastMinimap"),subtitles=Boolean(fields,"subtitles"),eventHints=Boolean(fields,"eventHints")};
            if(!fields.TryGetValue("hotkeys",out var keys)||!(keys is List<object> array)||array.Count!=Enum.GetValues(typeof(PlayerAction)).Length-1)throw new FormatException("Hotkey list is invalid");
            value.hotkeys=new HotkeyBinding[array.Count];
            for(int i=0;i<array.Count;i++)
            {
                if(!(array[i] is Dictionary<string,object> binding))throw new FormatException("Hotkey entry is invalid");
                double action=Number(binding,"action"),key=Number(binding,"key");
                if(action!=Math.Truncate(action)||key!=Math.Truncate(key)||action<0||action>int.MaxValue||key<0||key>int.MaxValue)throw new FormatException("Hotkey numbers are invalid");
                value.hotkeys[i]=new HotkeyBinding((PlayerAction)(int)action,(Key)(int)key);
            }
            if(!Validate(value,out string reason))throw new FormatException(reason);return value;
        }
        private static double Number(Dictionary<string,object> fields,string name)
        {if(!fields.TryGetValue(name,out var raw)||!(raw is double value)||double.IsNaN(value)||double.IsInfinity(value))throw new FormatException("Invalid numeric setting: "+name);return value;}
        private static bool Boolean(Dictionary<string,object> fields,string name)
        {if(!fields.TryGetValue(name,out var raw)||!(raw is bool value))throw new FormatException("Invalid boolean setting: "+name);return value;}
    }
}
