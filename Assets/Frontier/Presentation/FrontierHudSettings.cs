using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Frontier
{
    public sealed partial class FrontierHud
    {
        PlayerSettings settingsDraft;
        PlayerAction waitingBinding=PlayerAction.None;
        Text bindingPrompt;
        int bindingPage;
        public void ShowSettings(){settingsDraft=game.Settings.Current;RenderSettings(0);}
        void RenderSettings(int tab,int page=0)
        {
            var box=Modal("PLAYER SETTINGS",720);bindingPage=page;
            Button(box,"AUDIO",new Rect(25,78,258,46),()=>RenderSettings(0));
            Button(box,"ACCESSIBILITY",new Rect(300,78,258,46),()=>RenderSettings(1));
            Button(box,"HOTKEYS",new Rect(575,78,265,46),()=>RenderSettings(2));
            if(tab==0)
            {
                SettingsSlider(box,"Master volume",150,settingsDraft.masterVolume,v=>settingsDraft.masterVolume=v);
                SettingsSlider(box,"Effects & alerts",232,settingsDraft.sfxVolume,v=>settingsDraft.sfxVolume=v);
                SettingsSlider(box,"Music",314,settingsDraft.musicVolume,v=>settingsDraft.musicVolume=v);
                SettingsSlider(box,"Environment",396,settingsDraft.environmentVolume,v=>settingsDraft.environmentVolume=v);
                SettingsToggle(box,"Mute all",new Rect(25,500,255,50),()=>settingsDraft.muted,v=>settingsDraft.muted=v);
                SettingsToggle(box,"Music enabled",new Rect(300,500,255,50),()=>settingsDraft.musicEnabled,v=>settingsDraft.musicEnabled=v);
                SettingsToggle(box,"Environment enabled",new Rect(575,500,265,50),()=>settingsDraft.environmentEnabled,v=>settingsDraft.environmentEnabled=v);
                Label(box,"Effects, environment and music use separate gain controls. This build has synthetic sound effects and no recorded unit voices.",new Rect(25,568,815,55),17,muted);
            }
            else if(tab==1)
            {
                SettingsSlider(box,"UI scale",150,settingsDraft.uiScale,v=>settingsDraft.uiScale=v,.85f,1.3f);
                SettingsToggle(box,"Friend / foe markers",new Rect(25,248,815,56),()=>settingsDraft.friendEnemyPatterns,v=>settingsDraft.friendEnemyPatterns=v);
                SettingsToggle(box,"High contrast minimap",new Rect(25,324,815,56),()=>settingsDraft.highContrastMinimap,v=>settingsDraft.highContrastMinimap=v);
                SettingsToggle(box,"Event captions",new Rect(25,400,815,56),()=>settingsDraft.subtitles,v=>settingsDraft.subtitles=v);
                SettingsToggle(box,"Action hints",new Rect(25,476,815,56),()=>settingsDraft.eventHints,v=>settingsDraft.eventHints=v);
                Label(box,"Markers add distinct player shapes and friend / foe cues. Captions describe audible game events, including when sound is muted.",new Rect(25,557,815,65),17,muted);
            }
            else
            {
                bindingPrompt=Label(box,"Click an action, then press a key. A successful rebind is saved immediately. Escape, F8–F10, digits and modifiers stay reserved.",new Rect(25,140,815,72),18,muted);
                var actions=(PlayerAction[])Enum.GetValues(typeof(PlayerAction));int total=actions.Length-1,pages=Mathf.CeilToInt(total/8f);page=Mathf.Clamp(page,0,pages-1);bindingPage=page;
                for(int i=0;i<8&&page*8+i<total;i++)
                {
                    var action=actions[1+page*8+i];
                    Button(box,PlayerBindings.ActionLabel(action)+"  ["+game.Settings.BindingLabel(action)+"]",new Rect(25+(i%2)*414,229+(i/2)*75,400,62),()=>{waitingBinding=action;bindingPrompt.text="Press a new key for "+PlayerBindings.ActionLabel(action)+". Conflicting and reserved keys are rejected.";});
                }
                Button(box,"PREVIOUS",new Rect(25,550,250,50),()=>RenderSettings(2,page-1),"",page>0);
                Button(box,"NEXT "+(page+1)+"/"+pages,new Rect(590,550,250,50),()=>RenderSettings(2,page+1),"",page+1<pages);
            }
            Button(box,"SAVE SETTINGS",new Rect(25,650,397,48),()=>
            {
                if(game.Settings.TryUpdate(settingsDraft,out string reason)){CloseModal();if(game.InMenu)ShowMenu();else ShowPause(true);game.Notify("Player settings saved");}
                else game.Notify("Settings not saved: "+reason);
            });
            Button(box,"BACK",new Rect(443,650,397,48),()=>{CloseModal();if(game.InMenu)ShowMenu();else ShowPause(true);});
        }
        void SettingsSlider(Transform parent,string name,float y,float value,Action<float> changed,float min=0,float max=1)
        {
            var text=Label(parent,name+" · "+value.ToString("0.00"),new Rect(25,y,815,32),20,gold);
            var track=Panel(parent,name+" slider",new Rect(25,y+39,815,20),new Color(.23f,.30f,.31f));
            var handle=Panel(track,"Handle",new Rect(0,0,22,28),gold);var slider=track.gameObject.AddComponent<Slider>();
            slider.handleRect=handle;slider.direction=Slider.Direction.LeftToRight;slider.minValue=min;slider.maxValue=max;slider.value=value;
            slider.onValueChanged.AddListener(v=>{changed(v);text.text=name+" · "+v.ToString("0.00");});
        }
        void SettingsToggle(Transform parent,string name,Rect rect,Func<bool> value,Action<bool> changed)
        {
            Button button=null;button=Button(parent,name+": "+(value()?"ON":"OFF"),rect,()=>{changed(!value());button.GetComponentInChildren<Text>().text=name+": "+(value()?"ON":"OFF");});
        }
        void CaptureBinding()
        {
            if(waitingBinding==PlayerAction.None||!ModalVisible||Keyboard.current==null)return;
            foreach(var key in Keyboard.current.allKeys)
            {
                if(!key.wasPressedThisFrame)continue;
                var action=waitingBinding;waitingBinding=PlayerAction.None;
                if(game.Settings.TryRebind(action,key.keyCode,out string reason))
                {settingsDraft.hotkeys=game.Settings.Current.hotkeys;RenderSettings(2,bindingPage);}
                else bindingPrompt.text=reason+" Click the action to try again.";
                break;
            }
        }
        static readonly PlayerAction[] hintActions=(PlayerAction[])Enum.GetValues(typeof(PlayerAction));
        static readonly string[] hintMarkers={"[A]","[S]","[J]","[G]","[R]","[H]","[B]","[V]","[T]","[M]","[F]"};
        string lastHintInput,lastHintOutput;int lastHintRevision=-1;
        string DynamicBindingLabel(string text)
        {
            if(lastHintRevision==game.Settings.Revision&&lastHintInput==text)return lastHintOutput;
            lastHintInput=text;lastHintRevision=game.Settings.Revision;
            var actions=hintActions;
            var old=hintMarkers;
            for(int i=0;i<old.Length;i++)if(text.Contains(old[i]))text=text.Replace(old[i],"["+game.Settings.BindingLabel(actions[i+1])+"]");
            lastHintOutput=text;return text;
        }
    }
}
