using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class FrontierHud
    {
        private void AddScoutingActions075(EntityState first,Action<string,Action,string,bool> add)
        {
            var w=game.World;
            var scouts=new List<int>();var stealthUnits=new List<int>();EntityState stealthUnit=null;
            bool allScouting=true;
            foreach(int id in game.Controls.Selected)
            {
                var actor=w.Get(id);if(actor==null||actor.owner!=game.LocalPlayerSlot)continue;
                if(w.CanScout(actor)){scouts.Add(id);allScouting&=actor.orders.Count>0&&actor.orders.Peek().kind==OrderKind.Scout;}
                if(actor.def.canStealth){stealthUnits.Add(id);if(stealthUnit==null)stealthUnit=actor;}
            }
            if(scouts.Count>0)
                add(allScouting?"STOP SCOUTING":"AUTO SCOUT",()=>{
                    game.Controls.CancelTargetMode();
                    SubmitUiCommand(new GameCommand{player=game.LocalPlayerSlot,kind=CommandKind.Order,actors=scouts.ToArray(),order=allScouting?OrderKind.Stop:OrderKind.Scout,append=!allScouting&&game.Controls.Shift});
                    InvalidateActions();
                },"Explore reachable unknown terrain. Only visible threats guide retreats. Shift queues scouting; a later queued order takes over after the current exploration leg. Stop cancels the route.",true);
            if(stealthUnit!=null)
            {
                string reason=w.StealthReason(game.LocalPlayerSlot,stealthUnit);
                add(stealthUnit.stealth?"LEAVE STEALTH":"ENTER STEALTH",()=>{
                    SubmitUiCommand(new GameCommand{player=game.LocalPlayerSlot,kind=CommandKind.ToggleStealth,actors=stealthUnits.ToArray()});InvalidateActions();
                },"Native Scouts move at 60% speed in stealth. Scouts, Pathfinders, town centers, towers and nearby guardians can detect them. Attacking breaks stealth; damage reveals them for 6 seconds. The last surviving unit cannot hide. "+reason,reason=="");
            }
            if(first.def.id!="explorer"||w.Players[game.LocalPlayerSlot].civilization!="river")return;
            var unit=game.Rules.Entity("envoy");string training=w.ProductionReason(game.LocalPlayerSlot,first,unit.id);
            add(unit.name+"\n"+ShortCost(w.UnitCost(game.LocalPlayerSlot,unit)),()=>{
                var producer=TrainingProducer(first,unit,1);
                SubmitUiCommand(new GameCommand{player=game.LocalPlayerSlot,kind=CommandKind.Train,actors=new[]{producer.id},item=unit.id});InvalidateActions();
            },unit.description+"\n"+unit.time+"s · "+unit.population+" population. Limit "+w.UnitLimit(game.LocalPlayerSlot,unit)+" includes every living and queued Envoy. "+training,training=="");
            add("ENVOY RALLY",()=>game.Controls.BeginEnvoyRally(),"Choose the terrain flag used by future Envoys. Right-click movement still moves the Pathfinder.",first.Alive&&first.Complete&&first.garrisonedIn==0);
        }
    }
}
