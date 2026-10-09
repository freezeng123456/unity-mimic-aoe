using System;
using System.Collections.Generic;

namespace Frontier
{
    public partial class FrontierHud
    {
        private string HealingSummary076(EntityState healer)
        {
            var w=game.World;
            string text="Heal "+w.HealRate(healer).ToString("0.#")+" HP/s · Range "+w.HealRange(healer).ToString("0.#")+" · Auto "+(healer.autoHeal?"ON":"OFF");
            var target=w.Get(healer.healingTarget);
            if(target==null)return text+"\n"+(healer.healingBlockedUntil>w.Time?"Combat recovery · "+(healer.healingBlockedUntil-w.Time).ToString("0.0")+"s":"Ready for a stationary wounded ally");
            // Own treatment is inspectable; a hidden ally never becomes a HUD
            // locator just because the support actor remains visible.
            if(!w.IsVisible(game.ObserverPlayer,target))return text+"\nPatient not currently visible";
            string reason=w.HealingPauseReason(healer,target);
            return text+"\nPatient: "+target.def.name+" #"+target.id+" · "+(reason==""?"Treating":reason);
        }
        private void AddHealingActions076(EntityState first,Action<string,Action,string,bool> add)
        {
            var actors=new List<int>();bool anyOn=false,anyOff=false;
            foreach(int id in game.Controls.Selected)
            {
                var actor=game.World.Get(id);
                if(actor==null||actor.owner!=game.LocalPlayerSlot||!game.World.CanHeal(actor)||actors.Contains(id))continue;
                actors.Add(id);anyOn|=actor.autoHeal;anyOff|=!actor.autoHeal;
            }
            if(actors.Count>0)
            {
                add(game.Controls.HealMode?"CANCEL HEAL":"HEAL",()=>game.Controls.BeginHeal(),
                    "Choose a wounded visible friendly or allied land unit. Shift queues patients. Treatment waits for combat recovery, stationary actors and a clear in-range path. One doctor treats each patient; no resource cost per HP.",true);
                add("AUTO HEAL: "+(anyOn&&anyOff?"MIXED":anyOn?"ON":"OFF"),()=>{
                    SubmitUiCommand(new GameCommand{player=game.LocalPlayerSlot,kind=CommandKind.ToggleAutoHeal,actors=actors.ToArray()});InvalidateActions();
                },"Toggle idle / hold-position treatment for each selected medical unit. Mixed selections flip each setting. Does not interrupt travel or explicit treatment. Stop cancels treatment and turns automatic care off.",true);
            }
            if(first.Healer)add("MEDICAL UNITS",()=>game.Controls.SelectHealers(),"Select your visible, unboarded medical units. Military selection keeps doctors in a separate support role.",true);
        }
    }
}
