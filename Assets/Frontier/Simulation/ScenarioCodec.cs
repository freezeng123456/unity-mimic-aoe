using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Frontier
{
    public static class ScenarioCodec
    {
        public const int MaxBytes=2*1024*1024,MaxPlacements=2048,MaxRivers=8,MaxRiverPoints=512;
        public const int MaxObjectives=32,MaxTriggers=64,MaxSpawnBudget=4096,MaxGroups=64,MaxGroupMembers=128;
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        static readonly HashSet<string> ObjectiveKinds=Set("manual","hold_region","destroy_object","reach_region","collect_resource","embark_group","escort_group");
        static readonly HashSet<string> ConditionKinds=Set("time","object_dead","units_in_region","resource","objective_complete","objective_failed","trigger_fired","units_embarked","units_lost");
        static readonly HashSet<string> ActionKinds=Set("message","spawn","order_move","order_attack","order_attack_move","grant_resource","complete_objective","fail_objective","win","lose","enable_trigger","disable_trigger");
        static readonly HashSet<string> LegacyObjectiveKinds=Set("manual","hold_region","destroy_object","reach_region","collect_resource");
        static readonly HashSet<string> LegacyConditionKinds=Set("time","object_dead","units_in_region","resource","objective_complete","objective_failed","trigger_fired");
        static readonly HashSet<string> LegacyActionKinds=Set("message","spawn","order_move","order_attack","grant_resource","complete_objective","fail_objective","win","lose","enable_trigger","disable_trigger");
        static HashSet<string> Set(params string[] values)=>new HashSet<string>(values,StringComparer.Ordinal);
        public static ScenarioDocument Clone(ScenarioDocument document)
        {if(document==null)throw new ArgumentNullException(nameof(document));return JsonUtility.FromJson<ScenarioDocument>(JsonUtility.ToJson(document));}
        public static string ContentHash(ScenarioDocument document)
        {if(document==null)throw new ArgumentNullException(nameof(document));return SnapshotCodec.Hash(JsonUtility.ToJson(document));}
        public static string Encode(ScenarioDocument document,RuleCatalog rules)
        {
            string reason=Validate(document,rules);if(reason!="")throw new ArgumentException(reason);
            string payload=JsonUtility.ToJson(document);
            string json=JsonUtility.ToJson(new ScenarioEnvelope{payload=payload,sha256=SnapshotCodec.Hash(payload)});
            CheckSize(json);return json;
        }
        public static ScenarioDocument Decode(string json,RuleCatalog rules)
        {
            try
            {
                CheckSize(json);ReplayJson.Validate<ScenarioEnvelope>(json);
                var envelope=JsonUtility.FromJson<ScenarioEnvelope>(json);
                if(envelope.schema!=1||envelope.encoding!="frontier-scenario-json-v1"||!IsHash(envelope.sha256)||
                    string.IsNullOrEmpty(envelope.payload)||SnapshotCodec.Hash(envelope.payload)!=envelope.sha256)
                    throw new InvalidDataException("Scenario file format or content hash is damaged");
                CheckSize(envelope.payload);
                bool legacy=false;
                try{ReplayJson.Validate<ScenarioDocument>(envelope.payload);}
                catch(FormatException)
                {
                    // The old envelope checksum is checked before any migration.
                    // Only the complete original DTO is accepted here; missing
                    // old fields, unknown keys and mixed old/new shapes still fail.
                    ReplayJson.Validate<LegacyDocument069>(envelope.payload);legacy=true;
                }
                var document=JsonUtility.FromJson<ScenarioDocument>(envelope.payload);
                if(legacy)
                {
                    document.groups=Array.Empty<ScenarioGroup>();
                    foreach(var objective in document.objectives??Array.Empty<ScenarioObjective>())if(objective!=null)
                    {
                        if(!LegacyObjectiveKinds.Contains(objective.kind))throw new InvalidDataException("Legacy scenario objective kind is unsupported");
                        objective.targetId="";
                    }
                    foreach(var trigger in document.triggers??Array.Empty<ScenarioTrigger>())if(trigger!=null)
                    {
                        foreach(var condition in trigger.conditions??Array.Empty<ScenarioCondition>())if(condition!=null)
                        {
                            if(!LegacyConditionKinds.Contains(condition.kind))throw new InvalidDataException("Legacy scenario condition kind is unsupported");
                            condition.targetId="";
                        }
                        foreach(var action in trigger.actions??Array.Empty<ScenarioAction>())if(action!=null&&!LegacyActionKinds.Contains(action.kind))
                            throw new InvalidDataException("Legacy scenario action kind is unsupported");
                    }
                }
                string reason=Validate(document,rules);if(reason!="")throw new InvalidDataException(reason);
                return document;
            }
            catch(Exception error)when(error is ArgumentException||error is FormatException)
            {throw new InvalidDataException("Scenario JSON is incomplete or invalid: "+error.Message,error);}
        }
        // Strict legacy shape for portable 0.12 imports. This is deliberately
        // separate from current fields rather than making the JSON reader
        // accept arbitrary missing members. Encoding always writes the new shape.
        [Serializable] sealed class LegacyDocument069
        {
            public int schema,width,height,seed,waterHeightCm,victoryPlayer;
            public string id,name,description,rulesHash;
            public bool enableElimination;
            public int[] heightsCm,surfaces;
            public ScenarioRiver[] rivers;
            public ScenarioBridge[] bridges;
            public ScenarioStart[] starts;
            public ScenarioPlacement[] resources,objects;
            public Vector2[] tradeRoute;
            public ScenarioTradeSocket[] tradeSockets;
            public ScenarioTreasure[] treasures;
            public ScenarioNeutralBinding[] neutralBindings;
            public ScenarioRegion[] regions;
            public LegacyObjective069[] objectives;
            public LegacyTrigger069[] triggers;
        }
        [Serializable] sealed class LegacyObjective069
        {
            public string id,title,description,kind,objectId,regionId;
            public int player;
            public bool required;
            public ResourceKind resource;
            public float amount,holdSeconds,deadline;
            public string[] prerequisites;
        }
        [Serializable] sealed class LegacyTrigger069
        {
            public string id;
            public bool enabled,matchAny;
            public int maxFires;
            public float repeatSeconds;
            public LegacyCondition069[] conditions;
            public ScenarioAction[] actions;
        }
        [Serializable] sealed class LegacyCondition069
        {
            public string kind,objectId,regionId,objectiveId,triggerId;
            public int player;
            public ResourceKind resource;
            public float amount,time;
        }
        static void CheckSize(string text)
        {
            if(text==null||text.Length>MaxBytes||Utf8.GetByteCount(text)>MaxBytes)throw new InvalidDataException("Scenario file exceeds the 2 MiB UTF-8 limit or is missing");
        }
        public static bool IsIdentifier(string value)
        {
            if(string.IsNullOrEmpty(value)||value.Length>64)return false;
            foreach(char c in value)if(!(c>='a'&&c<='z'||c>='0'&&c<='9'||c=='_'||c=='-'))return false;
            return true;
        }
        static bool IsHash(string value)
        {if(value==null||value.Length!=64)return false;foreach(char c in value)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))return false;return true;}
        static void Require(bool condition,string reason){if(!condition)throw new ArgumentException("Invalid scenario: "+reason);}
        static bool Text(string value,int maximum,bool nonempty=false)
        {
            if(value==null||value.Length>maximum||nonempty&&string.IsNullOrWhiteSpace(value))return false;
            try{Utf8.GetByteCount(value);}catch(EncoderFallbackException){return false;}
            foreach(char c in value)if(c=='\0'||char.IsControl(c)&&c!='\n'&&c!='\r'&&c!='\t')return false;
            return true;
        }
        static bool Number(float value,float min,float max)=>SnapshotCodec.Finite(value)&&value>=min&&value<=max;
        static bool Point(Vector2 point,float min=0,float max=128)=>Number(point.x,min,max)&&Number(point.y,min,max);
        static bool Owner(int player,int count,bool neutral=false)=>player>=(neutral?-1:0)&&player<count;
        static bool Resource(ResourceKind resource)=>resource==ResourceKind.Food||resource==ResourceKind.Wood||resource==ResourceKind.Coin;
        static bool Nonnegative(Cost cost)=>Number(cost.food,0,1000000)&&Number(cost.wood,0,1000000)&&Number(cost.coin,0,1000000);
        public static string StartTownId(int player)=>"start-"+player+"-town";
        public static string StartExplorerId(int player)=>"start-"+player+"-explorer";
        public static string StartWorkerId(int player,int index)=>"start-"+player+"-worker-"+index;
        public static string TreasureGuardId(string id,int index)=>id+"-guard-"+index;
        public static string Validate(ScenarioDocument document,RuleCatalog rules)
        {
            try{ValidateOwned(document,rules);return "";}
            catch(Exception error)when(error is ArgumentException||error is InvalidDataException||error is FormatException)
            {return error.Message;}
        }
        static void ValidateOwned(ScenarioDocument d,RuleCatalog rules)
        {
            Require(d!=null&&rules!=null,"document and rule catalog are required");
            Require(d.schema==ScenarioDocument.CurrentSchema&&d.width==128&&d.height==128,"unsupported format or map dimensions");
            Require(IsIdentifier(d.id)&&Text(d.name,100,true)&&Text(d.description,2000),"document ID/name/description");
            Require(IsHash(d.rulesHash)&&d.rulesHash==SnapshotCodec.RulesHash(rules),"rule catalog changed; use the original game rules");
            Require(d.heightsCm?.Length==129*129&&d.surfaces?.Length==128*128,"terrain array dimensions");
            foreach(int v in d.heightsCm)Require(v>=-2000&&v<=10000,"terrain height range (-2000..10000 cm)");
            foreach(int v in d.surfaces)Require(v>=0&&v<=3,"surface must be grass/sand/mud/rock");
            Require(d.waterHeightCm>=-2000&&d.waterHeightCm<=10000,"water height range");
            Require(d.starts!=null&&d.starts.Length>=2&&d.starts.Length<=4,"requires 2–4 explicit player slots");
            int players=d.starts.Length;Require(Owner(d.victoryPlayer,players),"victory player");
            var ids=Set();var entities=Set();var regions=Set();var sockets=Set();var objectiveIds=Set();var triggerIds=Set();
            var initialUnits=new Dictionary<string,EntityDef>(StringComparer.Ordinal);
            void Add(string id,string kind,bool entity=false)
            {Require(IsIdentifier(id)&&ids.Add(id),"duplicate or invalid "+kind+" ID "+id);if(entity)entities.Add(id);}
            for(int i=0;i<players;i++)
            {
                var s=d.starts[i];Require(s!=null&&s.player==i,"player slots must be ordered and continuous");
                Require(s.team>=0&&s.team<=3&&rules.Civ(s.civilization)!=null&&rules.Age(s.age)!=null&&Nonnegative(s.initialResources),"player "+i+" civilization/team/era/resources");
                Require(Point(s.town)&&Point(s.explorer)&&s.workers!=null&&s.workers.Length<=99,"player "+i+" starting positions");
                Add(StartTownId(i),"starting town",true);Add(StartExplorerId(i),"starting explorer",true);
                initialUnits.Add(StartExplorerId(i),rules.Entity("explorer"));
                for(int w=0;w<s.workers.Length;w++)
                {Require(Point(s.workers[w]),"starting worker position");Add(StartWorkerId(i,w),"starting worker",true);initialUnits.Add(StartWorkerId(i,w),rules.Entity(rules.Civ(s.civilization).workerUnit));}
            }
            Require(d.rivers!=null&&d.rivers.Length<=MaxRivers&&d.bridges!=null&&d.bridges.Length<=32,"river/bridge count");
            int points=0;
            foreach(var r in d.rivers)
            {
                Require(r!=null,"null river");Add(r.id,"river");
                Require(r.points!=null&&r.halfWidths!=null&&r.points.Length>=2&&r.points.Length==r.halfWidths.Length,"river controls and widths must match");
                points+=r.points.Length;Require(points<=MaxRiverPoints,"total river point limit");
                for(int i=0;i<r.points.Length;i++)
                {Require(Point(r.points[i],-32,160)&&Number(r.halfWidths[i],1,48),"river coordinate or width range");if(i>0)Require((r.points[i]-r.points[i-1]).sqrMagnitude>=.0001f,"consecutive river controls must differ");}
            }
            foreach(var b in d.bridges)
            {
                Require(b!=null,"null bridge");Add(b.id,"bridge");
                Require(Point(b.center)&&Point(b.direction,-1.001f,1.001f)&&Number(b.direction.sqrMagnitude,.999f,1.001f)&&
                    Number(b.halfLength,1,64)&&Number(b.halfWidth,1.01f,16)&&Number(b.rampLength,.5f,32)&&Number(b.height,.01f,100),"bridge geometry");
            }
            Require(d.resources!=null&&d.objects!=null&&d.resources.Length+d.objects.Length<=MaxPlacements,"resource/object count");
            void Placements(ScenarioPlacement[] values,bool resources)
            {
                foreach(var p in values)
                {
                    Require(p!=null,"null placement");Add(p.id,"placement",true);var definition=rules.Entity(p.definition);
                    Require(definition!=null&&(resources?definition.kind==EntityKind.Resource:definition.kind==EntityKind.Unit||definition.kind==EntityKind.Building),"placement catalog kind "+p.definition);
                    Require(Point(p.position)&&Owner(p.owner,players,true)&&Number(p.remaining,-1,1000000)&&(p.remaining==-1||p.remaining>=0),"placement position/owner/remaining");
                    Require(resources?p.complete:true,"resources must be complete");
                    Require(!resources||!string.IsNullOrEmpty(definition.id),"resource definition");
                    if(definition.kind==EntityKind.Unit)initialUnits.Add(p.id,definition);
                }
            }
            Placements(d.resources,true);Placements(d.objects,false);
            Require(d.tradeRoute!=null&&d.tradeRoute.Length<=512&&(d.tradeRoute.Length==0||d.tradeRoute.Length>=2),"trade route needs 0 or 2–512 points");
            for(int i=0;i<d.tradeRoute.Length;i++)
            {Require(Point(d.tradeRoute[i]),"trade route coordinate");if(i>0)Require((d.tradeRoute[i]-d.tradeRoute[i-1]).sqrMagnitude>=.0001f,"consecutive trade waypoints must differ");}
            Require(d.tradeSockets!=null&&d.tradeSockets.Length<=32,"trade socket count");
            foreach(var s in d.tradeSockets)
            {
                Require(s!=null,"null trade socket");Add(s.id,"trade socket",true);sockets.Add(s.id);
                Require(Point(s.position)&&s.waypoint>=0&&s.waypoint<d.tradeRoute.Length,"trade socket position/waypoint");
            }
            Require(d.treasures!=null&&d.treasures.Length<=32,"treasure count");
            foreach(var t in d.treasures)
            {
                Require(t!=null,"null treasure");Add(t.id,"treasure",true);
                Require(t.definition>=0&&t.definition<(rules.landmarks?.treasures?.Length??0)&&Point(t.position)&&t.guardPositions!=null&&t.guardPositions.Length<=32,"treasure definition/guards/position");
                for(int i=0;i<t.guardPositions.Length;i++)
                {Require(Point(t.guardPositions[i]),"treasure guard position");string id=TreasureGuardId(t.id,i);Add(id,"treasure guard",true);initialUnits.Add(id,rules.Entity("guardian"));}
            }
            Require(d.groups!=null&&d.groups.Length<=MaxGroups,"unit group count (64)");
            var groups=new Dictionary<string,ScenarioGroup>(StringComparer.Ordinal);
            foreach(var group in d.groups)
            {
                Require(group!=null,"null unit group");Add(group.id,"unit group",true);
                Require(group.objectIds!=null&&group.objectIds.Length>=1&&group.objectIds.Length<=MaxGroupMembers,"unit group needs 1–128 initial members");
                var members=Set();foreach(string id in group.objectIds)
                    Require(id!=null&&initialUnits.ContainsKey(id)&&members.Add(id),"unit group must reference unique initial units: "+id);
                groups.Add(group.id,group);
            }
            var unitReferences=Set();foreach(string id in initialUnits.Keys)unitReferences.Add(id);foreach(string id in groups.Keys)unitReferences.Add(id);
            Require(d.neutralBindings!=null&&d.neutralBindings.Length<=32,"neutral binding count");var neutralIds=Set();var neutralSockets=Set();
            foreach(var n in d.neutralBindings)
            {
                Require(n!=null&&Text(n.definition,64,true)&&Array.Exists(rules.neutralAllies??Array.Empty<NeutralAllyDef>(),x=>x!=null&&x.id==n.definition),"unknown neutral faction");
                Require(sockets.Contains(n.socketId)&&neutralIds.Add(n.definition)&&neutralSockets.Add(n.socketId),"neutral binding requires unique faction/socket");
            }
            Require(d.regions!=null&&d.regions.Length<=64,"region count");
            foreach(var r in d.regions)
            {
                Require(r!=null,"null region");Add(r.id,"region");regions.Add(r.id);
                Require(Point(r.center)&&Number(r.size.x,.1f,128)&&Number(r.size.y,.1f,128)&&Point(r.center-r.size*.5f)&&Point(r.center+r.size*.5f),"region rectangle must fit the map");
            }
            Require(d.objectives!=null&&d.objectives.Length<=MaxObjectives&&d.triggers!=null&&d.triggers.Length<=MaxTriggers,"objective/trigger count");
            foreach(var o in d.objectives){Require(o!=null,"null objective");Add(o.id,"objective");objectiveIds.Add(o.id);}
            foreach(var t in d.triggers){Require(t!=null,"null trigger");Add(t.id,"trigger");triggerIds.Add(t.id);}
            int spawnBudget=0;
            foreach(var t in d.triggers)
            {
                Require(t.conditions!=null&&t.conditions.Length>=1&&t.conditions.Length<=8&&t.actions!=null&&t.actions.Length>=1&&t.actions.Length<=8&&t.maxFires>=1&&t.maxFires<=64&&Number(t.repeatSeconds,.1f,86400),"trigger limits");
                foreach(var a in t.actions)
                {
                    Require(a!=null&&ActionKinds.Contains(a.kind),"unknown trigger action");
                    if(a.kind!="spawn")continue;
                    Require(a.count>=1&&a.count<=64,"spawn count");spawnBudget+=a.count*t.maxFires;Require(spawnBudget<=MaxSpawnBudget,"total trigger spawn budget (4096)");
                    if(a.objectId!=""){Add(a.objectId,"spawn group",true);unitReferences.Add(a.objectId);}
                }
            }
            void Reference(string id,HashSet<string> known,string purpose,bool required=false)
            {Require(id!=null&&(id==""?!required:known.Contains(id)),purpose+" reference "+id);}
            void TransportGroup(string objectId,string targetId,float amount)
            {
                ScenarioGroup group=null;EntityDef ship=null;
                Require(objectId!=null&&groups.TryGetValue(objectId,out group),"embarkation requires an initial unit group");
                Require(targetId!=null&&initialUnits.TryGetValue(targetId,out ship)&&ship.waterborne&&ship.transportShip&&ship.transportCapacity>0,"embarkation target must be one initial transport ship");
                Require(amount>=1&&amount<=group.objectIds.Length&&amount==Mathf.Floor(amount),"embarkation threshold must be a possible integer member count");
                foreach(string id in group.objectIds)
                {
                    var member=initialUnits[id];
                    Require(member.kind==EntityKind.Unit&&!member.waterborne&&!member.civilian&&(member.tags&Tags.Villager)==0,"embarkation group must contain only initial land military units");
                }
            }
            foreach(var o in d.objectives)
            {
                Require(Text(o.title,100,true)&&Text(o.description,2000)&&ObjectiveKinds.Contains(o.kind)&&Owner(o.player,players)&&Enum.IsDefined(typeof(ResourceKind),o.resource),"objective title/kind/player/resource");
                Require(Number(o.amount,.001f,1000000)&&Number(o.holdSeconds,.1f,86400)&&Number(o.deadline,0,86400)&&o.prerequisites!=null&&o.prerequisites.Length<=MaxObjectives,"objective requirements");
                Reference(o.objectId,entities,"objective object",o.kind=="destroy_object"||o.kind=="embark_group"||o.kind=="escort_group");Reference(o.regionId,regions,"objective region",o.kind=="hold_region"||o.kind=="reach_region"||o.kind=="escort_group");
                Reference(o.targetId,entities,"objective target",o.kind=="embark_group"||o.kind=="escort_group");
                if(o.kind=="embark_group"||o.kind=="escort_group")TransportGroup(o.objectId,o.targetId,o.amount);
                Require(o.kind!="collect_resource"||Resource(o.resource),"collect objective resource");
                var seen=Set();foreach(string id in o.prerequisites){Reference(id,objectiveIds,"objective prerequisite",true);Require(id!=o.id&&seen.Add(id),"self/duplicate objective prerequisite");}
            }
            var visiting=Set();var visited=Set();var lookup=new Dictionary<string,ScenarioObjective>(StringComparer.Ordinal);foreach(var o in d.objectives)lookup.Add(o.id,o);
            void Visit(string id)
            {if(visited.Contains(id))return;Require(visiting.Add(id),"objective prerequisite cycle");foreach(string dependency in lookup[id].prerequisites)Visit(dependency);visiting.Remove(id);visited.Add(id);}
            foreach(var o in d.objectives)Visit(o.id);
            foreach(var t in d.triggers)
            {
                foreach(var c in t.conditions)
                {
                    Require(c!=null&&ConditionKinds.Contains(c.kind)&&Owner(c.player,players)&&Enum.IsDefined(typeof(ResourceKind),c.resource)&&Number(c.amount,0,1000000)&&Number(c.time,0,86400),"trigger condition values");
                    Reference(c.objectId,entities,"condition object",c.kind=="object_dead"||c.kind=="units_embarked"||c.kind=="units_lost");Reference(c.regionId,regions,"condition region",c.kind=="units_in_region");
                    Reference(c.targetId,entities,"condition target",c.kind=="units_embarked");
                    if(c.kind=="units_embarked")TransportGroup(c.objectId,c.targetId,c.amount);
                    if(c.kind=="units_lost")Require(unitReferences.Contains(c.objectId)&&c.amount>=1&&c.amount<=4096&&c.amount==Mathf.Floor(c.amount),"unit loss threshold must reference units and be an integer member count");
                    Reference(c.objectiveId,objectiveIds,"condition objective",c.kind=="objective_complete"||c.kind=="objective_failed");Reference(c.triggerId,triggerIds,"condition trigger",c.kind=="trigger_fired");
                    Require(c.kind!="resource"||Resource(c.resource),"resource condition type");
                    Require(c.kind!="units_in_region"||c.amount>=1&&c.amount<=4096&&c.amount==Mathf.Floor(c.amount),"region unit count");
                    Require(c.kind!="trigger_fired"||c.amount>=1&&c.amount<=64&&c.amount==Mathf.Floor(c.amount),"trigger fired count");
                }
                foreach(var a in t.actions)
                {
                    Require(Owner(a.player,players)&&a.count>=1&&a.count<=64&&Point(a.point)&&Enum.IsDefined(typeof(ResourceKind),a.resource)&&Number(a.amount,0,1000000)&&Text(a.message,1024),"trigger action values");
                    Reference(a.objectiveId,objectiveIds,"action objective",a.kind=="complete_objective"||a.kind=="fail_objective");Reference(a.regionId,regions,"action region");
                    Reference(a.objectId,entities,"action object",a.kind=="order_move"||a.kind=="order_attack"||a.kind=="order_attack_move");Reference(a.targetId,entities,"action target",a.kind=="order_attack");
                    Reference(a.triggerId,triggerIds,"action trigger",a.kind=="enable_trigger"||a.kind=="disable_trigger");
                    Require(a.definition!=null&&(a.definition==""||rules.Entity(a.definition)!=null),"action definition");
                    if(a.kind=="spawn")Require(rules.Entity(a.definition)?.kind==EntityKind.Unit,"spawn needs a catalog unit");
                    if(a.kind=="grant_resource")Require(Resource(a.resource)&&a.amount>0,"resource grant type/amount");
                    if(a.kind=="message")Require(Text(a.message,1024,true),"message action needs text");
                }
            }
            string payload=JsonUtility.ToJson(d);CheckSize(payload);
            CheckSize(JsonUtility.ToJson(new ScenarioEnvelope{payload=payload,sha256=SnapshotCodec.Hash(payload)}));
        }
    }
}
