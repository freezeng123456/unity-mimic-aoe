using System;

namespace Frontier
{
    // A commitment to fund one age. Reads only own economy and observed threats.
    public sealed class AiAgePlan
    {
        private readonly World world;
        private readonly int player;
        private float retryAt,resumeAt,progressAt,bestCoverage;
        private bool saving;
        public int TargetAge { get; private set; }
        public bool Saving=>TargetAge>0&&saving;
        public Cost Reserve=>Saving?world.Rules.Age(TargetAge).cost:new Cost();
        public AiAgePlan(World world,int player){this.world=world;this.player=player;}
        public AiAgePlanSnapshot CaptureState()=>new AiAgePlanSnapshot{targetAge=TargetAge,saving=saving,retryAt=retryAt,resumeAt=resumeAt,progressAt=progressAt,bestCoverage=bestCoverage};
        public void RestoreState(AiAgePlanSnapshot state)
        {
            AiSaveValidation.Require(state!=null,"missing age plan");
            AiSaveValidation.Require(state.targetAge==0||state.targetAge>=2&&state.targetAge<=world.MaxAge,"age target");
            AiSaveValidation.Require(!state.saving||state.targetAge>0,"age savings without a target");
            AiSaveValidation.Require(AiSaveValidation.Clock(state.retryAt)&&AiSaveValidation.Clock(state.resumeAt)&&AiSaveValidation.Clock(state.progressAt)&&
                SnapshotCodec.Finite(state.bestCoverage)&&state.bestCoverage>=0&&state.bestCoverage<=3.001f,"age plan clocks or coverage");
            TargetAge=state.targetAge;saving=state.saving;retryAt=state.retryAt;resumeAt=state.resumeAt;progressAt=state.progressAt;bestCoverage=state.bestCoverage;
        }
        public void Tick(EntityState town,int workers,int soldiers,int visibleRaiders,bool advancing,bool allowAdvance=true)
        {
            if(world.Paused||world.IsMatchEnded||!world.IsPlayerActive(player))return;
            var p=world.Players[player];
            if(!allowAdvance||p.politicalPhase!=PoliticalPhase.Empire||world.WorkerSourcesClosed(player))
            {if(TargetAge>0)End(town,"Political commitment replaces age savings",0);return;}
            int required=Math.Min(world.WorkerLimit(player),p.age==1?12:p.age==2?20:p.age==3?30:38);
            if(TargetAge>0)
            {
                if(advancing||p.age>=TargetAge){End(town,"Age study queued or completed",0);return;}
                if(town==null||!town.Alive||workers<Math.Max(6,required*.6f))
                {End(town,"Rebuild town or workforce before advancing",30);return;}
            }
            if(TargetAge==0)
            {
                if(world.Time<retryAt||advancing||p.age>=world.MaxAge||town==null||!town.Alive||!town.Complete||workers<required||p.age>1&&soldiers<8)return;
                TargetAge=p.age+1;saving=true;progressAt=world.Time;resumeAt=world.Time;bestCoverage=Coverage();
                Log(town,"Saving for age "+TargetAge);
            }
            if(visibleRaiders>0&&soldiers<4)
            {
                if(saving)Log(town,"Pause age "+TargetAge+" savings to defend");
                saving=false;resumeAt=world.Time+20;progressAt=world.Time;return;
            }
            if(!saving)
            {
                if(world.Time<resumeAt)return;
                saving=true;progressAt=world.Time;bestCoverage=Coverage();Log(town,"Resume saving for age "+TargetAge);
            }
            float coverage=Coverage();
            if(coverage>bestCoverage+.02f){bestCoverage=coverage;progressAt=world.Time;}
            if(world.Time-progressAt>150)End(town,"Age savings stalled; rebuild economy",30);
        }
        public string ChooseBranch(bool underAttack)
        {
            var choices=world.AgeChoices(player,world.Players[player].age+1);
            string selected=underAttack?"military":"economic";float best=float.MinValue;
            string civ=world.Players[player].civilization;
            string preferred=civ=="coast"?(underAttack?"militia":"homestead"):
                civ=="river"?(underAttack?"contracts":"banking"):
                civ=="france"?(underAttack?"cavalry":"homesteads"):
                civ=="ottoman"?(underAttack?"janissaries":"administration"):
                civ=="wayfarer"?(underAttack?"armed":world.Players[player].age==1?"safe":"distant"):
                civ=="highland"?(underAttack?"fortress":world.Players[player].age==1?"civil":"ordnance"):"";
            foreach(var choice in choices)
            {
                float score=0;var unit=world.Rules.Entity(choice.unit);
                if(unit!=null)score+=underAttack?(unit.kind==EntityKind.Unit&&!unit.civilian&&(unit.tags&Tags.Villager)==0?40:0):((unit.tags&Tags.Villager)!=0?30:5);
                float crates=choice.crates.food+choice.crates.wood+choice.crates.coin;
                score+=crates*(underAttack?.003f:.015f);
                // Route IDs are authored civilization choices. Prefer the civ's
                // economic path in peace and military path in observed danger.
                string id=choice.id??"";
                if(underAttack&&(id.Contains("military")||id.Contains("defense")||id.Contains("armed")||id.Contains("arsenal")))score+=30;
                if(!underAttack&&(id.Contains("economic")||id.Contains("trade")||id.Contains("civil")||id.Contains("bank")||id.Contains("manor")))score+=30;
                if(!string.IsNullOrEmpty(choice.tech))score+=8;
                if(preferred!=""&&id.Contains("_"+preferred+"_"))score+=100;
                if(score>best){best=score;selected=choice.id;}
            }
            return selected;
        }
        private float Coverage()
        {
            var cost=world.Rules.Age(TargetAge).cost;var r=world.Players[player].resources;
            float fraction(float amount,float required)=>required>0?Math.Min(1,amount/required):1;
            return fraction(r.food,cost.food)+fraction(r.wood,cost.wood)+fraction(r.coin,cost.coin);
        }
        private void End(EntityState town,string reason,float retry)
        {Log(town,reason);TargetAge=0;saving=false;retryAt=world.Time+retry;}
        private void Log(EntityState town,string message)=>world.Emit("AIStrategy",player,town?.id??0,message);
    }
}
