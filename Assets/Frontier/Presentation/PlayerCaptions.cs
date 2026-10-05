using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed class PlayerCaptions
    {
        private sealed class Line {public string text;public bool hint;public float expires;}
        private readonly PlayerSettingsService settings;
        private readonly List<Line> lines=new List<Line>();
        private PlayerSettings options;
        private string lastCue="";private float lastCueAt=-999;
        public string Text {get;private set;}="";
        public int Revision {get;private set;}
        public event Action Changed;
        internal PlayerCaptions(PlayerSettingsService settings)
        {this.settings=settings;options=settings.Current;settings.Changed+=ApplySettings;}
        private void ApplySettings()
        {options=settings.Current;lines.RemoveAll(line=>line.hint?!options.eventHints:!options.subtitles);Publish();}
        public void OnEvent(World world,int viewer,SimEvent ev)
        {
            if(!options.subtitles||world==null||ev==null||world.Paused||viewer<0||viewer>=world.Players.Length)return;
            string text=null;
            if(NoticeSoundClips.TryClassify(ev.kind,out var cue))
            {
                if(ev.player!=viewer)return;
                switch(cue){case NoticeCue.Shipment:text="[Shipment arrived]";break;case NoticeCue.AgeAdvanced:text="[Age advanced]";break;case NoticeCue.PopulationShortage:text="[Population limit: build housing]";break;case NoticeCue.ResourceDepleted:text="[Resource depleted]";break;case NoticeCue.BuildingComplete:text="[Building completed]";break;case NoticeCue.Treasure:text="[Treasure collected]";break;}
            }
            else if((ev.kind=="Fire"||ev.kind=="Impact")&&CombatFeedback.CanDisplay(world,viewer,ev)&&world.Time>=ev.time&&world.Time-ev.time<=.4f)
            {
                if(ev.kind=="Impact")text="[Impact]";
                else if(ev.message=="Melee")text="[Melee contact]";
                else
                {
                    var actor=world.Get(ev.entity);bool visibleActor=actor!=null&&world.IsVisible(viewer,actor);
                    text=visibleActor&&(actor.def.tags&(Tags.Artillery|Tags.Naval))!=0?"[Cannon fire]":"[Gunfire]";
                }
            }
            if(text==null)return;float now=Time.unscaledTime;
            if(text==lastCue&&now-lastCueAt<.8f)return;lastCue=text;lastCueAt=now;Add(text,false,now);
        }
        public void Hint(string text)
        {
            if(!options.eventHints||string.IsNullOrWhiteSpace(text))return;
            text=text.Replace('<','(').Replace('>',')').Replace('\r',' ').Replace('\n',' ');
            if(text.Length>160)text=text.Substring(0,160);
            text="[Hint] "+text;
            if(lines.Count>0&&lines[lines.Count-1].text==text)return;
            Add(text,true,Time.unscaledTime);
        }
        private void Add(string text,bool hint,float now)
        {Tick(now);if(lines.Count==3)lines.RemoveAt(0);lines.Add(new Line{text=text,hint=hint,expires=now+4});Publish();}
        public void Tick(float unscaledTime)
        {if(lines.RemoveAll(line=>line.expires<=unscaledTime)>0)Publish();}
        public void Clear(){lines.Clear();lastCue="";lastCueAt=-999;Publish();}
        private void Publish()
        {string next=string.Join("\n",lines.ConvertAll(line=>line.text));if(next==Text)return;Text=next;Revision++;Changed?.Invoke();}
    }
}
