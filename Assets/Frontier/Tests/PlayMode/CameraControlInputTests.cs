using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class CameraControlInputTests : NativeInputFixture
    {
        [UnityTest] public IEnumerator EdgeHoverRespectsButtonsAndMinimapButNotPassiveHudPanels()
        {
            yield return Ready();
            var pause=ButtonCenter(FindButton("II"));Assert.IsTrue(game.Hud.BlocksCameraEdgePan(pause));
            var mini=session.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Interactive minimap");
            var point=RectTransformUtility.WorldToScreenPoint(null,mini.TransformPoint(mini.rect.center));Assert.IsTrue(game.Hud.BlocksCameraEdgePan(point));
            var bar=session.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Resource and age bar");
            point=RectTransformUtility.WorldToScreenPoint(null,bar.TransformPoint(bar.rect.center));
            Assert.IsTrue(game.Hud.BlocksWorldInput(point));Assert.IsFalse(game.Hud.BlocksCameraEdgePan(point));
        }

        [UnityTest] public IEnumerator HeldArrowPansUnexploredGroundAndPauseStopsIt()
        {
            var start=new Vector2(80,70);game.Controls.Focus(start);
            Assert.AreEqual(VisibilityState.Unexplored,game.World.Fog.At(0,start));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));yield return Ready();
            Assert.Greater(game.Controls.CameraFocus.x,start.x);
            Assert.AreEqual(VisibilityState.Unexplored,game.World.Fog.At(0,start));
            game.TogglePause();var stopped=game.Controls.CameraFocus;yield return Ready();
            Assert.AreEqual(stopped,game.Controls.CameraFocus);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();game.TogglePause();yield return Ready();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftArrow));yield return Ready();
            Assert.Less(game.Controls.CameraFocus.x,stopped.x);
        }

        [UnityTest] public IEnumerator HomeCityAndMainMenuBlockHeldCameraArrows()
        {
            game.Controls.Focus(new Vector2(80,70));game.Hud.ShowHomeCity();yield return Ready();var before=game.Controls.CameraFocus;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));yield return Ready();Assert.AreEqual(before,game.Controls.CameraFocus);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();game.ReturnToMenu();yield return Ready();before=game.Controls.CameraFocus;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.UpArrow));yield return Ready();Assert.AreEqual(before,game.Controls.CameraFocus);
        }
    }
}
