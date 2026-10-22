using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Frontier
{
    [Serializable] public sealed class GuidedPlayCommandProof
    {
        public GameCommand command;
        public int eventIndex;
        public int stage;
    }
    [Serializable] public sealed class GuidedPlaySnapshot
    {
        public int schema=1,catalogRevision=GuidedPlayCatalog.Revision,stageIndex,revision,startEventIndex,resultEventIndex=-1;
        public string definitionId,attemptId,rulesHash,reason="",scenarioHash="";
        public GuidedPlayStatus status;
        public float completedSeconds,startingWood;
        public int town,scout,firstTarget,secondTarget,transport,neutralSite;
        public int[] workers=Array.Empty<int>(),soldiers=Array.Empty<int>(),selection=Array.Empty<int>(),stageEvents=Array.Empty<int>();
        public Vector2 movePoint,contactPoint,boardingPoint,landingPoint,buildPoint,barracksPoint;
        public bool raidIssued;
        public GuidedPlayCommandProof[] commands=Array.Empty<GuidedPlayCommandProof>();
        public string shipmentCard="";
    }
    // This session observes the same commands, entities, payments and fixed
    // scheduler as skirmish. It never completes a lesson from a HUD click alone.
    public sealed partial class GuidedPlaySession:IDisposable
    {
        public World World {get;}
        public MatchSimulation Simulation {get;}
        public GuidedPlayDefinition Definition {get;}
        public GuidedPlayStatus Status=>state.status;
        public string Reason=>state.reason;
        public string AttemptId=>state.attemptId;
        public string NextId=>Status==GuidedPlayStatus.Completed?Definition.NextId:"";
        public string StartingSetupDescription=>Definition.StartingSetupDescription;
        public int Revision=>state.revision;
        public GuidedPlayStage Stage=>state.stageIndex<Definition.Stages.Count?Definition.Stages[state.stageIndex]:null;
        public float CompletedSeconds=>state.completedSeconds;
        public IReadOnlyList<GuidedPlayChecklist> Checklist=>checklist;
        public Vector2 ObjectivePoint
        {
            get
            {
                if(IsAuthored)return AuthoredObjectivePoint;
                if(IsCivilizationRoute084)return CivilizationObjectivePoint084;
                switch(Stage?.Goal)
                {
                    case GuidedPlayGoal.Contact:case GuidedPlayGoal.TradingPost:case GuidedPlayGoal.Alliance:case GuidedPlayGoal.Recruit:return state.contactPoint;
                    case GuidedPlayGoal.Embark:return state.boardingPoint;
                    case GuidedPlayGoal.Land:case GuidedPlayGoal.HoldLanding:return state.landingPoint;
                    case GuidedPlayGoal.DestroyFirst:return World.Get(state.firstTarget)?.position??state.movePoint;
                    case GuidedPlayGoal.DestroySecond:return World.Get(state.secondTarget)?.position??state.movePoint;
                    case GuidedPlayGoal.House:return state.buildPoint;
                    case GuidedPlayGoal.Barracks:return state.barracksPoint;
                    default:return state.movePoint;
                }
            }
        }
        public int SuggestedTarget
        {
            get
            {
                if(IsAuthored)return AuthoredSuggestedTarget;
                if(IsCivilizationRoute084)return Stage?.Goal==GuidedPlayGoal.PaidDamage084&&World.IsVisible(0,World.Get(state.firstTarget))?state.firstTarget:0;
                switch(Stage?.Goal)
                {
                    case GuidedPlayGoal.DestroyFirst:case GuidedPlayGoal.Defend:return state.firstTarget;
                    case GuidedPlayGoal.DestroySecond:return state.secondTarget;
                    case GuidedPlayGoal.Embark:return state.transport;
                    case GuidedPlayGoal.Contact:case GuidedPlayGoal.TradingPost:case GuidedPlayGoal.Alliance:case GuidedPlayGoal.Recruit:return state.neutralSite;
                    default:return 0;
                }
            }
        }
        public int[] SuggestedActors
        {
            get
            {
                if(IsAuthored)return AuthoredSuggestedActors;
                if(IsCivilizationRoute084)return CivilizationSuggestedActors084;
                switch(Stage?.Goal)
                {
                    case GuidedPlayGoal.Selection:case GuidedPlayGoal.Contact:return new[]{state.scout};
                    case GuidedPlayGoal.Move:return state.soldiers.Length>0?(int[])state.soldiers.Clone():new[]{state.scout};
                    case GuidedPlayGoal.GatherWood:case GuidedPlayGoal.House:case GuidedPlayGoal.Barracks:case GuidedPlayGoal.TradingPost:return (int[])state.workers.Clone();
                    case GuidedPlayGoal.Age:case GuidedPlayGoal.SendShipment:case GuidedPlayGoal.ShipmentArrival:return new[]{state.town};
                    case GuidedPlayGoal.Train:return World.Entities.Where(e=>e.Alive&&e.owner==0&&e.Complete&&e.def.id=="barracks").Select(e=>e.id).ToArray();
                    case GuidedPlayGoal.Alliance:case GuidedPlayGoal.Recruit:var post=World.TradingPostAt(state.neutralSite);return post!=null?new[]{post.id}:Array.Empty<int>();
                    case GuidedPlayGoal.Defend:return World.Entities.Where(e=>e.Alive&&e.owner==0&&e.Military&&!e.Worker&&!e.def.waterborne&&e.def.id!="explorer").Select(e=>e.id).ToArray();
                    case GuidedPlayGoal.Land:return new[]{state.transport};
                    default:return (int[])state.soldiers.Clone();
                }
            }
        }
        readonly GuidedPlaySnapshot state;
        readonly List<GuidedPlayCommandProof> commands;
        readonly List<int> stageEvents;
        readonly GuidedPlayChecklist[] checklist;
        bool disposed;
        GuidedPlaySession(World world,MatchSimulation simulation,GuidedPlayDefinition definition,GuidedPlaySnapshot saved)
        {
            World=world;Simulation=simulation;Definition=definition;state=saved;
            authoredDocument=definition.AuthoredScenarioId==""?null:world.ScenarioDocument;
            commands=new List<GuidedPlayCommandProof>(saved.commands);stageEvents=new List<int>(saved.stageEvents);
            checklist=new GuidedPlayChecklist[definition.Stages.Count];
            for(int i=0;i<checklist.Length;i++)checklist[i]=new GuidedPlayChecklist{Id=definition.Stages[i].Id,Text=definition.Stages[i].Title,Required=definition.Stages[i].Required};
            RefreshChecklist();Simulation.StepCompleted+=Tick;
        }
        public static GuidedPlaySession Start(RuleCatalog rules,string id,string attemptId=null)
        {
            var definition=GuidedPlayCatalog.Find(id)??throw new ArgumentException("Unknown guided scenario",nameof(id));
            attemptId??=Guid.NewGuid().ToString("N");Require(ValidAttempt(attemptId),"attempt ID");
            var world=definition.AuthoredScenarioId==""?new World(rules,definition.Seed,definition.PlayerCivilization,"france",5,definition.MapId):
                new World(rules,RiverContent070.Build(rules,definition.AuthoredScenarioId),new[]{"coast","france"},new[]{0,1},5);
            var saved=new GuidedPlaySnapshot{definitionId=id,attemptId=attemptId,rulesHash=SnapshotCodec.RulesHash(rules),startingWood=world.Players[0].gathered.wood,scenarioHash=world.HasScenario?world.ScenarioHash:""};
            Configure(world,definition,saved);var simulation=new MatchSimulation(world,Array.Empty<int>(),"standard");
            saved.startEventIndex=world.Events.Count;
            world.Emit("GuidedStarted",0,saved.town,StartMarker(saved));
            return new GuidedPlaySession(world,simulation,definition,saved);
        }
        public static GuidedPlaySession Restore(World world,MatchSimulation simulation,GuidedPlaySnapshot saved)
        {
            Require(world!=null&&simulation!=null&&ReferenceEquals(world,simulation.World)&&saved!=null,"missing world/session");
            var definition=GuidedPlayCatalog.Find(saved.definitionId);Require(definition!=null,"unknown scenario");
            Require(saved.schema==1&&saved.catalogRevision==GuidedPlayCatalog.Revision,"schema or catalog revision");
            Require(ValidAttempt(saved.attemptId)&&saved.rulesHash==SnapshotCodec.RulesHash(world.Rules),"attempt or rules");
            Require(world.MapId==definition.MapId&&world.Seed==definition.Seed&&world.MaxAge==5&&world.Players.Length==2&&world.Players[0].civilization==definition.PlayerCivilization&&world.Players[1].civilization=="france","scenario/world identity");
            if(definition.AuthoredScenarioId!="")
                Require(world.HasScenario&&saved.scenarioHash==world.ScenarioHash&&world.ScenarioHash==ScenarioCodec.ContentHash(RiverContent070.Build(world.Rules,definition.AuthoredScenarioId)),"authored campaign content identity");
            else Require(!world.HasScenario&&saved.scenarioHash=="","unexpected authored campaign identity");
            Require(Enum.IsDefined(typeof(GuidedPlayStatus),saved.status)&&saved.stageIndex>=0&&saved.stageIndex<=definition.Stages.Count&&saved.revision>=0,"status or stage");
            Require(saved.reason!=null&&saved.reason.Length<=512&&SnapshotCodec.Finite(saved.completedSeconds)&&saved.completedSeconds>=0&&saved.completedSeconds<=world.Time+.001f&&SnapshotCodec.Finite(saved.startingWood)&&saved.startingWood>=0,"result clock");
            Require(saved.workers!=null&&saved.soldiers!=null&&saved.selection!=null&&saved.stageEvents!=null&&saved.commands!=null&&saved.commands.Length<=4096&&saved.stageEvents.Length==saved.stageIndex,"collections");
            foreach(var point in new[]{saved.movePoint,saved.contactPoint,saved.boardingPoint,saved.landingPoint,saved.buildPoint,saved.barracksPoint})Require(SnapshotCodec.Finite(point)&&point.x>=0&&point.x<=128&&point.y>=0&&point.y<=128,"goal point");
            ValidateRole(world,saved.town,"town",0);ValidateRole(world,saved.scout,"explorer",0);
            var ids=new HashSet<int>();foreach(int worker in saved.workers){Require(ids.Add(worker)&&world.Get(worker)?.owner==0&&world.Get(worker).Worker,"worker role");}
            ids.Clear();foreach(int soldier in saved.soldiers){var e=world.Get(soldier);Require(ids.Add(soldier)&&e!=null&&e.owner==0&&e.def.kind==EntityKind.Unit&&!e.Worker&&!e.def.waterborne,"soldier role");}
            if(saved.transport!=0){var e=world.Get(saved.transport);Require(e!=null&&e.owner==0&&e.def.transportShip,"transport role");}
            foreach(int target in new[]{saved.firstTarget,saved.secondTarget})if(target!=0)Require(world.Get(target)?.owner==1,"enemy target role");
            if(saved.neutralSite!=0)Require(world.NeutralAt(saved.neutralSite)!=null,"neutral role");
            Require(EventIs(world,saved.startEventIndex,"GuidedStarted",StartMarker(saved))&&world.Events[saved.startEventIndex].time==0,"opening witness");
            int previous=saved.startEventIndex;
            for(int i=0;i<saved.stageEvents.Length;i++){int index=saved.stageEvents[i];Require(index>previous&&EventIs(world,index,"GuidedStageCompleted",StageMarker(saved,definition.Stages[i].Id)),"stage witness");previous=index;}
            ids.Clear();foreach(int selected in saved.selection){var e=world.Get(selected);Require(ids.Add(selected)&&e!=null&&e.owner==0,"selection");}
            int commandIndex=saved.startEventIndex;
            foreach(var proof in saved.commands)
            {
                Require(proof?.command!=null&&proof.stage>=0&&proof.stage<definition.Stages.Count&&proof.eventIndex>commandIndex&&proof.eventIndex<world.Events.Count,"command witness range");
                var c=proof.command;Require(c.player==0&&Enum.IsDefined(typeof(CommandKind),c.kind)&&Enum.IsDefined(typeof(OrderKind),c.order)&&SnapshotCodec.Finite(c.point)&&c.actors!=null&&c.actors.Length<=1000&&c.item!=null&&c.choice!=null,"accepted command shape");
                foreach(int actor in c.actors)Require(world.Get(actor)?.owner==0,"accepted actor owner");
                Require(EventIs(world,proof.eventIndex,"GuidedAcceptedCommand",CommandMarker(saved,c)),"accepted command witness");commandIndex=proof.eventIndex;
            }
            Require(saved.shipmentCard!=null&&(saved.shipmentCard==""||world.Rules.Card(saved.shipmentCard)!=null&&world.Players[0].sentCards.Contains(saved.shipmentCard)),"shipment witness");
            Require(saved.status==GuidedPlayStatus.Completed?saved.stageIndex==definition.Stages.Count:saved.stageIndex<definition.Stages.Count,"terminal stage");
            if(saved.status!=GuidedPlayStatus.Running)Require(world.Paused&&saved.resultEventIndex>previous&&EventIs(world,saved.resultEventIndex,saved.status==GuidedPlayStatus.Completed?"GuidedCompleted":"GuidedFailed",ResultMarker(saved)),"result witness");
            else Require(saved.resultEventIndex==-1&&saved.completedSeconds==0&&saved.reason=="","running result");
            Require(!saved.raidIssued||world.Events.Any(e=>e.kind=="GuidedRaid"&&e.player==1&&e.message==saved.attemptId),"raid witness");
            if(definition.AuthoredScenarioId!="")ValidateAuthoredProgress(world,definition,saved);
            if(ClassicCivilizationRoutes084.Contains(definition.Id))ValidateCivilizationProgress084(world,simulation,definition,saved);
            // Restoring only adopts validated existing state. No Start, Spawn,
            // grant, command, stage evaluation or event is run here.
            return new GuidedPlaySession(world,simulation,definition,SnapshotCodec.Clone(saved));
        }
        public GuidedPlaySession Retry()=>Start(World.Rules,Definition.Id);
        public GuidedPlaySnapshot CaptureState()
        {
            state.commands=commands.ToArray();state.stageEvents=stageEvents.ToArray();return SnapshotCodec.Clone(state);
        }
        public CommandResult Issue(GameCommand command)
        {
            if(disposed||Status!=GuidedPlayStatus.Running)return CommandResult.Fail("Guided scenario has ended");
            if(command==null||command.player!=0)return CommandResult.Fail("Guided scenario controls player 0");
            if(commands.Count>=4096)return CommandResult.Fail("Guided command history is full; retry the scenario");
            var result=World.Issue(command);if(!result.accepted)return result;
            var accepted=SnapshotCodec.Clone(command);var proof=new GuidedPlayCommandProof{command=accepted,eventIndex=World.Events.Count,stage=state.stageIndex};
            World.Emit("GuidedAcceptedCommand",0,accepted.actors.Length>0?accepted.actors[0]:0,CommandMarker(state,accepted),0,accepted.target,accepted.point);commands.Add(proof);
            if(accepted.kind==CommandKind.Ship&&state.shipmentCard=="")state.shipmentCard=accepted.item;
            state.revision++;Tick();return result;
        }
        public void ObserveSelection(int[] selected)
        {
            if(disposed||Status!=GuidedPlayStatus.Running)return;
            var valid=new List<int>();foreach(int id in selected??Array.Empty<int>()){var e=World.Get(id);if(e!=null&&e.Alive&&e.owner==0&&e.garrisonedIn==0&&!valid.Contains(id))valid.Add(id);}
            if(state.selection.SequenceEqual(valid))return;
            state.selection=valid.ToArray();state.revision++;
            World.Emit("GuidedSelection",0,valid.Count>0?valid[0]:0,state.attemptId,valid.Count);Tick();
        }
        public void Tick()
        {
            if(disposed||Status!=GuidedPlayStatus.Running)return;
            if(IsAuthored){TickAuthored();return;}
            if(IsCivilizationRoute084&&CivilizationFailure084())return;
            if(World.Get(state.town)?.Alive!=true||World.Players[0].resigned||World.Players[0].eliminated){Finish(GuidedPlayStatus.Failed,"城镇中心已失守或己方退出；同种子重试可重新开始。");return;}
            if(state.transport!=0&&(World.Get(state.transport)?.Alive!=true||state.soldiers.All(id=>World.Get(id)?.Alive!=true))){Finish(GuidedPlayStatus.Failed,"运输舰或登陆队全部损失；请重新安排登陆行动。");return;}
            if(World.Time>=Definition.TimeLimit){Finish(GuidedPlayStatus.Failed,"场景时限已到；可按同一地图种子重试。");return;}
            if((Definition.Id=="lesson_selection"||Stage.Goal==GuidedPlayGoal.Contact)&&World.Get(state.scout)?.Alive!=true){Finish(GuidedPlayStatus.Failed,"探路者已损失；请重试选取与移动课程。");return;}
            if((Definition.Id=="lesson_targeting"||Definition.Id=="campaign_bridge")&&state.soldiers.All(id=>World.Get(id)?.Alive!=true)){Finish(GuidedPlayStatus.Failed,"前线部队全部损失；请重试并调整集火目标。");return;}
            if((Stage.Goal==GuidedPlayGoal.DestroyFirst||Stage.Goal==GuidedPlayGoal.DestroySecond)&&World.Get(Stage.Goal==GuidedPlayGoal.DestroyFirst?state.firstTarget:state.secondTarget)?.Alive==false&&!DirectedAt(Stage.Goal==GuidedPlayGoal.DestroyFirst?state.firstTarget:state.secondTarget)){Finish(GuidedPlayStatus.Failed,"指定目标在集火命令前被摧毁；请重试并使用指定攻击。");return;}
            if((Stage.Goal==GuidedPlayGoal.Embark||Stage.Goal==GuidedPlayGoal.Land||Stage.Goal==GuidedPlayGoal.HoldLanding||Stage.Goal==GuidedPlayGoal.Move&&state.soldiers.Length>0)&&state.soldiers.Count(id=>World.Get(id)?.Alive==true)<Stage.Required){Finish(GuidedPlayStatus.Failed,"可用部队人数已不足以完成当前目标；请重试并保护部队。");return;}
            float progress=Progress(Stage);
            if(Mathf.Abs(checklist[state.stageIndex].Progress-progress)>.0001f)state.revision++;
            checklist[state.stageIndex].Progress=progress;
            if(progress+.0001f>=Stage.Required)
            {
                int index=World.Events.Count;World.Emit("GuidedStageCompleted",0,0,StageMarker(state,Stage.Id));stageEvents.Add(index);state.stageIndex++;state.revision++;
                if(state.stageIndex==Definition.Stages.Count){Finish(GuidedPlayStatus.Completed,"目标全部完成；已暂停场景，可继续下一关或重玩。");return;}
                if(Definition.Id=="campaign_supply"&&Stage.Goal==GuidedPlayGoal.Defend&&!state.raidIssued)StartRaid();
                RefreshChecklist();
            }
            if(World.IsMatchEnded&&Status==GuidedPlayStatus.Running)Finish(GuidedPlayStatus.Failed,"战局在场景目标完成前结束；请按目标顺序重试。");
        }
        void StartRaid()
        {
            foreach(int target in new[]{state.firstTarget,state.secondTarget})
            {
                var result=World.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{target},order=OrderKind.AttackMove,point=World.Get(state.town).position});
                if(!result.accepted){Finish(GuidedPlayStatus.Failed,"突袭单位无法接到命令；请重试场景。");return;}
            }
            state.raidIssued=true;World.Emit("GuidedRaid",1,state.firstTarget,state.attemptId);state.revision++;
        }
        void Finish(GuidedPlayStatus status,string reason)
        {
            state.status=status;state.reason=reason;state.completedSeconds=World.Time;state.resultEventIndex=World.Events.Count;
            World.Emit(status==GuidedPlayStatus.Completed?"GuidedCompleted":"GuidedFailed",0,0,ResultMarker(state));World.Paused=true;state.revision++;RefreshChecklist();
        }
        void RefreshChecklist()
        {
            for(int i=0;i<checklist.Length;i++){checklist[i].Complete=i<state.stageIndex;checklist[i].Current=i==state.stageIndex&&Status==GuidedPlayStatus.Running;if(checklist[i].Complete)checklist[i].Progress=checklist[i].Required;else if(checklist[i].Current)checklist[i].Progress=Progress(Definition.Stages[i]);}
        }
        bool Command(Func<GameCommand,bool> condition)=>commands.Any(p=>condition(p.command));
        bool Order(OrderKind order,int target=0)=>Command(c=>c.kind==CommandKind.Order&&c.order==order&&(target==0||c.target==target));
        bool Built(string item)
        {
            foreach(var proof in commands)
            {
                var c=proof.command;if(c.kind!=CommandKind.Build||c.item!=item)continue;
                // The foundation must be spawned before this accepted witness;
                // existing initial scenario buildings never satisfy this goal.
                for(int i=Math.Max(state.startEventIndex,proof.eventIndex-4-c.actors.Length);i<proof.eventIndex;i++)
                {
                    var ev=World.Events[i];var building=World.Get(ev.entity);
                    if(ev.kind=="Spawn"&&ev.player==0&&building!=null&&building.def.id==item&&(building.position-c.point).sqrMagnitude<.01f&&building.Alive&&building.Complete)return true;
                }
            }
            return false;
        }
        bool DirectedAt(int target)
        {
            if(World.Get(target)?.Alive!=false)return false;
            int death=World.Events.FindIndex(state.startEventIndex,e=>e.kind=="Death"&&e.entity==target&&e.target==0);
            return death>=0&&commands.Any(p=>p.eventIndex<death&&p.command.kind==CommandKind.Order&&p.command.order==OrderKind.Attack&&p.command.target==target);
        }
        float Progress(GuidedPlayStage stage)
        {
            if(IsAuthored)return AuthoredProgress(stage);
            if(IsCivilizationRoute084)return CivilizationProgress084(stage);
            switch(stage.Goal)
            {
                case GuidedPlayGoal.Selection:return state.selection.Contains(state.scout)?1:0;
                case GuidedPlayGoal.Move:
                    var movers=state.soldiers.Length>0?state.soldiers:new[]{state.scout};
                    return movers.Count(id=>World.Get(id)?.Alive==true&&World.Get(id).garrisonedIn==0&&(World.Get(id).position-state.movePoint).sqrMagnitude<=36&&Command(c=>c.kind==CommandKind.Order&&(c.order==OrderKind.Move||c.order==OrderKind.AttackMove)&&c.actors.Contains(id)&&(c.point-state.movePoint).sqrMagnitude<=36));
                case GuidedPlayGoal.GatherWood:return Order(OrderKind.Gather)?Mathf.Max(0,World.Players[0].gathered.wood-state.startingWood):0;
                case GuidedPlayGoal.House:return Built("house")?1:0;
                case GuidedPlayGoal.Barracks:return Built("barracks")?1:0;
                case GuidedPlayGoal.Age:return Command(c=>c.kind==CommandKind.AdvanceAge)&&World.Players[0].age>=stage.Required?stage.Required:0;
                case GuidedPlayGoal.Train:
                    if(!Command(c=>c.kind==CommandKind.Train&&c.item=="musketeer"))return 0;
                    return World.Events.Skip(state.startEventIndex).Where(e=>e.player==0&&e.kind=="UnitTrained"&&World.Get(e.target)?.def.id=="musketeer").Sum(e=>e.amount);
                case GuidedPlayGoal.SendShipment:return state.shipmentCard!=""?1:0;
                case GuidedPlayGoal.ShipmentArrival:return state.shipmentCard!=""&&World.Events.Skip(state.startEventIndex).Any(e=>e.player==0&&e.kind=="ShipmentArrived"&&e.message==World.Rules.Card(state.shipmentCard).name+" arrived")?1:0;
                case GuidedPlayGoal.DestroyFirst:return DirectedAt(state.firstTarget)?1:0;
                case GuidedPlayGoal.DestroySecond:return DirectedAt(state.secondTarget)?1:0;
                case GuidedPlayGoal.Contact:return World.Get(state.scout)?.Alive==true&&(World.Get(state.scout).position-state.contactPoint).sqrMagnitude<=9&&Command(c=>c.kind==CommandKind.Order&&c.order==OrderKind.Move&&c.actors.Contains(state.scout)&&(c.point-state.contactPoint).sqrMagnitude<=9)?1:0;
                case GuidedPlayGoal.TradingPost:return Built("trade_post")&&World.TradingPostAt(state.neutralSite)?.owner==0?1:0;
                case GuidedPlayGoal.Alliance:return Command(c=>c.kind==CommandKind.NeutralAlliance&&c.target==state.neutralSite)&&World.NeutralAt(state.neutralSite)?.owner==0?1:0;
                case GuidedPlayGoal.Recruit:return Command(c=>c.kind==CommandKind.NeutralRecruit&&c.target==state.neutralSite)&&World.Events.Skip(state.startEventIndex).Any(e=>e.kind=="NeutralRecruitComplete"&&e.player==0&&e.entity==state.neutralSite&&World.Get(e.target)?.Alive==true)?1:0;
                case GuidedPlayGoal.Embark:return Command(c=>c.kind==CommandKind.Embark&&c.target==state.transport||c.kind==CommandKind.Order&&c.order==OrderKind.Embark&&c.target==state.transport)?state.soldiers.Count(id=>World.Get(id)?.Alive==true&&World.Get(id).garrisonedIn==state.transport):0;
                case GuidedPlayGoal.Land:
                    if(!Command(c=>(c.kind==CommandKind.Disembark||c.kind==CommandKind.Order&&c.order==OrderKind.Disembark)&&c.actors.Contains(state.transport)&&(c.point-state.landingPoint).sqrMagnitude<=144))return 0;
                    return state.soldiers.Count(id=>World.Get(id)?.Alive==true&&World.Get(id).garrisonedIn==0&&(World.Get(id).position-state.landingPoint).sqrMagnitude<=144&&World.Events.Skip(state.startEventIndex).Any(e=>e.kind=="Disembarked"&&e.entity==id&&e.target==state.transport));
                case GuidedPlayGoal.HoldLanding:return state.soldiers.Count(id=>World.Get(id)?.Alive==true&&World.Get(id).garrisonedIn==0&&World.Get(id).hold&&(World.Get(id).position-state.landingPoint).sqrMagnitude<=144&&Command(c=>c.kind==CommandKind.Order&&c.order==OrderKind.Hold&&c.actors.Contains(id)));
                case GuidedPlayGoal.Defend:
                    if(!state.raidIssued||!Command(c=>c.kind==CommandKind.Order&&(c.order==OrderKind.Attack&&(c.target==state.firstTarget||c.target==state.secondTarget)||c.order==OrderKind.AttackMove)))return 0;
                    return new[]{state.firstTarget,state.secondTarget}.Count(id=>World.Get(id)?.Alive==false&&World.Events.Any(e=>e.kind=="Death"&&e.entity==id&&e.target==0));
                default:return 0;
            }
        }
        public void Dispose(){if(disposed)return;disposed=true;Simulation.StepCompleted-=Tick;}
        static bool ValidAttempt(string id)=>!string.IsNullOrEmpty(id)&&id.Length<=96&&id.All(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-');
        static void Require(bool valid,string reason){if(!valid)throw new ArgumentException("Invalid guided progress: "+reason);}
        static void ValidateRole(World world,int id,string definition,int owner)=>Require(world.Get(id)?.def.id==definition&&world.Get(id).owner==owner,"role "+definition);
        static bool EventIs(World w,int index,string kind,string message)=>index>=0&&index<w.Events.Count&&w.Events[index].kind==kind&&w.Events[index].player==0&&w.Events[index].message==message;
        static string StartMarker(GuidedPlaySnapshot s)=>s.definitionId+"|"+s.attemptId+"|"+SetupHash(s);
        static string SetupHash(GuidedPlaySnapshot s)=>SnapshotCodec.Hash(s.definitionId+"|"+s.rulesHash+"|"+s.town+"|"+s.scout+"|"+s.firstTarget+"|"+s.secondTarget+"|"+s.transport+"|"+s.neutralSite+"|"+JsonUtility.ToJson(new GuidedPlaySetupWitness{workers=s.workers,soldiers=s.soldiers,move=s.movePoint,contact=s.contactPoint,boarding=s.boardingPoint,landing=s.landingPoint,build=s.buildPoint,barracks=s.barracksPoint,wood=s.startingWood})+(s.scenarioHash==""?"":"|"+s.scenarioHash));
        static string StageMarker(GuidedPlaySnapshot s,string stage)=>s.definitionId+"|"+s.attemptId+"|"+stage;
        static string CommandMarker(GuidedPlaySnapshot s,GameCommand c)=>s.attemptId+"|"+JsonUtility.ToJson(c);
        static string ResultMarker(GuidedPlaySnapshot s)=>s.definitionId+"|"+s.attemptId+"|"+s.reason;
        [Serializable] sealed class GuidedPlaySetupWitness {public int[] workers,soldiers;public Vector2 move,contact,boarding,landing,build,barracks;public float wood;}
        static Vector2 SafeLand(World w,Vector2 desired,float radius)
        {
            foreach(var p in w.Navigation.FormationSlots(desired,512,radius*2+.6f))
            {
                if(!w.Navigation.IsWalkable(p,radius)||w.IsWater(p)||w.IsBridgeApproach(p))continue;
                if(w.Entities.Any(e=>e.Alive&&e.garrisonedIn==0&&(e.def.kind==EntityKind.Building?w.BuildingFootprintContains(e.def,e.position,p,radius+.3f):(e.position-p).sqrMagnitude<Mathf.Pow(e.def.radius+radius+.4f,2))))continue;
                return p;
            }
            throw new InvalidOperationException("No safe scenario land placement");
        }
        static EntityState Place(World w,string id,int owner,Vector2 near,bool hold=true)
        {var d=w.Rules.Entity(id);var e=w.Spawn(id,owner,SafeLand(w,near,d.kind==EntityKind.Building?Mathf.Max(d.width,d.depth)*.5f+1:d.radius));e.hold=hold;return e;}
        static Vector2 BuildSite(World w,string id,Vector2 desired)
        {
            for(int distance=0;distance<=25;distance++)for(int y=-distance;y<=distance;y++)for(int x=-distance;x<=distance;x++)
            {
                if(Math.Max(Math.Abs(x),Math.Abs(y))!=distance)continue;var p=desired+new Vector2(x,y);
                if(w.CanBuild(0,id,p)=="")return p;
            }
            throw new InvalidOperationException("No legal scenario construction hint");
        }
        static void Configure(World w,GuidedPlayDefinition d,GuidedPlaySnapshot s)
        {
            s.town=w.Entities.First(e=>e.owner==0&&e.def.id=="town").id;s.scout=w.Entities.First(e=>e.owner==0&&e.def.id=="explorer").id;
            s.workers=w.Entities.Where(e=>e.owner==0&&e.Worker).Select(e=>e.id).ToArray();
            if(d.AuthoredScenarioId!=""){ConfigureAuthored(w,d,s);return;}
            if(ClassicCivilizationRoutes084.Contains(d.Id)){ConfigureCivilization084(w,d,s);return;}
            w.Players[0].age=d.StartingAge;w.Players[1].age=d.StartingAge;
            var grant=d.ScenarioGrant;
            if(grant.food+grant.wood+grant.coin>0){w.Players[0].resources+=grant;w.Emit("GuidedScenarioGrant",0,0,d.StartingSetupDescription,grant.food+grant.wood+grant.coin);}
            if(d.StartingAge>1)w.Emit("GuidedScenarioEra",0,s.town,"Scenario initial era "+d.StartingAge,d.StartingAge);
            s.movePoint=SafeLand(w,w.Get(s.scout).position+new Vector2(9,5),w.Get(s.scout).def.radius);
            w.Fog.Tick(w.Entities);s.buildPoint=BuildSite(w,"house",w.OpeningTownPosition(0)+new Vector2(13,6));
            s.barracksPoint=SafeLand(w,w.OpeningTownPosition(0)+new Vector2(12,11),Mathf.Max(w.Rules.Entity("barracks").width,w.Rules.Entity("barracks").depth)*.5f+1);
            if(d.Id=="lesson_shipments")w.AwardXp(0,150,s.town,"Explicit guided scenario experience");
            if(d.Id=="lesson_targeting")
            {
                var army=new List<int>();for(int i=0;i<4;i++)army.Add(Place(w,"pike",0,new Vector2(43,29+i*1.6f)).id);s.soldiers=army.ToArray();
                s.scout=Place(w,"explorer",0,new Vector2(46,27)).id;s.firstTarget=Place(w,"hussar",1,new Vector2(59,30)).id;s.secondTarget=Place(w,"house",1,new Vector2(65,30)).id;
            }
            if(d.Id=="lesson_allies")
            {
                s.neutralSite=w.NeutralAllies.OrderBy(a=>(w.Get(a.site).position-w.OpeningTownPosition(0)).sqrMagnitude).First().site;
                var site=w.Get(s.neutralSite).position;s.contactPoint=SafeLand(w,site+new Vector2(0,-5),.4f);
                s.scout=Place(w,"explorer",0,s.contactPoint+new Vector2(-7,0)).id;
                var workers=new List<int>(s.workers);for(int i=0;i<2;i++)workers.Add(Place(w,"villager",0,site+new Vector2(-6,i*2-2)).id);s.workers=workers.ToArray();
                s.movePoint=s.contactPoint;s.buildPoint=site;
            }
            if(d.Id=="campaign_supply")
            {
                var army=new List<int>();for(int i=0;i<2;i++)army.Add(Place(w,"pike",0,w.OpeningTownPosition(0)+new Vector2(10,8+i*2)).id);s.soldiers=army.ToArray();
                s.firstTarget=Place(w,"hussar",1,new Vector2(52,30)).id;s.secondTarget=Place(w,"hussar",1,new Vector2(52,34)).id;s.movePoint=new Vector2(45,32);
            }
            if(d.Id=="campaign_bridge")
            {
                var bridge=w.Layout.Bridges[0];float distance=bridge.HalfLength+bridge.RampLength+7;Vector2 start=bridge.Point(-distance,0);
                var army=new List<int>();for(int i=0;i<6;i++)army.Add(Place(w,i<4?"musketeer":"pike",0,start+new Vector2(0,i*1.8f-4)).id);s.soldiers=army.ToArray();
                Place(w,"house",0,w.OpeningTownPosition(0)+new Vector2(15,12));s.scout=Place(w,"explorer",0,start+new Vector2(1,-7)).id;
                s.movePoint=SafeLand(w,bridge.Point(distance,0),.6f);s.firstTarget=Place(w,"house",1,s.movePoint+new Vector2(9,0)).id;
                for(int i=0;i<2;i++)Place(w,"pike",1,w.Get(s.firstTarget).position+new Vector2(0,8+i*2));
            }
            if(d.Id=="lesson_transport"||d.Id=="campaign_estuary")ConfigureTransport(w,d,s);
            w.Fog.Tick(w.Entities);w.Emit("GuidedScenarioSetup",0,s.scout,d.StartingSetupDescription);
        }
        static void ConfigureTransport(World w,GuidedPlayDefinition d,GuidedPlaySnapshot s)
        {
            var shipDef=w.Rules.Entity("transport_ship");EntityState ship=null;
            foreach(var candidate in w.Layout.LandingSites[0])
            {
                Vector2 desired=candidate+w.Layout.WaterDirection(candidate)*8;
                var point=w.WaterNavigation.NearestWalkable(desired,shipDef.radius);
                if(!w.WaterNavigation.IsWalkable(point,shipDef.radius)||!w.IsWater(point)||w.Entities.Any(e=>e.Alive&&(e.position-point).sqrMagnitude<Mathf.Pow(e.def.radius+shipDef.radius+.4f,2)))continue;
                ship=w.Spawn(shipDef.id,0,point);ship.hold=true;
                if(w.TryTransportShore(ship,candidate,.65f,out var shore,out _)){s.boardingPoint=shore;break;}
                w.Remove(ship);ship=null;
            }
            if(ship==null)throw new InvalidOperationException("No playable initial transport shore");s.transport=ship.id;
            bool found=false;foreach(var candidate in w.Layout.IslandLandingSites)
                if(w.TryTransportShore(ship,candidate,.65f,out var shore,out _)&&(shore-s.boardingPoint).sqrMagnitude>400){s.landingPoint=shore;found=true;break;}
            if(!found)throw new InvalidOperationException("No distinct island landing shore");
            var soldiers=new List<int>();int count=d.Kind==GuidedPlayKind.Campaign?6:2;
            Vector2 landward=-w.Layout.WaterDirection(s.boardingPoint);Vector2 side=new Vector2(-landward.y,landward.x);
            for(int i=0;i<count;i++)soldiers.Add(Place(w,i<4?"musketeer":"pike",0,s.boardingPoint+landward*(7+i/3*2)+side*(i%3*2-2)).id);s.soldiers=soldiers.ToArray();
            s.scout=Place(w,"explorer",0,s.boardingPoint+landward*10+side*5).id;s.movePoint=s.boardingPoint;
            if(d.Kind==GuidedPlayKind.Campaign)
            {
                Place(w,"house",0,w.OpeningTownPosition(0)+new Vector2(15,12));
                Vector2 inland=-w.Layout.WaterDirection(s.landingPoint);
                s.firstTarget=Place(w,"house",1,s.landingPoint+inland*11).id;
                Place(w,"pike",1,w.Get(s.firstTarget).position+inland*6);Place(w,"pike",1,w.Get(s.firstTarget).position+inland*8+side*2);
            }
        }
    }
}
