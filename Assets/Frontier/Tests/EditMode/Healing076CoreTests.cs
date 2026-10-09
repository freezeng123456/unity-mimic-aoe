using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Controlled medicine mechanics fixtures. Initial wounds, wallets and terrain
    // are declared here; these are not ordinary-match or native GUI evidence.
    public sealed class Healing076CoreTests
    {
        const string Healer="healing076_fixture",FastHealer="healing076_fast",MedicalTech="healing076_tech";
        sealed class Fixture
        {
            public readonly World World;public readonly EntityState Medic,Patient;
            public Fixture(bool teams=false)
            {
                var rules=SnapshotCodec.Clone(Catalog.Load());
                var healer=new EntityDef{id=Healer,name="Fixture medic",kind=EntityKind.Unit,tags=Tags.Healer,healer=true,canReceiveHealing=true,
                    healRate=10,healRange=12,trainingLimit=3,hp=200,speed=4,vision=28,radius=.4f,damage=5,meleeDamage=5,range=1.3f,period=1.5f,
                    producer="barracks",cost=new Cost(10,0,0),time=10,population=1,batch=1};
                var fast=SnapshotCodec.Clone(healer);fast.id=FastHealer;fast.name="Fast fixture medic";fast.healRate=20;
                rules.entities=rules.entities.Concat(new[]{healer,fast}).ToArray();
                rules.techs=rules.techs.Concat(new[]{new TechDef{id=MedicalTech,name="Fixture medical research",building="barracks",tags=Tags.Healer,
                    healing=1,healerTrainSpeed=.5f,health=.5f}}).ToArray();
                foreach(var definition in rules.entities)if(definition.id=="musketeer"||definition.id=="villager"||definition.id=="native_scout"||definition.id=="falconet")definition.canReceiveHealing=true;
                World=teams?new World(rules,7601,new[]{"coast","coast","coast","coast"},new[]{0,1,0,1},5):new World(rules,7601,"coast","coast",5);
                foreach(var e in World.Entities.Where(e=>e.def.id!="town").ToArray())World.Remove(e);
                foreach(var player in World.Players){player.resources=new Cost(1000,1000,1000);player.populationCap=200;player.age=3;}
                Medic=World.Spawn(Healer,0,new Vector2(40,20));Patient=World.Spawn("musketeer",0,new Vector2(45,20));Patient.hp=50;
                World.Fog.Tick(World.Entities);
            }
            public EntityState Spawn(string id,int owner,Vector2 point)
            {var result=World.Spawn(id,owner,point);World.Fog.Tick(World.Entities);return result;}
        }
        static GameCommand Order(EntityState actor,OrderKind kind,int target=0,bool append=false,Vector2? point=null)=>new GameCommand{
            player=actor.owner,kind=CommandKind.Order,actors=new[]{actor.id},order=kind,target=target,append=append,point=point??actor.position};
        static void Accepted(CommandResult result)=>Assert.IsTrue(result.accepted,result.reason);
        static void Steps(World world,int count){for(int i=0;i<count;i++)world.Tick(.1f);}
        static string State(World world)=>JsonUtility.ToJson(world.CaptureState());
        static SimEvent[] Heals(World world)=>world.Events.Where(e=>e.hasHealing).ToArray();

        [Test] public void ManualHealRecordsActualPositiveDeltaAndWorkWithoutPaymentOrMovement()
        {
            var f=new Fixture();var w=f.World;var funds=w.Players[0].resources;var start=f.Medic.position;var pop=w.Players[0].usedPopulation;
            Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));w.Tick(.1f);
            Assert.AreEqual(51,f.Patient.hp,.0001f);Assert.AreEqual(start,f.Medic.position);Assert.AreEqual(funds,w.Players[0].resources);Assert.AreEqual(pop,w.Players[0].usedPopulation);
            var ev=Heals(w).Single();Assert.AreEqual("Heal",ev.kind);Assert.AreEqual(f.Medic.id,ev.healing.source);Assert.AreEqual(f.Patient.id,ev.healing.target);
            Assert.AreEqual(50,ev.healing.hpBefore);Assert.AreEqual(51,ev.healing.hpAfter);Assert.AreEqual(1,ev.healing.actual);Assert.AreEqual(10,ev.healing.rate);
            Assert.AreEqual(WorkKind.Heal,f.Medic.work.kind);Assert.IsTrue(f.Medic.IsWorkingAt(w.Time));Assert.IsFalse(f.Medic.Military);
        }
        [Test] public void FullHealthAndSelfTargetsRejectWithoutMutatingState()
        {
            var f=new Fixture();var w=f.World;f.Patient.hp=w.MaxHp(f.Patient);string before=State(w);
            Assert.IsFalse(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)).accepted);Assert.AreEqual(before,State(w));
            Assert.IsFalse(w.Issue(Order(f.Medic,OrderKind.Heal,f.Medic.id)).accepted);Assert.AreEqual(before,State(w));
        }
        [Test] public void MixedSelectionAndDuplicateIdsOnlyOrderOneCapableHealer()
        {
            var f=new Fixture();var worker=f.Spawn("villager",0,new Vector2(37,20));Accepted(f.World.Issue(Order(worker,OrderKind.Move,point:new Vector2(35,20))));
            var original=worker.orders.Peek();Accepted(f.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,order=OrderKind.Heal,
                actors=new[]{worker.id,f.Medic.id,f.Medic.id},target=f.Patient.id}));
            Assert.AreSame(original,worker.orders.Peek());Assert.AreEqual(1,f.Medic.orders.Count);f.World.Tick(.1f);Assert.AreEqual(1,Heals(f.World).Length);
        }
        [Test] public void RealTeamPatientAcceptedButEnemyNeutralBuildingAndUndeclaredCargoReject()
        {
            var f=new Fixture(true);var w=f.World;var ally=f.Spawn("musketeer",2,new Vector2(44,22));ally.hp=50;
            var enemy=f.Spawn("musketeer",1,new Vector2(100,70));enemy.hp=50;
            var neutral=f.Spawn("guardian",-1,new Vector2(100,75));neutral.hp=20;
            var cargo=f.Spawn("factory_wagon",0,new Vector2(43,23));cargo.hp=20;
            Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,ally.id)));
            foreach(var invalid in new[]{enemy,neutral,cargo,w.Entities.First(e=>e.owner==0&&e.def.id=="town")})
            {string before=State(w);Assert.IsFalse(w.Issue(Order(f.Medic,OrderKind.Heal,invalid.id)).accepted);Assert.AreEqual(before,State(w));}
            w.Tick(.1f);Assert.AreEqual(51,ally.hp,.0001f);
        }
        [Test] public void StaticGathererKeepsRealEconomyOrderWhileReceivingMedicine()
        {
            var f=new Fixture();var w=f.World;var worker=f.Spawn("villager",0,new Vector2(42,23));worker.hp=30;
            var berry=f.Spawn("berry",-1,new Vector2(43,23));Accepted(w.Issue(Order(worker,OrderKind.Gather,berry.id)));
            Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,worker.id)));float food=w.Players[0].resources.food;w.Tick(.1f);
            Assert.Greater(worker.hp,30);Assert.AreEqual(OrderKind.Gather,worker.orders.Peek().kind);Assert.Greater(w.Players[0].resources.food,food);
            Assert.AreEqual(WorkKind.Gather,worker.work.kind);Assert.AreEqual(WorkKind.Heal,f.Medic.work.kind);
        }
        [Test] public void AutoHealingHasNoSyntheticOrderAndNeverChasesDistantPatient()
        {
            var f=new Fixture();var w=f.World;f.Patient.position=f.Patient.previousPosition=new Vector2(60,20);w.Fog.Tick(w.Entities);var start=f.Medic.position;
            Steps(w,20);Assert.AreEqual(50,f.Patient.hp);Assert.AreEqual(start,f.Medic.position);Assert.IsEmpty(f.Medic.orders);
            f.Patient.position=f.Patient.previousPosition=new Vector2(45,20);w.Fog.Tick(w.Entities);Steps(w,6);
            Assert.Greater(f.Patient.hp,50);Assert.IsEmpty(f.Medic.orders);Assert.AreEqual(start,f.Medic.position);Assert.IsTrue(f.Medic.IsWorkingAt(w.Time));
        }
        [Test] public void StopClosesAutoHealAndHoldAllowsExplicitlyReenabledStationaryCare()
        {
            var f=new Fixture();var w=f.World;Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));w.Tick(.1f);
            Accepted(w.Issue(Order(f.Medic,OrderKind.Stop)));float hp=f.Patient.hp;Steps(w,6);Assert.AreEqual(hp,f.Patient.hp);Assert.IsFalse(f.Medic.autoHeal);Assert.AreEqual(0,f.Medic.healingTarget);
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.ToggleAutoHeal,actors=new[]{f.Medic.id,f.Medic.id}}));Assert.IsTrue(f.Medic.autoHeal);
            Accepted(w.Issue(Order(f.Medic,OrderKind.Hold)));w.Tick(.1f);Assert.Greater(f.Patient.hp,hp);Assert.IsTrue(f.Medic.hold);
        }
        [Test] public void ShiftHealSequenceCompletesBothPatientsWithoutOverhealOrSwallowingMove()
        {
            var f=new Fixture();var w=f.World;var second=f.Spawn("musketeer",0,new Vector2(45,23));f.Patient.hp=w.MaxHp(f.Patient)-.5f;second.hp=w.MaxHp(second)-1.5f;
            f.Medic.autoHeal=false;Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,second.id,true)));
            Accepted(w.Issue(Order(f.Medic,OrderKind.Move,append:true,point:new Vector2(36,20))));Assert.AreEqual(3,f.Medic.orders.Count);
            Steps(w,3);Assert.AreEqual(w.MaxHp(f.Patient),f.Patient.hp);Assert.AreEqual(w.MaxHp(second),second.hp);Assert.AreEqual(OrderKind.Move,f.Medic.orders.Peek().kind);
            Assert.AreEqual(.5f,Heals(w).First().healing.actual,.0001f);Assert.AreEqual(2,Heals(w).Sum(e=>e.healing.actual),.0001f);
        }
        [Test] public void MovingPatientAndMovingMedicDoNotReceiveSameTickHealing()
        {
            var f=new Fixture();var w=f.World;Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));Accepted(w.Issue(Order(f.Patient,OrderKind.Move,point:new Vector2(55,20))));
            w.Tick(.1f);Assert.AreEqual(50,f.Patient.hp);Assert.IsEmpty(Heals(w));Assert.Greater((f.Patient.position-f.Patient.previousPosition).sqrMagnitude,0);
            Accepted(w.Issue(Order(f.Patient,OrderKind.Stop)));Accepted(w.Issue(Order(f.Medic,OrderKind.Move,point:new Vector2(35,20))));w.Tick(.1f);Assert.AreEqual(50,f.Patient.hp);
        }
        [Test] public void ActualDamageLocksBothCurrentTickAndThreeSecondRecoveryWindow()
        {
            var f=new Fixture();var w=f.World;Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));w.ApplyDamage(f.Patient,10,DamageKind.Melee,1);
            float injured=f.Patient.hp;Assert.AreEqual(3,f.Patient.healingBlockedUntil,.0001f);Steps(w,29);Assert.AreEqual(injured,f.Patient.hp);Assert.IsEmpty(Heals(w));
            Steps(w,2);Assert.Greater(f.Patient.hp,injured);
            w.ApplyDamage(f.Medic,10,DamageKind.Melee,1);float before=f.Patient.hp;w.Tick(.1f);Assert.AreEqual(before,f.Patient.hp);
        }
        [Test] public void ProjectileImpactSettlesBeforeHealingEvenWhenHealerPrecedesShooterInList()
        {
            var f=new Fixture();var w=f.World;var enemy=f.Spawn("musketeer",1,new Vector2(88,75));Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));
            w.Projectiles.Add(new ProjectileState{source=enemy.id,target=f.Patient.id,owner=1,hitAt=.1f,damage=10,kind=DamageKind.Ranged});
            w.Tick(.1f);Assert.AreEqual(40,f.Patient.hp,.001f);Assert.IsEmpty(Heals(w));Assert.Greater(f.Patient.healingBlockedUntil,w.Time);
        }
        [Test] public void ActualAttackPreparationLocksPatientAndIdleHealerNeverPursuesEnemy()
        {
            var f=new Fixture();var w=f.World;var enemy=f.Spawn("musketeer",1,new Vector2(50,20));Accepted(w.Issue(Order(f.Patient,OrderKind.Attack,enemy.id)));
            Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));var start=f.Medic.position;w.Tick(.1f);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="AttackPrepared"&&e.entity==f.Patient.id));Assert.IsEmpty(Heals(w));
            Accepted(w.Issue(Order(f.Medic,OrderKind.Stop)));Steps(w,10);Assert.AreEqual(start,f.Medic.position);Assert.IsFalse(w.Events.Any(e=>e.kind=="AttackPrepared"&&e.entity==f.Medic.id));
        }
        [Test] public void NonstackArbitrationSelectsHighestRateIndependentOfEntityListOrder()
        {
            var a=new Fixture();var b=new Fixture();
            foreach(var f in new[]{a,b})
            {
                var fast=f.Spawn(FastHealer,0,new Vector2(41,22));Accepted(f.World.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));
                Accepted(f.World.Issue(Order(fast,OrderKind.Heal,f.Patient.id)));
            }
            b.World.Entities.Reverse();a.World.Tick(.1f);b.World.Tick(.1f);
            Assert.AreEqual(52,a.Patient.hp,.0001f);Assert.AreEqual(a.Patient.hp,b.Patient.hp);Assert.AreEqual(1,Heals(a.World).Length);Assert.AreEqual(1,Heals(b.World).Length);
            Assert.AreEqual(FastHealer,a.World.Get(Heals(a.World)[0].entity).def.id);Assert.AreEqual(Heals(a.World)[0].entity,Heals(b.World)[0].entity);
        }
        [Test] public void RealWeaponInjuryRetreatCombatRecoveryHealingAndReengagementFormOnePlayableCycle()
        {
            var f=new Fixture();var w=f.World;
            // Full health is part of this controlled opening, before any gameplay
            // command. There are no HP edits, injected damage or synthetic missiles below.
            f.Patient.hp=w.MaxHp(f.Patient);f.Medic.autoHeal=false;var opening=f.Patient.position;
            var enemy=f.Spawn("musketeer",1,new Vector2(54,20));Accepted(w.Issue(Order(f.Patient,OrderKind.Hold)));
            Accepted(w.Issue(Order(enemy,OrderKind.Attack,f.Patient.id)));
            for(int i=0;i<100&&!w.Events.Any(e=>e.kind=="Damage"&&e.entity==enemy.id&&e.target==f.Patient.id);i++)w.Tick(.1f);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="AttackPrepared"&&e.entity==enemy.id&&e.target==f.Patient.id));
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Fire"&&e.entity==enemy.id&&e.target==f.Patient.id));
            Assert.Less(f.Patient.hp,w.MaxHp(f.Patient));Assert.IsTrue(f.Patient.Alive);
            Accepted(w.Issue(Order(enemy,OrderKind.Hold)));
            Accepted(w.Issue(Order(f.Patient,OrderKind.Move,point:new Vector2(35,20))));
            Accepted(w.Issue(Order(f.Medic,OrderKind.Move,point:new Vector2(35,24))));
            for(int i=0;i<250&&(f.Patient.orders.Count>0||f.Medic.orders.Count>0);i++)w.Tick(.1f);
            Assert.IsEmpty(f.Patient.orders);Assert.IsEmpty(f.Medic.orders);Assert.Less(Vector2.Distance(f.Patient.position,new Vector2(35,20)),.1f);
            Assert.Greater(Vector2.Distance(opening,f.Patient.position),8);Assert.IsTrue(f.Patient.Alive);Assert.IsEmpty(Heals(w));
            float lastDamage=w.Events.Last(e=>e.kind=="Damage"&&e.target==f.Patient.id).time;float wounded=f.Patient.hp;
            Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));
            for(int i=0;i<200&&f.Patient.hp<w.MaxHp(f.Patient);i++)
            {
                w.Tick(.1f);if(w.Time<lastDamage+World.HealingCombatDelay076)Assert.AreEqual(wounded,f.Patient.hp);
            }
            Assert.AreEqual(w.MaxHp(f.Patient),f.Patient.hp);Assert.Greater(Heals(w).Length,0);
            Assert.GreaterOrEqual(Heals(w).First().time+.0001f,lastDamage+World.HealingCombatDelay076);
            int resumed=w.Events.Count;Accepted(w.Issue(Order(f.Patient,OrderKind.Attack,enemy.id)));
            for(int i=0;i<400&&!w.Events.Skip(resumed).Any(e=>e.kind=="Fire"&&e.entity==f.Patient.id&&e.target==enemy.id);i++)w.Tick(.1f);
            Assert.IsTrue(w.Events.Skip(resumed).Any(e=>e.kind=="AttackPrepared"&&e.entity==f.Patient.id&&e.target==enemy.id));
            Assert.IsTrue(w.Events.Skip(resumed).Any(e=>e.kind=="Fire"&&e.entity==f.Patient.id&&e.target==enemy.id));
        }
        [Test] public void DefaultOttomanAgeOneWorkersFundRealMosqueAgeAdvanceAndPaidImam()
        {
            // Ordinary map/default opening, scripted player input only. No entity
            // spawning, removal, wallet grants, HP edits, accelerated dt or AI claims.
            var w=new World(SnapshotCodec.Clone(Catalog.Load()),7602,"ottoman","coast",3);
            var town=w.Entities.First(e=>e.owner==0&&e.def.id=="town");var workers=w.Entities.Where(e=>e.owner==0&&e.Worker).ToArray();
            Assert.AreEqual(1,w.Players[0].age);Assert.AreEqual(6,workers.Length);Assert.AreEqual(300,w.Players[0].resources.food);Assert.IsFalse(w.Entities.Any(e=>e.Healer));
            Vector2 foundation=default;bool found=false;
            for(int y=-14;y<=14&&!found;y+=2)for(int x=-14;x<=14&&!found;x+=2)
            {
                var point=town.position+new Vector2(x,y);if(w.CanBuild(0,"mosque",point)==""){foundation=point;found=true;}
            }
            Assert.IsTrue(found,"A real visible legal Mosque foundation is required");
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{workers[0].id},item="mosque",point=foundation}));
            var mosque=w.Entities.Single(e=>e.owner==0&&e.def.id=="mosque");Assert.AreEqual(100,mosque.paidConstruction.wood);Assert.AreEqual(200,w.Players[0].resources.wood);
            void GatherFood(EntityState worker)
            {
                var food=w.Entities.Where(e=>e.Alive&&e.def.resource==ResourceKind.Food&&w.IsVisible(0,e)&&w.CanGather(worker,e))
                    .OrderBy(e=>(e.position-worker.position).sqrMagnitude).FirstOrDefault();
                Assert.IsNotNull(food);Accepted(w.Issue(Order(worker,OrderKind.Gather,food.id)));
            }
            foreach(var worker in workers.Skip(1))GatherFood(worker);
            for(int i=0;i<2000&&!mosque.Complete;i++)w.Tick(.1f);Assert.IsTrue(mosque.Complete);
            // Ordinary automatic settlers fill the initial cap; housing is also
            // paid and physically built before reserving the Imam's population.
            found=false;
            for(int y=-14;y<=14&&!found;y+=2)for(int x=-14;x<=14&&!found;x+=2)
            {
                var point=town.position+new Vector2(x,y);if(w.CanBuild(0,"house",point)==""){foundation=point;found=true;}
            }
            Assert.IsTrue(found);Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{workers[0].id},item="house",point=foundation}));
            var house=w.Entities.Single(e=>e.owner==0&&e.def.id=="house");Assert.AreEqual(100,house.paidConstruction.wood);
            for(int i=0;i<2000&&!house.Complete;i++)w.Tick(.1f);Assert.IsTrue(house.Complete);GatherFood(workers[0]);
            Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{mosque.id},item="imam"}).accepted,"Imam requires actual Age II");
            for(int i=0;i<6000&&w.Players[0].resources.food<800;i++)w.Tick(.1f);
            Assert.GreaterOrEqual(w.Players[0].resources.food,800);Assert.Greater(w.Players[0].gathered.food,0);
            float beforeAge=w.Players[0].resources.food;Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{town.id},choice="ottoman_administration_2"}));
            Assert.AreEqual(beforeAge-800,w.Players[0].resources.food,.001f);Assert.AreEqual(800,town.queue.Single().paid.food);
            for(int i=0;i<1000&&w.Players[0].age<2;i++)w.Tick(.1f);Assert.AreEqual(2,w.Players[0].age);
            for(int i=0;i<2000&&w.Players[0].resources.food<115;i++)w.Tick(.1f);
            float beforeImam=w.Players[0].resources.food;Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{mosque.id},item="imam"}));
            Assert.AreEqual(beforeImam-115,w.Players[0].resources.food,.001f);Assert.AreEqual(115,mosque.queue.Single().paid.food);Assert.AreEqual(1,mosque.queue.Single().population);
            for(int i=0;i<400&&!w.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.id=="imam");i++)w.Tick(.1f);
            var imam=w.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id=="imam");Assert.IsTrue(w.CanHeal(imam));Assert.IsEmpty(mosque.queue);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="UnitTrained"&&e.entity==mosque.id&&e.target==imam.id));
            Assert.GreaterOrEqual(w.Players[0].spent.food,915);Assert.GreaterOrEqual(w.Players[0].spent.wood,100);
        }
        [Test] public void MultipleAutomaticMedicsFillDifferentPatientsDeterministically()
        {
            var a=new Fixture();var b=new Fixture();
            foreach(var f in new[]{a,b})
            {
                f.Spawn(FastHealer,0,new Vector2(41,22));var second=f.Spawn("musketeer",0,new Vector2(46,23));second.hp=80;
            }
            b.World.Entities.Reverse();a.World.Tick(.1f);b.World.Tick(.1f);
            Assert.AreEqual(2,Heals(a.World).Length);Assert.AreEqual(2,Heals(b.World).Length);Assert.AreEqual(2,Heals(a.World).Select(e=>e.target).Distinct().Count());
            CollectionAssert.AreEqual(Heals(a.World).Select(e=>e.entity+":"+e.target+":"+e.amount),Heals(b.World).Select(e=>e.entity+":"+e.target+":"+e.amount));
        }
        [Test] public void EqualRateAndEqualDistanceArbitrationUsesStableLowestId()
        {
            var f=new Fixture();var other=f.Spawn(Healer,0,new Vector2(50,20));Accepted(f.World.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));
            Accepted(f.World.Issue(Order(other,OrderKind.Heal,f.Patient.id)));f.World.Entities.Reverse();f.World.Tick(.1f);
            Assert.AreEqual(51,f.Patient.hp);Assert.AreEqual(f.Medic.id,Heals(f.World).Single().entity);
        }
        [Test] public void SealedTerrainPreventsThroughWallHealingAndDoesNotTeleportMedic()
        {
            var f=new Fixture();var w=f.World;w.Navigation.ConfigureTerrain(point=>Mathf.Abs(point.x-42)<.1f);Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));
            var start=f.Medic.position;Steps(w,30);Assert.AreEqual(start,f.Medic.position);Assert.AreEqual(50,f.Patient.hp);Assert.IsEmpty(Heals(w));
            w.Navigation.ConfigureTerrain(point=>false);Steps(w,2);Assert.Greater(f.Patient.hp,50);
        }
        [Test] public void ExplicitHealUsesActualNavigableDetourAroundFiniteWall()
        {
            var f=new Fixture();var w=f.World;f.Patient.position=f.Patient.previousPosition=new Vector2(52,20);w.Fog.Tick(w.Entities);
            w.Navigation.ConfigureTerrain(point=>Mathf.Abs(point.x-46)<.1f&&point.y<27);Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));var start=f.Medic.position;
            for(int i=0;i<400&&Heals(w).Length==0;i++)
            {
                var previous=f.Medic.position;w.Tick(.1f);Assert.IsTrue(w.Navigation.ClearLine(previous,f.Medic.position,f.Medic.def.radius));
                if(Heals(w).Length>0)Assert.IsTrue(w.Navigation.ClearLine(f.Medic.position,f.Patient.position,.05f));
            }
            Assert.Greater((f.Medic.position-start).sqrMagnitude,1);Assert.Greater(f.Patient.hp,50);Assert.Greater(Heals(w).Length,0);
        }
        [Test] public void ConcealedAllyPatientIsNotDisclosedByVisibleHealerReceipt()
        {
            var f=new Fixture();var w=f.World;var scout=f.Spawn("native_scout",0,new Vector2(44,20));scout.hp=40;scout.stealth=true;
            var enemy=f.Spawn("musketeer",1,new Vector2(50,23));Accepted(w.Issue(Order(enemy,OrderKind.Hold)));w.Fog.Tick(w.Entities);
            Assert.IsTrue(w.IsVisible(1,f.Medic));Assert.IsFalse(w.IsVisible(1,scout));Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,scout.id)));
            var command=w.Events.Last(e=>e.kind=="Command"&&e.entity==f.Medic.id);Assert.AreEqual(scout.id,command.target);Assert.AreEqual(0,command.visibilityMask&(1<<1));
            w.Tick(.1f);
            var heal=Heals(w).Single();Assert.AreEqual(0,heal.visibilityMask&(1<<1));Assert.IsTrue(scout.stealth);Assert.Greater(scout.hp,40);
        }
        [Test] public void VisiblePatientDoesNotDiscloseFogHiddenHealerSourceThroughCommandOrReceipt()
        {
            var f=new Fixture();var w=f.World;var enemy=f.Spawn("musketeer",1,new Vector2(63,20));
            Accepted(w.Issue(Order(enemy,OrderKind.Hold)));Accepted(w.Issue(Order(f.Patient,OrderKind.Hold)));w.Fog.Tick(w.Entities);
            Assert.IsTrue(w.IsVisible(1,f.Patient));Assert.IsFalse(w.IsVisible(1,f.Medic));
            Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));
            var command=w.Events.Last(e=>e.kind=="Command"&&e.entity==f.Medic.id);Assert.AreEqual(0,command.visibilityMask&(1<<1));
            w.Tick(.1f);var heal=Heals(w).Single();Assert.AreEqual(0,heal.visibilityMask&(1<<1));Assert.AreEqual(f.Medic.id,heal.healing.source);
        }
        [Test] public void DeathClearsActiveAndQueuedPatientLinksBeforeAnImmediateSnapshot()
        {
            var f=new Fixture();var w=f.World;var second=f.Spawn("musketeer",0,new Vector2(46,23));second.hp=40;
            Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,second.id,true)));
            w.Remove(f.Patient);Assert.AreEqual(0,f.Medic.healingTarget);Assert.AreEqual(1,f.Medic.orders.Count);Assert.AreEqual(second.id,f.Medic.orders.Peek().target);
            World.Restore(w.Rules,w.CaptureState());w.Remove(f.Medic);float hp=second.hp;w.Tick(.1f);Assert.AreEqual(hp,second.hp);
        }
        [Test] public void CombatLockAndExplicitHealingContinueExactlyAfterColdJsonSnapshot()
        {
            var f=new Fixture();var w=f.World;Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));w.ApplyDamage(f.Patient,5,DamageKind.Melee,1);Steps(w,8);
            var saved=JsonUtility.FromJson<WorldSnapshot>(State(w));Assert.AreEqual(5,saved.schema);var cold=World.Restore(w.Rules,saved);Assert.AreEqual(State(w),State(cold));
            for(int i=0;i<40;i++){w.Tick(.1f);cold.Tick(.1f);}Assert.AreEqual(State(w),State(cold));Assert.Greater(f.Patient.hp,45);
        }
        [Test] public void HealingSnapshotRejectsForgedCapabilityTimersReferencesAndReceipt()
        {
            var f=new Fixture();var w=f.World;Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));w.Tick(.1f);
            var save=w.CaptureState();save.entities.First(e=>e.state.id==f.Patient.id).state.autoHeal=true;Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,save));
            save=w.CaptureState();save.entities.First(e=>e.state.id==f.Medic.id).state.healingBlockedUntil=float.NaN;Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,save));
            save=w.CaptureState();save.entities.First(e=>e.state.id==f.Medic.id).state.healingTarget=f.Medic.id;Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,save));
            save=w.CaptureState();save.events.Last(e=>e.hasHealing).healing.actual=-1;Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,save));
            save=w.CaptureState();save.entities.First(e=>e.state.id==f.Medic.id).state.work=new WorkActivity{kind=WorkKind.Heal,target=f.Medic.id,time=w.Time};Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,save));
            save=w.CaptureState();save.schema=4;Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,save));
        }
        [Test] public void HealingSnapshotRejectsForgedQueuedPatientButAllowsAlreadyRestoredQueuedPatient()
        {
            var f=new Fixture();var w=f.World;var second=f.Spawn("musketeer",0,new Vector2(46,23));second.hp=80;
            Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,f.Patient.id)));Accepted(w.Issue(Order(f.Medic,OrderKind.Heal,second.id,true)));
            second.hp=w.MaxHp(second);World.Restore(w.Rules,w.CaptureState());
            var saved=w.CaptureState();saved.entities.First(e=>e.state.id==f.Medic.id).orders[1].target=f.Medic.id;
            Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,saved));
            saved=w.CaptureState();saved.entities.First(e=>e.state.id==f.Medic.id).orders[1].target=int.MaxValue;
            Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,saved));
            var enemy=f.Spawn("musketeer",1,new Vector2(100,70));saved=w.CaptureState();saved.entities.First(e=>e.state.id==f.Medic.id).orders[1].target=enemy.id;
            Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,saved));
        }
        [Test] public void HealingSnapshotRejectsExcessPaidTrainingReservations()
        {
            var f=new Fixture();var w=f.World;var producer=f.Spawn("barracks",0,new Vector2(40,40));
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{producer.id},item=Healer}));var saved=w.CaptureState();
            var queue=saved.entities.First(e=>e.state.id==producer.id).queue;
            saved.entities.First(e=>e.state.id==producer.id).queue=new[]{queue[0],SnapshotCodec.Clone(queue[0]),SnapshotCodec.Clone(queue[0])};
            Assert.Throws<ArgumentException>(()=>World.Restore(w.Rules,saved));
        }
        [Test] public void HealerResearchPreservesHealthRatioAndDoesNotAccelerateOtherTraining()
        {
            var f=new Fixture();var w=f.World;var producer=f.Spawn("barracks",0,new Vector2(40,40));f.Medic.hp=100;
            w.ApplyTechnology(0,MedicalTech);Assert.AreEqual(300,w.MaxHp(f.Medic));Assert.AreEqual(150,f.Medic.hp);Assert.AreEqual(20,w.HealRate(f.Medic));
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{producer.id},item=Healer}));w.TickEconomy(1);Assert.AreEqual(1.5f,producer.queue[0].elapsed,.0001f);
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{producer.id},index=0}));
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{producer.id},item="musketeer"}));w.TickEconomy(1);Assert.AreEqual(1,producer.queue[0].elapsed,.0001f);
        }
        [Test] public void TrainingCapIncludesAllPaidProducerReservationsAndCancellationReleasesSlot()
        {
            var f=new Fixture();var w=f.World;var a=f.Spawn("barracks",0,new Vector2(40,40));var b=f.Spawn("barracks",0,new Vector2(50,40));
            foreach(var producer in new[]{a,b})Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{producer.id},item=Healer}));
            Assert.AreEqual(3,w.UnitCountIncludingProduction(0,Healer));var funds=w.Players[0].resources;
            Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{a.id},item=Healer}).accepted);Assert.AreEqual(funds,w.Players[0].resources);
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{a.id},index=0}));Assert.AreEqual(2,w.UnitCountIncludingProduction(0,Healer));
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{a.id},item=Healer}));w.Remove(b);Assert.AreEqual(2,w.UnitCountIncludingProduction(0,Healer));
        }
    }
}
