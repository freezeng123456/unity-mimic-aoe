using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    // Explicit age/resource fixtures for later human native input. Every new
    // building and fighting unit still goes through normal paid commands.
    public static class NavalPreviewExport
    {
        const string Version="0.8.0";
        const float CaseLimit=1800;
        [MenuItem("Frontier/Export Naval And Mission Previews")]
        public static void Run()
        {
            string project=Path.GetDirectoryName(Application.dataPath),requested=Environment.GetEnvironmentVariable("FRONTIER_REPORT_PATH");
            if(Application.version!=Version)throw new InvalidOperationException("Naval preview exporter requires runtime "+Version);
            if(string.IsNullOrWhiteSpace(requested))throw new ArgumentException("FRONTIER_REPORT_PATH must specify an empty preview directory");
            string directory=Path.GetFullPath(requested),saves=Path.GetFullPath(Path.Combine(Application.persistentDataPath,"Saves"));
            if(Within(directory,saves)||new[]{"Assets","Packages","ProjectSettings","Tools"}.Any(d=>Within(directory,Path.Combine(project,d))))throw new IOException("Preview output must be outside player Saves and frozen source");
            for(var parent=new DirectoryInfo(directory);parent!=null;parent=parent.Parent)
                if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Preview output must not traverse a filesystem link: "+parent.FullName);
            if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any())throw new IOException("Preview directory is not empty: "+directory);
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)throw new InvalidOperationException("Finish compilation and asset import before preview export");
            string fingerprint=SourceFingerprint(project),receipt=RequireTests(project,fingerprint);Directory.CreateDirectory(directory);
            var report=new Manifest{startedUtc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,unityVersion=Application.unityVersion,
                sourceFingerprint=fingerprint,testReceipt=receipt,testReceiptSha256=FileHash(receipt)};WriteManifest(directory,report);
            try
            {
                foreach(var definition in new[]{new CaseDefinition("naval-strait-314159",MapTopology.GeneratedStrait,314159),new CaseDefinition("neutral-delta-271828",MapTopology.GeneratedDelta,271828),new CaseDefinition("bridge-river-161803",MapTopology.GeneratedRiver,161803),new CaseDefinition("monitor-harbor-161803",MapTopology.River,161803,4)})
                {
                    var item=new Receipt{id=definition.id,map=definition.map,seed=definition.seed,maxAge=definition.maxAge,sourceFingerprint=fingerprint,appVersion=Version};
                    if(item.maxAge==4)item.scope="Controlled age IV and explicit resource grants, capped at IV; no normal age advancement, population/health grants or direct entity spawning. Paid house, dock and monitor production with no AI; awaiting native user input verification.";
                    report.previews.Add(item);
                    string output=Path.Combine(directory,item.id);Directory.CreateDirectory(output);Context context=null;
                    try
                    {
                        context=new Context(item);Initialize(context);if(item.id.StartsWith("naval"))PrepareFleet(context);else if(item.id.StartsWith("neutral"))PrepareAllies(context);else if(item.id.StartsWith("monitor"))PrepareMonitor(context);else PrepareBridge(context);
                        SaveReady(output,context);item.status="exported";
                    }
                    catch(Exception error){item.status="failed";item.error=error.ToString();}
                    finally
                    {
                        if(context!=null)WriteDiagnostics(output,context);
                        File.WriteAllText(Path.Combine(output,"receipt.json"),JsonUtility.ToJson(item,true));WriteManifest(directory,report);
                    }
                }
                report.sourceFingerprintAfter=SourceFingerprint(project);
                if(report.sourceFingerprintAfter!=fingerprint)throw new InvalidOperationException("Source changed during naval preview export");
                if(report.previews.Any(p=>p.status!="exported"))throw new InvalidOperationException("One or more preview cases failed; inspect their retained receipts/events/state");
                report.status="exported";
            }
            catch(Exception error){report.status="failed";report.error=error.ToString();throw;}
            finally{report.finishedUtc=DateTime.UtcNow.ToString("o");WriteManifest(directory,report);}
        }
        sealed class Context
        {
            public readonly Receipt item;public readonly World world;public readonly MatchSimulation simulation;
            public Context(Receipt receipt)
            {
                item=receipt;world=new World(SnapshotCodec.Clone(Catalog.Load()),item.seed,"coast","coast",item.maxAge,item.map);
                simulation=new MatchSimulation(world,Array.Empty<int>());item.openingPopulation=new[]{PopulationSnapshot.From(world.Players[0]),PopulationSnapshot.From(world.Players[1])};
                item.openingResources=new[]{world.Players[0].resources,world.Players[1].resources};
            }
        }
        static void Initialize(Context c)
        {
            if(c.item.id.StartsWith("bridge"))Check(c.world.ConfigureMapObjective("defend_bridge"),"Configure objective before time advances");
            Grant(c,0,new Cost(8000,8000,8000));if(c.item.id.StartsWith("naval"))Grant(c,1,new Cost(2000,2000,2000));
        }
        static void Grant(Context c,int player,Cost resources)
        {
            var p=c.world.Players[player];Cost before=p.resources;int beforeAge=p.age;p.resources+=resources;p.age=c.item.maxAge;
            string age=c.item.maxAge==4?"IV":"III";p.ageHistory.Add("Controlled native preview: "+age+" @ 0s; no age advancement claim");
            var grant=new GrantRecord{player=player,previousAge=beforeAge,controlledAge=c.item.maxAge,amount=resources,before=before,after=p.resources};c.item.grants.Add(grant);
            var e=c.world.Emit("ControlledPreviewResourceGrant",player,0,"Explicit age-"+age+" native preview grant; not a normal opening");e.hasEconomy=true;
            e.economy=new EconomyTransaction{requestedDelta=resources,actualDelta=resources,balanceBefore=before,balanceAfter=p.resources};
        }
        static EntityState Town(Context c,int player)=>c.world.Entities.First(e=>e.Alive&&e.owner==player&&e.def.id=="town");
        static EntityState Worker(Context c,int player)=>c.world.Entities.First(e=>e.Alive&&e.owner==player&&e.Worker&&!e.def.waterborne&&e.garrisonedIn==0);
        static EntityState Scout(Context c,int player)=>c.world.Entities.First(e=>e.Alive&&e.owner==player&&(e.def.tags&Tags.Explorer)!=0);
        static void Check(CommandResult result,string purpose){if(!result.accepted)throw new InvalidOperationException(purpose+": "+result.reason);}
        static CommandResult Command(Context c,GameCommand command,bool required=true)
        {
            int first=c.world.Events.Count;var player=c.world.Players[command.player];var before=PopulationSnapshot.From(player);Cost resources=player.resources;
            var result=c.world.Issue(command);
            c.item.commands.Add(new CommandRecord{time=c.world.Time,command=SnapshotCodec.Clone(command),accepted=result.accepted,reason=result.reason,
                before=before,after=PopulationSnapshot.From(player),resourcesBefore=resources,resourcesAfter=player.resources,firstEvent=first,eventCount=c.world.Events.Count-first,
                queues=c.world.Entities.Where(e=>e.Alive&&e.owner==command.player&&e.queue.Count>0).Select(e=>new QueueRecord{entity=e.id,jobs=e.queue.Select(j=>SnapshotCodec.Clone(j)).ToArray()}).ToArray()});
            if(required)Check(result,command.kind+" "+command.item);return result;
        }
        static void Until(Context c,Func<bool> complete,float limit,string reason)
        {
            for(int step=0;step<Mathf.CeilToInt(limit/MatchSimulation.StepSeconds)&&!complete();step++)
                if(c.world.Time>=CaseLimit||!c.simulation.Step())throw new InvalidOperationException(reason+"; simulation stopped at "+c.world.Time);
            if(!complete())throw new InvalidOperationException(reason+"; timeout at "+c.world.Time);
        }
        static void Advance(Context c,float seconds)
        {float until=c.world.Time+seconds;Until(c,()=>c.world.Time+.0001f>=until,seconds+1,"Advancing ordinary simulation");}
        static bool HasPath(World world,EntityState actor,Vector2 point)
        {
            var navigation=world.NavigationFor(actor);if(!navigation.IsWalkable(point,actor.def.radius))return false;
            var path=navigation.FindPath(actor.position,point,actor.def.radius);return path.Count>0&&(path[path.Count-1]-point).sqrMagnitude<.01f;
        }
        static void Move(Context c,EntityState actor,Vector2 point,float limit=180)
        {
            if(!HasPath(c.world,actor,point))throw new InvalidOperationException(actor.def.id+" has no real path to "+point);
            Command(c,new GameCommand{player=actor.owner,kind=CommandKind.Order,actors=new[]{actor.id},order=OrderKind.Move,point=point});
            var order=actor.orders.Count>0?actor.orders.Peek():null;
            if(order==null||order.kind!=OrderKind.Move)throw new InvalidOperationException("Public Move did not create its movement order");
            // Single-actor Move still passes through normal formation slots.
            // Retain the actual order, including normal occupied-endpoint
            // resolution, rather than waiting forever at the requested point.
            var movement=new MovementRecord{entity=actor.id,startedAt=c.world.Time,requested=point,assigned=order.point};c.item.movements.Add(movement);
            Until(c,()=>
            {
                movement.actual=actor.position;movement.destination=order.hasResolvedPoint?order.resolvedPoint:order.point;movement.observedAt=c.world.Time;
                movement.distance=Vector2.Distance(actor.position,movement.destination);
                if(!actor.Alive)throw new InvalidOperationException("Moving actor "+actor.id+" ("+actor.def.id+") died before arrival");
                return movement.distance<.85f;
            },limit,"Actor "+actor.id+" must reach its actual public Move destination");
            movement.arrived=true;
            Command(c,new GameCommand{player=actor.owner,kind=CommandKind.Order,actors=new[]{actor.id},order=OrderKind.Hold});Advance(c,.5f);
        }
        static EntityState Build(Context c,int player,string id,Vector2 point)
        {
            int before=c.world.Entities.Max(e=>e.id);var worker=Worker(c,player);
            Command(c,new GameCommand{player=player,kind=CommandKind.Build,actors=new[]{worker.id},item=id,point=point});
            var building=c.world.Entities.First(e=>e.id>before&&e.owner==player&&e.def.id==id);
            Until(c,()=>
            {
                if(!worker.Alive)throw new InvalidOperationException("Builder "+worker.id+" died while constructing paid "+id+" "+building.id);
                if(!building.Alive)throw new InvalidOperationException("Paid "+id+" "+building.id+" was destroyed during construction");
                return building.Complete;
            },240,"Paid "+id+" construction must finish");
            c.item.buildings.Add(new BuildingRecord{entity=building.id,player=player,definition=id,position=building.position,paid=building.paidConstruction,completedAt=c.world.Time});return building;
        }
        static EntityState BuildNear(Context c,int player,string id,Vector2 anchor)
        {
            var sites=new List<Vector2>();for(int y=-20;y<=20;y++)for(int x=-20;x<=20;x++)sites.Add(new Vector2(Mathf.Round(anchor.x)+x,Mathf.Round(anchor.y)+y));
            sites=sites.OrderBy(p=>(p-anchor).sqrMagnitude).ThenBy(p=>p.x).ThenBy(p=>p.y).ToList();var worker=Worker(c,player);
            foreach(var site in sites)
            {
                if(c.world.CanBuild(player,id,site)!="")continue;
                var hypothetical=new EntityState{def=c.world.Rules.Entity(id),position=site};if(!HasPath(c.world,worker,c.world.WorkPoint(hypothetical,worker)))continue;
                return Build(c,player,id,site);
            }
            throw new InvalidOperationException("No visible legal reachable "+id+" site near "+anchor);
        }
        static void Housing(Context c,int player,int additionalPopulation)
        {
            int maximum=Math.Min(c.world.Players[player].populationCap,c.world.Rules.populationLimit);
            while(c.world.Players[player].usedPopulation+c.world.Players[player].reservedPopulation+additionalPopulation>maximum)
            {
                if(maximum>=c.world.Rules.populationLimit)throw new InvalidOperationException("Preview exceeds population limit");
                BuildNear(c,player,"house",Town(c,player).position);maximum=Math.Min(c.world.Players[player].populationCap,c.world.Rules.populationLimit);
            }
        }
        static EntityState Dock(Context c,int player)
        {
            var scout=Scout(c,player);
            foreach(var site in c.world.Layout.DockSites[player])
            {
                Vector2 watch=c.world.Navigation.NearestWalkable(site-c.world.Layout.WaterDirection(site)*8,scout.def.radius);if(!HasPath(c.world,scout,watch))continue;
                Move(c,scout,watch);string reason=c.world.CanBuild(player,"dock",site);
                c.item.probes.Add(new Probe{purpose="harbor",player=player,point=site,accepted=reason=="",reason=reason});
                if(reason=="")return Build(c,player,"dock",site);
            }
            throw new InvalidOperationException("No legal dock site reached by player "+player);
        }
        static EntityState Train(Context c,EntityState producer,string id)
        {
            Housing(c,producer.owner,c.world.Rules.Entity(id).population);int before=c.world.Entities.Max(e=>e.id),eventStart=c.world.Events.Count;
            Command(c,new GameCommand{player=producer.owner,kind=CommandKind.Train,actors=new[]{producer.id},item=id,count=1});
            Until(c,()=>c.world.Entities.Any(e=>e.id>before&&e.Alive&&e.owner==producer.owner&&e.def.id==id),c.world.Rules.Entity(id).time+120,"Paid "+id+" must be produced at a clear exit");
            var unit=c.world.Entities.First(e=>e.id>before&&e.Alive&&e.owner==producer.owner&&e.def.id==id);
            c.item.births.Add(new Birth{entity=unit.id,producer=producer.id,player=unit.owner,definition=id,bornAt=c.world.Time,firstEvent=eventStart,
                paymentEvents=c.world.Events.Skip(eventStart).Count(e=>e.kind=="Payment"&&e.entity==producer.id),spawnEvents=c.world.Events.Skip(eventStart).Count(e=>e.kind=="Spawn"&&e.entity==unit.id)});
            if(c.item.births.Last().paymentEvents!=1||c.item.births.Last().spawnEvents!=1)throw new InvalidOperationException("Unit birth is not paired to exactly one actual production payment");
            return unit;
        }
        static Vector2 FreeWater(Context c,EntityState ship,Vector2 desired,bool clearBridge=false)
        {
            var choices=new List<Vector2>();for(int y=-8;y<=8;y++)for(int x=-8;x<=8;x++)choices.Add(desired+new Vector2(x,y));
            foreach(var point in choices.OrderBy(p=>(p-desired).sqrMagnitude).ThenBy(p=>p.x).ThenBy(p=>p.y))
            {
                if(clearBridge&&c.world.Layout.BridgeFootprintOverlaps(point,6,6))continue;
                var slots=c.world.WaterNavigation.FormationSlots(point,1,Mathf.Max(1.8f,ship.def.radius*4+.3f));
                if(slots.Count!=1||(slots[0]-point).sqrMagnitude>.0001f)continue;
                if(HasPath(c.world,ship,point)&&!c.world.Entities.Any(e=>e.Alive&&e.id!=ship.id&&e.garrisonedIn==0&&(e.position-point).sqrMagnitude<Mathf.Pow(e.def.radius+ship.def.radius+1,2)))return point;
            }
            throw new InvalidOperationException("No clear reachable fleet parking point");
        }
        static void PrepareFleet(Context c)
        {
            var dock=Dock(c,0);Housing(c,0,15);var barracks=BuildNear(c,0,"barracks",Town(c,0).position);
            var first=Train(c,barracks,"pike");var second=Train(c,barracks,"musketeer");
            var transport=Train(c,dock,"transport_ship");var frigate=Train(c,dock,"frigate");var gunboat=Train(c,dock,"gunboat");
            Vector2 requested=c.world.Layout.LandingSites[0].OrderBy(p=>(p-dock.position).sqrMagnitude).First();
            if(!c.world.TryTransportShore(transport,requested,Mathf.Max(first.def.radius,second.def.radius),out var land,out var water))throw new InvalidOperationException("No usable embarkation shore for the produced army");
            Move(c,transport,water);Vector2 direction=c.world.Layout.WaterDirection(land),side=new Vector2(-direction.y,direction.x);
            Move(c,frigate,FreeWater(c,frigate,water+side*6));Move(c,gunboat,FreeWater(c,gunboat,water-side*6));
            Move(c,first,c.world.Navigation.NearestWalkable(land-direction*4+side*2,first.def.radius));
            Move(c,second,c.world.Navigation.NearestWalkable(land-direction*4-side*2,second.def.radius));
            c.item.transport=transport.id;c.item.friendlyUnits=new[]{first.id,second.id,transport.id,frigate.id,gunboat.id};c.item.embarkLand=land;c.item.embarkWater=water;
            var enemyDock=Dock(c,1);var enemy=Train(c,enemyDock,"gunboat");
            var destinations=new List<Vector2>();for(int ring=19;ring<=21;ring++)for(int i=0;i<72;i++)destinations.Add(frigate.position+new Vector2(Mathf.Cos(i*Mathf.PI/36),Mathf.Sin(i*Mathf.PI/36))*ring);
            Vector2 target=Vector2.zero;float best=float.MaxValue;
            foreach(var point in destinations)
            {
                if(!HasPath(c.world,enemy,point)||Vector2.Distance(point,frigate.position)>21||new[]{frigate,gunboat}.Any(e=>Vector2.Distance(e.position,point)<e.def.range+e.def.radius+enemy.def.radius+1.5f))continue;
                if(c.world.Entities.Any(e=>e.Alive&&e.id!=enemy.id&&e.garrisonedIn==0&&(e.position-point).sqrMagnitude<Mathf.Pow(e.def.radius+enemy.def.radius+.5f,2)))continue;
                var route=c.world.WaterNavigation.FindPath(enemy.position,point,enemy.def.radius);float length=0;for(int i=1;i<route.Count;i++)length+=Vector2.Distance(route[i-1],route[i]);
                if(length<best){best=length;target=point;}
            }
            if(best==float.MaxValue)throw new InvalidOperationException("No reachable visible enemy practice position outside initial weapon range");
            Move(c,enemy,target,300);if(!c.world.IsVisible(0,enemy)||!enemy.Alive)throw new InvalidOperationException("Paid enemy ship must be currently visible and alive for native targeting");
            foreach(int id in c.item.friendlyUnits)if(!c.world.Get(id).Alive)throw new InvalidOperationException("Fleet preview lost a required paid unit during preparation");
            c.item.enemyTarget=enemy.id;c.item.cameraFocus=(land+water+frigate.position+gunboat.position)/4;c.item.cameraSize=23;c.item.selection=new[]{first.id,second.id};
        }
        static void PrepareAllies(Context c)
        {
            Housing(c,0,5);var scout=Scout(c,0);
            foreach(var state in c.world.NeutralAllies.OrderBy(s=>(c.world.Get(s.site).position-Town(c,0).position).sqrMagnitude))
            {
                Vector2 site=c.world.Get(state.site).position;Vector2 approach=c.world.Navigation.NearestWalkable(site+new Vector2(-6,0),scout.def.radius);
                bool reachable=HasPath(c.world,scout,approach);c.item.probes.Add(new Probe{purpose="neutral contact",player=0,point=approach,accepted=reachable,reason=reachable?"":"No land path"});if(!reachable)continue;
                Move(c,scout,approach);ClearVisibleGuardians(c,site);Move(c,scout,approach);
                var post=Build(c,0,"trade_post",site);var definition=c.world.NeutralDefinition(state.id);
                Command(c,new GameCommand{player=0,kind=CommandKind.NeutralAlliance,target=state.site});Until(c,()=>state.owner==0,definition.allianceSeconds+20,"Paid alliance must complete in actual contact");
                int before=c.world.Entities.Max(e=>e.id),eventStart=c.world.Events.Count;
                Command(c,new GameCommand{player=0,kind=CommandKind.NeutralRecruit,target=state.site});
                Until(c,()=>c.world.Entities.Any(e=>e.Alive&&e.id>before&&e.owner==0&&e.def.id==definition.unit),c.world.Rules.Entity(definition.unit).time+60,"Paid ally must actually spawn");
                var ally=c.world.Entities.First(e=>e.Alive&&e.id>before&&e.owner==0&&e.def.id==definition.unit);
                c.item.births.Add(new Birth{entity=ally.id,producer=state.site,player=0,definition=ally.def.id,bornAt=c.world.Time,firstEvent=eventStart,paymentEvents=c.world.Events.Skip(eventStart).Count(e=>e.kind=="Payment"&&e.entity==state.site),spawnEvents=c.world.Events.Skip(eventStart).Count(e=>e.kind=="Spawn"&&e.entity==ally.id)});
                if(c.item.births.Last().paymentEvents!=1||c.item.births.Last().spawnEvents!=1)throw new InvalidOperationException("Allied birth lacks its actual service payment");
                Command(c,new GameCommand{player=0,kind=CommandKind.NeutralResearch,target=state.site});Until(c,()=>c.world.Players[0].technologies.Contains(definition.tech),c.world.Rules.Tech(definition.tech).time+20,"Paid ally research must really complete");
                c.item.allies.Add(new AllyRecord{id=state.id,site=state.site,post=post.id,unit=ally.id,tech=definition.tech,completeAt=c.world.Time});c.item.cameraFocus=post.position;c.item.selection=new[]{post.id};
                if(c.item.allies.Count==2)break;
            }
            if(c.item.allies.Count<2)throw new InvalidOperationException("Fewer than two neutral services could be reached and completed on the specified seed");
            c.item.friendlyUnits=c.item.allies.Select(a=>a.unit).ToArray();c.item.cameraSize=19;
        }
        static void ClearVisibleGuardians(Context c,Vector2 site)
        {
            // Scout contact is real vision. Previously the unguarded worker
            // reached the second site and died midway through construction.
            // Use only the starting explorer and already paid allied recruits;
            // discover and defeat each visible camp defender through Attack.
            for(int attempt=0;attempt<16;attempt++)
            {
                var target=c.world.Entities.FirstOrDefault(e=>e.Alive&&World.Guardian(e)&&c.world.IsVisible(0,e)&&(e.position-site).sqrMagnitude<=18*18);
                if(target==null)return;
                var fighters=new[]{Scout(c,0)}.Concat(c.item.allies.Select(a=>c.world.Get(a.unit))).Where(e=>e!=null&&e.Alive).Select(e=>e.id).ToArray();
                Command(c,new GameCommand{player=0,kind=CommandKind.Order,actors=fighters,order=OrderKind.Attack,target=target.id});
                Until(c,()=>!target.Alive,90,"Visible guardian "+target.id+" must be defeated before exposing the builder");
                Advance(c,.5f);
            }
            throw new InvalidOperationException("Visible settlement guardians exceeded the bounded combat preparation");
        }
        static void PrepareMonitor(Context c)
        {
            if(c.world.MaxAge!=4||c.world.Players[0].age!=4)throw new InvalidOperationException("Monitor preview requires its explicit age-IV fixture grant and cap");
            BuildNear(c,0,"house",Town(c,0).position);var dock=Dock(c,0);var monitor=Train(c,dock,"monitor");
            Vector2 direction=c.world.Layout.WaterDirection(dock.position),side=new Vector2(-direction.y,direction.x);
            // The authored straight river has a legal harbor and an open-water
            // approach away from the bridge, without adding a hostile target.
            Vector2 parking=FreeWater(c,monitor,dock.position+direction*9-side*10,true);
            if(c.world.Layout.BridgeFootprintOverlaps(parking,6,6))throw new InvalidOperationException("Monitor silhouette parking must be clear of bridge decks");
            Move(c,monitor,parking);
            if(!monitor.Alive||!monitor.hold||!c.world.IsWater(monitor.position)||c.world.Layout.BridgeFootprintOverlaps(monitor.position,6,6)||c.world.Fog.At(0,monitor.position)!=VisibilityState.Visible)
                throw new InvalidOperationException("Paid monitor must be alive, held and actually visible in open water");
            c.item.friendlyUnits=new[]{monitor.id};c.item.selection=new[]{monitor.id};c.item.cameraFocus=monitor.position;c.item.cameraSize=13;
        }
        static void PrepareBridge(Context c)
        {
            Housing(c,0,4);var barracks=BuildNear(c,0,"barracks",Town(c,0).position);var units=new[]{Train(c,barracks,"pike"),Train(c,barracks,"musketeer"),Train(c,barracks,"pike")};
            Command(c,new GameCommand{player=0,kind=CommandKind.Order,actors=units.Select(e=>e.id).ToArray(),order=OrderKind.Move,point=c.world.MapObjective.center});
            Until(c,()=>c.world.MapObjective.progress>=8&&units.All(e=>e.Alive&&(e.position-c.world.MapObjective.center).sqrMagnitude<64),150,"Paid bridge guard must march into the real control radius");
            Command(c,new GameCommand{player=0,kind=CommandKind.Order,actors=units.Select(e=>e.id).ToArray(),order=OrderKind.Hold});
            var definition=c.world.ObjectiveDefinition(c.world.MapObjective.mode);
            if(c.world.Winner>=0||c.world.MapObjective.status!="active"||c.world.MapObjective.progress>=definition.holdSeconds)throw new InvalidOperationException("Bridge preview must remain an unfinished active mission");
            c.item.friendlyUnits=units.Select(e=>e.id).ToArray();c.item.cameraFocus=c.world.MapObjective.center;c.item.selection=c.item.friendlyUnits;c.item.cameraSize=20;
        }
        static void SaveReady(string output,Context c)
        {
            c.world.FlushEconomyLog();c.world.Paused=true;c.item.matchId="naval-preview-"+c.item.id+"-"+Guid.NewGuid().ToString("N");
            var save=new SavedMatch{matchId=c.item.matchId,appVersion=Version,world=c.world.CaptureState(),simulation=c.simulation.CaptureState(),
                cameraFocus=c.item.cameraFocus,cameraPosition=new Vector3(c.item.cameraFocus.x,50,c.item.cameraFocus.y-42),cameraSize=c.item.cameraSize,
                selection=c.item.selection,groups=Array.Empty<ControlGroupSnapshot>(),autoplay=false,simulationSpeed=1,muted=false,environmentSound=true,music=true};
            string path=Path.Combine(output,"candidate-preview.json");MatchSaveStore.Write(path,save);var loaded=MatchSaveStore.Read(path,Version);
            var restored=World.Restore(c.world.Rules,loaded.world);var simulation=MatchSimulation.Restore(restored,loaded.simulation);
            string before=JsonUtility.ToJson(c.world.CaptureState()),after=JsonUtility.ToJson(restored.CaptureState());
            if(before!=after||JsonUtility.ToJson(c.simulation.CaptureState())!=JsonUtility.ToJson(simulation.CaptureState())||!restored.Paused||simulation.CaptureState().controllers.Length!=0)throw new InvalidOperationException("Cold JSON round trip differs from the prepared world/simulation");
            restored.Paused=c.world.Paused=false;
            for(int i=0;i<20;i++){if(!simulation.Step()||!c.simulation.Step())throw new InvalidOperationException("Preview ended during cold continuation verification");}
            if(JsonUtility.ToJson(restored.CaptureState())!=JsonUtility.ToJson(c.world.CaptureState())||JsonUtility.ToJson(simulation.CaptureState())!=JsonUtility.ToJson(c.simulation.CaptureState()))throw new InvalidOperationException("Original and cold-restored simulations diverged on continuation");
            // The delivered paused state is the verified continuation endpoint;
            // all receipt clocks/queues/health describe this exact saved world.
            c.world.Paused=true;save.world=c.world.CaptureState();save.simulation=c.simulation.CaptureState();MatchSaveStore.Write(path,save);
            loaded=MatchSaveStore.Read(path,Version);restored=World.Restore(c.world.Rules,loaded.world);simulation=MatchSimulation.Restore(restored,loaded.simulation);
            before=JsonUtility.ToJson(c.world.CaptureState());
            if(before!=JsonUtility.ToJson(restored.CaptureState())||JsonUtility.ToJson(c.simulation.CaptureState())!=JsonUtility.ToJson(simulation.CaptureState()))throw new InvalidOperationException("Final paused preview round trip differs");
            string ready=Path.Combine(output,"preview.json");File.Move(path,ready);path=ready;
            c.item.coldRoundTripPassed=true;c.item.coldContinuationSteps=20;c.item.worldJsonSha256=SnapshotCodec.Hash(before);c.item.simulationJsonSha256=SnapshotCodec.Hash(JsonUtility.ToJson(c.simulation.CaptureState()));
            c.item.saveFile="preview.json";c.item.saveSha256=FileHash(path);c.item.saveBytes=new FileInfo(path).Length;c.item.rulesHash=save.world.rulesHash;
        }
        static void WriteDiagnostics(string output,Context c)
        {
            c.item.worldTime=c.world.Time;c.item.winner=c.world.Winner;c.item.objective=c.world.CaptureMapObjective();c.item.neutralAllies=c.world.CaptureNeutralAllies();
            c.item.finalResources=c.world.Players.Select(p=>p.resources).ToArray();c.item.spent=c.world.Players.Select(p=>p.spent).ToArray();c.item.finalPopulation=c.world.Players.Select(PopulationSnapshot.From).ToArray();
            c.item.entities=c.world.Entities.Where(e=>e.Alive&&e.owner>=0).Select(e=>new EntityRecord{id=e.id,owner=e.owner,definition=e.def.id,position=e.position,hp=e.hp,complete=e.Complete,garrisonedIn=e.garrisonedIn,queue=e.queue.ToArray()}).ToArray();
            string events=Path.Combine(output,"events.json");File.WriteAllText(events,JsonUtility.ToJson(new EventLog{events=c.world.Events.ToArray()},true));c.item.eventsSha256=FileHash(events);c.item.eventCount=c.world.Events.Count;
            if(c.item.status=="failed")File.WriteAllText(Path.Combine(output,"failed-world.json"),JsonUtility.ToJson(c.world.CaptureState(),true));
        }
        static bool Within(string path,string directory)=>path.Equals(Path.GetFullPath(directory),StringComparison.OrdinalIgnoreCase)||path.StartsWith(Path.GetFullPath(directory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
        static void WriteManifest(string directory,Manifest report)=>File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonUtility.ToJson(report,true));
        static string FileHash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        static string SourceFingerprint(string project)
        {
            var sources=new SortedDictionary<string,string>(StringComparer.Ordinal);
            foreach(string directory in new[]{"Assets","Packages","ProjectSettings","Tools"})foreach(string path in Directory.GetFiles(Path.Combine(project,directory),"*",SearchOption.AllDirectories))
            {string relative=path.Substring(project.Length+1).Replace(Path.DirectorySeparatorChar,'/');if(relative.Split('/').Any(p=>p.StartsWith(".",StringComparison.Ordinal)||p=="__pycache__"))continue;sources.Add(relative,FileHash(path));}
            return SnapshotCodec.Hash("{"+string.Join(",",sources.Select(s=>Quote(s.Key)+":"+Quote(s.Value)))+"}");
        }
        static string Quote(string value)
        {
            var result=new StringBuilder("\"");foreach(char c in value)
            {if(c=='"'||c=='\\')result.Append('\\').Append(c);else if(c=='\b')result.Append("\\b");else if(c=='\f')result.Append("\\f");else if(c=='\n')result.Append("\\n");else if(c=='\r')result.Append("\\r");else if(c=='\t')result.Append("\\t");else if(c<32||c>126)result.Append("\\u").Append(((int)c).ToString("x4"));else result.Append(c);}
            return result.Append('"').ToString();
        }
        static string RequireTests(string project,string fingerprint)
        {
            string directory=Environment.GetEnvironmentVariable("FRONTIER_VERIFIED_TEST_REPORTS");
            if(string.IsNullOrEmpty(directory))directory=Path.Combine(project,"Builds/Tests",File.ReadAllText(Path.Combine(project,"Builds/Tests/latest-run.txt")).Trim());
            string path=Path.Combine(directory,"source-evidence.json");var receipt=JsonUtility.FromJson<TestReceipt>(File.ReadAllText(path));
            if(receipt==null||receipt.sourceFingerprint!=fingerprint)throw new InvalidOperationException("Preview requires complete tests bound to the current source fingerprint");
            foreach(var report in new[]{receipt.reports?.EditMode,receipt.reports?.PlayMode})
            {
                if(report==null)throw new InvalidOperationException("Both EditMode and PlayMode reports are required");
                string xmlPath=Path.Combine(directory,report.file);var xml=new XmlDocument();xml.Load(xmlPath);var node=xml.DocumentElement;
                if(node.Name!="test-run"||node.GetAttribute("result")!="Passed"||node.GetAttribute("failed")!="0"||node.GetAttribute("passed")!=node.GetAttribute("total")||report.passed<=0||node.GetAttribute("passed")!=report.passed.ToString()||FileHash(xmlPath)!=report.sha256)throw new InvalidOperationException("Incomplete or changed test report: "+xmlPath);
            }
            return Path.GetFullPath(path);
        }
        sealed class CaseDefinition{public string id,map;public int seed,maxAge;public CaseDefinition(string id,string map,int seed,int maxAge=3){this.id=id;this.map=map;this.seed=seed;this.maxAge=maxAge;}}
        [Serializable]sealed class TestReceipt{public string sourceFingerprint;public TestReports reports;}
        [Serializable]sealed class TestReports{public TestReport EditMode,PlayMode;}
        [Serializable]sealed class TestReport{public string file,sha256;public int passed;}
        [Serializable]sealed class Manifest
        {
            public string status="running",startedUtc,finishedUtc,appVersion,unityVersion,sourceFingerprint,sourceFingerprintAfter,testReceipt,testReceiptSha256,error;
            public string scope="Four controlled age/resource previews with no AI: fleet, settlements and bridge at age III/cap III, plus a separate monitor harbor at age IV/cap IV. All new buildings and combat units use normal paid commands. Not standard-opening, native-rendering, competitive or performance evidence.";
            public List<Receipt> previews=new List<Receipt>();
        }
        [Serializable]sealed class Receipt
        {
            public string id,map,appVersion,sourceFingerprint,status="running",error,matchId,saveFile,saveSha256,rulesHash,worldJsonSha256,simulationJsonSha256,eventsSha256;
            public string scope="Controlled age III and explicit resource grants; no population/health grants and no direct entity spawning. Paid construction/production/services with no AI; awaiting native user input verification.";
            public int seed,maxAge=3,winner=-1,transport,enemyTarget,eventCount,coldContinuationSteps;public long saveBytes;public float worldTime,cameraSize=22;public bool coldRoundTripPassed;
            public int[] automatedPlayers=Array.Empty<int>(),friendlyUnits=Array.Empty<int>(),selection=Array.Empty<int>();public Vector2 cameraFocus,embarkLand,embarkWater;
            public Cost[] openingResources,finalResources,spent;public PopulationSnapshot[] openingPopulation,finalPopulation;public EntityRecord[] entities;public MapObjectiveState objective;public NeutralAllianceState[] neutralAllies;
            public List<GrantRecord> grants=new List<GrantRecord>();public List<CommandRecord> commands=new List<CommandRecord>();public List<BuildingRecord> buildings=new List<BuildingRecord>();
            public List<Birth> births=new List<Birth>();public List<AllyRecord> allies=new List<AllyRecord>();public List<Probe> probes=new List<Probe>();public List<MovementRecord> movements=new List<MovementRecord>();
        }
        [Serializable]sealed class MovementRecord{public int entity;public float startedAt,observedAt,distance;public Vector2 requested,assigned,destination,actual;public bool arrived;}
        [Serializable]sealed class GrantRecord{public int player,previousAge,controlledAge;public Cost amount,before,after;}
        [Serializable]sealed class CommandRecord{public float time;public GameCommand command;public bool accepted;public string reason;public int firstEvent,eventCount;public PopulationSnapshot before,after;public Cost resourcesBefore,resourcesAfter;public QueueRecord[] queues;}
        [Serializable]sealed class QueueRecord{public int entity;public ProductionJob[] jobs;}
        [Serializable]sealed class BuildingRecord{public int entity,player;public string definition;public Vector2 position;public Cost paid;public float completedAt;}
        [Serializable]sealed class Birth{public int entity,producer,player,firstEvent,paymentEvents,spawnEvents;public string definition;public float bornAt;}
        [Serializable]sealed class AllyRecord{public string id,tech;public int site,post,unit;public float completeAt;}
        [Serializable]sealed class Probe{public string purpose,reason;public int player;public Vector2 point;public bool accepted;}
        [Serializable]sealed class EntityRecord{public int id,owner,garrisonedIn;public string definition;public Vector2 position;public float hp;public bool complete;public ProductionJob[] queue;}
        [Serializable]sealed class EventLog{public SimEvent[] events;}
    }
}
