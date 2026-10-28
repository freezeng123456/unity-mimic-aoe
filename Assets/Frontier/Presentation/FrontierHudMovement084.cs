using System.Globalization;
using UnityEngine;

namespace Frontier
{
    public sealed partial class FrontierHud
    {
        // Presentation reads only the first owned selected unit's current
        // command and saved route. It never plans a route, prepares bridge
        // traffic, reads an enemy's orders, changes fog or records new Sim state.
        private bool TryMovementFeedback084(EntityState unit,out string summary,out string activity)
        {
            summary=activity="";var world=game.World;
            if(world==null||unit==null||!unit.Alive||unit.owner!=game.LocalPlayerSlot||unit.garrisonedIn!=0||
                unit.def.kind!=EntityKind.Unit||unit.orders.Count==0||game.IsWatching||game.Autoplay)return false;
            var order=unit.orders.Peek();if(order.kind!=OrderKind.Move&&order.kind!=OrderKind.AttackMove)return false;
            Vector2 destination=order.hasResolvedPoint?order.resolvedPoint:order.point;
            activity=MovementActivity084(world,unit,order);
            string command=order.kind==OrderKind.AttackMove?"ATTACK MOVE":"MOVE";
            string point="("+destination.x.ToString("0",CultureInfo.InvariantCulture)+","+destination.y.ToString("0",CultureInfo.InvariantCulture)+")";
            summary=(game.Controls.Selected.Count>1?"FIRST ":"")+command+" TO "+point;
            if(order.hasResolvedPoint)summary+=" · ADJUSTED";
            string bridge=MovementBridge084(world,unit,destination);if(bridge!="")summary+=" · "+bridge;
            summary+=" · "+activity;return true;
        }
        private string MovementActivity084(World world,EntityState unit,UnitOrder order)
        {
            if(world.Paused)return "PAUSED";
            if(unit.shielded||world.Time<unit.shieldReadyAt)return "CHANGING STANCE";
            // Actual displacement is stronger evidence than a stale Moving or
            // Waiting status. Tiny sub-millimetre contact noise is not progress.
            if((unit.position-unit.previousPosition).sqrMagnitude>.000001f)return "MOVING";
            if(unit.status=="Deploying cannon")return "DEPLOYING";
            if(order.kind==OrderKind.AttackMove&&(unit.fireAt>=0||unit.status=="Preparing attack"||unit.status=="Reloading"))return "ENGAGING";
            if(unit.status=="Transport required")return "TRANSPORT REQUIRED";
            if(unit.status=="Destination occupied; waiting")return "WAITING: DESTINATION";
            if(unit.status=="Route blocked; retrying")return "WAITING: ROUTE BLOCKED";
            if(unit.pathIndex>=unit.path.Count)return unit.pathRetryAt>world.Time?"WAITING: ROUTE BLOCKED":"PLANNING ROUTE";
            Vector2 to=unit.path[unit.pathIndex]-unit.position;
            if(to.sqrMagnitude>.000001f)
            {
                Vector2 end=unit.position+Vector2.ClampMagnitude(to,Mathf.Max(.1f,world.Speed(unit)*MatchSimulation.StepSeconds));
                foreach(var other in world.Entities)
                {
                    // Owned bodies are always inspectable. Do not use a hidden
                    // opponent, allied route or enemy order to explain a wait.
                    if(other.id==unit.id||!other.Alive||other.owner!=game.LocalPlayerSlot||other.garrisonedIn!=0||
                        other.def.kind!=EntityKind.Unit||other.def.waterborne!=unit.def.waterborne||Vector2.Dot(other.position-unit.position,to)<=0)continue;
                    float radius=unit.def.radius+other.def.radius+.1f;
                    if(MovementSegmentDistance084(unit.position,end,other.position)<radius)return "YIELDING: FRIENDLY TRAFFIC";
                }
                // Probe only the immediate observed ground. No UI query explores
                // future terrain or infers a hidden enemy building's location.
                if(world.Fog.At(game.LocalPlayerSlot,end)==VisibilityState.Visible&&
                    !(unit.def.waterborne?world.WaterNavigation:world.Navigation).ClearLine(unit.position,end,unit.def.radius))return "WAITING: OBSTRUCTION";
            }
            if(unit.status=="Waiting for passage"||unit.status=="Moving"||world.Time-unit.progressAt>2)return "WAITING: PASSAGE";
            return "PLANNING ROUTE";
        }
        private static float MovementSegmentDistance084(Vector2 start,Vector2 end,Vector2 point)
        {
            Vector2 delta=end-start;float length=delta.sqrMagnitude;
            float at=length>1e-12f?Mathf.Clamp01(Vector2.Dot(point-start,delta)/length):0;
            return Vector2.Distance(start+delta*at,point);
        }
        private string MovementBridge084(World world,EntityState unit,Vector2 destination)
        {
            if(unit.def.waterborne)return "";
            for(int i=0;i<world.Layout.Bridges.Length;i++)
            {
                var span=world.Layout.Bridges[i];
                if(span.Contains(unit.position))return "BRIDGE "+(i+1);
                if(span.Approach(unit.position))return "BRIDGE "+(i+1)+" APPROACH";
            }
            if((unit.pathGoal-destination).sqrMagnitude>4||unit.pathIndex>=unit.path.Count)return "";
            Vector2 start=unit.position;
            for(int n=Mathf.Max(0,unit.pathIndex);n<unit.path.Count;n++)
            {
                Vector2 end=unit.path[n];int next=-1;float first=float.MaxValue;
                for(int i=0;i<world.Layout.Bridges.Length;i++)
                {
                    var span=world.Layout.Bridges[i];
                    // The route is owned intent, but an unexplored crossing is
                    // not a map discovery supplied by the HUD.
                    if(world.Fog.At(game.LocalPlayerSlot,span.Center)==VisibilityState.Unexplored)continue;
                    if(MovementBridgeEntry084(span,start,end,out float at)&&at<first){first=at;next=i;}
                }
                if(next>=0)return "BRIDGE "+(next+1)+" AHEAD";start=end;
            }
            return "";
        }
        private static bool MovementBridgeEntry084(BridgeSpan span,Vector2 start,Vector2 end,out float entry)
        {
            Vector2 a=span.Local(start),b=span.Local(end),delta=b-a;float from=0,to=1;
            bool Clip(float origin,float direction,float low,float high)
            {
                if(Mathf.Abs(direction)<1e-8f)return origin>=low&&origin<=high;
                float x=(low-origin)/direction,y=(high-origin)/direction;if(x>y){float swap=x;x=y;y=swap;}
                from=Mathf.Max(from,x);to=Mathf.Min(to,y);return from<=to;
            }
            // A saved leg must actually enter the deck interior. Touching a
            // same-bank ramp or railing is not an upcoming bridge crossing.
            const float edge=.001f;
            bool hit=Clip(a.x,delta.x,-span.HalfLength+edge,span.HalfLength-edge)&&
                Clip(a.y,delta.y,-span.HalfWidth+edge,span.HalfWidth-edge);entry=from;return hit&&to-from>edge;
        }
        private string MovementStatus084(EntityState selected)
        {return TryMovementFeedback084(selected,out _,out string activity)?activity:selected.status;}
    }
}
