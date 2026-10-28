using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Frontier
{
    public sealed partial class FrontierHud
    {
        int editorCondition069,editorAction069,editorTreasureDefinition069;
        static readonly string[] objectiveKinds069={"manual","hold_region","destroy_object","reach_region","collect_resource","embark_group","escort_group"};
        static readonly string[] conditionKinds069={"time","object_dead","units_in_region","resource","objective_complete","objective_failed","trigger_fired","units_embarked","units_lost"};
        static readonly string[] actionKinds069={"message","spawn","order_move","order_attack","order_attack_move","grant_resource","complete_objective","fail_objective","win","lose","enable_trigger","disable_trigger"};
        void BeginTradeGesture069(Vector2 p)
        {
            var doc=gestureDocument069;editorPoint069=-1;float best=5*5;
            if(editorTool069=="socket")
            {editorSelected069="";foreach(var s in doc.tradeSockets)if((s.position-p).sqrMagnitude<best){best=(s.position-p).sqrMagnitude;editorSelected069=s.id;}return;}
            if(editorTool069=="treasure")
            {editorSelected069="";foreach(var t in doc.treasures)if((t.position-p).sqrMagnitude<best){best=(t.position-p).sqrMagnitude;editorSelected069=t.id;}return;}
            editorSelected069="route";
            for(int i=0;i<doc.tradeRoute.Length;i++)if((doc.tradeRoute[i]-p).sqrMagnitude<best){best=(doc.tradeRoute[i]-p).sqrMagnitude;editorPoint069=i;}
            if(editorPoint069<0&&editorTool069=="route_append")
            {
                if(doc.tradeRoute.Length==0)doc.tradeRoute=new[]{p,new Vector2(Mathf.Clamp(p.x+8,0,128),Mathf.Clamp(p.y+8,0,128))};else doc.tradeRoute=AppendEditor069(doc.tradeRoute,p);
                editorPoint069=doc.tradeRoute.Length-1;editorGestureChanged069=true;
            }
        }
        void MoveTradePoint069(Vector2 p)
        {
            var doc=gestureDocument069;
            if(editorSelected069=="route"&&editorPoint069>=0&&editorPoint069<doc.tradeRoute.Length){if(doc.tradeRoute[editorPoint069]==p)return;doc.tradeRoute[editorPoint069]=p;editorGestureChanged069=true;return;}
            var socket=Array.Find(doc.tradeSockets,s=>s.id==editorSelected069);if(socket!=null){if(socket.position==p)return;socket.position=p;editorGestureChanged069=true;return;}
            var treasure=Array.Find(doc.treasures,t=>t.id==editorSelected069);if(treasure!=null){if(treasure.position==p)return;Vector2 delta=p-treasure.position;treasure.position=p;for(int i=0;i<treasure.guardPositions.Length;i++)treasure.guardPositions[i]+=delta;editorGestureChanged069=true;}
        }
        void TradeInspector069(ScenarioDocument doc,ref float y)
        {
            InspectorNote069("Edit the trade road and its sites. Move treasure markers\nwith their guards; rewards come from the chosen treasure type.",ref y);
            string mode=new[]{"route_move","route_append","socket","treasure"}.Contains069(editorTool069)?editorTool069:"route_move";
            InspectorChoice069("POINTER",mode,new[]{"route_move","route_append","socket","treasure"},v=>{editorTool069=v;editorSelected069="";editorPoint069=-1;RefreshEditorInspector069();},ref y);
            InspectorButton069("ADD TRADE SITE AT CURSOR",()=>EditScenario069(d=>{string id=NewEditorId069(d,"site");d.tradeSockets=AppendEditor069(d.tradeSockets,new ScenarioTradeSocket{id=id,position=editorCursor069,waypoint=0});editorSelected069=id;editorTool069="socket";}),ref y,doc.tradeRoute.Length>1&&doc.tradeSockets.Length<32);
            InspectorButton069("ADD TREASURE AT CURSOR",()=>EditScenario069(d=>{string id=NewEditorId069(d,"treasure");d.treasures=AppendEditor069(d.treasures,new ScenarioTreasure{id=id,position=editorCursor069,definition=editorTreasureDefinition069});editorSelected069=id;editorTool069="treasure";}),ref y,game.Rules.landmarks?.treasures?.Length>0&&doc.treasures.Length<32);
            if(editorSelected069=="route"&&editorPoint069>=0&&editorPoint069<doc.tradeRoute.Length)
            {
                int index=editorPoint069;InspectorInteger069("WAYPOINT",index,v=>{editorPoint069=v;RefreshEditorInspector069();RenderEditorMap069();},ref y,0,doc.tradeRoute.Length-1);
                InspectorPoint069("POSITION",doc.tradeRoute[index],p=>EditScenario069(d=>d.tradeRoute[index]=p),ref y);
                InspectorButton069("REMOVE WAYPOINT",()=>EditScenario069(d=>{d.tradeRoute=RemoveEditor069(d.tradeRoute,index);foreach(var site in d.tradeSockets){if(site.waypoint>index)site.waypoint--;else if(site.waypoint==index)site.waypoint=Mathf.Min(index,d.tradeRoute.Length-1);}editorPoint069=Mathf.Max(0,index-1);}),ref y,doc.tradeRoute.Length>2);
            }
            var socket=Array.Find(doc.tradeSockets,s=>s.id==editorSelected069);
            if(socket!=null)
            {
                InspectorTitle069(socket.id,ref y);
                InspectorInteger069("BOUND WAYPOINT",socket.waypoint,v=>EditScenario069(d=>Array.Find(d.tradeSockets,s=>s.id==editorSelected069).waypoint=v),ref y,0,doc.tradeRoute.Length-1);
                InspectorPoint069("SITE POSITION",socket.position,p=>EditScenario069(d=>Array.Find(d.tradeSockets,s=>s.id==editorSelected069).position=p),ref y);
                var binding=Array.Find(doc.neutralBindings,b=>b.socketId==socket.id);string[] factions=new string[(game.Rules.neutralAllies?.Length??0)+1];factions[0]="none";
                for(int i=1;i<factions.Length;i++)factions[i]=game.Rules.neutralAllies[i-1].id;
                InspectorChoice069("NEUTRAL ALLY",binding?.definition??"none",factions,v=>EditScenario069(d=>{var rows=new List<ScenarioNeutralBinding>();foreach(var b in d.neutralBindings)if(b.socketId!=editorSelected069)rows.Add(b);if(v!="none")rows.Add(new ScenarioNeutralBinding{definition=v,socketId=editorSelected069});d.neutralBindings=rows.ToArray();}),ref y);
                InspectorButton069("REMOVE TRADE SITE",()=>EditScenario069(d=>{d.tradeSockets=RemoveEditor069(d.tradeSockets,Array.FindIndex(d.tradeSockets,s=>s.id==editorSelected069));var rows=new List<ScenarioNeutralBinding>();foreach(var b in d.neutralBindings)if(b.socketId!=editorSelected069)rows.Add(b);d.neutralBindings=rows.ToArray();editorSelected069="";}),ref y);
            }
            var treasure=Array.Find(doc.treasures,t=>t.id==editorSelected069);
            if(treasure!=null)
            {
                InspectorTitle069(treasure.id,ref y);
                InspectorInteger069("TREASURE TYPE",treasure.definition,v=>EditScenario069(d=>Array.Find(d.treasures,t=>t.id==editorSelected069).definition=v),ref y,0,game.Rules.landmarks.treasures.Length-1);
                InspectorPoint069("POSITION",treasure.position,p=>EditScenario069(d=>{var t=Array.Find(d.treasures,e=>e.id==editorSelected069);Vector2 delta=p-t.position;t.position=p;for(int i=0;i<t.guardPositions.Length;i++)t.guardPositions[i]+=delta;}),ref y);
                InspectorButton069("ADD GUARD AT CURSOR",()=>EditScenario069(d=>{var t=Array.Find(d.treasures,e=>e.id==editorSelected069);t.guardPositions=AppendEditor069(t.guardPositions,editorCursor069);}),ref y,treasure.guardPositions.Length<32);
                InspectorButton069("REMOVE LAST GUARD",()=>EditScenario069(d=>{var t=Array.Find(d.treasures,e=>e.id==editorSelected069);t.guardPositions=RemoveEditor069(t.guardPositions,t.guardPositions.Length-1);}),ref y,treasure.guardPositions.Length>0);
                for(int i=0;i<treasure.guardPositions.Length;i++){int index=i;InspectorPoint069("GUARD "+i,treasure.guardPositions[i],p=>EditScenario069(d=>Array.Find(d.treasures,t=>t.id==editorSelected069).guardPositions[index]=p),ref y);}
                InspectorButton069("REMOVE TREASURE",()=>EditScenario069(d=>{d.treasures=RemoveEditor069(d.treasures,Array.FindIndex(d.treasures,t=>t.id==editorSelected069));editorSelected069="";}),ref y);
            }
        }
        string[] EditorEntityReferences069(ScenarioDocument doc)
        {
            var ids=new List<string>();foreach(var s in doc.starts){ids.Add("start-"+s.player+"-town");ids.Add("start-"+s.player+"-explorer");for(int i=0;i<s.workers.Length;i++)ids.Add("start-"+s.player+"-worker-"+i);}
            foreach(var e in doc.objects)ids.Add(e.id);foreach(var e in doc.resources)ids.Add(e.id);foreach(var socket in doc.tradeSockets)ids.Add(socket.id);
            foreach(var e in doc.treasures){ids.Add(e.id);for(int i=0;i<e.guardPositions.Length;i++)ids.Add(ScenarioCodec.TreasureGuardId(e.id,i));}
            foreach(var group in doc.groups)ids.Add(group.id);
            foreach(var t in doc.triggers)foreach(var a in t.actions)if(a.kind=="spawn"&&a.objectId!=""&&!ids.Contains(a.objectId))ids.Add(a.objectId);return ids.ToArray();
        }
        string[] EditorInitialUnitReferences070(ScenarioDocument doc)
        {
            var ids=new List<string>();foreach(var s in doc.starts)
            {ids.Add(ScenarioCodec.StartExplorerId(s.player));for(int i=0;i<s.workers.Length;i++)ids.Add(ScenarioCodec.StartWorkerId(s.player,i));}
            foreach(var e in doc.objects)if(game.Rules.Entity(e.definition)?.kind==EntityKind.Unit)ids.Add(e.id);
            foreach(var e in doc.treasures)for(int i=0;i<e.guardPositions.Length;i++)ids.Add(ScenarioCodec.TreasureGuardId(e.id,i));
            return ids.ToArray();
        }
        string[] EditorUnitReferences070(ScenarioDocument doc)
        {
            var ids=new List<string>(EditorInitialUnitReferences070(doc));foreach(var group in doc.groups)ids.Add(group.id);
            foreach(var t in doc.triggers)foreach(var a in t.actions)if(a.kind=="spawn"&&a.objectId!="")ids.Add(a.objectId);return ids.ToArray();
        }
        EntityDef EditorInitialUnitDefinition070(ScenarioDocument doc,string id)
        {
            foreach(var s in doc.starts)
            {if(id==ScenarioCodec.StartExplorerId(s.player))return game.Rules.Entity("explorer");for(int i=0;i<s.workers.Length;i++)if(id==ScenarioCodec.StartWorkerId(s.player,i))return game.Rules.Entity(game.Rules.Civ(s.civilization).workerUnit);}
            var item=Array.Find(doc.objects,e=>e.id==id);if(item!=null)return game.Rules.Entity(item.definition);
            foreach(var treasure in doc.treasures)for(int i=0;i<treasure.guardPositions.Length;i++)if(id==ScenarioCodec.TreasureGuardId(treasure.id,i))return game.Rules.Entity("guardian");return null;
        }
        string[] EditorMilitaryGroups070(ScenarioDocument doc)
        {
            var ids=new List<string>();foreach(var group in doc.groups)
            {
                bool eligible=group.objectIds.Length>0;foreach(string id in group.objectIds)
                {var d=EditorInitialUnitDefinition070(doc,id);if(d==null||d.kind!=EntityKind.Unit||d.waterborne||d.civilian||(d.tags&Tags.Villager)!=0){eligible=false;break;}}
                if(eligible)ids.Add(group.id);
            }
            return ids.ToArray();
        }
        string[] EditorTransportReferences070(ScenarioDocument doc)
        {
            var ids=new List<string>();foreach(var item in doc.objects)
            {var d=game.Rules.Entity(item.definition);if(d!=null&&d.waterborne&&d.transportShip&&d.transportCapacity>0)ids.Add(item.id);}return ids.ToArray();
        }
        string[] EditorOptionalReferences070(string[] ids)
        {var result=new string[ids.Length+1];result[0]="";Array.Copy(ids,0,result,1,ids.Length);return result;}
        void EditorEmbarkDefaults070(ScenarioDocument doc,out string group,out string ship)
        {
            var groups=EditorMilitaryGroups070(doc);var transports=EditorTransportReferences070(doc);
            if(groups.Length==0||transports.Length==0)throw new ArgumentException("Add an initial land military group and a transport ship on the Objects page first");
            group=groups[0];ship=transports[0];
        }
        string[] EditorRegionIds069(ScenarioDocument d){var ids=new string[d.regions.Length];for(int i=0;i<ids.Length;i++)ids[i]=d.regions[i].id;return ids;}
        string[] EditorObjectiveIds069(ScenarioDocument d){var ids=new string[d.objectives.Length];for(int i=0;i<ids.Length;i++)ids[i]=d.objectives[i].id;return ids;}
        string[] EditorTriggerIds069(ScenarioDocument d){var ids=new string[d.triggers.Length];for(int i=0;i<ids.Length;i++)ids[i]=d.triggers[i].id;return ids;}
        ScenarioObjective SelectedObjective069(ScenarioDocument d)=>Array.Find(d.objectives,o=>o.id==editorSelected069);
        void ObjectivesInspector069(ScenarioDocument doc,ref float y)
        {
            InspectorNote069("Required goals can finish the scenario while enemies remain.\nDependencies unlock goals in order; deadline 0 means no limit.",ref y);
            InspectorBool069("STANDARD ELIMINATION",doc.enableElimination,v=>EditScenario069(d=>d.enableElimination=v),ref y);
            InspectorInteger069("MISSION WINNER SLOT",doc.victoryPlayer,v=>EditScenario069(d=>d.victoryPlayer=v),ref y,0,doc.starts.Length-1);
            InspectorButton069("ADD OBJECTIVE",()=>EditScenario069(d=>{string id=NewEditorId069(d,"objective");d.objectives=AppendEditor069(d.objectives,new ScenarioObjective{id=id,title="New objective",player=editorOwner069<0?0:editorOwner069});editorSelected069=id;}),ref y,doc.objectives.Length<32);
            foreach(var objective in doc.objectives){string id=objective.id;InspectorButton069((editorSelected069==id?"[SELECTED] ":"")+objective.title+" · "+id,()=>{editorSelected069=id;RefreshEditorInspector069();},ref y);}
            var o=SelectedObjective069(doc);if(o==null)return;
            InspectorField069("TITLE",o.title,v=>EditScenario069(d=>SelectedObjective069(d).title=v),ref y,100);
            InspectorField069("DESCRIPTION",o.description,v=>EditScenario069(d=>SelectedObjective069(d).description=v),ref y,2000);
            InspectorChoice069("GOAL",o.kind,objectiveKinds069,v=>EditScenario069(d=>{var goal=SelectedObjective069(d);goal.kind=v;goal.objectId=goal.targetId=goal.regionId="";if(v=="destroy_object")goal.objectId=EditorEntityReferences069(d)[0];if(v=="hold_region"||v=="reach_region"||v=="escort_group"){if(d.regions.Length==0)throw new ArgumentException("Add a region first");goal.regionId=d.regions[0].id;}if(v=="collect_resource")goal.resource=ResourceKind.Food;if(v=="embark_group"||v=="escort_group"){EditorEmbarkDefaults070(d,out goal.objectId,out goal.targetId);goal.amount=1;}}),ref y);
            InspectorInteger069("PLAYER SLOT",o.player,v=>EditScenario069(d=>SelectedObjective069(d).player=v),ref y,0,doc.starts.Length-1);
            InspectorBool069("REQUIRED",o.required,v=>EditScenario069(d=>SelectedObjective069(d).required=v),ref y);
            if(o.kind=="destroy_object")InspectorChoice069("TARGET",o.objectId,EditorEntityReferences069(doc),v=>EditScenario069(d=>SelectedObjective069(d).objectId=v),ref y);
            if(o.kind=="hold_region"||o.kind=="reach_region"||o.kind=="escort_group")InspectorChoice069("REGION",o.regionId,EditorRegionIds069(doc),v=>EditScenario069(d=>SelectedObjective069(d).regionId=v),ref y);
            if(o.kind=="hold_region"||o.kind=="reach_region")InspectorChoice069("BOUND UNITS (empty = all)",o.objectId,EditorOptionalReferences070(EditorUnitReferences070(doc)),v=>EditScenario069(d=>SelectedObjective069(d).objectId=v),ref y);
            if(o.kind=="embark_group"||o.kind=="escort_group")
            {
                InspectorChoice069("MILITARY GROUP",o.objectId,EditorMilitaryGroups070(doc),v=>EditScenario069(d=>{var goal=SelectedObjective069(d);goal.objectId=v;goal.amount=Mathf.Min(goal.amount,Array.Find(d.groups,g=>g.id==v).objectIds.Length);}),ref y);
                InspectorChoice069("TRANSPORT SHIP",o.targetId,EditorTransportReferences070(doc),v=>EditScenario069(d=>SelectedObjective069(d).targetId=v),ref y);
            }
            if(o.kind=="collect_resource")InspectorResource069(o.resource,v=>EditScenario069(d=>SelectedObjective069(d).resource=v),ref y);
            if(o.kind=="embark_group"||o.kind=="escort_group")InspectorInteger069("SPECIFIED SOLDIER COUNT",(int)o.amount,v=>EditScenario069(d=>SelectedObjective069(d).amount=v),ref y,1,Array.Find(doc.groups,g=>g.id==o.objectId)?.objectIds.Length??128);
            else InspectorNumber069(o.kind=="collect_resource"?"RESOURCE AMOUNT":"UNIT COUNT",o.amount,v=>EditScenario069(d=>SelectedObjective069(d).amount=v),ref y,.001f,1000000);
            InspectorNumber069("HOLD TIME (seconds)",o.holdSeconds,v=>EditScenario069(d=>SelectedObjective069(d).holdSeconds=v),ref y,.1f,86400);
            InspectorNumber069("DEADLINE (seconds)",o.deadline,v=>EditScenario069(d=>SelectedObjective069(d).deadline=v),ref y,0,86400);
            InspectorField069("REQUIRES (comma IDs)",string.Join(",",o.prerequisites),v=>EditScenario069(d=>SelectedObjective069(d).prerequisites=string.IsNullOrWhiteSpace(v)?Array.Empty<string>():v.Replace(" ","").Split(',')),ref y,1000);
            InspectorButton069("REMOVE OBJECTIVE",()=>EditScenario069(d=>{d.objectives=RemoveEditor069(d.objectives,Array.FindIndex(d.objectives,o=>o.id==editorSelected069));editorSelected069="";}),ref y);
        }
        ScenarioTrigger SelectedTrigger069(ScenarioDocument d)=>Array.Find(d.triggers,t=>t.id==editorSelected069);
        void TriggersInspector069(ScenarioDocument doc,ref float y)
        {
            InspectorNote069("When conditions match, actions run in their listed order.\nA single-use trigger cannot duplicate its reward after a save.",ref y);
            InspectorButton069("ADD TRIGGER",()=>EditScenario069(d=>{string id=NewEditorId069(d,"trigger");d.triggers=AppendEditor069(d.triggers,new ScenarioTrigger{id=id,conditions=new[]{new ScenarioCondition{kind="time",time=30}},actions=new[]{new ScenarioAction{kind="message",message="Reinforcements are ready."}}});editorSelected069=id;editorCondition069=editorAction069=0;}),ref y,doc.triggers.Length<64);
            foreach(var trigger in doc.triggers){string id=trigger.id;InspectorButton069((editorSelected069==id?"[SELECTED] ":"")+id,()=>{editorSelected069=id;editorCondition069=editorAction069=0;RefreshEditorInspector069();},ref y);}
            var t=SelectedTrigger069(doc);if(t==null)return;
            InspectorBool069("ENABLED",t.enabled,v=>EditScenario069(d=>SelectedTrigger069(d).enabled=v),ref y);
            InspectorBool069("ANY CONDITION (else ALL)",t.matchAny,v=>EditScenario069(d=>SelectedTrigger069(d).matchAny=v),ref y);
            InspectorInteger069("MAX FIRES",t.maxFires,v=>EditScenario069(d=>SelectedTrigger069(d).maxFires=v),ref y,1,64);
            InspectorNumber069("REPEAT WAIT (seconds)",t.repeatSeconds,v=>EditScenario069(d=>SelectedTrigger069(d).repeatSeconds=v),ref y,.1f,86400);
            InspectorTitle069("CONDITIONS",ref y);editorCondition069=Mathf.Clamp(editorCondition069,0,t.conditions.Length-1);
            InspectorInteger069("SELECT CONDITION",editorCondition069,v=>{editorCondition069=v;RefreshEditorInspector069();},ref y,0,t.conditions.Length-1);
            InspectorButton069("ADD CONDITION",()=>EditScenario069(d=>{var selected=SelectedTrigger069(d);selected.conditions=AppendEditor069(selected.conditions,new ScenarioCondition{kind="time",time=60});editorCondition069=selected.conditions.Length-1;}),ref y,t.conditions.Length<8);
            var c=t.conditions[editorCondition069];ConditionInspector069(doc,c,ref y);
            InspectorButton069("REMOVE CONDITION",()=>EditScenario069(d=>{var selected=SelectedTrigger069(d);selected.conditions=RemoveEditor069(selected.conditions,editorCondition069);editorCondition069=0;}),ref y,t.conditions.Length>1);
            InspectorTitle069("ACTIONS (ordered)",ref y);editorAction069=Mathf.Clamp(editorAction069,0,t.actions.Length-1);
            InspectorInteger069("SELECT ACTION",editorAction069,v=>{editorAction069=v;RefreshEditorInspector069();},ref y,0,t.actions.Length-1);
            InspectorButton069("ADD ACTION",()=>EditScenario069(d=>{var selected=SelectedTrigger069(d);selected.actions=AppendEditor069(selected.actions,new ScenarioAction{kind="message",message="Objective updated."});editorAction069=selected.actions.Length-1;}),ref y,t.actions.Length<8);
            var a=t.actions[editorAction069];ActionInspector069(doc,a,ref y);
            InspectorButton069("MOVE ACTION EARLIER",()=>EditScenario069(d=>{var actions=SelectedTrigger069(d).actions;var previous=actions[editorAction069-1];actions[editorAction069-1]=actions[editorAction069];actions[editorAction069]=previous;editorAction069--;}),ref y,editorAction069>0);
            InspectorButton069("REMOVE ACTION",()=>EditScenario069(d=>{var selected=SelectedTrigger069(d);selected.actions=RemoveEditor069(selected.actions,editorAction069);editorAction069=0;}),ref y,t.actions.Length>1);
            InspectorButton069("REMOVE TRIGGER",()=>EditScenario069(d=>{d.triggers=RemoveEditor069(d.triggers,Array.FindIndex(d.triggers,t=>t.id==editorSelected069));editorSelected069="";}),ref y);
        }
        void EditCondition069(Action<ScenarioCondition> change)=>EditScenario069(d=>change(SelectedTrigger069(d).conditions[editorCondition069]));
        void EditAction069(Action<ScenarioAction> change)=>EditScenario069(d=>change(SelectedTrigger069(d).actions[editorAction069]));
        void ConditionInspector069(ScenarioDocument doc,ScenarioCondition c,ref float y)
        {
            InspectorChoice069("WHEN",c.kind,conditionKinds069,kind=>EditScenario069(d=>
            {
                var x=SelectedTrigger069(d).conditions[editorCondition069];x.kind=kind;x.objectId=x.targetId=x.regionId=x.objectiveId=x.triggerId="";
                if(kind=="object_dead")x.objectId=EditorEntityReferences069(d)[0];
                if(kind=="units_in_region"){if(d.regions.Length==0)throw new ArgumentException("Add a region first");x.regionId=d.regions[0].id;x.amount=1;}
                if(kind=="objective_complete"||kind=="objective_failed"){if(d.objectives.Length==0)throw new ArgumentException("Add an objective first");x.objectiveId=d.objectives[0].id;}
                if(kind=="trigger_fired"){x.triggerId=d.triggers[0].id;x.amount=1;}
                if(kind=="resource")x.resource=ResourceKind.Food;
                if(kind=="units_embarked"){EditorEmbarkDefaults070(d,out x.objectId,out x.targetId);x.amount=1;}
                if(kind=="units_lost"){x.objectId=EditorUnitReferences070(d)[0];x.amount=1;}
            }),ref y);
            InspectorInteger069("PLAYER",c.player,v=>EditCondition069(x=>x.player=v),ref y,0,doc.starts.Length-1);
            if(c.kind=="time")InspectorNumber069("TIME (seconds)",c.time,v=>EditCondition069(x=>x.time=v),ref y,0,86400);
            if(c.kind=="object_dead")InspectorChoice069("OBJECT",c.objectId,EditorEntityReferences069(doc),v=>EditCondition069(x=>x.objectId=v),ref y);
            if(c.kind=="units_in_region")InspectorChoice069("REGION",c.regionId,EditorRegionIds069(doc),v=>EditCondition069(x=>x.regionId=v),ref y);
            if(c.kind=="units_in_region")InspectorChoice069("BOUND UNITS (empty = all)",c.objectId,EditorOptionalReferences070(EditorUnitReferences070(doc)),v=>EditCondition069(x=>x.objectId=v),ref y);
            if(c.kind=="units_embarked")
            {
                InspectorChoice069("MILITARY GROUP",c.objectId,EditorMilitaryGroups070(doc),v=>EditScenario069(d=>{var x=SelectedTrigger069(d).conditions[editorCondition069];x.objectId=v;x.amount=Mathf.Min(x.amount,Array.Find(d.groups,g=>g.id==v).objectIds.Length);}),ref y);
                InspectorChoice069("TRANSPORT SHIP",c.targetId,EditorTransportReferences070(doc),v=>EditCondition069(x=>x.targetId=v),ref y);
            }
            if(c.kind=="units_lost")InspectorChoice069("SPECIFIED UNITS",c.objectId,EditorUnitReferences070(doc),v=>EditCondition069(x=>x.objectId=v),ref y);
            if(c.kind=="resource")InspectorResource069(c.resource,v=>EditCondition069(x=>x.resource=v),ref y);
            if(c.kind=="objective_complete"||c.kind=="objective_failed")InspectorChoice069("OBJECTIVE",c.objectiveId,EditorObjectiveIds069(doc),v=>EditCondition069(x=>x.objectiveId=v),ref y);
            if(c.kind=="trigger_fired")InspectorChoice069("TRIGGER",c.triggerId,EditorTriggerIds069(doc),v=>EditCondition069(x=>x.triggerId=v),ref y);
            if(c.kind=="resource"||c.kind=="units_in_region"||c.kind=="trigger_fired")InspectorNumber069("THRESHOLD",c.amount,v=>EditCondition069(x=>x.amount=v),ref y,1,c.kind=="trigger_fired"?64:1000000);
            if(c.kind=="units_embarked"||c.kind=="units_lost")InspectorInteger069("MEMBER COUNT",(int)c.amount,v=>EditCondition069(x=>x.amount=v),ref y,1,c.kind=="units_embarked"?(Array.Find(doc.groups,g=>g.id==c.objectId)?.objectIds.Length??128):4096);
        }
        void ActionInspector069(ScenarioDocument doc,ScenarioAction a,ref float y)
        {
            InspectorChoice069("THEN",a.kind,actionKinds069,kind=>EditScenario069(d=>
            {
                var x=SelectedTrigger069(d).actions[editorAction069];x.kind=kind;x.objectiveId=x.regionId=x.objectId=x.targetId=x.triggerId=x.definition="";
                if(kind=="message")x.message="Objective updated.";
                if(kind=="spawn"){x.definition=game.Rules.Civ(d.starts[x.player].civilization).workerUnit;x.objectId=NewEditorId069(d,"army");x.point=editorCursor069;}
                if(kind=="order_move"||kind=="order_attack"||kind=="order_attack_move"){x.objectId=EditorUnitReferences070(d)[0];x.point=editorCursor069;if(kind=="order_attack")x.targetId="start-1-town";}
                if(kind=="grant_resource"){x.resource=ResourceKind.Food;x.amount=100;}
                if(kind=="complete_objective"||kind=="fail_objective"){if(d.objectives.Length==0)throw new ArgumentException("Add an objective first");x.objectiveId=d.objectives[0].id;}
                if(kind=="enable_trigger"||kind=="disable_trigger")x.triggerId=d.triggers[0].id;
            }),ref y);
            InspectorInteger069("PLAYER",a.player,v=>EditAction069(x=>x.player=v),ref y,0,doc.starts.Length-1);
            if(a.kind=="message")InspectorField069("MESSAGE",a.message,v=>EditAction069(x=>x.message=v),ref y,1024);
            if(a.kind=="spawn")
            {InspectorField069("UNIT CATALOG ID",a.definition,v=>EditAction069(x=>x.definition=v),ref y,64);InspectorField069("NEW GROUP ID",a.objectId,v=>EditAction069(x=>x.objectId=v),ref y,64);InspectorInteger069("COUNT",a.count,v=>EditAction069(x=>x.count=v),ref y,1,64);}
            if(a.kind=="order_move"||a.kind=="order_attack"||a.kind=="order_attack_move")InspectorChoice069("ACTOR GROUP",a.objectId,EditorUnitReferences070(doc),v=>EditAction069(x=>x.objectId=v),ref y);
            if(a.kind=="order_attack")InspectorChoice069("TARGET OBJECT",a.targetId,EditorEntityReferences069(doc),v=>EditAction069(x=>x.targetId=v),ref y);
            if(a.kind=="spawn"||a.kind=="order_move"||a.kind=="order_attack_move")InspectorPoint069("DESTINATION",a.point,p=>EditAction069(x=>x.point=p),ref y);
            if(a.kind=="grant_resource"){InspectorResource069(a.resource,v=>EditAction069(x=>x.resource=v),ref y);InspectorNumber069("AMOUNT",a.amount,v=>EditAction069(x=>x.amount=v),ref y,.001f,1000000);}
            if(a.kind=="complete_objective"||a.kind=="fail_objective")InspectorChoice069("OBJECTIVE",a.objectiveId,EditorObjectiveIds069(doc),v=>EditAction069(x=>x.objectiveId=v),ref y);
            if(a.kind=="enable_trigger"||a.kind=="disable_trigger")InspectorChoice069("TRIGGER",a.triggerId,EditorTriggerIds069(doc),v=>EditAction069(x=>x.triggerId=v),ref y);
        }
        string ScenarioObjectiveSummary069()
        {
            var doc=game.World.ScenarioDocument;var states=game.World.ScenarioObjectiveStates;var text=new StringBuilder();int count=0;
            foreach(var state in states)
            {var definition=Array.Find(doc.objectives,o=>o.id==state.id);if(definition==null||definition.player!=game.ObserverPlayer||state.status=="locked")continue;
                if(count++>=2){text.Append(" + more objectives");break;}if(text.Length>0)text.Append('\n');text.Append(definition.title).Append(" · ").Append(state.status.ToUpperInvariant()).Append(" · ").Append(state.progress.ToString("0.#"));}
            return text.ToString();
        }
    }
}
