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
    // Public UI actions travel through actual InputSystem pointer/key events.
    public sealed class CivilizationExpansionUiTests : NativeInputFixture
    {
        private IEnumerator SelectMenuCivilization(CivDef civ,bool opponent=false)
        {
            yield return Click(ButtonCenter(FindButton(opponent?"AI:":"YOU:")));
            int index=System.Array.FindIndex(game.Rules.civs,c=>c.id==civ.id);Assert.GreaterOrEqual(index,0);
            for(int page=0;page<index/4;page++)
            {var next=FindButton("NEXT ");Assert.IsTrue(next.interactable,"Civilization must be reachable by the visible page control");yield return Click(ButtonCenter(next));}
            string panel=opponent?"CHOOSE OPPONENT CIVILIZATION":"CHOOSE YOUR CIVILIZATION";
            var choice=session.GetComponentsInChildren<Button>().Single(b=>b.name.Contains(civ.name.ToUpperInvariant())&&b.transform.parent.name==panel);
            yield return Click(ButtonCenter(choice));
        }
        [UnityTest] public IEnumerator CivilizationMenuSelectsEveryCatalogEntryAndKeepsLegalDecks()
        {
            game.ReturnToMenu();yield return Ready();
            foreach(var civ in game.Rules.civs)
            {
                yield return SelectMenuCivilization(civ);
                Assert.AreEqual(civ.id,game.PlayerCivilization);
                Assert.AreEqual("",DeckLibrary.Validate(game.Rules,civ.id,game.SelectedDeck));
                Assert.IsTrue(FindButton("YOU:").name.Contains(civ.name));
            }
            var opponent=game.Rules.Civ("highland");
            yield return SelectMenuCivilization(opponent,true);
            Assert.AreEqual("highland",game.OpponentCivilization);
        }
        [UnityTest] public IEnumerator TechnologyPresetAndLaterDeckPagesAreReachableWithoutChangingCards()
        {
            game.ReturnToMenu();game.PlayerCivilization="highland";game.DeckPreset="economy";game.ResetDeckPreset();game.Hud.ShowMenu();yield return Ready();
            yield return Click(ButtonCenter(FindButton("PRESET:")));yield return Click(ButtonCenter(FindButton("PRESET:")));
            Assert.AreEqual("technology",game.DeckPreset);Assert.AreEqual(25,game.SelectedDeck.Count);
            var deck=game.SelectedDeck.ToArray();yield return Click(ButtonCenter(FindButton("EDIT DECK")));
            for(int i=0;i<3;i++)yield return Click(ButtonCenter(FindButton("NEXT ")));
            Assert.IsTrue(FindButton("NEXT ").name.StartsWith("NEXT 4/"));CollectionAssert.AreEqual(deck,game.SelectedDeck);
        }
        [UnityTest] public IEnumerator NeutralServicePagesAndEligibilityRefreshWithoutReopening()
        {
            var world=game.World;var state=world.NeutralAllies[0];var def=world.NeutralDefinition(state.id);
            var previousUnits=def.additionalUnits;var previousTechs=def.additionalTechs;
            try
            {
                // A third valid catalog item exercises pagination; configuration is restored afterwards.
                def.additionalUnits=new[]{previousUnits[0],"hussar"};
                def.additionalTechs=new[]{previousTechs[0],world.NeutralDefinition(world.NeutralAllies[1].id).tech};
                var socket=world.Get(state.site);var post=world.Spawn("trade_post",0,socket.position);
                world.Spawn("villager",0,socket.position+new Vector2(2,0));world.Fog.Reveal(0,socket.position,12);
                state.owner=0;state.post=post.id;world.Players[0].age=1;world.Players[0].resources=new Cost(5000,5000,5000);
                game.Controls.Select(socket);yield return Ready();yield return Click(ButtonCenter(FindButton("SETTLEMENT")));
                string first=world.Rules.Entity(def.unit).name;
                Assert.IsFalse(FindButton(first).interactable,"Age I lacks the age II recruit");
                world.Players[0].age=2;
                Assert.AreEqual("",world.NeutralCommandReason(0,state.site,CommandKind.NeutralRecruit,def.unit),"Fixture must satisfy all real command requirements after the age change");
                yield return Ready();
                Assert.IsTrue(FindButton(first).interactable,"Age change refreshes the existing settlement menu");
                yield return Click(ButtonCenter(FindButton("NEXT RECRUIT")));
                Assert.IsNotNull(FindButton(world.Rules.Entity("hussar").name),"Third recruit is accessible");
                yield return Click(ButtonCenter(FindButton("NEXT RESEARCH")));
                Assert.IsNotNull(FindButton(world.Rules.Tech(def.additionalTechs[1]).name),"Third research is accessible");
                world.Remove(post);yield return Ready();
                Assert.IsFalse(FindButton(world.Rules.Entity("hussar").name).interactable,"Post removal refreshes the existing page");
            }
            finally{def.additionalUnits=previousUnits;def.additionalTechs=previousTechs;}
        }
        [UnityTest] public IEnumerator NeutralServiceCountdownIsVisibleOnlyToItsOwningObserver()
        {
            var world=game.World;var state=world.NeutralAllies[0];var definition=world.NeutralDefinition(state.id);var socket=world.Get(state.site);
            // Controlled post ownership/contact: both services themselves are
            // accepted paid commands, not manufactured progress labels.
            world.Players[1].age=5;world.Players[1].resources=new Cost(5000,5000,5000);
            var enemyPost=world.Spawn("trade_post",1,socket.position);
            world.Spawn("villager",1,world.Navigation.NearestWalkable(socket.position+new Vector2(5,0),.4f));
            state.owner=1;state.post=enemyPost.id;world.Fog.Tick(world.Entities);world.Fog.Reveal(0,socket.position,12);
            var enemy=world.Issue(new GameCommand{player=1,kind=CommandKind.NeutralResearch,target=state.site,item=definition.tech});
            Assert.IsTrue(enemy.accepted,enemy.reason);Assert.AreEqual("Research",state.jobs.Single().kind);
            game.Controls.Select(socket);yield return Ready();yield return Click(ButtonCenter(FindButton("SETTLEMENT")));
            string EnemyLabel()=>session.GetComponentsInChildren<Text>().Single(t=>t.text.StartsWith("Treaty:")).text;
            Assert.IsFalse(EnemyLabel().Contains("Service "),"Enemy service existence and countdown must not appear in the modal");
            game.Hud.CloseModal();world.Remove(enemyPost);world.Tick(.1f);Assert.AreEqual(-1,state.owner);
            world.Players[0].age=5;world.Players[0].resources=new Cost(5000,5000,5000);
            var ownPost=world.Spawn("trade_post",0,socket.position);
            world.Spawn("villager",0,world.Navigation.NearestWalkable(socket.position+new Vector2(-5,0),.4f));
            state.owner=0;state.post=ownPost.id;world.Fog.Tick(world.Entities);
            var own=world.Issue(new GameCommand{player=0,kind=CommandKind.NeutralResearch,target=state.site,item=definition.tech});
            Assert.IsTrue(own.accepted,own.reason);Assert.AreEqual("Research",state.jobs.Single().kind);
            game.Controls.Select(socket);yield return Ready();yield return Click(ButtonCenter(FindButton("SETTLEMENT")));
            Assert.IsTrue(EnemyLabel().Contains("Service "),"The owner can see its own paid service countdown");
        }
        [UnityTest] public IEnumerator NewCivilizationRosterHidesReplacedUnits()
        {
            game.World.Players[0].civilization="wayfarer";game.World.Players[0].age=5;
            var barracks=SpawnHere("barracks",0);game.Controls.Select(barracks);yield return Ready();
            var titles=session.GetComponentsInChildren<Button>().Where(b=>b.transform.parent.name=="Context actions").Select(b=>b.name).ToArray();
            Assert.IsTrue(titles.Any(t=>t.StartsWith("Caravan Escort")));Assert.IsTrue(titles.Any(t=>t.StartsWith("Convoy Gunner")));
            Assert.IsFalse(titles.Any(t=>t.StartsWith(game.Rules.Entity("pike").name)));Assert.IsFalse(titles.Any(t=>t.StartsWith(game.Rules.Entity("skirmisher").name)));
        }
        [UnityTest] public IEnumerator ThirdCivilizationAgeChoiceQueuesItsOwnChoiceAndCost()
        {
            var p=game.World.Players[0];p.civilization="highland";p.resources=new Cost(5000,5000,5000);
            var town=game.World.Entities.First(e=>e.Alive&&e.owner==0&&e.def.id=="town");game.Controls.Select(town);yield return Ready();
            yield return Click(ButtonCenter(FindButton("ADVANCE AGE")));var choices=game.World.AgeChoices(0,2);Assert.AreEqual(3,choices.Count);
            yield return Click(ButtonCenter(FindButton(choices[2].name.ToUpperInvariant())));
            var job=town.queue.Single();Assert.AreEqual("Age",job.kind);Assert.AreEqual(choices[2].id,job.choice);
            Assert.AreEqual(5000-game.Rules.Age(2).cost.food,p.resources.food);Assert.IsFalse(game.Hud.ModalVisible);
        }
        [UnityTest] public IEnumerator CaravanRouteButtonStartsTravelWithoutInstantIncome()
        {
            var p=game.World.Players[0];p.civilization="wayfarer";p.age=2;p.resources=new Cost(1000,1000,1000);
            var station=SpawnHere("waystation",0,site+Vector2.up*7);var cart=SpawnHere("caravan",0,site+Vector2.right*6);
            game.Controls.Select(cart);yield return Ready();yield return Click(ButtonCenter(FindButton("TRADE ROUTE")));
            var coin=p.resources.coin;yield return Click(ButtonCenter(FindButton("Waystation #"+station.id)));
            Assert.AreEqual(station.id,cart.routeStation);Assert.AreEqual(1,cart.routeLeg);Assert.AreEqual(coin,p.resources.coin);
            Assert.IsFalse(game.World.Events.Any(e=>e.kind=="CaravanDelivered"&&e.entity==cart.id));Assert.IsFalse(game.Hud.ModalVisible);
        }
        [UnityTest] public IEnumerator DamagedWorkshopDefaultsToManufactureAndExplicitRepairOverridesIt()
        {
            game.World.Players[0].civilization="highland";var workshop=SpawnHere("workshop",0);workshop.hp-=200;
            var worker=SpawnHere("villager",0,site+Vector2.right*6);game.Controls.Select(worker);yield return Ready();
            yield return Click(ScreenPoint(workshop.position),MouseButton.Right);
            Assert.AreEqual(OrderKind.Manufacture,worker.orders.Single().kind);
            yield return Press(Key.R);Assert.IsTrue(game.Controls.RepairMode);
            yield return Click(ScreenPoint(workshop.position));
            Assert.AreEqual(OrderKind.Repair,worker.orders.Single().kind);Assert.AreEqual(workshop.id,worker.orders.Single().target);Assert.IsFalse(game.Controls.RepairMode);
        }
        [UnityTest] public IEnumerator EngineerRightClickRepairsArtilleryAndCannotOpenConstruction()
        {
            game.World.Players[0].civilization="highland";
            var cannon=SpawnHere("culverin",0);cannon.hp-=70;var engineer=SpawnHere("field_engineer",0,site+Vector2.right*6);
            game.Controls.Select(engineer);yield return Ready();yield return Click(ScreenPoint(cannon.position,1),MouseButton.Right);
            Assert.AreEqual(OrderKind.Repair,engineer.orders.Single().kind);Assert.AreEqual(cannon.id,engineer.orders.Single().target);
            Assert.IsFalse(session.GetComponentsInChildren<Button>().Any(b=>b.name=="BUILD [B]"));Assert.IsNotNull(FindButton("REPAIR [R]"));
        }
        [UnityTest] public IEnumerator ManufactureButtonPaysAndUnstaffedQueueCannotComplete()
        {
            var p=game.World.Players[0];p.civilization="highland";p.age=2;p.resources=new Cost(5000,5000,5000);
            var workshop=SpawnHere("workshop",0);game.Controls.Select(workshop);yield return Ready();
            var recipe=game.Rules.manufactureRecipes.Single(r=>r.id=="construction_prefab");
            yield return Click(ButtonCenter(FindButton("MANUFACTURE")));Assert.IsFalse(FindButton(recipe.name+" ·").interactable,"The recipe must stay locked before Standard Jigs");
            yield return Press(Key.Escape);yield return Click(ButtonCenter(FindButton("TECHNOLOGIES")));
            var unlock=game.Rules.Tech(recipe.requires);Assert.IsNotNull(unlock);yield return Click(ButtonCenter(FindButton(unlock.name+" ·")));
            Assert.AreEqual(recipe.requires,workshop.queue.Single().item);
            for(int i=0;i<Mathf.CeilToInt(unlock.time/.1f)+2;i++)game.World.Tick(.1f);
            Assert.IsTrue(p.technologies.Contains(recipe.requires));Assert.IsEmpty(workshop.queue);
            yield return Press(Key.Escape);yield return Click(ButtonCenter(FindButton("MANUFACTURE")));var woodBefore=p.resources.wood;
            yield return Click(ButtonCenter(FindButton(recipe.name+" ·")));
            Assert.AreEqual("Manufacture",workshop.queue.Single().kind);Assert.AreEqual(woodBefore-recipe.cost.wood,p.resources.wood);
            for(int i=0;i<20;i++)game.World.Tick(.1f);
            Assert.AreEqual(0,workshop.queue.Single().elapsed);Assert.AreEqual(0,p.prefabCharges);
        }
        [UnityTest] public IEnumerator ShieldButtonUpdatesStanceAndLocksRepeatedToggleDuringTransition()
        {
            game.World.Players[0].civilization="highland";var guard=SpawnHere("shield_guard",0);game.Controls.Select(guard);yield return Ready();
            yield return Click(ButtonCenter(FindButton("BRACE SHIELD")));Assert.IsTrue(guard.shielded);Assert.Greater(guard.shieldReadyAt,game.World.Time);
            var folded=FindButton("FOLD SHIELD");Assert.IsFalse(folded.interactable);yield return Click(ButtonCenter(folded));Assert.IsTrue(guard.shielded);
            Assert.AreEqual(1,game.World.Events.Count(e=>e.kind=="ShieldStance"&&e.entity==guard.id));
        }
        [UnityTest] public IEnumerator EscapeAndRightClickCancelRepairWithoutIssuingOrders()
        {
            var worker=SpawnHere("villager",0);game.Controls.Select(worker);yield return Ready();
            yield return Press(Key.R);Assert.IsTrue(game.Controls.RepairMode);yield return Press(Key.Escape);Assert.IsFalse(game.Controls.RepairMode);Assert.IsFalse(game.IsPaused);
            yield return Press(Key.R);Assert.IsTrue(game.Controls.RepairMode);yield return new WaitForSecondsRealtime(.25f);
            yield return Click(ScreenPoint(site+Vector2.up*8),MouseButton.Right);Assert.IsFalse(game.Controls.RepairMode);Assert.IsEmpty(worker.orders);
        }
    }
}
