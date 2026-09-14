using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace Frontier.Tests
{
    public class PointerTests
    {
        private Mouse testMouse;
        private Keyboard testKeyboard;
        [SetUp] public void CreateInputDevices()
        {
            // Headless editors have no OS devices. Every input scenario requires
            // the complete mouse-and-keyboard pair used by the runtime controller.
            testMouse=InputSystem.AddDevice<Mouse>();testKeyboard=InputSystem.AddDevice<Keyboard>();
        }
        [TearDown] public void RemoveInputDevices(){InputSystem.RemoveDevice(testMouse);InputSystem.RemoveDevice(testKeyboard);}
        [UnityTest]public IEnumerator DragCompletedWithinOneInputUpdateStillSelectsWorkers()
        {
            var mouse=testMouse;var go=new GameObject("Pointer test session");
            try
            {
                var controller=go.AddComponent<GameController>();yield return null;controller.StartMatch();yield return null;
                var workers=controller.World.Entities.Where(e=>e.owner==0&&e.Worker).ToArray();
                var positions=workers.Select(e=>(Vector2)controller.GameCamera.WorldToScreenPoint(new Vector3(e.position.x,1,e.position.y))).ToArray();
                Vector2 low=new Vector2(positions.Min(p=>p.x)-10,positions.Min(p=>p.y)-10),high=new Vector2(positions.Max(p=>p.x)+10,positions.Max(p=>p.y)+10);
                Debug.Log($"TEST_POINTER_BOUNDS {low} {high} screen={Screen.width}x{Screen.height} blocked={controller.Hud.BlocksWorldInput(low)}");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=low}.WithButton(MouseButton.Left));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=high}.WithButton(MouseButton.Left));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=high});
                yield return null;yield return null;
                Assert.AreEqual(6,controller.Controls.Selected.Count,"Pressed/moved/released between rendered frames must retain drag endpoints");
            }
            finally{Object.Destroy(go);}yield return null;
        }
        [UnityTest]public IEnumerator FastModifierChordSavesAndRestoresControlGroup()
        {
            var keyboard=testKeyboard;var go=new GameObject("Group test session");
            try
            {
                var controller=go.AddComponent<GameController>();yield return null;controller.StartMatch();yield return null;
                var worker=controller.World.Entities.First(e=>e.owner==0&&e.Worker);controller.Controls.Select(worker);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftMeta,Key.Digit1));InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                yield return null;yield return null;
                controller.Controls.Select(controller.World.Entities.First(e=>e.owner==0&&e.def.id=="town"));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit1));InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                yield return null;yield return null;
                Assert.AreEqual(worker.id,controller.Controls.FirstSelected.id);
            }
            finally{Object.Destroy(go);}yield return null;
        }
        [UnityTest]public IEnumerator PlacementSuppressesTheMenuClickBeforeAcceptingNextClick()
        {
            var mouse=testMouse;var go=new GameObject("Placement test session");
            try
            {
                var controller=go.AddComponent<GameController>();yield return null;controller.StartMatch();yield return null;
                var worker=controller.World.Entities.First(e=>e.owner==0&&e.Worker);controller.Controls.Select(worker);
                Vector2 point=new Vector2(38,35);controller.World.Fog.Reveal(0,point,10);
                Vector2 screen=controller.GameCamera.WorldToScreenPoint(new Vector3(point.x,0,point.y));
                float wood=controller.World.Players[0].resources.wood;controller.Controls.BeginBuild("house");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen}.WithButton(MouseButton.Left));InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});
                yield return null;yield return null;
                Assert.AreEqual(wood,controller.World.Players[0].resources.wood);
                yield return new WaitForSecondsRealtime(.3f);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=screen}.WithButton(MouseButton.Left));InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});
                yield return null;yield return null;
                Assert.AreEqual(wood-controller.Rules.Entity("house").cost.wood,controller.World.Players[0].resources.wood,.01f);
            }
            finally{Object.Destroy(go);}yield return null;
        }
    }
}
