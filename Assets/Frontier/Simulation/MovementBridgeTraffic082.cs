using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        private struct BridgeTrafficRule
        {public BridgeSpan span;public float direction;}
        // Rebuilt from the saved path/current origin for each mover. No hidden
        // velocity, reservation or new save field is retained between actors.
        private readonly List<BridgeTrafficRule> bridgeTrafficRules=new List<BridgeTrafficRule>(4);
        private int bridgeTrafficActor;
        private Vector2 bridgeTrafficOrigin;
        private float bridgeTrafficRadius;
        private void PrepareBridgeTraffic(EntityState unit,Vector2 goal,IReadOnlyList<Vector2> route)
        {
            bridgeTrafficRules.Clear();bridgeTrafficActor=unit.id;bridgeTrafficOrigin=unit.position;bridgeTrafficRadius=unit.def.radius;
            if(unit.def.waterborne)return;
            if(route!=null&&route.Count>=2)foreach(var span in Layout.Bridges)
            {
                // A public destination on the bridge still means that exact
                // destination. Through traffic uses separated local halves.
                if(span.Approach(goal))continue;
                bool found=false;Vector2 first=Vector2.zero,last=Vector2.zero;
                for(int i=1;i<route.Count;i++)
                {
                    Vector2 a=span.Local(route[i-1]),b=span.Local(route[i]);
                    if(!ClipBridgeTraffic(a,b,span.HalfLength+span.RampLength,span.HalfWidth,out Vector2 entry,out Vector2 exit))continue;
                    if(!found){first=entry;found=true;}last=exit;
                }
                bool crossedMedian=found&&first.x*last.x<=0;
                bool exitsFromDeck=found&&span.Contains(unit.position)&&
                    Mathf.Abs(last.x)>=span.HalfLength+span.RampLength-.0001f&&last.x*(last.x-first.x)>0;
                // Same-bank movement may touch a ramp without using its deck.
                // Retain complete saved-path crossings and deck-born exits.
                if(found&&Mathf.Abs(last.x-first.x)>.25f&&(crossedMedian||exitsFromDeck))
                    bridgeTrafficRules.Add(new BridgeTrafficRule{span=span,direction=Mathf.Sign(last.x-first.x)});
            }
            // Pure query: a saved transit supplements a newly shortened path.
            // A newly identified reverse traversal takes precedence, while an
            // exact destination on this bridge keeps its original freedom.
            var order=unit.orders.Count>0?unit.orders.Peek():null;
            if(order==null||order.bridgeTransitIndex<=0||order.bridgeTransitIndex>Layout.Bridges.Length||
                (order.bridgeTransitDirection!=1&&order.bridgeTransitDirection!=-1))return;
            var current=Layout.Bridges[order.bridgeTransitIndex-1];
            if(!current.Approach(unit.position)||current.Approach(goal))return;
            foreach(var rule in bridgeTrafficRules)if(ReferenceEquals(rule.span,current))return;
            bridgeTrafficRules.Add(new BridgeTrafficRule{span=current,direction=order.bridgeTransitDirection});
        }
        // Called only by actual MoveCore execution, never by path/line queries.
        // A new public order starts with zero context; outgoing ramp recovery
        // retains the old through direction until the body really leaves it.
        private void CommitBridgeTransit082(EntityState unit,Vector2 goal)
        {
            var order=unit.orders.Count>0?unit.orders.Peek():null;if(order==null)return;
            if(order.bridgeTransitIndex!=0)
            {
                int index=order.bridgeTransitIndex-1;
                if(unit.def.waterborne||index<0||index>=Layout.Bridges.Length||
                    !Layout.Bridges[index].Approach(unit.position)||Layout.Bridges[index].Approach(goal))
                {order.bridgeTransitIndex=0;order.bridgeTransitDirection=0;}
            }
            if(unit.def.waterborne||bridgeTrafficActor!=unit.id)return;
            foreach(var rule in bridgeTrafficRules)
            {
                if(!rule.span.Approach(unit.position)||rule.span.Approach(goal))continue;
                order.bridgeTransitIndex=Array.IndexOf(Layout.Bridges,rule.span)+1;
                order.bridgeTransitDirection=rule.direction>0?1:-1;return;
            }
        }
        private static bool ClipBridgeTraffic(Vector2 a,Vector2 b,float across,float along,out Vector2 entry,out Vector2 exit)
        {
            float t0=0,t1=1;Vector2 delta=b-a;
            bool Clip(float origin,float direction,float half)
            {
                if(Mathf.Abs(direction)<.000001f)return Mathf.Abs(origin)<=half;
                float low=(-half-origin)/direction,high=(half-origin)/direction;
                if(low>high){float swap=low;low=high;high=swap;}
                t0=Mathf.Max(t0,low);t1=Mathf.Min(t1,high);return t0<=t1;
            }
            if(!Clip(a.x,delta.x,across)||!Clip(a.y,delta.y,along)){entry=exit=Vector2.zero;return false;}
            entry=a+delta*t0;exit=a+delta*t1;return true;
        }
        private const double BridgeTrafficTolerance082=.00001f;
        // Point and segment queries share one affine double coordinate system.
        // The point tolerance, bridge dimensions and body radius stay unchanged.
        private static void BridgeTrafficLocal082(BridgeSpan span,Vector2 point,out double across,out double along)
        {
            double x=(double)point.x-span.Center.x,y=(double)point.y-span.Center.y;
            across=x*span.Direction.x+y*span.Direction.y;
            along=-x*span.Direction.y+y*span.Direction.x;
        }
        private bool BridgeTrafficCorrection082(BridgeTrafficRule rule,float bridgeEnd,double baseLimit,out double originalSide)
        {
            BridgeTrafficLocal082(rule.span,bridgeTrafficOrigin,out double across,out double along);
            across*=rule.direction;originalSide=along*rule.direction;
            double width=rule.span.HalfWidth+(across<-bridgeEnd?6:0);
            return across>=-bridgeEnd-6&&across<=bridgeEnd&&Math.Abs(along)<=width&&originalSide>baseLimit;
        }
        private bool BridgeTrafficPointFree(Vector2 point)
        {
            foreach(var rule in bridgeTrafficRules)
            {
                var span=rule.span;float bridgeEnd=span.HalfLength+span.RampLength;
                BridgeTrafficLocal082(span,point,out double across,out double side);
                across*=rule.direction;side*=rule.direction;
                double width=span.HalfWidth+(across<-bridgeEnd?6:0);
                if(across<-bridgeEnd-6||across>bridgeEnd||Math.Abs(side)>width)continue;
                double limit=-bridgeTrafficRadius-.1f;
                if(BridgeTrafficCorrection082(rule,bridgeEnd,limit,out double originalSide))
                {
                    double x=(double)point.x-bridgeTrafficOrigin.x,y=(double)point.y-bridgeTrafficOrigin.y;
                    limit=Math.Max(limit,originalSide-.75*Math.Sqrt(x*x+y*y));
                }
                if(side>limit+BridgeTrafficTolerance082)return false;
            }
            return true;
        }
        // Clip a parameter interval with explicit open bounds. In particular,
        // entry x=-bridgeEnd is excluded when the wider entry flank disappears.
        // Exact zero is deliberate: near-parallel or very short intersections
        // must not discard a thin but real forbidden part of the segment.
        private static bool ClipBridgeTrafficInterval082(double origin,double delta,double lower,double upper,
            bool lowerOpen,bool upperOpen,ref double from,ref double to,ref bool fromOpen,ref bool toOpen)
        {
            if(delta==0)return (origin>lower||!lowerOpen&&origin==lower)&&(origin<upper||!upperOpen&&origin==upper);
            double a=(lower-origin)/delta,b=(upper-origin)/delta;bool aOpen=lowerOpen,bOpen=upperOpen;
            if(delta<0)
            {
                double swap=a;a=b;b=swap;bool open=aOpen;aOpen=bOpen;bOpen=open;
            }
            if(a>from){from=a;fromOpen=aOpen;}else if(a==from)fromOpen|=aOpen;
            if(b<to){to=b;toOpen=bOpen;}else if(b==to)toOpen|=bOpen;
            return from<to||from==to&&!fromOpen&&!toOpen;
        }
        private bool BridgeTrafficSegmentFree(Vector2 start,Vector2 end)
        {
            if(bridgeTrafficRules.Count==0)return true;
            // Exact endpoint checks also preserve the point contract at isolated
            // closed touches; every nonempty interior is checked continuously.
            if(!BridgeTrafficPointFree(start)||!BridgeTrafficPointFree(end))return false;
            double worldX=(double)start.x-bridgeTrafficOrigin.x,worldY=(double)start.y-bridgeTrafficOrigin.y;
            double worldDx=(double)end.x-start.x,worldDy=(double)end.y-start.y;
            foreach(var rule in bridgeTrafficRules)
            {
                var span=rule.span;float bridgeEnd=span.HalfLength+span.RampLength;
                BridgeTrafficLocal082(span,start,out double across,out double side);
                BridgeTrafficLocal082(span,end,out double lastAcross,out double lastSide);
                across*=rule.direction;lastAcross*=rule.direction;side*=rule.direction;lastSide*=rule.direction;
                double dx=lastAcross-across,dy=lastSide-side,baseLimit=-bridgeTrafficRadius-.1f;
                bool correcting=BridgeTrafficCorrection082(rule,bridgeEnd,baseLimit,out double originalSide);
                bool Region(double left,double right,double width,bool rightOpen)
                {
                    double from=0,to=1;bool fromOpen=false,toOpen=false;
                    if(!ClipBridgeTrafficInterval082(across,dx,left,right,false,rightOpen,ref from,ref to,ref fromOpen,ref toOpen)||
                        !ClipBridgeTrafficInterval082(side,dy,-width,width,false,false,ref from,ref to,ref fromOpen,ref toOpen)||
                        !ClipBridgeTrafficInterval082(side,dy,baseLimit+BridgeTrafficTolerance082,double.PositiveInfinity,true,false,ref from,ref to,ref fromOpen,ref toOpen))return true;
                    if(!correcting)return false;
                    double Excess(double t)
                    {
                        double x=worldX+worldDx*t,y=worldY+worldDy*t;
                        return .75*Math.Sqrt(x*x+y*y)+side+dy*t-originalSide;
                    }
                    // On the strict side>base+epsilon interval, failure also
                    // requires Excess>epsilon. Excess is convex, so its maximum
                    // over the closure lies at one of these two endpoints. An
                    // open endpoint with a strict excess has a violating nearby
                    // interior; an empty open-only touch was removed by clipping.
                    return Excess(from)<=BridgeTrafficTolerance082&&Excess(to)<=BridgeTrafficTolerance082;
                }
                double entryStart=-bridgeEnd-6,entryWidth=span.HalfWidth+6;
                if(!Region(entryStart,-bridgeEnd,entryWidth,true)||!Region(-bridgeEnd,bridgeEnd,span.HalfWidth,false))return false;
            }
            return true;
        }
        private bool MovementLineFree(EntityState unit,Vector2 start,Vector2 end)=>
            NavigationFor(unit).ClearLine(start,end,unit.def.radius)&&
            (bridgeTrafficActor!=unit.id||BridgeTrafficSegmentFree(start,end));
        private const float BridgeConvoyPreferredHeadway082=.1f;
        // A canonical geometric key is independent of actor processing order.
        // Every same-key wait edge strictly increases directed bridge-across.
        private bool BridgeConvoyKey082(EntityState unit,Vector2 forward,out BridgeSpan span,out float direction)
        {
            span=null;direction=0;
            foreach(var candidate in Layout.Bridges)
            {
                float dot=Vector2.Dot(forward,candidate.Direction);if(Mathf.Abs(dot)<=.5f)continue;
                float sign=dot>0?1:-1;Vector2 local=candidate.Local(unit.position);
                float end=candidate.HalfLength+candidate.RampLength,across=local.x*sign;
                float along=candidate.HalfWidth+(across<-end?6:0);
                if(across<-end-6||across>end||Mathf.Abs(local.y)>along)continue;
                span=candidate;direction=sign;return true;
            }
            return false;
        }
        private static Vector2 BridgeConvoyForward082(EntityState unit)
        {
            for(int i=Math.Max(0,unit.pathIndex);i<unit.path.Count;i++)
            {Vector2 to=unit.path[i]-unit.position;if(to.sqrMagnitude>.0001f)return to.normalized;}
            return Vector2.zero;
        }
        // Keep the existing preferred body spacing before any full-speed
        // recovery shortcut can consume it. A zero gap means physical waiting,
        // with the normal progress clocks and recovery policy left unchanged.
        private bool TryBridgeConvoyVelocity082(EntityState unit,Vector2 forward,float speed,float dt,out Vector2 velocity)
        {
            velocity=Vector2.zero;if(unit.def.waterborne||bridgeTrafficActor!=unit.id||dt<=0||speed<=0)return false;
            if(!BridgeConvoyKey082(unit,forward,out var span,out float direction))return false;
            bool guided=false;foreach(var rule in bridgeTrafficRules)
                if(ReferenceEquals(rule.span,span)&&rule.direction==direction){guided=true;break;}
            if(!guided)return false;
            float maximum=speed*dt;Vector2 full=unit.position+forward*maximum;
            if(!MovementLineFree(unit,unit.position,full))return false;
            float allowed=maximum;bool following=false;float originAcross=span.Local(unit.position).x*direction;
            foreach(var other in Nearby(unit.position,maximum+3.6f))
            {
                if(!PhysicalMovementAlly(unit,other)||StationaryAlly(unit,other))continue;
                Vector2 otherForward=BridgeConvoyForward082(other);if(Vector2.Dot(forward,otherForward)<.75f)continue;
                if(!BridgeConvoyKey082(other,otherForward,out var otherSpan,out float otherDirection)||
                    !ReferenceEquals(span,otherSpan)||otherDirection!=direction||
                    span.Local(other.position).x*direction<=originAcross+.0001f)continue;
                Vector2 relative=other.position-unit.position;float along=Vector2.Dot(relative,forward);if(along<=0)continue;
                float physical=unit.def.radius+other.def.radius;
                if(relative.sqrMagnitude<physical*physical)continue; // Preserve original monotone overlap escape.
                float preferred=physical+BridgeConvoyPreferredHeadway082;
                float lateralSquared=Mathf.Max(0,relative.sqrMagnitude-along*along);
                if(lateralSquared>=preferred*preferred)continue;
                float contact=along-Mathf.Sqrt(preferred*preferred-lateralSquared);
                if(contact<allowed){allowed=Mathf.Max(0,contact-.0001f);following=true;}
            }
            if(!following||allowed>=maximum)return false;
            Vector2 candidate=unit.position+forward*allowed;
            if(!MovementLineFree(unit,unit.position,candidate)||!AllyMovementSegmentFree(unit,unit.position,candidate))return false;
            velocity=forward*(allowed/dt);return true;
        }
        private List<Vector2> FindMovementPath(EntityState unit,Vector2 goal,Func<Vector2,bool> pointFilter=null,Func<Vector2,Vector2,bool> segmentFilter=null)
        {
            var navigation=NavigationFor(unit);
            var route=navigation.FindPath(unit.position,goal,unit.def.radius,pointFilter,segmentFilter);
            if(route.Count<2)
            {
                // The parked-body coarse graph may fail before fine recovery.
                // Identify bridge direction from the static route as well, so
                // that the fine graph retains the same traffic constraint.
                PrepareBridgeTraffic(unit,goal,navigation.FindPath(unit.position,goal,unit.def.radius));
                return route;
            }
            bool Point(Vector2 point)=>BridgeTrafficPointFree(point)&&(pointFilter==null||pointFilter(point));
            bool Segment(Vector2 start,Vector2 end)=>BridgeTrafficSegmentFree(start,end)&&(segmentFilter==null||segmentFilter(start,end));
            return StabilizeBridgeTrafficRoute082(unit,goal,route,
                ()=>navigation.FindPath(unit.position,goal,unit.def.radius,Point,Segment));
        }
        // Rebuilding a route can choose a different bridge. Each new directed
        // bridge corridor is accumulated before another query; a returned path
        // must already satisfy the rules that its own next Tick will rebuild.
        // The callback can also rebuild a converted fine route. It must clear
        // any query-local point memo whenever this rule set changes.
        private List<Vector2> StabilizeBridgeTrafficRoute082(EntityState unit,Vector2 goal,List<Vector2> route,Func<List<Vector2>> rebuild)
        {
            if(route.Count<2)return route;
            if(unit.def.waterborne||Layout.Bridges.Length==0)
            {PrepareBridgeTraffic(unit,goal,route);return route;}
            var accumulated=new List<BridgeTrafficRule>(Layout.Bridges.Length);
            int attempts=Layout.Bridges.Length*2+1;
            for(int attempt=0;attempt<attempts;attempt++)
            {
                PrepareBridgeTraffic(unit,goal,route);bool added=false;
                foreach(var rule in bridgeTrafficRules)
                {
                    bool known=false;
                    foreach(var previous in accumulated)
                        if(ReferenceEquals(previous.span,rule.span)&&previous.direction==rule.direction){known=true;break;}
                    if(!known){accumulated.Add(rule);added=true;}
                }
                bridgeTrafficRules.Clear();bridgeTrafficRules.AddRange(accumulated);
                bool valid=true;
                for(int i=1;i<route.Count;i++)
                    if(!MovementLineFree(unit,route[i-1],route[i])){valid=false;break;}
                if(valid)return route;
                // At most two directions per existing bridge can be added. A
                // failed or non-progressing query stays a blocked route rather
                // than returning a corridor that will invalidate itself.
                if(!added)return new List<Vector2>();
                route=rebuild();if(route.Count<2)return route;
            }
            return new List<Vector2>();
        }
    }
}
