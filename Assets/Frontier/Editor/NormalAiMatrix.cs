using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Frontier.Editor
{
    // Evidence orchestration only. StrategyAudit uses the same MatchSimulation
    // scheduler as native play; this tool never alters rules or world state.
    public static class NormalAiMatrix
    {
        const int Seed=314159;
        const float Limit=2400;
        static readonly string[] EnvironmentKeys={"FRONTIER_REPORT_PATH","FRONTIER_AUDIT_CIV0","FRONTIER_AUDIT_CIV1","FRONTIER_AUDIT_DECK0","FRONTIER_AUDIT_DECK1","FRONTIER_AUDIT_SEED","FRONTIER_AUDIT_LIMIT","FRONTIER_AUDIT_DIFFICULTY"};

        public static void Run()=>Execute(false);
        public static void RunPilot()=>Execute(true);

        static void Execute(bool pilot)
        {
            var previous=EnvironmentKeys.ToDictionary(key=>key,Environment.GetEnvironmentVariable);
            string project=Path.GetDirectoryName(Application.dataPath);
            string directory=Path.GetFullPath(previous["FRONTIER_REPORT_PATH"]??Path.Combine(project,"Builds",pilot?"NormalAiPilot":"NormalAiMatrix"));
            // Never overwrite a timed-out, failed or previously completed run.
            if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any())
                throw new IOException("Use a new empty evidence directory: "+directory);
            Directory.CreateDirectory(directory);
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for compilation and asset import to finish before starting evidence");
            var rules=Catalog.Load();
            var all=Cases(rules);
            var manifest=new Matrix
            {
                startedUtc=DateTime.UtcNow.ToString("o"),appVersion=Application.version,unityVersion=Application.unityVersion,
                platform=Application.platform.ToString(),pilot=pilot,plannedMatrixCases=all.Length,
                seed=Seed,maxAge=5,simulationLimitSeconds=Limit,stepSeconds=MatchSimulation.StepSeconds,
                cases=pilot?all.Take(1).ToArray():all,sourcesBefore=Sources(project),
                runtimeAssembly=CompiledAssembly(typeof(World).Assembly),editorAssembly=CompiledAssembly(typeof(StrategyAudit).Assembly)
            };
            manifest.sourceFingerprintBefore=Fingerprint(manifest.sourcesBefore);
            manifest.testEvidence=RequireCurrentTests(project,manifest.sourceFingerprintBefore);
            File.WriteAllText(Path.Combine(directory,"catalog-loaded.json"),JsonUtility.ToJson(rules,true));
            manifest.loadedCatalog=Identify(Path.Combine(directory,"catalog-loaded.json"));
            Save(directory,manifest);
            try
            {
                foreach(var item in manifest.cases)
                {
                    var output=Path.Combine(directory,item.id);Directory.CreateDirectory(output);
                    item.status="running";item.startedUtc=DateTime.UtcNow.ToString("o");
                    File.WriteAllText(Path.Combine(output,"case.json"),JsonUtility.ToJson(item,true));Save(directory,manifest);
                    var elapsed=System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        Environment.SetEnvironmentVariable("FRONTIER_REPORT_PATH",output);
                        Environment.SetEnvironmentVariable("FRONTIER_AUDIT_CIV0",item.civilization0);
                        Environment.SetEnvironmentVariable("FRONTIER_AUDIT_CIV1",item.civilization1);
                        Environment.SetEnvironmentVariable("FRONTIER_AUDIT_DECK0",item.deckPreset0);
                        Environment.SetEnvironmentVariable("FRONTIER_AUDIT_DECK1",item.deckPreset1);
                        Environment.SetEnvironmentVariable("FRONTIER_AUDIT_DIFFICULTY",item.difficulty);
                        Environment.SetEnvironmentVariable("FRONTIER_AUDIT_SEED",Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        Environment.SetEnvironmentVariable("FRONTIER_AUDIT_LIMIT",Limit.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        StrategyAudit.Run();
                        item.status="exported"; // Independent event audit decides end/timeout/failure.
                    }
                    catch(Exception error)
                    {
                        item.status="exception";item.error=error.ToString();
                        File.WriteAllText(Path.Combine(output,"exception.txt"),item.error);
                        Debug.LogError("NORMAL_AI_MATRIX "+item.id+": "+error);
                    }
                    finally
                    {
                        elapsed.Stop();item.wallSeconds=elapsed.Elapsed.TotalSeconds;item.finishedUtc=DateTime.UtcNow.ToString("o");
                        File.WriteAllText(Path.Combine(output,"case.json"),JsonUtility.ToJson(item,true));Save(directory,manifest);
                    }
                }
            }
            finally
            {
                foreach(var pair in previous)Environment.SetEnvironmentVariable(pair.Key,pair.Value);
                manifest.finishedUtc=DateTime.UtcNow.ToString("o");manifest.sourcesAfter=Sources(project);
                manifest.sourceFingerprintAfter=Fingerprint(manifest.sourcesAfter);
                manifest.sourcesStable=manifest.sourceFingerprintBefore==manifest.sourceFingerprintAfter;
                manifest.status=!manifest.sourcesStable?"source_drift":manifest.cases.Any(c=>c.status=="exception")?"exports_with_exceptions":"exports_completed";
                Save(directory,manifest);
            }
            if(!manifest.sourcesStable)throw new InvalidOperationException("Source changed during AI matrix; evidence is marked source_drift and must not pass");
            if(manifest.cases.Any(c=>c.status=="exception"))throw new InvalidOperationException("AI matrix retained exceptions; inspect matrix.json before retrying in a new directory");
            Debug.Log("NORMAL_AI_MATRIX "+manifest.cases.Length+" exports in "+directory+"; run independent event audit before interpreting results");
        }

        static Case[] Cases(RuleCatalog rules)
        {
            var cases=new List<Case>();
            foreach(string difficulty in new[]{"easy","standard","hard"})
            {
                AiDifficultyProfile.From(rules,difficulty);
                Add("cross-forward","coast","economy","river","military");
                Add("cross-reverse","river","military","coast","economy");
                Add("coast-mirror","coast","economy","coast","economy");
                Add("river-mirror","river","military","river","military");
                void Add(string pairing,string civ0,string deck0,string civ1,string deck1)
                {
                    var cards0=DeckLibrary.Preset(rules,civ0,deck0).ToArray();var cards1=DeckLibrary.Preset(rules,civ1,deck1).ToArray();
                    string problem0=DeckLibrary.Validate(rules,civ0,cards0),problem1=DeckLibrary.Validate(rules,civ1,cards1);
                    if(problem0!=""||problem1!="")throw new InvalidOperationException("Invalid normal preset: "+problem0+" / "+problem1);
                    cases.Add(new Case{id=difficulty+"-"+pairing,pairing=pairing,difficulty=difficulty,
                        civilization0=civ0,civilization1=civ1,deckPreset0=deck0,deckPreset1=deck1,deck0=cards0,deck1=cards1,
                        startingResources0=rules.Civ(civ0).startingResources,startingResources1=rules.Civ(civ1).startingResources});
                }
            }
            return cases.ToArray();
        }

        static Source[] Sources(string project)
        {
            // Same coverage as Tools/verification_evidence.py, including new,
            // untracked files. Hidden paths and __pycache__ are excluded there.
            var files=new List<Source>();
            foreach(string directory in new[]{"Assets","Packages","ProjectSettings","Tools"})
            foreach(string path in Directory.GetFiles(Path.Combine(project,directory),"*",SearchOption.AllDirectories))
            {
                string relative=path.Substring(project.Length+1).Replace(Path.DirectorySeparatorChar,'/');
                if(relative.Split('/').Any(part=>part.StartsWith(".",StringComparison.Ordinal)||part=="__pycache__"))continue;
                var source=Identify(path);source.relativePath=relative;files.Add(source);
            }
            return files.OrderBy(s=>s.relativePath,StringComparer.Ordinal).ToArray();
        }

        static string Fingerprint(Source[] sources)
        {
            string json="{"+string.Join(",",sources.Select(s=>Quote(s.relativePath)+":"+Quote(s.sha256)))+"}";
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-","").ToLowerInvariant();
        }
        static string Quote(string value)
        {
            var text=new StringBuilder("\"");
            foreach(char ch in value)
            {
                if(ch=='"'||ch=='\\')text.Append('\\').Append(ch);
                else if(ch=='\b')text.Append("\\b");else if(ch=='\f')text.Append("\\f");else if(ch=='\n')text.Append("\\n");
                else if(ch=='\r')text.Append("\\r");else if(ch=='\t')text.Append("\\t");
                else if(ch<32||ch>126)text.Append("\\u").Append(((int)ch).ToString("x4"));else text.Append(ch);
            }
            return text.Append('"').ToString();
        }

        static CompiledSource CompiledAssembly(System.Reflection.Assembly loaded)
        {
            var assembly=CompilationPipeline.GetAssemblies(AssembliesType.Editor).Single(a=>a.name==loaded.GetName().Name);
            string output=Path.GetFullPath(assembly.outputPath),location=Path.GetFullPath(loaded.Location);
            var newest=assembly.sourceFiles.Select(path=>File.GetLastWriteTimeUtc(Path.GetFullPath(path))).Max();
            DateTime compiled=File.GetLastWriteTimeUtc(output);
            if(output!=location||!File.Exists(output)||compiled<newest)
                throw new InvalidOperationException("Compiled assembly is older than current source or differs from pipeline output: "+assembly.name);
            return new CompiledSource{file=Identify(output),loadedModuleVersionId=loaded.ManifestModule.ModuleVersionId.ToString(),
                currentByPipelineAndSourceTime=true,sourceFiles=assembly.sourceFiles,compiledUtc=compiled.ToString("o"),newestSourceUtc=newest.ToString("o")};
        }

        static Source RequireCurrentTests(string project,string fingerprint)
        {
            string reports=Environment.GetEnvironmentVariable("FRONTIER_VERIFIED_TEST_REPORTS");
            if(string.IsNullOrEmpty(reports))reports=Path.Combine(project,"Builds/Tests",File.ReadAllText(Path.Combine(project,"Builds/Tests/latest-run.txt")).Trim());
            string receipt=Path.Combine(reports,"source-evidence.json");
            var evidence=JsonUtility.FromJson<TestEvidence>(File.ReadAllText(receipt));
            if(evidence==null||evidence.sourceFingerprint!=fingerprint)throw new InvalidOperationException("Fresh passing tests must be bound to current source before AI matrix: "+receipt);
            Check(evidence.reports.EditMode,"EditMode.xml");Check(evidence.reports.PlayMode,"PlayMode.xml");
            return Identify(receipt);
            void Check(TestReport report,string name)
            {
                string path=Path.Combine(reports,name);var xml=new XmlDocument();xml.Load(path);var run=xml.DocumentElement;
                if(report==null||report.file!=name||report.passed<=0||Identify(path).sha256!=report.sha256||run.Name!="test-run"||run.GetAttribute("result")!="Passed"||run.GetAttribute("failed")!="0"||run.GetAttribute("passed")!=report.passed.ToString()||run.GetAttribute("total")!=report.passed.ToString())
                    throw new InvalidOperationException("Current test receipt is not a complete passing run: "+path);
            }
        }

        static Source Identify(string path)
        {
            if(string.IsNullOrEmpty(path)||!File.Exists(path))return new Source{path=path??"",sha256="",missing=true};
            using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))
                return new Source{path=Path.GetFullPath(path),sha256=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()};
        }

        static void Save(string directory,Matrix matrix)=>File.WriteAllText(Path.Combine(directory,"matrix.json"),JsonUtility.ToJson(matrix,true));
        [Serializable]sealed class Source{public string path,sha256,relativePath="";public bool missing;}
        [Serializable]sealed class CompiledSource{public Source file;public string loadedModuleVersionId,compiledUtc,newestSourceUtc;public string[] sourceFiles;public bool currentByPipelineAndSourceTime;}
        [Serializable]sealed class TestEvidence{public string sourceFingerprint;public TestReports reports;}
        [Serializable]sealed class TestReports{public TestReport EditMode,PlayMode;}
        [Serializable]sealed class TestReport{public string file,sha256;public int passed;}
        [Serializable]sealed class Case
        {
            public string id,pairing,difficulty,civilization0,civilization1,deckPreset0,deckPreset1;
            public string[] deck0,deck1;public Cost startingResources0,startingResources1;
            public string status="pending",startedUtc="",finishedUtc="",error="";public double wallSeconds;
        }
        [Serializable]sealed class Matrix
        {
            public string scope="Headless normal-rule AI regression; not native player, visual, audio, performance or statistical balance acceptance";
            public string map="Fixed authored land map; seed labels do not create independent map samples";
            public string startedUtc,finishedUtc="",appVersion,unityVersion,platform,status="running",sourceFingerprintBefore,sourceFingerprintAfter;
            public bool pilot,sourcesStable;public int plannedMatrixCases,seed,maxAge;public float simulationLimitSeconds,stepSeconds;
            public Case[] cases;public Source[] sourcesBefore,sourcesAfter;public Source loadedCatalog,testEvidence;public CompiledSource runtimeAssembly,editorAssembly;
        }
    }
}
