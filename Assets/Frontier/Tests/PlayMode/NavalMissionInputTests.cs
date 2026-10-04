using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    // Controlled in-engine pointer fixtures. These exercise native InputSystem
    // edges and UGUI/world routing; they do not certify operating-system clicks.
    public sealed class NavalMissionInputTests : NativeInputFixture
    {
        EntityState ship,passenger;
        IEnumerator ShoreFixture()
        {
            game.SelectedMap=MapTopology.River;game.SelectedObjective="elimination";game.StartMatch();game.enabled=false;
            foreach(var e in game.World.Entities.Where(e=>e.def.id!="town"&&e.def.id!="trade_socket").ToArray())game.World.Remove(e);
            ship=SpawnHere("transport_ship",0,new Vector2(61,25));passenger=SpawnHere("pike",0,new Vector2(52,25));
            game.Controls.Focus(new Vector2(63,25));game.World.Fog.Reveal(0,new Vector2(63,25),40);yield return Ready();
        }
        Vector2 EntityPoint(EntityState entity)=>ScreenPoint(entity.position,game.World.SurfaceHeight(entity.position,entity.def.waterborne)+1);
        IEnumerator SelectByPointer(EntityState entity)
        {var point=EntityPoint(entity);AssertWorldPoint(point);yield return Click(point);Assert.AreEqual(entity.id,game.Controls.FirstSelected?.id);}
        void CompleteBoarding()
        {
            var world=game.World;
            for(int step=0;step<600&&passenger.garrisonedIn==0;step++)world.Tick(.1f);
            Assert.AreEqual(ship.id,passenger.garrisonedIn,"The passenger must physically reach the ship through its shore order");
            CollectionAssert.Contains(ship.occupants,passenger.id);Assert.Greater(world.TransportLoad(ship),0);
            Assert.IsTrue(world.Events.Any(e=>e.kind=="Embarked"&&e.entity==passenger.id&&e.target==ship.id));
            Assert.IsTrue(world.WaterNavigation.IsWalkable(ship.position,ship.def.radius));
        }
        IEnumerator BoardByContext()
        {
            yield return SelectByPointer(passenger);var point=EntityPoint(ship);AssertWorldPoint(point);yield return Click(point,MouseButton.Right);
            Assert.AreEqual(OrderKind.Embark,passenger.orders.Peek().kind);CompleteBoarding();yield return Ready();
        }
        IEnumerator Box(Vector2 first,Vector2 second)
        {
            Vector2 low=Vector2.Min(first,second)-Vector2.one*15,high=Vector2.Max(first,second)+Vector2.one*15;
            AssertWorldPoint(low);AssertWorldPoint(high);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=low}.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=high}.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=high});yield return Ready();
        }
        [UnityTest] public IEnumerator ContextBoardingHidesCargoFromClickBoxAndSameTypeDoubleClick()
        {
            yield return ShoreFixture();yield return BoardByContext();
            Assert.IsFalse(game.World.IsVisible(0,passenger));yield return SelectByPointer(ship);
            CollectionAssert.DoesNotContain(game.Controls.Selected,passenger.id);
            var ashore=SpawnHere("pike",0,new Vector2(52,30));yield return Ready();
            yield return Box(EntityPoint(ship),EntityPoint(ashore));
            CollectionAssert.AreEquivalent(new[]{ship.id,ashore.id},game.Controls.Selected,"Selection rectangle must include visible actors and exclude embarked cargo");
            var point=EntityPoint(ashore);double time=InputState.currentTime;
            for(int i=0;i<2;i++)
            {
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left),time);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point},time);
            }
            yield return Ready();CollectionAssert.AreEqual(new[]{ashore.id},game.Controls.Selected,"Select-all of a visible type must not select a hidden passenger of that type");
            yield return SelectByPointer(ship);yield return Click(ButtonCenter(FindButton("PASSENGERS")));
            Assert.IsTrue(game.Hud.ModalVisible);
            Assert.IsTrue(session.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(passenger.def.name)&&t.text.Contains("slots")),"The cargo panel must describe the actual embarked passenger");
        }
        [UnityTest] public IEnumerator HudBoardAndUnloadClicksDeliverThePassengerOntoTheChosenOppositeBank()
        {
            yield return ShoreFixture();yield return SelectByPointer(passenger);
            yield return Click(ButtonCenter(FindButton("BOARD SHIP")));Assert.IsTrue(game.Controls.EmbarkMode);
            yield return Click(EntityPoint(ship));Assert.IsFalse(game.Controls.EmbarkMode);CompleteBoarding();yield return Ready();
            yield return SelectByPointer(ship);var unload=FindButton("UNLOAD");Assert.IsTrue(unload.interactable);
            yield return Click(ButtonCenter(unload));Assert.IsTrue(game.Controls.DisembarkMode);
            Vector2 requested=new Vector2(74,25);var landing=ScreenPoint(requested,game.World.SurfaceHeight(requested));AssertWorldPoint(landing);
            yield return Click(landing);Assert.IsFalse(game.Controls.DisembarkMode);Assert.AreEqual(OrderKind.Disembark,ship.orders.Peek().kind);
            for(int step=0;step<600&&passenger.garrisonedIn!=0;step++)game.World.Tick(.1f);
            Assert.AreEqual(0,passenger.garrisonedIn);Assert.IsEmpty(ship.occupants);Assert.AreEqual(0,game.World.TransportLoad(ship));
            Assert.Greater(passenger.position.x,MapTopology.RiverMaxX);Assert.Less(Vector2.Distance(requested,passenger.position),6);
            Assert.IsTrue(game.World.Navigation.IsWalkable(passenger.position,passenger.def.radius));Assert.IsTrue(game.World.WaterNavigation.IsWalkable(ship.position,ship.def.radius));
            Assert.IsTrue(game.World.Events.Any(e=>e.kind=="Disembarked"&&e.entity==passenger.id&&e.target==ship.id));
            game.World.Fog.Tick(game.World.Entities);yield return Ready();yield return SelectByPointer(passenger);
        }
        [UnityTest] public IEnumerator OrdinaryGarrisonTargetingCannotBypassTransportBoarding()
        {
            yield return ShoreFixture();yield return SelectByPointer(passenger);yield return Press(Key.G);Assert.IsTrue(game.Controls.GarrisonMode);
            yield return Click(EntityPoint(ship));Assert.IsTrue(game.Controls.GarrisonMode);Assert.IsEmpty(passenger.orders);Assert.IsEmpty(ship.occupants);
            var result=game.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{passenger.id},order=OrderKind.Garrison,target=ship.id});
            Assert.IsFalse(result.accepted);Assert.AreEqual(0,passenger.garrisonedIn);
            yield return Press(Key.Escape);yield return new WaitForSecondsRealtime(.25f);
            yield return Click(ButtonCenter(FindButton("BOARD SHIP")));yield return Click(EntityPoint(ship));CompleteBoarding();
        }
        [UnityTest] public IEnumerator SavedCargoKeepsGroupMembershipButOnlyBecomesSelectableAfterRealUnloading()
        {
            string directory=Path.Combine(Path.GetTempPath(),"Frontier-cargo-group-test-"+System.Guid.NewGuid().ToString("N"));
            var overrideField=typeof(GameController).GetField("saveDirectoryOverride",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.IsNotNull(overrideField);var previousOverride=overrideField.GetValue(game);string playerSaveDirectory=game.SaveDirectory;bool owned=false;
            try
            {
                Assert.IsFalse(Directory.Exists(directory));Directory.CreateDirectory(directory);owned=true;overrideField.SetValue(game,directory);
                Assert.AreEqual(directory,game.SaveDirectory);Assert.AreNotEqual(playerSaveDirectory,game.SaveDirectory);
                yield return ShoreFixture();yield return SelectByPointer(passenger);
                yield return Click(ButtonCenter(FindButton("GROUPS")));yield return Click(ButtonCenter(FindButton("SAVE 4")));
                Assert.AreEqual(1,game.Controls.GroupCount(4));CollectionAssert.AreEqual(new[]{passenger.id},game.Controls.CaptureGroups().Single(g=>g.number==4).actors);
                yield return new WaitForSecondsRealtime(.25f);
                yield return Click(ButtonCenter(FindButton("BOARD SHIP")));yield return Click(EntityPoint(ship));CompleteBoarding();yield return Ready();
                int passengerId=passenger.id,shipId=ship.id;var originalWorld=game.World;var originalPassenger=passenger;
                game.TogglePause();Assert.IsTrue(game.SaveMatch(1));string path=MatchSaveStore.SlotPath(directory,1);Assert.IsTrue(File.Exists(path));
                var disk=MatchSaveStore.Read(path,Application.version);
                CollectionAssert.AreEqual(new[]{passengerId},disk.groups.Single(g=>g.number==4).actors,"A ship passenger remains a saved group member");
                Assert.AreEqual(shipId,disk.world.entities.Single(e=>e.state.id==passengerId).state.garrisonedIn);
                game.ReturnToMenu();Assert.IsTrue(game.LoadMatch(1));Assert.AreNotSame(originalWorld,game.World);Assert.IsTrue(game.IsPaused);
                passenger=game.World.Get(passengerId);ship=game.World.Get(shipId);Assert.AreNotSame(originalPassenger,passenger);Assert.AreEqual(ship.id,passenger.garrisonedIn);
                CollectionAssert.AreEqual(new[]{passenger.id},game.Controls.CaptureGroups().Single(g=>g.number==4).actors,"Cold loading must retain owned living cargo in the membership list");
                Assert.AreEqual(1,game.Controls.GroupCount(4));
                game.Controls.RecallGroup(4);Assert.IsEmpty(game.Controls.Selected,"Recall must exclude cargo immediately, before the next frame can prune selection");
                game.TogglePause();yield return new WaitForSecondsRealtime(.25f);yield return Ready();yield return Press(Key.Digit4);
                Assert.IsEmpty(game.Controls.Selected);Assert.AreEqual(1,game.Controls.GroupCount(4),"An unavailable recall must not erase membership");
                yield return SelectByPointer(ship);yield return Click(ButtonCenter(FindButton("UNLOAD")));Assert.IsTrue(game.Controls.DisembarkMode);
                Vector2 shore=new Vector2(74,25);var point=ScreenPoint(shore,game.World.SurfaceHeight(shore));AssertWorldPoint(point);yield return Click(point);
                Assert.IsFalse(game.Controls.DisembarkMode);
                for(int step=0;step<600&&passenger.garrisonedIn!=0;step++)game.World.Tick(.1f);
                Assert.AreEqual(0,passenger.garrisonedIn);Assert.Greater(passenger.position.x,MapTopology.RiverMaxX);
                Assert.IsTrue(game.World.Events.Any(e=>e.kind=="Disembarked"&&e.entity==passenger.id&&e.target==ship.id));
                yield return Ready();yield return Press(Key.Digit4);
                CollectionAssert.AreEqual(new[]{passenger.id},game.Controls.Selected,"After an actual shore landing the same saved group recalls its surviving member again");
                CollectionAssert.AreEqual(new[]{passenger.id},game.Controls.CaptureGroups().Single(g=>g.number==4).actors);
            }
            finally
            {
                overrideField.SetValue(game,previousOverride);
                if(owned&&Directory.Exists(directory))Directory.Delete(directory,true);
            }
        }
        IEnumerator SelectMenuMap(string map)
        {
            game.ReturnToMenu();yield return Ready();
            for(int i=0;i<MapTopology.All.Length&&game.SelectedMap!=map;i++)yield return Click(ButtonCenter(FindButton("MAP:")));
            Assert.AreEqual(map,game.SelectedMap);yield return Click(ButtonCenter(FindButton("MAP, SEED & OBJECTIVES")));
            Assert.IsTrue(game.Hud.ModalVisible);
        }
        [UnityTest] public IEnumerator SeedPreviewAndMissionButtonsCreateTheSelectedGeneratedMatch()
        {
            yield return SelectMenuMap(MapTopology.GeneratedRiver);
            var preview=session.GetComponentsInChildren<RawImage>().Single(x=>x.name=="Terrain preview");var before=((Texture2D)preview.texture).GetPixels32();
            var input=session.GetComponentsInChildren<InputField>().Single(x=>x.name=="Map seed");
            var rect=(RectTransform)input.transform;yield return Click(RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)));
            // The widget receives a controlled text value; APPLY, mission and
            // BEGIN all use real pointer events, without invoking callbacks.
            input.text="81473";yield return Click(ButtonCenter(FindButton("APPLY")));
            Assert.AreEqual(81473,game.MatchSeed);Assert.AreEqual("81473",session.GetComponentsInChildren<InputField>().Single().text);
            var after=((Texture2D)session.GetComponentsInChildren<RawImage>().Single(x=>x.name=="Terrain preview").texture).GetPixels32();
            Assert.IsFalse(before.SequenceEqual(after),"A different generated seed must change actual preview terrain pixels");
            var mission=FindButton("Defend the Bridge");Assert.IsTrue(mission.interactable);yield return Click(ButtonCenter(mission));
            yield return Click(ButtonCenter(FindButton("DONE")));yield return Click(ButtonCenter(FindButton("BEGIN SKIRMISH")));yield return Ready();
            Assert.AreEqual(MapTopology.GeneratedRiver,game.World.MapId);Assert.AreEqual(81473,game.World.Seed);Assert.AreEqual(81473,game.World.Layout.Seed);
            Assert.AreEqual("defend_bridge",game.World.MapObjective.mode);Assert.AreEqual("active",game.World.MapObjective.status);
            Assert.IsTrue(game.World.Layout.Bridges.Any(b=>(b.Center-game.World.MapObjective.center).sqrMagnitude<.001f));
            Assert.IsTrue(session.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("DEFEND THE BRIDGE")&&t.text.Contains("remaining")),"Mission progress and deadline must be visible in the in-game HUD");
        }
        [UnityTest] public IEnumerator IslandMenuDisablesImpossibleBridgeAndLandEscortMissions()
        {
            yield return SelectMenuMap(MapTopology.GeneratedStrait);string selected=game.SelectedObjective;
            var bridge=FindButton("Defend the Bridge");var escort=FindButton("Escort the Caravan");
            Assert.IsFalse(bridge.interactable);Assert.IsFalse(escort.interactable);
            yield return Click(ButtonCenter(bridge));Assert.AreEqual(selected,game.SelectedObjective);
            yield return Click(ButtonCenter(escort));Assert.AreEqual(selected,game.SelectedObjective);
            yield return Click(ButtonCenter(FindButton("DONE")));yield return Click(ButtonCenter(FindButton("BEGIN SKIRMISH")));yield return Ready();
            Assert.IsTrue(game.World.Layout.RequiresTransport);Assert.IsEmpty(game.World.Layout.Bridges);Assert.AreEqual("elimination",game.World.MapObjective.mode);
            Assert.IsFalse(game.World.Entities.Any(e=>e.Alive&&e.def.id=="objective_cart"));
        }
        static Vector3[] ModelVertices(Transform model)
            =>model.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<Renderer>()?.enabled==true)
                .SelectMany(f=>f.sharedMesh.vertices.Select(v=>model.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
        static Bounds VertexBounds(Vector3[] points)
        {Assert.IsNotEmpty(points);var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)bounds.Encapsulate(point);return bounds;}
        [UnityTest] public IEnumerator ThreeFleetRolesHaveDifferentRealHullAndWeaponGeometryAndRemainPickable()
        {
            yield return ShoreFixture();game.World.Remove(ship);game.World.Remove(passenger);
            var boats=new[]{SpawnHere("transport_ship",0,new Vector2(64,19)),SpawnHere("frigate",0,new Vector2(64,26)),SpawnHere("monitor",0,new Vector2(64,33))};
            game.Controls.Focus(new Vector2(64,26));yield return Ready();
            var models=boats.Select(b=>session.transform.Find("Simulation Entity Views/Entity "+b.id).GetComponentInChildren<BoatPresentationMotion>().transform).ToArray();
            var vertices=models.Select(ModelVertices).ToArray();var bounds=vertices.Select(VertexBounds).ToArray();
            for(int i=0;i<boats.Length;i++)
            {
                Assert.Greater(vertices[i].Length,500,"Fleet role requires actual modeled hull and fittings");Assert.LessOrEqual(bounds[i].max.y,WaterPresentation.MaximumBoatHeight+.05f,"Folded river rig must fit below bridge clearance");
                Assert.IsTrue(game.View.TryGetPickBounds(boats[i].id,out var pick));Assert.Greater(pick.size.z,3);
                yield return SelectByPointer(boats[i]);
            }
            Assert.Greater(bounds[1].size.z,bounds[0].size.z+.25f,"The broadside frigate needs a visibly longer hull than the troop carrier");
            Assert.AreEqual(3,vertices.Select(v=>v.Length).Distinct().Count(),"All three roles must contain different geometry, not a renamed shared boat");
            Assert.IsNull(models[0].GetComponent<UnitRig>().Barrel,"Troop carrier has no cannon");
            var frigateGun=models[1].GetComponent<UnitRig>().Barrel;var siegeGun=models[2].GetComponent<UnitRig>().Barrel;
            Assert.IsNotNull(frigateGun);Assert.IsNotNull(siegeGun);
            var light=frigateGun.GetComponentsInChildren<MeshFilter>().Single().sharedMesh.bounds.size;
            var heavy=siegeGun.GetComponentsInChildren<MeshFilter>().Single().sharedMesh.bounds.size;
            Assert.Greater(Mathf.Min(heavy.x,heavy.y,heavy.z),Mathf.Min(light.x,light.y,light.z)*1.5f,"Monitor siege bore must have substantially heavier actual geometry than a frigate side gun");
            Assert.AreNotEqual(bounds[0].size,bounds[2].size);
        }
    }
}
