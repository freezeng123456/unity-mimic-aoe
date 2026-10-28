using System;
using System.Collections.Generic;

namespace Frontier
{
    // Four new finite exercises on the ordinary opening. They add no rules,
    // resources, units, technologies, enemies or scripted economic commands.
    public static class ClassicCivilizationRoutes084
    {
        public const string British="civilization_british084",Dutch="civilization_dutch084",French="civilization_french084",Ottoman="civilization_ottoman084";
        public static bool Contains(string id)=>id==British||id==Dutch||id==French||id==Ottoman;
        static GuidedPlayStage S(string id,string title,string instruction,GuidedPlayGoal goal,string item="",float required=1)
            =>new GuidedPlayStage{Id=id,Title=title,Instruction=instruction,Goal=goal,Item=item,Required=required};
        static GuidedPlayStage Age(int age)=>S("age"+age,"付费升至时代 "+age,"采食物、钱币；选城镇中心的本国时代路线，付费等升级完成。",GuidedPlayGoal.PaidAge084,"",age);
        static GuidedPlayStage Build(string id,string title,string instruction)=>S("build_"+id,title,instruction,GuidedPlayGoal.PaidBuilding084,id);
        static GuidedPlayStage Train(string id,int n,string title,string instruction)=>S("train_"+id,title,instruction,GuidedPlayGoal.PaidTrain084,id,n);
        static GuidedPlayStage Research(string id,string title,string instruction)=>S("research_"+id,title,instruction,GuidedPlayGoal.PaidResearch084,id);
        static GuidedPlayStage Drill(string item,string instruction)=>S("field_use","侦察与实战",instruction,GuidedPlayGoal.PaidDamage084,item,30);
        static GuidedPlayDefinition D(string id,string civ,string title,string description,int seed,params GuidedPlayStage[] stages)
            =>new GuidedPlayDefinition{Id=id,PlayerCivilization=civ,Title=title,Description=description,Kind=GuidedPlayKind.Lesson,MapId=MapTopology.Winding,Seed=seed,StartingAge=1,ScenarioGrant=new Cost(),TimeLimit=3600,NextId="",UnlockAfterId="",AuthoredScenarioId="",Stages=Array.AsReadOnly(stages),
                StartingSetupDescription="本项目新增的曲河谷60分钟发展练习：本国正常时代 I 资源、6名工人、探路者与城镇中心；双方无经济 AI，无额外教学补给、预放部队或预设科技；正常时代奖励按原规则。按普通操作采集、扩建住房、升时代与付费生产。末段侦察原有敌方探路者，再用指定部队攻击；城镇失守、末段所需部队全损或超时会失败。"};
        internal static IEnumerable<GuidedPlayDefinition> Definitions()
        {
            yield return D(British,"coast","英国：庄园与长弓","用庄园扩工，再取舍食物与木材，发展长弓与老兵升级。",904841,
                Build("house","付费建成庄园","选工人花135木材建住宅Manor；等完工，它会自动招来1名村民。"),
                S("manor_worker","等庄园招来1名村民","留人口和出口，等庄园招来村民；开局工人、城镇训练不计入。",GuidedPlayGoal.CivilizationEconomy084,"manor_worker"),Age(2),
                Build("barracks","付费建成兵营","选工人付费建兵营；长弓用食物、木材，留木材扩住房，新村民继续采集。"),
                Train("longbow",4,"训练4名长弓","兵营付费募4名Longbowman；等出生，站前排后持续远射。"),Age(3),
                Research("veteran_longbow","研究老兵长弓","兵营付费研究Veteran Longbowman，等完成；全体获提升。"),
                Drill("longbow","探路者找到敌探路者；选付费长弓右键攻击，打30伤害；避敌城镇。"));
            yield return D(Dutch,"river","荷兰：银行与轻骑兵","用钱币训练村民，投入银行，再供养低人口的 Ruyter。",904842,
                Train("villager",1,"用钱币训练1名村民","选城镇中心，用钱币训练1名村民，等出生；保留升时代储备。"),Age(2),
                Build("bank","付费建成银行","采350食物、350木材；选工人付费建Bank，等完工。"),
                S("bank_income","银行赚取50钱币","银行不占采集工人，完工后自动产币；等本次付费银行赚取50钱币。",GuidedPlayGoal.CivilizationEconomy084,"bank_income",50),
                Build("stable","付费建成马厩","选工人付费建马厩；继续采食物、钱币，留人口训练骑兵。"),Age(3),
                Train("carabineer",3,"训练3名Ruyter","马厩付费募3名Ruyter，等出生；每名1人口，远程反骑，忌近身。"),
                Research("veteran_carabineer","研究老兵Ruyter","选马厩付费研究Veteran Ruyter，等实际完成。"),
                Drill("carabineer","探路者找到敌探路者；付费Ruyter右键攻击，打30伤害；避敌城镇。"));
            yield return D(French,"france","法国：猎民与胸甲骑兵","让高效率猎民补足昂贵开支，再生产与升级胸甲骑兵。",904843,
                Train("coureur",1,"训练1名猎民","城镇花120食物募1名Coureur；等出生，开局猎民不计。"),
                S("coureur_income","新猎民采集120资源","新募猎民右键采食物、树或矿，累计120资源；其效率、造价均更高。",GuidedPlayGoal.CivilizationEconomy084,"coureur_income",120),Age(2),
                Build("stable","付费建成马厩","工人付费建马厩；骑兵每名150食物、150钱币、3人口，先扩住房、采矿。"),Age(3),
                Train("cuirassier",2,"训练2名胸甲骑兵","马厩付费募2名Cuirassier；等出生，范围伤害，避开长枪。"),
                Research("veteran_cuirassier","研究老兵胸甲骑兵","马厩付费研究Veteran Cuirassier，等完成；全体获提升。"),
                Drill("cuirassier","探路者找到敌探路者；付费胸甲骑兵右键攻击，打30伤害；避敌城镇。"));
            yield return D(Ottoman,"ottoman","奥斯曼：自动招工与步炮协同","经营自动招工与清真寺升级，再用禁卫军掩护 Abus。",904844,
                S("automatic_worker","等自动招来1名村民","城镇队列留空、人口和出口畅通，等自动村民出生；无需点击训练。",GuidedPlayGoal.CivilizationEconomy084,"automatic_worker"),
                Build("mosque","付费建成清真寺","工人花100木材建Mosque；建筑本身不加快招工，需另付费研究。"),
                Research("mosque_millet","加快自动招工","清真寺花75木材研究Millet System；等完成，扩住房。"),Age(2),
                Build("barracks","付费建成兵营","选工人付费建兵营；禁卫军保护后排，近战反骑。"),
                Train("janissary",3,"训练3名禁卫军","兵营付费募3名Janissary；等出生，采食物、钱币供军。"),
                Build("foundry","付费建成铸炮厂","选工人付费建铸炮厂；本国时代II即可训练Abus。"),
                Train("abus",2,"训练2名Abus","铸炮厂付费募2名Abus，等出生；每名2人口，射程长，后排避城镇。"),
                Drill("janissary,abus","探路者找到敌探路者；付费禁卫军、Abus分别右键攻击，各打30伤害。"));
        }
    }
}
