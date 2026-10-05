using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    [Serializable] public sealed class NeutralAllyDef
    {
        public string id,name,unit,tech;
        public int socketIndex;
        public Cost allianceCost;
        public float allianceSeconds=8,controlRadius=9,lossSeconds=15;
    }
    [Serializable] public sealed class NeutralJobState
    {
        public string kind,item;
        public Cost paid;
        public float elapsed,duration;
        public int population;
    }
    [Serializable] public sealed class NeutralAllianceState
    {
        public string id;
        public int site,post,owner=-1,claimant=-1;
        public float claimProgress,lossProgress;
        public bool contested;
        public List<NeutralJobState> jobs=new List<NeutralJobState>();
    }

    public partial class World
    {
        public readonly List<NeutralAllianceState> NeutralAllies=new List<NeutralAllianceState>();
        public NeutralAllyDef NeutralDefinition(string id)
        {foreach(var d in Rules.neutralAllies??Array.Empty<NeutralAllyDef>())if(d.id==id)return d;return null;}
        public NeutralAllianceState NeutralAt(int site)
        {foreach(var state in NeutralAllies)if(state.site==site)return state;return null;}

        private void InitializeNeutralAllies()
        {
            if(HasScenario){InitializeScenarioNeutralAllies();return;}
            var ids=new HashSet<string>();var sockets=new HashSet<int>();
            foreach(var d in Rules.neutralAllies??Array.Empty<NeutralAllyDef>())
            {
                if(d==null||string.IsNullOrEmpty(d.id)||!ids.Add(d.id)||!sockets.Add(d.socketIndex)||d.socketIndex<0||d.socketIndex>=TradeSockets.Count||
                    Rules.Entity(d.unit)?.kind!=EntityKind.Unit||Rules.Tech(d.tech)==null||!SnapshotCodec.Finite(d.allianceCost)||
                    d.allianceCost.food<0||d.allianceCost.wood<0||d.allianceCost.coin<0||d.allianceCost.food+d.allianceCost.wood+d.allianceCost.coin<=0||
                    !SnapshotCodec.Finite(d.allianceSeconds)||d.allianceSeconds<=0||!SnapshotCodec.Finite(d.lossSeconds)||d.lossSeconds<=0||!SnapshotCodec.Finite(d.controlRadius)||d.controlRadius<=0)
                    throw new ArgumentException("Neutral ally needs valid paid services and a unique trade socket");
                NeutralAllies.Add(new NeutralAllianceState{id=d.id,site=TradeSockets[d.socketIndex].entity});
            }
        }

        // Contact is an actual ungarrisoned land unit, never explored fog or a
        // remote UI selection. Hostile military presence can disrupt a treaty.
        private bool NeutralPresence(int player,Vector2 point,float radius,bool militaryOnly=false)
        {
            foreach(var e in Entities)
                if(e.Alive&&e.owner==player&&e.garrisonedIn==0&&e.def.kind==EntityKind.Unit&&!e.def.waterborne&&
                    (!militaryOnly||e.Military)&&(e.position-point).sqrMagnitude<=radius*radius)return true;
            return false;
        }
        public string NeutralCommandReason(int player,int site,CommandKind kind)
        {
            if(player<0||player>=Players.Length)return "Invalid player";
            if(IsMatchEnded)return "Match has ended";
            if(!IsPlayerActive(player))return "Player is eliminated or resigned";
            if(Paused)return "Resume the match first";
            var state=NeutralAt(site);var d=state==null?null:NeutralDefinition(state.id);var anchor=Get(site);
            if(d==null||anchor==null||!anchor.Alive)return "Unknown neutral settlement";
            if(!IsVisible(player,anchor))return "Neutral settlement is not currently visible";
            if(!NeutralPresence(player,anchor.position,d.controlRadius))return "Bring a land unit to contact the settlement";
            var post=TradingPostAt(site);
            if(post==null||post.owner!=player||!post.Complete)return "Build your trading post at this settlement";
            if(HostileNeutralPresence(player,anchor.position,d.controlRadius))return "Settlement is contested";
            if(kind==CommandKind.NeutralAlliance)
            {
                if(state.owner>=0||state.claimant>=0)return "Alliance already held or being negotiated";
                if(!CanAfford(player,d.allianceCost))return "Need "+d.allianceCost;
                return "";
            }
            if(state.owner!=player)return "Complete a paid alliance first";
            if(state.jobs.Count>0)return "Settlement service is already in progress";
            if(kind==CommandKind.NeutralRecruit)
            {
                var unit=Rules.Entity(d.unit);if(unit==null||unit.kind!=EntityKind.Unit||unit.waterborne)return "Invalid allied unit";
                if(Players[player].age<UnitAge(player,unit))return "Requires age "+UnitAge(player,unit);
                if(!EntityAvailable(player,unit))return "Civilization requirement";
                string limit=UnitLimitReason(player,unit,1);if(limit!="")return limit;
                if(!CanAfford(player,UnitCost(player,unit)))return "Need "+UnitCost(player,unit);
                if(Players[player].usedPopulation+Players[player].reservedPopulation+unit.population>Mathf.Min(Players[player].populationCap,Rules.populationLimit))return "Need more housing";
                return "";
            }
            if(kind==CommandKind.NeutralResearch)
            {
                var tech=Rules.Tech(d.tech);if(tech==null)return "Invalid allied technology";
                if(Players[player].age<tech.age)return "Requires age "+tech.age;
                if(tech.units!=null&&tech.units.Length>0&&!Array.Exists(tech.units,id=>EntityAvailable(player,Rules.Entity(id))))return "No available units use this technology";
                if(Players[player].technologies.Contains(tech.id)||TechnologyQueued(player,tech.id))return "Already completed or queued";
                foreach(var other in NeutralAllies)if(other.owner==player)foreach(var job in other.jobs)if(job.kind=="Research"&&job.item==tech.id)return "Already completed or queued";
                if(!string.IsNullOrEmpty(tech.requires)&&!Players[player].technologies.Contains(tech.requires))return "Requires "+tech.requires;
                if(!string.IsNullOrEmpty(tech.civilization)&&tech.civilization!=Players[player].civilization)return "Civilization requirement";
                if(!CanAfford(player,tech.cost))return "Need "+tech.cost;
                return "";
            }
            return "Unknown neutral service";
        }
        public CommandResult IssueNeutralCommand(GameCommand command)
        {
            if(command==null)return CommandResult.Fail("Missing command");
            if(command.kind!=CommandKind.NeutralAlliance&&command.kind!=CommandKind.NeutralRecruit&&command.kind!=CommandKind.NeutralResearch)return CommandResult.Fail("Unknown neutral service");
            if(command.count!=1)return CommandResult.Fail("Recruit one allied unit at a time");
            string reason=NeutralCommandReason(command.player,command.target,command.kind);if(reason!="")return CommandResult.Fail(reason);
            var state=NeutralAt(command.target);var d=NeutralDefinition(state.id);var post=TradingPostAt(state.site);int player=command.player;
            if(command.kind==CommandKind.NeutralAlliance)
            {
                Pay(player,d.allianceCost,state.site,d.name+" alliance");
                state.claimant=player;state.post=post.id;state.claimProgress=0;state.lossProgress=0;
                Emit("NeutralAllianceStarted",player,state.site,d.id,0,post.id);
            }
            else if(command.kind==CommandKind.NeutralRecruit)
            {
                var unit=Rules.Entity(d.unit);Cost cost=UnitCost(player,unit);Pay(player,cost,state.site,d.name+" recruitment");
                Players[player].reservedPopulation+=unit.population;
                state.jobs.Add(new NeutralJobState{kind="Train",item=unit.id,paid=cost,duration=unit.time,population=unit.population});
                var queued=Emit("NeutralRecruitQueued",player,state.site,unit.id,unit.population);queued.hasPopulation=true;queued.population=PopulationSnapshot.From(Players[player]);
            }
            else
            {
                var tech=Rules.Tech(d.tech);Pay(player,tech.cost,state.site,d.name+" research");
                state.jobs.Add(new NeutralJobState{kind="Research",item=tech.id,paid=tech.cost,duration=tech.time});
                Emit("NeutralResearchQueued",player,state.site,tech.id);
            }
            return CommandResult.Ok("Paid settlement service started");
        }
        private void LoseNeutralAlliance(NeutralAllianceState state,string reason)
        {
            int player=state.owner>=0?state.owner:state.claimant;
            if(player>=0)foreach(var job in state.jobs)Players[player].reservedPopulation=Mathf.Max(0,Players[player].reservedPopulation-job.population);
            // The fee bought the interrupted service. Losing its site forfeits
            // unfinished work, as destroying an ordinary producer does.
            state.jobs.Clear();state.owner=-1;state.claimant=-1;state.post=0;state.claimProgress=0;state.lossProgress=0;state.contested=false;
            var lost=Emit("NeutralAllianceLost",player,state.site,reason);
            if(player>=0){lost.hasPopulation=true;lost.population=PopulationSnapshot.From(Players[player]);}
        }
        private void TickNeutralAllies(float dt)
        {
            foreach(var state in NeutralAllies)
            {
                int player=state.owner>=0?state.owner:state.claimant;if(player<0)continue;
                var d=NeutralDefinition(state.id);var anchor=Get(state.site);var post=Get(state.post);
                if(!IsPlayerActive(player)||anchor==null||!anchor.Alive||post==null||!post.Alive||post.owner!=player||!post.Complete)
                {LoseNeutralAlliance(state,"Trading post lost");continue;}
                bool friendly=NeutralPresence(player,anchor.position,d.controlRadius);
                bool hostile=HostileNeutralPresence(player,anchor.position,d.controlRadius);
                if(state.contested!=hostile){state.contested=hostile;Emit("NeutralContest",player,state.site,hostile?"Hostile troops contest the settlement":"Settlement secure");}
                state.lossProgress=hostile&&!friendly?state.lossProgress+dt:0;
                if(state.lossProgress+.0001f>=d.lossSeconds){LoseNeutralAlliance(state,"Hostile troops broke control");continue;}
                if(hostile)continue;
                if(state.claimant>=0)
                {
                    if(!friendly)continue;
                    state.claimProgress=Mathf.Min(d.allianceSeconds,state.claimProgress+dt);
                    if(state.claimProgress+.0001f<d.allianceSeconds)continue;
                    state.owner=state.claimant;state.claimant=-1;Emit("NeutralAllianceComplete",state.owner,state.site,d.id);
                }
                if(state.jobs.Count==0)continue;
                var job=state.jobs[0];job.elapsed=Mathf.Min(job.duration,job.elapsed+dt);if(job.elapsed+.0001f<job.duration)continue;
                if(job.kind=="Research")
                {ApplyTechnology(player,job.item);state.jobs.RemoveAt(0);Emit("NeutralResearchComplete",player,state.site,job.item);}
                else
                {
                    var unit=Rules.Entity(job.item);
                    if(UnitLimitReason(player,unit,1)!=""||Players[player].usedPopulation+Players[player].reservedPopulation>Mathf.Min(Players[player].populationCap,Rules.populationLimit))continue;
                    if(!FindSpawnPositions(post,1,unit,out var positions))continue;
                    state.jobs.RemoveAt(0);Players[player].reservedPopulation=Mathf.Max(0,Players[player].reservedPopulation-job.population);
                    var created=Spawn(unit.id,player,positions[0]);Players[player].trained++;AwardXp(player,unit.trainXp,state.site,"Allied unit recruited");
                    Emit("NeutralRecruitComplete",player,state.site,unit.id,1,created.id);
                }
            }
        }
        public NeutralAllianceState[] CaptureNeutralAllies()
        {var states=new NeutralAllianceState[NeutralAllies.Count];for(int i=0;i<states.Length;i++)states[i]=SnapshotCodec.Clone(NeutralAllies[i]);return states;}
        public void RestoreNeutralAllies(NeutralAllianceState[] states)
        {NeutralAllies.Clear();foreach(var state in states)NeutralAllies.Add(SnapshotCodec.Clone(state));}
        public static void ValidateNeutralAlliesSnapshot(RuleCatalog rules,NeutralAllianceState[] states,EntitySnapshot[] entities,PlayerSnapshot[] players,ScenarioDocument scenario=null,ScenarioRuntimeState runtime=null)
        {
            void Require(bool valid){if(!valid)throw new ArgumentException("Invalid saved neutral alliances");}
            var defs=rules.neutralAllies??Array.Empty<NeutralAllyDef>();Require(states!=null&&states.Length==(scenario==null?defs.Length:scenario.neutralBindings.Length));
            var ids=new HashSet<string>();var sites=new HashSet<int>();var reserved=new int[players.Length];
            EntitySnapshot Find(int id)=>Array.Find(entities,e=>e.state.id==id);
            foreach(var state in states)
            {
                Require(state!=null);var d=Array.Find(defs,x=>x.id==state.id);
                Require(d!=null&&ids.Add(state.id)&&sites.Add(state.site)&&Find(state.site)?.definition=="trade_socket");
                if(scenario!=null)
                {
                    var declared=Array.Find(scenario.neutralBindings,b=>b.definition==state.id);
                    var socket=declared==null?null:Array.Find(runtime?.bindings??Array.Empty<ScenarioBindingState>(),b=>b.objectId==declared.socketId);
                    Require(socket?.entityIds?.Length==1&&socket.entityIds[0]==state.site);
                }
                Require(state.owner>=-1&&state.owner<players.Length&&state.claimant>=-1&&state.claimant<players.Length&&!(state.owner>=0&&state.claimant>=0));
                Require(SnapshotCodec.Finite(state.claimProgress)&&state.claimProgress>=0&&state.claimProgress<=d.allianceSeconds+.001f&&SnapshotCodec.Finite(state.lossProgress)&&state.lossProgress>=0&&state.lossProgress<=d.lossSeconds+.001f);
                Require(state.jobs!=null&&state.jobs.Count<=1);int player=state.owner>=0?state.owner:state.claimant;
                Require(player<0?state.post==0&&state.jobs.Count==0:Find(state.post)?.definition=="trade_post"&&Find(state.post).state.owner==player&&!players[player].state.eliminated&&!players[player].state.resigned);
                foreach(var job in state.jobs)
                {
                    Require(job!=null&&state.owner>=0&&SnapshotCodec.Finite(job.elapsed)&&job.elapsed>=0&&SnapshotCodec.Finite(job.duration)&&job.duration>=0&&job.elapsed<=job.duration+.001f&&SnapshotCodec.Finite(job.paid)&&job.paid.food>=0&&job.paid.wood>=0&&job.paid.coin>=0);
                    Require(job.kind=="Train"?job.item==d.unit&&rules.Entity(job.item)!=null&&job.population==rules.Entity(job.item).population:job.kind=="Research"&&job.item==d.tech&&rules.Tech(job.item)!=null&&job.population==0);
                    reserved[player]+=job.population;
                }
            }
            for(int player=0;player<players.Length;player++)Require(players[player].state.reservedPopulation>=reserved[player]);
        }
    }
}
