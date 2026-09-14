using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // OS focus sequence simulation: this is not a measurement of native macOS
    // focus delivery. It verifies held-state synchronization around the focus
    // callbacks while still driving the real input/UI seams.
    public sealed class FocusHeldInputTests : NativeInputFixture
    {
        private EntityState Worker()=>game.World.Entities.First(e=>e.owner==0&&e.Worker);
        private EntityState Town()=>game.World.Entities.First(e=>e.owner==0&&e.def.id=="town");
        private void FocusLostAndRegained(){game.Controls.SendMessage("OnApplicationFocus",false,SendMessageOptions.DontRequireReceiver);InputSystem.ResetDevice(keyboard);InputSystem.ResetDevice(mouse);game.Controls.SendMessage("OnApplicationFocus",true,SendMessageOptions.DontRequireReceiver);}
        private void Held(Key key)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
        private void Released()=>InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        private void HeldMouse(Vector2 p)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Left));
        private void ReleasedMouse(Vector2 p)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=p});

        [UnityTest] public IEnumerator HeldAttackKeyAcrossFocusGainIsNotACommandUntilFreshPress()
        {
            game.enabled=true;game.Controls.Select(Worker());Held(Key.A);yield return Ready();Assert.IsTrue(game.Controls.AttackMode);game.Controls.CancelTargetMode();
            FocusLostAndRegained();Held(Key.A);yield return Ready();Assert.IsFalse(game.Controls.AttackMode);
            Released();yield return Ready();Held(Key.A);Released();yield return Ready();game.enabled=false;Assert.IsTrue(game.Controls.AttackMode);
        }

        [UnityTest] public IEnumerator HeldEscapeAcrossFocusGainCannotResumeOrPauseUntilFreshPress()
        {
            game.enabled=true;Held(Key.Escape);yield return Ready();Assert.IsTrue(game.IsPaused);
            FocusLostAndRegained();Held(Key.Escape);yield return Ready();Assert.IsTrue(game.IsPaused);
            Released();yield return Ready();Assert.IsTrue(game.IsPaused);Held(Key.Escape);Released();yield return Ready();game.enabled=false;Assert.IsFalse(game.IsPaused);
        }

        [UnityTest] public IEnumerator HeldResumeButtonAcrossFocusGainCannotClickUntilFreshPointerPress()
        {
            game.enabled=true;var town=Town();game.Controls.Focus(town.position);AssertWorldPoint(ScreenPoint(town.position));game.TogglePause();yield return Ready();var p=ButtonCenter(FindButton("RESUME"));
            HeldMouse(p);yield return Ready();game.Controls.SendMessage("OnApplicationFocus",false,SendMessageOptions.DontRequireReceiver);InputSystem.ResetDevice(mouse);game.Controls.SendMessage("OnApplicationFocus",true,SendMessageOptions.DontRequireReceiver);HeldMouse(p);yield return Ready();Assert.IsTrue(game.IsPaused);
            ReleasedMouse(p);yield return Ready();Assert.IsTrue(game.IsPaused);HeldMouse(p);ReleasedMouse(p);yield return Ready();game.enabled=false;Assert.IsFalse(game.IsPaused);
        }
    }
}
