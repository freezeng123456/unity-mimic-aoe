using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    [Serializable] public sealed class ScenarioBindingState
    {public string objectId="";public int[] entityIds=Array.Empty<int>();}
    [Serializable] public sealed class ScenarioObjectiveState
    {public string id="",status="active",reason="";public float progress,elapsed;}
    [Serializable] public sealed class ScenarioTriggerState
    {public string id="",lastError="";public bool enabled;public int fires;public float nextAt;}
    [Serializable] public sealed class ScenarioRuntimeState
    {
        public int schema=1,winner=-1;public string documentHash="",reason="";public bool scenarioVictory;
        public ScenarioObjectiveState[] objectiveStates=Array.Empty<ScenarioObjectiveState>();
        public ScenarioTriggerState[] triggerStates=Array.Empty<ScenarioTriggerState>();
        public ScenarioBindingState[] bindings=Array.Empty<ScenarioBindingState>();
    }
    // Only the authority runs Tick. Replay runs the same authority from its
    // saved boundary; replicas install this state without executing any action.
    public sealed class ScenarioRuntime
    {
        public const int Version=2;
        readonly World world;readonly ScenarioDocument document;
        ScenarioRuntimeState state;
        public ScenarioRuntime(World world,ScenarioDocument document,List<ScenarioBindingState> bindings)
        {
            this.world=world;this.document=document;
            state=new ScenarioRuntimeState{documentHash=world.ScenarioHash,bindings=SnapshotCodec.Clone(new BindingRows{rows=bindings.ToArray()}).rows,
                objectiveStates=new ScenarioObjectiveState[document.objectives.Length],triggerStates=new ScenarioTriggerState[document.triggers.Length]};
            foreach(var group in document.groups)
            {
                var members=new List<int>();foreach(string id in group.objectIds)members.AddRange(BoundEntities(id));
                AddBinding(group.id,members.ToArray());
            }
            for(int i=0;i<document.objectives.Length;i++)state.objectiveStates[i]=new ScenarioObjectiveState{id=document.objectives[i].id,status=document.objectives[i].prerequisites.Length==0?"active":"locked"};
            for(int i=0;i<document.triggers.Length;i++)
            {
                var trigger=document.triggers[i];state.triggerStates[i]=new ScenarioTriggerState{id=trigger.id,enabled=trigger.enabled};
                foreach(var action in trigger.actions)if(action.kind=="spawn"&&action.objectId!=""&&Binding(action.objectId)==null)
                    AddBinding(action.objectId,Array.Empty<int>());
            }
        }
        [Serializable] sealed class BindingRows {public ScenarioBindingState[] rows;}
        ScenarioBindingState Binding(string id)=>Array.Find(state.bindings,b=>b.objectId==id);
        public int[] BoundEntities(string id){var binding=Binding(id);return binding==null?Array.Empty<int>():(int[])binding.entityIds.Clone();}
        void AddBinding(string id,int[] ids)
        {
            var existing=Binding(id);
            if(existing!=null){var combined=new List<int>(existing.entityIds);combined.AddRange(ids);existing.entityIds=combined.ToArray();return;}
            var rows=new List<ScenarioBindingState>(state.bindings){new ScenarioBindingState{objectId=id,entityIds=(int[])ids.Clone()}};state.bindings=rows.ToArray();
        }
        public ScenarioRuntimeState Capture()=>SnapshotCodec.Clone(state);
        public void Restore(ScenarioRuntimeState saved)
        {
            Validate(document,world.ScenarioHash,saved,world.Entities.ToArray(),world.Winner,world.Time,
                world.Entities.ConvertAll(e=>e.def.id).ToArray(),Array.ConvertAll(world.Players,p=>world.Rules.Civ(p.civilization).workerUnit),
                world.Rules,world.Players,world.MaxAge);state=SnapshotCodec.Clone(saved);
        }
        ScenarioObjectiveState Objective(string id)=>Array.Find(state.objectiveStates,o=>o.id==id);
        ScenarioTriggerState Trigger(string id)=>Array.Find(state.triggerStates,t=>t.id==id);
        ScenarioRegion Region(string id)=>Array.Find(document.regions,r=>r.id==id);
        bool InRegion(EntityState entity,ScenarioRegion region)
        {
            if(entity==null||!entity.Alive||entity.garrisonedIn!=0||entity.def.kind!=EntityKind.Unit||region==null)return false;
            var delta=entity.position-region.center;return Mathf.Abs(delta.x)<=region.size.x*.5f&&Mathf.Abs(delta.y)<=region.size.y*.5f;
        }
        int RegionCount(string id,int player,string objectId="")
        {
            var region=Region(id);int count=0;
            if(objectId!="")foreach(int entity in BoundEntities(objectId)){var e=world.Get(entity);if(InRegion(e,region)&&(player<0||world.AreAllies(player,e.owner)))count++;}
            else foreach(var e in world.Entities)if(InRegion(e,region)&&(player<0||world.AreAllies(player,e.owner)))count++;
            return count;
        }
        bool Dead(string id)
        {var ids=BoundEntities(id);if(ids.Length==0)return false;foreach(int entity in ids)if(world.Get(entity)?.Alive==true)return false;return true;}
        int EmbarkedCount(string objectId,string targetId,int player)
        {
            var targets=BoundEntities(targetId);if(targets.Length!=1)return 0;
            var ship=world.Get(targets[0]);
            if(ship==null||!ship.Alive||!ship.def.waterborne||!ship.def.transportShip||!world.AreAllies(player,ship.owner))return 0;
            int count=0;foreach(int id in BoundEntities(objectId))
            {
                var member=world.Get(id);
                if(member!=null&&member.Alive&&member.Military&&!member.def.waterborne&&world.AreAllies(player,member.owner)&&
                    member.garrisonedIn==ship.id&&ship.occupants.Contains(member.id))count++;
            }
            return count;
        }
        int LostCount(string objectId,int player)
        {
            // An empty predeclared spawn group is not a destroyed unit. Live
            // world entities are retained after death, preserving ownership.
            int count=0;foreach(int id in BoundEntities(objectId))
            {var member=world.Get(id);if(member!=null&&!member.Alive&&member.def.kind==EntityKind.Unit&&world.AreAllies(player,member.owner))count++;}
            return count;
        }
        float Resource(int player,ResourceKind kind)
        {var value=world.Players[player].resources;return kind==ResourceKind.Food?value.food:kind==ResourceKind.Wood?value.wood:value.coin;}
        bool Condition(ScenarioCondition condition)
        {
            switch(condition.kind)
            {
                case "time":return world.Time+.0001f>=condition.time;
                case "object_dead":return Dead(condition.objectId);
                case "units_in_region":return RegionCount(condition.regionId,condition.player,condition.objectId)+.0001f>=condition.amount;
                case "units_embarked":return EmbarkedCount(condition.objectId,condition.targetId,condition.player)+.0001f>=condition.amount;
                case "units_lost":return LostCount(condition.objectId,condition.player)+.0001f>=condition.amount;
                case "resource":return Resource(condition.player,condition.resource)+.0001f>=condition.amount;
                case "objective_complete":return Objective(condition.objectiveId)?.status=="completed";
                case "objective_failed":return Objective(condition.objectiveId)?.status=="failed";
                case "trigger_fired":return (Trigger(condition.triggerId)?.fires??0)+.0001f>=condition.amount;
                default:return false;
            }
        }
        bool Conditions(ScenarioTrigger trigger)
        {
            if(trigger.conditions.Length==0)return true;
            foreach(var condition in trigger.conditions)
            {bool matched=Condition(condition);if(trigger.matchAny&&matched)return true;if(!trigger.matchAny&&!matched)return false;}
            return !trigger.matchAny;
        }
        void ObjectiveResult(string id,string status,string reason)
        {
            var objective=Objective(id);if(objective==null||objective.status=="completed"||objective.status=="failed")return;
            objective.status=status;objective.reason=reason;
            PublicEvent(status=="completed"?"ScenarioObjectiveCompleted":"ScenarioObjectiveFailed",document.victoryPlayer,id+": "+reason);
        }
        void PublicEvent(string kind,int player,string message)
        {var ev=world.Emit(kind,player,0,message);ev.visibilityMask=(1<<world.Players.Length)-1;}
        public void Tick(float dt)
        {
            if(world.IsMatchEnded||world.Paused)return;
            for(int i=0;i<document.objectives.Length;i++)
            {
                var definition=document.objectives[i];var objective=state.objectiveStates[i];
                if(objective.status=="locked")
                {bool ready=true;foreach(string id in definition.prerequisites)if(Objective(id)?.status!="completed"){ready=false;break;}if(ready)objective.status="active";}
                if(objective.status!="active")continue;
                objective.elapsed+=dt;
                if(definition.deadline>0&&objective.elapsed+.0001f>=definition.deadline)
                {ObjectiveResult(definition.id,"failed","Deadline expired");continue;}
                if(definition.kind=="hold_region")
                {
                    bool friendly=RegionCount(definition.regionId,definition.player,definition.objectId)+.0001f>=definition.amount,hostile=false;
                    for(int player=0;player<world.Players.Length;player++)if(world.AreEnemies(definition.player,player)&&RegionCount(definition.regionId,player)>0){hostile=true;break;}
                    objective.progress=friendly&&!hostile?Mathf.Min(definition.holdSeconds,objective.progress+dt):0;
                    if(objective.progress+.0001f>=definition.holdSeconds)ObjectiveResult(definition.id,"completed","Region held");
                }
                else if(definition.kind=="destroy_object")
                {objective.progress=Dead(definition.objectId)?1:0;if(objective.progress>0)ObjectiveResult(definition.id,"completed","All bound targets destroyed");}
                else if(definition.kind=="reach_region")
                {objective.progress=RegionCount(definition.regionId,definition.player,definition.objectId);if(objective.progress+.0001f>=definition.amount)ObjectiveResult(definition.id,"completed","Units reached the region");}
                else if(definition.kind=="collect_resource")
                {objective.progress=Resource(definition.player,definition.resource);if(objective.progress+.0001f>=definition.amount)ObjectiveResult(definition.id,"completed","Resource threshold reached");}
                else if(definition.kind=="embark_group")
                {objective.progress=EmbarkedCount(definition.objectId,definition.targetId,definition.player);if(objective.progress+.0001f>=definition.amount)ObjectiveResult(definition.id,"completed","Specified military units embarked");}
                else if(definition.kind=="escort_group")
                {
                    var ship=world.ScenarioEntity(definition.targetId);
                    objective.progress=InRegion(ship,Region(definition.regionId))?EmbarkedCount(definition.objectId,definition.targetId,definition.player):0;
                    if(objective.progress+.0001f>=definition.amount)ObjectiveResult(definition.id,"completed","Loaded transport reached the region");
                }
            }
            // A chain enabled by this pass runs on the following fixed step.
            var eligible=new bool[document.triggers.Length];
            for(int i=0;i<eligible.Length;i++)eligible[i]=state.triggerStates[i].enabled&&state.triggerStates[i].fires<document.triggers[i].maxFires&&world.Time+.0001f>=state.triggerStates[i].nextAt&&Conditions(document.triggers[i]);
            for(int i=0;i<eligible.Length&&!world.IsMatchEnded;i++)if(eligible[i])Fire(document.triggers[i],state.triggerStates[i]);
            if(world.IsMatchEnded)return;
            bool required=false,complete=true,failed=false;
            for(int i=0;i<document.objectives.Length;i++)if(document.objectives[i].required)
            {required=true;complete&=state.objectiveStates[i].status=="completed";failed|=state.objectiveStates[i].status=="failed";}
            if(failed)world.FinishScenario(world.ScenarioFailureWinner(document.victoryPlayer),"Required scenario objective failed");
            else if(required&&complete&&world.IsPlayerActive(document.victoryPlayer))world.FinishScenario(document.victoryPlayer,"All required scenario objectives completed");
        }
        void Fire(ScenarioTrigger trigger,ScenarioTriggerState result)
        {
            var planned=new Dictionary<int,Vector2[]>();var reserved=new List<Tuple<EntityDef,Vector2>>();
            // A blocked group cannot grant a preceding reward and then repeat it
            // on the next retry. Plan all group spawns before any side effect.
            for(int i=0;i<trigger.actions.Length;i++)if(trigger.actions[i].kind=="spawn")
            {
                var action=trigger.actions[i];
                if(!world.PlanScenarioSpawnGroup(action,reserved,out var points,out var error))
                {result.lastError=error;result.nextAt=world.Time+trigger.repeatSeconds;return;}
                planned.Add(i,points);foreach(var p in points)reserved.Add(Tuple.Create(world.Rules.Entity(action.definition),p));
            }
            result.fires++;result.nextAt=world.Time+trigger.repeatSeconds;result.lastError="";
            for(int i=0;i<trigger.actions.Length&&!world.IsMatchEnded;i++)
            {
                var action=trigger.actions[i];string error="";
                switch(action.kind)
                {
                    case "message":PublicEvent("ScenarioMessage",action.player,action.message);break;
                    case "spawn":var spawned=world.SpawnScenarioGroup(action,planned[i]);if(action.objectId!="")AddBinding(action.objectId,spawned);break;
                    case "grant_resource":if(world.IsPlayerActive(action.player))world.GrantScenarioResource(action.player,action.resource,action.amount);else error="Reward player is inactive";break;
                    case "complete_objective":ObjectiveResult(action.objectiveId,"completed","Completed by trigger "+trigger.id);break;
                    case "fail_objective":ObjectiveResult(action.objectiveId,"failed","Failed by trigger "+trigger.id);break;
                    case "enable_trigger":Trigger(action.triggerId).enabled=true;break;
                    case "disable_trigger":Trigger(action.triggerId).enabled=false;break;
                    case "win":if(world.IsPlayerActive(action.player))world.FinishScenario(action.player,action.message==""?"Scenario victory":action.message);else error="Victory player is inactive";break;
                    case "lose":world.FinishScenario(world.ScenarioFailureWinner(action.player),action.message==""?"Scenario defeat":action.message);break;
                    case "order_move":case "order_attack":case "order_attack_move":
                    {
                        var target=action.kind=="order_attack"?world.ScenarioEntity(action.targetId):null;
                        var command=new GameCommand{player=action.player,kind=CommandKind.Order,actors=BoundEntities(action.objectId),point=target?.position??action.point,target=target?.id??0,
                            order=action.kind=="order_attack"?OrderKind.Attack:action.kind=="order_attack_move"?OrderKind.AttackMove:OrderKind.Move};
                        var outcome=world.Issue(command);if(!outcome.accepted)error=outcome.reason;break;
                    }
                }
                if(error!=""){result.lastError="Action "+i+": "+error;PublicEvent("ScenarioTriggerFailed",action.player,trigger.id+": "+result.lastError);break;}
            }
            if(result.lastError=="")PublicEvent("ScenarioTriggerFired",document.victoryPlayer,trigger.id+" · execution "+result.fires);
        }
        public void MarkResult(int winner,string reason,bool scenarioVictory)
        {state.winner=winner;state.reason=reason;state.scenarioVictory=scenarioVictory;}
        public static void Validate(ScenarioDocument document,string hash,ScenarioRuntimeState state,EntityState[] entities,int winner,float time,string[] entityDefinitions=null,string[] workerDefinitions=null,
            RuleCatalog rules=null,PlayerState[] players=null,int maxAge=0)
        {
            void Require(bool valid,string reason){if(!valid)throw new ArgumentException("Invalid scenario runtime state: "+reason);}
            Require(state!=null&&state.schema==1&&state.documentHash==hash,"identity");
            Require(state.objectiveStates?.Length==document.objectives.Length&&state.triggerStates?.Length==document.triggers.Length&&state.bindings!=null,"collections");
            Require(state.winner==winner&&state.reason!=null&&state.reason.Length<=1024,"result");
            if(winner==-1)Require(!state.scenarioVictory&&state.reason=="","active result");
            // Cold EntityState.def is nonserialized. Snapshot validation passes
            // EntitySnapshot.definition without mutating the source state.
            entityDefinitions??=Array.ConvertAll(entities,e=>e.def?.id??"");
            Require(entityDefinitions.Length==entities.Length&&workerDefinitions?.Length==document.starts.Length,"entity/worker definition context");
            Require(rules==null&&players==null||rules!=null&&players?.Length==document.starts.Length,"political role context");
            bool ConvertedWorkerRole(string original,string current,int owner)
            {
                // This is a definition transition of the same authored actor,
                // never a replacement binding or a general military exception.
                // WorldSnapshot validates the full political receipt first.
                if(rules==null||players==null||owner<0||owner>=players.Length||maxAge!=5)return false;
                var p=players[owner];var before=rules.Entity(original);var after=rules.Entity(current);
                var revolution=p==null?null:Array.Find(rules.revolutions??Array.Empty<RevolutionDef>(),r=>r!=null&&r.civilization==p.civilization);
                return p!=null&&p.id==owner&&(p.politicalPhase==PoliticalPhase.Revolutionary||p.politicalPhase==PoliticalPhase.CivilianRebuilt)&&p.age==4&&
                    revolution!=null&&p.revolutionId==revolution.id&&SnapshotCodec.Finite(p.revolutionAt)&&p.revolutionAt>=0&&p.revolutionAt<=time&&
                    before!=null&&before.kind==EntityKind.Unit&&(before.tags&Tags.Villager)!=0&&!before.waterborne&&
                    after!=null&&after.kind==EntityKind.Unit&&after.revolutionary&&!after.waterborne&&current==revolution.unit&&
                    before.population==after.population&&before.radius==after.radius;
            }
            var ids=new HashSet<int>();var entityRows=new Dictionary<int,EntityState>();
            var definitions=new Dictionary<int,string>();
            for(int i=0;i<entities.Length;i++)
            {var entity=entities[i];ids.Add(entity.id);entityRows.Add(entity.id,entity);Require(!string.IsNullOrEmpty(entityDefinitions[i]),"missing entity definition");definitions.Add(entity.id,entityDefinitions[i]);}
            var known=new HashSet<string>();
            var initial=new Dictionary<string,Tuple<int,Vector2>>();
            var roles=new Dictionary<string,string>();
            void Initial(string id,int owner,Vector2 position,string definition="")
            {known.Add(id);initial.Add(id,Tuple.Create(owner,position));if(definition!="")roles.Add(id,definition);}
            foreach(var s in document.starts)
            {
                Initial("start-"+s.player+"-town",s.player,s.town,"town");Initial("start-"+s.player+"-explorer",s.player,s.explorer,"explorer");
                Require(!string.IsNullOrEmpty(workerDefinitions[s.player]),"missing actual worker definition");
                for(int i=0;i<s.workers.Length;i++)Initial("start-"+s.player+"-worker-"+i,s.player,s.workers[i],workerDefinitions[s.player]);
            }
            foreach(var e in document.resources)Initial(e.id,e.owner,e.position,e.definition);foreach(var e in document.objects)Initial(e.id,e.owner,e.position,e.definition);foreach(var e in document.tradeSockets)Initial(e.id,-1,e.position,"trade_socket");
            foreach(var e in document.treasures){Initial(e.id,-1,e.position);for(int i=0;i<e.guardPositions.Length;i++)Initial(e.id+"-guard-"+i,-1,e.guardPositions[i],"guardian");}
            var required=new HashSet<string>(known);
            var groups=new Dictionary<string,ScenarioGroup>();foreach(var group in document.groups){known.Add(group.id);groups.Add(group.id,group);}
            var spawnCounts=new Dictionary<string,int>();
            for(int i=0;i<document.triggers.Length;i++)
            {
                Require(state.triggerStates[i]!=null&&state.triggerStates[i].fires>=0&&state.triggerStates[i].fires<=document.triggers[i].maxFires,"trigger execution count");
                foreach(var action in document.triggers[i].actions)if(action.kind=="spawn"&&action.objectId!="")
                {known.Add(action.objectId);spawnCounts.Add(action.objectId,action.count*state.triggerStates[i].fires);}
            }
            var bound=new HashSet<string>();var actors=new HashSet<int>();
            foreach(var binding in state.bindings)
            {
                Require(binding!=null&&known.Contains(binding.objectId)&&bound.Add(binding.objectId)&&binding.entityIds!=null&&binding.entityIds.Length<=4096,"binding");
                if(groups.ContainsKey(binding.objectId))
                {
                    // Group bindings alias existing initial actors. They never
                    // allocate new IDs or relax uniqueness of primary bindings.
                    Require(binding.entityIds.Length==groups[binding.objectId].objectIds.Length,"group binding size");
                    foreach(int entity in binding.entityIds)Require(ids.Contains(entity),"group binding entity");
                    continue;
                }
                if(required.Contains(binding.objectId))Require(binding.entityIds.Length==1,"initial binding");
                foreach(int entity in binding.entityIds)Require(ids.Contains(entity)&&actors.Add(entity),"binding entity");
                if(initial.TryGetValue(binding.objectId,out var expected))
                {
                    var entity=entityRows[binding.entityIds[0]];
                    Require(entity.owner==expected.Item1&&SnapshotCodec.Finite(entity.origin)&&(entity.origin-expected.Item2).sqrMagnitude<.000001f,"initial binding identity");
                    if(roles.TryGetValue(binding.objectId,out string role))Require(definitions[entity.id]==role||ConvertedWorkerRole(role,definitions[entity.id],expected.Item1),"initial binding definition "+binding.objectId);
                }
                else Require(binding.entityIds.Length<=spawnCounts[binding.objectId],"spawn binding execution count");
            }
            foreach(string id in known)Require(bound.Contains(id),"missing binding");
            foreach(var group in document.groups)
            {
                var aggregate=Array.Find(state.bindings,b=>b.objectId==group.id);
                for(int i=0;i<group.objectIds.Length;i++)
                {
                    var primary=Array.Find(state.bindings,b=>b.objectId==group.objectIds[i]);
                    Require(primary!=null&&primary.entityIds.Length==1&&aggregate.entityIds[i]==primary.entityIds[0],"group binding identity");
                }
            }
            for(int i=0;i<document.objectives.Length;i++)
            {
                var objective=state.objectiveStates[i];Require(objective!=null&&objective.id==document.objectives[i].id&&
                    (objective.status=="locked"||objective.status=="active"||objective.status=="completed"||objective.status=="failed")&&
                    SnapshotCodec.Finite(objective.progress)&&objective.progress>=0&&SnapshotCodec.Finite(objective.elapsed)&&objective.elapsed>=0&&objective.elapsed<=time+.1f&&objective.reason!=null&&objective.reason.Length<=1024,"objective");
                if(objective.status=="locked")Require(objective.progress==0&&objective.elapsed==0,"locked objective clock");
                if(document.objectives[i].kind=="hold_region")
                    Require(objective.progress<=document.objectives[i].holdSeconds+.001f&&objective.progress<=objective.elapsed+.001f,"hold objective clock");
            }
            for(int i=0;i<document.triggers.Length;i++)
            {
                var trigger=state.triggerStates[i];Require(trigger!=null&&trigger.id==document.triggers[i].id&&trigger.fires>=0&&trigger.fires<=document.triggers[i].maxFires&&
                    SnapshotCodec.Finite(trigger.nextAt)&&trigger.nextAt>=0&&trigger.lastError!=null&&trigger.lastError.Length<=1024,"trigger");
            }
        }
    }
}
