using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed class EventAudio : MonoBehaviour
    {
        public enum SoundCategory { Gun,Cannon,Melee,Impact,Notice,Footstep,Hoof,Wheel,Woodcut,Mine,Handling,Build }
        private sealed class Channel { public AudioSource source;public bool notice,activity;public float last=-999,until;public int actor; }
        private GameController game;
        private readonly List<Channel> channels=new List<Channel>();
        private readonly Dictionary<SoundCategory,float> lastByCategory=new Dictionary<SoundCategory,float>();
        private readonly Dictionary<SoundCategory,AudioClip> clips=new Dictionary<SoundCategory,AudioClip>();
        private readonly Dictionary<NoticeCue,AudioClip> notices=new Dictionary<NoticeCue,AudioClip>();
        private readonly UnitActivityAudio activity=new UnitActivityAudio();
        private float lastActivity=-999;
        public int PlayedCount { get; private set; }
        public int ActivityPlayedCount { get; private set; }
        public SoundCategory LastCategory { get; private set; }
        public NoticeCue LastNotice { get; private set; }
        public int SourceCount=>channels.Count;
        public void Initialize(GameController controller)
        {
            DisposeAudio();game=controller;
            for(int i=0;i<12;i++)
            {
                var child=new GameObject("Event audio "+i);child.transform.SetParent(transform,false);
                var source=child.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.mute=game.Muted;
                channels.Add(new Channel{source=source,notice=i>=10,activity=i>=8&&i<10});
            }
            clips[SoundCategory.Gun]=Synthesize("Musket report",180,.12f,.7f);
            clips[SoundCategory.Cannon]=Synthesize("Cannon report",70,.3f,.8f);
            clips[SoundCategory.Melee]=Synthesize("Melee contact",320,.075f,.3f);
            clips[SoundCategory.Impact]=Synthesize("Impact",110,.09f,.5f);
            foreach(NoticeCue cue in System.Enum.GetValues(typeof(NoticeCue)))if(cue!=NoticeCue.None)notices[cue]=NoticeSoundClips.Create(cue);
            foreach(var category in new[]{SoundCategory.Footstep,SoundCategory.Hoof,SoundCategory.Wheel,SoundCategory.Woodcut,SoundCategory.Mine,SoundCategory.Handling,SoundCategory.Build})
                clips[category]=ActivitySoundClips.Create(category);
        }
        public void OnEvent(SimEvent ev)
        {
            if(game==null||ev==null||game.Muted||game.World==null||game.World.Paused)return;
            SoundCategory category;
            NoticeCue noticeCue=NoticeCue.None;
            if(ev.kind=="Fire")
            {
                var source=game.World.Get(ev.entity);
                category=ev.message=="Melee"?SoundCategory.Melee:source!=null&&(source.def.tags&(Tags.Artillery|Tags.Naval))!=0?SoundCategory.Cannon:SoundCategory.Gun;
            }
            else if(ev.kind=="Impact")category=SoundCategory.Impact;
            else if(NoticeSoundClips.TryClassify(ev.kind,out noticeCue))category=SoundCategory.Notice;
            else return;
            bool notice=category==SoundCategory.Notice;
            if(notice){if(ev.player!=0)return;}
            else
            {
                if(!CombatFeedback.CanDisplay(game.World,0,ev)||game.World.Time-ev.time>.4f)return;
                var camera=game.GameCamera.transform.position;
                if(Vector2.Distance(new Vector2(camera.x,camera.z),ev.position)>65)return;
            }
            float now=Time.unscaledTime;
            float interval=category==SoundCategory.Impact?.035f:notice?.08f:.06f;
            if(lastByCategory.TryGetValue(category,out float last)&&now-last<interval)return;
            Channel chosen=null;
            foreach(var channel in channels)
            {
                if(channel.activity||channel.notice!=notice)continue;
                if(!channel.source.isPlaying){chosen=channel;break;}
                if((notice||category==SoundCategory.Cannon)&&(chosen==null||channel.last<chosen.last))chosen=channel;
            }
            if(chosen==null)return;
            // PlayOneShot can overlap multiple voices on a single source. Stop the
            // replaced voice first so twelve sources also means twelve voice slots.
            chosen.source.Stop();chosen.last=now;chosen.source.volume=notice?.3f:.22f;
            chosen.source.transform.position=new Vector3(ev.position.x,0,ev.position.y);
            chosen.source.PlayOneShot(notice?notices[noticeCue]:clips[category]);lastByCategory[category]=now;
            LastCategory=category;LastNotice=noticeCue;PlayedCount++;
        }
        public void TickActivity()
        {
            if(game==null)return;
            // A short active cue is silenced too when its current unit leaves
            // visibility or the viewport; a later reveal cannot resume it.
            foreach(var channel in channels)if(channel.activity&&channel.actor!=0)
            {
                if(game.World==null||game.Muted||game.World.Paused||game.World.Winner>=0||!UnitActivityAudio.OnScreen(game,game.World.Get(channel.actor),out _))
                {channel.source.Stop();channel.actor=0;channel.until=0;}
            }
            activity.Tick(game,this);
        }
        public bool TryPlayActivity(EntityState actor,SoundCategory category)
        {
            if(game==null||game.World==null||game.Muted||game.World.Paused||game.World.Winner>=0||category<SoundCategory.Footstep
                ||!UnitActivityAudio.OnScreen(game,actor,out var screen))return false;
            float now=Time.unscaledTime;if(now-lastActivity<.085f)return false;
            Channel chosen=null;foreach(var channel in channels)if(channel.activity&&now>=channel.until){chosen=channel;break;}
            if(chosen==null)return false;
            var clip=clips[category];chosen.source.Stop();chosen.source.clip=clip;chosen.source.volume=.1f;
            chosen.source.panStereo=Mathf.Clamp((screen.x-.5f)*1.5f,-.75f,.75f);
            chosen.source.transform.position=new Vector3(actor.position.x,0,actor.position.y);chosen.source.Play();
            chosen.actor=actor.id;chosen.last=now;chosen.until=now+clip.length;lastActivity=now;
            LastCategory=category;LastNotice=NoticeCue.None;PlayedCount++;ActivityPlayedCount++;
            return true;
        }
        public void SetMuted(bool muted)
        {
            foreach(var channel in channels)
            {channel.source.Stop();channel.source.mute=muted;channel.actor=0;channel.until=0;}
            activity.Reset(game?.World);lastActivity=-999;lastByCategory.Clear();
            game?.Ambience?.SetMuted(muted);
        }
        public void ResetMatch()
        {
            foreach(var channel in channels)if(channel.source!=null){channel.source.Stop();channel.last=-999;channel.actor=0;channel.until=0;}
            lastByCategory.Clear();activity.Reset(game?.World);lastActivity=-999;PlayedCount=0;ActivityPlayedCount=0;LastNotice=NoticeCue.None;
            game?.Ambience?.ResetMatch();
        }
        public void PauseAll(bool paused)
        {
            activity.Reset(game?.World);
            foreach(var channel in channels)if(paused)channel.source.Pause();else if(game!=null&&game.Muted)channel.source.Stop();else channel.source.UnPause();
            game?.Ambience?.SetPaused(paused);
        }
        private AudioClip Synthesize(string name,float frequency,float seconds,float noiseMix)
        {
            const int rate=22050;int count=Mathf.CeilToInt(rate*seconds);var samples=new float[count];
            var random=new System.Random(731);float tail=0;
            for(int i=0;i<count;i++)
            {
                float envelope=Mathf.Exp(-7f*i/count);float noise=(float)random.NextDouble()*2-1;
                tail=Mathf.Lerp(tail,noise,.4f);
                samples[i]=(Mathf.Sin(2*Mathf.PI*frequency*i/rate)*(1-noiseMix)+tail*noiseMix)*envelope*.6f;
            }
            var clip=AudioClip.Create(name,count,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void DisposeAudio()
        {
            ResetMatch();foreach(var channel in channels)if(channel.source!=null)Destroy(channel.source.gameObject);channels.Clear();
            foreach(var clip in clips.Values)if(clip!=null)Destroy(clip);clips.Clear();
            foreach(var clip in notices.Values)if(clip!=null)Destroy(clip);notices.Clear();game=null;
        }
        private void OnDestroy(){DisposeAudio();}
    }
}
