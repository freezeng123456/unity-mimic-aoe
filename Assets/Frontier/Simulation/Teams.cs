using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        public bool IsMatchEnded=>Winner!=-1;
        public bool IsPlayerActive(int player)=>player>=0&&player<Players.Length&&!Players[player].resigned&&!Players[player].eliminated;
        public bool AreAllies(int a,int b)=>a>=0&&a<Players.Length&&b>=0&&b<Players.Length&&Players[a].team==Players[b].team;
        public bool AreEnemies(int a,int b)=>a>=0&&a<Players.Length&&b>=0&&b<Players.Length&&!AreAllies(a,b);
        private bool ShareMovementSide(int a,int b)=>a==b||AreAllies(a,b);
        public bool IsPlayerWinner(int player)=>Winner>=0&&AreAllies(player,Winner);
        public int FirstEnemyPlayer(int player)
        {
            if(player<0||player>=Players.Length)throw new ArgumentOutOfRangeException(nameof(player));
            for(int i=0;i<Players.Length;i++)if(AreEnemies(player,i)&&IsPlayerActive(i))return i;
            // A terminal snapshot can still restore its AI's authored search plan.
            for(int i=0;i<Players.Length;i++)if(AreEnemies(player,i))return i;
            throw new InvalidOperationException("A match requires at least two factions");
        }
        private static void ValidateRoster(RuleCatalog rules,string[] civilizations,int[] teams)
        {
            if(civilizations==null||teams==null||civilizations.Length<2||civilizations.Length>4||teams.Length!=civilizations.Length)
                throw new ArgumentException("Choose two to four continuous player slots with matching teams");
            var factions=new HashSet<int>();
            for(int i=0;i<civilizations.Length;i++)
            {
                if(rules.Civ(civilizations[i])==null)throw new ArgumentException("Unknown civilization");
                if(teams[i]<0||teams[i]>3)throw new ArgumentException("Team must be between zero and three");
                factions.Add(teams[i]);
            }
            if(factions.Count<2)throw new ArgumentException("A match requires at least two factions");
        }
        private bool IsHostileTarget(int owner,EntityState target)
        {
            if(target==null)return false;
            if(owner<0)return target.owner>=0&&IsPlayerActive(target.owner);
            return owner<Players.Length&&(target.owner<0?Guardian(target)||target.def.kind==EntityKind.Building:AreEnemies(owner,target.owner));
        }
        private bool HostileNeutralPresence(int player,Vector2 point,float radius)
        {
            for(int other=0;other<Players.Length;other++)
                if(IsPlayerActive(other)&&AreEnemies(player,other)&&NeutralPresence(other,point,radius,true))return true;
            return false;
        }
        private CommandResult ResignPlayer(int player)
        {
            Players[player].resigned=true;Players[player].eliminated=true;
            DeactivatePlayer(player);var resigned=Emit("Resign",player,0,"Player resigned");resigned.visibilityMask=(1<<Players.Length)-1;EvaluateElimination();
            return CommandResult.Ok("Resigned");
        }
        private void DeactivatePlayer(int player)
        {
            foreach(var e in Entities)if(e.owner==player)
            {ClearHealing076(e);e.autoHeal=false;ClearPatientHealingLinks076(e);e.orders.Clear();e.path.Clear();e.fireAt=-1;e.attackTarget=0;e.status="Eliminated";}
            foreach(var state in NeutralAllies)if(state.owner==player||state.claimant==player)LoseNeutralAlliance(state,"Player eliminated");
        }
        private void EvaluateElimination()
        {
            for(int player=0;player<Players.Length;player++)if((!HasScenario||scenarioDocument.enableElimination)&&IsPlayerActive(player))
            {
                bool remains=false;
                foreach(var e in Entities)if(e.Alive&&e.owner==player&&e.def.kind!=EntityKind.Resource){remains=true;break;}
                if(!remains){Players[player].eliminated=true;DeactivatePlayer(player);var eliminated=Emit("PlayerEliminated",player,0,"All units and buildings eliminated");eliminated.visibilityMask=(1<<Players.Length)-1;}
            }
            int representative=-1;var surviving=new HashSet<int>();
            for(int player=0;player<Players.Length;player++)if(IsPlayerActive(player))
            {if(representative<0)representative=player;surviving.Add(Players[player].team);}
            if(surviving.Count>1)return;
            Winner=surviving.Count==0?-2:representative;FinalizeMapObjectiveForMatchEnd();scenarioRuntime?.MarkResult(Winner,"Elimination or resignation",false);
            var ended=Emit("MatchEnded",Winner,0,Winner==-2?"Draw: no surviving faction":"All opposing factions eliminated");
            ended.visibilityMask=(1<<Players.Length)-1;
        }
    }
}
