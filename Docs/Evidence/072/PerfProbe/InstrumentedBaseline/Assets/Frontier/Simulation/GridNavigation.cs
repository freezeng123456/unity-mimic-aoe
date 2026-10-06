using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed class GridNavigation
    {
        public readonly int Width,Height;
        public int Version {get;private set;}=1;
        private readonly bool[] blocked,terrainBlocked;
        private readonly float[] costs;
        private readonly int[] parents,visited,closed;
        private int search;
        private readonly List<Node> heap=new List<Node>(2048);
        // All actors of the same radius share the static clearance graph until
        // an obstacle changes. A sealed route must not flood the same half-map
        // once per soldier on every retry. Dynamic ally filters stay per query.
        private readonly Dictionary<float,ClearanceGraph> clearanceGraphs=new Dictionary<float,ClearanceGraph>();
        private sealed class ClearanceGraph
        {public int version;public bool[] walkable;public int[] component;}
        private static readonly int[] Dx={1,-1,0,0,1,1,-1,-1},Dy={0,0,1,-1,1,-1,1,-1};
        public GridNavigation(int w=128,int h=128){Width=w;Height=h;blocked=new bool[w*h];terrainBlocked=new bool[w*h];costs=new float[w*h];parents=new int[w*h];visited=new int[w*h];closed=new int[w*h];}
        public NavigationSnapshot CaptureState()=>new NavigationSnapshot{width=Width,height=Height,version=Version,blocked=(bool[])blocked.Clone(),terrain=(bool[])terrainBlocked.Clone()};
        public void RestoreState(NavigationSnapshot state)
        {
            if(state==null||state.width!=Width||state.height!=Height||state.version<1||state.blocked?.Length!=blocked.Length||state.terrain?.Length!=terrainBlocked.Length)
                throw new ArgumentException("Invalid navigation state");
            Array.Copy(state.blocked,blocked,blocked.Length);Array.Copy(state.terrain,terrainBlocked,terrainBlocked.Length);
            Version=state.version;clearanceGraphs.Clear();heap.Clear();search=0;Array.Clear(visited,0,visited.Length);Array.Clear(closed,0,closed.Length);
        }
        private bool Inside(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height;
        public bool IsWalkable(Vector2 point,float radius=.4f)
        {
            if(point.x-radius<.5f||point.y-radius<.5f||point.x+radius>Width-1.5f||point.y+radius>Height-1.5f)return false;
            int x0=Mathf.FloorToInt(point.x-radius+.5f),x1=Mathf.FloorToInt(point.x+radius+.5f),y0=Mathf.FloorToInt(point.y-radius+.5f),y1=Mathf.FloorToInt(point.y+radius+.5f);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                if(!Inside(x,y))return false;if(!blocked[x+y*Width]&&!terrainBlocked[x+y*Width])continue;
                float dx=Mathf.Max(0,Mathf.Abs(point.x-x)-.5f),dy=Mathf.Max(0,Mathf.Abs(point.y-y)-.5f);
                if(dx*dx+dy*dy<radius*radius+.00001f)return false;
            }
            return true;
        }
        public void SetBlocked(Vector2 point,float radius,bool value)
        {
            bool changed=false;var footprint=BlockedFootprint(point,radius);
            int x0=Mathf.CeilToInt(footprint.xMin),x1=Mathf.FloorToInt(footprint.xMax);
            int y0=Mathf.CeilToInt(footprint.yMin),y1=Mathf.FloorToInt(footprint.yMax);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)if(Inside(x,y)&&blocked[x+y*Width]!=value){blocked[x+y*Width]=value;changed=true;}
            if(changed)Version++;
        }
        // Terrain and placed obstacles are independent layers: demolishing a
        // building or releasing a temporary blockade cannot erase a riverbank.
        public void SetTerrainBlocked(Vector2 point,float radius,bool value)
        {
            bool changed=false;var footprint=BlockedFootprint(point,radius);
            for(int y=Mathf.CeilToInt(footprint.yMin);y<=Mathf.FloorToInt(footprint.yMax);y++)
                for(int x=Mathf.CeilToInt(footprint.xMin);x<=Mathf.FloorToInt(footprint.xMax);x++)
                    if(Inside(x,y)&&terrainBlocked[x+y*Width]!=value){terrainBlocked[x+y*Width]=value;changed=true;}
            if(changed)Version++;
        }
        public void ConfigureTerrain(Func<Vector2,bool> isBlocked)
        {
            bool changed=false;
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
            {int i=x+y*Width;bool value=isBlocked(new Vector2(x,y));if(terrainBlocked[i]!=value){terrainBlocked[i]=value;changed=true;}}
            if(changed)Version++;
        }
        // Static obstacles occupy complete grid cells. Expose their actual
        // outer edges so presentation does not interpret the input as a disc.
        public static Rect BlockedFootprint(Vector2 point,float radius)
        {
            int r=Mathf.FloorToInt(radius);var center=Vector2Int.RoundToInt(point);
            return new Rect(center.x-r-.5f,center.y-r-.5f,r*2+1,r*2+1);
        }
        public bool IsAreaFree(Vector2 center,float width,float depth)
        {
            if(center.x-width*.5f<1||center.y-depth*.5f<1||center.x+width*.5f>Width-2||center.y+depth*.5f>Height-2)return false;
            for(int y=Mathf.FloorToInt(center.y-depth*.5f);y<=Mathf.CeilToInt(center.y+depth*.5f);y++)for(int x=Mathf.FloorToInt(center.x-width*.5f);x<=Mathf.CeilToInt(center.x+width*.5f);x++)if(!Inside(x,y)||blocked[x+y*Width]||terrainBlocked[x+y*Width])return false;
            return true;
        }
        public Vector2 NearestWalkable(Vector2 point,float radius=.4f)
        {
            point=new Vector2(Mathf.Clamp(point.x,1,Width-2),Mathf.Clamp(point.y,1,Height-2));if(IsWalkable(point,radius))return point;
            Vector2 nearest=point;float best=float.MaxValue;
            for(int ring=1;ring<=24;ring++)
            {
                for(int y=-ring;y<=ring;y++)for(int x=-ring;x<=ring;x++)
                {if(Mathf.Abs(x)!=ring&&Mathf.Abs(y)!=ring)continue;Vector2 q=Vector2Int.RoundToInt(point)+new Vector2(x,y);float distance=(q-point).sqrMagnitude;if(distance<best&&IsWalkable(q,radius)){best=distance;nearest=q;}}
                if(best<float.MaxValue)return nearest;
            }
            return nearest;
        }
        public List<Vector2> FindPath(Vector2 start,Vector2 end,float radius=.4f,Func<Vector2,bool> extraWalkable=null,Func<Vector2,Vector2,bool> extraSegmentWalkable=null)
        {
#if UNITY_EDITOR
            using var perfTimer072=PerfProbeDiagnostics072.Measure(extraWalkable!=null||extraSegmentWalkable!=null?"FindPathFiltered":"FindPathOrdinary");
#endif
            var output=new List<Vector2>();start=NearestWalkable(start,radius);end=NearestWalkable(end,radius);
            if(!PathWalkable(start,radius,extraWalkable)||!PathWalkable(end,radius,extraWalkable))return output;
            if(PathClearLine(start,end,radius,extraWalkable,extraSegmentWalkable)){output.Add(start);output.Add(end);return output;}
            // Fractional positions near a wall can round into an obstructed cell.
            // Both endpoints must connect to walkable graph nodes without crossing it.
            var graph=Clearance(radius);
            int s=ConnectedNode(start,radius,extraWalkable,graph,extraSegmentWalkable),g=ConnectedNode(end,radius,extraWalkable,graph,extraSegmentWalkable);
            if(s<0||g<0)return output;
            if(graph.component[s]!=graph.component[g])return output;
            int gx=g%Width,gy=g/Width;bool found=false;
            int attempts=extraWalkable!=null||extraSegmentWalkable!=null?2:1;
            for(int attempt=0;attempt<attempts&&!found;attempt++)
            {
                List<int> starts=null;HashSet<int> ends=null;
                if(attempt>0)
                {
                    // Parked discs can isolate the nearest integer endpoint while
                    // the fractional actor sees another usable grid connection.
                    // Retry those alternatives together, rather than reporting a
                    // false blockade or flooding the map once per candidate.
                    starts=ConnectedNodes(start,radius,extraWalkable,graph,extraSegmentWalkable);
                    ends=new HashSet<int>(ConnectedNodes(end,radius,extraWalkable,graph,extraSegmentWalkable));
                    if(starts.Count==0||ends.Count==0)return output;
                }
                search++;if(search==int.MaxValue){Array.Clear(visited,0,visited.Length);Array.Clear(closed,0,closed.Length);search=1;}
                heap.Clear();
                if(starts==null){costs[s]=0;visited[s]=search;parents[s]=-1;Push(s,Heuristic(s%Width,s/Width,gx,gy));}
                else foreach(int source in starts)
                {float cost=Vector2.Distance(start,new Vector2(source%Width,source/Width));costs[source]=cost;visited[source]=search;parents[source]=-1;Push(source,cost+Heuristic(source%Width,source/Width,gx,gy));}
                while(heap.Count>0)
                {
                    int current=Pop();
#if UNITY_EDITOR
                    if(PerfProbeDiagnostics072.Enabled)PerfProbeDiagnostics072.PathNodePops++;
#endif
                    if(closed[current]==search)continue;closed[current]=search;
                    if(ends==null?current==g:ends.Contains(current)){g=current;found=true;break;}
                    int x=current%Width,y=current/Width;
                    for(int i=0;i<8;i++)
                    {
                        int nx=x+Dx[i],ny=y+Dy[i];if(!Inside(nx,ny))continue;int next=nx+ny*Width;
                        if(closed[next]==search||!NodeWalkable(nx,ny,graph,extraWalkable))continue;
                        if(i>=4&&(!NodeWalkable(x+Dx[i],y,graph,extraWalkable)||!NodeWalkable(x,y+Dy[i],graph,extraWalkable)))continue;
                        if((extraWalkable!=null||extraSegmentWalkable!=null)&&!PathClearLine(new Vector2(x,y),new Vector2(nx,ny),radius,extraWalkable,extraSegmentWalkable))continue;
                        float score=costs[current]+(i<4?1:1.41421356f);
                        if(visited[next]!=search||score<costs[next]){visited[next]=search;costs[next]=score;parents[next]=current;Push(next,score+Heuristic(nx,ny,gx,gy));}
                    }
                }
            }
            if(!found)return output;
            var reverse=new List<Vector2>();for(int at=g;at>=0;at=parents[at])reverse.Add(new Vector2(at%Width,at/Width));reverse.Reverse();
            reverse.Insert(0,start);reverse.Add(end);
            output.Add(start);int anchor=0;
            while(anchor<reverse.Count-1)
            {int next=anchor+1;while(next+1<reverse.Count&&PathClearLine(reverse[anchor],reverse[next+1],radius,extraWalkable,extraSegmentWalkable))next++;output.Add(reverse[next]);anchor=next;}
            return output;
        }
        private ClearanceGraph Clearance(float radius)
        {
            if(clearanceGraphs.TryGetValue(radius,out var graph)&&graph.version==Version)return graph;
            int count=Width*Height;
            if(graph==null)graph=new ClearanceGraph{walkable=new bool[count],component=new int[count]};
            Array.Clear(graph.component,0,count);
            for(int i=0;i<count;i++)graph.walkable[i]=IsWalkable(new Vector2(i%Width,i/Width),radius);
            var queue=new int[count];int label=0;
            for(int root=0;root<count;root++)
            {
                if(!graph.walkable[root]||graph.component[root]!=0)continue;
                label++;int head=0,tail=0;queue[tail++]=root;graph.component[root]=label;
                while(head<tail)
                {
                    int current=queue[head++],x=current%Width,y=current/Width;
                    for(int direction=0;direction<8;direction++)
                    {
                        int nx=x+Dx[direction],ny=y+Dy[direction];if(!Inside(nx,ny))continue;int next=nx+ny*Width;
                        if(!graph.walkable[next]||graph.component[next]!=0)continue;
                        if(direction>=4&&(!graph.walkable[x+Dx[direction]+y*Width]||!graph.walkable[x+(y+Dy[direction])*Width]))continue;
                        graph.component[next]=label;queue[tail++]=next;
                    }
                }
            }
            graph.version=Version;clearanceGraphs[radius]=graph;return graph;
        }
        private bool NodeWalkable(int x,int y,ClearanceGraph graph,Func<Vector2,bool> extraWalkable)
        {return graph.walkable[x+y*Width]&&(extraWalkable==null||extraWalkable(new Vector2(x,y)));}
        private int ConnectedNode(Vector2 point,float radius,Func<Vector2,bool> extraWalkable,ClearanceGraph graph,Func<Vector2,Vector2,bool> extraSegmentWalkable)
        {
            int cx=Mathf.RoundToInt(point.x),cy=Mathf.RoundToInt(point.y),best=-1;float distance=float.MaxValue;
            // A fractional actor in a narrow bank passage may need a farther
            // connection around a parked ally; every connection remains swept.
            int rings=extraWalkable!=null||extraSegmentWalkable!=null?6:3;
            for(int ring=0;ring<=rings;ring++)
            {
                for(int y=cy-ring;y<=cy+ring;y++)for(int x=cx-ring;x<=cx+ring;x++)
                {
                    if(!Inside(x,y)||Mathf.Max(Mathf.Abs(x-cx),Mathf.Abs(y-cy))!=ring)continue;
                    var node=new Vector2(x,y);float d=(node-point).sqrMagnitude;
                    if(d<distance&&NodeWalkable(x,y,graph,extraWalkable)&&PathClearLine(point,node,radius,extraWalkable,extraSegmentWalkable)){distance=d;best=x+y*Width;}
                }
                if(best>=0)return best;
            }
            return -1;
        }
        private List<int> ConnectedNodes(Vector2 point,float radius,Func<Vector2,bool> extraWalkable,ClearanceGraph graph,Func<Vector2,Vector2,bool> extraSegmentWalkable)
        {
            var nodes=new List<int>();int cx=Mathf.RoundToInt(point.x),cy=Mathf.RoundToInt(point.y);
            for(int y=cy-6;y<=cy+6;y++)for(int x=cx-6;x<=cx+6;x++)
                if(Inside(x,y)&&NodeWalkable(x,y,graph,extraWalkable)&&PathClearLine(point,new Vector2(x,y),radius,extraWalkable,extraSegmentWalkable))nodes.Add(x+y*Width);
            return nodes;
        }
        private bool PathWalkable(Vector2 point,float radius,Func<Vector2,bool> extraWalkable)
        {return IsWalkable(point,radius)&&(extraWalkable==null||extraWalkable(point));}
        private bool PathClearLine(Vector2 start,Vector2 end,float radius,Func<Vector2,bool> extraWalkable,Func<Vector2,Vector2,bool> extraSegmentWalkable)
        {
#if UNITY_EDITOR
            if(PerfProbeDiagnostics072.Enabled)PerfProbeDiagnostics072.PathClearLineCalls++;
#endif
            if(!ClearLine(start,end,radius)||extraSegmentWalkable!=null&&!extraSegmentWalkable(start,end))return false;if(extraWalkable==null)return true;
            int steps=Mathf.CeilToInt(Vector2.Distance(start,end)*3);
            for(int i=0;i<=steps;i++)if(!PathWalkable(Vector2.Lerp(start,end,i/(float)Mathf.Max(1,steps)),radius,extraWalkable))return false;
            return true;
        }
        public bool ClearLine(Vector2 start,Vector2 end,float radius)
        {
            if(!IsWalkable(start,radius)||!IsWalkable(end,radius))return false;
            Vector2 delta=end-start;float squaredLength=delta.sqrMagnitude;if(squaredLength<.0000001f)return true;
            float limit=radius*radius+.00001f,expandedHalf=.5f+radius;
            int y0=Mathf.FloorToInt(Mathf.Min(start.y,end.y)-radius+.5f),y1=Mathf.FloorToInt(Mathf.Max(start.y,end.y)+radius+.5f);
            for(int y=y0;y<=y1;y++)
            {
                // Clip the swept segment to one expanded cell row. A diagonal
                // journey examines a narrow corridor, not its full AABB area.
                float from=0,to=1;
                if(Mathf.Abs(delta.y)>.0000001f)
                {
                    float first=(y-expandedHalf-start.y)/delta.y,last=(y+expandedHalf-start.y)/delta.y;
                    if(first>last){float swap=first;first=last;last=swap;}
                    from=Mathf.Max(0,first);to=Mathf.Min(1,last);if(from>to)continue;
                }
                else if(Mathf.Abs(start.y-y)>expandedHalf)continue;
                float left=start.x+delta.x*from,right=start.x+delta.x*to;
                int x0=Mathf.FloorToInt(Mathf.Min(left,right)-radius+.5f),x1=Mathf.FloorToInt(Mathf.Max(left,right)+radius+.5f);
                for(int x=x0;x<=x1;x++)
                {
                    if(!Inside(x,y))return false;int index=x+y*Width;if(!blocked[index]&&!terrainBlocked[index])continue;
                    if(SegmentCrossesCell(start,delta,x,y))return false;
                    // Endpoints already passed IsWalkable. The remaining
                    // closest pair is a square corner and the segment interior.
                    for(int corner=0;corner<4;corner++)
                    {
                        Vector2 point=new Vector2(x+(corner%2==0?-.5f:.5f),y+(corner<2?-.5f:.5f));
                        float t=Mathf.Clamp(Vector2.Dot(point-start,delta)/squaredLength,0,1);
                        if((point-(start+delta*t)).sqrMagnitude<limit)return false;
                    }
                }
            }
            return true;
        }
        private static bool SegmentCrossesCell(Vector2 start,Vector2 delta,int x,int y)
        {
            float from=0,to=1;
            for(int axis=0;axis<2;axis++)
            {
                float origin=axis==0?start.x:start.y,direction=axis==0?delta.x:delta.y,center=axis==0?x:y;
                if(Mathf.Abs(direction)<.0000001f){if(Mathf.Abs(origin-center)>.5f)return false;continue;}
                float first=(center-.5f-origin)/direction,last=(center+.5f-origin)/direction;
                if(first>last){float swap=first;first=last;last=swap;}
                from=Mathf.Max(from,first);to=Mathf.Min(to,last);if(from>to)return false;
            }
            return true;
        }
        private static float Heuristic(int x,int y,int gx,int gy){int dx=Math.Abs(x-gx),dy=Math.Abs(y-gy);return Mathf.Max(dx,dy)+.41421356f*Mathf.Min(dx,dy);}
        private struct Node{public int id;public float score;}
        private void Push(int id,float score)
        {
            var value=new Node{id=id,score=score};heap.Add(value);int i=heap.Count-1;
            while(i>0){int parent=(i-1)/2;if(heap[parent].score<=score)break;heap[i]=heap[parent];i=parent;}heap[i]=value;
        }
        private int Pop()
        {
            int result=heap[0].id;int last=heap.Count-1;var value=heap[last];heap.RemoveAt(last);if(last==0)return result;int i=0;
            while(i*2+1<heap.Count){int child=i*2+1;if(child+1<heap.Count&&heap[child+1].score<heap[child].score)child++;if(heap[child].score>=value.score)break;heap[i]=heap[child];i=child;}heap[i]=value;return result;
        }
        public List<Vector2> FormationSlots(Vector2 center,int count,float spacing)
        {
            spacing=Mathf.Max(.9f,spacing);center=new Vector2(Mathf.Clamp(center.x,8,Width-9),Mathf.Clamp(center.y,8,Height-9));var result=new List<Vector2>(count);
            for(int ring=0;ring<Mathf.Max(Width,Height)&&result.Count<count;ring++)for(int y=-ring;y<=ring&&result.Count<count;y++)for(int x=-ring;x<=ring&&result.Count<count;x++)
            {
                if(ring>0&&Mathf.Abs(x)!=ring&&Mathf.Abs(y)!=ring)continue;var point=center+new Vector2(x,y)*spacing;if(!IsWalkable(point,spacing*.38f))continue;result.Add(point);
            }
            return result;
        }
    }
}
