using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class Healing076AiTests
    {
        // Declared peaceful Age-II / wounded-force fixtures. Prepared starting
        // units, age and wallets below are not ordinary Age-I match evidence.
        sealed class Arena
        {
            public readonly World World;public readonly AiMedicalSupport076 Medical;
            public readonly EntityState Producer;public readonly List<EntityState> Soldiers=new List<EntityState>();
            public Arena(string civilization="ottoman",int soldiers=6,int doctors=0,int age=2)
            {
                World=new World(SnapshotCodec.Clone(Catalog.Load()),7601,civilization,"coast",5);
                foreach(var e in World.Entities.Where(e=>e.owner==0&&e.def.kind==EntityKind.Unit||World.Guardian(e)).ToArray())World.Remove(e);
                World.Players[0].age=age;World.Players[0].resources=new Cost(10000,10000,10000);
                World.Spawn("house",0,new Vector2(20,40));World.Spawn("house",0,new Vector2(25,40));World.Spawn("house",0,new Vector2(30,40));
                Producer=World.Spawn(civilization=="ottoman"?"mosque":"church",0,new Vector2(40,36));
                for(int i=0;i<12;i++)World.Spawn(World.WorkerDefinition(0).id,0,new Vector2(16+i%4*2,22+i/4*2));
                string soldier=civilization=="ottoman"?"janissary":"musketeer";
                for(int i=0;i<soldiers;i++)Soldiers.Add(World.Spawn(soldier,0,new Vector2(33+i%5*2,25+i/5*2)));
                for(int i=0;i<doctors;i++)World.Spawn(civilization=="ottoman"?"imam":"priest",0,new Vector2(36+i*2,30));
                World.Fog.Tick(World.Entities);Medical=new AiMedicalSupport076(World,0);
            }
            public List<EntityState> Own()=>World.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
            public List<EntityState> Visible()=>World.Entities.Where(e=>AiResourceContinuity072.EconomicThreat(World,0,e)).ToList();
            public int WorkerCount=>Own().Count(e=>e.Worker);
            public void Support()=>Medical.ReviewSupport(Own(),Soldiers,Visible(),World.OpeningTownPosition(0));
        }
        static void Accepted(CommandResult result)=>Assert.IsTrue(result.accepted,result.reason);
        static void Steps(World w,int count){for(int i=0;i<count;i++)w.Tick(.1f);}
        static void Hold(World w,EntityState e)=>Accepted(w.Issue(new GameCommand{player=e.owner,kind=CommandKind.Order,actors=new[]{e.id},order=OrderKind.Hold}));
        static void Wound(Arena a,int count=1)
        {for(int i=0;i<count;i++)a.Soldiers[i].hp=a.World.MaxHp(a.Soldiers[i])*.3f;}

        [Test] public void ExplicitEligibilityCoversLivingLandRolesAndExcludesCargoNavalBuildingsAndGuardians()
        {
            var rules=Catalog.Load();
            foreach(string id in new[]{"villager","coureur","explorer","envoy","native_scout","musketeer","janissary","cuirassier","cannon","mortar","heavy_cannon","great_bombard","imam","priest","revolutionary"})
                Assert.IsTrue(rules.Entity(id).canReceiveHealing,id);
            foreach(var e in rules.entities.Where(e=>e.kind!=EntityKind.Unit||e.waterborne||e.civilian||e.id=="guardian"))
                Assert.IsFalse(e.canReceiveHealing,e.id);
            foreach(string id in new[]{"imam","priest"})
            {
                var d=rules.Entity(id);Assert.IsTrue(d.healer);Assert.AreEqual(Tags.Healer,d.tags);Assert.AreEqual(10,d.healRate);
                Assert.AreEqual(12,d.healRange);Assert.AreEqual(10,d.trainingLimit);Assert.AreEqual(1,d.population);Assert.AreEqual(30,d.time);
            }
        }

        [TestCase("ottoman","imam",115,0)][TestCase("coast","priest",0,100)]
        [TestCase("river","priest",0,100)][TestCase("france","priest",0,100)]
        public void FourClassicCivilizationsBuyRealDoctorsAndCancelRefundsTheExactPaidQueue(string civ,string unit,float food,float coin)
        {
            var a=new Arena(civ);var w=a.World;var p=w.Players[0];var before=p.resources;
            Assert.AreEqual(0,w.UnitCountIncludingProduction(0,unit));
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{a.Producer.id},item=unit}));
            Assert.AreEqual(before.food-food,p.resources.food);Assert.AreEqual(before.coin-coin,p.resources.coin);
            Assert.AreEqual(1,p.reservedPopulation);Assert.AreEqual(1,w.UnitCountIncludingProduction(0,unit));
            Assert.AreEqual(30,a.Producer.queue[0].duration);Assert.AreEqual(new Cost(food,0,coin),a.Producer.queue[0].paid);
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{a.Producer.id},index=0}));
            Assert.AreEqual(before,p.resources);Assert.AreEqual(0,p.reservedPopulation);Assert.AreEqual(0,w.UnitCountIncludingProduction(0,unit));
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Payment"&&e.entity==a.Producer.id));Assert.IsTrue(w.Events.Any(e=>e.kind=="Refund"&&e.entity==a.Producer.id));
        }

        [Test] public void ChurchPriestAndMosqueImamRemainCivilizationAndEraBound()
        {
            var rules=Catalog.Load();
            foreach(string civ in new[]{"coast","river","france","ottoman","wayfarer","highland"})
            {
                var w=new World(SnapshotCodec.Clone(rules),7602,civ,"coast",5);
                Assert.AreEqual(civ=="coast"||civ=="river"||civ=="france",w.EntityAvailable(0,w.Rules.Entity("church")),civ);
                Assert.AreEqual(civ=="coast"||civ=="river"||civ=="france",w.EntityAvailable(0,w.Rules.Entity("priest")),civ);
                Assert.AreEqual(civ=="ottoman",w.EntityAvailable(0,w.Rules.Entity("imam")),civ);
            }
            var a=new Arena(age:1);Assert.IsNotEmpty(a.World.ProductionReason(0,a.Producer,"imam"));
            var before=a.World.Players[0].resources;
            Assert.IsFalse(a.World.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{a.Producer.id},item="imam"}).accepted);
            Assert.AreEqual(before,a.World.Players[0].resources);Assert.IsEmpty(a.Producer.queue);
        }

        [Test] public void PaidMissionFervorChangesOnlyMedicalHealthRateAndTrainingSpeed()
        {
            var a=new Arena(doctors:1,age:3);var w=a.World;var doctor=a.Own().Single(e=>e.def.healer);
            doctor.hp=100;float soldierMax=w.MaxHp(a.Soldiers[0]);var before=w.Players[0].resources;
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{a.Producer.id},item="mission_fervor"}));
            Assert.AreEqual(before.wood-200,w.Players[0].resources.wood);Assert.AreEqual(before.coin-150,w.Players[0].resources.coin);
            w.TickEconomy(40);Assert.IsTrue(w.Players[0].technologies.Contains("mission_fervor"));
            Assert.AreEqual(300,w.MaxHp(doctor));Assert.AreEqual(150,doctor.hp,.001f);Assert.AreEqual(20,w.HealRate(doctor));
            Assert.AreEqual(soldierMax,w.MaxHp(a.Soldiers[0]));
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{a.Producer.id},item="imam"}));
            w.TickEconomy(19);Assert.AreEqual(28.5f,a.Producer.queue[0].elapsed,.001f);w.TickEconomy(1);
            Assert.IsEmpty(a.Producer.queue);Assert.AreEqual(2,w.UnitCountIncludingProduction(0,"imam"));
            var barracks=w.Spawn("barracks",0,new Vector2(42,45));
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{barracks.id},item="janissary"}));
            w.TickEconomy(1);Assert.AreEqual(1,barracks.queue[0].elapsed,.001f,"Medical training speed must not accelerate combat queues");
        }

        [Test] public void DoctorsAreNeverCombatStrengthOrColdArmyCohortMembersButVisibleWeaponsRemainThreats()
        {
            var a=new Arena(doctors:1);var w=a.World;var doctor=a.Own().Single(e=>e.def.healer);
            Assert.IsFalse(doctor.Military);Assert.IsTrue(doctor.Healer);
            var plan=new AiArmyPlan072(w,0,AiDifficultyProfile.From(w.Rules,"standard"));var state=plan.CaptureState();
            state.phase=AiArmyPhase072.March;state.initialWaveSize=3;state.wave=new[]{doctor.id};
            Assert.Throws<ArgumentException>(()=>plan.RestoreState(state));
            var enemy=w.Spawn("priest",1,new Vector2(38,28));Hold(w,enemy);w.Fog.Tick(w.Entities);
            Assert.IsTrue(w.IsVisible(0,enemy));Assert.IsTrue(AiResourceContinuity072.EconomicThreat(w,0,enemy));
            Assert.IsTrue(plan.HasImmediateThreat(a.Own(),new List<EntityState>{enemy}));
            Assert.IsFalse(AiResourceContinuity072.Safe(w,0,doctor.position,new List<EntityState>{enemy}));
            enemy.def.damage=enemy.def.meleeDamage=enemy.def.siegeDamage=0;
            Assert.IsFalse(AiResourceContinuity072.EconomicThreat(w,0,enemy));Assert.IsFalse(plan.HasImmediateThreat(a.Own(),new List<EntityState>{enemy}));
        }

        [Test] public void MedicalInvestmentNeedsRealWoundsAndProtectsReservesAndPaidPendingCount()
        {
            var a=new Arena();var w=a.World;var own=a.Own();var before=w.Players[0].resources;
            a.Medical.TryInvest(own,12,6,new Cost(),true);Assert.IsEmpty(a.Producer.queue);Assert.AreEqual(before,w.Players[0].resources);
            Wound(a);Assert.IsTrue(a.Medical.HasInvestmentDemand(own,12,6));
            a.Medical.TryInvest(own,11,6,new Cost(),true);a.Medical.TryInvest(own,12,5,new Cost(),true);
            a.Medical.TryInvest(own,12,6,new Cost(),false);Assert.IsEmpty(a.Producer.queue);
            a.Medical.TryInvest(own,12,6,before,true);Assert.IsEmpty(a.Producer.queue);Assert.AreEqual(before,w.Players[0].resources);
            a.Medical.TryInvest(own,12,6,new Cost(),true);Assert.AreEqual(1,a.Producer.queue.Count);Assert.AreEqual("imam",a.Producer.queue[0].item);
            Assert.AreEqual(before.food-115,w.Players[0].resources.food);Assert.AreEqual(1,w.UnitCountIncludingProduction(0,"imam"));
            a.Medical.TryInvest(own,12,6,new Cost(),true);Assert.AreEqual(1,a.Producer.queue.Count,"An already paid doctor is not purchased twice");
        }

        [Test] public void SecondDoctorNeedsTwentyCombatUnitsAndLargeActualDeficit()
        {
            var a=new Arena(soldiers:20,doctors:1);Wound(a,3);var w=a.World;
            a.Medical.TryInvest(a.Own(),12,19,new Cost(),true);Assert.IsEmpty(a.Producer.queue);
            a.Medical.TryInvest(a.Own(),12,20,new Cost(),true);Assert.AreEqual(1,a.Producer.queue.Count);Assert.AreEqual("imam",a.Producer.queue[0].item);
            w.TickEconomy(30);a.Medical.TryInvest(a.Own(),12,20,new Cost(),true);
            Assert.IsEmpty(a.Producer.queue,"AI cap two is lower than the player's data cap ten");
        }

        [Test] public void FirstDoctorGetsFairMosqueSlotWithoutBlockingCriticalWorkerLimitResearch()
        {
            var a=new Arena();Wound(a);var w=a.World;var economy=new AiCivilizationEconomy(w,0);
            Assert.IsTrue(a.Medical.YieldNoncriticalMosqueResearch(a.Own(),12,6));
            economy.Tick(a.Own(),a.Own().Where(e=>e.Worker).ToList(),a.Visible(),new Cost(),false,false,true);
            Assert.IsEmpty(a.Producer.queue);Assert.AreEqual(new Cost(),economy.TechnologyReserve(a.Own(),false,false,true));
            int limit=w.WorkerLimit(0);
            for(int i=12;i<limit-4;i++)w.Spawn("villager",0,new Vector2(46+i%4,24+i%3));
            Assert.IsFalse(a.Medical.YieldNoncriticalMosqueResearch(a.Own(),limit-4,6));
            economy.Tick(a.Own(),a.Own().Where(e=>e.Worker).ToList(),a.Visible(),new Cost(),false,false,false);
            Assert.AreEqual("mosque_galata",a.Producer.queue[0].item,"An actually needed worker limit remains ahead of medicine");
        }

        [Test] public void RealRecoveryMovesWaitsHealsAndReturnsWithoutOverwritingDifferentOrders()
        {
            var a=new Arena(doctors:1);Wound(a,2);var w=a.World;var patients=a.Soldiers.Take(2).ToArray();
            var starts=patients.Select(e=>e.position).ToArray();a.Support();
            Assert.AreEqual(2,a.Medical.RecoveryMembers.Length);Assert.IsTrue(a.Medical.HasStation);
            Assert.IsTrue(patients.All(e=>e.orders.Count==1&&e.orders.Peek().kind==OrderKind.Move));
            Assert.IsTrue(w.Events.Any(e=>e.kind=="AIMedicalRetreat"));
            for(int i=0;i<850&&a.Medical.RecoveryMembers.Length>0;i++){w.Tick(.1f);a.Support();}
            Assert.IsEmpty(a.Medical.RecoveryMembers);Assert.IsTrue(patients.Any(e=>Vector2.Distance(e.position,starts[Array.IndexOf(patients,e)])>2));
            Assert.IsTrue(patients.All(e=>e.hp>=w.MaxHp(e)*.85f));Assert.IsTrue(w.Events.Any(e=>e.kind=="Heal"));
            Assert.AreEqual(2,w.Events.Count(e=>e.kind=="AIMedicalReturn"&&e.message.StartsWith("Reached 85%")));
            // A later genuine manual-style order owns the intent and releases
            // this patient, rather than support review repeatedly overriding it.
            Wound(a);Steps(w,14);a.Support();var patient=patients[0];Assert.IsTrue(a.Medical.IsRecovering(patient.id));
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{patient.id},order=OrderKind.Move,point=new Vector2(55,22)}));
            Steps(w,14);a.Support();Assert.IsFalse(a.Medical.IsRecovering(patient.id));
            Assert.IsTrue(w.Events.Any(e=>e.kind=="AIMedicalReturn"&&e.entity==patient.id&&e.message.Contains("different order")));
        }

        [Test] public void ColdMedicalStateContinuesRealRecoveryAndRejectsForgedMembersClocksAndDuplicates()
        {
            var a=new Arena(doctors:1);Wound(a);a.Support();Steps(a.World,17);a.Support();
            var state=SnapshotCodec.Clone(a.Medical.CaptureState());Assert.IsNotEmpty(state.recovering);
            var cold=World.Restore(a.World.Rules,SnapshotCodec.Clone(a.World.CaptureState()));
            var restored=new AiMedicalSupport076(cold,0);restored.RestoreState(state);
            for(int i=0;i<120;i++)
            {
                a.World.Tick(.1f);cold.Tick(.1f);a.Support();
                var own=cold.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
                restored.ReviewSupport(own,own.Where(e=>e.Military&&!e.def.waterborne).ToList(),cold.Entities.Where(e=>AiResourceContinuity072.EconomicThreat(cold,0,e)).ToList(),cold.OpeningTownPosition(0));
            }
            Assert.AreEqual(JsonUtility.ToJson(a.Medical.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(a.World.CaptureState()),JsonUtility.ToJson(cold.CaptureState()));
            var bad=SnapshotCodec.Clone(state);bad.recovering[0].id=cold.Entities.First(e=>e.owner==1).id;
            Assert.Throws<ArgumentException>(()=>restored.RestoreState(bad));bad=SnapshotCodec.Clone(state);bad.reviewAt=float.NaN;
            Assert.Throws<ArgumentException>(()=>restored.RestoreState(bad));bad=SnapshotCodec.Clone(state);bad.recovering[0].deadline=cold.Time+1000;
            Assert.Throws<ArgumentException>(()=>restored.RestoreState(bad));bad=SnapshotCodec.Clone(state);bad.recovering=new[]{bad.recovering[0],bad.recovering[0]};
            Assert.Throws<ArgumentException>(()=>restored.RestoreState(bad));
        }

        static Arena BusyDoctorRecovery()
        {
            var a=new Arena(doctors:1);Wound(a);var w=a.World;var doctor=a.Own().Single(e=>e.def.healer);
            a.Support();Assert.IsTrue(a.Medical.IsRecovering(a.Soldiers[0].id));
            // Real queued movement keeps the doctor occupied for longer than
            // the recovery deadline. There are no HP edits after this opening
            // wound, no fake clocks and no direct order-queue mutations.
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{doctor.id},order=OrderKind.Stop}));
            for(int i=0;i<40;i++)Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{doctor.id},
                order=OrderKind.Move,append=i>0,point=i%2==0?new Vector2(50,18):new Vector2(32,18)}));
            Assert.IsFalse(doctor.autoHeal);Assert.AreEqual(40,doctor.orders.Count);return a;
        }
        static void OrdinaryArmyReview(World w,AiMedicalSupport076 medical)
        {
            var profile=AiDifficultyProfile.From(w.Rules,"standard");var plan=new AiArmyPlan072(w,0,profile);
            var own=w.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
            plan.Review(own,own.Where(e=>e.Military&&!e.def.waterborne).ToList(),new List<EntityState>(),w.OpeningTownPosition(1),true,
                new AiSearchPlan(w,0,w.OpeningTownPosition(1)),new AiWaterEconomy(w,0),new AiRevolution071(w,0,profile),new AiCivilizationEconomy(w,0),medical);
        }
        [Test] public void BlockedDoctorTimeoutReleasesToArmyWithoutSameTurnReadmissionAndColdContinuationAgrees()
        {
            var a=BusyDoctorRecovery();var w=a.World;var patient=a.Soldiers[0];float injured=patient.hp;
            for(int i=0;i<880;i++){w.Tick(.1f);a.Support();}
            Assert.IsTrue(a.Medical.IsRecovering(patient.id));Assert.AreEqual(injured,patient.hp);
            Assert.Greater(a.Own().Single(e=>e.def.healer).orders.Count,1,"The doctor is still genuinely committed to its queued movement");
            var cold=World.Restore(w.Rules,w.CaptureState());var restored=new AiMedicalSupport076(cold,0);
            restored.RestoreState(SnapshotCodec.Clone(a.Medical.CaptureState()));
            for(int i=0;i<50&&a.Medical.IsRecovering(patient.id);i++)
            {
                w.Tick(.1f);cold.Tick(.1f);a.Support();
                var own=cold.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
                restored.ReviewSupport(own,own.Where(e=>e.Military&&!e.def.waterborne).ToList(),new List<EntityState>(),cold.OpeningTownPosition(0));
            }
            Assert.IsFalse(a.Medical.IsRecovering(patient.id));Assert.IsFalse(restored.IsRecovering(patient.id));
            Assert.AreEqual(injured,patient.hp);Assert.AreEqual(1,w.Events.Count(e=>e.kind=="AIMedicalRetreat"&&e.entity==patient.id));
            Assert.IsFalse(a.Own().Single(e=>e.def.healer).orders.Any(o=>o.kind==OrderKind.Heal&&o.target==patient.id),
                "A released patient is not followed by a stale medical assignment");
            var returned=w.Events.Single(e=>e.kind=="AIMedicalReturn"&&e.entity==patient.id);
            Assert.AreEqual("Bounded recovery window expired",returned.message);Assert.GreaterOrEqual(returned.time,90);Assert.Less(returned.time,92);
            var retry=a.Medical.CaptureState().cooldowns.Single();Assert.AreEqual(patient.id,retry.id);Assert.AreEqual(returned.time+30,retry.retryAt,.001f);
            Assert.AreEqual(JsonUtility.ToJson(a.Medical.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(w.CaptureState()),JsonUtility.ToJson(cold.CaptureState()));
            // The normal planner gets an actual command opportunity before any
            // later medical retry, rather than another renewed90-second Hold.
            OrdinaryArmyReview(w,a.Medical);OrdinaryArmyReview(cold,restored);
            Assert.IsFalse(patient.hold);Assert.AreEqual(OrderKind.Move,patient.orders.Peek().kind);
            for(int i=0;i<25;i++)
            {
                w.Tick(.1f);cold.Tick(.1f);a.Support();
                var own=cold.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
                restored.ReviewSupport(own,own.Where(e=>e.Military&&!e.def.waterborne).ToList(),new List<EntityState>(),cold.OpeningTownPosition(0));
                Assert.IsFalse(a.Medical.IsRecovering(patient.id));Assert.IsFalse(restored.IsRecovering(patient.id));
            }
            Assert.AreEqual(1,w.Events.Count(e=>e.kind=="AIMedicalRetreat"&&e.entity==patient.id));
            Assert.AreEqual(JsonUtility.ToJson(a.Medical.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(w.CaptureState()),JsonUtility.ToJson(cold.CaptureState()));
        }
        [TestCase(false)][TestCase(true)]
        public void ReleaseClearsSingleKnownHealThroughHoldButPreservesQueuedIntent(bool queued)
        {
            var a=new Arena(doctors:1);Wound(a);var w=a.World;var patient=a.Soldiers[0];var doctor=a.Own().Single(e=>e.def.healer);
            a.Support();
            for(int i=0;i<500&&!(doctor.orders.Count==1&&doctor.orders.Peek().kind==OrderKind.Heal&&doctor.orders.Peek().target==patient.id);i++)
            {w.Tick(.1f);a.Support();}
            Assert.IsTrue(a.Medical.IsRecovering(patient.id));Assert.AreEqual(1,doctor.orders.Count);
            Assert.AreEqual(OrderKind.Heal,doctor.orders.Peek().kind);Assert.AreEqual(patient.id,doctor.orders.Peek().target);
            if(queued)Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{doctor.id},
                order=OrderKind.Move,append=true,point=new Vector2(50,18)}));
            float overriddenAt=w.Time;
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{patient.id},order=OrderKind.Move,point=new Vector2(55,22)}));
            for(int i=0;i<30&&a.Medical.IsRecovering(patient.id);i++){w.Tick(.1f);a.Support();}
            Assert.IsFalse(a.Medical.IsRecovering(patient.id));Assert.AreEqual(patient.id,a.Medical.CaptureState().cooldowns.Single().id);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="AIMedicalReturn"&&e.entity==patient.id&&e.message.Contains("different order")));
            Assert.IsTrue(doctor.autoHeal,"Ending an AI assignment does not disable automatic healing");
            if(queued)
            {
                Assert.AreEqual(2,doctor.orders.Count);Assert.AreEqual(OrderKind.Heal,doctor.orders.Peek().kind);
                Assert.AreEqual(patient.id,doctor.orders.Peek().target);Assert.AreEqual(OrderKind.Move,doctor.orders.Last().kind);
                Assert.IsFalse(w.Events.Any(e=>e.time>=overriddenAt&&e.kind=="Command"&&e.entity==doctor.id&&e.message.StartsWith("Hold")));
            }
            else
            {
                Assert.IsEmpty(doctor.orders);Assert.IsTrue(doctor.hold);
                Assert.IsTrue(w.Events.Any(e=>e.time>=overriddenAt&&e.kind=="Command"&&e.entity==doctor.id&&e.message.StartsWith("Hold")),
                    "The stale assignment is cleared through an accepted authority Hold command");
            }
        }
        [Test] public void RetryCooldownRejectsForgedRolesTimesDuplicatesAndCleansExpiredOrDeadMembers()
        {
            var a=new Arena(doctors:1);var w=a.World;var patient=a.Soldiers[0];var state=a.Medical.CaptureState();
            state.cooldowns=new[]{new AiMedicalRetry076Snapshot{id=patient.id,retryAt=w.Time+20}};
            a.Medical.RestoreState(state);Assert.AreEqual(1,a.Medical.CaptureState().cooldowns.Length);
            for(int mutation=0;mutation<8;mutation++)
            {
                var bad=SnapshotCodec.Clone(state);
                if(mutation==0)bad.cooldowns=null;
                if(mutation==1)bad.cooldowns[0].retryAt=float.NaN;
                if(mutation==2)bad.cooldowns[0].retryAt=w.Time;
                if(mutation==3)bad.cooldowns[0].retryAt=w.Time+31;
                if(mutation==4)bad.cooldowns[0].id=a.Own().Single(e=>e.def.healer).id;
                if(mutation==5)bad.cooldowns[0].id=w.Entities.First(e=>e.owner==1).id;
                if(mutation==6)bad.cooldowns=new[]{bad.cooldowns[0],SnapshotCodec.Clone(bad.cooldowns[0])};
                if(mutation==7)bad.recovering=new[]{new AiMedicalRecovery076Snapshot{id=patient.id,deadline=w.Time+90,point=patient.position}};
                Assert.Throws<ArgumentException>(()=>a.Medical.RestoreState(bad),"Invalid medical retry state "+mutation);
            }
            state.cooldowns[0].retryAt=w.Time+.2f;a.Medical.RestoreState(state);Steps(w,3);
            Assert.IsEmpty(a.Medical.CaptureState().cooldowns,"Expired records are absent even before the next scheduled review");
            state.cooldowns[0].retryAt=w.Time+20;a.Medical.RestoreState(state);w.Remove(patient);
            Assert.IsEmpty(a.Medical.CaptureState().cooldowns,"A dead patient never persists a retry reservation");
            var cold=World.Restore(w.Rules,w.CaptureState());new AiMedicalSupport076(cold,0).RestoreState(a.Medical.CaptureState());
        }

        [Test] public void RecoveryKeepsFourDefendersRejectsBlockedRoutesAndDoesNotReadHiddenThreats()
        {
            var a=new Arena(doctors:1);Wound(a,6);a.Support();Assert.AreEqual(2,a.Medical.RecoveryMembers.Length);
            var b=new Arena(soldiers:4,doctors:1);Wound(b,4);b.Support();Assert.IsEmpty(b.Medical.RecoveryMembers);
            var blocked=new Arena(doctors:1);Wound(blocked);blocked.World.Navigation.ConfigureTerrain(p=>p.x>=28&&p.x<=30);
            blocked.Support();Assert.IsEmpty(blocked.Medical.RecoveryMembers,"A reachable-looking endpoint across an impassable wall does not authorize retreat");
            var first=new Arena(doctors:1);var second=new Arena(doctors:1);Wound(first);Wound(second);
            var hidden=second.World.Spawn("musketeer",1,new Vector2(108,100));second.World.Fog.Tick(second.World.Entities);
            Assert.IsFalse(second.World.IsVisible(0,hidden));first.Support();second.Support();
            Assert.AreEqual(first.Medical.Station,second.Medical.Station);Assert.AreEqual(first.Medical.RecoveryMembers,second.Medical.RecoveryMembers);
            Assert.AreEqual(first.Soldiers[0].orders.Peek().point,second.Soldiers[0].orders.Peek().point);
        }
    }
}
