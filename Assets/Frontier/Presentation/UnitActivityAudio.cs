using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Samples the latest actual simulation step. No orders create sound by
    // themselves, and omitted or offscreen steps never become a playback queue.
    public sealed class UnitActivityAudio
    {
        private sealed class Rhythm { public float distance,nextWork; }
        private readonly Dictionary<int,Rhythm> rhythms=new Dictionary<int,Rhythm>();
        private World observedWorld;
        private float observedAt=-1;
        public void Reset(World world=null)
        {rhythms.Clear();observedWorld=world;observedAt=world?.Time??-1;}

        public static bool OnScreen(GameController game,EntityState entity,out Vector3 screen)
        {
            screen=default;
            if(entity==null||!entity.Alive||entity.def.kind!=EntityKind.Unit||entity.garrisonedIn!=0||!game.World.IsVisible(0,entity))return false;
            screen=game.GameCamera.WorldToViewportPoint(new Vector3(entity.position.x,1,entity.position.y));
            return screen.z>0&&screen.x>=0&&screen.x<=1&&screen.y>=0&&screen.y<=1;
        }
        public void Tick(GameController game,EventAudio audio)
        {
            var world=game.World;
            if(world==null||game.InMenu||world.Paused||world.Winner>=0||audio.EffectiveSfxVolume<=0){Reset(world);return;}
            if(observedWorld!=world){Reset();observedWorld=world;}
            if(world.Time<=observedAt)return;observedAt=world.Time;
            EntityState chosen=null;Rhythm chosenRhythm=null;EventAudio.SoundCategory chosenCategory=default;
            bool chosenWork=false;float closest=float.MaxValue;
            foreach(var unit in world.Entities)
            {
                if(!OnScreen(game,unit,out var screen)){rhythms.Remove(unit.id);continue;}
                if(!rhythms.TryGetValue(unit.id,out var rhythm)){rhythm=new Rhythm();rhythms.Add(unit.id,rhythm);}
                float moved=Vector2.Distance(unit.position,unit.previousPosition);
                float stride=(unit.def.tags&Tags.Artillery)!=0?1.8f:(unit.def.tags&Tags.Cavalry)!=0?2.2f:1.4f;
                if(unit.status=="Moving"&&moved>.001f&&moved<=Mathf.Max(1,unit.def.speed*.3f))rhythm.distance=Mathf.Min(stride,rhythm.distance+moved);
                else rhythm.distance=0;
                bool work=unit.IsWorkingAt(world.Time);EventAudio.SoundCategory category;
                if(work)
                {
                    if(world.Time<rhythm.nextWork)continue;
                    var target=world.Get(unit.work.target);
                    category=unit.work.kind!=WorkKind.Gather?EventAudio.SoundCategory.Build:
                        target?.def.id=="tree"?EventAudio.SoundCategory.Woodcut:
                        target?.def.id=="mine"?EventAudio.SoundCategory.Mine:EventAudio.SoundCategory.Handling;
                }
                else
                {
                    if(rhythm.distance<stride)continue;
                    category=(unit.def.tags&Tags.Artillery)!=0?EventAudio.SoundCategory.Wheel:
                        (unit.def.tags&Tags.Cavalry)!=0?EventAudio.SoundCategory.Hoof:EventAudio.SoundCategory.Footstep;
                }
                float score=(screen.x-.5f)*(screen.x-.5f)+(screen.y-.5f)*(screen.y-.5f);
                if(game.Controls.Selected.Contains(unit.id))score-=1;
                if(score>=closest)continue;
                closest=score;chosen=unit;chosenRhythm=rhythm;chosenCategory=category;chosenWork=work;
            }
            if(chosen!=null&&audio.TryPlayActivity(chosen,chosenCategory))
            {chosenRhythm.distance=0;if(chosenWork)chosenRhythm.nextWork=world.Time+.8f;}
        }
    }
}
