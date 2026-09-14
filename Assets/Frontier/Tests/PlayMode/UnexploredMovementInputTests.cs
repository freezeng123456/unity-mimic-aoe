using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class UnexploredMovementInputTests : NativeInputFixture
    {
        private EntityState Worker()=>game.World.Entities.First(e=>e.owner==0&&e.Worker&&e.garrisonedIn==0);
        private Vector2 Unexplored(float x,float y)=>new Vector2(x,y);
        private void AssertUnexplored(Vector2 p){Assert.AreEqual(VisibilityState.Unexplored,game.World.Fog.At(0,p),"destination must remain unexplored");}
        private IEnumerator QueueRight(Vector2 p){AssertWorldPoint(ScreenPoint(p));yield return Click(ScreenPoint(p),MouseButton.Right);}

        [UnityTest] public IEnumerator RightClickUnexploredGroundQueuesMoveAndAdvances()
        {
            var unit=Worker();unit.position=new Vector2(76,25);game.World.Fog.Tick(game.World.Entities);var destination=Unexplored(108,25);AssertUnexplored(destination);game.Controls.Focus(destination);var before=unit.position;game.Controls.Select(unit);
            yield return QueueRight(destination);Assert.IsTrue(unit.orders.Any(o=>o.kind==OrderKind.Move));
            for(int i=0;i<120;i++)game.World.Tick(.1f);
            Assert.Greater(Vector2.Distance(unit.position,before),.1f);Assert.Less(Vector2.Distance(unit.position,destination),3f);Assert.AreEqual(VisibilityState.Visible,game.World.Fog.At(0,destination));
        }

        [UnityTest] public IEnumerator AThenUnexploredGroundQueuesAttackMove()
        {
            var unit=Worker();unit.position=new Vector2(76,25);game.World.Fog.Tick(game.World.Entities);var destination=Unexplored(108,25);AssertUnexplored(destination);game.Controls.Focus(destination);game.Controls.Select(unit);yield return Press(Key.A);yield return Click(ScreenPoint(destination),MouseButton.Left);
            Assert.IsTrue(unit.orders.Any(o=>o.kind==OrderKind.AttackMove));
        }

        [UnityTest] public IEnumerator HiddenEnemyAtUnexploredGroundCannotBecomeAttackTarget()
        {
            var unit=Worker();unit.position=new Vector2(76,25);game.World.Fog.Tick(game.World.Entities);var destination=Unexplored(108,25);AssertUnexplored(destination);game.Controls.Focus(destination);var hidden=game.World.Spawn("pike",1,destination,true);Assert.IsFalse(game.World.IsVisible(0,hidden));game.Controls.Select(unit);
            yield return QueueRight(destination);Assert.IsTrue(unit.orders.Any(o=>o.kind==OrderKind.Move));Assert.IsFalse(unit.orders.Any(o=>o.kind==OrderKind.Attack&&o.target==hidden.id));
        }

        [UnityTest] public IEnumerator UnexploredMinimapLeftClickFocusesAndRightClickQueuesMove()
        {
            var minimap=session.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Interactive minimap");Assert.IsNotNull(minimap,"production minimap must be present");var p=RectTransformUtility.WorldToScreenPoint(null,minimap.TransformPoint(minimap.rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Left));InputSystem.QueueStateEvent(mouse,new MouseState{position=p});yield return Ready();
            Assert.That(game.Controls.CameraFocus.x,Is.InRange(55,73));Assert.That(game.Controls.CameraFocus.y,Is.InRange(55,73));
            var unit=Worker();unit.position=new Vector2(76,25);game.World.Fog.Tick(game.World.Entities);game.Controls.Select(unit);var destination=Unexplored(108,25);AssertUnexplored(destination);var local=minimap.rect;var q=RectTransformUtility.WorldToScreenPoint(null,minimap.TransformPoint(new Vector2(Mathf.Lerp(local.xMin,local.xMax,destination.x/128f),Mathf.Lerp(local.yMin,local.yMax,destination.y/128f))));
            yield return Click(q,MouseButton.Right);Assert.IsTrue(unit.orders.Any(o=>o.kind==OrderKind.Move));
        }
    }
}
