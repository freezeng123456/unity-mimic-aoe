using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // Every objective, reward, injury and repair below comes from accepted
    // player commands and the ordinary fixed scheduler. No game state is injected.
    public sealed class RiverMissionBranches070Tests
    {
        RuleCatalog rules;string directory;
        [SetUp] public void SetUp()
        {rules=Catalog.Load();directory=Path.Combine(Path.GetTempPath(),"frontier-mission-branches070-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);}
        [TearDown] public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        static ScenarioObjectiveState Goal(GuidedPlaySession s,string id)=>s.World.ScenarioObjectiveStates.Single(o=>o.id==id);
        static ScenarioTriggerState Trigger(GuidedPlaySession s,string id)=>s.World.ScenarioState.triggerStates.Single(t=>t.id==id);
        static ScenarioRegion Region(GuidedPlaySession s,string id)=>s.World.ScenarioDocument.regions.Single(r=>r.id==id);
        static int[] Alive(GuidedPlaySession s,int[] ids)=>ids.Where(id=>s.World.Get(id)?.Alive==true&&s.World.Get(id).garrisonedIn==0).ToArray();
        static Vector2 ChannelPoint(RiverChannel channel,float z)=>channel.Centers.OrderBy(p=>Mathf.Abs(p.y-z)).First();
        static string Explain(GuidedPlaySession s)
        {
            var soldiers=s.CaptureState().soldiers;
            return " t="+s.World.Time+" status="+s.Status+" stage="+s.Stage?.Id+" reason="+s.Reason+
            " actors="+string.Join(";",s.World.Entities.Where(e=>e.owner==0&&(e.def.waterborne||soldiers.Contains(e.id)))
                .Select(e=>e.id+":"+e.def.id+"@"+e.position+" hp="+e.hp+" state="+e.status+" order="+(e.orders.Count==0?"none":e.orders.Peek().kind.ToString())))+
            " cache="+s.World.ScenarioEntity("supply-cache")?.hp+" river-wave="+s.World.ScenarioEntity("river-wave")?.hp;
        }
        static void Accepted(GuidedPlaySession s,GameCommand c)
        {var result=s.Issue(c);Assert.IsTrue(result.accepted,result.reason+Explain(s));}
        static void Order(GuidedPlaySession s,int[] actors,OrderKind kind,Vector2 point,int target=0)
        =>Accepted(s,new GameCommand{player=0,kind=CommandKind.Order,actors=Alive(s,actors),order=kind,point=point,target=target});
        static void Until(GuidedPlaySession s,Func<bool> condition,float seconds,string why)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds*10)&&!condition()&&s.Status==GuidedPlayStatus.Running;i++)s.Simulation.Step();
            Assert.IsTrue(condition(),why+Explain(s));
        }
        static void StepSeconds(GuidedPlaySession s,float seconds)
        {for(int i=0;i<Mathf.CeilToInt(seconds*10);i++){Assert.AreEqual(GuidedPlayStatus.Running,s.Status,Explain(s));Assert.IsTrue(s.Simulation.Step());}}
        static SimEvent[] Grants(GuidedPlaySession s)=>s.World.Events.Where(e=>e.player==0&&e.kind=="ScenarioResourceGranted").ToArray();
        static void AssertDelivery(GuidedPlaySession s,string trigger,float wood,float coin)
        {
            Assert.AreEqual(1,Trigger(s,trigger).fires,trigger+" must fire only once");Assert.AreEqual("",Trigger(s,trigger).lastError);
            var receipts=Grants(s);Assert.AreEqual((wood>0?1:0)+(coin>0?1:0),receipts.Length);
            Assert.IsTrue(receipts.All(e=>e.hasEconomy&&e.economy!=null));
            Assert.AreEqual(wood,receipts.Sum(e=>e.economy.actualDelta.wood),.001f);
            Assert.AreEqual(coin,receipts.Sum(e=>e.economy.actualDelta.coin),.001f);
            Assert.AreEqual(0,receipts.Sum(e=>e.economy.actualDelta.food),.001f);
        }
        GuidedPlaySession Cold(GuidedPlaySession s,int slot=1)
        {
            s.World.Paused=true;var world=s.World.CaptureState();var guided=s.CaptureState();
            string path=MatchSaveStore.SlotPath(directory,slot);
            MatchSaveStore.Write(path,new SavedMatch{matchId=s.AttemptId,appVersion="branches070-tests",world=world,
                simulation=s.Simulation.CaptureState(),guided=guided,cameraSize=26,simulationSpeed=1});
            var saved=MatchSaveStore.Read(path,"branches070-tests");var restoredWorld=World.Restore(rules,saved.world);
            var simulation=MatchSimulation.Restore(restoredWorld,saved.simulation);string before=JsonUtility.ToJson(restoredWorld.CaptureState());
            var restored=GuidedPlaySession.Restore(restoredWorld,simulation,saved.guided);
            Assert.AreEqual(before,JsonUtility.ToJson(restoredWorld.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(guided),JsonUtility.ToJson(restored.CaptureState()));
            Assert.AreEqual(world.entities.Length,restoredWorld.Entities.Count);Assert.AreEqual(world.events.Length,restoredWorld.Events.Count);
            return restored;
        }
        static void MoveExplorer(GuidedPlaySession s,Vector2 point)
        {
            int scout=s.CaptureState().scout;Order(s,new[]{scout},OrderKind.Move,point);
            Until(s,()=>s.World.Get(scout).Alive&&s.World.Get(scout).orders.Count==0&&(s.World.Get(scout).position-point).sqrMagnitude<1,
                120,"The explorer must really finish the assigned movement");
        }
        // The same human-scale choices as the already successful defense solver:
        // stay beside the shooters, focus visible attackers, then return to stations.
        static void DefendPort(GuidedPlaySession s)
        {
            var state=s.CaptureState();var centre=Region(s,"port-perimeter").center;
            var stations=state.soldiers.ToDictionary(id=>id,id=>s.World.Get(id).position);
            Order(s,state.soldiers,OrderKind.Hold,centre);Order(s,new[]{state.scout},OrderKind.Move,centre+new Vector2(0,-5));
            var escort=s.World.ScenarioEntity("escort");Order(s,new[]{escort.id},OrderKind.AttackMove,ChannelPoint(s.World.Layout.Channels[0],82));
            int selected=0;bool regrouping=false;
            for(int decision=0;decision<220&&s.Status==GuidedPlayStatus.Running&&s.Stage?.Id=="hold";decision++)
            {
                var old=s.World.Get(selected);
                var threat=old?.Alive==true&&s.World.IsVisible(0,old)?old:
                    s.World.Entities.Where(e=>e.Alive&&e.owner==1&&e.def.kind==EntityKind.Unit&&!e.def.waterborne&&!e.Worker&&
                        s.World.IsVisible(0,e)&&(e.position-centre).sqrMagnitude<24*24)
                    .OrderBy(e=>e.def.id=="hussar"?0:1).ThenBy(e=>(e.position-centre).sqrMagnitude).FirstOrDefault();
                if(threat!=null&&threat.id!=selected)
                {
                    var actors=Alive(s,state.soldiers).ToList();var scout=s.World.Get(state.scout);
                    if(scout.Alive&&(scout.position-centre).sqrMagnitude<20*20)actors.Add(scout.id);
                    Order(s,actors.ToArray(),OrderKind.Attack,threat.position,threat.id);selected=threat.id;regrouping=false;
                }
                else if(threat==null&&selected!=0)
                {
                    foreach(int id in Alive(s,state.soldiers))Order(s,new[]{id},OrderKind.Move,stations[id]);
                    selected=0;regrouping=true;
                }
                else if(threat==null&&regrouping&&Alive(s,state.soldiers).All(id=>(s.World.Get(id).position-stations[id]).sqrMagnitude<1))
                {Order(s,state.soldiers,OrderKind.Hold,centre);regrouping=false;}
                for(int step=0;step<10&&s.Status==GuidedPlayStatus.Running&&s.Stage?.Id=="hold";step++)s.Simulation.Step();
            }
            Assert.AreEqual(GuidedPlayStatus.Running,s.Status,Explain(s));Assert.AreEqual("withdraw",s.Stage.Id);
            Assert.AreEqual("completed",Goal(s,"hold").status);
            // Match the runtime's completion tolerance for accumulated fixed steps.
            Assert.GreaterOrEqual(Goal(s,"hold").progress+.0001f,40);
            Assert.IsTrue(s.World.ScenarioEntity("forward-port").Alive);Assert.GreaterOrEqual(Alive(s,state.soldiers).Length,4);
            foreach(string wave in new[]{"land-wave-one","land-wave-two","water-wave"})Assert.AreEqual(1,Trigger(s,wave).fires);
        }
        static void DestroyCache(GuidedPlaySession s)
        {
            var cache=s.World.ScenarioEntity("supply-cache");Assert.IsTrue(cache.Alive,"The optional cache must still require actual combat");
            var before=s.World.Players[0].resources;
            if(!s.World.IsVisible(0,cache))
            {
                var near=s.World.Navigation.NearestWalkable(cache.position+new Vector2(-8,-4),.4f);
                Order(s,s.CaptureState().soldiers,OrderKind.AttackMove,near);
                Until(s,()=>s.World.IsVisible(0,cache),90,"Reveal the optional cache through ordinary movement");
            }
            int acceptedIndex=s.World.Events.Count;
            Order(s,s.CaptureState().soldiers,OrderKind.Attack,cache.position,cache.id);
            Until(s,()=>!cache.Alive&&Goal(s,"cache").status=="completed"&&Trigger(s,"cache-supply").fires==1,
                160,"Destroy the optional market with a normal specified-target attack");
            Assert.IsTrue(s.CaptureState().commands.Any(p=>p.eventIndex>=acceptedIndex&&p.command.order==OrderKind.Attack&&p.command.target==cache.id));
            Assert.IsTrue(s.World.Events.Any(e=>e.kind=="Death"&&e.entity==cache.id));
            Assert.AreEqual(before.wood+100,s.World.Players[0].resources.wood,.001f);
            Assert.AreEqual(before.coin+80,s.World.Players[0].resources.coin,.001f);
            AssertDelivery(s,"cache-supply",100,80);
        }
        static void DefendCampUntil(GuidedPlaySession s,Func<bool> done,float seconds)
        {
            var state=s.CaptureState();var town=s.World.Get(state.town);
            var stations=state.soldiers.ToDictionary(id=>id,id=>s.World.Get(id).position);
            Order(s,state.soldiers,OrderKind.Hold,town.position);Order(s,new[]{state.scout},OrderKind.Hold,s.World.Get(state.scout).position);
            int selected=0;bool regrouping=false;
            for(int decision=0;decision<Mathf.CeilToInt(seconds)&&!done()&&s.Status==GuidedPlayStatus.Running;decision++)
            {
                var old=s.World.Get(selected);
                var threat=old?.Alive==true&&s.World.IsVisible(0,old)?old:
                    s.World.Entities.Where(e=>e.Alive&&e.owner==1&&e.Military&&!e.def.waterborne&&
                        s.World.IsVisible(0,e)&&(e.position-town.position).sqrMagnitude<40*40)
                    .OrderBy(e=>e.def.id=="hussar"?0:1).ThenBy(e=>(e.position-town.position).sqrMagnitude).FirstOrDefault();
                if(threat!=null&&threat.id!=selected)
                {Order(s,state.soldiers,OrderKind.Attack,threat.position,threat.id);selected=threat.id;regrouping=false;}
                else if(threat==null&&selected!=0)
                {
                    foreach(int id in Alive(s,state.soldiers))Order(s,new[]{id},OrderKind.Move,stations[id]);
                    selected=0;regrouping=true;
                }
                else if(threat==null&&regrouping&&Alive(s,state.soldiers).All(id=>(s.World.Get(id).position-stations[id]).sqrMagnitude<1))
                {Order(s,state.soldiers,OrderKind.Hold,town.position);regrouping=false;}
                for(int step=0;step<10&&!done()&&s.Status==GuidedPlayStatus.Running;step++)s.Simulation.Step();
            }
            Assert.IsTrue(done(),"The real camp defense must preserve the intended deadline path"+Explain(s));
            Assert.IsTrue(town.Alive);Assert.IsTrue(s.World.Get(state.scout).Alive);Assert.GreaterOrEqual(Alive(s,state.soldiers).Length,2);
            Assert.AreEqual(0,Trigger(s,"survey-ambush").fires,"No survey means the authored camp raid must not fire");
        }

        [Test] public void ActualLookoutVisitAndRepeatedColdVisitsDeliverExactlyOneWoodShipment()
        {
            using(var source=GuidedPlaySession.Start(rules,RiverContent070.Scout))
            {
                var before=source.World.Players[0].resources;var state=source.CaptureState();
                Order(source,state.soldiers,OrderKind.Hold,source.World.Get(state.town).position);
                // Approach along the same mainland without walking through the
                // idle raiders or completing the separate far-bank survey.
                MoveExplorer(source,source.World.Navigation.NearestWalkable(new Vector2(14,66),.4f));
                MoveExplorer(source,Region(source,"lookout-zone").center);
                Assert.AreEqual("completed",Goal(source,"lookout").status);Assert.AreEqual("active",Goal(source,"survey").status);
                Assert.AreEqual(before.wood+120,source.World.Players[0].resources.wood,.001f);
                Assert.AreEqual(before.food,source.World.Players[0].resources.food,.001f);Assert.AreEqual(before.coin,source.World.Players[0].resources.coin,.001f);
                AssertDelivery(source,"lookout-supply",120,0);
                using(var cold=Cold(source))
                {
                    var after=cold.World.Players[0].resources;cold.World.Paused=false;
                    var zone=Region(cold,"lookout-zone");
                    for(int repeat=0;repeat<2;repeat++)
                    {
                        var outside=cold.World.Navigation.NearestWalkable(zone.center+new Vector2(-12,0),.4f);
                        MoveExplorer(cold,outside);Assert.Greater(Mathf.Abs(cold.World.Get(state.scout).position.x-zone.center.x),zone.size.x*.5f);
                        MoveExplorer(cold,zone.center);StepSeconds(cold,2);
                        AssertDelivery(cold,"lookout-supply",120,0);
                        Assert.AreEqual(after.wood,cold.World.Players[0].resources.wood,.001f);
                        Assert.AreEqual(after.coin,cold.World.Players[0].resources.coin,.001f);
                    }
                    Assert.AreEqual("survey",cold.Stage.Id);Assert.AreEqual(0,cold.CaptureState().stageEvents.Length);
                }
            }
        }
        [Test] public void ActualCacheDestructionPaysOneRepairDeliveryAndColdRestoreDoesNotRepeatIt()
        {
            using(var source=GuidedPlaySession.Start(rules,RiverContent070.Retreat))
            {
                DefendPort(source);DestroyCache(source);
                using(var cold=Cold(source))
                {
                    var after=cold.World.Players[0].resources;cold.World.Paused=false;StepSeconds(cold,12);
                    AssertDelivery(cold,"cache-supply",100,80);Assert.AreEqual("completed",Goal(cold,"cache").status);
                    Assert.AreEqual(after.wood,cold.World.Players[0].resources.wood,.001f);Assert.AreEqual(after.coin,cold.World.Players[0].resources.coin,.001f);
                    Assert.AreEqual("withdraw",cold.Stage.Id);Assert.GreaterOrEqual(Alive(cold,cold.CaptureState().soldiers).Length,4);
                    foreach(string wave in new[]{"land-wave-one","land-wave-two","water-wave"})Assert.AreEqual(1,Trigger(cold,wave).fires);
                }
            }
        }
        [Test] public void RealCounterattackDamageIsRepairedAtTheForwardDockWithActualPaidHealthReceipts()
        {
            using(var source=GuidedPlaySession.Start(rules,RiverContent070.Retreat))
            {
                DefendPort(source);DestroyCache(source);
                using(var cold=Cold(source))
                {
                    cold.World.Paused=false;var escort=cold.World.ScenarioEntity("escort");Assert.IsTrue(escort.Alive,"The actual escort must survive to reach repairs");
                    var enemy=cold.World.ScenarioEntity("river-wave");Assert.IsNotNull(enemy,"The actual naval counterattack must have spawned");
                    if(enemy.Alive)
                    {
                        if(!cold.World.IsVisible(0,enemy))
                        {
                            Order(cold,new[]{escort.id},OrderKind.AttackMove,ChannelPoint(cold.World.Layout.Channels[0],82));
                            Until(cold,()=>cold.World.IsVisible(0,enemy),60,"Reveal the actual counterattack boat before naming it");
                        }
                        Order(cold,new[]{escort.id},OrderKind.Attack,enemy.position,enemy.id);
                        Until(cold,()=>!enemy.Alive&&escort.Alive,100,"The escort must finish the real duel before returning to the dock");
                    }
                    StepSeconds(cold,2);Assert.IsTrue(escort.Alive);Assert.Less(escort.hp,cold.World.MaxHp(escort)-.01f,
                        "This test requires genuine combat damage; no health injection is permitted");
                    var dock=cold.World.ScenarioEntity("forward-port");Assert.IsTrue(dock.Alive);
                    float hpBefore=escort.hp;var before=cold.World.Players[0].resources;int eventStart=cold.World.Events.Count;
                    Accepted(cold,new GameCommand{player=0,kind=CommandKind.DockRepair,actors=new[]{escort.id},target=dock.id});
                    Until(cold,()=>escort.Alive&&escort.orders.Count==0&&escort.hp>=cold.World.MaxHp(escort)-.01f,120,
                        "The actually damaged boat must sail to the real port and finish paid repairs");
                    var payments=cold.World.Events.Skip(eventStart).Where(e=>e.kind=="Payment"&&e.entity==escort.id&&e.hasEconomy&&e.economy.hasRepair).ToArray();
                    Assert.IsNotEmpty(payments);Assert.IsTrue(payments.All(e=>e.target==dock.id&&e.economy.healthTarget==escort.id&&e.economy.hpAfter>e.economy.hpBefore));
                    Assert.AreEqual(hpBefore,payments.First().economy.hpBefore,.001f);Assert.AreEqual(escort.hp,payments.Last().economy.hpAfter,.001f);
                    Assert.AreEqual(escort.hp-hpBefore,payments.Sum(e=>e.economy.hpAfter-e.economy.hpBefore),.02f);
                    Assert.Less(cold.World.Players[0].resources.wood,before.wood);Assert.Less(cold.World.Players[0].resources.coin,before.coin);
                    Assert.AreEqual(before.wood+payments.Sum(e=>e.economy.actualDelta.wood),cold.World.Players[0].resources.wood,.02f);
                    Assert.AreEqual(before.coin+payments.Sum(e=>e.economy.actualDelta.coin),cold.World.Players[0].resources.coin,.02f);
                    Assert.AreEqual(before.food,cold.World.Players[0].resources.food,.001f);AssertDelivery(cold,"cache-supply",100,80);
                }
            }
        }
        [Test] public void TheSurveyDeadlineFailsThroughRealSchedulerStepsAndRetryHasNoOldRewardOrResult()
        {
            using(var source=GuidedPlaySession.Start(rules,RiverContent070.Scout))
            {
                string attempt=source.AttemptId;string hash=source.World.ScenarioHash;
                DefendCampUntil(source,()=>source.World.Time>=180,190);Assert.AreEqual("active",Goal(source,"survey").status);
                Assert.GreaterOrEqual(Goal(source,"survey").elapsed,179.9f);Assert.AreEqual(0,Grants(source).Length);
                using(var cold=Cold(source))
                {
                    cold.World.Paused=false;
                    DefendCampUntil(cold,()=>cold.Status==GuidedPlayStatus.Failed,190);
                    Assert.AreEqual("failed",Goal(cold,"survey").status);Assert.AreEqual("Deadline expired",Goal(cold,"survey").reason);
                    Assert.GreaterOrEqual(cold.World.Time,359.9f);Assert.Less(cold.World.Time,360.2f);
                    Assert.AreEqual(1,cold.World.Winner);Assert.IsTrue(cold.World.Paused);Assert.AreEqual("locked",Goal(cold,"blockade").status);
                    Assert.AreEqual(1,cold.World.Events.Count(e=>e.kind=="ScenarioObjectiveFailed"&&e.player==0&&e.message=="survey: Deadline expired"));
                    Assert.AreEqual(0,Grants(cold).Length);Assert.AreEqual(0,cold.CaptureState().stageEvents.Length);
                    var account=AccountProgressStore.Load(memoryOnly:true);Assert.IsFalse(account.RecordCompletion(cold));Assert.IsFalse(account.IsUnlocked(RiverContent070.Convoy));
                    using(var failed=Cold(cold,2))
                    {
                        Assert.AreEqual(GuidedPlayStatus.Failed,failed.Status);Assert.AreEqual(attempt,failed.AttemptId);
                        using(var retry=failed.Retry())
                        {
                            Assert.AreNotEqual(attempt,retry.AttemptId);Assert.AreEqual(hash,retry.World.ScenarioHash);Assert.AreEqual(0,retry.World.Time);
                            Assert.AreEqual(GuidedPlayStatus.Running,retry.Status);Assert.AreEqual("active",Goal(retry,"survey").status);
                            Assert.AreEqual(0,Goal(retry,"survey").elapsed);Assert.AreEqual(0,Grants(retry).Length);
                            Assert.AreEqual(0,retry.CaptureState().stageEvents.Length);Assert.IsTrue(retry.World.ScenarioState.triggerStates.All(t=>t.fires==0));
                            Assert.AreEqual(200,retry.World.Players[0].resources.food);Assert.AreEqual(180,retry.World.Players[0].resources.wood);Assert.AreEqual(100,retry.World.Players[0].resources.coin);
                        }
                    }
                }
            }
        }
    }
}
