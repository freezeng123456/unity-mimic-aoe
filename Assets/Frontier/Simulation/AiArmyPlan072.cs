using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public enum AiArmyPhase072 { Muster, March, Defend }
    [Serializable] public sealed class AiArmyPlan072Snapshot
    {
        public int schema=1,initialWaveSize,anchor,stageNavigationVersion=-1;
        public AiArmyPhase072 phase;
        public bool hasStage;
        public float reviewAt;
        public Vector2 stage,lastHome,destination;
        public int[] wave=Array.Empty<int>(),guards=Array.Empty<int>();
    }

    // Plans owned cohorts. Every movement, rally and shot still goes through
    // the ordinary command authority and the world's existing navigation.
    public sealed class AiArmyPlan072
    {
        readonly World world;readonly int player;readonly AiDifficultyProfile difficulty;
        readonly List<int> wave=new List<int>(),guards=new List<int>();
        AiArmyPhase072 phase;int initialWaveSize,anchor,stageNavigationVersion=-1;
        bool hasStage;float reviewAt;Vector2 stage,lastHome,destination;
        public AiArmyPlan072(World world,int player,AiDifficultyProfile difficulty)
        {this.world=world??throw new ArgumentNullException(nameof(world));this.player=player;this.difficulty=difficulty??throw new ArgumentNullException(nameof(difficulty));}
        public AiArmyPhase072 Phase=>phase;
        public Vector2 RallyPoint=>hasStage?stage:Home;
        public int[] WaveMembers=>wave.ToArray();
        public int[] GuardMembers=>guards.ToArray();
        public Vector2 Home
        {
            get
            {
                EntityState town=null,rescue=null;float nearest=float.MaxValue;
                var opening=world.OpeningTownPosition(player);
                foreach(var e in world.Entities)
                {
                    if(!e.Alive||e.owner!=player||e.garrisonedIn!=0||e.def.waterborne)continue;
                    if(e.def.id=="town"&&(town==null||e.Complete&&!town.Complete||e.Complete==town.Complete&&e.id<town.id))town=e;
                    if(!e.Worker&&e.def.id!="factory"&&e.def.id!="farm"&&e.def.id!="estate")continue;
                    bool unsafePoint=false;
                    foreach(var enemy in world.Entities)if(DangerAt(enemy,e.position)){unsafePoint=true;break;}
                    if(unsafePoint)continue;float distance=(e.position-opening).sqrMagnitude;
                    if(distance<nearest){nearest=distance;rescue=e;}
                }
                return town?.position??rescue?.position??opening;
            }
        }
        bool IsThreat(EntityState enemy)=>enemy.Alive&&world.AreEnemies(player,enemy.owner)&&enemy.Military&&
            (enemy.def.tags&Tags.Explorer)==0&&(enemy.def.damage>0||enemy.def.meleeDamage>0||enemy.def.siegeDamage>0)&&world.IsVisible(player,enemy);
        bool IsArmedBuilding(EntityState enemy)=>enemy.Alive&&enemy.Complete&&enemy.def.kind==EntityKind.Building&&
            world.AreEnemies(player,enemy.owner)&&(enemy.def.damage>0||enemy.def.siegeDamage>0)&&world.IsVisible(player,enemy);
        bool DangerAt(EntityState enemy,Vector2 point)
        {
            if(IsArmedBuilding(enemy))return world.BuildingFootprintDistance(enemy.def,enemy.position,point)<enemy.def.range+6;
            return IsThreat(enemy)&&(enemy.position-point).sqrMagnitude<Mathf.Pow(Mathf.Max(18,enemy.def.range+8),2);
        }
        public bool HasImmediateThreat(List<EntityState> own,List<EntityState> visibleEnemies)=>ChooseThreat(own,visibleEnemies)!=null;
        EntityState ChooseThreat(List<EntityState> own,List<EntityState> enemies)
        {
            EntityState chosen=null;float best=float.MaxValue;
            foreach(var enemy in enemies)
            {
                bool building=IsArmedBuilding(enemy);if(!building&&!IsThreat(enemy))continue;
                foreach(var asset in own)
                {
                    if(!asset.Alive||asset.owner!=player||asset.garrisonedIn!=0||asset.def.waterborne||
                        !asset.Worker&&asset.def.kind!=EntityKind.Building)continue;
                    float distance=(enemy.position-asset.position).sqrMagnitude;
                    float reach=asset.def.id=="town"?34:Mathf.Max(18,enemy.def.range+8);
                    if(building)
                    {
                        float edge=world.EdgeDistance(enemy,asset);
                        // Static weapons matter only when a real owned asset is
                        // in their firing envelope. A distant town is no raid.
                        if(edge<enemy.def.minRange||edge>enemy.def.range+.1f)continue;
                    }
                    else if(distance>=reach*reach)continue;
                    float score=distance+(asset.def.id=="town"?-300:asset.Worker?-100:0);
                    if(score<best||score==best&&(chosen==null||enemy.id<chosen.id)){best=score;chosen=enemy;}
                }
            }
            return chosen;
        }
        public AiArmyPlan072Snapshot CaptureState()=>new AiArmyPlan072Snapshot
        {phase=phase,initialWaveSize=initialWaveSize,anchor=anchor,hasStage=hasStage,stage=stage,lastHome=lastHome,
            destination=destination,stageNavigationVersion=stageNavigationVersion,reviewAt=reviewAt,wave=wave.ToArray(),guards=guards.ToArray()};
        public void RestoreState(AiArmyPlan072Snapshot state)
        {
            // Older saves have no cohort plan. They resume a deterministic fresh
            // muster rather than guessing expedition members from hidden targets.
            if(state==null)
            {
                phase=AiArmyPhase072.Muster;initialWaveSize=anchor=0;hasStage=false;reviewAt=0;
                stage=lastHome=destination=Vector2.zero;stageNavigationVersion=-1;wave.Clear();guards.Clear();return;
            }
            AiSaveValidation.Require(state.schema==1&&Enum.IsDefined(typeof(AiArmyPhase072),state.phase)&&
                state.initialWaveSize>=0&&state.initialWaveSize<=world.Rules.populationLimit&&state.anchor>=0&&state.stageNavigationVersion>=-1&&
                AiSaveValidation.Clock(state.reviewAt)&&state.reviewAt<=world.Time+difficulty.ArmySeconds+MatchSimulation.StepSeconds+.01f&&
                InMap(state.stage)&&InMap(state.lastHome)&&InMap(state.destination)&&
                state.wave!=null&&state.guards!=null&&state.wave.Length+state.guards.Length<=world.Rules.populationLimit,"army plan state");
            var seen=new HashSet<int>();
            foreach(var group in new[]{state.wave,state.guards})foreach(int id in group)
            {
                var e=world.Get(id);AiSaveValidation.Require(id>0&&seen.Add(id)&&e!=null&&e.owner==player&&e.Military&&
                    !e.def.waterborne&&(e.def.tags&Tags.Explorer)==0,"army cohort member");
            }
            AiSaveValidation.Require(state.phase!=AiArmyPhase072.March||state.initialWaveSize>=3,"march without a committed wave");
            AiSaveValidation.Require(state.phase!=AiArmyPhase072.Muster||state.wave.Length==0&&state.guards.Length==0&&state.initialWaveSize==0,"muster with committed members");
            var savedAnchor=world.Get(state.anchor);AiSaveValidation.Require(state.anchor==0||savedAnchor!=null&&savedAnchor.owner==player&&savedAnchor.def.id=="town","army anchor");
            phase=state.phase;initialWaveSize=state.initialWaveSize;anchor=state.anchor;hasStage=state.hasStage;stage=state.stage;
            lastHome=state.lastHome;destination=state.destination;stageNavigationVersion=state.stageNavigationVersion;reviewAt=state.reviewAt;
            wave.Clear();wave.AddRange(state.wave);guards.Clear();guards.AddRange(state.guards);
        }
        bool InMap(Vector2 p)=>SnapshotCodec.Finite(p)&&p.x>=0&&p.y>=0&&p.x<=world.Navigation.Width&&p.y<=world.Navigation.Height;
        bool Reachable(Vector2 from,Vector2 to,float radius)
        {
            if(!world.Navigation.IsWalkable(from,radius)||!world.Navigation.IsWalkable(to,radius))return false;
            if(world.Navigation.ClearLine(from,to,radius))return true;
            var path=world.Navigation.FindPath(from,to,radius);return path.Count>=2&&(path[path.Count-1]-to).sqrMagnitude<.0001f;
        }
        bool Safe(Vector2 point,List<EntityState> enemies)
        {
            foreach(var e in enemies)if(DangerAt(e,point))return false;
            return true;
        }
        bool SelectStage(List<EntityState> own,List<EntityState> soldiers,List<EntityState> enemies)
        {
            Vector2 home=Home;float radius=.4f;foreach(var e in soldiers)radius=Mathf.Max(radius,e.def.radius);
            var exit=world.Navigation.NearestWalkable(home,radius);
            int nextAnchor=0;foreach(var e in own)if(e.Alive&&e.def.id=="town"&&(e.position-home).sqrMagnitude<.01f){nextAnchor=e.id;break;}
            if(hasStage&&anchor==nextAnchor&&(home-lastHome).sqrMagnitude<1&&Safe(stage,enemies)&&
                world.Navigation.IsAreaFree(stage,7,7)&&Reachable(exit,stage,radius))
            {stageNavigationVersion=world.Navigation.Version;return true;}
            var desired=home+Vector2.ClampMagnitude(new Vector2(world.Navigation.Width*.5f,world.Navigation.Height*.5f)-home,12);
            for(int ring=0;ring<4;ring++)for(int n=0;n<(ring==0?1:8);n++)
            {
                float angle=n*Mathf.PI/4;var point=desired+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*ring*4;
                point=new Vector2(Mathf.Round(point.x),Mathf.Round(point.y));
                if(world.Fog.At(player,point)!=VisibilityState.Visible||!Safe(point,enemies)||!world.Navigation.IsAreaFree(point,7,7)||!Reachable(exit,point,radius))continue;
                stage=point;hasStage=true;lastHome=home;anchor=nextAnchor;stageNavigationVersion=world.Navigation.Version;return true;
            }
            hasStage=false;return false;
        }
        bool AtStage(EntityState e)
        {
            if(!hasStage||(e.position-stage).sqrMagnitude>10*10||!world.Navigation.IsWalkable(e.position,e.def.radius))return false;
            if(world.Navigation.ClearLine(e.position,stage,e.def.radius))return true;
            var path=world.Navigation.FindPath(e.position,stage,e.def.radius);
            if(path.Count<2||(path[path.Count-1]-stage).sqrMagnitude>.0001f)return false;
            float length=0;for(int i=1;i<path.Count;i++)length+=Vector2.Distance(path[i-1],path[i]);
            // A nearby point across a long wall or on the opposite river bank
            // is reachable eventually, but is not an assembled soldier yet.
            return length<=14;
        }
        void ChangePhase(AiArmyPhase072 next,string reason)
        {if(phase==next)return;phase=next;world.Emit("AIArmyPhase",player,0,reason,wave.Count,0,hasStage?stage:Home);}
        void RallyProducers(List<EntityState> own)
        {
            foreach(var e in own)if(e.Alive&&e.Complete&&(e.def.id=="barracks"||e.def.id=="stable"||e.def.id=="foundry"||e.def.id=="factory"&&e.factoryMode=="artillery")&&
                (e.rallyTarget!=0||(e.rally-stage).sqrMagnitude>.01f))Send(new GameCommand{kind=CommandKind.Rally,actors=new[]{e.id},point=stage});
        }
        void Dispatch(List<EntityState> group,OrderKind kind,Vector2 point,AiWaterEconomy water,bool holdAtStage=false)
        {
            var ids=new List<int>();var parked=new List<int>();
            foreach(var e in group)
            {
                if(holdAtStage&&AtStage(e))
                {if(!e.hold)parked.Add(e.id);continue;}
                Vector2 goal=water.LandArmyDestination(e,point);
                // Connected bridge cohorts share one public formation command.
                // Different islands retain the existing transport staging rule.
                if((goal-point).sqrMagnitude>1)
                {if(e.orders.Count==0||e.orders.Peek().kind!=kind||(e.orders.Peek().point-goal).sqrMagnitude>18*18)Send(new GameCommand{kind=CommandKind.Order,actors=new[]{e.id},order=kind,point=goal});continue;}
                if(e.orders.Count==0||e.orders.Peek().kind!=kind||(e.orders.Peek().point-point).sqrMagnitude>18*18)ids.Add(e.id);
            }
            if(ids.Count>0)Send(new GameCommand{kind=CommandKind.Order,actors=ids.ToArray(),order=kind,point=point});
            if(parked.Count>0)Send(new GameCommand{kind=CommandKind.Order,actors=parked.ToArray(),order=OrderKind.Hold,point=stage});
        }
        public void Review(List<EntityState> own,List<EntityState> landArmy,List<EntityState> visibleEnemies,
            Vector2 rememberedOrOpeningDestination,bool hasKnownTarget,AiSearchPlan search,AiWaterEconomy water,
            AiRevolution071 revolution,AiCivilizationEconomy civilization)
        {
            if(world.Paused||world.IsMatchEnded||!world.IsPlayerActive(player)||world.Time<reviewAt)return;
            reviewAt=world.Time+difficulty.ArmySeconds;
            var soldiers=new List<EntityState>();
            foreach(var e in landArmy)
            {
                if(!e.Alive||e.owner!=player||!e.Military||e.def.waterborne||e.garrisonedIn!=0||(e.def.tags&Tags.Explorer)!=0)continue;
                if(revolution.KeepSupplyGuard(e,visibleEnemies)||e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Embark)continue;
                if(e.hp<world.MaxHp(e)*.22f&&visibleEnemies.Count>landArmy.Count)
                {if(e.orders.Count==0||e.orders.Peek().kind!=OrderKind.Move)Send(new GameCommand{kind=CommandKind.Order,actors=new[]{e.id},order=OrderKind.Move,point=water.LandArmyDestination(e,Home)});continue;}
                if(civilization.Support(e,landArmy,visibleEnemies))continue;soldiers.Add(e);
            }
            soldiers.Sort((a,b)=>a.id.CompareTo(b.id));var available=new HashSet<int>();foreach(var e in soldiers)available.Add(e.id);
            wave.RemoveAll(id=>!available.Contains(id));guards.RemoveAll(id=>!available.Contains(id));
            search.ObserveStart(soldiers);
            // An observed weapon can invalidate yesterday's assembly point even
            // while the army must defend immediately. Correct production rally
            // before that dispatch, rather than sending new troops to the gun.
            bool stageReady=SelectStage(own,soldiers,visibleEnemies);
            if(stageReady)RallyProducers(own);
            var threat=ChooseThreat(own,visibleEnemies);
            if(threat!=null)
            {
                ChangePhase(AiArmyPhase072.Defend,"Visible military threatens owned economy");
                Dispatch(soldiers,OrderKind.AttackMove,threat.position,water);return;
            }
            if(phase==AiArmyPhase072.Defend){wave.Clear();guards.Clear();initialWaveSize=0;ChangePhase(AiArmyPhase072.Muster,"Visible raid cleared; assemble again");}
            if(phase==AiArmyPhase072.March&&wave.Count<Math.Max(3,initialWaveSize/2))
            {wave.Clear();guards.Clear();initialWaveSize=0;ChangePhase(AiArmyPhase072.Muster,"Combat losses require a new cohort");}
            if(!stageReady)return;
            int muster=world.Players[player].age<=2?6:world.Players[player].age==3?10:14;
            var staged=soldiers.FindAll(AtStage);
            if(phase==AiArmyPhase072.Muster&&staged.Count>=muster)
            {
                wave.Clear();guards.Clear();for(int i=0;i<muster;i++)wave.Add(staged[i].id);
                for(int i=muster;i<Math.Min(staged.Count,muster+2);i++)guards.Add(staged[i].id);
                initialWaveSize=wave.Count;ChangePhase(AiArmyPhase072.March,"Owned soldiers actually assembled for the first wave");
            }
            if(phase==AiArmyPhase072.Muster){Dispatch(soldiers,OrderKind.Move,stage,water,true);return;}
            var waiting=soldiers.FindAll(e=>!wave.Contains(e.id)&&!guards.Contains(e.id));
            if(guards.Count<2)foreach(var e in waiting.FindAll(AtStage))
            {guards.Add(e.id);if(guards.Count>=2)break;}
            waiting.RemoveAll(e=>guards.Contains(e.id));var ready=waiting.FindAll(AtStage);
            if(ready.Count>=4)
            {
                foreach(var e in ready)wave.Add(e.id);initialWaveSize=Math.Max(initialWaveSize,wave.Count);
                world.Emit("AIArmyReinforcement",player,ready[0].id,"A real assembled reinforcement cohort joins the expedition",ready.Count,0,stage);
            }
            destination=rememberedOrOpeningDestination;
            var marching=soldiers.FindAll(e=>wave.Contains(e.id));
            if(!hasKnownTarget)search.TryDestination(marching,out destination);
            // A visible military raid already returned above; objectives never
            // override defense or manufacture a departure before true muster.
            if(world.TryAiObjectiveDestination(player,out var objective))destination=objective;
            Dispatch(marching,OrderKind.AttackMove,destination,water);
            Dispatch(soldiers.FindAll(e=>guards.Contains(e.id)),OrderKind.Move,stage,water,true);
            Dispatch(soldiers.FindAll(e=>!wave.Contains(e.id)&&!guards.Contains(e.id)),OrderKind.Move,stage,water,true);
        }
        CommandResult Send(GameCommand c){c.player=player;return world.Issue(c);}
    }
}
