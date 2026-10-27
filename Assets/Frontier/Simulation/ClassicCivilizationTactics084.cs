using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Explicitly controlled encounters. Their forces and era belong only to these
    // new IDs; the normal Age-I civilization development routes stay independent.
    public static class ClassicCivilizationTactics084
    {
        public const string British="tactics_british084",Dutch="tactics_dutch084",French="tactics_french084",Ottoman="tactics_ottoman084";
        public const string ArmyGroup="tactic-army",FrontGroup="tactic-front",RangedGroup="tactic-ranged";
        public const string Wave1Group="wave-one",Wave2Group="wave-two",DeploymentRegion="deployment";
        public const string DeploymentGoal="deployment-reached",Wave1Goal="wave-one-cleared",Wave2Goal="wave-two-cleared";
        public const string Wave1Trigger="wave-one-advance",Wave2Trigger="wave-two-arrival";
        public const float Wave1Seconds=10,Wave2Seconds=40,LimitSeconds=900;
        public static bool Contains(string id)=>id==British||id==Dutch||id==French||id==Ottoman;
        static void Known(string id){if(!Contains(id))throw new ArgumentException("Unknown classic tactical encounter",nameof(id));}
        public static string SourceMapName(string id)
        {Known(id);return id==British?"曲河谷":id==Dutch?"热带河湾":id==French?"支流汇合":"河口";}
        public static string SourceMapId(string id)
        {Known(id);return id==British?MapTopology.Winding:id==Dutch?MapTopology.Tropical:id==French?MapTopology.Tributaries:MapTopology.Estuary;}
        public static string Civilization(string id)
        {Known(id);return id==British?"coast":id==Dutch?"river":id==French?"france":"ottoman";}
        public static int StartingAge(string id){Known(id);return id==Dutch||id==French?3:2;}
        public static int Seed(string id){Known(id);return id==British?904851:id==Dutch?904852:id==French?904853:904854;}
        public static int ArmyCount(string id){Known(id);return id==British?7:id==Dutch?6:id==French?3:6;}
        public static int MinimumSurvivors(string id){Known(id);return id==British?4:id==Dutch?2:id==French?1:3;}
        public static string SurvivalText(string id)
        {Known(id);return id==British?"至少4兵，含2长弓和1长枪":id==Dutch?"至少2名Ruyter":id==French?"至少1名胸甲骑兵":"至少3兵，含1禁卫和1Abus";}
        public static int Wave2Count(string id){Known(id);return id==British||id==Ottoman?2:4;}
        public static int EnemyCount(string id)=>4+Wave2Count(id);
        public static string FirstEnemy(string id){Known(id);return id==Dutch?"hussar":"musketeer";}
        public static string SecondEnemy(string id){Known(id);return id==French?"musketeer":"hussar";}
        public static Vector2 DeploymentPoint(string id){Known(id);return id==French?new Vector2(104,50):new Vector2(24,76);}
        public static int RequiredSurvivors(string id,string definition)
        {Known(id);return id==British?(definition=="longbow"?2:definition=="pike"?1:0):id==Dutch?(definition=="carabineer"?2:0):id==French?(definition=="cuirassier"?1:0):definition=="janissary"||definition=="abus"?1:0;}
        static string Title(string id)=>id==British?"英国战术：长弓与前排":id==Dutch?"荷兰战术：反骑与机动":id==French?"法国战术：范围冲击":"奥斯曼战术：禁卫与Abus";
        static GuidedPlayStage Stage(string id,string title,string text,float required=1)=>new GuidedPlayStage{Id=id,Title=title,Instruction=text,Goal=GuidedPlayGoal.ScenarioObjective,Item=id,Required=required};
        public static IEnumerable<GuidedPlayDefinition> Definitions()
        {
            foreach(string id in new[]{British,Dutch,French,Ottoman})
            {
                string trait=id==British?"长枪前排受击；长弓Hold或指定可见敌，18米外实际命中。":id==Dutch?"Ruyter远射骑兵后，单选该骑手移动8米并抵达。":id==French?"胸甲骑兵贴近成团步兵，让一次近战同时伤到多个敌人。":"切禁卫近战命中骑兵；Abus后排远射重步兵，分别命中。";
                string force=id==British?"时代II，4长弓与3长枪":id==Dutch?"时代III，6名Ruyter":id==French?"时代III，3名胸甲骑兵":"时代II，3禁卫与3Abus";
                string waves=id==Dutch?"两波各4名骠骑兵":id==French?"两波各4名火枪兵":"首波4火枪兵，次波2骠骑兵";
                yield return new GuidedPlayDefinition{Id=id,Title=Title(id),Description=force+"；"+waves+"，练习编队和实际交战。",Kind=GuidedPlayKind.TacticalEncounter084,
                    PlayerCivilization=Civilization(id),MapId=MapTopology.Scenario,Seed=Seed(id),StartingAge=StartingAge(id),ScenarioGrant=new Cost(),TimeLimit=LimitSeconds,
                    NextId="",UnlockAfterId="",AuthoredScenarioId=id,
                    StartingSetupDescription=(id==Dutch?"受控入门遭遇，15分钟；":"受控遭遇，15分钟；")+SourceMapName(id)+"既有地形。"+force+"，无研究升级；双方资源0，无预放工人或经济AI，己方无军援。"+waves+"；首波10秒进军，次波40秒抵达。用普通命令完成特色、清除"+EnemyCount(id)+"敌；保留"+SurvivalText(id)+"。失城、兵力不足、无特色全清或超时失败。奥斯曼自动招工照常。",
                    Stages=Array.AsReadOnly(new[]{Stage("deploy","展开部队","指定部队移至目标区；前排在前，远射在后，用Hold守位。",id==British||id==Ottoman?4:2),
                        Stage("trait","运用兵种特色",trait),Stage("clear","清除两波敌军","用普通攻击清除两波共"+EnemyCount(id)+"名敌兵；保留"+SurvivalText(id)+"。")})};
            }
        }
        public static ScenarioDocument Build(RuleCatalog rules,string id)
        {
            Known(id);if(rules==null)throw new ArgumentNullException(nameof(rules));
            // The existing editor exporter freezes the original map's actual river,
            // bridge and terrain geometry. No new navigation or unit rules are used.
            var d=ScenarioEditorSession.FromMap(rules,SourceMapId(id),Seed(id),2);
            d.id=id;d.name=Title(id);d.description="受控战术遭遇；既有地形来源："+MapTopology.Name(SourceMapId(id))+"。首波10秒进军，次波40秒抵达；仅普通命令与实际战斗结果计入。";
            d.seed=Seed(id);d.victoryPlayer=0;d.enableElimination=false;
            d.resources=Array.Empty<ScenarioPlacement>();d.objects=Array.Empty<ScenarioPlacement>();d.groups=Array.Empty<ScenarioGroup>();
            d.tradeRoute=Array.Empty<Vector2>();d.tradeSockets=Array.Empty<ScenarioTradeSocket>();d.treasures=Array.Empty<ScenarioTreasure>();d.neutralBindings=Array.Empty<ScenarioNeutralBinding>();
            d.regions=Array.Empty<ScenarioRegion>();d.objectives=Array.Empty<ScenarioObjective>();d.triggers=Array.Empty<ScenarioTrigger>();
            for(int p=0;p<2;p++){d.starts[p].civilization=p==0?Civilization(id):"france";d.starts[p].age=StartingAge(id);d.starts[p].initialResources=new Cost();d.starts[p].workers=Array.Empty<Vector2>();}
            var a=new Author(rules,d);var center=DeploymentPoint(id);var army=new List<string>();var front=new List<string>();var ranged=new List<string>();
            if(id==British)
            {
                a.Line("longbow",0,"longbow",4,center+new Vector2(0,-12),army,ranged);
                a.Line("pike",0,"pike",3,center+new Vector2(0,-8),army,front);
            }
            else if(id==Dutch)a.Line("carabineer",0,"ruyter",ArmyCount(id),center+new Vector2(0,-12),army,ranged);
            else if(id==French)a.Line("cuirassier",0,"cuirassier",3,center+new Vector2(0,-12),army,front);
            else
            {
                a.Line("abus",0,"abus",3,center+new Vector2(0,-12),army,ranged);
                a.Line("janissary",0,"janissary",3,center+new Vector2(0,-8),army,front);
            }
            a.Group(ArmyGroup,army);if(front.Count>0)a.Group(FrontGroup,front);if(ranged.Count>0)a.Group(RangedGroup,ranged);
            var enemies=new List<string>();a.Pack(FirstEnemy(id),1,"enemy-one",4,center+new Vector2(0,22),enemies);a.Group(Wave1Group,enemies);
            d.regions=new[]{new ScenarioRegion{id=DeploymentRegion,center=center,size=new Vector2(18,16)}};
            d.objectives=new[]{
                new ScenarioObjective{id=DeploymentGoal,title="展开区",description="指定军队实际进入展开区域；战术课程另核对普通命令。",kind="reach_region",objectId=ArmyGroup,regionId=DeploymentRegion,player=0,amount=1,required=false},
                new ScenarioObjective{id=Wave1Goal,title="清除首波",description="首波绑定敌兵全部在真实战斗中损失。",kind="destroy_object",objectId=Wave1Group,player=0},
                new ScenarioObjective{id=Wave2Goal,title="清除次波",description="40秒抵达的次波绑定敌兵全部在真实战斗中损失。",kind="destroy_object",objectId=Wave2Group,player=0}};
            // Attack-move has a public terrain destination, so no hidden target ID
            // is selected for an AI attack. Ordinary visibility/acquisition applies.
            d.triggers=new[]{
                new ScenarioTrigger{id=Wave1Trigger,conditions=new[]{new ScenarioCondition{kind="time",time=Wave1Seconds}},actions=new[]{AttackMove(Wave1Group,center)}},
                new ScenarioTrigger{id=Wave2Trigger,conditions=new[]{new ScenarioCondition{kind="time",time=Wave2Seconds}},actions=new[]{
                    new ScenarioAction{kind="spawn",player=1,definition=SecondEnemy(id),count=Wave2Count(id),point=center+new Vector2(0,30),objectId=Wave2Group},AttackMove(Wave2Group,center)}}};
            d.objects=a.Objects.ToArray();d.groups=a.Groups.ToArray();
            string reason=ScenarioCompiler.Validate(d,rules,2);if(reason!="")throw new InvalidOperationException(id+": "+reason);
            return ScenarioCodec.Clone(d);
        }
        static ScenarioAction AttackMove(string group,Vector2 point)=>new ScenarioAction{kind="order_attack_move",player=1,objectId=group,point=point};
        sealed class Author
        {
            public readonly List<ScenarioPlacement> Objects=new List<ScenarioPlacement>();public readonly List<ScenarioGroup> Groups=new List<ScenarioGroup>();
            readonly World world;readonly string id;
            public Author(RuleCatalog rules,ScenarioDocument d){id=d.id;world=new World(rules,d,new[]{d.starts[0].civilization,"france"},new[]{0,1},5);}
            Vector2 Place(string definition,int owner,string name,Vector2 near)
            {
                // Resolve only finite initial placement. This does not move or damage
                // a running encounter, and the exact chosen point is frozen in data.
                for(int ring=0;ring<=10;ring++)for(int y=-ring;y<=ring;y++)for(int x=-ring;x<=ring;x++)
                {
                    if(Mathf.Max(Mathf.Abs(x),Mathf.Abs(y))!=ring)continue;var p=near+new Vector2(x,y)*.5f;
                    if(p.x<5||p.y<5||p.x>123||p.y>123||world.IsWater(p)||world.IsBridgeApproach(p)||world.ScenarioPlacementReason(definition,p)!="")continue;
                    world.Spawn(definition,owner,p);Objects.Add(new ScenarioPlacement{id=name,definition=definition,owner=owner,position=p});return p;
                }
                throw new InvalidOperationException(id+" cannot place "+name+" near "+near);
            }
            public void Line(string definition,int owner,string prefix,int count,Vector2 center,List<string> all,List<string> role)
            {for(int i=0;i<count;i++){string name=prefix+"-"+i;Place(definition,owner,name,center+new Vector2((i-(count-1)*.5f)*1.6f,0));all.Add(name);role.Add(name);}}
            public void Pack(string definition,int owner,string prefix,int count,Vector2 center,List<string> all)
            {for(int i=0;i<count;i++){string name=prefix+"-"+i;Place(definition,owner,name,center+new Vector2((i%2-.5f)*1.6f,(i/2)*1.6f));all.Add(name);}}
            public void Group(string name,List<string> members)=>Groups.Add(new ScenarioGroup{id=name,objectIds=members.ToArray()});
        }
    }
}
