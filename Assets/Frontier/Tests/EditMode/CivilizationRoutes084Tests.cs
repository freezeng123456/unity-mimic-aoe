using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    // This driver spends only the ordinary opening and genuine income. It
    // never assigns resources/age/technology, spawns entities, or damages a target.
    internal static class NormalPaidCivilization084Driver
    {
        static GuidedPlaySession observed;
        static string lastCommand="",lastResult="";
        internal static void ResetEvidence(){observed=null;lastCommand=lastResult="";}
        internal static GuidedPlaySession Observe(GuidedPlaySession session){observed=session;return session;}
        internal static GuidedPlaySession Start(RuleCatalog rules,string id)=>Observe(GuidedPlaySession.Start(rules,id));
        internal static void ArchiveFailure()
        {
            if(observed==null)return;
            string output=Environment.GetEnvironmentVariable("FRONTIER084_CIV_EVIDENCE");
            if(string.IsNullOrEmpty(output))output=Environment.GetEnvironmentVariable("FRONTIER084_EVIDENCE");
            if(string.IsNullOrEmpty(output))output=Environment.GetEnvironmentVariable("FRONTIER082_EVIDENCE");
            if(string.IsNullOrEmpty(output))output=Path.Combine(Path.GetTempPath(),"Frontier-civilization084-failures");
            string name=new string(TestContext.CurrentContext.Test.Name.Select(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'_').ToArray());
            string directory=Path.Combine(output,"Civilization084Failures",name+"-"+Guid.NewGuid().ToString("N"));
            try{Directory.CreateDirectory(directory);}
            catch(Exception error)
            {
                TestContext.Error.WriteLine("Requested failure archive could not be created: "+error);
                directory=Path.Combine(Path.GetTempPath(),"Frontier-civilization084-failures",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            }
            var context=TestContext.CurrentContext;
            File.WriteAllText(Path.Combine(directory,"failure.txt"),"test="+context.Test.FullName+"\nroute="+observed.Definition.Id+"\nattempt="+observed.AttemptId+"\nstage="+observed.Stage?.Id+"\nsimulationTime="+observed.World.Time+"\nlastCommand="+lastCommand+"\nlastResult="+lastResult+"\n"+context.Result.Message+"\n"+context.Result.StackTrace);
            // Each raw part is independent: preserve the other scene parts even
            // when the failing state prevents one capture. Do not flush or tick.
            bool complete=true;
            complete&=ArchivePart(directory,"world",()=>observed.World.CaptureState());
            complete&=ArchivePart(directory,"simulation",()=>observed.Simulation.CaptureState());
            complete&=ArchivePart(directory,"guided",()=>observed.CaptureState());
            complete&=ArchivePart(directory,"rules",()=>observed.World.Rules);
            TestContext.Error.WriteLine("Civilization084 failure scene (complete="+complete+"): "+directory);
            if(!complete)Assert.Fail("A failure scene part could not be archived; see *.capture-error.txt in "+directory);
        }
        static bool ArchivePart(string directory,string name,Func<object> capture)
        {
            try{File.WriteAllText(Path.Combine(directory,name+".json"),JsonUtility.ToJson(capture(),true));return true;}
            catch(Exception error){File.WriteAllText(Path.Combine(directory,name+".capture-error.txt"),error.ToString());return false;}
        }
        internal static void Accepted(GuidedPlaySession s,GameCommand c)
        {Observe(s);lastCommand=JsonUtility.ToJson(c);var r=s.Issue(c);lastResult=r.accepted+":"+r.reason;Assert.IsTrue(r.accepted,r.reason+"; "+s.Definition.Id+" / "+s.Stage?.Id+" @ "+s.World.Time+" actors="+WorkerState(s,c.actors));}
        internal static int[] Workers(GuidedPlaySession s)=>s.World.Entities.Where(e=>e.owner==0&&e.Alive&&e.Complete&&e.Worker&&e.def.kind==EntityKind.Unit&&!e.def.waterborne&&e.garrisonedIn==0).Select(e=>e.id).ToArray();
        static string WorkerState(GuidedPlaySession s,int[] ids)=>string.Join(";",ids.Select(id=>
        {
            var e=s.World.Get(id);if(e==null)return id+":missing";
            var host=s.World.Get(e.garrisonedIn);
            return id+":"+e.def.id+" alive="+e.Alive+" owner="+e.owner+" kind="+e.def.kind+" host="+e.garrisonedIn+":"+host?.def.id+" orders="+string.Join(",",e.orders.Select(o=>o.kind.ToString()));
        }));
        static int[] ReadyWorkers(GuidedPlaySession s)
        {
            var workers=Workers(s);if(workers.Length>0)return workers;
            var sheltered=s.World.Entities.Where(e=>e.owner==0&&e.Alive&&e.Worker&&e.garrisonedIn!=0).ToArray();
            Assert.IsNotEmpty(sheltered,"No ordinary mobile or sheltered worker remains; "+s.Definition.Id+" @ "+s.World.Time);
            var ids=sheltered.Select(e=>e.id).ToArray();
            // Ungarrison mutates these same EntityState objects. Freeze the host
            // IDs before issuing it, or lazy Select/Distinct observes a new host 0.
            var hosts=sheltered.Select(e=>e.garrisonedIn).Distinct().ToArray();
            var commands=s.CaptureState().commands;
            var witnesses=s.World.Events.Where(e=>e.kind=="Garrison"&&ids.Contains(e.entity)).Reverse().Take(8).Reverse().Select(e=>
            {
                var hit=s.World.Events.LastOrDefault(h=>h.kind=="Damage"&&h.target==e.entity&&h.time<=e.time);
                var gather=commands.LastOrDefault(p=>p.command.kind==CommandKind.Order&&p.command.order==OrderKind.Gather&&p.command.actors.Contains(e.entity));
                var resource=gather==null?null:s.World.Get(gather.command.target);
                int explicitGarrison=commands.Count(p=>p.command.kind==CommandKind.Order&&p.command.order==OrderKind.Garrison&&p.command.actors.Contains(e.entity));
                return e.time.ToString("0.0")+":"+e.entity+"->"+e.target+"; lastDamage="+(hit==null?"none":hit.time.ToString("0.0")+":"+hit.entity+"/"+s.World.Get(hit.entity)?.def.id+" owner="+hit.player+" amount="+hit.amount)+"; lastGatherTarget="+(resource==null?"none":resource.id+":"+resource.def.id+" kind="+resource.def.kind+" resource="+resource.def.resource)+"; publicGarrisonCommands="+explicitGarrison;
            });
            TestContext.WriteLine("Driver resumes real sheltered workers through public Ungarrison @ "+s.World.Time.ToString("0.0")+"; "+WorkerState(s,ids)+"; actual Garrison witnesses="+string.Join(";",witnesses));
            foreach(int host in hosts)
            {
                var building=s.World.Get(host);Assert.IsTrue(building!=null&&building.Alive&&building.owner==0&&building.Complete&&building.def.kind==EntityKind.Building&&building.def.garrison>0,"Only a real owned completed garrison building can release these workers: host="+host+"; "+WorkerState(s,ids));
                Accepted(s,new GameCommand{player=0,kind=CommandKind.Ungarrison,actors=new[]{host}});
                Assert.IsTrue(ids.Where(id=>s.World.Get(id)?.Alive==true).All(id=>s.World.Get(id).garrisonedIn!=host),"Public Ungarrison must release the actual surviving occupants from host "+host);
            }
            workers=Workers(s);Assert.IsNotEmpty(workers,"The normal public garrison exit must actually release a mobile worker; "+WorkerState(s,ids));return workers;
        }
        internal static void Order(GuidedPlaySession s,int[] ids,OrderKind kind,Vector2 point,int target=0)
            =>Accepted(s,new GameCommand{player=0,kind=CommandKind.Order,actors=ids,order=kind,point=point,target=target});
        internal static void Until(GuidedPlaySession s,Func<bool> condition,float seconds,string reason)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds*10)&&!condition()&&s.Status==GuidedPlayStatus.Running;i++)s.Simulation.Step();
            Assert.IsTrue(condition(),reason+"; "+s.Definition.Id+" / "+s.Stage?.Id+" @ "+s.World.Time+" status="+s.Status+" "+s.Reason);
        }
        static EntityState Resource(GuidedPlaySession s,ResourceKind kind,int[] workers)
        {
            var w=s.World;var centre=w.Get(workers[0]).position;
            return w.Entities.Where(e=>e.Alive&&e.Complete&&e.def.resource==kind&&(e.remaining>0||e.def.id=="farm")&&w.IsVisible(0,e)&&!e.def.fishingBoat&&!e.def.waterborne)
                .OrderBy(e=>(e.position-centre).sqrMagnitude).FirstOrDefault();
        }
        internal static void Save(GuidedPlaySession s,Cost cost)
        {
            var w=s.World;float started=w.Time;ResourceKind gathering=ResourceKind.None;int target=0;
            while(!w.CanAfford(0,cost)&&s.Status==GuidedPlayStatus.Running&&w.Time-started<1400)
            {
                var p=w.Players[0];var kind=p.resources.food+.01f<cost.food?ResourceKind.Food:p.resources.wood+.01f<cost.wood?ResourceKind.Wood:ResourceKind.Coin;
                var workers=ReadyWorkers(s);
                if(kind!=gathering||w.Get(target)?.Alive!=true||w.Get(target).remaining<=0&&w.Get(target).def.id!="farm"||workers.All(id=>w.Get(id).orders.Count==0))
                {
                    var resource=Resource(s,kind,workers);
                    if(resource==null)
                    {
                        var scout=w.Get(s.CaptureState().scout);var home=w.OpeningTownPosition(0);
                        Order(s,new[]{scout.id},OrderKind.Move,home+new Vector2(9,25));
                        Until(s,()=>Resource(s,kind,workers)!=null||(scout.position-(home+new Vector2(9,25))).sqrMagnitude<16,90,"Scout own forward resources");
                        workers=ReadyWorkers(s);resource=Resource(s,kind,workers);
                    }
                    if(resource==null&&kind==ResourceKind.Food){Build(s,"farm");workers=ReadyWorkers(s);resource=Resource(s,kind,workers);}
                    Assert.IsNotNull(resource,"No visible ordinary resource for "+kind);
                    // Scouting or ordinary farm construction above can advance the World.
                    // Re-select current public mobile actors at the actual command boundary.
                    workers=ReadyWorkers(s);gathering=kind;target=resource.id;Order(s,workers,OrderKind.Gather,resource.position,resource.id);
                }
                s.Simulation.Step();
            }
            Assert.IsTrue(w.CanAfford(0,cost),"Real income must afford "+cost+" at "+w.Time+" stage "+s.Stage?.Id);
        }
        internal static Vector2 Site(GuidedPlaySession s,string item)
        {
            var w=s.World;var home=w.OpeningTownPosition(0);
            for(int ring=0;ring<=32;ring++)for(int y=-ring;y<=ring;y++)for(int x=-ring;x<=ring;x++)
            {if(Math.Max(Math.Abs(x),Math.Abs(y))!=ring)continue;var p=home+new Vector2(x,y);if(w.CanBuild(0,item,p)=="")return p;}
            Assert.Fail("No visible paid site for "+item);return Vector2.zero;
        }
        internal static EntityState Build(GuidedPlaySession s,string item)
        {
            var w=s.World;Save(s,w.EntityCost(0,w.Rules.Entity(item)));var point=Site(s,item);
            Accepted(s,new GameCommand{player=0,kind=CommandKind.Build,actors=ReadyWorkers(s),item=item,point=point});
            var foundation=w.Entities.Last(e=>e.owner==0&&e.def.id==item&&!e.Complete);
            Until(s,()=>foundation.Complete,150,"Real paid construction must finish "+item);return foundation;
        }
        internal static void Train(GuidedPlaySession s,string item,int count)
        {
            var w=s.World;var d=w.Rules.Entity(item);
            while(w.Players[0].usedPopulation+w.Players[0].reservedPopulation+d.population*count>w.Players[0].populationCap)Build(s,"house");
            Save(s,w.UnitCost(0,d)*count);var producer=w.Entities.First(e=>e.Alive&&e.owner==0&&e.Complete&&e.def.id==d.producer);
            Accepted(s,new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{producer.id},item=item,count=count});
        }
        internal static void QueueCurrent(GuidedPlaySession s)
        {
            var stage=s.Stage;var w=s.World;
            switch(stage.Goal)
            {
                case GuidedPlayGoal.PaidBuilding084:Build(s,stage.Item);return;
                case GuidedPlayGoal.PaidTrain084:Train(s,stage.Item,(int)stage.Required);return;
                case GuidedPlayGoal.PaidAge084:
                    Save(s,w.Rules.Age((int)stage.Required).cost);
                    string choice=w.Rules.ageChoices.First(a=>a.civilization==s.Definition.PlayerCivilization&&a.age==(int)stage.Required&&a.count==0).id;
                    Accepted(s,new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{s.CaptureState().town},choice=choice});return;
                case GuidedPlayGoal.PaidResearch084:
                    var tech=w.Rules.Tech(stage.Item);Save(s,tech.cost);var producer=w.Entities.First(e=>e.Alive&&e.owner==0&&e.Complete&&e.def.id==tech.building);
                    Accepted(s,new GameCommand{player=0,kind=CommandKind.Research,actors=new[]{producer.id},item=tech.id});return;
                case GuidedPlayGoal.CivilizationEconomy084:
                    if(stage.Item=="coureur_income")
                    {
                        int worker=w.Events.Last(e=>e.kind=="UnitTrained"&&e.player==0&&e.message=="coureur").target;
                        var resource=Resource(s,ResourceKind.Food,new[]{worker});Assert.IsNotNull(resource);
                        Order(s,new[]{worker},OrderKind.Gather,resource.position,resource.id);
                    }
                    return;
                case GuidedPlayGoal.PaidDamage084:
                    var state=s.CaptureState();var target=w.Get(state.firstTarget);
                    Assert.IsFalse(w.IsVisible(0,target),"Start with an actually hidden original opponent");
                    Assert.AreEqual(0,s.SuggestedTarget,"Hidden target ID is not exposed by the objective hint");
                    Assert.AreEqual(w.OpeningTownPosition(1),s.ObjectivePoint,"Hidden current position is not exposed");
                    Assert.IsFalse(s.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=s.SuggestedActors,order=OrderKind.Attack,target=target.id,point=target.position}).accepted,"A hidden-ID attack must be rejected by ordinary visibility");
                    Order(s,new[]{state.scout},OrderKind.Move,w.OpeningTownPosition(1));
                    Until(s,()=>w.IsVisible(0,target),180,"Reveal the existing opposing explorer with real movement and fog");
                    Assert.AreEqual(target.id,s.SuggestedTarget);
                    Order(s,new[]{state.scout},OrderKind.Hold,w.Get(state.scout).position);
                    Order(s,s.SuggestedActors,OrderKind.Attack,target.position,target.id);return;
                default:Assert.Fail("Unexpected route goal "+stage.Goal);return;
            }
        }
        internal static void StepStage(GuidedPlaySession s)
        {
            Observe(s);var stage=s.Stage;QueueCurrent(s);
            Until(s,()=>s.Stage!=stage||s.Status==GuidedPlayStatus.Completed,240,"Genuine paid/economy/combat result must complete "+stage.Id);
            TestContext.WriteLine(s.Definition.Id+" completed "+stage.Id+" @ "+s.World.Time.ToString("0.0")+"s; spent="+s.World.Players[0].spent);
        }
        internal static void Solve(GuidedPlaySession s)
        {int guard=0;while(s.Status==GuidedPlayStatus.Running&&guard++<16)StepStage(s);Assert.AreEqual(GuidedPlayStatus.Completed,s.Status,s.Reason);}
        internal static GuidedPlaySession Cold(GuidedPlaySession original,bool slot=false)
        {
            Observe(original);original.World.FlushEconomyLog();var saved=new SavedMatch{matchId=original.AttemptId,appVersion="civilization084-test",world=original.World.CaptureState(),simulation=original.Simulation.CaptureState(),guided=original.CaptureState(),cameraPosition=Vector3.zero,cameraFocus=Vector2.zero,cameraSize=26,simulationSpeed=1};
            string directory=Path.Combine(Path.GetTempPath(),"frontier-civ084-"+Guid.NewGuid().ToString("N"));
            try
            {
                if(slot){Directory.CreateDirectory(directory);var path=MatchSaveStore.SlotPath(directory,1);MatchSaveStore.Write(path,saved);saved=MatchSaveStore.Read(path,"civilization084-test");}
                var world=World.Restore(original.World.Rules,saved.world);var sim=MatchSimulation.Restore(world,saved.simulation);string before=JsonUtility.ToJson(world.CaptureState());
                var restored=Observe(GuidedPlaySession.Restore(world,sim,saved.guided));Assert.AreEqual(before,JsonUtility.ToJson(world.CaptureState()),"Cold adoption cannot mutate the world");
                Assert.AreEqual(JsonUtility.ToJson(saved.guided),JsonUtility.ToJson(restored.CaptureState()));return restored;
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
    public sealed class CivilizationRoutes084Tests
    {
        [SetUp] public void ResetFailureEvidence()=>NormalPaidCivilization084Driver.ResetEvidence();
        [TearDown] public void PreserveActualFailureScene()
        {
            if(TestContext.CurrentContext.Result.Outcome.Status==NUnit.Framework.Interfaces.TestStatus.Failed)NormalPaidCivilization084Driver.ArchiveFailure();
        }
        static readonly string[] Routes={ClassicCivilizationRoutes084.British,ClassicCivilizationRoutes084.Dutch,ClassicCivilizationRoutes084.French,ClassicCivilizationRoutes084.Ottoman};
        [Test] public void AdditionsKeepAllThirteenLegacyIdsDefaultCivilizationAndCatalogRevision()
        {
            Assert.AreEqual(1,GuidedPlayCatalog.Revision);Assert.AreEqual(17,GuidedPlayCatalog.Definitions.Count);
            Assert.AreEqual(7,GuidedPlayCatalog.Definitions.Count(d=>d.Kind==GuidedPlayKind.Lesson&&!ClassicCivilizationRoutes084.Contains(d.Id)));
            Assert.AreEqual(6,GuidedPlayCatalog.Definitions.Count(d=>d.Kind==GuidedPlayKind.Campaign));
            foreach(var d in GuidedPlayCatalog.Definitions.Where(d=>!ClassicCivilizationRoutes084.Contains(d.Id)))Assert.AreEqual("coast",d.PlayerCivilization);
            Assert.AreEqual("campaign_bridge",GuidedPlayCatalog.Find("campaign_supply").NextId);
            Assert.AreEqual("campaign_estuary",GuidedPlayCatalog.Find("campaign_bridge").NextId);
            foreach(string id in Routes)Assert.IsTrue(AccountProgressStore.Load(memoryOnly:true).IsUnlocked(id));
        }
        [TestCase(ClassicCivilizationRoutes084.British)][TestCase(ClassicCivilizationRoutes084.Dutch)][TestCase(ClassicCivilizationRoutes084.French)][TestCase(ClassicCivilizationRoutes084.Ottoman)]
        public void NewRoutesStartAtExactNormalAgeIWithNoEntityResourceTechnologyOrAiGift(string id)
        {
            var rules=Catalog.Load();using(var s=NormalPaidCivilization084Driver.Start(rules,id))
            {
                var normal=new World(rules,s.Definition.Seed,s.Definition.PlayerCivilization,"france",5,s.Definition.MapId);
                Assert.AreEqual(1,s.World.Players[0].age);Assert.AreEqual(3600,s.Definition.TimeLimit);Assert.AreEqual(MapTopology.Winding,s.World.MapId);
                Assert.AreEqual(JsonUtility.ToJson(normal.Players[0]),JsonUtility.ToJson(s.World.Players[0]));
                Assert.AreEqual(normal.Entities.Count,s.World.Entities.Count);
                for(int i=0;i<normal.Entities.Count;i++)Assert.AreEqual(JsonUtility.ToJson(normal.Entities[i]),JsonUtility.ToJson(s.World.Entities[i]),"Opening entity "+i);
                Assert.AreEqual(0,s.Simulation.CaptureState().controllers.Length);Assert.IsFalse(s.World.HasScenario);
                Assert.IsFalse(s.World.Events.Any(e=>e.kind=="GuidedScenarioGrant"||e.kind=="GuidedScenarioEra"||e.kind=="Payment"));Assert.IsEmpty(s.World.Players[0].technologies);
            }
        }
        [TestCase(ClassicCivilizationRoutes084.British)][TestCase(ClassicCivilizationRoutes084.Dutch)][TestCase(ClassicCivilizationRoutes084.French)][TestCase(ClassicCivilizationRoutes084.Ottoman)]
        public void FourCountryRoutesActuallyProgressFromNormalIncomeToPaidArmyResearchAndRealCombat(string id)
        {
            using(var s=NormalPaidCivilization084Driver.Start(Catalog.Load(),id))
            {
                NormalPaidCivilization084Driver.Solve(s);Assert.IsTrue(s.Checklist.All(c=>c.Complete));Assert.Less(s.World.Time,3600);
                Assert.Greater(s.World.Players[0].spent.food+s.World.Players[0].spent.wood+s.World.Players[0].spent.coin,500);
                Assert.IsTrue(s.World.Events.Any(e=>e.kind=="Gather"&&e.player==0));Assert.IsTrue(s.World.Events.Any(e=>e.kind=="ResearchComplete"&&e.player==0));
                Assert.IsTrue(s.World.Events.Any(e=>e.kind=="Damage"&&e.player==0&&e.target==s.CaptureState().firstTarget));Assert.AreEqual(-1,s.World.Winner,"The exercise does not claim an entire skirmish victory");
                using(var completed=NormalPaidCivilization084Driver.Cold(s,true))
                {
                    Assert.AreEqual(GuidedPlayStatus.Completed,completed.Status);var account=AccountProgressStore.Load(memoryOnly:true);Assert.IsTrue(account.RecordCompletion(completed));Assert.IsTrue(account.RecordCompletion(completed));Assert.AreEqual(1,account.CompletionCount(id));
                }
            }
        }
        [TestCase(ClassicCivilizationRoutes084.British)][TestCase(ClassicCivilizationRoutes084.Dutch)][TestCase(ClassicCivilizationRoutes084.French)][TestCase(ClassicCivilizationRoutes084.Ottoman)]
        public void AffordabilityAndActualPaidQueueColdRestoreDoNotSpendAgainOrDuplicateBirths(string id)
        {
            using(var original=NormalPaidCivilization084Driver.Start(Catalog.Load(),id))
            {
                var choice=original.World.Rules.ageChoices.First(a=>a.civilization==original.Definition.PlayerCivilization&&a.age==2).id;
                Assert.IsFalse(original.Issue(new GameCommand{player=0,kind=CommandKind.AdvanceAge,actors=new[]{original.CaptureState().town},choice=choice}).accepted,"Normal opening cannot afford the age upgrade");
                Assert.IsFalse(original.World.Events.Any(e=>e.kind=="Payment"));
                // Save after a real affordability rejection, then continue the finite route
                // through a genuine paid in-flight training or research job.
                using(var opened=NormalPaidCivilization084Driver.Cold(original,true))
                {
                    while(opened.Stage.Goal!=GuidedPlayGoal.PaidTrain084&&opened.Stage.Goal!=GuidedPlayGoal.PaidResearch084)NormalPaidCivilization084Driver.StepStage(opened);
                    var stage=opened.Stage;NormalPaidCivilization084Driver.QueueCurrent(opened);opened.Simulation.Step();
                    Assert.AreSame(stage,opened.Stage,"A queued job is not a completed objective");var spent=opened.World.Players[0].spent;int payments=opened.World.Events.Count(e=>e.kind=="Payment");
                    using(var resumed=NormalPaidCivilization084Driver.Cold(opened,true))
                    {
                        NormalPaidCivilization084Driver.Until(resumed,()=>resumed.Stage!=stage,150,"Adopt and finish paid work after a cold slot restore");
                        Assert.AreEqual(JsonUtility.ToJson(spent),JsonUtility.ToJson(resumed.World.Players[0].spent));Assert.AreEqual(payments,resumed.World.Events.Count(e=>e.kind=="Payment"));
                        NormalPaidCivilization084Driver.Solve(resumed);
                    }
                }
            }
        }
        [Test] public void ActualManorBirthHasOwnWitnessAndCannotBeReplacedByOpeningOrPaidTownWorker()
        {
            using(var s=NormalPaidCivilization084Driver.Start(Catalog.Load(),ClassicCivilizationRoutes084.British))
            {
                Assert.AreEqual(0,s.Checklist[1].Progress);NormalPaidCivilization084Driver.StepStage(s);
                NormalPaidCivilization084Driver.Until(s,()=>s.Stage.Id=="age2",3,"The actual Manor gift must finish");
                var birth=s.World.Events.Single(e=>e.kind=="ManorWorkerProduced"&&e.player==0);Assert.AreEqual("house",s.World.Get(birth.entity).def.id);Assert.AreEqual("villager",s.World.Get(birth.target).def.id);
                Assert.IsFalse(s.World.Events.Any(e=>e.kind=="UnitTrained"&&e.target==birth.target),"Manor birth is not paid TC production");
                var saved=s.CaptureState();birth.kind="Spawn";Assert.Throws<ArgumentException>(()=>GuidedPlaySession.Restore(s.World,s.Simulation,saved),"Cold progress requires the original actual Manor birth witness");
            }
        }
        [TestCase(ClassicCivilizationRoutes084.British)][TestCase(ClassicCivilizationRoutes084.Dutch)][TestCase(ClassicCivilizationRoutes084.French)][TestCase(ClassicCivilizationRoutes084.Ottoman)]
        public void BaseLossFailsAndRetryCreatesFreshNormalAttempt(string id)
        {
            using(var s=NormalPaidCivilization084Driver.Start(Catalog.Load(),id))
            {
                NormalPaidCivilization084Driver.Accepted(s,new GameCommand{player=0,kind=CommandKind.Demolish,actors=new[]{s.CaptureState().town}});Assert.AreEqual(GuidedPlayStatus.Failed,s.Status);
                using(var failed=NormalPaidCivilization084Driver.Cold(s))Assert.AreEqual(GuidedPlayStatus.Failed,failed.Status);
                using(var retry=NormalPaidCivilization084Driver.Observe(s.Retry())){Assert.AreNotEqual(s.AttemptId,retry.AttemptId);Assert.AreEqual(1,retry.World.Players[0].age);Assert.AreEqual(s.World.Seed,retry.World.Seed);}
            }
        }
    }
}
