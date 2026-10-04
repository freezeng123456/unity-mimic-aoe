using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class IndustrialEconomy064Tests
    {
        // These isolated age-IV fixtures test the public shipment/construction
        // lifecycle. Age and XP are controlled inputs, not normal-match proof.
        private World IndustryWorld(string civilization="france",string map=MapTopology.Frontier)
        {
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            var world=new World(rules,civ0:civilization,civ1:"coast",maxAge:5,mapId:map);
            foreach(var entity in world.Entities.ToArray())world.Remove(entity);
            var town=world.Spawn("town",0,new Vector2(20,20));world.Spawn("town",1,new Vector2(110,110));
            for(int i=0;i<6;i++)world.Spawn(world.WorkerDefinition(0).id,0,new Vector2(25+i*1.3f,15));
            world.Spawn("explorer",0,new Vector2(32,25));
            world.Players[0].age=4;world.Players[0].deck.Clear();
            world.Players[0].deck.AddRange(new[]{"factory_wagon","robber_barons"});
            world.Fog.Tick(world.Entities);
            if(world.AutoRecruitSeconds(0)>0)Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.ToggleRecruitment,actors=new[]{town.id}}).accepted);
            return world;
        }

        private static void Steps(World world,float seconds)
        {for(int i=0;i<Mathf.CeilToInt(seconds*10);i++)world.Tick(.1f);}

        private static void Until(World world,Func<bool> complete,float maximumSeconds,string message)
        {
            for(int i=0;i<Mathf.CeilToInt(maximumSeconds*10)&&!complete();i++)world.Tick(.1f);
            if(!complete())
            {
                TestContext.WriteLine("Industrial diagnostic time="+world.Time+" winner="+world.Winner+" factories="+world.Entities.Count(e=>e.Alive&&e.def.id=="factory"));
                foreach(var e in world.Entities.Where(e=>e.Alive&&(e.def.id=="factory_wagon"||e.def.id=="factory")))
                {
                    var order=e.orders.Count>0?e.orders.Peek():null;
                    string detail="id="+e.id+" def="+e.def.id+" pos="+e.position+" previous="+e.previousPosition+" origin="+e.origin+" status="+e.status+" construction="+e.construction+" path="+e.pathIndex+"/"+e.path.Count+" navversion="+e.pathVersion+"/"+world.Navigation.Version+" pathGoal="+e.pathGoal+" stuck="+e.stuckFor+" retry="+e.pathRetryAt;
                    if(order!=null)
                    {
                        var route=world.Navigation.FindPath(e.position,order.resolvedPoint,e.def.radius);
                        detail+=" order="+order.kind+" site="+order.point+" approach="+order.resolvedPoint+" approachDistance="+Vector2.Distance(e.position,order.resolvedPoint)+" approachWalkable="+world.Navigation.IsWalkable(order.resolvedPoint,e.def.radius)+" routeNodes="+route.Count+" routeEnd="+(route.Count>0?route[route.Count-1].ToString():"none")+" arrivalReason="+world.CanBuild(e.owner,"factory",order.point)+" reservationReason="+world.IndustrialDeploymentReason(e.owner,order.point,e.id)+" destinationRetry="+order.destinationRetryAt+" cursor="+order.destinationCursor;
                    }
                    TestContext.WriteLine(detail);
                }
            }
            Assert.IsTrue(complete(),message);
        }

        private static void Send(World world,string card="factory_wagon")
        {
            var player=world.Players[0];
            world.AwardXp(0,world.Rules.initialXp+world.Rules.xpIncrease*player.xpLevels,0,"Controlled industry fixture XP");
            int before=player.shipmentTokens;
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item=card});
            Assert.IsTrue(result.accepted,result.reason);Assert.AreEqual(before-1,player.shipmentTokens);
        }

        private static Vector2 BuildSite(World world,string building)
        {
            world.Fog.Tick(world.Entities);
            for(int y=7;y<=44;y++)for(int x=7;x<=44;x++)
            {
                var point=new Vector2(x,y);
                if(world.CanBuild(0,building,point)==""&&(building!="factory"||world.IndustrialDeploymentReason(0,point)==""))return point;
            }
            Assert.Fail("No currently visible legal "+building+" site");return Vector2.zero;
        }

        private static EntityState Foundation(World world,string card="factory_wagon")
        {
            Send(world,card);float travel=world.Rules.Card(card).travel;
            Steps(world,travel-1);Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.id=="factory_wagon"));
            Until(world,()=>world.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.id=="factory_wagon"),3,"The shipment must deliver a real wagon");
            var wagon=world.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="factory_wagon");
            Assert.IsFalse(wagon.Worker);Assert.IsFalse(wagon.Military);
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Build,item="factory",actors=new[]{wagon.id},point=BuildSite(world,"factory")});
            Assert.IsTrue(result.accepted,result.reason);Assert.IsTrue(wagon.Alive);
            Assert.AreEqual(OrderKind.DeployFactory,wagon.orders.Peek().kind);
            Until(world,()=>!wagon.Alive,60,"A real wagon must navigate to its construction approach");
            var foundation=world.Entities.Last(e=>e.Alive&&e.owner==0&&e.def.id=="factory");
            Assert.IsFalse(foundation.Complete);Assert.Less(foundation.construction,.01f);
            Assert.IsFalse(world.Navigation.IsWalkable(foundation.position,.4f));return foundation;
        }

        private static EntityState ReadyFactory(World world,string card="factory_wagon")
        {
            var factory=Foundation(world,card);
            Until(world,()=>factory.Complete,factory.def.time+1,"The wagon foundation must finish through ordinary ticks");return factory;
        }

        private static CommandResult Mode(World world,EntityState factory,string mode)=>world.Issue(new GameCommand
        {player=0,kind=CommandKind.FactoryMode,actors=new[]{factory.id},item=mode});

        [TestCase("coast","rocket")]
        [TestCase("river","heavy_cannon")]
        [TestCase("france","heavy_cannon")]
        [TestCase("ottoman","great_bombard")]
        public void ShipmentWagonTimedFoundationAndCivilizationArtilleryUsePublicCommands(string civilization,string artillery)
        {
            var world=IndustryWorld(civilization);var factory=Foundation(world);var player=world.Players[0];
            Cost before=player.resources;Steps(world,factory.def.time-1);
            Assert.IsFalse(factory.Complete);Assert.AreEqual(before.food,player.resources.food,.001f);
            Assert.IsFalse(Mode(world,factory,"artillery").accepted,"An unfinished factory cannot change production");
            Until(world,()=>factory.Complete,3,"Timed construction must finish");
            Assert.IsTrue(Mode(world,factory,"artillery").accepted);
            before=player.resources;int population=player.usedPopulation;
            Until(world,()=>world.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.id==artillery),world.FactoryArtillerySeconds(0)+1,"Factory must produce its own civilization's artillery");
            var gun=world.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id==artillery);
            Assert.AreEqual(population+gun.def.population,player.usedPopulation);
            Assert.IsTrue(world.Navigation.IsWalkable(gun.position,gun.def.radius));
            Assert.Greater(world.BuildingFootprintDistance(factory.def,factory.position,gun.position),gun.def.radius);
            Assert.AreEqual(before.food,player.resources.food,.001f);Assert.AreEqual(before.wood,player.resources.wood,.001f);Assert.AreEqual(before.coin,player.resources.coin,.001f);
            Assert.AreEqual(1,world.Events.Count(e=>e.kind=="FactoryArtilleryProduced"&&e.target==gun.id));
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{factory.id},item=artillery}).accepted,"The normal Train command must not bypass the production-mode contract");
        }

        [Test] public void ResourceModesAreExclusiveAndPaidUpgradeChangesOnlyItsResource()
        {
            var world=IndustryWorld();var factory=ReadyFactory(world);var player=world.Players[0];Cost before=player.resources;
            Steps(world,10);Assert.AreEqual(before.food+world.FactoryResourceRate(0,ResourceKind.Food)*10,player.resources.food,.02f);
            Assert.AreEqual(before.wood,player.resources.wood,.001f);Assert.AreEqual(before.coin,player.resources.coin,.001f);
            Assert.IsTrue(Mode(world,factory,"wood").accepted);before=player.resources;Steps(world,10);
            Assert.AreEqual(before.food,player.resources.food,.001f);Assert.AreEqual(before.wood+55,player.resources.wood,.02f);
            float oldFoodRate=world.FactoryResourceRate(0,ResourceKind.Food),oldWoodRate=world.FactoryResourceRate(0,ResourceKind.Wood);
            var upgrade=world.Rules.Tech("factory_cannery");before=player.resources;
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{factory.id},item=upgrade.id}).accepted);
            Assert.AreEqual(before.food-upgrade.cost.food,player.resources.food,.001f);Assert.AreEqual(before.wood-upgrade.cost.wood,player.resources.wood,.001f);
            Assert.IsTrue(world.Events.Any(e=>e.kind=="Payment"&&e.entity==factory.id&&e.hasEconomy));
            var afterPayment=player.resources;var passive=player.passiveIncome;
            Steps(world,upgrade.time-2);
            Assert.IsFalse(player.technologies.Contains(upgrade.id));
            Assert.IsTrue(factory.queue.Any(j=>j.kind=="Research"&&j.item==upgrade.id));
            Assert.AreEqual(afterPayment.food,player.resources.food,.001f);
            Assert.AreEqual(afterPayment.wood,player.resources.wood,.001f);
            Assert.AreEqual(afterPayment.coin,player.resources.coin,.001f);
            Assert.AreEqual(passive.food,player.passiveIncome.food,.001f);
            Assert.AreEqual(passive.wood,player.passiveIncome.wood,.001f);
            Assert.AreEqual(passive.coin,player.passiveIncome.coin,.001f);
            Assert.IsTrue(Mode(world,factory,"artillery").accepted);
            Steps(world,1);Assert.IsFalse(player.technologies.Contains(upgrade.id));
            Assert.AreEqual(0,factory.factoryElapsed,.001f,"Paid research must also pause the artillery clock");
            Until(world,()=>player.technologies.Contains(upgrade.id),3,"Paid factory research must finish");
            float gunProgress=factory.factoryElapsed;Steps(world,1);
            Assert.AreEqual(gunProgress+1,factory.factoryElapsed,.002f,"Artillery production resumes after research");
            Assert.IsTrue(Mode(world,factory,"wood").accepted);
            float resumedWood=player.resources.wood;Steps(world,1);
            Assert.AreEqual(resumedWood+world.FactoryResourceRate(0,ResourceKind.Wood),player.resources.wood,.002f,"Resource production resumes after research");
            Assert.AreEqual(oldFoodRate*1.3f,world.FactoryResourceRate(0,ResourceKind.Food),.001f);
            Assert.AreEqual(oldWoodRate,world.FactoryResourceRate(0,ResourceKind.Wood),.001f);
            world.FlushEconomyLog();Assert.IsTrue(world.Events.Any(e=>e.kind=="FactoryIncome"&&e.message=="food"));
            Assert.IsTrue(world.Events.Any(e=>e.kind=="FactoryIncome"&&e.message=="wood"));
        }

        [Test] public void NoWagonWorkerAndUnseenPlacementCannotCreateOrConsumeAFactory()
        {
            var world=IndustryWorld();var worker=world.Entities.First(e=>e.owner==0&&e.Worker);
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Build,item="factory",actors=new[]{worker.id},point=new Vector2(12,12)}).accepted);
            Send(world);Steps(world,world.Rules.Card("factory_wagon").travel+1);
            var wagon=world.Entities.Single(e=>e.Alive&&e.def.id=="factory_wagon");
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Build,item="factory",actors=new[]{wagon.id},point=new Vector2(80,80)}).accepted);
            Assert.IsTrue(wagon.Alive);Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.def.id=="factory"));
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Build,item="factory",actors=new[]{worker.id},point=BuildSite(world,"factory")}).accepted);
        }

        [Test] public void DistantDeploymentMovesTheWagonAndStopRetainsItsUnspentEntitlement()
        {
            var world=IndustryWorld();Send(world);Steps(world,world.Rules.Card("factory_wagon").travel+1);
            var wagon=world.Entities.Single(e=>e.Alive&&e.def.id=="factory_wagon");Vector2 original=wagon.position;
            var command=new GameCommand{player=0,kind=CommandKind.Build,item="factory",actors=new[]{wagon.id},point=BuildSite(world,"factory")};
            Assert.IsTrue(world.Issue(command).accepted);Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.def.id=="factory"));
            Steps(world,.5f);Assert.Greater(Vector2.Distance(original,wagon.position),.2f);
            Assert.IsTrue(wagon.Alive);Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.def.id=="factory"));
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{wagon.id},order=OrderKind.Stop}).accepted);
            Steps(world,3);Assert.IsTrue(wagon.Alive);Assert.IsEmpty(wagon.orders);
            Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.def.id=="factory"));
            Assert.IsTrue(world.Issue(command).accepted);
            Until(world,()=>!wagon.Alive,60,"A stopped wagon must remain reusable through the same public build command");
            Assert.AreEqual(1,world.Entities.Count(e=>e.Alive&&e.def.id=="factory"));
        }

        [Test] public void OppositeBankDeploymentCrossesARealBridgeAndItsMovingSaveResumesSafely()
        {
            var world=IndustryWorld(map:MapTopology.Winding);
            world.Spawn("explorer",0,new Vector2(94,24));world.Fog.Tick(world.Entities);
            Send(world);Steps(world,world.Rules.Card("factory_wagon").travel+1);
            var wagon=world.Entities.Single(e=>e.Alive&&e.def.id=="factory_wagon");Vector2 site=Vector2.zero;bool found=false;
            for(int y=15;y<=35&&!found;y++)for(int x=86;x<=103&&!found;x++)
                if(world.CanBuild(0,"factory",new Vector2(x,y))==""){site=new Vector2(x,y);found=true;}
            Assert.IsTrue(found,"A real scout must reveal a legal opposite-bank industrial site");
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Build,item="factory",actors=new[]{wagon.id},point=site}).accepted);
            Steps(world,2);Assert.IsTrue(wagon.Alive);Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.def.id=="factory"));
            var restored=World.Restore(world.Rules,world.CaptureState());var loaded=restored.Get(wagon.id);
            Assert.AreEqual(OrderKind.DeployFactory,loaded.orders.Peek().kind);Assert.AreEqual(site,loaded.orders.Peek().point);
            Assert.AreEqual(wagon.orders.Peek().resolvedPoint,loaded.orders.Peek().resolvedPoint);
            bool crossed=false;
            for(int i=0;i<1200&&loaded.Alive;i++)
            {
                Vector2 before=loaded.position;restored.Tick(.1f);
                Assert.IsTrue(restored.Navigation.ClearLine(before,loaded.position,loaded.def.radius),"Every deployment step must respect the same static body sweep");
                Assert.IsFalse(restored.IsWater(loaded.position)&&!restored.IsBridge(loaded.position),"A factory wagon cannot cross water outside a bridge");
                crossed|=restored.IsBridge(loaded.position);
            }
            Assert.IsTrue(crossed,"An opposite-bank wagon must really use a bridge");Assert.IsFalse(loaded.Alive,"The moving save must arrive and deploy within 120 seconds");
            var foundation=restored.Entities.Single(e=>e.Alive&&e.def.id=="factory");Assert.AreEqual(site,foundation.position);
            Assert.IsFalse(foundation.Complete);Assert.AreEqual(1,restored.Events.Count(e=>e.kind=="FactoryFoundationStarted"));
        }

        [Test] public void BothPendingWagonsReserveTheCapAndConsumedCardsCannotRecreateDestroyedFactories()
        {
            var world=IndustryWorld();Send(world);Send(world,"robber_barons");
            Assert.AreEqual(2,world.Players[0].shipments.Count);
            var extra=world.Rules.Card("factory_wagon");Assert.IsNotEmpty(world.IndustrialShipmentReason(0,extra));
            Steps(world,extra.travel*2+1);Assert.AreEqual(2,world.Entities.Count(e=>e.Alive&&e.def.id=="factory_wagon"));
            foreach(var wagon in world.Entities.Where(e=>e.Alive&&e.def.id=="factory_wagon").ToArray())
                Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.Build,item="factory",actors=new[]{wagon.id},point=BuildSite(world,"factory")}).accepted);
            var deploying=world.Entities.Where(e=>e.Alive&&e.def.id=="factory_wagon").ToArray();
            Vector2 firstSite=deploying[0].orders.Peek().point,secondSite=deploying[1].orders.Peek().point;
            Assert.IsTrue(Mathf.Abs(firstSite.x-secondSite.x)>=6||Mathf.Abs(firstSite.y-secondSite.y)>=6,"Pending sites must reserve the same conservative grid clearance as actual CanBuild");
            Until(world,()=>world.Entities.Count(e=>e.Alive&&e.def.id=="factory")==2,60,"Both delivered wagons must physically deploy");
            var first=world.Entities.First(e=>e.Alive&&e.def.id=="factory");
            Assert.IsTrue(world.Issue(new GameCommand{player=0,kind=CommandKind.CancelBuilding,actors=new[]{first.id}}).accepted);
            Assert.IsTrue(world.Navigation.IsWalkable(first.position,.4f));
            int tokens=world.Players[0].shipmentTokens;
            Assert.IsFalse(world.Issue(new GameCommand{player=0,kind=CommandKind.Ship,item="factory_wagon"}).accepted);
            Assert.AreEqual(tokens,world.Players[0].shipmentTokens);
            Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.def.id=="factory_wagon"));
        }

        [Test] public void ArtilleryReadyAtFullPopulationWaitsAndNormalHouseConstructionReleasesIt()
        {
            var world=IndustryWorld();var first=ReadyFactory(world);var second=ReadyFactory(world,"robber_barons");
            var town=world.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id=="town");int workers=world.Entities.Count(e=>e.Alive&&e.owner==0&&e.Worker);
            var hired=world.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{town.id},item=world.WorkerDefinition(0).id});
            Assert.IsTrue(hired.accepted,hired.reason);
            Until(world,()=>world.Entities.Count(e=>e.Alive&&e.owner==0&&e.Worker)==workers+1,30,"One normally paid worker must make the second gun exceed housing");
            Assert.IsTrue(Mode(world,first,"artillery").accepted);Assert.IsTrue(Mode(world,second,"artillery").accepted);
            Steps(world,world.FactoryArtillerySeconds(0)+1);
            Assert.AreEqual(1,world.Entities.Count(e=>e.Alive&&e.def.id=="heavy_cannon"));
            Assert.AreEqual(world.FactoryArtillerySeconds(0),second.factoryElapsed,.001f);
            Assert.That(second.status,Does.Contain("housing"));
            var worker=world.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker);
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Build,item="house",actors=new[]{worker.id},point=BuildSite(world,"house")});
            Assert.IsTrue(result.accepted,result.reason);
            Until(world,()=>world.Entities.Count(e=>e.Alive&&e.def.id=="heavy_cannon")==2,90,"Ordinary paid housing must release the waiting factory artillery");
            Assert.AreEqual(2,world.Events.Count(e=>e.kind=="FactoryArtilleryProduced"));
        }

        [Test] public void StationaryAllyAtTheInitialApproachMakesTheWagonDeployFromAnotherSafeSide()
        {
            var world=IndustryWorld();Send(world);Steps(world,world.Rules.Card("factory_wagon").travel+1);
            var wagon=world.Entities.Single(e=>e.Alive&&e.def.id=="factory_wagon");
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Build,item="factory",actors=new[]{wagon.id},point=BuildSite(world,"factory")});
            Assert.IsTrue(result.accepted,result.reason);var deployment=wagon.orders.Peek();Vector2 originalApproach=deployment.resolvedPoint;
            var blocker=world.Spawn(world.WorkerDefinition(0).id,0,originalApproach);
            Assert.IsTrue(world.Navigation.IsWalkable(blocker.position,blocker.def.radius));
            bool changed=false;
            for(int i=0;i<600&&wagon.Alive;i++)
            {
                world.Tick(.1f);changed|=deployment.hasResolvedPoint&&(deployment.resolvedPoint-originalApproach).sqrMagnitude>.01f;
                if(wagon.Alive)Assert.GreaterOrEqual(Vector2.Distance(wagon.position,blocker.position),wagon.def.radius+blocker.def.radius-.001f);
            }
            Assert.IsTrue(changed,"The real deployment must select an unoccupied construction side");
            Assert.IsFalse(wagon.Alive,"A legal stationary ally cannot permanently trap the factory entitlement");
            Assert.AreEqual(1,world.Entities.Count(e=>e.Alive&&e.def.id=="factory"));Assert.IsTrue(blocker.Alive);
        }

        [Test] public void BlockedExitPreservesReadyProgressAcrossSaveAndSpawnsExactlyOnceAfterRelease()
        {
            var world=IndustryWorld();var factory=ReadyFactory(world);Assert.IsTrue(Mode(world,factory,"artillery").accepted);
            var clearNavigation=world.Navigation.CaptureState();world.Navigation.SetBlocked(factory.position,12,true);
            Steps(world,world.FactoryArtillerySeconds(0)+2);
            Assert.AreEqual(world.FactoryArtillerySeconds(0),factory.factoryElapsed,.001f);Assert.That(factory.status,Does.Contain("blocked"));
            Assert.IsFalse(world.Entities.Any(e=>e.Alive&&e.def.id=="heavy_cannon"));
            var restored=World.Restore(world.Rules,world.CaptureState());var loadedFactory=restored.Get(factory.id);
            Assert.AreEqual("artillery",loadedFactory.factoryMode);Assert.AreEqual(factory.factoryElapsed,loadedFactory.factoryElapsed);
            Steps(restored,2);Assert.IsFalse(restored.Entities.Any(e=>e.Alive&&e.def.id=="heavy_cannon"));
            restored.Navigation.RestoreState(clearNavigation);Steps(restored,.1f);
            Assert.AreEqual(1,restored.Entities.Count(e=>e.Alive&&e.def.id=="heavy_cannon"));
            Assert.AreEqual(0,loadedFactory.factoryElapsed);Steps(restored,1);
            Assert.AreEqual(1,restored.Events.Count(e=>e.kind=="FactoryArtilleryProduced"));
        }

        [Test] public void FreeShipmentConstructionDoesNotMakeFactoryRepairsFree()
        {
            var world=IndustryWorld();var factory=ReadyFactory(world);Assert.IsTrue(Mode(world,factory,"artillery").accepted);
            Assert.AreEqual(0,factory.def.cost.wood);Assert.Greater(factory.def.repairCost.wood,0);
            world.ApplyDamage(factory,200,DamageKind.Siege,1);float damaged=factory.hp;
            var worker=world.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker);
            world.Players[0].resources=new Cost();
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Repair,target=factory.id});
            Assert.IsTrue(result.accepted,result.reason);
            Until(world,()=>world.EdgeDistance(worker,factory)<=1.5f,30,"Normal worker repair must reach the factory perimeter");
            Steps(world,1);Assert.AreEqual(damaged,factory.hp,.001f);Assert.That(worker.status,Does.Contain("resources"));
            world.Players[0].resources=new Cost(0,10,0);float paidBefore=world.Players[0].spent.wood;
            Steps(world,1);Assert.Greater(factory.hp,damaged);Assert.Less(world.Players[0].resources.wood,10);
            Assert.Greater(world.Players[0].spent.wood,paidBefore);
            Assert.IsTrue(world.Events.Any(e=>e.kind=="Payment"&&e.entity==worker.id&&e.target==factory.id&&e.hasEconomy&&e.economy.hasRepair&&e.economy.actualDelta.wood<0));
        }

        [TestCase("shipment")]
        [TestCase("foundation")]
        [TestCase("resource")]
        [TestCase("artillery")]
        public void SavingDuringEachIndustrialStageResumesWithoutDuplicatingTheEntitlement(string stage)
        {
            var world=IndustryWorld();EntityState factory=null;
            if(stage=="shipment"){Send(world);Steps(world,10);}
            else
            {
                factory=Foundation(world);Steps(world,10);
                if(stage!="foundation")
                {
                    Until(world,()=>factory.Complete,factory.def.time,"Factory must complete before production-state capture");
                    Assert.IsTrue(Mode(world,factory,stage=="resource"?"coin":"artillery").accepted);Steps(world,10);
                }
            }
            var restored=World.Restore(world.Rules,world.CaptureState());
            Assert.AreEqual(1,restored.Players[0].shipsSent);Assert.AreEqual(1,restored.Players[0].sentCards.Count);
            if(stage=="shipment")
            {
                Steps(restored,40);Assert.AreEqual(1,restored.Entities.Count(e=>e.Alive&&e.def.id=="factory_wagon"));Assert.IsEmpty(restored.Players[0].shipments);
            }
            else
            {
                var loaded=restored.Get(factory.id);Assert.AreEqual(factory.construction,loaded.construction);Assert.AreEqual(factory.factoryMode,loaded.factoryMode);Assert.AreEqual(factory.factoryElapsed,loaded.factoryElapsed);
                Steps(world,3);Steps(restored,3);
                Assert.AreEqual(world.Players[0].resources.food,restored.Players[0].resources.food,.001f);
                Assert.AreEqual(world.Players[0].resources.coin,restored.Players[0].resources.coin,.001f);
                Assert.AreEqual(factory.construction,loaded.construction,.001f);Assert.AreEqual(factory.factoryElapsed,loaded.factoryElapsed,.001f);
            }
        }

        [TestCase("wayfarer")]
        [TestCase("highland")]
        public void OriginalCivilizationsCannotSelectIndustrialShipments(string civilization)
        {
            var rules=Catalog.Load();Assert.IsFalse(DeckLibrary.CardAllowed(rules,civilization,rules.Card("factory_wagon")));
            Assert.IsFalse(DeckLibrary.CardAllowed(rules,civilization,rules.Card("robber_barons")));
        }
    }
}
