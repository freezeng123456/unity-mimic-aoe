using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class NeutralSaveMigration088Tests
    {
        string directory,source,destination;
        RuleCatalog current,previous;
        SavedMatch saved;
        [SetUp] public void Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"frontier-migrate088-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            source=Path.Combine(directory,"old.json");destination=Path.Combine(directory,"new.json");
            current=Catalog.Load();previous=NeutralSaveMigration088.ReconstructPreviousRules(current);
            var world=new World(previous);var simulation=new MatchSimulation(world,1);
            simulation.Step();int worker=world.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker).id;
            saved=new SavedMatch{matchId="b3a20564b0574fc0ad61df024a62897a",appVersion="0.25.0",world=world.CaptureState(),simulation=simulation.CaptureState(),
                cameraPosition=new Vector3(11,52,33),cameraFocus=new Vector2(57,64),cameraSize=33,
                selection=new[]{worker},groups=new[]{new ControlGroupSnapshot{number=3,actors=new[]{worker}}},
                muted=true,environmentSound=false,music=false,autoplay=true,simulationSpeed=4};
            saved.world.rulesHash=NeutralSaveMigration088.LegacyRulesHash;
            MatchSaveStore.Write(source,saved);
        }
        [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        [Test] public void MigratesCopyOnlyAfterCheckingBothWorldsAndPreservesOriginalBytes()
        {
            var original=File.ReadAllBytes(source);NeutralSaveMigration088.CreateCopy(source,destination,current,"0.25.0");
            CollectionAssert.AreEqual(original,File.ReadAllBytes(source));
            var after=MatchSaveStore.Read(destination,"0.25.0");
            Assert.AreEqual(SnapshotCodec.RulesHash(current),after.world.rulesHash);
            after.world.rulesHash=NeutralSaveMigration088.LegacyRulesHash;
            Assert.AreEqual(JsonUtility.ToJson(saved),JsonUtility.ToJson(after),"Every SavedMatch field other than the already-normalized rules hash must survive");
            Assert.AreEqual(new Vector2(57,64),after.cameraFocus);Assert.AreEqual(33,after.cameraSize);
            Assert.AreEqual(3,after.groups[0].number);CollectionAssert.AreEqual(saved.selection,after.groups[0].actors);
            Assert.IsTrue(after.muted);Assert.IsFalse(after.environmentSound);Assert.IsFalse(after.music);Assert.IsTrue(after.autoplay);Assert.AreEqual(4,after.simulationSpeed);
            Assert.IsEmpty(Directory.GetFiles(directory,".neutral-migration-*"));
        }
        [Test] public void ExistingDestinationWrongVersionOrWrongLegacyHashNeverChangesFiles()
        {
            byte[] before=File.ReadAllBytes(source);File.WriteAllText(destination,"existing");
            Assert.Throws<IOException>(()=>NeutralSaveMigration088.CreateCopy(source,destination,current,"0.25.0"));
            Assert.AreEqual("existing",File.ReadAllText(destination));File.Delete(destination);
            Assert.Throws<InvalidDataException>(()=>NeutralSaveMigration088.CreateCopy(source,destination,current,"9.9.9"));
            saved.world.rulesHash=new string('0',64);MatchSaveStore.Write(source,saved);before=File.ReadAllBytes(source);
            Assert.Throws<InvalidDataException>(()=>NeutralSaveMigration088.CreateCopy(source,destination,current,"0.25.0"));
            CollectionAssert.AreEqual(before,File.ReadAllBytes(source));Assert.IsFalse(File.Exists(destination));
        }
        [Test] public void RejectsChangedPriorRuleAndInvalidOldSnapshot()
        {
            var modified=SnapshotCodec.Clone(current);modified.entities[0].hp+=1;
            Assert.Throws<InvalidDataException>(()=>NeutralSaveMigration088.CreateCopy(source,destination,modified,"0.25.0"));
            saved.world.entities[1].state.id=saved.world.entities[0].state.id;MatchSaveStore.Write(source,saved);var before=File.ReadAllBytes(source);
            Assert.Throws<ArgumentException>(()=>NeutralSaveMigration088.CreateCopy(source,destination,current,"0.25.0"));
            CollectionAssert.AreEqual(before,File.ReadAllBytes(source));Assert.IsFalse(File.Exists(destination));
        }
        [Test] public void RejectsForgedPathOrNoncanonicalMatchIdentityWithoutWritingCopy()
        {
            foreach(string id in new[]{"../unsafe",new string('a',256),"B3A20564B0574FC0AD61DF024A62897A"})
            {
                saved.matchId=id;MatchSaveStore.Write(source,saved);byte[] before=File.ReadAllBytes(source);
                Assert.Throws<InvalidDataException>(()=>NeutralSaveMigration088.CreateCopy(source,destination,current,"0.25.0"));
                CollectionAssert.AreEqual(before,File.ReadAllBytes(source));Assert.IsFalse(File.Exists(destination));
            }
        }
        [Test] public void RejectsScenarioAndChallengeWithoutWritingCopy()
        {
            saved.world.hasScenarioDocument=true;MatchSaveStore.Write(source,saved);
            Assert.Throws<InvalidDataException>(()=>NeutralSaveMigration088.CreateCopy(source,destination,current,"0.25.0"));
            saved.world.hasScenarioDocument=false;saved.guided=new GuidedPlaySnapshot{definitionId="challenge"};MatchSaveStore.Write(source,saved);
            Assert.Throws<InvalidDataException>(()=>NeutralSaveMigration088.CreateCopy(source,destination,current,"0.25.0"));
            Assert.IsFalse(File.Exists(destination));
        }
    }
}
