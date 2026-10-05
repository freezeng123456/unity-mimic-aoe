using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Edge = Frontier.DiscreteInputBuffer.Edge;
using EdgeKind = Frontier.DiscreteInputBuffer.EdgeKind;

namespace Frontier
{
    [System.Serializable] public sealed class ControlGroupSnapshot {public int number;public int[] actors;}
    public sealed class RtsInput : MonoBehaviour
    {
        private GameController game;
        private int Observer=>game.ObserverPlayer;
        private int LocalPlayer=>game.LocalPlayerSlot;
        private int GroupOwner=>game.IsWatching?Observer:LocalPlayer;
        private int targetGeneration;
        private World groundWorld;private Camera groundCamera;private Vector2 groundPointer,groundResult;private Matrix4x4 groundView,groundProjection;private Rect groundPixelRect;private bool groundCached,groundBoatsOnly;
        public readonly List<int> Selected=new List<int>();
        private readonly Dictionary<int,List<int>> groups=new Dictionary<int,List<int>>();
        public Vector2 CameraFocus {get;private set;}
        public Vector2 GroundMouse {get;private set;}
        public string BuildingToPlace="";
        public bool AttackMode;
        public bool GarrisonMode;
        public bool RepairMode;
        public bool EmbarkMode,DisembarkMode,DockRepairMode;
        public bool Dragging {get;private set;}
        public Vector2 DragStart {get;private set;}
        public Vector2 DragEnd {get;private set;}
        private bool beganWorld;
        private float zoom=26;private double lastClickTime;
        private string lastType;
        private Vector2 lastClickPosition;
        private const float ClickSlopSquared=64;
        private Vector2 lastMouse;
        private float suppressUntil;
        private DiscreteInputBuffer input;
        private bool inputTrace,beganMinimap;
        public void SuppressWorldInput(bool clearPending=true)
        {ResetDoubleClick();suppressUntil=Time.unscaledTime+.22f;beganWorld=false;beganMinimap=false;Dragging=false;if(clearPending)input?.DiscardPointerEdges();game?.Hud?.CancelUiPointer();}
        private void ResetDoubleClick(){lastType=null;lastClickTime=0;lastClickPosition=default;}
        private void OnDestroy(){if(game?.Settings!=null)game.Settings.Changed-=ClearTransientInput;input?.Dispose();}
        private void OnApplicationFocus(bool focused){if(inputTrace)Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"FRONTIER_FOCUS focused={0} frame={1}",focused,Time.frameCount);input?.SetFocus(focused);if(!focused)ClearTransientInput();}
        public void ClearTransientInput()
        {input?.Clear();SuppressWorldInput();}
        // HUD button batching retains its frame-based modifier convention. World
        // pointer/keyboard orders use the explicit per-event edge.shift instead.
        public bool Shift=>Keyboard.current!=null&&(Keyboard.current.leftShiftKey.isPressed||Keyboard.current.rightShiftKey.isPressed||Keyboard.current.leftShiftKey.wasPressedThisFrame||Keyboard.current.rightShiftKey.wasPressedThisFrame);
        public void Initialize(GameController controller)
        {game=controller;inputTrace=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-frontierInputTrace")>=0;input=new DiscreteInputBuffer(inputTrace,game.Settings);game.Settings.Changed+=ClearTransientInput;}
        public void Focus(Vector2 point){ResetDoubleClick();CameraFocus=new Vector2(Mathf.Clamp(point.x,7,121),Mathf.Clamp(point.y,7,121));UpdateCamera();}
        public void RestoreCamera(Vector2 focus,float size)
        {ResetDoubleClick();zoom=Mathf.Clamp(size,12,47);Focus(focus);groundCached=false;}
        public ControlGroupSnapshot[] CaptureGroups()
        {
            var result=new List<ControlGroupSnapshot>();for(int number=0;number<10;number++)if(groups.TryGetValue(number,out var members))result.Add(new ControlGroupSnapshot{number=number,actors=members.ToArray()});return result.ToArray();
        }
        public void RestoreGroups(System.Collections.Generic.IEnumerable<ControlGroupSnapshot> snapshots)
        {
            groups.Clear();if(snapshots==null||game.World==null)return;
            foreach(var snapshot in snapshots)
            {
                if(snapshot==null||snapshot.number<0||snapshot.number>9||snapshot.actors==null)continue;
                var members=new List<int>();foreach(int id in snapshot.actors){var e=game.World.Get(id);if(e!=null&&e.Alive&&e.owner==GroupOwner&&!members.Contains(id))members.Add(id);}groups[snapshot.number]=members;
            }
        }
        public void RestoreSelection(System.Collections.Generic.IEnumerable<int> members)
        {
            ResetSelection();if(members==null||game.World==null)return;
            foreach(int id in members){var e=game.World.Get(id);if(e!=null&&e.Alive&&e.owner>=-1&&e.owner<game.World.Players.Length&&game.World.IsVisible(Observer,e)&&!Selected.Contains(id))Selected.Add(id);}game.Hud?.InvalidateActions();
        }
        public void ResetSelection(){targetGeneration++;ResetDoubleClick();Selected.Clear();groups.Clear();BuildingToPlace="";AttackMode=false;GarrisonMode=false;RepairMode=false;EmbarkMode=false;DisembarkMode=false;DockRepairMode=false;Dragging=false;beganWorld=false;beganMinimap=false;input?.Clear();game?.Hud?.CancelUiPointer();}
        private void UpdateCamera()
        {
            if(game.GameCamera==null)return;
            float ground=game.World?.Layout.HasScenario==true?game.World.SurfaceHeight(CameraFocus):0;
            game.GameCamera.transform.position=new Vector3(CameraFocus.x,50+ground,CameraFocus.y-42);
            game.GameCamera.transform.rotation=Quaternion.Euler(50,0,0);game.GameCamera.orthographicSize=zoom;
        }
        public EntityState FirstSelected
        {
            get{if(game.World==null)return null;foreach(int id in Selected){var e=game.World.Get(id);if(e!=null&&e.Alive&&game.World.IsVisible(Observer,e))return e;}return null;}
        }
        public void PruneInvisibleSelection()
        {if(game.World==null)return;for(int i=Selected.Count-1;i>=0;i--)if(!game.World.IsVisible(Observer,game.World.Get(Selected[i])))Selected.RemoveAt(i);}
        public void Select(EntityState entity,bool append=false)
        {ResetDoubleClick();if(!append)Selected.Clear();if(entity==null||game.World==null||!game.World.IsVisible(Observer,entity))return;if(append&&Selected.Contains(entity.id))Selected.Remove(entity.id);else Selected.Add(entity.id);game.Hud.InvalidateActions();}
        public void SaveGroup(int number)
        {
            ResetDoubleClick();
            var members=new List<int>();foreach(int id in Selected){var e=game.World.Get(id);if(e!=null&&e.Alive&&e.owner==GroupOwner)members.Add(id);}
            groups[number]=members;game.Notify("Control group "+number+" saved · "+members.Count+" objects");
        }
        public int GroupCount(int number){if(!groups.TryGetValue(number,out var group))return 0;int count=0;foreach(int id in group){var e=game.World.Get(id);if(e!=null&&e.Alive&&e.owner==GroupOwner)count++;}return count;}
        public void RecallGroup(int number)
        {
            ResetDoubleClick();
            if(!groups.TryGetValue(number,out var group))return;Selected.Clear();foreach(int id in group){var e=game.World.Get(id);if(e!=null&&e.Alive&&e.owner==GroupOwner&&game.World.IsVisible(Observer,e))Selected.Add(id);}
            game.Hud.InvalidateActions();if(FirstSelected!=null)Focus(FirstSelected.position);
        }
        public void BeginBuild(string id){if(!game.CanSubmitOrders){CancelTargetMode();return;}SuppressWorldInput();CancelTargetMode();BuildingToPlace=id;game.Notify("Place "+game.Rules.Entity(id).name+". Right click or Esc cancels.");}
        public void CancelTargetMode(){targetGeneration++;ResetDoubleClick();BuildingToPlace="";AttackMode=false;GarrisonMode=false;RepairMode=false;EmbarkMode=false;DisembarkMode=false;DockRepairMode=false;}
        public void BeginAttackMove(){if(!game.CanSubmitOrders){CancelTargetMode();return;}CancelTargetMode();AttackMode=true;game.Notify("Attack move: choose a destination");}
        public void BeginGarrison()
        {
            if(!game.CanSubmitOrders){CancelTargetMode();return;}
            if(GarrisonMode){CancelTargetMode();game.Notify("Garrison targeting cancelled");return;}
            bool mobile=Selected.Exists(id=>{var e=game.World?.Get(id);return e!=null&&e.owner==LocalPlayer&&e.def.kind==EntityKind.Unit&&game.World.IsVisible(Observer,e);});
            if(!mobile){game.Notify("Select mobile units before choosing a shelter");return;}
            CancelTargetMode();GarrisonMode=true;game.Notify("Garrison: choose a completed friendly shelter. "+game.Settings.BindingLabel(PlayerAction.Garrison)+" / right click / Esc cancels.");
        }
        public void BeginEmbark(){if(!game.CanSubmitOrders){CancelTargetMode();return;}CancelTargetMode();EmbarkMode=true;game.Notify("Board: choose your troop transport. Right click or Esc cancels.");}
        public void BeginDisembark(){if(!game.CanSubmitOrders){CancelTargetMode();return;}CancelTargetMode();DisembarkMode=true;game.Notify("Unload: choose an open land shore. Right click or Esc cancels.");}
        public void BeginDockRepair(){if(!game.CanSubmitOrders){CancelTargetMode();return;}CancelTargetMode();DockRepairMode=true;game.Notify("Harbor repair: choose your completed dock. Right click or Esc cancels.");}
        public void BeginRepair()
        {
            if(!game.CanSubmitOrders){CancelTargetMode();return;}
            if(RepairMode){CancelTargetMode();game.Notify("Repair targeting cancelled");return;}
            bool capable=Selected.Exists(id=>{var e=game.World?.Get(id);return e!=null&&e.owner==LocalPlayer&&(e.Worker||e.def.repairArtillery)&&game.World.IsVisible(Observer,e);});
            if(!capable){game.Notify("Select settlers or field engineers before repairing");return;}
            CancelTargetMode();RepairMode=true;game.Notify("Repair: settlers repair buildings; field engineers repair artillery. "+game.Settings.BindingLabel(PlayerAction.Repair)+" / right click / Esc cancels.");
        }
        public void IssueOrder(OrderKind kind,Vector2 point,int target=0){if(kind==OrderKind.Stop||kind==OrderKind.Hold)CancelTargetMode();IssueOrder(kind,point,target,Shift);}
        private void IssueOrder(OrderKind kind,Vector2 point,int target,bool append,System.Action accepted=null)
        {ResetDoubleClick();if(!game.CanSubmitOrders)return;game.SubmitCommand(new GameCommand{player=LocalPlayer,kind=CommandKind.Order,actors=Selected.ToArray(),order=kind,point=point,target=target,append=append},accepted==null?null:GuardTargetResult(accepted));}
        private System.Action<CommandResult> GuardTargetResult(System.Action accepted)
        {
            var world=game.World;string match=game.MatchId,mode=TargetSignature();int player=LocalPlayer,generation=++targetGeneration;
            return result=>
            {
                if(this==null||!result.accepted||generation!=targetGeneration||mode!=TargetSignature()||
                    !ReferenceEquals(world,game.World)||match!=game.MatchId||player!=LocalPlayer)return;
                accepted?.Invoke();
            };
        }
        private string TargetSignature()=>BuildingToPlace+"/"+AttackMode+"/"+GarrisonMode+"/"+RepairMode+"/"+EmbarkMode+"/"+DisembarkMode+"/"+DockRepairMode;
        public void MinimapOrder(Vector2 point,bool right)=>MinimapOrder(point,right,Shift);
        private void MinimapOrder(Vector2 point,bool right,bool append)
        {
            ResetDoubleClick();if(!game.CanSubmitOrders){CancelTargetMode();if(!right)Focus(point);return;}
            if(right)ContextOrder(point,null,append);
            else if(DisembarkMode)game.SubmitCommand(new GameCommand{player=LocalPlayer,kind=CommandKind.Disembark,actors=Selected.ToArray(),point=point},GuardTargetResult(CancelTargetMode));
            else if(EmbarkMode||DockRepairMode)game.Notify("Choose a visible transport or friendly harbor in the world view");
            else if(RepairMode)game.Notify("Repair: choose a visible damaged friendly target");
            else if(GarrisonMode)game.Notify("Garrison: choose a visible shelter in the world view");
            else if(AttackMode)IssueOrder(OrderKind.AttackMove,point,0,append,CancelTargetMode);
            else Focus(point);
        }
        private bool WorldActive=>!game.InMenu&&game.World!=null&&(game.IsWatching||game.IsLan&&!game.LocalMenuPaused||!game.IsLan&&!game.IsPaused&&!game.World.IsMatchEnded);
        // UI and world clicks consume the same native edge stream. A later hover
        // position cannot retarget a completed UGUI click. UI transitions still
        // consume pending world edges so closing a modal never orders terrain.
        private void LateUpdate()
        {
            if(game==null||input==null)return;
            if((game.IsWatching||game.IsLan&&!game.CanSubmitOrders)&&(BuildingToPlace!=""||AttackMode||GarrisonMode||RepairMode||EmbarkMode||DisembarkMode||DockRepairMode))CancelTargetMode();
            if(input.TakePointerReset())
            {ResetDoubleClick();game.Hud.CancelUiPointer();beganWorld=false;beganMinimap=false;Dragging=false;}
            // Only UI edges wait for newly rebuilt graphics. World commands and
            // keyboard shortcuts do not initiate a wait. Later events stay in FIFO order.
            while(input.TryPeek(out var edge))
            {
                game.Hud.EnsureActionContextCurrent();
                if(edge.kind==EdgeKind.Left&&game.Hud.NeedsUiLayoutAt(edge.position))break;
                input.TryTake(out edge);
                if(edge.kind==EdgeKind.Key&&(edge.key==Key.Escape||edge.key==Key.F9))
                {if(edge.key==Key.F9)game.Capture();else HandleEscape();continue;}
                if(edge.kind==EdgeKind.Left&&game.Hud.ProcessUiPointer(edge.position,edge.pressed))
                {ResetDoubleClick();beganWorld=false;beganMinimap=false;Dragging=false;
                    // Newly created UGUI graphics become raycastable after the
                    // canvas render pass. Retain remaining edges for next frame.
                    if(game.Hud.PendingInputLayout)break;continue;}
                if(!WorldActive||game.Hud.ModalVisible)continue;
                // The transition barrier prevents pointer click-through. Fresh
                // keyboard edges can act immediately after a modal has closed.
                if(edge.kind!=EdgeKind.Key&&Time.unscaledTime<suppressUntil)continue;
                PruneInvisibleSelection();
                if(edge.kind==EdgeKind.Key)ProcessKey(edge);
                else if(edge.kind==EdgeKind.Right&&edge.pressed)ProcessRight(edge);
                else if(edge.kind==EdgeKind.Left)ProcessPointer(edge);
            }
            if(!WorldActive){beganWorld=false;beganMinimap=false;Dragging=false;return;}
            PruneInvisibleSelection();
            var mouse=Mouse.current;var keys=Keyboard.current;if(mouse==null||keys==null)return;
            Vector2 pointer=mouse.position.ReadValue();DragEnd=pointer;
            if(!game.Hud.ModalVisible&&Time.unscaledTime>=suppressUntil)
            {
                if(mouse.leftButton.isPressed&&beganMinimap&&game.Hud.TryMinimapPoint(pointer,out var mini))Focus(mini);
                if(mouse.leftButton.isPressed&&beganWorld&&(pointer-DragStart).sqrMagnitude>ClickSlopSquared)Dragging=true;
            }
            Vector2 pan=Vector2.zero;
            if(!game.Hud.ModalVisible)
            {
                pan=CameraPanInput.KeyDirection(keys,game.Settings.Bindings);
                if(!game.Hud.BlocksCameraEdgePan(pointer))pan+=CameraPanInput.EdgeDirection(pointer,new Vector2(Screen.width,Screen.height),Application.isFocused);
                pan=Vector2.ClampMagnitude(pan,1);
                if(mouse.middleButton.isPressed)pan-=(pointer-lastMouse)*.08f;
            }
            if(pan.sqrMagnitude>0)Focus(CameraFocus+pan*Time.unscaledDeltaTime*30*(zoom/26));lastMouse=pointer;
            if(!game.Hud.BlocksWorldInput(pointer)){float scroll=mouse.scroll.ReadValue().y;if(Mathf.Abs(scroll)>.01f){zoom=Mathf.Clamp(zoom-scroll*.018f,12,47);UpdateCamera();}}
            GroundMouse=HoverGroundPoint(pointer);
        }
        private void HandleEscape()
        {
            if(game.IsLan)
            {
                if(game.InMenu)game.Hud.ShowLanLobby();
                else if(BuildingToPlace!=""||AttackMode||GarrisonMode||RepairMode||EmbarkMode||DisembarkMode||DockRepairMode)CancelTargetMode();
                else if(game.LocalMenuPaused)game.CloseLocalMenu();
                else if(game.Hud.ResultVisible)game.TogglePause();
                else if(game.Hud.ModalVisible)game.Hud.CloseModal();
                else game.TogglePause();
                SuppressWorldInput();return;
            }
            if(game.IsWatching)
            {CancelTargetMode();if(game.Hud.ModalVisible)game.Hud.CloseModal();else game.ToggleReplayPlayback();SuppressWorldInput();return;}
            if(BuildingToPlace!=""||AttackMode||GarrisonMode||RepairMode||EmbarkMode||DisembarkMode||DockRepairMode){CancelTargetMode();game.Notify("Targeting cancelled");}
            else if(game.Hud.ResultVisible)game.ReturnToMenu();
            else if(game.IsPaused)game.TogglePause();
            else if(game.Hud.ModalVisible)game.Hud.CloseModal();
            else game.TogglePause();
            // Drop pending pointer events, including a same-frame right click.
            // Later keyboard edges retain their order and see the new UI state.
            SuppressWorldInput();
        }
        private void ProcessKey(Edge edge)
        {
            ResetDoubleClick();
            if((game.IsWatching||game.IsLan&&!game.CanSubmitOrders)&&edge.action!=PlayerAction.None&&edge.action!=PlayerAction.IdleWorker&&edge.action!=PlayerAction.TownCenter&&edge.action!=PlayerAction.Army&&edge.action!=PlayerAction.FocusSelection&&edge.action!=PlayerAction.ZoomIn&&edge.action!=PlayerAction.ZoomOut)return;
            switch(edge.action)
            {
                case PlayerAction.AttackMove:BeginAttackMove();break;
                case PlayerAction.Garrison:BeginGarrison();break;
                case PlayerAction.Repair:BeginRepair();break;
                case PlayerAction.Embark:BeginEmbark();break;
                case PlayerAction.Disembark:BeginDisembark();break;
                case PlayerAction.DockRepair:BeginDockRepair();break;
                case PlayerAction.Stop:CancelTargetMode();IssueOrder(OrderKind.Stop,Vector2.zero,0,edge.shift);break;
                case PlayerAction.Hold:CancelTargetMode();IssueOrder(OrderKind.Hold,Vector2.zero,0,edge.shift);break;
                case PlayerAction.HomeCity:game.Hud.ShowHomeCity();beganWorld=false;Dragging=false;break;
                case PlayerAction.BuildMenu:game.Hud.ShowBuildMenu();beganWorld=false;Dragging=false;break;
                case PlayerAction.IdleWorker:Locate(0);break;
                case PlayerAction.TownCenter:Locate(1);break;
                case PlayerAction.Army:Locate(2);break;
                case PlayerAction.FocusSelection:if(FirstSelected!=null)Focus(FirstSelected.position);break;
                case PlayerAction.ZoomIn:zoom=Mathf.Clamp(zoom-2,12,47);UpdateCamera();break;
                case PlayerAction.ZoomOut:zoom=Mathf.Clamp(zoom+2,12,47);UpdateCamera();break;
                default:
                    int n=edge.key==Key.Digit0?0:edge.key>=Key.Digit1&&edge.key<=Key.Digit9?(int)edge.key-(int)Key.Digit1+1:-1;
                    if(n>=0){if(edge.saveGroup)SaveGroup(n);else RecallGroup(n);}break;
            }
        }

        private void ProcessRight(Edge edge)
        {
            ResetDoubleClick();beganWorld=false;beganMinimap=false;Dragging=false;
            if(!game.CanSubmitOrders){CancelTargetMode();return;}
            if(BuildingToPlace!=""||AttackMode||GarrisonMode||RepairMode||EmbarkMode||DisembarkMode||DockRepairMode){CancelTargetMode();return;}
            if(game.Hud.TryMinimapPoint(edge.position,out var mini)){MinimapOrder(mini,true,edge.shift);return;}
            if(!game.Hud.BlocksWorldInput(edge.position))ContextOrder(GroundPoint(edge.position),Pick(edge.position,true),edge.shift);
        }
        private void ProcessPointer(Edge edge)
        {
            Vector2 pointer=edge.position;Vector2 point=GroundPoint(pointer);
            if(edge.pressed)
            {
                beganMinimap=game.Hud.TryMinimapPoint(pointer,out var mini);
                if(beganMinimap){beganWorld=false;Dragging=false;MinimapOrder(mini,false,edge.shift);return;}
                beganWorld=!game.Hud.BlocksWorldInput(pointer);if(!beganWorld){ResetDoubleClick();return;}
                if(BuildingToPlace!="")
                {
                    bool append=edge.shift;game.SubmitCommand(new GameCommand{player=LocalPlayer,kind=CommandKind.Build,actors=Selected.ToArray(),item=BuildingToPlace,point=game.World.BuildPoint(LocalPlayer,BuildingToPlace,point),append=append},GuardTargetResult(()=>{if(!append)CancelTargetMode();}));
                    beganWorld=false;return;
                }
                if(AttackMode){var target=Pick(pointer,true);IssueOrder(target!=null&&(game.World.AreEnemies(LocalPlayer,target.owner)||World.Guardian(target))?OrderKind.Attack:OrderKind.AttackMove,point,target?.id??0,edge.shift,CancelTargetMode);beganWorld=false;return;}
                if(EmbarkMode||DisembarkMode||DockRepairMode)
                {
                    var target=Pick(pointer,true,true);var command=EmbarkMode?CommandKind.Embark:DisembarkMode?CommandKind.Disembark:CommandKind.DockRepair;
                    game.SubmitCommand(new GameCommand{player=LocalPlayer,kind=command,actors=Selected.ToArray(),target=target?.id??0,point=command==CommandKind.Embark?Vector2.zero:point,append=edge.shift},GuardTargetResult(CancelTargetMode));
                    beganWorld=false;return;
                }
                if(RepairMode)
                {
                    var target=Pick(pointer,true,false,true);var actors=new List<int>();
                    if(target!=null&&target.owner==LocalPlayer&&target.Complete&&target.hp<game.World.MaxHp(target)-1)
                        foreach(int id in Selected){var actor=game.World.Get(id);if(actor!=null&&actor.owner==LocalPlayer&&(actor.Worker&&target.def.kind==EntityKind.Building||actor.def.repairArtillery&&(target.def.tags&Tags.Artillery)!=0))actors.Add(id);}
                    if(actors.Count>0)game.SubmitCommand(new GameCommand{player=LocalPlayer,kind=CommandKind.Order,actors=actors.ToArray(),order=OrderKind.Repair,point=target.position,target=target.id,append=edge.shift},GuardTargetResult(CancelTargetMode));
                    else game.Notify("Repair: choose a damaged friendly target compatible with the selected workers");
                    beganWorld=false;return;
                }
                if(GarrisonMode)
                {
                    var target=Pick(pointer,true,true);
                    if(target!=null&&target.owner==LocalPlayer&&target.def.kind==EntityKind.Building&&target.Complete&&target.def.garrison>0)
                    {
                        game.SubmitCommand(new GameCommand{player=LocalPlayer,kind=CommandKind.Order,actors=Selected.ToArray(),order=OrderKind.Garrison,point=point,target=target.id,append=edge.shift},GuardTargetResult(CancelTargetMode));
                    }
                    else game.Notify(target==null?"Garrison: choose a visible friendly shelter":target.owner!=LocalPlayer?"Garrison: choose your own building":target.def.garrison<=0?"Garrison: building cannot shelter units":"Garrison: building is unfinished");
                    beganWorld=false;return;
                }
                DragStart=pointer;Dragging=false;return;
            }
            if(beganMinimap){if(game.Hud.TryMinimapPoint(pointer,out var mini))Focus(mini);beganMinimap=false;return;}
            if(!beganWorld)return;
            DragEnd=pointer;Dragging|=(pointer-DragStart).sqrMagnitude>ClickSlopSquared;
            if(Dragging){ResetDoubleClick();BoxSelect(DragStart,pointer,edge.shift);}
            else
            {
                var entity=Pick(pointer);
                // A double click is one nearby, uninterrupted pointer gesture.
                // Fast selection of another worker must not select the whole economy.
                double elapsed=edge.time-lastClickTime;
                if(entity!=null&&entity.owner==Observer&&lastType==entity.def.id&&elapsed>=0&&elapsed<.3&&(pointer-lastClickPosition).sqrMagnitude<=ClickSlopSquared)
                {if(!edge.shift)Selected.Clear();foreach(var e in game.World.Entities)if(e.Alive&&e.owner==Observer&&e.def.id==entity.def.id&&OnScreen(e)&&!Selected.Contains(e.id))Selected.Add(e.id);}
                else Select(entity,edge.shift);
                lastType=entity?.owner==Observer?entity.def.id:null;lastClickTime=edge.time;lastClickPosition=pointer;
            }
            if(inputTrace)Debug.Log($"FRONTIER_SELECTION drag={Dragging} from={DragStart} to={pointer} selected={Selected.Count}");
            Dragging=false;beganWorld=false;game.Hud.InvalidateActions();
        }
        private void Locate(int kind)
        {
            var candidates=new List<EntityState>();foreach(var e in game.World.Entities)if(e.Alive&&e.owner==Observer&&e.garrisonedIn==0&&game.World.IsVisible(Observer,e))
            {if(kind==0&&e.Worker&&e.orders.Count==0||kind==1&&e.def.id=="town"||kind==2&&e.Military)candidates.Add(e);}
            if(candidates.Count==0){game.Notify(kind==0?"No idle settlers":"No matching units");return;}
            int index=candidates.FindIndex(e=>Selected.Contains(e.id));var target=candidates[(index+1)%candidates.Count];Select(target);Focus(target.position);
        }
        private void ContextOrder(Vector2 point,EntityState target,bool append)
        {
            if(!game.CanSubmitOrders)return;
            var buildings=new List<int>();var mobile=new List<int>();foreach(int id in Selected){var e=game.World.Get(id);if(e==null||e.owner!=LocalPlayer)continue;if(e.def.kind==EntityKind.Building)buildings.Add(id);else if(e.def.kind==EntityKind.Unit)mobile.Add(id);}
            bool resource=game.World.GatherableResource(LocalPlayer,target);
            if(buildings.Count>0)game.SubmitCommand(new GameCommand{player=LocalPlayer,kind=CommandKind.Rally,actors=buildings.ToArray(),point=point,target=resource?target.id:0});
            // Context is resolved per capability. A mixed group retains both its
            // workers' task and its soldiers' movement, including Shift appends.
            var kinds=new List<OrderKind>();var groups=new Dictionary<OrderKind,List<int>>();
            foreach(int id in mobile)
            {
                var actor=game.World.Get(id);OrderKind kind=OrderKind.Move;
                if(target!=null)
                {
                    if(resource){if(game.World.CanGather(actor,target))kind=OrderKind.Gather;}
                    else if(World.Treasure(target)){if((actor.def.tags&Tags.Explorer)!=0)kind=OrderKind.Claim;}
                    else if(game.World.AreEnemies(LocalPlayer,target.owner)||World.Guardian(target))kind=OrderKind.Attack;
                    else if(target.owner==LocalPlayer&&target.def.transportShip&&!actor.def.waterborne)kind=OrderKind.Embark;
                    else if(target.owner==LocalPlayer&&target.def.id=="dock"&&actor.def.waterborne&&actor.hp<game.World.MaxHp(actor)-1)kind=OrderKind.DockRepair;
                    else if(target.owner==LocalPlayer&&target.def.kind==EntityKind.Building)
                    {if(actor.Worker&&!target.Complete)kind=OrderKind.Build;else if(actor.Worker&&target.Complete&&target.def.id=="workshop")kind=OrderKind.Manufacture;else if(actor.Worker&&target.hp<game.World.MaxHp(target)-1)kind=OrderKind.Repair;else if(target.Complete&&target.def.garrison>0)kind=OrderKind.Garrison;}
                    else if(target.owner==LocalPlayer&&actor.def.repairArtillery&&(target.def.tags&Tags.Artillery)!=0&&target.hp<game.World.MaxHp(target)-1)kind=OrderKind.Repair;
                }
                if(!groups.TryGetValue(kind,out var actors)){actors=new List<int>();groups.Add(kind,actors);kinds.Add(kind);}actors.Add(id);
            }
            foreach(var kind in kinds)game.SubmitCommand(new GameCommand{player=LocalPlayer,kind=kind==OrderKind.Embark?CommandKind.Embark:kind==OrderKind.DockRepair?CommandKind.DockRepair:CommandKind.Order,actors=groups[kind].ToArray(),order=kind,point=kind==OrderKind.Embark?Vector2.zero:target?.position??point,target=kind==OrderKind.Move?0:target?.id??0,append=append});
        }
        public Vector2 GroundPoint(Vector2 screen)
        {
            var ray=game.GameCamera.ScreenPointToRay(screen);var plane=new Plane(Vector3.up,Vector3.zero);
            if(game.World?.Layout.HasScenario==true)return ScenarioGroundPoint(ray,OnlySelectedBoats());
            if(!plane.Raycast(ray,out float enter))return CameraFocus;
            // Resolve actual elevated bridge surfaces before the base plane. A
            // boat-only selection targets the water channel beneath the deck.
            bool boatsOnly=OnlySelectedBoats();
            if(game.World?.Layout.HasWater==true&&game.World.MapId!=MapTopology.River)
            {
                // Carved banks are lower than the old y=0 plane. Solve against
                // the same height field rendered by the terrain, before testing
                // elevated bridge decks. Water clicks use the visible surface.
                float low=Mathf.Max(0,enter-2),high=enter+4;
                for(int n=0;n<16;n++)
                {
                    float middle=(low+high)*.5f;var hit=ray.GetPoint(middle);var point=new Vector2(hit.x,hit.z);
                    float height=boatsOnly||game.World.IsWater(point)?game.World.Layout.WaterHeight:game.World.Layout.BankHeight(point);
                    if(hit.y>height)low=middle;else high=middle;
                }
                enter=(low+high)*.5f;
            }
            if(game.World?.Layout.HasWater==true&&!boatsOnly)
            {
                foreach(var bridge in game.World.Layout.Bridges)
                {
                    for(int i=0;i<3;i++)
                    {
                        float a=i==0?-bridge.HalfLength-bridge.RampLength:i==1?-bridge.HalfLength:bridge.HalfLength;
                        float b=i==0?-bridge.HalfLength:i==1?bridge.HalfLength:bridge.HalfLength+bridge.RampLength;
                        Vector3 Point(float across,float along)
                        {var p=bridge.Point(across,along);float height=bridge.Height*Mathf.Clamp01((bridge.HalfLength+bridge.RampLength-Mathf.Abs(across))/bridge.RampLength);return new Vector3(p.x,height,p.y);}
                        var bridgePlane=new Plane(Point(a,-bridge.HalfWidth),Point(a,bridge.HalfWidth),Point(b,bridge.HalfWidth));
                        if(!bridgePlane.Raycast(ray,out float t)||t>=enter)continue;
                        var hit=ray.GetPoint(t);var local=bridge.Local(new Vector2(hit.x,hit.z));
                        if(local.x>=a-.001f&&local.x<=b+.001f&&Mathf.Abs(local.y)<=bridge.HalfWidth)enter=t;
                    }
                }
            }
            var p=ray.GetPoint(enter);return new Vector2(Mathf.Clamp(p.x,1,126),Mathf.Clamp(p.z,1,126));
        }
        private Vector2 ScenarioGroundPoint(Ray ray,bool boatsOnly)
        {
            // The authored contract allows -20..100m; water-bed carving needs
            // another metre below it. Traverse actual rendered grid faces so a
            // steep or narrow peak cannot be skipped by an approximate ray march.
            float near=0,far=float.PositiveInfinity;
            bool Slab(float origin,float direction,float minimum,float maximum)
            {
                if(Mathf.Abs(direction)<.0000001f)return origin>=minimum&&origin<=maximum;
                float a=(minimum-origin)/direction,b=(maximum-origin)/direction;if(a>b){float swap=a;a=b;b=swap;}
                near=Mathf.Max(near,a);far=Mathf.Min(far,b);return near<=far;
            }
            if(!Slab(ray.origin.x,ray.direction.x,0,128)||!Slab(ray.origin.y,ray.direction.y,-22,102)||!Slab(ray.origin.z,ray.direction.z,0,128)||far<0)return CameraFocus;
            var layout=game.World.Layout;float nearest=far+.001f;bool hit=false;
            if(layout.HasWater&&Mathf.Abs(ray.direction.y)>.0000001f)
            {
                float t=(layout.WaterHeight-ray.origin.y)/ray.direction.y;var p=ray.GetPoint(t);
                if(t>=near&&t<=far&&layout.IsWater(new Vector2(p.x,p.z))){nearest=t;hit=true;}
            }
            if(!boatsOnly)foreach(var span in layout.Bridges)for(int i=0;i<3;i++)
            {
                float a=i==0?-span.HalfLength-span.RampLength:i==1?-span.HalfLength:span.HalfLength;
                float b=i==0?-span.HalfLength:i==1?span.HalfLength:span.HalfLength+span.RampLength;
                Vector3 Point(float across,float along){var p=span.Point(across,along);return new Vector3(p.x,span.GroundHeight(span.Point(across,0)),p.y);}
                var plane=new Plane(Point(a,-span.HalfWidth),Point(a,span.HalfWidth),Point(b,span.HalfWidth));
                if(!plane.Raycast(ray,out float t)||t<near||t>far||t>=nearest)continue;
                var p=ray.GetPoint(t);var local=span.Local(new Vector2(p.x,p.z));
                if(local.x>=a-.0001f&&local.x<=b+.0001f&&Mathf.Abs(local.y)<=span.HalfWidth){nearest=t;hit=true;}
            }
            var entry=ray.GetPoint(Mathf.Min(far,near+.00001f));int x=Mathf.Clamp(Mathf.FloorToInt(entry.x),0,127),z=Mathf.Clamp(Mathf.FloorToInt(entry.z),0,127);
            int sx=ray.direction.x>0?1:ray.direction.x<0?-1:0,sz=ray.direction.z>0?1:ray.direction.z<0?-1:0;
            float dx=sx==0?float.PositiveInfinity:1/Mathf.Abs(ray.direction.x),dz=sz==0?float.PositiveInfinity:1/Mathf.Abs(ray.direction.z);
            float nextX=sx==0?float.PositiveInfinity:(x+(sx>0?1:0)-ray.origin.x)/ray.direction.x,nextZ=sz==0?float.PositiveInfinity:(z+(sz>0?1:0)-ray.origin.z)/ray.direction.z;
            float cursor=near;
            for(int traversed=0;traversed<260&&x>=0&&x<128&&z>=0&&z<128&&cursor<=far&&cursor<=nearest+.0001f;traversed++)
            {
                float end=Mathf.Min(far,Mathf.Min(nextX,nextZ));
                Vector3 Vertex(int xx,int zz)=>new Vector3(xx,layout.BankHeight(new Vector2(xx,zz)),zz);
                var a=Vertex(x,z);var b=Vertex(x,z+1);var c=Vertex(x+1,z+1);var d=Vertex(x+1,z);
                for(int triangle=0;triangle<2;triangle++)
                {
                    var e1=(triangle==0?b:c)-a;var e2=(triangle==0?c:d)-a;var cross=Vector3.Cross(ray.direction,e2);float determinant=Vector3.Dot(e1,cross);
                    if(Mathf.Abs(determinant)<.0000001f)continue;float inv=1/determinant;var distance=ray.origin-a;float u=Vector3.Dot(distance,cross)*inv;if(u<-.00001f||u>1.00001f)continue;
                    var q=Vector3.Cross(distance,e1);float v=Vector3.Dot(ray.direction,q)*inv;if(v<-.00001f||u+v>1.00001f)continue;
                    float t=Vector3.Dot(e2,q)*inv;if(t>=cursor-.0001f&&t<=end+.0001f&&t<nearest){nearest=t;hit=true;}
                }
                if(end>=far)break;bool stepX=nextX<=end+.00001f,stepZ=nextZ<=end+.00001f;
                if(stepX){x+=sx;nextX+=dx;}if(stepZ){z+=sz;nextZ+=dz;}cursor=end;
            }
            if(!hit)return CameraFocus;var result=ray.GetPoint(nearest);return new Vector2(Mathf.Clamp(result.x,0,128),Mathf.Clamp(result.z,0,128));
        }
        private bool OnlySelectedBoats()
        {if(Selected.Count==0)return false;foreach(int id in Selected)if(game.World?.Get(id)?.def.waterborne!=true)return false;return true;}
        private Vector2 HoverGroundPoint(Vector2 pointer)
        {
            var camera=game.GameCamera;var view=camera.worldToCameraMatrix;var projection=camera.projectionMatrix;var rect=camera.pixelRect;bool boats=OnlySelectedBoats();
            if(!groundCached||groundWorld!=game.World||groundCamera!=camera||!pointer.Equals(groundPointer)||!view.Equals(groundView)||!projection.Equals(groundProjection)||!rect.Equals(groundPixelRect)||boats!=groundBoatsOnly)
            {groundResult=GroundPoint(pointer);groundWorld=game.World;groundCamera=camera;groundPointer=pointer;groundView=view;groundProjection=projection;groundPixelRect=rect;groundBoatsOnly=boats;groundCached=true;}
            return groundResult;
        }
        private bool OnScreen(EntityState entity){if(!game.World.IsVisible(Observer,entity))return false;var p=game.GameCamera.WorldToViewportPoint(new Vector3(entity.position.x,game.World.SurfaceHeight(entity.position,entity.def.waterborne)+1,entity.position.y));return p.z>0&&p.x>0&&p.x<1&&p.y>0&&p.y<1;}
        private EntityState Pick(Vector2 screen,bool command=false,bool shelter=false,bool repair=false)
        {
            EntityState best=null;float score=float.MaxValue;int bestRank=int.MaxValue;var point=GroundPoint(screen);var ray=game.GameCamera.ScreenPointToRay(screen);
            bool workers=false,explorers=false;
            if(command)foreach(int id in Selected){var actor=game.World.Get(id);if(actor==null||actor.owner!=Observer||!actor.Alive||actor.garrisonedIn!=0)continue;workers|=actor.Worker;explorers|=(actor.def.tags&Tags.Explorer)!=0;}
            foreach(var e in game.World.Entities)
            {
                if(!game.World.IsVisible(Observer,e)||e.def.id=="trade_socket"&&game.World.TradingPostAt(e.id)!=null)continue;
                var projected=game.GameCamera.WorldToScreenPoint(new Vector3(e.position.x,game.World.SurfaceHeight(e.position,e.def.waterborne)+(e.def.kind==EntityKind.Unit?1:0),e.position.y));if(projected.z<0)continue;
                float d=Vector2.Distance(screen,projected);float threshold=e.def.kind==EntityKind.Unit?Mathf.Max(13,Screen.height/game.GameCamera.orthographicSize*.35f):20;
                bool visualHit=false;
                if((e.def.kind!=EntityKind.Unit||e.def.waterborne)&&game.View.TryGetPickBounds(e.id,out var bounds))visualHit=EntityPickGeometry.RayHits(bounds,ray);
                bool footprint=e.def.kind==EntityKind.Building&&game.World.BuildingFootprintContains(e.def,e.position,point,0);
                if(d>threshold&&!footprint&&!visualHit)continue;
                int rank=0;
                if(command)
                {
                    bool enemy=game.World.AreEnemies(Observer,e.owner)||World.Guardian(e);
                    bool resource=Selected.Exists(id=>game.World.CanGather(game.World.Get(id),e));
                    bool treasure=explorers&&World.Treasure(e);
                    bool building=e.owner==Observer&&e.def.kind==EntityKind.Building&&(e.def.garrison>0||workers&&(!e.Complete||e.hp<game.World.MaxHp(e)-1||e.def.id=="workshop"));
                    rank=enemy?0:resource||treasure||building?1:e.owner==LocalPlayer&&e.def.kind==EntityKind.Unit?3:2;
                }
                // Explicit shelter targeting must not be stolen by an attacking unit
                // overlapping the same building. Ordinary right-click ranking is unchanged.
                if(shelter&&e.owner==Observer&&e.def.kind==EntityKind.Building&&e.Complete&&e.def.garrison>0)rank=-1;
                if(repair&&e.owner==Observer&&e.Complete&&e.hp<game.World.MaxHp(e)-1&&(e.def.kind==EntityKind.Building||(e.def.tags&Tags.Artillery)!=0))rank=-1;
                float priority=d+(e.def.kind==EntityKind.Unit?0:e.def.kind==EntityKind.Building?60:40);
                if(rank<bestRank||rank==bestRank&&priority<score){bestRank=rank;score=priority;best=e;}
            }
            return best;
        }
        private void BoxSelect(Vector2 a,Vector2 b,bool append)
        {
            if(!append)Selected.Clear();var rect=Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));var found=new List<EntityState>();
            foreach(var e in game.World.Entities)if(e.owner==Observer&&game.World.IsVisible(Observer,e))
            {var p=game.GameCamera.WorldToScreenPoint(new Vector3(e.position.x,game.World.SurfaceHeight(e.position,e.def.waterborne)+1,e.position.y));if(p.z>0&&rect.Contains(p))found.Add(e);}
            bool units=found.Exists(e=>e.def.kind==EntityKind.Unit);foreach(var e in found)if((!units||e.def.kind==EntityKind.Unit)&&!Selected.Contains(e.id))Selected.Add(e.id);
        }
    }
}
