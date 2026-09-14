using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class InputResetIsolationTests : NativeInputFixture
    {
        private EntityState Worker()=>game.World.Entities.First(e=>e.owner==0&&e.Worker);
        private EntityState Town()=>game.World.Entities.First(e=>e.owner==0&&e.def.id=="town");
        private void Seed(Vector2 p)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState{position=p});InputSystem.Update();}
        private void QueueClick(Vector2 p)
        {InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Left));InputSystem.QueueStateEvent(mouse,new MouseState{position=p});}

        [UnityTest] public IEnumerator CapturedTrainingClickSurvivesKeyboardReset()
        {
            game.Controls.Select(Town());yield return Ready();var p=ButtonCenter(FindButton("Settler"));Seed(p);
            QueueClick(p);InputSystem.Update(); // Buffer captured both edges; no frame/LateUpdate has run yet.
            InputSystem.ResetDevice(keyboard);yield return Ready();
            Assert.AreEqual(1,Town().queue.Count);Assert.AreEqual(100,game.World.Players[0].spent.food,.001f);
        }

        [UnityTest] public IEnumerator CapturedAttackKeySurvivesMouseReset()
        {
            game.Controls.Select(Worker());Seed(new Vector2(Screen.width*.5f,Screen.height*.5f));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A));InputSystem.Update();
            InputSystem.ResetDevice(mouse);yield return Ready();Assert.IsTrue(game.Controls.AttackMode);
        }

        [UnityTest] public IEnumerator ResetDropsBufferedMouseClickAndAllowsImmediateFreshClick()
        {
            game.Controls.Select(Town());yield return Ready();var p=ButtonCenter(FindButton("Settler"));Seed(p);
            QueueClick(p);InputSystem.Update();InputSystem.ResetDevice(mouse);yield return Ready();
            Assert.AreEqual(0,game.World.Players[0].spent.food,.001f);Assert.IsEmpty(Town().queue);
            InputSystem.ResetDevice(mouse);QueueClick(p);yield return Ready();
            Assert.AreEqual(100,game.World.Players[0].spent.food,.001f);Assert.AreEqual(1,Town().queue.Count);
        }

        [UnityTest] public IEnumerator FocusRegainPreservesNonHeldFirstTShortcut()
        {
            Seed(new Vector2(Screen.width*.5f,Screen.height*.5f));game.Controls.Select(Worker());
            game.Controls.SendMessage("OnApplicationFocus",false,SendMessageOptions.DontRequireReceiver);InputSystem.ResetDevice(keyboard);game.Controls.SendMessage("OnApplicationFocus",true,SendMessageOptions.DontRequireReceiver);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.T));InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();
            Assert.AreEqual(Town().id,game.Controls.FirstSelected.id);
        }
    }
}
