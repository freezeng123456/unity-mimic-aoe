using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Frontier
{
    public sealed partial class FrontierHud
    {
        Texture2D editorMap069;
        RawImage editorMapImage069;
        Text editorStatus069,editorTitle069;
        RectTransform editorInspector069,editorScrollContent069;
        ScenarioEditorSession shownEditor069;
        ScenarioDocument gestureDocument069,gestureBase069;
        string editorPage069="terrain",editorTool069="raise",editorSelected069="",editorDefinition069="tree";
        int editorPoint069=-1,editorOwner069,editorSurface069,editorActor069,editorRosterPart069,editorCatalogPage069;
        float editorRadius069=5,editorStrength069=50,editorLevel069,editorRiverWidth069=6;
        Vector2 editorCursor069=new Vector2(64,64),editorLastStroke069;
        readonly HashSet<int> editorTouched069=new HashSet<int>();
        float editorPreviewAt069;
        bool editorGestureChanged069,editorMoved069;
        string savedEditorHash069="";
        static readonly string[] editorPages069={"terrain","rivers","bridges","objects","players","regions","trade","objectives","triggers"};
        static readonly string[] editorPageTitles069={"TERRAIN","RIVERS","BRIDGES","OBJECTS & RESOURCES","PLAYERS & STARTS","REGIONS","TRADE & TREASURES","OBJECTIVES","TRIGGERS"};
        public bool ScenarioCanvasVisible=>game.ScenarioEditorOpen&&menuPanel!=null&&menuPanel.name=="Scenario workshop"&&shownEditor069==game.ScenarioEditor;
        ScenarioDocument EditorDocument069=>gestureDocument069??game.ScenarioEditor.Document;

        public void ShowScenarioEditor()
        {
            if(!game.ScenarioEditorOpen||game.ScenarioEditor==null)return;
            if(shownEditor069!=game.ScenarioEditor){shownEditor069=game.ScenarioEditor;editorSelected069="";editorPoint069=-1;savedEditorHash069="";}
            CloseModal();ResultVisible=false;CancelUiPointer();
            if(gamePanel!=null){gamePanel.gameObject.SetActive(false);Destroy(gamePanel.gameObject);gamePanel=null;}
            if(menuPanel!=null){menuPanel.gameObject.SetActive(false);Destroy(menuPanel.gameObject);}
            menuPanel=Panel(root,"Scenario workshop",new Rect(0,0,1600,900),background);Fill(menuPanel);
            editorTitle069=Label(menuPanel,"",new Rect(20,14,1000,34),26,gold);
            Label(menuPanel,"128 m · authored terrain and scenario",new Rect(1130,19,450,30),16,muted);
            string[] titles={"NEW","OPEN","SAVE","SAVE AS","IMPORT","EXPORT","UNDO","REDO","VALIDATE","PLAYTEST","PREV DRAFT","CLOSE"};
            Action[] actions={ShowScenarioNew069,()=>ShowScenarioLibrary069(0),()=>SaveEditor069(false),()=>SaveEditor069(true),()=>ShowScenarioPath069(false),()=>ShowScenarioPath069(true),()=>UndoEditor069(false),()=>UndoEditor069(true),()=>{game.ValidateScenarioDraft();RefreshEditorStatus069();},()=>game.PlaytestScenario(),()=>game.RestorePreviousScenarioDraft(),()=>game.CloseScenarioEditor()};
            for(int i=0;i<titles.Length;i++)Button(menuPanel,titles[i],new Rect(20+i*130,62,120,46),actions[i]);
            for(int i=0;i<editorPages069.Length;i++)
            {
                string page=editorPages069[i];Button(menuPanel,(page==editorPage069?"▸ ":"")+editorPageTitles069[i],new Rect(16,151+i*53,248,44),()=>{editorPage069=page;editorSelected069="";editorPoint069=-1;ShowScenarioEditor();});
            }
            Button(menuPanel,"DRAFT HISTORY",new Rect(22,626,236,40),ShowEditorDraftHistory069);
            Label(menuPanel,"VIEW FROM ABOVE\nNorth is up. Click to select; drag\nto paint or move the selected item.\n\nUNDO: Ctrl/Cmd+Z\nREDO: Ctrl/Cmd+Y\nSAVE: Ctrl/Cmd+S\n\nPlaytest runs an owned copy.\nReturn to editor keeps this draft.",new Rect(22,675,235,215),15,muted);
            Label(menuPanel,"MAP CANVAS · "+editorPageTitles069[Array.IndexOf(editorPages069,editorPage069)],new Rect(284,122,640,30),18,gold);
            var map=Rect(menuPanel,"Editable map canvas",new Rect(284,160,640,640));
            editorMapImage069=map.gameObject.AddComponent<RawImage>();editorMapImage069.color=Color.white;
            var input=map.gameObject.AddComponent<ScenarioEditorInput>();input.Begin=BeginEditorGesture069;input.Move=MoveEditorGesture069;input.End=EndEditorGesture069;
            editorInspector069=Panel(menuPanel,"Workshop inspector",new Rect(944,122,636,678),panelColor);
            editorStatus069=Label(menuPanel,"",new Rect(284,812,1296,72),16,muted);editorStatus069.supportRichText=false;
            RenderEditorMap069();RefreshEditorInspector069();RefreshEditorStatus069();ApplyPreferences();
        }
        void LayoutScenarioEditor069()
        {
            if(menuPanel==null||!game.ScenarioEditorOpen)return;
            var size=CanvasSize;float fit=Mathf.Min(1,size.x/1600,size.y/900);
            var panel=(RectTransform)menuPanel;panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,1);
            panel.sizeDelta=new Vector2(1600,900);panel.anchoredPosition=new Vector2(0,-Mathf.Max(0,(size.y-900*fit)*.5f));panel.localScale=Vector3.one*fit;
        }
        void RefreshEditorStatus069()
        {
            if(!ScenarioCanvasVisible)return;var doc=EditorDocument069;
            if(editorTitle069!=null)editorTitle069.text=doc.name+(gestureDocument069==null&&ScenarioCodec.ContentHash(doc)==savedEditorHash069?"  · SAVED":"  · DRAFT");
            if(editorStatus069!=null)editorStatus069.text="X "+editorCursor069.x.ToString("0.0")+" · Z "+editorCursor069.y.ToString("0.0")+" · "+doc.starts.Length+" players · "+doc.rivers.Length+" rivers · "+(doc.resources.Length+doc.objects.Length)+" objects\n"+game.ScenarioEditorMessage;
        }
        void UpdateScenarioEditor069()
        {
            if(!ScenarioCanvasVisible)return;
            var key=Keyboard.current;if(key!=null&&(key.ctrlKey.isPressed||key.leftMetaKey.isPressed||key.rightMetaKey.isPressed)&&!ModalVisible)
            {
                bool typing=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.GetComponent<InputField>()!=null;
                if(!typing){if(key.zKey.wasPressedThisFrame)UndoEditor069(key.shiftKey.isPressed);else if(key.yKey.wasPressedThisFrame)UndoEditor069(true);else if(key.sKey.wasPressedThisFrame)SaveEditor069(false);}
            }
        }
        void UndoEditor069(bool redo)
        {
            CancelEditorGesture069();bool ok=redo?game.ScenarioEditor.Redo():game.ScenarioEditor.Undo();
            game.EditorMessage(ok?(redo?"Redid edit":"Undid edit"):"No edit in that direction");RenderEditorMap069();RefreshEditorInspector069();RefreshEditorStatus069();
        }
        void CancelEditorGesture069(){gestureDocument069=null;gestureBase069=null;editorGestureChanged069=false;editorMoved069=false;editorTouched069.Clear();}
        void EditScenario069(Action<ScenarioDocument> change,bool rebuild=true)
        {CancelEditorGesture069();game.ApplyScenarioEdit(change);RenderEditorMap069();if(rebuild)RefreshEditorInspector069();RefreshEditorStatus069();}
        void SaveEditor069(bool saveAs)
        {
            var doc=game.ScenarioEditor.Document;
            if(!saveAs){if(game.SaveScenarioDraft(doc.id,doc.name))savedEditorHash069=ScenarioCodec.ContentHash(game.ScenarioEditor.Document);RefreshEditorStatus069();return;}
            var box=Modal("SAVE SCENARIO AS",350);Label(box,"A separate name keeps an earlier scenario available in the library.",new Rect(25,70,800,50),17,muted);
            string copyId=(doc.id.Length>48?doc.id.Substring(0,48):doc.id)+"-copy-"+Guid.NewGuid().ToString("N").Substring(0,4);
            var id=EditorInput069(box,"FILE ID",copyId,new Rect(25,140,380,44),64);
            var name=EditorInput069(box,"TITLE",doc.name,new Rect(430,140,380,44),100);
            Button(box,"SAVE",new Rect(590,265,220,48),()=>{if(game.SaveScenarioDraft(id.text,name.text)){savedEditorHash069=ScenarioCodec.ContentHash(game.ScenarioEditor.Document);CloseModal();ShowScenarioEditor();}});
        }
        void ShowScenarioNew069()
        {
            var box=Modal("NEW MAP FROM TEMPLATE",600);
            Label(box,"Templates are expanded into editable terrain, rivers, actors and resources.\nCreating a draft does not save over a library file.",new Rect(25,75,800,70),18,muted);
            for(int i=0;i<MapTopology.All.Length;i++){string id=MapTopology.All[i];Button(box,MapTopology.Name(id),new Rect(25+(i%2)*410,164+(i/2)*74,395,60),()=>{CancelEditorGesture069();game.OpenScenarioEditor(id);});}
        }
        void ShowScenarioLibrary069(int page)
        {
            var rows=game.Scenarios.List();int pages=Mathf.Max(1,Mathf.CeilToInt(rows.Length/5f));page=Mathf.Clamp(page,0,pages-1);
            var box=Modal("SCENARIO LIBRARY",680);Label(box,"Saved maps in this game profile. Import also accepts an exported local file.",new Rect(25,72,800,55),18,muted);
            for(int i=0;i<5&&page*5+i<rows.Length;i++)
            {var row=rows[page*5+i];Button(box,row.name+"\n"+(row.reason==""?row.id:row.reason),new Rect(25,138+i*86,815,74),()=>{if(game.OpenScenarioFile(row.path))CloseModal();},row.path,row.reason=="");}
            if(rows.Length==0)Label(box,"No saved scenarios yet. Paint a draft and use SAVE.",new Rect(25,170,800,60),22,gold);
            Button(box,"PREVIOUS",new Rect(25,603,220,48),()=>ShowScenarioLibrary069(page-1),"",page>0);
            Button(box,"NEXT "+(page+1)+"/"+pages,new Rect(265,603,220,48),()=>ShowScenarioLibrary069(page+1),"",page+1<pages);
            Button(box,"DONE",new Rect(620,603,220,48),CloseModal);
        }
        void ShowEditorDraftHistory069()
        {
            var names=game.PreviousScenarioDraftNames;var box=Modal("RECOVER PREVIOUS DRAFT",570);
            Label(box,"The last four drafts retain their editing history in this session.\nPortable recovery copies also remain in ScenarioDrafts.",new Rect(25,75,800,60),17,muted);
            for(int i=0;i<names.Length;i++){int index=i;Button(box,names[i],new Rect(25,154+i*83,815,70),()=>game.RestorePreviousScenarioDraft(index));}
            if(names.Length==0)Label(box,"No previous draft in this session.",new Rect(25,170,800,50),20,gold);
        }
        void ShowScenarioPath069(bool export)
        {
            var box=Modal(export?"EXPORT SCENARIO":"IMPORT SCENARIO",400);
            Label(box,export?"Write a portable scenario file. Terrain, actors and mission rules travel together.":"Open an exported scenario file. A damaged import keeps the current draft intact.",new Rect(25,75,800,65),18,muted);
            string suggested=export?Path.Combine(Path.GetDirectoryName(game.ScenarioDirectory),"ScenarioExports",game.ScenarioEditor.Document.id+".scenario.json"):game.ScenarioDirectory+Path.DirectorySeparatorChar;
            var path=EditorInput069(box,"LOCAL FILE PATH",suggested,new Rect(25,178,815,48),1024);
            Button(box,export?"EXPORT":"IMPORT",new Rect(620,310,220,48),()=>{if(export?game.ExportScenarioDraft(path.text):game.OpenScenarioFile(path.text))CloseModal();});
        }
        InputField EditorInput069(Transform parent,string title,string text,Rect rect,int limit=100)
        {
            var pane=Rect(parent,title,rect);pane.gameObject.AddComponent<Image>().color=background;
            var input=pane.gameObject.AddComponent<InputField>();var label=Label(pane,"",new Rect(9,7,rect.width-18,rect.height-12),17);label.font=cjkFont??font;label.supportRichText=false;
            input.textComponent=label;input.characterLimit=limit;input.text=text??"";input.selectionColor=new Color(.6f,.7f,.45f,.5f);
            return input;
        }
        void BeginEditorGesture069(Vector2 point)
        {
            if(!ScenarioCanvasVisible||ModalVisible)return;
            CancelEditorGesture069();gestureBase069=game.ScenarioEditor.Document;gestureDocument069=ScenarioCodec.Clone(gestureBase069);editorCursor069=editorLastStroke069=point;
            if(editorPage069=="terrain")PaintEditor069(point);
            else if(editorPage069=="objects")BeginObjectGesture069(point);
            else if(editorPage069=="rivers")BeginRiverGesture069(point);
            else if(editorPage069=="bridges")SelectClosestBridge069(point);
            else if(editorPage069=="players")MoveStart069(point);
            else if(editorPage069=="regions")BeginRegionGesture069(point);
            else if(editorPage069=="trade")BeginTradeGesture069(point);
            RenderEditorMap069();RefreshEditorStatus069();
        }
        void MoveEditorGesture069(Vector2 point)
        {
            if(gestureDocument069==null||(point-editorLastStroke069).sqrMagnitude<.00000001f)return;editorMoved069=true;editorCursor069=point;
            if(editorPage069=="terrain")
            {float distance=Vector2.Distance(editorLastStroke069,point);int count=Mathf.Max(1,Mathf.CeilToInt(distance/Mathf.Max(.5f,editorRadius069*.3f)));for(int i=1;i<=count;i++)PaintEditor069(Vector2.Lerp(editorLastStroke069,point,i/(float)count));}
            else if(editorPage069=="objects")MoveObject069(point);
            else if(editorPage069=="rivers")MoveRiverPoint069(point);
            else if(editorPage069=="bridges")MoveBridge069(point);
            else if(editorPage069=="players")MoveStart069(point);
            else if(editorPage069=="regions")MoveRegion069(point);
            else if(editorPage069=="trade")MoveTradePoint069(point);
            editorLastStroke069=point;
            if(Time.unscaledTime>=editorPreviewAt069){RenderEditorMap069();editorPreviewAt069=Time.unscaledTime+.07f;}RefreshEditorStatus069();
        }
        void EndEditorGesture069(Vector2 point)
        {
            if(gestureDocument069==null)return;if(editorMoved069)MoveEditorGesture069(point);
            var final=gestureDocument069;bool changed=editorGestureChanged069;
            CancelEditorGesture069();if(changed)game.ApplyScenarioEdit(d=>JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(final),d));
            RenderEditorMap069();RefreshEditorInspector069();RefreshEditorStatus069();
        }
        void PaintEditor069(Vector2 point)
        {
            var d=gestureDocument069;int max=editorTool069=="paint"?127:128,stride=max+1;
            int minX=Mathf.Max(0,Mathf.FloorToInt(point.x-editorRadius069)),maxX=Mathf.Min(max,Mathf.CeilToInt(point.x+editorRadius069));
            int minY=Mathf.Max(0,Mathf.FloorToInt(point.y-editorRadius069)),maxY=Mathf.Min(max,Mathf.CeilToInt(point.y+editorRadius069));
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
            {
                if((new Vector2(x,y)-point).sqrMagnitude>editorRadius069*editorRadius069)continue;int i=x+y*stride;if(!editorTouched069.Add(i))continue;
                if(editorTool069=="paint"){d.surfaces[i]=editorSurface069;editorGestureChanged069|=d.surfaces[i]!=gestureBase069.surfaces[i];continue;}
                int height=d.heightsCm[i];
                if(editorTool069=="smooth")
                {int sum=0,n=0;for(int yy=Mathf.Max(0,y-1);yy<=Mathf.Min(128,y+1);yy++)for(int xx=Mathf.Max(0,x-1);xx<=Mathf.Min(128,x+1);xx++){sum+=gestureBase069.heightsCm[xx+yy*129];n++;}height=Mathf.RoundToInt(sum/(float)n);}
                else if(editorTool069=="flatten")height=Mathf.RoundToInt(editorLevel069*100);
                else height+=Mathf.RoundToInt(editorStrength069*(editorTool069=="lower"?-1:1));
                d.heightsCm[i]=Mathf.Clamp(height,-2000,10000);editorGestureChanged069|=d.heightsCm[i]!=gestureBase069.heightsCm[i];
            }
        }
        void RenderEditorMap069()
        {
            if(editorMapImage069==null||game.ScenarioEditor==null)return;
            if(editorMap069==null)editorMap069=new Texture2D(128,128,TextureFormat.RGBA32,false){name="Workshop terrain preview",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var doc=EditorDocument069;var pixels=new Color32[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                int i=x+y*128;float level=doc.heightsCm[x+y*129]*.01f;Color baseColor=doc.surfaces[i]==1?new Color(.65f,.56f,.38f):doc.surfaces[i]==2?new Color(.38f,.31f,.23f):doc.surfaces[i]==3?new Color(.40f,.43f,.44f):new Color(.35f,.46f,.26f);
                float shade=Mathf.Clamp(1+level*.014f+(doc.heightsCm[x+1+y*129]-doc.heightsCm[x+y*129])*.0008f,.45f,1.55f);pixels[i]=baseColor*shade;
            }
            void Dot(Vector2 p,Color32 color,int radius=1){int cx=Mathf.RoundToInt(p.x),cy=Mathf.RoundToInt(p.y);for(int y=Mathf.Max(0,cy-radius);y<=Mathf.Min(127,cy+radius);y++)for(int x=Mathf.Max(0,cx-radius);x<=Mathf.Min(127,cx+radius);x++)if((x-cx)*(x-cx)+(y-cy)*(y-cy)<=radius*radius)pixels[x+y*128]=color;}
            void Line(Vector2 a,Vector2 b,Color32 color,int radius=0){int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)*2));for(int i=0;i<=n;i++)Dot(Vector2.Lerp(a,b,i/(float)n),color,radius);}
            foreach(var r in doc.rivers)
            {
                var channel=r.smooth?MapLayout.Sample(r.points,r.halfWidths):new RiverChannel(r.points,r.halfWidths);
                for(int i=1;i<channel.Centers.Length;i++)
                {int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(channel.Centers[i-1],channel.Centers[i])*2));for(int j=0;j<=n;j++){float t=j/(float)n;Dot(Vector2.Lerp(channel.Centers[i-1],channel.Centers[i],t),new Color32(36,99,135,255),Mathf.CeilToInt(Mathf.Lerp(channel.HalfWidths[i-1],channel.HalfWidths[i],t)));}}
            }
            foreach(var b in doc.bridges){var normal=new Vector2(-b.direction.y,b.direction.x);Line(b.center-b.direction*(b.halfLength+b.rampLength),b.center+b.direction*(b.halfLength+b.rampLength),new Color32(195,164,89,255),Mathf.CeilToInt(b.halfWidth));Line(b.center-normal*b.halfWidth,b.center+normal*b.halfWidth,Color.white);}
            for(int i=1;i<doc.tradeRoute.Length;i++)Line(doc.tradeRoute[i-1],doc.tradeRoute[i],new Color32(203,176,130,255),0);
            foreach(var e in doc.resources)Dot(e.position,e.definition=="fish"?new Color32(144,222,236,255):e.definition=="mine"?new Color32(235,191,74,255):new Color32(145,174,106,255),1);
            foreach(var e in doc.objects)Dot(e.position,e.owner<0?Color.gray:ProceduralArt.OwnerColor(e.owner),2);
            foreach(var socket in doc.tradeSockets)Dot(socket.position,new Color32(255,241,206,255),2);
            foreach(var treasure in doc.treasures)Dot(treasure.position,new Color32(238,170,90,255),2);
            foreach(var s in doc.starts){Dot(s.town,ProceduralArt.OwnerColor(s.player),3);Dot(s.explorer,ProceduralArt.OwnerColor(s.player),1);foreach(var p in s.workers)Dot(p,ProceduralArt.OwnerColor(s.player),1);}
            foreach(var region in doc.regions){Vector2 a=region.center-region.size*.5f,b=region.center+region.size*.5f;Color color=region.id==editorSelected069?Color.white:new Color(.75f,.65f,.9f);Line(a,new Vector2(b.x,a.y),color);Line(new Vector2(b.x,a.y),b,color);Line(b,new Vector2(a.x,b.y),color);Line(new Vector2(a.x,b.y),a,color);}
            if(editorPage069=="rivers")foreach(var r in doc.rivers)for(int i=0;i<r.points.Length;i++)Dot(r.points[i],r.id==editorSelected069&&i==editorPoint069?Color.white:new Color(.85f,.65f,.4f),1);
            if(editorPage069=="trade")for(int i=0;i<doc.tradeRoute.Length;i++)Dot(doc.tradeRoute[i],editorSelected069=="route"&&editorPoint069==i?Color.white:new Color(.94f,.87f,.57f),1);
            editorMap069.SetPixels32(pixels);editorMap069.Apply(false);editorMapImage069.texture=editorMap069;
        }
    }
}
