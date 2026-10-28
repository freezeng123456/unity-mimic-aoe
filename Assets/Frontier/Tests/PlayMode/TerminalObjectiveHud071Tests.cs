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
    public sealed class TerminalObjectiveHud071Tests
    {
        const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        GameObject root;GameController game;string directory;
        [UnitySetUp] public IEnumerator SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"frontier-terminal-hud071-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            root=new GameObject("Isolated terminal objective HUD");game=root.AddComponent<GameController>();
            SetField("dataDirectoryOverride",directory);SetField("saveDirectoryOverride",Path.Combine(directory,"Saves"));
            SetField("reportDirectoryOverride",Path.Combine(directory,"Reports"));SetField("replayDirectoryOverride",Path.Combine(directory,"Replays"));
            yield return null;game.enabled=false;
            var preferences=game.Settings.Current;preferences.muted=true;preferences.musicEnabled=false;
            Assert.IsTrue(game.Settings.TryUpdate(preferences,out string reason),reason);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(game!=null)
            {
                game.enabled=false;if(game.World!=null)game.ReturnToMenu();
                Invoke("FinishReplayFileWrites");Invoke("PollReplayOperations");Assert.IsFalse(game.ReplaySaving);
            }
            if(root!=null)UnityEngine.Object.Destroy(root);yield return null;
            if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
        }
        void SetField(string name,object value)=>typeof(GameController).GetField(name,PrivateInstance).SetValue(game,value);
        void Invoke(string name)=>typeof(GameController).GetMethod(name,PrivateInstance).Invoke(game,null);
        Text ObjectiveText()=>root.GetComponentsInChildren<Text>().Single(t=>t.text.StartsWith("FINAL TICK GOAL ·",StringComparison.Ordinal));
        int Idle()=>game.World.Entities.Count(e=>e.Alive&&e.owner==game.ObserverPlayer&&e.Worker&&e.garrisonedIn==0&&e.orders.Count==0);
        ScenarioDocument Document(bool fail)
        {
            return new ScenarioDocument{id="terminal-hud071",name="Terminal HUD fixture",enableElimination=false,rulesHash=SnapshotCodec.RulesHash(game.Rules),
                starts=new[]{new ScenarioStart{player=0,team=0,civilization="coast",town=new Vector2(24,28),explorer=new Vector2(31,32),workers=new[]{new Vector2(29,25)}},
                    new ScenarioStart{player=1,team=1,civilization="france",town=new Vector2(104,100),explorer=new Vector2(97,96),workers=new[]{new Vector2(99,103)}}},
                objectives=new[]{new ScenarioObjective{id="final-goal",title="FINAL TICK GOAL",deadline=fail?.5f:0}},
                triggers=fail?Array.Empty<ScenarioTrigger>():new[]{new ScenarioTrigger{id="finish",conditions=new[]{new ScenarioCondition{time=.5f}},
                    actions=new[]{new ScenarioAction{kind="complete_objective",objectiveId="final-goal"}}}}};
        }
        IEnumerator CheckTerminal(bool fail)
        {
            Assert.IsTrue(game.OpenScenarioDocument(Document(fail)),game.Notice);Assert.IsTrue(game.PlaytestScenario(),game.Notice);
            for(int i=0;i<4;i++)Assert.IsTrue(game.Simulation.Step());yield return null;
            var text=ObjectiveText();Assert.That(text.text,Does.Contain("ACTIVE"));int seconds=(int)game.World.Time,idle=Idle();
            Assert.IsTrue(game.Simulation.Step());Assert.IsTrue(game.World.IsMatchEnded);
            Assert.AreEqual(seconds,(int)game.World.Time,"The terminal tick must not reach a new displayed clock second");Assert.AreEqual(idle,Idle());
            Assert.AreEqual(fail?"failed":"completed",game.World.ScenarioObjectiveStates.Single().status);
            // The real result dialog can open in the controller's Update before
            // the HUD gets its next Update. Check its background synchronously.
            game.Hud.ShowResult();Assert.AreSame(text,ObjectiveText());
            Assert.That(text.text,Does.Contain(fail?"FAILED":"COMPLETED"));Assert.That(text.text,Does.Not.Contain("ACTIVE"));
            yield return null;Assert.That(ObjectiveText().text,Does.Contain(fail?"FAILED":"COMPLETED"));
        }
        [UnityTest] public IEnumerator CompletionDialogRefreshesObjectiveOnTheSameFinalTickWithinTheSameClockSecond()
        {yield return CheckTerminal(false);}
        [UnityTest] public IEnumerator DeadlineFailureDialogRefreshesObjectiveOnTheSameFinalTickWithinTheSameClockSecond()
        {yield return CheckTerminal(true);}
    }
}
