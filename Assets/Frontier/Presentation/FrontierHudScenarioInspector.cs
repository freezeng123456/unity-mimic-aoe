using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier
{
    public sealed partial class FrontierHud
    {
        void RefreshEditorInspector069()
        {
            if(editorInspector069==null||game.ScenarioEditor==null)return;
            var previousScroll=editorInspector069.GetComponent<ScrollRect>();float oldScroll=previousScroll?.verticalNormalizedPosition??1;
            ClearChildren(editorInspector069);
            var viewport=Rect(editorInspector069,"Inspector viewport",new Rect(8,8,620,662));viewport.gameObject.AddComponent<Image>().color=panelColor;viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            editorScrollContent069=Rect(viewport,"Inspector controls",new Rect(0,0,598,662));
            var scroll=editorInspector069.gameObject.GetComponent<ScrollRect>()??editorInspector069.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=viewport;scroll.content=editorScrollContent069;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;scroll.verticalNormalizedPosition=oldScroll;
            float y=12;var doc=game.ScenarioEditor.Document;
            InspectorTitle069(editorPageTitles069[Array.IndexOf(editorPages069,editorPage069)],ref y);
            if(editorPage069=="terrain")TerrainInspector069(doc,ref y);
            else if(editorPage069=="rivers")RiverInspector069(doc,ref y);
            else if(editorPage069=="bridges")BridgeInspector069(doc,ref y);
            else if(editorPage069=="objects")ObjectsInspector069(doc,ref y);
            else if(editorPage069=="players")PlayersInspector069(doc,ref y);
            else if(editorPage069=="regions")RegionsInspector069(doc,ref y);
            else if(editorPage069=="trade")TradeInspector069(doc,ref y);
            else if(editorPage069=="objectives")ObjectivesInspector069(doc,ref y);
            else TriggersInspector069(doc,ref y);
            editorScrollContent069.sizeDelta=new Vector2(598,Mathf.Max(662,y+20));Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=oldScroll;
        }
        void InspectorTitle069(string title,ref float y)
        {Label(editorScrollContent069,title,new Rect(12,y,572,34),22,gold);y+=42;}
        void InspectorNote069(string text,ref float y,int lines=2)
        {Label(editorScrollContent069,text,new Rect(12,y,572,lines*23),16,muted);y+=lines*23+8;}
        void InspectorButton069(string title,Action action,ref float y,bool enabled=true)
        {Button(editorScrollContent069,title,new Rect(12,y,572,40),action,"",enabled);y+=48;}
        void InspectorField069(string title,string value,Action<string> apply,ref float y,int limit=100)
        {
            Label(editorScrollContent069,title,new Rect(12,y+9,205,36),16,muted);
            var input=EditorInput069(editorScrollContent069,title,value,new Rect(222,y,354,40),limit);
            input.onEndEdit.AddListener(v=>{if(v!=value)apply(v);});y+=48;
        }
        void InspectorNumber069(string title,float value,Action<float> apply,ref float y,float minimum=-100000,float maximum=100000)
        {
            InspectorField069(title,value.ToString("0.###",CultureInfo.InvariantCulture),v=>
            {if(float.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out float n)&&SnapshotCodec.Finite(n)&&n>=minimum&&n<=maximum)apply(n);else {game.EditorMessage(title+": enter a number from "+minimum+" to "+maximum);RefreshEditorInspector069();}},ref y,20);
        }
        void InspectorInteger069(string title,int value,Action<int> apply,ref float y,int minimum=0,int maximum=100000)
        {
            InspectorField069(title,value.ToString(CultureInfo.InvariantCulture),v=>
            {if(int.TryParse(v,NumberStyles.Integer,CultureInfo.InvariantCulture,out int n)&&n>=minimum&&n<=maximum)apply(n);else {game.EditorMessage(title+": enter a whole number from "+minimum+" to "+maximum);RefreshEditorInspector069();}},ref y,20);
        }
        void InspectorChoice069(string title,string selected,string[] values,Action<string> apply,ref float y)
        {
            InspectorButton069(title+": "+selected,()=>ShowEditorChoice069(title,selected,values,apply,0),ref y,values.Length>0);
        }
        void ShowEditorChoice069(string title,string selected,string[] values,Action<string> apply,int page)
        {
            int pages=Mathf.Max(1,Mathf.CeilToInt(values.Length/8f));page=Mathf.Clamp(page,0,pages-1);
            var box=Modal(title,650);
            Label(box,"Choose a value. Scroll the inspector for further properties.",new Rect(25,74,800,50),17,muted);
            for(int i=0;i<8&&page*8+i<values.Length;i++)
            {string value=values[page*8+i];Button(box,(value==selected?"[SELECTED] ":"")+value,new Rect(25+(i%2)*410,140+(i/2)*91,395,78),()=>{apply(value);CloseModal();RefreshEditorInspector069();RenderEditorMap069();});}
            Button(box,"PREVIOUS",new Rect(25,557,220,48),()=>ShowEditorChoice069(title,selected,values,apply,page-1),"",page>0);
            Button(box,"NEXT "+(page+1)+"/"+pages,new Rect(265,557,220,48),()=>ShowEditorChoice069(title,selected,values,apply,page+1),"",page+1<pages);
            Button(box,"CANCEL",new Rect(620,557,220,48),CloseModal);
        }
        void InspectorBool069(string title,bool value,Action<bool> apply,ref float y)
        {InspectorButton069(title+": "+(value?"ON":"OFF"),()=>apply(!value),ref y);}
        void InspectorPoint069(string title,Vector2 p,Action<Vector2> apply,ref float y,float minimum=0,float maximum=128)
        {
            InspectorNumber069(title+" X",p.x,x=>apply(new Vector2(x,p.y)),ref y,minimum,maximum);
            InspectorNumber069(title+" Z",p.y,z=>apply(new Vector2(p.x,z)),ref y,minimum,maximum);
        }
        void InspectorResource069(ResourceKind value,Action<ResourceKind> apply,ref float y)
        {InspectorChoice069("RESOURCE",value==ResourceKind.None?"Food":value.ToString(),new[]{"Food","Wood","Coin"},v=>apply((ResourceKind)Enum.Parse(typeof(ResourceKind),v)),ref y);}
        string NewEditorId069(ScenarioDocument doc,string prefix)
        {
            var ids=new HashSet<string>();
            foreach(var x in doc.rivers)ids.Add(x.id);foreach(var x in doc.bridges)ids.Add(x.id);foreach(var x in doc.resources)ids.Add(x.id);foreach(var x in doc.objects)ids.Add(x.id);
            foreach(var x in doc.tradeSockets)ids.Add(x.id);foreach(var x in doc.treasures)ids.Add(x.id);foreach(var x in doc.regions)ids.Add(x.id);foreach(var x in doc.objectives)ids.Add(x.id);foreach(var x in doc.triggers){ids.Add(x.id);foreach(var a in x.actions)if(a.objectId!="")ids.Add(a.objectId);}
            for(int i=1;i<10000;i++){string id=prefix+"-"+i;if(!ids.Contains(id))return id;}throw new InvalidOperationException("Too many authored IDs");
        }
        static T[] AppendEditor069<T>(T[] array,T item){var result=new T[array.Length+1];Array.Copy(array,result,array.Length);result[array.Length]=item;return result;}
        static T[] RemoveEditor069<T>(T[] array,int index){var result=new T[array.Length-1];if(index>0)Array.Copy(array,result,index);if(index<result.Length)Array.Copy(array,index+1,result,index,result.Length-index);return result;}
        void TerrainInspector069(ScenarioDocument doc,ref float y)
        {
            if(Array.IndexOf(new[]{"raise","lower","smooth","flatten","paint"},editorTool069)<0)editorTool069="raise";
            InspectorNote069("Drag one brush stroke, then release to commit.\nRock blocks travel; steep building sites are rejected in validation.",ref y);
            InspectorChoice069("BRUSH",editorTool069,new[]{"raise","lower","smooth","flatten","paint"},v=>{editorTool069=v;RefreshEditorInspector069();},ref y);
            InspectorNumber069("RADIUS (m)",editorRadius069,v=>editorRadius069=v,ref y,1,24);
            InspectorNumber069("HEIGHT STEP (cm)",editorStrength069,v=>editorStrength069=v,ref y,1,500);
            InspectorNumber069("FLATTEN TO (m)",editorLevel069,v=>editorLevel069=v,ref y,-20,100);
            string[] surfaces={"grass","sand","mud","blocking rock"};
            InspectorChoice069("SURFACE",surfaces[editorSurface069],surfaces,v=>{editorSurface069=Array.IndexOf(surfaces,v);editorTool069="paint";RefreshEditorInspector069();},ref y);
            InspectorInteger069("WATER HEIGHT (cm)",doc.waterHeightCm,v=>EditScenario069(d=>d.waterHeightCm=v),ref y,-2000,10000);
            InspectorField069("TITLE",doc.name,v=>EditScenario069(d=>d.name=v),ref y,100);
            InspectorField069("DESCRIPTION",doc.description,v=>EditScenario069(d=>d.description=v),ref y,2000);
        }
        ScenarioRiver SelectedRiver069(ScenarioDocument d)=>Array.Find(d.rivers,r=>r.id==editorSelected069);
        void BeginRiverGesture069(Vector2 p)
        {
            var doc=gestureDocument069;float best=5*5;ScenarioRiver chosen=null;int point=-1;
            foreach(var r in doc.rivers)for(int i=0;i<r.points.Length;i++){float distance=(r.points[i]-p).sqrMagnitude;if(distance<best){best=distance;chosen=r;point=i;}}
            if(chosen!=null){editorSelected069=chosen.id;editorPoint069=point;return;}
            var selected=SelectedRiver069(doc);if(selected!=null&&editorTool069=="append")
            {selected.points=AppendEditor069(selected.points,p);selected.halfWidths=AppendEditor069(selected.halfWidths,editorRiverWidth069);editorPoint069=selected.points.Length-1;editorGestureChanged069=true;}
        }
        void MoveRiverPoint069(Vector2 p)
        {
            var r=SelectedRiver069(gestureDocument069);if(r==null||editorPoint069<0||editorPoint069>=r.points.Length)return;
            if((r.points[editorPoint069]-p).sqrMagnitude<.0001f)return;
            var original=SelectedRiver069(gestureBase069);
            if(editorTool069=="move bend"&&original!=null&&editorPoint069<original.points.Length)
            {
                Vector2 center=original.points[editorPoint069],delta=p-center;
                for(int i=0;i<original.points.Length;i++)
                {float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(1-Vector2.Distance(original.points[i],center)/editorRadius069));r.points[i]=original.points[i]+delta*weight;}
            }
            else r.points[editorPoint069]=p;
            editorGestureChanged069=true;
        }
        void RiverInspector069(ScenarioDocument doc,ref float y)
        {
            InspectorNote069("Orange dots are river controls. Select and drag a dot.\nAppend adds controls; each control can have its own width.",ref y);
            InspectorButton069("ADD RIVER",()=>EditScenario069(d=>{string id=NewEditorId069(d,"river");d.rivers=AppendEditor069(d.rivers,new ScenarioRiver{id=id,points=new[]{new Vector2(50,30),new Vector2(76,96)},halfWidths=new[]{editorRiverWidth069,editorRiverWidth069}});editorSelected069=id;editorPoint069=0;}),ref y,doc.rivers.Length<8);
            InspectorChoice069("POINTER",new[]{"move","move bend","append"}.Contains069(editorTool069)?editorTool069:"move",new[]{"move","move bend","append"},v=>{editorTool069=v;RefreshEditorInspector069();},ref y);
            InspectorNumber069("BEND RADIUS (m)",editorRadius069,v=>editorRadius069=v,ref y,1,32);
            InspectorNumber069("NEW HALF WIDTH (m)",editorRiverWidth069,v=>editorRiverWidth069=v,ref y,1,48);
            foreach(var r in doc.rivers){string id=r.id;InspectorButton069((editorSelected069==id?"[SELECTED] ":"")+id+" · "+r.points.Length+" controls",()=>{editorSelected069=id;editorPoint069=0;RefreshEditorInspector069();RenderEditorMap069();},ref y);}
            var river=SelectedRiver069(doc);if(river==null)return;
            InspectorBool069("SMOOTH CURVE",river.smooth,v=>EditScenario069(d=>SelectedRiver069(d).smooth=v),ref y);
            int index=Mathf.Clamp(editorPoint069,0,river.points.Length-1);editorPoint069=index;
            InspectorInteger069("CONTROL INDEX",index,v=>{editorPoint069=v;RefreshEditorInspector069();RenderEditorMap069();},ref y,0,river.points.Length-1);
            InspectorPoint069("CONTROL",river.points[index],p=>EditScenario069(d=>SelectedRiver069(d).points[index]=p),ref y,-32,160);
            InspectorNumber069("HALF WIDTH (m)",river.halfWidths[index],v=>EditScenario069(d=>SelectedRiver069(d).halfWidths[index]=v),ref y,1,48);
            InspectorButton069("REMOVE CONTROL",()=>EditScenario069(d=>{var r=SelectedRiver069(d);r.points=RemoveEditor069(r.points,index);r.halfWidths=RemoveEditor069(r.halfWidths,index);editorPoint069=Mathf.Max(0,index-1);}),ref y,river.points.Length>2);
            InspectorButton069("REMOVE RIVER",()=>EditScenario069(d=>{d.rivers=RemoveEditor069(d.rivers,Array.FindIndex(d.rivers,r=>r.id==editorSelected069));editorSelected069="";editorPoint069=-1;}),ref y);
        }
        void SelectClosestBridge069(Vector2 p)
        {float best=12*12;editorSelected069="";foreach(var b in gestureDocument069.bridges)if((b.center-p).sqrMagnitude<best){best=(b.center-p).sqrMagnitude;editorSelected069=b.id;}}
        void MoveBridge069(Vector2 p){var b=Array.Find(gestureDocument069.bridges,x=>x.id==editorSelected069);if(b==null)return;if(b.center==p)return;b.center=p;editorGestureChanged069=true;}
        void BridgeInspector069(ScenarioDocument doc,ref float y)
        {
            InspectorNote069("Drag a crossing by its center. Set angle, span and ramps.\nBoth ramps must reach clear land; ships need deck clearance.",ref y);
            InspectorButton069("ADD CROSSING",()=>EditScenario069(d=>{string id=NewEditorId069(d,"bridge");d.bridges=AppendEditor069(d.bridges,new ScenarioBridge{id=id,center=editorCursor069,halfLength=editorRiverWidth069+1});editorSelected069=id;}),ref y,doc.bridges.Length<32);
            foreach(var b in doc.bridges){string id=b.id;InspectorButton069(id,()=>{editorSelected069=id;RefreshEditorInspector069();},ref y);}
            var bridge=Array.Find(doc.bridges,b=>b.id==editorSelected069);if(bridge==null)return;
            InspectorPoint069("CENTER",bridge.center,p=>EditScenario069(d=>Array.Find(d.bridges,b=>b.id==editorSelected069).center=p),ref y);
            InspectorNumber069("ANGLE (degrees)",Mathf.Atan2(bridge.direction.y,bridge.direction.x)*Mathf.Rad2Deg,a=>EditScenario069(d=>Array.Find(d.bridges,b=>b.id==editorSelected069).direction=new Vector2(Mathf.Cos(a*Mathf.Deg2Rad),Mathf.Sin(a*Mathf.Deg2Rad))),ref y,-360,360);
            InspectorNumber069("HALF SPAN (m)",bridge.halfLength,v=>EditScenario069(d=>Array.Find(d.bridges,b=>b.id==editorSelected069).halfLength=v),ref y,1,64);
            InspectorNumber069("HALF WIDTH (m)",bridge.halfWidth,v=>EditScenario069(d=>Array.Find(d.bridges,b=>b.id==editorSelected069).halfWidth=v),ref y,1.5f,16);
            InspectorNumber069("RAMP LENGTH (m)",bridge.rampLength,v=>EditScenario069(d=>Array.Find(d.bridges,b=>b.id==editorSelected069).rampLength=v),ref y,1,32);
            InspectorNumber069("DECK HEIGHT (m)",bridge.height,v=>EditScenario069(d=>Array.Find(d.bridges,b=>b.id==editorSelected069).height=v),ref y,.1f,100);
            InspectorButton069("REMOVE CROSSING",()=>EditScenario069(d=>{d.bridges=RemoveEditor069(d.bridges,Array.FindIndex(d.bridges,b=>b.id==editorSelected069));editorSelected069="";}),ref y);
        }
        ScenarioPlacement SelectedPlacement069(ScenarioDocument doc)=>Array.Find(doc.resources,e=>e.id==editorSelected069)??Array.Find(doc.objects,e=>e.id==editorSelected069);
        void BeginObjectGesture069(Vector2 p)
        {
            var doc=gestureDocument069;float best=6*6;ScenarioPlacement chosen=null;
            foreach(var collection in new[]{doc.resources,doc.objects})foreach(var e in collection)if((e.position-p).sqrMagnitude<best){best=(e.position-p).sqrMagnitude;chosen=e;}
            if(editorTool069=="erase")
            {if(chosen==null)return;string id=chosen.id;int index=Array.FindIndex(doc.resources,e=>e.id==id);if(index>=0)doc.resources=RemoveEditor069(doc.resources,index);else doc.objects=RemoveEditor069(doc.objects,Array.FindIndex(doc.objects,e=>e.id==id));editorSelected069="";editorGestureChanged069=true;return;}
            if(editorTool069=="place")
            {
                var def=game.Rules.Entity(editorDefinition069);if(def==null)return;var e=new ScenarioPlacement{id=NewEditorId069(doc,def.kind==EntityKind.Resource?"resource":"object"),definition=def.id,position=p,owner=def.kind==EntityKind.Resource?-1:editorOwner069};
                if(def.kind==EntityKind.Resource)doc.resources=AppendEditor069(doc.resources,e);else doc.objects=AppendEditor069(doc.objects,e);editorSelected069=e.id;editorGestureChanged069=true;
            }
            else editorSelected069=chosen?.id??"";
        }
        void MoveObject069(Vector2 p)
        {var e=SelectedPlacement069(gestureDocument069);if(e==null||editorTool069=="erase")return;if(e.position==p)return;e.position=p;editorGestureChanged069=true;}
        void ObjectsInspector069(ScenarioDocument doc,ref float y)
        {
            InspectorNote069("Select and drag an existing object, or choose PLACE.\nResource amounts and building completion are editable.",ref y);
            InspectorChoice069("POINTER",new[]{"place","erase"}.Contains069(editorTool069)?editorTool069:"select",new[]{"select","place","erase"},v=>{editorTool069=v;RefreshEditorInspector069();},ref y);
            InspectorField069("CATALOG ID",editorDefinition069,v=>{if(game.Rules.Entity(v.Trim())!=null){editorDefinition069=v.Trim();editorTool069="place";}else game.EditorMessage("Unknown unit, building or resource");RefreshEditorInspector069();},ref y,64);
            var entries=new List<EntityDef>();foreach(var d in game.Rules.entities)if(d.id!="trade_socket"&&d.id!="treasure")entries.Add(d);
            int pages=Mathf.Max(1,Mathf.CeilToInt(entries.Count/6f));editorCatalogPage069=Mathf.Clamp(editorCatalogPage069,0,pages-1);
            for(int i=0;i<6&&editorCatalogPage069*6+i<entries.Count;i++){var def=entries[editorCatalogPage069*6+i];InspectorButton069(def.name+" · "+def.id,()=>{editorDefinition069=def.id;editorTool069="place";RefreshEditorInspector069();},ref y);}
            InspectorButton069("MORE OBJECTS "+(editorCatalogPage069+1)+" / "+pages,()=>{editorCatalogPage069=(editorCatalogPage069+1)%pages;RefreshEditorInspector069();},ref y);
            InspectorInteger069("NEW OBJECT OWNER",editorOwner069,v=>editorOwner069=v,ref y,-1,doc.starts.Length-1);
            var item=SelectedPlacement069(doc);if(item==null){InspectorNote069("No object selected. Choose select, then click a map marker.",ref y);return;}
            InspectorTitle069(item.id,ref y);InspectorNote069(game.Rules.Entity(item.definition).name,ref y,1);
            InspectorPoint069("POSITION",item.position,p=>EditScenario069(d=>SelectedPlacement069(d).position=p),ref y);
            if(game.Rules.Entity(item.definition).kind==EntityKind.Resource)InspectorNumber069("AMOUNT (-1 default)",item.remaining,v=>EditScenario069(d=>SelectedPlacement069(d).remaining=v),ref y,-1,1000000);
            else
            {InspectorInteger069("OWNER",item.owner,v=>EditScenario069(d=>SelectedPlacement069(d).owner=v),ref y,-1,doc.starts.Length-1);InspectorBool069("COMPLETED",item.complete,v=>EditScenario069(d=>SelectedPlacement069(d).complete=v),ref y);}
            InspectorButton069("REMOVE SELECTED",()=>EditScenario069(d=>{int index=Array.FindIndex(d.resources,e=>e.id==editorSelected069);if(index>=0)d.resources=RemoveEditor069(d.resources,index);else d.objects=RemoveEditor069(d.objects,Array.FindIndex(d.objects,e=>e.id==editorSelected069));editorSelected069="";}),ref y);
        }
        void MoveStart069(Vector2 p)
        {
            var s=gestureDocument069.starts[Mathf.Clamp(editorActor069,0,gestureDocument069.starts.Length-1)];
            if(editorRosterPart069==0){if(s.town==p)return;s.town=p;}
            else if(editorRosterPart069==1){if(s.explorer==p)return;s.explorer=p;}
            else {int i=editorRosterPart069-2;if(i>=s.workers.Length)return;if(s.workers[i]==p)return;s.workers[i]=p;}
            editorGestureChanged069=true;
        }
        void PlayersInspector069(ScenarioDocument doc,ref float y)
        {
            editorActor069=Mathf.Clamp(editorActor069,0,doc.starts.Length-1);var s=doc.starts[editorActor069];
            InspectorNote069("Choose a player and actor, then click or drag on the map.\nPlayer data controls the real starting economy and era.",ref y);
            InspectorButton069("ADD PLAYER SLOT",()=>EditScenario069(d=>
            {
                int slot=d.starts.Length;var town=slot==2?new Vector2(24,104):new Vector2(104,24);
                d.starts=AppendEditor069(d.starts,new ScenarioStart{player=slot,team=slot,civilization="coast",town=town,explorer=town+new Vector2(7,0),workers=new[]{town+new Vector2(5,-3)}});editorActor069=slot;editorRosterPart069=0;
            }),ref y,doc.starts.Length<4);
            InspectorButton069("REMOVE LAST PLAYER",()=>EditScenario069(d=>{d.starts=RemoveEditor069(d.starts,d.starts.Length-1);editorActor069=0;editorRosterPart069=0;}),ref y,doc.starts.Length>2);
            InspectorInteger069("PLAYER SLOT",editorActor069,v=>{editorActor069=v;editorRosterPart069=0;RefreshEditorInspector069();},ref y,0,doc.starts.Length-1);
            InspectorInteger069("ACTOR (0 town,1 hero)",editorRosterPart069,v=>{editorRosterPart069=v;RefreshEditorInspector069();},ref y,0,s.workers.Length+1);
            var civs=new string[game.Rules.civs.Length];for(int i=0;i<civs.Length;i++)civs[i]=game.Rules.civs[i].id;
            InspectorChoice069("CIVILIZATION",s.civilization,civs,v=>EditScenario069(d=>d.starts[editorActor069].civilization=v),ref y);
            InspectorInteger069("TEAM",s.team,v=>EditScenario069(d=>d.starts[editorActor069].team=v),ref y,0,3);
            InspectorInteger069("START AGE",s.age,v=>EditScenario069(d=>d.starts[editorActor069].age=v),ref y,1,5);
            InspectorNumber069("START FOOD",s.initialResources.food,v=>EditScenario069(d=>{var cost=d.starts[editorActor069].initialResources;cost.food=v;d.starts[editorActor069].initialResources=cost;}),ref y,0,1000000);
            InspectorNumber069("START WOOD",s.initialResources.wood,v=>EditScenario069(d=>{var cost=d.starts[editorActor069].initialResources;cost.wood=v;d.starts[editorActor069].initialResources=cost;}),ref y,0,1000000);
            InspectorNumber069("START COIN",s.initialResources.coin,v=>EditScenario069(d=>{var cost=d.starts[editorActor069].initialResources;cost.coin=v;d.starts[editorActor069].initialResources=cost;}),ref y,0,1000000);
            Vector2 position=editorRosterPart069==0?s.town:editorRosterPart069==1?s.explorer:s.workers[Mathf.Clamp(editorRosterPart069-2,0,s.workers.Length-1)];
            InspectorPoint069("ACTOR POSITION",position,p=>EditScenario069(d=>{var start=d.starts[editorActor069];if(editorRosterPart069==0)start.town=p;else if(editorRosterPart069==1)start.explorer=p;else start.workers[editorRosterPart069-2]=p;}),ref y);
            InspectorButton069("ADD STARTING WORKER",()=>EditScenario069(d=>{var start=d.starts[editorActor069];start.workers=AppendEditor069(start.workers,editorCursor069);editorRosterPart069=start.workers.Length+1;}),ref y,s.workers.Length<64);
            InspectorButton069("REMOVE SELECTED WORKER",()=>EditScenario069(d=>{var start=d.starts[editorActor069];start.workers=RemoveEditor069(start.workers,editorRosterPart069-2);editorRosterPart069=0;}),ref y,editorRosterPart069>=2&&editorRosterPart069<s.workers.Length+2);
        }
        void BeginRegionGesture069(Vector2 p)
        {editorSelected069="";float best=16*16;foreach(var r in gestureDocument069.regions)if((r.center-p).sqrMagnitude<best){best=(r.center-p).sqrMagnitude;editorSelected069=r.id;}}
        void MoveRegion069(Vector2 p)
        {var r=Array.Find(gestureDocument069.regions,x=>x.id==editorSelected069);if(r==null)return;var next=new Vector2(Mathf.Clamp(p.x,r.size.x*.5f,128-r.size.x*.5f),Mathf.Clamp(p.y,r.size.y*.5f,128-r.size.y*.5f));if(r.center==next)return;r.center=next;editorGestureChanged069=true;}
        void RegionsInspector069(ScenarioDocument doc,ref float y)
        {
            InspectorNote069("Regions mark mission areas; they do not block units.\nDrag a region center or enter its size below.",ref y);
            InspectorButton069("ADD REGION AT CURSOR",()=>EditScenario069(d=>{string id=NewEditorId069(d,"region");d.regions=AppendEditor069(d.regions,new ScenarioRegion{id=id,center=new Vector2(Mathf.Clamp(editorCursor069.x,5,123),Mathf.Clamp(editorCursor069.y,5,123))});editorSelected069=id;}),ref y,doc.regions.Length<64);
            foreach(var r in doc.regions){string id=r.id;InspectorButton069(id,()=>{editorSelected069=id;RefreshEditorInspector069();RenderEditorMap069();},ref y);}
            var region=Array.Find(doc.regions,r=>r.id==editorSelected069);if(region==null)return;
            InspectorPoint069("CENTER",region.center,p=>EditScenario069(d=>Array.Find(d.regions,r=>r.id==editorSelected069).center=p),ref y);
            InspectorPoint069("SIZE",region.size,p=>EditScenario069(d=>Array.Find(d.regions,r=>r.id==editorSelected069).size=p),ref y,.1f,128);
            InspectorButton069("REMOVE REGION",()=>EditScenario069(d=>{d.regions=RemoveEditor069(d.regions,Array.FindIndex(d.regions,r=>r.id==editorSelected069));editorSelected069="";}),ref y);
        }
    }
    internal static class ScenarioEditorArray069
    {public static bool Contains069<T>(this T[] values,T value)=>Array.IndexOf(values,value)>=0;}
}
