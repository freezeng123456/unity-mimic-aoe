using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class NavalTransport065Tests
    {
        // These funded age-II fixtures isolate transport mechanics. Only the
        // final AI test is an unmodified, normal-resource opening.
        private static World Prepared(string map=MapTopology.GeneratedStrait)
        {
            var rules=SnapshotCodec.Clone(Catalog.Load());var world=new World(rules,314159,"coast","coast",5,map);
            world.Players[0].age=2;world.Players[0].resources=new Cost(5000,5000,5000);
            foreach(var unit in world.Entities.Where(e=>e.def.kind==EntityKind.Unit&&(e.owner!=0)).ToArray())world.Remove(unit);
            // Capacity is a fixture input; construction/transport spending below
            // still goes through the actual public commands and accounting.
            world.Spawn("house",0,SafeLand(world,world.OpeningTownPosition(0)+new Vector2(-4,14),.4f));
            world.Fog.Tick(world.Entities);return world;
        }
        private static void Until(World world,Func<bool> condition,float seconds,string reason)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds*10)&&!condition()&&world.Winner<0;i++)world.Tick(.1f);
            string state=string.Join("; ",world.Entities.Where(e=>e.Alive&&e.owner==0&&(e.def.transportShip||e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Embark)).Select(e=>
                $"{e.id}/{e.def.id} pos={e.position} status={e.status} cargo={e.garrisonedIn} path={e.pathIndex}/{e.path.Count} order={(e.orders.Count>0?e.orders.Peek().kind.ToString():"none")} goal={(e.orders.Count>0?e.orders.Peek().point:Vector2.zero)}"));
            Assert.IsTrue(condition(),reason+" t="+world.Time+" winner="+world.Winner+" "+state);
        }
        private static Vector2 SafeLand(World world,Vector2 center,float radius)
        {
            foreach(var point in world.Navigation.FormationSlots(center,512,radius*2+.4f))
            {
                if(!world.Navigation.IsWalkable(point,radius)||world.IsWater(point))continue;
                if(world.Entities.Any(e=>e.Alive&&e.garrisonedIn==0&&e.def.kind!=EntityKind.Building&&!e.def.waterborne&&(e.position-point).sqrMagnitude<Mathf.Pow(radius+e.def.radius+.2f,2)))continue;
                return point;
            }
            Assert.Fail("No body-safe fixture land point near "+center);return Vector2.zero;
        }
        private static EntityState PaidTransport(World world,out EntityState dock)
        {
            var builder=world.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker);dock=null;
            foreach(var candidate in world.Layout.DockSites[0])
            {
                Vector2 survey=world.Navigation.NearestWalkable(candidate-world.Layout.WaterDirection(candidate)*4,builder.def.radius);
                Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{builder.id},order=OrderKind.Move,point=survey}).accepted);
                Until(world,()=>world.Fog.At(0,candidate)==VisibilityState.Visible,90,"The builder must physically survey the dock");
                if(world.CanBuild(0,"dock",candidate)!="")continue;
                var before=world.Players[0].resources;var cost=world.EntityCost(0,world.Rules.Entity("dock"));
                var command=world.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{builder.id},item="dock",point=candidate});
                Assert.IsTrue(command.accepted,command.reason);Assert.AreEqual(before.wood-cost.wood,world.Players[0].resources.wood,.001f);
                dock=world.Entities.Last(e=>e.Alive&&e.owner==0&&e.def.id=="dock");var constructed=dock;
                Until(world,()=>constructed.Complete,130,"A paid dock must finish through actual worker construction");break;
            }
            Assert.IsNotNull(dock,"No legal paid harbor");var player=world.Players[0];var definition=world.Rules.Entity("transport_ship");
            Cost funds=player.resources;int population=player.usedPopulation;
            var train=world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{dock.id},item=definition.id});
            Assert.IsTrue(train.accepted,train.reason);var paid=world.UnitCost(0,definition);
            Assert.AreEqual(funds.wood-paid.wood,player.resources.wood,.001f);Assert.AreEqual(funds.coin-paid.coin,player.resources.coin,.001f);
            Assert.AreEqual(definition.population,player.reservedPopulation);
            Until(world,()=>world.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.transportShip),definition.time+2,"The paid queue must deliver a real ship at its water exit");
            var ship=world.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.transportShip);
            Assert.AreEqual(population+definition.population,player.usedPopulation);Assert.AreEqual(0,player.reservedPopulation);
            Assert.IsTrue(world.WaterNavigation.IsWalkable(ship.position,ship.def.radius));Assert.IsTrue(world.IsWater(ship.position));
            return ship;
        }
        private static Vector2 Shore(World world,EntityState ship,int player=0)
        {
            var candidates=world.Layout.LandingSites[player].ToList();
            foreach(var site in world.Layout.DockSites[player])
            {var direction=world.Layout.WaterDirection(site);var side=new Vector2(-direction.y,direction.x);candidates.Add(site+side*10);candidates.Add(site-side*10);}
            foreach(var candidate in candidates)if(world.TryTransportShore(ship,candidate,.9f,out var land,out _))return land;
            Assert.Fail("No usable transport shore for player "+player);return Vector2.zero;
        }
        private static EntityState Soldier(World world,Vector2 shore,string definition="musketeer")
        {
            var d=world.Rules.Entity(definition);return world.Spawn(definition,0,SafeLand(world,shore-world.Layout.WaterDirection(shore)*8,d.radius));
        }
        private static void Board(World world,EntityState ship,Vector2 shore,params EntityState[] passengers)
        {
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=passengers.Select(e=>e.id).ToArray(),target=ship.id,point=shore});
            Assert.IsTrue(result.accepted,result.reason);
            Until(world,()=>passengers.All(e=>e.garrisonedIn==ship.id),90,"Passengers and transport must actually navigate to a common shore");
        }

        [TestCase(MapTopology.Winding)]
        [TestCase(MapTopology.GeneratedStrait)]
        public void PaidHarborAndProductionThenRemoteBoardingArePhysicalAndPopulationBased(string map)
        {
            var world=Prepared(map);var ship=PaidTransport(world,out var dock);var shore=Shore(world,ship);
            var passengers=new[]{Soldier(world,shore,"cannon"),Soldier(world,shore,"hussar"),Soldier(world,shore),Soldier(world,shore,"explorer")};
            int population=world.Players[0].usedPopulation;Cost funds=world.Players[0].resources;Vector2 start=passengers[0].position;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=passengers.Select(e=>e.id).ToArray(),target=ship.id,point=shore}).accepted);
            Assert.IsTrue(passengers.All(e=>e.garrisonedIn==0));world.Tick(.1f);
            Assert.Greater(Vector2.Distance(start,passengers[0].position),0,"Boarding must walk rather than teleport from the command position");
            Assert.IsTrue(passengers.All(e=>e.garrisonedIn==0));
            Until(world,()=>passengers.All(e=>e.garrisonedIn==ship.id),90,"The common boarding shore must be reached");
            Assert.AreEqual(8,world.TransportLoad(ship),"4 cannon + 2 cavalry + 1 infantry + minimum 1 slot for a zero-pop explorer");
            Assert.AreEqual(population,world.Players[0].usedPopulation);Assert.AreEqual(funds.wood,world.Players[0].resources.wood,.001f);Assert.AreEqual(funds.coin,world.Players[0].resources.coin,.001f);
            foreach(var passenger in passengers){Assert.Contains(passenger.id,ship.occupants);Assert.AreEqual(ship.position,passenger.position);Assert.IsEmpty(passenger.orders);}
            Assert.IsTrue(world.Events.Any(e=>e.kind=="Payment"&&e.entity==dock.id&&e.message.StartsWith(ship.def.name)));
        }

        [Test] public void CapacityReservationsCannotOverbookAndPublicStopCancelsBoardingWithoutLosingUnits()
        {
            var world=Prepared();var ship=PaidTransport(world,out _);var shore=Shore(world,ship);
            var full=Enumerable.Range(0,7).Select(_=>Soldier(world,shore,"hussar")).ToArray();var extra=Soldier(world,shore);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=full.Select(e=>e.id).ToArray(),target=ship.id,point=shore}).accepted);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=new[]{extra.id},target=ship.id,point=shore}).accepted);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{full[0].id},order=OrderKind.Stop}).accepted);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=new[]{extra.id},target=ship.id,point=shore}).accepted);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{ship.id},order=OrderKind.Stop}).accepted);
            world.Tick(.1f);Assert.IsTrue(full.All(e=>e.Alive&&e.garrisonedIn==0));Assert.IsTrue(extra.Alive&&extra.garrisonedIn==0);Assert.IsEmpty(ship.occupants);
            Assert.IsTrue(full.All(e=>e.orders.Count==0));Assert.IsEmpty(extra.orders);
        }

        [Test] public void BoardingRejectsEnemyUnitsMixedDomainsAndGenericGarrisonShips()
        {
            var world=Prepared();var ship=PaidTransport(world,out var dock);var shore=Shore(world,ship);var passenger=Soldier(world,shore);
            var enemy=world.Spawn("musketeer",1,SafeLand(world,world.OpeningTownPosition(1)+new Vector2(-7,-10),.4f));
            foreach(var actor in new[]{enemy,ship,dock})Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=new[]{passenger.id,actor.id},target=ship.id,point=shore}).accepted);
            Assert.IsEmpty(passenger.orders,"Atomic validation must not board a valid prefix of an invalid group");
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=new[]{passenger.id},target=dock.id,point=shore}).accepted);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{passenger.id},order=OrderKind.Garrison,target=ship.id}).accepted);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.DockRepair,actors=new[]{ship.id},target=ship.id}).accepted);
        }

        [Test] public void MixedCargoSailsAcrossAnUnbridgedStraitAndLandsOnSeparateBodySafeSlots()
        {
            var world=Prepared();var ship=PaidTransport(world,out _);var home=Shore(world,ship);var landing=Shore(world,ship,1);
            var economy=new AiWaterEconomy(world,0);
            var passengers=new[]{Soldier(world,home,"cannon"),Soldier(world,home,"hussar"),Soldier(world,home),Soldier(world,home)};
            Assert.IsEmpty(world.Navigation.FindPath(passengers[0].position,landing,passengers[0].def.radius),"This fixture must require sea transport");
            Board(world,ship,home,passengers);int population=world.Players[0].usedPopulation;Vector2 start=ship.position;
            var unload=world.Issue(new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{ship.id},point=landing});Assert.IsTrue(unload.accepted,unload.reason);
            world.Tick(.1f);Assert.IsTrue(passengers.All(e=>e.garrisonedIn==ship.id));Assert.Greater(Vector2.Distance(start,ship.position),0);
            Until(world,()=>passengers.All(e=>e.garrisonedIn==0),150,"A transport must really sail across the water and unload");
            Assert.AreEqual(population,world.Players[0].usedPopulation);Assert.IsEmpty(ship.occupants);
            foreach(var passenger in passengers)
            {
                Assert.IsTrue(world.Navigation.IsWalkable(passenger.position,passenger.def.radius));Assert.IsFalse(world.IsWater(passenger.position));
                Assert.AreEqual(world.OpeningTownPosition(1),economy.LandArmyDestination(passenger,world.OpeningTownPosition(1)),"An actually landed soldier retains its attack on this enemy landmass");
                var anchor=world.Navigation.NearestWalkable(world.OpeningTownPosition(1),passenger.def.radius);
                Assert.IsNotEmpty(world.Navigation.FindPath(passenger.position,anchor,passenger.def.radius),"Landed on the destination's actual land component");
                foreach(var other in passengers)if(other.id!=passenger.id)Assert.GreaterOrEqual(Vector2.Distance(passenger.position,other.position),passenger.def.radius+other.def.radius+.15f-.001f);
            }
            Assert.AreEqual(passengers.Length,world.Events.Count(e=>e.kind=="Disembarked"&&e.target==ship.id));
        }

        [TestCase(314159)]
        [TestCase(271828)]
        public void UnboardedArmyAndExplorerUseReachableDestinationsOnTheirCurrentLandmass(int seed)
        {
            // The six military actors are fixture inputs and must really assemble;
            // the long normal-opening cases below still grant no actors or funds.
            var world=new World(Catalog.Load(),seed,"coast","coast",2,MapTopology.GeneratedStrait);
            Vector2 home=world.OpeningTownPosition(0),enemy=world.OpeningTownPosition(1);int region=world.Layout.LandRegion(home);
            Assert.AreNotEqual(region,world.Layout.LandRegion(enemy));
            float radius=world.Rules.Entity("musketeer").radius;
            var army=Enumerable.Range(0,6).Select(_=>world.Spawn("musketeer",0,SafeLand(world,home+new Vector2(-8,10),radius))).ToArray();
            var ids=army.Select(e=>e.id).ToArray();var travelled=ids.ToDictionary(id=>id,id=>0f);
            var explorer=world.Entities.First(e=>e.Alive&&e.owner==0&&(e.def.tags&Tags.Explorer)!=0);
            var economy=new AiWaterEconomy(world,0);
            string Bodies()=>string.Join("; ",army.Select(e=>$"{e.id} alive={e.Alive} cargo={e.garrisonedIn} pos={e.position} moved={travelled[e.id]:F3} hold={e.hold} order={(e.orders.Count>0?e.orders.Peek().kind.ToString():"none")} goal={(e.orders.Count>0?e.orders.Peek().point:Vector2.zero)}"));
            void CheckBodies()
            {
                foreach(var soldier in army)
                {
                    string state=$"seed={seed} t={world.Time} actor={soldier.id} pos={soldier.position}";
                    Assert.IsTrue(soldier.Alive,state+" The original soldier must survive the real muster");
                    Assert.AreEqual(0,soldier.garrisonedIn,state+" The soldier must remain unboarded");
                    Assert.AreEqual(region,world.Layout.LandRegion(soldier.position),state+" An unboarded body cannot cross to another landmass");
                    Assert.IsFalse(world.IsWater(soldier.position),state+" An unboarded body cannot enter water");
                    Assert.IsTrue(world.Navigation.IsWalkable(soldier.position,soldier.def.radius),state+" The actual body must fit on land");
                }
            }
            void CheckRoute(EntityState actor,Vector2 point,string reason)
            {
                string state=$"{reason} seed={seed} t={world.Time} actor={actor.id} pos={actor.position} goal={point}";
                Assert.AreEqual(region,world.Layout.LandRegion(point),state+" The destination must stay on the current island");
                Assert.IsFalse(world.IsWater(point),state+" The destination cannot be water");
                Assert.IsTrue(world.Navigation.IsWalkable(point,actor.def.radius),state+" The destination must fit the actor");
                var route=world.Navigation.FindPath(actor.position,point,actor.def.radius);
                Assert.IsNotEmpty(route,state+" The destination must have a complete land path");
                Assert.LessOrEqual((route[route.Count-1]-point).sqrMagnitude,.0001f,state+" A path to a different snapped endpoint is insufficient");
            }
            bool AtStage(EntityState soldier,Vector2 stage)
            {
                if((soldier.position-stage).sqrMagnitude>100||!world.Navigation.IsWalkable(soldier.position,soldier.def.radius))return false;
                if(world.Navigation.ClearLine(soldier.position,stage,soldier.def.radius))return true;
                var route=world.Navigation.FindPath(soldier.position,stage,soldier.def.radius);
                if(route.Count<2||(route[route.Count-1]-stage).sqrMagnitude>.0001f)return false;
                float length=0;for(int i=1;i<route.Count;i++)length+=Vector2.Distance(route[i-1],route[i]);
                return length<=14;
            }
            Vector2 scout=economy.LandArmyDestination(explorer,enemy);
            CheckRoute(explorer,scout,"The scout API must return a reachable bank");
            CheckBodies();Assert.IsFalse(world.Paused);Assert.IsFalse(world.IsMatchEnded);
            var controller=new SkirmishAI(world,0);int eventStart=world.Events.Count;controller.Tick(.1f);
            var plan=controller.CaptureState().armyPlan;
            Assert.IsTrue(plan.hasStage,$"seed={seed} The first ordinary AI decision must select a real stage");
            Assert.AreEqual(AiArmyPhase072.Muster,plan.phase,$"seed={seed} The fixture soldiers have not yet assembled");
            Assert.IsEmpty(plan.wave);Assert.AreEqual(0,plan.initialWaveSize);
            var initiallyOutside=army.Where(e=>!AtStage(e,plan.stage)).Select(e=>e.id).ToArray();
            Assert.IsNotEmpty(initiallyOutside,"This fixture must exercise actual movement into the stage");
            foreach(var soldier in army)
            {
                if(AtStage(soldier,plan.stage))
                {Assert.IsTrue(soldier.hold,"An already assembled soldier must receive ordinary Hold");Assert.IsEmpty(soldier.orders);}
                else
                {
                    Assert.IsFalse(soldier.hold);Assert.AreEqual(1,soldier.orders.Count);
                    Assert.AreEqual(OrderKind.Move,soldier.orders.Peek().kind,"Muster must use ordinary Move before departure");
                    CheckRoute(soldier,soldier.orders.Peek().point,"The actual muster Move");
                }
            }
            if(explorer.orders.Count>0&&explorer.orders.Peek().kind==OrderKind.Move)
                CheckRoute(explorer,explorer.orders.Peek().point,"The actual Explore command");
            bool marched=false;int departureStep=-1;
            // Step zero completes the first Tick above. Every subsequent step
            // uses the ordinary controller/World pair; no orders or poses are injected.
            for(int step=0;step<1200;step++)
            {
                Assert.IsFalse(world.Paused,$"seed={seed} t={world.Time} Pausing cannot satisfy the departure bound");
                Assert.IsFalse(world.IsMatchEnded,$"seed={seed} t={world.Time} Terminal play cannot satisfy the departure bound");
                CheckBodies();
                if(step>0){eventStart=world.Events.Count;controller.Tick(.1f);}
                Assert.IsFalse(world.Paused);Assert.IsFalse(world.IsMatchEnded);CheckBodies();
                plan=controller.CaptureState().armyPlan;
                if(plan.phase==AiArmyPhase072.March)
                {
                    // Inspect before World.Tick: departure cannot be justified by
                    // movement that happens after the AI has already sent the wave.
                    Assert.IsTrue(plan.hasStage);Assert.AreEqual(6,plan.initialWaveSize);
                    CollectionAssert.AreEquivalent(ids,plan.wave,"The first wave must contain exactly the six declared original soldiers");
                    foreach(var soldier in army)
                    {
                        Assert.IsTrue(AtStage(soldier,plan.stage),$"First March before World.Tick: seed={seed} t={world.Time} stage={plan.stage} "+Bodies());
                        Assert.IsFalse(soldier.hold);Assert.AreEqual(1,soldier.orders.Count);
                        var order=soldier.orders.Peek();Assert.AreEqual(OrderKind.AttackMove,order.kind);Assert.AreEqual(0,order.target);
                        Vector2 mapped=economy.LandArmyDestination(soldier,plan.destination);
                        var slots=(mapped-plan.destination).sqrMagnitude>1
                            ?world.Navigation.FormationSlots(mapped,1,1.9f)
                            :world.Navigation.FormationSlots(plan.destination,6,1.9f);
                        Assert.IsTrue(slots.Any(point=>(point-order.point).sqrMagnitude<=.0001f),$"Actor {soldier.id} must receive the normal formation slot for destination={plan.destination}, mapped={mapped}, actual={order.point}");
                        CheckRoute(soldier,order.point,"The actual first March AttackMove");
                    }
                    var decisionEvents=world.Events.Skip(eventStart).ToArray();
                    Assert.IsTrue(decisionEvents.Any(e=>e.kind=="AIArmyPhase"&&e.player==0&&e.amount==6&&e.message=="Owned soldiers actually assembled for the first wave"),"The first March needs its real phase-change event in this AI decision");
                    var commands=decisionEvents.Where(e=>e.kind=="Command"&&e.player==0&&ids.Contains(e.entity)&&e.message.StartsWith("AttackMove ×",StringComparison.Ordinal)).ToArray();
                    Assert.IsNotEmpty(commands,"The phase snapshot alone is not an accepted AttackMove command");
                    Assert.AreEqual(6f,commands.Sum(e=>e.amount),"The ordinary accepted AttackMove commands must account for all six soldiers");
                    Assert.IsTrue(commands.All(e=>e.target==0&&e.amount>=1&&e.amount<=6));
                    marched=true;departureStep=step;
                }
                var before=army.Select(e=>e.position).ToArray();world.Tick(.1f);
                for(int i=0;i<army.Length;i++)travelled[army[i].id]+=Vector2.Distance(before[i],army[i].position);
                Assert.IsFalse(world.Paused);Assert.IsFalse(world.IsMatchEnded);CheckBodies();
                if(marched)break;
            }
            Assert.IsTrue(marched,$"seed={seed} No actual first March within 1200 normal steps; t={world.Time} phase={plan.phase} stage={plan.stage} "+Bodies());
            foreach(int id in initiallyOutside)Assert.Greater(travelled[id],0f,$"Original soldier {id} must really move into assembly");
            TestContext.WriteLine($"seed={seed} firstMarchStep={departureStep} t={world.Time} stage={plan.stage} destination={plan.destination}; "+Bodies());
        }

        [Test] public void AnOccupiedLandingRetainsAllCargoAndStopCancelsTheAttempt()
        {
            var world=Prepared();var ship=PaidTransport(world,out _);var home=Shore(world,ship);var passenger=Soldier(world,home);
            Board(world,ship,home,passenger);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{ship.id},point=home}).accepted);
            var shore=ship.orders.Peek().point;
            // Occupants are physical units, not a changed water mask: the ship can
            // still reach its berth while every local land exit is occupied.
            foreach(var point in world.Navigation.FormationSlots(shore,256,.5f))
                if((point-shore).sqrMagnitude<=40&&world.Navigation.IsWalkable(point,.4f))world.Spawn("villager",0,point);
            for(int i=0;i<90;i++)world.Tick(.1f);
            Assert.AreEqual(ship.id,passenger.garrisonedIn);Assert.Contains(passenger.id,ship.occupants);Assert.GreaterOrEqual(ship.orders.Peek().destinationCursor,6);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{ship.id},order=OrderKind.Stop}).accepted);
            world.Tick(.1f);Assert.AreEqual(ship.id,passenger.garrisonedIn);Assert.IsEmpty(ship.orders);
        }

        [Test] public void CargoCannotGatherFireOrContributeItsLandVisionAndSinkingUsesNormalLossAccounting()
        {
            var world=Prepared();world.Rules.Entity("explorer").vision=48;var ship=PaidTransport(world,out _);var shore=Shore(world,ship);
            var worker=Soldier(world,shore,"villager");var explorer=Soldier(world,shore,"explorer");Board(world,ship,shore,worker,explorer);
            var resource=world.Entities.First(e=>e.Alive&&e.def.id=="tree");var enemy=world.Entities.First(e=>e.Alive&&e.owner==1&&e.def.id=="town");
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Gather,target=resource.id}).accepted);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{explorer.id},order=OrderKind.Attack,target=enemy.id}).accepted);
            var expected=new Visibility();expected.Tick(world.Entities.Where(e=>e.garrisonedIn==0).ToList());world.Fog.Tick(world.Entities);
            // Compare current visibility rather than previously explored history.
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)Assert.AreEqual(expected.At(0,new Vector2(x,y))==VisibilityState.Visible,world.Fog.At(0,new Vector2(x,y))==VisibilityState.Visible);
            int population=world.Players[0].usedPopulation,losses=world.Players[0].losses,kills=world.Players[1].kills;int lostPopulation=ship.def.population+worker.def.population+explorer.def.population;
            world.Remove(ship,1);Assert.IsFalse(worker.Alive);Assert.IsFalse(explorer.Alive);Assert.IsEmpty(ship.occupants);
            Assert.AreEqual(population-lostPopulation,world.Players[0].usedPopulation);Assert.AreEqual(losses+3,world.Players[0].losses);Assert.AreEqual(kills+3,world.Players[1].kills);
            Assert.AreEqual(0,worker.garrisonedIn);Assert.AreEqual(0,explorer.garrisonedIn);world.Tick(.1f);
            Assert.IsFalse(world.Events.Any(e=>e.kind=="Ungarrison"&&e.entity==ship.id),"Sinking must not use free building evacuation");
        }

        [Test] public void ColdSaveContinuesBoardingAndSailingWithoutDuplicatingPassengers()
        {
            var world=Prepared();var ship=PaidTransport(world,out _);var shore=Shore(world,ship);var passenger=Soldier(world,shore,"hussar");
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=new[]{passenger.id},target=ship.id,point=shore}).accepted);world.Tick(.1f);
            var loaded=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));
            Until(world,()=>passenger.garrisonedIn==ship.id,90,"Live boarding must finish");
            Until(loaded,()=>loaded.Get(passenger.id).garrisonedIn==ship.id,90,"Cold restored boarding must finish");
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(loaded.CaptureState()));
            var landing=Shore(world,ship,1);Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{ship.id},point=landing}).accepted);
            world.Tick(.1f);loaded=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));
            for(int i=0;i<1600&&passenger.garrisonedIn!=0;i++){world.Tick(.1f);loaded.Tick(.1f);}
            Assert.AreEqual(0,passenger.garrisonedIn);Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(loaded.CaptureState()));
            var invalid=SnapshotCodec.Clone(world.CaptureState());var passengerState=invalid.entities.Single(e=>e.state.id==passenger.id);passengerState.state.garrisonedIn=ship.id;
            Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,invalid),"Unlisted cargo must not be accepted as an invisible saved passenger");
        }

        [Test] public void DockRepairRequiresArrivalResourcesAndPaidCareeningImprovesOnlyItsRate()
        {
            var world=Prepared();var ship=PaidTransport(world,out var dock);ship.hp-=100;float damaged=ship.hp;
            Vector2 away=world.WaterNavigation.NearestWalkable(ship.position+world.Layout.WaterDirection(dock.position)*9,ship.def.radius);
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{ship.id},order=OrderKind.Move,point=away}).accepted);
            Until(world,()=>ship.orders.Count==0,45,"Ship must actually sail away from the repair berth");
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.DockRepair,actors=new[]{ship.id},target=dock.id}).accepted);
            world.Tick(.1f);Assert.AreEqual(damaged,ship.hp,.001f,"A distant dock cannot remotely repair");
            world.Players[0].resources=new Cost();Until(world,()=>ship.status=="Dock repair needs resources",45,"Ship must dock before testing the empty account");Assert.AreEqual(damaged,ship.hp,.001f);
            world.Players[0].resources=new Cost(100,1000,1000);var before=world.Players[0].resources;world.Tick(.1f);
            Assert.AreEqual(damaged+1.8f,ship.hp,.001f);Assert.AreEqual(before.food,world.Players[0].resources.food,.001f);
            Assert.Less(world.Players[0].resources.wood,before.wood);Assert.Less(world.Players[0].resources.coin,before.coin);
            var receipt=world.Events.Last(e=>e.kind=="Payment"&&e.entity==ship.id);Assert.IsTrue(receipt.hasEconomy&&receipt.economy.hasRepair);
            Assert.AreEqual(damaged,receipt.economy.hpBefore,.001f);Assert.AreEqual(ship.hp,receipt.economy.hpAfter,.001f);
            float oldRate=world.NavalRepairRate(ship);var research=world.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{dock.id},item="careening"});Assert.IsTrue(research.accepted,research.reason);
            Until(world,()=>world.Players[0].technologies.Contains("careening"),world.Rules.Tech("careening").time+1,"Careening is a normal paid timed technology");
            Assert.AreEqual(oldRate*1.25f,world.NavalRepairRate(ship),.001f);
            ship.hp=world.MaxHp(ship)*.4f;var plan=new AiNavalOperations(world,0);
            plan.Tick(world.Entities.Where(e=>e.Alive&&e.owner==0).ToList(),new List<EntityState>(),false);
            Assert.AreEqual(OrderKind.DockRepair,ship.orders.Peek().kind,"An empty damaged AI transport must use the actual paid harbor repair command");
            var retained=ship.orders.Peek();plan.Tick(world.Entities.Where(e=>e.Alive&&e.owner==0).ToList(),new List<EntityState>(),false);
            Assert.AreSame(retained,ship.orders.Peek(),"An existing repair order must not be restarted");
            before=world.Players[0].resources;float hurt=ship.hp;world.Tick(.1f);Assert.Greater(ship.hp,hurt);Assert.Less(world.Players[0].resources.wood,before.wood);Assert.Less(world.Players[0].resources.coin,before.coin);
            var enemyDock=world.Spawn("dock",1,world.Layout.DockSites[1][0]);Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.DockRepair,actors=new[]{ship.id},target=enemyDock.id}).accepted);
        }

        [Test] public void AColdNavalPlanPreservesItsPhysicalMusterAndLandingRetryState()
        {
            var world=Prepared();var ship=PaidTransport(world,out _);var shore=Shore(world,ship);
            for(int i=0;i<4;i++)Soldier(world,shore);
            var plan=new AiNavalOperations(world,0);var enemies=new List<EntityState>();
            plan.Tick(world.Entities.Where(e=>e.Alive&&e.owner==0).ToList(),enemies,false);world.Tick(.1f);
            var state=SnapshotCodec.Clone(plan.CaptureState());Assert.AreEqual(1,state.phase);Assert.AreEqual(4,state.passengers.Length);
            var restored=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));var otherPlan=new AiNavalOperations(restored,0);otherPlan.RestoreState(state);
            for(int i=0;i<400;i++)
            {
                plan.Tick(world.Entities.Where(e=>e.Alive&&e.owner==0).ToList(),enemies,false);otherPlan.Tick(restored.Entities.Where(e=>e.Alive&&e.owner==0).ToList(),enemies,false);
                world.Tick(.1f);restored.Tick(.1f);
            }
            Assert.AreEqual(JsonUtility.ToJson(plan.CaptureState()),JsonUtility.ToJson(otherPlan.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
            state.landingAttempts=7;Assert.Throws<ArgumentException>(()=>otherPlan.RestoreState(state),"An unbounded retry state must not enter a restored controller");
        }

        [TestCase(314159)]
        [TestCase(271828)]
        public void NormalOpeningAiPaysForTransportAndPhysicallyLandsAcrossUnbridgedWater(int seed)
        {
            var world=new World(Catalog.Load(),seed,"coast","coast",2,MapTopology.GeneratedStrait);var simulation=new MatchSimulation(world,new[]{0},"standard");
            Assert.AreEqual(new Cost(300,300,100),world.Players[0].resources);Assert.AreEqual(1,world.Players[0].age);Assert.IsTrue(world.Layout.RequiresTransport);
            int cursor=world.Events.Count;SimEvent landing=null;
            for(int i=0;i<14000&&landing==null&&world.Winner<0;i++)
            {
                simulation.Step();
                for(;cursor<world.Events.Count;cursor++)if(world.Events[cursor].kind=="NavalLandingCompleted"&&world.Events[cursor].player==0)landing=world.Events[cursor];
            }
            string diagnostic=$"seed={seed} t={world.Time} winner={world.Winner} age={world.Players[0].age} funds={world.Players[0].resources}; "+
                string.Join("; ",world.Entities.Where(e=>e.Alive&&e.owner==0&&(e.def.id=="dock"||e.def.transportShip)).Select(e=>$"{e.id} {e.def.id} {e.position} {e.status} cargo={e.occupants.Count}"));
            Assert.IsNotNull(landing,"Normal AI transport must land within 1400 simulated seconds, without an opposing controller or resource/age/XP/HP grants. "+diagnostic);
            var ship=world.Get(landing.entity);Assert.IsTrue(ship.Alive&&ship.def.transportShip);Assert.GreaterOrEqual(landing.amount,4);
            var paid=world.Events.First(e=>e.kind=="Payment"&&e.player==0&&e.message.StartsWith(ship.def.name+" ×"));
            Assert.IsTrue(paid.hasEconomy);Assert.AreEqual(-world.UnitCost(0,ship.def).wood,paid.economy.actualDelta.wood,.001f);Assert.AreEqual(-world.UnitCost(0,ship.def).coin,paid.economy.actualDelta.coin,.001f);
            Assert.IsTrue(world.Events.Any(e=>e.kind=="BuildingComplete"&&e.player==0&&world.Get(e.entity)?.def.id=="dock"));
            var disembarked=world.Events.Where(e=>e.kind=="Disembarked"&&e.target==ship.id).ToArray();Assert.GreaterOrEqual(disembarked.Length,4);
            foreach(var ev in disembarked)
            {
                var unit=world.Get(ev.entity);Assert.IsTrue(world.Navigation.IsWalkable(ev.position,unit.def.radius));Assert.IsFalse(world.IsWater(ev.position));
                var home=world.Navigation.NearestWalkable(world.OpeningTownPosition(0),unit.def.radius);
                Assert.IsEmpty(world.Navigation.FindPath(home,ev.position,unit.def.radius),"The actual landing crosses disconnected land components");
            }
            int fineAttempts=(int)typeof(World).GetField("fineRecoveryAttempts",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(world);
            int fineFailures=(int)typeof(World).GetField("fineRecoveryFailures",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(world);
            TestContext.WriteLine($"NormalOpeningTransport seed={seed} t={landing.time:0.000} ship={ship.id} landed={disembarked.Length} woodPayment={-paid.economy.actualDelta.wood} coinPayment={-paid.economy.actualDelta.coin} fineAttempts={fineAttempts} fineFailures={fineFailures}; original era-I resources, age-II ceiling, opposing controller absent");
        }

        [Test] public void AWaterBerthBlockedAfterTheOrderSurvivesColdRestoreAndAiChangesToAnOpenShore()
        {
            var world=Prepared();var ship=PaidTransport(world,out _);var home=Shore(world,ship);
            var passengers=Enumerable.Range(0,4).Select(_=>Soldier(world,home)).ToArray();var plan=new AiNavalOperations(world,0);
            plan.Tick(world.Entities.Where(e=>e.Alive&&e.owner==0).ToList(),new List<EntityState>(),false);
            Until(world,()=>passengers.All(e=>e.garrisonedIn==ship.id),90,"The initial muster must really board");
            plan.Tick(world.Entities.Where(e=>e.Alive&&e.owner==0).ToList(),new List<EntityState>(),false);
            Assert.AreEqual(OrderKind.Disembark,ship.orders.Peek().kind);var orderedShore=ship.orders.Peek().point;
            for(int y=1;y<127;y++)for(int x=1;x<127;x++)
            {
                var cell=new Vector2(x,y);
                if(world.IsWater(cell)&&(cell-orderedShore).sqrMagnitude<=17*17)world.WaterNavigation.SetBlocked(cell,0,true);
            }
            Assert.IsFalse(world.WaterNavigation.IsWalkable(ship.orders.Peek().resolvedPoint,ship.def.radius));
            Until(world,()=>ship.orders.Peek().destinationCursor==6&&ship.status.StartsWith("Shore blocked",StringComparison.Ordinal),9,"An invalidated water berth must have a bounded retry state");
            var restored=World.Restore(world.Rules,SnapshotCodec.Clone(world.CaptureState()));var restoredPlan=new AiNavalOperations(restored,0);restoredPlan.RestoreState(SnapshotCodec.Clone(plan.CaptureState()));
            for(int i=0;i<1500&&passengers.Any(e=>e.garrisonedIn!=0);i++)
            {
                plan.Tick(world.Entities.Where(e=>e.Alive&&e.owner==0).ToList(),new List<EntityState>(),false);
                restoredPlan.Tick(restored.Entities.Where(e=>e.Alive&&e.owner==0).ToList(),new List<EntityState>(),false);
                world.Tick(.1f);restored.Tick(.1f);
            }
            Assert.IsTrue(passengers.All(e=>e.garrisonedIn==0),"The AI must choose a different reachable shore instead of leaving cargo permanently stuck");
            var landing=world.Events.Last(e=>e.kind=="Disembarked"&&e.target==ship.id);Assert.Greater(Vector2.Distance(orderedShore,landing.position),17);
            Assert.AreEqual(JsonUtility.ToJson(plan.CaptureState()),JsonUtility.ToJson(restoredPlan.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState()),JsonUtility.ToJson(restored.CaptureState()));
        }

        [TestCase(MapTopology.Winding)]
        [TestCase(MapTopology.GeneratedStrait)]
        public void ClickingAnOffshoreTransportReturnsItToTheLandGroupsReachableShore(string map)
        {
            var world=Prepared(map);var ship=PaidTransport(world,out _);var home=Shore(world,ship);var passenger=Soldier(world,home,"hussar");
            var distant=Shore(world,ship,1);Assert.IsTrue(world.TryTransportShore(ship,distant,passenger.def.radius,out _,out var farBerth));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{ship.id},order=OrderKind.Move,point=farBerth}).accepted);
            Until(world,()=>ship.orders.Count==0,150,"The transport must actually sail to the opposite bank before the pickup request");
            Vector2 away=ship.position;Assert.Greater(Vector2.Distance(away,passenger.position),15);
            var order=world.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=new[]{passenger.id},target=ship.id,point=Vector2.zero});Assert.IsTrue(order.accepted,order.reason);
            Assert.IsTrue(world.Navigation.IsWalkable(ship.orders.Peek().point,passenger.def.radius));
            Assert.IsNotEmpty(world.Navigation.FindPath(passenger.position,ship.orders.Peek().point,passenger.def.radius),"The common shore must belong to the passenger's reachable land");
            world.Tick(.1f);Assert.AreEqual(0,passenger.garrisonedIn);Assert.Greater(Vector2.Distance(away,ship.position),0,"A distant pickup must sail rather than instantly load");
            Until(world,()=>passenger.garrisonedIn==ship.id,150,"The far transport must return to a reachable boarding shore and load the real passenger");
            Assert.Greater(Vector2.Distance(away,ship.position),15);Assert.AreEqual(2,world.TransportLoad(ship));
        }
    }
}
