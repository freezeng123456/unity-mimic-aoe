using System;
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
    // Actual InputSystem edges and UGUI dispatch; these are Editor integration
    // tests, not operating-system native acceptance. No button callbacks invoked.
    public sealed class ScoutingInput075Tests : NativeInputFixture
    {
        const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        string directory;
        [UnitySetUp] public IEnumerator IsolateScoutingStorage()
        {
            Assert.IsFalse(game.Settings.Persistent);Assert.IsTrue(game.Progress.MemoryOnly);
            directory=Path.Combine(Path.GetTempPath(),"Frontier-scout-input-075-"+Guid.NewGuid().ToString("N"));
            Assert.IsFalse(Directory.Exists(directory));Directory.CreateDirectory(directory);
            foreach(string kind in new[]{"save","report","replay"})
            {
                var field=typeof(GameController).GetField(kind+"DirectoryOverride",PrivateInstance);Assert.IsNotNull(field);
                field.SetValue(game,Path.Combine(directory,kind));
            }
            yield return Ready();
        }
        [UnityTearDown] public IEnumerator RemoveOnlyScoutingStorage()
        {
            if(game!=null)
            {
                game.enabled=false;
                typeof(GameController).GetMethod("FinishReplayFileWrites",PrivateInstance).Invoke(game,null);
            }
            if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
            yield return null;
        }
        EntityState Own(string id)=>game.World.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id==id);
        Transform Visual(EntityState actor)=>game.View.transform.Find("Simulation Entity Views/Entity "+actor.id);
        Button Action(string prefix)=>session.GetComponentsInChildren<Button>().Single(b=>b.isActiveAndEnabled&&b.transform.parent.name=="Context actions"&&b.name.StartsWith(prefix));
        string[] Actions()=>session.GetComponentsInChildren<Button>().Where(b=>b.isActiveAndEnabled&&b.transform.parent.name=="Context actions").Select(b=>b.name).ToArray();
        void Tick(float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.1f);i++)game.World.Tick(.1f);}
        IEnumerator Civilization(string id)
        {
            game.PlayerCivilization=id;game.OpponentCivilization="france";game.SkirmishMode="duel";
            game.SelectedMap=MapTopology.Frontier;game.AgeLimit=5;game.Autoplay=false;game.StartMatch();game.enabled=false;
            game.Controls.Focus(Own("town").position+new Vector2(6,3));yield return Ready();
        }
        IEnumerator SelectByPointer(EntityState actor)
        {
            game.Controls.Focus(actor.position);yield return Ready();
            var point=ScreenPoint(actor.position,game.World.SurfaceHeight(actor.position)+1);
            AssertWorldPoint(point);yield return Click(point);Assert.AreEqual(actor.id,game.Controls.FirstSelected?.id);
        }
        static Rect ScreenBounds(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var p=corners.Select(v=>RectTransformUtility.WorldToScreenPoint(null,v)).ToArray();
            return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));
        }
        IEnumerator Drag(Vector2 from,Vector2 to)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=from}.WithButton(MouseButton.Left));yield return Ready();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=Vector2.Lerp(from,to,.5f)}.WithButton(MouseButton.Left));yield return Ready();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=to}.WithButton(MouseButton.Left));yield return Ready();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=to});yield return Ready();
        }
        IEnumerator ClickAction(string prefix)
        {
            // Reach controls through the actual scroll viewport if a compact
            // GameView places a later scout action below the visible rows.
            for(int attempt=0;attempt<6;attempt++)
            {
                var button=Action(prefix);var viewport=button.GetComponentInParent<ScrollRect>().viewport;
                var bounds=ScreenBounds(viewport);var point=ButtonCenter(button);
                if(bounds.Contains(point))
                {Assert.IsTrue(button.interactable,prefix);yield return Click(point);yield break;}
                float direction=point.y<bounds.yMin?1:-1;
                yield return Drag(bounds.center-Vector2.up*direction*bounds.height*.30f,bounds.center+Vector2.up*direction*bounds.height*.30f);
            }
            Assert.Fail("The rendered action cannot be reached through its scroll viewport: "+prefix);
        }
        IEnumerator PaidScout(string civilization,string id)
        {
            yield return Civilization(civilization);var producer=Own(game.Rules.Entity(id).producer);
            yield return SelectByPointer(producer);var resources=game.World.Players[0].resources;
            yield return ClickAction(game.Rules.Entity(id).name+"\n");
            Assert.AreEqual(id,producer.queue.Single().item);Assert.AreEqual(resources.food-50,game.World.Players[0].resources.food);
            Tick(game.Rules.Entity(id).time+.2f);yield return Ready();
            Assert.IsEmpty(producer.queue);Assert.IsTrue(game.World.Events.Any(e=>e.kind=="UnitTrained"&&e.entity==producer.id&&game.World.Get(e.target)?.def.id==id));
            yield return SelectByPointer(Own(id));
        }
        IEnumerator RightTerrain(Vector2 point)
        {var screen=ScreenPoint(point,game.World.SurfaceHeight(point));AssertWorldPoint(screen);yield return Click(screen,MouseButton.Right);}

        [UnityTest] public IEnumerator DutchExplorerTrainAndQueueCancellationUseRealButtonsAndAccounts()
        {
            yield return Civilization("river");var explorer=Own("explorer");yield return SelectByPointer(explorer);
            Assert.IsFalse(Actions().Any(s=>s.StartsWith("ENTER STEALTH")));var resources=game.World.Players[0].resources;
            var used=game.World.Players[0].usedPopulation;yield return ClickAction("Envoy\n");
            Assert.AreEqual("envoy",explorer.queue.Single().item);Assert.AreEqual(50,explorer.queue[0].paid.food);
            Assert.AreEqual(resources.food-50,game.World.Players[0].resources.food);Assert.AreEqual(0,game.World.Players[0].reservedPopulation);
            Assert.AreEqual(used,game.World.Players[0].usedPopulation);
            var queue=session.GetComponentsInChildren<Button>().Single(b=>b.isActiveAndEnabled&&b.name.StartsWith("Envoy\n")&&b.transform.parent.name!="Context actions");
            yield return Click(ButtonCenter(queue));Assert.IsEmpty(explorer.queue);Assert.AreEqual(resources,game.World.Players[0].resources);
            Assert.IsTrue(game.World.Events.Any(e=>e.kind=="Refund"&&e.entity==explorer.id));
            yield return ClickAction("Envoy\n");Tick(15.2f);yield return Ready();
            var envoy=Own("envoy");Assert.IsTrue(envoy.Scout);Assert.IsFalse(envoy.Worker||envoy.Military);
            Assert.AreEqual(used,game.World.Players[0].usedPopulation);Assert.AreEqual(resources.food-50,game.World.Players[0].resources.food);
        }

        [UnityTest] public IEnumerator EnvoyRallyWorldAndMinimapPointerChangeOnlyTheFlagNotExplorerTravel()
        {
            yield return Civilization("river");var explorer=Own("explorer");yield return SelectByPointer(explorer);
            yield return RightTerrain(explorer.position+new Vector2(8,-4));var travel=explorer.orders.Single();var position=explorer.position;
            yield return ClickAction("ENVOY RALLY");Assert.IsTrue(game.Controls.EnvoyRallyMode);
            var destination=explorer.position+new Vector2(5,7);var screen=ScreenPoint(destination);AssertWorldPoint(screen);
            yield return Click(screen);Assert.IsFalse(game.Controls.EnvoyRallyMode);Assert.Less(Vector2.Distance(destination,explorer.rally),.1f);
            Assert.AreSame(travel,explorer.orders.Single());Assert.AreEqual(position,explorer.position);Assert.AreEqual(OrderKind.Move,travel.kind);
            yield return ClickAction("ENVOY RALLY");
            var mini=session.GetComponentsInChildren<RawImage>().Single(i=>i.isActiveAndEnabled&&i.name=="Interactive minimap").rectTransform;
            var terrain=new Vector2(44,24);var local=new Vector2(Mathf.Lerp(mini.rect.xMin,mini.rect.xMax,terrain.x/128),Mathf.Lerp(mini.rect.yMin,mini.rect.yMax,terrain.y/128));
            var pointer=RectTransformUtility.WorldToScreenPoint(null,mini.TransformPoint(local));Assert.IsTrue(game.Hud.TryMinimapPoint(pointer,out var decoded));
            Assert.Less(Vector2.Distance(decoded,terrain),.01f);yield return Click(pointer);
            Assert.IsFalse(game.Controls.EnvoyRallyMode);Assert.Less(Vector2.Distance(explorer.rally,terrain),.1f);
            Assert.AreSame(travel,explorer.orders.Single());Assert.AreEqual(position,explorer.position);
            Assert.AreEqual(2,game.World.Events.Count(e=>e.kind=="RallySet"&&e.entity==explorer.id));
            game.Controls.Focus(explorer.position);yield return Ready();yield return ClickAction("ENVOY RALLY");yield return Press(Key.Escape);
            Assert.IsFalse(game.Controls.EnvoyRallyMode);Assert.AreSame(travel,explorer.orders.Single());
        }

        [UnityTest] public IEnumerator EnvoyBuildMenuAndRightClickFoundationAllowOnlyItsTradePostCapability()
        {
            yield return PaidScout("river","envoy");var envoy=Own("envoy");var w=game.World;
            Assert.IsFalse(Actions().Any(s=>s.StartsWith("REPAIR")||s.StartsWith("MANUFACTURE")));
            // Isolate construction from treasure combat; movement, fog, cost,
            // socket and construction are still the ordinary world mechanisms.
            foreach(var guard in w.Entities.Where(World.Guardian).ToArray())w.Remove(guard);
            var socket=w.Get(w.TradeSockets[0].entity);var approach=socket.position+Vector2.down*6;
            Assert.IsTrue(game.Command(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{envoy.id},order=OrderKind.Move,point=approach}).accepted);
            for(int i=0;i<1800&&Vector2.Distance(envoy.position,approach)>1;i++)w.Tick(.1f);
            Assert.Less(Vector2.Distance(envoy.position,approach),1);Assert.IsTrue(w.IsVisible(0,socket));
            game.Controls.Focus(socket.position);yield return Ready();yield return Press(Key.B);Assert.IsTrue(game.Hud.ModalVisible);
            var buildings=game.Rules.entities.Where(d=>d.kind==EntityKind.Building).Select(d=>d.name+"  ·").ToArray();
            var choices=session.GetComponentsInChildren<Button>().Where(b=>b.isActiveAndEnabled&&buildings.Any(prefix=>b.name.StartsWith(prefix))).ToArray();
            Assert.AreEqual(1,choices.Length);StringAssert.StartsWith(game.Rules.Entity("trade_post").name+"  ·",choices[0].name);
            yield return Click(ButtonCenter(choices[0]));Assert.AreEqual("trade_post",game.Controls.BuildingToPlace);
            var funds=w.Players[0].resources;var targetPoint=ScreenPoint(socket.position);AssertWorldPoint(targetPoint);yield return Click(targetPoint);
            Assert.IsEmpty(game.Controls.BuildingToPlace);var post=Own("trade_post");Assert.IsFalse(post.Complete);
            Assert.AreEqual(funds.wood-200,w.Players[0].resources.wood);Assert.AreEqual(200,post.paidConstruction.wood);
            yield return Press(Key.S);Assert.IsEmpty(envoy.orders);
            var foundation=ScreenPoint(post.position,.8f);AssertWorldPoint(foundation);yield return Click(foundation,MouseButton.Right);
            Assert.AreEqual(OrderKind.Build,envoy.orders.Single().kind);Assert.AreEqual(post.id,envoy.orders.Peek().target);
            bool worked=false;for(int i=0;i<150&&!worked;i++){w.Tick(.1f);worked=envoy.IsWorkingAt(w.Time)&&envoy.work.kind==WorkKind.Build&&envoy.work.target==post.id;}
            Assert.IsTrue(worked);Assert.Greater(post.construction,0);
            yield return Ready();Assert.IsFalse(Actions().Any(s=>s.StartsWith("REPAIR")));
            // An ordinary resource remains a terrain move for an Envoy.
            var berry=SpawnHere("berry",-1,socket.position+new Vector2(7,-6));yield return Ready();
            yield return Click(ScreenPoint(berry.position,.5f),MouseButton.Right);
            Assert.AreEqual(OrderKind.Move,envoy.orders.Single().kind);Assert.AreEqual(0,envoy.orders.Peek().target);
        }

        [UnityTest] public IEnumerator FrenchPaidScoutUsesAutoScoutShiftQueueStopAndActualControlGroupKeys()
        {
            yield return PaidScout("france","native_scout");var scout=Own("native_scout");var w=game.World;
            Assert.AreEqual(25,w.Players[0].spent.wood);Assert.AreEqual(0,w.Players[0].reservedPopulation);
            Assert.IsFalse(Actions().Any(s=>s.StartsWith("BUILD")||s.StartsWith("REPAIR")||s.StartsWith("Envoy")));
            yield return ClickAction("AUTO SCOUT");Assert.AreEqual(OrderKind.Scout,scout.orders.Single().kind);
            var start=scout.position;Tick(3);Assert.Greater(Vector2.Distance(start,scout.position),.5f,"Auto Scout must produce actual travel");
            game.Controls.Focus(scout.position);yield return Ready();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));yield return Ready();
            yield return RightTerrain(scout.position+new Vector2(4,6));
            CollectionAssert.AreEqual(new[]{OrderKind.Scout,OrderKind.Move},scout.orders.Select(o=>o.kind).ToArray());
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();yield return Press(Key.S);Assert.IsEmpty(scout.orders);
            Assert.IsTrue(Action("AUTO SCOUT").interactable,"Keyboard Stop must refresh the scouting action immediately");
            yield return RightTerrain(scout.position+new Vector2(5,4));var first=scout.orders.Peek();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));yield return Ready();yield return ClickAction("AUTO SCOUT");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();
            Assert.AreSame(first,scout.orders.Peek());CollectionAssert.AreEqual(new[]{OrderKind.Move,OrderKind.Scout},scout.orders.Select(o=>o.kind).ToArray());
            yield return RightTerrain(scout.position+new Vector2(-5,4));Assert.AreEqual(OrderKind.Move,scout.orders.Single().kind);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftCtrl,Key.Digit4));InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();
            Assert.AreEqual(1,game.Controls.GroupCount(4));yield return Press(Key.M);Assert.AreNotEqual(scout.id,game.Controls.FirstSelected?.id,"Military cycling excludes the scout role");
            yield return Press(Key.Digit4);Assert.AreEqual(scout.id,game.Controls.FirstSelected?.id);yield return Press(Key.S);Assert.IsEmpty(scout.orders);
        }

        [UnityTest] public IEnumerator StealthButtonHidesFromOrdinarySightAndManualAttackBreaksItWithRealDamage()
        {
            yield return PaidScout("france","native_scout");var scout=Own("native_scout");var w=game.World;
            var enemy=SpawnHere("villager",1,scout.position+Vector2.right*10);
            Assert.IsTrue(w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{enemy.id},order=OrderKind.Hold}).accepted);
            w.Fog.Tick(w.Entities);yield return Ready();Assert.IsTrue(w.IsVisible(1,scout));
            yield return ClickAction("ENTER STEALTH");Assert.IsTrue(scout.stealth);Assert.AreEqual(3,w.Speed(scout),.001f);
            Assert.AreEqual(VisibilityState.Visible,w.Fog.At(1,scout.position));Assert.IsFalse(w.IsVisible(1,scout),"An ordinary worker's map vision is not stealth detection");
            Assert.AreEqual(scout.id,game.Controls.FirstSelected?.id);Assert.IsTrue(w.IsVisible(0,scout));
            yield return Press(Key.S);Assert.IsTrue(scout.stealth,"Stopping alone does not fire a weapon or cancel stealth");
            var point=ScreenPoint(enemy.position,1);AssertWorldPoint(point);yield return Click(point,MouseButton.Right);
            Assert.AreEqual(OrderKind.Attack,scout.orders.Single().kind);Assert.AreEqual(enemy.id,scout.orders.Peek().target);
            Assert.IsFalse(scout.stealth);Assert.IsTrue(w.IsVisible(1,scout));Assert.IsFalse(Action("ENTER STEALTH").interactable,"Attack starts the re-entry cooldown");
            for(int i=0;i<100&&!w.Events.Any(e=>e.kind=="Damage"&&e.entity==scout.id&&e.target==enemy.id);i++)w.Tick(.1f);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Fire"&&e.entity==scout.id&&e.target==enemy.id));
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Damage"&&e.entity==scout.id&&e.target==enemy.id));Assert.Less(enemy.hp,w.MaxHp(enemy));
        }

        [UnityTest] public IEnumerator HiddenEnemyScoutCannotBePickedUntilThePaidDetectorPhysicallyApproaches()
        {
            yield return PaidScout("france","native_scout");var detector=Own("native_scout");var w=game.World;
            foreach(var guard in w.Entities.Where(World.Guardian).ToArray())w.Remove(guard);
            var staging=site+Vector2.left*11;
            Assert.IsTrue(game.Command(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{detector.id},order=OrderKind.Move,point=staging}).accepted);
            for(int i=0;i<1800&&Vector2.Distance(detector.position,staging)>1;i++)w.Tick(.1f);
            Assert.Less(Vector2.Distance(detector.position,staging),1);
            var enemy=SpawnHere("native_scout",1,site);
            Assert.IsTrue(w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{enemy.id},order=OrderKind.Hold}).accepted);
            w.Fog.Tick(w.Entities);game.Controls.Focus(site+Vector2.left*4);yield return Ready();Assert.IsTrue(w.IsVisible(0,enemy));
            var visual=Visual(enemy);Assert.IsNotNull(visual);Assert.IsTrue(visual.gameObject.activeInHierarchy);
            Assert.IsTrue(visual.GetComponentsInChildren<Renderer>().Any(r=>r.enabled));
            Assert.IsTrue(w.Issue(new GameCommand{player=1,kind=CommandKind.ToggleStealth,actors=new[]{enemy.id}}).accepted);
            yield return Ready();Assert.IsTrue(enemy.stealth);Assert.IsFalse(w.IsVisible(0,enemy));Assert.IsFalse(visual.gameObject.activeInHierarchy);
            var hidden=ScreenPoint(enemy.position,1);AssertWorldPoint(hidden);yield return Click(hidden);Assert.AreNotEqual(enemy.id,game.Controls.FirstSelected?.id);
            yield return SelectByPointer(detector);yield return RightTerrain(enemy.position);
            Assert.AreEqual(OrderKind.Move,detector.orders.Single().kind,"Invisible enemy pixels must not synthesize an Attack order");
            for(int i=0;i<300&&!w.IsVisible(0,enemy);i++)w.Tick(.1f);
            Assert.IsTrue(w.IsVisible(0,enemy));Assert.LessOrEqual(Vector2.Distance(detector.position,enemy.position),detector.def.detectionRange+.01f);
            game.Controls.Focus(enemy.position);yield return Ready();Assert.IsTrue(visual.gameObject.activeInHierarchy);
            yield return Click(ScreenPoint(enemy.position,1));Assert.AreEqual(enemy.id,game.Controls.FirstSelected?.id);
        }
    }
}
