using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Edge = Frontier.DiscreteInputBuffer.Edge;
using EdgeKind = Frontier.DiscreteInputBuffer.EdgeKind;

namespace Frontier
{
    public sealed class RtsInput : MonoBehaviour
    {
        private GameController game;
        public readonly List<int> Selected=new List<int>();
        private readonly Dictionary<int,List<int>> groups=new Dictionary<int,List<int>>();
        public Vector2 CameraFocus {get;private set;}
        public Vector2 GroundMouse {get;private set;}
        public string BuildingToPlace="";
        public bool AttackMode;
        public bool GarrisonMode;
        public bool Dragging {get;private set;}
        public Vector2 DragStart {get;private set;}
        public Vector2 DragEnd {get;private set;}
        private bool beganWorld;
        private float zoom=26;private double lastClickTime;
        private string lastType;
        private Vector2 lastMouse;
        private float suppressUntil;
        private DiscreteInputBuffer input;
        private bool inputTrace,beganMinimap;
        public void SuppressWorldInput(bool clearPending=true)
        {suppressUntil=Time.unscaledTime+.22f;beganWorld=false;beganMinimap=false;Dragging=false;if(clearPending)input?.DiscardPointerEdges();game?.Hud?.CancelUiPointer();}
        private void OnDestroy()=>input?.Dispose();
        private void OnApplicationFocus(bool focused){if(inputTrace)Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"FRONTIER_FOCUS focused={0} frame={1}",focused,Time.frameCount);if(!focused)ClearTransientInput();}
        public void ClearTransientInput()
        {input?.Clear();SuppressWorldInput();lastType=null;lastClickTime=0;}
        // HUD button batching retains its frame-based modifier convention. World
        // pointer/keyboard orders use the explicit per-event edge.shift instead.
        public bool Shift=>Keyboard.current!=null&&(Keyboard.current.leftShiftKey.isPressed||Keyboard.current.rightShiftKey.isPressed||Keyboard.current.leftShiftKey.wasPressedThisFrame||Keyboard.current.rightShiftKey.wasPressedThisFrame);
        public void Initialize(GameController controller)
        {game=controller;inputTrace=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-frontierInputTrace")>=0;input=new DiscreteInputBuffer(inputTrace);}
        public void Focus(Vector2 point){CameraFocus=new Vector2(Mathf.Clamp(point.x,7,121),Mathf.Clamp(point.y,7,121));UpdateCamera();}
        public void ResetSelection(){Selected.Clear();groups.Clear();BuildingToPlace="";AttackMode=false;GarrisonMode=false;Dragging=false;beganWorld=false;beganMinimap=false;input?.Clear();game?.Hud?.CancelUiPointer();lastType=null;lastClickTime=0;}
        private void UpdateCamera()
        {
            if(game.GameCamera==null)return;
            game.GameCamera.transform.position=new Vector3(CameraFocus.x,50,CameraFocus.y-42);
            game.GameCamera.transform.rotation=Quaternion.Euler(50,0,0);game.GameCamera.orthographicSize=zoom;
        }
        public EntityState FirstSelected
        {
            get{if(game.World==null)return null;foreach(int id in Selected){var e=game.World.Get(id);if(e!=null&&e.Alive)return e;}return null;}
        }
        public void Select(EntityState entity,bool append=false)
        {if(!append)Selected.Clear();if(entity==null)return;if(append&&Selected.Contains(entity.id))Selected.Remove(entity.id);else Selected.Add(entity.id);game.Hud.InvalidateActions();}
        public void SaveGroup(int number)
        {
            var members=new List<int>();foreach(int id in Selected){var e=game.World.Get(id);if(e!=null&&e.Alive&&e.owner==0)members.Add(id);}
            groups[number]=members;game.Notify("Control group "+number+" saved · "+members.Count+" objects");
        }
        public int GroupCount(int number){if(!groups.TryGetValue(number,out var group))return 0;int count=0;foreach(int id in group){var e=game.World.Get(id);if(e!=null&&e.Alive&&e.owner==0)count++;}return count;}
        public void RecallGroup(int number)
        {
            if(!groups.TryGetValue(number,out var group))return;Selected.Clear();foreach(int id in group){var e=game.World.Get(id);if(e!=null&&e.Alive&&e.owner==0)Selected.Add(id);}
            game.Hud.InvalidateActions();if(FirstSelected!=null)Focus(FirstSelected.position);
        }
        public void BeginBuild(string id){SuppressWorldInput();BuildingToPlace=id;AttackMode=false;GarrisonMode=false;game.Notify("Place "+game.Rules.Entity(id).name+". Right click or Esc cancels.");}
        public void CancelTargetMode(){BuildingToPlace="";AttackMode=false;GarrisonMode=false;}
        public void BeginAttackMove(){CancelTargetMode();AttackMode=true;game.Notify("Attack move: choose a destination");}
        public void BeginGarrison()
        {
            if(GarrisonMode){CancelTargetMode();game.Notify("Garrison targeting cancelled");return;}
            bool mobile=Selected.Exists(id=>{var e=game.World?.Get(id);return e!=null&&e.owner==0&&e.def.kind==EntityKind.Unit&&game.World.IsVisible(0,e);});
            if(!mobile){game.Notify("Select mobile units before choosing a shelter");return;}
            CancelTargetMode();GarrisonMode=true;game.Notify("Garrison: choose a completed friendly shelter. G / right click / Esc cancels.");
        }
        public void IssueOrder(OrderKind kind,Vector2 point,int target=0){if(kind==OrderKind.Stop||kind==OrderKind.Hold)CancelTargetMode();IssueOrder(kind,point,target,Shift);}
        private void IssueOrder(OrderKind kind,Vector2 point,int target,bool append)
        {game.Command(new GameCommand{player=0,kind=CommandKind.Order,actors=Selected.ToArray(),order=kind,point=point,target=target,append=append});}
        public void MinimapOrder(Vector2 point,bool right)=>MinimapOrder(point,right,Shift);
        private void MinimapOrder(Vector2 point,bool right,bool append)
        {if(right)ContextOrder(point,null,append);else if(GarrisonMode)game.Notify("Garrison: choose a visible shelter in the world view");else if(AttackMode){IssueOrder(OrderKind.AttackMove,point,0,append);AttackMode=false;}else Focus(point);}
        private bool WorldActive=>!game.InMenu&&game.World!=null&&!game.IsPaused&&game.World.Winner<0;
        // UI and world clicks consume the same native edge stream. A later hover
        // position cannot retarget a completed UGUI click. UI transitions still
        // consume pending world edges so closing a modal never orders terrain.
        private void LateUpdate()
        {
            if(game==null||input==null)return;
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
                {beganWorld=false;beganMinimap=false;Dragging=false;
                    // Newly created UGUI graphics become raycastable after the
                    // canvas render pass. Retain remaining edges for next frame.
                    if(game.Hud.PendingInputLayout)break;continue;}
                if(!WorldActive||game.Hud.ModalVisible)continue;
                // The transition barrier prevents pointer click-through. Fresh
                // keyboard edges can act immediately after a modal has closed.
                if(edge.kind!=EdgeKind.Key&&Time.unscaledTime<suppressUntil)continue;
                Selected.RemoveAll(id=>!game.World.IsVisible(0,game.World.Get(id)));
                if(edge.kind==EdgeKind.Key)ProcessKey(edge);
                else if(edge.kind==EdgeKind.Right&&edge.pressed)ProcessRight(edge);
                else if(edge.kind==EdgeKind.Left)ProcessPointer(edge);
            }
            if(!WorldActive){beganWorld=false;beganMinimap=false;Dragging=false;return;}
            Selected.RemoveAll(id=>!game.World.IsVisible(0,game.World.Get(id)));
            var mouse=Mouse.current;var keys=Keyboard.current;if(mouse==null||keys==null)return;
            Vector2 pointer=mouse.position.ReadValue();DragEnd=pointer;
            if(!game.Hud.ModalVisible&&Time.unscaledTime>=suppressUntil)
            {
                if(mouse.leftButton.isPressed&&beganMinimap&&game.Hud.TryMinimapPoint(pointer,out var mini))Focus(mini);
                if(mouse.leftButton.isPressed&&beganWorld&&(pointer-DragStart).sqrMagnitude>64)Dragging=true;
            }
            Vector2 pan=Vector2.zero;
            if(!game.Hud.ModalVisible)
            {
                if(keys.leftArrowKey.isPressed)pan.x--;if(keys.rightArrowKey.isPressed)pan.x++;if(keys.upArrowKey.isPressed)pan.y++;if(keys.downArrowKey.isPressed)pan.y--;
                if(Application.isFocused&&pointer.x>=0&&pointer.y>=0&&pointer.x<Screen.width&&pointer.y<Screen.height)
                {if(pointer.x<8)pan.x--;if(pointer.x>Screen.width-8)pan.x++;if(pointer.y>Screen.height-8)pan.y++;if(pointer.y<8)pan.y--;}
                if(mouse.middleButton.isPressed)pan-=(pointer-lastMouse)*.08f;
            }
            if(pan.sqrMagnitude>0)Focus(CameraFocus+pan*Time.unscaledDeltaTime*30*(zoom/26));lastMouse=pointer;
            if(!game.Hud.BlocksWorldInput(pointer)){float scroll=mouse.scroll.ReadValue().y;if(Mathf.Abs(scroll)>.01f){zoom=Mathf.Clamp(zoom-scroll*.018f,12,47);UpdateCamera();}}
            GroundMouse=GroundPoint(pointer);
        }
        private void HandleEscape()
        {
            if(BuildingToPlace!=""||AttackMode||GarrisonMode){BuildingToPlace="";AttackMode=false;GarrisonMode=false;game.Notify("Placement cancelled");}
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
            switch(edge.key)
            {
                case Key.A:BeginAttackMove();break;
                case Key.G:BeginGarrison();break;
                case Key.S:CancelTargetMode();IssueOrder(OrderKind.Stop,Vector2.zero,0,edge.shift);break;
                case Key.J:CancelTargetMode();IssueOrder(OrderKind.Hold,Vector2.zero,0,edge.shift);break;
                case Key.H:game.Hud.ShowHomeCity();beganWorld=false;Dragging=false;break;
                case Key.B:game.Hud.ShowBuildMenu();beganWorld=false;Dragging=false;break;
                case Key.V:case Key.Period:Locate(0);break;
                case Key.T:Locate(1);break;
                case Key.M:Locate(2);break;
                case Key.F:if(FirstSelected!=null)Focus(FirstSelected.position);break;
                default:
                    int n=edge.key==Key.Digit0?0:edge.key>=Key.Digit1&&edge.key<=Key.Digit9?(int)edge.key-(int)Key.Digit1+1:-1;
                    if(n>=0){if(edge.saveGroup)SaveGroup(n);else RecallGroup(n);}break;
            }
        }
        private void ProcessRight(Edge edge)
        {
            beganWorld=false;beganMinimap=false;Dragging=false;
            if(BuildingToPlace!=""||AttackMode||GarrisonMode){BuildingToPlace="";AttackMode=false;GarrisonMode=false;return;}
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
                beganWorld=!game.Hud.BlocksWorldInput(pointer);if(!beganWorld)return;
                if(BuildingToPlace!="")
                {
                    var r=game.Command(new GameCommand{player=0,kind=CommandKind.Build,actors=Selected.ToArray(),item=BuildingToPlace,point=game.World.BuildPoint(0,BuildingToPlace,point),append=edge.shift});
                    if(r.accepted&&!edge.shift)BuildingToPlace="";beganWorld=false;return;
                }
                if(AttackMode){var target=Pick(pointer,true);IssueOrder(target!=null&&(target.owner==1||World.Guardian(target))?OrderKind.Attack:OrderKind.AttackMove,point,target?.id??0,edge.shift);AttackMode=false;beganWorld=false;return;}
                if(GarrisonMode)
                {
                    var target=Pick(pointer,true,true);
                    if(target!=null&&target.owner==0&&target.def.kind==EntityKind.Building&&target.Complete&&target.def.garrison>0)
                    {
                        var result=game.Command(new GameCommand{player=0,kind=CommandKind.Order,actors=Selected.ToArray(),order=OrderKind.Garrison,point=point,target=target.id,append=edge.shift});
                        if(result.accepted)GarrisonMode=false;
                    }
                    else game.Notify(target==null?"Garrison: choose a visible friendly shelter":target.owner!=0?"Garrison: choose your own building":target.def.garrison<=0?"Garrison: building cannot shelter units":"Garrison: building is unfinished");
                    beganWorld=false;return;
                }
                DragStart=pointer;Dragging=false;return;
            }
            if(beganMinimap){if(game.Hud.TryMinimapPoint(pointer,out var mini))Focus(mini);beganMinimap=false;return;}
            if(!beganWorld)return;
            DragEnd=pointer;Dragging|=(pointer-DragStart).sqrMagnitude>64;
            if(Dragging)BoxSelect(DragStart,pointer,edge.shift);
            else
            {
                var entity=Pick(pointer);
                if(entity!=null&&entity.owner==0&&lastType==entity.def.id&&edge.time-lastClickTime<.3)
                {if(!edge.shift)Selected.Clear();foreach(var e in game.World.Entities)if(e.Alive&&e.owner==0&&e.def.id==entity.def.id&&OnScreen(e)&&!Selected.Contains(e.id))Selected.Add(e.id);}
                else Select(entity,edge.shift);
                lastType=entity?.def.id;lastClickTime=edge.time;
            }
            if(inputTrace)Debug.Log($"FRONTIER_SELECTION drag={Dragging} from={DragStart} to={pointer} selected={Selected.Count}");
            Dragging=false;beganWorld=false;game.Hud.InvalidateActions();
        }
        private void Locate(int kind)
        {
            var candidates=new List<EntityState>();foreach(var e in game.World.Entities)if(e.Alive&&e.owner==0&&e.garrisonedIn==0)
            {if(kind==0&&e.Worker&&e.orders.Count==0||kind==1&&e.def.id=="town"||kind==2&&e.Military)candidates.Add(e);}
            if(candidates.Count==0){game.Notify(kind==0?"No idle settlers":"No matching units");return;}
            int index=candidates.FindIndex(e=>Selected.Contains(e.id));var target=candidates[(index+1)%candidates.Count];Select(target);Focus(target.position);
        }
        private void ContextOrder(Vector2 point,EntityState target,bool append)
        {
            var buildings=new List<int>();var mobile=new List<int>();foreach(int id in Selected){var e=game.World.Get(id);if(e==null||e.owner!=0)continue;if(e.def.kind==EntityKind.Building)buildings.Add(id);else if(e.def.kind==EntityKind.Unit)mobile.Add(id);}
            bool resource=game.World.GatherableResource(0,target);
            if(buildings.Count>0)game.Command(new GameCommand{player=0,kind=CommandKind.Rally,actors=buildings.ToArray(),point=point,target=resource?target.id:0});
            // Context is resolved per capability. A mixed group retains both its
            // workers' task and its soldiers' movement, including Shift appends.
            var kinds=new List<OrderKind>();var groups=new Dictionary<OrderKind,List<int>>();
            foreach(int id in mobile)
            {
                var actor=game.World.Get(id);OrderKind kind=OrderKind.Move;
                if(target!=null)
                {
                    if(resource){if(actor.Worker)kind=OrderKind.Gather;}
                    else if(World.Treasure(target)){if((actor.def.tags&Tags.Explorer)!=0)kind=OrderKind.Claim;}
                    else if(target.owner==1||World.Guardian(target))kind=OrderKind.Attack;
                    else if(target.owner==0&&target.def.kind==EntityKind.Building)
                    {if(actor.Worker&&!target.Complete)kind=OrderKind.Build;else if(actor.Worker&&target.hp<game.World.MaxHp(target)-1)kind=OrderKind.Repair;else if(target.Complete&&target.def.garrison>0)kind=OrderKind.Garrison;}
                }
                if(!groups.TryGetValue(kind,out var actors)){actors=new List<int>();groups.Add(kind,actors);kinds.Add(kind);}actors.Add(id);
            }
            foreach(var kind in kinds)game.Command(new GameCommand{player=0,kind=CommandKind.Order,actors=groups[kind].ToArray(),order=kind,point=target?.position??point,target=kind==OrderKind.Move?0:target?.id??0,append=append});
        }
        public Vector2 GroundPoint(Vector2 screen)
        {
            var ray=game.GameCamera.ScreenPointToRay(screen);var plane=new Plane(Vector3.up,Vector3.zero);if(plane.Raycast(ray,out float enter)){var p=ray.GetPoint(enter);return new Vector2(Mathf.Clamp(p.x,1,126),Mathf.Clamp(p.z,1,126));}return CameraFocus;
        }
        private bool OnScreen(EntityState entity){var p=game.GameCamera.WorldToViewportPoint(new Vector3(entity.position.x,1,entity.position.y));return p.z>0&&p.x>0&&p.x<1&&p.y>0&&p.y<1;}
        private EntityState Pick(Vector2 screen,bool command=false,bool shelter=false)
        {
            EntityState best=null;float score=float.MaxValue;int bestRank=int.MaxValue;var point=GroundPoint(screen);var ray=game.GameCamera.ScreenPointToRay(screen);
            bool workers=false,explorers=false;
            if(command)foreach(int id in Selected){var actor=game.World.Get(id);if(actor==null||actor.owner!=0||!actor.Alive||actor.garrisonedIn!=0)continue;workers|=actor.Worker;explorers|=(actor.def.tags&Tags.Explorer)!=0;}
            foreach(var e in game.World.Entities)
            {
                if(!game.World.IsVisible(0,e)||e.def.id=="trade_socket"&&game.World.TradingPostAt(e.id)!=null)continue;
                var projected=game.GameCamera.WorldToScreenPoint(new Vector3(e.position.x,e.def.kind==EntityKind.Unit?1:0,e.position.y));if(projected.z<0)continue;
                float d=Vector2.Distance(screen,projected);float threshold=e.def.kind==EntityKind.Unit?Mathf.Max(13,Screen.height/game.GameCamera.orthographicSize*.35f):20;
                bool visualHit=false;
                if(e.def.kind!=EntityKind.Unit&&game.View.TryGetPickBounds(e.id,out var bounds))visualHit=EntityPickGeometry.RayHits(bounds,ray);
                bool footprint=e.def.kind==EntityKind.Building&&Mathf.Abs(e.position.x-point.x)<=e.def.width*.5f&&Mathf.Abs(e.position.y-point.y)<=e.def.depth*.5f;
                if(d>threshold&&!footprint&&!visualHit)continue;
                int rank=0;
                if(command)
                {
                    bool enemy=e.owner==1||World.Guardian(e);
                    bool resource=workers&&e.def.resource!=ResourceKind.None&&(e.def.kind==EntityKind.Resource||e.owner==0);
                    bool treasure=explorers&&World.Treasure(e);
                    bool building=e.owner==0&&e.def.kind==EntityKind.Building&&(e.def.garrison>0||workers&&(!e.Complete||e.hp<game.World.MaxHp(e)-1));
                    rank=enemy?0:resource||treasure||building?1:e.owner==0&&e.def.kind==EntityKind.Unit?3:2;
                }
                // Explicit shelter targeting must not be stolen by an attacking unit
                // overlapping the same building. Ordinary right-click ranking is unchanged.
                if(shelter&&e.owner==0&&e.def.kind==EntityKind.Building&&e.Complete&&e.def.garrison>0)rank=-1;
                float priority=d+(e.def.kind==EntityKind.Unit?0:e.def.kind==EntityKind.Building?60:40);
                if(rank<bestRank||rank==bestRank&&priority<score){bestRank=rank;score=priority;best=e;}
            }
            return best;
        }
        private void BoxSelect(Vector2 a,Vector2 b,bool append)
        {
            if(!append)Selected.Clear();var rect=Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));var found=new List<EntityState>();
            foreach(var e in game.World.Entities)if(e.owner==0&&game.World.IsVisible(0,e))
            {var p=game.GameCamera.WorldToScreenPoint(new Vector3(e.position.x,1,e.position.y));if(p.z>0&&rect.Contains(p))found.Add(e);}
            bool units=found.Exists(e=>e.def.kind==EntityKind.Unit);foreach(var e in found)if((!units||e.def.kind==EntityKind.Unit)&&!Selected.Contains(e.id))Selected.Add(e.id);
        }
    }
}
