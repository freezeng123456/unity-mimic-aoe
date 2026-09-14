using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class ActivityAudioCoverageTests
    {
        private GameObject root;private GameController game;
        private static readonly MethodInfo Update=typeof(GameController).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
        [UnitySetUp] public IEnumerator SetUp()
        {
            root=new GameObject("Activity coverage");game=root.AddComponent<GameController>();yield return null;
            game.StartMatch();game.enabled=false;game.Controls.enabled=false;game.View.enabled=false;game.Autoplay=true;game.SimulationSpeed=0;
            foreach(var e in game.World.Entities.Where(e=>(e.def.tags&Tags.Guardian)!=0).ToArray())game.World.Remove(e);
            game.Audio.ResetMatch();
        }
        [UnityTearDown] public IEnumerator TearDown(){Object.Destroy(root);yield return null;}
        private void Step(int n=1){for(int i=0;i<n;i++){game.World.Tick(.1f);Update.Invoke(game,null);}}
        private void Order(EntityState e,OrderKind kind,int target=0,Vector2 point=default)
        {
            var r=game.World.Issue(new GameCommand{player=e.owner,kind=CommandKind.Order,actors=new[]{e.id},order=kind,target=target,point=point});Assert.IsTrue(r.accepted,r.reason);
        }
        [UnityTest] public IEnumerator MountedAndWheeledUnitsUseDistinctMotionCues()
        {
            foreach(string type in new[]{"hussar","cannon"})
            {
                var e=game.World.Spawn(type,0,new Vector2(34,36));game.Controls.Focus(e.position);game.Audio.ResetMatch();
                Order(e,OrderKind.Move,point:new Vector2(44,36));Step(14);
                Assert.Greater(game.Audio.ActivityPlayedCount,0);Assert.AreEqual(type=="hussar"?"Hoof":"Wheel",game.Audio.LastCategory.ToString());
                Order(e,OrderKind.Stop);game.World.Remove(e);yield return null;
            }
        }
        [UnityTest] public IEnumerator MaterialCuesFollowRealGatheringAndDoNotMutateIncome()
        {
            foreach(string type in new[]{"mine","berry","crate_wood"})
            {
                var resource=game.World.Spawn(type,-1,new Vector2(50,50));var worker=game.World.Spawn("villager",0,new Vector2(47.8f,50));
                game.Controls.Focus(resource.position);game.World.Fog.Tick(game.World.Entities);game.Audio.ResetMatch();Order(worker,OrderKind.Gather,resource.id);
                var before=game.World.Players[0].gathered;Step(2);var after=game.World.Players[0].gathered;
                Assert.Greater(after.food+after.wood+after.coin,before.food+before.wood+before.coin);
                Assert.Greater(game.Audio.ActivityPlayedCount,0);Assert.AreEqual(type=="mine"?"Mine":"Handling",game.Audio.LastCategory.ToString());
                var balance=game.World.Players[0].resources;int events=game.World.Events.Count;
                for(int i=0;i<20;i++)game.Audio.TickActivity();
                Assert.AreEqual(balance,game.World.Players[0].resources);Assert.AreEqual(events,game.World.Events.Count,"Rendering activity cannot produce economy or simulation events");
                Order(worker,OrderKind.Stop);game.World.Remove(worker);game.World.Remove(resource);yield return null;
            }
        }
        [UnityTest] public IEnumerator ActivityCrowdKeepsTwoSlotsAndLeavesCombatAndNoticeSlotsAvailable()
        {
            var w=game.World;var tree=w.Spawn("tree",-1,new Vector2(50,50));game.Controls.Focus(tree.position);
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI/4;var e=w.Spawn("villager",0,tree.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*2.2f);
                w.Fog.Tick(w.Entities);Order(e,OrderKind.Gather,tree.id);
            }
            for(int i=0;i<8;i++){Step();yield return new WaitForSecondsRealtime(.1f);}
            Assert.Greater(game.Audio.ActivityPlayedCount,2);Assert.AreEqual(12,game.Audio.SourceCount);
            Assert.LessOrEqual(game.Audio.GetComponentsInChildren<AudioSource>().Count(s=>s.clip!=null&&s.clip.name.StartsWith("Activity ")),2);
            int played=game.Audio.PlayedCount;
            w.Emit("Fire",0,0,"Ranged",0,0,tree.position);game.Audio.OnEvent(w.Events.Last());Assert.AreEqual("Gun",game.Audio.LastCategory.ToString());
            w.Emit("ShipmentArrived",0,0,"Delivery");game.Audio.OnEvent(w.Events.Last());Assert.AreEqual("Notice",game.Audio.LastCategory.ToString());
            Assert.AreEqual(played+2,game.Audio.PlayedCount,"Activity cannot take either reserved voice class");
            game.StartMatch();Assert.AreEqual(0,game.Audio.ActivityPlayedCount);Assert.AreEqual(12,game.Audio.SourceCount);
        }
    }
}
