using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Controlled fixtures exercise real commands/economy and the native controller's
    // presentation dispatch. They are not ordinary-opening or listening acceptance.
    public sealed class ActivityAudioTests
    {
        private GameObject root;
        private GameController game;
        private World W=>game.World;
        private EventAudio Audio=>game.Audio;
        private static readonly MethodInfo Update=typeof(GameController).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
        [UnitySetUp] public IEnumerator SetUp()
        {
            root=new GameObject("Activity audio fixture");game=root.AddComponent<GameController>();
            yield return null;game.StartMatch();game.enabled=false;game.Controls.enabled=false;game.Autoplay=true;game.SimulationSpeed=0;
            foreach(var entity in W.Entities.Where(e=>(e.def.tags&Tags.Guardian)!=0).ToArray())W.Remove(entity);
            Audio.ResetMatch();
        }
        [UnityTearDown] public IEnumerator TearDown(){Object.Destroy(root);yield return null;}
        private void Pump(){Update.Invoke(game,null);}
        private void Step(int count=1){for(int i=0;i<count;i++){W.Tick(.1f);Pump();}}
        private void Order(EntityState worker,OrderKind kind,int target=0,Vector2 point=default)
        {
            var r=W.Issue(new GameCommand{player=worker.owner,kind=CommandKind.Order,actors=new[]{worker.id},order=kind,target=target,point=point});
            Assert.IsTrue(r.accepted,r.reason);
        }

        [UnityTest] public IEnumerator RealMovementProducesStepsAndStoppedUnitIsSilent()
        {
            var unit=W.Spawn("villager",0,new Vector2(34,36));var start=unit.position;
            Order(unit,OrderKind.Move,point:new Vector2(44,36));Step(12);
            Assert.Greater(Vector2.Distance(start,unit.position),2);
            Assert.Greater(Audio.PlayedCount,0,"Actual visible movement needs a footstep cue");
            Assert.AreEqual("Footstep",Audio.LastCategory.ToString());
            Order(unit,OrderKind.Stop);Audio.ResetMatch();Step(12);
            Assert.AreEqual(0,Audio.PlayedCount,"A stopped unit must not keep a footstep loop running");
            yield return null;
        }

        [UnityTest] public IEnumerator RealWoodIncomeProducesAWorkCueAndUnfinishedSourceDoesNot()
        {
            var tree=W.Spawn("tree",-1,new Vector2(34,32));var worker=W.Spawn("villager",0,new Vector2(31.8f,32));
            W.Fog.Tick(W.Entities);float before=W.Players[0].resources.wood;Order(worker,OrderKind.Gather,tree.id);Step(2);
            Assert.Greater(W.Players[0].resources.wood,before);
            Assert.Greater(Audio.PlayedCount,0,"The work cue must accompany actual gathering income");
            Assert.AreEqual("Woodcut",Audio.LastCategory.ToString());
            Order(worker,OrderKind.Stop);
            var farm=W.Spawn("farm",0,new Vector2(42,36),false);var waiting=W.Spawn("villager",0,new Vector2(46,36));
            W.Fog.Tick(W.Entities);Order(waiting,OrderKind.Gather,farm.id);Audio.ResetMatch();Step(2);
            Assert.IsFalse(farm.Complete);Assert.AreEqual(0,Audio.PlayedCount,"An accepted Gather order on an unfinished facility is not work");
            yield return null;
        }

        [UnityTest] public IEnumerator ConstructionAndPaidRepairProduceCuesButUnpaidRepairDoesNot()
        {
            W.Players[0].resources=new Cost(1000,1000,1000);
            var worker=W.Spawn("villager",0,new Vector2(33,28));
            var r=W.Issue(new GameCommand{player=0,kind=CommandKind.Build,actors=new[]{worker.id},item="house",point=new Vector2(36,28)});
            Assert.IsTrue(r.accepted,r.reason);var foundation=W.Get(worker.orders.Peek().target);float progress=foundation.construction;
            Step(3);Assert.Greater(foundation.construction,progress);Assert.Greater(W.Players[0].spent.wood,0);
            Assert.Greater(Audio.PlayedCount,0);Assert.AreEqual("Build",Audio.LastCategory.ToString());
            Order(worker,OrderKind.Stop);
            var target=W.Spawn("barracks",0,new Vector2(45,40));target.hp-=100;
            var repairer=W.Spawn("villager",0,target.position+Vector2.right*(target.def.width*.5f+W.Rules.Entity("villager").radius+.75f));
            game.Controls.Focus(target.position);W.Fog.Tick(W.Entities);Order(repairer,OrderKind.Repair,target.id);
            Audio.ResetMatch();float hp=target.hp,wood=W.Players[0].resources.wood;Step(2);
            Assert.Greater(target.hp,hp);Assert.Less(W.Players[0].resources.wood,wood);
            Assert.Greater(Audio.PlayedCount,0);Assert.AreEqual("Build",Audio.LastCategory.ToString());
            W.Players[0].resources=new Cost();Audio.ResetMatch();hp=target.hp;Step(12);
            Assert.AreEqual(hp,target.hp);Assert.AreEqual("Repair needs resources",repairer.status);
            Assert.AreEqual(0,Audio.PlayedCount,"Unaffordable repair must not sound like a hammer doing real work");
            yield return null;
        }

        [UnityTest] public IEnumerator HiddenOffscreenAndPausedWorkCannotProduceOrReplayCues()
        {
            var tree=W.Spawn("tree",-1,new Vector2(112,110));var worker=W.Spawn("villager",1,new Vector2(109.8f,110));
            W.Fog.Tick(W.Entities);Order(worker,OrderKind.Gather,tree.id);Step(2);
            Assert.Greater(W.Players[1].gathered.wood,0);Assert.IsFalse(W.IsVisible(0,worker));Assert.AreEqual(0,Audio.PlayedCount);
            W.Fog.Reveal(0,worker.position,5);game.Controls.Focus(worker.position);Pump();
            Assert.AreEqual(0,Audio.PlayedCount,"Revealing a worker later cannot replay past activity from the same simulation tick");
            Step(2);Assert.Greater(Audio.PlayedCount,0,"New work while visible may be heard");
            game.Controls.Focus(new Vector2(20,20));Audio.ResetMatch();Step(2);Assert.AreEqual(0,Audio.PlayedCount,"Activity outside the viewport must remain silent");
            game.Controls.Focus(worker.position);W.Paused=true;Audio.ResetMatch();Step(5);Assert.AreEqual(0,Audio.PlayedCount);
            yield return null;
        }

        [UnityTest] public IEnumerator MutingDuringPauseSilencesExistingSourcesAndSurvivesRestart()
        {
            var shooter=W.Entities.First(e=>e.owner==0&&(e.def.tags&Tags.Explorer)!=0);
            W.Emit("Fire",0,shooter.id,"Melee");Audio.OnEvent(W.Events.Last());Assert.AreEqual(1,Audio.PlayedCount);
            Audio.PauseAll(true);game.Muted=true;
            var sources=Audio.GetComponentsInChildren<AudioSource>();
            Assert.IsTrue(sources.All(s=>s.mute),"The SOUND OFF setting must apply to existing voices, including paused ones");
            Audio.PauseAll(false);Assert.IsTrue(sources.All(s=>s.mute));
            game.StartMatch();Assert.AreEqual(0,Audio.PlayedCount);Assert.AreEqual(12,Audio.SourceCount);Assert.IsTrue(sources.All(s=>s.mute));
            game.Muted=false;Assert.IsTrue(sources.All(s=>!s.mute));Assert.AreEqual(0,Audio.PlayedCount,"Unmuting must not replay an old cue");
            yield return null;
        }
    }
}
