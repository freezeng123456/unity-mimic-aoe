using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    public sealed class NeutralSaveUi088Tests : NativeInputFixture
    {
        [UnityTest] public IEnumerator LegacyOrdinarySaveMustBeCopiedToEmptySlotBeforeLoad()
        {
            string dir=Path.Combine(Path.GetTempPath(),"Frontier-ui-migration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                typeof(GameController).GetField("saveDirectoryOverride",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,dir);
                var previous=NeutralSaveMigration088.ReconstructPreviousRules(game.Rules);
                var world=new World(previous);var sim=new MatchSimulation(world,1);sim.Step();
                var save=new SavedMatch{matchId="b3a20564b0574fc0ad61df024a62897a",appVersion=Application.version,world=world.CaptureState(),simulation=sim.CaptureState()};
                save.world.rulesHash=NeutralSaveMigration088.LegacyRulesHash;
                string source=MatchSaveStore.SlotPath(dir,1),dest=MatchSaveStore.SlotPath(dir,2),occupied=MatchSaveStore.SlotPath(dir,3);
                MatchSaveStore.Write(source,save);File.WriteAllText(occupied,"occupied");byte[] original=File.ReadAllBytes(source);
                game.ReturnToMenu();game.Hud.ShowSaveSlots(false);yield return Ready();
                var old=FindButton("SLOT 1 · OLD CHECKPOINT · COPY REQUIRED");Assert.IsTrue(old.interactable);
                yield return Click(ButtonCenter(old));
                Assert.IsFalse(FindButton("SLOT 1 · ORIGINAL").interactable);
                Assert.IsFalse(FindButton("SLOT 3 · OCCUPIED").interactable);
                yield return Click(ButtonCenter(FindButton("COPY TO SLOT 2 · EMPTY")));
                Assert.IsTrue(File.Exists(dest));CollectionAssert.AreEqual(original,File.ReadAllBytes(source));Assert.AreEqual("occupied",File.ReadAllText(occupied));
                Assert.AreEqual(SnapshotCodec.RulesHash(game.Rules),MatchSaveStore.Read(dest).world.rulesHash);
                yield return Click(ButtonCenter(FindButton("SLOT 2 ·")));
                Assert.IsFalse(game.InMenu);Assert.IsTrue(game.IsPaused);Assert.AreEqual("b3a20564b0574fc0ad61df024a62897a",game.MatchId);
            }
            finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
        [UnityTest] public IEnumerator UnsupportedLegacyScenarioIsNotOfferedAndStaleDestinationIsNeverOverwritten()
        {
            string dir=Path.Combine(Path.GetTempPath(),"Frontier-ui-migration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                typeof(GameController).GetField("saveDirectoryOverride",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,dir);
                var previous=NeutralSaveMigration088.ReconstructPreviousRules(game.Rules);var world=new World(previous);var sim=new MatchSimulation(world,1);
                var save=new SavedMatch{matchId="b3a20564b0574fc0ad61df024a62897a",appVersion=Application.version,world=world.CaptureState(),simulation=sim.CaptureState()};save.world.rulesHash=NeutralSaveMigration088.LegacyRulesHash;
                string source=MatchSaveStore.SlotPath(dir,1),dest=MatchSaveStore.SlotPath(dir,2);MatchSaveStore.Write(source,save);
                game.ReturnToMenu();game.Hud.ShowSaveSlots(false);yield return Ready();yield return Click(ButtonCenter(FindButton("SLOT 1 · OLD CHECKPOINT")));
                File.WriteAllText(dest,"occupied after menu opened");yield return Click(ButtonCenter(FindButton("COPY TO SLOT 2 · EMPTY")));
                Assert.AreEqual("occupied after menu opened",File.ReadAllText(dest));Assert.IsTrue(game.IsLegacyMatchCandidate(1));
                Assert.IsTrue(game.InMenu,"Failure must be legible from the main menu, where the game HUD notice is absent");
                var message=session.GetComponentsInChildren<Text>().Single(t=>t.name=="Checkpoint copy error");
                StringAssert.Contains("Could not copy checkpoint:",message.text);StringAssert.Contains("unused destination",message.text);
                save.world.hasScenarioDocument=true;MatchSaveStore.Write(source,save);Assert.IsFalse(game.IsLegacyMatchCandidate(1));
            }
            finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        }
    }
}
