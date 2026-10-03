using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    // Four-civilization normal-rule simulation. This does not certify visuals,
    // native interaction, performance or statistical balance. Every unsuccessful
    // or timed-out case is preserved and listed, never silently retried in place.
    public static class ContentExpansionAudit
    {
        static readonly string[] Keys={"FRONTIER_REPORT_PATH","FRONTIER_AUDIT_CIV0","FRONTIER_AUDIT_CIV1","FRONTIER_AUDIT_DECK0","FRONTIER_AUDIT_DECK1","FRONTIER_AUDIT_SEED","FRONTIER_AUDIT_LIMIT","FRONTIER_AUDIT_DIFFICULTY"};
        public static void Run()=>Execute(false);
        public static void RunPilot()=>Execute(true);
        static void Execute(bool pilot)
        {
            var prior=Keys.ToDictionary(k=>k,Environment.GetEnvironmentVariable);
            string project=Path.GetDirectoryName(Application.dataPath);
            string directory=Path.GetFullPath(prior["FRONTIER_REPORT_PATH"]??Path.Combine(project,"Builds","ContentExpansion"));
            if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any())throw new IOException("Choose a new empty evidence directory: "+directory);
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)throw new InvalidOperationException("Wait for compilation and import before audit");
            Directory.CreateDirectory(directory);
            float limit=3600;if(!string.IsNullOrEmpty(prior["FRONTIER_AUDIT_LIMIT"]))limit=float.Parse(prior["FRONTIER_AUDIT_LIMIT"],CultureInfo.InvariantCulture);
            if(float.IsNaN(limit)||float.IsInfinity(limit)||limit<=0)throw new ArgumentOutOfRangeException("FRONTIER_AUDIT_LIMIT");
            var rules=Catalog.Load();var cases=new List<Case>();
            string[] civs={"coast","river","wayfarer","highland"};
            foreach(var civ in civs)if(rules.Civ(civ)==null)throw new InvalidOperationException("Missing civilization: "+civ);
            foreach(var a in civs)foreach(var b in civs)
            {
                if(pilot&&!(a=="wayfarer"&&b=="highland"||a=="highland"&&b=="wayfarer"))continue;
                var deck0=DeckLibrary.Preset(rules,a,"economy").ToArray();var deck1=DeckLibrary.Preset(rules,b,"military").ToArray();
                if(DeckLibrary.Validate(rules,a,deck0)!=""||DeckLibrary.Validate(rules,b,deck1)!="")throw new InvalidOperationException("Invalid authored deck for "+a+" / "+b);
                cases.Add(new Case{id=a+"-vs-"+b,civilization0=a,civilization1=b,deck0=deck0,deck1=deck1});
            }
            var manifest=new Matrix{pilot=pilot,appVersion=Application.version,unityVersion=Application.unityVersion,startedUtc=DateTime.UtcNow.ToString("o"),limit=limit,cases=cases.ToArray(),sourcesBefore=Sources(project)};
            manifest.sourceFingerprintBefore=Fingerprint(manifest.sourcesBefore);
            manifest.testReceipt=RequireCurrentTests(project,manifest.sourceFingerprintBefore);
            File.WriteAllText(Path.Combine(directory,"catalog-loaded.json"),JsonUtility.ToJson(rules,true));
            manifest.catalogSha256=Hash(Path.Combine(directory,"catalog-loaded.json"));Save(directory,manifest);
            try
            {
                foreach(var item in manifest.cases)
                {
                    string output=Path.Combine(directory,item.id);Directory.CreateDirectory(output);item.status="running";item.startedUtc=DateTime.UtcNow.ToString("o");Save(directory,manifest);
                    var watch=System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        Environment.SetEnvironmentVariable("FRONTIER_REPORT_PATH",output);
                        Environment.SetEnvironmentVariable("FRONTIER_AUDIT_CIV0",item.civilization0);Environment.SetEnvironmentVariable("FRONTIER_AUDIT_CIV1",item.civilization1);
                        Environment.SetEnvironmentVariable("FRONTIER_AUDIT_DECK0","economy");Environment.SetEnvironmentVariable("FRONTIER_AUDIT_DECK1","military");
                        Environment.SetEnvironmentVariable("FRONTIER_AUDIT_DIFFICULTY","standard");Environment.SetEnvironmentVariable("FRONTIER_AUDIT_SEED","314159");
                        Environment.SetEnvironmentVariable("FRONTIER_AUDIT_LIMIT",limit.ToString(CultureInfo.InvariantCulture));
                        StrategyAudit.Run();
                        item.result=JsonUtility.FromJson<Result>(File.ReadAllText(Path.Combine(output,"result.json")));
                        item.observations=ReadEvents(Path.Combine(output,"events.jsonl"),rules,item);
                        item.status=item.result.winner>=0&&item.observations.naturalEnds==1&&item.observations.resigns==0?"natural_end":"incomplete_or_abnormal_end";
                        item.files=Directory.GetFiles(output).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>new Source{path=Path.GetFileName(p),sha256=Hash(p)}).ToArray();
                    }
                    catch(Exception error){item.status="exception";item.error=error.ToString();File.WriteAllText(Path.Combine(output,"exception.txt"),item.error);Debug.LogError(error);}
                    finally{watch.Stop();item.wallSeconds=watch.Elapsed.TotalSeconds;item.finishedUtc=DateTime.UtcNow.ToString("o");File.WriteAllText(Path.Combine(output,"case.json"),JsonUtility.ToJson(item,true));Save(directory,manifest);}
                }
            }
            finally
            {
                foreach(var entry in prior)Environment.SetEnvironmentVariable(entry.Key,entry.Value);
                manifest.finishedUtc=DateTime.UtcNow.ToString("o");manifest.sourcesAfter=Sources(project);manifest.sourceFingerprintAfter=Fingerprint(manifest.sourcesAfter);
                manifest.sourcesStable=manifest.sourceFingerprintBefore==manifest.sourceFingerprintAfter;
                manifest.status=!manifest.sourcesStable?"source_drift":cases.All(c=>c.status=="natural_end")?"all_naturally_ended":"retained_incomplete_cases";
                Save(directory,manifest);
            }
            if(!manifest.sourcesStable)throw new InvalidOperationException("Sources changed during audit; evidence marked source_drift");
            if(cases.Any(c=>c.status=="exception"))throw new InvalidOperationException("Exceptions retained in expansion audit");
            Debug.Log("CONTENT_EXPANSION_AUDIT "+manifest.status+"; mechanic coverage in matrix.json; native content review remains separate");
        }
        static Observation ReadEvents(string path,RuleCatalog rules,Case item)
        {
            var result=new Observation{players=new[]{new PlayerObservation{civilization=item.civilization0},new PlayerObservation{civilization=item.civilization1}}};
            var counts=new Dictionary<string,int>();var entityTypes=new Dictionary<int,string>();
            foreach(var line in File.ReadLines(path))
            {
                var e=JsonUtility.FromJson<SimEvent>(line);if(e==null||string.IsNullOrEmpty(e.kind))throw new InvalidDataException("Malformed event in "+path);
                counts.TryGetValue(e.kind,out int n);counts[e.kind]=n+1;result.events++;
                if(e.kind=="MatchEnded")result.naturalEnds++;if(e.kind=="Resign")result.resigns++;
                if(e.kind=="Spawn")
                {
                    var definition=Array.Find(rules.entities,d=>d.name==e.message);if(definition!=null)entityTypes[e.entity]=definition.id;
                }
                if(e.player<0||e.player>1)continue;var p=result.players[e.player];
                if(e.kind=="Payment")
                {
                    p.payments++;
                    if(e.hasEconomy&&e.economy!=null&&e.economy.hasRepair&&entityTypes.TryGetValue(e.entity,out string repairer)&&repairer=="field_engineer")
                    {p.engineerPaidRepairs++;p.repairedHp+=e.economy.hpAfter-e.economy.hpBefore;}
                }
                if(e.kind=="AgeRouteChosen")p.ageRoutes.Add(e.message);
                if(e.kind=="Spawn"&&entityTypes.TryGetValue(e.entity,out string spawned)&&rules.Entity(spawned).civilization==p.civilization)p.specialistSpawns++;
                if(e.kind=="Damage"&&entityTypes.TryGetValue(e.entity,out string damaging)&&rules.Entity(damaging).civilization==p.civilization){p.specialistDamageEvents++;p.specialistDamage+=e.amount;}
                if(e.kind=="CaravanRoute")p.caravanRoutes++;
                if(e.kind=="CaravanLoaded")p.paidCargoLoads++;
                if(e.kind=="CaravanDelivered"){p.deliveries++;p.deliveredCoin+=e.amount;}
                if(e.kind=="CaravanCargoLost")p.lostCargo++;
                if(e.kind=="ManufactureQueued")p.manufactureQueued++;
                if(e.kind=="ManufactureComplete")p.manufactureCompleted++;
                if(e.kind=="PrefabUsed")p.prefabsUsed++;
                if(e.kind=="ShieldStance")p.shieldChanges++;
                if(e.kind=="Command"&&e.message.StartsWith("Manufacture",StringComparison.Ordinal))p.artisanAssignments++;
                if(e.kind=="Command"&&e.message.StartsWith("Repair",StringComparison.Ordinal)&&entityTypes.TryGetValue(e.entity,out string actor)&&actor=="field_engineer")p.engineerRepairOrders++;
                if(e.kind=="ProductionQueued")
                {
                    string id=e.message.Split(' ')[0];var definition=rules.Entity(id);
                    if(definition!=null&&definition.civilization==p.civilization)p.specialistUnitsQueued+=(int)e.amount;
                }
            }
            result.eventKinds=counts.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new Count{kind=pair.Key,count=pair.Value}).ToArray();
            return result;
        }
        static Source[] Sources(string project)
        {
            return new[]{"Assets","Packages","ProjectSettings","Tools"}.SelectMany(dir=>Directory.GetFiles(Path.Combine(project,dir),"*",SearchOption.AllDirectories))
                .Select(p=>new{full=p,relative=p.Substring(project.Length+1).Replace(Path.DirectorySeparatorChar,'/')})
                .Where(p=>!p.relative.Split('/').Any(x=>x.StartsWith(".",StringComparison.Ordinal)||x=="__pycache__"))
                .OrderBy(p=>p.relative,StringComparer.Ordinal).Select(p=>new Source{path=p.relative,sha256=Hash(p.full)}).ToArray();
        }
        static string Fingerprint(Source[] entries)
        {
            string json="{"+string.Join(",",entries.Select(e=>Quote(e.path)+":"+Quote(e.sha256)))+"}";
            using(var sha=SHA256.Create())return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(json)));
        }
        static string Quote(string input)
        {
            var b=new StringBuilder("\"");foreach(char c in input)
            {if(c=='"'||c=='\\')b.Append('\\').Append(c);else if(c<32||c>126)b.Append("\\u").Append(((int)c).ToString("x4"));else b.Append(c);}return b.Append('"').ToString();
        }
        static Source RequireCurrentTests(string project,string fingerprint)
        {
            string directory=Environment.GetEnvironmentVariable("FRONTIER_VERIFIED_TEST_REPORTS");
            if(string.IsNullOrEmpty(directory))directory=Path.Combine(project,"Builds/Tests",File.ReadAllText(Path.Combine(project,"Builds/Tests/latest-run.txt")).Trim());
            string receipt=Path.Combine(directory,"source-evidence.json");var evidence=JsonUtility.FromJson<TestEvidence>(File.ReadAllText(receipt));
            if(evidence.sourceFingerprint!=fingerprint)throw new InvalidOperationException("Passing tests must bind current expansion source: "+receipt);
            Check(evidence.reports.EditMode,"EditMode.xml");Check(evidence.reports.PlayMode,"PlayMode.xml");return new Source{path=receipt,sha256=Hash(receipt)};
            void Check(TestReport report,string name)
            {
                var xml=new XmlDocument();string path=Path.Combine(directory,name);xml.Load(path);var run=xml.DocumentElement;
                if(report==null||report.file!=name||report.passed<=0||Hash(path)!=report.sha256||run.Name!="test-run"||run.GetAttribute("result")!="Passed"||run.GetAttribute("failed")!="0"||run.GetAttribute("passed")!=report.passed.ToString()||run.GetAttribute("total")!=report.passed.ToString())throw new InvalidOperationException("Tests are not a complete passing run: "+path);
            }
        }
        static string Hex(byte[] bytes)=>BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
        static string Hash(string path){using(var sha=SHA256.Create())using(var file=File.OpenRead(path))return Hex(sha.ComputeHash(file));}
        static void Save(string directory,Matrix data)=>File.WriteAllText(Path.Combine(directory,"matrix.json"),JsonUtility.ToJson(data,true));
        [Serializable]sealed class Matrix
        {
            public string scope="Normal-rule AI strategy and content coverage; not native UI, visuals, audio, performance or statistical balance";
            public string map="Fixed authored map; seed is reproducibility, not a multi-map sample";
            public string status="running",appVersion,unityVersion,startedUtc,finishedUtc,sourceFingerprintBefore,sourceFingerprintAfter,catalogSha256;
            public bool pilot,sourcesStable;public int seed=314159,maxAge=5;public float limit;public string difficulty="standard";
            public Source[] sourcesBefore,sourcesAfter;public Source testReceipt;public Case[] cases;
        }
        [Serializable]sealed class Case{public string id,civilization0,civilization1,status="pending",startedUtc,finishedUtc,error;public string[] deck0,deck1;public double wallSeconds;public Result result;public Observation observations;public Source[] files;}
        [Serializable]sealed class Result{public int winner,age0,age1,events;public float seconds;public string civilization0,civilization1;}
        [Serializable]sealed class Observation{public int events,naturalEnds,resigns;public Count[] eventKinds;public PlayerObservation[] players;}
        [Serializable]sealed class PlayerObservation{public string civilization;public int payments,caravanRoutes,paidCargoLoads,deliveries,lostCargo,manufactureQueued,manufactureCompleted,prefabsUsed,shieldChanges,artisanAssignments,engineerRepairOrders,engineerPaidRepairs,specialistUnitsQueued,specialistSpawns,specialistDamageEvents;public float deliveredCoin,repairedHp,specialistDamage;public List<string> ageRoutes=new List<string>();}
        [Serializable]sealed class Count{public string kind;public int count;}
        [Serializable]sealed class Source{public string path,sha256;}
        [Serializable]sealed class TestEvidence{public string sourceFingerprint;public TestReports reports;}
        [Serializable]sealed class TestReports{public TestReport EditMode,PlayMode;}
        [Serializable]sealed class TestReport{public string file,sha256;public int passed;}
    }
}
