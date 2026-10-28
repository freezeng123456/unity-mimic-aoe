using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    // Controlled InputSystem/UGUI fixture; no paid-economic or OS-click claim.
    public sealed class TransportCargoHud079Tests : NativeInputFixture
    {
        EntityState ship,passenger;
        IEnumerator PrepareIncoming()
        {
            game.SelectedMap=MapTopology.River;game.SelectedObjective="elimination";game.StartMatch();game.enabled=false;
            foreach(var entity in game.World.Entities.Where(e=>e.def.id!="town"&&e.def.id!="trade_socket").ToArray())game.World.Remove(entity);
            ship=SpawnHere("transport_ship",0,new Vector2(64,25));passenger=SpawnHere("hussar",0,new Vector2(49,25));
            game.Controls.Focus(new Vector2(63,25));game.World.Fog.Tick(game.World.Entities);
            var result=game.World.Issue(new GameCommand{player=0,kind=CommandKind.Embark,actors=new[]{passenger.id},target=ship.id,point=new Vector2(55,25)});
            Assert.IsTrue(result.accepted,result.reason);yield return Ready();
        }
        bool HasText(string expected)=>session.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(expected));
        IEnumerator OpenCargo()
        {
            var point=ScreenPoint(ship.position,game.World.SurfaceHeight(ship.position,true)+1);AssertWorldPoint(point);
            yield return Click(point);Assert.AreEqual(ship.id,game.Controls.FirstSelected?.id);
            yield return Click(ButtonCenter(FindButton("PASSENGERS")));Assert.IsTrue(game.Hud.ModalVisible);
        }

        [UnityTest] public IEnumerator CargoModalShowsIncomingAndRefreshesAfterStopWithoutReopening()
        {
            yield return PrepareIncoming();string before=JsonUtility.ToJson(game.World.CaptureState());
            Assert.AreEqual("Cargo 0 + 2 incoming /14 slots · 0 aboard · 1 incoming",game.Hud.TransportCargoSummary079(ship));
            Assert.AreEqual(before,JsonUtility.ToJson(game.World.CaptureState()));
            yield return OpenCargo();Assert.IsTrue(HasText("Cargo 0 + 2 incoming /14 slots · 0 aboard · 1 incoming"));
            Assert.IsTrue(HasText("No passengers aboard"));
            Assert.IsTrue(game.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{passenger.id},order=OrderKind.Stop}).accepted);
            // Expansion panels refresh with the existing one-second match clock.
            game.World.Tick(1.1f);yield return Ready();Assert.IsTrue(game.Hud.ModalVisible);
            Assert.IsTrue(HasText("Cargo 0 + 0 incoming /14 slots · 0 aboard · 0 incoming"));
            Assert.IsFalse(HasText("Cargo 0 + 2 incoming /14"));
        }

        [UnityTest] public IEnumerator CargoModalRefreshesRealBoardingAndAClosedModalDoesNotReceiveStaleUpdates()
        {
            yield return PrepareIncoming();yield return OpenCargo();
            for(int step=0;step<600&&passenger.garrisonedIn==0;step++)game.World.Tick(.1f);
            Assert.AreEqual(ship.id,passenger.garrisonedIn);yield return Ready();
            Assert.IsTrue(HasText("Cargo 2 + 0 incoming /14 slots · 1 aboard · 0 incoming"));
            Assert.IsTrue(HasText(passenger.def.name+" · 2 slots"));
            game.Hud.CloseModal();game.Hud.ShowGroups();Assert.IsTrue(game.Hud.ModalVisible);
            game.World.Tick(1.1f);yield return Ready();Assert.IsTrue(HasText("CONTROL GROUPS"));
            var groupsBox=session.GetComponentsInChildren<Image>().Single(image=>image.name=="CONTROL GROUPS");
            Assert.IsTrue(groupsBox.gameObject.activeInHierarchy);
            Assert.IsFalse(groupsBox.GetComponentsInChildren<Text>().Any(text=>text.text.Contains("Cargo 2 + 0 incoming /14")),
                "Only the replacement modal must exclude cargo text; the selected ship's main HUD correctly retains its cargo summary");
        }

        [UnityTest] public IEnumerator ForeignTransportHelperReturnsEmptyWithoutChangingWorldState()
        {
            yield return PrepareIncoming();var enemy=SpawnHere("transport_ship",1,new Vector2(64,100));
            string before=JsonUtility.ToJson(game.World.CaptureState());
            Assert.AreEqual("",game.Hud.TransportCargoSummary079(enemy));Assert.AreEqual("",game.Hud.TransportCargoSummary079(null));
            Assert.AreEqual(before,JsonUtility.ToJson(game.World.CaptureState()));
        }
    }
}
