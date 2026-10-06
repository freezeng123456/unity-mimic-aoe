using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ScenarioGroups070Tests
    {
        static ScenarioDocument Land()
        {
            var rules=Catalog.Load();return new ScenarioDocument{id="groups-070",name="Initial force fixture",rulesHash=SnapshotCodec.RulesHash(rules),enableElimination=false,starts=new[]{
                new ScenarioStart{player=0,team=0,civilization="coast",town=new Vector2(24,28),explorer=new Vector2(31,32),workers=new[]{new Vector2(29,25)}},
                new ScenarioStart{player=1,team=1,civilization="france",town=new Vector2(104,100),explorer=new Vector2(97,96),workers=new[]{new Vector2(99,103)}}},objects=new[]{
                new ScenarioPlacement{id="soldier-a",definition="musketeer",owner=0,position=new Vector2(40,40)},
                new ScenarioPlacement{id="soldier-b",definition="musketeer",owner=0,position=new Vector2(40,44)},
                new ScenarioPlacement{id="outsider",definition="musketeer",owner=0,position=new Vector2(44,48)},
                new ScenarioPlacement{id="enemy",definition="musketeer",owner=1,position=new Vector2(90,80)}},
                groups=new[]{new ScenarioGroup{id="landing-force",objectIds=new[]{"soldier-a","soldier-b"}}}};
        }
        static ScenarioDocument River()
        {
            var d=Land();d.rivers=new[]{new ScenarioRiver{id="channel",points=new[]{new Vector2(64,-8),new Vector2(64,136)},halfWidths=new[]{8f,8f},smooth=false}};
            d.objects=new[]{
                new ScenarioPlacement{id="soldier-a",definition="musketeer",owner=0,position=new Vector2(51,30)},
                new ScenarioPlacement{id="soldier-b",definition="musketeer",owner=0,position=new Vector2(51,34)},
                new ScenarioPlacement{id="outsider",definition="musketeer",owner=0,position=new Vector2(51,38)},
                new ScenarioPlacement{id="cargo-worker",definition="villager",owner=0,position=new Vector2(51,42)},
                new ScenarioPlacement{id="transport",definition="transport_ship",owner=0,position=new Vector2(64,30)},
                new ScenarioPlacement{id="other-transport",definition="transport_ship",owner=0,position=new Vector2(64,80)}};
            d.objectives=new[]{new ScenarioObjective{id="board-force",title="Board the designated force",kind="embark_group",objectId="landing-force",targetId="transport",player=0,amount=2,required=false}};
            d.triggers=new[]{new ScenarioTrigger{id="boarding-reward",conditions=new[]{new ScenarioCondition{kind="units_embarked",objectId="landing-force",targetId="transport",player=0,amount=2}},
                actions=new[]{new ScenarioAction{kind="grant_resource",player=0,resource=ResourceKind.Food,amount=25}}}};
            return d;
        }
        static World Play(ScenarioDocument document)=>new World(Catalog.Load(),document,document.starts.Select(s=>s.civilization).ToArray(),document.starts.Select(s=>s.team).ToArray(),5);
        static void Until(World world,Func<bool> condition,float seconds,string reason)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds*10)&&!condition()&&!world.IsMatchEnded;i++)world.Tick(.1f);
            Assert.IsTrue(condition(),reason+"; time="+world.Time+"; "+string.Join("; ",world.Entities.Where(e=>e.Alive&&e.owner==0).Select(e=>e.def.id+" "+e.position+" "+e.status+" cargo="+e.garrisonedIn)));
        }
        static void Board(World world,string ship,Vector2 shore,params string[] passengers)
        {
            int transport=world.ScenarioEntity(ship).id;int[] actors=passengers.Select(id=>world.ScenarioEntity(id).id).ToArray();
            var result=world.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=actors,target=transport,point=shore});Assert.IsTrue(result.accepted,result.reason);
            Until(world,()=>actors.All(id=>world.Get(id).garrisonedIn==transport),120,"Specified units must actually walk to a berth and board");world.Tick(.1f);
        }
        static string LegacyPayload(ScenarioDocument d)
        {
            Assert.IsEmpty(d.groups);string current=JsonUtility.ToJson(d);
            // 069 objectives/conditions lacked targetId; 069 actions already
            // had it. Remove only the two original insertion positions.
            return current.Replace(",\"groups\":[]","").Replace(",\"targetId\":\"\",\"regionId\":",",\"regionId\":");
        }
        static string Envelope(string payload)=>JsonUtility.ToJson(new ScenarioEnvelope{payload=payload,sha256=SnapshotCodec.Hash(payload)});

        [Test] public void CompleteLegacyPortableShapeChecksTheOriginalHashBeforeAddingEmptyFields()
        {
            var d=Land();d.groups=Array.Empty<ScenarioGroup>();d.objectives=new[]{new ScenarioObjective{id="old-goal",kind="manual"}};
            d.triggers=new[]{new ScenarioTrigger{id="old-trigger",conditions=new[]{new ScenarioCondition{kind="time",time=20}},actions=new[]{new ScenarioAction{kind="message",message="legacy"}}}};
            d.description="The words groups and targetId inside prose do not select a JSON version.";
            string payload=LegacyPayload(d),file=Envelope(payload);Assert.IsFalse(payload.Contains("\"groups\":"));
            Assert.That(payload,Does.Contain("\"targetId\":\"\",\"message\""),"Legacy action fields must remain complete");
            var loaded=ScenarioCodec.Decode(file,Catalog.Load());Assert.IsEmpty(loaded.groups);Assert.AreEqual("",loaded.objectives[0].targetId);Assert.AreEqual("",loaded.triggers[0].conditions[0].targetId);
            Assert.AreEqual(d.description,loaded.description);Assert.AreEqual(d.objects[0].position,loaded.objects[0].position);
            string rewritten=ScenarioCodec.Encode(loaded,Catalog.Load());Assert.That(rewritten,Does.Contain("groups"));
            Assert.AreEqual(ScenarioCodec.ContentHash(loaded),ScenarioCodec.ContentHash(ScenarioCodec.Decode(rewritten,Catalog.Load())));
            Assert.AreNotEqual(SnapshotCodec.Hash(payload),ScenarioCodec.ContentHash(loaded),"Migration returns current owned data; it does not pretend the upgraded DTO has the old hash");
            var corrupt=JsonUtility.FromJson<ScenarioEnvelope>(file);corrupt.payload=payload.Replace("legacy","changed");
            Assert.Throws<InvalidDataException>(()=>ScenarioCodec.Decode(JsonUtility.ToJson(corrupt),Catalog.Load()));
        }
        [Test] public void MissingOldFieldsMixedShapesAndUnknownFieldsAreNotLegacyCompatibility()
        {
            var d=Land();d.groups=Array.Empty<ScenarioGroup>();d.objectives=new[]{new ScenarioObjective{id="old-goal"}};
            d.triggers=new[]{new ScenarioTrigger{id="old-trigger",conditions=new[]{new ScenarioCondition{kind="time"}},actions=new[]{new ScenarioAction{kind="message",message="legacy"}}}};
            string old=LegacyPayload(d);
            int amount=old.IndexOf("\"amount\":",StringComparison.Ordinal)+"\"amount\":".Length,end=old.IndexOf(',',amount);
            string coerced=old.Substring(0,amount)+"\"1\""+old.Substring(end);
            foreach(string invalid in new[]{old.Replace("\"enableElimination\":false,",""),old.Replace("\"objectId\":\"\",\"regionId\":","\"objectId\":\"\",\"targetId\":\"\",\"regionId\":"),old.Replace("\"schema\":1","\"schema\":1,\"unknown\":0"),coerced})
                Assert.Throws<InvalidDataException>(()=>ScenarioCodec.Decode(Envelope(invalid),Catalog.Load()),invalid.Substring(0,Math.Min(160,invalid.Length)));
        }
        [Test] public void FailedLegacyImportPreservesItsOriginalBytesAndAnExistingValidLibraryRow()
        {
            string directory=Path.Combine(Path.GetTempPath(),"frontier-groups-070-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            try
            {
                var store=new ScenarioStore(directory,Catalog.Load());var d=Land();string existing=store.Write(d);byte[] previous=File.ReadAllBytes(existing);
                d.groups=Array.Empty<ScenarioGroup>();string payload=LegacyPayload(d);var envelope=new ScenarioEnvelope{payload=payload,sha256=new string('0',64)};
                string imported=Path.Combine(directory,"bad-import.scenario.json");File.WriteAllText(imported,JsonUtility.ToJson(envelope));byte[] bad=File.ReadAllBytes(imported);
                Assert.Throws<InvalidDataException>(()=>store.ReadFile(imported));CollectionAssert.AreEqual(bad,File.ReadAllBytes(imported));CollectionAssert.AreEqual(previous,File.ReadAllBytes(existing));
                Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp-*"));
            }
            finally{Directory.Delete(directory,true);}
        }
        [TestCase("missing")][TestCase("start-0-town")][TestCase("depot")][TestCase("berries")][TestCase("landing-force")]
        public void GroupsRejectUnknownNestedBuildingAndResourceReferences(string member)
        {
            var d=Land();d.objects=d.objects.Concat(new[]{new ScenarioPlacement{id="depot",definition="house",owner=0,position=new Vector2(44,60)}}).ToArray();
            d.resources=new[]{new ScenarioPlacement{id="berries",definition="berry",position=new Vector2(60,60)}};
            d.groups[0].objectIds=new[]{member};Assert.IsNotEmpty(ScenarioCodec.Validate(d,Catalog.Load()));
        }
        [Test] public void GroupIdentifiersAndMemberListsAreUniqueAndBounded()
        {
            var d=Land();d.groups[0].objectIds=new[]{"soldier-a","soldier-a"};Assert.IsNotEmpty(ScenarioCodec.Validate(d,Catalog.Load()));
            d=Land();d.groups[0].id="soldier-a";Assert.IsNotEmpty(ScenarioCodec.Validate(d,Catalog.Load()));
            d=Land();d.groups=new[]{d.groups[0],ScenarioCodec.Clone(d).groups[0]};Assert.IsNotEmpty(ScenarioCodec.Validate(d,Catalog.Load()));
            d=Land();d.groups[0].objectIds=Array.Empty<string>();Assert.IsNotEmpty(ScenarioCodec.Validate(d,Catalog.Load()));
            d=Land();d.groups=Enumerable.Range(0,64).Select(i=>new ScenarioGroup{id="group-"+i,objectIds=new[]{"soldier-a"}}).ToArray();Assert.IsEmpty(ScenarioCodec.Validate(d,Catalog.Load()));
            d.groups=d.groups.Concat(new[]{new ScenarioGroup{id="group-64",objectIds=new[]{"soldier-b"}}}).ToArray();Assert.IsNotEmpty(ScenarioCodec.Validate(d,Catalog.Load()));
            d=Land();d.objects=Enumerable.Range(0,129).Select(i=>new ScenarioPlacement{id="member-"+i,definition="musketeer",owner=0,position=new Vector2(40,40)}).ToArray();
            d.groups[0].objectIds=d.objects.Take(128).Select(e=>e.id).ToArray();Assert.IsEmpty(ScenarioCodec.Validate(d,Catalog.Load()));
            d.groups[0].objectIds=d.objects.Select(e=>e.id).ToArray();Assert.IsNotEmpty(ScenarioCodec.Validate(d,Catalog.Load()));
        }
        [Test] public void ExplorerWorkersAndTreasureGuardsCanBeInitialGroupMembers()
        {
            var d=Land();d.treasures=new[]{new ScenarioTreasure{id="camp",definition=0,position=new Vector2(60,70),guardPositions=new[]{new Vector2(64,70)}}};
            d.groups=new[]{new ScenarioGroup{id="initial-units",objectIds=new[]{"start-0-explorer","start-0-worker-0","camp-guard-0","soldier-a"}}};
            Assert.IsEmpty(ScenarioCodec.Validate(d,Catalog.Load()));var world=Play(d);
            CollectionAssert.AreEqual(d.groups[0].objectIds.Select(id=>world.ScenarioEntity(id).id).ToArray(),world.ScenarioEntities("initial-units"));Assert.DoesNotThrow(()=>World.Restore(world.Rules,world.CaptureState()));
        }
        [Test] public void InitialGroupsCannotReferToFutureSpawnGroups()
        {
            var d=Land();d.triggers=new[]{new ScenarioTrigger{id="future",conditions=new[]{new ScenarioCondition{kind="time",time=20}},actions=new[]{new ScenarioAction{kind="spawn",definition="musketeer",player=0,count=2,point=new Vector2(55,30),objectId="future-force"}}}};
            d.groups[0].objectIds=new[]{"future-force"};Assert.IsNotEmpty(ScenarioCodec.Validate(d,Catalog.Load()));
        }
        [Test] public void AggregateBindingsAliasOnlyTheirExactOrderedInitialMembersAndReturnedArraysAreOwned()
        {
            var d=Land();d.groups=d.groups.Concat(new[]{new ScenarioGroup{id="rear-guard",objectIds=new[]{"soldier-b"}}}).ToArray();var world=Play(d);
            int[] ids=world.ScenarioEntities("landing-force");CollectionAssert.AreEqual(new[]{world.ScenarioEntity("soldier-a").id,world.ScenarioEntity("soldier-b").id},ids);
            ids[0]=999999;Assert.AreEqual(world.ScenarioEntity("soldier-a").id,world.ScenarioEntities("landing-force")[0]);Assert.IsEmpty(world.ScenarioEntities("missing"));
            string original=JsonUtility.ToJson(world.CaptureState());
            foreach(Action<ScenarioBindingState> corrupt in new Action<ScenarioBindingState>[] {b=>Array.Reverse(b.entityIds),b=>b.entityIds[1]=b.entityIds[0],b=>b.entityIds[0]=world.ScenarioEntity("outsider").id,b=>b.entityIds=new[]{b.entityIds[0]},b=>b.entityIds[0]=999999})
            {
                var saved=world.CaptureState();corrupt(Array.Find(saved.scenarioState.bindings,b=>b.objectId=="landing-force"));
                Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,saved));Assert.AreEqual(original,JsonUtility.ToJson(world.CaptureState()));
            }
            var missing=world.CaptureState();missing.scenarioState.bindings=missing.scenarioState.bindings.Where(b=>b.objectId!="rear-guard").ToArray();Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,missing));
            var cold=World.Restore(world.Rules,world.CaptureState());CollectionAssert.AreEqual(world.ScenarioEntities("landing-force"),cold.ScenarioEntities("landing-force"));Assert.AreEqual(original,JsonUtility.ToJson(cold.CaptureState()));
        }
        [TestCase("cargo-worker")][TestCase("transport")]
        public void EmbarkGoalsCannotUseWorkersOrShipsAsTheirSpecifiedMilitaryGroup(string member)
        {
            var d=River();d.groups[0].objectIds=new[]{member};d.objectives[0].amount=1;d.triggers[0].conditions[0].amount=1;
            Assert.IsNotEmpty(ScenarioCodec.Validate(d,Catalog.Load()));
        }
        [TestCase("soldier-a","hussar")][TestCase("start-0-explorer","musketeer")][TestCase("camp-guard-0","musketeer")]
        public void AColdGroupMemberCannotKeepItsIdAndOriginButChangeItsAuthoredDefinition(string member,string replacement)
        {
            var d=Land();d.treasures=new[]{new ScenarioTreasure{id="camp",definition=0,position=new Vector2(60,70),guardPositions=new[]{new Vector2(64,70)}}};
            d.groups=d.groups.Concat(new[]{new ScenarioGroup{id="role-check",objectIds=new[]{"soldier-a","start-0-explorer","camp-guard-0"}}}).ToArray();
            var world=Play(d);var saved=world.CaptureState();int id=world.ScenarioEntity(member).id;
            var row=Array.Find(saved.entities,e=>e.state.id==id);Assert.IsNull(row.state.def,"The cold JSON has no runtime EntityDef reference");
            row.definition=replacement;string before=JsonUtility.ToJson(saved);
            var failure=Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,saved));Assert.That(failure.Message,Does.Contain("initial binding definition "+member));
            Assert.AreEqual(before,JsonUtility.ToJson(saved),"Validation must not hydrate or mutate the supplied cold DTO");Assert.AreEqual(member=="soldier-a"?"musketeer":member=="start-0-explorer"?"explorer":"guardian",world.ScenarioEntity(member).def.id);
        }
        [Test] public void StartingWorkerRolesUseTheActualOverriddenCivilizationAndNotTheEditableDocumentRoster()
        {
            var d=Land();d.groups=d.groups.Concat(new[]{new ScenarioGroup{id="opening-party",objectIds=new[]{"start-0-worker-0","start-0-explorer"}}}).ToArray();
            var rules=Catalog.Load();var world=new World(rules,d,new[]{"france","coast"},new[]{0,1},5);Assert.AreEqual("coast",d.starts[0].civilization);
            Assert.AreEqual(rules.Civ("france").workerUnit,world.ScenarioEntity("start-0-worker-0").def.id);Assert.AreNotEqual(rules.Civ("coast").workerUnit,rules.Civ("france").workerUnit);
            var saved=world.CaptureState();var restored=World.Restore(rules,saved);Assert.AreEqual(JsonUtility.ToJson(saved),JsonUtility.ToJson(restored.CaptureState()));
            var wrong=world.CaptureState();Array.Find(wrong.entities,e=>e.state.id==world.ScenarioEntity("start-0-worker-0").id).definition=rules.Civ("coast").workerUnit;
            var failure=Assert.Throws<ArgumentException>(()=>World.Restore(rules,wrong));Assert.That(failure.Message,Does.Contain("initial binding definition start-0-worker-0"));
        }
        [Test] public void EmbarkGoalsRequireOneRealTransportAndPossibleIntegerThresholds()
        {
            foreach(Action<ScenarioDocument> change in new Action<ScenarioDocument>[] {d=>d.objectives[0].targetId="landing-force",d=>d.objectives[0].targetId="soldier-a",d=>d.objectives[0].objectId="soldier-a",d=>d.objectives[0].amount=.5f,d=>d.objectives[0].amount=3,d=>d.triggers[0].conditions[0].amount=1.5f,d=>d.triggers[0].conditions[0].targetId=""})
            {var d=River();change(d);Assert.IsNotEmpty(ScenarioCodec.Validate(d,Catalog.Load()));}
        }
        [Test] public void OnlyTheSpecifiedLivingSoldiersInsideTheSpecifiedShipCompleteBoardingAfterColdRestore()
        {
            var world=Play(River());Board(world,"transport",new Vector2(52,30),"outsider","cargo-worker");
            Assert.AreEqual(0,world.ScenarioObjectiveStates[0].progress);Assert.AreEqual(0,world.ScenarioState.triggerStates[0].fires);
            Board(world,"other-transport",new Vector2(52,80),"soldier-a");Assert.AreEqual(0,world.ScenarioObjectiveStates[0].progress,"A specified soldier aboard another transport cannot count");
            Board(world,"transport",new Vector2(52,30),"soldier-b");Assert.AreEqual(1,world.ScenarioObjectiveStates[0].progress);Assert.AreEqual("active",world.ScenarioObjectiveStates[0].status);
            var saved=world.CaptureState();var cold=World.Restore(world.Rules,saved);Assert.AreEqual(JsonUtility.ToJson(saved),JsonUtility.ToJson(cold.CaptureState()));
            int other=cold.ScenarioEntity("other-transport").id;var unload=cold.Issue(new GameCommand{player=0,kind=CommandKind.Disembark,actors=new[]{other},point=new Vector2(52,80)});Assert.IsTrue(unload.accepted,unload.reason);
            Until(cold,()=>cold.ScenarioEntity("soldier-a").garrisonedIn==0,90,"The other ship must physically unload its soldier");
            Board(cold,"transport",new Vector2(52,30),"soldier-a");Assert.AreEqual("completed",cold.ScenarioObjectiveStates[0].status);Assert.AreEqual(2,cold.ScenarioObjectiveStates[0].progress);
            Assert.AreEqual(1,cold.ScenarioState.triggerStates[0].fires);Assert.AreEqual(1,cold.Events.Count(e=>e.kind=="ScenarioResourceGranted"));Assert.AreEqual(325,cold.Players[0].resources.food,.001f);
            var completed=World.Restore(cold.Rules,cold.CaptureState());for(int i=0;i<20;i++)completed.Tick(.1f);
            Assert.AreEqual(1,completed.Events.Count(e=>e.kind=="ScenarioResourceGranted"));Assert.AreEqual(1,completed.ScenarioState.triggerStates[0].fires);
        }
        [Test] public void AForeignPlayersEmbarkObjectiveDoesNotCountTheOtherFactionCargo()
        {
            var d=River();d.objectives[0].player=1;d.triggers[0].conditions[0].player=1;var world=Play(d);
            Board(world,"transport",new Vector2(52,30),"soldier-a","soldier-b");Assert.AreEqual(0,world.ScenarioObjectiveStates[0].progress);Assert.AreEqual("active",world.ScenarioObjectiveStates[0].status);Assert.AreEqual(0,world.ScenarioState.triggerStates[0].fires);
        }
        [Test] public void SinkingTheTransportRemovesCargoAndOnlySpecifiedActualLossesTriggerFailure()
        {
            var d=River();d.objectives=new[]{new ScenarioObjective{id="survive",title="Protect the landing force",kind="manual"}};
            d.triggers=new[]{new ScenarioTrigger{id="insufficient-force",conditions=new[]{new ScenarioCondition{kind="units_lost",objectId="landing-force",player=0,amount=2}},actions=new[]{new ScenarioAction{kind="fail_objective",objectiveId="survive"}}}};
            var world=Play(d);world.Remove(world.ScenarioEntity("outsider"),1);world.Remove(world.ScenarioEntity("soldier-a"),1);world.Tick(.1f);
            Assert.AreEqual(0,world.ScenarioState.triggerStates[0].fires);Assert.IsFalse(world.IsMatchEnded,"An outsider loss cannot complete the two-member threshold");
            var cold=World.Restore(world.Rules,world.CaptureState());Board(cold,"transport",new Vector2(52,30),"soldier-b","cargo-worker");
            cold.Remove(cold.ScenarioEntity("transport"),1);cold.Tick(.1f);Assert.IsFalse(cold.ScenarioEntity("soldier-b").Alive);Assert.IsFalse(cold.ScenarioEntity("cargo-worker").Alive);
            Assert.AreEqual(1,cold.ScenarioState.triggerStates[0].fires);Assert.AreEqual("failed",cold.ScenarioObjectiveStates[0].status);Assert.AreEqual(1,cold.Winner);
            int events=cold.Events.Count;var ended=World.Restore(cold.Rules,cold.CaptureState());ended.Tick(.1f);Assert.AreEqual(events,ended.Events.Count);
        }
        [Test] public void EmptyFutureGroupsDoNotCountAsLossesAndTheirRealDeathsDo()
        {
            var d=Land();d.triggers=new[]{
                new ScenarioTrigger{id="loss-notice",conditions=new[]{new ScenarioCondition{kind="units_lost",objectId="future-force",player=0,amount=1}},actions=new[]{new ScenarioAction{kind="message",message="A real reinforcement was lost"}}},
                new ScenarioTrigger{id="future-spawn",conditions=new[]{new ScenarioCondition{kind="time",time=1}},actions=new[]{new ScenarioAction{kind="spawn",definition="musketeer",player=0,count=2,point=new Vector2(55,30),objectId="future-force"}}}};
            var world=Play(d);world.Tick(.1f);Assert.IsEmpty(world.ScenarioEntities("future-force"));Assert.AreEqual(0,world.ScenarioState.triggerStates[0].fires);
            var cold=World.Restore(world.Rules,world.CaptureState());Until(cold,()=>cold.ScenarioEntities("future-force").Length==2,3,"The real spawn must occur before a loss can count");
            Assert.AreEqual(0,cold.ScenarioState.triggerStates[0].fires);cold.Remove(cold.Get(cold.ScenarioEntities("future-force")[0]),1);cold.Tick(.1f);
            Assert.AreEqual(1,cold.ScenarioState.triggerStates[0].fires);Assert.AreEqual(1,cold.Events.Count(e=>e.kind=="ScenarioMessage"&&e.message=="A real reinforcement was lost"));
        }
        [TestCase(0)][TestCase(.5f)][TestCase(4097)]
        public void LossThresholdsMustBePositiveBoundedIntegers(float amount)
        {
            var d=Land();d.triggers=new[]{new ScenarioTrigger{id="loss",conditions=new[]{new ScenarioCondition{kind="units_lost",objectId="landing-force",amount=amount}},actions=new[]{new ScenarioAction{kind="message",message="loss"}}}};
            Assert.IsNotEmpty(ScenarioCodec.Validate(d,Catalog.Load()));
        }
        [Test] public void AttackMoveUsesNormalOwnedGroupCommandsAndColdRestoreDoesNotReissueTheTrigger()
        {
            var d=Land();d.triggers=new[]{
                new ScenarioTrigger{id="hidden-direct-attack",conditions=new[]{new ScenarioCondition{kind="time",time=.1f}},actions=new[]{new ScenarioAction{kind="order_attack",player=0,objectId="landing-force",targetId="enemy"}}},
                new ScenarioTrigger{id="advance",conditions=new[]{new ScenarioCondition{kind="time",time=.2f}},actions=new[]{new ScenarioAction{kind="order_attack_move",player=0,objectId="landing-force",point=new Vector2(70,60)}}}};
            var world=Play(d);Assert.IsFalse(world.IsVisible(0,world.ScenarioEntity("enemy")));world.Tick(.1f);
            Assert.That(world.ScenarioState.triggerStates[0].lastError,Does.Contain("not currently visible"));world.Tick(.1f);
            Assert.AreEqual("",world.ScenarioState.triggerStates[1].lastError);Assert.AreEqual(1,world.Events.Count(e=>e.kind=="Command"&&e.player==0&&e.message=="AttackMove ×2"));
            foreach(int id in world.ScenarioEntities("landing-force")){Assert.AreEqual(OrderKind.AttackMove,world.Get(id).orders.Peek().kind);Assert.AreEqual(0,world.Get(id).attackTarget);}
            var saved=world.CaptureState();var cold=World.Restore(world.Rules,saved);Assert.AreEqual(JsonUtility.ToJson(saved),JsonUtility.ToJson(cold.CaptureState()));Vector2 before=cold.ScenarioEntity("soldier-a").position;
            for(int i=0;i<20;i++)cold.Tick(.1f);Assert.Greater(Vector2.Distance(before,cold.ScenarioEntity("soldier-a").position),1);
            Assert.AreEqual(1,cold.Events.Count(e=>e.kind=="Command"&&e.message=="AttackMove ×2"));Assert.AreEqual(1,cold.ScenarioState.triggerStates[1].fires);Assert.IsFalse(cold.IsVisible(0,cold.ScenarioEntity("enemy")));
        }
    }
}
