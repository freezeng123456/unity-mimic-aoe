using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        private struct RecoveryDisc {public Vector2 position;public float radiusSquared,startSquared;}
        private readonly List<RecoveryDisc> recoveryDiscs=new List<RecoveryDisc>(64);
        private Vector2 recoveryStart;
        private readonly List<int>[] recoveryBins=new List<int>[32*32];
        private int[] recoveryVisits=new int[64];private int recoveryQuery;
        private Func<Vector2,bool> recoveryFilter;
        private Func<Vector2,Vector2,bool> recoverySegmentFilter;
        private GridNavigation fineRecoveryNavigation,recoveryNavigation;
        private float recoveryRadius;
        private byte[] fineRecoveryPointMemo;
        private int fineRecoveryAttempts,fineRecoveryFailures,fineRecoveryDeferrals,allyRecoveryCoarseAttempts;private double fineRecoveryPeakMs;
        private bool fineRecoveryDeferred;private int fineRecoverySlots=1;
        private sealed class FineRecoveryRequest {public EntityState unit;public UnitOrder order;}
        private readonly Queue<FineRecoveryRequest> pendingFineRecoveryQueue=new Queue<FineRecoveryRequest>();
        private readonly Dictionary<int,FineRecoveryRequest> pendingFineRecoveries=new Dictionary<int,FineRecoveryRequest>();
        private readonly HashSet<int> reservedFineRecoveries=new HashSet<int>();
        private void BeginFineRecoveryTick()
        {
            fineRecoverySlots=1;reservedFineRecoveries.Clear();int count=pendingFineRecoveryQueue.Count;
            for(int i=0;i<count;i++)
            {
                var request=pendingFineRecoveryQueue.Dequeue();var unit=request.unit;
                if(!pendingFineRecoveries.TryGetValue(unit.id,out var current)||!ReferenceEquals(current,request))continue;
                var order=unit.orders.Count>0?unit.orders.Peek():null;
                if(!unit.Alive||!unit.Complete||unit.garrisonedIn!=0||!ReferenceEquals(order,request.order)){pendingFineRecoveries.Remove(unit.id);continue;}
                pendingFineRecoveryQueue.Enqueue(request);
                if(reservedFineRecoveries.Count<fineRecoverySlots)reservedFineRecoveries.Add(unit.id);
            }
        }
        private void EndFineRecoveryTick()
        {
            // If a reserved actor no longer asks for recovery, it must not hold
            // the queue behind an attack, finished task or changed intention.
            foreach(int id in reservedFineRecoveries)pendingFineRecoveries.Remove(id);
        }
        private bool ReserveFineRecovery(EntityState unit)
        {
            if(fineRecoverySlots>0&&(pendingFineRecoveries.Count==0||reservedFineRecoveries.Contains(unit.id)))
            {fineRecoverySlots--;pendingFineRecoveries.Remove(unit.id);return true;}
            var order=unit.orders.Count>0?unit.orders.Peek():null;
            if(!pendingFineRecoveries.TryGetValue(unit.id,out var request)||!ReferenceEquals(request.order,order))
            {
                request=new FineRecoveryRequest{unit=unit,order=order};pendingFineRecoveries[unit.id]=request;pendingFineRecoveryQueue.Enqueue(request);
            }
            fineRecoveryDeferred=true;fineRecoveryDeferrals++;return false;
        }
        private Func<Vector2,bool> fineRecoveryFilter;
        private Func<Vector2,Vector2,bool> fineRecoverySegmentFilter;
        // Only a stalled actor pays for a route around stationary allies.
        // The full snapshot matches live segment checks; spatial bins keep each
        // point and segment query local without mutating the building grid.
        private bool TryAllyRecovery(EntityState unit,Vector2 goal)
        {
            fineRecoveryDeferred=false;
            bool queued=pendingFineRecoveries.TryGetValue(unit.id,out var pending);
            var order=unit.orders.Count>0?unit.orders.Peek():null;
            if(queued&&!ReferenceEquals(pending.order,order)){pendingFineRecoveries.Remove(unit.id);queued=false;}
            if(queued&&!reservedFineRecoveries.Contains(unit.id))
            {fineRecoveryDeferred=true;fineRecoveryDeferrals++;return false;}
            var navigation=NavigationFor(unit);recoveryDiscs.Clear();recoveryStart=unit.position;
            foreach(var bin in recoveryBins)bin?.Clear();
            foreach(var other in Entities)
            {
                if(!StationaryAlly(unit,other))continue;
                float radius=AllyClearanceRadius(unit,other,unit.position);
                int index=recoveryDiscs.Count;recoveryDiscs.Add(new RecoveryDisc{position=other.position,radiusSquared=radius*radius,startSquared=(other.position-unit.position).sqrMagnitude});
                for(int y=Mathf.Clamp(Mathf.FloorToInt((other.position.y-radius)/4),0,31);y<=Mathf.Clamp(Mathf.FloorToInt((other.position.y+radius)/4),0,31);y++)
                    for(int x=Mathf.Clamp(Mathf.FloorToInt((other.position.x-radius)/4),0,31);x<=Mathf.Clamp(Mathf.FloorToInt((other.position.x+radius)/4),0,31);x++)
                    {int cell=x+y*32;if(recoveryBins[cell]==null)recoveryBins[cell]=new List<int>(4);recoveryBins[cell].Add(index);}
            }
            if(recoveryDiscs.Count==0)return false;while(recoveryVisits.Length<recoveryDiscs.Count)Array.Resize(ref recoveryVisits,recoveryVisits.Length*2);
            if(recoveryFilter==null)recoveryFilter=RecoveryPointFree;if(recoverySegmentFilter==null)recoverySegmentFilter=RecoverySegmentFree;
            // This queued query already failed on the coarse graph. Its turn
            // rebuilds the current collision snapshot and searches the fine graph
            // directly; waiting actors do neither search until their reservation.
            var route=new List<Vector2>();
            if(!queued){allyRecoveryCoarseAttempts++;route=navigation.FindPath(unit.position,goal,unit.def.radius,recoveryFilter,recoverySegmentFilter);}
            if(route.Count<2||(route[route.Count-1]-goal).sqrMagnitude>.0001f)
            {
                // A one-meter graph may miss a body-clear passage between parked
                // formation rows. Only a failed recovery pays for the finer graph;
                // original static cells and all ally discs still use exact sweeps.
                if(!navigation.IsWalkable(unit.position,unit.def.radius)||!navigation.IsWalkable(goal,unit.def.radius)||!RecoveryPointFree(unit.position)||!RecoveryPointFree(goal))return false;
                if(!ReserveFineRecovery(unit))return false;
                recoveryNavigation=navigation;recoveryRadius=unit.def.radius;
                if(fineRecoveryNavigation==null)fineRecoveryNavigation=new GridNavigation(navigation.Width*4,navigation.Height*4);
                if(fineRecoveryFilter==null)fineRecoveryFilter=FineRecoveryPointFree;
                if(fineRecoverySegmentFilter==null)fineRecoverySegmentFilter=FineRecoverySegmentFree;
                if(fineRecoveryPointMemo==null)fineRecoveryPointMemo=new byte[fineRecoveryNavigation.Width*fineRecoveryNavigation.Height];
                Array.Clear(fineRecoveryPointMemo,0,fineRecoveryPointMemo.Length);
                fineRecoveryAttempts++;long fineStart=System.Diagnostics.Stopwatch.GetTimestamp();
                var fine=fineRecoveryNavigation.FindPath(unit.position*4,goal*4,0,fineRecoveryFilter,fineRecoverySegmentFilter);
                fineRecoveryPeakMs=Math.Max(fineRecoveryPeakMs,(System.Diagnostics.Stopwatch.GetTimestamp()-fineStart)*1000.0/System.Diagnostics.Stopwatch.Frequency);
                if(fine.Count<2){fineRecoveryFailures++;return false;}route.Clear();foreach(var point in fine)route.Add(point*.25f);
                if((route[route.Count-1]-goal).sqrMagnitude>.0001f){fineRecoveryFailures++;return false;}
            }
            pendingFineRecoveries.Remove(unit.id);unit.path.Clear();unit.path.Add(route[0]);
            for(int i=1;i<route.Count;i++)
            {
                int pieces=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(route[i-1],route[i])/8));
                for(int piece=1;piece<=pieces;piece++)unit.path.Add(Vector2.Lerp(route[i-1],route[i],(float)piece/pieces));
            }
            unit.pathIndex=1;unit.pathGoal=goal;
            unit.pathVersion=navigation.Version;unit.pathRetryAt=Time+1.2f;unit.pathAvoidsAllies=true;
            return true;
        }
        private bool FineRecoveryPointFree(Vector2 point)
        {
            // The fine search is synchronous. Its navigation, radius and complete
            // ally snapshot stay fixed until it returns. Clear this memo before
            // each search; do not reuse it across versions, actors or channels.
            int x=(int)point.x,y=(int)point.y;
            bool node=point.x==x&&point.y==y&&x>=0&&y>=0&&x<fineRecoveryNavigation.Width&&y<fineRecoveryNavigation.Height;
            int index=node?x+y*fineRecoveryNavigation.Width:0;
            if(node&&fineRecoveryPointMemo[index]!=0)return fineRecoveryPointMemo[index]==2;
            point*=.25f;bool free=recoveryNavigation.IsWalkable(point,recoveryRadius)&&RecoveryPointFree(point);
            if(node)fineRecoveryPointMemo[index]=(byte)(free?2:1);
            return free;
        }
        private bool FineRecoverySegmentFree(Vector2 start,Vector2 end)
        {start*=.25f;end*=.25f;return recoveryNavigation.ClearLine(start,end,recoveryRadius)&&RecoverySegmentFree(start,end);}
        private bool RecoveryPointFree(Vector2 point)
        {
            var bin=recoveryBins[Mathf.Clamp(Mathf.FloorToInt(point.x/4),0,31)+Mathf.Clamp(Mathf.FloorToInt(point.y/4),0,31)*32];
            if(bin==null)return true;
            foreach(int index in bin)
            {
                var disc=recoveryDiscs[index];float distance=(point-disc.position).sqrMagnitude;
                if(distance>=disc.radiusSquared)continue;
                // Preserve the actual start and permit moving out of a pre-existing
                // contact. Never snap or deepen that contact to manufacture a path.
                if(disc.startSquared<disc.radiusSquared&&distance+.00001f>=disc.startSquared&&Vector2.Dot(point-recoveryStart,disc.position-recoveryStart)<=0)continue;
                return false;
            }
            return true;
        }
        private bool RecoverySegmentFree(Vector2 start,Vector2 end)
        {
            Vector2 delta=end-start;float length=delta.sqrMagnitude;if(length<.0000001f)return RecoveryPointFree(start);
            if(++recoveryQuery==int.MaxValue){Array.Clear(recoveryVisits,0,recoveryVisits.Length);recoveryQuery=1;}
            int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(start.x,end.x)/4),0,31),x1=Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(start.x,end.x)/4),0,31);
            int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(start.y,end.y)/4),0,31),y1=Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(start.y,end.y)/4),0,31);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                var bin=recoveryBins[x+y*32];if(bin==null)continue;
                foreach(int index in bin)
                {
                    if(recoveryVisits[index]==recoveryQuery)continue;recoveryVisits[index]=recoveryQuery;var disc=recoveryDiscs[index];
                    float along=Vector2.Dot(disc.position-start,delta)/length;
                    if((start-disc.position).sqrMagnitude<disc.radiusSquared&&along<=0)continue;
                    if((start+delta*Mathf.Clamp01(along)-disc.position).sqrMagnitude<disc.radiusSquared)return false;
                }
            }
            return true;
        }
        private static float AllyClearanceRadius(EntityState unit,EntityState other,Vector2 start)
        {
            float physical=unit.def.radius+other.def.radius,buffered=physical+.1f;
            float distance=(start-other.position).sqrMagnitude;
            // Steering may leave less than the preferred buffer at a parked ally.
            // Recover through the actual free space without weakening body collision.
            return distance>=physical*physical&&distance<buffered*buffered?physical+.00001f:buffered;
        }
        private bool AllyMovementSegmentFree(EntityState unit,Vector2 start,Vector2 end)
        {
            // Keep stationary recovery/parking buffers in the existing planner.
            if(!AllySegmentFree(unit,start,end,unit.pathAvoidsAllies))return false;
            var segment=end-start;float lengthSquared=segment.sqrMagnitude;
            if(lengthSquared<.000000000001f)return true;
            // A moving ally is still a physical body. Test the entire submitted
            // short step, including sub-centimetre steps; soft steering alone
            // cannot guarantee separation. Moving bodies get no parking buffer.
            foreach(var other in Nearby((start+end)*.5f,segment.magnitude*.5f+3.5f))
            {
                if(!PhysicalMovementAlly(unit,other))continue;
                float radius=unit.def.radius+other.def.radius,radiusSquared=radius*radius;
                float startSquared=(other.position-start).sqrMagnitude;
                float along=Vector2.Dot(other.position-start,segment)/lengthSquared;
                // Existing contacts may separate monotonically. Never snap,
                // deepen a contact, or cross the body while leaving it.
                if(startSquared<radiusSquared&&along<=0&&
                    (other.position-end).sqrMagnitude+.0000001f>=startSquared)continue;
                var closest=start+segment*Mathf.Clamp01(along);
                if((closest-other.position).sqrMagnitude<radiusSquared)return false;
            }
            return true;
        }
        private bool AllySegmentFree(EntityState unit,Vector2 start,Vector2 end,bool preferredBuffer=true)
        {
            var segment=end-start;float lengthSquared=segment.sqrMagnitude;if(lengthSquared<.0001f)return true;
            foreach(var other in Nearby((start+end)*.5f,segment.magnitude*.5f+3.5f))
            {
                if(!StationaryAlly(unit,other))continue;
                float radius=preferredBuffer?AllyClearanceRadius(unit,other,start):unit.def.radius+other.def.radius;
                float along=Vector2.Dot(other.position-start,segment)/lengthSquared;
                if((other.position-start).sqrMagnitude<radius*radius&&along<=0)continue;
                var closest=start+segment*Mathf.Clamp01(along);
                if((closest-other.position).sqrMagnitude<radius*radius)return false;
            }
            return true;
        }
    }
}
