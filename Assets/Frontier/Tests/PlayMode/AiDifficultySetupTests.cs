using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class AiDifficultySetupTests : NativeInputFixture
    {
        private IEnumerator ReturnMenu(){game.ReturnToMenu();yield return Ready();Assert.IsTrue(game.InMenu);}
        private IEnumerator CycleTo(string expected)
        {
            var button=FindButton("AI DIFFICULTY:");
            for(int i=0;i<4&&game.OpponentDifficulty!=expected;i++){yield return Click(ButtonCenter(button));button=FindButton("AI DIFFICULTY:");}
            Assert.AreEqual(expected,game.OpponentDifficulty);
        }

        [UnityTest] public IEnumerator DifficultyButtonUsesNativeInputAndStartsMatchingSnapshot()
        {
            yield return ReturnMenu();yield return CycleTo("hard");
            yield return Click(ButtonCenter(FindButton("BEGIN SKIRMISH")));yield return Ready();
            Assert.IsFalse(game.InMenu);Assert.IsNotNull(game.World);Assert.AreEqual("hard",game.OpponentDifficulty);
            Assert.AreEqual("hard",game.Simulation.Difficulty.Id);Assert.IsEmpty(game.DifficultyReason);
        }

        [UnityTest] public IEnumerator MenuSelectionSurvivesReturnAndRestart()
        {
            yield return ReturnMenu();yield return CycleTo("easy");yield return Click(ButtonCenter(FindButton("BEGIN SKIRMISH")));yield return Ready();
            Assert.AreEqual("easy",game.Simulation.Difficulty.Id);
            var oldWorld=game.World;var oldProfile=game.Simulation.Difficulty;Assert.IsNotNull(oldWorld);
            game.StartMatch();yield return Ready();Assert.AreNotSame(oldWorld,game.World);Assert.AreNotSame(oldProfile,game.Simulation.Difficulty);Assert.AreEqual("easy",game.Simulation.Difficulty.Id);
            yield return ReturnMenu();Assert.AreEqual("easy",game.OpponentDifficulty);
            yield return Click(ButtonCenter(FindButton("BEGIN SKIRMISH")));yield return Ready();Assert.AreEqual("easy",game.Simulation.Difficulty.Id);
        }

        [UnityTest] public IEnumerator SimulationDifficultySnapshotDoesNotChangeAfterMenuDefinitionMutation()
        {
            yield return ReturnMenu();yield return CycleTo("standard");yield return Click(ButtonCenter(FindButton("BEGIN SKIRMISH")));yield return Ready();
            var id=game.Simulation.Difficulty.Id;var decision=game.Simulation.Difficulty.DecisionSeconds;var army=game.Simulation.Difficulty.ArmySeconds;
            var definition=game.Rules.Difficulty("standard");
            try
            {
                definition.decisionSeconds=8;definition.armySeconds=16;
                Assert.AreEqual(decision,game.Simulation.Difficulty.DecisionSeconds);Assert.AreEqual(army,game.Simulation.Difficulty.ArmySeconds);
            }
            finally{definition.decisionSeconds=decision;definition.armySeconds=army;}
            game.OpponentDifficulty="hard";
            game.SaveReport("difficulty_profile_test");
            var path=(string)typeof(GameController).GetField("lastReport",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
            var report=JsonUtility.FromJson<CaptureMetadata>(File.ReadAllText(path));
            Assert.AreEqual("standard",report.aiDifficulty);Assert.AreEqual(decision,report.aiDecisionSeconds);Assert.AreEqual(army,report.aiArmySeconds);
            var metadata=CaptureMetadata.Read(game);Assert.AreEqual("standard",metadata.aiDifficulty);Assert.AreEqual(decision,metadata.aiDecisionSeconds);Assert.AreEqual(army,metadata.aiArmySeconds);Assert.AreEqual(id,game.Simulation.Difficulty.Id);Assert.AreEqual(decision,game.Simulation.Difficulty.DecisionSeconds);Assert.AreEqual(army,game.Simulation.Difficulty.ArmySeconds);yield return ReturnMenu();var menuMetadata=CaptureMetadata.Read(game);Assert.IsEmpty(menuMetadata.aiDifficulty);Assert.AreEqual(0,menuMetadata.aiDecisionSeconds);Assert.AreEqual(0,menuMetadata.aiArmySeconds);
        }

        [UnityTest] public IEnumerator InvalidDifficultyRejectsStartAndKeepsMenu()
        {
            yield return ReturnMenu();game.OpponentDifficulty="invalid";game.StartMatch();yield return Ready();Assert.IsTrue(game.InMenu);Assert.IsNull(game.World);Assert.IsFalse(string.IsNullOrEmpty(game.DifficultyReason));
            yield return CycleTo("standard");game.StartMatch();yield return Ready();Assert.IsNotNull(game.World);
        }
    }
}
