using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed class EventAudio : MonoBehaviour
    {
        public enum SoundCategory { Gun,Cannon,Melee,Impact,Notice }
        private sealed class Channel { public AudioSource source;public bool notice;public float last=-999; }
        private GameController game;
        private readonly List<Channel> channels=new List<Channel>();
        private readonly Dictionary<SoundCategory,float> lastByCategory=new Dictionary<SoundCategory,float>();
        private readonly Dictionary<SoundCategory,AudioClip> clips=new Dictionary<SoundCategory,AudioClip>();
        public int PlayedCount { get; private set; }
        public SoundCategory LastCategory { get; private set; }
        public int SourceCount=>channels.Count;
        public void Initialize(GameController controller)
        {
            DisposeAudio();game=controller;
            for(int i=0;i<12;i++)
            {
                var child=new GameObject("Event audio "+i);child.transform.SetParent(transform,false);
                var source=child.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;
                channels.Add(new Channel{source=source,notice=i>=10});
            }
            clips[SoundCategory.Gun]=Synthesize("Musket report",180,.12f,.7f);
            clips[SoundCategory.Cannon]=Synthesize("Cannon report",70,.3f,.8f);
            clips[SoundCategory.Melee]=Synthesize("Melee contact",320,.075f,.3f);
            clips[SoundCategory.Impact]=Synthesize("Impact",110,.09f,.5f);
            clips[SoundCategory.Notice]=Synthesize("Notification",520,.16f,0);
        }
        public void OnEvent(SimEvent ev)
        {
            if(game==null||ev==null||game.Muted||game.World==null||game.World.Paused)return;
            SoundCategory category;
            if(ev.kind=="Fire")
            {
                var source=game.World.Get(ev.entity);
                category=ev.message=="Melee"?SoundCategory.Melee:source!=null&&(source.def.tags&Tags.Artillery)!=0?SoundCategory.Cannon:SoundCategory.Gun;
            }
            else if(ev.kind=="Impact")category=SoundCategory.Impact;
            else if(ev.kind=="ShipmentArrived"||ev.kind=="BuildingComplete"||ev.kind=="AgeAdvanced"||ev.kind=="PopulationShortage"||ev.kind=="ResourceDepleted"||ev.kind=="TreasureClaimed")category=SoundCategory.Notice;
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
                if(channel.notice!=notice)continue;
                if(!channel.source.isPlaying){chosen=channel;break;}
                if((notice||category==SoundCategory.Cannon)&&(chosen==null||channel.last<chosen.last))chosen=channel;
            }
            if(chosen==null)return;
            // PlayOneShot can overlap multiple voices on a single source. Stop the
            // replaced voice first so twelve sources also means twelve voice slots.
            chosen.source.Stop();chosen.last=now;chosen.source.volume=notice?.3f:.22f;
            chosen.source.transform.position=new Vector3(ev.position.x,0,ev.position.y);
            chosen.source.PlayOneShot(clips[category]);lastByCategory[category]=now;
            LastCategory=category;PlayedCount++;
        }
        public void ResetMatch()
        {
            foreach(var channel in channels)if(channel.source!=null){channel.source.Stop();channel.last=-999;}
            lastByCategory.Clear();PlayedCount=0;
        }
        public void PauseAll(bool paused)
        {foreach(var channel in channels)if(paused)channel.source.Pause();else channel.source.UnPause();}
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
            foreach(var clip in clips.Values)if(clip!=null)Destroy(clip);clips.Clear();game=null;
        }
        private void OnDestroy(){DisposeAudio();}
    }
}
