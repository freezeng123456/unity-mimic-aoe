using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class InputResetTests : NativeInputFixture
    {
        private EntityState Worker()=>game.World.Entities.First(e=>e.owner==0&&e.Worker);
        private EntityState Town()=>game.World.Entities.First(e=>e.owner==0&&e.def.id=="town");
        private void Down(Key key)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
        private void Up(Key key)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        private void Down(Vector2 p)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Left));
        private void Up(Vector2 p)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=p});

        [UnityTest] public IEnumerator ResetDeviceDuringHeldAttackKeyRequiresFreshPress()
        {
            game.enabled=true;game.Controls.Select(Worker());Down(Key.A);yield return Ready();Assert.IsTrue(game.Controls.AttackMode);
            InputSystem.ResetDevice(keyboard);Up(Key.A);yield return Ready();game.Controls.CancelTargetMode();
            Down(Key.A);Up(Key.A);yield return Ready();game.enabled=false;Assert.IsTrue(game.Controls.AttackMode);
        }

        [UnityTest] public IEnumerator ResetDeviceDuringHeldEscapeDoesNotTurnReleaseIntoResume()
        {
            game.enabled=true;Down(Key.Escape);yield return Ready();Assert.IsTrue(game.IsPaused);
            InputSystem.ResetDevice(keyboard);Up(Key.Escape);yield return Ready();Assert.IsTrue(game.IsPaused);
            Down(Key.Escape);Up(Key.Escape);yield return Ready();game.enabled=false;Assert.IsFalse(game.IsPaused);
        }

        [UnityTest] public IEnumerator ResetDeviceDuringHeldLeftClickDropsOldClickAndAcceptsFreshSelection()
        {
            game.enabled=true;var worker=Worker();var town=Town();game.Controls.Focus(town.position);yield return Ready();var wp=ScreenPoint(worker.position);var tp=ScreenPoint(town.position);AssertWorldPoint(tp);
            Down(wp);yield return Ready();InputSystem.ResetDevice(mouse);Up(wp);yield return Ready();
            Down(tp);Up(tp);yield return Ready();game.enabled=false;Assert.AreEqual(town.id,game.Controls.FirstSelected.id);
        }

        [UnityTest] public IEnumerator OrdinaryUiPointerClearWhileHeldDoesNotInventFreshRelease()
        {
            game.enabled=true;var worker=Worker();game.Controls.Select(worker);game.Hud.ShowHomeCity();yield return Ready();
            var p=ButtonCenter(FindButton("X"));Down(p);yield return Ready();game.Hud.CancelUiPointer();Up(p);yield return Ready();
            Assert.IsTrue(game.Hud.ModalVisible,"Clearing UI capture must not synthesize a click or close the modal");
            Down(p);Up(p);yield return Ready();game.enabled=false;Assert.IsFalse(game.Hud.ModalVisible);
        }

        [UnityTest] public IEnumerator ResetWithoutPhysicalKeyReleaseStillAcceptsFreshAttackPress()
        {
            game.enabled=true;game.Controls.Select(Worker());Down(Key.A);yield return Ready();game.Controls.CancelTargetMode();Assert.IsFalse(game.Controls.AttackMode);InputSystem.ResetDevice(keyboard);
            Down(Key.A);Up(Key.A);yield return Ready();game.enabled=false;Assert.IsTrue(game.Controls.AttackMode);
        }

        [UnityTest] public IEnumerator ResetWithoutPhysicalEscapeReleaseStillAcceptsFreshEscapePress()
        {
            game.enabled=true;Down(Key.Escape);yield return Ready();Assert.IsTrue(game.IsPaused);game.TogglePause();yield return Ready();
            InputSystem.ResetDevice(keyboard);Down(Key.Escape);Up(Key.Escape);yield return Ready();game.enabled=false;Assert.IsTrue(game.IsPaused);
        }

        [UnityTest] public IEnumerator ResetWithoutPhysicalMouseReleaseStillAcceptsFreshWorldClick()
        {
            game.enabled=true;game.Hud.ShowHomeCity();yield return Ready();var p=ButtonCenter(FindButton("X"));
            Down(p);yield return Ready();game.Hud.CancelUiPointer();InputSystem.ResetDevice(mouse);Down(p);Up(p);yield return Ready();game.enabled=false;Assert.IsFalse(game.Hud.ModalVisible);
        }
    }
}
