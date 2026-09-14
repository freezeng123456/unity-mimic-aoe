using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    // Regression coverage for the boundary between UGUI modal transitions and
    // the discrete keyboard/world-input stream. These tests deliberately use
    // real InputSystem state events and UGUI pointer paths.
    public sealed class ModalShortcutTests : NativeInputFixture
    {
        private EntityState Town()=>game.World.Entities.First(e=>e.owner==0&&e.def.id=="town");
        private EntityState Worker()=>game.World.Entities.First(e=>e.owner==0&&e.Worker);
        private int Commands()=>game.World.Events.Count(e=>e.kind=="Command"&&e.player==0);
        private void QueueKey(Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        }
        private void QueueRight(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Right));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
        }

        [UnityTest] public IEnumerator HomeCityEscapeThenTownShortcutUsesFreshKeyboardEdge()
        {
            game.Controls.Select(Worker());game.Hud.ShowHomeCity();yield return Ready();
            Assert.IsTrue(game.Hud.ModalVisible,"Fixture requires an open Home City modal");
            QueueKey(Key.Escape);QueueKey(Key.T);yield return Ready();
            Assert.IsFalse(game.Hud.ModalVisible);
            Assert.AreEqual("town",game.Controls.FirstSelected.def.id);
        }

        [UnityTest] public IEnumerator BuildModalEscapeThenIdleVillagerShortcutUsesFreshKeyboardEdge()
        {
            game.Controls.Select(Worker());game.Hud.ShowBuildMenu();yield return Ready();
            Assert.IsTrue(game.Hud.ModalVisible,"Fixture requires an open construction modal");
            game.Controls.Select(Town());
            QueueKey(Key.Escape);QueueKey(Key.V);yield return Ready();
            Assert.IsFalse(game.Hud.ModalVisible);
            Assert.AreEqual(Worker().id,game.Controls.FirstSelected.id);
        }

        [UnityTest] public IEnumerator NativeCloseButtonThenTownShortcutWorksDuringPointerBarrier()
        {
            game.Controls.Select(Worker());game.Hud.ShowHomeCity();yield return Ready();
            var close=session.GetComponentsInChildren<Button>().Single(b=>b.name=="X");
            yield return Click(ButtonCenter(close));
            QueueKey(Key.T);yield return Ready();
            Assert.IsFalse(game.Hud.ModalVisible);
            Assert.AreEqual("town",game.Controls.FirstSelected.def.id);
        }

        [UnityTest] public IEnumerator RealBuildButtonThenFreshGCanEnterGarrisonMode()
        {
            game.Controls.Select(Worker());yield return Ready();
            var build=FindButton("BUILD");yield return Click(ButtonCenter(build));
            var manor=session.GetComponentsInChildren<Button>().Single(b=>b.name.StartsWith("Manor"));
            yield return Click(ButtonCenter(manor));
            Assert.AreEqual("house",game.Controls.BuildingToPlace,"Fixture requires the real Manor button to enter placement");
            QueueKey(Key.G);yield return Ready();
            Assert.IsTrue(game.Controls.GarrisonMode,"A fresh keyboard shortcut must not be lost by the build-button pointer suppression barrier");
            Assert.IsEmpty(game.Controls.BuildingToPlace,"Garrison mode must replace placement");
        }

        [UnityTest] public IEnumerator KeyboardShortcutRemainsIgnoredWhileModalIsOpen()
        {
            game.Hud.ShowHomeCity();yield return Ready();
            game.Controls.Select(Worker());QueueKey(Key.T);yield return Ready();
            Assert.IsTrue(game.Hud.ModalVisible);
            Assert.AreEqual(Worker().id,game.Controls.FirstSelected.id);
        }

        [UnityTest] public IEnumerator EscapeAndQueuedRightPointerCannotIssueWorldMove()
        {
            var worker=Worker();game.Controls.Select(worker);game.Hud.ShowHomeCity();yield return Ready();
            var destination=ScreenPoint(new Vector2(40,37),1);QueueKey(Key.Escape);QueueRight(destination);yield return Ready();
            Assert.IsFalse(game.Hud.ModalVisible);
            Assert.AreEqual(0,worker.orders.Count);
            Assert.AreEqual(0,Commands());
        }

        [UnityTest] public IEnumerator ShortcutConsumedByOpenModalCannotReviveWhenItCloses()
        {
            var worker=Worker();game.Controls.Select(worker);game.Hud.ShowHomeCity();yield return Ready();
            QueueKey(Key.T);yield return Ready();
            Assert.IsTrue(game.Hud.ModalVisible);Assert.AreEqual(worker.id,game.Controls.FirstSelected.id);
            QueueKey(Key.Escape);yield return Ready();
            Assert.IsFalse(game.Hud.ModalVisible);Assert.AreEqual(worker.id,game.Controls.FirstSelected.id);
        }

        [UnityTest] public IEnumerator RealResumeButtonAcceptsFreshKeyButRejectsQueuedRightPointer()
        {
            var worker=Worker();game.Controls.Select(worker);game.TogglePause();yield return Ready();
            Assert.IsTrue(game.IsPaused);
            var point=ButtonCenter(FindButton("RESUME"));
            yield return Click(point);
            QueueRight(ScreenPoint(new Vector2(40,37),1));QueueKey(Key.T);yield return Ready();
            Assert.IsFalse(game.IsPaused);Assert.AreEqual(Town().id,game.Controls.FirstSelected.id);
            Assert.AreEqual(0,worker.orders.Count);Assert.AreEqual(0,Commands());
        }

        [UnityTest] public IEnumerator SameUpdateResumeClickThenKeyRetainsOrderAcrossCanvasRebuild()
        {
            var worker=Worker();game.Controls.Select(worker);game.TogglePause();yield return Ready();
            var point=ButtonCenter(FindButton("RESUME"));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
            QueueRight(ScreenPoint(new Vector2(40,37),1));QueueKey(Key.T);
            // Closing the real modal invalidates canvas raycasts. The FIFO may
            // span another render pass before dispatching events behind the click.
            yield return Ready();yield return Ready();
            Assert.IsFalse(game.IsPaused);Assert.AreEqual(Town().id,game.Controls.FirstSelected.id);
            Assert.AreEqual(0,worker.orders.Count);Assert.AreEqual(0,Commands());
        }
    }
}
