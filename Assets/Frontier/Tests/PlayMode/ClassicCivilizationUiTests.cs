using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    public sealed class ClassicCivilizationUiTests : NativeInputFixture
    {
        private IEnumerator PrepareCivilization(string civilization)
        {
            game.PlayerCivilization=civilization;game.AgeLimit=5;game.StartMatch();game.enabled=false;
            game.Controls.Select(Town());yield return Ready();
        }
        private EntityState Town()=>game.World.Entities.Single(e=>e.Alive&&e.owner==0&&e.def.id=="town");
        private int Workers()=>game.World.Entities.Count(e=>e.Alive&&e.owner==0&&e.Worker);
        private void Tick(float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.1f);i++)game.World.Tick(.1f);}
        private bool HasText(string text)=>session.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(text));
        private string[] Actions()=>session.GetComponentsInChildren<Button>().Where(b=>b.transform.parent.name=="Context actions").Select(b=>b.name).ToArray();

        [UnityTest] public IEnumerator ClassicCivilizationsOccupyFirstPageAndOriginalCivsRemainReachable()
        {
            game.ReturnToMenu();yield return Ready();yield return Click(ButtonCenter(FindButton("YOU:")));
            CollectionAssert.AreEqual(new[]{"coast","river","france","ottoman"},game.Rules.civs.Take(4).Select(c=>c.id));
            foreach(var civ in game.Rules.civs.Take(4))Assert.IsTrue(HasText(civ.name.ToUpperInvariant()));
            Assert.IsTrue(FindButton("NEXT 1/2").interactable);yield return Click(ButtonCenter(FindButton("NEXT 1/2")));
            Assert.IsTrue(HasText("WAYFARER CITIES"));Assert.IsTrue(HasText("HIGHLAND GUILD"));Assert.IsFalse(FindButton("NEXT 2/2").interactable);
            yield return Click(ButtonCenter(FindButton("PREVIOUS")));Assert.IsNotNull(FindButton("NEXT 1/2"));
            yield return Click(ButtonCenter(FindButton("FRENCH")));Assert.AreEqual("france",game.PlayerCivilization);
        }
        [UnityTest] public IEnumerator FrenchTownProducesCoureurForItsActualFoodCost()
        {
            yield return PrepareCivilization("france");var worker=game.World.WorkerDefinition(0);Assert.AreEqual("coureur",worker.id);
            Assert.IsTrue(game.World.Entities.Where(e=>e.owner==0&&e.Worker).All(e=>e.def.id=="coureur"));
            Assert.IsFalse(Actions().Any(t=>t.StartsWith(game.Rules.Entity("villager").name+"\n")));
            var button=FindButton(worker.name+"\n");StringAssert.Contains("120F",button.name);float before=game.World.Players[0].resources.food;
            yield return Click(ButtonCenter(button));var job=Town().queue.Single();Assert.AreEqual("coureur",job.item);Assert.AreEqual(120,job.paid.food);Assert.AreEqual(before-120,game.World.Players[0].resources.food);
        }
        [UnityTest] public IEnumerator OttomanTogglePausesAndResumesActualFreeRecruitment()
        {
            yield return PrepareCivilization("ottoman");var town=Town();int initial=Workers();var resources=game.World.Players[0].resources;Tick(5);
            yield return Ready();Assert.IsTrue(HasText("Free settlers:"));Assert.IsTrue(HasText("Workers "+initial+"/"+game.World.WorkerLimit(0)));
            yield return Click(ButtonCenter(FindButton("AUTO SETTLERS: ON")));Assert.IsTrue(town.autoRecruitPaused);float elapsed=town.autoRecruitElapsed;
            Tick(game.World.AutoRecruitSeconds(0)+1);yield return Ready();Assert.AreEqual(initial,Workers());Assert.AreEqual(elapsed,town.autoRecruitElapsed,.001f);Assert.IsTrue(HasText("Paused by player"));
            yield return Click(ButtonCenter(FindButton("AUTO SETTLERS: OFF")));Assert.IsFalse(town.autoRecruitPaused);
            Tick(game.World.AutoRecruitSeconds(0)-elapsed+.2f);yield return Ready();Assert.AreEqual(initial+1,Workers());Assert.IsEmpty(town.queue);
            Assert.AreEqual(resources.food,game.World.Players[0].resources.food);Assert.AreEqual(resources.wood,game.World.Players[0].resources.wood);Assert.AreEqual(resources.coin,game.World.Players[0].resources.coin);
            Assert.AreEqual(1,game.World.Events.Count(e=>e.kind=="AutomaticWorkerProduced"&&e.player==0));
            Assert.IsFalse(Actions().Any(t=>t.StartsWith(game.World.WorkerDefinition(0).name+"\n")),"Automatic recruitment must not expose a misleading paid settler button");
        }
        [UnityTest] public IEnumerator CoureurMeleeControlDoesNotOverlapDetailsOrLoseBuildAndRepair()
        {
            yield return PrepareCivilization("france");var worker=game.World.Entities.First(e=>e.owner==0&&e.Worker);game.Controls.Select(worker);yield return Ready();
            var mode=FindButton("MELEE MODE");var details=FindButton("DETAILS");Assert.Greater(Vector2.Distance(ButtonCenter(mode),ButtonCenter(details)),100);
            Assert.IsNotNull(FindButton("BUILD [B]"));Assert.IsNotNull(FindButton("REPAIR [R]"));yield return Click(ButtonCenter(mode));
            Assert.IsTrue(worker.forceMelee);Assert.IsFalse(game.Hud.ModalVisible);yield return Click(ButtonCenter(FindButton("DETAILS")));Assert.IsTrue(game.Hud.ModalVisible);
        }
        [UnityTest] public IEnumerator OttomanHousingBlockAppearsAndCannotAccumulateHiddenRecruits()
        {
            yield return PrepareCivilization("ottoman");var p=game.World.Players[0];var town=Town();
            while(p.usedPopulation<p.populationCap)SpawnHere("janissary",0,site+Vector2.right*8);
            var count=Workers();Tick(game.World.AutoRecruitSeconds(0)*2);yield return Ready();
            Assert.AreEqual("Need more housing",game.World.AutoRecruitReason(town));Assert.IsTrue(HasText("Need more housing"));Assert.AreEqual(count,Workers());Assert.AreEqual(0,town.autoRecruitElapsed,.001f);
            SpawnHere("house",0,site);Tick(.1f);yield return Ready();Assert.AreEqual("",game.World.AutoRecruitReason(town));Assert.Greater(town.autoRecruitElapsed,0);Assert.AreEqual(count,Workers());
        }
        [UnityTest] public IEnumerator OttomanWorkerLimitIsVisibleDespiteAvailableHousing()
        {
            yield return PrepareCivilization("ottoman");var limit=game.World.WorkerLimit(0);
            for(int i=0;i<3;i++)SpawnHere("house",0,site+new Vector2(i*8,0));
            while(Workers()<limit)SpawnHere("villager",0,site+Vector2.up*9);
            var town=Town();Assert.Greater(game.World.Players[0].populationCap,game.World.Players[0].usedPopulation);Tick(.2f);yield return Ready();
            StringAssert.Contains("limit reached",game.World.AutoRecruitReason(town));Assert.IsTrue(HasText("Workers "+limit+"/"+limit));Assert.IsTrue(HasText(game.World.AutoRecruitReason(town)));
        }
        [UnityTest] public IEnumerator MosqueResearchUpdatesDisplayedAutomaticCycle()
        {
            yield return PrepareCivilization("ottoman");game.World.Players[0].resources=new Cost(5000,5000,5000);var mosque=SpawnHere("mosque",0);
            var technology=game.Rules.techs.First(t=>t.building=="mosque"&&t.autoVillagerSpeed>0&&string.IsNullOrEmpty(t.requires));
            float before=game.World.AutoRecruitSeconds(0);game.Controls.Select(mosque);yield return Ready();yield return Click(ButtonCenter(FindButton("TECHNOLOGIES")));
            var button=FindButton(technology.name+" ·");StringAssert.Contains("automatic settlers",button.name);yield return Click(ButtonCenter(button));
            Assert.AreEqual(technology.id,mosque.queue.Single().item);Tick(technology.time+.2f);Assert.IsTrue(game.World.Players[0].technologies.Contains(technology.id));
            yield return Press(Key.Escape);game.Controls.Select(Town());yield return Ready();var after=game.World.AutoRecruitSeconds(0);Assert.Less(after,before);Assert.IsTrue(HasText("/"+after.ToString("0.0")+"s"));
        }
        [UnityTest] public IEnumerator DutchSkirmisherUsesAgeTwoAndExcludedMusketeerIsAbsent()
        {
            yield return PrepareCivilization("river");game.World.Players[0].age=2;game.World.Players[0].resources=new Cost(5000,5000,5000);
            var barracks=SpawnHere("barracks",0);game.Controls.Select(barracks);yield return Ready();
            var skirmisher=FindButton(game.Rules.Entity("skirmisher").name+"\n");Assert.IsTrue(skirmisher.interactable);Assert.AreEqual(2,game.World.UnitAge(0,game.Rules.Entity("skirmisher")));
            Assert.IsFalse(Actions().Any(t=>t.StartsWith(game.Rules.Entity("musketeer").name+"\n")));yield return Click(ButtonCenter(skirmisher));Assert.AreEqual("skirmisher",barracks.queue.Single().item);
        }
        [UnityTest] public IEnumerator OttomanFoundryIsAvailableInAgeTwoButSpahiHasNoPaidProducer()
        {
            yield return PrepareCivilization("ottoman");game.World.Players[0].age=2;game.World.Players[0].resources=new Cost(5000,5000,5000);
            game.Controls.Select(game.World.Entities.First(e=>e.owner==0&&e.Worker));yield return Ready();yield return Press(Key.B);
            var building=FindButton(game.Rules.Entity("foundry").name+"  ·");Assert.IsTrue(building.interactable);StringAssert.Contains("Age 2",building.name);
            yield return Press(Key.Escape);var foundry=SpawnHere("foundry",0);game.Controls.Select(foundry);yield return Ready();
            var gunner=FindButton("Abus Gunner\n");Assert.IsTrue(gunner.interactable);yield return Click(ButtonCenter(gunner));Assert.AreEqual("abus",foundry.queue.Single().item);
            var stable=SpawnHere("stable",0,site+Vector2.up*9);game.Controls.Select(stable);yield return Ready();Assert.IsFalse(Actions().Any(t=>t.StartsWith("Spahi")));
        }
    }
}
