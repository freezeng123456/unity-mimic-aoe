using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class SaveLoadSessionTests
    {
        [UnityTest] public IEnumerator MenuLoadRestoresMatchCameraSelectionGroupsAndPausedInput()
        {
            string directory=Path.Combine(Path.GetTempPath(),"Frontier-session-test-"+Guid.NewGuid().ToString("N"));var root=new GameObject("Save load native session test");
            try
            {
                var game=root.AddComponent<GameController>();yield return null;game.enabled=false;
                typeof(GameController).GetField("saveDirectoryOverride",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,directory);
                game.SelectedMap=MapTopology.Winding;game.PlayerCivilization="france";game.StartMatch();
                for(int i=0;i<30;i++)game.Simulation.Step();game.TogglePause();
                var worker=game.World.Entities.First(e=>e.Alive&&e.owner==0&&e.Worker);game.Controls.Select(worker);game.Controls.SaveGroup(3);game.Controls.RestoreCamera(new Vector2(46,51),32);
                var original=game.World;float time=original.Time;string id=game.MatchId;string state=JsonUtility.ToJson(original.CaptureState());
                Assert.IsTrue(game.SaveMatch(1));game.ReturnToMenu();Assert.IsTrue(game.InMenu);Assert.IsTrue(game.LoadMatch(1));
                Assert.AreNotSame(original,game.World);Assert.AreEqual(time,game.World.Time);Assert.AreEqual(id,game.MatchId);
                Assert.AreEqual(state,JsonUtility.ToJson(game.World.CaptureState()));Assert.IsTrue(game.IsPaused);Assert.IsFalse(game.InMenu);
                CollectionAssert.AreEqual(new[]{worker.id},game.Controls.Selected);Assert.AreEqual(1,game.Controls.GroupCount(3));
                Assert.AreEqual(new Vector2(46,51),game.Controls.CameraFocus);Assert.AreEqual(32,game.GameCamera.orthographicSize);
                yield return null;yield return null;Assert.AreEqual(32,game.GameCamera.orthographicSize,"Camera update must preserve restored zoom");Assert.AreEqual(time,game.World.Time);
                Assert.IsTrue(root.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t=>t.text=="SAVE MATCH"),"Loaded game must expose the ordinary pause menu");
                game.TogglePause();Assert.IsTrue(game.Simulation.Step());Assert.Greater(game.World.Time,time);
                var valid=MatchSaveStore.Read(MatchSaveStore.SlotPath(directory,1));var active=game.World;
                valid.appVersion="older-version";MatchSaveStore.Write(MatchSaveStore.SlotPath(directory,2),valid);
                Assert.IsFalse(game.LoadMatch(2));Assert.AreSame(active,game.World);
                valid.appVersion=Application.version;valid.world.caravanWaypoint=-1;MatchSaveStore.Write(MatchSaveStore.SlotPath(directory,2),valid);
                Assert.IsFalse(game.LoadMatch(2));Assert.AreSame(active,game.World,"A structurally damaged checkpoint with a valid checksum must not replace the session");
                var previous=game.World;string path=MatchSaveStore.SlotPath(directory,1);File.WriteAllText(path,"damaged");
                Assert.IsFalse(game.LoadMatch(1));Assert.AreSame(previous,game.World,"A corrupt save must leave the running match intact");
            }
            finally{UnityEngine.Object.Destroy(root);if(Directory.Exists(directory))Directory.Delete(directory,true);}
            yield return null;
        }
    }
}
