using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Funded mechanics fixtures. The boat is ALWAYS paid, built and trained via
    // public commands. This is not normal-opening economic acceptance evidence.
    public sealed class AiBridgeTransport084Tests
    {
        sealed class Arena
        {
            public World w;public AiArmyPlan072 army;public AiBridgeTransport084 bridge;
            public AiWaterEconomy water;public AiSearchPlan search;public AiRevolution071 revolution;public AiCivilizationEconomy civ;public AiMedicalSupport076 medical;
            public EntityState ship;public Vector2 boarding,berth,landing,landingWater,destination;
            public Arena(bool paidBoat=true)
            {
                w=new World(SnapshotCodec.Clone(Catalog.Load()),314159,"coast","coast",5,MapTopology.Estuary);w.Players[0].age=2;w.Players[0].resources=new Cost(9000,9000,9000);
                foreach(var e in w.Entities.Where(e=>e.owner!=0&&e.def.kind==EntityKind.Unit).ToArray())w.Remove(e);
                // The house is declared fixture capacity; the dock/ship below
                // must still consume their actual prices and production time.
                w.Spawn("house",0,Safe(w,w.OpeningTownPosition(0)+new Vector2(-7,14),.4f));
                if(paidBoat)PayBoat();
                Initialize();
                ReviewArmy();Assert.IsTrue(army.CaptureState().hasStage);
                foreach(var p in w.Navigation.FormationSlots(army.RallyPoint,8,2.1f))w.Spawn("musketeer",0,p);
                w.Fog.Tick(w.Entities);
                for(int i=0;i<120&&army.Phase!=AiArmyPhase072.March;i++){ReviewArmy();w.Tick(.1f);}
                Assert.AreEqual(AiArmyPhase072.March,army.Phase,"Eight fixture soldiers must actually assemble before forming wave/guards");
                Assert.AreEqual(6,army.WaveMembers.Length);Assert.AreEqual(2,army.GuardMembers.Length);
                destination=army.Destination;
                if(ship!=null)
                {
                    bool found=false;foreach(var p in w.Layout.LandingSites.SelectMany(x=>x))
                        if(w.TryTransportShore(ship,p,.4f,out boarding,out berth)){found=true;break;}
                    Assert.IsTrue(found);
                    found=false;foreach(var p in w.Layout.LandingSites.SelectMany(x=>x))
                        if(w.TryTransportShore(ship,p,.4f,out var land,out var wet)&&(land-boarding).sqrMagnitude>144)
                        {landing=land;landingWater=wet;found=true;break;}
                    Assert.IsTrue(found,"Different exact bridge shores required");
                }
            }
            public void Initialize()
            {
                var difficulty=AiDifficultyProfile.From(w.Rules,"standard");army=new AiArmyPlan072(w,0,difficulty);water=new AiWaterEconomy(w,0);search=new AiSearchPlan(w,0,w.OpeningTownPosition(1));
                revolution=new AiRevolution071(w,0,difficulty);civ=new AiCivilizationEconomy(w,0);medical=new AiMedicalSupport076(w,0);bridge=new AiBridgeTransport084(w,0,army,revolution,medical);army.BindTransport(bridge.Owns);
            }
            public List<EntityState> Own()=>w.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
            public void ReviewArmy()
            {var own=Own();army.Review(own,own.Where(e=>e.Military&&!e.def.waterborne).ToList(),new List<EntityState>(),w.OpeningTownPosition(1),true,search,water,revolution,civ,medical);}
            public void PayBoat()
            {
                var builder=w.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker);EntityState dock=null;
                foreach(var p in w.Layout.DockSites[0])
                {
                    var survey=w.Navigation.NearestWalkable(p-w.DockWaterDirection(p)*4,builder.def.radius);
                    Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{builder.id},order=OrderKind.Move,point=survey}).accepted);
                    Until(w,()=>w.Fog.At(0,p)==VisibilityState.Visible,100,"paid dock survey");if(w.CanBuild(0,"dock",p)!="")continue;
                    var before=w.Players[0].spent;
                    Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{builder.id},item="dock",point=p}).accepted);
                    Assert.AreEqual(before.wood+w.EntityCost(0,w.Rules.Entity("dock")).wood,w.Players[0].spent.wood,.001f);
                    dock=w.Entities.Last(e=>e.Alive&&e.owner==0&&e.def.id=="dock");Until(w,()=>dock.Complete,150,"paid dock construction");break;
                }
                Assert.IsNotNull(dock);var spent=w.Players[0].spent;var cost=w.UnitCost(0,w.Rules.Entity("transport_ship"));
                Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{dock.id},item="transport_ship"}).accepted);
                Assert.AreEqual(spent.wood+cost.wood,w.Players[0].spent.wood,.001f);Assert.AreEqual(spent.coin+cost.coin,w.Players[0].spent.coin,.001f);
                Assert.AreEqual(w.Rules.Entity("transport_ship").population,w.Players[0].reservedPopulation);
                Until(w,()=>w.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.transportShip),65,"paid normal ship queue");ship=w.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.transportShip);
                Assert.AreEqual(0,w.Players[0].reservedPopulation);Assert.IsTrue(w.Events.Any(e=>e.kind=="Payment"&&e.entity==dock.id));
            }
            public void CommitPublicFixture()
            {
                var ids=army.WaveMembers.Take(4).ToArray();
                Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=ids,target=ship.id,point=boarding}).accepted);
                var order=ship.orders.Peek();boarding=order.point;berth=order.resolvedPoint;
                bridge.Restore(null,new AiBridgeMission084{ship=ship.id,members=ids,originalCount=ids.Length,startedAt=w.Time,destination=destination,
                    boardingShore=boarding,boardingWater=berth,landingShore=landing,landingWater=landingWater,directSeconds=200,transportSeconds=100},AiBridgeReview084.Committed,0);
                bridge.ValidateOwnership();
            }
            public void Step(float seconds,bool danger=false)
            {for(int i=0;i<Mathf.CeilToInt(seconds*10);i++){bridge.Tick(Own(),new List<EntityState>(),danger);ReviewArmy();w.Tick(.1f);}}
        }
        static Vector2 Safe(World w,Vector2 center,float radius)
        {
            foreach(var p in w.Navigation.FormationSlots(center,512,radius*2+.5f))
                if(w.Navigation.IsWalkable(p,radius)&&!w.IsWater(p)&&!w.Entities.Any(e=>e.Alive&&e.garrisonedIn==0&&e.def.kind==EntityKind.Unit&&(e.position-p).sqrMagnitude<Mathf.Pow(e.def.radius+radius+.2f,2)))return p;
            Assert.Fail("No body-safe fixture position");return Vector2.zero;
        }
        static void Until(World w,Func<bool> test,float seconds,string reason)
        {for(int i=0;i<Mathf.CeilToInt(seconds*10)&&!test();i++)w.Tick(.1f);Assert.IsTrue(test(),reason+" t="+w.Time);}
        [Test] public void BridgeNativeQueriesKeepLiveWorldAndTransientContextUnchanged()
        {
            var a=new Arena(false);var e=a.w.Get(a.army.WaveMembers[0]);var before=JsonUtility.ToJson(a.w.CaptureState());
            Assert.IsFalse(a.w.Navigation.IsWalkable(a.army.Destination,e.def.radius),"The ordinary enemy town center is occupied");
            Assert.IsTrue(a.w.TryMoveEndpoint084(e,e.position,e.orders.Peek().point,out var actualEndpoint,true));
            bool route=a.w.TryMovementRoute084(e,e.position,actualEndpoint,out var distance,true);
            Assert.IsTrue(route);Assert.Greater(distance,0);Assert.IsTrue(a.w.MovementConnector084(e,e.position,e.position,true));
            Assert.IsFalse(a.w.TryMovementRoute084(e,e.position,new Vector2(-10,-10),out _));
            Assert.AreEqual(before,JsonUtility.ToJson(a.w.CaptureState()),"Queries must not rewrite commands, paths, transit, events, IDs or economy");
        }
        [Test] public void NoPaidBoatCannotCreateFirstInvestmentOrStopRealWave()
        {
            var a=new Arena(false);var spent=JsonUtility.ToJson(a.w.Players[0].spent);var commands=a.army.WaveMembers.Select(id=>JsonUtility.ToJson(a.w.Get(id).orders.Peek())).ToArray();
            a.bridge.Tick(a.Own(),new List<EntityState>(),false);
            Assert.AreEqual(AiBridgeReview084.NoPaidBoat,a.bridge.LastReview);Assert.IsNull(a.bridge.Mission);Assert.AreEqual(spent,JsonUtility.ToJson(a.w.Players[0].spent));
            CollectionAssert.AreEqual(commands,a.army.WaveMembers.Select(id=>JsonUtility.ToJson(a.w.Get(id).orders.Peek())).ToArray());Assert.IsFalse(a.Own().Any(e=>e.def.id=="dock"));
        }
        [Test] public void CompleteRouteSurveyDefersAfterSixNativeQueriesAndPersistsCursor()
        {
            var a=new Arena();Assert.IsFalse(a.w.Navigation.IsWalkable(a.army.Destination,.4f),"Use the true ordinary enemy town intent without manually projecting it");
            a.bridge.Tick(a.Own(),new List<EntityState>(),false);
            Assert.LessOrEqual(a.bridge.LastRouteQueries,6);Assert.AreEqual(6,a.bridge.LastRouteQueries,"The first six direct routes must complete before deferral");
            Assert.AreEqual(AiBridgeReview084.Deferred,a.bridge.LastReview);Assert.IsNotNull(a.bridge.Candidate);Assert.AreEqual(6,a.bridge.Candidate.cursor);Assert.IsNull(a.bridge.Mission);
            var copied=SnapshotCodec.Clone(a.bridge.Candidate);var bridge=new AiBridgeTransport084(a.w,0,a.army,a.revolution,null);bridge.Restore(copied,null,a.bridge.LastReview,a.bridge.RetryAt);
            Assert.AreEqual(JsonUtility.ToJson(copied),JsonUtility.ToJson(bridge.Candidate));
            copied.cursor=0;Assert.AreEqual(6,bridge.Candidate.cursor,"Restore must own its DTO arrays and values");
        }
        [Test] public void ShortCompleteLandRouteRejectsOptionalTurnaroundWithoutIssuingEmbarkOrSpending()
        {
            var a=new Arena();var state=a.army.CaptureState();state.destination=a.w.Get(a.army.WaveMembers[0]).position;a.army.RestoreState(state);
            var before=a.w.Events.Count;var spent=JsonUtility.ToJson(a.w.Players[0].spent);int queries=0;
            // No World.Tick here: this isolates route choice at one actual
            // world state, without inventing a traffic-delay bonus.
            for(int i=0;i<5;i++){a.bridge.Tick(a.Own(),new List<EntityState>(),false);Assert.LessOrEqual(a.bridge.LastRouteQueries,6);queries+=a.bridge.LastRouteQueries;}
            Assert.GreaterOrEqual(queries,20,"A complete first candidate must be proved, including continuation and real water route");
            Assert.IsNull(a.bridge.Mission);Assert.IsFalse(a.w.Events.Skip(before).Any(e=>e.kind=="EmbarkOrdered"));Assert.AreEqual(spent,JsonUtility.ToJson(a.w.Players[0].spent));
            Assert.IsTrue(a.army.WaveMembers.All(id=>a.w.Get(id).orders.Count>0&&a.w.Get(id).orders.Peek().kind==OrderKind.AttackMove));
        }
        [Test] public void ActualPublicBoardingAndAboardPassengersRemainOriginalWaveAndGuardsKeepOrders()
        {
            var a=new Arena();a.CommitPublicFixture();var members=a.bridge.Mission.members.ToArray();var wave=a.army.WaveMembers;var guards=a.army.GuardMembers;
            var guardOrders=guards.Select(id=>JsonUtility.ToJson(a.w.Get(id).orders.ToArray())).ToArray();
            for(int i=0;i<1600&&!members.All(id=>a.w.Get(id).garrisonedIn==a.ship.id);i++){a.bridge.Tick(a.Own(),new List<EntityState>(),false);a.ReviewArmy();a.w.Tick(.1f);}
            Assert.IsTrue(members.All(id=>a.w.Get(id).garrisonedIn==a.ship.id),"Original soldiers must physically walk and board the paid boat");
            CollectionAssert.AreEqual(wave,a.army.WaveMembers);CollectionAssert.AreEqual(guards,a.army.GuardMembers);Assert.AreEqual(AiArmyPhase072.March,a.army.Phase);
            CollectionAssert.AreEqual(guardOrders,guards.Select(id=>JsonUtility.ToJson(a.w.Get(id).orders.ToArray())).ToArray());
        }
        [TestCase(1)][TestCase(2)][TestCase(3)]
        public void BoardingSailingAndExactReturnColdRestoreKeepActualWorldCommandsAndWave(int phase)
        {
            var a=new Arena();a.CommitPublicFixture();
            if(phase>=2)for(int i=0;i<1800&&a.bridge.Mission!=null&&a.bridge.Mission.phase==1;i++)a.Step(.1f);
            Assert.IsNotNull(a.bridge.Mission);
            if(phase==3){a.bridge.Tick(a.Own(),new List<EntityState>(),true);Assert.AreEqual(3,a.bridge.Mission.phase);}
            Assert.AreEqual(phase,a.bridge.Mission.phase);
            var worldState=SnapshotCodec.Clone(a.w.CaptureState());var armyState=SnapshotCodec.Clone(a.army.CaptureState());
            var candidate=a.bridge.Candidate==null?null:SnapshotCodec.Clone(a.bridge.Candidate);var mission=SnapshotCodec.Clone(a.bridge.Mission);var review=a.bridge.LastReview;float retry=a.bridge.RetryAt;
            var cold=World.Restore(a.w.Rules,worldState);var profile=AiDifficultyProfile.From(cold.Rules,"standard");var army=new AiArmyPlan072(cold,0,profile);army.RestoreState(armyState);
            var bridge=new AiBridgeTransport084(cold,0,army,new AiRevolution071(cold,0,profile),null);army.BindTransport(bridge.Owns);bridge.Restore(candidate,mission,review,retry);bridge.ValidateOwnership();
            // Limit this comparison to native naval movement. Army's saved
            // ownership is checked above and normal army cold tests cover search.
            for(int i=0;i<80;i++)
            {
                a.bridge.Tick(a.Own(),new List<EntityState>(),phase==3);bridge.Tick(cold.Entities.Where(e=>e.Alive&&e.owner==0).ToList(),new List<EntityState>(),phase==3);
                a.w.Tick(.1f);cold.Tick(.1f);
            }
            Assert.AreEqual(JsonUtility.ToJson(a.bridge.Mission),JsonUtility.ToJson(bridge.Mission));
            Assert.AreEqual(JsonUtility.ToJson(a.w.CaptureState()),JsonUtility.ToJson(cold.CaptureState()),"Same public transport commands, movement, cargo, economy and events after cold restore");
        }
        [Test] public void OriginalPassengersActuallyLandThenResumeSavedPublicDestination()
        {
            var a=new Arena();a.CommitPublicFixture();var members=a.bridge.Mission.members.ToArray();var destination=a.bridge.Mission.destination;
            for(int i=0;i<3600&&a.bridge.Mission!=null;i++){a.bridge.Tick(a.Own(),new List<EntityState>(),false);a.w.Tick(.1f);}
            Assert.IsNull(a.bridge.Mission);var complete=a.w.Events.SingleOrDefault(e=>e.kind=="AIBridgeTransportCompleted");Assert.IsNotNull(complete,"Completion requires actual Disembarked events");
            Assert.AreEqual(4,complete.amount);Assert.AreEqual(4,complete.target);
            foreach(int id in members)
            {
                var e=a.w.Get(id);Assert.IsTrue(e.Alive);Assert.AreEqual(0,e.garrisonedIn);Assert.IsTrue(a.w.Events.Any(ev=>ev.kind=="Disembarked"&&ev.entity==id&&ev.target==a.ship.id));
                Assert.IsTrue(e.orders.Count>0);Assert.AreEqual(OrderKind.AttackMove,e.orders.Peek().kind);Assert.Less((destination-e.orders.Peek().point).sqrMagnitude,144,"Ordinary formation offsets remain allowed");
            }
        }
        [Test] public void RestoreRejectsActuallyPendingGuardButAcceptsReleasedMemberJoiningGuards()
        {
            var a=new Arena();a.CommitPublicFixture();var original=SnapshotCodec.Clone(a.bridge.Mission);var state=SnapshotCodec.Clone(original);int guard=a.army.GuardMembers[0];state.members[0]=guard;
            Assert.IsTrue(a.w.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=new[]{guard},target=a.ship.id,point=a.boarding}).accepted);
            a.bridge.Restore(null,state,AiBridgeReview084.Committed,0);Assert.IsTrue(a.bridge.Owns(guard));Assert.Throws<ArgumentException>(()=>a.bridge.ValidateOwnership());
            Assert.IsTrue(a.w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{guard},order=OrderKind.Stop}).accepted);
            int released=original.members[0];Assert.IsTrue(a.w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{released},order=OrderKind.Stop}).accepted);
            var army=a.army.CaptureState();army.wave=army.wave.Where(id=>id!=released).ToArray();army.guards=army.guards.Concat(new[]{released}).ToArray();a.army.RestoreState(army);
            a.bridge.Restore(null,original,AiBridgeReview084.Committed,0);a.bridge.ValidateOwnership();Assert.IsFalse(a.bridge.Owns(released),"Provenance alone cannot reserve a real cancelled passenger");
        }
        [Test] public void AboardSurvivorKeepsWaveAfterRealLossesAndRestoresLegally()
        {
            var a=new Arena();a.CommitPublicFixture();var original=a.bridge.Mission.members.ToArray();
            for(int i=0;i<1600&&!original.All(id=>a.w.Get(id).garrisonedIn==a.ship.id);i++){a.bridge.Tick(a.Own(),new List<EntityState>(),false);a.ReviewArmy();a.w.Tick(.1f);}
            Assert.IsTrue(original.All(id=>a.w.Get(id).garrisonedIn==a.ship.id));int survivor=original[0];
            foreach(int id in a.army.WaveMembers)if(id!=survivor)a.w.Remove(a.w.Get(id));
            var state=a.army.CaptureState();state.reviewAt=0;a.army.RestoreState(state);a.ReviewArmy();
            Assert.AreEqual(AiArmyPhase072.March,a.army.Phase);CollectionAssert.AreEqual(new[]{survivor},a.army.WaveMembers);a.bridge.ValidateOwnership();
            var cold=World.Restore(a.w.Rules,SnapshotCodec.Clone(a.w.CaptureState()));var army=new AiArmyPlan072(cold,0,AiDifficultyProfile.From(cold.Rules,"standard"));army.RestoreState(SnapshotCodec.Clone(a.army.CaptureState()));
            var bridge=new AiBridgeTransport084(cold,0,army,null,null);army.BindTransport(bridge.Owns);bridge.Restore(null,SnapshotCodec.Clone(a.bridge.Mission),a.bridge.LastReview,a.bridge.RetryAt);bridge.ValidateOwnership();Assert.IsTrue(bridge.Owns(survivor));
        }
        [TestCase("tower",1f)][TestCase("town",1f)][TestCase("tower",.5f)]
        public void VisibleArmedBuildingAtEvaluatedShoreReturnsActualCargo(string building,float hpFraction)
        {
            var a=new Arena();a.CommitPublicFixture();var ids=a.bridge.Mission.members.ToArray();
            for(int i=0;i<1800&&a.bridge.Mission!=null&&a.bridge.Mission.phase==1;i++)a.Step(.1f);
            Assert.IsNotNull(a.bridge.Mission);Assert.IsTrue(ids.Any(id=>a.w.Get(id).garrisonedIn==a.ship.id));
            var definition=a.w.Rules.Entity(building);Assert.IsNotNull(definition);var threat=a.w.Spawn(building,1,a.landing+new Vector2(0,3));threat.hp=a.w.MaxHp(threat)*hpFraction;
            // An own scout makes the bank weapon genuinely visible. This is a
            // declared mechanics input and never a hidden enemy selector.
            a.w.Spawn("explorer",0,Safe(a.w,a.landing+new Vector2(-5,0),.4f));a.w.Fog.Tick(a.w.Entities);Assert.IsTrue(a.w.IsVisible(0,threat));
            a.bridge.Tick(a.Own(),new List<EntityState>{threat},false);Assert.AreEqual(3,a.bridge.Mission.phase);
        }
        [Test] public void ActualDifferentShoreUnloadBetweenReviewsCannotReportCompletion()
        {
            var a=new Arena();a.CommitPublicFixture();var ids=a.bridge.Mission.members.ToArray();
            for(int i=0;i<1800&&a.bridge.Mission!=null&&a.bridge.Mission.phase==1;i++)a.Step(.1f);
            Assert.IsNotNull(a.bridge.Mission);Assert.AreEqual(2,a.bridge.Mission.phase);
            // A normal public landing replacement occurs and physically finishes
            // while this optional planner is not reviewed. Its next review must
            // inspect real Disembarked coordinates rather than absent orders.
            Assert.IsTrue(a.w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{a.ship.id},order=OrderKind.Stop}).accepted);
            Assert.IsTrue(a.w.Issue(new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{a.ship.id},point=a.boarding}).accepted);
            Until(a.w,()=>ids.All(id=>a.w.Get(id).garrisonedIn==0),160,"Actual public unload at a different original shore");
            Assert.IsTrue(a.w.Events.Any(e=>e.kind=="Disembarked"&&e.target==a.ship.id&&(e.position-a.landing).sqrMagnitude>36));
            a.bridge.Tick(a.Own(),new List<EntityState>(),false);Assert.IsNull(a.bridge.Mission);Assert.IsFalse(a.w.Events.Any(e=>e.kind=="AIBridgeTransportCompleted"));
            Assert.IsTrue(a.w.Events.Any(e=>e.kind=="AIBridgeTransportReleased"));
        }

        static EntityState RevealThreat(Arena a,string unit,Vector2 shore)
        {
            var threat=a.w.Spawn(unit,1,Safe(a.w,shore+new Vector2(0,3),a.w.Rules.Entity(unit).radius));
            Assert.IsTrue(a.w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{threat.id},order=OrderKind.Hold}).accepted);
            a.w.Spawn("explorer",0,Safe(a.w,shore+new Vector2(-5,0),.4f));a.w.Fog.Tick(a.w.Entities);
            Assert.IsTrue(a.w.IsVisible(0,threat));Assert.IsTrue(AiResourceContinuity072.EconomicThreat(a.w,0,threat));
            Assert.Less((threat.position-shore).sqrMagnitude,100,"The actual visible weapon must cover the shore safety envelope");return threat;
        }
        [TestCase("hussar")][TestCase("pike")]
        public void VisibleLandMeleeAtTargetShoreReturnsActualCargo(string unit)
        {
            var a=new Arena();a.CommitPublicFixture();var ids=a.bridge.Mission.members.ToArray();
            for(int i=0;i<1800&&a.bridge.Mission!=null&&a.bridge.Mission.phase==1;i++)a.Step(.1f);
            Assert.IsNotNull(a.bridge.Mission);Assert.AreEqual(2,a.bridge.Mission.phase);Assert.IsTrue(ids.Any(id=>a.w.Get(id).garrisonedIn==a.ship.id));
            var threat=RevealThreat(a,unit,a.landing);a.bridge.Tick(a.Own(),new List<EntityState>{threat},false);
            Assert.AreEqual(3,a.bridge.Mission.phase,"A visible melee land weapon still threatens real disembarking passengers");
            Assert.IsFalse(a.w.Events.Any(e=>e.kind=="AIBridgeTransportCompleted"));
        }
        [TestCase("hussar")][TestCase("pike")]
        public void VisibleLandMeleeAtBoardingShoreCancelsRealPendingPassengers(string unit)
        {
            var a=new Arena();a.CommitPublicFixture();var ids=a.bridge.Mission.members.ToArray();
            Assert.IsTrue(ids.All(id=>a.w.Get(id).orders.Peek().kind==OrderKind.Embark));Assert.IsEmpty(a.ship.occupants);
            var threat=RevealThreat(a,unit,a.boarding);a.bridge.Tick(a.Own(),new List<EntityState>{threat},false);
            Assert.IsNull(a.bridge.Mission);Assert.IsEmpty(a.ship.occupants);
            Assert.IsTrue(ids.All(id=>a.w.Get(id).orders.All(o=>o.kind!=OrderKind.Embark)));
            Assert.IsFalse(a.w.Events.Any(e=>e.kind=="Disembarked"&&e.target==a.ship.id));
            Assert.IsTrue(a.w.Events.Any(e=>e.kind=="AIBridgeTransportReleased"));
        }
        [Test] public void CancelledRealPassengerCanEnterActualMedicalRecoveryAndColdRestoreWithoutTransportReclaim()
        {
            var a=new Arena();a.CommitPublicFixture();int id=a.bridge.Mission.members[0];var passenger=a.w.Get(id);
            Assert.IsTrue(a.w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{id},order=OrderKind.Stop}).accepted);
            // Declared wound/doctor mechanics input. Admission itself must run
            // the actual support policy and issue the public retreat command.
            passenger.hp=a.w.MaxHp(passenger)*.3f;
            a.w.Spawn("priest",0,Safe(a.w,a.army.Home+new Vector2(-6,-6),.4f));a.w.Fog.Tick(a.w.Entities);
            var soldiers=a.Own().Where(e=>e.Military&&!e.def.waterborne).ToList();
            a.medical.ReviewSupport(a.Own(),soldiers,new List<EntityState>(),a.army.Home);
            Assert.IsTrue(a.medical.HasStation);Assert.IsTrue(a.medical.IsRecovering(id),"Real policy must admit the cancelled wounded passenger");
            Assert.IsTrue(a.w.Events.Any(e=>e.kind=="AIMedicalRetreat"&&e.entity==id));
            var state=a.army.CaptureState();state.reviewAt=0;a.army.RestoreState(state);a.ReviewArmy();
            Assert.IsFalse(a.army.WaveMembers.Contains(id));Assert.IsFalse(a.army.GuardMembers.Contains(id));Assert.IsFalse(a.bridge.Owns(id));a.bridge.ValidateOwnership();
            var cold=World.Restore(a.w.Rules,SnapshotCodec.Clone(a.w.CaptureState()));var profile=AiDifficultyProfile.From(cold.Rules,"standard");
            var army=new AiArmyPlan072(cold,0,profile);army.RestoreState(SnapshotCodec.Clone(a.army.CaptureState()));
            var medical=new AiMedicalSupport076(cold,0);medical.RestoreState(SnapshotCodec.Clone(a.medical.CaptureState()));
            var bridge=new AiBridgeTransport084(cold,0,army,null,medical);army.BindTransport(bridge.Owns);
            bridge.Restore(null,SnapshotCodec.Clone(a.bridge.Mission),a.bridge.LastReview,a.bridge.RetryAt);bridge.ValidateOwnership();
            var before=JsonUtility.ToJson(passenger.orders.ToArray());var coldBefore=JsonUtility.ToJson(cold.Get(id).orders.ToArray());
            a.bridge.Tick(a.Own(),new List<EntityState>(),false);bridge.Tick(cold.Entities.Where(e=>e.Alive&&e.owner==0).ToList(),new List<EntityState>(),false);
            Assert.IsTrue(medical.IsRecovering(id));Assert.IsFalse(bridge.Owns(id));
            Assert.AreEqual(before,JsonUtility.ToJson(passenger.orders.ToArray()));Assert.AreEqual(coldBefore,JsonUtility.ToJson(cold.Get(id).orders.ToArray()));
            Assert.AreEqual(JsonUtility.ToJson(a.w.CaptureState()),JsonUtility.ToJson(cold.CaptureState()));
        }
        [Test] public void ActualReturnStaysAboardAtObservedUnsafeOriginalBankThenUnloadsOriginalShoreAcrossColdRestore()
        {
            var a=new Arena();a.CommitPublicFixture();var ids=a.bridge.Mission.members.ToArray();
            for(int i=0;i<1800&&a.bridge.Mission!=null&&a.bridge.Mission.phase==1;i++)a.Step(.1f);
            Assert.IsNotNull(a.bridge.Mission);Assert.AreEqual(2,a.bridge.Mission.phase);Assert.IsTrue(ids.All(id=>a.w.Get(id).garrisonedIn==a.ship.id));
            var threat=RevealThreat(a,"pike",a.boarding);a.bridge.Tick(a.Own(),new List<EntityState>{threat},true);
            Assert.AreEqual(3,a.bridge.Mission.phase);Assert.IsFalse(a.ship.orders.Any(o=>o.kind==OrderKind.Disembark));
            Assert.IsTrue(ids.All(id=>a.w.Get(id).garrisonedIn==a.ship.id));
            // Remove the declared observed threat rather than weakening safety;
            // both live and restored planners now use the saved original shore.
            a.w.Remove(threat);a.w.Fog.Tick(a.w.Entities);
            var cold=World.Restore(a.w.Rules,SnapshotCodec.Clone(a.w.CaptureState()));var profile=AiDifficultyProfile.From(cold.Rules,"standard");
            var army=new AiArmyPlan072(cold,0,profile);army.RestoreState(SnapshotCodec.Clone(a.army.CaptureState()));
            var medical=new AiMedicalSupport076(cold,0);medical.RestoreState(SnapshotCodec.Clone(a.medical.CaptureState()));
            var bridge=new AiBridgeTransport084(cold,0,army,null,medical);army.BindTransport(bridge.Owns);bridge.Restore(null,SnapshotCodec.Clone(a.bridge.Mission),a.bridge.LastReview,a.bridge.RetryAt);bridge.ValidateOwnership();
            for(int i=0;i<2400&&(a.bridge.Mission!=null||bridge.Mission!=null);i++)
            {
                a.bridge.Tick(a.Own(),new List<EntityState>(),true);bridge.Tick(cold.Entities.Where(e=>e.Alive&&e.owner==0).ToList(),new List<EntityState>(),true);
                Assert.LessOrEqual(a.bridge.LastRouteQueries,6);Assert.LessOrEqual(bridge.LastRouteQueries,6);a.w.Tick(.1f);cold.Tick(.1f);
            }
            Assert.IsNull(a.bridge.Mission);Assert.IsNull(bridge.Mission);Assert.IsEmpty(a.ship.occupants);
            foreach(int id in ids)
            {
                var release=a.w.Events.Single(e=>e.kind=="Disembarked"&&e.entity==id&&e.target==a.ship.id);
                Assert.LessOrEqual((release.position-a.boarding).sqrMagnitude,36.0001f);Assert.IsTrue(a.w.Navigation.ClearLine(a.boarding,release.position,a.w.Get(id).def.radius));
                Assert.AreEqual(0,a.w.Get(id).garrisonedIn);
            }
            Assert.IsTrue(a.w.Events.Any(e=>e.kind=="AIBridgeTransportReleased"));Assert.IsFalse(a.w.Events.Any(e=>e.kind=="AIBridgeTransportCompleted"));
            Assert.AreEqual(JsonUtility.ToJson(a.w.CaptureState()),JsonUtility.ToJson(cold.CaptureState()),"Both continuations must actually return and release the original passengers");
        }
    }
}
