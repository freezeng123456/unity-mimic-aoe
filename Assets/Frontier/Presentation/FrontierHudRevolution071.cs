using UnityEngine;

namespace Frontier
{
    public partial class FrontierHud
    {
        private PoliticalPhase homePoliticalPhase071;
        private void ShowDisband071()
        {
            var actors=new System.Collections.Generic.List<int>();
            foreach(int id in game.Controls.Selected)
            {
                var e=game.World.Get(id);if(e!=null&&e.Alive&&e.owner==game.LocalPlayerSlot&&e.def.kind==EntityKind.Unit&&!e.def.waterborne&&e.garrisonedIn==0)actors.Add(id);
            }
            if(actors.Count==0)return;
            var box=Modal("DISBAND LAND UNITS",320);
            Label(box,"Dismiss "+actors.Count+" selected land units permanently in this match.\nTheir population is released. No resources or combat XP are refunded.\nUse a free population slot to resume settlers after civilian rebuilding.",new Rect(25,85,815,130),19);
            Button(box,"DISBAND "+actors.Count+" UNITS",new Rect(25,235,397,60),()=>SubmitModalCommand(new GameCommand{player=game.LocalPlayerSlot,kind=CommandKind.Disband,actors=actors.ToArray()},box,r=>{if(r.accepted){CloseModal();InvalidateActions();}}),"",game.CanSubmitOrders);
            Button(box,"KEEP UNITS",new Rect(443,235,397,60),CloseModal);
        }
        private void ShowRevolution071(EntityState town)
        {
            if(game.World==null||town==null)return;
            var w=game.World;var p=w.Players[game.LocalPlayerSlot];var rule=w.RevolutionDefinition(p.id);if(rule==null)return;
            var box=Modal(rule.name.ToUpperInvariant(),620);
            if(p.politicalPhase==PoliticalPhase.Empire)
            {
                int count=0;foreach(var e in w.Entities)if(e.Alive&&e.owner==p.id&&e.Worker&&!e.def.waterborne)count++;
                Label(box,"AGE IV COMMITMENT\n"+rule.cost+" · "+rule.time+"s · "+count+" settlers currently eligible\n\nOn completion, all living land settlers become revolutionary infantry, including sheltered and transported settlers. Health ratios, IDs and population remain intact. Age V is permanently closed.\n\nNew settler training closes when this order is accepted. Cancelling the unfinished queue refunds its payment; losing the town center loses the investment. Old paid shipments and training still arrive.\n\nThe active deck changes; soldiers can collect your supply crates, but cannot gather natural resources, construct or repair. Prepare housing and economy before committing.",new Rect(25,80,815,390),19);
                string reason=w.RevolutionReason(p.id,town);
                var first=w.WorkerDefinition(p.id);string restart=w.AutoRecruitSeconds(p.id)>0?"Automatic settlers resume after rebuilding.":"Then the first settler needs "+w.UnitCost(p.id,first)+" and a population slot.";
                Label(box,reason==""?"Civilian rebuilding: 400 F / 400 C, 60s.\n"+restart:reason,new Rect(25,475,815,55),18,gold);
                Button(box,"START REVOLUTION",new Rect(25,540,397,60),()=>SubmitModalCommand(new GameCommand{player=game.LocalPlayerSlot,kind=CommandKind.Revolution,actors=new[]{town.id}},box,r=>{if(r.accepted){CloseModal();InvalidateActions();}}),reason,game.CanSubmitOrders&&reason=="");
            }
            else
            {
                var tech=game.Rules.Tech(World.CivilianRebuildingTechnology);string reason=w.ProductionReason(p.id,town,tech.id,true);
                Label(box,"REVOLUTION COMPLETED · AGE IV\n\nThe opening deck and its card history are retained for already accepted deliveries. Your available Home City shipments now follow "+rule.name+".\n\nCivilian rebuilding restores your civilization's settler sources and existing worker technologies. Current revolutionaries remain soldiers; age V stays closed.\n\n"+tech.cost+" · "+tech.time+"s\n"+(p.politicalPhase==PoliticalPhase.CivilianRebuilt?"Civilian government restored.":reason),new Rect(25,90,815,370),20);
                Button(box,"REBUILD CIVILIAN GOVERNMENT",new Rect(25,540,397,60),()=>SubmitModalCommand(new GameCommand{player=game.LocalPlayerSlot,kind=CommandKind.Research,actors=new[]{town.id},item=tech.id},box,r=>{if(r.accepted){CloseModal();InvalidateActions();}}),reason,game.CanSubmitOrders&&reason=="");
            }
            Button(box,"KEEP PLAYING",new Rect(443,540,397,60),CloseModal);
        }
    }
}
