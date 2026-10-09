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
    // Editor integration using actual InputSystem edges and rendered UGUI.
    // Controlled wounded fixtures are not ordinary-opening/native acceptance.
    public sealed class Healing076InputPlayModeTests : NativeInputFixture
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        EntityState healer,patient,soldier;
        string directory;
        [UnitySetUp] public IEnumerator MedicalFixture()
        {
            Assert.IsFalse(game.Settings.Persistent);Assert.IsTrue(game.Progress.MemoryOnly);
            directory=Path.Combine(Path.GetTempPath(),"Frontier-healing-076-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            foreach(string kind in new[]{"save","report","replay"})typeof(GameController).GetField(kind+"DirectoryOverride",Hidden).SetValue(game,Path.Combine(directory,kind));
            game.Controls.ClearTransientInput();
            foreach(var guard in game.World.Entities.Where(World.Guardian).ToArray())game.World.Remove(guard);
            healer=SpawnHere("imam",0,site+new Vector2(-4,0));
            patient=SpawnHere("janissary",0,site+new Vector2(4,0));patient.hp-=80;
            soldier=SpawnHere("pike",0,site+new Vector2(-4,5));
            healer.autoHeal=false;game.Controls.Focus(site);yield return new WaitForSecondsRealtime(.25f);yield return Ready();
        }
        [UnityTearDown] public IEnumerator MedicalStorageCleanup()
        {
            if(game!=null){game.enabled=false;typeof(GameController).GetMethod("FinishReplayFileWrites",Hidden).Invoke(game,null);}
            if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);yield return null;
        }
        Button Action(string prefix)=>session.GetComponentsInChildren<Button>().Single(b=>b.isActiveAndEnabled&&b.transform.parent.name=="Context actions"&&b.name.StartsWith(prefix,StringComparison.Ordinal));
        static Rect Bounds(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(null,p)).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        IEnumerator ClickAction(string prefix)
        {
            for(int i=0;i<9;i++)
            {
                var button=Action(prefix);var viewport=button.GetComponentInParent<ScrollRect>().viewport;var bounds=Bounds(viewport);var p=ButtonCenter(button);
                if(bounds.Contains(p)){Assert.IsTrue(button.interactable,prefix);yield return Click(p);yield break;}
                float direction=p.y<bounds.yMin?1:-1;
                var from=bounds.center-Vector2.up*direction*bounds.height*.31f;var to=bounds.center+Vector2.up*direction*bounds.height*.31f;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=from}.WithButton(MouseButton.Left));yield return Ready();
                InputSystem.QueueStateEvent(mouse,new MouseState{position=to}.WithButton(MouseButton.Left));yield return Ready();
                InputSystem.QueueStateEvent(mouse,new MouseState{position=to});yield return Ready();
            }
            Assert.Fail("Rendered medical action is outside the reachable scroll viewport: "+prefix);
        }
        IEnumerator Select(EntityState entity)
        {
            game.Controls.Focus(site);yield return Ready();var p=ScreenPoint(entity.position,1);AssertWorldPoint(p);yield return Click(p);
            Assert.AreEqual(entity.id,game.Controls.FirstSelected?.id);yield return Ready();
        }
        IEnumerator PatientClick(EntityState target)
        {var p=ScreenPoint(target.position,1);AssertWorldPoint(p);yield return Click(p);}
        void Tick(int steps=1){for(int i=0;i<steps;i++)game.World.Tick(.1f);}
        IEnumerator HealingMode()
        {yield return ClickAction("HEAL");Assert.IsTrue(game.Controls.HealMode);yield return new WaitForSecondsRealtime(.25f);yield return Ready();}

        [UnityTest] public IEnumerator RealMosqueImamButtonPaysFoodReservesPopulationAndQueueButtonRefundsBeforeRealCompletion()
        {
            var w=game.World;w.Players[0].civilization="ottoman";w.Players[0].age=2;w.Players[0].resources=new Cost(1000,1000,1000);
            foreach(var town in w.Entities.Where(e=>e.owner==0&&e.def.id=="town"))town.autoRecruitPaused=true;
            var mosque=SpawnHere("mosque",0,site+new Vector2(-8,-8));yield return Select(mosque);
            var before=w.Players[0].resources;int used=w.Players[0].usedPopulation;yield return ClickAction("Imam\n");
            var job=mosque.queue.Single();Assert.AreEqual("imam",job.item);Assert.AreEqual(115,job.paid.food);Assert.AreEqual(0,job.paid.wood+job.paid.coin);Assert.AreEqual(30,job.duration);
            Assert.AreEqual(before.food-115,w.Players[0].resources.food);Assert.AreEqual(1,w.Players[0].reservedPopulation);Assert.AreEqual(used,w.Players[0].usedPopulation);
            Assert.IsTrue(w.Events.Any(e=>e.kind=="Payment"&&e.entity==mosque.id));
            var queue=session.GetComponentsInChildren<Button>().Single(b=>b.isActiveAndEnabled&&b.transform.parent.name!="Context actions"&&b.name.StartsWith("Imam\n",StringComparison.Ordinal));
            yield return Click(ButtonCenter(queue));Assert.IsEmpty(mosque.queue);Assert.AreEqual(before,w.Players[0].resources);Assert.AreEqual(0,w.Players[0].reservedPopulation);
            yield return ClickAction("Imam\n");Tick(302);yield return Ready();
            Assert.IsEmpty(mosque.queue);Assert.AreEqual(0,w.Players[0].reservedPopulation);Assert.AreEqual(used+1,w.Players[0].usedPopulation);
            var birth=w.Events.Last(e=>e.kind=="UnitTrained"&&e.entity==mosque.id);var produced=w.Get(birth.target);Assert.IsNotNull(produced);Assert.AreEqual("imam",produced.def.id);Assert.IsTrue(produced.Healer);Assert.IsTrue(produced.autoHeal);
        }

        [UnityTest] public IEnumerator RealHealButtonAndWorldPatientClickFilterMixedSelectionWithoutReplacingSoldierOrders()
        {
            yield return Select(soldier);var original=new UnitOrder(OrderKind.Move,site+Vector2.up*9);soldier.orders.Enqueue(original);
            game.Controls.Select(healer,true);yield return Ready();yield return HealingMode();yield return PatientClick(patient);
            Assert.IsFalse(game.Controls.HealMode);Assert.AreEqual(OrderKind.Heal,healer.orders.Single().kind);Assert.AreEqual(patient.id,healer.orders.Peek().target);
            Assert.AreSame(original,soldier.orders.Single(),"Incapable mixed actors retain their exact intent");
            float hp=patient.hp;Tick();Assert.Greater(patient.hp,hp);Assert.IsTrue(healer.IsWorkingAt(game.World.Time));Assert.AreEqual(WorkKind.Heal,healer.work.kind);
            game.Controls.Select(healer);yield return Ready();Assert.IsTrue(session.GetComponentsInChildren<Text>().Any(t=>t.isActiveAndEnabled&&t.text.Contains("Heal 10 HP/s")&&t.text.Contains("Patient:")));
        }

        [UnityTest] public IEnumerator ShiftQueuesTwoPatientsAndStopClearsExplicitAndAutomaticHealing()
        {
            var second=SpawnHere("janissary",0,site+new Vector2(4,5));second.hp-=70;yield return Select(healer);yield return HealingMode();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));yield return Ready();yield return PatientClick(patient);Assert.IsTrue(game.Controls.HealMode);
            yield return PatientClick(second);CollectionAssert.AreEqual(new[]{patient.id,second.id},healer.orders.Select(o=>o.target).ToArray());
            Assert.IsTrue(healer.orders.All(o=>o.kind==OrderKind.Heal));InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Ready();
            healer.autoHeal=true;yield return Press(Key.S);Assert.IsFalse(game.Controls.HealMode);Assert.IsEmpty(healer.orders);Assert.IsFalse(healer.autoHeal);
            float hp=patient.hp;Tick(5);Assert.AreEqual(hp,patient.hp);Assert.AreEqual(0,healer.healingTarget);
        }

        [UnityTest] public IEnumerator InvalidFullEnemyBuildingAndMinimapTargetsKeepModeUntilRightClickOrEscape()
        {
            var full=SpawnHere("janissary",0,site+new Vector2(5,-5));var enemy=SpawnHere("pike",1,site+new Vector2(7,4));enemy.hp-=30;
            var building=SpawnHere("house",0,site+new Vector2(4,10));building.hp-=50;yield return Select(healer);yield return HealingMode();
            foreach(var target in new[]{full,enemy,building}){yield return PatientClick(target);Assert.IsTrue(game.Controls.HealMode);Assert.IsEmpty(healer.orders);}
            var mini=session.GetComponentsInChildren<RawImage>().Single(i=>i.isActiveAndEnabled&&i.name=="Interactive minimap");
            var miniPoint=RectTransformUtility.WorldToScreenPoint(null,mini.rectTransform.TransformPoint(mini.rectTransform.rect.center));
            yield return Click(miniPoint);Assert.IsTrue(game.Controls.HealMode);Assert.IsEmpty(healer.orders);
            yield return Click(ScreenPoint(patient.position,1),MouseButton.Right);Assert.IsFalse(game.Controls.HealMode);Assert.IsEmpty(healer.orders);
            yield return HealingMode();yield return Press(Key.Escape);Assert.IsFalse(game.Controls.HealMode);Assert.IsFalse(game.IsPaused);
        }

        [UnityTest] public IEnumerator RightClickResolvesHealingPerActorAndAutoToggleUsesOnlySelectedMedicalCapabilities()
        {
            yield return Select(soldier);game.Controls.Select(healer,true);yield return Ready();yield return Click(ScreenPoint(patient.position,1),MouseButton.Right);
            Assert.AreEqual(OrderKind.Heal,healer.orders.Single().kind);Assert.AreEqual(patient.id,healer.orders.Peek().target);
            Assert.AreEqual(OrderKind.Move,soldier.orders.Single().kind);Assert.AreEqual(0,soldier.orders.Peek().target);
            yield return ClickAction("AUTO HEAL: OFF");Assert.IsTrue(healer.autoHeal);Assert.IsFalse(soldier.autoHeal);
            Assert.AreEqual(OrderKind.Heal,healer.orders.Single().kind,"Setting automatic care must preserve explicit treatment");
            yield return ClickAction("AUTO HEAL: ON");Assert.IsFalse(healer.autoHeal);Assert.AreEqual(OrderKind.Heal,healer.orders.Single().kind);
        }

        [UnityTest] public IEnumerator MedicalSelectionAndArmyShortcutKeepTheSupportRoleSeparate()
        {
            var priest=SpawnHere("priest",0,site+new Vector2(-2,-4));yield return Select(healer);yield return ClickAction("MEDICAL UNITS");
            CollectionAssert.AreEquivalent(new[]{healer.id,priest.id},game.Controls.Selected);
            yield return Press(Key.M);Assert.IsNotNull(game.Controls.FirstSelected);Assert.IsTrue(game.Controls.FirstSelected.Military);Assert.IsFalse(game.Controls.Selected.Contains(healer.id));
            game.Controls.Select(healer);game.Controls.BeginHeal();Assert.IsTrue(game.Controls.HealMode);game.Controls.ResetSelection();Assert.IsFalse(game.Controls.HealMode);
        }

        [UnityTest] public IEnumerator TransitionBarrierDiscardsBufferedPatientClickAndDeviceResetDoesNotInventAnOrder()
        {
            yield return Select(healer);var p=ScreenPoint(patient.position,1);AssertWorldPoint(p);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Left));InputSystem.QueueStateEvent(mouse,new MouseState{position=p});InputSystem.Update();
            game.Controls.BeginHeal();yield return Ready();Assert.IsEmpty(healer.orders,"Opening targeting discards captured pointer edges");Assert.IsTrue(game.Controls.HealMode);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Left));InputSystem.Update();InputSystem.ResetDevice(mouse);yield return Ready();Assert.IsEmpty(healer.orders);
            yield return new WaitForSecondsRealtime(.25f);yield return PatientClick(patient);Assert.AreEqual(patient.id,healer.orders.Single().target);
        }

        [UnityTest] public IEnumerator AcceptedDelayedTargetCallbackIsBoundToGenerationModeWorldAndMatch()
        {
            yield return Select(healer);game.Controls.BeginHeal();var method=typeof(RtsInput).GetMethod("GuardTargetResult",Hidden);Assert.IsNotNull(method);
            Func<Action<CommandResult>> guard=()=> (Action<CommandResult>)method.Invoke(game.Controls,new object[]{new Action(game.Controls.CancelTargetMode)});
            var old=guard();game.Controls.CancelTargetMode();game.Controls.BeginAttackMove();old(CommandResult.Ok());Assert.IsTrue(game.Controls.AttackMode,"An old healing ACK must not close a newer attack mode");
            game.Controls.BeginHeal();var rejected=guard();rejected(CommandResult.Fail("patient changed"));Assert.IsTrue(game.Controls.HealMode);
            var current=guard();current(CommandResult.Ok());Assert.IsFalse(game.Controls.HealMode);
            game.Controls.BeginHeal();var staleWorld=guard();game.StartMatch();game.enabled=false;staleWorld(CommandResult.Ok());Assert.IsFalse(game.Controls.HealMode);Assert.IsEmpty(game.Controls.Selected);
            yield return Ready();
        }

        [UnityTest] public IEnumerator ActualHealingEventsAloneProduceDedicatedAudioAndVisibilityFilteredBoundedFeedback()
        {
            game.Controls.Select(healer);game.Controls.Focus(site);game.Audio.ResetMatch();
            var command=game.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{healer.id},order=OrderKind.Heal,target=patient.id});Assert.IsTrue(command.accepted,command.reason);
            game.Audio.TickActivity();Assert.AreEqual(0,game.Audio.PlayedCount,"An accepted treatment intent is not actual work");
            var feedback=session.AddComponent<CombatFeedback>();feedback.Bind(game.World,0,session.transform);Tick();
            var ev=game.World.Events.Last(e=>e.kind=="Heal"&&e.entity==healer.id);Assert.IsTrue(ev.hasHealing);Assert.Greater(ev.healing.actual,0);
            game.Audio.OnEvent(ev);Assert.AreEqual(EventAudio.SoundCategory.Heal,game.Audio.LastCategory);Assert.AreEqual(1,game.Audio.PlayedCount);
            game.Audio.TickActivity();Assert.AreEqual(1,game.Audio.PlayedCount,"The activity sampler must not emit a repair hammer for a healer");
            yield return new WaitForSecondsRealtime(.85f);game.Audio.OnEvent(ev);Assert.AreEqual(1,game.Audio.PlayedCount,"Duplicate event delivery stays silent even beyond rate limiting");
            feedback.Tick();Assert.AreEqual(1,feedback.ActiveCount);feedback.Tick();Assert.AreEqual(1,feedback.ActiveCount,"Repeated visual sampling does not duplicate an event");
            Tick(12);feedback.Tick();Assert.LessOrEqual(feedback.ActiveCount,1,"Per-patient heal feedback remains sparse rather than a continuous beam");
            feedback.Dispose();yield return Ready();
        }

        [UnityTest] public IEnumerator ObserverMedicalMetadataIsDetachedAndHiddenOrBoardedPatientsDoNotSupplyTargetIdentifiers()
        {
            var w=game.World;Assert.IsTrue(w.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{healer.id},order=OrderKind.Heal,target=patient.id}).accepted);Tick();yield return Ready();
            var metadata=CaptureMetadata.Read(game);Assert.AreEqual(7,metadata.schemaVersion);var row=metadata.units.Single(e=>e.entity==healer.id);
            Assert.IsTrue(row.healer);Assert.IsFalse(row.autoHeal);Assert.AreEqual(10,row.healRate);Assert.AreEqual(12,row.healRange);Assert.AreEqual(patient.id,row.healingTarget);Assert.IsTrue(row.healingTargetVisible);Assert.AreEqual("Heal",row.work);
            string frozen=JsonUtility.ToJson(metadata);healer.autoHeal=true;healer.healingBlockedUntil=w.Time+3;
            patient.garrisonedIn=w.Entities.First(e=>e.owner==0&&e.def.id=="town").id;
            var hidden=CaptureMetadata.Read(game).units.Single(e=>e.entity==healer.id);Assert.AreEqual(0,hidden.healingTarget);Assert.AreEqual(0,hidden.target);Assert.IsFalse(hidden.healingTargetVisible);Assert.AreEqual("Patient not currently visible",hidden.healingPauseReason);
            Assert.AreEqual(frozen,JsonUtility.ToJson(metadata),"F9 projection stays detached from subsequent simulation state");
            var foreignHealer=SpawnHere("imam",1,site+new Vector2(8,5));var foreignPatient=SpawnHere("native_scout",1,site+new Vector2(12,5));foreignPatient.hp-=20;
            Assert.IsTrue(w.Issue(new GameCommand{player=1,kind=CommandKind.ToggleStealth,actors=new[]{foreignPatient.id}}).accepted);
            Assert.IsTrue(w.Issue(new GameCommand{player=1,kind=CommandKind.Order,actors=new[]{foreignHealer.id},order=OrderKind.Heal,target=foreignPatient.id}).accepted);
            Assert.IsTrue(w.IsVisible(0,foreignHealer));Assert.IsFalse(w.IsVisible(0,foreignPatient));Assert.IsFalse(WorldView.CanShowMedicalActivity076(w,0,foreignHealer));Assert.IsTrue(WorldView.CanShowMedicalActivity076(w,1,foreignHealer));
            Assert.IsFalse(CaptureMetadata.Read(game).units.Any(e=>e.entity==foreignHealer.id||e.entity==foreignPatient.id));
        }
    }

    public sealed class Healing076PresentationPlayModeTests
    {
        GameObject stage;RuleCatalog rules;
        [SetUp] public void Setup(){stage=new GameObject("Original medical geometry and pose fixture");rules=Catalog.Load();}
        [TearDown] public void Cleanup(){if(stage!=null)UnityEngine.Object.DestroyImmediate(stage);}
        static Transform Named(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name==name);
        EntityState Entity(string id)=>new EntityState{id=42,owner=0,def=rules.Entity(id),hp=100,construction=1,fireAt=-1};

        [TestCase("imam","Imam long kaftan","Imam felt cap")]
        [TestCase("priest","Priest full cassock","Priest square biretta")]
        public void OriginalHealerSilhouettesHaveIndependentBatchedBookAndNoInfantryOrWorkerEquipment(string id,string robe,string cap)
        {
            var actor=Entity(id);var rig=ProceduralArt.BuildEntity(actor,stage.transform).GetComponent<UnitRig>();
            Assert.IsTrue(rig.Unarmed);Assert.IsNotNull(rig.HealerBody);Assert.IsNotNull(rig.HealerBook);Assert.IsNull(rig.WorkerEquipment);Assert.IsNull(rig.Weapon);
            Assert.IsNotNull(Named(rig.transform,robe));Assert.IsNotNull(Named(rig.transform,cap));Assert.IsNotNull(rig.HealerBody.Find("Batched rig surface"));Assert.IsNotNull(rig.HealerBook.Find("Batched rig surface"));
            Assert.IsTrue(rig.HealerBook.IsChildOf(rig.LeftHand));foreach(string name in new[]{"Musket","Hammer","Axe","Pick","Basket","Tailored coat"})Assert.IsNull(Named(rig.transform,name));
            var renderers=rig.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();Assert.IsNotEmpty(renderers);Assert.LessOrEqual(renderers.Length,16);
            foreach(var renderer in renderers){Assert.AreEqual("Batched rig surface",renderer.name);var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;Assert.AreEqual(mesh.vertexCount,mesh.uv.Length);Assert.IsTrue(mesh.vertices.All(v=>!float.IsNaN(v.sqrMagnitude)&&!float.IsInfinity(v.sqrMagnitude)));}
        }

        [Test] public void OnlyActualHealingWorkUsesOpenHandPoseAndPoolingRestoresBookAndTorsoWithoutStateWrites()
        {
            var actor=Entity("imam");var patient=Entity("janissary");patient.id=43;actor.healingTarget=patient.id;
            var rig=ProceduralArt.BuildEntity(actor,stage.transform).GetComponent<UnitRig>();var bodyPosition=rig.HealerBody.localPosition;var bookPosition=rig.HealerBook.localPosition;var bookScale=rig.HealerBook.localScale;
            actor.orders.Enqueue(new UnitOrder(OrderKind.Heal,new Vector2(4,0),patient.id));Assert.AreEqual(UnitPoseState.Idle,UnitAnimation.Evaluate(actor,2).state,"An order without positive work is not the treatment pose");
            actor.work=new WorkActivity{kind=WorkKind.Heal,target=patient.id,time=2};string before=JsonUtility.ToJson(actor);var pose=UnitAnimation.Evaluate(actor,2,workTarget:patient);
            Assert.AreEqual(UnitPoseState.Heal,pose.state);Assert.AreEqual(WorkerTool.None,pose.tool);UnitAnimation.Apply(rig,pose);var hand=rig.RightHand.position;
            UnitAnimation.Apply(rig,new AnimationPose{state=UnitPoseState.Heal,actionPhase=.7f});Assert.Greater(Vector3.Distance(hand,rig.RightHand.position),.005f);
            UnitAnimation.Apply(rig,new AnimationPose{state=UnitPoseState.Idle});Assert.AreEqual(bodyPosition,rig.HealerBody.localPosition);Assert.AreEqual(bookPosition,rig.HealerBook.localPosition);Assert.AreEqual(bookScale,rig.HealerBook.localScale);
            rig.RestoreInitialPose();Assert.AreEqual(Quaternion.identity,rig.HealerBook.localRotation);Assert.AreEqual(before,JsonUtility.ToJson(actor));
            Assert.AreEqual(UnitPoseState.Idle,UnitAnimation.Evaluate(actor,2.1f).state,"Stale work does not loop the heal gesture");
        }

        [TestCase("coast")][TestCase("river")][TestCase("france")]
        public void ChurchHasOriginalNaveBellSpireCrossAndActualConstructionVisibility(string civilization)
        {
            var church=Entity("church");var root=ProceduralArt.BuildEntity(church,stage.transform,civilization,2);var model=root.GetComponent<ArchitectureModel>();Assert.IsNotNull(model);
            Assert.IsNotNull(Named(root,"Church octagonal bell spire"));Assert.IsNotNull(Named(root,"Church spire cross"));Assert.Greater(model.LocalBounds.size.y,4.7f);
            Assert.IsTrue(model.Roofs.gameObject.activeSelf);model.SetConstruction(.15f);Assert.IsFalse(model.Roofs.gameObject.activeSelf);Assert.IsTrue(model.Scaffold.gameObject.activeSelf);model.SetConstruction(1);Assert.IsTrue(model.Roofs.gameObject.activeSelf);Assert.IsFalse(model.Scaffold.gameObject.activeSelf);
        }
    }
}
