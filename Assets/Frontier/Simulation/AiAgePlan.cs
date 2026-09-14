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
        public void Tick(EntityState town,int workers,int soldiers,int visibleRaiders,bool advancing)
        {
            if(world.Paused||world.Winner>=0)return;
            var p=world.Players[player];int required=p.age==1?12:p.age==2?20:p.age==3?30:38;
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
