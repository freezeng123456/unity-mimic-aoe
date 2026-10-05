using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ScenarioDocument069Tests
    {
        RuleCatalog rules;string directory;
        [SetUp] public void SetUp()
        {rules=Catalog.Load();directory=Path.Combine(Path.GetTempPath(),"frontier-scenario069-"+Guid.NewGuid().ToString("N"));}
        [TearDown] public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        ScenarioDocument Draft()
        {
            return new ScenarioDocument{id="test-scenario",name="可编辑河谷",rulesHash=SnapshotCodec.RulesHash(rules),starts=new[]{
                new ScenarioStart{player=0,team=0,town=new Vector2(20,20),explorer=new Vector2(27,24),workers=new[]{new Vector2(25,17)}},
                new ScenarioStart{player=1,team=1,town=new Vector2(108,108),explorer=new Vector2(101,104),workers=new[]{new Vector2(103,111)}}}};
        }
        ScenarioDocument Full()
        {
            var d=Draft();d.heightsCm[5+9*129]=360;d.surfaces[7+8*128]=3;
            d.rivers=new[]{new ScenarioRiver{id="bend",points=new[]{new Vector2(64,-5),new Vector2(72,50),new Vector2(45,80),new Vector2(90,65),new Vector2(64,136)},halfWidths=new[]{4f,7,6,8,9}}};
            d.bridges=new[]{new ScenarioBridge{id="crossing",center=new Vector2(66,30),direction=Vector2.right}};
            d.resources=new[]{new ScenarioPlacement{id="ore",definition="mine",position=new Vector2(31,28),remaining=777}};
            d.objects=new[]{new ScenarioPlacement{id="guarded-tower",definition="tower",position=new Vector2(85,84),owner=1}};
            d.tradeRoute=new[]{new Vector2(10,70),new Vector2(30,70),new Vector2(100,70)};
            d.tradeSockets=new[]{new ScenarioTradeSocket{id="settlement",position=new Vector2(30,70),waypoint=1}};
            d.neutralBindings=new[]{new ScenarioNeutralBinding{definition=rules.neutralAllies[0].id,socketId="settlement"}};
            d.treasures=new[]{new ScenarioTreasure{id="camp",definition=0,position=new Vector2(83,35),guardPositions=new[]{new Vector2(86,37)}}};
            d.regions=new[]{new ScenarioRegion{id="hill",center=new Vector2(80,85),size=new Vector2(10,12)}};
            d.objectives=new[]{new ScenarioObjective{id="hold",title="占领高地",kind="hold_region",regionId="hill",holdSeconds=12},
                new ScenarioObjective{id="finish",title="战胜守军",kind="destroy_object",objectId="guarded-tower",prerequisites=new[]{"hold"}}};
            d.triggers=new[]{new ScenarioTrigger{id="reinforce",conditions=new[]{new ScenarioCondition{kind="time",time=15}},actions=new[]{
                new ScenarioAction{kind="spawn",definition="musketeer",objectId="reinforcements",player=0,count=3,point=new Vector2(33,35)},
                new ScenarioAction{kind="order_attack",objectId="reinforcements",targetId="guarded-tower"}}},
                new ScenarioTrigger{id="reward",conditions=new[]{new ScenarioCondition{kind="object_dead",objectId="reinforcements"}},actions=new[]{new ScenarioAction{kind="grant_resource",resource=ResourceKind.Coin,amount=100}}}};
            return d;
        }
        [Test] public void FullDocumentRoundtripRetainsTerrainFreeRiverPlayerEconomyAndStableReferences()
        {
            var d=Full();d.starts[0].civilization="france";d.starts[0].age=3;d.starts[0].initialResources=new Cost(234,567,890);
            var decoded=ScenarioCodec.Decode(ScenarioCodec.Encode(d,rules),rules);
            Assert.AreEqual(ScenarioCodec.ContentHash(d),ScenarioCodec.ContentHash(decoded));Assert.AreEqual(360,decoded.heightsCm[5+9*129]);
            Assert.AreEqual(3,decoded.surfaces[7+8*128]);Assert.AreEqual(new Vector2(90,65),decoded.rivers[0].points[3]);
            Assert.AreEqual("france",decoded.starts[0].civilization);Assert.AreEqual(3,decoded.starts[0].age);Assert.AreEqual(890,decoded.starts[0].initialResources.coin);
            Assert.AreEqual(777,decoded.resources[0].remaining);Assert.AreEqual("reinforcements",decoded.triggers[1].conditions[0].objectId);
            Assert.AreEqual("settlement",decoded.neutralBindings[0].socketId);Assert.AreEqual(1,decoded.treasures[0].guardPositions.Length);
        }
        [TestCase("schema",9)] [TestCase("width",64)] [TestCase("height",256)]
        public void UnsupportedFormatAndDimensionsRejectWithoutCreatingFiles(string field,int value)
        {
            var d=Draft();typeof(ScenarioDocument).GetField(field).SetValue(d,value);Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
            Assert.Throws<ArgumentException>(()=>new ScenarioStore(directory,rules).Write(d));Assert.IsFalse(Directory.Exists(directory));
        }
        [Test] public void DocumentRulesAreBoundToTheExactCatalog()
        {var d=Draft();d.rulesHash=new string('0',64);Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));Assert.Throws<ArgumentException>(()=>ScenarioCodec.Encode(d,rules));}
        [TestCase("../escape")] [TestCase("UPPER")] [TestCase("a/b")] [TestCase("a.json")] [TestCase("")]
        public void LibraryIDsCannotEscapeTheDirectory(string id)
        {var store=new ScenarioStore(directory,rules);Assert.Throws<ArgumentException>(()=>store.Read(id));var d=Draft();d.id=id;Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));}
        [Test] public void TerrainBoundsAndArrayLengthsAreStrict()
        {
            var d=Draft();d.heightsCm[0]=10001;Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));d.heightsCm[0]=0;
            d.surfaces[0]=4;Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));d.surfaces=new int[1];Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
        }
        [Test] public void NonFiniteAndMismatchedFreeRiverControlsAreRejected()
        {
            var d=Full();d.rivers[0].halfWidths[1]=float.NaN;Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
            d.rivers[0].halfWidths[1]=7;d.rivers[0].halfWidths=new[]{4f};Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
            d=Full();d.rivers[0].points[1]=d.rivers[0].points[0];Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
        }
        [Test] public void WideEstuaryControlsAndNonMonotoneRiverAreLegitimateData()
        {var d=Full();d.rivers[0].points[4]=new Vector2(63,153);d.rivers[0].halfWidths[4]=37;Assert.AreEqual("",ScenarioCodec.Validate(d,rules));}
        [Test] public void ExplicitPlayersRequireContinuousSlotsAndLegalData()
        {
            var d=Draft();d.starts[1].player=2;Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));d.starts[1].player=1;
            d.starts[0].initialResources.food=-1;Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));d.starts[0].initialResources.food=300;
            d.starts[0].civilization="unknown";Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
        }
        [Test] public void UnknownResourcesObjectKindsAndOutOfMapCoordinatesAreRejected()
        {
            var d=Full();d.resources[0].definition="musketeer";Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
            d=Full();d.objects[0].definition="mine";Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
            d=Full();d.resources[0].position=new Vector2(-1,30);Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
        }
        [Test] public void DanglingDuplicateAndReservedStartingIDsAreRejected()
        {
            var d=Full();d.objects[0].id="ore";Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
            d=Full();d.objects[0].id=ScenarioCodec.StartTownId(0);Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
            d=Full();d.triggers[0].actions[1].targetId="missing";Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
        }
        [Test] public void ObjectivePrerequisiteCyclesReject()
        {var d=Full();d.objectives[0].prerequisites=new[]{"finish"};Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));}
        [Test] public void NeutralBindingsNeedExistingUniqueSocketsAndCatalogs()
        {
            var d=Full();d.neutralBindings[0].socketId="missing";Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
            d=Full();d.tradeSockets[0].waypoint=3;Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
            d=Full();d.neutralBindings=new[]{d.neutralBindings[0],d.neutralBindings[0]};Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
        }
        [Test] public void SpawnReferencesMayBePredeclaredButCannotShadowExistingObjects()
        {
            var d=Full();Assert.AreEqual("",ScenarioCodec.Validate(d,rules));d.triggers[0].actions[0].objectId="ore";
            Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
        }
        [Test] public void TriggerLimitsAndFiniteSpawnBudgetAreEnforced()
        {
            var d=Full();d.triggers[0].maxFires=64;d.triggers[0].actions[0].count=64;Assert.AreEqual("",ScenarioCodec.Validate(d,rules));
            d.triggers[1].actions=new[]{new ScenarioAction{kind="spawn",definition="musketeer",count=1}};Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
            d=Full();d.triggers[0].repeatSeconds=0;Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
            d=Full();d.triggers[0].actions=Enumerable.Range(0,9).Select(i=>new ScenarioAction{kind="message",message="x"}).ToArray();Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));
        }
        [TestCase("code")] [TestCase("../script")] [TestCase("reflection")] public void UnknownActionKindsAreNotExecutable(string kind)
        {var d=Full();d.triggers[0].actions[0].kind=kind;Assert.IsNotEmpty(ScenarioCodec.Validate(d,rules));}
        [Test] public void MissingUnknownDuplicateAndCoercedJSONFieldsRejectBeforeLoading()
        {
            string json=ScenarioCodec.Encode(Draft(),rules);
            Assert.Throws<InvalidDataException>(()=>ScenarioCodec.Decode(json.Replace("\"schema\":1,",""),rules));
            Assert.Throws<InvalidDataException>(()=>ScenarioCodec.Decode(json.Replace("\"schema\":1","\"schema\":1,\"schema\":1"),rules));
            Assert.Throws<InvalidDataException>(()=>ScenarioCodec.Decode(json.Replace("\"schema\":1","\"schema\":\"1\""),rules));
            Assert.Throws<InvalidDataException>(()=>ScenarioCodec.Decode(json.Replace("\"encoding\":","\"unknown\":false,\"encoding\":"),rules));
        }
        [Test] public void PayloadHashAndStrictNestedStructureAreBothRequired()
        {
            var envelope=JsonUtility.FromJson<ScenarioEnvelope>(ScenarioCodec.Encode(Draft(),rules));envelope.sha256=new string('0',64);
            Assert.Throws<InvalidDataException>(()=>ScenarioCodec.Decode(JsonUtility.ToJson(envelope),rules));
            envelope.payload=JsonUtility.ToJson(Draft()).Replace("\"workers\":", "\"unknown\":[],\"workers\":");envelope.sha256=SnapshotCodec.Hash(envelope.payload);
            Assert.Throws<InvalidDataException>(()=>ScenarioCodec.Decode(JsonUtility.ToJson(envelope),rules));
        }
        [Test] public void EncodedSizeLimitRejectsBeforeParsing()
        {Assert.Throws<InvalidDataException>(()=>ScenarioCodec.Decode(new string('x',ScenarioCodec.MaxBytes+1),rules));}
        [Test] public void TransactionCommitsOneStrokeAndOwnsBothInputAndReturnedDocument()
        {
            var original=Draft();var session=new ScenarioEditorSession(rules,original);original.name="outside";Assert.AreNotEqual("outside",session.Document.name);
            ScenarioDocument retained=null;Assert.IsTrue(session.Apply(d=>{retained=d;d.name="one stroke";d.heightsCm[0]=200;d.heightsCm[1]=300;},out var reason),reason);
            retained.name="retained mutation";var view=session.Document;view.heightsCm[0]=900;Assert.AreEqual("one stroke",session.Document.name);Assert.AreEqual(200,session.Document.heightsCm[0]);
            Assert.IsTrue(session.Undo());Assert.AreEqual(0,session.Document.heightsCm[1]);Assert.IsFalse(session.CanUndo);Assert.IsTrue(session.Redo());Assert.AreEqual(300,session.Document.heightsCm[1]);
        }
        [Test] public void FailedEditsAndExceptionsRetainDraftUndoAndRedo()
        {
            var session=new ScenarioEditorSession(rules,Draft());Assert.IsTrue(session.Apply(d=>d.name="after",out _));Assert.IsTrue(session.Undo());
            string before=ScenarioCodec.ContentHash(session.Document);Assert.IsTrue(session.CanRedo);
            Assert.IsFalse(session.Apply(d=>d.resources=new[]{new ScenarioPlacement{definition="unknown",id="bad"}},out var reason));Assert.IsNotEmpty(reason);
            Assert.AreEqual(before,ScenarioCodec.ContentHash(session.Document));Assert.IsTrue(session.CanRedo);Assert.IsFalse(session.CanUndo);
            Assert.IsFalse(session.Apply(d=>{d.name="partial";throw new Exception("edit failed");},out reason));Assert.AreEqual("edit failed",reason);
            Assert.AreEqual(before,ScenarioCodec.ContentHash(session.Document));Assert.IsTrue(session.Redo());Assert.AreEqual("after",session.Document.name);
        }
        [Test] public void NewBranchClearsRedoButNoOpPreservesItAndHistoryIsBounded()
        {
            var session=new ScenarioEditorSession(rules,Draft());session.Apply(d=>d.name="one",out _);session.Undo();session.Apply(d=>{},out _);Assert.IsTrue(session.CanRedo);
            session.Apply(d=>d.name="branch",out _);Assert.IsFalse(session.CanRedo);
            for(int i=0;i<40;i++){int value=i;session.Apply(d=>d.heightsCm[0]=value,out _);}int steps=0;while(session.Undo())steps++;
            Assert.LessOrEqual(steps,ScenarioEditorSession.MaxHistory);
        }
        [Test] public void LibraryReadWriteExportAndImportAreColdRoundtripsWithoutImplicitImportWrites()
        {
            var d=Full();var store=new ScenarioStore(directory,rules);string path=store.Write(d);var cold=new ScenarioStore(directory,rules);
            Assert.AreEqual(ScenarioCodec.ContentHash(d),ScenarioCodec.ContentHash(cold.Read(d.id)));var rows=cold.List();Assert.AreEqual(1,rows.Length);Assert.AreEqual("",rows[0].reason);
            var imported=cold.Import(cold.Export(d.id));Assert.AreEqual(ScenarioCodec.ContentHash(d),ScenarioCodec.ContentHash(imported));Assert.AreEqual(1,cold.List().Length);
            string export=Path.Combine(directory,"exported.frscenario");Assert.AreEqual(export,cold.ExportFile(export,d));Assert.AreEqual(ScenarioCodec.ContentHash(d),ScenarioCodec.ContentHash(cold.ReadFile(export)));
            Assert.AreEqual(1,cold.List().Length);Assert.IsTrue(File.Exists(path));
        }
        [Test] public void InvalidWritesAndImportsPreserveTheExistingFileAndNoTemporaryArtifacts()
        {
            var d=Draft();var store=new ScenarioStore(directory,rules);string path=store.Write(d);byte[] before=File.ReadAllBytes(path);
            d.starts[0].age=999;Assert.Throws<ArgumentException>(()=>store.Write(d));CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
            Assert.Throws<InvalidDataException>(()=>store.Import("{}"));CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
            Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp-*"));
        }
        [Test] public void ReplacementIsAtomicAndDamagedLibraryRowsRemainVisible()
        {
            var d=Draft();var store=new ScenarioStore(directory,rules);store.Write(d);d.name="replacement";store.Write(d);
            Assert.AreEqual("replacement",new ScenarioStore(directory,rules).Read(d.id).name);File.WriteAllText(Path.Combine(directory,"damaged.json"),"{}");
            var rows=store.List();Assert.AreEqual(2,rows.Length);Assert.IsNotEmpty(rows.Single(x=>x.id=="damaged").reason);Assert.AreEqual("",rows.Single(x=>x.id==d.id).reason);
        }
        [Test] public void DiskReadsRejectInvalidUTF8AndOversizedFiles()
        {
            Directory.CreateDirectory(directory);string path=Path.Combine(directory,"bad.frscenario");File.WriteAllBytes(path,new byte[]{0xc3,0x28});
            Assert.Throws<InvalidDataException>(()=>new ScenarioStore(directory,rules).ReadFile(path));
            using(var stream=new FileStream(path,FileMode.Create))stream.SetLength(ScenarioCodec.MaxBytes+1);
            Assert.Throws<InvalidDataException>(()=>new ScenarioStore(directory,rules).ReadFile(path));
        }
        [Test] public void LibraryFilenameIdentityAndFiniteCatalogAreEnforced()
        {
            Directory.CreateDirectory(directory);var store=new ScenarioStore(directory,rules);File.WriteAllText(Path.Combine(directory,"different.json"),ScenarioCodec.Encode(Draft(),rules));
            Assert.Throws<InvalidDataException>(()=>store.Read("different"));Assert.IsNotEmpty(store.List()[0].reason);
            for(int i=0;i<ScenarioStore.MaxLibraryFiles;i++)File.WriteAllText(Path.Combine(directory,"entry-"+i+".json"),"{}");
            Assert.Throws<IOException>(()=>store.List());Assert.Throws<IOException>(()=>store.Write(Draft()));
        }
        [TestCase("frontier")] [TestCase("river")] [TestCase("winding")] [TestCase("tropical")] [TestCase("tributaries")] [TestCase("estuary")]
        [TestCase("generated_river")] [TestCase("generated_delta")] [TestCase("generated_strait")]
        public void FromMapExpandsEveryBuiltinTemplateIntoOwnedEditableData(string map)
        {
            var d=ScenarioEditorSession.FromMap(rules,map,314159,2);Assert.AreEqual("",ScenarioCodec.Validate(d,rules));
            Assert.AreEqual(2,d.starts.Length);Assert.Greater(d.resources.Length,60);Assert.AreEqual(6,d.starts[0].workers.Length);
            Assert.AreEqual(3,d.tradeSockets.Length);Assert.AreEqual(rules.neutralAllies.Length,d.neutralBindings.Length);
            Assert.IsFalse(d.objects.Any(x=>x.definition=="town"||x.definition=="explorer"||x.definition=="guardian"));
            string before=ScenarioCodec.ContentHash(d);var session=new ScenarioEditorSession(rules,d);Assert.IsTrue(session.Apply(x=>x.heightsCm[0]+=100,out _));
            Assert.AreEqual(before,ScenarioCodec.ContentHash(d));Assert.AreNotEqual(before,ScenarioCodec.ContentHash(session.Document));
        }
        [Test] public void FromMapFourPlayerExpansionStoresAllActualOpeningSlots()
        {
            var d=ScenarioEditorSession.FromMap(rules,"generated_river",314159,4);Assert.AreEqual(4,d.starts.Length);Assert.AreEqual("",ScenarioCodec.Validate(d,rules));
            Assert.AreEqual(4,d.starts.Select(x=>x.town).Distinct().Count());Assert.IsTrue(d.starts.All(x=>x.workers.Length==6));Assert.Greater(d.resources.Length,160);
        }
    }
}
