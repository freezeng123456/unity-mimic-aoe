using System;
using System.Collections.Generic;

namespace Frontier
{
    public enum GuidedPlayKind { Lesson, Campaign }
    public enum GuidedPlayStatus { Running, Completed, Failed }
    public enum GuidedPlayGoal { Selection, Move, GatherWood, House, Age, Barracks, Train, SendShipment, ShipmentArrival, DestroyFirst, DestroySecond, Contact, TradingPost, Alliance, Recruit, Embark, Land, HoldLanding, Defend, ScenarioObjective }
    public sealed class GuidedPlayStage
    {
        public string Id {get; internal set;}
        public string Title {get; internal set;}
        public string Instruction {get; internal set;}
        public GuidedPlayGoal Goal {get; internal set;}
        public float Required {get; internal set;}
    }
    public sealed class GuidedPlayDefinition
    {
        public string Id {get; internal set;}
        public string Title {get; internal set;}
        public string Description {get; internal set;}
        public GuidedPlayKind Kind {get; internal set;}
        public string MapId {get; internal set;}
        public int Seed {get; internal set;}
        public int StartingAge {get; internal set;}
        public Cost ScenarioGrant {get; internal set;}
        public float TimeLimit {get; internal set;}
        public string NextId {get; internal set;}
        public string UnlockAfterId {get; internal set;}
        public string StartingSetupDescription {get; internal set;}
        public string AuthoredScenarioId {get; internal set;}="";
        public IReadOnlyList<GuidedPlayStage> Stages {get; internal set;}
    }
    public sealed class GuidedPlayChecklist
    {
        public string Id {get; internal set;}
        public string Text {get; internal set;}
        public bool Complete {get; internal set;}
        public bool Current {get; internal set;}
        public float Progress {get; internal set;}
        public float Required {get; internal set;}
    }
    public static class GuidedPlayCatalog
    {
        public const int Revision=1;
        static GuidedPlayStage S(string id,string title,string instruction,GuidedPlayGoal goal,float required=1)
            =>new GuidedPlayStage{Id=id,Title=title,Instruction=instruction,Goal=goal,Required=required};
        static GuidedPlayDefinition D(string id,string title,string description,GuidedPlayKind kind,string map,int seed,int age,Cost grant,string setup,params GuidedPlayStage[] stages)
            =>new GuidedPlayDefinition{Id=id,Title=title,Description=description,Kind=kind,MapId=map,Seed=seed,StartingAge=age,ScenarioGrant=grant,StartingSetupDescription=setup,Stages=Array.AsReadOnly(stages),TimeLimit=kind==GuidedPlayKind.Lesson?1200:1800,NextId="",UnlockAfterId=""};
        static readonly GuidedPlayDefinition[] definitions=Create();
        public static IReadOnlyList<GuidedPlayDefinition> Definitions {get;}=Array.AsReadOnly(definitions);
        public static GuidedPlayDefinition Find(string id)=>Array.Find(definitions,d=>d.Id==id);
        static GuidedPlayDefinition[] Create()
        {
            var a=new[]{
                D("lesson_selection","1. 选取与移动","选中真实探路者，发出移动命令并抵达目标。",GuidedPlayKind.Lesson,MapTopology.Frontier,900101,1,new Cost(),"标准时代 I 开局资源；双方 AI 关闭，无额外物资。",
                    S("select","选中探路者","单击己方探路者；只有真实选择会计入目标。",GuidedPlayGoal.Selection),S("move","抵达标记","选中探路者，右键目标标记，等待其实际抵达。",GuidedPlayGoal.Move)),
                D("lesson_economy","2. 采集与建造","采集木材，再付费完成住宅。",GuidedPlayKind.Lesson,MapTopology.Frontier,900102,1,new Cost(),"标准时代 I 开局资源；双方 AI 关闭；需实际采集 75 木材并付费建造。",
                    S("wood","采集木材","选择村民，右键树木；等待实际采集 75 木材。",GuidedPlayGoal.GatherWood,75),S("house","完成住宅","选择村民，建造菜单中选住宅，放在空地并等待建造完成。",GuidedPlayGoal.House)),
                D("lesson_age_army","3. 升时代与募兵","选择时代路线、建造兵营并募兵。",GuidedPlayKind.Lesson,MapTopology.Frontier,900103,1,new Cost(1200,600,300),"教学补给：额外 1200 食物、600 木材、300 钱币；双方 AI 关闭，时代 I 开始；升级和募兵仍实际付费。",
                    S("age","升至时代 II","选择城镇中心，选择一条时代路线并等候升级完成。",GuidedPlayGoal.Age,2),S("barracks","完成兵营","选择村民，付费建造兵营并等候完成。",GuidedPlayGoal.Barracks),S("train","募出两名火枪兵","选择兵营，招募至少两名火枪兵；排队并不等于完成。",GuidedPlayGoal.Train,2)),
                D("lesson_shipments","4. 家乡船运","使用一次船运机会，等待补给实际到达。",GuidedPlayKind.Lesson,MapTopology.Frontier,900104,1,new Cost(),"时代 I；教学额外授予 150 经验获得一次船运机会；卡牌来自合法经济牌组，双方 AI 关闭。",
                    S("ship","请求船运","打开家乡卡牌，发送任意当前时代的可用卡牌。",GuidedPlayGoal.SendShipment),S("arrival","等候到港","继续模拟，等待请求的船运实际抵达；仅点击发送不算到达。",GuidedPlayGoal.ShipmentArrival)),
                D("lesson_targeting","5. 指定目标攻击","用长枪兵集中攻击骑兵，再摧毁指定建筑。",GuidedPlayKind.Lesson,MapTopology.Frontier,900105,2,new Cost(),"受控时代 II 演习：己方额外 4 长枪兵和 1 探路者，敌方 1 骑兵和 1 仓房；初始部队原地守卫，双方 AI 关闭。",
                    S("cavalry","集中攻击骑兵","选择长枪兵，右键指定敌方骑兵；实际命令和击杀都必须完成。",GuidedPlayGoal.DestroyFirst),S("depot","摧毁指定仓房","选择部队，右键敌方仓房；坚持攻击直到建筑摧毁。",GuidedPlayGoal.DestroySecond)),
                D("lesson_allies","6. 中立盟友","接触中立聚落，付费建造贸易站、结盟并募出盟军。",GuidedPlayKind.Lesson,MapTopology.GeneratedDelta,900106,3,new Cost(900,900,900),"受控时代 III：额外 900/900/900 资源；聚落旁初始放置 2 村民和 1 探路者；双方 AI 关闭；贸易站、条约和盟军均付费。",
                    S("contact","接触聚落","选择提示的探路者，右键聚落旁标记并抵达。",GuidedPlayGoal.Contact),S("post","完成贸易站","选择村民，在聚落贸易插槽付费建贸易站。",GuidedPlayGoal.TradingPost),S("alliance","完成付费条约","保持附近陆军接触，在聚落服务中付费结盟并等候条约完成。",GuidedPlayGoal.Alliance),S("recruit","招募盟军","在聚落服务中付费招募一名盟军，等待其实际出现。",GuidedPlayGoal.Recruit)),
                D("lesson_transport","7. 运输与登陆","士兵实际登船、航行并在另一座岛上登陆。",GuidedPlayKind.Lesson,MapTopology.GeneratedStrait,900107,2,new Cost(),"受控时代 II：额外 1 运输舰、2 火枪兵、1 探路者；双方 AI 关闭；船与士兵均在合法初始位置。",
                    S("embark","实际登船","选两名火枪兵，右键己方运输舰；等到两名乘员实际登船。",GuidedPlayGoal.Embark,2),S("land","另一座岛登陆","选运输舰，使用卸载并指定目标岛岸；等两名士兵实际卸载。",GuidedPlayGoal.Land,2),S("hold","守住登陆点","选登陆士兵，发出原地守卫命令。",GuidedPlayGoal.HoldLanding,2)),
                D("campaign_supply","战役 I：河谷补给线","原创边境故事：修建前哨补给营，募兵抵御来袭骑兵。",GuidedPlayKind.Campaign,MapTopology.Frontier,901201,2,new Cost(700,400,300),"原创场景：时代 II，额外 700/400/300 资源和 2 长枪兵；敌方 2 骑兵待命。营地募兵后，敌军通过真实攻击移动命令进攻；无敌方经济 AI。",
                    S("wood","补充木材","实际采集 100 木材，为补给营备料。",GuidedPlayGoal.GatherWood,100),S("house","建补给住宅","实际付费建完一座住宅。",GuidedPlayGoal.House),S("barracks","建前哨兵营","实际付费建完一座兵营。",GuidedPlayGoal.Barracks),S("army","训练守军","实际付费训练出 4 火枪兵。",GuidedPlayGoal.Train,4),S("defend","击退骑兵突袭","指定攻击来袭骑兵或向来袭区攻击移动；击退两名骑兵并保住城镇中心。",GuidedPlayGoal.Defend,2)),
                D("campaign_bridge","战役 II：桥头通路","补给线恢复后，部队越过曲河桥梁，拆除封锁仓房。",GuidedPlayKind.Campaign,MapTopology.GeneratedRiver,901202,3,new Cost(),"原创受控时代 III 场景：桥西初始 4 火枪兵、2 长枪兵、1 探路者和补给住宅；桥东敌方仓房与 2 守卫长枪兵；双方 AI 关闭，使用真实桥梁寻路与战斗。",
                    S("cross","越过曲河桥梁","选择前线部队，向桥东标记发出移动或攻击移动，至少两名士兵实际越桥抵达。",GuidedPlayGoal.Move,2),S("blockade","拆除封锁仓房","向指定敌方仓房发出攻击并摧毁。",GuidedPlayGoal.DestroyFirst)),
                D("campaign_estuary","战役 III：河口登陆","桥头告捷后，运兵舰穿越海峡，在中洲建立登陆据点并清除敌方仓房。",GuidedPlayKind.Campaign,MapTopology.GeneratedStrait,901203,3,new Cost(),"原创受控时代 III：合法初始 1 运输舰、4 火枪兵、2 长枪兵、1 探路者和补给住宅；中洲敌方仓房与守卫；双方 AI 关闭，登陆和战斗使用实际命令。",
                    S("embark","装载登陆队","选择提示的 6 名士兵，右键己方运输舰；等至少 4 名乘员实际登船。",GuidedPlayGoal.Embark,4),S("landing","海峡岛岸登陆","选运输舰，在标记岛岸卸载，至少 4 名士兵实际登陆。",GuidedPlayGoal.Land,4),S("depot","夺取中洲据点","登陆部队指定攻击并摧毁敌方仓房。",GuidedPlayGoal.DestroyFirst))
            };
            for(int i=0;i<6;i++)a[i].NextId=a[i+1].Id;
            a[6].NextId=a[7].Id;a[7].NextId=a[8].Id;a[8].NextId=a[9].Id;
            a[8].UnlockAfterId=a[7].Id;a[9].UnlockAfterId=a[8].Id;
            // The original three-chapter arc and its account revision remain
            // unchanged. A second expedition has its own first unlocked chapter.
            var additions=new[]{
                D(RiverContent070.Scout,"远征 I：双桥侦察","侦察远岸、选择渡桥，并清除商路封锁；侧线瞭望提供一次补给。",GuidedPlayKind.Campaign,MapTopology.Scenario,902004,2,new Cost(),
                    "原创时代 II 任务：200 食物、180 木材、100 钱币；6 工人、探路者、4 火枪兵、2 长枪兵及补给住宅。无敌方经济 AI；营地或前线部队损失、目标超时会失败。",
                    S("survey","侦察远岸","移动探路者越桥进入远岸标记；可先去西侧瞭望点获得一次补给。",GuidedPlayGoal.ScenarioObjective),
                    S("blockade","清除商路封锁","侦察目标后，指挥部队摧毁敌方补给市场，保护后方营地。",GuidedPlayGoal.ScenarioObjective)),
                D(RiverContent070.Convoy,"远征 II：渡湾护送","运输指定登陆队穿过分流河湾，护航或绕行后建立岛上据点。",GuidedPlayKind.Campaign,MapTopology.Scenario,902005,2,new Cost(),
                    "原创时代 II 任务：160 食物、180 木材、120 钱币；6 工人、6 名指定登陆兵、运输舰、炮艇与己方港口。岛与本岸没有陆路；最低4名指定登陆兵及运输舰必须生存。无敌方经济 AI。",
                    S("embark","装载指定登陆队","选择登陆兵，右键指定运输舰；至少4人实际登船。",GuidedPlayGoal.ScenarioObjective,4),
                    S("sail","护送运输舰","保护载有至少4名指定登陆兵的运输舰进入西侧岛岸水域；可用炮艇压制来袭船或经东侧水道绕行。",GuidedPlayGoal.ScenarioObjective,4),
                    S("land","建立岛上登陆点","在岛岸卸载，再将至少4名指定登陆兵移入岛中标记；船和工人不计入。",GuidedPlayGoal.ScenarioObjective,4),
                    S("blockade","清除岛上封锁","指挥登陆队摧毁已侦察的敌方补给市场。",GuidedPlayGoal.ScenarioObjective)),
                D(RiverContent070.Retreat,"远征 III：守港回撤","陆海反击下守住港区，再选择过桥或船运撤回后方。",GuidedPlayKind.Campaign,MapTopology.Scenario,902006,3,new Cost(),
                    "原创时代 III 任务：160 食物、180 木材、120 钱币；6名指定后卫、运输舰、炮艇及前方港口；敌军分三次增援。至少4名后卫、前港和后方城镇须生存；修船和新生产仍实际付费。",
                    S("hold","守住港区","至少4名后卫在标记内守住40秒；敌人进入时进度清零。保护码头，应对陆海增援。",GuidedPlayGoal.ScenarioObjective,40),
                    S("withdraw","后卫回撤","将至少4名指定幸存后卫带回后方标记；可走南桥或装载、航行、卸载。",GuidedPlayGoal.ScenarioObjective,4))};
            for(int i=0;i<additions.Length;i++)
            {
                additions[i].AuthoredScenarioId=additions[i].Id;additions[i].TimeLimit=1200;
                if(i>0)additions[i].UnlockAfterId=additions[i-1].Id;
                if(i+1<additions.Length)additions[i].NextId=additions[i+1].Id;
            }
            var all=new List<GuidedPlayDefinition>(a);all.AddRange(additions);return all.ToArray();
        }
    }
}
