using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class DoubleClickSelectionTests : NativeInputFixture
    {
        private EntityState[] villagers;
        private double clickTime;

        [UnitySetUp] public IEnumerator SetUpDoubleClickFixture()
        {
            villagers=new[]
            {
                SpawnHere("villager",0,site+new Vector2(-6,0)),
                SpawnHere("villager",0,site+new Vector2(6,0)),
                SpawnHere("villager",0,site+new Vector2(0,4))
            };
            yield return Ready();
        }

        private IEnumerator LeftClick(Vector2 point)
        {
            clickTime=InputState.currentTime;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left),clickTime);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point},clickTime);
            yield return Ready();
        }

        private IEnumerator RightClick(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Right));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
            yield return Ready();
        }

        private Vector2 UnitPoint(EntityState e)=>ScreenPoint(e.position,1);
        private void AssertRapid(double firstTime)
        {Assert.Less(clickTime-firstTime,.3,"The input events must exercise the double-click window, not pass through a timeout");}
        private void AssertOnly(EntityState e)
        { CollectionAssert.AreEqual(new[]{e.id},game.Controls.Selected); }

        [UnityTest] public IEnumerator NearSameTypeDoubleClickSelectsVisibleSameTypeUnits()
        {
            var first=UnitPoint(villagers[0]);var near=first+Vector2.right;
            AssertWorldPoint(first);yield return LeftClick(first);AssertOnly(villagers[0]);var firstTime=clickTime;yield return LeftClick(near);AssertRapid(firstTime);
            Assert.AreEqual(villagers[0].id,game.Controls.FirstSelected.id);
            CollectionAssert.AreEquivalent(villagers.Select(v=>v.id),game.Controls.Selected,
                "A rapid nearby second click on the same unit type retains select-all behavior");
        }

        [UnityTest] public IEnumerator FarSameTypeClicksRemainSingleSelection()
        {
            var first=UnitPoint(villagers[0]);var second=UnitPoint(villagers[1]);
            Assert.Greater(Vector2.Distance(first,second),100f,"Fixture must provide a separated click pair");
            AssertWorldPoint(first);AssertWorldPoint(second);
            yield return LeftClick(first);AssertOnly(villagers[0]);var firstTime=clickTime;yield return LeftClick(second);AssertRapid(firstTime);
            AssertOnly(villagers[1]);
        }

        [UnityTest] public IEnumerator RightCommandSeparatesLaterSameTypeClick()
        {
            var first=UnitPoint(villagers[0]);var resource=SpawnHere("berry",-1,site+Vector2.up*8);
            AssertWorldPoint(first);AssertWorldPoint(ScreenPoint(resource.position));
            yield return LeftClick(first);AssertOnly(villagers[0]);var firstTime=clickTime;yield return RightClick(ScreenPoint(resource.position));
            Assert.AreEqual(OrderKind.Gather,villagers[0].orders.Single().kind);Assert.AreEqual(resource.id,villagers[0].orders.Single().target);
            yield return LeftClick(first);AssertRapid(firstTime);
            AssertOnly(villagers[0]);
        }

        [UnityTest] public IEnumerator BlankClickBreaksDoubleClickCandidate()
        {
            var first=UnitPoint(villagers[0]);var second=UnitPoint(villagers[1]);
            var blank=ScreenPoint(site+new Vector2(12,10));AssertWorldPoint(blank);
            yield return LeftClick(first);AssertOnly(villagers[0]);var firstTime=clickTime;yield return LeftClick(blank);Assert.IsEmpty(game.Controls.Selected);yield return LeftClick(second);AssertRapid(firstTime);
            AssertOnly(villagers[1]);
        }

        [UnityTest] public IEnumerator SlowSameTypeSecondClickRemainsSingleSelection()
        {
            var first=UnitPoint(villagers[0]);var second=UnitPoint(villagers[1]);
            yield return LeftClick(first);AssertOnly(villagers[0]);var firstTime=clickTime;yield return new WaitForSecondsRealtime(.35f);yield return LeftClick(second);Assert.GreaterOrEqual(clickTime-firstTime,.35);
            AssertOnly(villagers[1]);
        }

        [UnityTest] public IEnumerator DragSelectionBreaksDoubleClickCandidate()
        {
            var first=UnitPoint(villagers[0]);var second=UnitPoint(villagers[1]);
            yield return LeftClick(first);AssertOnly(villagers[0]);var firstTime=clickTime;
            var a=second-Vector2.one*18;var b=second+Vector2.one*18;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=a}.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=b}.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=b});yield return Ready();
            AssertOnly(villagers[1]);yield return LeftClick(first);AssertRapid(firstTime);AssertOnly(villagers[0]);
        }

        [UnityTest] public IEnumerator UiClickBreaksDoubleClickCandidate()
        {
            var first=UnitPoint(villagers[0]);
            yield return LeftClick(first);AssertOnly(villagers[0]);var firstTime=clickTime;
            villagers[0].orders.Enqueue(new UnitOrder(OrderKind.Move,site));
            var before=game.World.Events.Count(e=>e.kind=="Command"&&e.player==0);
            yield return LeftClick(ButtonCenter(FindButton("STOP")));
            AssertOnly(villagers[0]);Assert.IsEmpty(villagers[0].orders);
            Assert.AreEqual(before+1,game.World.Events.Count(e=>e.kind=="Command"&&e.player==0),"Actual UGUI stop command must be accepted");
            yield return LeftClick(first);AssertRapid(firstTime);AssertOnly(villagers[0]);
        }
    }
}
