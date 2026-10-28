using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed class ScenarioEditorSession
    {
        public const int MaxHistory=32,MaxHistoryBytes=8*1024*1024;
        readonly RuleCatalog rules;ScenarioDocument document;
        readonly List<string> undo=new List<string>(),redo=new List<string>();
        public ScenarioDocument Document=>ScenarioCodec.Clone(document);
        public bool CanUndo=>undo.Count>0;
        public bool CanRedo=>redo.Count>0;
        public ScenarioEditorSession(RuleCatalog rules,ScenarioDocument document)
        {
            this.rules=rules??throw new ArgumentNullException(nameof(rules));string reason=ScenarioCodec.Validate(document,rules);
            if(reason!="")throw new ArgumentException(reason);this.document=ScenarioCodec.Clone(document);
        }
        public bool Apply(Action<ScenarioDocument> mutation,out string reason)
        {
            reason="";if(mutation==null){reason="No editor mutation provided";return false;}
            try
            {
                var draft=ScenarioCodec.Clone(document);mutation(draft);
                reason=ScenarioCodec.Validate(draft,rules);if(reason!="")return false;
                string before=JsonUtility.ToJson(document),after=JsonUtility.ToJson(draft);
                if(before==after)return true;
                // The mutation can retain its argument; it must never gain a
                // mutable reference to the session's committed document.
                var owned=ScenarioCodec.Clone(draft);Push(undo,before);redo.Clear();document=owned;return true;
            }
            catch(Exception error)when(!(error is OutOfMemoryException))
            {reason=error.Message;return false;}
        }
        public bool Undo()
        {
            if(!CanUndo)return false;string previous=undo[undo.Count-1];var restored=JsonUtility.FromJson<ScenarioDocument>(previous);
            string reason=ScenarioCodec.Validate(restored,rules);if(reason!="")throw new InvalidOperationException(reason);
            Push(redo,JsonUtility.ToJson(document));undo.RemoveAt(undo.Count-1);document=restored;return true;
        }
        public bool Redo()
        {
            if(!CanRedo)return false;string next=redo[redo.Count-1];var restored=JsonUtility.FromJson<ScenarioDocument>(next);
            string reason=ScenarioCodec.Validate(restored,rules);if(reason!="")throw new InvalidOperationException(reason);
            Push(undo,JsonUtility.ToJson(document));redo.RemoveAt(redo.Count-1);document=restored;return true;
        }
        static void Push(List<string> history,string value)
        {
            history.Add(value);long bytes=0;foreach(string item in history)bytes+=System.Text.Encoding.UTF8.GetByteCount(item);
            while(history.Count>MaxHistory||bytes>MaxHistoryBytes&&history.Count>1)
            {bytes-=System.Text.Encoding.UTF8.GetByteCount(history[0]);history.RemoveAt(0);}
        }
        public static ScenarioDocument FromMap(RuleCatalog rules,string mapId,int seed,int playerCount)
        {
            if(rules==null)throw new ArgumentNullException(nameof(rules));if(playerCount<2||playerCount>4)throw new ArgumentOutOfRangeException(nameof(playerCount));
            var civilizations=new string[playerCount];var teams=new int[playerCount];
            for(int i=0;i<playerCount;i++){civilizations[i]="coast";teams[i]=i;}
            // A fresh normal world resolves the existing template's actual
            // opening locations once. The document stores those locations,
            // so custom playtest does not repeat template auto-placement.
            var world=new World(rules,seed,civilizations,teams,5,mapId);var layout=world.Layout;
            var d=new ScenarioDocument{id=Guid.NewGuid().ToString("N"),name=mapId+" scenario",description="Editable expansion of "+mapId+" (seed "+seed+")",rulesHash=SnapshotCodec.RulesHash(rules),seed=seed,
                waterHeightCm=Mathf.RoundToInt(layout.WaterHeight*100),starts=new ScenarioStart[playerCount]};
            for(int y=0;y<=128;y++)for(int x=0;x<=128;x++)d.heightsCm[x+y*129]=Mathf.RoundToInt(layout.BankHeight(new Vector2(x,y))*100);
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                var p=new Vector2(x+.5f,y+.5f);float shore=layout.SignedBankDistance(p);d.surfaces[x+y*128]=shore<=0?2:shore<3?1:0;
                foreach(var obstacle in world.TerrainObstacles)if(Mathf.Abs(p.x-obstacle.x)<=obstacle.z+.5f&&Mathf.Abs(p.y-obstacle.y)<=obstacle.z+.5f){d.surfaces[x+y*128]=3;break;}
            }
            d.rivers=new ScenarioRiver[layout.Channels.Length];int total=0;
            for(int i=0;i<layout.Channels.Length;i++)
            {
                var channel=layout.Channels[i];Vector2[] centers=(Vector2[])channel.Centers.Clone();float[] widths=(float[])channel.HalfWidths.Clone();
                // Existing sampled splines are already explicit editable paths.
                // Do not spline them a second time or change their bank width.
                d.rivers[i]=new ScenarioRiver{id="river-"+i,points=centers,halfWidths=widths,smooth=false};total+=centers.Length;
            }
            if(total>ScenarioCodec.MaxRiverPoints)
            {
                foreach(var river in d.rivers)Simplify(river,.025f);
                total=0;foreach(var river in d.rivers)total+=river.points.Length;
                if(total>ScenarioCodec.MaxRiverPoints)throw new InvalidOperationException("Template river geometry exceeds the editable 512-point limit");
            }
            d.bridges=new ScenarioBridge[layout.Bridges.Length];
            for(int i=0;i<d.bridges.Length;i++){var b=layout.Bridges[i];d.bridges[i]=new ScenarioBridge{id="bridge-"+i,center=b.Center,direction=b.Direction,halfLength=b.HalfLength,halfWidth=b.HalfWidth,rampLength=b.RampLength,height=b.Height};}
            var excluded=new HashSet<int>();
            for(int player=0;player<playerCount;player++)
            {
                var p=world.Players[player];var town=world.Entities.Find(e=>e.owner==player&&e.def.id=="town");var explorer=world.Entities.Find(e=>e.owner==player&&e.def.id=="explorer");
                var workers=world.Entities.FindAll(e=>e.owner==player&&e.Worker);var positions=new Vector2[workers.Count];
                for(int i=0;i<workers.Count;i++){positions[i]=workers[i].position;excluded.Add(workers[i].id);}
                d.starts[player]=new ScenarioStart{player=player,team=p.team,age=p.age,civilization=p.civilization,initialResources=p.resources,town=town.position,explorer=explorer.position,workers=positions};
                excluded.Add(town.id);excluded.Add(explorer.id);
            }
            d.tradeRoute=(Vector2[])world.TradeRoute.Clone();d.tradeSockets=new ScenarioTradeSocket[world.TradeSockets.Count];var socketIds=new Dictionary<int,string>();
            for(int i=0;i<d.tradeSockets.Length;i++)
            {var s=world.TradeSockets[i];var e=world.Get(s.entity);string id="socket-"+i;d.tradeSockets[i]=new ScenarioTradeSocket{id=id,position=e.position,waypoint=s.waypoint};socketIds.Add(e.id,id);excluded.Add(e.id);}
            d.treasures=new ScenarioTreasure[world.Treasures.Count];
            for(int i=0;i<d.treasures.Length;i++)
            {
                var t=world.Treasures[i];var guards=new Vector2[t.guards.Count];excluded.Add(t.entity);
                for(int g=0;g<guards.Length;g++){guards[g]=world.Get(t.guards[g]).position;excluded.Add(t.guards[g]);}
                d.treasures[i]=new ScenarioTreasure{id="treasure-"+i,definition=Array.IndexOf(rules.landmarks.treasures,t.definition),position=world.Get(t.entity).position,guardPositions=guards};
            }
            d.neutralBindings=new ScenarioNeutralBinding[world.NeutralAllies.Count];
            for(int i=0;i<d.neutralBindings.Length;i++){var n=world.NeutralAllies[i];d.neutralBindings[i]=new ScenarioNeutralBinding{definition=n.id,socketId=socketIds[n.site]};}
            var resources=new List<ScenarioPlacement>();var objects=new List<ScenarioPlacement>();
            foreach(var e in world.Entities)if(!excluded.Contains(e.id)&&e.Alive)
            {
                var placement=new ScenarioPlacement{id="object-"+e.id,definition=e.def.id,position=e.position,owner=e.owner,remaining=e.remaining,complete=e.Complete};
                (e.def.kind==EntityKind.Resource?resources:objects).Add(placement);
            }
            d.resources=resources.ToArray();d.objects=objects.ToArray();string reason=ScenarioCodec.Validate(d,rules);
            if(reason!="")throw new InvalidOperationException(reason);return d;
        }
        static void Simplify(ScenarioRiver river,float tolerance)
        {
            var keep=new SortedSet<int>{0,river.points.Length-1};
            void Segment(int first,int last)
            {
                if(last<=first+1)return;float largest=tolerance;int chosen=-1;Vector2 delta=river.points[last]-river.points[first];
                for(int i=first+1;i<last;i++)
                {
                    float t=Mathf.Clamp01(Vector2.Dot(river.points[i]-river.points[first],delta)/Mathf.Max(.000001f,delta.sqrMagnitude));
                    float error=Mathf.Max(Vector2.Distance(river.points[i],river.points[first]+delta*t),Mathf.Abs(river.halfWidths[i]-Mathf.Lerp(river.halfWidths[first],river.halfWidths[last],t)));
                    if(error>largest){largest=error;chosen=i;}
                }
                if(chosen<0)return;keep.Add(chosen);Segment(first,chosen);Segment(chosen,last);
            }
            Segment(0,river.points.Length-1);var points=new Vector2[keep.Count];var widths=new float[keep.Count];int index=0;
            foreach(int i in keep){points[index]=river.points[i];widths[index++]=river.halfWidths[i];}river.points=points;river.halfWidths=widths;
        }
    }
}
