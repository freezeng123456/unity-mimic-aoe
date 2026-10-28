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
    // Controlled visual fixtures. This exporter never writes a player's slots,
    // changes a scene, prepares a project, or grants resources/health/entities.
    public static class IndustrialPreviewExport
    {
        const float CaseLimit=900;
        [MenuItem("Frontier/Export Industrial Previews")]
        public static void Run()
        {
            string project=Path.GetDirectoryName(Application.dataPath);
            string requested=Environment.GetEnvironmentVariable("FRONTIER_REPORT_PATH");
            if(string.IsNullOrWhiteSpace(requested))throw new ArgumentException("FRONTIER_REPORT_PATH must specify a fresh preview directory");
            string directory=Path.GetFullPath(requested),saves=Path.GetFullPath(Path.Combine(Application.persistentDataPath,"Saves"));
            if(Within(directory,saves)||new[]{"Assets","Packages","ProjectSettings","Tools"}.Any(d=>Within(directory,Path.Combine(project,d))))
                throw new IOException("Preview output must be outside player Saves and frozen source directories");
            if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any())throw new IOException("Preview directory is not empty: "+directory);
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)throw new InvalidOperationException("Finish compilation and asset import before exporting previews");
            string fingerprint=SourceFingerprint(project),receipt=RequireTests(project,fingerprint);
            Directory.CreateDirectory(directory);
            var report=new Report{startedUtc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,unityVersion=Application.unityVersion,
                sourceFingerprint=fingerprint,testReceipt=receipt,testReceiptSha256=FileHash(receipt)};
            SaveReport(directory,report);
            try
            {
                foreach(string civilization in new[]{"coast","river","france","ottoman"})
                {
                    var item=new Preview{civilization=civilization,sourceFingerprint=fingerprint,appVersion=Application.version};report.previews.Add(item);SaveReport(directory,report);
                    try{Generate(directory,item);item.status="exported";}
                    catch(Exception error){item.status="failed";item.error=error.ToString();throw;}
                    finally{File.WriteAllText(Path.Combine(directory,"industrial-"+civilization+"-report.json"),JsonUtility.ToJson(item,true));SaveReport(directory,report);}
                }
                report.sourceFingerprintAfter=SourceFingerprint(project);
                if(report.sourceFingerprintAfter!=fingerprint)throw new InvalidOperationException("Source changed during industrial export");
                report.status="exported";
            }
            catch(Exception error){report.status="failed";report.error=error.ToString();throw;}
            finally{report.finishedUtc=DateTime.UtcNow.ToString("o");SaveReport(directory,report);}
        }

        static void Generate(string directory,Preview item)
        {
            var rules=JsonUtility.FromJson<RuleCatalog>(JsonUtility.ToJson(Catalog.Load()));
            var world=new World(rules,314159,item.civilization,"coast",5,MapTopology.Winding);
            var simulation=new MatchSimulation(world,Array.Empty<int>());var player=world.Players[0];
            player.age=4;player.ageHistory.Add("Controlled visual preview: IV @ 0s; no age advancement claim");
            player.deck.Clear();player.deck.AddRange(new[]{"factory_wagon","robber_barons"});
            var town=world.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="town");
            if(world.AutoRecruitSeconds(0)>0)Command(world,new GameCommand{player=0,kind=CommandKind.ToggleRecruitment,actors=new[]{town.id}});
            item.startingResources=player.resources;item.startingPopulation=player.usedPopulation;item.town=town.id;
            var scout=world.Entities.First(e=>e.Alive&&e.owner==0&&(e.def.tags&Tags.Explorer)!=0);
            var worker=world.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker);
            EntityState dock=null;
            foreach(var site in world.Layout.DockSites[0])
            {
                Vector2 direction=world.Layout.WaterDirection(site);
                Vector2 scoutPoint=world.Navigation.NearestWalkable(site-direction*8,scout.def.radius);
                Command(world,new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{scout.id},order=OrderKind.Move,point=scoutPoint});
                Until(simulation,()=>Vector2.Distance(scout.position,scoutPoint)<.9f,120,"Scout must reach the harbor through normal navigation");
                if(world.CanBuild(0,"dock",site)!="")continue;
                int previousId=world.Entities.Max(e=>e.id);
                Command(world,new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{worker.id},item="dock",point=site});
                dock=world.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="dock"&&e.id>previousId);
                Until(simulation,()=>dock.Complete,180,"A normally paid harbor must finish construction");break;
            }
            if(dock==null)throw new InvalidOperationException("No currently visible legal harbor site");
            item.dock=dock.id;
            var resourceFactory=Factory(simulation,dock,"factory_wagon",item);
            Command(world,new GameCommand{player=0,kind=CommandKind.FactoryMode,actors=new[]{resourceFactory.id},item="coin"});
            var artilleryFactory=Factory(simulation,dock,"robber_barons",item);
            var artilleryDefinition=world.FactoryArtilleryDefinition(0);
            if(!world.FindSpawnPositions(artilleryFactory,1,artilleryDefinition.radius,out var rallyPoints))throw new InvalidOperationException("Industrial preview has no safe artillery exit");
            Command(world,new GameCommand{player=0,kind=CommandKind.Rally,actors=new[]{artilleryFactory.id},point=rallyPoints[0]});
            Command(world,new GameCommand{player=0,kind=CommandKind.FactoryMode,actors=new[]{artilleryFactory.id},item="artillery"});
            Until(simulation,()=>world.Events.Any(e=>e.kind=="FactoryArtilleryProduced"&&e.entity==artilleryFactory.id),world.FactoryArtillerySeconds(0)+5,"Factory must actually produce the civilization's artillery");
            var produced=world.Events.Last(e=>e.kind=="FactoryArtilleryProduced"&&e.entity==artilleryFactory.id);
            var gun=world.Get(produced.target);
            if(gun==null||!gun.Alive||gun.def.id!=artilleryDefinition.id||!world.Navigation.IsWalkable(gun.position,gun.def.radius))throw new InvalidOperationException("Produced artillery is missing or has an unsafe body position");
            item.artillery=gun.id;item.artilleryDefinition=gun.def.id;item.artilleryPosition=gun.position;
            item.factories=new[]{new FactoryRecord(resourceFactory),new FactoryRecord(artilleryFactory)};
            item.shipmentArrivals=world.Events.Count(e=>e.kind=="ShipmentArrived"&&e.player==0);
            item.productionEvents=world.Events.Count(e=>e.kind=="FactoryArtilleryProduced"&&e.entity==artilleryFactory.id);
            item.endingResources=player.resources;item.spent=player.spent;item.passiveIncome=player.passiveIncome;item.worldTime=world.Time;
            world.FlushEconomyLog();world.Paused=true;
            Vector2 focus=(dock.position+resourceFactory.position+artilleryFactory.position)/3;
            item.cameraFocus=focus;item.matchId="industrial-preview-"+item.civilization+"-"+Guid.NewGuid().ToString("N");
            var save=new SavedMatch{matchId=item.matchId,appVersion=Application.version,world=world.CaptureState(),simulation=simulation.CaptureState(),
                cameraFocus=focus,cameraPosition=new Vector3(focus.x,50,focus.y-42),cameraSize=15,selection=new[]{artilleryFactory.id},groups=Array.Empty<ControlGroupSnapshot>(),
                autoplay=false,simulationSpeed=1,muted=false,environmentSound=true,music=true};
            item.saveFile="industrial-"+item.civilization+".json";string path=Path.Combine(directory,item.saveFile);MatchSaveStore.Write(path,save);
            var loaded=MatchSaveStore.Read(path,Application.version);var restored=World.Restore(rules,loaded.world);
            var restoredSimulation=MatchSimulation.Restore(restored,loaded.simulation);
            if(!restored.Paused||restoredSimulation.CaptureState().controllers.Length!=0||restored.Get(gun.id)?.def.id!=gun.def.id)throw new InvalidOperationException("Preview save did not restore its paused industrial state");
            item.saveSha256=FileHash(path);item.saveBytes=new FileInfo(path).Length;item.rulesHash=save.world.rulesHash;
            Debug.Log("INDUSTRIAL_PREVIEW "+item.civilization+" "+path+" artillery="+gun.id+" controlled_age_IV");
        }

        static EntityState Factory(MatchSimulation simulation,EntityState dock,string card,Preview item)
        {
            var world=simulation.World;var player=world.Players[0];
            if(player.shipmentTokens==0)
            {
                float xp=Mathf.Max(0,world.Rules.initialXp+world.Rules.xpIncrease*player.xpLevels-player.xp);
                world.AwardXp(0,xp,0,"Controlled industrial visual preview XP input");item.controlledXp+=xp;
            }
            int oldId=world.Entities.Max(e=>e.id);Command(world,new GameCommand{player=0,kind=CommandKind.Ship,item=card});
            Until(simulation,()=>world.Entities.Any(e=>e.Alive&&e.owner==0&&e.def.id=="factory_wagon"&&e.id>oldId),world.Rules.Card(card).travel+5,"Real factory shipment must arrive");
            var wagon=world.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="factory_wagon"&&e.id>oldId);Vector2 start=wagon.position;
            Vector2 anchor=dock.position-world.Layout.WaterDirection(dock.position)*9;
            var candidates=new List<Vector2>();
            for(int y=-18;y<=18;y++)for(int x=-18;x<=18;x++)candidates.Add(new Vector2(Mathf.Round(anchor.x)+x,Mathf.Round(anchor.y)+y));
            candidates.Sort((a,b)=>(a-anchor).sqrMagnitude.CompareTo((b-anchor).sqrMagnitude));
            bool found=false;Vector2 site=Vector2.zero;
            foreach(var point in candidates)if(world.CanBuild(0,"factory",point)==""&&world.IndustrialDeploymentReason(0,point,wagon.id)==""){site=point;found=true;break;}
            if(!found)throw new InvalidOperationException("No visible legal industrial yard near the paid harbor");
            Command(world,new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{wagon.id},item="factory",point=site});
            int before=world.Entities.Max(e=>e.id);
            Until(simulation,()=>!wagon.Alive,180,"Factory wagon must move to its own construction approach");
            var factory=world.Entities.FirstOrDefault(e=>e.Alive&&e.owner==0&&e.def.id=="factory"&&e.id>before);
            if(factory==null)throw new InvalidOperationException("Wagon disappeared without constructing its factory");
            item.deployments.Add(new Deployment{card=card,wagon=wagon.id,wagonStart=start,site=factory.position,arrivalPosition=wagon.position,foundationTime=world.Time});
            Until(simulation,()=>factory.Complete,world.Rules.Entity("factory").time+5,"Factory foundation must complete through ordinary simulation steps");
            return factory;
        }

        static void Command(World world,GameCommand command)
        {var result=world.Issue(command);if(!result.accepted)throw new InvalidOperationException(command.kind+": "+result.reason);}
        static void Until(MatchSimulation simulation,Func<bool> complete,float seconds,string reason)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/MatchSimulation.StepSeconds)&&!complete();i++)
                if(simulation.World.Time>=CaseLimit||!simulation.Step())throw new InvalidOperationException(reason+"; simulation stopped or exceeded 900s");
            if(!complete())throw new InvalidOperationException(reason+"; time="+simulation.World.Time);
        }
        static bool Within(string path,string directory)=>path.Equals(Path.GetFullPath(directory),StringComparison.OrdinalIgnoreCase)||path.StartsWith(Path.GetFullPath(directory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
        static void SaveReport(string directory,Report report)=>File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonUtility.ToJson(report,true));
        static string FileHash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return Hex(sha.ComputeHash(stream));}
        static string Hex(byte[] hash)=>BitConverter.ToString(hash).Replace("-","").ToLowerInvariant();
        static string SourceFingerprint(string project)
        {
            var sources=new SortedDictionary<string,string>(StringComparer.Ordinal);
            foreach(string directory in new[]{"Assets","Packages","ProjectSettings","Tools"})foreach(string path in Directory.GetFiles(Path.Combine(project,directory),"*",SearchOption.AllDirectories))
            {
                string relative=path.Substring(project.Length+1).Replace(Path.DirectorySeparatorChar,'/');
                if(relative.Split('/').Any(p=>p.StartsWith(".",StringComparison.Ordinal)||p=="__pycache__"))continue;
                sources.Add(relative,FileHash(path));
            }
            string json="{"+string.Join(",",sources.Select(s=>Quote(s.Key)+":"+Quote(s.Value)))+"}";
            return SnapshotCodec.Hash(json);
        }
        static string Quote(string value)
        {
            var result=new StringBuilder("\"");foreach(char c in value)
            {
                if(c=='"'||c=='\\')result.Append('\\').Append(c);
                else if(c=='\b')result.Append("\\b");else if(c=='\f')result.Append("\\f");else if(c=='\n')result.Append("\\n");
                else if(c=='\r')result.Append("\\r");else if(c=='\t')result.Append("\\t");
                else if(c<32||c>126)result.Append("\\u").Append(((int)c).ToString("x4"));else result.Append(c);
            }
            return result.Append('"').ToString();
        }
        static string RequireTests(string project,string fingerprint)
        {
            string directory=Environment.GetEnvironmentVariable("FRONTIER_VERIFIED_TEST_REPORTS");
            if(string.IsNullOrEmpty(directory))directory=Path.Combine(project,"Builds/Tests",File.ReadAllText(Path.Combine(project,"Builds/Tests/latest-run.txt")).Trim());
            string path=Path.Combine(directory,"source-evidence.json");var receipt=JsonUtility.FromJson<TestReceipt>(File.ReadAllText(path));
            if(receipt==null||receipt.sourceFingerprint!=fingerprint)throw new InvalidOperationException("Preview requires passing tests bound to current source");
            foreach(var report in new[]{receipt.reports?.EditMode,receipt.reports?.PlayMode})
            {
                if(report==null)throw new InvalidOperationException("Preview requires both test reports");
                string xmlPath=Path.Combine(directory,report.file);var xml=new XmlDocument();xml.Load(xmlPath);var node=xml.DocumentElement;
                if(node.Name!="test-run"||node.GetAttribute("result")!="Passed"||node.GetAttribute("failed")!="0"||node.GetAttribute("passed")!=node.GetAttribute("total")||report.passed<=0||node.GetAttribute("passed")!=report.passed.ToString()||FileHash(xmlPath)!=report.sha256)
                    throw new InvalidOperationException("Preview requires a complete passing current test report: "+xmlPath);
            }
            return Path.GetFullPath(path);
        }
        [Serializable]sealed class TestReceipt{public string sourceFingerprint;public TestReports reports;}
        [Serializable]sealed class TestReports{public TestReport EditMode,PlayMode;}
        [Serializable]sealed class TestReport{public string file,sha256;public int passed;}
        [Serializable]sealed class Report
        {
            public string status="running",startedUtc,finishedUtc,appVersion,unityVersion,sourceFingerprint,sourceFingerprintAfter,testReceipt,testReceiptSha256,error;
            public string scope="Four controlled age-IV industrial visual previews on Winding; I–V age cap, no AI, no resource/health/entity grants. Not normal-opening, competitive, balance or native rendering evidence.";
            public List<Preview> previews=new List<Preview>();
        }
        [Serializable]sealed class Preview
        {
            public string status="running",civilization,appVersion,sourceFingerprint,saveFile,saveSha256,rulesHash,matchId,artilleryDefinition,error;
            public string scope="Controlled age-IV visual fixture: explicit age/XP inputs, normal paid harbor and real shipment/deployment/construction/production. No AI, resource grants, health grants or direct entity spawns; not normal-match or native screenshot evidence.";
            public int controlledAge=4,maxAge=5,town,dock,artillery,startingPopulation,shipmentArrivals,productionEvents;
            public long saveBytes;public float controlledXp,worldTime,cameraSize=15;public bool paused=true;
            public int[] automatedPlayers=Array.Empty<int>();public Vector2 cameraFocus,artilleryPosition;
            public Cost startingResources,endingResources,spent,passiveIncome;public FactoryRecord[] factories;
            public List<Deployment> deployments=new List<Deployment>();
        }
        [Serializable]sealed class FactoryRecord
        {
            public int entity;public string mode;public Vector2 position;public float artilleryElapsed,construction;
            public FactoryRecord(EntityState factory){entity=factory.id;mode=factory.factoryMode;position=factory.position;artilleryElapsed=factory.factoryElapsed;construction=factory.construction;}
        }
        [Serializable]sealed class Deployment{public string card;public int wagon;public Vector2 wagonStart,site,arrivalPosition;public float foundationTime;}
    }
}
