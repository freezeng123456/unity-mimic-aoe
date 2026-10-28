using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class PlayerSettingsInput066Tests : NativeInputFixture
    {
        [UnityTest] public IEnumerator ReboundNondefaultKeysReachActualAttackStopAndHoldDispatchAndOldKeysStopWorking()
        {
            var unit=SpawnHere("musketeer",0);game.Controls.Select(unit);Assert.IsTrue(game.Settings.TryRebind(PlayerAction.AttackMove,Key.Q,out var error),error);yield return Ready();
            yield return Press(Key.A);Assert.IsFalse(game.Controls.AttackMode);yield return Press(Key.Q);Assert.IsTrue(game.Controls.AttackMode);
            game.Controls.CancelTargetMode();Assert.IsTrue(game.Settings.TryRebind(PlayerAction.Stop,Key.N,out error),error);Assert.IsTrue(game.Settings.TryRebind(PlayerAction.Hold,Key.Z,out error),error);yield return Ready();
            Assert.IsTrue(game.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Move,point=site+Vector2.right*8}).accepted);Assert.IsNotEmpty(unit.orders);
            yield return Press(Key.S);Assert.IsNotEmpty(unit.orders);yield return Press(Key.N);Assert.IsEmpty(unit.orders);
            yield return Press(Key.J);Assert.IsFalse(unit.hold);yield return Press(Key.Z);Assert.IsTrue(unit.hold);StringAssert.AreEqualIgnoringCase("Q",game.Settings.BindingLabel(PlayerAction.AttackMove));
        }
        [UnityTest] public IEnumerator ChangingBindingsDiscardsPreviouslyCapturedEdgesAndNewShortcutOrdersTheFollowingNativeClick()
        {
            var unit=SpawnHere("musketeer",0);game.Controls.Select(unit);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A));InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            Assert.IsTrue(game.Settings.TryRebind(PlayerAction.AttackMove,Key.Q,out var error),error);yield return Ready();Assert.IsFalse(game.Controls.AttackMode,"A queued edge using the previous mapping cannot survive settings commit");
            yield return new WaitForSecondsRealtime(.25f);var destination=site+Vector2.up*6;var point=ScreenPoint(destination);AssertWorldPoint(point);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q));InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Ready();
            Assert.AreEqual(1,unit.orders.Count);Assert.AreEqual(OrderKind.AttackMove,unit.orders.Peek().kind);Assert.Less(Vector2.Distance(destination,unit.orders.Peek().point),.2f);Assert.IsFalse(game.Controls.AttackMode);
        }
        [UnityTest] public IEnumerator ReboundCameraUsesHeldPhysicalKeysAndStopsForModalOrPause()
        {
            game.Controls.Focus(new Vector2(80,70));Assert.IsTrue(game.Settings.TryRebind(PlayerAction.CameraRight,Key.D,out var error),error);yield return Ready();var before=game.Controls.CameraFocus;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));yield return Ready();Assert.AreEqual(before,game.Controls.CameraFocus);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return Ready();Assert.Greater(game.Controls.CameraFocus.x,before.x);
            game.TogglePause();before=game.Controls.CameraFocus;yield return Ready();Assert.AreEqual(before,game.Controls.CameraFocus);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();game.TogglePause();game.Hud.ShowHomeCity();yield return Ready();before=game.Controls.CameraFocus;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return Ready();Assert.AreEqual(before,game.Controls.CameraFocus);
        }
        [UnityTest] public IEnumerator ReboundIdleWorkerRetiresBothOldShortcutsAndNewBoardingHotkeysActivateTargetModes()
        {
            var soldier=SpawnHere("musketeer",0);game.Controls.Select(soldier);Assert.IsTrue(game.Settings.TryRebind(PlayerAction.IdleWorker,Key.Y,out var error),error);yield return Ready();
            yield return Press(Key.V);CollectionAssert.AreEqual(new[]{soldier.id},game.Controls.Selected);yield return Press(Key.Period);CollectionAssert.AreEqual(new[]{soldier.id},game.Controls.Selected);
            yield return Press(Key.Y);Assert.IsTrue(game.Controls.FirstSelected.Worker);
            game.Controls.Select(soldier);yield return Press(Key.E);Assert.IsTrue(game.Controls.EmbarkMode);yield return Press(Key.U);Assert.IsTrue(game.Controls.DisembarkMode);Assert.IsFalse(game.Controls.EmbarkMode);
            yield return Press(Key.K);Assert.IsTrue(game.Controls.DockRepairMode);Assert.IsFalse(game.Controls.DisembarkMode);
        }
        [UnityTest] public IEnumerator LiveMasterAndCategoryVolumesChangeTheActualThreeAudioBuses()
        {
            var draft=game.Settings.Current;draft.masterVolume=.5f;draft.sfxVolume=.4f;draft.musicVolume=.8f;draft.environmentVolume=.6f;Assert.IsTrue(game.Settings.TryUpdate(draft,out var error),error);yield return Ready();
            // EventAudio sits on the session root; a subtree query also sees
            // the two ambient children. Check actual ownership and all 14 voices.
            var all=game.Audio.GetComponentsInChildren<AudioSource>();Assert.AreEqual(14,all.Length);
            var events=all.Where(source=>source.transform.parent==game.Audio.transform&&source.name.StartsWith("Event audio ")).ToArray();
            Assert.AreEqual(12,events.Length);Assert.AreEqual(12,game.Audio.SourceCount);
            Assert.AreEqual(.044f,events.Single(s=>s.name=="Event audio 0").volume,1e-6f);Assert.AreEqual(.02f,events.Single(s=>s.name=="Event audio 8").volume,1e-6f);Assert.AreEqual(.06f,events.Single(s=>s.name=="Event audio 10").volume,1e-6f);
            var beds=game.Ambience.GetComponentsInChildren<AudioSource>();Assert.AreEqual(2,beds.Length);Assert.IsTrue(beds.All(source=>source.transform.parent==game.Ambience.transform));
            CollectionAssert.AreEquivalent(all,events.Concat(beds).ToArray(),"SFX and the two ambient beds must exhaust all existing sources without overlap");
            Assert.AreEqual(.0195f,beds.Single(s=>s.name=="Original Wind bed").volume,1e-6f);Assert.AreEqual(.018f,beds.Single(s=>s.name=="Original FrontierTheme bed").volume,1e-6f);
            var unit=SpawnHere("musketeer",0);game.Audio.ResetMatch();game.World.Emit("AgeAdvanced",0,0,"Own age");game.Audio.OnEvent(game.World.Events.Last());Assert.AreEqual(1,game.Audio.PlayedCount);
            draft=game.Settings.Current;draft.sfxVolume=0;Assert.IsTrue(game.Settings.TryUpdate(draft,out error),error);Assert.IsTrue(events.All(s=>!s.isPlaying));game.Audio.ResetMatch();game.Audio.OnEvent(game.World.Events.Last());Assert.AreEqual(0,game.Audio.PlayedCount);Assert.IsFalse(game.Audio.TryPlayActivity(unit,EventAudio.SoundCategory.Footstep));Assert.AreEqual(.4f,game.Ambience.EffectiveMusicVolume,1e-6f);
            draft=game.Settings.Current;draft.muted=true;Assert.IsTrue(game.Settings.TryUpdate(draft,out error),error);Assert.IsTrue(events.Concat(beds).All(s=>s.mute&&!s.isPlaying));
            game.Settings.Captions.Clear();game.Settings.Captions.OnEvent(game.World,0,game.World.Events.Last());StringAssert.Contains("Age advanced",game.Settings.Captions.Text,"Captions are independent of audible playback");
        }
        [UnityTest] public IEnumerator ReboundRepairInstructionsAndZoomUseTheCurrentBindings()
        {
            var worker=game.World.Entities.First(e=>e.owner==0&&e.Worker);game.Controls.Select(worker);Assert.IsTrue(game.Settings.TryRebind(PlayerAction.Repair,Key.N,out var error),error);yield return Ready();
            yield return Press(Key.N);Assert.IsTrue(game.Controls.RepairMode);StringAssert.Contains("N / right click / Esc",game.Notice);game.Controls.CancelTargetMode();
            float size=game.GameCamera.orthographicSize;Assert.IsTrue(game.Settings.TryRebind(PlayerAction.ZoomIn,Key.Z,out error),error);yield return Ready();yield return Press(Key.Equals);Assert.AreEqual(size,game.GameCamera.orthographicSize);yield return Press(Key.Z);Assert.AreEqual(size-2,game.GameCamera.orthographicSize);
        }
    }
}
