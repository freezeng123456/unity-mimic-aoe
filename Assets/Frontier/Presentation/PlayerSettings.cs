using System;
using UnityEngine.InputSystem;

namespace Frontier
{
    public enum PlayerAction
    {
        None, AttackMove, Stop, Hold, Garrison, Repair, HomeCity, BuildMenu,
        IdleWorker, TownCenter, Army, FocusSelection, Embark, Disembark, DockRepair,
        CameraLeft, CameraRight, CameraUp, CameraDown, ZoomIn, ZoomOut
    }
    [Serializable] public sealed class HotkeyBinding
    {
        public PlayerAction action; public Key key;
        public HotkeyBinding() {}
        public HotkeyBinding(PlayerAction action,Key key){this.action=action;this.key=key;}
    }
    [Serializable] public sealed class PlayerSettings
    {
        public float masterVolume=1,sfxVolume=1,musicVolume=1,environmentVolume=1,uiScale=1;
        public bool muted,musicEnabled=true,environmentEnabled=true;
        public bool friendEnemyPatterns,highContrastMinimap,subtitles=true,eventHints=true;
        public HotkeyBinding[] hotkeys;
        public static PlayerSettings Defaults()=>new PlayerSettings{hotkeys=new[]{
            new HotkeyBinding(PlayerAction.AttackMove,Key.A),new HotkeyBinding(PlayerAction.Stop,Key.S),
            new HotkeyBinding(PlayerAction.Hold,Key.J),new HotkeyBinding(PlayerAction.Garrison,Key.G),
            new HotkeyBinding(PlayerAction.Repair,Key.R),new HotkeyBinding(PlayerAction.HomeCity,Key.H),
            new HotkeyBinding(PlayerAction.BuildMenu,Key.B),new HotkeyBinding(PlayerAction.IdleWorker,Key.V),
            new HotkeyBinding(PlayerAction.TownCenter,Key.T),new HotkeyBinding(PlayerAction.Army,Key.M),
            new HotkeyBinding(PlayerAction.FocusSelection,Key.F),new HotkeyBinding(PlayerAction.Embark,Key.E),
            new HotkeyBinding(PlayerAction.Disembark,Key.U),new HotkeyBinding(PlayerAction.DockRepair,Key.K),
            new HotkeyBinding(PlayerAction.CameraLeft,Key.LeftArrow),new HotkeyBinding(PlayerAction.CameraRight,Key.RightArrow),
            new HotkeyBinding(PlayerAction.CameraUp,Key.UpArrow),new HotkeyBinding(PlayerAction.CameraDown,Key.DownArrow),
            new HotkeyBinding(PlayerAction.ZoomIn,Key.Equals),new HotkeyBinding(PlayerAction.ZoomOut,Key.Minus)}};
        public PlayerSettings Clone()
        {
            var copy=(PlayerSettings)MemberwiseClone();
            if(hotkeys!=null){copy.hotkeys=new HotkeyBinding[hotkeys.Length];for(int i=0;i<hotkeys.Length;i++)if(hotkeys[i]!=null)copy.hotkeys[i]=new HotkeyBinding(hotkeys[i].action,hotkeys[i].key);}
            return copy;
        }
    }
    public sealed class PlayerBindings
    {
        private readonly PlayerSettingsService service;
        internal PlayerBindings(PlayerSettingsService service){this.service=service;}
        public PlayerAction Resolve(Key key)
        {
            foreach(var binding in service.Snapshot.hotkeys)if(binding.key==key)return binding.action;
            // Preserve the original alternate idle-worker key only while its
            // default mapping is active; a rebind must retire both old keys.
            if(key==Key.Period&&KeyFor(PlayerAction.IdleWorker)==Key.V)return PlayerAction.IdleWorker;
            return PlayerAction.None;
        }
        public Key KeyFor(PlayerAction action)
        {foreach(var binding in service.Snapshot.hotkeys)if(binding.action==action)return binding.key;return Key.None;}
        public bool IsPressed(Keyboard keyboard,PlayerAction action)
        {var key=KeyFor(action);return keyboard!=null&&key!=Key.None&&keyboard[key].isPressed;}
        public static bool Reserved(Key key)
        {
            return !Enum.IsDefined(typeof(Key),key)||key.ToString()=="IMESelected"||key==Key.None||key==Key.Escape||key==Key.F8||key==Key.F9||key==Key.F10||key==Key.Period
                ||key==Key.Tab||key==Key.Enter||key==Key.NumpadEnter||key==Key.Space
                ||key>=Key.Digit1&&key<=Key.Digit0||key==Key.LeftShift||key==Key.RightShift
                ||key==Key.LeftCtrl||key==Key.RightCtrl||key==Key.LeftAlt||key==Key.RightAlt
                ||key==Key.LeftMeta||key==Key.RightMeta||key==Key.ContextMenu;
        }
        public static string KeyLabel(Key key)
        {
            switch(key){case Key.LeftArrow:return "Left";case Key.RightArrow:return "Right";case Key.UpArrow:return "Up";case Key.DownArrow:return "Down";case Key.Equals:return "+";case Key.Minus:return "-";default:return key.ToString();}
        }
        public static string ActionLabel(PlayerAction action)
        {
            switch(action){case PlayerAction.AttackMove:return "Attack move";case PlayerAction.HomeCity:return "Home city";case PlayerAction.BuildMenu:return "Build menu";case PlayerAction.IdleWorker:return "Idle worker";case PlayerAction.TownCenter:return "Town center";case PlayerAction.FocusSelection:return "Focus selection";case PlayerAction.DockRepair:return "Harbor repair";case PlayerAction.CameraLeft:return "Camera left";case PlayerAction.CameraRight:return "Camera right";case PlayerAction.CameraUp:return "Camera up";case PlayerAction.CameraDown:return "Camera down";case PlayerAction.ZoomIn:return "Zoom in";case PlayerAction.ZoomOut:return "Zoom out";default:return action.ToString();}
        }
    }
}
