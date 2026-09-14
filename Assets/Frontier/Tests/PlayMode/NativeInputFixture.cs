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
    // Real discrete InputSystem events, with focus routing restored after each test.
    public abstract class NativeInputFixture
    {
        protected Mouse mouse;
        protected Keyboard keyboard;
        protected GameObject session;
        protected GameController game;
        protected readonly Vector2 site=new Vector2(76,25);
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        [UnitySetUp] public IEnumerator StartNativeInput()
        {
            background=InputSystem.settings.backgroundBehavior;
            editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
            session=new GameObject("Native input regression");game=session.AddComponent<GameController>();yield return null;
            game.StartMatch();game.enabled=false;game.Controls.Focus(site);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f)});
            yield return Ready();
        }
        [UnityTearDown] public IEnumerator FinishNativeInput()
        {
            Object.Destroy(session);yield return null;
            InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior=background;
            InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
        }
        protected IEnumerator Ready(){yield return null;Canvas.ForceUpdateCanvases();yield return null;}
        protected Vector2 ScreenPoint(Vector2 p,float height=0)=>game.GameCamera.WorldToScreenPoint(new Vector3(p.x,height,p.y));
        protected EntityState SpawnHere(string id,int owner=-1,Vector2? position=null,bool complete=true)
        {var e=game.World.Spawn(id,owner,position??site,complete);game.World.Fog.Reveal(0,e.position,12);return e;}
        protected IEnumerator Click(Vector2 point,MouseButton button=MouseButton.Left)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(button));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Ready();
        }
        protected IEnumerator Press(Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();}
        protected Button FindButton(string prefix)=>session.GetComponentsInChildren<Button>().Single(b=>b.name.StartsWith(prefix));
        protected Vector2 ButtonCenter(Button button)
        {var rect=(RectTransform)button.transform;return RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));}
        protected void AssertWorldPoint(Vector2 point)
        {
            Assert.That(point.x,Is.InRange(1,Screen.width-1));Assert.That(point.y,Is.InRange(1,Screen.height-1));
            Assert.IsFalse(game.Hud.BlocksWorldInput(point),"Fixture point must hit unobstructed game viewport");
        }
    }
}
