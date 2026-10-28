using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Original authored layouts. Factories resolve a finite placement search
    // once and freeze its actual result in the document, never in a live match.
    public static class RiverContent070
    {
        public const string Skirmish="traders_bend070",Scout="campaign_scout070",Convoy="campaign_convoy070",Retreat="campaign_retreat070";
        public static bool Contains(string id)=>id==Skirmish||id==Scout||id==Convoy||id==Retreat;
        public static ScenarioDocument Build(RuleCatalog rules,string id)
        {
            if(!Contains(id))throw new ArgumentException("Unknown river expedition content",nameof(id));
            bool bay=id==Convoy;var d=Terrain(rules,id,bay);
            if(id!=Skirmish)
            {
                d.enableElimination=false;
                d.starts[0].age=id==Retreat?3:2;d.starts[1].age=d.starts[0].age;
                d.starts[0].initialResources=id==Scout?new Cost(200,180,100):new Cost(160,180,120);
                d.starts[1].initialResources=new Cost();
            }
            var author=new Author(rules,d);
            OpeningResources(author);
            if(id==Skirmish)Trade(author);
            else
            {
                author.Put("supply-house","house",0,new Vector2(12,44));
                if(id==Scout)Scouting(author);
                else if(id==Convoy)ConvoyMission(author);
                else RetreatMission(author);
            }
            return author.Finish();
        }
        static ScenarioDocument Terrain(RuleCatalog rules,string id,bool bay)
        {
            var d=new ScenarioDocument{id=id,name=id==Skirmish?"Traders' Bend":id==Scout?"Valley Reconnaissance":id==Convoy?"Across the Bay":"Estuary Withdrawal",
                description=id==Skirmish?"Two bridges carry land armies and a northern trade route. The broad central bend permits ships and a landing behind either crossing. Home resources are paired; forward mines, woods and fishing demand expansion.":
                    id==Scout?"The restored supply route reaches a winding valley. Survey the far bank, choose a crossing and remove the blockade. A western lookout offers optional supplies.":
                    id==Convoy?"The blockade has fallen. Carry the landing party through a split river, protect its transport and establish a foothold on the central island.":
                    "The island foothold is supplied, but a counterattack threatens the forward port. Hold its perimeter, then withdraw the surviving rear guard by bridge or transport.",
                rulesHash=SnapshotCodec.RulesHash(rules),seed=id==Skirmish?902001:id==Scout?902004:id==Convoy?902005:902006,
                starts=new[]{Start(0),Start(1)}};
            if(bay)
            {
                d.rivers=new[]{
                    River("west-channel",new[]{new Vector2(70,-8),new Vector2(62,17),new Vector2(44,37),new Vector2(42,62),new Vector2(52,85),new Vector2(70,105),new Vector2(70,136)},9),
                    River("east-channel",new[]{new Vector2(70,-8),new Vector2(78,17),new Vector2(96,37),new Vector2(100,62),new Vector2(90,85),new Vector2(70,105),new Vector2(70,136)},10)};
            }
            else
            {
                var main=River("meandering-river",new[]{new Vector2(58,-8),new Vector2(64,16),new Vector2(48,34),new Vector2(48,50),new Vector2(80,78),new Vector2(80,94),new Vector2(64,112),new Vector2(70,136)},8);
                main.halfWidths=new float[]{8,8,9,10,10,9,8,8};d.rivers=new[]{main};
                var c=MapLayout.Sample(d.rivers[0].points,d.rivers[0].halfWidths);
                d.bridges=new[]{Bridge(c,"south-crossing",34),Bridge(c,"north-crossing",94)};
            }
            var channels=new RiverChannel[d.rivers.Length];
            for(int i=0;i<channels.Length;i++)channels[i]=MapLayout.Sample(d.rivers[i].points,d.rivers[i].halfWidths);
            var layout=new MapLayout(MapTopology.Scenario,channels,Array.Empty<BridgeSpan>(),Array.Empty<Vector2>(),Array.Empty<Vector2>(),Array.Empty<Vector2>(),scenario:d);
            for(int y=0;y<=128;y++)for(int x=0;x<=128;x++)
            {
                float distance=layout.SignedBankDistance(new Vector2(x,y));
                d.heightsCm[x+y*129]=distance>=0?0:Mathf.RoundToInt(Mathf.Max(-1.2f,distance*.6f)*100);
            }
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float distance=layout.SignedBankDistance(new Vector2(x+.5f,y+.5f));
                d.surfaces[x+y*128]=distance<0?2:distance<3?1:0;
            }
            return d;
        }
        static ScenarioStart Start(int player)
        {
            Vector2 Reflect(Vector2 p)=>player==0?p:new Vector2(128-p.x,128-p.y);
            var workers=new[]{new Vector2(12,18),new Vector2(14,16),new Vector2(17,14),new Vector2(26,14),new Vector2(30,16),new Vector2(32,18)};
            for(int i=0;i<workers.Length;i++)workers[i]=Reflect(workers[i]);
            return new ScenarioStart{player=player,team=player,civilization=player==0?"coast":"france",town=Reflect(new Vector2(22,24)),
                explorer=Reflect(new Vector2(35,24)),workers=workers};
        }
        static ScenarioRiver River(string id,Vector2[] points,float width)
        {
            var widths=new float[points.Length];for(int i=0;i<widths.Length;i++)widths[i]=width+(i==3?2:i==4?1:0);
            return new ScenarioRiver{id=id,points=points,halfWidths=widths,smooth=true};
        }
        static ScenarioBridge Bridge(RiverChannel river,string id,float z)
        {
            var center=MapLayout.CenterAt(river,z);var tangent=(MapLayout.CenterAt(river,z+1)-MapLayout.CenterAt(river,z-1)).normalized;
            var direction=new Vector2(tangent.y,-tangent.x);
            return new ScenarioBridge{id=id,center=center,direction=direction,halfLength=12,halfWidth=4.8f,rampLength=8,height=3.2f};
        }
        static void OpeningResources(Author a)
        {
            for(int player=0;player<2;player++)
            {
                Vector2 R(Vector2 p)=>player==0?p:new Vector2(128-p.x,128-p.y);
                a.Put("home-mine-"+player,"mine",-1,R(new Vector2(36,12)),remaining:2200);
                for(int i=0;i<5;i++)a.Put("home-food-"+player+"-"+i,"deer",-1,R(new Vector2(9+i*2,9)),remaining:350);
                for(int row=0;row<3;row++)for(int col=0;col<5;col++)
                    a.Put("home-wood-"+player+"-"+row+"-"+col,"tree",-1,R(new Vector2(7+col*2,34+row*2)),remaining:250);
            }
            // The contestable deposits are deliberately away from both homes.
            for(int player=0;player<2;player++)
            {
                Vector2 R(Vector2 p)=>player==0?p:new Vector2(128-p.x,128-p.y);
                bool island=a.Document.id==Convoy;
                a.Put("forward-mine-"+player,"mine",-1,island?(player==0?new Vector2(20,75):new Vector2(117,65)):R(new Vector2(28,76)),remaining:4200);
                for(int i=0;i<8;i++)a.Put("forward-wood-"+player+"-"+i,"tree",-1,
                    island?(player==0?new Vector2(10+i*2,62):new Vector2(114+i%4*2,46+i/4*2)):R(new Vector2(16+i*2,62)),remaining:300);
            }
            for(int i=0;i<4;i++)
            {
                var p=MapLayout.CenterAt(a.World.Layout.Channels[0],20+i*28);
                a.Put("river-fish-"+i,"fish",-1,p,remaining:2500);
            }
        }
        static void Trade(Author a)
        {
            var b=a.Document.bridges[1];var extent=b.halfLength+b.rampLength+5;
            var left=b.center-b.direction*extent;var right=b.center+b.direction*extent;
            a.Document.tradeRoute=new[]{new Vector2(12,44),new Vector2(34,55),left,b.center-b.direction*b.halfLength,b.center+b.direction*b.halfLength,right,
                new Vector2(118,116),new Vector2(116,84),new Vector2(94,73)};
            a.Document.tradeSockets=new[]{
                new ScenarioTradeSocket{id="west-home-trade",position=a.Document.tradeRoute[0],waypoint=0},
                new ScenarioTradeSocket{id="west-crossing-trade",position=a.Document.tradeRoute[1],waypoint=1},
                new ScenarioTradeSocket{id="east-crossing-trade",position=a.Document.tradeRoute[8],waypoint=8},
                new ScenarioTradeSocket{id="east-home-trade",position=a.Document.tradeRoute[7],waypoint=7}};
            a.Document.neutralBindings=new[]{
                new ScenarioNeutralBinding{definition="river_guild",socketId="west-crossing-trade"},
                new ScenarioNeutralBinding{definition="coastal_league",socketId="east-crossing-trade"}};
        }
        static void Scouting(Author a)
        {
            a.Army("field-force",0,new Vector2(27,42),4,2);
            a.Put("road-market","market",1,new Vector2(80,38));
            a.Put("guard-a","pike",1,new Vector2(74,39));a.Put("guard-b","pike",1,new Vector2(78,45));
            // Stay beyond the default south-crossing army's acquisition range
            // until the survey orders the raid. The western lookout can still
            // reveal them to a player who deliberately scouts farther upstream.
            a.Put("raider-a","hussar",1,new Vector2(24,100));a.Put("raider-b","hussar",1,new Vector2(27,100));
            a.Group("raiders","raider-a","raider-b");
            a.Region("survey-zone",new Vector2(72,33),new Vector2(10,10));a.Region("lookout-zone",new Vector2(30,78),new Vector2(10,10));
            a.Goal("survey","Survey the far bank","Move the explorer across either bridge into the far-bank marker.","reach_region","start-0-explorer","survey-zone",1,deadline:360);
            a.Goal("blockade","Break the road blockade","Reveal and destroy the enemy supply market with the field force.","destroy_object","road-market","",1,deadline:420,after:"survey");
            a.Goal("lookout","Optional: western lookout","The explorer can visit the western lookout for one wood delivery.","reach_region","start-0-explorer","lookout-zone",1,required:false);
            a.Trigger("lookout-supply",C("objective_complete",objective:"lookout"),new[]{Grant(ResourceKind.Wood,120),Message("The western lookout has sent 120 wood. The delivery is available once.")});
            a.Trigger("survey-ambush",C("objective_complete",objective:"survey"),new[]{MoveAttack(1,"raiders",a.Document.starts[0].town),Message("Cavalry are approaching the supply camp. Protect the town while the field force clears the blockade.")});
            a.Protect("camp-loss","start-0-town","The supply camp has fallen.");
            a.Trigger("force-loss",C("units_lost",objectId:"field-force",amount:5),new[]{Lose("Too few field troops remain to clear the blockade.")});
        }
        static void ConvoyMission(Author a)
        {
            a.Army("landing-force",0,new Vector2(29,30),4,2);
            a.Put("transport","transport_ship",0,MapLayout.CenterAt(a.World.Layout.Channels[0],30));
            a.Put("escort","gunboat",0,MapLayout.CenterAt(a.World.Layout.Channels[0],18));
            a.Dock("home-port",0,new Vector2(39,29));
            a.Put("river-raider","gunboat",1,MapLayout.CenterAt(a.World.Layout.Channels[0],65));
            a.Put("island-blockade","market",1,new Vector2(73,77));
            a.Put("island-guard-a","pike",1,new Vector2(69,72));a.Put("island-guard-b","pike",1,new Vector2(77,71));
            a.Region("bay-water",MapLayout.CenterAt(a.World.Layout.Channels[0],58),new Vector2(12,12));
            a.Region("island",new Vector2(70,55),new Vector2(20,20));
            a.Goal("embark","Load the landing party","At least four of the six designated soldiers must actually board the marked transport.","embark_group","landing-force","",4,deadline:180,target:"transport");
            a.Goal("sail","Escort the loaded transport","Bring at least four designated soldiers aboard the transport to the western island channel. Use the escort or take the longer outer channel.","escort_group","landing-force","bay-water",4,deadline:240,after:"embark",target:"transport");
            a.Goal("land","Establish the island foothold","Unload, then move at least four designated soldiers into the island interior. Ships and workers do not count.","reach_region","landing-force","island",4,deadline:240,after:"sail");
            a.Goal("blockade","Clear the island supply market","Reveal and destroy the supply market after the landing party has established its foothold.","destroy_object","island-blockade","",1,deadline:360,after:"land");
            a.Trigger("river-ambush",C("objective_complete",objective:"embark"),new[]{MoveAttack(1,"river-raider",MapLayout.CenterAt(a.World.Layout.Channels[0],36)),Message("An enemy boat is closing on the convoy. Keep the transport behind its escort or sail around the eastern channel.")});
            a.Protect("transport-loss","transport","The designated transport was sunk.");
            a.Protect("camp-loss","start-0-town","The supply camp has fallen.");
            a.Trigger("landing-loss",C("units_lost",objectId:"landing-force",amount:3),new[]{Lose("Fewer than four designated landing troops remain.")});
        }
        static void RetreatMission(Author a)
        {
            a.Army("rear-guard",0,new Vector2(97,70),4,2);
            a.Dock("forward-port",0,new Vector2(90,70));
            a.Put("transport","transport_ship",0,MapLayout.CenterAt(a.World.Layout.Channels[0],68));
            a.Put("escort","gunboat",0,MapLayout.CenterAt(a.World.Layout.Channels[0],55));
            a.Put("supply-cache","market",1,new Vector2(109,81));
            a.Region("port-perimeter",new Vector2(100,73),new Vector2(16,14));
            a.Region("rear-camp",new Vector2(33,22),new Vector2(16,12));
            a.Goal("hold","Hold the port perimeter","Keep at least four rear-guard soldiers in the marker for 40 uncontested seconds. The forward dock must survive.","hold_region","rear-guard","port-perimeter",4,deadline:240,hold:40);
            a.Goal("withdraw","Withdraw the rear guard","After holding, bring at least four designated survivors to the rear camp. Cross either bridge or board, sail and unload.","reach_region","rear-guard","rear-camp",4,deadline:300,after:"hold");
            a.Goal("cache","Optional: seize repair supplies","Destroy the enemy supply market for one repair delivery.","destroy_object","supply-cache","",1,required:false);
            a.Trigger("cache-supply",C("objective_complete",objective:"cache"),new[]{Grant(ResourceKind.Wood,100),Grant(ResourceKind.Coin,80),Message("One repair delivery: 100 wood and 80 coin. Dock repairs still consume resources normally.")});
            a.Trigger("land-wave-one",C("time",time:12),new[]{Spawn(1,"hussar",2,new Vector2(114,87),"raid-one"),MoveAttack(1,"raid-one",new Vector2(100,73)),Message("Cavalry are advancing toward the forward port.")});
            a.Trigger("land-wave-two",C("time",time:28),new[]{Spawn(1,"pike",3,new Vector2(112,89),"raid-two"),MoveAttack(1,"raid-two",new Vector2(100,73))});
            a.Trigger("water-wave",C("time",time:24),new[]{Spawn(1,"gunboat",1,MapLayout.CenterAt(a.World.Layout.Channels[0],94),"river-wave"),MoveAttack(1,"river-wave",MapLayout.CenterAt(a.World.Layout.Channels[0],70)),Message("A boat is approaching the dock. The rear guard can leave over the south bridge or use the transport once the perimeter is secure.")});
            a.Protect("port-loss","forward-port","The forward port was destroyed before the withdrawal was complete.");
            a.Protect("camp-loss","start-0-town","The rear camp has fallen.");
            a.Trigger("rear-guard-loss",C("units_lost",objectId:"rear-guard",amount:3),new[]{Lose("Fewer than four designated rear-guard soldiers survive.")});
        }
        static ScenarioCondition C(string kind,string objectId="",string objective="",float amount=1,float time=0)
            =>new ScenarioCondition{kind=kind,objectId=objectId,objectiveId=objective,amount=amount,time=time};
        static ScenarioAction Message(string text)=>new ScenarioAction{kind="message",message=text};
        static ScenarioAction Grant(ResourceKind resource,float amount)=>new ScenarioAction{kind="grant_resource",resource=resource,amount=amount};
        static ScenarioAction Lose(string text)=>new ScenarioAction{kind="lose",message=text};
        static ScenarioAction MoveAttack(int player,string group,Vector2 point)=>new ScenarioAction{kind="order_attack_move",player=player,objectId=group,point=point};
        static ScenarioAction Spawn(int player,string definition,int count,Vector2 point,string group)=>new ScenarioAction{kind="spawn",player=player,definition=definition,count=count,point=point,objectId=group};
        sealed class Author
        {
            public readonly ScenarioDocument Document;public readonly World World;
            readonly RuleCatalog rules;readonly List<ScenarioPlacement> resources=new List<ScenarioPlacement>(),objects=new List<ScenarioPlacement>();
            readonly List<ScenarioGroup> groups=new List<ScenarioGroup>();readonly List<ScenarioRegion> regions=new List<ScenarioRegion>();
            readonly List<ScenarioObjective> objectives=new List<ScenarioObjective>();readonly List<ScenarioTrigger> triggers=new List<ScenarioTrigger>();
            public Author(RuleCatalog rules,ScenarioDocument document)
            {
                this.rules=rules;Document=document;
                World=new World(rules,document,new[]{"coast","france"},new[]{0,1},5);
            }
            public void Put(string id,string definition,int owner,Vector2 point,float remaining=-1)
            {
                string reason=World.ScenarioPlacementReason(definition,point);
                if(reason!="")throw new InvalidOperationException(Document.id+" / "+id+": "+reason+" at "+point);
                World.Spawn(definition,owner,point);
                var p=new ScenarioPlacement{id=id,definition=definition,owner=owner,position=point,remaining=remaining};
                (rules.Entity(definition).kind==EntityKind.Resource?resources:objects).Add(p);
            }
            public void Dock(string id,int owner,Vector2 near)
            {
                for(int ring=0;ring<=14;ring++)for(int y=-ring;y<=ring;y++)for(int x=-ring;x<=ring;x++)
                {
                    if(Mathf.Max(Mathf.Abs(x),Mathf.Abs(y))!=ring)continue;
                    var point=near+new Vector2(x,y);if(point.x<4||point.y<4||point.x>124||point.y>124||World.ScenarioPlacementReason("dock",point)!=""||!CompilerClearance("dock",point))continue;
                    Put(id,"dock",owner,point);return;
                }
                throw new InvalidOperationException(Document.id+" has no authored port near "+near);
            }
            bool CompilerClearance(string definition,Vector2 point)
            {
                // Frozen documents also reserve the conservative axis-aligned
                // building bounds checked by the portable map compiler.
                var d=rules.Entity(definition);
                foreach(var e in World.Entities)
                {
                    if(!e.Alive||e.def.id=="trade_socket")continue;
                    var delta=point-e.position;
                    float width=d.width*.5f+(e.def.kind==EntityKind.Building?e.def.width*.5f:e.def.radius)+.08f;
                    float depth=d.depth*.5f+(e.def.kind==EntityKind.Building?e.def.depth*.5f:e.def.radius)+.08f;
                    if(Mathf.Abs(delta.x)<width&&Mathf.Abs(delta.y)<depth)return false;
                }
                return true;
            }
            public void Army(string group,int owner,Vector2 near,int musketeers,int pikes)
            {
                var ids=new string[musketeers+pikes];
                for(int i=0;i<ids.Length;i++){ids[i]=group+"-"+i;Put(ids[i],i<musketeers?"musketeer":"pike",owner,near+new Vector2(i%3*2,i/3*2));}
                Group(group,ids);
            }
            public void Group(string id,params string[] ids)=>groups.Add(new ScenarioGroup{id=id,objectIds=ids});
            public void Region(string id,Vector2 center,Vector2 size)=>regions.Add(new ScenarioRegion{id=id,center=center,size=size});
            public void Goal(string id,string title,string description,string kind,string objectId,string region,float amount,bool required=true,float deadline=0,float hold=30,string after="",string target="")
                =>objectives.Add(new ScenarioObjective{id=id,title=title,description=description,kind=kind,objectId=objectId,regionId=region,amount=amount,
                    required=required,deadline=deadline,holdSeconds=hold,prerequisites=after==""?Array.Empty<string>():new[]{after},targetId=target});
            public void Trigger(string id,ScenarioCondition condition,ScenarioAction[] actions)=>triggers.Add(new ScenarioTrigger{id=id,conditions=new[]{condition},actions=actions});
            public void Protect(string id,string objectId,string reason)=>Trigger(id,C("object_dead",objectId),new[]{Lose(reason)});
            public ScenarioDocument Finish()
            {
                Document.resources=resources.ToArray();Document.objects=objects.ToArray();Document.groups=groups.ToArray();Document.regions=regions.ToArray();
                Document.objectives=objectives.ToArray();Document.triggers=triggers.ToArray();
                string reason=ScenarioCompiler.Validate(Document,rules,2);if(reason!="")throw new InvalidOperationException(Document.id+": "+reason);
                return ScenarioCodec.Clone(Document);
            }
        }
    }
}
