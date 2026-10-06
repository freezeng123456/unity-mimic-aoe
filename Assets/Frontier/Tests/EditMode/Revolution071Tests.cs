using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class Revolution071Tests
    {
        // Age IV, the assembled workforce and funds are explicit isolation
        // inputs. Political changes, payments, arrivals and recovery below
        // use public commands and the same fixed scheduler as native play.
        sealed class Fixture
        {
            public World world;
            public MatchSimulation simulation;
            public int townId;
            public EntityState Town=>world.Get(townId);
            public PlayerState Player=>world.Players[0];
            public EntityState[] Workers=>world.Entities.Where(e=>e.Alive&&e.owner==0&&e.Worker).ToArray();
        }
        static Fixture Prepared(string civilization="coast",string map="frontier",int maxAge=5)
        {
            var world=new World(SnapshotCodec.Clone(Catalog.Load()),314159,civilization,"coast",maxAge,map);
            foreach(var e in world.Entities.ToArray())world.Remove(e);
            var town=world.Spawn("town",0,new Vector2(20,20));
            world.Spawn("town",1,new Vector2(110,110));
            world.Spawn("house",0,new Vector2(42,18));world.Spawn("house",0,new Vector2(18,42));
            for(int i=0;i<6;i++)world.Spawn(world.WorkerDefinition(0).id,0,new Vector2(24+i*1.4f,14));
            world.Players[0].age=4;world.Players[0].resources=new Cost(10000,10000,10000);
            world.Fog.Tick(world.Entities);
            if(world.AutoRecruitSeconds(0)>0)Accepted(world.Issue(new GameCommand{player=0,kind=CommandKind.ToggleRecruitment,actors=new[]{town.id}}));
            return new Fixture{world=world,simulation=new MatchSimulation(world,Array.Empty<int>()),townId=town.id};
        }
        static void Accepted(CommandResult result)=>Assert.IsTrue(result.accepted,result.reason);
        static void CostEquals(Cost expected,Cost actual,string message="")
        {Assert.AreEqual(expected.food,actual.food,.002f,message+" food");Assert.AreEqual(expected.wood,actual.wood,.002f,message+" wood");Assert.AreEqual(expected.coin,actual.coin,.002f,message+" coin");}
        static Cost Minus(Cost a,Cost b)=>new Cost(a.food-b.food,a.wood-b.wood,a.coin-b.coin);
        static void Steps(Fixture f,float seconds)
        {for(int i=0;i<Mathf.CeilToInt(seconds/MatchSimulation.StepSeconds);i++)Assert.IsTrue(f.simulation.Step(),"Fixed scheduler stopped");}
        static void Until(Fixture f,Func<bool> condition,float seconds,string message)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/MatchSimulation.StepSeconds)&&!condition();i++)Assert.IsTrue(f.simulation.Step(),"Scheduler stopped: "+message);
            Assert.IsTrue(condition(),message+"; t="+f.world.Time+" phase="+f.Player.politicalPhase+" queue="+string.Join(",",f.Town.queue.Select(j=>j.kind+":"+j.item+" "+j.elapsed+"/"+j.duration)));
        }
        static GameCommand Revolution(Fixture f,int town=0)=>new GameCommand{player=0,kind=CommandKind.Revolution,actors=new[]{town==0?f.townId:town}};
        static void CompleteRevolution(Fixture f)
        {Accepted(f.world.Issue(Revolution(f)));Until(f,()=>f.Player.politicalPhase==PoliticalPhase.Revolutionary,f.world.RevolutionDefinition(0).time+2,"Paid revolution must complete");}
        static GameCommand Rebuild(Fixture f)=>new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{f.townId},item=World.CivilianRebuildingTechnology};
        static Fixture ColdSlot(Fixture live)
        {
            string directory=Path.Combine(Path.GetTempPath(),"frontier-revolution071-"+Guid.NewGuid().ToString("N"));
            try
            {
                var group=live.world.Entities.Where(e=>e.Alive&&e.owner==0&&e.def.kind==EntityKind.Unit).Take(6).Select(e=>e.id).ToArray();
                var save=new SavedMatch{matchId=Guid.NewGuid().ToString("N"),appVersion="revolution071-test",world=live.world.CaptureState(),simulation=live.simulation.CaptureState(),
                    selection=group,groups=new[]{new ControlGroupSnapshot{number=1,actors=group}},cameraPosition=new Vector3(20,40,20),cameraFocus=new Vector2(20,20),cameraSize=26,simulationSpeed=1};
                string owned=JsonUtility.ToJson(save);string path=MatchSaveStore.SlotPath(directory,1);
                MatchSaveStore.Write(path,save);var loaded=MatchSaveStore.Read(path,"revolution071-test");
                Assert.AreEqual(owned,JsonUtility.ToJson(save),"Writing a slot must not alter its source object");
                CollectionAssert.AreEqual(group,loaded.selection);CollectionAssert.AreEqual(group,loaded.groups[0].actors);
                var world=World.Restore(live.world.Rules,loaded.world);
                var cold=new Fixture{world=world,simulation=MatchSimulation.Restore(world,loaded.simulation),townId=live.townId};
                EqualState(live,cold);return cold;
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        static void EqualState(Fixture a,Fixture b)
        {Assert.AreEqual(JsonUtility.ToJson(a.world.CaptureState()),JsonUtility.ToJson(b.world.CaptureState()));Assert.AreEqual(JsonUtility.ToJson(a.simulation.CaptureState()),JsonUtility.ToJson(b.simulation.CaptureState()));}
        static void PairedSteps(Fixture a,Fixture b,float seconds)
        {Steps(a,seconds);Steps(b,seconds);EqualState(a,b);}

        [TestCase("coast")][TestCase("river")][TestCase("france")][TestCase("ottoman")]
        public void FourCivilizationsPayOnceKeepIdentityAndRebuildWithoutUndoingTheirArmy(string civilization)
        {
            var f=Prepared(civilization);var w=f.world;var p=f.Player;var workers=f.Workers;var ids=workers.Select(e=>e.id).ToArray();
            var positions=workers.Select(e=>e.position).ToArray();var def=w.RevolutionDefinition(0);int population=p.usedPopulation;
            var explorer=w.Spawn("explorer",0,new Vector2(35,35));var wagon=w.Spawn("factory_wagon",0,new Vector2(38,35));
            int excludedPopulation=explorer.def.population+wagon.def.population;var openingDeck=p.deck.ToArray();var balance=p.resources;var spent=p.spent;
            Assert.AreEqual("",w.RevolutionReason(0,f.Town));Accepted(w.Issue(Revolution(f)));
            CostEquals(Minus(balance,def.cost),p.resources);CostEquals(spent+def.cost,p.spent);
            Assert.AreEqual(PoliticalPhase.Empire,p.politicalPhase);Assert.IsTrue(w.WorkerSourcesClosed(0));Assert.IsTrue(w.CivilianWorkersEnabled(0));
            Steps(f,def.time-1);Assert.IsTrue(workers.All(e=>e.Worker));Assert.AreEqual(PoliticalPhase.Empire,p.politicalPhase);
            Until(f,()=>p.politicalPhase==PoliticalPhase.Revolutionary,3,"Completion must happen only at the paid queue boundary");
            Assert.AreEqual(4,p.age);Assert.AreEqual(civilization,p.civilization);Assert.AreEqual(def.id,p.revolutionId);Assert.Greater(p.revolutionAt,0);
            Assert.AreEqual(population+excludedPopulation,p.usedPopulation);Assert.AreEqual(0,p.reservedPopulation);
            for(int i=0;i<ids.Length;i++)
            {
                var unit=w.Get(ids[i]);Assert.IsTrue(unit.Alive);Assert.AreEqual(def.unit,unit.def.id);Assert.IsFalse(unit.Worker);Assert.IsFalse(unit.def.civilian);
                Assert.AreEqual(1,unit.def.population);Assert.AreEqual(.4f,unit.def.radius,.0001f);Assert.AreEqual(positions[i],unit.position);Assert.AreEqual(w.MaxHp(unit),unit.hp,.001f);
            }
            Assert.AreEqual("explorer",explorer.def.id);Assert.AreEqual("factory_wagon",wagon.def.id);CollectionAssert.AreEqual(openingDeck,p.deck);
            var active=w.ActiveShipmentCards(0);CollectionAssert.AreEqual(def.cards,active);active.Clear();CollectionAssert.AreEqual(def.cards,w.ActiveShipmentCards(0));
            Assert.IsFalse(w.Issue(Revolution(f)).accepted);Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{f.townId},choice="economic"}).accepted);
            Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{f.townId},item=w.WorkerDefinition(0).id}).accepted);
            var rebuild=w.Rules.Tech(World.CivilianRebuildingTechnology);balance=p.resources;Accepted(w.Issue(Rebuild(f)));
            CostEquals(Minus(balance,rebuild.cost),p.resources);Steps(f,rebuild.time-1);Assert.AreEqual(PoliticalPhase.Revolutionary,p.politicalPhase);
            Until(f,()=>p.politicalPhase==PoliticalPhase.CivilianRebuilt,3,"Paid civilian rebuilding must finish");
            Assert.IsTrue(p.technologies.Contains(rebuild.id));Assert.GreaterOrEqual(p.civilianRebuiltAt,p.revolutionAt);Assert.AreEqual(4,p.age);
            Assert.IsTrue(ids.All(id=>w.Get(id).def.revolutionary));Assert.IsFalse(w.WorkerSourcesClosed(0));Assert.IsTrue(w.CivilianWorkersEnabled(0));
            CollectionAssert.AreEqual(def.cards,w.ActiveShipmentCards(0));Assert.IsFalse(w.Issue(Rebuild(f)).accepted);Assert.IsFalse(w.Issue(Revolution(f)).accepted);
            Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{f.townId},choice="economic"}).accepted);
            if(civilization=="ottoman")
            {Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.ToggleRecruitment,actors=new[]{f.townId}}));Until(f,()=>f.Workers.Length==1,w.AutoRecruitSeconds(0)+2,"Ottoman automatic settlers resume after rebuilding");}
            else
            {Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{f.townId},item=w.WorkerDefinition(0).id}));Until(f,()=>f.Workers.Length==1,w.WorkerDefinition(0).time+2,"Paid civilian production resumes");}
            Assert.AreEqual(w.WorkerDefinition(0).id,f.Workers.Single().def.id);Assert.AreEqual(1,w.Events.Count(e=>e.kind=="RevolutionCompleted"&&e.player==0));
            Assert.AreEqual(1,w.Events.Count(e=>e.kind=="CivilianRebuilt"&&e.player==0));
        }

        [TestCase("coast")][TestCase("river")][TestCase("france")][TestCase("ottoman")]
        public void PendingRevolutionIsGlobalAndCancellationReopensSourcesWithOneRefund(string civilization)
        {
            var f=Prepared(civilization);var second=f.world.Spawn("town",0,new Vector2(75,20));var before=f.Player.resources;
            var settlerCard=DeckLibrary.Available(f.world.Rules,civilization).First(c=>!string.IsNullOrEmpty(c.unit)&&(f.world.Rules.Entity(c.unit).tags&Tags.Villager)!=0);
            f.Player.deck.Clear();f.Player.deck.Add(settlerCard.id);f.world.AwardXp(0,500,f.townId,"Explicit earned opportunity fixture");int tokens=f.Player.shipmentTokens;
            Accepted(f.world.Issue(Revolution(f)));Steps(f,5);
            Assert.IsFalse(f.world.Issue(Revolution(f,second.id)).accepted);
            Assert.IsFalse(f.world.Issue(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{second.id},choice="economic"}).accepted);
            Assert.IsFalse(f.world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{second.id},item=f.world.WorkerDefinition(0).id}).accepted);
            Assert.IsFalse(f.world.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item=settlerCard.id}).accepted,"New settler shipments close during the paid commitment");
            Assert.AreEqual(tokens,f.Player.shipmentTokens);Assert.IsEmpty(f.Player.shipments);
            Accepted(f.world.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{f.townId},index=0}));
            CostEquals(before,f.Player.resources);Assert.IsFalse(f.world.WorkerSourcesClosed(0));Assert.AreEqual(PoliticalPhase.Empire,f.Player.politicalPhase);
            Assert.IsFalse(f.world.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{f.townId},index=0}).accepted);CostEquals(before,f.Player.resources);
            Assert.AreEqual("",f.world.ShipmentReason(0,settlerCard.id),"Cancellation reopens the actual card without consuming it");
            Accepted(f.world.Issue(Revolution(f,second.id)));Until(f,()=>f.Player.politicalPhase==PoliticalPhase.Revolutionary,92,"A new legitimate commitment after cancellation completes");
            Assert.AreEqual(1,f.world.Events.Count(e=>e.kind=="RevolutionCompleted"&&e.player==0));
        }

        [Test] public void AgeAdvancementAndRevolutionExcludeOneAnotherAcrossTownCenters()
        {
            var f=Prepared();var second=f.world.Spawn("town",0,new Vector2(75,20));
            Accepted(f.world.Issue(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{f.townId},choice=f.world.AgeChoices(0,5)[0].id}));
            var paid=f.Player.resources;Assert.IsFalse(f.world.Issue(Revolution(f,second.id)).accepted);CostEquals(paid,f.Player.resources);
            Accepted(f.world.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{f.townId},index=0}));
            Accepted(f.world.Issue(Revolution(f,second.id)));Assert.AreEqual("Revolution",second.queue.Single().kind);
        }

        [Test] public void DestroyingTheCommittingTownLosesPaymentAndDoesNotConvertOrLockOtherTowns()
        {
            var f=Prepared();var second=f.world.Spawn("town",0,new Vector2(75,20));var workers=f.Workers.Select(e=>e.id).ToArray();
            Accepted(f.world.Issue(Revolution(f)));var paid=f.Player.resources;Steps(f,8);
            Accepted(f.world.Issue(new GameCommand{player=0,kind=CommandKind.Demolish,actors=new[]{f.townId}}));
            CostEquals(paid,f.Player.resources);Assert.IsFalse(f.world.WorkerSourcesClosed(0));Steps(f,95);
            Assert.AreEqual(PoliticalPhase.Empire,f.Player.politicalPhase);Assert.IsTrue(workers.All(id=>f.world.Get(id).Worker));
            Accepted(f.world.Issue(Revolution(f,second.id)));Until(f,()=>f.Player.politicalPhase==PoliticalPhase.Revolutionary,92,"Surviving town can make a new paid commitment");
        }

        [TestCase("wayfarer",4,5)][TestCase("highland",4,5)][TestCase("coast",3,5)][TestCase("coast",5,5)][TestCase("coast",4,4)]
        public void UnsupportedCivilizationsAgesAndEraCapsRejectWithoutPayment(string civilization,int age,int cap)
        {
            var f=Prepared(civilization,maxAge:cap);f.Player.age=age;var balance=f.Player.resources;
            Assert.IsFalse(f.world.Issue(Revolution(f)).accepted);CostEquals(balance,f.Player.resources);Assert.IsEmpty(f.Town.queue);
            Assert.AreEqual(PoliticalPhase.Empire,f.Player.politicalPhase);
        }

        [Test] public void ForeignIncompletePausedAndUnaffordableCommitmentsDoNotPay()
        {
            var f=Prepared();var enemy=f.world.Entities.Single(e=>e.Alive&&e.owner==1&&e.def.id=="town");var balance=f.Player.resources;
            Assert.IsFalse(f.world.Issue(Revolution(f,enemy.id)).accepted);f.Town.construction=.5f;Assert.IsFalse(f.world.Issue(Revolution(f)).accepted);f.Town.construction=1;
            f.world.Paused=true;Assert.IsFalse(f.world.Issue(Revolution(f)).accepted);f.world.Paused=false;CostEquals(balance,f.Player.resources);
            f.Player.resources=new Cost(799,800,800);Assert.IsFalse(f.world.Issue(Revolution(f)).accepted);CostEquals(new Cost(799,800,800),f.Player.resources);
            Assert.IsFalse(f.world.Issue(Rebuild(f)).accepted);Assert.IsEmpty(f.Town.queue);
        }

        [TestCase("coast")][TestCase("river")][TestCase("france")][TestCase("ottoman")]
        public void ActualCombatDamageFractionSurvivesConversionWithoutFreeHealing(string civilization)
        {
            var f=Prepared(civilization);var worker=f.world.Spawn(f.world.WorkerDefinition(0).id,0,new Vector2(45,55));
            var enemy=f.world.Spawn("villager",1,new Vector2(46.3f,55));f.world.Fog.Tick(f.world.Entities);
            Accepted(f.world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Hold}));
            Accepted(f.world.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{enemy.id},order=OrderKind.Attack,target=worker.id}));
            Until(f,()=>worker.hp<f.world.MaxHp(worker),10,"Enemy must inflict actual combat damage");
            Accepted(f.world.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{enemy.id},order=OrderKind.Move,point=new Vector2(100,55)}));
            Until(f,()=>Vector2.Distance(enemy.position,worker.position)>35,35,"Attacker must physically withdraw");
            Assert.IsTrue(worker.Alive);float ratio=worker.hp/f.world.MaxHp(worker);int id=worker.id,population=f.Player.usedPopulation;
            CompleteRevolution(f);Assert.AreSame(worker,f.world.Get(id));Assert.AreEqual(ratio,worker.hp/f.world.MaxHp(worker),.0001f);
            Assert.Less(worker.hp,f.world.MaxHp(worker));Assert.AreEqual(population,f.Player.usedPopulation);
        }

        static Vector2 SafeLand(World world,Vector2 center,float radius)
        {
            foreach(var point in world.Navigation.FormationSlots(center,512,radius*2+.4f))
            {
                if(!world.Navigation.IsWalkable(point,radius)||world.IsWater(point))continue;
                if(world.Entities.Any(e=>e.Alive&&e.garrisonedIn==0&&e.def.kind!=EntityKind.Building&&!e.def.waterborne&&Vector2.Distance(point,e.position)<radius+e.def.radius+.2f))continue;
                return point;
            }
            Assert.Fail("No body-safe fixture land point near "+center);return Vector2.zero;
        }
        static Vector2 Shore(World world,EntityState ship)
        {
            var candidates=world.Layout.LandingSites[0].ToList();
            foreach(var dock in world.Layout.DockSites[0])
            {var direction=world.Layout.WaterDirection(dock);var side=new Vector2(-direction.y,direction.x);candidates.Add(dock+side*10);candidates.Add(dock-side*10);}
            foreach(var candidate in candidates)if(world.TryTransportShore(ship,candidate,.4f,out var land,out _))return land;
            Assert.Fail("No actually navigable shore in the transport fixture");return Vector2.zero;
        }

        [TestCase("coast")][TestCase("river")][TestCase("france")][TestCase("ottoman")]
        public void ActuallyShelteredAndEmbarkedWorkersKeepContainersAndCanExitAfterColdConversion(string civilization)
        {
            var f=Prepared(civilization,MapTopology.Winding);var w=f.world;var shipDef=w.Rules.Entity("transport_ship");
            // The assembled vessel is a declared fixture input, not evidence of
            // paid naval construction; embarking and both exits are actual play.
            var ship=w.Spawn(shipDef.id,0,w.WaterNavigation.NearestWalkable(new Vector2(64,25),shipDef.radius));
            Assert.IsTrue(w.WaterNavigation.IsWalkable(ship.position,ship.def.radius));var shore=Shore(w,ship);
            var cargo=Enumerable.Range(0,2).Select(_=>w.Spawn(w.WorkerDefinition(0).id,0,SafeLand(w,shore-w.Layout.WaterDirection(shore)*8,.4f))).ToArray();
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=cargo.Select(e=>e.id).ToArray(),target=ship.id,point=shore}));
            Until(f,()=>cargo.All(e=>e.garrisonedIn==ship.id),100,"The two settlers must physically board before conversion");
            var shelter=f.Workers.First(e=>e.garrisonedIn==0);w.Fog.Tick(w.Entities);
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{shelter.id},order=OrderKind.Garrison,target=f.townId}));
            Until(f,()=>shelter.garrisonedIn==f.townId,50,"A settler must actually enter the town shelter");Assert.AreEqual(1,w.GarrisonFirepowerCount(f.Town));
            int population=f.Player.usedPopulation;var occupants=ship.occupants.ToArray();var cargoPoint=ship.position;
            CompleteRevolution(f);Assert.AreEqual(population,f.Player.usedPopulation);Assert.AreEqual(2,w.TransportLoad(ship));
            CollectionAssert.AreEqual(occupants,ship.occupants);Assert.AreEqual(0,w.GarrisonFirepowerCount(f.Town));Assert.Contains(shelter.id,f.Town.occupants);
            foreach(var passenger in cargo){Assert.AreEqual(ship.id,passenger.garrisonedIn);Assert.AreEqual(cargoPoint,passenger.position);Assert.IsTrue(passenger.def.revolutionary);}
            Assert.AreEqual(f.townId,shelter.garrisonedIn);var cold=ColdSlot(f);
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{ship.id},point=shore}));
            Accepted(cold.world.Issue(new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{ship.id},point=shore}));
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Ungarrison,actors=new[]{f.townId}}));
            Accepted(cold.world.Issue(new GameCommand{player=0,kind=CommandKind.Ungarrison,actors=new[]{f.townId}}));
            for(int i=0;i<1000&&cargo.Any(e=>e.garrisonedIn!=0);i++){Assert.IsTrue(f.simulation.Step());Assert.IsTrue(cold.simulation.Step());}
            EqualState(f,cold);Assert.IsTrue(cargo.All(e=>e.garrisonedIn==0));Assert.AreEqual(0,shelter.garrisonedIn);Assert.IsEmpty(ship.occupants);
            Assert.AreEqual(population,f.Player.usedPopulation);
            foreach(var unit in cargo.Concat(new[]{shelter}))
            {Assert.IsTrue(unit.Alive);Assert.IsTrue(unit.def.revolutionary);Assert.IsTrue(w.Navigation.IsWalkable(unit.position,unit.def.radius));Assert.IsFalse(w.IsWater(unit.position));}
        }

        [Test] public void BritishCompletedManorPromiseSurvivesConversionAndDeliversOnlyOneSoldier()
        {
            var f=Prepared();var w=f.world;var builders=f.Workers.Take(3).ToArray();Vector2 site=Vector2.zero;bool found=false;
            foreach(var point in w.Navigation.FormationSlots(new Vector2(32,30),200,2))
                if(w.CanBuild(0,"house",point)==""){site=point;found=true;break;}
            Assert.IsTrue(found,"The manor must have an actually buildable visible site");
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=builders.Select(e=>e.id).ToArray(),item="house",point=site}));
            var house=w.Entities.Last(e=>e.Alive&&e.owner==0&&e.def.id=="house");
            Until(f,()=>house.Complete,70,"Actual paid manor construction must finish");Assert.AreEqual("Gift",house.queue.Single().kind);
            // The gift is generated by actual completion and blocked only after
            // that boundary. It is never manufactured by editing a job list.
            w.Navigation.SetBlocked(house.position,15,true);int population=f.Player.usedPopulation;CompleteRevolution(f);
            Assert.AreEqual(1,house.queue.Count);Assert.AreEqual(population,f.Player.usedPopulation);var cold=ColdSlot(f);
            w.Navigation.SetBlocked(house.position,15,false);cold.world.Navigation.SetBlocked(house.position,15,false);
            PairedSteps(f,cold,3);Assert.IsEmpty(house.queue);Assert.IsEmpty(f.Workers);Assert.AreEqual(population+1,f.Player.usedPopulation);
            Assert.AreEqual(1,w.Events.Count(e=>e.kind=="BuildingComplete"&&e.entity==house.id));PairedSteps(f,cold,5);Assert.AreEqual(population+1,f.Player.usedPopulation);
        }

        [TestCase("coast")][TestCase("river")][TestCase("france")][TestCase("ottoman")]
        public void OldPaidTrainingAndWorkerShipmentDeliverSoldiersOnceAfterColdRecovery(string civilization)
        {
            var f=Prepared(civilization);var w=f.world;var second=w.Spawn("town",0,new Vector2(75,20));
            var card=DeckLibrary.Available(w.Rules,civilization).First(c=>!string.IsNullOrEmpty(c.unit)&&(w.Rules.Entity(c.unit).tags&Tags.Villager)!=0);
            f.Player.deck.Clear();f.Player.deck.Add(card.id);w.AwardXp(0,500,f.townId,"Explicit shipment opportunity fixture");
            var existing=f.Workers.Select(e=>e.id).ToArray();int trained=0;
            if(civilization!="ottoman")
            {Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{f.townId},item=w.WorkerDefinition(0).id}));trained=1;}
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item=card.id}));
            // A controlled blocked exit keeps already accepted requests pending
            // across the 90-second commitment; no worker duration is shortened.
            w.Navigation.SetBlocked(f.Town.position,15,true);Accepted(w.Issue(Revolution(f,second.id)));
            Until(f,()=>f.Player.politicalPhase==PoliticalPhase.Revolutionary,92,"Revolution completes while old arrivals remain blocked");
            Assert.AreEqual(1,f.Player.shipments.Count);Assert.AreEqual(trained,f.Player.reservedPopulation);Assert.AreEqual(trained,f.Town.queue.Count);
            Assert.IsTrue(existing.All(id=>w.Get(id).def.revolutionary));var paid=f.Player.resources;var cold=ColdSlot(f);
            w.Navigation.SetBlocked(f.Town.position,15,false);cold.world.Navigation.SetBlocked(cold.Town.position,15,false);
            PairedSteps(f,cold,5);Assert.IsEmpty(f.Town.queue);Assert.IsEmpty(f.Player.shipments);Assert.AreEqual(0,f.Player.reservedPopulation);
            Assert.AreEqual(existing.Length+trained+card.count,w.Entities.Count(e=>e.Alive&&e.owner==0&&e.def.revolutionary));Assert.IsEmpty(f.Workers);
            CostEquals(paid,f.Player.resources);Assert.IsTrue(f.Player.sentCards.Contains(card.id));Assert.AreEqual(trained,f.Player.trained);
            Assert.AreEqual(1,w.Events.Count(e=>e.player==0&&e.kind=="ShipmentArrived"));PairedSteps(f,cold,5);
        }

        [TestCase("coast")][TestCase("river")][TestCase("france")][TestCase("ottoman")]
        public void ZeroBalanceSupplyCratesCanFundPaidCivilianRebuildingThroughRealGatherOrders(string civilization)
        {
            var f=Prepared(civilization);var w=f.world;f.Player.resources=w.RevolutionDefinition(0).cost;
            w.AwardXp(0,2000,f.townId,"Explicit earned shipment opportunities fixture");CompleteRevolution(f);CostEquals(new Cost(),f.Player.resources);
            var cards=w.ActiveShipmentCards(0).Select(w.Rules.Card).ToArray();var food=cards.Single(c=>c.crates.food>0);var engineering=cards.Single(c=>c.crates.coin>0);
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item=food.id}));Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item=engineering.id}));
            Until(f,()=>f.Player.shipments.Count==0,food.travel+engineering.travel+3,"Earned supplies must physically arrive");CostEquals(new Cost(),f.Player.resources);
            var crates=w.Entities.Where(e=>e.Alive&&e.owner==0&&(e.def.id=="crate_food"||e.def.id=="crate_wood"||e.def.id=="crate_coin")).ToArray();
            Assert.AreEqual(3,crates.Length);var soldiers=w.Entities.Where(e=>e.Alive&&e.owner==0&&e.def.revolutionary).ToArray();
            for(int i=0;i<crates.Length;i++)Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{soldiers[i].id},order=OrderKind.Gather,target=crates[i].id}));
            var tech=w.Rules.Tech(World.CivilianRebuildingTechnology);
            Until(f,()=>w.CanAfford(0,tech.cost),100,"Revolution infantry must actually collect enough own food and coin crates");
            Assert.GreaterOrEqual(f.Player.gathered.food,tech.cost.food-.002f);Assert.GreaterOrEqual(f.Player.gathered.coin,tech.cost.coin-.002f);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Gather"&&soldiers.Any(s=>s.id==e.entity)));Assert.IsTrue(soldiers.All(s=>!s.Worker));
            var cold=ColdSlot(f);Accepted(w.Issue(Rebuild(f)));Accepted(cold.world.Issue(Rebuild(cold)));PairedSteps(f,cold,tech.time+2);
            Assert.AreEqual(PoliticalPhase.CivilianRebuilt,f.Player.politicalPhase);Assert.IsEmpty(f.Workers);Assert.IsTrue(soldiers.All(s=>s.def.revolutionary));
        }

        [Test] public void RevolutionaryCollectionRejectsNaturalEconomyEnemyCratesConstructionAndRepair()
        {
            var f=Prepared();CompleteRevolution(f);var w=f.world;var soldier=w.Entities.First(e=>e.Alive&&e.owner==0&&e.def.revolutionary);
            var targets=new[]{w.Spawn("tree",-1,new Vector2(35,20)),w.Spawn("mine",-1,new Vector2(36,23)),w.Spawn("berry",-1,new Vector2(36,26)),
                w.Spawn("farm",0,new Vector2(44,28)),w.Spawn("estate",0,new Vector2(44,38)),w.Spawn("crate_food",1,new Vector2(28,23)),w.Spawn("crate_coin",-1,new Vector2(28,26))};
            w.Fog.Tick(w.Entities);var balance=f.Player.resources;
            foreach(var target in targets)
            {Assert.IsFalse(w.RevolutionaryCanCollect(soldier,target));Assert.IsFalse(w.CanGather(soldier,target));Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{soldier.id},order=OrderKind.Gather,target=target.id}).accepted,target.def.id);}
            Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{soldier.id},item="house",point=new Vector2(34,34)}).accepted);
            Assert.IsFalse(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{soldier.id},order=OrderKind.Repair,target=f.townId}).accepted);
            Steps(f,2);CostEquals(balance,f.Player.resources);Assert.IsEmpty(soldier.orders);Assert.IsFalse(soldier.Worker);
        }

        [TestCase("commitment")][TestCase("revolution")][TestCase("rebuilding")][TestCase("rebuilt")]
        public void ActualSaveSlotPreservesEachLifecycleBoundaryAndDeterministicContinuation(string boundary)
        {
            var f=Prepared("france");Accepted(f.world.Issue(Revolution(f)));Steps(f,7);
            if(boundary!="commitment")Until(f,()=>f.Player.politicalPhase==PoliticalPhase.Revolutionary,92,"Complete revolution");
            if(boundary=="rebuilding"||boundary=="rebuilt")
            {Accepted(f.world.Issue(Rebuild(f)));Steps(f,8);if(boundary=="rebuilt")Until(f,()=>f.Player.politicalPhase==PoliticalPhase.CivilianRebuilt,62,"Complete rebuilding");}
            var cold=ColdSlot(f);PairedSteps(f,cold,95);
            Assert.AreEqual(boundary=="commitment"||boundary=="revolution"?PoliticalPhase.Revolutionary:PoliticalPhase.CivilianRebuilt,f.Player.politicalPhase);
            Assert.AreEqual(1,f.world.Events.Count(e=>e.player==0&&e.kind=="RevolutionCompleted"));
        }

        [Test] public void ExistingPaidCivilianTechnologyContinuesAlongsidePoliticalCommitmentAfterColdRead()
        {
            var f=Prepared();var market=f.world.Spawn("market",0,new Vector2(52,20));
            var tech=f.world.Rules.techs.First(t=>t.building=="market"&&!t.requiresRevolution&&f.world.ProductionReason(0,market,t.id,true)=="");
            var balance=f.Player.resources;Accepted(f.world.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{market.id},item=tech.id}));
            CostEquals(Minus(balance,tech.cost),f.Player.resources);Accepted(f.world.Issue(Revolution(f)));Steps(f,2);
            Assert.IsFalse(f.Player.technologies.Contains(tech.id));var cold=ColdSlot(f);PairedSteps(f,cold,95);
            Assert.IsTrue(f.Player.technologies.Contains(tech.id));Assert.AreEqual(PoliticalPhase.Revolutionary,f.Player.politicalPhase);
            Assert.AreEqual(1,f.world.Events.Count(e=>e.kind=="ResearchComplete"&&e.entity==market.id));
            Assert.IsFalse(f.world.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{market.id},item=tech.id}).accepted);
        }

        [Test] public void RebuildingCancellationRefundsOnceAndDestroyedResearchRequiresAnotherPayment()
        {
            var f=Prepared();var second=f.world.Spawn("town",0,new Vector2(75,20));CompleteRevolution(f);var balance=f.Player.resources;
            Accepted(f.world.Issue(Rebuild(f)));Steps(f,8);
            Assert.IsFalse(f.world.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{second.id},item=World.CivilianRebuildingTechnology}).accepted);
            Accepted(f.world.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{f.townId},index=0}));CostEquals(balance,f.Player.resources);
            Assert.IsFalse(f.world.Issue(new GameCommand{player=0,kind=CommandKind.CancelQueue,actors=new[]{f.townId},index=0}).accepted);CostEquals(balance,f.Player.resources);
            Assert.AreEqual(PoliticalPhase.Revolutionary,f.Player.politicalPhase);Assert.IsTrue(f.world.WorkerSourcesClosed(0));
            Accepted(f.world.Issue(Rebuild(f)));var paid=f.Player.resources;Steps(f,8);Accepted(f.world.Issue(new GameCommand{player=0,kind=CommandKind.Demolish,actors=new[]{f.townId}}));
            Steps(f,65);CostEquals(paid,f.Player.resources);Assert.AreEqual(PoliticalPhase.Revolutionary,f.Player.politicalPhase);
            Assert.IsFalse(f.Player.technologies.Contains(World.CivilianRebuildingTechnology));f.townId=second.id;
            Accepted(f.world.Issue(Rebuild(f)));var cold=ColdSlot(f);PairedSteps(f,cold,62);Assert.AreEqual(PoliticalPhase.CivilianRebuilt,f.Player.politicalPhase);
            Assert.AreEqual(1,f.world.Events.Count(e=>e.kind=="CivilianRebuilt"&&e.player==0));
        }

        [TestCase("phase")][TestCase("identity")][TestCase("missingIdentity")][TestCase("age")][TestCase("empireReceipt")]
        [TestCase("futureRevolution")][TestCase("nanClock")][TestCase("earlyRebuildingReceipt")][TestCase("unpaidRebuildingTechnology")][TestCase("livingWorker")][TestCase("population")][TestCase("reservation")]
        public void TamperedCompletedPhaseCannotEnterColdGameplay(string tamper)
        {
            var f=Prepared();CompleteRevolution(f);var s=f.world.CaptureState();var p=s.players[0].state;
            switch(tamper)
            {
                case "phase":p.politicalPhase=(PoliticalPhase)99;break;case "identity":p.revolutionId="rev_france";break;case "missingIdentity":p.revolutionId="";break;
                case "age":p.age=5;break;case "empireReceipt":p.politicalPhase=PoliticalPhase.Empire;break;case "futureRevolution":p.revolutionAt=s.time+1;break;
                case "nanClock":p.revolutionAt=float.NaN;break;case "earlyRebuildingReceipt":p.civilianRebuiltAt=s.time;break;
                case "unpaidRebuildingTechnology":s.players[0].technologies=s.players[0].technologies.Concat(new[]{World.CivilianRebuildingTechnology}).ToArray();break;
                case "livingWorker":s.entities.First(e=>e.state.Alive&&e.state.owner==0&&e.definition=="revolutionary").definition="villager";break;
                case "population":p.usedPopulation++;break;case "reservation":p.reservedPopulation++;break;
            }
            Assert.Throws<ArgumentException>(()=>World.Restore(f.world.Rules,s),tamper);
        }

        [TestCase(PoliticalPhase.Empire,"Train")][TestCase(PoliticalPhase.Empire,"Gift")][TestCase(PoliticalPhase.Empire,"Reward")]
        [TestCase(PoliticalPhase.Revolutionary,"Train")][TestCase(PoliticalPhase.Revolutionary,"Gift")][TestCase(PoliticalPhase.Revolutionary,"Reward")]
        [TestCase(PoliticalPhase.CivilianRebuilt,"Train")][TestCase(PoliticalPhase.CivilianRebuilt,"Gift")][TestCase(PoliticalPhase.CivilianRebuilt,"Reward")]
        public void ForgedDirectRevolutionaryOutputCannotEnterAnyColdPoliticalPhase(PoliticalPhase phase,string source)
        {
            var f=Prepared();
            if(phase!=PoliticalPhase.Empire)CompleteRevolution(f);
            if(phase==PoliticalPhase.CivilianRebuilt)
            {Accepted(f.world.Issue(Rebuild(f)));Until(f,()=>f.Player.politicalPhase==phase,62,"Paid civilian rebuilding must complete");}
            var snapshot=f.world.CaptureState();
            Assert.DoesNotThrow(()=>World.Restore(f.world.Rules,snapshot),"The real untouched boundary must be restorable");
            string soldier=f.world.RevolutionDefinition(0).unit;
            if(source=="Reward")
                snapshot.players[0].rewards=snapshot.players[0].rewards.Concat(new[]{new RewardJob{unit=soldier,count=1}}).ToArray();
            else
            {
                var town=snapshot.entities.Single(e=>e.state.id==f.townId);
                town.queue=town.queue.Concat(new[]{new ProductionJob{kind=source,item=soldier,count=1,population=1,duration=0,paid=new Cost()}}).ToArray();
                // Keep the forged reservation consistent so rejection cannot be
                // attributed to the unrelated completed-phase population check.
                snapshot.players[0].state.reservedPopulation++;
            }
            string directory=Path.Combine(Path.GetTempPath(),"frontier-revolution071-forged-"+Guid.NewGuid().ToString("N"));
            try
            {
                var malicious=new SavedMatch{matchId=Guid.NewGuid().ToString("N"),appVersion="revolution071-test",world=snapshot,simulation=f.simulation.CaptureState(),
                    selection=Array.Empty<int>(),groups=Array.Empty<ControlGroupSnapshot>(),cameraPosition=new Vector3(20,40,20),cameraFocus=new Vector2(20,20),cameraSize=26,simulationSpeed=1};
                string slot=MatchSaveStore.SlotPath(directory,2);MatchSaveStore.Write(slot,malicious);
                var loaded=MatchSaveStore.Read(slot,"revolution071-test");
                var error=Assert.Throws<ArgumentException>(()=>World.Restore(f.world.Rules,loaded.world),phase+" "+source);
                StringAssert.Contains("direct revolutionary",error.Message,"Reject this forbidden output before any cold simulation can mint a unit");
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }

        [TestCase("coast")][TestCase("river")][TestCase("france")][TestCase("ottoman")]
        public void AcceptedRevolutionaryReserveShipmentRemainsLegalAcrossColdRecovery(string civilization)
        {
            var f=Prepared(civilization);CompleteRevolution(f);var w=f.world;
            w.AwardXp(0,w.Rules.initialXp+w.Rules.xpIncrease*f.Player.xpLevels,f.townId,"Declared earned reserve shipment opportunity");
            var card=w.ActiveShipmentCards(0).Select(w.Rules.Card).Single(c=>w.Rules.Entity(c.unit)?.revolutionary==true);
            int population=f.Player.usedPopulation;
            Accepted(w.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item=card.id}));Steps(f,card.travel*.5f);
            Assert.AreEqual(1,f.Player.shipments.Count);Assert.AreEqual(population,f.Player.usedPopulation);
            var cold=ColdSlot(f);PairedSteps(f,cold,card.travel+2);
            Assert.IsEmpty(f.Player.shipments);Assert.AreEqual(population+card.count,f.Player.usedPopulation);
            Assert.AreEqual(1,w.Events.Count(e=>e.player==0&&e.kind=="ShipmentArrived"));
            PairedSteps(f,cold,4);Assert.AreEqual(population+card.count,f.Player.usedPopulation);
        }

        [TestCase("secondCommitment")][TestCase("paid")][TestCase("duration")][TestCase("item")]
        public void TamperedPendingPoliticalJobCannotResume(string tamper)
        {
            var f=Prepared();var second=f.world.Spawn("town",0,new Vector2(75,20));Accepted(f.world.Issue(Revolution(f)));Steps(f,3);
            var s=f.world.CaptureState();var job=s.entities.Single(e=>e.state.id==f.townId).queue.Single();
            if(tamper=="secondCommitment")s.entities.Single(e=>e.state.id==second.id).queue=new[]{SnapshotCodec.Clone(job)};
            else if(tamper=="paid")job.paid.food-=1;else if(tamper=="duration")job.duration+=1;else job.item="rev_france";
            Assert.Throws<ArgumentException>(()=>World.Restore(f.world.Rules,s),tamper);
        }
    }

    // This covers the in-process host authority with a four-player human seat;
    // it is not evidence of a session between two physical Macs.
    public sealed class RevolutionLan071Tests
    {
        static LanCommandRequest Request(long sequence,GameCommand command,int generation=1,long queueId=0)
            =>new LanCommandRequest{matchId="rev-match",epoch="rev-epoch",peerId="remote",generation=generation,sequence=sequence,operation=LanOperation.Command,command=command,expectedQueueId=queueId};
        [Test] public void HumanSeatTwoRejectsSpoofAndPaysOnceAcrossRetryReconnectAndStableCancellation()
        {
            var rules=Catalog.Load();var world=new World(rules,314159,new[]{"coast","river","france","ottoman"},new[]{0,1,0,1},5,MapTopology.GeneratedRiver);
            world.Players[2].age=4;world.Players[2].resources=new Cost(5000,5000,5000);var town=world.Entities.Single(e=>e.Alive&&e.owner==2&&e.def.id=="town");
            var simulation=new MatchSimulation(world);using(var recorder=new ReplayRecorder(simulation,ReplayIdentity.Create(rules,"0.14.0","revolution-LAN-test","test-engine"),segmentId:"revolution-authority"))
            {
                int applied=0,cancellations=0;const long stableQueueId=71001;ProductionJob committed=null;
                var authority=new LanAuthority(world,"rev-match","rev-epoch",c=>{applied++;var result=recorder.Issue(c);if(result.accepted&&c.kind==CommandKind.Revolution)committed=town.queue.Last();return result;},
                    (paused,reason)=>recorder.Pause(paused,reason),(command,id)=>
                    {cancellations++;if(id!=stableQueueId||!town.queue.Contains(committed))return CommandResult.Fail("Queue entry expired");command.index=town.queue.IndexOf(committed);applied++;return recorder.Issue(command);});
                authority.BindPeer("host",0,1);authority.BindPeer("remote",2,1);
                var before=world.Players[2].resources;var other=world.Players[0].resources;var cost=world.RevolutionDefinition(2).cost;
                var spoof=new GameCommand{player=0,kind=CommandKind.Revolution,actors=new[]{town.id}};
                Assert.IsTrue(authority.Submit(Request(1,spoof)).protocolRejected);Assert.IsEmpty(authority.Drain(0,1));Assert.AreEqual(0,applied);
                var command=new GameCommand{player=2,kind=CommandKind.Revolution,actors=new[]{town.id}};var first=authority.Submit(Request(2,command));
                var repeated=authority.Submit(Request(2,SnapshotCodec.Clone(command)));Assert.AreEqual(first.hostSequence,repeated.hostSequence);Assert.AreEqual(1,authority.PendingCount);
                authority.RetirePeer("remote",1);authority.BindPeer("remote",2,2);var receipt=authority.Drain(0,2).Single();Assert.IsTrue(receipt.accepted,receipt.reason);
                Assert.AreEqual(2,receipt.generation);Assert.AreEqual(1,applied);Assert.AreEqual(1,town.queue.Count);
                Assert.AreEqual(before.food-cost.food,world.Players[2].resources.food,.001f);Assert.AreEqual(before.wood-cost.wood,world.Players[2].resources.wood,.001f);Assert.AreEqual(before.coin-cost.coin,world.Players[2].resources.coin,.001f);
                Assert.AreEqual(other,world.Players[0].resources);Assert.IsTrue(authority.Submit(Request(2,SnapshotCodec.Clone(command),2)).accepted);Assert.IsEmpty(authority.Drain(0,3));Assert.AreEqual(1,applied);
                var cancel=new GameCommand{player=2,kind=CommandKind.CancelQueue,actors=new[]{town.id},index=5};
                authority.Submit(Request(3,cancel,2,stableQueueId));authority.RetirePeer("remote",2);var refunded=authority.Drain(0,4).Single();Assert.IsTrue(refunded.accepted,refunded.reason);
                Assert.AreEqual(before,world.Players[2].resources);Assert.IsEmpty(town.queue);Assert.IsFalse(world.WorkerSourcesClosed(2));Assert.AreEqual(1,cancellations);
                authority.BindPeer("remote",2,3);Assert.IsTrue(authority.Submit(Request(3,SnapshotCodec.Clone(cancel),3,stableQueueId)).accepted);Assert.IsEmpty(authority.Drain(0,5));Assert.AreEqual(1,cancellations);Assert.AreEqual(2,applied);
                Assert.AreEqual(5,cancel.index,"The callback corrects its owned copy, not the caller payload");Assert.AreEqual(1,recorder.Capture().records.Count(r=>r.hasCommand&&r.command.kind==CommandKind.Revolution));
                Assert.AreEqual(1,recorder.Capture().records.Count(r=>r.hasCommand&&r.command.kind==CommandKind.CancelQueue));
            }
        }
    }
}
