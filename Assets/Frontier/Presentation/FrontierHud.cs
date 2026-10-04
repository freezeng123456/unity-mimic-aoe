using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Frontier
{
    public sealed class FrontierHud : MonoBehaviour
    {
        private GameController game;
        private Canvas canvas;
        private Transform root,gamePanel,menuPanel,modal;
        private Transform actionPanel,queuePanel;
        private RectTransform minimapRect;
        private Text resources,selection,details,notice,help,orders,clockLabel,homeTransit,homeTitle;
        public string ResourceSummary => resources != null ? resources.text : "";
        public int ResourceSummaryFrame { get; private set; } = -1;
        public float ResourceSummaryTime { get; private set; }
        public string ResourceSummaryMatchId { get; private set; } = "";
        private readonly List<Button> homeButtons=new List<Button>();
        private readonly List<CardDef> homeCards=new List<CardDef>();
        private Image dragBox;
        private Font font;
        private int actionKey,actionEntity=-1;
        private readonly List<int> actionSelection=new List<int>();
        private bool batchTraining,lastShift;
        private bool traceInput,uiLayoutDirty;
        private int uiLayoutFrame=-1;
        private UiPointerDispatcher uiPointer;
        private float refreshAt;
        private readonly List<RaycastResult> hits=new List<RaycastResult>();
        private PointerEventData hoverPointer;
        private EventSystem hoverEventSystem;
        private struct ResourceDisplay
        {
            public Cost amount;public float xp,threshold;public int used,reserved,capacity,age,shipments;
            public bool Same(ResourceDisplay b)=>amount.food==b.amount.food&&amount.wood==b.amount.wood&&amount.coin==b.amount.coin&&xp==b.xp&&threshold==b.threshold&&used==b.used&&reserved==b.reserved&&capacity==b.capacity&&age==b.age&&shipments==b.shipments;
        }
        private ResourceDisplay lastResources;
        private Text lastResourceLabel,lastClockLabel;
        private int lastClockSeconds=-1,lastIdle=-1;
        private void RaycastHover(Vector2 pointer)
        {
            hits.Clear();var current=EventSystem.current;if(current==null)return;
            if(hoverPointer==null||hoverEventSystem!=current){hoverEventSystem=current;hoverPointer=new PointerEventData(current);}
            hoverPointer.Reset();hoverPointer.position=pointer;current.RaycastAll(hoverPointer,hits);
        }
        internal bool PendingInputLayout=>uiLayoutDirty;
        internal bool InputLayoutReady=>!uiLayoutDirty||Time.frameCount>uiLayoutFrame;
        private void MarkUiLayoutDirty(){uiLayoutDirty=true;uiLayoutFrame=Time.frameCount;}
        public bool ModalVisible=>modal!=null&&modal.gameObject.activeSelf;
        public bool ResultVisible {get;private set;}
        public string Help="";
        private readonly Color background=new Color(.065f,.104f,.12f,.98f),panelColor=new Color(.105f,.16f,.17f,.97f),gold=new Color(.82f,.72f,.43f),ink=new Color(.88f,.9f,.81f),muted=new Color(.58f,.68f,.65f);
        public void Initialize(GameController controller)
        {
            game=controller;traceInput=Array.IndexOf(Environment.GetCommandLineArgs(),"-frontierInputTrace")>=0;font=Resources.Load<Font>("Fonts/NotoSans-Regular");
            var c=new GameObject("Frontier Native UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));c.transform.SetParent(transform,false);
            canvas=c.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=10;root=c.transform;
            var scaler=c.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            if(EventSystem.current==null){var es=new GameObject("Frontier EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));es.transform.SetParent(transform);}
            var uiModule=EventSystem.current.GetComponent<InputSystemUIInputModule>();
            // Keep native UGUI hover/navigation; discrete clicks come from the
            // same ordered input stream as world commands, with edge positions.
            if(uiModule!=null){uiModule.leftClick=null;uiModule.rightClick=null;uiModule.middleClick=null;}
            uiPointer=new UiPointerDispatcher(EventSystem.current,traceInput);
            var drag=Panel(root,"Selection rectangle",new Rect(0,0,0,0),new Color(.6f,.8f,.56f,.12f));drag.GetComponent<Image>().raycastTarget=false;dragBox=drag.GetComponent<Image>();dragBox.gameObject.SetActive(false);
            var outline=drag.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.8f,.94f,.6f);outline.effectDistance=new Vector2(1,-1);
        }
        public void InvalidateActions(){actionKey=int.MinValue;refreshAt=0;}
        // Selection and completed HUD actions are ordered with native input,
        // not with the next HUD Update. Keep an already-held same-context
        // gesture stable; subsequent gestures wait for new graphics to render.
        internal void EnsureActionContextCurrent()
        {
            if(game.InMenu||game.World==null||actionPanel==null)return;
            var controls=game.Controls;
            controls.PruneInvisibleSelection();
            var selected=controls.FirstSelected;int id=selected?.id??0;
            bool changed=id!=actionEntity||actionSelection.Count!=controls.Selected.Count;
            if(!changed)for(int i=0;i<actionSelection.Count;i++)if(actionSelection[i]!=controls.Selected[i]){changed=true;break;}
            int key=CurrentActionKey(selected);
            if(!changed&&(key==actionKey||(uiPointer?.IsPressed??false)))return;
            if(changed)uiPointer?.CancelWithin(actionPanel,queuePanel);
            actionEntity=id;actionSelection.Clear();actionSelection.AddRange(controls.Selected);
            actionKey=key;refreshAt=Time.unscaledTime+1;
            RebuildActions(selected);
            if(traceInput)Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"FRONTIER_UI context entity={0} count={1} world={2:F3} frame={3}",id,actionSelection.Count,game.World.Time,Time.frameCount);
        }
        private int CurrentActionKey(EntityState selected)
        {
            var p=game.World.Players[0];int key=(selected?.id??0)*10000+p.age*100+p.technologies.Count*10+(selected?.queue.Count??0);
            // Arrival and release change the Ungarrison count and availability.
            // The existing pointer-capture guard still defers same-context rebuilds.
            return unchecked((((key*397+(selected?.occupants.Count??0))*31+(selected?.shielded==true?1:0))*31+(selected?.autoRecruitPaused==true?1:0))*31+(selected?.factoryMode?.GetHashCode()??0));
        }
        private RectTransform Rect(Transform parent,string name,Rect r,bool bottom=false)
        {
            MarkUiLayoutDirty();var go=new GameObject(name,typeof(RectTransform));var t=(RectTransform)go.transform;t.SetParent(parent,false);t.anchorMin=t.anchorMax=bottom?Vector2.zero:new Vector2(0,1);t.pivot=bottom?Vector2.zero:new Vector2(0,1);t.anchoredPosition=new Vector2(r.x,bottom?r.y:-r.y);t.sizeDelta=new Vector2(r.width,r.height);return t;
        }
        private RectTransform Panel(Transform parent,string name,Rect r,Color color,bool bottom=false)
        {var t=Rect(parent,name,r,bottom);var image=t.gameObject.AddComponent<Image>();image.color=color;return t;}
        private Text Label(Transform parent,string text,Rect r,int size=16,Color? color=null)
        {
            var t=Rect(parent,text.Length>30?text.Substring(0,30):text,r);var label=t.gameObject.AddComponent<Text>();label.font=font;label.fontSize=size;label.color=color??ink;label.text=text;label.raycastTarget=false;label.supportRichText=true;label.verticalOverflow=VerticalWrapMode.Overflow;return label;
        }
        private Button Button(Transform parent,string text,Rect r,Action click,string tooltip="",bool enabled=true)
        {
            var t=Panel(parent,text,r,new Color(.16f,.25f,.26f));var button=t.gameObject.AddComponent<Button>();button.interactable=enabled;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.35f,1.28f,.95f);colors.pressedColor=gold;colors.disabledColor=new Color(.45f,.51f,.5f);button.colors=colors;
            var label=Label(t,text,new Rect(10,8,r.width-20,r.height-12),15);label.alignment=TextAnchor.MiddleLeft;
            if(traceInput){var trace=t.gameObject.AddComponent<FrontierUiTrace>();trace.game=game;}
            button.onClick.AddListener(()=>
            {
                if(traceInput)Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"FRONTIER_UI action={0} world={1:F3} frame={2}",t.name.Replace('\n',' '),game.World?.Time??0,Time.frameCount);
                // UGUI owns this complete pointer gesture. Ordinary HUD actions
                // must not swallow the next distinct world click. Preserve the
                // transition barrier for buttons inside a modal, before click
                // can close it or replace the match. Pause/placement also guard
                // their own entry points.
                if(ModalVisible)game.Controls?.SuppressWorldInput(false);
                click();
            });var hover=t.gameObject.AddComponent<FrontierHover>();hover.hud=this;hover.message=tooltip;
            return button;
        }
        private void Fill(Transform transform)
        {var t=(RectTransform)transform;t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=t.offsetMax=Vector2.zero;}
        public void ShowMenu()
        {
            CloseModal();ResultVisible=false;if(gamePanel!=null)Destroy(gamePanel.gameObject);if(menuPanel!=null)Destroy(menuPanel.gameObject);
            menuPanel=Panel(root,"Main menu",new Rect(0,0,1600,900),background);Fill(menuPanel);
            Label(menuPanel,"FRONTIER\nRECKONING",new Rect(95,100,960,170),66,gold);
            Label(menuPanel,"A LAND OF CHOICES",new Rect(100,294,750,40),23,muted);
            Label(menuPanel,"Build an economy. Advance your age. Call for support.\nLead your army across the frontier.",new Rect(100,350,800,90),23);
            var configuration=Panel(menuPanel,"Skirmish configuration",new Rect(980,85,510,710),panelColor);
            Label(configuration,"LOCAL SKIRMISH",new Rect(28,25,450,40),26,gold);
            Button(configuration,"YOU: "+game.Rules.Civ(game.PlayerCivilization).name,new Rect(28,82,454,48),()=>ShowCivilizations(false,0),"Choose an economy, military roster and age progression.");
            Button(configuration,"AI: "+game.Rules.Civ(game.OpponentCivilization).name,new Rect(28,138,454,48),()=>ShowCivilizations(true,0));
            Button(configuration,"ERA LIMIT: "+game.AgeLimit+" / "+game.Rules.ages.Length,new Rect(28,194,454,48),()=>{game.AgeLimit=game.AgeLimit==3?game.Rules.ages.Length:3;ShowMenu();});
            var difficulty=game.Rules.Difficulty(game.OpponentDifficulty);
            Button(configuration,"AI DIFFICULTY: "+(difficulty?.name?.ToUpperInvariant()??"INVALID"),new Rect(28,250,454,48),()=>{game.CycleDifficulty();ShowMenu();},difficulty?.description??game.DifficultyReason);
            Label(configuration,difficulty?.description??game.DifficultyReason,new Rect(28,308,450,55),17,muted);
            Button(configuration,"MAP: "+game.MapName,new Rect(28,367,454,44),()=>{game.CycleMap();ShowMenu();},"Choose outcrops, river crossings, a winding valley, tropical river bend, tributaries or an estuary. River maps support shoreline docks, fishing and naval combat.");
            var civilizationSummary=Label(configuration,"SEED "+game.MatchSeed+" · "+game.Rules.Civ(game.PlayerCivilization).description,new Rect(28,418,450,96),17,muted);
            civilizationSummary.gameObject.name="Civilization summary";
            civilizationSummary.resizeTextForBestFit=true;civilizationSummary.resizeTextMinSize=14;civilizationSummary.resizeTextMaxSize=17;
            civilizationSummary.horizontalOverflow=HorizontalWrapMode.Wrap;civilizationSummary.verticalOverflow=VerticalWrapMode.Truncate;
            Button(configuration,"PRESET: "+game.DeckPreset.ToUpperInvariant(),new Rect(28,520,220,48),()=>{string[] presets={"economy","military","technology"};game.DeckPreset=presets[(Array.IndexOf(presets,game.DeckPreset)+1)%presets.Length];game.ResetDeckPreset();ShowMenu();},"Economy, military and technology plans use different cards. Edit any preset before play.");
            Button(configuration,"EDIT DECK  "+game.SelectedDeck.Count+"/"+game.Rules.deckCapacity,new Rect(258,520,224,48),()=>ShowDeckEditor(0));
            string deckReason=DeckLibrary.Validate(game.Rules,game.PlayerCivilization,game.SelectedDeck);if(deckReason=="")deckReason=game.DifficultyReason;
            Button(configuration,"BEGIN SKIRMISH",new Rect(28,584,454,58),()=>{game.Autoplay=false;game.StartMatch();},deckReason,deckReason=="");
            Button(configuration,"QUIT",new Rect(28,654,454,44),()=>Application.Quit());
            Button(menuPanel,"LOAD SAVED MATCH",new Rect(100,555,454,58),()=>ShowSaveSlots(false),"Continue a local match, including AI and production progress.");
            Label(menuPanel,"FIVE-ERA FRONTIER",new Rect(100,665,840,35),23,gold);
            Label(menuPanel,game.Rules.civs.Length+" civilizations · British · Dutch · French · Ottomans\nManors · Banks · Trade · Workshops · Rivers · Docks\nIndependent project. Not affiliated with Age of Empires or its publishers.",new Rect(100,715,820,110),18,muted);
        }
        private void ShowCivilizations(bool opponent,int page)
        {
            var civs=game.Rules.civs;int pages=Mathf.Max(1,Mathf.CeilToInt(civs.Length/4f));page=Mathf.Clamp(page,0,pages-1);
            var box=Modal(opponent?"CHOOSE OPPONENT CIVILIZATION":"CHOOSE YOUR CIVILIZATION",650);
            Label(box,"Each civilization has its own economy, units, cards and age choices.",new Rect(25,75,820,45),18,muted);
            for(int i=0;i<4&&page*4+i<civs.Length;i++)
            {
                var civ=civs[page*4+i];bool chosen=civ.id==(opponent?game.OpponentCivilization:game.PlayerCivilization);
                Button(box,(chosen?"[SELECTED] ":"")+civ.name.ToUpperInvariant()+"\n\n"+civ.description,new Rect(25+(i%2)*414,135+(i/2)*198,400,184),()=>{if(opponent)game.OpponentCivilization=civ.id;else game.PlayerCivilization=civ.id;ShowMenu();},civ.description);
            }
            Button(box,"PREVIOUS",new Rect(25,560,210,55),()=>ShowCivilizations(opponent,page-1),"",page>0);
            Button(box,"NEXT "+(page+1)+"/"+pages,new Rect(255,560,210,55),()=>ShowCivilizations(opponent,page+1),"",page+1<pages);
        }
        private void ShowDeckEditor(int page)
        {
            var cards=DeckLibrary.Available(game.Rules,game.PlayerCivilization);int pages=Mathf.CeilToInt(cards.Count/8f);page=Mathf.Clamp(page,0,pages-1);
            var box=Modal("DECK · "+game.SelectedDeck.Count+"/"+game.Rules.deckCapacity,690);
            Label(box,"Select up to "+game.Rules.deckCapacity+" cards. Changes apply to the next match.\n"+DeckLibrary.Validate(game.Rules,game.PlayerCivilization,game.SelectedDeck),new Rect(25,65,820,70),18,muted);
            for(int i=0;i<8&&page*8+i<cards.Count;i++)
            {var c=cards[page*8+i];bool chosen=game.SelectedDeck.Contains(c.id);Button(box,(chosen?"[IN DECK] ":"[ADD] ")+c.name+" · AGE "+c.age+"\n"+CardSummary(c),new Rect(25+(i%2)*414,145+(i/2)*112,400,100),()=>{game.ToggleDeckCard(c.id);ShowDeckEditor(page);},c.description);}
            Button(box,"PREVIOUS",new Rect(25,610,190,52),()=>ShowDeckEditor(page-1),"",page>0);
            Button(box,"NEXT "+(page+1)+"/"+pages,new Rect(230,610,190,52),()=>ShowDeckEditor(page+1),"",page+1<pages);
            Button(box,"DONE",new Rect(630,610,210,52),()=>{CloseModal();ShowMenu();});
        }
        public void ShowGame()
        {
            batchTraining=false;lastShift=false;CloseModal();ResultVisible=false;if(menuPanel!=null){Destroy(menuPanel.gameObject);menuPanel=null;}if(gamePanel!=null)Destroy(gamePanel.gameObject);
            gamePanel=Rect(root,"Skirmish HUD",new Rect(0,0,1600,900));Fill(gamePanel);
            var top=Panel(gamePanel,"Resource and age bar",new Rect(0,0,1600,64),background);
            Label(top,"FR",new Rect(16,14,52,40),27,gold);
            resources=Label(top,"",new Rect(83,14,1160,40),22);
            clockLabel=Label(top,"",new Rect(1190,19,180,30),17,muted);
            Button(top,"HOME CITY  [H]",new Rect(1352,10,162,43),ShowHomeCity,"Spend earned shipment opportunities.");
            Button(top,"II",new Rect(1525,10,57,43),()=>game.TogglePause(),"Pause [Esc]");
            var bottom=Panel(gamePanel,"Orders and economy",new Rect(10,10,1580,208),background,true);
            var mini=Rect(bottom,"Interactive minimap",new Rect(12,13,181,181));var image=mini.gameObject.AddComponent<RawImage>();image.texture=game.View.Minimap;minimapRect=mini;
            selection=Label(bottom,"",new Rect(213,14,530,30),23,gold);
            details=Label(bottom,"",new Rect(213,50,515,90),15);
            queuePanel=Rect(bottom,"Production queue",new Rect(213,145,530,48));
            actionPanel=Rect(bottom,"Context actions",new Rect(757,14,807,180));
            orders=Label(gamePanel,"",new Rect(22,82,610,32),17,gold);
            Label(gamePanel,"CAMERA: arrow keys / pointer near window edge\nMINIMAP: left click to view / right click to move",new Rect(840,80,735,54),15,muted);
            var messagePanel=Panel(gamePanel,"Events",new Rect(222,226,1100,38),new Color(.07f,.12f,.13f,.87f),true);messagePanel.GetComponent<Image>().raycastTarget=false;
            notice=Label(messagePanel,"",new Rect(12,8,1070,27),17,gold);
            var helpPanel=Rect(gamePanel,"Tooltips",new Rect(222,269,1110,75),true);help=Label(helpPanel,"",new Rect(0,0,1110,72),17);
            actionEntity=-1;actionSelection.Clear();InvalidateActions();
        }
        internal bool NeedsUiLayoutAt(Vector2 point)
        {
            if(InputLayoutReady)return false;
            Canvas.ForceUpdateCanvases();
            if(TryMinimapPoint(point,out _))return false;
            // Graphic.depth may still be -1 before its first render. Use only
            // rectangle coverage to retain UI edges, not to choose a click target.
            var graphics=GraphicRegistry.GetGraphicsForCanvas(canvas);
            // UGUI's IndexedSet implements indexing but not GetEnumerator.
            for(int i=0;i<graphics.Count;i++)
            {var graphic=graphics[i];if(graphic!=null&&graphic.isActiveAndEnabled&&graphic.raycastTarget&&RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform,point))return true;}
            return false;
        }
        public bool ProcessUiPointer(Vector2 point,bool down)
        {if(uiLayoutDirty&&InputLayoutReady){Canvas.ForceUpdateCanvases();uiLayoutDirty=false;}if(down&&TryMinimapPoint(point,out _))return false;return uiPointer?.Process(point,down)??false;}
        public void CancelUiPointer()=>uiPointer?.Cancel();
        public bool BlocksWorldInput(Vector2 pointer)
        {
            if(game.InMenu||ModalVisible||game.IsPaused||ResultVisible)return true;
            RaycastHover(pointer);return hits.Count>0;
        }
        public bool BlocksCameraEdgePan(Vector2 pointer)
        {
            if(game.InMenu||ModalVisible||game.IsPaused||ResultVisible||TryMinimapPoint(pointer,out _))return true;
            RaycastHover(pointer);
            // Passive HUD panels may touch a whole screen edge. Only controls
            // and the minimap own hover; otherwise top/bottom panning is lost.
            foreach(var hit in hits)if(hit.gameObject.GetComponentInParent<Button>()!=null)return true;
            return false;
        }
        public bool TryMinimapPoint(Vector2 pointer,out Vector2 point)
        {
            point=default;
            if(game.InMenu||ModalVisible||game.IsPaused||ResultVisible||minimapRect==null||!RectTransformUtility.RectangleContainsScreenPoint(minimapRect,pointer))return false;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(minimapRect,pointer,null,out var local))return false;
            var rect=minimapRect.rect;point=new Vector2(Mathf.InverseLerp(rect.xMin,rect.xMax,local.x)*128,Mathf.InverseLerp(rect.yMin,rect.yMax,local.y)*128);return true;
        }
        private void Update()
        {
            if(game.InMenu||game.World==null||gamePanel==null)return;
            EnsureActionContextCurrent();
            var w=game.World;var p=w.Players[0];int idle=0;foreach(var e in w.Entities)if(e.Alive&&e.owner==0&&e.Worker&&e.garrisonedIn==0&&e.orders.Count==0)idle++;
            var display=new ResourceDisplay{amount=p.resources,xp=p.xp,threshold=w.Rules.initialXp+w.Rules.xpIncrease*p.xpLevels,used=p.usedPopulation,reserved=p.reservedPopulation,capacity=p.populationCap,age=p.age,shipments=p.shipmentTokens};
            if(lastResourceLabel!=resources||!display.Same(lastResources))
            {
                resources.text=$"FOOD  {p.resources.food:0}     WOOD  {p.resources.wood:0}     COIN  {p.resources.coin:0}     POP  {p.usedPopulation}+{p.reservedPopulation}/{p.populationCap}     AGE  {p.age}     XP  {p.xp:0}/{display.threshold:0}     SHIP  {p.shipmentTokens}";
                lastResources=display;lastResourceLabel=resources;
            }
            ResourceSummaryFrame=Time.frameCount;ResourceSummaryTime=w.Time;ResourceSummaryMatchId=game.MatchId;
            UpdateHomeCity();
            if(lastClockLabel!=clockLabel||lastClockSeconds!=(int)w.Time||lastIdle!=idle)
            {clockLabel.text=$"{(int)w.Time/60:00}:{(int)w.Time%60:00}   IDLE {idle}";lastClockLabel=clockLabel;lastClockSeconds=(int)w.Time;lastIdle=idle;}
            orders.text=game.Controls.BuildingToPlace!=""?"PLACING  "+game.Rules.Entity(game.Controls.BuildingToPlace).name:game.Controls.RepairMode?"REPAIR [R] — CHOOSE A DAMAGED FRIENDLY TARGET":game.Controls.GarrisonMode?"GARRISON [G] — CHOOSE A FRIENDLY SHELTER":game.Controls.AttackMode?"ATTACK MOVE — CHOOSE DESTINATION":"[V] Idle settler     [T] Town center     [M] Military     [B] Build";
            notice.text=game.Notice;
            help.text=Help;
            var selected=game.Controls.FirstSelected;
            if(selected==null){selection.text="THE FRONTIER AWAITS";details.text="Select units or drag a selection box.\nRight click to move, gather, build or attack.\nShift queues orders · Ctrl+0–9 saves groups.";}
            else
            {
                selection.text=game.Controls.Selected.Count>1?game.Controls.Selected.Count+" SELECTED  ·  "+selected.def.name:selected.def.name.ToUpperInvariant();
                details.text=$"HP {selected.hp:0}/{w.MaxHp(selected):0}     ATK {w.Attack(selected):0}     RANGE {(selected.forceMelee?1.4f:selected.def.range):0.#}     POP {selected.def.population}\n{selected.status}  ·  Orders {selected.orders.Count}";
                if(selected.owner==0&&selected.def.id=="town"&&w.AutoRecruitSeconds(0)>0)details.text+="\n"+AutoRecruitSummary(selected);
                else if(selected.def.id=="caravan")details.text+="\n"+CaravanSummary(selected);
                else if(selected.def.id=="workshop")details.text+=$"\nArtisans in place: {w.AssignedArtisans(selected)} · Prefabs: {p.prefabCharges}";
                else if(selected.def.id=="factory")details.text+="\nProduction: "+selected.factoryMode.ToUpperInvariant()+" · "+selected.factoryElapsed.ToString("0.0")+"s";
                else if(selected.def.shieldAbility)details.text+="\nShield: "+(selected.shieldReadyAt>w.Time?"CHANGING STANCE · "+(selected.shieldReadyAt-w.Time).ToString("0.0")+"s":selected.shielded?"BRACED · movement folds shield":"FOLDED · mobile");
                else if(selected.def.resource!=ResourceKind.None)details.text+=$"\n{selected.def.resource}: {(World.Sustainable(selected)?"sustainable":selected.remaining.ToString("0"))}  ·  {selected.def.gatherRate:0.##}/s per settler";
                else if(selected.def.garrisonAttackLimit>0)details.text+=$"\nSheltered {selected.occupants.Count}/{selected.def.garrison} · Firing settlers {w.GarrisonFirepowerCount(selected)}/{selected.def.garrisonAttackLimit}";
                else details.text+="\n"+w.LandmarkDescription(0,selected);
                if(selected.owner==0&&selected.def.kind==EntityKind.Building)
                    details.text+="\nRally: "+(selected.rallyTarget==0?"move to flag":"gather "+selected.rallyResource+" near resource flag");
            }
            if(lastShift!=game.Controls.Shift){lastShift=game.Controls.Shift;InvalidateActions();}
            int key=CurrentActionKey(selected);
            if((key!=actionKey||Time.unscaledTime>refreshAt)&&!(uiPointer?.IsPressed??false)&&!(UnityEngine.InputSystem.Mouse.current?.leftButton.isPressed??false)){actionKey=key;refreshAt=Time.unscaledTime+1;RebuildActions(selected);}
            if(game.Controls.Dragging)
            {
                dragBox.gameObject.SetActive(true);dragBox.transform.SetAsLastSibling();var a=game.Controls.DragStart/canvas.scaleFactor;var b=game.Controls.DragEnd/canvas.scaleFactor;
                var t=dragBox.rectTransform;t.anchorMin=t.anchorMax=Vector2.zero;t.pivot=Vector2.zero;t.anchoredPosition=Vector2.Min(a,b);t.sizeDelta=new Vector2(Mathf.Abs(a.x-b.x),Mathf.Abs(a.y-b.y));
            }
            else dragBox.gameObject.SetActive(false);
        }
        private void ClearChildren(Transform parent){MarkUiLayoutDirty();for(int i=parent.childCount-1;i>=0;i--){var child=parent.GetChild(i).gameObject;child.SetActive(false);Destroy(child);}}
        private void RebuildActions(EntityState e)
        {
            ClearChildren(actionPanel);ClearChildren(queuePanel);if(e==null)return;int n=0;
            Action<string,Action,string,bool> add=(title,click,tooltip,enabled)=>{int i=n++;Button(actionPanel,title,new Rect((i%5)*162,(i/5)*57,155,50),click,tooltip,enabled);};
            if(e.owner!=0){add("DETAILS",()=>ShowObjectDetails(e),"Read attributes, resistances and counters.",true);return;}
            if(e.def.kind==EntityKind.Unit)
            {
                add("ATTACK MOVE [A]",()=>game.Controls.BeginAttackMove(),"Attack enemies encountered while travelling.",true);
                add("STOP [S]",()=>game.Controls.IssueOrder(OrderKind.Stop,Vector2.zero),"Stop orders. Attack cooldown continues.",true);
                add("HOLD [J]",()=>game.Controls.IssueOrder(OrderKind.Hold,Vector2.zero),"Defend this position without chasing.",true);
                if(e.Worker)add("BUILD [B]",ShowBuildMenu,"Choose a building. Shift placement queues construction.",true);
                if(e.def.id=="factory_wagon")add("BUILD FACTORY",()=>game.Controls.BeginBuild("factory"),"Place an industrial factory in current vision. This wagon is consumed when the foundation is placed.",true);
                add("FOCUS [F]",()=>game.Controls.Focus(e.position),"Center the camera on selection.",true);
                if(!e.Worker&&e.def.meleeDamage>0&&e.def.range>2)add(e.forceMelee?"RANGED MODE":"MELEE MODE",()=>{game.Command(new GameCommand{player=0,kind=CommandKind.ToggleMode,actors=game.Controls.Selected.ToArray()});},"Switch the selected unit's preferred combat mode.",true);
            }
            if(e.def.kind==EntityKind.Building)
            {
                if(!e.Complete){add("CANCEL BUILD",()=>game.Command(new GameCommand{player=0,kind=CommandKind.CancelBuilding,actors=new[]{e.id}}),"Refund 75% of the original construction cost.",true);return;}
                bool supportsBatch=false;
                foreach(var d in game.Rules.entities)if(e.def.id!="factory"&&d.kind==EntityKind.Unit&&d.producer==e.def.id&&game.World.EntityAvailable(0,d))
                {
                    // Automatic Ottoman settlers use their own visible timer and
                    // toggle. A second paid train button would misstate their economy.
                    if((d.tags&Tags.Villager)!=0&&game.World.AutoRecruitSeconds(0)>0)continue;
                    supportsBatch|=d.batch>1;var unit=d;int count=TrainingCount(unit);
                    var producer=TrainingProducer(e,unit,count);string reason=game.World.ProductionReason(0,producer,unit.id,false,count);
                    add(d.name+(count>1?" ×"+count:"")+"\n"+ShortCost(game.World.UnitCost(0,d)*count),()=>{
                        int requested=TrainingCount(unit);var chosen=TrainingProducer(e,unit,requested);
                        game.Command(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{chosen.id},item=unit.id,count=requested});InvalidateActions();
                    },d.description+"\nAge "+game.World.UnitAge(0,d)+" · "+count+" unit(s), "+(count*d.population)+" population, "+d.time+"s.\n"+(count*d.trainXp).ToString("0.#")+" XP on completed training. "+reason,reason=="");
                }
                if(supportsBatch)add(batchTraining?"TRAIN: BATCH":"TRAIN: SINGLE",()=>{batchTraining=!batchTraining;InvalidateActions();},"Choose one unit or the allowed batch per click. Shift also requests a batch. Costs and population are paid for the whole batch.",true);
                if(e.def.id=="factory")foreach(string mode in new[]{"food","wood","coin","artillery"})
                {
                    string chosen=mode;add((e.factoryMode==mode?"[ACTIVE] ":"")+mode.ToUpperInvariant(),()=>{game.Command(new GameCommand{player=0,kind=CommandKind.FactoryMode,actors=new[]{e.id},item=chosen});InvalidateActions();},"Produce resources or civilization artillery. Changing modes resets artillery progress. Research pauses production.",true);
                }
                if(e.def.id=="trade_post")for(int mode=0;mode<4;mode++){int chosen=mode;string label=mode==0?"XP":((ResourceKind)mode).ToString();add((mode==(int)e.tradeMode?"[ACTIVE] ":"")+label,()=>{game.Command(new GameCommand{player=0,kind=CommandKind.TradeMode,actors=new[]{e.id},index=chosen});InvalidateActions();},"Choose the reward paid when the caravan reaches this site.",mode==0||game.World.Players[0].technologies.Contains("cargo_ledgers"));}
                if(e.def.id=="town")add("ADVANCE AGE",ShowAgeChoices,"Queue the next age with a governor reward. "+game.World.AgeReason(0,e),game.World.AgeReason(0,e)=="");
                if(e.def.id=="town"&&game.World.AutoRecruitSeconds(0)>0)add(e.autoRecruitPaused?"AUTO SETTLERS: OFF":"AUTO SETTLERS: ON",()=>{game.Command(new GameCommand{player=0,kind=CommandKind.ToggleRecruitment,actors=new[]{e.id}});InvalidateActions();},"Free recruitment at each town center. Toggle to pause or resume. Worker limit, housing, safe exits and mosque technologies still apply.",true);
                if(e.def.id=="workshop")add("MANUFACTURE",()=>ShowManufacture(e,0),"Assign settlers by right-clicking this workshop. Orders need artisans physically working here.",true);
                if(Array.Exists(game.Rules.techs,t=>t.building==e.def.id&&(string.IsNullOrEmpty(t.civilization)||t.civilization==game.World.Players[0].civilization)))add("TECHNOLOGIES",()=>ShowTechnologies(e,0),"Research upgrades for current and future units.",true);
                if(e.def.garrison>0)add("UNGARRISON "+e.occupants.Count,()=>game.Command(new GameCommand{player=0,kind=CommandKind.Ungarrison,actors=new[]{e.id}}),"Release sheltered units at a safe exit.",e.occupants.Count>0);
                add("HOME CITY [H]",ShowHomeCity,"Request a shipment from your chosen deck.",true);
                add("DEMOLISH",()=>{var box=Modal("DEMOLISH "+e.def.name.ToUpperInvariant(),300);Label(box,"Remove this building and release its footprint. No refund.",new Rect(25,85,800,60),20);Button(box,"DEMOLISH BUILDING",new Rect(25,175,397,70),()=>{game.Command(new GameCommand{player=0,kind=CommandKind.Demolish,actors=new[]{e.id}});CloseModal();});Button(box,"KEEP BUILDING",new Rect(443,175,397,70),CloseModal);},"Destroy this completed building without refund.",true);
            }
            // Common squad controls keep their slots when the leading unit dies
            // and another soldier type becomes the first selected object.
            if(e.def.kind==EntityKind.Unit)n=5;
            add("DETAILS",()=>ShowObjectDetails(e),"Read attributes, resistances and counters.",true);
            add("GROUPS",ShowGroups,"Save and recall selected objects. Ctrl / Command + number also saves.",true);
            if(e.def.kind==EntityKind.Unit)add("GARRISON [G]",()=>game.Controls.BeginGarrison(),"Choose a completed friendly shelter, including a damaged one. Capacity is checked on arrival.",true);
            if(e.Worker||e.def.repairArtillery)add("REPAIR [R]",()=>game.Controls.BeginRepair(),e.Worker?"Choose a damaged friendly building. Use this to repair a workshop instead of manufacturing.":"Choose damaged friendly artillery to repair.",true);
            if(e.Worker&&e.def.meleeDamage>0&&e.def.range>2)add(e.forceMelee?"RANGED MODE":"MELEE MODE",()=>game.Command(new GameCommand{player=0,kind=CommandKind.ToggleMode,actors=game.Controls.Selected.ToArray()}),"Switch the selected worker's preferred combat mode.",true);
            if(e.def.id=="caravan")add("TRADE ROUTE",()=>ShowCaravanRoutes(e,0),"Choose a completed waystation. The cart loads paid cargo at a nearby town and earns coin only on arrival. Move / Stop abandons cargo without refund.",true);
            if(e.def.shieldAbility)add(e.shielded?"FOLD SHIELD":"BRACE SHIELD",()=>{game.Command(new GameCommand{player=0,kind=CommandKind.ToggleShield,actors=game.Controls.Selected.ToArray()});InvalidateActions();},"Changing stance takes 2 seconds. Bracing protects against ranged fire and prevents movement; moving or pursuing an attack folds the shield first.",game.World.Time>=e.shieldReadyAt);
            for(int i=0;i<e.queue.Count&&i<6;i++)
            {
                var job=e.queue[i];string name=job.kind=="Age"?"Age "+job.item:game.Rules.Entity(job.item)?.name??game.Rules.Tech(job.item)?.name??Array.Find(game.Rules.manufactureRecipes,r=>r.id==job.item)?.name??job.item;
                Button(queuePanel,name+(job.count>1?" ×"+job.count:"")+"\n"+Mathf.Max(0,job.duration-job.elapsed).ToString("0")+"s",new Rect(i*86,0,80,47),()=>{
                    // Production can finish while this pointer gesture is held.
                    // Follow the clicked job if it moved forward in the queue;
                    // a completed job must never cancel its successor.
                    int index=e.queue.IndexOf(job);
                    if(index>=0)game.Command(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{e.id},index=index});
                    else game.Notify("This production order has already finished");
                    InvalidateActions();
                },"Click to cancel this unfinished order and refund its paid cost.");
            }
        }
        private int TrainingCount(EntityDef unit)=>(batchTraining||game.Controls.Shift)?Mathf.Max(1,unit.batch):1;
        private EntityState TrainingProducer(EntityState fallback,EntityDef unit,int count)
        {
            EntityState best=fallback;bool ready=game.World.ProductionReason(0,best,unit.id,false,count)=="";
            foreach(int id in game.Controls.Selected)
            {
                var other=game.World.Get(id);if(other==null||!other.Alive||!other.Complete||other.owner!=0||other.def.id!=fallback.def.id)continue;
                bool available=game.World.ProductionReason(0,other,unit.id,false,count)=="";
                if(available&&!ready||available==ready&&other.queue.Count<best.queue.Count){best=other;ready=available;}
            }
            return best;
        }
        private string ShortCost(Cost c){string text="";if(c.food>0)text+=c.food+"F ";if(c.wood>0)text+=c.wood+"W ";if(c.coin>0)text+=c.coin+"C";return text.Trim();}
        private Transform Modal(string title,float height=640,Action onClose=null)
        {
            CloseModal();var dim=Panel(root,"Modal dim",new Rect(0,0,1600,900),new Color(.025f,.05f,.06f,.78f));Fill(dim);modal=dim;
            var box=Panel(dim,title,new Rect(365,95,870,height),background);Label(box,title,new Rect(25,22,740,40),29,gold);Button(box,"X",new Rect(798,15,48,43),()=>{if(onClose!=null)onClose();else{CloseModal();if(game.InMenu)ShowMenu();}});return box;
        }
        public void CloseModal(){MarkUiLayoutDirty();homeTransit=null;homeTitle=null;homeButtons.Clear();homeCards.Clear();if(modal!=null){modal.gameObject.SetActive(false);Destroy(modal.gameObject);modal=null;}Help="";}
        public void ShowBuildMenu()
        {
            game.Controls.CancelTargetMode();
            bool worker=game.Controls.Selected.Exists(id=>game.World.Get(id)?.Worker==true),wagon=game.Controls.Selected.Exists(id=>game.World.Get(id)?.def.id=="factory_wagon");
            if(game.Controls.FirstSelected==null||(!worker&&!wagon)){game.Notify("Select settlers or a factory wagon before opening the building menu");return;}
            var box=Modal("CONSTRUCTION",640);int n=0;var p=game.World.Players[0];
            foreach(var d in game.Rules.entities)if(d.kind==EntityKind.Building&&game.World.EntityAvailable(0,d)&&(d.id!="dock"||game.World.Layout.HasWater)&&(d.id=="factory"?wagon:worker))
            {var def=d;int i=n++;int age=game.World.UnitAge(0,d);Button(box,d.name+"  ·  "+ShortCost(game.World.EntityCost(0,d))+"\nAge "+age+"  ·  "+d.time+"s",new Rect(25+(i%3)*275,90+(i/3)*133,265,117),()=>{CloseModal();game.Controls.BeginBuild(def.id);},d.description,p.age>=age);}
        }
        public void ShowGroups()
        {
            var box=Modal("CONTROL GROUPS",620);
            Label(box,"Save selection with Ctrl / Command + 0–9; recall with 0–9.\nUse these buttons for the same commands.",new Rect(25,70,800,60),17,muted);
            for(int i=0;i<10;i++)
            {
                int group=i;int column=i%2,row=i/2;int count=game.Controls.GroupCount(group);
                Button(box,"SAVE "+group,new Rect(25+column*414,145+row*83,125,64),()=>{game.Controls.SaveGroup(group);CloseModal();},"Replace this group with current selection.",game.Controls.Selected.Count>0);
                Button(box,"RECALL "+group+" · "+count+" objects",new Rect(157+column*414,145+row*83,265,64),()=>{game.Controls.RecallGroup(group);CloseModal();},"Select living members and center the camera.",count>0);
            }
        }
        public void ShowHomeCity(){ShowHomeCityPage(0);}
        private void ShowHomeCityPage(int page)
        {
            if(game.World==null)return;var p=game.World.Players[0];var box=Modal("HOME CITY",690);homeTitle=box.GetComponentInChildren<Text>();
            Label(box,p.deck.Count+"-card deck · XP comes from real match events.\nShipments travel in sequence. Crates must be collected by settlers.",new Rect(25,70,825,60),17,muted);
            for(int i=0;i<8&&page*8+i<p.deck.Count;i++)
            {
                var card=game.Rules.Card(p.deck[page*8+i]);bool sent=p.sentCards.Contains(card.id);string reason=game.World.ShipmentReason(0,card.id);bool available=reason=="";
                string state=sent&&!card.repeatable?"SENT":card.repeatable?"REPEATABLE":"ONCE";
                var cardButton=Button(box,$"{card.name.ToUpperInvariant()}  ·  AGE {card.age}\n{CardSummary(card)}\n{state}  ·  {card.travel:0}s transit",new Rect(25+(i%2)*414,145+(i/2)*112,400,101),()=>{game.Command(new GameCommand{player=0,kind=CommandKind.Ship,item=card.id});ShowHomeCityPage(page);},CardSummary(card)+"\n"+reason,available);homeButtons.Add(cardButton);homeCards.Add(card);
            }
            Button(box,"PREV",new Rect(25,660,130,25),()=>ShowHomeCityPage(page-1),"",page>0);
            Button(box,"NEXT "+(page+1)+"/"+Mathf.CeilToInt(p.deck.Count/8f),new Rect(175,660,190,25),()=>ShowHomeCityPage(page+1),"",(page+1)*8<p.deck.Count);
            string queue="";foreach(var s in p.shipments){var c=game.Rules.Card(s.card);queue+=c.name+" "+(s.waiting!=""?s.waiting:Mathf.Max(0,c.travel-s.elapsed).ToString("0")+"s")+"    ";}
            homeTransit=Label(box,p.shipments.Count==0?"No shipments in transit.":"IN TRANSIT  "+queue,new Rect(25,610,820,60),17,gold);
        }
        private string TechSummary(TechDef t)
        {
            if(t==null)return "";var parts=new List<string>();
            if(t.id=="cargo_ledgers")parts.Add("choose resources; per visit +"+((game.Rules.landmarks.tradeUpgradeMultiplier-1)*100).ToString("0")+"%");
            if(t.attack!=0)parts.Add("attack +"+(t.attack*100).ToString("0")+"%");
            if(t.health!=0)parts.Add("health +"+(t.health*100).ToString("0")+"%");
            if(t.gather!=0)parts.Add((t.resource==ResourceKind.None?"gather":t.resource+" gather")+" +"+(t.gather*100).ToString("0")+"%");
            if(t.bankLimit!=0)parts.Add("bank limit +"+t.bankLimit);
            if(t.bankIncome!=0)parts.Add("bank income +"+(t.bankIncome*100).ToString("0")+"%");
            if(t.speed!=0)parts.Add("speed +"+(t.speed*100).ToString("0")+"%");
            if(t.caravanProfit!=0)parts.Add("caravan profit +"+(t.caravanProfit*100).ToString("0")+"%");
            if(t.manufactureSpeed!=0)parts.Add("manufacture +"+(t.manufactureSpeed*100).ToString("0")+"%");
            if(t.buildSpeed!=0)parts.Add("construction +"+(t.buildSpeed*100).ToString("0")+"%");
            if(t.trainSpeed!=0)parts.Add("training +"+(t.trainSpeed*100).ToString("0")+"%");
            if(t.villagerLimit!=0)parts.Add("settler limit +"+t.villagerLimit);
            if(t.autoVillagerSpeed!=0)parts.Add("automatic settlers +"+(t.autoVillagerSpeed*100).ToString("0")+"% speed");
            return string.Join(", ",parts);
        }
        private string CardSummary(CardDef card)
        {
            var unit=game.Rules.Entity(card.unit);if(unit!=null)return card.count+" × "+unit.name+"  ·  "+(card.count*unit.population)+" population";
            var tech=game.Rules.Tech(card.tech);if(tech!=null)return TechSummary(tech);
            return "Collect crates: "+ShortCost(card.crates);
        }
        private void ShowObjectDetails(EntityState entity)
        {
            var d=entity.def;var w=game.World;var box=Modal(d.name.ToUpperInvariant()+" · DETAILS",610);
            string text=d.description+"\n\nTags: "+d.tags+"\n";
            text+=$"Health {entity.hp:0}/{w.MaxHp(entity):0}    Preferred attack {w.Attack(entity):0.#}    Base siege {d.siegeDamage:0.#}\n";
            text+=$"Range {(entity.forceMelee?1.4f:d.range):0.#}    Minimum {d.minRange:0.#}    Attack interval {d.period:0.#}s    Preparation {d.windup:0.##}s\n";
            text+=$"Speed {w.Speed(entity):0.#}    Vision {d.vision:0.#}    Population {d.population}\n";
            text+=$"Experience: build {d.buildXp:0.#} / paid training {d.trainXp:0.#} / enemy kill {d.killXp:0.#}\n";
            text+=$"Resistance: melee {d.meleeResist*100:0}% / ranged {d.rangedResist*100:0}% / siege {d.siegeResist*100:0}%\n\n";
            text+="Target bonuses (highest matching bonus applies):\n";foreach(var bonus in d.bonuses)text+=bonus.tags+" ×"+bonus.multiplier.ToString("0.##")+"    ";
            if(d.bonuses.Length==0)text+="None";
            if(d.meleeBonuses.Length>0){text+="\nMelee-only bonuses: ";foreach(var bonus in d.meleeBonuses)text+=bonus.tags+" ×"+bonus.multiplier.ToString("0.##")+"    ";}
            if(d.meleeSplash>0)text+="\nMelee area radius: "+d.meleeSplash.ToString("0.##");
            text+="\n\nCost: "+(entity.owner>=0?w.UnitCost(entity.owner,d).ToString():d.cost.ToString())+"    Time "+d.time+"s    Age "+(entity.owner>=0?w.UnitAge(entity.owner,d):d.age);
            if(entity.owner==0){text+="\nActive matching upgrades: ";foreach(var id in w.Players[entity.owner].technologies){var t=game.Rules.Tech(id);if((t.units==null||t.units.Length==0||Array.IndexOf(t.units,d.id)>=0)&&(t.tags==Tags.None||(t.tags&d.tags)!=0))text+=t.name+" ("+TechSummary(t)+")  ";}}
            Label(box,text,new Rect(25,90,815,485),18);
        }
        private void UpdateHomeCity()
        {
            if(homeTransit==null||game.World==null)return;var p=game.World.Players[0];
            homeTitle.text="HOME CITY  ·  "+p.shipmentTokens+" OPPORTUNITIES";
            for(int i=0;i<homeButtons.Count;i++){var c=homeCards[i];homeButtons[i].interactable=game.World.ShipmentReason(0,c.id)=="";}
            string queue="";foreach(var s in p.shipments){var c=game.Rules.Card(s.card);queue+=c.name+" "+(s.waiting!=""?s.waiting:Mathf.Max(0,c.travel-s.elapsed).ToString("0")+"s")+"  ";}
            foreach(var reward in p.rewards)queue+="Governor: "+reward.waiting+"  ";
            homeTransit.text=queue==""?"No shipments in transit.":"IN TRANSIT  "+queue;
        }
        private void ShowTechnologies(EntityState producer,int page)
        {
            var list=new List<TechDef>();foreach(var t in game.Rules.techs)if(t.building==producer.def.id&&(string.IsNullOrEmpty(t.civilization)||t.civilization==game.World.Players[0].civilization))list.Add(t);
            list.Sort((a,b)=>a.age!=b.age?a.age.CompareTo(b.age):string.CompareOrdinal(a.id,b.id));
            var box=Modal(producer.def.name.ToUpperInvariant()+" · TECHNOLOGIES",690);
            for(int i=0;i<8&&page*8+i<list.Count;i++)
            {
                var t=list[page*8+i];string reason=game.World.ProductionReason(0,producer,t.id,true);
                Button(box,t.name+" · AGE "+t.age+"\n"+TechSummary(t)+"\n"+ShortCost(t.cost)+" · "+t.time+"s\n"+reason,new Rect(25+(i%2)*414,90+(i/2)*125,400,115),()=>{game.Command(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{producer.id},item=t.id});ShowTechnologies(producer,page);},t.description,reason=="");
            }
            Button(box,"PREVIOUS",new Rect(25,615,220,55),()=>ShowTechnologies(producer,page-1),"",page>0);
            Button(box,"NEXT",new Rect(265,615,220,55),()=>ShowTechnologies(producer,page+1),"",(page+1)*8<list.Count);
        }
        public void ShowAgeChoices()
        {
            var e=game.Controls.FirstSelected;if(e==null||e.def.id!="town")return;var p=game.World.Players[0];var a=game.Rules.Age(p.age+1);if(a==null||p.age>=game.World.MaxAge)return;
            var choices=game.World.AgeChoices(0,a.age);var box=Modal("ADVANCE TO AGE "+a.age,Mathf.Max(430,195+choices.Count*139));
            Label(box,a.cost+"  ·  "+a.time+"s in the town-center queue",new Rect(25,83,800,40),22);
            for(int i=0;i<choices.Count;i++)
            {
                var choice=choices[i];var rewards=new List<string>();if(choice.crates.food+choice.crates.wood+choice.crates.coin>0)rewards.Add("Crates: "+ShortCost(choice.crates));
                if(choice.count>0)rewards.Add(choice.count+" × "+game.Rules.Entity(choice.unit)?.name);
                if(!string.IsNullOrEmpty(choice.tech))rewards.Add(game.Rules.Tech(choice.tech)?.name+": "+TechSummary(game.Rules.Tech(choice.tech)));
                Button(box,choice.name.ToUpperInvariant()+"\n"+choice.description+"\n"+string.Join(" · ",rewards),new Rect(25,145+i*139,815,128),()=>{var result=game.Command(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{e.id},choice=choice.id});if(result.accepted)CloseModal();},"Rewards and the selected technology arrive when this age completes. "+game.World.AgeReason(0,e),game.World.AgeReason(0,e)=="");
            }
        }
        private string CaravanSummary(EntityState cart)
        {
            if(cart.routeLeg==0)return "No trade route · choose TRADE ROUTE to begin";
            string leg=cart.routeLeg==1?"Travelling to town / loading":cart.routeLeg==2?"Paid cargo outbound":"Returning to town";
            return leg+" · Cargo paid: "+(cart.cargoPaid.food+cart.cargoPaid.wood+cart.cargoPaid.coin>0?ShortCost(cart.cargoPaid):"none");
        }
        private string AutoRecruitSummary(EntityState town)
        {
            var w=game.World;int workers=0;foreach(var e in w.Entities)if(e.Alive&&e.owner==town.owner&&e.Worker)workers++;
            float seconds=w.AutoRecruitSeconds(town.owner);string reason=w.AutoRecruitReason(town);
            return "Free settlers: "+Mathf.Min(town.autoRecruitElapsed,seconds).ToString("0.0")+"/"+seconds.ToString("0.0")+"s · Workers "+workers+"/"+w.WorkerLimit(town.owner)+"\n"+(reason==""?"Recruiting automatically":reason);
        }
        private void ShowCaravanRoutes(EntityState cart,int page)
        {
            var stations=new List<EntityState>();foreach(var e in game.World.Entities)if(e.Alive&&e.Complete&&e.owner==0&&e.def.id=="waystation")stations.Add(e);
            int pages=Mathf.Max(1,Mathf.CeilToInt(stations.Count/6f));page=Mathf.Clamp(page,0,pages-1);
            var box=Modal("CARAVAN · TRADE ROUTES",660);
            Label(box,CaravanSummary(cart)+"\nCargo is paid at the nearest town. Coin is earned at the waystation.\nProtect the cart: destruction or a manual Move / Stop loses its cargo.",new Rect(25,74,815,100),18,muted);
            if(stations.Count==0)Label(box,"Build and finish a Waystation to open a trade route.",new Rect(25,205,810,70),21,gold);
            for(int i=0;i<6&&page*6+i<stations.Count;i++)
            {
                var station=stations[page*6+i];string reason=game.World.CaravanRouteReason(0,cart,station);
                Button(box,station.def.name+" #"+station.id+"  ("+station.position.x.ToString("0")+", "+station.position.y.ToString("0")+")\n"+(reason==""?"Start automatic round trips":reason),new Rect(25+(i%2)*414,195+(i/2)*119,400,105),()=>{var result=game.Command(new GameCommand{player=0,kind=CommandKind.TradeRoute,actors=new[]{cart.id},target=station.id});if(result.accepted)CloseModal();},"New routes abandon previously paid cargo. The route must connect distinct, sufficiently separated endpoints.",reason=="");
            }
            Button(box,"PREVIOUS",new Rect(25,583,200,52),()=>ShowCaravanRoutes(cart,page-1),"",page>0);
            Button(box,"NEXT "+(page+1)+"/"+pages,new Rect(245,583,200,52),()=>ShowCaravanRoutes(cart,page+1),"",page+1<pages);
        }
        private void ShowManufacture(EntityState workshop,int page)
        {
            var recipes=game.Rules.manufactureRecipes;int pages=Mathf.Max(1,Mathf.CeilToInt(recipes.Length/6f));page=Mathf.Clamp(page,0,pages-1);
            var box=Modal("WORKSHOP · MANUFACTURE",660);
            Label(box,"Artisans in place: "+game.World.AssignedArtisans(workshop)+" · Available prefabs: "+game.World.Players[0].prefabCharges+"\nRight-click this workshop with settlers to assign artisans. Artisans stop gathering.\nProduction waits without workers. REPAIR [R] repairs a damaged workshop.\nResearch Standard Jigs in TECHNOLOGIES before ordering prefabs.",new Rect(25,74,815,100),18,muted);
            for(int i=0;i<6&&page*6+i<recipes.Length;i++)
            {
                var recipe=recipes[page*6+i];string reason=game.World.ManufactureReason(0,workshop,recipe.id);
                if(reason=="Requires "+recipe.requires)reason="Requires "+(game.Rules.Tech(recipe.requires)?.name??recipe.requires);
                Button(box,recipe.name+" · "+ShortCost(recipe.cost)+"\n"+recipe.time+"s · "+recipe.prefabs+" prefab(s)\n"+(reason==""?recipe.description:reason),new Rect(25+(i%2)*414,195+(i/2)*119,400,108),()=>{game.Command(new GameCommand{player=0,kind=CommandKind.Manufacture,actors=new[]{workshop.id},item=recipe.id});ShowManufacture(workshop,page);},recipe.description,reason=="");
            }
            Button(box,"PREVIOUS",new Rect(25,583,200,52),()=>ShowManufacture(workshop,page-1),"",page>0);
            Button(box,"NEXT "+(page+1)+"/"+pages,new Rect(245,583,200,52),()=>ShowManufacture(workshop,page+1),"",page+1<pages);
        }
        public void ShowPause(bool paused)
        {
            if(!paused){CloseModal();return;}var box=Modal("MATCH PAUSED",610,()=>game.TogglePause());
            string reason=game.PausedForFocusLoss?"Paused when you left the game window. Resume when ready.":"Simulation time is stopped. The cursor is free.";
            Label(box,reason,new Rect(25,83,800,50),20);
            Button(box,"RESUME",new Rect(25,150,397,65),()=>game.TogglePause());Button(box,"RESTART",new Rect(443,150,397,65),()=>game.StartMatch());
            Button(box,game.Muted?"SOUND: OFF":"SOUND: ON",new Rect(25,240,397,65),()=>{game.Muted=!game.Muted;ShowPause(true);});Button(box,"RESIGN",new Rect(443,240,397,65),()=>{CloseModal();game.Resign();});
            Button(box,game.EnvironmentSoundEnabled?"ENVIRONMENT: ON":"ENVIRONMENT: OFF",new Rect(25,330,397,65),()=>{game.EnvironmentSoundEnabled=!game.EnvironmentSoundEnabled;ShowPause(true);},"Enable or disable environmental sounds. SOUND controls all audio.");
            Button(box,game.MusicEnabled?"MUSIC: ON":"MUSIC: OFF",new Rect(443,330,397,65),()=>{game.MusicEnabled=!game.MusicEnabled;ShowPause(true);},"Enable or disable music. SOUND controls all audio.");
            Button(box,"SAVE MATCH",new Rect(25,421,397,65),()=>ShowSaveSlots(true));Button(box,"LOAD MATCH",new Rect(443,421,397,65),()=>ShowSaveSlots(false));
            Button(box,"RETURN TO MENU",new Rect(25,511,815,65),()=>game.ReturnToMenu());
        }
        public void ShowSaveSlots(bool saving)
        {
            void Back(){if(game.InMenu){CloseModal();game.Hud.ShowMenu();}else game.Hud.ShowPause(true);}
            var box=Modal(saving?"SAVE MATCH":"LOAD MATCH",510,Back);
            Label(box,saving?"Choose a slot. Saving to an occupied slot replaces that checkpoint.":"Choose a checkpoint. The loaded match starts paused.",new Rect(25,80,800,55),18,muted);
            for(int i=1;i<=3;i++)
            {
                int slot=i;bool exists=System.IO.File.Exists(MatchSaveStore.SlotPath(game.SaveDirectory,slot));
                Button(box,game.SaveSlotLabel(slot),new Rect(25,145+(slot-1)*90,815,70),()=>{if(saving){game.SaveMatch(slot);ShowSaveSlots(true);}else game.LoadMatch(slot);},"",saving||exists);
            }
            Button(box,"BACK",new Rect(25,425,815,60),Back);
        }
        public void ShowResult()
        {
            ResultVisible=true;var w=game.World;var box=Modal(w.Winner==0?"VICTORY":"DEFEAT",610);
            Label(box,"Complete elimination or resignation · "+((int)w.Time/60)+"m "+((int)w.Time%60)+"s\nStatistics below are accumulated from this match's simulation.",new Rect(25,84,825,65),19,muted);
            Label(box,"                           YOU                         OPPONENT",new Rect(25,180,820,40),19,gold);
            var a=w.Players[0];var b=w.Players[1];
            Label(box,$"FOOD GATHERED          {a.gathered.food:0}                         {b.gathered.food:0}\nWOOD GATHERED          {a.gathered.wood:0}                         {b.gathered.wood:0}\nCOIN GATHERED           {a.gathered.coin:0}                         {b.gathered.coin:0}\nKILLS / LOSSES             {a.kills}/{a.losses}                         {b.kills}/{b.losses}\nAGE REACHED              {a.age}                            {b.age}\nSHIPMENTS SENT          {a.shipsSent}                            {b.shipsSent}",new Rect(25,227,820,240),22);
            Button(box,"PLAY AGAIN",new Rect(25,514,397,64),()=>game.StartMatch());Button(box,"MAIN MENU",new Rect(443,514,397,64),()=>game.ReturnToMenu());
        }
    }
    public sealed class FrontierHover : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        public FrontierHud hud;public string message;private bool hovering;
        public void OnPointerEnter(PointerEventData data){hovering=true;hud.Help=message;}
        public void OnPointerExit(PointerEventData data){hovering=false;hud.Help="";}
        private void OnDisable(){if(hovering&&hud!=null&&hud.Help==message)hud.Help="";}
    }
    public sealed class FrontierUiTrace : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerClickHandler
    {
        public GameController game;
        private void Record(string phase,PointerEventData e)
        {Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"FRONTIER_UI {0} object={1} p={2} press={3} over={4} world={5:F3} frame={6}",phase,name.Replace('\n',' '),e.position,e.pointerPress?.name.Replace('\n',' '),e.pointerCurrentRaycast.gameObject?.name.Replace('\n',' '),game.World?.Time??0,Time.frameCount);}
        public void OnPointerDown(PointerEventData e)=>Record("down",e);
        public void OnPointerUp(PointerEventData e)=>Record("up",e);
        public void OnPointerClick(PointerEventData e)=>Record("click",e);
    }

}
